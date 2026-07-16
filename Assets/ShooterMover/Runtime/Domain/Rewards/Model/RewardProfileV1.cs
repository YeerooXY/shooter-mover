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
    public sealed class IndependentRewardRollV1 :
        IEquatable<IndependentRewardRollV1>,
        IComparable<IndependentRewardRollV1>,
        IComparable
    {
        public const int ProbabilityScale = 1000000;

        private readonly string canonicalText;

        private IndependentRewardRollV1(
            StableId rollStableId,
            int probabilityMillionths,
            RewardGrantSpecificationV1 grant)
        {
            this.RollStableId = RewardModelFormatV1.RequireStableId(
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

        public RewardGrantSpecificationV1 Grant { get; }

        public static IndependentRewardRollV1 Create(
            StableId rollStableId,
            int probabilityMillionths,
            RewardGrantSpecificationV1 grant)
        {
            return new IndependentRewardRollV1(rollStableId, probabilityMillionths, grant);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(IndependentRewardRollV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as IndependentRewardRollV1);
        }

        public override int GetHashCode()
        {
            return RewardModelFormatV1.DeterministicHash(this.canonicalText);
        }

        public int CompareTo(IndependentRewardRollV1 other)
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

            IndependentRewardRollV1 other = obj as IndependentRewardRollV1;
            if (other == null)
            {
                throw new ArgumentException(
                    "Object must be an IndependentRewardRollV1.",
                    nameof(obj));
            }

            return this.CompareTo(other);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }
    }

    public enum WeightedRewardOutcomeKindV1
    {
        Grant = 1,
        ExplicitNoDrop = 2,
    }

    /// <summary>
    /// One positive-weight exclusive outcome. Explicit no-drop is a first-class
    /// outcome rather than an accidental missing grant.
    /// </summary>
    public sealed class WeightedRewardOutcomeV1 :
        IEquatable<WeightedRewardOutcomeV1>,
        IComparable<WeightedRewardOutcomeV1>,
        IComparable
    {
        private readonly string canonicalText;

        private WeightedRewardOutcomeV1(
            StableId outcomeStableId,
            long weight,
            WeightedRewardOutcomeKindV1 kind,
            RewardGrantSpecificationV1 grant)
        {
            this.OutcomeStableId = RewardModelFormatV1.RequireStableId(
                outcomeStableId,
                nameof(outcomeStableId));
            if (weight < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(weight),
                    weight,
                    "Exclusive reward weights must be positive.");
            }

            RewardModelFormatV1.RequireDefinedEnum(kind, nameof(kind));
            if (kind == WeightedRewardOutcomeKindV1.Grant && grant == null)
            {
                throw new ArgumentNullException(nameof(grant));
            }

            if (kind == WeightedRewardOutcomeKindV1.ExplicitNoDrop && grant != null)
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

        public WeightedRewardOutcomeKindV1 Kind { get; }

        public RewardGrantSpecificationV1 Grant { get; }

        public static WeightedRewardOutcomeV1 CreateGrant(
            StableId outcomeStableId,
            long weight,
            RewardGrantSpecificationV1 grant)
        {
            return new WeightedRewardOutcomeV1(
                outcomeStableId,
                weight,
                WeightedRewardOutcomeKindV1.Grant,
                grant);
        }

        public static WeightedRewardOutcomeV1 CreateExplicitNoDrop(
            StableId outcomeStableId,
            long weight)
        {
            return new WeightedRewardOutcomeV1(
                outcomeStableId,
                weight,
                WeightedRewardOutcomeKindV1.ExplicitNoDrop,
                null);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(WeightedRewardOutcomeV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as WeightedRewardOutcomeV1);
        }

        public override int GetHashCode()
        {
            return RewardModelFormatV1.DeterministicHash(this.canonicalText);
        }

        public int CompareTo(WeightedRewardOutcomeV1 other)
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

            WeightedRewardOutcomeV1 other = obj as WeightedRewardOutcomeV1;
            if (other == null)
            {
                throw new ArgumentException(
                    "Object must be a WeightedRewardOutcomeV1.",
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
    public sealed class ExclusiveRewardGroupV1 :
        IEquatable<ExclusiveRewardGroupV1>,
        IComparable<ExclusiveRewardGroupV1>,
        IComparable
    {
        private readonly ReadOnlyCollection<WeightedRewardOutcomeV1> outcomes;
        private readonly string canonicalText;

        private ExclusiveRewardGroupV1(
            StableId groupStableId,
            IEnumerable<WeightedRewardOutcomeV1> outcomes)
        {
            this.GroupStableId = RewardModelFormatV1.RequireStableId(
                groupStableId,
                nameof(groupStableId));
            this.outcomes = RewardModelFormatV1.CopyAndSortUnique(
                outcomes,
                nameof(outcomes),
                delegate(WeightedRewardOutcomeV1 item) { return item.OutcomeStableId; });
            if (this.outcomes.Count == 0)
            {
                throw new ArgumentException(
                    "Exclusive reward groups must contain at least one weighted outcome.",
                    nameof(outcomes));
            }

            this.canonicalText = this.BuildCanonicalText();
        }

        public StableId GroupStableId { get; }

        public IReadOnlyList<WeightedRewardOutcomeV1> Outcomes
        {
            get { return this.outcomes; }
        }

        public static ExclusiveRewardGroupV1 Create(
            StableId groupStableId,
            IEnumerable<WeightedRewardOutcomeV1> outcomes)
        {
            return new ExclusiveRewardGroupV1(groupStableId, outcomes);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(ExclusiveRewardGroupV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ExclusiveRewardGroupV1);
        }

        public override int GetHashCode()
        {
            return RewardModelFormatV1.DeterministicHash(this.canonicalText);
        }

        public int CompareTo(ExclusiveRewardGroupV1 other)
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

            ExclusiveRewardGroupV1 other = obj as ExclusiveRewardGroupV1;
            if (other == null)
            {
                throw new ArgumentException(
                    "Object must be an ExclusiveRewardGroupV1.",
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

    public enum RewardProfileDispositionV1
    {
        Configured = 1,
        ExplicitNoDrop = 2,
    }

    /// <summary>
    /// Immutable reward profile containing coexisting guaranteed, independent, and
    /// exclusive reward specifications. Collection input order is not significant.
    /// </summary>
    public sealed class RewardProfileV1 : IEquatable<RewardProfileV1>
    {
        private readonly ReadOnlyCollection<RewardGrantSpecificationV1> guaranteedEntries;
        private readonly ReadOnlyCollection<IndependentRewardRollV1> independentRolls;
        private readonly ReadOnlyCollection<ExclusiveRewardGroupV1> exclusiveGroups;
        private readonly string canonicalText;
        private readonly string fingerprint;

        private RewardProfileV1(
            StableId profileStableId,
            RewardProfileDispositionV1 disposition,
            IEnumerable<RewardGrantSpecificationV1> guaranteedEntries,
            IEnumerable<IndependentRewardRollV1> independentRolls,
            IEnumerable<ExclusiveRewardGroupV1> exclusiveGroups)
        {
            this.ProfileStableId = RewardModelFormatV1.RequireStableId(
                profileStableId,
                nameof(profileStableId));
            RewardModelFormatV1.RequireDefinedEnum(disposition, nameof(disposition));
            this.Disposition = disposition;
            this.guaranteedEntries = RewardModelFormatV1.CopyAndSortUnique(
                guaranteedEntries,
                nameof(guaranteedEntries),
                delegate(RewardGrantSpecificationV1 item) { return item.GrantStableId; });
            this.independentRolls = RewardModelFormatV1.CopyAndSortUnique(
                independentRolls,
                nameof(independentRolls),
                delegate(IndependentRewardRollV1 item) { return item.RollStableId; });
            this.exclusiveGroups = RewardModelFormatV1.CopyAndSortUnique(
                exclusiveGroups,
                nameof(exclusiveGroups),
                delegate(ExclusiveRewardGroupV1 item) { return item.GroupStableId; });

            this.ValidateDisposition();
            this.ValidateUniqueGrantIdentities();
            this.canonicalText = this.BuildCanonicalText();
            this.fingerprint = RewardModelFormatV1.Fingerprint(this.canonicalText);
        }

        public StableId ProfileStableId { get; }

        public RewardProfileDispositionV1 Disposition { get; }

        public IReadOnlyList<RewardGrantSpecificationV1> GuaranteedEntries
        {
            get { return this.guaranteedEntries; }
        }

        public IReadOnlyList<IndependentRewardRollV1> IndependentRolls
        {
            get { return this.independentRolls; }
        }

        public IReadOnlyList<ExclusiveRewardGroupV1> ExclusiveGroups
        {
            get { return this.exclusiveGroups; }
        }

        public string Fingerprint
        {
            get { return this.fingerprint; }
        }

        public static RewardProfileV1 Create(
            StableId profileStableId,
            IEnumerable<RewardGrantSpecificationV1> guaranteedEntries,
            IEnumerable<IndependentRewardRollV1> independentRolls,
            IEnumerable<ExclusiveRewardGroupV1> exclusiveGroups)
        {
            return new RewardProfileV1(
                profileStableId,
                RewardProfileDispositionV1.Configured,
                guaranteedEntries,
                independentRolls,
                exclusiveGroups);
        }

        public static RewardProfileV1 CreateExplicitNoDrop(StableId profileStableId)
        {
            return new RewardProfileV1(
                profileStableId,
                RewardProfileDispositionV1.ExplicitNoDrop,
                Array.Empty<RewardGrantSpecificationV1>(),
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
        }

        public RewardProfileV1 AppendGuaranteed(
            StableId resultProfileStableId,
            IEnumerable<RewardGrantSpecificationV1> additionalEntries)
        {
            if (additionalEntries == null)
            {
                throw new ArgumentNullException(nameof(additionalEntries));
            }

            List<RewardGrantSpecificationV1> combined =
                new List<RewardGrantSpecificationV1>(this.guaranteedEntries);
            foreach (RewardGrantSpecificationV1 entry in additionalEntries)
            {
                combined.Add(entry);
            }

            return new RewardProfileV1(
                resultProfileStableId,
                RewardProfileDispositionV1.Configured,
                combined,
                this.independentRolls,
                this.exclusiveGroups);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(RewardProfileV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as RewardProfileV1);
        }

        public override int GetHashCode()
        {
            return RewardModelFormatV1.DeterministicHash(this.canonicalText);
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
            if (this.Disposition == RewardProfileDispositionV1.ExplicitNoDrop
                && configuredEntryCount != 0)
            {
                throw new ArgumentException(
                    "Explicit no-drop profiles must not contain reward entries.");
            }

            if (this.Disposition == RewardProfileDispositionV1.Configured
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
                IReadOnlyList<WeightedRewardOutcomeV1> outcomes =
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

    public enum RewardSourceOverrideModeV1
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
    public sealed class RewardSourceOverrideV1 : IEquatable<RewardSourceOverrideV1>
    {
        private readonly ReadOnlyCollection<RewardGrantSpecificationV1> appendedGuaranteedEntries;
        private readonly string canonicalText;
        private readonly string fingerprint;

        private RewardSourceOverrideV1(
            StableId overrideStableId,
            StableId sourceInstanceStableId,
            RewardSourceOverrideModeV1 mode,
            StableId resultProfileStableId,
            RewardProfileV1 replacementProfile,
            IEnumerable<RewardGrantSpecificationV1> appendedGuaranteedEntries)
        {
            this.OverrideStableId = RewardModelFormatV1.RequireStableId(
                overrideStableId,
                nameof(overrideStableId));
            this.SourceInstanceStableId = RewardModelFormatV1.RequireStableId(
                sourceInstanceStableId,
                nameof(sourceInstanceStableId));
            RewardModelFormatV1.RequireDefinedEnum(mode, nameof(mode));
            this.Mode = mode;
            this.ResultProfileStableId = resultProfileStableId;
            this.ReplacementProfile = replacementProfile;
            this.appendedGuaranteedEntries = RewardModelFormatV1.CopyAndSortUnique(
                appendedGuaranteedEntries,
                nameof(appendedGuaranteedEntries),
                delegate(RewardGrantSpecificationV1 item) { return item.GrantStableId; });
            this.ValidateShape();
            this.canonicalText = this.BuildCanonicalText();
            this.fingerprint = RewardModelFormatV1.Fingerprint(this.canonicalText);
        }

        public StableId OverrideStableId { get; }

        public StableId SourceInstanceStableId { get; }

        public RewardSourceOverrideModeV1 Mode { get; }

        public StableId ResultProfileStableId { get; }

        public RewardProfileV1 ReplacementProfile { get; }

        public IReadOnlyList<RewardGrantSpecificationV1> AppendedGuaranteedEntries
        {
            get { return this.appendedGuaranteedEntries; }
        }

        public string Fingerprint
        {
            get { return this.fingerprint; }
        }

        public static RewardSourceOverrideV1 Inherit(
            StableId overrideStableId,
            StableId sourceInstanceStableId)
        {
            return new RewardSourceOverrideV1(
                overrideStableId,
                sourceInstanceStableId,
                RewardSourceOverrideModeV1.InheritDefault,
                null,
                null,
                Array.Empty<RewardGrantSpecificationV1>());
        }

        public static RewardSourceOverrideV1 NoReward(
            StableId overrideStableId,
            StableId sourceInstanceStableId,
            StableId resultProfileStableId)
        {
            return new RewardSourceOverrideV1(
                overrideStableId,
                sourceInstanceStableId,
                RewardSourceOverrideModeV1.NoReward,
                resultProfileStableId,
                null,
                Array.Empty<RewardGrantSpecificationV1>());
        }

        public static RewardSourceOverrideV1 ReplaceEntirely(
            StableId overrideStableId,
            StableId sourceInstanceStableId,
            RewardProfileV1 replacementProfile)
        {
            return new RewardSourceOverrideV1(
                overrideStableId,
                sourceInstanceStableId,
                RewardSourceOverrideModeV1.ReplaceEntirely,
                null,
                replacementProfile,
                Array.Empty<RewardGrantSpecificationV1>());
        }

        public static RewardSourceOverrideV1 AppendGuaranteedEntries(
            StableId overrideStableId,
            StableId sourceInstanceStableId,
            StableId resultProfileStableId,
            IEnumerable<RewardGrantSpecificationV1> appendedGuaranteedEntries)
        {
            return new RewardSourceOverrideV1(
                overrideStableId,
                sourceInstanceStableId,
                RewardSourceOverrideModeV1.AppendGuaranteedEntries,
                resultProfileStableId,
                null,
                appendedGuaranteedEntries);
        }

        public RewardProfileV1 Resolve(RewardProfileV1 inheritedProfile)
        {
            if (inheritedProfile == null)
            {
                throw new ArgumentNullException(nameof(inheritedProfile));
            }

            switch (this.Mode)
            {
                case RewardSourceOverrideModeV1.InheritDefault:
                    return inheritedProfile;
                case RewardSourceOverrideModeV1.NoReward:
                    return RewardProfileV1.CreateExplicitNoDrop(this.ResultProfileStableId);
                case RewardSourceOverrideModeV1.ReplaceEntirely:
                    return this.ReplacementProfile;
                case RewardSourceOverrideModeV1.AppendGuaranteedEntries:
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

        public bool Equals(RewardSourceOverrideV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as RewardSourceOverrideV1);
        }

        public override int GetHashCode()
        {
            return RewardModelFormatV1.DeterministicHash(this.canonicalText);
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
                case RewardSourceOverrideModeV1.InheritDefault:
                    if (hasResultId || hasReplacement || hasAppendedEntries)
                    {
                        throw new ArgumentException("Inherit overrides must not carry replacement data.");
                    }

                    break;
                case RewardSourceOverrideModeV1.NoReward:
                    if (!hasResultId || hasReplacement || hasAppendedEntries)
                    {
                        throw new ArgumentException(
                            "No-reward overrides require only a result profile StableId.");
                    }

                    break;
                case RewardSourceOverrideModeV1.ReplaceEntirely:
                    if (hasResultId || !hasReplacement || hasAppendedEntries)
                    {
                        throw new ArgumentException(
                            "Replace-entirely overrides require only a replacement profile.");
                    }

                    break;
                case RewardSourceOverrideModeV1.AppendGuaranteedEntries:
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
