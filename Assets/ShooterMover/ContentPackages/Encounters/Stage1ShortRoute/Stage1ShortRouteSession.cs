using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Encounters;
using ShooterMover.Contracts.Identity;
using ShooterMover.Contracts.Mission;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Encounters.Stage1ShortRoute
{
    public enum Stage1ShortRouteSessionDisposition
    {
        Applied = 1,
        NoChange = 2,
        Rejected = 3,
    }

    public sealed class Stage1ShortRouteSessionTransition
    {
        private Stage1ShortRouteSessionTransition(
            string operation,
            Stage1ShortRouteSessionDisposition disposition,
            string reason,
            string previousMarkerId,
            string currentMarkerId)
        {
            Operation = operation;
            Disposition = disposition;
            Reason = reason;
            PreviousMarkerId = previousMarkerId;
            CurrentMarkerId = currentMarkerId;
        }

        public string Operation { get; }

        public Stage1ShortRouteSessionDisposition Disposition { get; }

        public string Reason { get; }

        public string PreviousMarkerId { get; }

        public string CurrentMarkerId { get; }

        public bool WasApplied
        {
            get { return Disposition == Stage1ShortRouteSessionDisposition.Applied; }
        }

        public bool WasRejected
        {
            get { return Disposition == Stage1ShortRouteSessionDisposition.Rejected; }
        }

        public string ToCanonicalString()
        {
            return "operation=" + Operation
                + "\ndisposition=" + Disposition
                + "\nreason=" + (Reason ?? "none")
                + "\nprevious_marker=" + (PreviousMarkerId ?? "none")
                + "\ncurrent_marker=" + (CurrentMarkerId ?? "none");
        }

        internal static Stage1ShortRouteSessionTransition Applied(
            string operation,
            string previousMarkerId,
            string currentMarkerId)
        {
            return new Stage1ShortRouteSessionTransition(
                operation,
                Stage1ShortRouteSessionDisposition.Applied,
                null,
                previousMarkerId,
                currentMarkerId);
        }

        internal static Stage1ShortRouteSessionTransition NoChange(
            string operation,
            string reason,
            string markerId)
        {
            return new Stage1ShortRouteSessionTransition(
                operation,
                Stage1ShortRouteSessionDisposition.NoChange,
                reason,
                markerId,
                markerId);
        }

        internal static Stage1ShortRouteSessionTransition Rejected(
            string operation,
            string reason,
            string markerId)
        {
            return new Stage1ShortRouteSessionTransition(
                operation,
                Stage1ShortRouteSessionDisposition.Rejected,
                reason,
                markerId,
                markerId);
        }
    }

    /// <summary>
    /// Disposable EN-011 route attempt. It composes Room Projection v1 and Encounter
    /// Lifecycle v1 without loading scenes, spawning Unity objects, or owning durable state.
    /// </summary>
    public sealed class Stage1ShortRouteSession
    {
        private const string DefinitionFingerprint =
            "sha256:8c1e3a5f7b9d0f2a4c6e8b1d3f5a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a";

        private readonly Stage1ShortRouteComposition composition;
        private readonly StableId runId;
        private readonly HashSet<string> activeEnemyIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> activeHazardIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> activeProjectileIds =
            new HashSet<string>(StringComparer.Ordinal);

        private int generation;
        private int cursorIndex;
        private int visitSequence;
        private int completionEventCount;
        private Stage1ShortRouteRoomProjection currentProjection;
        private RouteEncounterRuntime currentRuntime;

        private Stage1ShortRouteSession(
            Stage1ShortRouteComposition composition,
            StableId runId)
        {
            this.composition = composition ?? throw new ArgumentNullException(nameof(composition));
            this.runId = runId ?? throw new ArgumentNullException(nameof(runId));
            generation = 1;
            cursorIndex = -1;
        }

        public int Generation
        {
            get { return generation; }
        }

        public string RunId
        {
            get { return runId.ToString(); }
        }

        public string CompositionFingerprint
        {
            get { return composition.Fingerprint; }
        }

        public string CurrentMarkerId
        {
            get { return currentProjection == null ? string.Empty : currentProjection.Definition.MarkerId; }
        }

        public string CurrentRoomKind
        {
            get
            {
                return currentProjection == null
                    ? string.Empty
                    : currentProjection.Definition.Kind.ToString();
            }
        }

        public string CurrentEncounterPhase
        {
            get
            {
                return currentRuntime == null
                    ? string.Empty
                    : currentRuntime.Lifecycle.Phase.ToString();
            }
        }

        public string CurrentLockdownState
        {
            get
            {
                return currentRuntime == null
                    ? EncounterLockdownState.Released.ToString()
                    : currentRuntime.Lifecycle.LockdownState.ToString();
            }
        }

        public int ActiveEnemyCount
        {
            get { return activeEnemyIds.Count; }
        }

        public int ActiveHazardCount
        {
            get { return activeHazardIds.Count; }
        }

        public int ActiveProjectileCount
        {
            get { return activeProjectileIds.Count; }
        }

        public int CompletionEventCount
        {
            get { return completionEventCount; }
        }

        public static Stage1ShortRouteSession CreateApproved(string runId)
        {
            return new Stage1ShortRouteSession(
                Stage1ShortRouteComposition.Approved,
                StableId.Parse(runId));
        }

        public string[] GetRoomOrder()
        {
            string[] result = new string[composition.Rooms.Count];
            for (int index = 0; index < composition.Rooms.Count; index++)
            {
                result[index] = composition.Rooms[index].MarkerId;
            }

            return result;
        }

        public string[] GetRoomParticipantRoleIds(string markerId)
        {
            Stage1ShortRouteRoomDefinition room = composition.GetRoom(markerId);
            string[] result = new string[room.Participants.Count];
            for (int index = 0; index < room.Participants.Count; index++)
            {
                result[index] = room.Participants[index].RoleId.ToString();
            }

            return result;
        }

        public string GetRoomHazardCanonical(string markerId)
        {
            Stage1ShortRouteRoomDefinition room = composition.GetRoom(markerId);
            return room.Hazards.Count == 0
                ? string.Empty
                : room.Hazards[0].ToCanonicalString();
        }

        public string GetProjectionCanonical(string markerId)
        {
            return composition.CreateProjection(markerId, runId, generation).ToCanonicalString();
        }

        public string[] GetActiveEnemyIds()
        {
            return CopySorted(activeEnemyIds);
        }

        public string[] GetActiveHazardIds()
        {
            return CopySorted(activeHazardIds);
        }

        public string[] GetActiveProjectileIds()
        {
            return CopySorted(activeProjectileIds);
        }

        public Stage1ShortRouteSessionTransition EnterNextRoom()
        {
            string previousMarker = CurrentMarkerId;
            if (currentRuntime != null
                && !currentRuntime.Lifecycle.IsCompleted
                && currentRuntime.Lifecycle.ActiveParticipantCount != 0)
            {
                return Stage1ShortRouteSessionTransition.Rejected(
                    "enter-next-room",
                    "participants-remain",
                    previousMarker);
            }

            int nextIndex = cursorIndex + 1;
            if (nextIndex >= composition.Rooms.Count)
            {
                return Stage1ShortRouteSessionTransition.Rejected(
                    "enter-next-room",
                    "route-end-reached",
                    previousMarker);
            }

            ClearRuntimeTokens();
            cursorIndex = nextIndex;
            Stage1ShortRouteRoomDefinition definition = composition.Rooms[cursorIndex];
            currentProjection = composition.CreateProjection(
                definition.MarkerId,
                runId,
                generation);

            if (definition.Kind == Stage1ShortRouteRoomKind.ProjectionOnly)
            {
                currentRuntime = null;
                return Stage1ShortRouteSessionTransition.Applied(
                    "enter-next-room",
                    previousMarker,
                    CurrentMarkerId);
            }

            visitSequence++;
            currentRuntime = CreateRuntime(definition, currentProjection, visitSequence);
            for (int index = 0; index < currentRuntime.ActorIds.Count; index++)
            {
                activeEnemyIds.Add(currentRuntime.ActorIds[index].ToString());
            }

            for (int index = 0; index < definition.Hazards.Count; index++)
            {
                activeHazardIds.Add(definition.Hazards[index].HazardId.ToString());
            }

            return Stage1ShortRouteSessionTransition.Applied(
                "enter-next-room",
                previousMarker,
                CurrentMarkerId);
        }

        public Stage1ShortRouteSessionTransition RetreatCurrentRoom()
        {
            string markerId = CurrentMarkerId;
            if (currentRuntime == null)
            {
                return Stage1ShortRouteSessionTransition.Rejected(
                    "retreat-current-room",
                    "no-active-encounter",
                    markerId);
            }

            if (currentRuntime.Lifecycle.IsCompleted)
            {
                return Stage1ShortRouteSessionTransition.Rejected(
                    "retreat-current-room",
                    "encounter-already-completed",
                    markerId);
            }

            if (currentRuntime.Lifecycle.LockdownState == EncounterLockdownState.Engaged)
            {
                return Stage1ShortRouteSessionTransition.Rejected(
                    "retreat-current-room",
                    "lockdown-active",
                    markerId);
            }

            if (!currentRuntime.Definition.AllowsRetreat || cursorIndex <= 0)
            {
                return Stage1ShortRouteSessionTransition.Rejected(
                    "retreat-current-room",
                    "retreat-not-allowed",
                    markerId);
            }

            EncounterRetreatMessage retreatMessage = new EncounterRetreatMessage(
                currentRuntime.Identity,
                StableId.Create("encounter-message", currentRuntime.Token + "-retreat"),
                StableId.Parse("route-controller.en011"),
                EncounterRetreatReason.TacticalWithdrawal);
            EncounterLifecycleTransition retreat =
                currentRuntime.Lifecycle.BeginRetreat(retreatMessage);
            if (retreat.Kind == EncounterTransitionKind.Rejected)
            {
                return Stage1ShortRouteSessionTransition.Rejected(
                    "retreat-current-room",
                    retreat.Rejection.ToString(),
                    markerId);
            }

            currentRuntime.Lifecycle = retreat.Next;
            for (int index = 0; index < currentRuntime.ActorIds.Count; index++)
            {
                StableId actorId = currentRuntime.ActorIds[index];
                EncounterWithdrawalMessage withdrawal = new EncounterWithdrawalMessage(
                    currentRuntime.Identity,
                    StableId.Create(
                        "encounter-message",
                        currentRuntime.Token + "-withdraw-" + index.ToString(CultureInfo.InvariantCulture)),
                    actorId,
                    EncounterWithdrawalReason.Retreat);
                EncounterLifecycleTransition resolution =
                    currentRuntime.Lifecycle.RecordWithdrawal(withdrawal);
                if (resolution.Kind == EncounterTransitionKind.Rejected)
                {
                    return Stage1ShortRouteSessionTransition.Rejected(
                        "retreat-current-room",
                        resolution.Rejection.ToString(),
                        markerId);
                }

                currentRuntime.Lifecycle = resolution.Next;
            }

            ClearRuntimeTokens();
            cursorIndex--;
            Stage1ShortRouteRoomDefinition previousRoom = composition.Rooms[cursorIndex];
            currentProjection = composition.CreateProjection(
                previousRoom.MarkerId,
                runId,
                generation);
            currentRuntime = null;
            return Stage1ShortRouteSessionTransition.Applied(
                "retreat-current-room",
                markerId,
                CurrentMarkerId);
        }

        public Stage1ShortRouteSessionTransition CompleteCurrentEncounter()
        {
            string markerId = CurrentMarkerId;
            if (currentRuntime == null)
            {
                return Stage1ShortRouteSessionTransition.Rejected(
                    "complete-current-encounter",
                    "no-active-encounter",
                    markerId);
            }

            if (currentRuntime.CompletionMessage != null)
            {
                EncounterLifecycleTransition repeat =
                    currentRuntime.Lifecycle.Complete(currentRuntime.CompletionMessage);
                if (repeat.Kind == EncounterTransitionKind.NoChange)
                {
                    return Stage1ShortRouteSessionTransition.NoChange(
                        "complete-current-encounter",
                        "completion-already-recorded",
                        markerId);
                }

                return Stage1ShortRouteSessionTransition.Rejected(
                    "complete-current-encounter",
                    repeat.Rejection.ToString(),
                    markerId);
            }

            for (int index = 0; index < currentRuntime.ActorIds.Count; index++)
            {
                StableId actorId = currentRuntime.ActorIds[index];
                VitalMessage destroyed = new VitalMessage(
                    StableId.Create(
                        "combat-event",
                        currentRuntime.Token + "-destroy-" + index.ToString(CultureInfo.InvariantCulture)),
                    StableId.Parse("actor.player-primary"),
                    actorId,
                    CombatChannel.Kinetic,
                    VitalResult.Destroyed,
                    new VitalState(0d, 100d, 0d, 0d));
                EncounterLifecycleTransition resolution =
                    currentRuntime.Lifecycle.RecordCombatResolution(
                        new EncounterCombatResolutionMessage(currentRuntime.Identity, destroyed));
                if (resolution.Kind == EncounterTransitionKind.Rejected)
                {
                    return Stage1ShortRouteSessionTransition.Rejected(
                        "complete-current-encounter",
                        resolution.Rejection.ToString(),
                        markerId);
                }

                currentRuntime.Lifecycle = resolution.Next;
            }

            if (currentRuntime.Lifecycle.LockdownState == EncounterLockdownState.Engaged)
            {
                EncounterLockdownMessage release = new EncounterLockdownMessage(
                    currentRuntime.Identity,
                    StableId.Create("encounter-message", currentRuntime.Token + "-lockdown-release"),
                    EncounterLockdownState.Released,
                    EncounterLockdownReason.RouteControl);
                EncounterLifecycleTransition released =
                    currentRuntime.Lifecycle.ApplyLockdown(release);
                if (released.Kind == EncounterTransitionKind.Rejected)
                {
                    return Stage1ShortRouteSessionTransition.Rejected(
                        "complete-current-encounter",
                        released.Rejection.ToString(),
                        markerId);
                }

                currentRuntime.Lifecycle = released.Next;
            }

            currentRuntime.CompletionMessage = CreateCompletionMessage(currentRuntime);
            EncounterLifecycleTransition completion =
                currentRuntime.Lifecycle.Complete(currentRuntime.CompletionMessage);
            if (completion.Kind != EncounterTransitionKind.Applied)
            {
                return Stage1ShortRouteSessionTransition.Rejected(
                    "complete-current-encounter",
                    completion.Rejection.ToString(),
                    markerId);
            }

            currentRuntime.Lifecycle = completion.Next;
            completionEventCount++;
            ClearRuntimeTokens();
            return Stage1ShortRouteSessionTransition.Applied(
                "complete-current-encounter",
                markerId,
                markerId);
        }

        public Stage1ShortRouteSessionTransition RegisterProjectile(string projectileId)
        {
            string markerId = CurrentMarkerId;
            if (currentRuntime == null
                || currentRuntime.Lifecycle.IsCompleted
                || currentRuntime.Lifecycle.ActiveParticipantCount == 0)
            {
                return Stage1ShortRouteSessionTransition.Rejected(
                    "register-projectile",
                    "encounter-not-active",
                    markerId);
            }

            StableId parsed = StableId.Parse(projectileId);
            if (!activeProjectileIds.Add(parsed.ToString()))
            {
                return Stage1ShortRouteSessionTransition.NoChange(
                    "register-projectile",
                    "projectile-already-registered",
                    markerId);
            }

            return Stage1ShortRouteSessionTransition.Applied(
                "register-projectile",
                markerId,
                markerId);
        }

        public Stage1ShortRouteSessionTransition Restart()
        {
            string previousMarker = CurrentMarkerId;
            generation++;
            cursorIndex = -1;
            visitSequence = 0;
            completionEventCount = 0;
            currentProjection = null;
            currentRuntime = null;
            ClearRuntimeTokens();
            return Stage1ShortRouteSessionTransition.Applied(
                "restart",
                previousMarker,
                string.Empty);
        }

        public string CaptureRuntimeState()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("composition_fingerprint=").Append(CompositionFingerprint)
                .Append("\nrun_id=").Append(runId)
                .Append("\ngeneration=").Append(generation.ToString(CultureInfo.InvariantCulture))
                .Append("\ncursor_index=").Append(cursorIndex.ToString(CultureInfo.InvariantCulture))
                .Append("\ncurrent_marker=").Append(CurrentMarkerId.Length == 0 ? "none" : CurrentMarkerId)
                .Append("\ncurrent_phase=").Append(CurrentEncounterPhase.Length == 0 ? "none" : CurrentEncounterPhase)
                .Append("\nlockdown=").Append(CurrentLockdownState)
                .Append("\ncompletion_event_count=")
                .Append(completionEventCount.ToString(CultureInfo.InvariantCulture))
                .Append("\nactive_enemies=").Append(Join(CopySorted(activeEnemyIds)))
                .Append("\nactive_hazards=").Append(Join(CopySorted(activeHazardIds)))
                .Append("\nactive_projectiles=").Append(Join(CopySorted(activeProjectileIds)));
            return builder.ToString();
        }

        private RouteEncounterRuntime CreateRuntime(
            Stage1ShortRouteRoomDefinition definition,
            Stage1ShortRouteRoomProjection projection,
            int visit)
        {
            string token = "g" + generation.ToString(CultureInfo.InvariantCulture)
                + "-v" + visit.ToString(CultureInfo.InvariantCulture)
                + "-r" + definition.LoadOrder.ToString(CultureInfo.InvariantCulture);
            EncounterRuntimeIdentity identity = new EncounterRuntimeIdentity(
                definition.EncounterId,
                StableId.Create("encounter-runtime", "en011-" + token),
                runId,
                projection.Identity);
            List<StableId> actorIds = new List<StableId>();
            List<EncounterParticipantEntry> entries = new List<EncounterParticipantEntry>();
            for (int index = 0; index < definition.Participants.Count; index++)
            {
                StableId actorId = StableId.Create(
                    "actor",
                    "en011-" + token + "-s" + index.ToString(CultureInfo.InvariantCulture));
                actorIds.Add(actorId);
                entries.Add(
                    new EncounterParticipantEntry(
                        StableId.Create(
                            "entry",
                            "en011-" + token + "-s" + index.ToString(CultureInfo.InvariantCulture)),
                        actorId,
                        definition.Participants[index].RoleId,
                        index));
            }

            EncounterPerformanceBudget budget = new EncounterPerformanceBudget(
                Math.Max(2, entries.Count),
                0,
                32,
                16.667d);
            EncounterStartMessage start = new EncounterStartMessage(
                identity,
                StableId.Create("encounter-message", "en011-" + token + "-start"),
                budget,
                entries);
            EncounterLifecycle lifecycle = EncounterLifecycle.Create(identity).Start(start).Next;
            if (definition.LocksOnEntry)
            {
                EncounterLockdownMessage lockdown = new EncounterLockdownMessage(
                    identity,
                    StableId.Create("encounter-message", "en011-" + token + "-lockdown-engage"),
                    EncounterLockdownState.Engaged,
                    EncounterLockdownReason.EncounterRule);
                lifecycle = lifecycle.ApplyLockdown(lockdown).Next;
            }

            return new RouteEncounterRuntime(
                definition,
                projection,
                identity,
                lifecycle,
                actorIds,
                token,
                visit);
        }

        private EncounterCompletionMessage CreateCompletionMessage(RouteEncounterRuntime runtime)
        {
            long sequence = ((long)generation * 100000L)
                + ((long)runtime.VisitSequence * 100L)
                + runtime.Definition.LoadOrder
                + 1L;
            MissionPayloadVersion version = new MissionPayloadVersion(
                1,
                ContentVersion.Create(1, DefinitionFingerprint));
            MissionEventEnvelope durableEvent = new MissionEventEnvelope(
                StableId.Create("mission-event", "en011-" + runtime.Token + "-room-cleared"),
                StableId.Create("mission-command", "en011-" + runtime.Token + "-room-clear"),
                runId,
                version,
                new MissionSequence(sequence),
                new RoomClearedEvent(
                    runtime.Definition.RoomId,
                    runtime.Definition.EncounterId));
            return new EncounterCompletionMessage(runtime.Identity, durableEvent);
        }

        private void ClearRuntimeTokens()
        {
            activeEnemyIds.Clear();
            activeHazardIds.Clear();
            activeProjectileIds.Clear();
        }

        private static string[] CopySorted(HashSet<string> source)
        {
            string[] result = new string[source.Count];
            source.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private static string Join(string[] values)
        {
            return values.Length == 0 ? "none" : string.Join(",", values);
        }

        private sealed class RouteEncounterRuntime
        {
            private readonly ReadOnlyCollection<StableId> actorIds;

            public RouteEncounterRuntime(
                Stage1ShortRouteRoomDefinition definition,
                Stage1ShortRouteRoomProjection projection,
                EncounterRuntimeIdentity identity,
                EncounterLifecycle lifecycle,
                IList<StableId> actorIds,
                string token,
                int visitSequence)
            {
                Definition = definition ?? throw new ArgumentNullException(nameof(definition));
                Projection = projection ?? throw new ArgumentNullException(nameof(projection));
                Identity = identity ?? throw new ArgumentNullException(nameof(identity));
                Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
                this.actorIds = new ReadOnlyCollection<StableId>(
                    new List<StableId>(actorIds ?? throw new ArgumentNullException(nameof(actorIds))));
                Token = string.IsNullOrWhiteSpace(token)
                    ? throw new ArgumentException("A runtime token is required.", nameof(token))
                    : token;
                VisitSequence = visitSequence;
            }

            public Stage1ShortRouteRoomDefinition Definition { get; }

            public Stage1ShortRouteRoomProjection Projection { get; }

            public EncounterRuntimeIdentity Identity { get; }

            public EncounterLifecycle Lifecycle { get; set; }

            public IReadOnlyList<StableId> ActorIds
            {
                get { return actorIds; }
            }

            public string Token { get; }

            public int VisitSequence { get; }

            public EncounterCompletionMessage CompletionMessage { get; set; }
        }
    }
}
