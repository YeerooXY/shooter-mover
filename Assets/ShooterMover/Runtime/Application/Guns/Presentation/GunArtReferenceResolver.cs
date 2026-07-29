using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Presentation
{
    /// <summary>
    /// Engine-neutral projection from an exact equipment instance to canonical gun
    /// presentation identity. Unity resource paths and sprites deliberately stay out of
    /// this layer.
    /// </summary>
    public sealed class GunArtReferenceView
    {
        public GunArtReferenceView(
            StableId equipmentInstanceStableId,
            StableId equipmentDefinitionStableId,
            string gunDefinitionId,
            string artReferenceId)
        {
            EquipmentInstanceStableId = equipmentInstanceStableId;
            EquipmentDefinitionStableId = equipmentDefinitionStableId
                ?? throw new ArgumentNullException(
                    nameof(equipmentDefinitionStableId));
            if (string.IsNullOrWhiteSpace(gunDefinitionId))
            {
                throw new ArgumentException(
                    "Gun definition identity is required.",
                    nameof(gunDefinitionId));
            }
            if (string.IsNullOrWhiteSpace(artReferenceId))
            {
                throw new ArgumentException(
                    "Gun art reference identity is required.",
                    nameof(artReferenceId));
            }

            GunDefinitionId = gunDefinitionId.Trim();
            ArtReferenceId = artReferenceId.Trim();
        }

        public StableId EquipmentInstanceStableId { get; }

        public StableId EquipmentDefinitionStableId { get; }

        public string GunDefinitionId { get; }

        public string ArtReferenceId { get; }
    }

    public static class GunArtReferenceResolver
    {
        public static bool TryResolve(
            EquipmentInstance instance,
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog,
            out GunArtReferenceView projection,
            out string rejectionCode)
        {
            projection = null;
            if (instance == null)
            {
                rejectionCode = "gun-art-equipment-instance-null";
                return false;
            }

            return TryResolve(
                instance.DefinitionId,
                instance.InstanceId,
                equipmentCatalog,
                gunCatalog,
                out projection,
                out rejectionCode);
        }

        public static bool TryResolve(
            StableId equipmentDefinitionStableId,
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog,
            out GunArtReferenceView projection,
            out string rejectionCode)
        {
            return TryResolve(
                equipmentDefinitionStableId,
                null,
                equipmentCatalog,
                gunCatalog,
                out projection,
                out rejectionCode);
        }

        private static bool TryResolve(
            StableId equipmentDefinitionStableId,
            StableId equipmentInstanceStableId,
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog,
            out GunArtReferenceView projection,
            out string rejectionCode)
        {
            projection = null;
            if (equipmentDefinitionStableId == null)
            {
                rejectionCode = "gun-art-equipment-definition-id-null";
                return false;
            }
            if (equipmentCatalog == null)
            {
                rejectionCode = "gun-art-equipment-catalog-unavailable";
                return false;
            }
            if (gunCatalog == null)
            {
                rejectionCode = "gun-art-gun-catalog-unavailable";
                return false;
            }

            EquipmentDefinition equipmentDefinition = equipmentCatalog
                .FindEquipmentDefinition(equipmentDefinitionStableId);
            if (equipmentDefinition == null)
            {
                rejectionCode = "gun-art-equipment-definition-missing:"
                    + equipmentDefinitionStableId;
                return false;
            }
            if (equipmentDefinition.CategoryId != EquipmentCategoryIds.Gun
                || equipmentDefinition.RuntimeGunReferenceId == null)
            {
                rejectionCode = "gun-art-equipment-is-not-gun:"
                    + equipmentDefinitionStableId;
                return false;
            }

            string gunDefinitionId = GunDefinitionId
                .FromRuntimeReference(
                    equipmentDefinition.RuntimeGunReferenceId)
                .Value;
            GunDefinitionData gunDefinition;
            if (!gunCatalog.TryGetDefinition(
                gunDefinitionId,
                out gunDefinition))
            {
                rejectionCode = "gun-art-gun-definition-missing:"
                    + gunDefinitionId;
                return false;
            }

            string artReferenceId = FirstReference(
                gunDefinition.SideProfileArtReferences);
            if (artReferenceId == null)
            {
                GunFamilyDefinition family;
                if (gunCatalog.TryGetFamily(
                    gunDefinition.FamilyId,
                    out family))
                {
                    artReferenceId = FirstReference(
                        family.SideProfileArtReferences);
                }
            }
            if (artReferenceId == null)
            {
                rejectionCode = "gun-art-reference-missing:"
                    + gunDefinitionId;
                return false;
            }

            projection = new GunArtReferenceView(
                equipmentInstanceStableId,
                equipmentDefinitionStableId,
                gunDefinitionId,
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
