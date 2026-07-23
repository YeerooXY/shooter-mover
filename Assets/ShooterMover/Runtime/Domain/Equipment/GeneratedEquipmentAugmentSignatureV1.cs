using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.Domain.Equipment
{
    /// <summary>
    /// Versioned immutable generation metadata for one exact equipment instance.
    /// Capacity and shared level describe future installation space; they never imply
    /// that an AugmentInstance has been installed.
    /// </summary>
    public sealed class GeneratedEquipmentAugmentSignatureV1 :
        IEquatable<GeneratedEquipmentAugmentSignatureV1>,
        IComparable<GeneratedEquipmentAugmentSignatureV1>
    {
        private readonly string canonicalText;

        public GeneratedEquipmentAugmentSignatureV1(
            StableId equipmentInstanceStableId,
            StableId sourceStrongboxInstanceStableId,
            StableId hybridPolicyStableId,
            int capacity,
            int sharedLevel,
            string hybridPolicyFingerprint,
            int algorithmVersion)
        {
            EquipmentInstanceStableId = equipmentInstanceStableId
                ?? throw new ArgumentNullException(nameof(equipmentInstanceStableId));
            SourceStrongboxInstanceStableId = sourceStrongboxInstanceStableId
                ?? throw new ArgumentNullException(nameof(sourceStrongboxInstanceStableId));
            HybridPolicyStableId = hybridPolicyStableId
                ?? throw new ArgumentNullException(nameof(hybridPolicyStableId));
            if (capacity < 0 || capacity > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            if ((capacity == 0 && sharedLevel != 0)
                || (capacity > 0 && sharedLevel < 1))
            {
                throw new ArgumentOutOfRangeException(nameof(sharedLevel));
            }
            if (string.IsNullOrWhiteSpace(hybridPolicyFingerprint))
            {
                throw new ArgumentException(
                    "The frozen hybrid policy fingerprint is required.",
                    nameof(hybridPolicyFingerprint));
            }
            if (algorithmVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(algorithmVersion));
            }

            Capacity = capacity;
            SharedLevel = sharedLevel;
            HybridPolicyFingerprint = hybridPolicyFingerprint.Trim();
            AlgorithmVersion = algorithmVersion;
            var builder = new StringBuilder(
                "schema=generated-equipment-augment-signature-v1");
            builder.Append("\nequipment_instance_id=")
                .Append(EquipmentInstanceStableId)
                .Append("\nsource_strongbox_instance_id=")
                .Append(SourceStrongboxInstanceStableId)
                .Append("\nhybrid_policy_id=")
                .Append(HybridPolicyStableId)
                .Append("\ncapacity=")
                .Append(Capacity.ToString(CultureInfo.InvariantCulture))
                .Append("\nshared_level=")
                .Append(SharedLevel.ToString(CultureInfo.InvariantCulture))
                .Append("\nhybrid_policy_fingerprint=")
                .Append(HybridPolicyFingerprint)
                .Append("\nalgorithm_version=")
                .Append(AlgorithmVersion.ToString(CultureInfo.InvariantCulture));
            canonicalText = builder.ToString();
            Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
        }

        public StableId EquipmentInstanceStableId { get; }
        public StableId SourceStrongboxInstanceStableId { get; }
        public StableId HybridPolicyStableId { get; }
        public int Capacity { get; }
        public int SharedLevel { get; }
        public string HybridPolicyFingerprint { get; }
        public int AlgorithmVersion { get; }
        public string Fingerprint { get; }
        public bool HasCapacity { get { return Capacity > 0; } }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public int CompareTo(GeneratedEquipmentAugmentSignatureV1 other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : EquipmentInstanceStableId.CompareTo(
                    other.EquipmentInstanceStableId);
        }

        public bool Equals(GeneratedEquipmentAugmentSignatureV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as GeneratedEquipmentAugmentSignatureV1);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(canonicalText);
        }
    }
}
