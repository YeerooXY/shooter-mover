using System;
using System.Collections.Generic;

namespace ShooterMover.Application.Guns.Catalog
{
    /// <summary>
    /// Production catalogue selected by GunCatalogProvider. A generated Weapon Maker projection
    /// is authoritative whenever it contains validated families. The legacy hand-built catalogue
    /// remains only as an explicit empty-generation migration fallback.
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
            get { return AuthoredGunCatalogueGenerated.FamilyCount > 0; }
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
            if (AuthoredGunCatalogueGenerated.FamilyCount == 0)
            {
                return GunCatalogue.Current;
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
            return GunCatalogue.CreateAuthoredView(families);
        }
    }
}
