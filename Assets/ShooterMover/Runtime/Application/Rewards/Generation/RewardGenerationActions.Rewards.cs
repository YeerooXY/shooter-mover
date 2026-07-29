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
    public sealed partial class RewardGenerationActions
    {
        public RewardGenerationResultEnvelope GenerateReward(RewardGenerationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            string contentFingerprint = BuildRewardContentFingerprint(request);
            TraceAccumulator trace = new TraceAccumulator();
            List<RewardTraceEntry> contractTraceEntries = new List<RewardTraceEntry>();
            List<RewardGrant> grants = new List<RewardGrant>();
            DeterministicRandom root = DeterministicRandom.Create(request.RootSeed, request.AlgorithmVersion);

            if (request.Profile.Disposition == RewardProfileDisposition.ExplicitNoDrop)
            {
                AddRewardDecision(
                    trace,
                    contractTraceEntries,
                    request.Operation.SourceOperationStableId,
                    StepResult,
                    request.Profile.ProfileStableId,
                    RewardGenerationTraceDecision.ExplicitNoDrop,
                    RewardTraceDecisionKind.ExplicitNoDrop,
                    null,
                    0UL,
                    0UL,
                    0L,
                    0L,
                    "profile-explicit-no-drop");
                RewardResult noDrop = RewardResult.CreateExplicitNoDrop(
                    request.Operation.CommitmentStableId,
                    request.Operation.SourceOperationStableId);
                return BuildRewardSuccess(
                    RewardGenerationStatus.ExplicitNoDrop,
                    noDrop,
                    request,
                    contentFingerprint,
                    trace,
                    contractTraceEntries);
            }

            for (int index = 0; index < request.Profile.GuaranteedEntries.Count; index++)
            {
                RewardGrantSpecification specification = request.Profile.GuaranteedEntries[index];
                AddRewardDecision(
                    trace,
                    contractTraceEntries,
                    request.Operation.SourceOperationStableId,
                    StepSelection,
                    specification.GrantStableId,
                    RewardGenerationTraceDecision.WeightedSelection,
                    RewardTraceDecisionKind.Guaranteed,
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
                IndependentRewardRoll roll = request.Profile.IndependentRolls[index];
                ulong streamOrdinal = RewardGenerationFingerprint.StableOrdinal(roll.RollStableId);
                DeterministicRandom stream = root.Fork(PurposeRewardIndependent, streamOrdinal);
                bool accepted;
                stream = stream.NextChance(
                    (ulong)roll.ProbabilityMillionths,
                    (ulong)IndependentRewardRoll.ProbabilityScale,
                    out accepted);
                AddRewardDecision(
                    trace,
                    contractTraceEntries,
                    request.Operation.SourceOperationStableId,
                    StepSelection,
                    roll.RollStableId,
                    RewardGenerationTraceDecision.IndependentChance,
                    RewardTraceDecisionKind.IndependentChance,
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
                ExclusiveRewardGroup group = request.Profile.ExclusiveGroups[groupIndex];
                ulong totalWeight;
                string weightFailure;
                if (!TrySumRewardWeights(group.Outcomes, out totalWeight, out weightFailure))
                {
                    return BuildRewardFailure(request, contentFingerprint, trace, weightFailure);
                }

                ulong streamOrdinal = RewardGenerationFingerprint.StableOrdinal(group.GroupStableId);
                DeterministicRandom stream = root.Fork(PurposeRewardExclusive, streamOrdinal);
                ulong sample;
                stream = stream.NextBoundedUInt64(totalWeight, out sample);
                WeightedRewardOutcome selected = SelectRewardOutcome(group.Outcomes, sample);
                AddRewardDecision(
                    trace,
                    contractTraceEntries,
                    request.Operation.SourceOperationStableId,
                    StepSelection,
                    group.GroupStableId,
                    RewardGenerationTraceDecision.ExclusiveSelection,
                    RewardTraceDecisionKind.ExclusiveSelection,
                    PurposeRewardExclusive,
                    streamOrdinal,
                    stream.SamplesConsumed,
                    checked((long)totalWeight),
                    checked((long)sample),
                    "selected=" + selected.OutcomeStableId);
                if (selected.Kind == WeightedRewardOutcomeKind.ExplicitNoDrop)
                {
                    AddRewardDecision(
                        trace,
                        contractTraceEntries,
                        request.Operation.SourceOperationStableId,
                        StepResult,
                        selected.OutcomeStableId,
                        RewardGenerationTraceDecision.ExplicitNoDrop,
                        RewardTraceDecisionKind.ExplicitNoDrop,
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

            RewardResult result = grants.Count == 0
                ? RewardResult.CreateExplicitNoDrop(
                    request.Operation.CommitmentStableId,
                    request.Operation.SourceOperationStableId)
                : RewardResult.CreateGrants(
                    request.Operation.CommitmentStableId,
                    request.Operation.SourceOperationStableId,
                    grants);
            RewardGenerationStatus status = grants.Count == 0
                ? RewardGenerationStatus.ExplicitNoDrop
                : RewardGenerationStatus.Generated;
            if (grants.Count == 0)
            {
                AddRewardDecision(
                    trace,
                    contractTraceEntries,
                    request.Operation.SourceOperationStableId,
                    StepResult,
                    request.Profile.ProfileStableId,
                    RewardGenerationTraceDecision.ExplicitNoDrop,
                    RewardTraceDecisionKind.ExplicitNoDrop,
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
            RewardGenerationRequest request,
            DeterministicRandom root,
            RewardGrantSpecification specification,
            TraceAccumulator trace,
            List<RewardTraceEntry> contractTraceEntries,
            List<RewardGrant> grants,
            out string failure)
        {
            ulong streamOrdinal = RewardGenerationFingerprint.StableOrdinal(specification.GrantStableId);
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
                RewardGenerationTraceDecision.Quantity,
                RewardTraceDecisionKind.Quantity,
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
                    RewardScalingInputDescriptor descriptor = specification.ScalingInputs[index];
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
                        RewardGenerationTraceDecision.ScalingInput,
                        RewardTraceDecisionKind.ScalingInput,
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

            RewardGrant grant = RewardGrant.Create(
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
                RewardGenerationTraceDecision.GrantProduced,
                RewardTraceDecisionKind.GrantProduced,
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
            RewardGenerationRequest request,
            RewardScalingInputDescriptor descriptor,
            out long value)
        {
            switch (descriptor.Kind)
            {
                case RewardScalingInputKind.CharacterLevel:
                    value = request.Context.CharacterLevel;
                    return true;
                case RewardScalingInputKind.RegionLevel:
                    value = request.Context.RegionLevel;
                    return true;
                case RewardScalingInputKind.Difficulty:
                    value = request.Context.DifficultyValue;
                    return true;
                case RewardScalingInputKind.SourceTier:
                case RewardScalingInputKind.Custom:
                    return request.TryGetScalingValue(descriptor.InputStableId, out value);
                default:
                    value = 0L;
                    return false;
            }
        }

        private static RewardGenerationResultEnvelope BuildRewardSuccess(
            RewardGenerationStatus status,
            RewardResult result,
            RewardGenerationRequest request,
            string contentFingerprint,
            TraceAccumulator trace,
            List<RewardTraceEntry> contractEntries)
        {
            RewardTrace rewardTrace = RewardTrace.Create(
                request.Operation.SourceOperationStableId,
                contractEntries);
            RewardGenerationTrace generationTrace = trace.Build(
                request.AlgorithmVersion,
                request.RootSeed,
                contentFingerprint,
                request.Context.Fingerprint,
                result.Fingerprint);
            return new RewardGenerationResultEnvelope(
                status,
                result,
                rewardTrace,
                generationTrace,
                contentFingerprint,
                request.Context.Fingerprint,
                result.Fingerprint,
                string.Empty);
        }

        private static RewardGenerationResultEnvelope BuildRewardFailure(
            RewardGenerationRequest request,
            string contentFingerprint,
            TraceAccumulator trace,
            string failureReason)
        {
            string resultFingerprint = RewardGenerationFingerprint.Compute(
                "schema=reward-generation-failure-v1\nstatus=impossible-policy\nrequest="
                + request.ToCanonicalString()
                + "\nreason="
                + failureReason);
            RewardGenerationTrace generationTrace = trace.Build(
                request.AlgorithmVersion,
                request.RootSeed,
                contentFingerprint,
                request.Context.Fingerprint,
                resultFingerprint);
            return new RewardGenerationResultEnvelope(
                RewardGenerationStatus.ImpossiblePolicy,
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
            List<RewardTraceEntry> contractEntries,
            StableId operationId,
            StableId stepId,
            StableId subjectId,
            RewardGenerationTraceDecision detailedDecision,
            RewardTraceDecisionKind contractDecision,
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
            StableId entryId = RewardGenerationFingerprint.DeriveStableId(
                "reward-trace",
                operationId.ToString(),
                ordinal.ToString(CultureInfo.InvariantCulture));
            contractEntries.Add(RewardTraceEntry.Create(
                entryId,
                ordinal,
                stepId,
                subjectId,
                contractDecision,
                input,
                output));
        }

        private static string BuildRewardContentFingerprint(RewardGenerationRequest request)
        {
            return RewardGenerationFingerprint.Compute(
                "schema=reward-generation-content-v1\noperation_content_fingerprint="
                + request.Operation.ContentFingerprint
                + "\nprofile_fingerprint="
                + request.Profile.Fingerprint);
        }

        private static bool TrySumRewardWeights(
            IReadOnlyList<WeightedRewardOutcome> outcomes,
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

        private static WeightedRewardOutcome SelectRewardOutcome(
            IReadOnlyList<WeightedRewardOutcome> outcomes,
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
