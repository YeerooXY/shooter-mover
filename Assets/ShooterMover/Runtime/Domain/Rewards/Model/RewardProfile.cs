using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Model
{
    /// <summary>
    /// Immutable independent chance roll using integer millionths, avoiding culture
    /// and floating-point serialization differences.
    /// </summary>
    public sealed class IndependentRewardRoll :
        IEquatable<IndependentRewardRoll>,
        IComparable<IndependentRewardRoll>,
        IComparable
    {
        public const int ProbabilityScale = 1000000;

        private readonly string canonicalText;

        private IndependentRewardRoll(
            StableId rollStableId,
            int probabilityMillionths,
            RewardGrantSpecification grant)
        {
            this.RollStableId = RewardModelFormat.RequireStableId(
                rollStableId,
                nameof(rollStableId));
            if (probabilityMillionths < 1 || probabilityMillionths > ProbabilityScale)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(probabilityMillionths),
                    probabilityMillionths,
                    "Independent reward probability must be between 1 and 1,000,000 millionths.");
            }

            this.ProbabilityMillionths = probabilityMillionths;
            this.Grant = grant ?? throw new ArgumentNullException(nameof(grant));
            this.canonicalText = "roll_stable_id="
                + this.RollStableId
                + "\nprobability_millionths="
                + this.ProbabilityMillionths.ToString(CultureInfo.InvariantCulture)
                + "\ngrant:\n"
                + this.Grant.ToCanonicalString();
        }

        public StableId RollStableId { get; }

        public int ProbabilityMillionths { get; }

        public RewardGrantSpecification Grant { get; }

        public static IndependentRewardRoll Create(
            StableId rollStableId,
            int probabilityMillionths,
            RewardGrantSpecification grant)
        {
            return new IndependentRewardRoll(rollStableId, probabilityMillionths, grant);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(IndependentRewardRoll other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as IndependentRewardRoll);
        }

        public override int GetHashCode()
        {
            return RewardModelFormat.DeterministicHash(this.canonicalText);
        }

        public int CompareTo(IndependentRewardRoll other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            return this.RollStableId.CompareTo(other.RollStableId);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            IndependentRewardRoll other = obj as IndependentRewardRoll;
            if (other == null)
            {
                throw new ArgumentException(
                    "Object must be an IndependentRewardRoll.",
                    nameof(obj));
            }

            return this.CompareTo(other);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }
    }

    public enum WeightedRewardOutcomeKind
    {
        Grant = 1,
        ExplicitNoDrop = 2,
    }

    /// <summary>
    /// One positive-weight exclusive outcome. Explicit no-drop is a first-class
    /// outcome rather than an accidental missing grant.
    /// </summary>
    public sealed class WeightedRewardOutcome :
        IEquatable<WeightedRewardOutcome>,
        IComparable<WeightedRewardOutcome>,
        IComparable
    {
        private readonly string canonicalText;

        private WeightedRewardOutcome(
            StableId outcomeStableId,
            long weight,
            WeightedRewardOutcomeKind kind,
            RewardGrantSpecification grant)
        {
            this.OutcomeStableId = RewardModelFormat.RequireStableId(
                outcomeStableId,
                nameof(outcomeStableId));
            if (weight < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(weight),
                    weight,
                    "Exclusive reward weights must be positive.");
            }

            RewardModelFormat.RequireDefinedEnum(kind, nameof(kind));
            if (kind == WeightedRewardOutcomeKind.Grant && grant == null)
            {
                throw new ArgumentNullException(nameof(grant));
            }

            if (kind == WeightedRewardOutcomeKind.ExplicitNoDrop && grant != null)
            {
                throw new ArgumentException(
                    "Explicit no-drop outcomes must not carry a grant.",
                    nameof(grant));
            }

            this.Weight = weight;
            this.Kind = kind;
            this.Grant = grant;
            this.canonicalText = "outcome_stable_id="
                + this.OutcomeStableId
                + "\nweight="
                + this.Weight.ToString(CultureInfo.InvariantCulture)
                + "\nkind="
                + ((int)this.Kind).ToString(CultureInfo.InvariantCulture)
                + "\ngrant:\n"
                + (this.Grant == null ? "null" : this.Grant.ToCanonicalString());
        }

        public StableId OutcomeStableId { get; }

        public long Weight { get; }

        public WeightedRewardOutcomeKind Kind { get; }

        public RewardGrantSpecification Grant { get; }

        public static WeightedRewardOutcome CreateGrant(
            StableId outcomeStableId,
            long weight,
            RewardGrantSpecification grant)
        {
            return new WeightedRewardOutcome(
                outcomeStableId,
                weight,
                WeightedRewardOutcomeKind.Grant,
                grant);
        }

        public static WeightedRewardOutcome CreateExplicitNoDrop(
            StableId outcomeStableId,
            long weight)
        {
            return new WeightedRewardOutcome(
                outcomeStableId,
                weight,
                WeightedRewardOutcomeKind.ExplicitNoDrop,
                null);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(WeightedRewardOutcome other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as WeightedRewardOutcome);
        }

        public override int GetHashCode()
        {
            return RewardModelFormat.DeterministicHash(this.canonicalText);
        }

        public int CompareTo(WeightedRewardOutcome other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            return this.OutcomeStableId.CompareTo(other.OutcomeStableId);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            WeightedRewardOutcome other = obj as WeightedRewardOutcome;
            if (other == null)
            {
                throw new ArgumentException(
                    "Object must be a WeightedRewardOutcome.",
                    nameof(obj));
            }

            return this.CompareTo(other);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }
    }

    /// <summary>
    /// One exclusive weighted group. Exactly one outcome is selected by a later
    /// generator; this type contains no sampling implementation.
    /// </summary>
    public sealed class ExclusiveRewardGroup :
        IEquatable<ExclusiveRewardGroup>,
        IComparable<ExclusiveRewardGroup>,
        IComparable
    {
        private readonly ReadOnlyCollection<WeightedRewardOutcome> outcomes;
        private readonly string canonicalText;

        private ExclusiveRewardGroup(
            StableId groupStableId,
            IEnumerable<WeightedRewardOutcome> outcomes)
        {
            this.GroupStableId = RewardModelFormat.RequireStableId(
                groupStableId,
                nameof(groupStableId));
            this.outcomes = RewardModelFormat.CopyAndSortUnique(
                outcomes,
                nameof(outcomes),
                delegate(WeightedRewardOutcome item) { return item.OutcomeStableId; });
            if (this.outcomes.Count == 0)
            {
                throw new ArgumentException(
                    "Exclusive reward groups must contain at least one weighted outcome.",
                    nameof(outcomes));
            }

            this.canonicalText = this.BuildCanonicalText();
        }

        public StableId GroupStableId { get; }

        public IReadOnlyList<WeightedRewardOutcome> Outcomes
        {
            get { return this.outcomes; }
        }

        public static ExclusiveRewardGroup Create(
            StableId groupStableId,
            IEnumerable<WeightedRewardOutcome> outcomes)
        {
            return new ExclusiveRewardGroup(groupStableId, outcomes);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(ExclusiveRewardGroup other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ExclusiveRewardGroup);
        }

        public override int GetHashCode()
        {
            return RewardModelFormat.DeterministicHash(this.canonicalText);
        }

        public int CompareTo(ExclusiveRewardGroup other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            return this.GroupStableId.CompareTo(other.GroupStableId);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            ExclusiveRewardGroup other = obj as ExclusiveRewardGroup;
            if (other == null)
            {
                throw new ArgumentException(
                    "Object must be an ExclusiveRewardGroup.",
                    nameof(obj));
            }

            return this.CompareTo(other);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("group_stable_id=")
                .Append(this.GroupStableId)
                .Append("\noutcome_count=")
                .Append(this.outcomes.Count.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < this.outcomes.Count; index++)
            {
                builder.Append("\noutcome_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append(":\n")
                    .Append(this.outcomes[index].ToCanonicalString());
            }

            return builder.ToString();
        }
    }

    public enum RewardProfileDisposition
    {
        Configured = 1,
        ExplicitNoDrop = 2,
    }

    /// <summary>
    /// Immutable reward profile containing coexisting guaranteed, independent, and
    /// exclusive reward specifications. Collection input order is not significant.
    /// </summary>
    public sealed class RewardProfile : IEquatable<RewardProfile>
    {
        private readonly ReadOnlyCollection<RewardGrantSpecification> guaranteedEntries;
        private readonly ReadOnlyCollection<IndependentRewardRoll> independentRolls;
        private readonly ReadOnlyCollection<ExclusiveRewardGroup> exclusiveGroups;
        private readonly string canonicalText;
        private readonly string fingerprint;

        private RewardProfile(
            StableId profileStableId,
            RewardProfileDisposition disposition,
            IEnumerable<RewardGrantSpecification> guaranteedEntries,
            IEnumerable<IndependentRewardRoll> independentRolls,
            IEnumerable<ExclusiveRewardGroup> exclusiveGroups)
        {
            this.ProfileStableId = RewardModelFormat.RequireStableId(
                profileStableId,
                nameof(profileStableId));
            RewardModelFormat.RequireDefinedEnum(disposition, nameof(disposition));
            this.Disposition = disposition;
            this.guaranteedEntries = RewardModelFormat.CopyAndSortUnique(
                guaranteedEntries,
                nameof(guaranteedEntries),
                delegate(RewardGrantSpecification item) { return item.GrantStableId; });
            this.independentRolls = RewardModelFormat.CopyAndSortUnique(
                independentRolls,
                nameof(independentRolls),
                delegate(IndependentRewardRoll item) { return item.RollStableId; });
            this.exclusiveGroups = RewardModelFormat.CopyAndSortUnique(
                exclusiveGroups,
                nameof(exclusiveGroups),
                delegate(ExclusiveRewardGroup item) { return item.GroupStableId; });

            this.ValidateDisposition();
            this.ValidateUniqueGrantIdentities();
            this.canonicalText = this.BuildCanonicalText();
            this.fingerprint = RewardModelFormat.Fingerprint(this.canonicalText);
        }

        public StableId ProfileStableId { get; }

        public RewardProfileDisposition Disposition { get; }

        public IReadOnlyList<RewardGrantSpecification> GuaranteedEntries
        {
            get { return this.guaranteedEntries; }
        }

        public IReadOnlyList<IndependentRewardRoll> IndependentRolls
        {
            get { return this.independentRolls; }
        }

        public IReadOnlyList<ExclusiveRewardGroup> ExclusiveGroups
        {
            get { return this.exclusiveGroups; }
        }

        public string Fingerprint
        {
            get { return this.fingerprint; }
        }

        public static RewardProfile Create(
            StableId profileStableId,
            IEnumerable<RewardGrantSpecification> guaranteedEntries,
            IEnumerable<IndependentRewardRoll> independentRolls,
            IEnumerable<ExclusiveRewardGroup> exclusiveGroups)
        {
            return new RewardProfile(
                profileStableId,
                RewardProfileDisposition.Configured,
                guaranteedEntries,
                independentRolls,
                exclusiveGroups);
        }

        public static RewardProfile CreateExplicitNoDrop(StableId profileStableId)
        {
            return new RewardProfile(
                profileStableId,
                RewardProfileDisposition.ExplicitNoDrop,
                Array.Empty<RewardGrantSpecification>(),
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());
        }

        public RewardProfile AppendGuaranteed(
            StableId resultProfileStableId,
            IEnumerable<RewardGrantSpecification> additionalEntries)
        {
            if (additionalEntries == null)
            {
                throw new ArgumentNullException(nameof(additionalEntries));
            }

            List<RewardGrantSpecification> combined =
                new List<RewardGrantSpecification>(this.guaranteedEntries);
            foreach (RewardGrantSpecification entry in additionalEntries)
            {
                combined.Add(entry);
            }

            return new RewardProfile(
                resultProfileStableId,
                RewardProfileDisposition.Configured,
                combined,
                this.independentRolls,
                this.exclusiveGroups);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(RewardProfile other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as RewardProfile);
        }

        public override int GetHashCode()
        {
            return RewardModelFormat.DeterministicHash(this.canonicalText);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }

        private void ValidateDisposition()
        {
            int configuredEntryCount = this.guaranteedEntries.Count
                + this.independentRolls.Count
                + this.exclusiveGroups.Count;
            if (this.Disposition == RewardProfileDisposition.ExplicitNoDrop
                && configuredEntryCount != 0)
            {
                throw new ArgumentException(
                    "Explicit no-drop profiles must not contain reward entries.");
            }

            if (this.Disposition == RewardProfileDisposition.Configured
                && configuredEntryCount == 0)
            {
                throw new ArgumentException(
                    "Configured reward profiles must not be accidentally empty; use CreateExplicitNoDrop instead.");
            }
        }

        private void ValidateUniqueGrantIdentities()
        {
            HashSet<StableId> grantIds = new HashSet<StableId>();
            for (int index = 0; index < this.guaranteedEntries.Count; index++)
            {
                this.AddGrantIdentity(grantIds, this.guaranteedEntries[index].GrantStableId);
            }

            for (int index = 0; index < this.independentRolls.Count; index++)
            {
                this.AddGrantIdentity(grantIds, this.independentRolls[index].Grant.GrantStableId);
            }

            for (int groupIndex = 0; groupIndex < this.exclusiveGroups.Count; groupIndex++)
            {
                IReadOnlyList<WeightedRewardOutcome> outcomes =
                    this.exclusiveGroups[groupIndex].Outcomes;
                for (int outcomeIndex = 0; outcomeIndex < outcomes.Count; outcomeIndex++)
                {
                    if (outcomes[outcomeIndex].Grant != null)
                    {
                        this.AddGrantIdentity(grantIds, outcomes[outcomeIndex].Grant.GrantStableId);
                    }
                }
            }
        }

        private void AddGrantIdentity(HashSet<StableId> grantIds, StableId grantId)
        {
            if (!grantIds.Add(grantId))
            {
                throw new ArgumentException(
                    "Reward profile contains duplicate grant identity " + grantId + ".");
            }
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("profile_stable_id=")
                .Append(this.ProfileStableId)
                .Append("\ndisposition=")
                .Append(((int)this.Disposition).ToString(CultureInfo.InvariantCulture));
            AppendCollection(builder, "guaranteed", this.guaranteedEntries);
            AppendCollection(builder, "independent_roll", this.independentRolls);
            AppendCollection(builder, "exclusive_group", this.exclusiveGroups);
            return builder.ToString();
        }

        private static void AppendCollection<T>(
            StringBuilder builder,
            string label,
            IReadOnlyList<T> values)
        {
            builder.Append("\n")
                .Append(label)
                .Append("_count=")
                .Append(values.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                builder.Append("\n")
                    .Append(label)
                    .Append("_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append(":\n")
                    .Append(values[index]);
            }
        }
    }

    public enum RewardSourceOverrideMode
    {
        InheritDefault = 1,
        NoReward = 2,
        ReplaceEntirely = 3,
        AppendGuaranteedEntries = 4,
    }

    /// <summary>
    /// Immutable source override. Resolution is a pure composition step and performs
    /// no generation, random sampling, claim, or application.
    /// </summary>
    public sealed class RewardSourceOverride : IEquatable<RewardSourceOverride>
    {
        private readonly ReadOnlyCollection<RewardGrantSpecification> appendedGuaranteedEntries;
        private readonly string canonicalText;
        private readonly string fingerprint;

        private RewardSourceOverride(
            StableId overrideStableId,
            StableId sourceInstanceStableId,
            RewardSourceOverrideMode mode,
            StableId resultProfileStableId,
            RewardProfile replacementProfile,
            IEnumerable<RewardGrantSpecification> appendedGuaranteedEntries)
        {
            this.OverrideStableId = RewardModelFormat.RequireStableId(
                overrideStableId,
                nameof(overrideStableId));
            this.SourceInstanceStableId = RewardModelFormat.RequireStableId(
                sourceInstanceStableId,
                nameof(sourceInstanceStableId));
            RewardModelFormat.RequireDefinedEnum(mode, nameof(mode));
            this.Mode = mode;
            this.ResultProfileStableId = resultProfileStableId;
            this.ReplacementProfile = replacementProfile;
            this.appendedGuaranteedEntries = RewardModelFormat.CopyAndSortUnique(
                appendedGuaranteedEntries,
                nameof(appendedGuaranteedEntries),
                delegate(RewardGrantSpecification item) { return item.GrantStableId; });
            this.ValidateShape();
            this.canonicalText = this.BuildCanonicalText();
            this.fingerprint = RewardModelFormat.Fingerprint(this.canonicalText);
        }

        public StableId OverrideStableId { get; }

        public StableId SourceInstanceStableId { get; }

        public RewardSourceOverrideMode Mode { get; }

        public StableId ResultProfileStableId { get; }

        public RewardProfile ReplacementProfile { get; }

        public IReadOnlyList<RewardGrantSpecification> AppendedGuaranteedEntries
        {
            get { return this.appendedGuaranteedEntries; }
        }

        public string Fingerprint
        {
            get { return this.fingerprint; }
        }

        public static RewardSourceOverride Inherit(
            StableId overrideStableId,
            StableId sourceInstanceStableId)
        {
            return new RewardSourceOverride(
                overrideStableId,
                sourceInstanceStableId,
                RewardSourceOverrideMode.InheritDefault,
                null,
                null,
                Array.Empty<RewardGrantSpecification>());
        }

        public static RewardSourceOverride NoReward(
            StableId overrideStableId,
            StableId sourceInstanceStableId,
            StableId resultProfileStableId)
        {
            return new RewardSourceOverride(
                overrideStableId,
                sourceInstanceStableId,
                RewardSourceOverrideMode.NoReward,
                resultProfileStableId,
                null,
                Array.Empty<RewardGrantSpecification>());
        }

        public static RewardSourceOverride ReplaceEntirely(
            StableId overrideStableId,
            StableId sourceInstanceStableId,
            RewardProfile replacementProfile)
        {
            return new RewardSourceOverride(
                overrideStableId,
                sourceInstanceStableId,
                RewardSourceOverrideMode.ReplaceEntirely,
                null,
                replacementProfile,
                Array.Empty<RewardGrantSpecification>());
        }

        public static RewardSourceOverride AppendGuaranteedEntries(
            StableId overrideStableId,
            StableId sourceInstanceStableId,
            StableId resultProfileStableId,
            IEnumerable<RewardGrantSpecification> appendedGuaranteedEntries)
        {
            return new RewardSourceOverride(
                overrideStableId,
                sourceInstanceStableId,
                RewardSourceOverrideMode.AppendGuaranteedEntries,
                resultProfileStableId,
                null,
                appendedGuaranteedEntries);
        }

        public RewardProfile Resolve(RewardProfile inheritedProfile)
        {
            if (inheritedProfile == null)
            {
                throw new ArgumentNullException(nameof(inheritedProfile));
            }

            switch (this.Mode)
            {
                case RewardSourceOverrideMode.InheritDefault:
                    return inheritedProfile;
                case RewardSourceOverrideMode.NoReward:
                    return RewardProfile.CreateExplicitNoDrop(this.ResultProfileStableId);
                case RewardSourceOverrideMode.ReplaceEntirely:
                    return this.ReplacementProfile;
                case RewardSourceOverrideMode.AppendGuaranteedEntries:
                    return inheritedProfile.AppendGuaranteed(
                        this.ResultProfileStableId,
                        this.appendedGuaranteedEntries);
                default:
                    throw new InvalidOperationException("Unsupported reward source override mode.");
            }
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(RewardSourceOverride other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as RewardSourceOverride);
        }

        public override int GetHashCode()
        {
            return RewardModelFormat.DeterministicHash(this.canonicalText);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }

        private void ValidateShape()
        {
            bool hasResultId = this.ResultProfileStableId != null;
            bool hasReplacement = this.ReplacementProfile != null;
            bool hasAppendedEntries = this.appendedGuaranteedEntries.Count > 0;

            switch (this.Mode)
            {
                case RewardSourceOverrideMode.InheritDefault:
                    if (hasResultId || hasReplacement || hasAppendedEntries)
                    {
                        throw new ArgumentException("Inherit overrides must not carry replacement data.");
                    }

                    break;
                case RewardSourceOverrideMode.NoReward:
                    if (!hasResultId || hasReplacement || hasAppendedEntries)
                    {
                        throw new ArgumentException(
                            "No-reward overrides require only a result profile StableId.");
                    }

                    break;
                case RewardSourceOverrideMode.ReplaceEntirely:
                    if (hasResultId || !hasReplacement || hasAppendedEntries)
                    {
                        throw new ArgumentException(
                            "Replace-entirely overrides require only a replacement profile.");
                    }

                    break;
                case RewardSourceOverrideMode.AppendGuaranteedEntries:
                    if (!hasResultId || hasReplacement || !hasAppendedEntries)
                    {
                        throw new ArgumentException(
                            "Append-guaranteed overrides require a result profile StableId and at least one grant.");
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Mode));
            }
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("override_stable_id=")
                .Append(this.OverrideStableId)
                .Append("\nsource_instance_stable_id=")
                .Append(this.SourceInstanceStableId)
                .Append("\nmode=")
                .Append(((int)this.Mode).ToString(CultureInfo.InvariantCulture))
                .Append("\nresult_profile_stable_id=")
                .Append(this.ResultProfileStableId == null ? "null" : this.ResultProfileStableId.ToString())
                .Append("\nreplacement_profile:\n")
                .Append(this.ReplacementProfile == null
                    ? "null"
                    : this.ReplacementProfile.ToCanonicalString())
                .Append("\nappended_guaranteed_count=")
                .Append(this.appendedGuaranteedEntries.Count.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < this.appendedGuaranteedEntries.Count; index++)
            {
                builder.Append("\nappended_guaranteed_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append(":\n")
                    .Append(this.appendedGuaranteedEntries[index].ToCanonicalString());
            }

            return builder.ToString();
        }
    }
}
