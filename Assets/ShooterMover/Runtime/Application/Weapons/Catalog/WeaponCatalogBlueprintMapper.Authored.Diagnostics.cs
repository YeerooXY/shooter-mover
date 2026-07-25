using System.Collections.Generic;

namespace ShooterMover.Application.Weapons.Catalog
{
    public static partial class WeaponCatalogBlueprintMapper
    {
        private static void Add(
            ICollection<WeaponBlueprintMappingIssue> issues,
            WeaponBlueprintMappingIssueCode code,
            string path,
            string detail)
        {
            issues.Add(new WeaponBlueprintMappingIssue(code, path, detail));
        }
    }
}
