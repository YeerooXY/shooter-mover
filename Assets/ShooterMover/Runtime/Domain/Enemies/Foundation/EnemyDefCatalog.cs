using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Enemies.Foundation
{
    public sealed class EnemyDefCatalog
    {
        private readonly ReadOnlyCollection<EnemyDef> definitions;
        private readonly Dictionary<StableId, EnemyDef> byId;

        public EnemyDefCatalog(IEnumerable<EnemyDef> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            var copy = new List<EnemyDef>();
            byId = new Dictionary<StableId, EnemyDef>();
            foreach (EnemyDef definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException(
                        "Enemy catalogs cannot contain null definitions.",
                        nameof(definitions));
                }
                if (byId.ContainsKey(definition.Id))
                {
                    throw new ArgumentException(
                        "Enemy definition ID is duplicated: " + definition.Id,
                        nameof(definitions));
                }
                byId.Add(definition.Id, definition);
                copy.Add(definition);
            }

            copy.Sort(delegate(EnemyDef left, EnemyDef right)
            {
                return left.Id.CompareTo(right.Id);
            });
            this.definitions = new ReadOnlyCollection<EnemyDef>(copy);
        }

        public IReadOnlyList<EnemyDef> Definitions { get { return definitions; } }

        public bool TryGet(StableId id, out EnemyDef definition)
        {
            definition = null;
            return id != null
                && byId.TryGetValue(id, out definition)
                && definition != null;
        }
    }
}
