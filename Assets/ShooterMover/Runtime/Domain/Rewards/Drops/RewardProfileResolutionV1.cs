using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.Domain.Rewards.Drops
{
    public sealed class RewardProfileResolutionV1 :
        IEquatable<RewardProfileResolutionV1>
    {
        private readonly ReadOnlyCollection<StableId> appliedOverrideIds;
        private readonly string canonicalText;

        public RewardProfileResolutionV1(
            StableId declaredProfileReferenceId,
            RewardSourceProfileV1 sourceDefaultProfile,
            RewardSourceProfileV1 effectiveProfile,
            IEnumerable<StableId> appliedOverrideIds)
        {
            DeclaredProfileReferenceId = declaredProfileReferenceId
                ?? throw new ArgumentNullException(nameof(declaredProfileReferenceId));
            SourceDefaultProfile = sourceDefaultProfile
                ?? throw new ArgumentNullException(nameof(sourceDefaultProfile));
            EffectiveProfile = effectiveProfile
                ?? throw new ArgumentNullException(nameof(effectiveProfile));
            this.appliedOverrideIds = CopyIds(appliedOverrideIds);

            var builder = new StringBuilder("schema=reward-profile-resolution-v1");
            builder.Append("\ndeclared_profile_reference_id=").Append(DeclaredProfileReferenceId)
                .Append("\nsource_default_profile=").Append(SourceDefaultProfile.Fingerprint)
                .Append("\neffective_profile=").Append(EffectiveProfile.Fingerprint)
                .Append("\napplied_override_count=").Append(this.appliedOverrideIds.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.appliedOverrideIds.Count; index++)
            {
                builder.Append("\napplied_override_").Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append("=").Append(this.appliedOverrideIds[index]);
            }
            canonicalText = builder.ToString();
            Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
        }

        public StableId DeclaredProfileReferenceId { get; }
        public RewardSourceProfileV1 SourceDefaultProfile { get; }
        public RewardSourceProfileV1 EffectiveProfile { get; }
        public IReadOnlyList<StableId> AppliedOverrideIds
        {
            get { return appliedOverrideIds; }
        }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(RewardProfileResolutionV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RewardProfileResolutionV1);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(canonicalText);
        }

        private static ReadOnlyCollection<StableId> CopyIds(
            IEnumerable<StableId> source)
        {
            var copy = new List<StableId>();
            if (source != null)
            {
                foreach (StableId value in source)
                {
                    if (value == null)
                    {
                        throw new ArgumentException(
                            "Applied override identities must not contain null entries.",
                            nameof(source));
                    }
                    copy.Add(value);
                }
            }
            return new ReadOnlyCollection<StableId>(copy);
        }
    }
}
