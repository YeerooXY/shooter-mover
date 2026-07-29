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
    public sealed class StrongboxHybridLootPolicy :
        IEquatable<StrongboxHybridLootPolicy>
    {
        public const int DefinitionWeightScale = 1000000;
        public const int RarityMultiplierScale = 1000;
        public const int BlendScale = 1000;
        public const int AuthoredNormalWeaponSlots = 3;
        public const int NormalMaximumAugmentLevel = 10;

        private readonly ReadOnlyCollection<StrongboxDistanceWeight>
            definitionBellWeights;
        private readonly ReadOnlyCollection<StrongboxWeightedIntOutcome>
            instanceLevelOffsets;
        private readonly ReadOnlyCollection<StrongboxWeightedIntOutcome>
            augmentSlotOutcomes;
        private readonly ReadOnlyCollection<StrongboxWeightedIntOutcome>
            augmentLevelOutcomes;
        private readonly ReadOnlyCollection<StrongboxRarityProfile>
            rarityProfiles;
        private readonly Dictionary<StableId, StrongboxRarityProfile> rarityById;
        private readonly string canonicalText;

        private StrongboxHybridLootPolicy(
            StableId policyId,
            int minimumTargetDelta,
            int mostLikelyTargetDelta,
            int maximumTargetDelta,
            int targetBlendPermille,
            IEnumerable<StrongboxDistanceWeight> definitionBellWeights,
            IEnumerable<StrongboxWeightedIntOutcome> instanceLevelOffsets,
            IEnumerable<StrongboxWeightedIntOutcome> augmentSlotOutcomes,
            IEnumerable<StrongboxWeightedIntOutcome> augmentLevelOutcomes,
            IEnumerable<StrongboxRarityProfile> rarityProfiles)
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
                StrongboxHybridLootPolicyValidation.CopyDistanceWeights(
                    definitionBellWeights);
            this.instanceLevelOffsets =
                StrongboxHybridLootPolicyValidation.CopyOutcomes(
                    instanceLevelOffsets,
                    nameof(instanceLevelOffsets),
                    int.MinValue);
            this.augmentSlotOutcomes =
                StrongboxHybridLootPolicyValidation.CopyOutcomes(
                    augmentSlotOutcomes,
                    nameof(augmentSlotOutcomes),
                    0);
            this.augmentLevelOutcomes =
                StrongboxHybridLootPolicyValidation.CopyOutcomes(
                    augmentLevelOutcomes,
                    nameof(augmentLevelOutcomes),
                    1);
            Dictionary<StableId, StrongboxRarityProfile> rarityMap;
            this.rarityProfiles =
                StrongboxHybridLootPolicyValidation.CopyRarities(
                    rarityProfiles,
                    out rarityMap);
            rarityById = rarityMap;

            DefinitionSelectionRadius = this.definitionBellWeights.Count - 1;
            StrongboxHybridLootPolicyValidation.ValidateOutcomeValues(
                this.augmentSlotOutcomes,
                this.augmentLevelOutcomes);
            canonicalText =
                StrongboxHybridLootPolicyValidation.BuildCanonicalText(
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
            Fingerprint = Strongbox.Fingerprint(canonicalText);
        }

        public StableId PolicyId { get; }
        public int MinimumTargetDelta { get; }
        public int MostLikelyTargetDelta { get; }
        public int MaximumTargetDelta { get; }
        public int TargetBlendPermille { get; }
        public int DefinitionSelectionRadius { get; }
        public IReadOnlyList<StrongboxDistanceWeight> DefinitionBellWeights
        {
            get { return definitionBellWeights; }
        }
        public IReadOnlyList<StrongboxWeightedIntOutcome> InstanceLevelOffsets
        {
            get { return instanceLevelOffsets; }
        }
        public IReadOnlyList<StrongboxWeightedIntOutcome> AugmentSlotOutcomes
        {
            get { return augmentSlotOutcomes; }
        }
        public IReadOnlyList<StrongboxWeightedIntOutcome> AugmentLevelOutcomes
        {
            get { return augmentLevelOutcomes; }
        }
        public IReadOnlyList<StrongboxRarityProfile> RarityProfiles
        {
            get { return rarityProfiles; }
        }
        public string Fingerprint { get; }

        public static StrongboxHybridLootPolicy Create(
            StableId policyId,
            int minimumTargetDelta,
            int mostLikelyTargetDelta,
            int maximumTargetDelta,
            int targetBlendPermille,
            IEnumerable<StrongboxDistanceWeight> definitionBellWeights,
            IEnumerable<StrongboxWeightedIntOutcome> instanceLevelOffsets,
            IEnumerable<StrongboxWeightedIntOutcome> augmentSlotOutcomes,
            IEnumerable<StrongboxWeightedIntOutcome> augmentLevelOutcomes,
            IEnumerable<StrongboxRarityProfile> rarityProfiles)
        {
            return new StrongboxHybridLootPolicy(
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

        public StrongboxTargetLevelRoll RollTargetLevel(
            int playerLevel,
            ulong rootSeed,
            int algorithmVersion,
            ulong equipmentSlotOrdinal)
        {
            if (playerLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }

            return StrongboxHybridLootRandom.RollTargetLevel(
                this,
                playerLevel,
                rootSeed,
                algorithmVersion,
                equipmentSlotOrdinal);
        }

        public double EvaluateDefinitionWeight(
            StrongboxTargetLevelRoll targetRoll,
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

            StrongboxRarityProfile rarity = RequireRarity(rarityId);
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

        public StrongboxInstanceLevelRoll RollInstanceLevel(
            StrongboxTargetLevelRoll targetRoll,
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

            return StrongboxHybridLootRandom.RollInstanceLevel(
                this,
                targetRoll,
                definitionPeakLevel,
                rarityId,
                instanceLevelOffsets,
                rootSeed,
                algorithmVersion,
                equipmentSlotOrdinal);
        }

        public StrongboxAugmentSignature RollAugmentSignature(
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

            return StrongboxHybridLootRandom.RollAugmentSignature(
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

        public bool Equals(StrongboxHybridLootPolicy other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxHybridLootPolicy);
        }

        public override int GetHashCode()
        {
            return Strongbox.DeterministicHash(canonicalText);
        }

        private void RequireTargetRoll(StrongboxTargetLevelRoll targetRoll)
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

        private StrongboxRarityProfile RequireRarity(StableId rarityId)
        {
            StrongboxRarityProfile profile;
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
