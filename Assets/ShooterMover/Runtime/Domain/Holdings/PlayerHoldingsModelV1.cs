using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Domain.Holdings
{
    /// <summary>
    /// Compile-time vocabulary marker for the holdings-owned idempotent ledger.
    /// </summary>
    public sealed class HoldingsLedgerVocabularyV1
    {
        private HoldingsLedgerVocabularyV1()
        {
        }
    }

    public static class HoldingsEntryTypeIdsV1
    {
        public static readonly StableId Equipment =
            StableId.Parse("holdings-entry.equipment");

        public static readonly StableId Strongbox =
            StableId.Parse("holdings-entry.strongbox");

        public static readonly StableId PremiumAmmo =
            StableId.Parse("holdings-entry.premium-ammo");

        public static readonly StableId Miscellaneous =
            StableId.Parse("holdings-entry.miscellaneous");

        public static readonly StableId Invalid =
            StableId.Parse("holdings-entry.invalid");

        public static StableId FromRewardKind(RewardGrantKindV1 rewardKind)
        {
            switch (rewardKind)
            {
                case RewardGrantKindV1.EquipmentReference:
                    return Equipment;
                case RewardGrantKindV1.Strongbox:
                    return Strongbox;
                case RewardGrantKindV1.PremiumAmmo:
                    return PremiumAmmo;
                case RewardGrantKindV1.Miscellaneous:
                    return Miscellaneous;
                default:
                    return Invalid;
            }
        }
    }

    /// <summary>
    /// Immutable source identity retained alongside every holdings transaction.
    /// Transaction and operation identities are carried by the paired economy
    /// transaction command; together they form the complete provenance tuple.
    /// </summary>
    public sealed class HoldingProvenanceV1 : IEquatable<HoldingProvenanceV1>
    {
        private readonly string canonicalText;

        private HoldingProvenanceV1(
            StableId grantStableId,
            StableId sourceStableId)
        {
            GrantStableId = grantStableId
                ?? throw new ArgumentNullException(nameof(grantStableId));
            SourceStableId = sourceStableId
                ?? throw new ArgumentNullException(nameof(sourceStableId));

            var builder = new StringBuilder();
            HoldingsCanonicalV1.AppendToken(
                builder,
                "grant_stable_id",
                GrantStableId.ToString());
            HoldingsCanonicalV1.AppendToken(
                builder,
                "source_stable_id",
                SourceStableId.ToString());
            canonicalText = builder.ToString();
            Fingerprint = HoldingsCanonicalV1.ComputeSha256(canonicalText);
        }

        public StableId GrantStableId { get; }

        public StableId SourceStableId { get; }

        public string Fingerprint { get; }

        public static HoldingProvenanceV1 Create(
            StableId grantStableId,
            StableId sourceStableId)
        {
            return new HoldingProvenanceV1(
                grantStableId,
                sourceStableId);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(HoldingProvenanceV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as HoldingProvenanceV1);
        }

        public override int GetHashCode()
        {
            return HoldingsCanonicalV1.DeterministicHash(canonicalText);
        }

        public override string ToString()
        {
            return canonicalText;
        }
    }

    /// <summary>
    /// Immutable current ownership projection for one unique strongbox or
    /// equipment instance.
    /// </summary>
    public sealed class UniqueHoldingSnapshotV1 :
        IEquatable<UniqueHoldingSnapshotV1>,
        IComparable<UniqueHoldingSnapshotV1>
    {
        private readonly string canonicalText;

        private UniqueHoldingSnapshotV1(
            RewardGrantKindV1 rewardKind,
            StableId definitionStableId,
            StableId instanceStableId,
            EquipmentInstance equipmentInstance,
            HoldingProvenanceV1 provenance)
        {
            if (rewardKind != RewardGrantKindV1.Strongbox
                && rewardKind != RewardGrantKindV1.EquipmentReference)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rewardKind),
                    rewardKind,
                    "Unique holdings are strongboxes or equipment references.");
            }

            DefinitionStableId = definitionStableId
                ?? throw new ArgumentNullException(nameof(definitionStableId));
            InstanceStableId = instanceStableId
                ?? throw new ArgumentNullException(nameof(instanceStableId));
            Provenance = provenance
                ?? throw new ArgumentNullException(nameof(provenance));

            if (rewardKind == RewardGrantKindV1.EquipmentReference)
            {
                EquipmentInstance = equipmentInstance
                    ?? throw new ArgumentNullException(nameof(equipmentInstance));

                if (EquipmentInstance.InstanceId != InstanceStableId)
                {
                    throw new ArgumentException(
                        "Equipment instance identity must match the holding instance identity.",
                        nameof(equipmentInstance));
                }

                if (EquipmentInstance.DefinitionId != DefinitionStableId)
                {
                    throw new ArgumentException(
                        "Equipment definition identity must match the holding definition identity.",
                        nameof(equipmentInstance));
                }
            }
            else if (equipmentInstance != null)
            {
                throw new ArgumentException(
                    "Strongbox holdings must not carry equipment payloads.",
                    nameof(equipmentInstance));
            }

            RewardKind = rewardKind;

            var builder = new StringBuilder();
            HoldingsCanonicalV1.AppendToken(
                builder,
                "reward_kind",
                ((int)RewardKind).ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "definition_stable_id",
                DefinitionStableId.ToString());
            HoldingsCanonicalV1.AppendToken(
                builder,
                "instance_stable_id",
                InstanceStableId.ToString());
            HoldingsCanonicalV1.AppendToken(
                builder,
                "equipment_instance",
                EquipmentInstance == null
                    ? "none"
                    : EquipmentInstance.ToCanonicalString());
            HoldingsCanonicalV1.AppendToken(
                builder,
                "provenance",
                Provenance.ToCanonicalString());
            canonicalText = builder.ToString();
            Fingerprint = HoldingsCanonicalV1.ComputeSha256(canonicalText);
        }

        public RewardGrantKindV1 RewardKind { get; }

        public StableId DefinitionStableId { get; }

        public StableId InstanceStableId { get; }

        public EquipmentInstance EquipmentInstance { get; }

        public HoldingProvenanceV1 Provenance { get; }

        public string Fingerprint { get; }

        public static UniqueHoldingSnapshotV1 Create(
            RewardGrantKindV1 rewardKind,
            StableId definitionStableId,
            StableId instanceStableId,
            EquipmentInstance equipmentInstance,
            HoldingProvenanceV1 provenance)
        {
            return new UniqueHoldingSnapshotV1(
                rewardKind,
                definitionStableId,
                instanceStableId,
                equipmentInstance,
                provenance);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public int CompareTo(UniqueHoldingSnapshotV1 other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int instanceComparison =
                InstanceStableId.CompareTo(other.InstanceStableId);
            if (instanceComparison != 0)
            {
                return instanceComparison;
            }

            int kindComparison = RewardKind.CompareTo(other.RewardKind);
            return kindComparison != 0
                ? kindComparison
                : DefinitionStableId.CompareTo(other.DefinitionStableId);
        }

        public bool Equals(UniqueHoldingSnapshotV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as UniqueHoldingSnapshotV1);
        }

        public override int GetHashCode()
        {
            return HoldingsCanonicalV1.DeterministicHash(canonicalText);
        }

        public override string ToString()
        {
            return canonicalText;
        }
    }

    /// <summary>
    /// Immutable current quantity projection for one typed stackable holding.
    /// </summary>
    public sealed class StackHoldingSnapshotV1 :
        IEquatable<StackHoldingSnapshotV1>,
        IComparable<StackHoldingSnapshotV1>
    {
        private readonly string canonicalText;

        private StackHoldingSnapshotV1(
            RewardGrantKindV1 rewardKind,
            StableId itemStableId,
            long quantity)
        {
            if (rewardKind != RewardGrantKindV1.PremiumAmmo
                && rewardKind != RewardGrantKindV1.Miscellaneous)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rewardKind),
                    rewardKind,
                    "Stack holdings are premium ammunition or miscellaneous items.");
            }

            ItemStableId = itemStableId
                ?? throw new ArgumentNullException(nameof(itemStableId));
            if (quantity < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    quantity,
                    "Stack snapshots contain positive current quantities only.");
            }

            RewardKind = rewardKind;
            Quantity = quantity;

            var builder = new StringBuilder();
            HoldingsCanonicalV1.AppendToken(
                builder,
                "reward_kind",
                ((int)RewardKind).ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "item_stable_id",
                ItemStableId.ToString());
            HoldingsCanonicalV1.AppendToken(
                builder,
                "quantity",
                Quantity.ToString(CultureInfo.InvariantCulture));
            canonicalText = builder.ToString();
            Fingerprint = HoldingsCanonicalV1.ComputeSha256(canonicalText);
        }

        public RewardGrantKindV1 RewardKind { get; }

        public StableId ItemStableId { get; }

        public long Quantity { get; }

        public string Fingerprint { get; }

        public static StackHoldingSnapshotV1 Create(
            RewardGrantKindV1 rewardKind,
            StableId itemStableId,
            long quantity)
        {
            return new StackHoldingSnapshotV1(
                rewardKind,
                itemStableId,
                quantity);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public int CompareTo(StackHoldingSnapshotV1 other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int kindComparison = RewardKind.CompareTo(other.RewardKind);
            return kindComparison != 0
                ? kindComparison
                : ItemStableId.CompareTo(other.ItemStableId);
        }

        public bool Equals(StackHoldingSnapshotV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StackHoldingSnapshotV1);
        }

        public override int GetHashCode()
        {
            return HoldingsCanonicalV1.DeterministicHash(canonicalText);
        }

        public override string ToString()
        {
            return canonicalText;
        }
    }

    /// <summary>
    /// Deterministic length-prefixed canonicalization and SHA-256 helpers shared
    /// by the holdings model and public contracts.
    /// </summary>
    public static class HoldingsCanonicalV1
    {
        private const string FingerprintPrefix = "sha256:";

        public static void AppendToken(
            StringBuilder builder,
            string name,
            string value)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            string normalized = value ?? "null";
            builder.Append(name)
                .Append(':')
                .Append(normalized.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(normalized)
                .Append('\n');
        }

        public static string ComputeSha256(string canonicalText)
        {
            if (canonicalText == null)
            {
                throw new ArgumentNullException(nameof(canonicalText));
            }

            byte[] bytes = Encoding.UTF8.GetBytes(canonicalText);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(bytes);
            }

            var builder = new StringBuilder(FingerprintPrefix, 71);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(
                    digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static bool IsCanonicalFingerprint(string value)
        {
            if (value == null
                || value.Length != FingerprintPrefix.Length + 64
                || !value.StartsWith(
                    FingerprintPrefix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            for (int index = FingerprintPrefix.Length;
                index < value.Length;
                index++)
            {
                char current = value[index];
                bool isDigit = current >= '0' && current <= '9';
                bool isLowerHex = current >= 'a' && current <= 'f';
                if (!isDigit && !isLowerHex)
                {
                    return false;
                }
            }

            return true;
        }

        public static int DeterministicHash(string value)
        {
            unchecked
            {
                const uint offsetBasis = 2166136261u;
                const uint prime = 16777619u;
                uint hash = offsetBasis;
                string text = value ?? string.Empty;
                for (int index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= prime;
                }

                return (int)hash;
            }
        }

        public static ReadOnlyCollection<T> CopyAndSort<T>(
            IEnumerable<T> values,
            Comparison<T> comparison,
            string parameterName)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<T>(values);
            for (int index = 0; index < copy.Count; index++)
            {
                if (ReferenceEquals(copy[index], null))
                {
                    throw new ArgumentException(
                        "Canonical holdings collections must not contain null entries.",
                        parameterName);
                }
            }

            copy.Sort(comparison);
            return new ReadOnlyCollection<T>(copy);
        }
    }
}
