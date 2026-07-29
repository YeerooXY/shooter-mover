using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// Short production access point for the single authored weapon catalogue.
    /// It owns no content and only exposes the canonical projections.
    /// </summary>
    public static class WeaponCatalogProvider
    {
        public static WeaponCatalogueView Current
        {
            get { return WeaponCatalogue.Current; }
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
