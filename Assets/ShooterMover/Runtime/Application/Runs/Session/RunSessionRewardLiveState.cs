using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Runs.Session
{
    public sealed partial class RunSessionAggregate
    {
        private readonly object rewardRuntimeGate = new object();
        private readonly Dictionary<StableId, RunRewardParticipantState>
            rewardParticipants =
                new Dictionary<StableId, RunRewardParticipantState>();
        private readonly Dictionary<string, ParticipantDropPacingState>
            rewardPacingStates =
                new Dictionary<string, ParticipantDropPacingState>(
                    StringComparer.Ordinal);
        private readonly Dictionary<StableId, PersonalRewardDeliveryEnvelope>
            personalRewardDeliveries =
                new Dictionary<StableId, PersonalRewardDeliveryEnvelope>();
        private RunRewardEnvironmentSnapshot rewardEnvironment;

        public void ConfigureRewardEnvironment(
            RunRewardEnvironmentSnapshot environment)
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

        public RunRewardEnvironmentSnapshot ExportRewardEnvironment()
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

        public void RegisterRewardParticipant(
            RunRewardParticipantState participant)
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

        public IReadOnlyList<RunRewardParticipantState>
            ExportRewardParticipants()
        {
            lock (rewardRuntimeGate)
            {
                EnsurePrimaryRewardParticipant();
                var values = new List<RunRewardParticipantState>(
                    rewardParticipants.Values);
                values.Sort();
                return new ReadOnlyCollection<RunRewardParticipantState>(
                    values);
            }
        }

        public IReadOnlyList<PersonalRewardDeliveryEnvelope>
            ExportPendingPersonalRewards(StableId participantStableId)
        {
            if (participantStableId == null)
            {
                throw new ArgumentNullException(nameof(participantStableId));
            }
            lock (rewardRuntimeGate)
            {
                var values = new List<PersonalRewardDeliveryEnvelope>();
                foreach (PersonalRewardDeliveryEnvelope value in
                    personalRewardDeliveries.Values)
                {
                    if (value.State == PersonalRewardDeliveryState.Pending
                        && value.Result.Context.ParticipantStableId
                            == participantStableId
                        && value.Result.Context.RunLifecycleGeneration
                            == LifecycleGeneration)
                    {
                        values.Add(value);
                    }
                }
                values.Sort();
                return new ReadOnlyCollection<PersonalRewardDeliveryEnvelope>(
                    values);
            }
        }

        internal bool TryEnqueuePersonalReward(
            PersonalRewardGenerationResult result,
            out PersonalRewardDeliveryEnvelope envelope,
            out string diagnostic)
        {
            envelope = null;
            diagnostic = string.Empty;
            if (result == null || !result.IsSuccess)
            {
                diagnostic = "personal-reward-outbox-result-invalid";
                return false;
            }
            if (result.Context.RunStableId != RunStableId
                || result.Context.RunLifecycleGeneration != LifecycleGeneration)
            {
                diagnostic = "personal-reward-outbox-run-lifecycle-mismatch";
                return false;
            }
            lock (rewardRuntimeGate)
            {
                PersonalRewardDeliveryEnvelope existing;
                if (personalRewardDeliveries.TryGetValue(
                        result.Context.OperationStableId,
                        out existing))
                {
                    envelope = existing;
                    if (!string.Equals(
                            existing.Result.Fingerprint,
                            result.Fingerprint,
                            StringComparison.Ordinal)
                        || existing.Result.Context.ParticipantStableId
                            != result.Context.ParticipantStableId)
                    {
                        diagnostic = "personal-reward-outbox-operation-conflict";
                        return false;
                    }
                    return true;
                }
                envelope = new PersonalRewardDeliveryEnvelope(
                    result,
                    PersonalRewardDeliveryState.Pending,
                    string.Empty);
                personalRewardDeliveries.Add(
                    result.Context.OperationStableId,
                    envelope);
                return true;
            }
        }

        internal bool TryMarkPersonalRewardDelivered(
            StableId operationStableId,
            StableId participantStableId,
            string resultFingerprint,
            string deliveryFingerprint,
            out PersonalRewardDeliveryEnvelope envelope,
            out string diagnostic)
        {
            envelope = null;
            diagnostic = string.Empty;
            if (operationStableId == null || participantStableId == null)
            {
                diagnostic = "personal-reward-delivery-identity-missing";
                return false;
            }
            lock (rewardRuntimeGate)
            {
                PersonalRewardDeliveryEnvelope existing;
                if (!personalRewardDeliveries.TryGetValue(
                        operationStableId,
                        out existing))
                {
                    diagnostic = "personal-reward-delivery-missing";
                    return false;
                }
                if (existing.Result.Context.ParticipantStableId
                        != participantStableId
                    || !string.Equals(
                        existing.Result.Fingerprint,
                        resultFingerprint,
                        StringComparison.Ordinal))
                {
                    envelope = existing;
                    diagnostic = "personal-reward-delivery-context-conflict";
                    return false;
                }
                if (existing.State == PersonalRewardDeliveryState.Delivered)
                {
                    envelope = existing;
                    if (!string.Equals(
                            existing.DeliveryFingerprint,
                            deliveryFingerprint,
                            StringComparison.Ordinal))
                    {
                        diagnostic = "personal-reward-delivery-fingerprint-conflict";
                        return false;
                    }
                    return true;
                }
                envelope = existing.WithDelivered(deliveryFingerprint);
                personalRewardDeliveries[operationStableId] = envelope;
                return true;
            }
        }

        public RunLootSnapshot ExportRewardRuntimeSnapshot()
        {
            lock (rewardRuntimeGate)
            {
                EnsurePrimaryRewardParticipant();
                if (rewardEnvironment == null)
                {
                    throw new InvalidOperationException(
                        "The run reward environment has not been configured.");
                }
                var participants = new List<RunRewardParticipantState>(
                    rewardParticipants.Values);
                participants.Sort();
                var pacing = new List<ParticipantDropPacingState>();
                foreach (ParticipantDropPacingState value in
                    rewardPacingStates.Values)
                {
                    if (value.RunLifecycleGeneration == LifecycleGeneration)
                    {
                        pacing.Add(value);
                    }
                }
                pacing.Sort(delegate(
                    ParticipantDropPacingState left,
                    ParticipantDropPacingState right)
                {
                    return left.ParticipantStableId.CompareTo(
                        right.ParticipantStableId);
                });
                var deliveries = new List<PersonalRewardDeliveryEnvelope>();
                foreach (PersonalRewardDeliveryEnvelope value in
                    personalRewardDeliveries.Values)
                {
                    if (value.Result.Context.RunLifecycleGeneration
                        == LifecycleGeneration)
                    {
                        deliveries.Add(value);
                    }
                }
                deliveries.Sort();
                return new RunLootSnapshot(
                    RunStableId,
                    checked((int)LifecycleGeneration),
                    rewardEnvironment,
                    participants,
                    pacing,
                    deliveries);
            }
        }

        public void RestoreRewardRuntimeSnapshot(
            RunLootSnapshot snapshot)
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
                    RunRewardParticipantState participant =
                        snapshot.Participants[index];
                    rewardParticipants.Add(
                        participant.ParticipantStableId,
                        participant);
                }
                rewardPacingStates.Clear();
                for (int index = 0; index < snapshot.PacingStates.Count; index++)
                {
                    ParticipantDropPacingState pacing =
                        snapshot.PacingStates[index];
                    rewardPacingStates.Add(PacingKey(pacing), pacing);
                }
                personalRewardDeliveries.Clear();
                for (int index = 0; index < snapshot.Deliveries.Count; index++)
                {
                    PersonalRewardDeliveryEnvelope delivery =
                        snapshot.Deliveries[index];
                    personalRewardDeliveries.Add(
                        delivery.Result.Context.OperationStableId,
                        delivery);
                }
                rewardEnvironment = snapshot.Environment;
            }
        }

        internal bool TryLoadRewardPacingState(
            StableId runStableId,
            int runLifecycleGeneration,
            StableId participantStableId,
            out ParticipantDropPacingState state)
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
            ParticipantDropPacingState state)
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
            RunPlayerSnapshot player =
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
                    new RunRewardParticipantState(
                        player.ParticipantStableId,
                        FrozenInputs.CharacterStats.Level,
                        true,
                        true,
                        true,
                        true,
                        false));
            }
        }

        private static string PacingKey(ParticipantDropPacingState state)
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
