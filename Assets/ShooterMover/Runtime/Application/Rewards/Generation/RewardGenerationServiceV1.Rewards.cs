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
        public RewardGenerationResultEnvelopeV1 GenerateReward(RewardGenerationRequestV1 request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            string contentFingerprint = BuildRewardContentFingerprint(request);
            TraceAccumulator trace = new TraceAccumulator();
            List<RewardTraceEntryV1> contractTraceEntries = new List<RewardTraceEntryV1>();
            List<RewardGrantV1> grants = new List<RewardGrantV1>();
            DeterministicRandom root = DeterministicRandom.Create(request.RootSeed, request.AlgorithmVersion);

            if (request.Profile.Disposition == RewardProfileDispositionV1.ExplicitNoDrop)
            {
                AddRewardDecision(
                    trace,
                    contractTraceEntries,
                    request.Operation.SourceOperationStableId,
                    StepResult,
                    request.Profile.ProfileStableId,
                    RewardGenerationTraceDecisionV1.ExplicitNoDrop,
                    RewardTraceDecisionKindV1.ExplicitNoDrop,
                    null,
                    0UL,
                    0UL,
                    0L,
                    0L,
                    "profile-explicit-no-drop");
                RewardResultV1 noDrop = RewardResultV1.CreateExplicitNoDrop(
                    request.Operation.CommitmentStableId,
                    request.Operation.SourceOperationStableId);
                return BuildRewardSuccess(
                    RewardGenerationStatusV1.ExplicitNoDrop,
                    noDrop,
                    request,
                    contentFingerprint,
                    trace,
                    contractTraceEntries);
            }

            for (int index = 0; index < request.Profile.GuaranteedEntries.Count; index++)
            {
                RewardGrantSpecificationV1 specification = request.Profile.GuaranteedEntries[index];
                AddRewardDecision(
                    trace,
                    contractTraceEntries,
                    request.Operation.SourceOperationStableId,
                    StepSelection,
                    specification.GrantStableId,
                    RewardGenerationTraceDecisionV1.WeightedSelection,
                    RewardTraceDecisionKindV1.Guaranteed,
                    null,
                    0UL,
                    0UL,
                    1L,
                    1L,
                    "guaranteed");
                string failure;
                if (!TryProduceGrant(request, root, specification, trace, contractTraceEntries, grants, out failure))
                {
                    return BuildRewardFailure(request, contentFingerprint, trace, failure);
                }
            }

            for (int index = 0; index < request.Profile.IndependentRolls.Count; index++)
            {
                IndependentRewardRollV1 roll = request.Profile.IndependentRolls[index];
                ulong streamOrdinal = RewardGenerationFingerprintV1.StableOrdinal(roll.RollStableId);
                DeterministicRandom stream = root.Fork(PurposeRewardIndependent, streamOrdinal);
                bool accepted;
                stream = stream.NextChance(
                    (ulong)roll.ProbabilityMillionths,
                    (ulong)IndependentRewardRollV1.ProbabilityScale,
                    out accepted);
                AddRewardDecision(
                    trace,
                    contractTraceEntries,
                    request.Operation.SourceOperationStableId,
                    StepSelection,
                    roll.RollStableId,
                    RewardGenerationTraceDecisionV1.IndependentChance,
                    RewardTraceDecisionKindV1.IndependentChance,
                    PurposeRewardIndependent,
                    streamOrdinal,
                    stream.SamplesConsumed,
                    roll.ProbabilityMillionths,
                    accepted ? 1L : 0L,
                    "probability-millionths");
                if (accepted)
                {
                    string failure;
                    if (!TryProduceGrant(request, root, roll.Grant, trace, contractTraceEntries, grants, out failure))
                    {
                        return BuildRewardFailure(request, contentFingerprint, trace, failure);
                    }
                }
            }

            for (int groupIndex = 0; groupIndex < request.Profile.ExclusiveGroups.Count; groupIndex++)
            {
                ExclusiveRewardGroupV1 group = request.Profile.ExclusiveGroups[groupIndex];
                ulong totalWeight;
                string weightFailure;
                if (!TrySumRewardWeights(group.Outcomes, out totalWeight, out weightFailure))
                {
                    return BuildRewardFailure(request, contentFingerprint, trace, weightFailure);
                }

                ulong streamOrdinal = RewardGenerationFingerprintV1.StableOrdinal(group.GroupStableId);
                DeterministicRandom stream = root.Fork(PurposeRewardExclusive, streamOrdinal);
                ulong sample;
                stream = stream.NextBoundedUInt64(totalWeight, out sample);
                WeightedRewardOutcomeV1 selected = SelectRewardOutcome(group.Outcomes, sample);
                AddRewardDecision(
                    trace,
                    contractTraceEntries,
                    request.Operation.SourceOperationStableId,
                    StepSelection,
                    group.GroupStableId,
                    RewardGenerationTraceDecisionV1.ExclusiveSelection,
                    RewardTraceDecisionKindV1.ExclusiveSelection,
                    PurposeRewardExclusive,
                    streamOrdinal,
                    stream.SamplesConsumed,
                    checked((long)totalWeight),
                    checked((long)sample),
                    "selected=" + selected.OutcomeStableId);
                if (selected.Kind == WeightedRewardOutcomeKindV1.ExplicitNoDrop)
                {
                    AddRewardDecision(
                        trace,
                        contractTraceEntries,
                        request.Operation.SourceOperationStableId,
                        StepResult,
                        selected.OutcomeStableId,
                        RewardGenerationTraceDecisionV1.ExplicitNoDrop,
                        RewardTraceDecisionKindV1.ExplicitNoDrop,
                        null,
                        0UL,
                        0UL,
                        0L,
                        0L,
                        "exclusive-explicit-no-drop");
                }
                else
                {
                    string failure;
                    if (!TryProduceGrant(request, root, selected.Grant, trace, contractTraceEntries, grants, out failure))
                    {
                        return BuildRewardFailure(request, contentFingerprint, trace, failure);
                    }
                }
            }

            RewardResultV1 result = grants.Count == 0
                ? RewardResultV1.CreateExplicitNoDrop(
                    request.Operation.CommitmentStableId,
                    request.Operation.SourceOperationStableId)
                : RewardResultV1.CreateGrants(
                    request.Operation.CommitmentStableId,
                    request.Operation.SourceOperationStableId,
                    grants);
            RewardGenerationStatusV1 status = grants.Count == 0
                ? RewardGenerationStatusV1.ExplicitNoDrop
                : RewardGenerationStatusV1.Generated;
            if (grants.Count == 0)
            {
                AddRewardDecision(
                    trace,
                    contractTraceEntries,
                    request.Operation.SourceOperationStableId,
                    StepResult,
                    request.Profile.ProfileStableId,
                    RewardGenerationTraceDecisionV1.ExplicitNoDrop,
                    RewardTraceDecisionKindV1.ExplicitNoDrop,
                    null,
                    0UL,
                    0UL,
                    0L,
                    0L,
                    "all-optional-decisions-produced-no-grant");
            }

            return BuildRewardSuccess(
                status,
                result,
                request,
                contentFingerprint,
                trace,
                contractTraceEntries);
        }

        private static bool TryProduceGrant(
            RewardGenerationRequestV1 request,
            DeterministicRandom root,
            RewardGrantSpecificationV1 specification,
            TraceAccumulator trace,
            List<RewardTraceEntryV1> contractTraceEntries,
            List<RewardGrantV1> grants,
            out string failure)
        {
            ulong streamOrdinal = RewardGenerationFingerprintV1.StableOrdinal(specification.GrantStableId);
            DeterministicRandom stream = root.Fork(PurposeRewardQuantity, streamOrdinal);
            long quantity;
            stream = NextInclusiveInt64(
                stream,
                specification.Quantity.Minimum,
                specification.Quantity.Maximum,
                out quantity);
            AddRewardDecision(
                trace,
                contractTraceEntries,
                request.Operation.SourceOperationStableId,
                StepQuantity,
                specification.GrantStableId,
                RewardGenerationTraceDecisionV1.Quantity,
                RewardTraceDecisionKindV1.Quantity,
                PurposeRewardQuantity,
                streamOrdinal,
                stream.SamplesConsumed,
                specification.Quantity.Maximum,
                quantity,
                "minimum=" + specification.Quantity.Minimum.ToString(CultureInfo.InvariantCulture));

            try
            {
                for (int index = 0; index < specification.ScalingInputs.Count; index++)
                {
                    RewardScalingInputDescriptorV1 descriptor = specification.ScalingInputs[index];
                    long value;
                    if (!TryResolveScalingValue(request, descriptor, out value))
                    {
                        failure = "missing-explicit-scaling-value:" + descriptor.InputStableId;
                        return false;
                    }

                    quantity = checked(quantity + value);
                    AddRewardDecision(
                        trace,
                        contractTraceEntries,
                        request.Operation.SourceOperationStableId,
                        StepScaling,
                        descriptor.InputStableId,
                        RewardGenerationTraceDecisionV1.ScalingInput,
                        RewardTraceDecisionKindV1.ScalingInput,
                        null,
                        0UL,
                        0UL,
                        value,
                        quantity,
                        "additive-scaling-kind=" + ((int)descriptor.Kind).ToString(CultureInfo.InvariantCulture));
                }
            }
            catch (OverflowException)
            {
                failure = "reward-quantity-overflow:" + specification.GrantStableId;
                return false;
            }

            RewardGrantV1 grant = RewardGrantV1.Create(
                specification.GrantStableId,
                specification.Kind,
                specification.ContentStableId,
                quantity);
            grants.Add(grant);
            AddRewardDecision(
                trace,
                contractTraceEntries,
                request.Operation.SourceOperationStableId,
                StepResult,
                specification.GrantStableId,
                RewardGenerationTraceDecisionV1.GrantProduced,
                RewardTraceDecisionKindV1.GrantProduced,
                null,
                0UL,
                0UL,
                quantity,
                quantity,
                "grant-fingerprint=" + grant.Fingerprint);
            failure = string.Empty;
            return true;
        }

        private static bool TryResolveScalingValue(
            RewardGenerationRequestV1 request,
            RewardScalingInputDescriptorV1 descriptor,
            out long value)
        {
            switch (descriptor.Kind)
            {
                case RewardScalingInputKindV1.CharacterLevel:
                    value = request.Context.CharacterLevel;
                    return true;
                case RewardScalingInputKindV1.RegionLevel:
                    value = request.Context.RegionLevel;
                    return true;
                case RewardScalingInputKindV1.Difficulty:
                    value = request.Context.DifficultyValue;
                    return true;
                case RewardScalingInputKindV1.SourceTier:
                case RewardScalingInputKindV1.Custom:
                    return request.TryGetScalingValue(descriptor.InputStableId, out value);
                default:
                    value = 0L;
                    return false;
            }
        }

        private static RewardGenerationResultEnvelopeV1 BuildRewardSuccess(
            RewardGenerationStatusV1 status,
            RewardResultV1 result,
            RewardGenerationRequestV1 request,
            string contentFingerprint,
            TraceAccumulator trace,
            List<RewardTraceEntryV1> contractEntries)
        {
            RewardTraceV1 rewardTrace = RewardTraceV1.Create(
                request.Operation.SourceOperationStableId,
                contractEntries);
            RewardGenerationTraceV1 generationTrace = trace.Build(
                request.AlgorithmVersion,
                request.RootSeed,
                contentFingerprint,
                request.Context.Fingerprint,
                result.Fingerprint);
            return new RewardGenerationResultEnvelopeV1(
                status,
                result,
                rewardTrace,
                generationTrace,
                contentFingerprint,
                request.Context.Fingerprint,
                result.Fingerprint,
                string.Empty);
        }

        private static RewardGenerationResultEnvelopeV1 BuildRewardFailure(
            RewardGenerationRequestV1 request,
            string contentFingerprint,
            TraceAccumulator trace,
            string failureReason)
        {
            string resultFingerprint = RewardGenerationFingerprintV1.Compute(
                "schema=reward-generation-failure-v1\nstatus=impossible-policy\nrequest="
                + request.ToCanonicalString()
                + "\nreason="
                + failureReason);
            RewardGenerationTraceV1 generationTrace = trace.Build(
                request.AlgorithmVersion,
                request.RootSeed,
                contentFingerprint,
                request.Context.Fingerprint,
                resultFingerprint);
            return new RewardGenerationResultEnvelopeV1(
                RewardGenerationStatusV1.ImpossiblePolicy,
                null,
                null,
                generationTrace,
                contentFingerprint,
                request.Context.Fingerprint,
                resultFingerprint,
                failureReason);
        }

        private static void AddRewardDecision(
            TraceAccumulator trace,
            List<RewardTraceEntryV1> contractEntries,
            StableId operationId,
            StableId stepId,
            StableId subjectId,
            RewardGenerationTraceDecisionV1 detailedDecision,
            RewardTraceDecisionKindV1 contractDecision,
            StableId purposeId,
            ulong substreamOrdinal,
            ulong samplesConsumed,
            long input,
            long output,
            string detail)
        {
            int ordinal = trace.Count;
            trace.Add(
                stepId,
                subjectId,
                detailedDecision,
                purposeId,
                substreamOrdinal,
                samplesConsumed,
                input,
                output,
                detail);
            StableId entryId = RewardGenerationFingerprintV1.DeriveStableId(
                "reward-trace",
                operationId.ToString(),
                ordinal.ToString(CultureInfo.InvariantCulture));
            contractEntries.Add(RewardTraceEntryV1.Create(
                entryId,
                ordinal,
                stepId,
                subjectId,
                contractDecision,
                input,
                output));
        }

        private static string BuildRewardContentFingerprint(RewardGenerationRequestV1 request)
        {
            return RewardGenerationFingerprintV1.Compute(
                "schema=reward-generation-content-v1\noperation_content_fingerprint="
                + request.Operation.ContentFingerprint
                + "\nprofile_fingerprint="
                + request.Profile.Fingerprint
                + "\nrequest:\n"
                + request.ToCanonicalString());
        }

        private static bool TrySumRewardWeights(
            IReadOnlyList<WeightedRewardOutcomeV1> outcomes,
            out ulong total,
            out string failure)
        {
            total = 0UL;
            try
            {
                for (int index = 0; index < outcomes.Count; index++)
                {
                    total = checked(total + (ulong)outcomes[index].Weight);
                }
            }
            catch (OverflowException)
            {
                failure = "exclusive-group-weight-overflow";
                return false;
            }

            if (total > long.MaxValue)
            {
                failure = "exclusive-group-weight-exceeds-trace-domain";
                return false;
            }

            failure = string.Empty;
            return total > 0UL;
        }

        private static WeightedRewardOutcomeV1 SelectRewardOutcome(
            IReadOnlyList<WeightedRewardOutcomeV1> outcomes,
            ulong sample)
        {
            ulong cursor = sample;
            for (int index = 0; index < outcomes.Count; index++)
            {
                ulong weight = (ulong)outcomes[index].Weight;
                if (cursor < weight)
                {
                    return outcomes[index];
                }

                cursor -= weight;
            }

            throw new InvalidOperationException("Weighted outcome sample exceeded the validated total.");
        }
    }
}
