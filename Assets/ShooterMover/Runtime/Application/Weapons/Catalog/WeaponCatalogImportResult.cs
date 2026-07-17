using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Weapons.Catalog
{
    public sealed class WeaponCatalogImportResult
    {
        private readonly ReadOnlyCollection<WeaponCatalogIssue> _issues;

        internal WeaponCatalogImportResult(WeaponCatalog catalog, IEnumerable<WeaponCatalogIssue> issues)
        {
            Catalog = catalog;
            List<WeaponCatalogIssue> sorted = issues == null
                ? new List<WeaponCatalogIssue>()
                : new List<WeaponCatalogIssue>(issues);
            sorted.Sort();
            _issues = new ReadOnlyCollection<WeaponCatalogIssue>(sorted);
        }

        public bool IsSuccess { get { return Catalog != null && _issues.Count == 0; } }
        public WeaponCatalog Catalog { get; private set; }
        public IReadOnlyList<WeaponCatalogIssue> Issues { get { return _issues; } }
    }

}
