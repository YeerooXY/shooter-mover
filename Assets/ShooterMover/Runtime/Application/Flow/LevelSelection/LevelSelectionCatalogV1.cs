using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Flow.LevelSelection
{
    public sealed class LevelSelectionCatalogV1
    {
        private sealed class DefinitionComparer :
            IComparer<LevelSelectionDefinitionV1>
        {
            public int Compare(
                LevelSelectionDefinitionV1 left,
                LevelSelectionDefinitionV1 right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (ReferenceEquals(left, null))
                {
                    return -1;
                }

                if (ReferenceEquals(right, null))
                {
                    return 1;
                }

                int orderComparison = left.SortOrder.CompareTo(right.SortOrder);
                if (orderComparison != 0)
                {
                    return orderComparison;
                }

                return string.Compare(
                    left.LevelStableId.ToString(),
                    right.LevelStableId.ToString(),
                    StringComparison.Ordinal);
            }
        }

        private readonly ReadOnlyCollection<LevelSelectionDefinitionV1> levels;
        private readonly Dictionary<StableId, LevelSelectionDefinitionV1>
            levelsById;

        public LevelSelectionCatalogV1(
            IEnumerable<LevelSelectionDefinitionV1> levels)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            var ordered = new List<LevelSelectionDefinitionV1>(levels);
            if (ordered.Count == 0)
            {
                throw new ArgumentException(
                    "At least one level definition is required.",
                    nameof(levels));
            }

            ordered.Sort(new DefinitionComparer());
            levelsById =
                new Dictionary<StableId, LevelSelectionDefinitionV1>();

            for (int index = 0; index < ordered.Count; index++)
            {
                LevelSelectionDefinitionV1 definition = ordered[index];
                if (definition == null)
                {
                    throw new ArgumentException(
                        "Level definitions cannot contain null.",
                        nameof(levels));
                }

                if (levelsById.ContainsKey(definition.LevelStableId))
                {
                    throw new ArgumentException(
                        "Level identities must be unique.",
                        nameof(levels));
                }

                levelsById.Add(definition.LevelStableId, definition);
            }

            this.levels =
                new ReadOnlyCollection<LevelSelectionDefinitionV1>(ordered);
            Fingerprint = ComputeFingerprint(ordered);
        }

        public IReadOnlyList<LevelSelectionDefinitionV1> Levels
        {
            get { return levels; }
        }

        public string Fingerprint { get; }

        public bool TryGet(
            StableId levelStableId,
            out LevelSelectionDefinitionV1 definition)
        {
            if (levelStableId == null)
            {
                definition = null;
                return false;
            }

            return levelsById.TryGetValue(levelStableId, out definition);
        }

        internal static int OrdinalHash(string value)
        {
            return StringComparer.Ordinal.GetHashCode(value ?? string.Empty);
        }

        private static string ComputeFingerprint(
            IList<LevelSelectionDefinitionV1> ordered)
        {
            var canonical = new StringBuilder();
            canonical.Append("level-selection-catalog-v1");
            canonical.Append('|');
            canonical.Append(
                ordered.Count.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < ordered.Count; index++)
            {
                canonical.Append('|');
                canonical.Append(ordered[index].ToCanonicalString());
            }

            byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(bytes);
                var hex = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                {
                    hex.Append(
                        digest[index].ToString(
                            "x2",
                            CultureInfo.InvariantCulture));
                }

                return "sha256:" + hex;
            }
        }
    }
}
