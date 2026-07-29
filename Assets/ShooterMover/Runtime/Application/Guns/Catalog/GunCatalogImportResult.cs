using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Application.Guns.Catalog
{
    public sealed class GunCatalogImportResult
    {
        private readonly ReadOnlyCollection<GunCatalogIssue> _issues;

        internal GunCatalogImportResult(GunCatalog catalog, IEnumerable<GunCatalogIssue> issues)
        {
            Catalog = catalog;
            List<GunCatalogIssue> sorted = issues == null
                ? new List<GunCatalogIssue>()
                : new List<GunCatalogIssue>(issues);
            sorted.Sort();
            _issues = new ReadOnlyCollection<GunCatalogIssue>(sorted);
        }

        public bool IsSuccess { get { return Catalog != null && _issues.Count == 0; } }
        public GunCatalog Catalog { get; private set; }
        public IReadOnlyList<GunCatalogIssue> Issues { get { return _issues; } }
    }

}
