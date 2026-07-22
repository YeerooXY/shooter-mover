using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    /// <summary>
    /// Immutable definition and reward provenance attached when a destructible prop runtime is
    /// configured. Terminal consumers copy this exact value; they never classify a prop from HP,
    /// presentation, object names, or destruction behavior.
    /// </summary>
    public sealed class DestructiblePropTerminalProvenanceV1
    {
        public DestructiblePropTerminalProvenanceV1(
            StableId definitionStableId,
            StableId dropProfileStableId,
            string definitionFingerprint)
        {
            DefinitionStableId = definitionStableId
                ?? throw new ArgumentNullException(nameof(definitionStableId));
            DropProfileStableId = dropProfileStableId
                ?? throw new ArgumentNullException(nameof(dropProfileStableId));
            if (string.IsNullOrWhiteSpace(definitionFingerprint))
            {
                throw new ArgumentException(
                    "A canonical prop-definition fingerprint is required.",
                    nameof(definitionFingerprint));
            }
            DefinitionFingerprint = definitionFingerprint.Trim();
        }

        public StableId DefinitionStableId { get; }
        public StableId DropProfileStableId { get; }
        public string DefinitionFingerprint { get; }
    }
}
