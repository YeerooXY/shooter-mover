using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.Domain.Rewards.Drops
{
    public enum RewardProfileOverrideOperation
    {
        Replace = 1,
        AddGroups = 2,
        Modify = 3,
        Disable = 4,
    }

    /// <summary>
    /// One immutable override layer. Precedence is owned by the resolver; this value
    /// only describes replace/add/modify/disable semantics.
    /// </summary>
    public sealed class RewardProfileOverride :
        IComparable<RewardProfileOverride>
    {
        private readonly ReadOnlyCollection<RewardRollGroup> addedGroups;
        private readonly string canonicalText;

        private RewardProfileOverride(
            StableId overrideStableId,
            RewardProfileOverrideOperation operation,
            RewardSourceProfile replacementProfile,
            IEnumerable<RewardRollGroup> addedGroups,
            int probabilityMultiplierPermille,
            int quantityMultiplierPermille,
            StableId strongboxTierSelectionProfileOverrideId)
        {
            OverrideStableId = overrideStableId
                ?? throw new ArgumentNullException(nameof(overrideStableId));
            if (!Enum.IsDefined(typeof(RewardProfileOverrideOperation), operation))
            {
                throw new ArgumentOutOfRangeException(nameof(operation));
            }
            if (probabilityMultiplierPermille < 0
                || quantityMultiplierPermille < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(probabilityMultiplierPermille));
            }

            Operation = operation;
            ReplacementProfile = replacementProfile;
            this.addedGroups = CopyGroups(addedGroups);
            ProbabilityMultiplierPermille = probabilityMultiplierPermille;
            QuantityMultiplierPermille = quantityMultiplierPermille;
            StrongboxTierSelectionProfileOverrideId =
                strongboxTierSelectionProfileOverrideId;
            ValidateShape();

            var builder = new StringBuilder("schema=reward-profile-override-v1");
            builder.Append("\noverride_id=").Append(OverrideStableId)
                .Append("\noperation=").Append(((int)Operation).ToString(CultureInfo.InvariantCulture))
                .Append("\nreplacement=").Append(ReplacementProfile == null ? "none" : ReplacementProfile.Fingerprint)
                .Append("\nprobability_multiplier_permille=").Append(ProbabilityMultiplierPermille.ToString(CultureInfo.InvariantCulture))
                .Append("\nquantity_multiplier_permille=").Append(QuantityMultiplierPermille.ToString(CultureInfo.InvariantCulture))
                .Append("\ntier_profile_override=")
                .Append(StrongboxTierSelectionProfileOverrideId == null
                    ? "none"
                    : StrongboxTierSelectionProfileOverrideId.ToString())
                .Append("\nadded_group_count=").Append(this.addedGroups.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.addedGroups.Count; index++)
            {
                builder.Append("\nadded_group_").Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append(":\n").Append(this.addedGroups[index].ToCanonicalString());
            }
            canonicalText = builder.ToString();
            Fingerprint = RewardGenerationFingerprint.Compute(canonicalText);
        }

        public StableId OverrideStableId { get; }
        public RewardProfileOverrideOperation Operation { get; }
        public RewardSourceProfile ReplacementProfile { get; }
        public IReadOnlyList<RewardRollGroup> AddedGroups { get { return addedGroups; } }
        public int ProbabilityMultiplierPermille { get; }
        public int QuantityMultiplierPermille { get; }
        public StableId StrongboxTierSelectionProfileOverrideId { get; }
        public string Fingerprint { get; }

        public static RewardProfileOverride Replace(
            StableId overrideStableId,
            RewardSourceProfile replacementProfile)
        {
            return new RewardProfileOverride(
                overrideStableId,
                RewardProfileOverrideOperation.Replace,
                replacementProfile,
                Array.Empty<RewardRollGroup>(),
                1000,
                1000,
                null);
        }

        public static RewardProfileOverride AddGroups(
            StableId overrideStableId,
            IEnumerable<RewardRollGroup> groups)
        {
            return new RewardProfileOverride(
                overrideStableId,
                RewardProfileOverrideOperation.AddGroups,
                null,
                groups,
                1000,
                1000,
                null);
        }

        public static RewardProfileOverride Modify(
            StableId overrideStableId,
            int probabilityMultiplierPermille,
            int quantityMultiplierPermille,
            StableId strongboxTierSelectionProfileOverrideId)
        {
            return new RewardProfileOverride(
                overrideStableId,
                RewardProfileOverrideOperation.Modify,
                null,
                Array.Empty<RewardRollGroup>(),
                probabilityMultiplierPermille,
                quantityMultiplierPermille,
                strongboxTierSelectionProfileOverrideId);
        }

        public static RewardProfileOverride Disable(
            StableId overrideStableId)
        {
            return new RewardProfileOverride(
                overrideStableId,
                RewardProfileOverrideOperation.Disable,
                null,
                Array.Empty<RewardRollGroup>(),
                1000,
                1000,
                null);
        }

        public int CompareTo(RewardProfileOverride other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : OverrideStableId.CompareTo(other.OverrideStableId);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        private static ReadOnlyCollection<RewardRollGroup> CopyGroups(
            IEnumerable<RewardRollGroup> source)
        {
            var copy = new List<RewardRollGroup>();
            if (source != null)
            {
                foreach (RewardRollGroup group in source)
                {
                    if (group == null)
                    {
                        throw new ArgumentException(
                            "Added groups must not contain null entries.",
                            nameof(source));
                    }
                    copy.Add(group);
                }
            }
            copy.Sort();
            return new ReadOnlyCollection<RewardRollGroup>(copy);
        }

        private void ValidateShape()
        {
            if ((Operation == RewardProfileOverrideOperation.Replace)
                != (ReplacementProfile != null))
            {
                throw new ArgumentException(
                    "Only replacement overrides carry a replacement profile.");
            }
            if ((Operation == RewardProfileOverrideOperation.AddGroups)
                != (addedGroups.Count > 0))
            {
                throw new ArgumentException(
                    "Only additive overrides carry added groups.");
            }
            if (Operation != RewardProfileOverrideOperation.Modify
                && (ProbabilityMultiplierPermille != 1000
                    || QuantityMultiplierPermille != 1000
                    || StrongboxTierSelectionProfileOverrideId != null))
            {
                throw new ArgumentException(
                    "Only modify overrides carry multipliers or tier-profile changes.");
            }
        }
    }
}
