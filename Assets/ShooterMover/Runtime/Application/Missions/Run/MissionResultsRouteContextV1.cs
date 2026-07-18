using System;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Run;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Missions.Run
{
    /// <summary>
    /// Narrow validated cross-scene handoff. It contains frozen result/session data and
    /// never calls gameplay, reward, inventory, XP, or strongbox authorities on read.
    /// </summary>
    public sealed class MissionResultsRoutePayloadV1
    {
        public MissionResultsRoutePayloadV1(
            MissionResultsSessionV1 session,
            PlayerRouteProfilePayloadV1 routePayload,
            StableId selectedModeStableId,
            StableId selectedLevelStableId,
            LevelRunSummaryV1 summary)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            RoutePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            if (!routePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The Results route payload fingerprint is invalid.",
                    nameof(routePayload));
            }

            SelectedModeStableId = selectedModeStableId
                ?? throw new ArgumentNullException(nameof(selectedModeStableId));
            SelectedLevelStableId = selectedLevelStableId
                ?? throw new ArgumentNullException(nameof(selectedLevelStableId));
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            if (!summary.HasValidFingerprint()
                || summary.RoutePayload.Fingerprint != routePayload.Fingerprint
                || summary.RunStableId != session.Snapshot.RunStableId
                || session.RoutePayload.Fingerprint != routePayload.Fingerprint)
            {
                throw new ArgumentException(
                    "Mission Results handoff inputs do not describe one frozen run.");
            }
        }

        public MissionResultsSessionV1 Session { get; }
        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public StableId SelectedModeStableId { get; }
        public StableId SelectedLevelStableId { get; }
        public LevelRunSummaryV1 Summary { get; }
    }

    public static class MissionResultsRouteContextV1
    {
        private static readonly object Gate = new object();
        private static MissionResultsRoutePayloadV1 current;

        public static bool HasValue
        {
            get
            {
                lock (Gate)
                {
                    return current != null;
                }
            }
        }

        public static void Capture(
            MissionResultsSessionV1 session,
            PlayerRouteProfilePayloadV1 routePayload,
            StableId selectedModeStableId,
            StableId selectedLevelStableId,
            LevelRunSummaryV1 summary)
        {
            var payload = new MissionResultsRoutePayloadV1(
                session,
                routePayload,
                selectedModeStableId,
                selectedLevelStableId,
                summary);
            lock (Gate)
            {
                current = payload;
            }
        }

        public static bool TryRead(out MissionResultsRoutePayloadV1 payload)
        {
            lock (Gate)
            {
                payload = current;
                return payload != null;
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                current = null;
            }
        }
    }
}
