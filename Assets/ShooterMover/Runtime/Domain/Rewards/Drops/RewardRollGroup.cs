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
    public enum RewardRollGroupBehavior
    {
        ExclusiveWeightedOutcome = 1,
        IndependentProbabilityRoll = 2,
        GuaranteedGrant = 3,
        WeightedRewardCountRoll = 4,
    }

    public enum RewardBoxPacingMode
    {
        None = 1,
        RandomBox = 2,
        GuaranteedBox = 3,
    }

    /// <summary>
    /// One ordered authored reward decision. The ordinal is semantically meaningful:
    /// changing it changes the profile fingerprint and deterministic stream ordinal.
    /// </summary>
    public sealed class RewardRollGroup :
        IComparable<RewardRollGroup>,
        IEquatable<RewardRollGroup>
    {
        public const int ProbabilityScale = 1000000;

        private readonly ReadOnlyCollection<RewardOutcome> outcomes;
        private readonly string canonicalText;

        private RewardRollGroup(
            StableId groupStableId,
            int ordinal,
            RewardRollGroupBehavior behavior,
            int probabilityMillionths,
            RewardBoxPacingMode boxPacingMode,
            IEnumerable<RewardOutcome> outcomes)
        {
            GroupStableId = groupStableId
                ?? throw new ArgumentNullException(nameof(groupStableId));
            if (ordinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            }
            if (!Enum.IsDefined(typeof(RewardRollGroupBehavior), behavior))
            {
                throw new ArgumentOutOfRangeException(nameof(behavior));
            }
            if (!Enum.IsDefined(typeof(RewardBoxPacingMode), boxPacingMode))
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
            Fingerprint = RewardGenerationFingerprint.Compute(canonicalText);
        }

        public StableId GroupStableId { get; }
        public int Ordinal { get; }
        public RewardRollGroupBehavior Behavior { get; }
        public int ProbabilityMillionths { get; }
        public RewardBoxPacingMode BoxPacingMode { get; }
        public IReadOnlyList<RewardOutcome> Outcomes { get { return outcomes; } }
        public string Fingerprint { get; }

        public bool ContainsStrongbox
        {
            get
            {
                for (int index = 0; index < outcomes.Count; index++)
                {
                    if (outcomes[index].Grant != null
                        && outcomes[index].Grant.Kind == RewardGrantKind.Strongbox)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public static RewardRollGroup CreateExclusive(
            StableId groupStableId,
            int ordinal,
            RewardBoxPacingMode boxPacingMode,
            IEnumerable<RewardOutcome> outcomes)
        {
            return new RewardRollGroup(
                groupStableId,
                ordinal,
                RewardRollGroupBehavior.ExclusiveWeightedOutcome,
                ProbabilityScale,
                boxPacingMode,
                outcomes);
        }

        public static RewardRollGroup CreateIndependent(
            StableId groupStableId,
            int ordinal,
            int probabilityMillionths,
            RewardBoxPacingMode boxPacingMode,
            RewardOutcome outcome)
        {
            return new RewardRollGroup(
                groupStableId,
                ordinal,
                RewardRollGroupBehavior.IndependentProbabilityRoll,
                probabilityMillionths,
                boxPacingMode,
                new[] { outcome });
        }

        public static RewardRollGroup CreateGuaranteed(
            StableId groupStableId,
            int ordinal,
            RewardBoxPacingMode boxPacingMode,
            IEnumerable<RewardOutcome> outcomes)
        {
            return new RewardRollGroup(
                groupStableId,
                ordinal,
                RewardRollGroupBehavior.GuaranteedGrant,
                ProbabilityScale,
                boxPacingMode,
                outcomes);
        }

        public static RewardRollGroup CreateWeightedCount(
            StableId groupStableId,
            int ordinal,
            RewardBoxPacingMode boxPacingMode,
            IEnumerable<RewardOutcome> outcomes)
        {
            return new RewardRollGroup(
                groupStableId,
                ordinal,
                RewardRollGroupBehavior.WeightedRewardCountRoll,
                ProbabilityScale,
                boxPacingMode,
                outcomes);
        }

        public RewardRollGroup With(
            StableId resultGroupStableId,
            int resultOrdinal,
            int resultProbabilityMillionths,
            RewardBoxPacingMode resultBoxPacingMode,
            IEnumerable<RewardOutcome> resultOutcomes)
        {
            return new RewardRollGroup(
                resultGroupStableId,
                resultOrdinal,
                Behavior,
                resultProbabilityMillionths,
                resultBoxPacingMode,
                resultOutcomes);
        }

        public int CompareTo(RewardRollGroup other)
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

        public bool Equals(RewardRollGroup other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RewardRollGroup);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(canonicalText);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        private ReadOnlyCollection<RewardOutcome> CopyOutcomes(
            IEnumerable<RewardOutcome> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            var copy = new List<RewardOutcome>();
            var ids = new HashSet<StableId>();
            foreach (RewardOutcome outcome in source)
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
            return new ReadOnlyCollection<RewardOutcome>(copy);
        }

        private void ValidateShape()
        {
            if (Behavior == RewardRollGroupBehavior.IndependentProbabilityRoll
                && outcomes.Count != 1)
            {
                throw new ArgumentException(
                    "An independent probability group requires exactly one outcome.");
            }
            if (Behavior == RewardRollGroupBehavior.GuaranteedGrant
                && (outcomes.Count != 1 || outcomes[0].IsExplicitNoDrop))
            {
                throw new ArgumentException(
                    "A guaranteed grant group requires exactly one grant outcome.");
            }
            if (Behavior != RewardRollGroupBehavior.IndependentProbabilityRoll
                && ProbabilityMillionths != ProbabilityScale)
            {
                throw new ArgumentException(
                    "Only independent groups author a group-level probability.");
            }
            if (BoxPacingMode != RewardBoxPacingMode.None && !ContainsStrongbox)
            {
                throw new ArgumentException(
                    "A paced box group must contain a strongbox outcome.");
            }
            if (BoxPacingMode == RewardBoxPacingMode.RandomBox
                && Behavior == RewardRollGroupBehavior.GuaranteedGrant)
            {
                throw new ArgumentException(
                    "Guaranteed grant groups cannot be random-box paced.");
            }
            if (Behavior == RewardRollGroupBehavior.WeightedRewardCountRoll)
            {
                RewardGrantKind? kind = null;
                StableId content = null;
                for (int index = 0; index < outcomes.Count; index++)
                {
                    RewardGrantSpecification grant = outcomes[index].Grant;
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
