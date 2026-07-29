using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Runs.Session
{
    /// <summary>
    /// Complete transient reward snapshot used by reconnect and host migration. It is
    /// run truth only and is never promoted into permanent character progression.
    /// </summary>
    public sealed class RunLootSnapshot
    {
        private readonly ReadOnlyCollection<RunRewardParticipantState> participants;
        private readonly ReadOnlyCollection<ParticipantDropPacingState> pacingStates;
        private readonly ReadOnlyCollection<PersonalRewardDeliveryEnvelope> deliveries;
        private readonly string canonicalText;

        public RunLootSnapshot(
            StableId runStableId,
            int runLifecycleGeneration,
            RunRewardEnvironmentSnapshot environment,
            IEnumerable<RunRewardParticipantState> participants,
            IEnumerable<ParticipantDropPacingState> pacingStates,
            IEnumerable<PersonalRewardDeliveryEnvelope> deliveries = null)
        {
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (runLifecycleGeneration < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runLifecycleGeneration));
            }
            RunLifecycleGeneration = runLifecycleGeneration;
            Environment = environment
                ?? throw new ArgumentNullException(nameof(environment));
            this.participants = CopyParticipants(participants);
            this.pacingStates = CopyPacing(
                pacingStates,
                RunStableId,
                RunLifecycleGeneration);
            this.deliveries = CopyDeliveries(
                deliveries,
                RunStableId,
                RunLifecycleGeneration);

            var builder = new StringBuilder(
                "schema=run-reward-runtime-snapshot-v1");
            builder.Append("\nrun_id=").Append(RunStableId)
                .Append("\nrun_lifecycle=")
                .Append(RunLifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture))
                .Append("\nenvironment=").Append(Environment.Fingerprint)
                .Append("\nparticipant_count=")
                .Append(this.participants.Count.ToString(
                    CultureInfo.InvariantCulture));
            for (int index = 0; index < this.participants.Count; index++)
            {
                builder.Append("\nparticipant_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append("=").Append(this.participants[index].Fingerprint);
            }
            builder.Append("\npacing_count=")
                .Append(this.pacingStates.Count.ToString(
                    CultureInfo.InvariantCulture));
            for (int index = 0; index < this.pacingStates.Count; index++)
            {
                builder.Append("\npacing_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append("=").Append(this.pacingStates[index].Fingerprint);
            }
            builder.Append("\ndelivery_count=")
                .Append(this.deliveries.Count.ToString(
                    CultureInfo.InvariantCulture));
            for (int index = 0; index < this.deliveries.Count; index++)
            {
                builder.Append("\ndelivery_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append("=").Append(this.deliveries[index].Fingerprint);
            }
            canonicalText = builder.ToString();
            Fingerprint = RunSessionFingerprint.Hash(canonicalText);
        }

        public StableId RunStableId { get; }
        public int RunLifecycleGeneration { get; }
        public RunRewardEnvironmentSnapshot Environment { get; }
        public IReadOnlyList<RunRewardParticipantState> Participants
        {
            get { return participants; }
        }
        public IReadOnlyList<ParticipantDropPacingState> PacingStates
        {
            get { return pacingStates; }
        }
        public IReadOnlyList<PersonalRewardDeliveryEnvelope> Deliveries
        {
            get { return deliveries; }
        }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        private static ReadOnlyCollection<RunRewardParticipantState>
            CopyParticipants(IEnumerable<RunRewardParticipantState> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            var values = new List<RunRewardParticipantState>();
            var ids = new HashSet<StableId>();
            foreach (RunRewardParticipantState value in source)
            {
                if (value == null || !ids.Add(value.ParticipantStableId))
                {
                    throw new ArgumentException(
                        "Reward participants must be non-null and unique.",
                        nameof(source));
                }
                values.Add(value);
            }
            values.Sort();
            if (values.Count < 1 || values.Count > 4)
            {
                throw new ArgumentException(
                    "A run reward snapshot supports one to four participants.",
                    nameof(source));
            }
            return new ReadOnlyCollection<RunRewardParticipantState>(values);
        }

        private static ReadOnlyCollection<ParticipantDropPacingState>
            CopyPacing(
                IEnumerable<ParticipantDropPacingState> source,
                StableId runStableId,
                int runLifecycleGeneration)
        {
            var values = new List<ParticipantDropPacingState>();
            var ids = new HashSet<StableId>();
            if (source != null)
            {
                foreach (ParticipantDropPacingState value in source)
                {
                    if (value == null
                        || value.RunStableId != runStableId
                        || value.RunLifecycleGeneration != runLifecycleGeneration
                        || !ids.Add(value.ParticipantStableId))
                    {
                        throw new ArgumentException(
                            "Pacing snapshots must be unique and belong to the exact run lifecycle.",
                            nameof(source));
                    }
                    values.Add(value);
                }
            }
            values.Sort(delegate(
                ParticipantDropPacingState left,
                ParticipantDropPacingState right)
            {
                return left.ParticipantStableId.CompareTo(
                    right.ParticipantStableId);
            });
            return new ReadOnlyCollection<ParticipantDropPacingState>(values);
        }

        private static ReadOnlyCollection<PersonalRewardDeliveryEnvelope>
            CopyDeliveries(
                IEnumerable<PersonalRewardDeliveryEnvelope> source,
                StableId runStableId,
                int runLifecycleGeneration)
        {
            var values = new List<PersonalRewardDeliveryEnvelope>();
            var operations = new HashSet<StableId>();
            if (source != null)
            {
                foreach (PersonalRewardDeliveryEnvelope value in source)
                {
                    if (value == null
                        || value.Result.Context.RunStableId != runStableId
                        || value.Result.Context.RunLifecycleGeneration
                            != runLifecycleGeneration
                        || !operations.Add(
                            value.Result.Context.OperationStableId))
                    {
                        throw new ArgumentException(
                            "Personal reward deliveries must be unique and belong to the exact run lifecycle.",
                            nameof(source));
                    }
                    values.Add(value);
                }
            }
            values.Sort();
            return new ReadOnlyCollection<PersonalRewardDeliveryEnvelope>(
                values);
        }
    }
}
