using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Runs.Session
{
    /// <summary>
    /// Complete transient reward snapshot used by reconnect and host migration. It is
    /// run truth only and is never promoted into permanent character progression.
    /// </summary>
    public sealed class RunRewardRuntimeSnapshotV1
    {
        private readonly ReadOnlyCollection<RunRewardParticipantStateV1> participants;
        private readonly ReadOnlyCollection<ParticipantDropPacingStateV1> pacingStates;
        private readonly string canonicalText;

        public RunRewardRuntimeSnapshotV1(
            StableId runStableId,
            int runLifecycleGeneration,
            RunRewardEnvironmentSnapshotV1 environment,
            IEnumerable<RunRewardParticipantStateV1> participants,
            IEnumerable<ParticipantDropPacingStateV1> pacingStates)
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
            canonicalText = builder.ToString();
            Fingerprint = RunSessionFingerprintV1.Hash(canonicalText);
        }

        public StableId RunStableId { get; }
        public int RunLifecycleGeneration { get; }
        public RunRewardEnvironmentSnapshotV1 Environment { get; }
        public IReadOnlyList<RunRewardParticipantStateV1> Participants
        {
            get { return participants; }
        }
        public IReadOnlyList<ParticipantDropPacingStateV1> PacingStates
        {
            get { return pacingStates; }
        }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        private static ReadOnlyCollection<RunRewardParticipantStateV1>
            CopyParticipants(IEnumerable<RunRewardParticipantStateV1> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            var values = new List<RunRewardParticipantStateV1>();
            var ids = new HashSet<StableId>();
            foreach (RunRewardParticipantStateV1 value in source)
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
            return new ReadOnlyCollection<RunRewardParticipantStateV1>(values);
        }

        private static ReadOnlyCollection<ParticipantDropPacingStateV1>
            CopyPacing(
                IEnumerable<ParticipantDropPacingStateV1> source,
                StableId runStableId,
                int runLifecycleGeneration)
        {
            var values = new List<ParticipantDropPacingStateV1>();
            var ids = new HashSet<StableId>();
            if (source != null)
            {
                foreach (ParticipantDropPacingStateV1 value in source)
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
                ParticipantDropPacingStateV1 left,
                ParticipantDropPacingStateV1 right)
            {
                return left.ParticipantStableId.CompareTo(
                    right.ParticipantStableId);
            });
            return new ReadOnlyCollection<ParticipantDropPacingStateV1>(values);
        }
    }
}
