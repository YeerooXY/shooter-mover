using System;
using System.Collections.Generic;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Content.Definitions.Strongboxes
{
    /// <summary>
    /// Thin content-boundary wrapper for an arbitrary authored strongbox tier set.
    /// It intentionally contains no built-in tier enum or production catalog values.
    /// </summary>
    public sealed class StrongboxDefinitionSet
    {
        public StrongboxDefinitionSet(IEnumerable<StrongboxDefinition> definitions)
        {
            Catalog = new StrongboxDefinitionCatalog(
                definitions ?? throw new ArgumentNullException(nameof(definitions)));
        }

        public StrongboxDefinitionCatalog Catalog { get; }
        public string Fingerprint { get { return Catalog.Fingerprint; } }
    }
}
