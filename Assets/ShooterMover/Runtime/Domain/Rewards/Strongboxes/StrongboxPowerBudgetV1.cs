using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    /// <summary>
    /// Data-driven SAS4-style strongbox equipment power budget.
    /// Item level is rolled first around player level plus a tier bonus. After GEN
    /// selects the equipment definition, augment capacity is inversely weighted
    /// against the selected item's distance from that mean.
    /// </summary>
    public sealed class StrongboxPowerBudgetPolicyV1 : IEquatable<StrongboxPowerBudgetPolicyV1>
    {
        public const int MaximumLevelDeviationV1 = 12;
        private const int FixedPointScale = 1000;
        private const int NormalSampleCount = 12;
        private const ulong UniformSampleExclusiveUpperBound = 2000001UL;
        private const long UniformCenter = 1000000L;
        private const long ApproximateNormalDivisor = 2000000L;

        private static readonly StableId ItemLevelPurposeId =
            StableId.Parse("strongbox-rng.item-level-v1");
        private static readonly StableId AugmentSlotsPurposeId =
            StableId.Parse("strongbox-rng.augment-slots-v1");

        private readonly string canonicalText;

        private StrongboxPowerBudgetPolicyV1(
            int tierLevelBonus,
            int itemLevelStandardDeviationMilli,
            int minimumAugmentSlots,
            int maximumAugmentSlots,
            int augmentSlotStandardDeviationMilli)
        {
            if (tierLevelBonus < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tierLevelBonus));
            }

            if (itemLevelStandardDeviationMilli <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(itemLevelStandardDeviationMilli));
            }

            if (minimumAugmentSlots < 0 || maximumAugmentSlots < minimumAugmentSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumAugmentSlots));
            }

            if (augmentSlotStandardDeviationMilli < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(augmentSlotStandardDeviationMilli));
            }

            TierLevelBonus = tierLevelBonus;
            ItemLevelStandardDeviationMilli = itemLevelStandardDeviationMilli;
            MinimumAugmentSlots = minimumAugmentSlots;
            MaximumAugmentSlots = maximumAugmentSlots;
            AugmentSlotStandardDeviationMilli = augmentSlotStandardDeviationMilli;

            canonicalText = "schema=strongbox-power-budget-policy-v1"
                + "\ntier_level_bonus=" + TierLevelBonus.ToString(CultureInfo.InvariantCulture)
                + "\nmaximum_level_deviation=" + MaximumLevelDeviationV1.ToString(CultureInfo.InvariantCulture)
                + "\nitem_level_standard_deviation_milli=" + ItemLevelStandardDeviationMilli.ToString(CultureInfo.InvariantCulture)
                + "\nminimum_augment_slots=" + MinimumAugmentSlots.ToString(CultureInfo.InvariantCulture)
                + "\nmaximum_augment_slots=" + MaximumAugmentSlots.ToString(CultureInfo.InvariantCulture)
                + "\naugment_slot_standard_deviation_milli=" + AugmentSlotStandardDeviationMilli.ToString(CultureInfo.InvariantCulture);
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public int TierLevelBonus { get; }
        public int ItemLevelStandardDeviationMilli { get; }
        public int MinimumAugmentSlots { get; }
        public int MaximumAugmentSlots { get; }
        public int AugmentSlotStandardDeviationMilli { get; }
        public string Fingerprint { get; }

        public static StrongboxPowerBudgetPolicyV1 Create(
            int tierLevelBonus,
            int itemLevelStandardDeviationMilli,
            int minimumAugmentSlots,
            int maximumAugmentSlots,
            int augmentSlotStandardDeviationMilli)
        {
            return new StrongboxPowerBudgetPolicyV1(
                tierLevelBonus,
                itemLevelStandardDeviationMilli,
                minimumAugmentSlots,
                maximumAugmentSlots,
                augmentSlotStandardDeviationMilli);
        }

        public StrongboxItemLevelRollV1 RollItemLevel(
            int playerLevel,
            ulong rootSeed,
            int algorithmVersion,
            ulong equipmentSlotOrdinal)
        {
            if (playerLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }

            int meanItemLevel = Math.Max(1, checked(playerLevel + TierLevelBonus));
            int minimumItemLevel = Math.Max(1, meanItemLevel - MaximumLevelDeviationV1);
            int maximumItemLevel = checked(meanItemLevel + MaximumLevelDeviationV1);

            DeterministicRandom itemLevelStream = DeterministicRandom.CreateSubstream(
                rootSeed,
                algorithmVersion,
                ItemLevelPurposeId,
                equipmentSlotOrdinal);
            long itemLevelNormalFixed;
            itemLevelStream = NextApproximateStandardNormalFixed(itemLevelStream, out itemLevelNormalFixed);
            long itemOffsetMilli = DivideRounded(
                checked(itemLevelNormalFixed * ItemLevelStandardDeviationMilli),
                ApproximateNormalDivisor);
            int itemLevelOffset = RoundFixedToInt(itemOffsetMilli);
            itemLevelOffset = Clamp(itemLevelOffset, -MaximumLevelDeviationV1, MaximumLevelDeviationV1);
            int targetItemLevel = Clamp(
                checked(meanItemLevel + itemLevelOffset),
                minimumItemLevel,
                maximumItemLevel);

            return new StrongboxItemLevelRollV1(
                equipmentSlotOrdinal,
                playerLevel,
                TierLevelBonus,
                meanItemLevel,
                minimumItemLevel,
                maximumItemLevel,
                targetItemLevel,
                targetItemLevel - meanItemLevel,
                itemLevelStream.GetTrace().SamplesConsumed,
                Fingerprint);
        }

        public StrongboxEquipmentRollPlanV1 RollAugmentSlots(
            StrongboxItemLevelRollV1 itemLevelRoll,
            StableId selectedEquipmentDefinitionId,
            int selectedEquipmentMaximumAugmentSlots,
            ulong rootSeed,
            int algorithmVersion)
        {
            if (itemLevelRoll == null)
            {
                throw new ArgumentNullException(nameof(itemLevelRoll));
            }

            if (!string.Equals(itemLevelRoll.PolicyFingerprint, Fingerprint, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Item-level roll was not produced by this power-budget policy.",
                    nameof(itemLevelRoll));
            }

            if (selectedEquipmentDefinitionId == null)
            {
                throw new ArgumentNullException(nameof(selectedEquipmentDefinitionId));
            }

            if (selectedEquipmentMaximumAugmentSlots < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(selectedEquipmentMaximumAugmentSlots));
            }

            int effectiveMaximumAugmentSlots = Math.Min(
                MaximumAugmentSlots,
                selectedEquipmentMaximumAugmentSlots);
            if (effectiveMaximumAugmentSlots < MinimumAugmentSlots)
            {
                throw new InvalidOperationException(
                    "Selected equipment cannot satisfy the strongbox minimum augment-slot policy.");
            }

            long expectedSlotsMilli = CalculateExpectedSlotsMilli(
                itemLevelRoll.DifferenceFromMean,
                effectiveMaximumAugmentSlots);
            DeterministicRandom augmentStream = DeterministicRandom.CreateSubstream(
                rootSeed,
                algorithmVersion,
                AugmentSlotsPurposeId,
                itemLevelRoll.EquipmentSlotOrdinal);
            long augmentNormalFixed;
            augmentStream = NextApproximateStandardNormalFixed(augmentStream, out augmentNormalFixed);
            long augmentOffsetMilli = DivideRounded(
                checked(augmentNormalFixed * AugmentSlotStandardDeviationMilli),
                ApproximateNormalDivisor);
            int rolledAugmentSlots = RoundFixedToInt(checked(expectedSlotsMilli + augmentOffsetMilli));
            rolledAugmentSlots = Clamp(
                rolledAugmentSlots,
                MinimumAugmentSlots,
                effectiveMaximumAugmentSlots);

            return new StrongboxEquipmentRollPlanV1(
                itemLevelRoll,
                selectedEquipmentDefinitionId,
                selectedEquipmentMaximumAugmentSlots,
                effectiveMaximumAugmentSlots,
                expectedSlotsMilli,
                rolledAugmentSlots,
                augmentStream.GetTrace().SamplesConsumed,
                Fingerprint);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(StrongboxPowerBudgetPolicyV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxPowerBudgetPolicyV1);
        }

        public override int GetHashCode()
        {
            return StrongboxCanonicalV1.DeterministicHash(canonicalText);
        }

        private long CalculateExpectedSlotsMilli(
            int differenceFromMean,
            int effectiveMaximumAugmentSlots)
        {
            int clampedDifference = Clamp(
                differenceFromMean,
                -MaximumLevelDeviationV1,
                MaximumLevelDeviationV1);
            long slotSpanMilli = checked(
                (long)(effectiveMaximumAugmentSlots - MinimumAugmentSlots) * FixedPointScale);
            long positionFromOverLevelExtreme = MaximumLevelDeviationV1 - clampedDifference;
            long interpolated = DivideRounded(
                checked(slotSpanMilli * positionFromOverLevelExtreme),
                2L * MaximumLevelDeviationV1);
            return checked((long)MinimumAugmentSlots * FixedPointScale + interpolated);
        }

        private static DeterministicRandom NextApproximateStandardNormalFixed(
            DeterministicRandom stream,
            out long normalFixed)
        {
            long centeredSum = 0L;
            DeterministicRandom cursor = stream;
            for (int index = 0; index < NormalSampleCount; index++)
            {
                ulong sample;
                cursor = cursor.NextBoundedUInt64(UniformSampleExclusiveUpperBound, out sample);
                centeredSum = checked(centeredSum + (long)sample - UniformCenter);
            }

            normalFixed = centeredSum;
            return cursor;
        }

        private static long DivideRounded(long numerator, long positiveDenominator)
        {
            if (positiveDenominator <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(positiveDenominator));
            }

            if (numerator >= 0L)
            {
                return checked((numerator + positiveDenominator / 2L) / positiveDenominator);
            }

            return checked(-((-numerator + positiveDenominator / 2L) / positiveDenominator));
        }

        private static int RoundFixedToInt(long milliValue)
        {
            long rounded = DivideRounded(milliValue, FixedPointScale);
            if (rounded < int.MinValue || rounded > int.MaxValue)
            {
                throw new OverflowException("Strongbox fixed-point value exceeds Int32 range.");
            }

            return (int)rounded;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Min(maximum, Math.Max(minimum, value));
        }
    }

    public sealed class StrongboxItemLevelRollV1 : IEquatable<StrongboxItemLevelRollV1>
    {
        private readonly string canonicalText;

        public StrongboxItemLevelRollV1(
            ulong equipmentSlotOrdinal,
            int playerLevel,
            int tierLevelBonus,
            int meanItemLevel,
            int minimumItemLevel,
            int maximumItemLevel,
            int targetItemLevel,
            int differenceFromMean,
            ulong itemLevelSamplesConsumed,
            string policyFingerprint)
        {
            if (playerLevel < 0 || tierLevelBonus < 0 || meanItemLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }

            if (minimumItemLevel < 1
                || maximumItemLevel < minimumItemLevel
                || targetItemLevel < minimumItemLevel
                || targetItemLevel > maximumItemLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(targetItemLevel));
            }

            if (differenceFromMean != targetItemLevel - meanItemLevel
                || Math.Abs(differenceFromMean) > StrongboxPowerBudgetPolicyV1.MaximumLevelDeviationV1)
            {
                throw new ArgumentException(
                    "Strongbox item-level difference must match the target and remain within the V1 bound.",
                    nameof(differenceFromMean));
            }

            if (!StrongboxCanonicalV1.IsFingerprint(policyFingerprint))
            {
                throw new ArgumentException(
                    "Power-budget policy fingerprint must be canonical.",
                    nameof(policyFingerprint));
            }

            EquipmentSlotOrdinal = equipmentSlotOrdinal;
            PlayerLevel = playerLevel;
            TierLevelBonus = tierLevelBonus;
            MeanItemLevel = meanItemLevel;
            MinimumItemLevel = minimumItemLevel;
            MaximumItemLevel = maximumItemLevel;
            TargetItemLevel = targetItemLevel;
            DifferenceFromMean = differenceFromMean;
            ItemLevelSamplesConsumed = itemLevelSamplesConsumed;
            PolicyFingerprint = policyFingerprint;

            StringBuilder builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "equipment_slot_ordinal", EquipmentSlotOrdinal.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "player_level", PlayerLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "tier_level_bonus", TierLevelBonus.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "mean_item_level", MeanItemLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "minimum_item_level", MinimumItemLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "maximum_item_level", MaximumItemLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "target_item_level", TargetItemLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "difference_from_mean", DifferenceFromMean.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "item_level_samples_consumed", ItemLevelSamplesConsumed.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "policy_fingerprint", PolicyFingerprint);
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public ulong EquipmentSlotOrdinal { get; }
        public int PlayerLevel { get; }
        public int TierLevelBonus { get; }
        public int MeanItemLevel { get; }
        public int MinimumItemLevel { get; }
        public int MaximumItemLevel { get; }
        public int TargetItemLevel { get; }
        public int DifferenceFromMean { get; }
        public ulong ItemLevelSamplesConsumed { get; }
        public string PolicyFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(StrongboxItemLevelRollV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxItemLevelRollV1);
        }

        public override int GetHashCode()
        {
            return StrongboxCanonicalV1.DeterministicHash(canonicalText);
        }
    }

    public sealed class StrongboxEquipmentRollPlanV1 : IEquatable<StrongboxEquipmentRollPlanV1>
    {
        private readonly string canonicalText;

        public StrongboxEquipmentRollPlanV1(
            StrongboxItemLevelRollV1 itemLevelRoll,
            StableId selectedEquipmentDefinitionId,
            int selectedEquipmentMaximumAugmentSlots,
            int effectiveMaximumAugmentSlots,
            long expectedAugmentSlotsMilli,
            int rolledAugmentSlots,
            ulong augmentSlotSamplesConsumed,
            string policyFingerprint)
        {
            ItemLevelRoll = itemLevelRoll ?? throw new ArgumentNullException(nameof(itemLevelRoll));
            SelectedEquipmentDefinitionId = selectedEquipmentDefinitionId
                ?? throw new ArgumentNullException(nameof(selectedEquipmentDefinitionId));
            if (selectedEquipmentMaximumAugmentSlots < 0
                || effectiveMaximumAugmentSlots < 0
                || effectiveMaximumAugmentSlots > selectedEquipmentMaximumAugmentSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(effectiveMaximumAugmentSlots));
            }

            if (expectedAugmentSlotsMilli < 0L
                || expectedAugmentSlotsMilli > (long)effectiveMaximumAugmentSlots * 1000L
                || rolledAugmentSlots < 0
                || rolledAugmentSlots > effectiveMaximumAugmentSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(rolledAugmentSlots));
            }

            if (!StrongboxCanonicalV1.IsFingerprint(policyFingerprint))
            {
                throw new ArgumentException(
                    "Power-budget policy fingerprint must be canonical.",
                    nameof(policyFingerprint));
            }

            SelectedEquipmentMaximumAugmentSlots = selectedEquipmentMaximumAugmentSlots;
            EffectiveMaximumAugmentSlots = effectiveMaximumAugmentSlots;
            ExpectedAugmentSlotsMilli = expectedAugmentSlotsMilli;
            RolledAugmentSlots = rolledAugmentSlots;
            AugmentSlotSamplesConsumed = augmentSlotSamplesConsumed;
            PolicyFingerprint = policyFingerprint;

            StringBuilder builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "item_level_roll", ItemLevelRoll.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "selected_equipment_definition_id", SelectedEquipmentDefinitionId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "selected_equipment_maximum_augment_slots", SelectedEquipmentMaximumAugmentSlots.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "effective_maximum_augment_slots", EffectiveMaximumAugmentSlots.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "expected_augment_slots_milli", ExpectedAugmentSlotsMilli.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "rolled_augment_slots", RolledAugmentSlots.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "augment_slot_samples_consumed", AugmentSlotSamplesConsumed.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "policy_fingerprint", PolicyFingerprint);
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public StrongboxItemLevelRollV1 ItemLevelRoll { get; }
        public StableId SelectedEquipmentDefinitionId { get; }
        public int SelectedEquipmentMaximumAugmentSlots { get; }
        public int EffectiveMaximumAugmentSlots { get; }
        public long ExpectedAugmentSlotsMilli { get; }
        public int RolledAugmentSlots { get; }
        public ulong AugmentSlotSamplesConsumed { get; }
        public string PolicyFingerprint { get; }
        public string Fingerprint { get; }

        public int MeanItemLevel { get { return ItemLevelRoll.MeanItemLevel; } }
        public int TargetItemLevel { get { return ItemLevelRoll.TargetItemLevel; } }
        public int DifferenceFromMean { get { return ItemLevelRoll.DifferenceFromMean; } }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(StrongboxEquipmentRollPlanV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxEquipmentRollPlanV1);
        }

        public override int GetHashCode()
        {
            return StrongboxCanonicalV1.DeterministicHash(canonicalText);
        }
    }
}
