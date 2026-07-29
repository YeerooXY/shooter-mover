using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Props.Breakables
{
    /// <summary>
    /// Immutable definition and reward provenance attached when a breakable runtime is
    /// configured. Terminal consumers copy this exact value; they never classify a prop from HP,
    /// presentation, object names, or destruction behavior.
    /// </summary>
    public sealed class BreakableTerminalProvenance
    {
        public BreakableTerminalProvenance(
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

        public BreakableTerminalProvenance(
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

        public BreakableTerminalProvenance WithPlacement(
            StableId roomStableId,
            StableId placementStableId)
        {
            return new BreakableTerminalProvenance(
                DefinitionStableId,
                DropProfileStableId,
                DefinitionFingerprint,
                roomStableId,
                placementStableId);
        }
    }
}
