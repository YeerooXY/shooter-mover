using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Rewards.Generation
{
    public sealed partial class RewardGenerationServiceV1
    {
        private static EquipmentGenerationResultV1 BuildEquipmentFailure(
            EquipmentGenerationRequestV1 request,
            string contentFingerprint,
            TraceAccumulator trace,
            string failureReason,
            RewardGenerationStatusV1 status = RewardGenerationStatusV1.ImpossiblePolicy)
        {
            string resultFingerprint = RewardGenerationFingerprintV1.Compute(
                "schema=equipment-generation-failure-v1\nstatus="
                + ((int)status).ToString(CultureInfo.InvariantCulture)
                + "\nrequest="
                + request.ToCanonicalString()
                + "\nreason="
                + failureReason);
            RewardGenerationTraceV1 finalTrace = trace.Build(
                request.AlgorithmVersion,
                request.RootSeed,
                contentFingerprint,
                request.Context.Fingerprint,
                resultFingerprint);
            return new EquipmentGenerationResultV1(
                status,
                null,
                finalTrace,
                contentFingerprint,
                request.Context.Fingerprint,
                resultFingerprint,
                failureReason);
        }

        private static List<WeightedAugmentCandidate> BuildEligibleAugments(
            EquipmentGenerationRequestV1 request,
            EquipmentDefinition equipmentDefinition,
            int itemLevel,
            StableId qualityId,
            List<AugmentInstance> existing,
            int slotIndex,
            TraceAccumulator trace)
        {
            List<WeightedAugmentCandidate> eligible = new List<WeightedAugmentCandidate>();
            for (int index = 0; index < request.Policy.AugmentCandidates.Count; index++)
            {
                AugmentGenerationCandidateV1 candidate = request.Policy.AugmentCandidates[index];
                AugmentDefinition definition = request.Catalog.FindAugmentDefinition(candidate.AugmentDefinitionId);
                bool allowed = candidate.IsLevelEligible(request.Context)
                    && definition != null
                    && definition.Compatibility != null
                    && definition.Compatibility.Allows(equipmentDefinition)
                    && definition.TierRange != null
                    && definition.TierRange.IsOrderedPositive
                    && definition.LevelRange != null
                    && definition.LevelRange.IsOrderedPositive;
                if (allowed)
                {
                    StableId provisionalId = RewardGenerationFingerprintV1.DeriveStableId(
                        "augment-provisional",
                        request.OperationId.ToString(),
                        slotIndex.ToString(CultureInfo.InvariantCulture),
                        candidate.AugmentDefinitionId.ToString());
                    List<AugmentInstance> provisionalAugments = new List<AugmentInstance>(existing);
                    provisionalAugments.Add(AugmentInstance.Create(
                        provisionalId,
                        definition.DefinitionId,
                        definition.TierRange.Minimum,
                        definition.LevelRange.Minimum));
                    EquipmentInstance provisional = EquipmentInstance.Create(
                        request.EquipmentInstanceId,
                        equipmentDefinition.DefinitionId,
                        itemLevel,
                        qualityId,
                        provisionalAugments);
                    allowed = request.Catalog.ValidateInstance(provisional).IsValid;
                }

                trace.Add(
                    StepEligibility,
                    candidate.AugmentDefinitionId,
                    RewardGenerationTraceDecisionV1.Eligibility,
                    null,
                    0UL,
                    0UL,
                    allowed ? 1L : 0L,
                    allowed ? checked((long)candidate.Weight) : 0L,
                    "augment-candidate;slot=" + slotIndex.ToString(CultureInfo.InvariantCulture));
                if (allowed)
                {
                    eligible.Add(new WeightedAugmentCandidate(candidate, definition, candidate.Weight));
                }
            }

            return eligible;
        }

        private static string BuildEquipmentContentFingerprint(EquipmentGenerationRequestV1 request)
        {
            return RewardGenerationFingerprintV1.Compute(
                "schema=equipment-generation-content-v1\npolicy:\n"
                + request.Policy.ToCanonicalString()
                + "\ncatalog:\n"
                + request.Catalog.CanonicalText);
        }

        private static bool TryScaleWeight(double weight, out ulong scaled, out string failure)
        {
            if (double.IsNaN(weight) || double.IsInfinity(weight) || weight <= 0.0)
            {
                scaled = 0UL;
                failure = "non-positive-or-non-finite-weight";
                return false;
            }

            double scaledDouble = Math.Floor((weight * 1000000.0) + 0.5);
            if (double.IsNaN(scaledDouble) || double.IsInfinity(scaledDouble) || scaledDouble > long.MaxValue)
            {
                scaled = 0UL;
                failure = "weight-exceeds-supported-trace-domain";
                return false;
            }

            scaled = scaledDouble < 1.0 ? 1UL : (ulong)scaledDouble;
            failure = string.Empty;
            return true;
        }

        private static bool TrySelectWeighted<T>(
            DeterministicRandom root,
            StableId purpose,
            ulong ordinal,
            IReadOnlyList<T> candidates,
            Func<T, ulong> weightSelector,
            out T selected,
            out DeterministicRandom stream,
            out ulong sample,
            out string failure)
        {
            ulong total = 0UL;
            try
            {
                for (int index = 0; index < candidates.Count; index++)
                {
                    total = checked(total + weightSelector(candidates[index]));
                }
            }
            catch (OverflowException)
            {
                selected = default(T);
                stream = default(DeterministicRandom);
                sample = 0UL;
                failure = "weighted-selection-overflow";
                return false;
            }

            if (total == 0UL || total > long.MaxValue)
            {
                selected = default(T);
                stream = default(DeterministicRandom);
                sample = 0UL;
                failure = "weighted-selection-total-out-of-range";
                return false;
            }

            stream = root.Fork(purpose, ordinal);
            stream = stream.NextBoundedUInt64(total, out sample);
            ulong cursor = sample;
            for (int index = 0; index < candidates.Count; index++)
            {
                ulong weight = weightSelector(candidates[index]);
                if (cursor < weight)
                {
                    selected = candidates[index];
                    failure = string.Empty;
                    return true;
                }

                cursor -= weight;
            }

            selected = default(T);
            failure = "weighted-selection-sample-not-resolved";
            return false;
        }

        private static DeterministicRandom NextInclusiveInt32(
            DeterministicRandom stream,
            int minimum,
            int maximum,
            out int value)
        {
            if (minimum < 0 || maximum < minimum)
            {
                throw new ArgumentOutOfRangeException(nameof(minimum));
            }

            ulong width = (ulong)((long)maximum - minimum) + 1UL;
            ulong offset;
            stream = stream.NextBoundedUInt64(width, out offset);
            value = checked(minimum + (int)offset);
            return stream;
        }

        private static DeterministicRandom NextInclusiveInt64(
            DeterministicRandom stream,
            long minimum,
            long maximum,
            out long value)
        {
            if (minimum < 1L || maximum < minimum)
            {
                throw new ArgumentOutOfRangeException(nameof(minimum));
            }

            ulong width = (ulong)(maximum - minimum) + 1UL;
            ulong offset;
            stream = stream.NextBoundedUInt64(width, out offset);
            value = checked(minimum + (long)offset);
            return stream;
        }

        private static string FormatIssues(IReadOnlyList<EquipmentModelIssue> issues)
        {
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < issues.Count; index++)
            {
                if (index > 0) { builder.Append(';'); }
                builder.Append(issues[index]);
            }

            return builder.ToString();
        }

        private sealed class WeightedEquipmentCandidate
        {
            public WeightedEquipmentCandidate(EquipmentGenerationCandidateV1 candidate, ulong weight)
            {
                Candidate = candidate;
                Weight = weight;
            }

            public EquipmentGenerationCandidateV1 Candidate { get; }
            public ulong Weight { get; }
        }

        private sealed class WeightedQualityCandidate
        {
            public WeightedQualityCandidate(EquipmentQualityCandidateV1 candidate, ulong weight)
            {
                Candidate = candidate;
                Weight = weight;
            }

            public EquipmentQualityCandidateV1 Candidate { get; }
            public ulong Weight { get; }
        }

        private sealed class WeightedAugmentCandidate
        {
            public WeightedAugmentCandidate(
                AugmentGenerationCandidateV1 candidate,
                AugmentDefinition definition,
                ulong weight)
            {
                Candidate = candidate;
                Definition = definition;
                Weight = weight;
            }

            public AugmentGenerationCandidateV1 Candidate { get; }
            public AugmentDefinition Definition { get; }
            public ulong Weight { get; }
        }
    }
}
