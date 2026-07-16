using System;
using System.Collections.Generic;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Content.Definitions.Strongboxes
{
    /// <summary>
    /// Thin content-boundary wrapper for an arbitrary authored strongbox tier set.
    /// It intentionally contains no built-in tier enum or production catalog values.
    /// </summary>
    public sealed class StrongboxDefinitionSetV1
    {
        public StrongboxDefinitionSetV1(IEnumerable<StrongboxDefinitionV1> definitions)
        {
            Catalog = new StrongboxDefinitionCatalogV1(
                definitions ?? throw new ArgumentNullException(nameof(definitions)));
        }

        public StrongboxDefinitionCatalogV1 Catalog { get; }
        public string Fingerprint { get { return Catalog.Fingerprint; } }
    }
}
