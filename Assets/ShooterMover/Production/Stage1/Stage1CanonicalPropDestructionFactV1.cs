using System;
using ShooterMover.Domain.Common;
using ShooterMover.TerminalDropBinding;
using ShooterMover.ContentPackages.Props.DestructibleProps;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Immutable Stage 1 prop terminal fact with exact run, room, placement and
    /// participant attribution. It exposes placement provenance directly so the
    /// generic reward facade never classifies props from names, health or visuals.
    /// </summary>
    internal sealed class Stage1CanonicalPropDestructionFactV1 :
        ITerminalRewardPlacementFactV1
    {
        private readonly string placementFingerprint;

        public Stage1CanonicalPropDestructionFactV1(
            DestructiblePropDestructionResult destruction,
            Stage1CanonicalPropTerminalSourceV1 provenance,
            StableId runStableId,
            long lifecycleGeneration,
            StableId attributedParticipantStableId,
            Vector2 terminalPosition,
            string positionFingerprint)
        {
            Destruction = destruction
                ?? throw new ArgumentNullException(nameof(destruction));
            Provenance = provenance
                ?? throw new ArgumentNullException(nameof(provenance));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (lifecycleGeneration <= 0L || lifecycleGeneration > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            LifecycleGeneration = lifecycleGeneration;
            AttributedParticipantStableId = attributedParticipantStableId;
            TerminalPosition = terminalPosition;
            if (string.IsNullOrWhiteSpace(positionFingerprint))
            {
                throw new ArgumentException(
                    "Prop terminal position fingerprint is required.",
                    nameof(positionFingerprint));
            }
            PositionFingerprint = positionFingerprint.Trim();
            placementFingerprint = TerminalDropCanonicalV1.Hash(
                PositionFingerprint
                + "|"
                + RoomStableId
                + "|"
                + PlacementStableId
                + "|"
                + LifecycleGeneration);
        }

        public DestructiblePropDestructionResult Destruction { get; }
        public Stage1CanonicalPropTerminalSourceV1 Provenance { get; }
        public StableId RunStableId { get; }
        public long LifecycleGeneration { get; }
        public StableId RoomStableId { get { return Provenance.RoomStableId; } }
        public StableId PlacementStableId
        {
            get { return Provenance.PlacementStableId; }
        }
        public StableId AttributedParticipantStableId { get; }
        public Vector2 TerminalPosition { get; }
        public string PositionFingerprint { get; }

        public StableId RewardTerminalEventStableId
        {
            get { return Destruction.EventId; }
        }
        public StableId RewardRoomStableId { get { return RoomStableId; } }
        public int RewardRoomLifecycleGeneration
        {
            get { return checked((int)LifecycleGeneration); }
        }
        public StableId RewardPlacementStableId
        {
            get { return PlacementStableId; }
        }
        public string RewardPlacementFingerprint
        {
            get { return placementFingerprint; }
        }
    }
}
