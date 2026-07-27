using System;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    /// <summary>
    /// Compatibility boundary for the retained scheduler contract. Resolution always begins with
    /// the exact canonical instance and its WeaponDefinitionId. It never substitutes another held
    /// weapon or fabricates a fallback definition. Assignment-bearing instances fail closed until
    /// their canonical assignment runtime policy is supplied.
    /// </summary>
    public sealed class CanonicalWeaponEquipmentProjectionLookupV2 :
        IPlayerEquipmentInstanceLookup
    {
        private readonly ProductionWeaponHoldingsAuthorityV2 holdings;
        private readonly EquipmentCatalog equipmentCatalog;

        public CanonicalWeaponEquipmentProjectionLookupV2(
            ProductionWeaponHoldingsAuthorityV2 weaponHoldings,
            EquipmentCatalog equipmentCatalog)
        {
            holdings = weaponHoldings
                ?? throw new ArgumentNullException(nameof(weaponHoldings));
            this.equipmentCatalog = equipmentCatalog
                ?? throw new ArgumentNullException(nameof(equipmentCatalog));
        }

        public bool TryResolve(
            EquipmentInstanceId equipmentInstanceId,
            out EquipmentInstance equipmentInstance)
        {
            equipmentInstance = null;
            if (equipmentInstanceId == null)
            {
                return false;
            }

            WeaponEquipmentInstance canonical = holdings.Find(
                equipmentInstanceId.Value);
            if (canonical == null
                || canonical.AugmentAssignments.Count != 0
                || canonical.OverclockAssignments.Count != 0)
            {
                return false;
            }

            ProductionWeaponMarkV1 mark;
            if (!ProductionWeaponCatalogProvider.Current.TryGetMark(
                    canonical.WeaponDefinitionId.Value,
                    out mark)
                || mark == null)
            {
                return false;
            }

            EquipmentDefinition definition =
                equipmentCatalog.FindEquipmentDefinition(
                    mark.EquipmentDefinitionId);
            if (definition == null
                || definition.RuntimeWeaponReferenceId == null
                || !string.Equals(
                    definition.RuntimeWeaponReferenceId.ToString(),
                    canonical.WeaponDefinitionId.Value,
                    StringComparison.Ordinal)
                || definition.QualityTiers == null
                || definition.QualityTiers.Count == 0)
            {
                return false;
            }

            EquipmentInstance projection = EquipmentInstance.Create(
                canonical.InstanceId,
                definition.DefinitionId,
                definition.ItemLevelRange.Minimum,
                definition.QualityTiers[0].QualityId,
                Array.Empty<AugmentInstance>());
            EquipmentValidationResult validation =
                equipmentCatalog.ValidateInstance(projection);
            if (validation == null || !validation.IsValid)
            {
                return false;
            }

            equipmentInstance = projection;
            return true;
        }
    }
}
