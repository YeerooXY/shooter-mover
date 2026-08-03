using System;
using System.Collections.Generic;

namespace ShooterMover.Application.Guns.Catalog
{
    public static partial class GunCatalogue
    {
        internal static GunCatalogueView CreateAuthoredView(
            IReadOnlyList<GunFamily> families)
        {
            if (families == null)
            {
                throw new ArgumentNullException(nameof(families));
            }
            if (families.Count == 0)
            {
                throw new ArgumentException(
                    "The authored gun catalogue requires at least one family.",
                    nameof(families));
            }

            return new GunCatalogueView(
                families,
                BuildGunCatalog(families),
                BuildEquipmentCatalog(families));
        }
    }
}
