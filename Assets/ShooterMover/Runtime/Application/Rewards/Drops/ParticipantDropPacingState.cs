using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Rewards.Drops
{
    /// <summary>
    /// Per-participant pacing and exact replay authority. State is cached locally for
    /// active generation and mirrored through the optional run-local store after every
    /// accepted transition. A recreated authority therefore resumes the exact pacing
    /// snapshot instead of resetting pity or saturation.
    /// </summary>
    public sealed class ParticipantDropPacingState
    {
        private sealed class ReplayRecord
        {
            public ReplayRecord(
                string contextFingerprint,
                PersonalRewardGenerationResult result)
            {
                ContextFingerprint = contextFingerprint;
                Result = result;
            }

            public string ContextFingerprint { get; }
            public PersonalRewardGenerationResult Result { get; }
        }

        private readonly object gate = new object();
        private readonly Dictionary<string, ParticipantDropPacingState> states =
            new Dictionary<string, ParticipantDropPacingState>(
                StringComparer.Ordinal);
        private readonly Dictionary<StableId, ReplayRecord> replay =
            new Dictionary<StableId, ReplayRecord>();
        private readonly IParticipantDropPacingStateStore stateStore;

        public ParticipantDropPacingState()
            : this(null)
        {
        }

        public ParticipantDropPacingState(
            IParticipantDropPacingStateStore stateStore)
        {
            this.stateStore = stateStore;
        }

        public PersonalRewardGenerationResult Execute(
            PersonalRewardRollContext context,
            Func<ParticipantDropPacingState,
                PersonalRewardGenerationResult> generateFresh)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (generateFresh == null)
            {
                throw new ArgumentNullException(nameof(generateFresh));
            }

            lock (gate)
            {
                ReplayRecord existing;
                if (replay.TryGetValue(context.OperationStableId, out existing))
                {
                    if (string.Equals(
                            existing.ContextFingerprint,
                            context.Fingerprint,
                            StringComparison.Ordinal))
                    {
                        return existing.Result.AsExactReplay();
                    }

                    ParticipantDropPacingState current =
                        GetOrCreateState(context);
                    return new PersonalRewardGenerationResult(
                        PersonalRewardGenerationStatus.ConflictingReplay,
                        context,
                        current,
                        current,
                        Array.Empty<RewardGrant>(),
                        Array.Empty<PersonalRewardDecision>(),
                        false,
                        "personal-reward-operation-identity-conflict");
                }

                ParticipantDropPacingState before = GetOrCreateState(context);
                PersonalRewardGenerationResult result = generateFresh(before);
                if (result == null
                    || !string.Equals(
                        result.Context.Fingerprint,
                        context.Fingerprint,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        result.PacingBefore.Fingerprint,
                        before.Fingerprint,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Personal reward generation returned an invalid authority transition.");
                }

                if (result.IsSuccess)
                {
                    string key = StateKey(context);
                    states[key] = result.PacingAfter;
                    if (stateStore != null)
                    {
                        stateStore.Save(result.PacingAfter);
                    }
                    replay.Add(
                        context.OperationStableId,
                        new ReplayRecord(context.Fingerprint, result));
                }
                return result;
            }
        }

        public bool TryExport(
            StableId runStableId,
            int runLifecycleGeneration,
            StableId participantStableId,
            out ParticipantDropPacingState state)
        {
            state = null;
            if (runStableId == null || participantStableId == null)
            {
                return false;
            }

            lock (gate)
            {
                string key = StateKey(
                    runStableId,
                    runLifecycleGeneration,
                    participantStableId);
                if (states.TryGetValue(key, out state))
                {
                    return true;
                }
                if (stateStore != null
                    && stateStore.TryLoad(
                        runStableId,
                        runLifecycleGeneration,
                        participantStableId,
                        out state)
                    && state != null)
                {
                    states[key] = state;
                    return true;
                }
                state = null;
                return false;
            }
        }

        public void Restore(ParticipantDropPacingState snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            lock (gate)
            {
                states[StateKey(
                    snapshot.RunStableId,
                    snapshot.RunLifecycleGeneration,
                    snapshot.ParticipantStableId)] = snapshot;
                if (stateStore != null)
                {
                    stateStore.Save(snapshot);
                }
            }
        }

        private ParticipantDropPacingState GetOrCreateState(
            PersonalRewardRollContext context)
        {
            string key = StateKey(context);
            ParticipantDropPacingState state;
            if (!states.TryGetValue(key, out state))
            {
                if (stateStore == null
                    || !stateStore.TryLoad(
                        context.RunStableId,
                        context.RunLifecycleGeneration,
                        context.ParticipantStableId,
                        out state)
                    || state == null)
                {
                    state = ParticipantDropPacingState.Start(
                        context.ParticipantStableId,
                        context.RunStableId,
                        context.RunLifecycleGeneration,
                        context.RoomStableId,
                        context.RoomLifecycleGeneration);
                }
                states.Add(key, state);
            }
            state = state.EnterRoom(
                context.RoomStableId,
                context.RoomLifecycleGeneration);
            states[key] = state;
            return state;
        }

        private static string StateKey(PersonalRewardRollContext context)
        {
            return StateKey(
                context.RunStableId,
                context.RunLifecycleGeneration,
                context.ParticipantStableId);
        }

        private static string StateKey(
            StableId runStableId,
            int runLifecycleGeneration,
            StableId participantStableId)
        {
            return runStableId
                + "|"
                + runLifecycleGeneration
                + "|"
                + participantStableId;
        }
    }
}
