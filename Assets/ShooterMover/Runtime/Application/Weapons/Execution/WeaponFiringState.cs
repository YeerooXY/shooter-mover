using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public sealed class WeaponFiringTrackState
    {
        internal WeaponFiringTrackState(
            WeaponActorInstanceId actorId,
            RunParticipantId participantId,
            EquipmentInstanceId equipmentInstanceId,
            WeaponDefinitionId weaponDefinitionId,
            LifecycleGeneration lifecycleGeneration,
            string effectiveWeaponFingerprint,
            bool triggerHeld,
            bool hasCadencePhase,
            long cadenceOriginTick,
            long nextCadenceOrdinal,
            long nextCadenceTick,
            long nextGlobalShotSequence,
            long lastObservedSimulationTick)
            : this(actorId, participantId, equipmentInstanceId, weaponDefinitionId,
                lifecycleGeneration, effectiveWeaponFingerprint, triggerHeld,
                hasCadencePhase, cadenceOriginTick, nextCadenceOrdinal,
                nextCadenceTick, nextGlobalShotSequence, lastObservedSimulationTick,
                0L, 0L, 0L, -1L, string.Empty)
        {
        }

        internal WeaponFiringTrackState(
            WeaponActorInstanceId actorId,
            RunParticipantId participantId,
            EquipmentInstanceId equipmentInstanceId,
            WeaponDefinitionId weaponDefinitionId,
            LifecycleGeneration lifecycleGeneration,
            string effectiveWeaponFingerprint,
            bool triggerHeld,
            bool hasCadencePhase,
            long cadenceOriginTick,
            long nextCadenceOrdinal,
            long nextCadenceTick,
            long nextGlobalShotSequence,
            long lastObservedSimulationTick,
            long nextOperationSequence,
            long firstRetainedOperationSequence,
            long firstRetainedShotSequence,
            long replayRetentionFloor,
            string cumulativeHistoryFingerprint)
        {
            ActorId = actorId;
            ParticipantId = participantId;
            EquipmentInstanceId = equipmentInstanceId;
            WeaponDefinitionId = weaponDefinitionId;
            LifecycleGeneration = lifecycleGeneration;
            EffectiveWeaponFingerprint = effectiveWeaponFingerprint ?? string.Empty;
            TriggerHeld = triggerHeld;
            HasCadencePhase = hasCadencePhase;
            CadenceOriginTick = cadenceOriginTick;
            NextCadenceOrdinal = nextCadenceOrdinal;
            NextCadenceTick = nextCadenceTick;
            NextGlobalShotSequence = nextGlobalShotSequence;
            LastObservedSimulationTick = lastObservedSimulationTick;
            NextOperationSequence = nextOperationSequence;
            FirstRetainedOperationSequence = firstRetainedOperationSequence;
            FirstRetainedShotSequence = firstRetainedShotSequence;
            ReplayRetentionFloor = replayRetentionFloor;
            CumulativeHistoryFingerprint = cumulativeHistoryFingerprint ?? string.Empty;
            CanonicalText = BuildCanonicalText();
            Fingerprint = WeaponExecutionFingerprint.Compute(CanonicalText);
        }

        public WeaponActorInstanceId ActorId { get; }
        public RunParticipantId ParticipantId { get; }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public WeaponDefinitionId WeaponDefinitionId { get; }
        public LifecycleGeneration LifecycleGeneration { get; }
        public string EffectiveWeaponFingerprint { get; }
        public bool TriggerHeld { get; }
        public bool HasCadencePhase { get; }
        public long CadenceOriginTick { get; }
        public long NextCadenceOrdinal { get; }
        public long NextCadenceTick { get; }
        public long NextGlobalShotSequence { get; }
        public long LastObservedSimulationTick { get; }
        public long NextOperationSequence { get; }
        public long FirstRetainedOperationSequence { get; }
        public long FirstRetainedShotSequence { get; }
        public long ReplayRetentionFloor { get; }
        public string CumulativeHistoryFingerprint { get; }
        public string CanonicalText { get; }
        public string Fingerprint { get; }

        internal string IdentityKey
        {
            get
            {
                return BuildIdentityKey(ActorId, ParticipantId, EquipmentInstanceId,
                    WeaponDefinitionId, LifecycleGeneration);
            }
        }

        internal string EquipmentLifecycleKey
        {
            get
            {
                return BuildEquipmentLifecycleKey(
                    ActorId, EquipmentInstanceId, LifecycleGeneration);
            }
        }

        internal bool IsReplayExpired(long simulationTick)
        {
            return ReplayRetentionFloor >= 0L && simulationTick < ReplayRetentionFloor;
        }

        internal WeaponFiringTrackState WithTransition(
            string effectiveWeaponFingerprint,
            bool triggerHeld,
            bool hasCadencePhase,
            long cadenceOriginTick,
            long nextCadenceOrdinal,
            long nextCadenceTick,
            long nextGlobalShotSequence,
            long lastObservedSimulationTick)
        {
            return new WeaponFiringTrackState(
                ActorId, ParticipantId, EquipmentInstanceId, WeaponDefinitionId,
                LifecycleGeneration, effectiveWeaponFingerprint, triggerHeld,
                hasCadencePhase, cadenceOriginTick, nextCadenceOrdinal,
                nextCadenceTick, nextGlobalShotSequence, lastObservedSimulationTick,
                NextOperationSequence, FirstRetainedOperationSequence,
                FirstRetainedShotSequence, ReplayRetentionFloor,
                CumulativeHistoryFingerprint);
        }

        internal WeaponFiringTrackState WithNextOperationSequence(long nextOperationSequence)
        {
            return new WeaponFiringTrackState(
                ActorId, ParticipantId, EquipmentInstanceId, WeaponDefinitionId,
                LifecycleGeneration, EffectiveWeaponFingerprint, TriggerHeld,
                HasCadencePhase, CadenceOriginTick, NextCadenceOrdinal,
                NextCadenceTick, NextGlobalShotSequence, LastObservedSimulationTick,
                nextOperationSequence, FirstRetainedOperationSequence,
                FirstRetainedShotSequence, ReplayRetentionFloor,
                CumulativeHistoryFingerprint);
        }

        internal WeaponFiringTrackState WithReplayRetention(
            long firstRetainedOperationSequence,
            long firstRetainedShotSequence,
            long replayRetentionFloor,
            string cumulativeHistoryFingerprint)
        {
            return new WeaponFiringTrackState(
                ActorId, ParticipantId, EquipmentInstanceId, WeaponDefinitionId,
                LifecycleGeneration, EffectiveWeaponFingerprint, TriggerHeld,
                HasCadencePhase, CadenceOriginTick, NextCadenceOrdinal,
                NextCadenceTick, NextGlobalShotSequence, LastObservedSimulationTick,
                NextOperationSequence, firstRetainedOperationSequence,
                firstRetainedShotSequence, replayRetentionFloor,
                cumulativeHistoryFingerprint);
        }

        public bool HasValidFingerprint()
        {
            if (ActorId == null || ParticipantId == null || EquipmentInstanceId == null
                || WeaponDefinitionId == null || LifecycleGeneration == null
                || string.IsNullOrWhiteSpace(EffectiveWeaponFingerprint)
                || NextGlobalShotSequence < 0L || FirstRetainedShotSequence < 0L
                || FirstRetainedShotSequence > NextGlobalShotSequence
                || NextOperationSequence < 0L || FirstRetainedOperationSequence < 0L
                || FirstRetainedOperationSequence > NextOperationSequence
                || LastObservedSimulationTick < -1L || ReplayRetentionFloor < -1L
                || string.IsNullOrWhiteSpace(Fingerprint)
                || ((FirstRetainedOperationSequence > 0L
                        || FirstRetainedShotSequence > 0L
                        || ReplayRetentionFloor >= 0L)
                    && string.IsNullOrWhiteSpace(CumulativeHistoryFingerprint)))
            {
                return false;
            }

            if (HasCadencePhase)
            {
                if (CadenceOriginTick < 0L || NextCadenceOrdinal < 0L
                    || NextCadenceTick < CadenceOriginTick
                    || (TriggerHeld && NextCadenceTick <= LastObservedSimulationTick))
                {
                    return false;
                }
            }
            else if (CadenceOriginTick != -1L || NextCadenceOrdinal != 0L
                || NextCadenceTick != -1L || TriggerHeld)
            {
                return false;
            }

            return string.Equals(Fingerprint,
                WeaponExecutionFingerprint.Compute(BuildCanonicalText()),
                StringComparison.Ordinal);
        }

        internal static string BuildIdentityKey(
            WeaponActorInstanceId actorId,
            RunParticipantId participantId,
            EquipmentInstanceId equipmentInstanceId,
            WeaponDefinitionId weaponDefinitionId,
            LifecycleGeneration lifecycleGeneration)
        {
            return actorId + "|" + participantId + "|" + equipmentInstanceId + "|"
                + weaponDefinitionId + "|" + lifecycleGeneration;
        }

        internal static string BuildEquipmentLifecycleKey(
            WeaponActorInstanceId actorId,
            EquipmentInstanceId equipmentInstanceId,
            LifecycleGeneration lifecycleGeneration)
        {
            return actorId + "|" + equipmentInstanceId + "|" + lifecycleGeneration;
        }

        private string BuildCanonicalText()
        {
            return string.Join("\n", new[]
            {
                "actor_id=" + ActorId,
                "participant_id=" + ParticipantId,
                "equipment_instance_id=" + EquipmentInstanceId,
                "weapon_definition_id=" + WeaponDefinitionId,
                "lifecycle_generation=" + LifecycleGeneration,
                "effective_weapon_fingerprint=" + EffectiveWeaponFingerprint,
                "trigger_held=" + (TriggerHeld ? "1" : "0"),
                "has_cadence_phase=" + (HasCadencePhase ? "1" : "0"),
                "cadence_origin_tick=" + CadenceOriginTick.ToString(CultureInfo.InvariantCulture),
                "next_cadence_ordinal=" + NextCadenceOrdinal.ToString(CultureInfo.InvariantCulture),
                "next_cadence_tick=" + NextCadenceTick.ToString(CultureInfo.InvariantCulture),
                "next_global_shot_sequence=" + NextGlobalShotSequence.ToString(CultureInfo.InvariantCulture),
                "last_observed_simulation_tick=" + LastObservedSimulationTick.ToString(CultureInfo.InvariantCulture),
                "next_operation_sequence=" + NextOperationSequence.ToString(CultureInfo.InvariantCulture),
                "first_retained_operation_sequence=" + FirstRetainedOperationSequence.ToString(CultureInfo.InvariantCulture),
                "first_retained_shot_sequence=" + FirstRetainedShotSequence.ToString(CultureInfo.InvariantCulture),
                "replay_retention_floor=" + ReplayRetentionFloor.ToString(CultureInfo.InvariantCulture),
                "cumulative_history_fingerprint=" + CumulativeHistoryFingerprint,
            });
        }
    }

    public enum WeaponFiringReplayResultKind
    {
        AcceptedSchedule = 1,
        SuccessfulTransition = 2,
    }

    public sealed class WeaponFiringReplayRecord
    {
        internal WeaponFiringReplayRecord(
            WeaponActorInstanceId actorId,
            RunParticipantId participantId,
            EquipmentInstanceId equipmentInstanceId,
            WeaponDefinitionId weaponDefinitionId,
            LifecycleGeneration lifecycleGeneration,
            FireOperationId sourceFireOperationId,
            string requestFingerprint,
            string effectiveWeaponFingerprint,
            long operationSequence,
            WeaponFiringScheduler.AcceptedSchedule acceptedSchedule)
            : this(actorId, participantId, equipmentInstanceId, weaponDefinitionId,
                lifecycleGeneration, sourceFireOperationId, requestFingerprint,
                effectiveWeaponFingerprint, operationSequence,
                WeaponFiringReplayResultKind.AcceptedSchedule,
                WeaponFiringScheduleStatus.Accepted, string.Empty,
                acceptedSchedule == null ? -1L : acceptedSchedule.SourceCommand.SimulationTick,
                acceptedSchedule, string.Empty)
        {
        }

        internal WeaponFiringReplayRecord(
            WeaponActorInstanceId actorId,
            RunParticipantId participantId,
            EquipmentInstanceId equipmentInstanceId,
            WeaponDefinitionId weaponDefinitionId,
            LifecycleGeneration lifecycleGeneration,
            FireOperationId sourceFireOperationId,
            string requestFingerprint,
            string effectiveWeaponFingerprint,
            long operationSequence,
            WeaponFiringScheduleStatus successfulStatus,
            string resultCode,
            long sourceSimulationTick,
            string resultingTrackFingerprint)
            : this(actorId, participantId, equipmentInstanceId, weaponDefinitionId,
                lifecycleGeneration, sourceFireOperationId, requestFingerprint,
                effectiveWeaponFingerprint, operationSequence,
                WeaponFiringReplayResultKind.SuccessfulTransition,
                successfulStatus, resultCode, sourceSimulationTick, null,
                resultingTrackFingerprint)
        {
        }

        private WeaponFiringReplayRecord(
            WeaponActorInstanceId actorId,
            RunParticipantId participantId,
            EquipmentInstanceId equipmentInstanceId,
            WeaponDefinitionId weaponDefinitionId,
            LifecycleGeneration lifecycleGeneration,
            FireOperationId sourceFireOperationId,
            string requestFingerprint,
            string effectiveWeaponFingerprint,
            long operationSequence,
            WeaponFiringReplayResultKind resultKind,
            WeaponFiringScheduleStatus successfulStatus,
            string resultCode,
            long sourceSimulationTick,
            WeaponFiringScheduler.AcceptedSchedule acceptedSchedule,
            string resultingTrackFingerprint)
        {
            ActorId = actorId;
            ParticipantId = participantId;
            EquipmentInstanceId = equipmentInstanceId;
            WeaponDefinitionId = weaponDefinitionId;
            LifecycleGeneration = lifecycleGeneration;
            SourceFireOperationId = sourceFireOperationId;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            EffectiveWeaponFingerprint = effectiveWeaponFingerprint ?? string.Empty;
            OperationSequence = operationSequence;
            ResultKind = resultKind;
            SuccessfulStatus = successfulStatus;
            ResultCode = resultCode ?? string.Empty;
            SourceSimulationTick = sourceSimulationTick;
            AcceptedSchedule = acceptedSchedule;
            ResultingTrackFingerprint = resultingTrackFingerprint ?? string.Empty;
            CanonicalText = BuildCanonicalText();
            Fingerprint = WeaponExecutionFingerprint.Compute(CanonicalText);
        }

        public WeaponActorInstanceId ActorId { get; }
        public RunParticipantId ParticipantId { get; }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public WeaponDefinitionId WeaponDefinitionId { get; }
        public LifecycleGeneration LifecycleGeneration { get; }
        public FireOperationId SourceFireOperationId { get; }
        public string RequestFingerprint { get; }
        public string EffectiveWeaponFingerprint { get; }
        public long OperationSequence { get; }
        public WeaponFiringReplayResultKind ResultKind { get; }
        public WeaponFiringScheduleStatus SuccessfulStatus { get; }
        public string ResultCode { get; }
        public long SourceSimulationTick { get; }
        public WeaponFiringScheduler.AcceptedSchedule AcceptedSchedule { get; }
        public string ResultingTrackFingerprint { get; }
        public string CanonicalText { get; }
        public string Fingerprint { get; }

        public bool HasAcceptedSchedule
        {
            get { return ResultKind == WeaponFiringReplayResultKind.AcceptedSchedule; }
        }

        internal string ReplayKey
        {
            get
            {
                return BuildReplayKey(ActorId, EquipmentInstanceId,
                    LifecycleGeneration, SourceFireOperationId);
            }
        }

        internal static string BuildReplayKey(
            WeaponActorInstanceId actorId,
            EquipmentInstanceId equipmentInstanceId,
            LifecycleGeneration lifecycleGeneration,
            FireOperationId sourceFireOperationId)
        {
            return WeaponFiringTrackState.BuildEquipmentLifecycleKey(
                actorId, equipmentInstanceId, lifecycleGeneration)
                + "|" + sourceFireOperationId;
        }

        public bool HasValidFingerprint()
        {
            if (ActorId == null || ParticipantId == null || EquipmentInstanceId == null
                || WeaponDefinitionId == null || LifecycleGeneration == null
                || SourceFireOperationId == null || SourceSimulationTick < 0L
                || OperationSequence < 0L
                || string.IsNullOrWhiteSpace(RequestFingerprint)
                || string.IsNullOrWhiteSpace(EffectiveWeaponFingerprint)
                || !Enum.IsDefined(typeof(WeaponFiringReplayResultKind), ResultKind))
            {
                return false;
            }

            if (HasAcceptedSchedule)
            {
                if (SuccessfulStatus != WeaponFiringScheduleStatus.Accepted
                    || AcceptedSchedule == null || !AcceptedSchedule.HasValidFingerprint()
                    || !string.IsNullOrEmpty(ResultingTrackFingerprint)
                    || !ActorId.Equals(AcceptedSchedule.ActorId)
                    || !ParticipantId.Equals(AcceptedSchedule.ParticipantId)
                    || !EquipmentInstanceId.Equals(AcceptedSchedule.EquipmentInstanceId)
                    || !WeaponDefinitionId.Equals(AcceptedSchedule.WeaponDefinitionId)
                    || !LifecycleGeneration.Equals(AcceptedSchedule.LifecycleGeneration)
                    || !SourceFireOperationId.Equals(AcceptedSchedule.SourceFireOperationId)
                    || SourceSimulationTick != AcceptedSchedule.SourceCommand.SimulationTick
                    || !string.Equals(RequestFingerprint,
                        AcceptedSchedule.RequestFingerprint, StringComparison.Ordinal)
                    || !string.Equals(EffectiveWeaponFingerprint,
                        AcceptedSchedule.EffectiveWeaponFingerprint, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            else if (AcceptedSchedule != null
                || (SuccessfulStatus != WeaponFiringScheduleStatus.WaitingForCadence
                    && SuccessfulStatus != WeaponFiringScheduleStatus.Released)
                || string.IsNullOrWhiteSpace(ResultingTrackFingerprint))
            {
                return false;
            }

            return string.Equals(Fingerprint,
                WeaponExecutionFingerprint.Compute(BuildCanonicalText()),
                StringComparison.Ordinal);
        }

        private string BuildCanonicalText()
        {
            return string.Join("\n", new[]
            {
                "actor_id=" + ActorId,
                "participant_id=" + ParticipantId,
                "equipment_instance_id=" + EquipmentInstanceId,
                "weapon_definition_id=" + WeaponDefinitionId,
                "lifecycle_generation=" + LifecycleGeneration,
                "source_fire_operation_id=" + SourceFireOperationId,
                "request_fingerprint=" + RequestFingerprint,
                "effective_weapon_fingerprint=" + EffectiveWeaponFingerprint,
                "operation_sequence=" + OperationSequence.ToString(CultureInfo.InvariantCulture),
                "result_kind=" + ResultKind,
                "successful_status=" + SuccessfulStatus,
                "result_code=" + ResultCode,
                "source_simulation_tick=" + SourceSimulationTick.ToString(CultureInfo.InvariantCulture),
                "accepted_schedule_fingerprint="
                    + (AcceptedSchedule == null ? "null" : AcceptedSchedule.Fingerprint),
                "resulting_track_fingerprint=" + ResultingTrackFingerprint,
            });
        }
    }

    public sealed class WeaponFiringSessionState
    {
        private readonly ReadOnlyCollection<WeaponFiringTrackState> tracks;
        private readonly ReadOnlyCollection<WeaponFiringReplayRecord> replayRecords;

        private WeaponFiringSessionState(
            int clockTicksPerSecond,
            IList<WeaponFiringTrackState> firingTracks,
            IList<WeaponFiringReplayRecord> operationReplayRecords)
        {
            ClockTicksPerSecond = clockTicksPerSecond;
            List<WeaponFiringTrackState> trackCopy =
                new List<WeaponFiringTrackState>(
                    firingTracks ?? new WeaponFiringTrackState[0]);
            trackCopy.Sort(CompareTracks);
            tracks = new ReadOnlyCollection<WeaponFiringTrackState>(trackCopy);

            List<WeaponFiringReplayRecord> replayCopy =
                new List<WeaponFiringReplayRecord>(
                    operationReplayRecords ?? new WeaponFiringReplayRecord[0]);
            replayCopy.Sort(CompareReplayRecords);
            replayRecords = new ReadOnlyCollection<WeaponFiringReplayRecord>(replayCopy);
            CanonicalText = BuildCanonicalText();
            Fingerprint = WeaponExecutionFingerprint.Compute(CanonicalText);
        }

        public static WeaponFiringSessionState Empty { get; } =
            new WeaponFiringSessionState(0,
                new WeaponFiringTrackState[0],
                new WeaponFiringReplayRecord[0]);

        public int ClockTicksPerSecond { get; }
        public IReadOnlyList<WeaponFiringTrackState> Tracks { get { return tracks; } }
        public IReadOnlyList<WeaponFiringReplayRecord> ReplayRecords { get { return replayRecords; } }
        public string CanonicalText { get; }
        public string Fingerprint { get; }

        public static bool TryRestore(
            int clockTicksPerSecond,
            IEnumerable<WeaponFiringTrackState> firingTracks,
            IEnumerable<WeaponFiringReplayRecord> operationReplayRecords,
            string expectedFingerprint,
            out WeaponFiringSessionState restoredState)
        {
            restoredState = null;
            if (clockTicksPerSecond < 0 || firingTracks == null
                || operationReplayRecords == null
                || string.IsNullOrWhiteSpace(expectedFingerprint))
            {
                return false;
            }

            try
            {
                WeaponFiringSessionState candidate = new WeaponFiringSessionState(
                    clockTicksPerSecond,
                    new List<WeaponFiringTrackState>(firingTracks),
                    new List<WeaponFiringReplayRecord>(operationReplayRecords));
                if (!candidate.HasValidFingerprint()
                    || !string.Equals(candidate.Fingerprint,
                        expectedFingerprint, StringComparison.Ordinal))
                {
                    return false;
                }

                restoredState = candidate;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public bool HasValidFingerprint()
        {
            if (ClockTicksPerSecond < 0
                || (ClockTicksPerSecond == 0
                    && (tracks.Count != 0 || replayRecords.Count != 0)))
            {
                return false;
            }

            string previousTrackKey = null;
            HashSet<string> equipmentLifecycleKeys =
                new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, long> expectedShotByTrack =
                new Dictionary<string, long>(StringComparer.Ordinal);
            Dictionary<string, long> expectedOperationByTrack =
                new Dictionary<string, long>(StringComparer.Ordinal);
            Dictionary<string, List<WeaponFiringReplayRecord>> recordsByTrack =
                new Dictionary<string, List<WeaponFiringReplayRecord>>(StringComparer.Ordinal);

            for (int index = 0; index < tracks.Count; index++)
            {
                WeaponFiringTrackState track = tracks[index];
                if (track == null || !track.HasValidFingerprint()
                    || !equipmentLifecycleKeys.Add(track.EquipmentLifecycleKey)
                    || (previousTrackKey != null
                        && string.CompareOrdinal(previousTrackKey, track.IdentityKey) >= 0))
                {
                    return false;
                }

                expectedShotByTrack.Add(track.IdentityKey, track.FirstRetainedShotSequence);
                expectedOperationByTrack.Add(
                    track.IdentityKey, track.FirstRetainedOperationSequence);
                recordsByTrack.Add(track.IdentityKey,
                    new List<WeaponFiringReplayRecord>());
                previousTrackKey = track.IdentityKey;
            }

            HashSet<string> replayKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < replayRecords.Count; index++)
            {
                WeaponFiringReplayRecord replay = replayRecords[index];
                if (replay == null || !replay.HasValidFingerprint()
                    || !replayKeys.Add(replay.ReplayKey))
                {
                    return false;
                }

                WeaponFiringTrackState track;
                if (!TryFindTrack(replay.ActorId, replay.ParticipantId,
                    replay.EquipmentInstanceId, replay.WeaponDefinitionId,
                    replay.LifecycleGeneration, out track))
                {
                    return false;
                }
                recordsByTrack[track.IdentityKey].Add(replay);
            }

            for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
            {
                WeaponFiringTrackState track = tracks[trackIndex];
                List<WeaponFiringReplayRecord> operationRecords =
                    recordsByTrack[track.IdentityKey];
                operationRecords.Sort(CompareReplayByOperationSequence);
                long expectedOperation = expectedOperationByTrack[track.IdentityKey];
                for (int index = 0; index < operationRecords.Count; index++)
                {
                    WeaponFiringReplayRecord replay = operationRecords[index];
                    if (replay.OperationSequence != expectedOperation
                        || !TryIncrement(expectedOperation, out expectedOperation))
                    {
                        return false;
                    }
                }
                if (expectedOperation != track.NextOperationSequence)
                {
                    return false;
                }

                List<WeaponFiringReplayRecord> acceptedRecords =
                    new List<WeaponFiringReplayRecord>();
                for (int index = 0; index < operationRecords.Count; index++)
                {
                    if (operationRecords[index].HasAcceptedSchedule)
                    {
                        acceptedRecords.Add(operationRecords[index]);
                    }
                }
                acceptedRecords.Sort(CompareAcceptedReplayBySequence);
                long expectedShot = expectedShotByTrack[track.IdentityKey];
                for (int index = 0; index < acceptedRecords.Count; index++)
                {
                    WeaponFiringReplayRecord replay = acceptedRecords[index];
                    if (replay.AcceptedSchedule.FirstShotSequence != expectedShot
                        || !TryIncrement(replay.AcceptedSchedule.LastShotSequence,
                            out expectedShot))
                    {
                        return false;
                    }
                }
                if (expectedShot != track.NextGlobalShotSequence)
                {
                    return false;
                }
            }

            return string.Equals(Fingerprint,
                WeaponExecutionFingerprint.Compute(BuildCanonicalText()),
                StringComparison.Ordinal);
        }

        internal bool TryFindTrack(
            WeaponActorInstanceId actorId,
            RunParticipantId participantId,
            EquipmentInstanceId equipmentInstanceId,
            WeaponDefinitionId weaponDefinitionId,
            LifecycleGeneration lifecycleGeneration,
            out WeaponFiringTrackState track)
        {
            string key = WeaponFiringTrackState.BuildIdentityKey(actorId,
                participantId, equipmentInstanceId, weaponDefinitionId,
                lifecycleGeneration);
            for (int index = 0; index < tracks.Count; index++)
            {
                int comparison = string.CompareOrdinal(tracks[index].IdentityKey, key);
                if (comparison == 0)
                {
                    track = tracks[index];
                    return true;
                }
                if (comparison > 0) { break; }
            }
            track = null;
            return false;
        }

        internal bool TryFindEquipmentLifecycleTrack(
            WeaponActorInstanceId actorId,
            EquipmentInstanceId equipmentInstanceId,
            LifecycleGeneration lifecycleGeneration,
            out WeaponFiringTrackState track)
        {
            string key = WeaponFiringTrackState.BuildEquipmentLifecycleKey(
                actorId, equipmentInstanceId, lifecycleGeneration);
            for (int index = 0; index < tracks.Count; index++)
            {
                if (string.Equals(tracks[index].EquipmentLifecycleKey,
                    key, StringComparison.Ordinal))
                {
                    track = tracks[index];
                    return true;
                }
            }
            track = null;
            return false;
        }

        internal bool TryFindReplay(
            WeaponActorInstanceId actorId,
            EquipmentInstanceId equipmentInstanceId,
            LifecycleGeneration lifecycleGeneration,
            FireOperationId sourceFireOperationId,
            out WeaponFiringReplayRecord replayRecord)
        {
            string key = WeaponFiringReplayRecord.BuildReplayKey(actorId,
                equipmentInstanceId, lifecycleGeneration, sourceFireOperationId);
            for (int index = 0; index < replayRecords.Count; index++)
            {
                int comparison = string.CompareOrdinal(replayRecords[index].ReplayKey, key);
                if (comparison == 0)
                {
                    replayRecord = replayRecords[index];
                    return true;
                }
                if (comparison > 0) { break; }
            }
            replayRecord = null;
            return false;
        }

        internal WeaponFiringSessionState WithTransition(
            int clockTicksPerSecond,
            int replayRetentionCapacity,
            WeaponFiringTrackState updatedTrack,
            WeaponFiringReplayRecord operationReplay)
        {
            if (clockTicksPerSecond < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(clockTicksPerSecond));
            }
            if (replayRetentionCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(replayRetentionCapacity));
            }
            if (ClockTicksPerSecond != 0
                && ClockTicksPerSecond != clockTicksPerSecond)
            {
                throw new InvalidOperationException(
                    "Firing state cannot transition under a different simulation clock.");
            }
            if (updatedTrack == null)
            {
                throw new ArgumentNullException(nameof(updatedTrack));
            }

            List<WeaponFiringReplayRecord> nextReplayRecords =
                new List<WeaponFiringReplayRecord>(
                    replayRecords.Count + (operationReplay == null ? 0 : 1));
            for (int index = 0; index < replayRecords.Count; index++)
            {
                nextReplayRecords.Add(replayRecords[index]);
            }
            if (operationReplay != null)
            {
                long expectedNextOperation;
                if (!TryIncrement(operationReplay.OperationSequence,
                    out expectedNextOperation)
                    || expectedNextOperation != updatedTrack.NextOperationSequence)
                {
                    throw new InvalidOperationException(
                        "The operation receipt must advance the track sequence exactly once.");
                }
                for (int index = 0; index < nextReplayRecords.Count; index++)
                {
                    if (string.Equals(nextReplayRecords[index].ReplayKey,
                        operationReplay.ReplayKey, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "A firing operation replay record cannot be replaced.");
                    }
                }
                nextReplayRecords.Add(operationReplay);
            }

            List<WeaponFiringReplayRecord> updatedTrackRecords =
                new List<WeaponFiringReplayRecord>();
            for (int index = 0; index < nextReplayRecords.Count; index++)
            {
                WeaponFiringReplayRecord candidate = nextReplayRecords[index];
                if (candidate.ActorId.Equals(updatedTrack.ActorId)
                    && candidate.ParticipantId.Equals(updatedTrack.ParticipantId)
                    && candidate.EquipmentInstanceId.Equals(updatedTrack.EquipmentInstanceId)
                    && candidate.WeaponDefinitionId.Equals(updatedTrack.WeaponDefinitionId)
                    && candidate.LifecycleGeneration.Equals(updatedTrack.LifecycleGeneration))
                {
                    updatedTrackRecords.Add(candidate);
                }
            }
            updatedTrackRecords.Sort(CompareReplayByOperationSequence);

            int pruneCount = Math.Max(0,
                updatedTrackRecords.Count - replayRetentionCapacity);
            long firstRetainedOperationSequence =
                updatedTrack.FirstRetainedOperationSequence;
            long firstRetainedShotSequence = updatedTrack.FirstRetainedShotSequence;
            long replayRetentionFloor = updatedTrack.ReplayRetentionFloor;
            string cumulativeHistoryFingerprint =
                updatedTrack.CumulativeHistoryFingerprint;
            for (int index = 0; index < pruneCount; index++)
            {
                WeaponFiringReplayRecord pruned = updatedTrackRecords[index];
                cumulativeHistoryFingerprint = WeaponExecutionFingerprint.Compute(
                    "previous=" + cumulativeHistoryFingerprint
                    + "\nrecord=" + pruned.Fingerprint);
                if (!TryIncrement(pruned.OperationSequence,
                    out firstRetainedOperationSequence))
                {
                    throw new InvalidOperationException("Operation sequence overflow.");
                }
                if (pruned.HasAcceptedSchedule
                    && !TryIncrement(pruned.AcceptedSchedule.LastShotSequence,
                        out firstRetainedShotSequence))
                {
                    throw new InvalidOperationException("Replay sequence overflow.");
                }
                long nextFloor;
                if (!TryIncrement(pruned.SourceSimulationTick, out nextFloor))
                {
                    throw new InvalidOperationException("Replay retention floor overflow.");
                }
                replayRetentionFloor = Math.Max(replayRetentionFloor, nextFloor);
                nextReplayRecords.Remove(pruned);
            }

            updatedTrack = updatedTrack.WithReplayRetention(
                firstRetainedOperationSequence,
                firstRetainedShotSequence,
                replayRetentionFloor,
                cumulativeHistoryFingerprint);

            List<WeaponFiringTrackState> nextTracks =
                new List<WeaponFiringTrackState>(tracks.Count + 1);
            bool replaced = false;
            for (int index = 0; index < tracks.Count; index++)
            {
                WeaponFiringTrackState current = tracks[index];
                if (string.Equals(current.IdentityKey, updatedTrack.IdentityKey,
                    StringComparison.Ordinal))
                {
                    nextTracks.Add(updatedTrack);
                    replaced = true;
                }
                else
                {
                    nextTracks.Add(current);
                }
            }
            if (!replaced) { nextTracks.Add(updatedTrack); }

            return new WeaponFiringSessionState(
                clockTicksPerSecond, nextTracks, nextReplayRecords);
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("clock_ticks_per_second=")
                .Append(ClockTicksPerSecond.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            builder.Append("track_count=")
                .Append(tracks.Count.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            for (int index = 0; index < tracks.Count; index++)
            {
                builder.Append("track[")
                    .Append(index.ToString(CultureInfo.InvariantCulture))
                    .Append("]=")
                    .Append(tracks[index] == null ? "null" : tracks[index].Fingerprint)
                    .Append('\n');
            }
            builder.Append("replay_count=")
                .Append(replayRecords.Count.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            for (int index = 0; index < replayRecords.Count; index++)
            {
                builder.Append("replay[")
                    .Append(index.ToString(CultureInfo.InvariantCulture))
                    .Append("]=")
                    .Append(replayRecords[index] == null
                        ? "null" : replayRecords[index].Fingerprint)
                    .Append('\n');
            }
            return builder.ToString();
        }

        private static int CompareTracks(
            WeaponFiringTrackState left,
            WeaponFiringTrackState right)
        {
            if (ReferenceEquals(left, right)) { return 0; }
            if (ReferenceEquals(left, null)) { return -1; }
            if (ReferenceEquals(right, null)) { return 1; }
            return string.CompareOrdinal(left.IdentityKey, right.IdentityKey);
        }

        private static int CompareReplayRecords(
            WeaponFiringReplayRecord left,
            WeaponFiringReplayRecord right)
        {
            if (ReferenceEquals(left, right)) { return 0; }
            if (ReferenceEquals(left, null)) { return -1; }
            if (ReferenceEquals(right, null)) { return 1; }
            return string.CompareOrdinal(left.ReplayKey, right.ReplayKey);
        }

        private static int CompareReplayByOperationSequence(
            WeaponFiringReplayRecord left,
            WeaponFiringReplayRecord right)
        {
            if (ReferenceEquals(left, right)) { return 0; }
            if (ReferenceEquals(left, null)) { return -1; }
            if (ReferenceEquals(right, null)) { return 1; }
            return left.OperationSequence.CompareTo(right.OperationSequence);
        }

        private static int CompareAcceptedReplayBySequence(
            WeaponFiringReplayRecord left,
            WeaponFiringReplayRecord right)
        {
            return left.AcceptedSchedule.FirstShotSequence.CompareTo(
                right.AcceptedSchedule.FirstShotSequence);
        }

        private static bool TryIncrement(long value, out long incremented)
        {
            try
            {
                incremented = checked(value + 1L);
                return true;
            }
            catch (OverflowException)
            {
                incremented = 0L;
                return false;
            }
        }
    }
}
