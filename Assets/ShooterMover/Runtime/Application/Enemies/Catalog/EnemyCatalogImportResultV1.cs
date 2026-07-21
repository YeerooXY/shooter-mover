using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Enemies.Catalog;

namespace ShooterMover.Application.Enemies.Catalog
{
    public sealed class EnemyCatalogImportResultV1
    {
        private readonly ReadOnlyCollection<EnemyCatalogIssueV1> issues;

        public EnemyCatalogImportResultV1(
            EnemyCatalogV1 catalog,
            IEnumerable<EnemyCatalogIssueV1> issues)
        {
            Catalog = catalog;
            this.issues = new ReadOnlyCollection<EnemyCatalogIssueV1>(
                issues == null
                    ? new List<EnemyCatalogIssueV1>()
                    : new List<EnemyCatalogIssueV1>(issues));
        }

        public EnemyCatalogV1 Catalog { get; }

        public IReadOnlyList<EnemyCatalogIssueV1> Issues
        {
            get { return issues; }
        }

        public bool IsValid
        {
            get { return Catalog != null && issues.Count == 0; }
        }
    }
}
