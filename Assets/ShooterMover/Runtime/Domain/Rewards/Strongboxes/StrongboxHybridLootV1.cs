using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    public static class StrongboxDefinitionRarityIdsV1
    {
        public static readonly StableId Common = StableId.Parse("rarity.common");
        public static readonly StableId Rare = StableId.Parse("rarity.rare");
        public static readonly StableId Epic = StableId.Parse("rarity.epic");
        public static readonly StableId Legendary = StableId.Parse("rarity.legendary");
        public static readonly StableId Artifact = StableId.Parse("rarity.artifact");
    }

    public sealed class StrongboxWeightedIntOutcomeV1 :
        IComparable<StrongboxWeightedIntOutcomeV1>,
        IEquatable<StrongboxWeightedIntOutcomeV1>
    {
        public StrongboxWeightedIntOutcomeV1(int value, ulong weight)
        {
            if (weight == 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(weight));
            }

            Value = value;
            Weight = weight;
        }

        public int Value { get; }
        public ulong Weight { get; }

        public int CompareTo(StrongboxWeightedIntOutcomeV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : Value.CompareTo(other.Value);
        }

        public bool Equals(StrongboxWeightedIntOutcomeV1 other)
        {
            return !ReferenceEquals(other, null)
                && Value == other.Value
                && Weight == other.Weight;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxWeightedIntOutcomeV1);
        }

        public override int GetHashCode()
        {
            return StrongboxCanonicalV1.DeterministicHash(ToCanonicalString());
        }

        public string ToCanonicalString()
        {
            return Value.ToString(CultureInfo.InvariantCulture)
                + ":"
                + Weight.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class StrongboxDistanceWeightV1 :
        IComparable<StrongboxDistanceWeightV1>,
        IEquatable<StrongboxDistanceWeightV1>
    {
        public StrongboxDistanceWeightV1(int distance, ulong weightMillionths)
        {
            if (distance < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(distance));
            }
            if (weightMillionths == 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(weightMillionths));
            }

            Distance = distance;
            WeightMillionths = weightMillionths;
        }

        public int Distance { get; }
        public ulong WeightMillionths { get; }

        public int CompareTo(StrongboxDistanceWeightV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : Distance.CompareTo(other.Distance);
        }

        public bool Equals(StrongboxDistanceWeightV1 other)
        {
            return !ReferenceEquals(other, null)
                && Distance == other.Distance
                && WeightMillionths == other.WeightMillionths;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxDistanceWeightV1);
        }

        public override int GetHashCode()
        {
            return StrongboxCanonicalV1.DeterministicHash(ToCanonicalString());
        }

        public string ToCanonicalString()
        {
            return Distance.ToString(CultureInfo.InvariantCulture)
                + ":"
                + WeightMillionths.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class StrongboxRarityProfileV1 :
        IComparable<StrongboxRarityProfileV1>,
        IEquatable<StrongboxRarityProfileV1>
    {
        public StrongboxRarityProfileV1(
            StableId rarityId,
            int selectionMultiplierMilli,
            int augmentBiasLevels)
        {
            RarityId = rarityId ?? throw new ArgumentNullException(nameof(rarityId));
            if (selectionMultiplierMilli < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(selectionMultiplierMilli));
            }
            if (augmentBiasLevels < -12 || augmentBiasLevels > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(augmentBiasLevels));
            }

            SelectionMultiplierMilli = selectionMultiplierMilli;
            AugmentBiasLevels = augmentBiasLevels;
        }

        public StableId RarityId { get; }
        public int SelectionMultiplierMilli { get; }
        public int AugmentBiasLevels { get; }

        public int CompareTo(StrongboxRarityProfileV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : RarityId.CompareTo(other.RarityId);
        }

        public bool Equals(StrongboxRarityProfileV1 other)
        {
            return !ReferenceEquals(other, null)
                && RarityId == other.RarityId
                && SelectionMultiplierMilli == other.SelectionMultiplierMilli
                && AugmentBiasLevels == other.AugmentBiasLevels;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxRarityProfileV1);
        }

        public override int GetHashCode()
        {
            return StrongboxCanonicalV1.DeterministicHash(ToCanonicalString());
        }

        public string ToCanonicalString()
        {
            return RarityId
                + ":"
                + SelectionMultiplierMilli.ToString(CultureInfo.InvariantCulture)
                + ":"
                + AugmentBiasLevels.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class StrongboxTargetLevelRollV1 :
        IEquatable<StrongboxTargetLevelRollV1>
    {
        private readonly string canonicalText;

        internal StrongboxTargetLevelRollV1(
            StableId policyId,
            int playerLevel,
            int rolledDelta,
            int unclampedTargetLevel,
            int targetLevel,
            ulong samplesConsumed,
            string policyFingerprint)
        {
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
            if (playerLevel < 0 || targetLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }
            if (!StrongboxCanonicalV1.IsFingerprint(policyFingerprint))
            {
                throw new ArgumentException(
                    "A canonical hybrid-loot policy fingerprint is required.",
                    nameof(policyFingerprint));
            }

            PlayerLevel = playerLevel;
            RolledDelta = rolledDelta;
            UnclampedTargetLevel = unclampedTargetLevel;
            TargetLevel = targetLevel;
            SamplesConsumed = samplesConsumed;
            PolicyFingerprint = policyFingerprint;

            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "policy_id", PolicyId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "player_level", PlayerLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "rolled_delta", RolledDelta.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "unclamped_target_level", UnclampedTargetLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "target_level", TargetLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "samples_consumed", SamplesConsumed.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "policy_fingerprint", PolicyFingerprint);
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId PolicyId { get; }
        public int PlayerLevel { get; }
        public int RolledDelta { get; }
        public int UnclampedTargetLevel { get; }
        public int TargetLevel { get; }
        public ulong SamplesConsumed { get; }
        public string PolicyFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(StrongboxTargetLevelRollV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxTargetLevelRollV1);
        }

        public override int GetHashCode()
        {
            return StrongboxCanonicalV1.DeterministicHash(canonicalText);
        }
    }

    public sealed class StrongboxInstanceLevelRollV1 :
        IEquatable<StrongboxInstanceLevelRollV1>
    {
        private readonly string canonicalText;

        internal StrongboxInstanceLevelRollV1(
            StrongboxTargetLevelRollV1 targetRoll,
            int definitionPeakLevel,
            StableId rarityId,
            int hybridCenterLevel,
            int variationOffset,
            int itemLevel,
            ulong samplesConsumed,
            string policyFingerprint)
        {
            TargetRoll = targetRoll ?? throw new ArgumentNullException(nameof(targetRoll));
            RarityId = rarityId ?? throw new ArgumentNullException(nameof(rarityId));
            if (definitionPeakLevel < 1 || hybridCenterLevel < 1 || itemLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(definitionPeakLevel));
            }
            if (!StrongboxCanonicalV1.IsFingerprint(policyFingerprint))
            {
                throw new ArgumentException(
                    "A canonical hybrid-loot policy fingerprint is required.",
                    nameof(policyFingerprint));
            }

            DefinitionPeakLevel = definitionPeakLevel;
            HybridCenterLevel = hybridCenterLevel;
            VariationOffset = variationOffset;
            ItemLevel = itemLevel;
            SamplesConsumed = samplesConsumed;
            PolicyFingerprint = policyFingerprint;

            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "target_roll", TargetRoll.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "definition_peak_level", DefinitionPeakLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "rarity_id", RarityId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "hybrid_center_level", HybridCenterLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "variation_offset", VariationOffset.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "item_level", ItemLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "samples_consumed", SamplesConsumed.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "policy_fingerprint", PolicyFingerprint);
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public StrongboxTargetLevelRollV1 TargetRoll { get; }
        public int DefinitionPeakLevel { get; }
        public StableId RarityId { get; }
        public int HybridCenterLevel { get; }
        public int VariationOffset { get; }
        public int ItemLevel { get; }
        public ulong SamplesConsumed { get; }
        public string PolicyFingerprint { get; }
        public string Fingerprint { get; }

        public int DefinitionDistanceFromTarget
        {
            get { return DefinitionPeakLevel - TargetRoll.TargetLevel; }
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(StrongboxInstanceLevelRollV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxInstanceLevelRollV1);
        }

        public override int GetHashCode()
        {
            return StrongboxCanonicalV1.DeterministicHash(canonicalText);
        }
    }

    public sealed class StrongboxAugmentSignatureV1 :
        IEquatable<StrongboxAugmentSignatureV1>
    {
        private readonly string canonicalText;

        internal StrongboxAugmentSignatureV1(
            StableId policyId,
            StableId rarityId,
            int playerLevel,
            int itemLevel,
            int effectiveBiasLevels,
            int normalMaximumSlots,
            int absoluteMaximumSlots,
            int authoredSlotOutcome,
            int slotCount,
            int sharedLevel,
            ulong slotSamplesConsumed,
            ulong levelSamplesConsumed,
            string policyFingerprint)
        {
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
            RarityId = rarityId ?? throw new ArgumentNullException(nameof(rarityId));
            if (playerLevel < 0 || itemLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }
            if (normalMaximumSlots < 0
                || absoluteMaximumSlots < normalMaximumSlots
                || slotCount < 0
                || slotCount > absoluteMaximumSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount));
            }
            if ((slotCount == 0 && sharedLevel != 0)
                || (slotCount > 0 && sharedLevel < 1))
            {
                throw new ArgumentOutOfRangeException(nameof(sharedLevel));
            }
            if (!StrongboxCanonicalV1.IsFingerprint(policyFingerprint))
            {
                throw new ArgumentException(
                    "A canonical hybrid-loot policy fingerprint is required.",
                    nameof(policyFingerprint));
            }

            PlayerLevel = playerLevel;
            ItemLevel = itemLevel;
            EffectiveBiasLevels = effectiveBiasLevels;
            NormalMaximumSlots = normalMaximumSlots;
            AbsoluteMaximumSlots = absoluteMaximumSlots;
            AuthoredSlotOutcome = authoredSlotOutcome;
            SlotCount = slotCount;
            SharedLevel = sharedLevel;
            SlotSamplesConsumed = slotSamplesConsumed;
            LevelSamplesConsumed = levelSamplesConsumed;
            PolicyFingerprint = policyFingerprint;

            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "policy_id", PolicyId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "rarity_id", RarityId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "player_level", PlayerLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "item_level", ItemLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "effective_bias_levels", EffectiveBiasLevels.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "normal_maximum_slots", NormalMaximumSlots.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "absolute_maximum_slots", AbsoluteMaximumSlots.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "authored_slot_outcome", AuthoredSlotOutcome.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "slot_count", SlotCount.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "shared_level", SharedLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "slot_samples_consumed", SlotSamplesConsumed.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "level_samples_consumed", LevelSamplesConsumed.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "policy_fingerprint", PolicyFingerprint);
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId PolicyId { get; }
        public StableId RarityId { get; }
        public int PlayerLevel { get; }
        public int ItemLevel { get; }
        public int EffectiveBiasLevels { get; }
        public int NormalMaximumSlots { get; }
        public int AbsoluteMaximumSlots { get; }
        public int AuthoredSlotOutcome { get; }
        public int SlotCount { get; }
        public int SharedLevel { get; }
        public ulong SlotSamplesConsumed { get; }
        public ulong LevelSamplesConsumed { get; }
        public string PolicyFingerprint { get; }
        public string Fingerprint { get; }

        public bool HasAugmentCapacity { get { return SlotCount > 0; } }
        public bool HasOvercapSlot { get { return SlotCount > NormalMaximumSlots; } }
        public bool HasOvercapLevel { get { return SharedLevel > 10; } }

        public string DisplaySignature
        {
            get
            {
                return SlotCount == 0
                    ? "0/0"
                    : SharedLevel.ToString(CultureInfo.InvariantCulture)
                        + "/"
                        + SlotCount.ToString(CultureInfo.InvariantCulture);
            }
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(StrongboxAugmentSignatureV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxAugmentSignatureV1);
        }

        public override int GetHashCode()
        {
            return StrongboxCanonicalV1.DeterministicHash(canonicalText);
        }
    }

    /// <summary>
    /// Engine-neutral, deterministic strongbox policy foundation. A tier first rolls
    /// a triangular target around the player. Definitions receive a bell-shaped
    /// affinity around that target. The selected definition then receives a hybrid
    /// instance level and one SAS-style shared augment signature such as 10/3.
    /// Installed augment identities remain owned by the equipment/augment authority.
    /// </summary>
    public sealed class StrongboxHybridLootPolicyV1 :
        IEquatable<StrongboxHybridLootPolicyV1>
    {
        public const int DefinitionWeightScale = 1000000;
        public const int RarityMultiplierScale = 1000;
        public const int BlendScale = 1000;
        public const int AuthoredNormalWeaponSlots = 3;
        public const int NormalMaximumAugmentLevel = 10;

        private const int SlotBiasPerStepMilli = 55;
        private const int LevelBiasPerStepMilli = 35;
        private const int MinimumBiasMultiplierMilli = 50;
        private const int MaximumBiasMultiplierMilli = 5000;

        private static readonly StableId TargetPurposeId =
            StableId.Parse("strongbox-rng.hybrid-target-v1");
        private static readonly StableId InstanceLevelPurposeId =
            StableId.Parse("strongbox-rng.hybrid-instance-level-v1");
        private static readonly StableId AugmentSlotsPurposeId =
            StableId.Parse("strongbox-rng.hybrid-augment-slots-v1");
        private static readonly StableId AugmentLevelPurposeId =
            StableId.Parse("strongbox-rng.hybrid-augment-level-v1");

        private readonly ReadOnlyCollection<StrongboxDistanceWeightV1> definitionBellWeights;
        private readonly ReadOnlyCollection<StrongboxWeightedIntOutcomeV1> instanceLevelOffsets;
        private readonly ReadOnlyCollection<StrongboxWeightedIntOutcomeV1> augmentSlotOutcomes;
        private readonly ReadOnlyCollection<StrongboxWeightedIntOutcomeV1> augmentLevelOutcomes;
        private readonly ReadOnlyCollection<StrongboxRarityProfileV1> rarityProfiles;
        private readonly Dictionary<StableId, StrongboxRarityProfileV1> rarityById;
        private readonly string canonicalText;

        private StrongboxHybridLootPolicyV1(
            StableId policyId,
            int minimumTargetDelta,
            int mostLikelyTargetDelta,
            int maximumTargetDelta,
            int targetBlendPermille,
            IEnumerable<StrongboxDistanceWeightV1> definitionBellWeights,
            IEnumerable<StrongboxWeightedIntOutcomeV1> instanceLevelOffsets,
            IEnumerable<StrongboxWeightedIntOutcomeV1> augmentSlotOutcomes,
            IEnumerable<StrongboxWeightedIntOutcomeV1> augmentLevelOutcomes,
            IEnumerable<StrongboxRarityProfileV1> rarityProfiles)
        {
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
            if (minimumTargetDelta > mostLikelyTargetDelta
                || mostLikelyTargetDelta > maximumTargetDelta)
            {
                throw new ArgumentException(
                    "The triangular target deltas must satisfy minimum <= mode <= maximum.");
            }
            if (targetBlendPermille < 0 || targetBlendPermille > BlendScale)
            {
                throw new ArgumentOutOfRangeException(nameof(targetBlendPermille));
            }

            MinimumTargetDelta = minimumTargetDelta;
            MostLikelyTargetDelta = mostLikelyTargetDelta;
            MaximumTargetDelta = maximumTargetDelta;
            TargetBlendPermille = targetBlendPermille;
            this.definitionBellWeights = CopyDistanceWeights(definitionBellWeights);
            this.instanceLevelOffsets = CopyOutcomes(
                instanceLevelOffsets,
                nameof(instanceLevelOffsets),
                int.MinValue);
            this.augmentSlotOutcomes = CopyOutcomes(
                augmentSlotOutcomes,
                nameof(augmentSlotOutcomes),
                0);
            this.augmentLevelOutcomes = CopyOutcomes(
                augmentLevelOutcomes,
                nameof(augmentLevelOutcomes),
                1);
            Dictionary<StableId, StrongboxRarityProfileV1> rarityMap;
            this.rarityProfiles = CopyRarities(rarityProfiles, out rarityMap);
            rarityById = rarityMap;

            DefinitionSelectionRadius = this.definitionBellWeights.Count - 1;
            ValidateOutcomeValues();
            canonicalText = BuildCanonicalText();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId PolicyId { get; }
        public int MinimumTargetDelta { get; }
        public int MostLikelyTargetDelta { get; }
        public int MaximumTargetDelta { get; }
        public int TargetBlendPermille { get; }
        public int DefinitionSelectionRadius { get; }
        public IReadOnlyList<StrongboxDistanceWeightV1> DefinitionBellWeights
        {
            get { return definitionBellWeights; }
        }
        public IReadOnlyList<StrongboxWeightedIntOutcomeV1> InstanceLevelOffsets
        {
            get { return instanceLevelOffsets; }
        }
        public IReadOnlyList<StrongboxWeightedIntOutcomeV1> AugmentSlotOutcomes
        {
            get { return augmentSlotOutcomes; }
        }
        public IReadOnlyList<StrongboxWeightedIntOutcomeV1> AugmentLevelOutcomes
        {
            get { return augmentLevelOutcomes; }
        }
        public IReadOnlyList<StrongboxRarityProfileV1> RarityProfiles
        {
            get { return rarityProfiles; }
        }
        public string Fingerprint { get; }

        public static StrongboxHybridLootPolicyV1 Create(
            StableId policyId,
            int minimumTargetDelta,
            int mostLikelyTargetDelta,
            int maximumTargetDelta,
            int targetBlendPermille,
            IEnumerable<StrongboxDistanceWeightV1> definitionBellWeights,
            IEnumerable<StrongboxWeightedIntOutcomeV1> instanceLevelOffsets,
            IEnumerable<StrongboxWeightedIntOutcomeV1> augmentSlotOutcomes,
            IEnumerable<StrongboxWeightedIntOutcomeV1> augmentLevelOutcomes,
            IEnumerable<StrongboxRarityProfileV1> rarityProfiles)
        {
            return new StrongboxHybridLootPolicyV1(
                policyId,
                minimumTargetDelta,
                mostLikelyTargetDelta,
                maximumTargetDelta,
                targetBlendPermille,
                definitionBellWeights,
                instanceLevelOffsets,
                augmentSlotOutcomes,
                augmentLevelOutcomes,
                rarityProfiles);
        }

        public StrongboxTargetLevelRollV1 RollTargetLevel(
            int playerLevel,
            ulong rootSeed,
            int algorithmVersion,
            ulong equipmentSlotOrdinal)
        {
            if (playerLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }

            List<StrongboxWeightedIntOutcomeV1> triangular =
                BuildTriangularTargetOutcomes();
            DeterministicRandom stream = DeterministicRandom.CreateSubstream(
                rootSeed,
                algorithmVersion,
                TargetPurposeId,
                equipmentSlotOrdinal);
            int delta;
            stream = RollWeighted(stream, triangular, out delta);
            int unclamped = checked(playerLevel + delta);
            int target = Math.Max(1, unclamped);
            return new StrongboxTargetLevelRollV1(
                PolicyId,
                playerLevel,
                delta,
                unclamped,
                target,
                stream.GetTrace().SamplesConsumed,
                Fingerprint);
        }

        public double EvaluateDefinitionWeight(
            StrongboxTargetLevelRollV1 targetRoll,
            int definitionPeakLevel,
            double baseDefinitionWeight,
            StableId rarityId)
        {
            RequireTargetRoll(targetRoll);
            if (definitionPeakLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(definitionPeakLevel));
            }
            if (double.IsNaN(baseDefinitionWeight)
                || double.IsInfinity(baseDefinitionWeight)
                || baseDefinitionWeight <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDefinitionWeight));
            }

            StrongboxRarityProfileV1 rarity = RequireRarity(rarityId);
            int distance = Math.Abs(definitionPeakLevel - targetRoll.TargetLevel);
            if (distance > DefinitionSelectionRadius
                || rarity.SelectionMultiplierMilli == 0)
            {
                return 0.0;
            }

            double levelAffinity = definitionBellWeights[distance].WeightMillionths
                / (double)DefinitionWeightScale;
            double rarityMultiplier = rarity.SelectionMultiplierMilli
                / (double)RarityMultiplierScale;
            return baseDefinitionWeight * levelAffinity * rarityMultiplier;
        }

        public StrongboxInstanceLevelRollV1 RollInstanceLevel(
            StrongboxTargetLevelRollV1 targetRoll,
            int definitionPeakLevel,
            StableId rarityId,
            ulong rootSeed,
            int algorithmVersion,
            ulong equipmentSlotOrdinal)
        {
            RequireTargetRoll(targetRoll);
            RequireRarity(rarityId);
            if (definitionPeakLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(definitionPeakLevel));
            }
            if (Math.Abs(definitionPeakLevel - targetRoll.TargetLevel)
                > DefinitionSelectionRadius)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definitionPeakLevel),
                    "The selected definition is outside the authored hybrid selection radius.");
            }

            long blended = checked(
                (long)targetRoll.TargetLevel * TargetBlendPermille
                + (long)definitionPeakLevel * (BlendScale - TargetBlendPermille));
            int center = Math.Max(1, CheckedInt(DivideRounded(blended, BlendScale)));

            DeterministicRandom stream = DeterministicRandom.CreateSubstream(
                rootSeed,
                algorithmVersion,
                InstanceLevelPurposeId,
                equipmentSlotOrdinal);
            int offset;
            stream = RollWeighted(stream, instanceLevelOffsets, out offset);
            int itemLevel = Math.Max(1, checked(center + offset));
            return new StrongboxInstanceLevelRollV1(
                targetRoll,
                definitionPeakLevel,
                rarityId,
                center,
                offset,
                itemLevel,
                stream.GetTrace().SamplesConsumed,
                Fingerprint);
        }

        public StrongboxAugmentSignatureV1 RollAugmentSignature(
            int playerLevel,
            int itemLevel,
            StableId rarityId,
            int normalMaximumSlots,
            int absoluteMaximumSlots,
            ulong rootSeed,
            int algorithmVersion,
            ulong equipmentSlotOrdinal)
        {
            if (playerLevel < 0 || itemLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }
            if (normalMaximumSlots < 0
                || absoluteMaximumSlots < normalMaximumSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(normalMaximumSlots));
            }

            StrongboxRarityProfileV1 rarity = RequireRarity(rarityId);
            int bias = Clamp(
                checked(playerLevel - itemLevel + rarity.AugmentBiasLevels),
                -12,
                12);
            List<StrongboxWeightedIntOutcomeV1> mappedSlots =
                MapAndAdjustSlotOutcomes(
                    normalMaximumSlots,
                    absoluteMaximumSlots,
                    bias);
            DeterministicRandom slotStream = DeterministicRandom.CreateSubstream(
                rootSeed,
                algorithmVersion,
                AugmentSlotsPurposeId,
                equipmentSlotOrdinal);
            int mappedSlotCount;
            slotStream = RollWeighted(slotStream, mappedSlots, out mappedSlotCount);
            int authoredSlotOutcome = ResolveRepresentativeAuthoredSlot(
                mappedSlotCount,
                normalMaximumSlots,
                absoluteMaximumSlots);

            int sharedLevel = 0;
            ulong levelSamples = 0UL;
            if (mappedSlotCount > 0)
            {
                List<StrongboxWeightedIntOutcomeV1> adjustedLevels =
                    AdjustOutcomes(augmentLevelOutcomes, bias, LevelBiasPerStepMilli);
                DeterministicRandom levelStream = DeterministicRandom.CreateSubstream(
                    rootSeed,
                    algorithmVersion,
                    AugmentLevelPurposeId,
                    equipmentSlotOrdinal);
                levelStream = RollWeighted(levelStream, adjustedLevels, out sharedLevel);
                levelSamples = levelStream.GetTrace().SamplesConsumed;
            }

            return new StrongboxAugmentSignatureV1(
                PolicyId,
                rarityId,
                playerLevel,
                itemLevel,
                bias,
                normalMaximumSlots,
                absoluteMaximumSlots,
                authoredSlotOutcome,
                mappedSlotCount,
                sharedLevel,
                slotStream.GetTrace().SamplesConsumed,
                levelSamples,
                Fingerprint);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(StrongboxHybridLootPolicyV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxHybridLootPolicyV1);
        }

        public override int GetHashCode()
        {
            return StrongboxCanonicalV1.DeterministicHash(canonicalText);
        }

        private void RequireTargetRoll(StrongboxTargetLevelRollV1 targetRoll)
        {
            if (targetRoll == null)
            {
                throw new ArgumentNullException(nameof(targetRoll));
            }
            if (targetRoll.PolicyId != PolicyId
                || !string.Equals(
                    targetRoll.PolicyFingerprint,
                    Fingerprint,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The target roll belongs to a different hybrid-loot policy.",
                    nameof(targetRoll));
            }
        }

        private StrongboxRarityProfileV1 RequireRarity(StableId rarityId)
        {
            StrongboxRarityProfileV1 profile;
            if (rarityId == null || !rarityById.TryGetValue(rarityId, out profile))
            {
                throw new ArgumentException(
                    "The definition rarity is not registered by this hybrid-loot policy.",
                    nameof(rarityId));
            }
            return profile;
        }

        private List<StrongboxWeightedIntOutcomeV1> BuildTriangularTargetOutcomes()
        {
            var outcomes = new List<StrongboxWeightedIntOutcomeV1>();
            int leftScale = checked(MaximumTargetDelta - MostLikelyTargetDelta + 1);
            int rightScale = checked(MostLikelyTargetDelta - MinimumTargetDelta + 1);
            for (int delta = MinimumTargetDelta; delta <= MaximumTargetDelta; delta++)
            {
                long weight = delta <= MostLikelyTargetDelta
                    ? checked((long)(delta - MinimumTargetDelta + 1) * leftScale)
                    : checked((long)(MaximumTargetDelta - delta + 1) * rightScale);
                outcomes.Add(new StrongboxWeightedIntOutcomeV1(delta, checked((ulong)weight)));
            }
            return outcomes;
        }

        private List<StrongboxWeightedIntOutcomeV1> MapAndAdjustSlotOutcomes(
            int normalMaximumSlots,
            int absoluteMaximumSlots,
            int bias)
        {
            var accumulated = new SortedDictionary<int, ulong>();
            for (int index = 0; index < augmentSlotOutcomes.Count; index++)
            {
                StrongboxWeightedIntOutcomeV1 authored = augmentSlotOutcomes[index];
                int mapped = MapAuthoredSlotCount(
                    authored.Value,
                    normalMaximumSlots,
                    absoluteMaximumSlots);
                ulong existing;
                accumulated.TryGetValue(mapped, out existing);
                accumulated[mapped] = checked(existing + authored.Weight);
            }

            var mappedOutcomes = new List<StrongboxWeightedIntOutcomeV1>();
            foreach (KeyValuePair<int, ulong> pair in accumulated)
            {
                mappedOutcomes.Add(new StrongboxWeightedIntOutcomeV1(pair.Key, pair.Value));
            }
            return AdjustOutcomes(mappedOutcomes, bias, SlotBiasPerStepMilli);
        }

        private static int MapAuthoredSlotCount(
            int authoredSlotCount,
            int normalMaximumSlots,
            int absoluteMaximumSlots)
        {
            if (authoredSlotCount <= AuthoredNormalWeaponSlots)
            {
                return Math.Min(authoredSlotCount, normalMaximumSlots);
            }

            int overcapSteps = authoredSlotCount - AuthoredNormalWeaponSlots;
            return Math.Min(
                checked(normalMaximumSlots + overcapSteps),
                absoluteMaximumSlots);
        }

        private int ResolveRepresentativeAuthoredSlot(
            int mappedSlotCount,
            int normalMaximumSlots,
            int absoluteMaximumSlots)
        {
            for (int index = 0; index < augmentSlotOutcomes.Count; index++)
            {
                int authored = augmentSlotOutcomes[index].Value;
                if (MapAuthoredSlotCount(
                    authored,
                    normalMaximumSlots,
                    absoluteMaximumSlots) == mappedSlotCount)
                {
                    return authored;
                }
            }
            return mappedSlotCount;
        }

        private static List<StrongboxWeightedIntOutcomeV1> AdjustOutcomes(
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> source,
            int bias,
            int slopeMilli)
        {
            var adjusted = new List<StrongboxWeightedIntOutcomeV1>(source.Count);
            int minimumValue = source[0].Value;
            for (int index = 0; index < source.Count; index++)
            {
                StrongboxWeightedIntOutcomeV1 outcome = source[index];
                int steps = outcome.Value - minimumValue;
                int multiplier = Clamp(
                    checked(RarityMultiplierScale + bias * slopeMilli * steps),
                    MinimumBiasMultiplierMilli,
                    MaximumBiasMultiplierMilli);
                ulong weight = checked(
                    outcome.Weight * checked((ulong)multiplier));
                adjusted.Add(new StrongboxWeightedIntOutcomeV1(
                    outcome.Value,
                    weight));
            }
            return adjusted;
        }

        private static DeterministicRandom RollWeighted(
            DeterministicRandom stream,
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> outcomes,
            out int value)
        {
            if (outcomes == null || outcomes.Count == 0)
            {
                throw new ArgumentException("At least one weighted outcome is required.", nameof(outcomes));
            }

            ulong total = 0UL;
            for (int index = 0; index < outcomes.Count; index++)
            {
                total = checked(total + outcomes[index].Weight);
            }

            ulong selected;
            DeterministicRandom next = stream.NextBoundedUInt64(total, out selected);
            ulong cumulative = 0UL;
            for (int index = 0; index < outcomes.Count; index++)
            {
                cumulative = checked(cumulative + outcomes[index].Weight);
                if (selected < cumulative)
                {
                    value = outcomes[index].Value;
                    return next;
                }
            }

            throw new InvalidOperationException("Weighted selection did not resolve an outcome.");
        }

        private static ReadOnlyCollection<StrongboxDistanceWeightV1> CopyDistanceWeights(
            IEnumerable<StrongboxDistanceWeightV1> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var copy = new List<StrongboxDistanceWeightV1>();
            foreach (StrongboxDistanceWeightV1 value in values)
            {
                if (value == null)
                {
                    throw new ArgumentException(
                        "Definition bell weights must not contain null entries.",
                        nameof(values));
                }
                copy.Add(value);
            }
            copy.Sort();
            if (copy.Count == 0 || copy[0].Distance != 0)
            {
                throw new ArgumentException(
                    "Definition bell weights must begin at distance zero.",
                    nameof(values));
            }
            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index].Distance != index)
                {
                    throw new ArgumentException(
                        "Definition bell weights must cover every distance contiguously.",
                        nameof(values));
                }
            }
            return new ReadOnlyCollection<StrongboxDistanceWeightV1>(copy);
        }

        private static ReadOnlyCollection<StrongboxWeightedIntOutcomeV1> CopyOutcomes(
            IEnumerable<StrongboxWeightedIntOutcomeV1> values,
            string parameterName,
            int minimumValue)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<StrongboxWeightedIntOutcomeV1>();
            var seen = new HashSet<int>();
            foreach (StrongboxWeightedIntOutcomeV1 value in values)
            {
                if (value == null || !seen.Add(value.Value))
                {
                    throw new ArgumentException(
                        "Weighted outcomes must be non-null and have unique values.",
                        parameterName);
                }
                if (value.Value < minimumValue)
                {
                    throw new ArgumentOutOfRangeException(parameterName);
                }
                copy.Add(value);
            }
            copy.Sort();
            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "At least one weighted outcome is required.",
                    parameterName);
            }
            return new ReadOnlyCollection<StrongboxWeightedIntOutcomeV1>(copy);
        }

        private static ReadOnlyCollection<StrongboxRarityProfileV1> CopyRarities(
            IEnumerable<StrongboxRarityProfileV1> values,
            out Dictionary<StableId, StrongboxRarityProfileV1> byId)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var copy = new List<StrongboxRarityProfileV1>();
            byId = new Dictionary<StableId, StrongboxRarityProfileV1>();
            foreach (StrongboxRarityProfileV1 value in values)
            {
                if (value == null || byId.ContainsKey(value.RarityId))
                {
                    throw new ArgumentException(
                        "Rarity profiles must be non-null and unique.",
                        nameof(values));
                }
                byId.Add(value.RarityId, value);
                copy.Add(value);
            }
            copy.Sort();
            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "At least one rarity profile is required.",
                    nameof(values));
            }
            return new ReadOnlyCollection<StrongboxRarityProfileV1>(copy);
        }

        private void ValidateOutcomeValues()
        {
            for (int index = 0; index < augmentLevelOutcomes.Count; index++)
            {
                if (augmentLevelOutcomes[index].Value > 11)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(augmentLevelOutcomes),
                        "V1 supports shared augment levels through level 11.");
                }
            }
            for (int index = 0; index < augmentSlotOutcomes.Count; index++)
            {
                if (augmentSlotOutcomes[index].Value > 4)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(augmentSlotOutcomes),
                        "V1 supports authored weapon slot outcomes through four slots.");
                }
            }
        }

        private string BuildCanonicalText()
        {
            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "schema", "strongbox-hybrid-loot-policy-v1");
            StrongboxCanonicalV1.AppendToken(builder, "policy_id", PolicyId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "minimum_target_delta", MinimumTargetDelta.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "most_likely_target_delta", MostLikelyTargetDelta.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "maximum_target_delta", MaximumTargetDelta.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "target_blend_permille", TargetBlendPermille.ToString(CultureInfo.InvariantCulture));
            AppendDistanceWeights(builder, definitionBellWeights);
            AppendOutcomes(builder, "instance_level_offset", instanceLevelOffsets);
            AppendOutcomes(builder, "augment_slot", augmentSlotOutcomes);
            AppendOutcomes(builder, "augment_level", augmentLevelOutcomes);
            StrongboxCanonicalV1.AppendToken(builder, "rarity_count", rarityProfiles.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < rarityProfiles.Count; index++)
            {
                StrongboxCanonicalV1.AppendToken(
                    builder,
                    "rarity_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    rarityProfiles[index].ToCanonicalString());
            }
            return builder.ToString();
        }

        private static void AppendDistanceWeights(
            StringBuilder builder,
            IReadOnlyList<StrongboxDistanceWeightV1> values)
        {
            StrongboxCanonicalV1.AppendToken(builder, "bell_weight_count", values.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                StrongboxCanonicalV1.AppendToken(
                    builder,
                    "bell_weight_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    values[index].ToCanonicalString());
            }
        }

        private static void AppendOutcomes(
            StringBuilder builder,
            string prefix,
            IReadOnlyList<StrongboxWeightedIntOutcomeV1> values)
        {
            StrongboxCanonicalV1.AppendToken(builder, prefix + "_count", values.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                StrongboxCanonicalV1.AppendToken(
                    builder,
                    prefix + "_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    values[index].ToCanonicalString());
            }
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

        private static int CheckedInt(long value)
        {
            if (value < int.MinValue || value > int.MaxValue)
            {
                throw new OverflowException("Hybrid strongbox value exceeds Int32 range.");
            }
            return (int)value;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Min(maximum, Math.Max(minimum, value));
        }
    }
}
