using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Weapons.Presentation
{
    /// <summary>
    /// Engine-neutral projection from an exact equipment instance to canonical weapon
    /// presentation identity. Unity resource paths and sprites deliberately stay out of
    /// this layer.
    /// </summary>
    public sealed class WeaponArtReferenceProjectionV1
    {
        public WeaponArtReferenceProjectionV1(
            StableId equipmentInstanceStableId,
            StableId equipmentDefinitionStableId,
            string weaponDefinitionId,
            string artReferenceId)
        {
            EquipmentInstanceStableId = equipmentInstanceStableId;
            EquipmentDefinitionStableId = equipmentDefinitionStableId
                ?? throw new ArgumentNullException(
                    nameof(equipmentDefinitionStableId));
            if (string.IsNullOrWhiteSpace(weaponDefinitionId))
            {
                throw new ArgumentException(
                    "Weapon definition identity is required.",
                    nameof(weaponDefinitionId));
            }
            if (string.IsNullOrWhiteSpace(artReferenceId))
            {
                throw new ArgumentException(
                    "Weapon art reference identity is required.",
                    nameof(artReferenceId));
            }

            WeaponDefinitionId = weaponDefinitionId.Trim();
            ArtReferenceId = artReferenceId.Trim();
        }

        public StableId EquipmentInstanceStableId { get; }

        public StableId EquipmentDefinitionStableId { get; }

        public string WeaponDefinitionId { get; }

        public string ArtReferenceId { get; }
    }

    public static class WeaponArtReferenceResolverV1
    {
        public static bool TryResolve(
            EquipmentInstance instance,
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            out WeaponArtReferenceProjectionV1 projection,
            out string rejectionCode)
        {
            projection = null;
            if (instance == null)
            {
                rejectionCode = "weapon-art-equipment-instance-null";
                return false;
            }

            return TryResolve(
                instance.DefinitionId,
                instance.InstanceId,
                equipmentCatalog,
                weaponCatalog,
                out projection,
                out rejectionCode);
        }

        public static bool TryResolve(
            StableId equipmentDefinitionStableId,
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            out WeaponArtReferenceProjectionV1 projection,
            out string rejectionCode)
        {
            return TryResolve(
                equipmentDefinitionStableId,
                null,
                equipmentCatalog,
                weaponCatalog,
                out projection,
                out rejectionCode);
        }

        private static bool TryResolve(
            StableId equipmentDefinitionStableId,
            StableId equipmentInstanceStableId,
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            out WeaponArtReferenceProjectionV1 projection,
            out string rejectionCode)
        {
            projection = null;
            if (equipmentDefinitionStableId == null)
            {
                rejectionCode = "weapon-art-equipment-definition-id-null";
                return false;
            }
            if (equipmentCatalog == null)
            {
                rejectionCode = "weapon-art-equipment-catalog-unavailable";
                return false;
            }
            if (weaponCatalog == null)
            {
                rejectionCode = "weapon-art-weapon-catalog-unavailable";
                return false;
            }

            EquipmentDefinition equipmentDefinition = equipmentCatalog
                .FindEquipmentDefinition(equipmentDefinitionStableId);
            if (equipmentDefinition == null)
            {
                rejectionCode = "weapon-art-equipment-definition-missing:"
                    + equipmentDefinitionStableId;
                return false;
            }
            if (equipmentDefinition.CategoryId != EquipmentCategoryIds.Weapon
                || equipmentDefinition.RuntimeWeaponReferenceId == null)
            {
                rejectionCode = "weapon-art-equipment-is-not-weapon:"
                    + equipmentDefinitionStableId;
                return false;
            }

            string weaponDefinitionId = equipmentDefinition
                .RuntimeWeaponReferenceId.ToString();
            WeaponDefinitionData weaponDefinition;
            if (!weaponCatalog.TryGetDefinition(
                weaponDefinitionId,
                out weaponDefinition))
            {
                rejectionCode = "weapon-art-weapon-definition-missing:"
                    + weaponDefinitionId;
                return false;
            }

            string artReferenceId = FirstReference(
                weaponDefinition.SideProfileArtReferences);
            if (artReferenceId == null)
            {
                WeaponFamilyDefinition family;
                if (weaponCatalog.TryGetFamily(
                    weaponDefinition.FamilyId,
                    out family))
                {
                    artReferenceId = FirstReference(
                        family.SideProfileArtReferences);
                }
            }
            if (artReferenceId == null)
            {
                rejectionCode = "weapon-art-reference-missing:"
                    + weaponDefinitionId;
                return false;
            }

            projection = new WeaponArtReferenceProjectionV1(
                equipmentInstanceStableId,
                equipmentDefinitionStableId,
                weaponDefinitionId,
                artReferenceId);
            rejectionCode = string.Empty;
            return true;
        }

        private static string FirstReference(
            System.Collections.Generic.IReadOnlyList<string> references)
        {
            if (references == null)
            {
                return null;
            }

            for (int index = 0; index < references.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(references[index]))
                {
                    return references[index].Trim();
                }
            }
            return null;
        }
    }
}
