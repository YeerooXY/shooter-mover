using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Foundation;

namespace ShooterMover.Application.Enemies.Foundation
{
    public sealed class EnemyPkgCompileResult
    {
        private readonly ReadOnlyCollection<EnemyPkgIssue> issues;

        public EnemyPkgCompileResult(
            EnemyDefCatalog catalog,
            IEnumerable<EnemyPkgIssue> issues)
        {
            Catalog = catalog;
            this.issues = new ReadOnlyCollection<EnemyPkgIssue>(
                new List<EnemyPkgIssue>(issues ?? Array.Empty<EnemyPkgIssue>()));
        }

        public EnemyDefCatalog Catalog { get; }
        public IReadOnlyList<EnemyPkgIssue> Issues { get { return issues; } }
        public bool IsValid { get { return Catalog != null && issues.Count == 0; } }
    }

    public static class EnemyPkgCompiler
    {
        public static EnemyPkgCompileResult Compile(
            IEnumerable<EnemyPkg> packages,
            IEnemyRefs refs)
        {
            if (packages == null) throw new ArgumentNullException(nameof(packages));
            if (refs == null) throw new ArgumentNullException(nameof(refs));

            var source = new List<EnemyPkg>();
            var issues = new List<EnemyPkgIssue>();
            var ids = new Dictionary<StableId, int>();
            int index = 0;

            foreach (EnemyPkg package in packages)
            {
                string root = "$packages[" + index + "]";
                if (package == null)
                {
                    issues.Add(new EnemyPkgIssue(
                        "enemy-pkg-null",
                        root,
                        "Enemy package cannot be null."));
                    index++;
                    continue;
                }

                int first;
                if (ids.TryGetValue(package.Enemy.Id, out first))
                {
                    issues.Add(new EnemyPkgIssue(
                        "enemy-pkg-id-duplicate",
                        root + ".enemy.id",
                        "Enemy ID is already published by package " + first
                            + ": " + package.Enemy.Id));
                }
                else
                {
                    ids.Add(package.Enemy.Id, index);
                }

                EnemyPkgResult check = EnemyPkgCheck.Check(package, refs);
                for (int issueIndex = 0;
                    issueIndex < check.Issues.Count;
                    issueIndex++)
                {
                    EnemyPkgIssue issue = check.Issues[issueIndex];
                    issues.Add(new EnemyPkgIssue(
                        issue.Code,
                        Prefix(root, issue.Path),
                        issue.Message));
                }

                source.Add(package);
                index++;
            }

            if (source.Count == 0)
            {
                issues.Add(new EnemyPkgIssue(
                    "enemy-pkg-set-empty",
                    "$packages",
                    "At least one enemy package is required."));
            }

            if (issues.Count > 0)
                return new EnemyPkgCompileResult(null, issues);

            var definitions = new List<EnemyDef>();
            for (int packageIndex = 0;
                packageIndex < source.Count;
                packageIndex++)
            {
                definitions.Add(source[packageIndex].Enemy);
            }

            return new EnemyPkgCompileResult(
                new EnemyDefCatalog(definitions),
                Array.Empty<EnemyPkgIssue>());
        }

        private static string Prefix(string root, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == "$")
                return root;
            return path[0] == '$'
                ? root + path.Substring(1)
                : root + "." + path;
        }
    }
}
