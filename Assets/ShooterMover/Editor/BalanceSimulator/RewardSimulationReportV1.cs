using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.EditorTools.BalanceSimulator
{
    public sealed class RewardSimulationReportV1
    {
        private readonly ReadOnlyCollection<RewardSimulationParticipantReportV1>
            participantReports;
        private readonly string canonicalText;

        public RewardSimulationReportV1(
            StableId sourceProfileReferenceId,
            string effectiveProfileFingerprint,
            string pacingPolicyFingerprint,
            int missionLevel,
            StableId difficultyStableId,
            StableId gameModeStableId,
            int sourcesPerRoom,
            int roomCount,
            int sampleCount,
            ulong seed,
            IEnumerable<RewardSimulationParticipantReportV1> participantReports,
            long rejectedGenerationCount,
            string diagnostic)
        {
            SourceProfileReferenceId = sourceProfileReferenceId
                ?? throw new ArgumentNullException(nameof(sourceProfileReferenceId));
            if (string.IsNullOrWhiteSpace(effectiveProfileFingerprint)
                || string.IsNullOrWhiteSpace(pacingPolicyFingerprint))
            {
                throw new ArgumentException(
                    "Simulation report requires frozen profile and pacing fingerprints.");
            }
            DifficultyStableId = difficultyStableId
                ?? throw new ArgumentNullException(nameof(difficultyStableId));
            GameModeStableId = gameModeStableId
                ?? throw new ArgumentNullException(nameof(gameModeStableId));
            if (missionLevel < 0
                || sourcesPerRoom < 1
                || roomCount < 1
                || sampleCount < 1
                || rejectedGenerationCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(missionLevel));
            }
            EffectiveProfileFingerprint = effectiveProfileFingerprint.Trim();
            PacingPolicyFingerprint = pacingPolicyFingerprint.Trim();
            MissionLevel = missionLevel;
            SourcesPerRoom = sourcesPerRoom;
            RoomCount = roomCount;
            SampleCount = sampleCount;
            Seed = seed;
            this.participantReports = CopyReports(participantReports);
            RejectedGenerationCount = rejectedGenerationCount;
            Diagnostic = diagnostic ?? string.Empty;

            var builder = new StringBuilder(
                "schema=reward-simulation-report-v1");
            builder.Append("\nsource_profile_reference_id=")
                .Append(SourceProfileReferenceId)
                .Append("\neffective_profile_fingerprint=")
                .Append(EffectiveProfileFingerprint)
                .Append("\npacing_policy_fingerprint=")
                .Append(PacingPolicyFingerprint)
                .Append("\nmission_level=")
                .Append(MissionLevel.ToString(CultureInfo.InvariantCulture))
                .Append("\ndifficulty_id=").Append(DifficultyStableId)
                .Append("\ngame_mode_id=").Append(GameModeStableId)
                .Append("\nsources_per_room=")
                .Append(SourcesPerRoom.ToString(CultureInfo.InvariantCulture))
                .Append("\nroom_count=")
                .Append(RoomCount.ToString(CultureInfo.InvariantCulture))
                .Append("\nsample_count=")
                .Append(SampleCount.ToString(CultureInfo.InvariantCulture))
                .Append("\nseed=")
                .Append(Seed.ToString(CultureInfo.InvariantCulture))
                .Append("\nrejected_generation_count=")
                .Append(RejectedGenerationCount.ToString(CultureInfo.InvariantCulture))
                .Append("\ndiagnostic=")
                .Append(Diagnostic.Replace("\r", string.Empty).Replace("\n", "\\n"))
                .Append("\nparticipant_report_count=")
                .Append(this.participantReports.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.participantReports.Count; index++)
            {
                builder.Append("\nparticipant_report_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append(":\n")
                    .Append(this.participantReports[index].ToCanonicalString());
            }
            canonicalText = builder.ToString();
            Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
        }

        public StableId SourceProfileReferenceId { get; }
        public string EffectiveProfileFingerprint { get; }
        public string PacingPolicyFingerprint { get; }
        public int MissionLevel { get; }
        public StableId DifficultyStableId { get; }
        public StableId GameModeStableId { get; }
        public int SourcesPerRoom { get; }
        public int RoomCount { get; }
        public int SampleCount { get; }
        public ulong Seed { get; }
        public IReadOnlyList<RewardSimulationParticipantReportV1> ParticipantReports
        {
            get { return participantReports; }
        }
        public long RejectedGenerationCount { get; }
        public string Diagnostic { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        private static ReadOnlyCollection<RewardSimulationParticipantReportV1>
            CopyReports(
                IEnumerable<RewardSimulationParticipantReportV1> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            var copy = new List<RewardSimulationParticipantReportV1>();
            var ids = new HashSet<StableId>();
            foreach (RewardSimulationParticipantReportV1 value in source)
            {
                if (value == null || !ids.Add(value.ParticipantStableId))
                {
                    throw new ArgumentException(
                        "Participant reports must be non-null and unique.",
                        nameof(source));
                }
                copy.Add(value);
            }
            copy.Sort();
            if (copy.Count < 1 || copy.Count > 4)
            {
                throw new ArgumentException(
                    "Reward simulation reports require one to four participants.",
                    nameof(source));
            }
            return new ReadOnlyCollection<RewardSimulationParticipantReportV1>(copy);
        }
    }
}
