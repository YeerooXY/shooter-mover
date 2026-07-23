using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.Domain.Rewards.Drops
{
    public sealed class DropSaturationBandV1 : IComparable<DropSaturationBandV1>
    {
        public DropSaturationBandV1(int minimumBoxCount, int multiplierMillionths)
        {
            if (minimumBoxCount < 0) throw new ArgumentOutOfRangeException(nameof(minimumBoxCount));
            if (multiplierMillionths < 1 || multiplierMillionths > RunDropPacingPolicyV1.ProbabilityScale)
                throw new ArgumentOutOfRangeException(nameof(multiplierMillionths));
            MinimumBoxCount = minimumBoxCount;
            MultiplierMillionths = multiplierMillionths;
        }

        public int MinimumBoxCount { get; }
        public int MultiplierMillionths { get; }
        public int CompareTo(DropSaturationBandV1 other) { return ReferenceEquals(other, null) ? 1 : MinimumBoxCount.CompareTo(other.MinimumBoxCount); }
        public string ToCanonicalString() { return MinimumBoxCount.ToString(CultureInfo.InvariantCulture) + ":" + MultiplierMillionths.ToString(CultureInfo.InvariantCulture); }
    }

    /// <summary>
    /// Explicit bounded pacing formula:
    /// effective = clamp(base * room / scale * run / scale + pity, 0, scale).
    /// Guaranteed rewards bypass this formula unless explicitly configured otherwise.
    /// </summary>
    public sealed class RunDropPacingPolicyV1
    {
        public const int ProbabilityScale = 1000000;
        private readonly ReadOnlyCollection<DropSaturationBandV1> roomBands;
        private readonly ReadOnlyCollection<DropSaturationBandV1> runBands;
        private readonly string canonicalText;

        public RunDropPacingPolicyV1(
            StableId policyStableId,
            int minimumBoxesPerCompletedRun,
            int pityBeginsAfterFailures,
            int pityIncreaseMillionthsPerFailure,
            int maximumPityBonusMillionths,
            bool guaranteedBoxesResetPity,
            IEnumerable<DropSaturationBandV1> roomBands,
            IEnumerable<DropSaturationBandV1> runBands)
        {
            PolicyStableId = policyStableId ?? throw new ArgumentNullException(nameof(policyStableId));
            if (minimumBoxesPerCompletedRun < 0 || pityBeginsAfterFailures < 0
                || pityIncreaseMillionthsPerFailure < 0 || maximumPityBonusMillionths < 0
                || maximumPityBonusMillionths > ProbabilityScale)
                throw new ArgumentOutOfRangeException(nameof(minimumBoxesPerCompletedRun));
            MinimumBoxesPerCompletedRun = minimumBoxesPerCompletedRun;
            PityBeginsAfterFailures = pityBeginsAfterFailures;
            PityIncreaseMillionthsPerFailure = pityIncreaseMillionthsPerFailure;
            MaximumPityBonusMillionths = maximumPityBonusMillionths;
            GuaranteedBoxesResetPity = guaranteedBoxesResetPity;
            this.roomBands = CopyBands(roomBands, nameof(roomBands));
            this.runBands = CopyBands(runBands, nameof(runBands));
            var builder = new StringBuilder("schema=run-drop-pacing-policy-v1");
            builder.Append("\npolicy_id=").Append(PolicyStableId)
                .Append("\nminimum_boxes_per_completed_run=").Append(MinimumBoxesPerCompletedRun.ToString(CultureInfo.InvariantCulture))
                .Append("\npity_begins_after_failures=").Append(PityBeginsAfterFailures.ToString(CultureInfo.InvariantCulture))
                .Append("\npity_increase_millionths=").Append(PityIncreaseMillionthsPerFailure.ToString(CultureInfo.InvariantCulture))
                .Append("\nmaximum_pity_bonus_millionths=").Append(MaximumPityBonusMillionths.ToString(CultureInfo.InvariantCulture))
                .Append("\nguaranteed_boxes_reset_pity=").Append(GuaranteedBoxesResetPity ? "1" : "0");
            AppendBands(builder, "room", this.roomBands);
            AppendBands(builder, "run", this.runBands);
            canonicalText = builder.ToString();
            Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
        }

        public StableId PolicyStableId { get; }
        public int MinimumBoxesPerCompletedRun { get; }
        public int PityBeginsAfterFailures { get; }
        public int PityIncreaseMillionthsPerFailure { get; }
        public int MaximumPityBonusMillionths { get; }
        public bool GuaranteedBoxesResetPity { get; }
        public IReadOnlyList<DropSaturationBandV1> RoomBands { get { return roomBands; } }
        public IReadOnlyList<DropSaturationBandV1> RunBands { get { return runBands; } }
        public string Fingerprint { get; }

        public int CalculatePityBonus(int consecutiveEligibleFailures)
        {
            if (consecutiveEligibleFailures < 0) throw new ArgumentOutOfRangeException(nameof(consecutiveEligibleFailures));
            if (consecutiveEligibleFailures < PityBeginsAfterFailures) return 0;
            long steps = checked((long)consecutiveEligibleFailures - PityBeginsAfterFailures + 1L);
            return (int)Math.Min(MaximumPityBonusMillionths, checked(steps * PityIncreaseMillionthsPerFailure));
        }

        public int GetRoomSaturationMultiplier(int randomBoxesInRoom) { return ResolveBand(roomBands, randomBoxesInRoom); }
        public int GetRunSaturationMultiplier(int randomBoxesInRun) { return ResolveBand(runBands, randomBoxesInRun); }

        public int CalculateEffectiveRandomBoxProbability(int baseProbabilityMillionths, ParticipantDropPacingStateV1 state)
        {
            if (baseProbabilityMillionths < 0 || baseProbabilityMillionths > ProbabilityScale)
                throw new ArgumentOutOfRangeException(nameof(baseProbabilityMillionths));
            if (state == null) throw new ArgumentNullException(nameof(state));
            long scaled = checked((long)baseProbabilityMillionths * GetRoomSaturationMultiplier(state.RandomBoxesInCurrentRoom) / ProbabilityScale);
            scaled = checked(scaled * GetRunSaturationMultiplier(state.RandomBoxesInRun) / ProbabilityScale);
            long effective = checked(scaled + CalculatePityBonus(state.ConsecutiveEligibleRandomBoxFailures));
            return (int)Math.Max(0L, Math.Min(ProbabilityScale, effective));
        }

        public string ToCanonicalString() { return canonicalText; }

        private static int ResolveBand(IReadOnlyList<DropSaturationBandV1> bands, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            int multiplier = bands[0].MultiplierMillionths;
            for (int index = 1; index < bands.Count; index++)
            {
                if (bands[index].MinimumBoxCount > count) break;
                multiplier = bands[index].MultiplierMillionths;
            }
            return multiplier;
        }

        private static ReadOnlyCollection<DropSaturationBandV1> CopyBands(IEnumerable<DropSaturationBandV1> source, string parameterName)
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            var copy = new List<DropSaturationBandV1>();
            foreach (DropSaturationBandV1 band in source)
            {
                if (band == null) throw new ArgumentException("Saturation bands must not contain null entries.", parameterName);
                copy.Add(band);
            }
            copy.Sort();
            if (copy.Count == 0 || copy[0].MinimumBoxCount != 0) throw new ArgumentException("Saturation bands must begin at zero.", parameterName);
            for (int index = 1; index < copy.Count; index++)
                if (copy[index - 1].MinimumBoxCount == copy[index].MinimumBoxCount)
                    throw new ArgumentException("Saturation band thresholds must be unique.", parameterName);
            return new ReadOnlyCollection<DropSaturationBandV1>(copy);
        }

        private static void AppendBands(StringBuilder builder, string prefix, IReadOnlyList<DropSaturationBandV1> bands)
        {
            builder.Append("\n").Append(prefix).Append("_band_count=").Append(bands.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < bands.Count; index++)
                builder.Append("\n").Append(prefix).Append("_band_").Append(index.ToString("D4", CultureInfo.InvariantCulture)).Append("=").Append(bands[index].ToCanonicalString());
        }
    }
}
