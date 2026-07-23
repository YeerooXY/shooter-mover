using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Rewards.Strongboxes
{
    /// <summary>
    /// Production BOX payload resolver for the hybrid policy. It selects from the
    /// canonical weapon/equipment catalogs, rolls item-level and augment-signature
    /// metadata, and creates equipment with zero installed augments.
    /// </summary>
    public sealed class StrongboxHybridEquipmentGenerationResolverV1 :
        IStrongboxEquipmentPayloadResolverV1
    {
        private sealed class Candidate
        {
            public Candidate(
                EquipmentDefinition equipment,
                WeaponDefinitionData weapon,
                StableId rarityId,
                ulong weight)
            {
                Equipment = equipment;
                Weapon = weapon;
                RarityId = rarityId;
                Weight = weight;
            }

            public EquipmentDefinition Equipment { get; }
            public WeaponDefinitionData Weapon { get; }
            public StableId RarityId { get; }
            public ulong Weight { get; }
        }

        private static readonly StableId DefinitionSelectionPurposeId =
            StableId.Parse(
                "strongbox-rng.hybrid-definition-selection-v1");
        private readonly EquipmentCatalog equipmentCatalog;
        private readonly WeaponCatalog weaponCatalog;
        private readonly StableId catalogDefinitionSelectionPurposeId;
        private readonly GeneratedEquipmentAugmentSignatureAuthorityV1
            augmentSignatures;
        private readonly IWeaponDefinitionDropWeightPolicyV1
            definitionDropWeightPolicy;
        private readonly IWeaponDefinitionDropEligibilityPolicyV1
            definitionDropEligibilityPolicy;

        public StrongboxHybridEquipmentGenerationResolverV1(
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            GeneratedEquipmentAugmentSignatureAuthorityV1 augmentSignatures)
            : this(
                equipmentCatalog,
                weaponCatalog,
                augmentSignatures,
                WeaponDefinitionDropWeightPolicyV1.CreateBaselineV1(),
                WeaponDefinitionDropEligibilityPolicyV1.CreateBaselineV1())
        {
        }

        public StrongboxHybridEquipmentGenerationResolverV1(
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            GeneratedEquipmentAugmentSignatureAuthorityV1 augmentSignatures,
            IWeaponDefinitionDropWeightPolicyV1 dropWeightPolicy)
            : this(
                equipmentCatalog,
                weaponCatalog,
                augmentSignatures,
                dropWeightPolicy,
                WeaponDefinitionDropEligibilityPolicyV1.CreateBaselineV1())
        {
        }

        public StrongboxHybridEquipmentGenerationResolverV1(
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            GeneratedEquipmentAugmentSignatureAuthorityV1 augmentSignatures,
            IWeaponDefinitionDropWeightPolicyV1 dropWeightPolicy,
            IWeaponDefinitionDropEligibilityPolicyV1 dropEligibilityPolicy)
        {
            this.equipmentCatalog = equipmentCatalog
                ?? throw new ArgumentNullException(nameof(equipmentCatalog));
            this.weaponCatalog = weaponCatalog
                ?? throw new ArgumentNullException(nameof(weaponCatalog));
            this.augmentSignatures = augmentSignatures
                ?? throw new ArgumentNullException(nameof(augmentSignatures));
            definitionDropWeightPolicy = dropWeightPolicy
                ?? throw new ArgumentNullException(nameof(dropWeightPolicy));
            definitionDropEligibilityPolicy = dropEligibilityPolicy
                ?? throw new ArgumentNullException(nameof(dropEligibilityPolicy));
            catalogDefinitionSelectionPurposeId = StrongboxCanonicalV1.DeriveId(
                "strongboxrng",
                DefinitionSelectionPurposeId.ToString(),
                weaponCatalog.Fingerprint,
                equipmentCatalog.Fingerprint,
                definitionDropWeightPolicy.Fingerprint,
                definitionDropEligibilityPolicy.Fingerprint);
        }

        public bool TryResolve(
            StrongboxDefinitionV1 definition,
            StrongboxInstanceContextV1 boxContext,
            RewardOperationRequestV1 operation,
            RewardGrantV1 equipmentGrant,
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
                rejectionCode =
                    "strongbox-hybrid-equipment-input-null";
                return false;
            }
            if (definition.TierStableId != boxContext.TierStableId)
            {
                rejectionCode =
                    "strongbox-hybrid-tier-context-mismatch";
                return false;
            }
            if (equipmentGrant.Kind
                    != RewardGrantKindV1.EquipmentReference
                || equipmentGrant.Quantity < 1L
                || equipmentGrant.Quantity > int.MaxValue)
            {
                rejectionCode =
                    "strongbox-hybrid-equipment-grant-invalid";
                return false;
            }

            StrongboxHybridLootPolicyV1 policy;
            if (!ProductionStrongboxHybridLootCatalogV1.TryGet(
                    definition.TierStableId,
                    out policy)
                || policy == null)
            {
                rejectionCode =
                    "strongbox-hybrid-policy-unavailable";
                return false;
            }
            int tierNumber =
                ResolveTierNumber(definition.TierStableId);
            if (tierNumber < 1)
            {
                rejectionCode =
                    "strongbox-hybrid-tier-number-unavailable";
                return false;
            }

            int quantity = checked((int)equipmentGrant.Quantity);
            var generated =
                new List<EquipmentInstance>(quantity);
            var signatures =
                new List<GeneratedEquipmentAugmentSignatureV1>(
                    quantity);
            for (int slotIndex = 0;
                slotIndex < quantity;
                slotIndex++)
            {
                ulong slotOrdinal = (ulong)slotIndex;
                StrongboxTargetLevelRollV1 target;
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
                    rejectionCode =
                        "strongbox-hybrid-target-roll-exception-"
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

                StrongboxInstanceLevelRollV1 instanceLevel;
                try
                {
                    instanceLevel = policy.RollInstanceLevel(
                        target,
                        selected.Weapon.PeakDropLevel,
                        selected.RarityId,
                        boxContext.RootSeed,
                        boxContext.AlgorithmVersion,
                        slotOrdinal);
                }
                catch (Exception exception)
                {
                    rejectionCode =
                        "strongbox-hybrid-instance-level-exception-"
                        + exception.GetType().Name.ToLowerInvariant();
                    return false;
                }
                int itemLevel = Clamp(
                    instanceLevel.ItemLevel,
                    selected.Equipment.ItemLevelRange.Minimum,
                    selected.Equipment.ItemLevelRange.Maximum);

                StableId qualityId;
                if (!TryResolveAuthoredQuality(
                        selected.Equipment,
                        out qualityId,
                        out rejectionCode))
                {
                    return false;
                }

                StableId equipmentInstanceId =
                    StrongboxCanonicalV1.DeriveId(
                        "boxequipment",
                        operation.SourceOperationStableId.ToString(),
                        equipmentGrant.GrantStableId.ToString(),
                        slotIndex.ToString(
                            CultureInfo.InvariantCulture));
                EquipmentInstance equipment =
                    EquipmentInstance.Create(
                        equipmentInstanceId,
                        selected.Equipment.DefinitionId,
                        itemLevel,
                        qualityId,
                        Array.Empty<AugmentInstance>());

                StrongboxAugmentSignatureV1 rolledSignature;
                try
                {
                    rolledSignature = policy.RollAugmentSignature(
                        boxContext.ProgressionContext.CharacterLevel,
                        itemLevel,
                        selected.RarityId,
                        StrongboxHybridLootPolicyV1
                            .AuthoredNormalWeaponSlots,
                        StrongboxHybridLootPolicyV1
                            .AuthoredNormalWeaponSlots + 1,
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
                signatures.Add(
                    new GeneratedEquipmentAugmentSignatureV1(
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
                rejectionCode =
                    string.IsNullOrWhiteSpace(signatureDiagnostic)
                        ? "strongbox-hybrid-augment-signature-stage-rejected"
                        : signatureDiagnostic;
                return false;
            }
            equipmentInstances = generated.AsReadOnly();
            return true;
        }

        private bool TrySelectCandidate(
            StrongboxHybridLootPolicyV1 policy,
            StrongboxTargetLevelRollV1 target,
            int tierNumber,
            StrongboxInstanceContextV1 boxContext,
            ulong slotOrdinal,
            out Candidate selected,
            out string rejectionCode)
        {
            var candidates = new List<Candidate>();
            ulong totalWeight = 0UL;
            int topTierNumber = ProductionStrongboxCatalogV1.Tiers.Count;
            for (int index = 0; index < equipmentCatalog.EquipmentDefinitions.Count; index++)
            {
                EquipmentDefinition equipment = equipmentCatalog.EquipmentDefinitions[index];
                if (equipment == null
                    || equipment.CategoryId != EquipmentCategoryIds.Weapon
                    || equipment.RuntimeWeaponReferenceId == null)
                {
                    continue;
                }

                WeaponDefinitionData weapon;
                if (!TryResolveWeapon(equipment, out weapon)
                    || weapon == null
                    || !definitionDropEligibilityPolicy.IsEligible(
                        new WeaponDefinitionDropEligibilityContextV1(
                            weapon,
                            tierNumber,
                            topTierNumber)))
                {
                    continue;
                }

                StableId rarityId;
                if (!TryResolveRarity(weapon.Rarity, out rarityId))
                {
                    continue;
                }

                ulong weight;
                try
                {
                    weight = definitionDropWeightPolicy.EvaluateWeightUnits(
                        new WeaponDefinitionDropWeightContextV1(
                            policy,
                            tierNumber,
                            target,
                            weapon,
                            rarityId));
                    totalWeight = checked(totalWeight + weight);
                }
                catch (ArgumentException)
                {
                    continue;
                }
                catch (OverflowException)
                {
                    selected = null;
                    rejectionCode = "strongbox-hybrid-definition-weight-overflow";
                    return false;
                }

                candidates.Add(new Candidate(equipment, weapon, rarityId, weight));
            }

            candidates.Sort(delegate(Candidate left, Candidate right)
            {
                return left.Equipment.DefinitionId.CompareTo(right.Equipment.DefinitionId);
            });
            if (candidates.Count == 0 || totalWeight == 0UL)
            {
                selected = null;
                rejectionCode = "strongbox-hybrid-no-eligible-definition";
                return false;
            }

            DeterministicRandom random = DeterministicRandom.CreateSubstream(
                boxContext.RootSeed,
                boxContext.AlgorithmVersion,
                catalogDefinitionSelectionPurposeId,
                slotOrdinal);
            random = random.NextBoundedUInt64(totalWeight, out ulong threshold);
            ulong cursor = 0UL;
            selected = candidates[candidates.Count - 1];
            for (int index = 0; index < candidates.Count; index++)
            {
                cursor = checked(cursor + candidates[index].Weight);
                if (threshold < cursor)
                {
                    selected = candidates[index];
                    break;
                }
            }
            rejectionCode = null;
            return true;
        }

        private bool TryResolveWeapon(
            EquipmentDefinition equipment,
            out WeaponDefinitionData weapon)
        {
            weapon = null;
            if (equipment == null
                || equipment.RuntimeWeaponReferenceId == null)
            {
                return false;
            }

            string definitionId;
            if (CanonicalWeaponCatalogProjectionV1
                .TryResolveDefinitionId(
                    weaponCatalog,
                    equipment.RuntimeWeaponReferenceId,
                    out definitionId)
                && weaponCatalog.TryGetDefinition(
                    definitionId,
                    out weapon)
                && weapon != null)
            {
                return true;
            }

            string reference =
                equipment.RuntimeWeaponReferenceId.ToString();
            if (weaponCatalog.TryGetDefinition(
                    reference,
                    out weapon)
                && weapon != null)
            {
                return true;
            }

            IReadOnlyList<WeaponDefinitionData> live =
                weaponCatalog.GetDefinitions(
                    WeaponCatalogContentFilter.LiveOnly);
            for (int index = 0; index < live.Count; index++)
            {
                WeaponDefinitionData candidate = live[index];
                StableId raw;
                if ((StableId.TryParse(
                            candidate.DefinitionId,
                            out raw)
                        && raw
                            == equipment.RuntimeWeaponReferenceId)
                    || StrongboxCanonicalV1.DeriveId(
                            "weapon",
                            candidate.DefinitionId)
                        == equipment.RuntimeWeaponReferenceId)
                {
                    weapon = candidate;
                    return true;
                }
            }
            weapon = null;
            return false;
        }

        private static bool TryResolveAuthoredQuality(
            EquipmentDefinition equipment,
            out StableId qualityId,
            out string rejectionCode)
        {
            if (equipment == null
                || equipment.QualityTiers == null
                || equipment.QualityTiers.Count != 1
                || equipment.QualityTiers[0] == null
                || equipment.QualityTiers[0].QualityId == null)
            {
                qualityId = null;
                rejectionCode = "strongbox-hybrid-authored-quality-invalid";
                return false;
            }
            qualityId = equipment.QualityTiers[0].QualityId;
            rejectionCode = null;
            return true;
        }

        private static bool TryResolveRarity(
            string rarity,
            out StableId rarityId)
        {
            switch ((rarity ?? string.Empty)
                .Trim()
                .ToLowerInvariant())
            {
                case "common":
                case "uncommon":
                    rarityId =
                        StrongboxDefinitionRarityIdsV1.Common;
                    return true;
                case "rare":
                    rarityId =
                        StrongboxDefinitionRarityIdsV1.Rare;
                    return true;
                case "epic":
                    rarityId =
                        StrongboxDefinitionRarityIdsV1.Epic;
                    return true;
                case "legendary":
                    rarityId =
                        StrongboxDefinitionRarityIdsV1.Legendary;
                    return true;
                case "mythic":
                case "artifact":
                case "mythicartifact":
                    rarityId =
                        StrongboxDefinitionRarityIdsV1
                            .MythicArtifact;
                    return true;
                default:
                    rarityId = null;
                    return false;
            }
        }

        private static int ResolveTierNumber(
            StableId tierStableId)
        {
            for (int index = 0;
                index < ProductionStrongboxCatalogV1.Tiers.Count;
                index++)
            {
                if (ProductionStrongboxCatalogV1
                    .Tiers[index]
                    .TierStableId == tierStableId)
                {
                    return index + 1;
                }
            }
            return 0;
        }

        private static int Clamp(
            int value,
            int minimum,
            int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }
            return value > maximum ? maximum : value;
        }
    }
}
