using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ShooterMover.Domain.Guns.Catalog
{
    public enum GunCatalogIssueCode
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

    public sealed class GunCatalogIssue : IComparable<GunCatalogIssue>
    {
        public GunCatalogIssue(GunCatalogIssueCode code, string path, string detail)
        {
            Code = code;
            Path = path ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public GunCatalogIssueCode Code { get; private set; }
        public string Path { get; private set; }
        public string Detail { get; private set; }

        public int CompareTo(GunCatalogIssue other)
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

    public sealed class GunCatalogValidationResult
    {
        private readonly ReadOnlyCollection<GunCatalogIssue> _issues;

        public GunCatalogValidationResult(IEnumerable<GunCatalogIssue> issues)
        {
            List<GunCatalogIssue> sorted = issues == null
                ? new List<GunCatalogIssue>()
                : new List<GunCatalogIssue>(issues);
            sorted.Sort();
            _issues = new ReadOnlyCollection<GunCatalogIssue>(sorted);
        }

        public bool IsValid { get { return _issues.Count == 0; } }
        public IReadOnlyList<GunCatalogIssue> Issues { get { return _issues; } }
    }

}
