using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Rewards.Strongboxes.Simulation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Editor.BalanceSimulator
{
    /// <summary>
    /// Canonical editor composition for the authoritative simulator gateway. Metadata and
    /// production fingerprints are projected from the exact catalogs consumed by the live
    /// hybrid resolver; callers cannot hand-author a competing eligibility or rarity view.
    /// </summary>
    public static class AuthoritativeStrongboxSimulationGatewayFactory
    {
        public static bool TryCreate(
            string gunCatalogJson,
            out AuthoritativeStrongboxSimulationGateway gateway,
            out string diagnostic)
        {
            gateway = null;
            diagnostic = string.Empty;
            LootboxSimulatorLive runtime;
            if (!LootboxSimulatorLive.TryCreate(
                    gunCatalogJson,
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
                    BuildMetadata(runtime.EquipmentCatalog, runtime.GunCatalog);
                StrongboxFingerprints fingerprints =
                    BuildFingerprints(gunCatalogJson, metadata);
                gateway = new AuthoritativeStrongboxSimulationGateway(
                    gunCatalogJson,
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
            GunCatalog gunCatalog)
        {
            var values = new List<StrongboxEquipmentMetadata>();
            for (int index = 0; index < equipmentCatalog.EquipmentDefinitions.Count; index++)
            {
                EquipmentDefinition equipment = equipmentCatalog.EquipmentDefinitions[index];
                if (equipment == null
                    || equipment.CategoryId != EquipmentCategoryIds.Gun
                    || equipment.RuntimeGunReferenceId == null)
                    continue;

                GunDefinitionData gun;
                if (!TryResolveGun(gunCatalog, equipment, out gun)
                    || gun == null
                    || gun.Availability != GunCatalogAvailability.Live)
                    continue;

                StableId rarityId;
                if (!TryResolveRarity(gun.Rarity, out rarityId))
                    throw new InvalidOperationException(
                        "Live strongbox gun has unsupported rarity: "
                        + gun.DefinitionId + " / " + gun.Rarity);

                values.Add(new StrongboxEquipmentMetadata(
                    equipment.DefinitionId,
                    gun.DisplayName,
                    equipment.CategoryId,
                    Strongbox.DeriveId("gunfamily", gun.FamilyId),
                    null,
                    Array.Empty<StableId>(),
                    rarityId,
                    Math.Max(1, gun.FirstAppearance),
                    Math.Max(1, gun.PeakDropLevel),
                    gun.FinalBaseWeight,
                    true,
                    gun.TopBoxOnly,
                    StrongboxHybridLootPolicy.AuthoredNormalGunSlots,
                    StrongboxHybridLootPolicy.AuthoredNormalGunSlots + 1,
                    StrongboxHybridLootPolicy.NormalMaximumAugmentLevel,
                    ResolveAbsoluteMaximumAugmentLevel()));
            }

            values.Sort(delegate(StrongboxEquipmentMetadata left, StrongboxEquipmentMetadata right)
            {
                return left.DefinitionId.CompareTo(right.DefinitionId);
            });
            if (values.Count == 0)
                throw new InvalidOperationException(
                    "The production strongbox metadata projection is empty.");
            return values.AsReadOnly();
        }

        private static StrongboxFingerprints BuildFingerprints(
            string gunCatalogJson,
            IReadOnlyList<StrongboxEquipmentMetadata> metadata)
        {
            string equipmentCatalog = Strongbox.Fingerprint(
                "strongbox-simulation-equipment-catalog-v1|" + gunCatalogJson);
            var projection = new StringBuilder(
                "strongbox-simulation-equipment-projection-v2");
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
                    .Append('|').Append(BitConverter.DoubleToInt64Bits(value.AuthoredBaseWeight)
                        .ToString("x16", CultureInfo.InvariantCulture))
                    .Append('|').Append(value.Available)
                    .Append('|').Append(value.TopBoxOnly)
                    .Append('|').Append(value.OrdinaryMaximumSlots)
                    .Append('|').Append(value.AbsoluteMaximumSlots)
                    .Append('|').Append(value.OrdinaryMaximumAugmentLevel)
                    .Append('|').Append(value.AbsoluteMaximumAugmentLevel);
            }
            string equipmentProjection = Strongbox.Fingerprint(
                projection.ToString());

            var policies = new StringBuilder(
                "strongbox-simulation-hybrid-policies-v1");
            for (int index = 0; index < StrongboxCatalog.Tiers.Count; index++)
            {
                StrongboxTier tier = StrongboxCatalog.Tiers[index];
                StrongboxHybridLootPolicy policy =
                    StrongboxHybridLootCatalog.GetByTierNumber(tier.TierNumber);
                policies.Append('\n')
                    .Append(tier.TierStableId)
                    .Append('|')
                    .Append(policy.Fingerprint);
            }
            string hybridPolicyAuthority = Strongbox.Fingerprint(
                policies.ToString());

            // Rarity weighting, item-level rolls, augment-slot rolls and augment-level
            // rolls are all decisions of StrongboxHybridLootPolicy today. These named
            // fields intentionally expose the same authority fingerprint rather than
            // manufacturing the appearance of four independent production authorities.
            return new StrongboxFingerprints(
                equipmentCatalog,
                equipmentProjection,
                hybridPolicyAuthority,
                hybridPolicyAuthority,
                hybridPolicyAuthority,
                hybridPolicyAuthority,
                hybridPolicyAuthority);
        }

        private static int ResolveAbsoluteMaximumAugmentLevel()
        {
            int maximum = StrongboxHybridLootPolicy.NormalMaximumAugmentLevel;
            for (int tierIndex = 0; tierIndex < StrongboxCatalog.Tiers.Count; tierIndex++)
            {
                StrongboxHybridLootPolicy policy =
                    StrongboxHybridLootCatalog.GetByTierNumber(
                        StrongboxCatalog.Tiers[tierIndex].TierNumber);
                for (int outcomeIndex = 0; outcomeIndex < policy.AugmentLevelOutcomes.Count; outcomeIndex++)
                    maximum = Math.Max(maximum, policy.AugmentLevelOutcomes[outcomeIndex].Value);
            }
            return maximum;
        }

        private static bool TryResolveGun(
            GunCatalog gunCatalog,
            EquipmentDefinition equipment,
            out GunDefinitionData gun)
        {
            string reference = GunDefinitionId.FromRuntimeReference(
                equipment.RuntimeGunReferenceId).Value;
            if (gunCatalog.TryGetDefinition(reference, out gun) && gun != null)
                return true;

            IReadOnlyList<GunDefinitionData> live =
                gunCatalog.GetDefinitions(GunCatalogContentFilter.LiveOnly);
            for (int index = 0; index < live.Count; index++)
            {
                GunDefinitionData candidate = live[index];
                StableId raw;
                if ((StableId.TryParse(candidate.DefinitionId, out raw)
                        && raw == equipment.RuntimeGunReferenceId)
                    || Strongbox.DeriveId("gun", candidate.DefinitionId)
                        == equipment.RuntimeGunReferenceId)
                {
                    gun = candidate;
                    return true;
                }
            }
            gun = null;
            return false;
        }

        private static bool TryResolveRarity(string rarity, out StableId rarityId)
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
                case "artifact":
                    rarityId = StrongboxDefinitionRarityIds.Artifact;
                    return true;
                default:
                    rarityId = null;
                    return false;
            }
        }
    }
}
