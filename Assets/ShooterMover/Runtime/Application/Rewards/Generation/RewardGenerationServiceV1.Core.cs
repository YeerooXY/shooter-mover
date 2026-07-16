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
        private static readonly StableId PurposeRewardIndependent = StableId.Parse("rng.reward-independent");
        private static readonly StableId PurposeRewardExclusive = StableId.Parse("rng.reward-exclusive");
        private static readonly StableId PurposeRewardQuantity = StableId.Parse("rng.reward-quantity");
        private static readonly StableId PurposeEquipmentCandidate = StableId.Parse("rng.equipment-candidate");
        private static readonly StableId PurposeEquipmentLevel = StableId.Parse("rng.equipment-level");
        private static readonly StableId PurposeEquipmentQuality = StableId.Parse("rng.equipment-quality");
        private static readonly StableId PurposeEquipmentSlots = StableId.Parse("rng.equipment-slots");
        private static readonly StableId PurposeAugmentSelection = StableId.Parse("rng.augment-selection");
        private static readonly StableId PurposeAugmentTier = StableId.Parse("rng.augment-tier");
        private static readonly StableId PurposeAugmentLevel = StableId.Parse("rng.augment-level");

        private static readonly StableId StepEligibility = StableId.Parse("generation.eligibility");
        private static readonly StableId StepSelection = StableId.Parse("generation.selection");
        private static readonly StableId StepQuantity = StableId.Parse("generation.quantity");
        private static readonly StableId StepScaling = StableId.Parse("generation.scaling");
        private static readonly StableId StepQuality = StableId.Parse("generation.quality");
        private static readonly StableId StepSlots = StableId.Parse("generation.slots");
        private static readonly StableId StepAugment = StableId.Parse("generation.augment");
        private static readonly StableId StepValidation = StableId.Parse("generation.validation");
        private static readonly StableId StepResult = StableId.Parse("generation.result");

        private sealed class TraceAccumulator
        {
            private readonly List<RewardGenerationTraceEntryV1> entries = new List<RewardGenerationTraceEntryV1>();

            public int Count { get { return entries.Count; } }

            public void Add(
                StableId stepId,
                StableId subjectId,
                RewardGenerationTraceDecisionV1 decision,
                StableId purposeId,
                ulong substreamOrdinal,
                ulong samplesConsumed,
                long input,
                long output,
                string detail)
            {
                entries.Add(new RewardGenerationTraceEntryV1(
                    entries.Count,
                    stepId,
                    subjectId,
                    decision,
                    purposeId,
                    substreamOrdinal,
                    samplesConsumed,
                    input,
                    output,
                    detail));
            }

            public RewardGenerationTraceV1 Build(
                int algorithmVersion,
                ulong rootSeed,
                string contentFingerprint,
                string contextFingerprint,
                string resultFingerprint)
            {
                return new RewardGenerationTraceV1(
                    algorithmVersion,
                    rootSeed,
                    contentFingerprint,
                    contextFingerprint,
                    resultFingerprint,
                    entries);
            }
        }
    }
}
