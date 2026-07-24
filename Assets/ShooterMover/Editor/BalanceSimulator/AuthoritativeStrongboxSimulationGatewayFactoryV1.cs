using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Rewards.Strongboxes.Simulation;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Editor.BalanceSimulator
{
    /// <summary>
    /// Canonical editor composition for the authoritative simulator gateway. Metadata and
    /// production fingerprints are projected from the exact catalogs consumed by the live
    /// hybrid resolver; callers cannot hand-author a competing eligibility or rarity view.
    /// </summary>
    public static class AuthoritativeStrongboxSimulationGatewayFactoryV1
    {
        public static bool TryCreate(
            string weaponCatalogJson,
            out AuthoritativeStrongboxSimulationProductionGatewayV1 gateway,
            out string diagnostic)
        {
            gateway = null;
            diagnostic = string.Empty;

            LootboxSimulatorRuntimeV1 runtime;
            if (!LootboxSimulatorRuntimeV1.TryCreate(
                    weaponCatalogJson,
                    out runtime,
                    out diagnostic)
                || runtime == null)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "strongbox-simulation-production-catalog-create-rejected"
                    : diagnostic;
                return false;
            }

            try
            {
                IReadOnlyList<StrongboxEquipmentMetadata> metadata =
                    BuildMetadata(runtime.EquipmentCatalog, runtime.WeaponCatalog);
                StrongboxProductionFingerprints fingerprints =
                    BuildFingerprints(weaponCatalogJson, metadata);
                gateway = new AuthoritativeStrongboxSimulationProductionGatewayV1(
                    weaponCatalogJson,
                    fingerprints,
                    metadata);
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = "strongbox-simulation-production-projection-exception-"
                    + exception.GetType().Name.ToLowerInvariant();
                gateway = null;
                return false;
            }
        }

        private static IReadOnlyList<StrongboxEquipmentMetadata> BuildMetadata(
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog)
        {
            var values = new List<StrongboxEquipmentMetadata>();
            for (int index = 0;
                 index < equipmentCatalog.EquipmentDefinitions.Count;
                 index++)
            {
                EquipmentDefinition equipment =
                    equipmentCatalog.EquipmentDefinitions[index];
                if (equipment == null
                    || equipment.CategoryId != EquipmentCategoryIds.Weapon
                    || equipment.RuntimeWeaponReferenceId == null)
                {
                    continue;
                }

                WeaponDefinitionData weapon;
                if (!TryResolveWeapon(weaponCatalog, equipment, out weapon)
                    || weapon == null
                    || weapon.Availability != WeaponCatalogAvailability.Live)
                {
                    continue;
                }

                StableId rarityId;
                if (!TryResolveRarity(weapon.Rarity, out rarityId))
                {
                    throw new InvalidOperationException(
                        "Live strongbox weapon has unsupported rarity: "
                        + weapon.DefinitionId + " / " + weapon.Rarity);
                }

                values.Add(new StrongboxEquipmentMetadata(
                    equipment.DefinitionId,
                    weapon.DisplayName,
                    equipment.CategoryId,
                    StrongboxCanonicalV1.DeriveId("weaponfamily", weapon.FamilyId),
                    null,
                    Array.Empty<StableId>(),
                    rarityId,
                    Math.Max(1, weapon.FirstAppearance),
                    Math.Max(1, weapon.PeakDropLevel),
                    weapon.FinalBaseWeight,
                    true,
                    weapon.TopBoxOnly,
                    StrongboxHybridLootPolicyV1.AuthoredNormalWeaponSlots,
                    StrongboxHybridLootPolicyV1.AuthoredNormalWeaponSlots + 1,
                    StrongboxHybridLootPolicyV1.NormalMaximumAugmentLevel,
                    ResolveAbsoluteMaximumAugmentLevel()));
            }

            values.Sort(delegate(
                StrongboxEquipmentMetadata left,
                StrongboxEquipmentMetadata right)
            {
                return left.DefinitionId.CompareTo(right.DefinitionId);
            });
            if (values.Count == 0)
            {
                throw new InvalidOperationException(
                    "The production strongbox metadata projection is empty.");
            }
            return values.AsReadOnly();
        }

        private static StrongboxProductionFingerprints BuildFingerprints(
            string weaponCatalogJson,
            IReadOnlyList<StrongboxEquipmentMetadata> metadata)
        {
            string equipmentCatalog = StrongboxCanonicalV1.Fingerprint(
                "strongbox-simulation-equipment-catalog-v1|"
                + weaponCatalogJson);

            var projection = new StringBuilder(
                "strongbox-simulation-equipment-projection-v1");
            for (int index = 0; index < metadata.Count; index++)
            {
                StrongboxEquipmentMetadata value = metadata[index];
                projection.Append('\n')
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append('|').Append(value.DefinitionId)
                    .Append('|').Append(value.DisplayName)
                    .Append('|').Append(value.CategoryId)
                    .Append('|').Append(value.FamilyId)
                    .Append('|').Append(value.RarityId)
                    .Append('|').Append(value.FirstAppearanceLevel)
                    .Append('|').Append(value.AnchorLevel)
                    .Append('|').Append(value.AuthoredBaseWeight.ToString(
                        "R",
                        CultureInfo.InvariantCulture))
                    .Append('|').Append(value.Available)
                    .Append('|').Append(value.TopBoxOnly)
                    .Append('|').Append(value.OrdinaryMaximumSlots)
                    .Append('|').Append(value.AbsoluteMaximumSlots)
                    .Append('|').Append(value.OrdinaryMaximumAugmentLevel)
                    .Append('|').Append(value.AbsoluteMaximumAugmentLevel);
            }
            string equipmentProjection = StrongboxCanonicalV1.Fingerprint(
                projection.ToString());

            var policies = new StringBuilder(
                "strongbox-simulation-hybrid-policies-v1");
            for (int index = 0;
                 index < ProductionStrongboxCatalogV1.Tiers.Count;
                 index++)
            {
                ProductionStrongboxTierV1 tier =
                    ProductionStrongboxCatalogV1.Tiers[index];
                StrongboxHybridLootPolicyV1 policy =
                    ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(
                        tier.TierNumber);
                policies.Append('\n')
                    .Append(tier.TierStableId)
                    .Append('|')
                    .Append(policy.Fingerprint);
            }
            string strongboxPolicy = StrongboxCanonicalV1.Fingerprint(
                policies.ToString());

            return new StrongboxProductionFingerprints(
                equipmentCatalog,
                equipmentProjection,
                strongboxPolicy,
                StrongboxCanonicalV1.Fingerprint(
                    "strongbox-simulation-rarity-policy-v1|" + strongboxPolicy),
                StrongboxCanonicalV1.Fingerprint(
                    "strongbox-simulation-item-level-policy-v1|" + strongboxPolicy),
                StrongboxCanonicalV1.Fingerprint(
                    "strongbox-simulation-augment-slot-policy-v1|" + strongboxPolicy),
                StrongboxCanonicalV1.Fingerprint(
                    "strongbox-simulation-augment-level-policy-v1|" + strongboxPolicy));
        }

        private static int ResolveAbsoluteMaximumAugmentLevel()
        {
            int maximum = StrongboxHybridLootPolicyV1.NormalMaximumAugmentLevel;
            for (int tierIndex = 0;
                 tierIndex < ProductionStrongboxCatalogV1.Tiers.Count;
                 tierIndex++)
            {
                StrongboxHybridLootPolicyV1 policy =
                    ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(
                        ProductionStrongboxCatalogV1.Tiers[tierIndex].TierNumber);
                for (int outcomeIndex = 0;
                     outcomeIndex < policy.AugmentLevelOutcomes.Count;
                     outcomeIndex++)
                {
                    maximum = Math.Max(
                        maximum,
                        policy.AugmentLevelOutcomes[outcomeIndex].Value);
                }
            }
            return maximum;
        }

        private static bool TryResolveWeapon(
            WeaponCatalog weaponCatalog,
            EquipmentDefinition equipment,
            out WeaponDefinitionData weapon)
        {
            string reference = equipment.RuntimeWeaponReferenceId.ToString();
            if (weaponCatalog.TryGetDefinition(reference, out weapon)
                && weapon != null)
            {
                return true;
            }

            IReadOnlyList<WeaponDefinitionData> live =
                weaponCatalog.GetDefinitions(WeaponCatalogContentFilter.LiveOnly);
            for (int index = 0; index < live.Count; index++)
            {
                WeaponDefinitionData candidate = live[index];
                StableId raw;
                if ((StableId.TryParse(candidate.DefinitionId, out raw)
                        && raw == equipment.RuntimeWeaponReferenceId)
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

        private static bool TryResolveRarity(
            string rarity,
            out StableId rarityId)
        {
            switch ((rarity ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "common":
                    rarityId = StrongboxDefinitionRarityIdsV1.Common;
                    return true;
                case "uncommon":
                    rarityId = StrongboxDefinitionRarityIdsV1.Uncommon;
                    return true;
                case "rare":
                    rarityId = StrongboxDefinitionRarityIdsV1.Rare;
                    return true;
                case "epic":
                    rarityId = StrongboxDefinitionRarityIdsV1.Epic;
                    return true;
                case "legendary":
                    rarityId = StrongboxDefinitionRarityIdsV1.Legendary;
                    return true;
                case "artifact":
                    rarityId = StrongboxDefinitionRarityIdsV1.Artifact;
                    return true;
                default:
                    rarityId = null;
                    return false;
            }
        }
    }
}
