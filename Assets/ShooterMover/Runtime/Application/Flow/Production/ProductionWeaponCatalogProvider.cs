using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// Short production access point for the single authored weapon catalogue.
    /// It owns no content and only exposes the canonical projections.
    /// </summary>
    public static class ProductionWeaponCatalogProvider
    {
        public static ProductionWeaponCatalogueProjectionV1 Current
        {
            get { return ProductionWeaponCatalogueV1.Current; }
        }

        public static WeaponCatalog WeaponCatalog
        {
            get { return Current.WeaponCatalog; }
        }

        public static EquipmentCatalog EquipmentCatalog
        {
            get { return Current.EquipmentCatalog; }
        }
    }
}
