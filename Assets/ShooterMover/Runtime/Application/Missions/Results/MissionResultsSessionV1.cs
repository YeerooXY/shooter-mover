using System;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;

namespace ShooterMover.Application.Missions.Results
{
    /// <summary>
    /// Read-only Results handoff. Construction freezes the exact payload reference;
    /// reads have no authority callbacks and therefore cannot open or grant anything.
    /// </summary>
    public sealed class MissionResultsSessionV1
    {
        private readonly MissionResultPayloadV1 payload;

        public MissionResultsSessionV1(MissionResultPayloadV1 payload)
        {
            this.payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public MissionResultPayloadV1 Snapshot { get { return payload; } }
        public PlayerRouteProfilePayloadV1 RoutePayload { get { return payload.RoutePayload; } }
        public int CollectedStrongboxCount { get { return payload.Strongboxes.Count; } }
        public int UnopenedStrongboxCount { get { return payload.UnopenedStrongboxes.Count; } }
        public int OpenedStrongboxCount { get { return payload.OpenedStrongboxes.Count; } }
    }
}
