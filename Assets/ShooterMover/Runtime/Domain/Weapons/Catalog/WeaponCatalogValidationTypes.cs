using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ShooterMover.Domain.Weapons.Catalog
{
    public enum WeaponCatalogIssueCode
    {
        InvalidJson = 0,
        MissingRequiredField = 1,
        InvalidValue = 2,
        DuplicateId = 3,
        InvalidId = 4,
        UnsupportedArchetype = 5,
        UnsupportedDamageType = 6,
        UnknownFamily = 7,
        DuplicateFamilyMark = 8,
        FamilyMarkMismatch = 9,
        RangeViolation = 10,
        ShareTotalMismatch = 11,
        DerivedValueMismatch = 12,
        InvalidAvailability = 13,
        InvalidArtReference = 14,
    }

    public sealed class WeaponCatalogIssue : IComparable<WeaponCatalogIssue>
    {
        public WeaponCatalogIssue(WeaponCatalogIssueCode code, string path, string detail)
        {
            Code = code;
            Path = path ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public WeaponCatalogIssueCode Code { get; private set; }
        public string Path { get; private set; }
        public string Detail { get; private set; }

        public int CompareTo(WeaponCatalogIssue other)
        {
            if (other == null)
            {
                return 1;
            }

            int path = string.CompareOrdinal(Path, other.Path);
            if (path != 0)
            {
                return path;
            }

            int code = Code.CompareTo(other.Code);
            return code != 0 ? code : string.CompareOrdinal(Detail, other.Detail);
        }

        public override string ToString()
        {
            return Code + " at " + Path + ": " + Detail;
        }
    }

    public sealed class WeaponCatalogValidationResult
    {
        private readonly ReadOnlyCollection<WeaponCatalogIssue> _issues;

        public WeaponCatalogValidationResult(IEnumerable<WeaponCatalogIssue> issues)
        {
            List<WeaponCatalogIssue> sorted = issues == null
                ? new List<WeaponCatalogIssue>()
                : new List<WeaponCatalogIssue>(issues);
            sorted.Sort();
            _issues = new ReadOnlyCollection<WeaponCatalogIssue>(sorted);
        }

        public bool IsValid { get { return _issues.Count == 0; } }
        public IReadOnlyList<WeaponCatalogIssue> Issues { get { return _issues; } }
    }

}
