using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
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

        private readonly ReadOnlyCollection<StrongboxDistanceWeightV1>
            definitionBellWeights;
        private readonly ReadOnlyCollection<StrongboxWeightedIntOutcomeV1>
            instanceLevelOffsets;
        private readonly ReadOnlyCollection<StrongboxWeightedIntOutcomeV1>
            augmentSlotOutcomes;
        private readonly ReadOnlyCollection<StrongboxWeightedIntOutcomeV1>
            augmentLevelOutcomes;
        private readonly ReadOnlyCollection<StrongboxRarityProfileV1>
            rarityProfiles;
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
            this.definitionBellWeights =
                StrongboxHybridLootPolicyValidationV1.CopyDistanceWeights(
                    definitionBellWeights);
            this.instanceLevelOffsets =
                StrongboxHybridLootPolicyValidationV1.CopyOutcomes(
                    instanceLevelOffsets,
                    nameof(instanceLevelOffsets),
                    int.MinValue);
            this.augmentSlotOutcomes =
                StrongboxHybridLootPolicyValidationV1.CopyOutcomes(
                    augmentSlotOutcomes,
                    nameof(augmentSlotOutcomes),
                    0);
            this.augmentLevelOutcomes =
                StrongboxHybridLootPolicyValidationV1.CopyOutcomes(
                    augmentLevelOutcomes,
                    nameof(augmentLevelOutcomes),
                    1);
            Dictionary<StableId, StrongboxRarityProfileV1> rarityMap;
            this.rarityProfiles =
                StrongboxHybridLootPolicyValidationV1.CopyRarities(
                    rarityProfiles,
                    out rarityMap);
            rarityById = rarityMap;

            DefinitionSelectionRadius = this.definitionBellWeights.Count - 1;
            StrongboxHybridLootPolicyValidationV1.ValidateOutcomeValues(
                this.augmentSlotOutcomes,
                this.augmentLevelOutcomes);
            canonicalText =
                StrongboxHybridLootPolicyValidationV1.BuildCanonicalText(
                    PolicyId,
                    MinimumTargetDelta,
                    MostLikelyTargetDelta,
                    MaximumTargetDelta,
                    TargetBlendPermille,
                    this.definitionBellWeights,
                    this.instanceLevelOffsets,
                    this.augmentSlotOutcomes,
                    this.augmentLevelOutcomes,
                    this.rarityProfiles);
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

            return StrongboxHybridLootRandomV1.RollTargetLevel(
                this,
                playerLevel,
                rootSeed,
                algorithmVersion,
                equipmentSlotOrdinal);
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

            return StrongboxHybridLootRandomV1.RollInstanceLevel(
                this,
                targetRoll,
                definitionPeakLevel,
                rarityId,
                instanceLevelOffsets,
                rootSeed,
                algorithmVersion,
                equipmentSlotOrdinal);
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

            return StrongboxHybridLootRandomV1.RollAugmentSignature(
                this,
                playerLevel,
                itemLevel,
                RequireRarity(rarityId),
                normalMaximumSlots,
                absoluteMaximumSlots,
                augmentSlotOutcomes,
                augmentLevelOutcomes,
                rootSeed,
                algorithmVersion,
                equipmentSlotOrdinal);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(StrongboxHybridLootPolicyV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
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
    }
}
