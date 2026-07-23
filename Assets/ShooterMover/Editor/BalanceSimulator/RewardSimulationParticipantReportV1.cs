using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.EditorTools.BalanceSimulator
{
    public sealed class RewardSimulationParticipantReportV1 :
        IComparable<RewardSimulationParticipantReportV1>
    {
        private readonly ReadOnlyDictionary<StableId, long> strongboxTierCounts;
        private readonly string canonicalText;

        public RewardSimulationParticipantReportV1(
            StableId participantStableId,
            long sourceAttempts,
            long noDropCount,
            long moneyQuantity,
            long scrapQuantity,
            long miscQuantity,
            long randomStrongboxCount,
            long guaranteedStrongboxCount,
            long pityActivations,
            long roomSaturationActivations,
            long runSaturationActivations,
            long runMinimumGrants,
            long rawStrongboxProbabilityMillionthsTotal,
            long effectiveStrongboxProbabilityMillionthsTotal,
            IDictionary<StableId, long> strongboxTierCounts,
            ParticipantDropPacingStateV1 finalPacingState)
        {
            ParticipantStableId = participantStableId
                ?? throw new ArgumentNullException(nameof(participantStableId));
            if (sourceAttempts < 0
                || noDropCount < 0
                || moneyQuantity < 0
                || scrapQuantity < 0
                || miscQuantity < 0
                || randomStrongboxCount < 0
                || guaranteedStrongboxCount < 0
                || pityActivations < 0
                || roomSaturationActivations < 0
                || runSaturationActivations < 0
                || runMinimumGrants < 0
                || rawStrongboxProbabilityMillionthsTotal < 0
                || effectiveStrongboxProbabilityMillionthsTotal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceAttempts));
            }
            SourceAttempts = sourceAttempts;
            NoDropCount = noDropCount;
            MoneyQuantity = moneyQuantity;
            ScrapQuantity = scrapQuantity;
            MiscQuantity = miscQuantity;
            RandomStrongboxCount = randomStrongboxCount;
            GuaranteedStrongboxCount = guaranteedStrongboxCount;
            PityActivations = pityActivations;
            RoomSaturationActivations = roomSaturationActivations;
            RunSaturationActivations = runSaturationActivations;
            RunMinimumGrants = runMinimumGrants;
            RawStrongboxProbabilityMillionthsTotal =
                rawStrongboxProbabilityMillionthsTotal;
            EffectiveStrongboxProbabilityMillionthsTotal =
                effectiveStrongboxProbabilityMillionthsTotal;
            FinalPacingState = finalPacingState;
            this.strongboxTierCounts = CopyTierCounts(strongboxTierCounts);

            var builder = new StringBuilder(
                "schema=reward-simulation-participant-report-v1");
            builder.Append("\nparticipant_id=").Append(ParticipantStableId)
                .Append("\nsource_attempts=").Append(SourceAttempts.ToString(CultureInfo.InvariantCulture))
                .Append("\nno_drop_count=").Append(NoDropCount.ToString(CultureInfo.InvariantCulture))
                .Append("\nmoney_quantity=").Append(MoneyQuantity.ToString(CultureInfo.InvariantCulture))
                .Append("\nscrap_quantity=").Append(ScrapQuantity.ToString(CultureInfo.InvariantCulture))
                .Append("\nmisc_quantity=").Append(MiscQuantity.ToString(CultureInfo.InvariantCulture))
                .Append("\nrandom_strongbox_count=").Append(RandomStrongboxCount.ToString(CultureInfo.InvariantCulture))
                .Append("\nguaranteed_strongbox_count=").Append(GuaranteedStrongboxCount.ToString(CultureInfo.InvariantCulture))
                .Append("\npity_activations=").Append(PityActivations.ToString(CultureInfo.InvariantCulture))
                .Append("\nroom_saturation_activations=").Append(RoomSaturationActivations.ToString(CultureInfo.InvariantCulture))
                .Append("\nrun_saturation_activations=").Append(RunSaturationActivations.ToString(CultureInfo.InvariantCulture))
                .Append("\nrun_minimum_grants=").Append(RunMinimumGrants.ToString(CultureInfo.InvariantCulture))
                .Append("\nraw_box_probability_total=").Append(RawStrongboxProbabilityMillionthsTotal.ToString(CultureInfo.InvariantCulture))
                .Append("\neffective_box_probability_total=").Append(EffectiveStrongboxProbabilityMillionthsTotal.ToString(CultureInfo.InvariantCulture))
                .Append("\nfinal_pacing=").Append(FinalPacingState == null ? "none" : FinalPacingState.Fingerprint)
                .Append("\ntier_count=").Append(this.strongboxTierCounts.Count.ToString(CultureInfo.InvariantCulture));
            int tierIndex = 0;
            foreach (KeyValuePair<StableId, long> pair in this.strongboxTierCounts)
            {
                builder.Append("\ntier_")
                    .Append(tierIndex.ToString("D4", CultureInfo.InvariantCulture))
                    .Append("=").Append(pair.Key)
                    .Append(":").Append(pair.Value.ToString(CultureInfo.InvariantCulture));
                tierIndex++;
            }
            canonicalText = builder.ToString();
            Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
        }

        public StableId ParticipantStableId { get; }
        public long SourceAttempts { get; }
        public long NoDropCount { get; }
        public long MoneyQuantity { get; }
        public long ScrapQuantity { get; }
        public long MiscQuantity { get; }
        public long RandomStrongboxCount { get; }
        public long GuaranteedStrongboxCount { get; }
        public long PityActivations { get; }
        public long RoomSaturationActivations { get; }
        public long RunSaturationActivations { get; }
        public long RunMinimumGrants { get; }
        public long RawStrongboxProbabilityMillionthsTotal { get; }
        public long EffectiveStrongboxProbabilityMillionthsTotal { get; }
        public IReadOnlyDictionary<StableId, long> StrongboxTierCounts
        {
            get { return strongboxTierCounts; }
        }
        public ParticipantDropPacingStateV1 FinalPacingState { get; }
        public string Fingerprint { get; }
        public double AverageRawStrongboxProbability
        {
            get
            {
                return SourceAttempts == 0L
                    ? 0d
                    : (double)RawStrongboxProbabilityMillionthsTotal
                        / SourceAttempts
                        / RunDropPacingPolicyV1.ProbabilityScale;
            }
        }
        public double AverageEffectiveStrongboxProbability
        {
            get
            {
                return SourceAttempts == 0L
                    ? 0d
                    : (double)EffectiveStrongboxProbabilityMillionthsTotal
                        / SourceAttempts
                        / RunDropPacingPolicyV1.ProbabilityScale;
            }
        }

        public int CompareTo(RewardSimulationParticipantReportV1 other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : ParticipantStableId.CompareTo(other.ParticipantStableId);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        private static ReadOnlyDictionary<StableId, long> CopyTierCounts(
            IDictionary<StableId, long> source)
        {
            var sorted = new SortedDictionary<StableId, long>();
            if (source != null)
            {
                foreach (KeyValuePair<StableId, long> pair in source)
                {
                    if (pair.Key == null || pair.Value < 0L)
                    {
                        throw new ArgumentException(
                            "Tier counts require non-null IDs and non-negative counts.",
                            nameof(source));
                    }
                    sorted.Add(pair.Key, pair.Value);
                }
            }
            return new ReadOnlyDictionary<StableId, long>(sorted);
        }
    }
}
