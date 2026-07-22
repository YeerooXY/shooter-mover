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
    public sealed class ParticipantDropPacingAuthorityV1
    {
        private sealed class ReplayRecord
        {
            public ReplayRecord(
                string contextFingerprint,
                PersonalRewardGenerationResultV1 result)
            {
                ContextFingerprint = contextFingerprint;
                Result = result;
            }

            public string ContextFingerprint { get; }
            public PersonalRewardGenerationResultV1 Result { get; }
        }

        private readonly object gate = new object();
        private readonly Dictionary<string, ParticipantDropPacingStateV1> states =
            new Dictionary<string, ParticipantDropPacingStateV1>(
                StringComparer.Ordinal);
        private readonly Dictionary<StableId, ReplayRecord> replay =
            new Dictionary<StableId, ReplayRecord>();
        private readonly IParticipantDropPacingStateStoreV1 stateStore;

        public ParticipantDropPacingAuthorityV1()
            : this(null)
        {
        }

        public ParticipantDropPacingAuthorityV1(
            IParticipantDropPacingStateStoreV1 stateStore)
        {
            this.stateStore = stateStore;
        }

        public PersonalRewardGenerationResultV1 Execute(
            PersonalRewardRollContextV1 context,
            Func<ParticipantDropPacingStateV1,
                PersonalRewardGenerationResultV1> generateFresh)
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

                    ParticipantDropPacingStateV1 current =
                        GetOrCreateState(context);
                    return new PersonalRewardGenerationResultV1(
                        PersonalRewardGenerationStatusV1.ConflictingReplay,
                        context,
                        current,
                        current,
                        Array.Empty<RewardGrantV1>(),
                        Array.Empty<PersonalRewardDecisionV1>(),
                        false,
                        "personal-reward-operation-identity-conflict");
                }

                ParticipantDropPacingStateV1 before = GetOrCreateState(context);
                PersonalRewardGenerationResultV1 result = generateFresh(before);
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
            out ParticipantDropPacingStateV1 state)
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

        public void Restore(ParticipantDropPacingStateV1 snapshot)
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

        private ParticipantDropPacingStateV1 GetOrCreateState(
            PersonalRewardRollContextV1 context)
        {
            string key = StateKey(context);
            ParticipantDropPacingStateV1 state;
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
                    state = ParticipantDropPacingStateV1.Start(
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

        private static string StateKey(PersonalRewardRollContextV1 context)
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
