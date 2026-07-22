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
            : this(
                definitionStableId,
                dropProfileStableId,
                definitionFingerprint,
                null,
                null)
        {
        }

        public DestructiblePropTerminalProvenanceV1(
            StableId definitionStableId,
            StableId dropProfileStableId,
            string definitionFingerprint,
            StableId roomStableId,
            StableId placementStableId)
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
            if ((roomStableId == null) != (placementStableId == null))
            {
                throw new ArgumentException(
                    "Room and placement provenance must be supplied together.");
            }
            DefinitionFingerprint = definitionFingerprint.Trim();
            RoomStableId = roomStableId;
            PlacementStableId = placementStableId;
        }

        public StableId DefinitionStableId { get; }
        public StableId DropProfileStableId { get; }
        public string DefinitionFingerprint { get; }
        public StableId RoomStableId { get; }
        public StableId PlacementStableId { get; }
        public bool HasPlacementProvenance
        {
            get { return RoomStableId != null && PlacementStableId != null; }
        }

        public DestructiblePropTerminalProvenanceV1 WithPlacement(
            StableId roomStableId,
            StableId placementStableId)
        {
            return new DestructiblePropTerminalProvenanceV1(
                DefinitionStableId,
                DropProfileStableId,
                DefinitionFingerprint,
                roomStableId,
                placementStableId);
        }
    }
}
