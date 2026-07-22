using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.Domain.Rewards.Drops
{
    public enum RewardProfileOverrideOperationV1
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
    public sealed class RewardProfileOverrideV1 :
        IComparable<RewardProfileOverrideV1>
    {
        private readonly ReadOnlyCollection<RewardRollGroupV1> addedGroups;
        private readonly string canonicalText;

        private RewardProfileOverrideV1(
            StableId overrideStableId,
            RewardProfileOverrideOperationV1 operation,
            RewardSourceProfileV1 replacementProfile,
            IEnumerable<RewardRollGroupV1> addedGroups,
            int probabilityMultiplierPermille,
            int quantityMultiplierPermille,
            StableId strongboxTierSelectionProfileOverrideId)
        {
            OverrideStableId = overrideStableId
                ?? throw new ArgumentNullException(nameof(overrideStableId));
            if (!Enum.IsDefined(typeof(RewardProfileOverrideOperationV1), operation))
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
            Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
        }

        public StableId OverrideStableId { get; }
        public RewardProfileOverrideOperationV1 Operation { get; }
        public RewardSourceProfileV1 ReplacementProfile { get; }
        public IReadOnlyList<RewardRollGroupV1> AddedGroups { get { return addedGroups; } }
        public int ProbabilityMultiplierPermille { get; }
        public int QuantityMultiplierPermille { get; }
        public StableId StrongboxTierSelectionProfileOverrideId { get; }
        public string Fingerprint { get; }

        public static RewardProfileOverrideV1 Replace(
            StableId overrideStableId,
            RewardSourceProfileV1 replacementProfile)
        {
            return new RewardProfileOverrideV1(
                overrideStableId,
                RewardProfileOverrideOperationV1.Replace,
                replacementProfile,
                Array.Empty<RewardRollGroupV1>(),
                1000,
                1000,
                null);
        }

        public static RewardProfileOverrideV1 AddGroups(
            StableId overrideStableId,
            IEnumerable<RewardRollGroupV1> groups)
        {
            return new RewardProfileOverrideV1(
                overrideStableId,
                RewardProfileOverrideOperationV1.AddGroups,
                null,
                groups,
                1000,
                1000,
                null);
        }

        public static RewardProfileOverrideV1 Modify(
            StableId overrideStableId,
            int probabilityMultiplierPermille,
            int quantityMultiplierPermille,
            StableId strongboxTierSelectionProfileOverrideId)
        {
            return new RewardProfileOverrideV1(
                overrideStableId,
                RewardProfileOverrideOperationV1.Modify,
                null,
                Array.Empty<RewardRollGroupV1>(),
                probabilityMultiplierPermille,
                quantityMultiplierPermille,
                strongboxTierSelectionProfileOverrideId);
        }

        public static RewardProfileOverrideV1 Disable(
            StableId overrideStableId)
        {
            return new RewardProfileOverrideV1(
                overrideStableId,
                RewardProfileOverrideOperationV1.Disable,
                null,
                Array.Empty<RewardRollGroupV1>(),
                1000,
                1000,
                null);
        }

        public int CompareTo(RewardProfileOverrideV1 other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : OverrideStableId.CompareTo(other.OverrideStableId);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        private static ReadOnlyCollection<RewardRollGroupV1> CopyGroups(
            IEnumerable<RewardRollGroupV1> source)
        {
            var copy = new List<RewardRollGroupV1>();
            if (source != null)
            {
                foreach (RewardRollGroupV1 group in source)
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
            return new ReadOnlyCollection<RewardRollGroupV1>(copy);
        }

        private void ValidateShape()
        {
            if ((Operation == RewardProfileOverrideOperationV1.Replace)
                != (ReplacementProfile != null))
            {
                throw new ArgumentException(
                    "Only replacement overrides carry a replacement profile.");
            }
            if ((Operation == RewardProfileOverrideOperationV1.AddGroups)
                != (addedGroups.Count > 0))
            {
                throw new ArgumentException(
                    "Only additive overrides carry added groups.");
            }
            if (Operation != RewardProfileOverrideOperationV1.Modify
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
