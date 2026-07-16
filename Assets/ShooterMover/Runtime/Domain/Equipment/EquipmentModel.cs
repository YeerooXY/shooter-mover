using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Equipment
{
    public static class EquipmentCategoryIds
    {
        public static readonly StableId Weapon = StableId.Parse("equipment-category.weapon");
        public static readonly StableId Armor = StableId.Parse("equipment-category.armor");
    }

    public enum AugmentDuplicatePolicy
    {
        DisallowSameDefinition = 1,
        AllowSameDefinition = 2,
    }

    public enum EquipmentModelIssueCode
    {
        NullEquipmentDefinition = 1,
        NullAugmentDefinition = 2,
        DuplicateEquipmentDefinitionId = 3,
        DuplicateAugmentDefinitionId = 4,
        DefinitionIdCollision = 5,
        MissingDefinitionId = 6,
        MissingCategoryId = 7,
        MissingFamilyId = 8,
        InvalidDisplayName = 9,
        InvalidItemLevelRange = 10,
        InvalidQualityTier = 11,
        DuplicateQualityId = 12,
        DuplicateQualityRank = 13,
        InvalidAugmentSlotMaximum = 14,
        InvalidRuntimeReference = 15,
        DuplicateEquipmentTag = 16,
        InvalidAugmentRange = 17,
        InvalidDuplicatePolicy = 18,
        InvalidCompatibility = 19,
        DuplicateCompatibilityValue = 20,
        DuplicateExclusionGroup = 21,
        ImpossibleAugmentCompatibility = 22,
        NullEquipmentInstance = 23,
        MissingEquipmentInstanceId = 24,
        UnknownEquipmentDefinition = 25,
        ItemLevelOutOfRange = 26,
        UnknownQuality = 27,
        AugmentSlotCapacityExceeded = 28,
        NullAugmentInstance = 29,
        MissingAugmentInstanceId = 30,
        DuplicateAugmentInstanceId = 31,
        UnknownAugmentDefinition = 32,
        AugmentTierOutOfRange = 33,
        AugmentLevelOutOfRange = 34,
        IncompatibleAugmentCategory = 35,
        IncompatibleAugmentFamily = 36,
        MissingRequiredEquipmentTag = 37,
        ExcludedEquipmentTag = 38,
        DuplicateAugmentNotAllowed = 39,
        ExclusionGroupConflict = 40,
    }

    public sealed class EquipmentModelIssue : IEquatable<EquipmentModelIssue>, IComparable<EquipmentModelIssue>
    {
        public EquipmentModelIssue(EquipmentModelIssueCode code, StableId subjectId, string detail)
        {
            Code = code;
            SubjectId = subjectId;
            Detail = detail ?? string.Empty;
        }

        public EquipmentModelIssueCode Code { get; }
        public StableId SubjectId { get; }
        public string Detail { get; }

        public int CompareTo(EquipmentModelIssue other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int codeComparison = Code.CompareTo(other.Code);
            if (codeComparison != 0)
            {
                return codeComparison;
            }

            int subjectComparison = CompareStableIds(SubjectId, other.SubjectId);
            return subjectComparison != 0
                ? subjectComparison
                : string.CompareOrdinal(Detail, other.Detail);
        }

        public bool Equals(EquipmentModelIssue other)
        {
            return !ReferenceEquals(other, null)
                && Code == other.Code
                && Equals(SubjectId, other.SubjectId)
                && string.Equals(Detail, other.Detail, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EquipmentModelIssue);
        }

        public override int GetHashCode()
        {
            return EquipmentFingerprint.OrdinalHash(ToString());
        }

        public override string ToString()
        {
            return ((int)Code).ToString(CultureInfo.InvariantCulture)
                + "|"
                + (SubjectId == null ? "null" : SubjectId.ToString())
                + "|"
                + Detail;
        }

        private static int CompareStableIds(StableId left, StableId right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (ReferenceEquals(left, null))
            {
                return -1;
            }

            if (ReferenceEquals(right, null))
            {
                return 1;
            }

            return left.CompareTo(right);
        }
    }

    public sealed class InclusiveIntRange : IEquatable<InclusiveIntRange>
    {
        private InclusiveIntRange(int minimum, int maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        public int Minimum { get; }
        public int Maximum { get; }
        public bool IsOrderedPositive { get { return Minimum >= 1 && Maximum >= Minimum; } }

        public static InclusiveIntRange Create(int minimum, int maximum)
        {
            return new InclusiveIntRange(minimum, maximum);
        }

        public bool Contains(int value)
        {
            return value >= Minimum && value <= Maximum;
        }

        public string ToCanonicalString()
        {
            return Minimum.ToString(CultureInfo.InvariantCulture)
                + ".."
                + Maximum.ToString(CultureInfo.InvariantCulture);
        }

        public bool Equals(InclusiveIntRange other)
        {
            return !ReferenceEquals(other, null)
                && Minimum == other.Minimum
                && Maximum == other.Maximum;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as InclusiveIntRange);
        }

        public override int GetHashCode()
        {
            return EquipmentFingerprint.OrdinalHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    public sealed class EquipmentQualityTier : IEquatable<EquipmentQualityTier>, IComparable<EquipmentQualityTier>
    {
        private EquipmentQualityTier(StableId qualityId, string label, int rank)
        {
            QualityId = qualityId;
            Label = label;
            Rank = rank;
        }

        public StableId QualityId { get; }
        public string Label { get; }
        public int Rank { get; }

        public static EquipmentQualityTier Create(StableId qualityId, string label, int rank)
        {
            return new EquipmentQualityTier(qualityId, label, rank);
        }

        public int CompareTo(EquipmentQualityTier other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int rankComparison = Rank.CompareTo(other.Rank);
            if (rankComparison != 0)
            {
                return rankComparison;
            }

            return CompareStableIds(QualityId, other.QualityId);
        }

        public bool Equals(EquipmentQualityTier other)
        {
            return !ReferenceEquals(other, null)
                && Equals(QualityId, other.QualityId)
                && string.Equals(Label, other.Label, StringComparison.Ordinal)
                && Rank == other.Rank;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EquipmentQualityTier);
        }

        public override int GetHashCode()
        {
            return EquipmentFingerprint.OrdinalHash(ToCanonicalString());
        }

        public string ToCanonicalString()
        {
            return (QualityId == null ? "null" : QualityId.ToString())
                + "|"
                + Rank.ToString(CultureInfo.InvariantCulture)
                + "|"
                + (Label ?? "null");
        }

        private static int CompareStableIds(StableId left, StableId right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (ReferenceEquals(left, null))
            {
                return -1;
            }

            return ReferenceEquals(right, null) ? 1 : left.CompareTo(right);
        }
    }

    public sealed class EquipmentDefinition : IEquatable<EquipmentDefinition>, IComparable<EquipmentDefinition>
    {
        private readonly ReadOnlyCollection<EquipmentQualityTier> qualityTiers;
        private readonly ReadOnlyCollection<StableId> tags;
        private readonly string canonicalText;

        private EquipmentDefinition(
            StableId definitionId,
            StableId categoryId,
            StableId familyId,
            string displayName,
            StableId runtimeWeaponReferenceId,
            InclusiveIntRange itemLevelRange,
            int maximumAugmentSlots,
            IEnumerable<EquipmentQualityTier> qualityTiers,
            IEnumerable<StableId> tags)
        {
            DefinitionId = definitionId;
            CategoryId = categoryId;
            FamilyId = familyId;
            DisplayName = displayName;
            RuntimeWeaponReferenceId = runtimeWeaponReferenceId;
            ItemLevelRange = itemLevelRange;
            MaximumAugmentSlots = maximumAugmentSlots;
            this.qualityTiers = CopyAndSort(qualityTiers, CompareQualityTiers);
            this.tags = CopyAndSort(tags, CompareStableIds);
            canonicalText = BuildCanonicalText();
        }

        public StableId DefinitionId { get; }
        public StableId CategoryId { get; }
        public StableId FamilyId { get; }
        public string DisplayName { get; }
        public StableId RuntimeWeaponReferenceId { get; }
        public InclusiveIntRange ItemLevelRange { get; }
        public int MaximumAugmentSlots { get; }
        public IReadOnlyList<EquipmentQualityTier> QualityTiers { get { return qualityTiers; } }
        public IReadOnlyList<StableId> Tags { get { return tags; } }

        public static EquipmentDefinition Create(
            StableId definitionId,
            StableId categoryId,
            StableId familyId,
            string displayName,
            StableId runtimeWeaponReferenceId,
            InclusiveIntRange itemLevelRange,
            int maximumAugmentSlots,
            IEnumerable<EquipmentQualityTier> qualityTiers,
            IEnumerable<StableId> tags)
        {
            return new EquipmentDefinition(
                definitionId,
                categoryId,
                familyId,
                displayName,
                runtimeWeaponReferenceId,
                itemLevelRange,
                maximumAugmentSlots,
                qualityTiers,
                tags);
        }

        public bool HasTag(StableId tagId)
        {
            return tagId != null && tags != null && tags.BinarySearch(tagId, StableIdComparer.Instance) >= 0;
        }

        public bool SupportsQuality(StableId qualityId)
        {
            if (qualityId == null || qualityTiers == null)
            {
                return false;
            }

            for (int index = 0; index < qualityTiers.Count; index++)
            {
                EquipmentQualityTier tier = qualityTiers[index];
                if (tier != null && Equals(tier.QualityId, qualityId))
                {
                    return true;
                }
            }

            return false;
        }

        public int CompareTo(EquipmentDefinition other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            return CompareStableIds(DefinitionId, other.DefinitionId);
        }

        public bool Equals(EquipmentDefinition other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EquipmentDefinition);
        }

        public override int GetHashCode()
        {
            return EquipmentFingerprint.OrdinalHash(canonicalText);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            Append(builder, "definition_id", DefinitionId);
            Append(builder, "category_id", CategoryId);
            Append(builder, "family_id", FamilyId);
            builder.Append("display_name=").Append(DisplayName ?? "null").Append('\n');
            Append(builder, "runtime_weapon_reference_id", RuntimeWeaponReferenceId);
            builder.Append("item_level_range=")
                .Append(ItemLevelRange == null ? "null" : ItemLevelRange.ToCanonicalString())
                .Append('\n');
            builder.Append("maximum_augment_slots=")
                .Append(MaximumAugmentSlots.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            AppendList(builder, "quality", qualityTiers, delegate(EquipmentQualityTier value)
            {
                return value == null ? "null" : value.ToCanonicalString();
            });
            AppendList(builder, "tag", tags, delegate(StableId value)
            {
                return value == null ? "null" : value.ToString();
            });
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string name, StableId value)
        {
            builder.Append(name).Append('=').Append(value == null ? "null" : value.ToString()).Append('\n');
        }

        internal static ReadOnlyCollection<T> CopyAndSort<T>(IEnumerable<T> values, Comparison<T> comparison)
        {
            if (values == null)
            {
                return null;
            }

            List<T> copy = new List<T>(values);
            copy.Sort(comparison);
            return new ReadOnlyCollection<T>(copy);
        }

        internal static void AppendList<T>(StringBuilder builder, string prefix, IReadOnlyList<T> values, Func<T, string> formatter)
        {
            if (values == null)
            {
                builder.Append(prefix).Append("_count=null\n");
                return;
            }

            builder.Append(prefix).Append("_count=")
                .Append(values.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            for (int index = 0; index < values.Count; index++)
            {
                builder.Append(prefix).Append('_').Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append('=').Append(formatter(values[index])).Append('\n');
            }
        }

        internal static int CompareStableIds(StableId left, StableId right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (ReferenceEquals(left, null))
            {
                return -1;
            }

            return ReferenceEquals(right, null) ? 1 : left.CompareTo(right);
        }

        private static int CompareQualityTiers(EquipmentQualityTier left, EquipmentQualityTier right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (ReferenceEquals(left, null))
            {
                return -1;
            }

            return ReferenceEquals(right, null) ? 1 : left.CompareTo(right);
        }

        private sealed class StableIdComparer : IComparer<StableId>
        {
            public static readonly StableIdComparer Instance = new StableIdComparer();
            public int Compare(StableId x, StableId y) { return CompareStableIds(x, y); }
        }
    }

    public sealed class AugmentCompatibility : IEquatable<AugmentCompatibility>
    {
        private readonly ReadOnlyCollection<StableId> categoryIds;
        private readonly ReadOnlyCollection<StableId> familyIds;
        private readonly ReadOnlyCollection<StableId> requiredTags;
        private readonly ReadOnlyCollection<StableId> excludedTags;
        private readonly string canonicalText;

        private AugmentCompatibility(
            IEnumerable<StableId> categoryIds,
            IEnumerable<StableId> familyIds,
            IEnumerable<StableId> requiredTags,
            IEnumerable<StableId> excludedTags)
        {
            this.categoryIds = EquipmentDefinition.CopyAndSort(categoryIds, EquipmentDefinition.CompareStableIds);
            this.familyIds = EquipmentDefinition.CopyAndSort(familyIds, EquipmentDefinition.CompareStableIds);
            this.requiredTags = EquipmentDefinition.CopyAndSort(requiredTags, EquipmentDefinition.CompareStableIds);
            this.excludedTags = EquipmentDefinition.CopyAndSort(excludedTags, EquipmentDefinition.CompareStableIds);
            canonicalText = BuildCanonicalText();
        }

        public IReadOnlyList<StableId> CategoryIds { get { return categoryIds; } }
        public IReadOnlyList<StableId> FamilyIds { get { return familyIds; } }
        public IReadOnlyList<StableId> RequiredTags { get { return requiredTags; } }
        public IReadOnlyList<StableId> ExcludedTags { get { return excludedTags; } }

        public static AugmentCompatibility Create(
            IEnumerable<StableId> categoryIds,
            IEnumerable<StableId> familyIds,
            IEnumerable<StableId> requiredTags,
            IEnumerable<StableId> excludedTags)
        {
            return new AugmentCompatibility(categoryIds, familyIds, requiredTags, excludedTags);
        }

        public bool Allows(EquipmentDefinition equipment)
        {
            if (equipment == null)
            {
                return false;
            }

            if (categoryIds != null && categoryIds.Count > 0 && !Contains(categoryIds, equipment.CategoryId))
            {
                return false;
            }

            if (familyIds != null && familyIds.Count > 0 && !Contains(familyIds, equipment.FamilyId))
            {
                return false;
            }

            if (requiredTags != null)
            {
                for (int index = 0; index < requiredTags.Count; index++)
                {
                    if (!equipment.HasTag(requiredTags[index]))
                    {
                        return false;
                    }
                }
            }

            if (excludedTags != null)
            {
                for (int index = 0; index < excludedTags.Count; index++)
                {
                    if (equipment.HasTag(excludedTags[index]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool Equals(AugmentCompatibility other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AugmentCompatibility);
        }

        public override int GetHashCode()
        {
            return EquipmentFingerprint.OrdinalHash(canonicalText);
        }

        public string ToCanonicalString() { return canonicalText; }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            EquipmentDefinition.AppendList(builder, "category", categoryIds, FormatStableId);
            EquipmentDefinition.AppendList(builder, "family", familyIds, FormatStableId);
            EquipmentDefinition.AppendList(builder, "required_tag", requiredTags, FormatStableId);
            EquipmentDefinition.AppendList(builder, "excluded_tag", excludedTags, FormatStableId);
            return builder.ToString();
        }

        private static string FormatStableId(StableId value)
        {
            return value == null ? "null" : value.ToString();
        }

        private static bool Contains(IReadOnlyList<StableId> values, StableId candidate)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (Equals(values[index], candidate))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class AugmentDefinition : IEquatable<AugmentDefinition>, IComparable<AugmentDefinition>
    {
        private readonly ReadOnlyCollection<StableId> exclusionGroupIds;
        private readonly string canonicalText;

        private AugmentDefinition(
            StableId definitionId,
            StableId familyId,
            string displayName,
            AugmentCompatibility compatibility,
            IEnumerable<StableId> exclusionGroupIds,
            AugmentDuplicatePolicy duplicatePolicy,
            InclusiveIntRange tierRange,
            InclusiveIntRange levelRange)
        {
            DefinitionId = definitionId;
            FamilyId = familyId;
            DisplayName = displayName;
            Compatibility = compatibility;
            this.exclusionGroupIds = EquipmentDefinition.CopyAndSort(exclusionGroupIds, EquipmentDefinition.CompareStableIds);
            DuplicatePolicy = duplicatePolicy;
            TierRange = tierRange;
            LevelRange = levelRange;
            canonicalText = BuildCanonicalText();
        }

        public StableId DefinitionId { get; }
        public StableId FamilyId { get; }
        public string DisplayName { get; }
        public AugmentCompatibility Compatibility { get; }
        public IReadOnlyList<StableId> ExclusionGroupIds { get { return exclusionGroupIds; } }
        public AugmentDuplicatePolicy DuplicatePolicy { get; }
        public InclusiveIntRange TierRange { get; }
        public InclusiveIntRange LevelRange { get; }

        public static AugmentDefinition Create(
            StableId definitionId,
            StableId familyId,
            string displayName,
            AugmentCompatibility compatibility,
            IEnumerable<StableId> exclusionGroupIds,
            AugmentDuplicatePolicy duplicatePolicy,
            InclusiveIntRange tierRange,
            InclusiveIntRange levelRange)
        {
            return new AugmentDefinition(
                definitionId,
                familyId,
                displayName,
                compatibility,
                exclusionGroupIds,
                duplicatePolicy,
                tierRange,
                levelRange);
        }

        public bool SharesExclusionGroup(AugmentDefinition other)
        {
            if (other == null || exclusionGroupIds == null || other.exclusionGroupIds == null)
            {
                return false;
            }

            int left = 0;
            int right = 0;
            while (left < exclusionGroupIds.Count && right < other.exclusionGroupIds.Count)
            {
                int comparison = EquipmentDefinition.CompareStableIds(exclusionGroupIds[left], other.exclusionGroupIds[right]);
                if (comparison == 0)
                {
                    return true;
                }

                if (comparison < 0) { left++; } else { right++; }
            }

            return false;
        }

        public int CompareTo(AugmentDefinition other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : EquipmentDefinition.CompareStableIds(DefinitionId, other.DefinitionId);
        }

        public bool Equals(AugmentDefinition other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return Equals(obj as AugmentDefinition); }
        public override int GetHashCode() { return EquipmentFingerprint.OrdinalHash(canonicalText); }
        public string ToCanonicalString() { return canonicalText; }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("definition_id=").Append(DefinitionId == null ? "null" : DefinitionId.ToString()).Append('\n');
            builder.Append("family_id=").Append(FamilyId == null ? "null" : FamilyId.ToString()).Append('\n');
            builder.Append("display_name=").Append(DisplayName ?? "null").Append('\n');
            builder.Append("duplicate_policy=").Append(((int)DuplicatePolicy).ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("tier_range=").Append(TierRange == null ? "null" : TierRange.ToCanonicalString()).Append('\n');
            builder.Append("level_range=").Append(LevelRange == null ? "null" : LevelRange.ToCanonicalString()).Append('\n');
            builder.Append("compatibility:\n").Append(Compatibility == null ? "null\n" : Compatibility.ToCanonicalString());
            EquipmentDefinition.AppendList(builder, "exclusion_group", exclusionGroupIds, delegate(StableId value)
            {
                return value == null ? "null" : value.ToString();
            });
            return builder.ToString();
        }
    }

    public sealed class AugmentInstance : IEquatable<AugmentInstance>, IComparable<AugmentInstance>
    {
        private readonly string canonicalText;

        private AugmentInstance(StableId instanceId, StableId definitionId, int tier, int level)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Tier = tier;
            Level = level;
            canonicalText = BuildCanonicalText();
        }

        public StableId InstanceId { get; }
        public StableId DefinitionId { get; }
        public int Tier { get; }
        public int Level { get; }

        public static AugmentInstance Create(StableId instanceId, StableId definitionId, int tier, int level)
        {
            return new AugmentInstance(instanceId, definitionId, tier, level);
        }

        public AugmentInstance WithLevel(int level)
        {
            return new AugmentInstance(InstanceId, DefinitionId, Tier, level);
        }

        public int CompareTo(AugmentInstance other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int instanceComparison = EquipmentDefinition.CompareStableIds(InstanceId, other.InstanceId);
            return instanceComparison != 0
                ? instanceComparison
                : string.CompareOrdinal(canonicalText, other.canonicalText);
        }

        public bool Equals(AugmentInstance other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return Equals(obj as AugmentInstance); }
        public override int GetHashCode() { return EquipmentFingerprint.OrdinalHash(canonicalText); }
        public string ToCanonicalString() { return canonicalText; }

        private string BuildCanonicalText()
        {
            return "instance_id=" + (InstanceId == null ? "null" : InstanceId.ToString())
                + "\ndefinition_id=" + (DefinitionId == null ? "null" : DefinitionId.ToString())
                + "\ntier=" + Tier.ToString(CultureInfo.InvariantCulture)
                + "\nlevel=" + Level.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class EquipmentInstance : IEquatable<EquipmentInstance>
    {
        private readonly ReadOnlyCollection<AugmentInstance> augments;
        private readonly string canonicalText;

        private EquipmentInstance(
            StableId instanceId,
            StableId definitionId,
            int itemLevel,
            StableId qualityId,
            IEnumerable<AugmentInstance> augments)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            ItemLevel = itemLevel;
            QualityId = qualityId;
            this.augments = EquipmentDefinition.CopyAndSort(augments, CompareAugments);
            canonicalText = BuildCanonicalText();
            Fingerprint = EquipmentFingerprint.Compute(canonicalText);
        }

        public StableId InstanceId { get; }
        public StableId DefinitionId { get; }
        public int ItemLevel { get; }
        public StableId QualityId { get; }
        public IReadOnlyList<AugmentInstance> Augments { get { return augments; } }
        public string Fingerprint { get; }

        public static EquipmentInstance Create(
            StableId instanceId,
            StableId definitionId,
            int itemLevel,
            StableId qualityId,
            IEnumerable<AugmentInstance> augments)
        {
            return new EquipmentInstance(instanceId, definitionId, itemLevel, qualityId, augments);
        }

        public EquipmentInstance ReplaceAugment(AugmentInstance replacement)
        {
            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            if (augments == null)
            {
                throw new InvalidOperationException("Equipment instance has no augment collection.");
            }

            List<AugmentInstance> copy = new List<AugmentInstance>(augments.Count);
            bool replaced = false;
            for (int index = 0; index < augments.Count; index++)
            {
                AugmentInstance current = augments[index];
                if (current != null && Equals(current.InstanceId, replacement.InstanceId))
                {
                    copy.Add(replacement);
                    replaced = true;
                }
                else
                {
                    copy.Add(current);
                }
            }

            if (!replaced)
            {
                throw new InvalidOperationException("The replacement augment instance identity is not installed.");
            }

            return new EquipmentInstance(InstanceId, DefinitionId, ItemLevel, QualityId, copy);
        }

        public bool Equals(EquipmentInstance other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return Equals(obj as EquipmentInstance); }
        public override int GetHashCode() { return EquipmentFingerprint.OrdinalHash(canonicalText); }
        public string ToCanonicalString() { return canonicalText; }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("instance_id=").Append(InstanceId == null ? "null" : InstanceId.ToString()).Append('\n');
            builder.Append("definition_id=").Append(DefinitionId == null ? "null" : DefinitionId.ToString()).Append('\n');
            builder.Append("item_level=").Append(ItemLevel.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("quality_id=").Append(QualityId == null ? "null" : QualityId.ToString()).Append('\n');
            EquipmentDefinition.AppendList(builder, "augment", augments, delegate(AugmentInstance value)
            {
                return value == null ? "null" : value.ToCanonicalString().Replace("\n", "\\n");
            });
            return builder.ToString();
        }

        private static int CompareAugments(AugmentInstance left, AugmentInstance right)
        {
            if (ReferenceEquals(left, right)) { return 0; }
            if (ReferenceEquals(left, null)) { return -1; }
            return ReferenceEquals(right, null) ? 1 : left.CompareTo(right);
        }
    }

    public sealed class EquipmentValidationResult
    {
        private readonly ReadOnlyCollection<EquipmentModelIssue> issues;

        internal EquipmentValidationResult(IEnumerable<EquipmentModelIssue> issues)
        {
            List<EquipmentModelIssue> ordered = new List<EquipmentModelIssue>(issues ?? Enumerable.Empty<EquipmentModelIssue>());
            ordered.Sort();
            this.issues = new ReadOnlyCollection<EquipmentModelIssue>(ordered);
        }

        public bool IsValid { get { return issues.Count == 0; } }
        public IReadOnlyList<EquipmentModelIssue> Issues { get { return issues; } }
    }

    public sealed class EquipmentCatalogBuildResult
    {
        private readonly ReadOnlyCollection<EquipmentModelIssue> issues;

        internal EquipmentCatalogBuildResult(EquipmentCatalog catalog, IEnumerable<EquipmentModelIssue> issues)
        {
            Catalog = catalog;
            List<EquipmentModelIssue> ordered = new List<EquipmentModelIssue>(issues ?? Enumerable.Empty<EquipmentModelIssue>());
            ordered.Sort();
            this.issues = new ReadOnlyCollection<EquipmentModelIssue>(ordered);
        }

        public bool IsValid { get { return Catalog != null && issues.Count == 0; } }
        public EquipmentCatalog Catalog { get; }
        public IReadOnlyList<EquipmentModelIssue> Issues { get { return issues; } }
    }

    public sealed class EquipmentCatalog
    {
        private readonly ReadOnlyCollection<EquipmentDefinition> equipmentDefinitions;
        private readonly ReadOnlyCollection<AugmentDefinition> augmentDefinitions;
        private readonly Dictionary<StableId, EquipmentDefinition> equipmentById;
        private readonly Dictionary<StableId, AugmentDefinition> augmentById;

        private EquipmentCatalog(
            IList<EquipmentDefinition> equipmentDefinitions,
            IList<AugmentDefinition> augmentDefinitions)
        {
            this.equipmentDefinitions = new ReadOnlyCollection<EquipmentDefinition>(new List<EquipmentDefinition>(equipmentDefinitions));
            this.augmentDefinitions = new ReadOnlyCollection<AugmentDefinition>(new List<AugmentDefinition>(augmentDefinitions));
            equipmentById = this.equipmentDefinitions.ToDictionary(value => value.DefinitionId);
            augmentById = this.augmentDefinitions.ToDictionary(value => value.DefinitionId);
            CanonicalText = BuildCanonicalText();
            Fingerprint = EquipmentFingerprint.Compute(CanonicalText);
        }

        public IReadOnlyList<EquipmentDefinition> EquipmentDefinitions { get { return equipmentDefinitions; } }
        public IReadOnlyList<AugmentDefinition> AugmentDefinitions { get { return augmentDefinitions; } }
        public string CanonicalText { get; }
        public string Fingerprint { get; }

        public static EquipmentCatalogBuildResult Build(
            IEnumerable<EquipmentDefinition> equipmentDefinitions,
            IEnumerable<AugmentDefinition> augmentDefinitions)
        {
            List<EquipmentDefinition> equipment = new List<EquipmentDefinition>(equipmentDefinitions ?? Enumerable.Empty<EquipmentDefinition>());
            List<AugmentDefinition> augments = new List<AugmentDefinition>(augmentDefinitions ?? Enumerable.Empty<AugmentDefinition>());
            equipment.Sort(CompareEquipmentDefinitions);
            augments.Sort(CompareAugmentDefinitions);

            List<EquipmentModelIssue> issues = new List<EquipmentModelIssue>();
            ValidateEquipmentDefinitions(equipment, issues);
            ValidateAugmentDefinitions(augments, equipment, issues);
            ValidateCrossTypeIds(equipment, augments, issues);

            return issues.Count == 0
                ? new EquipmentCatalogBuildResult(new EquipmentCatalog(equipment, augments), issues)
                : new EquipmentCatalogBuildResult(null, issues);
        }

        public EquipmentDefinition FindEquipmentDefinition(StableId definitionId)
        {
            EquipmentDefinition value;
            return definitionId != null && equipmentById.TryGetValue(definitionId, out value) ? value : null;
        }

        public AugmentDefinition FindAugmentDefinition(StableId definitionId)
        {
            AugmentDefinition value;
            return definitionId != null && augmentById.TryGetValue(definitionId, out value) ? value : null;
        }

        public EquipmentValidationResult ValidateInstance(EquipmentInstance instance)
        {
            List<EquipmentModelIssue> issues = new List<EquipmentModelIssue>();
            if (instance == null)
            {
                issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.NullEquipmentInstance, null, "instance"));
                return new EquipmentValidationResult(issues);
            }

            if (instance.InstanceId == null)
            {
                issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.MissingEquipmentInstanceId, null, "instance_id"));
            }

            EquipmentDefinition equipment = FindEquipmentDefinition(instance.DefinitionId);
            if (equipment == null)
            {
                issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.UnknownEquipmentDefinition, instance.DefinitionId, "definition_id"));
                return new EquipmentValidationResult(issues);
            }

            if (equipment.ItemLevelRange == null || !equipment.ItemLevelRange.Contains(instance.ItemLevel))
            {
                issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.ItemLevelOutOfRange, instance.DefinitionId, instance.ItemLevel.ToString(CultureInfo.InvariantCulture)));
            }

            if (!equipment.SupportsQuality(instance.QualityId))
            {
                issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.UnknownQuality, instance.QualityId, "quality_id"));
            }

            IReadOnlyList<AugmentInstance> installed = instance.Augments;
            if (installed == null)
            {
                issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.NullAugmentInstance, instance.InstanceId, "augment_collection"));
                return new EquipmentValidationResult(issues);
            }

            if (installed.Count > equipment.MaximumAugmentSlots)
            {
                issues.Add(new EquipmentModelIssue(
                    EquipmentModelIssueCode.AugmentSlotCapacityExceeded,
                    instance.InstanceId,
                    installed.Count.ToString(CultureInfo.InvariantCulture) + ">" + equipment.MaximumAugmentSlots.ToString(CultureInfo.InvariantCulture)));
            }

            Dictionary<StableId, int> definitionCounts = new Dictionary<StableId, int>();
            HashSet<StableId> instanceIds = new HashSet<StableId>();
            List<AugmentDefinition> resolvedDefinitions = new List<AugmentDefinition>();

            for (int index = 0; index < installed.Count; index++)
            {
                AugmentInstance augment = installed[index];
                if (augment == null)
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.NullAugmentInstance, instance.InstanceId, index.ToString(CultureInfo.InvariantCulture)));
                    resolvedDefinitions.Add(null);
                    continue;
                }

                if (augment.InstanceId == null)
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.MissingAugmentInstanceId, augment.DefinitionId, index.ToString(CultureInfo.InvariantCulture)));
                }
                else if (!instanceIds.Add(augment.InstanceId))
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.DuplicateAugmentInstanceId, augment.InstanceId, "instance_id"));
                }

                AugmentDefinition definition = FindAugmentDefinition(augment.DefinitionId);
                resolvedDefinitions.Add(definition);
                if (definition == null)
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.UnknownAugmentDefinition, augment.DefinitionId, "definition_id"));
                    continue;
                }

                int count;
                definitionCounts.TryGetValue(definition.DefinitionId, out count);
                definitionCounts[definition.DefinitionId] = count + 1;

                if (definition.TierRange == null || !definition.TierRange.Contains(augment.Tier))
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.AugmentTierOutOfRange, definition.DefinitionId, augment.Tier.ToString(CultureInfo.InvariantCulture)));
                }

                if (definition.LevelRange == null || !definition.LevelRange.Contains(augment.Level))
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.AugmentLevelOutOfRange, definition.DefinitionId, augment.Level.ToString(CultureInfo.InvariantCulture)));
                }

                AddCompatibilityIssues(equipment, definition, issues);
            }

            foreach (KeyValuePair<StableId, int> pair in definitionCounts)
            {
                AugmentDefinition definition = FindAugmentDefinition(pair.Key);
                if (pair.Value > 1 && definition != null && definition.DuplicatePolicy == AugmentDuplicatePolicy.DisallowSameDefinition)
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.DuplicateAugmentNotAllowed, pair.Key, pair.Value.ToString(CultureInfo.InvariantCulture)));
                }
            }

            for (int left = 0; left < resolvedDefinitions.Count; left++)
            {
                AugmentDefinition leftDefinition = resolvedDefinitions[left];
                if (leftDefinition == null) { continue; }
                for (int right = left + 1; right < resolvedDefinitions.Count; right++)
                {
                    AugmentDefinition rightDefinition = resolvedDefinitions[right];
                    if (rightDefinition != null && leftDefinition.SharesExclusionGroup(rightDefinition))
                    {
                        issues.Add(new EquipmentModelIssue(
                            EquipmentModelIssueCode.ExclusionGroupConflict,
                            leftDefinition.DefinitionId,
                            rightDefinition.DefinitionId.ToString()));
                    }
                }
            }

            return new EquipmentValidationResult(issues);
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            EquipmentDefinition.AppendList(builder, "equipment_definition", equipmentDefinitions, delegate(EquipmentDefinition value)
            {
                return value.ToCanonicalString().Replace("\n", "\\n");
            });
            EquipmentDefinition.AppendList(builder, "augment_definition", augmentDefinitions, delegate(AugmentDefinition value)
            {
                return value.ToCanonicalString().Replace("\n", "\\n");
            });
            return builder.ToString();
        }

        private static void ValidateEquipmentDefinitions(IList<EquipmentDefinition> definitions, ICollection<EquipmentModelIssue> issues)
        {
            HashSet<StableId> ids = new HashSet<StableId>();
            for (int index = 0; index < definitions.Count; index++)
            {
                EquipmentDefinition definition = definitions[index];
                if (definition == null)
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.NullEquipmentDefinition, null, index.ToString(CultureInfo.InvariantCulture)));
                    continue;
                }

                if (definition.DefinitionId == null)
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.MissingDefinitionId, null, "equipment"));
                }
                else if (!ids.Add(definition.DefinitionId))
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.DuplicateEquipmentDefinitionId, definition.DefinitionId, "equipment"));
                }

                if (definition.CategoryId == null)
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.MissingCategoryId, definition.DefinitionId, "category_id"));
                }

                if (definition.FamilyId == null)
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.MissingFamilyId, definition.DefinitionId, "family_id"));
                }

                if (string.IsNullOrWhiteSpace(definition.DisplayName) || !string.Equals(definition.DisplayName, definition.DisplayName.Trim(), StringComparison.Ordinal))
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.InvalidDisplayName, definition.DefinitionId, "display_name"));
                }

                if (definition.ItemLevelRange == null || !definition.ItemLevelRange.IsOrderedPositive)
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.InvalidItemLevelRange, definition.DefinitionId, definition.ItemLevelRange == null ? "null" : definition.ItemLevelRange.ToCanonicalString()));
                }

                if (definition.MaximumAugmentSlots < 0)
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.InvalidAugmentSlotMaximum, definition.DefinitionId, definition.MaximumAugmentSlots.ToString(CultureInfo.InvariantCulture)));
                }

                ValidateRuntimeReference(definition, issues);
                ValidateQualityTiers(definition, issues);
                ValidateUniqueIds(definition.Tags, definition.DefinitionId, EquipmentModelIssueCode.DuplicateEquipmentTag, "tag", issues);
            }
        }

        private static void ValidateRuntimeReference(EquipmentDefinition definition, ICollection<EquipmentModelIssue> issues)
        {
            if (Equals(definition.CategoryId, EquipmentCategoryIds.Weapon))
            {
                if (definition.RuntimeWeaponReferenceId == null
                    || !string.Equals(definition.RuntimeWeaponReferenceId.Namespace, "weapon", StringComparison.Ordinal))
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.InvalidRuntimeReference, definition.DefinitionId, "weapon category requires weapon.* reference"));
                }
            }
            else if (definition.RuntimeWeaponReferenceId != null)
            {
                issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.InvalidRuntimeReference, definition.DefinitionId, "non-weapon equipment cannot carry a weapon runtime reference"));
            }
        }

        private static void ValidateQualityTiers(EquipmentDefinition definition, ICollection<EquipmentModelIssue> issues)
        {
            IReadOnlyList<EquipmentQualityTier> tiers = definition.QualityTiers;
            if (tiers == null || tiers.Count == 0)
            {
                issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.InvalidQualityTier, definition.DefinitionId, "at least one quality tier is required"));
                return;
            }

            HashSet<StableId> ids = new HashSet<StableId>();
            HashSet<int> ranks = new HashSet<int>();
            for (int index = 0; index < tiers.Count; index++)
            {
                EquipmentQualityTier tier = tiers[index];
                if (tier == null || tier.QualityId == null || tier.Rank < 1 || string.IsNullOrWhiteSpace(tier.Label))
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.InvalidQualityTier, definition.DefinitionId, index.ToString(CultureInfo.InvariantCulture)));
                    continue;
                }

                if (!ids.Add(tier.QualityId))
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.DuplicateQualityId, tier.QualityId, definition.DefinitionId.ToString()));
                }

                if (!ranks.Add(tier.Rank))
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.DuplicateQualityRank, definition.DefinitionId, tier.Rank.ToString(CultureInfo.InvariantCulture)));
                }
            }
        }

        private static void ValidateAugmentDefinitions(
            IList<AugmentDefinition> definitions,
            IList<EquipmentDefinition> equipment,
            ICollection<EquipmentModelIssue> issues)
        {
            HashSet<StableId> ids = new HashSet<StableId>();
            for (int index = 0; index < definitions.Count; index++)
            {
                AugmentDefinition definition = definitions[index];
                if (definition == null)
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.NullAugmentDefinition, null, index.ToString(CultureInfo.InvariantCulture)));
                    continue;
                }

                if (definition.DefinitionId == null)
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.MissingDefinitionId, null, "augment"));
                }
                else if (!ids.Add(definition.DefinitionId))
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.DuplicateAugmentDefinitionId, definition.DefinitionId, "augment"));
                }

                if (definition.FamilyId == null)
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.MissingFamilyId, definition.DefinitionId, "family_id"));
                }

                if (string.IsNullOrWhiteSpace(definition.DisplayName))
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.InvalidDisplayName, definition.DefinitionId, "display_name"));
                }

                if (definition.TierRange == null || !definition.TierRange.IsOrderedPositive
                    || definition.LevelRange == null || !definition.LevelRange.IsOrderedPositive)
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.InvalidAugmentRange, definition.DefinitionId, "tier or level range"));
                }

                if (definition.DuplicatePolicy != AugmentDuplicatePolicy.DisallowSameDefinition
                    && definition.DuplicatePolicy != AugmentDuplicatePolicy.AllowSameDefinition)
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.InvalidDuplicatePolicy, definition.DefinitionId, ((int)definition.DuplicatePolicy).ToString(CultureInfo.InvariantCulture)));
                }

                ValidateCompatibility(definition, equipment, issues);
                ValidateUniqueIds(definition.ExclusionGroupIds, definition.DefinitionId, EquipmentModelIssueCode.DuplicateExclusionGroup, "exclusion_group", issues);
            }
        }

        private static void ValidateCompatibility(
            AugmentDefinition definition,
            IList<EquipmentDefinition> equipment,
            ICollection<EquipmentModelIssue> issues)
        {
            AugmentCompatibility compatibility = definition.Compatibility;
            if (compatibility == null)
            {
                issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.InvalidCompatibility, definition.DefinitionId, "null"));
                return;
            }

            ValidateUniqueIds(compatibility.CategoryIds, definition.DefinitionId, EquipmentModelIssueCode.DuplicateCompatibilityValue, "category", issues);
            ValidateUniqueIds(compatibility.FamilyIds, definition.DefinitionId, EquipmentModelIssueCode.DuplicateCompatibilityValue, "family", issues);
            ValidateUniqueIds(compatibility.RequiredTags, definition.DefinitionId, EquipmentModelIssueCode.DuplicateCompatibilityValue, "required_tag", issues);
            ValidateUniqueIds(compatibility.ExcludedTags, definition.DefinitionId, EquipmentModelIssueCode.DuplicateCompatibilityValue, "excluded_tag", issues);

            if (compatibility.RequiredTags != null && compatibility.ExcludedTags != null)
            {
                for (int index = 0; index < compatibility.RequiredTags.Count; index++)
                {
                    StableId tag = compatibility.RequiredTags[index];
                    if (ContainsId(compatibility.ExcludedTags, tag))
                    {
                        issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.InvalidCompatibility, definition.DefinitionId, "required and excluded tag: " + tag));
                    }
                }
            }

            bool anyCompatible = false;
            for (int index = 0; index < equipment.Count; index++)
            {
                if (equipment[index] != null && compatibility.Allows(equipment[index]))
                {
                    anyCompatible = true;
                    break;
                }
            }

            if (equipment.Count > 0 && !anyCompatible)
            {
                issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.ImpossibleAugmentCompatibility, definition.DefinitionId, "no compatible equipment definition"));
            }
        }

        private static void ValidateCrossTypeIds(
            IList<EquipmentDefinition> equipment,
            IList<AugmentDefinition> augments,
            ICollection<EquipmentModelIssue> issues)
        {
            HashSet<StableId> equipmentIds = new HashSet<StableId>(equipment.Where(value => value != null && value.DefinitionId != null).Select(value => value.DefinitionId));
            for (int index = 0; index < augments.Count; index++)
            {
                AugmentDefinition augment = augments[index];
                if (augment != null && augment.DefinitionId != null && equipmentIds.Contains(augment.DefinitionId))
                {
                    issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.DefinitionIdCollision, augment.DefinitionId, "equipment and augment"));
                }
            }
        }

        private static void AddCompatibilityIssues(
            EquipmentDefinition equipment,
            AugmentDefinition augment,
            ICollection<EquipmentModelIssue> issues)
        {
            AugmentCompatibility compatibility = augment.Compatibility;
            if (compatibility == null)
            {
                issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.InvalidCompatibility, augment.DefinitionId, "null"));
                return;
            }

            if (compatibility.CategoryIds != null && compatibility.CategoryIds.Count > 0 && !ContainsId(compatibility.CategoryIds, equipment.CategoryId))
            {
                issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.IncompatibleAugmentCategory, augment.DefinitionId, equipment.CategoryId.ToString()));
            }

            if (compatibility.FamilyIds != null && compatibility.FamilyIds.Count > 0 && !ContainsId(compatibility.FamilyIds, equipment.FamilyId))
            {
                issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.IncompatibleAugmentFamily, augment.DefinitionId, equipment.FamilyId.ToString()));
            }

            if (compatibility.RequiredTags != null)
            {
                for (int index = 0; index < compatibility.RequiredTags.Count; index++)
                {
                    StableId tag = compatibility.RequiredTags[index];
                    if (!equipment.HasTag(tag))
                    {
                        issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.MissingRequiredEquipmentTag, augment.DefinitionId, tag.ToString()));
                    }
                }
            }

            if (compatibility.ExcludedTags != null)
            {
                for (int index = 0; index < compatibility.ExcludedTags.Count; index++)
                {
                    StableId tag = compatibility.ExcludedTags[index];
                    if (equipment.HasTag(tag))
                    {
                        issues.Add(new EquipmentModelIssue(EquipmentModelIssueCode.ExcludedEquipmentTag, augment.DefinitionId, tag.ToString()));
                    }
                }
            }
        }

        private static void ValidateUniqueIds(
            IReadOnlyList<StableId> values,
            StableId subjectId,
            EquipmentModelIssueCode code,
            string detail,
            ICollection<EquipmentModelIssue> issues)
        {
            if (values == null)
            {
                issues.Add(new EquipmentModelIssue(code, subjectId, detail + " collection is null"));
                return;
            }

            HashSet<StableId> seen = new HashSet<StableId>();
            for (int index = 0; index < values.Count; index++)
            {
                StableId value = values[index];
                if (value == null || !seen.Add(value))
                {
                    issues.Add(new EquipmentModelIssue(code, subjectId, detail + ":" + (value == null ? "null" : value.ToString())));
                }
            }
        }

        private static bool ContainsId(IReadOnlyList<StableId> values, StableId id)
        {
            if (values == null) { return false; }
            for (int index = 0; index < values.Count; index++)
            {
                if (Equals(values[index], id)) { return true; }
            }
            return false;
        }

        private static int CompareEquipmentDefinitions(EquipmentDefinition left, EquipmentDefinition right)
        {
            if (ReferenceEquals(left, right)) { return 0; }
            if (ReferenceEquals(left, null)) { return -1; }
            return ReferenceEquals(right, null) ? 1 : left.CompareTo(right);
        }

        private static int CompareAugmentDefinitions(AugmentDefinition left, AugmentDefinition right)
        {
            if (ReferenceEquals(left, right)) { return 0; }
            if (ReferenceEquals(left, null)) { return -1; }
            return ReferenceEquals(right, null) ? 1 : left.CompareTo(right);
        }
    }

    public static class EquipmentFingerprint
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static string Compute(string canonicalText)
        {
            if (canonicalText == null)
            {
                throw new ArgumentNullException(nameof(canonicalText));
            }

            unchecked
            {
                ulong hash = OffsetBasis;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    char value = canonicalText[index];
                    hash ^= (byte)(value & 0xff);
                    hash *= Prime;
                    hash ^= (byte)(value >> 8);
                    hash *= Prime;
                }

                return hash.ToString("x16", CultureInfo.InvariantCulture);
            }
        }

        internal static int OrdinalHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }
    }
}
