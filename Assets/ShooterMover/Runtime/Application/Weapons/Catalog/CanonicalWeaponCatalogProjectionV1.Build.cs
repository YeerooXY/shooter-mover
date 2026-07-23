
using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Weapons.Catalog
{
    public sealed partial class CanonicalWeaponCatalogProjectionV1
    {
        public static bool TryCreate(
            IWeaponCatalogSourceV1 source,
            IWeaponRarityNormalizationPolicyV1 rarityPolicy,
            out CanonicalWeaponCatalogProjectionV1 projection,
            out string diagnostic)
        {
            projection = null;
            diagnostic = string.Empty;
            if (source == null)
            {
                diagnostic = "weapon-catalog-source-null";
                return false;
            }
            if (rarityPolicy == null)
            {
                diagnostic = "weapon-rarity-normalization-policy-null";
                return false;
            }

            string json;
            try
            {
                json = source.ReadJson();
            }
            catch (Exception exception)
            {
                diagnostic = "weapon-catalog-source-read-failed:"
                    + exception.GetType().Name.ToLowerInvariant();
                return false;
            }

            WeaponCatalogImportResult raw = WeaponCatalogJsonImporter.ImportRaw(json);
            if (!raw.IsSuccess)
            {
                diagnostic = raw.Issues.Count == 0
                    ? "weapon-catalog-import-failed"
                    : raw.Issues[0].Path + ":" + raw.Issues[0].Detail;
                return false;
            }

            WeaponCatalog normalized;
            if (!WeaponCatalogRarityProjectorV1.TryProject(
                    raw.Catalog,
                    rarityPolicy,
                    out normalized,
                    out diagnostic))
            {
                return false;
            }

            var archetypes = new Dictionary<string, WeaponArchetypeDefinition>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, WeaponArchetypeDefinition> pair in normalized.Archetypes)
            {
                archetypes.Add(pair.Key, pair.Value);
            }

            var rawById = new Dictionary<string, WeaponDefinitionData>(StringComparer.Ordinal);
            for (int index = 0; index < raw.Catalog.Definitions.Count; index++)
            {
                WeaponDefinitionData rawDefinition = raw.Catalog.Definitions[index];
                rawById.Add(rawDefinition.DefinitionId, rawDefinition);
            }

            var definitions = new List<WeaponDefinitionData>(normalized.Definitions.Count);
            for (int index = 0; index < normalized.Definitions.Count; index++)
            {
                WeaponDefinitionData definition = normalized.Definitions[index];
                string fallbackArt;
                if (!ExistingArtReferences.TryGetValue(definition.DefinitionId, out fallbackArt))
                {
                    fallbackArt = "weapon-art.generic." + Slug(definition.DefinitionId) + ".side-v1";
                }
                var artReferences = new List<string>(definition.SideProfileArtReferences);
                if (artReferences.Count == 0)
                {
                    artReferences.Add(fallbackArt);
                }
                definitions.Add(WeaponCatalogRarityProjectorV1.CloneDefinition(
                    definition,
                    definition.Rarity,
                    artReferences));
            }

            WeaponCatalog finalCatalog = new WeaponCatalog(
                normalized.Version,
                normalized.Status,
                normalized.Rules,
                normalized.Inputs,
                archetypes,
                normalized.Families,
                definitions,
                rarityPolicy.Fingerprint);

            var equipmentDefinitions = new List<EquipmentDefinition>(finalCatalog.Definitions.Count);
            var entries = new List<CanonicalWeaponCatalogEntryV1>(finalCatalog.Definitions.Count);
            for (int index = 0; index < finalCatalog.Definitions.Count; index++)
            {
                WeaponDefinitionData definition = finalCatalog.Definitions[index];
                StableId equipmentId;
                if (!ExistingEquipmentIds.TryGetValue(definition.DefinitionId, out equipmentId))
                {
                    equipmentId = StableId.Create("equipment", "weapon-" + Slug(definition.DefinitionId));
                }

                EquipmentQualityTier quality = QualityFor(definition.Rarity);
                EquipmentDefinition equipment = EquipmentDefinition.Create(
                    equipmentId,
                    EquipmentCategoryIds.Weapon,
                    StableId.Create("equipment-family", "weapon-" + Slug(definition.FamilyId)),
                    definition.DisplayName,
                    RuntimeReferenceFor(definition.DefinitionId),
                    InclusiveIntRange.Create(1, Math.Max(200, checked(definition.PowerAnchor + 50))),
                    Math.Max(0, finalCatalog.Rules.MaxAugments),
                    new[] { quality },
                    Array.Empty<StableId>());
                equipmentDefinitions.Add(equipment);

                WeaponDefinitionData rawDefinition = rawById[definition.DefinitionId];
                entries.Add(new CanonicalWeaponCatalogEntryV1(
                    definition,
                    equipment,
                    rawDefinition.Rarity,
                    definition.SideProfileArtReferences[0]));
            }

            EquipmentCatalogBuildResult equipmentBuild = EquipmentCatalog.Build(
                equipmentDefinitions,
                Array.Empty<AugmentDefinition>());
            if (!equipmentBuild.IsValid || equipmentBuild.Catalog == null)
            {
                diagnostic = "canonical-weapon-equipment-catalog-invalid:"
                    + (equipmentBuild.Issues.Count == 0 ? "unknown" : equipmentBuild.Issues[0].ToString());
                return false;
            }

            projection = new CanonicalWeaponCatalogProjectionV1(
                source.SourceId,
                Sha256(json),
                rarityPolicy.Fingerprint,
                finalCatalog,
                equipmentBuild.Catalog,
                entries);
            return true;
        }

#if UNITY_EDITOR
        /// <summary>Explicit Editor/test composition over the tracked repository snapshot.</summary>
        public static CanonicalWeaponCatalogProjectionV1 CreateEditorRepositoryBaseline()
        {
            CanonicalWeaponCatalogProjectionV1 projection;
            string diagnostic;
            if (!TryCreate(
                    new FileWeaponCatalogSourceV1("weapon-baseline-v01-editor", BaselineRepositoryPath),
                    WeaponRarityNormalizationPolicyV1.CreateBaselineV1(),
                    out projection,
                    out diagnostic))
            {
                throw new InvalidOperationException(
                    "Editor weapon catalog composition failed: " + diagnostic);
            }
            return projection;
        }
#endif

        private static EquipmentQualityTier QualityFor(string normalizedRarity)
        {
            StableId id = WeaponEquipmentQualityIdsV1.ForNormalizedRarity(normalizedRarity);
            if (id == WeaponEquipmentQualityIdsV1.Common)
            {
                return EquipmentQualityTier.Create(id, "Common", 1);
            }
            if (id == WeaponEquipmentQualityIdsV1.Rare)
            {
                return EquipmentQualityTier.Create(id, "Rare", 2);
            }
            if (id == WeaponEquipmentQualityIdsV1.Epic)
            {
                return EquipmentQualityTier.Create(id, "Epic", 3);
            }
            if (id == WeaponEquipmentQualityIdsV1.Legendary)
            {
                return EquipmentQualityTier.Create(id, "Legendary", 4);
            }
            return EquipmentQualityTier.Create(id, "Mythic / Artifact", 5);
        }
    }
}
