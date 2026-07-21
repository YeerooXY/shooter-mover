using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Enemies.Catalog
{
    public sealed class EnemyCatalogIssueV1
    {
        public EnemyCatalogIssueV1(string code, string path, string message)
        {
            Code = string.IsNullOrWhiteSpace(code) ? "enemy-catalog-invalid" : code;
            Path = string.IsNullOrWhiteSpace(path) ? "$" : path;
            Message = message ?? string.Empty;
        }

        public string Code { get; }

        public string Path { get; }

        public string Message { get; }

        public override string ToString()
        {
            return Code + ":" + Path + ":" + Message;
        }
    }

    public sealed class EnemyCatalogValidationResultV1
    {
        private readonly ReadOnlyCollection<EnemyCatalogIssueV1> issues;

        public EnemyCatalogValidationResultV1(IEnumerable<EnemyCatalogIssueV1> issues)
        {
            this.issues = new ReadOnlyCollection<EnemyCatalogIssueV1>(
                issues == null
                    ? new List<EnemyCatalogIssueV1>()
                    : new List<EnemyCatalogIssueV1>(issues));
        }

        public bool IsValid { get { return issues.Count == 0; } }

        public IReadOnlyList<EnemyCatalogIssueV1> Issues { get { return issues; } }
    }
}
