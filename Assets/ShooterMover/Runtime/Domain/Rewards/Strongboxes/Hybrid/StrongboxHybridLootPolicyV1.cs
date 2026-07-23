using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    /// <summary>
    /// Engine-neutral, deterministic strongbox policy foundation. A tier rolls a
    /// triangular target around the player, then owns instance-level and augment-signature
    /// metadata. Definition eligibility and soft-tail selection weights are composed by
    /// the Application layer from authored weapon data; this policy has no bounded
    /// definition-distance table or hidden unlock radius.
    /// </summary>
    public sealed class StrongboxHybridLootPolicyV1 :
        IEquatable<StrongboxHybridLootPolicyV1>
    {
        public const int RarityMultiplierScale = 1000;
        public const int BlendScale = 1000;
        public const int AuthoredNormalWeaponSlots = 3;
        public const int NormalMaximumAugmentLevel = 10;

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

        public int ResolveDefinitionRaritySelectionMultiplierMilli(
            StableId rarityId)
        {
            return RequireRarity(rarityId).SelectionMultiplierMilli;
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
