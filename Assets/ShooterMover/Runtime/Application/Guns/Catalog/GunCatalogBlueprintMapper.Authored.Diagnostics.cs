using System.Collections.Generic;

namespace ShooterMover.Application.Guns.Catalog
{
    public static partial class GunCatalogBlueprintMapper
    {
        private static void Add(
            ICollection<GunMappingIssue> issues,
            GunMappingIssueCode code,
            string path,
            string detail)
        {
            issues.Add(new GunMappingIssue(code, path, detail));
        }
    }
}
