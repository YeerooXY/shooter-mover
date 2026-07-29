using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Enemies.Catalog
{
    public sealed class EnemyCatalogIssue
    {
        public EnemyCatalogIssue(string code, string path, string message)
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

    public sealed class EnemyCatalogValidationResult
    {
        private readonly ReadOnlyCollection<EnemyCatalogIssue> issues;

        public EnemyCatalogValidationResult(IEnumerable<EnemyCatalogIssue> issues)
        {
            this.issues = new ReadOnlyCollection<EnemyCatalogIssue>(
                issues == null
                    ? new List<EnemyCatalogIssue>()
                    : new List<EnemyCatalogIssue>(issues));
        }

        public bool IsValid { get { return issues.Count == 0; } }

        public IReadOnlyList<EnemyCatalogIssue> Issues { get { return issues; } }
    }
}
