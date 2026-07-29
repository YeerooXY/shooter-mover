using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Enemies.Catalog;

namespace ShooterMover.Application.Enemies.Catalog
{
    public sealed class EnemyCatalogImportResult
    {
        private readonly ReadOnlyCollection<EnemyCatalogIssue> issues;

        public EnemyCatalogImportResult(
            EnemyCatalog catalog,
            IEnumerable<EnemyCatalogIssue> issues)
        {
            Catalog = catalog;
            this.issues = new ReadOnlyCollection<EnemyCatalogIssue>(
                issues == null
                    ? new List<EnemyCatalogIssue>()
                    : new List<EnemyCatalogIssue>(issues));
        }

        public EnemyCatalog Catalog { get; }

        public IReadOnlyList<EnemyCatalogIssue> Issues
        {
            get { return issues; }
        }

        public bool IsValid
        {
            get { return Catalog != null && issues.Count == 0; }
        }
    }
}
