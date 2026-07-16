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
        public EquipmentGenerationResultV1 GenerateEquipment(EquipmentGenerationRequestV1 request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            string contentFingerprint = BuildEquipmentContentFingerprint(request);
            TraceAccumulator trace = new TraceAccumulator();
            DeterministicRandom root = DeterministicRandom.Create(request.RootSeed, request.AlgorithmVersion);
            List<WeightedEquipmentCandidate> eligibleEquipment = new List<WeightedEquipmentCandidate>();
            for (int index = 0; index < request.Policy.EquipmentCandidates.Count; index++)
            {
                EquipmentGenerationCandidateV1 candidate = request.Policy.EquipmentCandidates[index];
                bool eligible = candidate.IsEligible(request.Context, request.Catalog);
                ulong weight = 0UL;
                if (eligible)
                {
                    string failure;
                    if (!TryScaleWeight(
                        candidate.EvaluateWeight(request.Context, request.Policy.Activation, request.Policy.Obsolescence),
                        out weight,
                        out failure))
                    {
                        return BuildEquipmentFailure(request, contentFingerprint, trace, failure);
                    }

                    eligibleEquipment.Add(new WeightedEquipmentCandidate(candidate, weight));
                }

                trace.Add(
                    StepEligibility,
                    candidate.EquipmentDefinitionId,
                    RewardGenerationTraceDecisionV1.Eligibility,
                    null,
                    0UL,
                    0UL,
                    eligible ? 1L : 0L,
                    eligible ? checked((long)weight) : 0L,
                    "equipment-candidate");
            }

            if (eligibleEquipment.Count == 0)
            {
                return BuildEquipmentFailure(
                    request,
                    contentFingerprint,
                    trace,
                    "no-eligible-equipment-candidate",
                    RewardGenerationStatusV1.NoEligibleCandidate);
            }

            WeightedEquipmentCandidate selectedEquipment;
            DeterministicRandom candidateStream;
            ulong candidateSample;
            string selectionFailure;
            if (!TrySelectWeighted(
                root,
                PurposeEquipmentCandidate,
                RewardGenerationFingerprintV1.StableOrdinal(request.OperationId),
                eligibleEquipment,
                delegate(WeightedEquipmentCandidate value) { return value.Weight; },
                out selectedEquipment,
                out candidateStream,
                out candidateSample,
                out selectionFailure))
            {
                return BuildEquipmentFailure(request, contentFingerprint, trace, selectionFailure);
            }

            trace.Add(
                StepSelection,
                selectedEquipment.Candidate.EquipmentDefinitionId,
                RewardGenerationTraceDecisionV1.WeightedSelection,
                PurposeEquipmentCandidate,
                RewardGenerationFingerprintV1.StableOrdinal(request.OperationId),
                candidateStream.SamplesConsumed,
                checked((long)candidateSample),
                checked((long)selectedEquipment.Weight),
                "equipment-definition");

            EquipmentDefinition definition = request.Catalog.FindEquipmentDefinition(
                selectedEquipment.Candidate.EquipmentDefinitionId);
            int minimumItemLevel = Math.Max(
                definition.ItemLevelRange.Minimum,
                selectedEquipment.Candidate.GeneratedItemLevelRange.Minimum);
            int maximumItemLevel = Math.Min(
                definition.ItemLevelRange.Maximum,
                selectedEquipment.Candidate.GeneratedItemLevelRange.Maximum);
            int itemLevel;
            DeterministicRandom levelStream = root.Fork(
                PurposeEquipmentLevel,
                RewardGenerationFingerprintV1.StableOrdinal(definition.DefinitionId));
            levelStream = NextInclusiveInt32(levelStream, minimumItemLevel, maximumItemLevel, out itemLevel);
            trace.Add(
                StepSelection,
                definition.DefinitionId,
                RewardGenerationTraceDecisionV1.Quantity,
                PurposeEquipmentLevel,
                RewardGenerationFingerprintV1.StableOrdinal(definition.DefinitionId),
                levelStream.SamplesConsumed,
                minimumItemLevel,
                itemLevel,
                "item-level;maximum=" + maximumItemLevel.ToString(CultureInfo.InvariantCulture));

            List<WeightedQualityCandidate> eligibleQualities = new List<WeightedQualityCandidate>();
            for (int index = 0; index < request.Policy.QualityCandidates.Count; index++)
            {
                EquipmentQualityCandidateV1 candidate = request.Policy.QualityCandidates[index];
                if (!definition.SupportsQuality(candidate.QualityId))
                {
                    continue;
                }

                double availability = ProgressionCurveMath.EvaluateQualityAvailability(
                    request.Context.CharacterLevel,
                    candidate.NominalAvailabilityLevel,
                    request.Policy.Activation);
                ulong weight;
                string failure;
                if (!TryScaleWeight(availability * candidate.Weight, out weight, out failure))
                {
                    return BuildEquipmentFailure(request, contentFingerprint, trace, failure);
                }

                eligibleQualities.Add(new WeightedQualityCandidate(candidate, weight));
                trace.Add(
                    StepQuality,
                    candidate.QualityId,
                    RewardGenerationTraceDecisionV1.Eligibility,
                    null,
                    0UL,
                    0UL,
                    checked((long)(availability * 1000000.0)),
                    checked((long)weight),
                    "quality-availability-millionths");
            }

            if (eligibleQualities.Count == 0)
            {
                return BuildEquipmentFailure(request, contentFingerprint, trace, "no-eligible-quality-candidate");
            }

            WeightedQualityCandidate selectedQuality;
            DeterministicRandom qualityStream;
            ulong qualitySample;
            ulong qualityOrdinal = RewardGenerationFingerprintV1.StableOrdinal(definition.DefinitionId);
            if (!TrySelectWeighted(
                root,
                PurposeEquipmentQuality,
                qualityOrdinal,
                eligibleQualities,
                delegate(WeightedQualityCandidate value) { return value.Weight; },
                out selectedQuality,
                out qualityStream,
                out qualitySample,
                out selectionFailure))
            {
                return BuildEquipmentFailure(request, contentFingerprint, trace, selectionFailure);
            }

            trace.Add(
                StepQuality,
                selectedQuality.Candidate.QualityId,
                RewardGenerationTraceDecisionV1.Quality,
                PurposeEquipmentQuality,
                qualityOrdinal,
                qualityStream.SamplesConsumed,
                checked((long)qualitySample),
                checked((long)selectedQuality.Weight),
                "quality-selection");

            int maximumSlots = Math.Min(request.Policy.MaximumAugmentSlots, definition.MaximumAugmentSlots);
            if (request.Policy.MinimumAugmentSlots > maximumSlots)
            {
                return BuildEquipmentFailure(request, contentFingerprint, trace, "minimum-slot-count-exceeds-definition-capacity");
            }

            int requestedSlots;
            ulong slotsOrdinal = RewardGenerationFingerprintV1.StableOrdinal(request.EquipmentInstanceId);
            DeterministicRandom slotsStream = root.Fork(PurposeEquipmentSlots, slotsOrdinal);
            slotsStream = NextInclusiveInt32(
                slotsStream,
                request.Policy.MinimumAugmentSlots,
                maximumSlots,
                out requestedSlots);
            trace.Add(
                StepSlots,
                request.EquipmentInstanceId,
                RewardGenerationTraceDecisionV1.SlotCount,
                PurposeEquipmentSlots,
                slotsOrdinal,
                slotsStream.SamplesConsumed,
                maximumSlots,
                requestedSlots,
                "minimum=" + request.Policy.MinimumAugmentSlots.ToString(CultureInfo.InvariantCulture));

            List<AugmentInstance> augments = new List<AugmentInstance>();
            for (int slotIndex = 0; slotIndex < requestedSlots; slotIndex++)
            {
                List<WeightedAugmentCandidate> eligibleAugments = BuildEligibleAugments(
                    request,
                    definition,
                    itemLevel,
                    selectedQuality.Candidate.QualityId,
                    augments,
                    slotIndex,
                    trace);
                if (eligibleAugments.Count == 0)
                {
                    if (request.Policy.RequireExactSlotCount || augments.Count < request.Policy.MinimumAugmentSlots)
                    {
                        return BuildEquipmentFailure(
                            request,
                            contentFingerprint,
                            trace,
                            "no-compatible-augment-for-required-slot-" + slotIndex.ToString(CultureInfo.InvariantCulture));
                    }

                    break;
                }

                StableId slotSubject = RewardGenerationFingerprintV1.DeriveStableId(
                    "generation-slot",
                    request.OperationId.ToString(),
                    slotIndex.ToString(CultureInfo.InvariantCulture));
                ulong augmentOrdinal = RewardGenerationFingerprintV1.StableOrdinal(slotSubject);
                WeightedAugmentCandidate selectedAugment;
                DeterministicRandom augmentStream;
                ulong augmentSample;
                if (!TrySelectWeighted(
                    root,
                    PurposeAugmentSelection,
                    augmentOrdinal,
                    eligibleAugments,
                    delegate(WeightedAugmentCandidate value) { return value.Weight; },
                    out selectedAugment,
                    out augmentStream,
                    out augmentSample,
                    out selectionFailure))
                {
                    return BuildEquipmentFailure(request, contentFingerprint, trace, selectionFailure);
                }

                trace.Add(
                    StepAugment,
                    selectedAugment.Definition.DefinitionId,
                    RewardGenerationTraceDecisionV1.AugmentSelection,
                    PurposeAugmentSelection,
                    augmentOrdinal,
                    augmentStream.SamplesConsumed,
                    checked((long)augmentSample),
                    checked((long)selectedAugment.Weight),
                    "slot=" + slotIndex.ToString(CultureInfo.InvariantCulture));

                StableId augmentInstanceId = RewardGenerationFingerprintV1.DeriveStableId(
                    "augment-instance",
                    request.OperationId.ToString(),
                    slotIndex.ToString(CultureInfo.InvariantCulture));
                int augmentTier;
                DeterministicRandom tierStream = root.Fork(
                    PurposeAugmentTier,
                    RewardGenerationFingerprintV1.StableOrdinal(augmentInstanceId));
                tierStream = NextInclusiveInt32(
                    tierStream,
                    selectedAugment.Definition.TierRange.Minimum,
                    selectedAugment.Definition.TierRange.Maximum,
                    out augmentTier);
                trace.Add(
                    StepAugment,
                    selectedAugment.Definition.DefinitionId,
                    RewardGenerationTraceDecisionV1.AugmentTier,
                    PurposeAugmentTier,
                    RewardGenerationFingerprintV1.StableOrdinal(augmentInstanceId),
                    tierStream.SamplesConsumed,
                    selectedAugment.Definition.TierRange.Maximum,
                    augmentTier,
                    "tier-minimum=" + selectedAugment.Definition.TierRange.Minimum.ToString(CultureInfo.InvariantCulture));

                int augmentLevel;
                DeterministicRandom augmentLevelStream = root.Fork(
                    PurposeAugmentLevel,
                    RewardGenerationFingerprintV1.StableOrdinal(augmentInstanceId));
                augmentLevelStream = NextInclusiveInt32(
                    augmentLevelStream,
                    selectedAugment.Definition.LevelRange.Minimum,
                    selectedAugment.Definition.LevelRange.Maximum,
                    out augmentLevel);
                trace.Add(
                    StepAugment,
                    selectedAugment.Definition.DefinitionId,
                    RewardGenerationTraceDecisionV1.AugmentLevel,
                    PurposeAugmentLevel,
                    RewardGenerationFingerprintV1.StableOrdinal(augmentInstanceId),
                    augmentLevelStream.SamplesConsumed,
                    selectedAugment.Definition.LevelRange.Maximum,
                    augmentLevel,
                    "level-minimum=" + selectedAugment.Definition.LevelRange.Minimum.ToString(CultureInfo.InvariantCulture));

                augments.Add(AugmentInstance.Create(
                    augmentInstanceId,
                    selectedAugment.Definition.DefinitionId,
                    augmentTier,
                    augmentLevel));
            }

            EquipmentInstance equipment = EquipmentInstance.Create(
                request.EquipmentInstanceId,
                definition.DefinitionId,
                itemLevel,
                selectedQuality.Candidate.QualityId,
                augments);
            EquipmentValidationResult validation = request.Catalog.ValidateInstance(equipment);
            if (!validation.IsValid)
            {
                return BuildEquipmentFailure(
                    request,
                    contentFingerprint,
                    trace,
                    "generated-instance-failed-catalog-validation:" + FormatIssues(validation.Issues));
            }

            trace.Add(
                StepValidation,
                equipment.InstanceId,
                RewardGenerationTraceDecisionV1.Validation,
                null,
                0UL,
                0UL,
                1L,
                1L,
                "equipment-catalog-validation");
            string resultFingerprint = RewardGenerationFingerprintV1.Compute(
                "schema=equipment-generation-result-v1\nstatus=generated\nrequest="
                + request.ToCanonicalString()
                + "\nequipment:\n"
                + equipment.ToCanonicalString());
            RewardGenerationTraceV1 finalTrace = trace.Build(
                request.AlgorithmVersion,
                request.RootSeed,
                contentFingerprint,
                request.Context.Fingerprint,
                resultFingerprint);
            return new EquipmentGenerationResultV1(
                RewardGenerationStatusV1.Generated,
                equipment,
                finalTrace,
                contentFingerprint,
                request.Context.Fingerprint,
                resultFingerprint,
                string.Empty);
        }
    }
}
