using System;
using System.Collections.Generic;

namespace ShooterMover.Application.Guns.Catalog
{
    /// <summary>
    /// Production catalogue selected by GunCatalogProvider. The generated Weapon Maker projection
    /// is the sole production content authority. Missing or invalid publication fails closed.
    /// </summary>
    public static class AuthoredGunCatalogue
    {
        private static readonly GunCatalogueView current = Build();

        public static GunCatalogueView Current
        {
            get { return current; }
        }

        public static bool UsesGeneratedSource
        {
            get { return true; }
        }

        public static string SourceFingerprint
        {
            get { return AuthoredGunCatalogueGenerated.SourceFingerprint; }
        }

        private static GunCatalogueView Build()
        {
            if (AuthoredGunCatalogueGenerated.Schema != 1)
            {
                throw new InvalidOperationException(
                    "authored-gun-catalog-generated-schema-unsupported");
            }
            if (AuthoredGunCatalogueGenerated.FamilyCount == 0
                || AuthoredGunCatalogueGenerated.DefinitionCount == 0)
            {
                throw new InvalidOperationException(
                    "authored-gun-catalog-not-generated");
            }

            List<GunFamily> families = AuthoredGunCatalogJsonImporter.Import(
                AuthoredGunCatalogueGenerated.Json);
            int definitionCount = 0;
            for (int index = 0; index < families.Count; index++)
            {
                definitionCount = checked(
                    definitionCount + families[index].Marks.Count);
            }
            if (families.Count != AuthoredGunCatalogueGenerated.FamilyCount
                || definitionCount
                    != AuthoredGunCatalogueGenerated.DefinitionCount)
            {
                throw new InvalidOperationException(
                    "authored-gun-catalog-generated-count-mismatch");
            }

            return AuthoredGunCatalogueProjection.Create(families);
        }
    }
}
