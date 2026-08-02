using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Rewards.Strongboxes
{
    /// <summary>
    /// Production BOX payload resolver for the hybrid policy. It selects one eligible
    /// rarity first, then selects one live gun definition inside that rarity using
    /// authored base weight and target-level affinity. It rolls the concrete item level
    /// and shared augment capacity/level, then creates an equipment instance with an
    /// empty installed-augment collection. Generated augment metadata is staged as
    /// immutable opening intent and committed only by RAP after equipment applies.
    /// Live opening and simulation share this exact resolver.
    /// </summary>
    public sealed class StrongboxHybridEquipmentGenerationResolver :
        IStrongboxEquipmentPayloadResolver
    {
        private sealed class Candidate
        {
            public Candidate(
                EquipmentDefinition equipment,
                GunDefinitionData gun,
                StableId rarityId,
                double weight)
            {
                Equipment = equipment;
                Gun = gun;
                RarityId = rarityId;
                Weight = weight;
            }

            public EquipmentDefinition Equipment { get; }
            public GunDefinitionData Gun { get; }
            public StableId RarityId { get; }
            public double Weight { get; }
        }

        private sealed class RarityPool
        {
            public RarityPool(StableId rarityId, int selectionWeight)
            {
                RarityId = rarityId
                    ?? throw new ArgumentNullException(nameof(rarityId));
                if (selectionWeight <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(selectionWeight));
                }

                SelectionWeight = selectionWeight;
                Candidates = new List<Candidate>();
            }

            public StableId RarityId { get; }
            public int SelectionWeight { get; }
            public List<Candidate> Candidates { get; }
            public double TotalDefinitionWeight { get; set; }
        }

        private static readonly StableId RaritySelectionPurposeId =
            StableId.Parse("strongbox-rng.hybrid-rarity-selection-v1");
        private static readonly StableId DefinitionSelectionPurposeId =
            StableId.Parse("strongbox-rng.hybrid-definition-selection-v1");
        private static readonly StableId QualitySelectionPurposeId =
            StableId.Parse("strongbox-rng.hybrid-quality-selection-v1");

        private readonly EquipmentCatalog equipmentCatalog;
        private readonly GunCatalog gunCatalog;
        private readonly GeneratedEquipmentAugmentSignatureState
            augmentSignatures;

        public StrongboxHybridEquipmentGenerationResolver(
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog,
            GeneratedEquipmentAugmentSignatureState augmentSignatures)
        {
            this.equipmentCatalog = equipmentCatalog
                ?? throw new ArgumentNullException(nameof(equipmentCatalog));
            this.gunCatalog = gunCatalog
                ?? throw new ArgumentNullException(nameof(gunCatalog));
            this.augmentSignatures = augmentSignatures
                ?? throw new ArgumentNullException(nameof(augmentSignatures));
        }

        public bool TryResolve(
            StrongboxDefinition definition,
            StrongboxInstanceContext boxContext,
            RewardOperationRequest operation,
            RewardGrant equipmentGrant,
            out IReadOnlyList<EquipmentInstance> equipmentInstances,
            out string rejectionCode)
        {
            equipmentInstances = Array.Empty<EquipmentInstance>();
            rejectionCode = null;
            if (definition == null
                || boxContext == null
                || operation == null
                || equipmentGrant == null)
            {
                rejectionCode = "strongbox-hybrid-equipment-input-null";
                return false;
            }
            if (definition.TierStableId != boxContext.TierStableId)
            {
                rejectionCode = "strongbox-hybrid-tier-context-mismatch";
                return false;
            }
            if (equipmentGrant.Kind != RewardGrantKind.EquipmentReference
                || equipmentGrant.Quantity < 1L
                || equipmentGrant.Quantity > int.MaxValue)
            {
                rejectionCode = "strongbox-hybrid-equipment-grant-invalid";
                return false;
            }

            StrongboxHybridLootPolicy policy;
            if (!StrongboxHybridLootCatalog.TryGet(
                    definition.TierStableId,
                    out policy)
                || policy == null)
            {
                rejectionCode = "strongbox-hybrid-policy-unavailable";
                return false;
            }
            int tierNumber = ResolveTierNumber(definition.TierStableId);
            if (tierNumber < 1)
            {
                rejectionCode = "strongbox-hybrid-tier-number-unavailable";
                return false;
            }

            int quantity = checked((int)equipmentGrant.Quantity);
            var generated = new List<EquipmentInstance>(quantity);
            var signatures = new List<GeneratedEquipmentAugmentSignature>(
                quantity);
            for (int slotIndex = 0; slotIndex < quantity; slotIndex++)
            {
                ulong slotOrdinal = (ulong)slotIndex;
                StrongboxTargetLevelRoll target;
                try
                {
                    target = policy.RollTargetLevel(
                        boxContext.ProgressionContext.CharacterLevel,
                        boxContext.RootSeed,
                        boxContext.AlgorithmVersion,
                        slotOrdinal);
                }
                catch (Exception exception)
                {
                    rejectionCode = "strongbox-hybrid-target-roll-exception-"
                        + exception.GetType().Name.ToLowerInvariant();
                    return false;
                }

                Candidate selected;
                if (!TrySelectCandidate(
                        policy,
                        target,
                        tierNumber,
                        boxContext,
                        slotOrdinal,
                        out selected,
                        out rejectionCode))
                {
                    return false;
                }

                StrongboxInstanceLevelRoll instanceLevel;
                try
                {
                    instanceLevel = policy.RollInstanceLevel(
                        target,
                        selected.Gun.PeakDropLevel,
                        selected.RarityId,
                        boxContext.RootSeed,
                        boxContext.AlgorithmVersion,
                        slotOrdinal);
                }
                catch (Exception exception)
                {
                    rejectionCode = "strongbox-hybrid-instance-level-exception-"
                        + exception.GetType().Name.ToLowerInvariant();
                    return false;
                }
                int itemLevel = Clamp(
                    instanceLevel.ItemLevel,
                    selected.Equipment.ItemLevelRange.Minimum,
                    selected.Equipment.ItemLevelRange.Maximum);

                StableId qualityId;
                if (!TrySelectQuality(
                        selected.Equipment,
                        boxContext,
                        slotOrdinal,
                        out qualityId,
                        out rejectionCode))
                {
                    return false;
                }

                StableId equipmentInstanceId = Strongbox.DeriveId(
                    "boxequipment",
                    operation.SourceOperationStableId.ToString(),
                    equipmentGrant.GrantStableId.ToString(),
                    slotIndex.ToString(CultureInfo.InvariantCulture));
                EquipmentInstance equipment = EquipmentInstance.Create(
                    equipmentInstanceId,
                    selected.Equipment.DefinitionId,
                    itemLevel,
                    qualityId,
                    Array.Empty<AugmentInstance>());

                StrongboxAugmentSignature rolledSignature;
                try
                {
                    rolledSignature = policy.RollAugmentSignature(
                        boxContext.ProgressionContext.CharacterLevel,
                        itemLevel,
                        selected.RarityId,
                        StrongboxHybridLootPolicy.AuthoredNormalGunSlots,
                        StrongboxHybridLootPolicy.AuthoredNormalGunSlots + 1,
                        boxContext.RootSeed,
                        boxContext.AlgorithmVersion,
                        slotOrdinal);
                }
                catch (Exception exception)
                {
                    rejectionCode =
                        "strongbox-hybrid-augment-signature-exception-"
                        + exception.GetType().Name.ToLowerInvariant();
                    return false;
                }

                generated.Add(equipment);
                signatures.Add(new GeneratedEquipmentAugmentSignature(
                    equipment.InstanceId,
                    boxContext.InstanceStableId,
                    policy.PolicyId,
                    rolledSignature.SlotCount,
                    rolledSignature.SharedLevel,
                    policy.Fingerprint,
                    boxContext.AlgorithmVersion));
            }

            string signatureDiagnostic;
            if (!augmentSignatures.TryStageBatch(
                    signatures,
                    out signatureDiagnostic))
            {
                rejectionCode = string.IsNullOrWhiteSpace(signatureDiagnostic)
                    ? "strongbox-hybrid-augment-signature-stage-rejected"
                    : signatureDiagnostic;
                return false;
            }
            equipmentInstances = generated.AsReadOnly();
            return true;
        }

        private bool TrySelectCandidate(
            StrongboxHybridLootPolicy policy,
            StrongboxTargetLevelRoll target,
            int tierNumber,
            StrongboxInstanceContext boxContext,
            ulong slotOrdinal,
            out Candidate selected,
            out string rejectionCode)
        {
            var rarityPools = new List<RarityPool>();
            bool topTier = tierNumber == StrongboxCatalog.Tiers.Count;
            for (int index = 0;
                 index < equipmentCatalog.EquipmentDefinitions.Count;
                 index++)
            {
                EquipmentDefinition equipment =
                    equipmentCatalog.EquipmentDefinitions[index];
                if (equipment == null
                    || equipment.CategoryId != EquipmentCategoryIds.Gun
                    || equipment.RuntimeGunReferenceId == null)
                {
                    continue;
                }

                GunDefinitionData gun;
                if (!TryResolveGun(equipment, out gun)
                    || gun == null
                    || gun.Availability != GunCatalogAvailability.Live
                    || (gun.TopBoxOnly && !topTier))
                {
                    continue;
                }

                StableId rarityId;
                if (!TryResolveRarity(gun.Rarity, out rarityId))
                {
                    continue;
                }

                int raritySelectionWeight;
                double definitionWeight;
                try
                {
                    raritySelectionWeight =
                        policy.GetRaritySelectionWeight(rarityId);
                    definitionWeight = policy.EvaluateDefinitionAffinity(
                        target,
                        gun.PeakDropLevel,
                        gun.FinalBaseWeight);
                }
                catch (ArgumentException)
                {
                    continue;
                }
                if (raritySelectionWeight <= 0
                    || double.IsNaN(definitionWeight)
                    || double.IsInfinity(definitionWeight)
                    || definitionWeight <= 0d)
                {
                    continue;
                }

                RarityPool pool = FindPool(rarityPools, rarityId);
                if (pool == null)
                {
                    pool = new RarityPool(
                        rarityId,
                        raritySelectionWeight);
                    rarityPools.Add(pool);
                }
                pool.Candidates.Add(new Candidate(
                    equipment,
                    gun,
                    rarityId,
                    definitionWeight));
                pool.TotalDefinitionWeight += definitionWeight;
            }

            if (rarityPools.Count == 0)
            {
                selected = null;
                rejectionCode = "strongbox-hybrid-no-eligible-definition";
                return false;
            }

            rarityPools.Sort(delegate(RarityPool left, RarityPool right)
            {
                return left.RarityId.CompareTo(right.RarityId);
            });
            ulong totalRarityWeight = 0UL;
            for (int index = 0; index < rarityPools.Count; index++)
            {
                RarityPool pool = rarityPools[index];
                pool.Candidates.Sort(delegate(Candidate left, Candidate right)
                {
                    return left.Equipment.DefinitionId.CompareTo(
                        right.Equipment.DefinitionId);
                });
                totalRarityWeight = checked(
                    totalRarityWeight + (ulong)pool.SelectionWeight);
            }

            DeterministicRandom rarityRandom =
                DeterministicRandom.CreateSubstream(
                    boxContext.RootSeed,
                    boxContext.AlgorithmVersion,
                    RaritySelectionPurposeId,
                    slotOrdinal);
            rarityRandom = rarityRandom.NextBoundedUInt64(
                totalRarityWeight,
                out ulong rarityThreshold);
            ulong rarityCursor = 0UL;
            RarityPool selectedPool = rarityPools[rarityPools.Count - 1];
            for (int index = 0; index < rarityPools.Count; index++)
            {
                rarityCursor = checked(
                    rarityCursor + (ulong)rarityPools[index].SelectionWeight);
                if (rarityThreshold < rarityCursor)
                {
                    selectedPool = rarityPools[index];
                    break;
                }
            }

            if (double.IsNaN(selectedPool.TotalDefinitionWeight)
                || double.IsInfinity(selectedPool.TotalDefinitionWeight)
                || selectedPool.TotalDefinitionWeight <= 0d)
            {
                selected = null;
                rejectionCode = "strongbox-hybrid-selected-rarity-empty";
                return false;
            }

            DeterministicRandom definitionRandom =
                DeterministicRandom.CreateSubstream(
                    boxContext.RootSeed,
                    boxContext.AlgorithmVersion,
                    DefinitionSelectionPurposeId,
                    slotOrdinal);
            definitionRandom = definitionRandom.NextUnitInterval(out double unit);
            double definitionThreshold =
                unit * selectedPool.TotalDefinitionWeight;
            double definitionCursor = 0d;
            selected = selectedPool.Candidates[
                selectedPool.Candidates.Count - 1];
            for (int index = 0;
                 index < selectedPool.Candidates.Count;
                 index++)
            {
                definitionCursor += selectedPool.Candidates[index].Weight;
                if (definitionThreshold < definitionCursor)
                {
                    selected = selectedPool.Candidates[index];
                    break;
                }
            }

            rejectionCode = null;
            return true;
        }

        private static RarityPool FindPool(
            IReadOnlyList<RarityPool> pools,
            StableId rarityId)
        {
            for (int index = 0; index < pools.Count; index++)
            {
                if (pools[index].RarityId == rarityId)
                {
                    return pools[index];
                }
            }
            return null;
        }

        private bool TryResolveGun(
            EquipmentDefinition equipment,
            out GunDefinitionData gun)
        {
            gun = null;
            if (equipment == null
                || equipment.RuntimeGunReferenceId == null)
            {
                return false;
            }

            string reference = GunDefinitionId.FromRuntimeReference(
                equipment.RuntimeGunReferenceId).Value;
            if (gunCatalog.TryGetDefinition(reference, out gun)
                && gun != null)
            {
                return true;
            }

            IReadOnlyList<GunDefinitionData> live =
                gunCatalog.GetDefinitions(GunCatalogContentFilter.LiveOnly);
            for (int index = 0; index < live.Count; index++)
            {
                GunDefinitionData candidate = live[index];
                StableId raw;
                if ((StableId.TryParse(candidate.DefinitionId, out raw)
                        && raw == equipment.RuntimeGunReferenceId)
                    || Strongbox.DeriveId(
                            "gun",
                            candidate.DefinitionId)
                        == equipment.RuntimeGunReferenceId)
                {
                    gun = candidate;
                    return true;
                }
            }
            gun = null;
            return false;
        }

        private static bool TrySelectQuality(
            EquipmentDefinition equipment,
            StrongboxInstanceContext boxContext,
            ulong slotOrdinal,
            out StableId qualityId,
            out string rejectionCode)
        {
            if (equipment.QualityTiers == null
                || equipment.QualityTiers.Count == 0)
            {
                qualityId = null;
                rejectionCode = "strongbox-hybrid-quality-unavailable";
                return false;
            }
            DeterministicRandom random = DeterministicRandom.CreateSubstream(
                boxContext.RootSeed,
                boxContext.AlgorithmVersion,
                QualitySelectionPurposeId,
                slotOrdinal);
            random = random.NextBoundedUInt64(
                (ulong)equipment.QualityTiers.Count,
                out ulong selectedIndex);
            qualityId = equipment.QualityTiers[(int)selectedIndex].QualityId;
            rejectionCode = null;
            return qualityId != null;
        }

        private static bool TryResolveRarity(
            string rarity,
            out StableId rarityId)
        {
            switch ((rarity ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "common":
                    rarityId = StrongboxDefinitionRarityIds.Common;
                    return true;
                case "uncommon":
                    rarityId = StrongboxDefinitionRarityIds.Uncommon;
                    return true;
                case "rare":
                    rarityId = StrongboxDefinitionRarityIds.Rare;
                    return true;
                case "epic":
                    rarityId = StrongboxDefinitionRarityIds.Epic;
                    return true;
                case "legendary":
                    rarityId = StrongboxDefinitionRarityIds.Legendary;
                    return true;
                case "mythic":
                    rarityId = StrongboxDefinitionRarityIds.Mythic;
                    return true;
                case "artifact":
                    rarityId = StrongboxDefinitionRarityIds.Artifact;
                    return true;
                default:
                    rarityId = null;
                    return false;
            }
        }

        private static int ResolveTierNumber(StableId tierStableId)
        {
            for (int index = 0;
                 index < StrongboxCatalog.Tiers.Count;
                 index++)
            {
                if (StrongboxCatalog.Tiers[index].TierStableId
                    == tierStableId)
                {
                    return index + 1;
                }
            }
            return 0;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
