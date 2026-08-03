using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Application.Flow.Game
{
    /// <summary>
    /// Short production access point for the single authored gun catalogue.
    /// It owns no content and only exposes the canonical projections.
    /// </summary>
    public static class GunCatalogProvider
    {
        public static GunCatalogueView Current
        {
            get { return AuthoredGunCatalogue.Current; }
        }

        public static GunCatalog GunCatalog
        {
            get { return Current.GunCatalog; }
        }

        public static EquipmentCatalog EquipmentCatalog
        {
            get { return Current.EquipmentCatalog; }
        }
    }
}
