using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Domain.Rewards.Drops
{
    public enum RewardRollGroupBehaviorV1
    {
        ExclusiveWeightedOutcome = 1,
        IndependentProbabilityRoll = 2,
        GuaranteedGrant = 3,
        WeightedRewardCountRoll = 4,
    }

    public enum RewardBoxPacingModeV1
    {
        None = 1,
        RandomBox = 2,
        GuaranteedBox = 3,
    }

    /// <summary>
    /// One ordered authored reward decision. The ordinal is semantically meaningful:
    /// changing it changes the profile fingerprint and deterministic stream ordinal.
    /// </summary>
    public sealed class RewardRollGroupV1 :
        IComparable<RewardRollGroupV1>,
        IEquatable<RewardRollGroupV1>
    {
        public const int ProbabilityScale = 1000000;

        private readonly ReadOnlyCollection<RewardOutcomeV1> outcomes;
        private readonly string canonicalText;

        private RewardRollGroupV1(
            StableId groupStableId,
            int ordinal,
            RewardRollGroupBehaviorV1 behavior,
            int probabilityMillionths,
            RewardBoxPacingModeV1 boxPacingMode,
            IEnumerable<RewardOutcomeV1> outcomes)
        {
            GroupStableId = groupStableId
                ?? throw new ArgumentNullException(nameof(groupStableId));
            if (ordinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            }
            if (!Enum.IsDefined(typeof(RewardRollGroupBehaviorV1), behavior))
            {
                throw new ArgumentOutOfRangeException(nameof(behavior));
            }
            if (!Enum.IsDefined(typeof(RewardBoxPacingModeV1), boxPacingMode))
            {
                throw new ArgumentOutOfRangeException(nameof(boxPacingMode));
            }
            if (probabilityMillionths < 0 || probabilityMillionths > ProbabilityScale)
            {
                throw new ArgumentOutOfRangeException(nameof(probabilityMillionths));
            }

            Ordinal = ordinal;
            Behavior = behavior;
            ProbabilityMillionths = probabilityMillionths;
            BoxPacingMode = boxPacingMode;
            this.outcomes = CopyOutcomes(outcomes);
            ValidateShape();

            var builder = new StringBuilder("schema=reward-roll-group-v1");
            builder.Append("\ngroup_id=").Append(GroupStableId)
                .Append("\nordinal=").Append(Ordinal.ToString(CultureInfo.InvariantCulture))
                .Append("\nbehavior=").Append(((int)Behavior).ToString(CultureInfo.InvariantCulture))
                .Append("\nprobability_millionths=").Append(ProbabilityMillionths.ToString(CultureInfo.InvariantCulture))
                .Append("\nbox_pacing_mode=").Append(((int)BoxPacingMode).ToString(CultureInfo.InvariantCulture))
                .Append("\noutcome_count=").Append(this.outcomes.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.outcomes.Count; index++)
            {
                builder.Append("\noutcome_").Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append(":\n").Append(this.outcomes[index].ToCanonicalString());
            }
            canonicalText = builder.ToString();
            Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
        }

        public StableId GroupStableId { get; }
        public int Ordinal { get; }
        public RewardRollGroupBehaviorV1 Behavior { get; }
        public int ProbabilityMillionths { get; }
        public RewardBoxPacingModeV1 BoxPacingMode { get; }
        public IReadOnlyList<RewardOutcomeV1> Outcomes { get { return outcomes; } }
        public string Fingerprint { get; }

        public bool ContainsStrongbox
        {
            get
            {
                for (int index = 0; index < outcomes.Count; index++)
                {
                    if (outcomes[index].Grant != null
                        && outcomes[index].Grant.Kind == RewardGrantKindV1.Strongbox)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public static RewardRollGroupV1 CreateExclusive(
            StableId groupStableId,
            int ordinal,
            RewardBoxPacingModeV1 boxPacingMode,
            IEnumerable<RewardOutcomeV1> outcomes)
        {
            return new RewardRollGroupV1(
                groupStableId,
                ordinal,
                RewardRollGroupBehaviorV1.ExclusiveWeightedOutcome,
                ProbabilityScale,
                boxPacingMode,
                outcomes);
        }

        public static RewardRollGroupV1 CreateIndependent(
            StableId groupStableId,
            int ordinal,
            int probabilityMillionths,
            RewardBoxPacingModeV1 boxPacingMode,
            RewardOutcomeV1 outcome)
        {
            return new RewardRollGroupV1(
                groupStableId,
                ordinal,
                RewardRollGroupBehaviorV1.IndependentProbabilityRoll,
                probabilityMillionths,
                boxPacingMode,
                new[] { outcome });
        }

        public static RewardRollGroupV1 CreateGuaranteed(
            StableId groupStableId,
            int ordinal,
            RewardBoxPacingModeV1 boxPacingMode,
            IEnumerable<RewardOutcomeV1> outcomes)
        {
            return new RewardRollGroupV1(
                groupStableId,
                ordinal,
                RewardRollGroupBehaviorV1.GuaranteedGrant,
                ProbabilityScale,
                boxPacingMode,
                outcomes);
        }

        public static RewardRollGroupV1 CreateWeightedCount(
            StableId groupStableId,
            int ordinal,
            RewardBoxPacingModeV1 boxPacingMode,
            IEnumerable<RewardOutcomeV1> outcomes)
        {
            return new RewardRollGroupV1(
                groupStableId,
                ordinal,
                RewardRollGroupBehaviorV1.WeightedRewardCountRoll,
                ProbabilityScale,
                boxPacingMode,
                outcomes);
        }

        public RewardRollGroupV1 With(
            StableId resultGroupStableId,
            int resultOrdinal,
            int resultProbabilityMillionths,
            RewardBoxPacingModeV1 resultBoxPacingMode,
            IEnumerable<RewardOutcomeV1> resultOutcomes)
        {
            return new RewardRollGroupV1(
                resultGroupStableId,
                resultOrdinal,
                Behavior,
                resultProbabilityMillionths,
                resultBoxPacingMode,
                resultOutcomes);
        }

        public int CompareTo(RewardRollGroupV1 other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }
            int ordinalComparison = Ordinal.CompareTo(other.Ordinal);
            return ordinalComparison != 0
                ? ordinalComparison
                : GroupStableId.CompareTo(other.GroupStableId);
        }

        public bool Equals(RewardRollGroupV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RewardRollGroupV1);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(canonicalText);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        private ReadOnlyCollection<RewardOutcomeV1> CopyOutcomes(
            IEnumerable<RewardOutcomeV1> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            var copy = new List<RewardOutcomeV1>();
            var ids = new HashSet<StableId>();
            foreach (RewardOutcomeV1 outcome in source)
            {
                if (outcome == null || !ids.Add(outcome.OutcomeStableId))
                {
                    throw new ArgumentException(
                        "Reward outcomes must be non-null and have unique identities.",
                        nameof(source));
                }
                copy.Add(outcome);
            }
            copy.Sort();
            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "A reward roll group requires at least one outcome.",
                    nameof(source));
            }
            return new ReadOnlyCollection<RewardOutcomeV1>(copy);
        }

        private void ValidateShape()
        {
            if (Behavior == RewardRollGroupBehaviorV1.IndependentProbabilityRoll
                && outcomes.Count != 1)
            {
                throw new ArgumentException(
                    "An independent probability group requires exactly one outcome.");
            }
            if (Behavior == RewardRollGroupBehaviorV1.GuaranteedGrant
                && (outcomes.Count != 1 || outcomes[0].IsExplicitNoDrop))
            {
                throw new ArgumentException(
                    "A guaranteed grant group requires exactly one grant outcome.");
            }
            if (Behavior != RewardRollGroupBehaviorV1.IndependentProbabilityRoll
                && ProbabilityMillionths != ProbabilityScale)
            {
                throw new ArgumentException(
                    "Only independent groups author a group-level probability.");
            }
            if (BoxPacingMode != RewardBoxPacingModeV1.None && !ContainsStrongbox)
            {
                throw new ArgumentException(
                    "A paced box group must contain a strongbox outcome.");
            }
            if (BoxPacingMode == RewardBoxPacingModeV1.RandomBox
                && Behavior == RewardRollGroupBehaviorV1.GuaranteedGrant)
            {
                throw new ArgumentException(
                    "Guaranteed grant groups cannot be random-box paced.");
            }
            if (Behavior == RewardRollGroupBehaviorV1.WeightedRewardCountRoll)
            {
                RewardGrantKindV1? kind = null;
                StableId content = null;
                for (int index = 0; index < outcomes.Count; index++)
                {
                    RewardGrantSpecificationV1 grant = outcomes[index].Grant;
                    if (grant == null || outcomes[index].IsExplicitNoDrop || !grant.Quantity.IsFixed)
                    {
                        throw new ArgumentException(
                            "Weighted count outcomes require fixed grant quantities.");
                    }
                    if (kind.HasValue
                        && (kind.Value != grant.Kind || content != grant.ContentStableId))
                    {
                        throw new ArgumentException(
                            "Weighted count outcomes must target one reward kind and content identity.");
                    }
                    kind = grant.Kind;
                    content = grant.ContentStableId;
                }
            }
        }
    }
}
