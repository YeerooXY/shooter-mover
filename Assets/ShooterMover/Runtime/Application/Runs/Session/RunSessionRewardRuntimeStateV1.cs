using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Runs.Session
{
    public sealed partial class RunSessionAggregateV1
    {
        private readonly object rewardRuntimeGate = new object();
        private readonly Dictionary<StableId, RunRewardParticipantStateV1>
            rewardParticipants =
                new Dictionary<StableId, RunRewardParticipantStateV1>();
        private readonly Dictionary<string, ParticipantDropPacingStateV1>
            rewardPacingStates =
                new Dictionary<string, ParticipantDropPacingStateV1>(
                    StringComparer.Ordinal);
        private RunRewardEnvironmentSnapshotV1 rewardEnvironment;

        /// <summary>
        /// Installs the authored run reward environment exactly once. Exact replay is
        /// accepted; conflicting configuration is rejected before any reward roll.
        /// </summary>
        public void ConfigureRewardEnvironment(
            RunRewardEnvironmentSnapshotV1 environment)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }
            lock (rewardRuntimeGate)
            {
                if (rewardEnvironment == null)
                {
                    rewardEnvironment = environment;
                    return;
                }
                if (!string.Equals(
                        rewardEnvironment.Fingerprint,
                        environment.Fingerprint,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The run reward environment is already frozen with different authored inputs.");
                }
            }
        }

        public RunRewardEnvironmentSnapshotV1 ExportRewardEnvironment()
        {
            lock (rewardRuntimeGate)
            {
                if (rewardEnvironment == null)
                {
                    throw new InvalidOperationException(
                        "The run reward environment has not been configured.");
                }
                return rewardEnvironment;
            }
        }

        /// <summary>
        /// Adds or refreshes one network-authored participant eligibility record. The
        /// stable roster is bounded to the supported one-to-four-player run size.
        /// </summary>
        public void RegisterRewardParticipant(
            RunRewardParticipantStateV1 participant)
        {
            if (participant == null)
            {
                throw new ArgumentNullException(nameof(participant));
            }
            lock (rewardRuntimeGate)
            {
                if (!rewardParticipants.ContainsKey(
                        participant.ParticipantStableId)
                    && rewardParticipants.Count >= 4)
                {
                    throw new InvalidOperationException(
                        "A run reward roster supports at most four participants.");
                }
                rewardParticipants[participant.ParticipantStableId] = participant;
            }
        }

        public IReadOnlyList<RunRewardParticipantStateV1>
            ExportRewardParticipants()
        {
            lock (rewardRuntimeGate)
            {
                EnsurePrimaryRewardParticipant();
                var values = new List<RunRewardParticipantStateV1>(
                    rewardParticipants.Values);
                values.Sort();
                return new ReadOnlyCollection<RunRewardParticipantStateV1>(
                    values);
            }
        }

        public RunRewardRuntimeSnapshotV1 ExportRewardRuntimeSnapshot()
        {
            lock (rewardRuntimeGate)
            {
                EnsurePrimaryRewardParticipant();
                if (rewardEnvironment == null)
                {
                    throw new InvalidOperationException(
                        "The run reward environment has not been configured.");
                }
                var participants = new List<RunRewardParticipantStateV1>(
                    rewardParticipants.Values);
                participants.Sort();
                var pacing = new List<ParticipantDropPacingStateV1>();
                foreach (ParticipantDropPacingStateV1 value in
                    rewardPacingStates.Values)
                {
                    if (value.RunLifecycleGeneration == LifecycleGeneration)
                    {
                        pacing.Add(value);
                    }
                }
                pacing.Sort(delegate(
                    ParticipantDropPacingStateV1 left,
                    ParticipantDropPacingStateV1 right)
                {
                    return left.ParticipantStableId.CompareTo(
                        right.ParticipantStableId);
                });
                return new RunRewardRuntimeSnapshotV1(
                    RunStableId,
                    checked((int)LifecycleGeneration),
                    rewardEnvironment,
                    participants,
                    pacing);
            }
        }

        public void RestoreRewardRuntimeSnapshot(
            RunRewardRuntimeSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (snapshot.RunStableId != RunStableId
                || snapshot.RunLifecycleGeneration != LifecycleGeneration)
            {
                throw new ArgumentException(
                    "The reward snapshot belongs to a different run lifecycle.",
                    nameof(snapshot));
            }

            lock (rewardRuntimeGate)
            {
                rewardParticipants.Clear();
                for (int index = 0; index < snapshot.Participants.Count; index++)
                {
                    RunRewardParticipantStateV1 participant =
                        snapshot.Participants[index];
                    rewardParticipants.Add(
                        participant.ParticipantStableId,
                        participant);
                }
                rewardPacingStates.Clear();
                for (int index = 0; index < snapshot.PacingStates.Count; index++)
                {
                    ParticipantDropPacingStateV1 pacing =
                        snapshot.PacingStates[index];
                    rewardPacingStates.Add(PacingKey(pacing), pacing);
                }
                rewardEnvironment = snapshot.Environment;
            }
        }

        internal bool TryLoadRewardPacingState(
            StableId runStableId,
            int runLifecycleGeneration,
            StableId participantStableId,
            out ParticipantDropPacingStateV1 state)
        {
            state = null;
            if (runStableId != RunStableId
                || runLifecycleGeneration != LifecycleGeneration
                || participantStableId == null)
            {
                return false;
            }
            lock (rewardRuntimeGate)
            {
                return rewardPacingStates.TryGetValue(
                    PacingKey(
                        runStableId,
                        runLifecycleGeneration,
                        participantStableId),
                    out state);
            }
        }

        internal void SaveRewardPacingState(
            ParticipantDropPacingStateV1 state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (state.RunStableId != RunStableId
                || state.RunLifecycleGeneration != LifecycleGeneration)
            {
                throw new ArgumentException(
                    "Pacing state belongs to a different run lifecycle.",
                    nameof(state));
            }
            lock (rewardRuntimeGate)
            {
                rewardPacingStates[PacingKey(state)] = state;
            }
        }

        private void EnsurePrimaryRewardParticipant()
        {
            RunPlayerRuntimeSnapshotV1 player =
                RuntimePorts.Player.ExportSnapshot();
            if (player == null || player.ParticipantStableId == null)
            {
                throw new InvalidOperationException(
                    "The run player snapshot cannot seed reward participation.");
            }
            if (!rewardParticipants.ContainsKey(player.ParticipantStableId))
            {
                rewardParticipants.Add(
                    player.ParticipantStableId,
                    new RunRewardParticipantStateV1(
                        player.ParticipantStableId,
                        FrozenInputs.Character.CharacterLevel,
                        true,
                        true,
                        true,
                        true,
                        false));
            }
        }

        private static string PacingKey(ParticipantDropPacingStateV1 state)
        {
            return PacingKey(
                state.RunStableId,
                state.RunLifecycleGeneration,
                state.ParticipantStableId);
        }

        private static string PacingKey(
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
