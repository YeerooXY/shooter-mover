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
    public sealed class HoldingsLedgerVocabulary
    {
        private HoldingsLedgerVocabulary()
        {
        }
    }

    public static class HoldingsEntryTypeIds
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

        public static StableId FromRewardKind(RewardGrantKind rewardKind)
        {
            switch (rewardKind)
            {
                case RewardGrantKind.EquipmentReference:
                    return Equipment;
                case RewardGrantKind.Strongbox:
                    return Strongbox;
                case RewardGrantKind.PremiumAmmo:
                    return PremiumAmmo;
                case RewardGrantKind.Miscellaneous:
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
    public sealed class HoldingProvenance : IEquatable<HoldingProvenance>
    {
        private readonly string canonicalText;

        private HoldingProvenance(
            StableId grantStableId,
            StableId sourceStableId)
        {
            GrantStableId = grantStableId
                ?? throw new ArgumentNullException(nameof(grantStableId));
            SourceStableId = sourceStableId
                ?? throw new ArgumentNullException(nameof(sourceStableId));

            var builder = new StringBuilder();
            HoldingsFormat.AppendToken(
                builder,
                "grant_stable_id",
                GrantStableId.ToString());
            HoldingsFormat.AppendToken(
                builder,
                "source_stable_id",
                SourceStableId.ToString());
            canonicalText = builder.ToString();
            Fingerprint = HoldingsFormat.ComputeSha256(canonicalText);
        }

        public StableId GrantStableId { get; }

        public StableId SourceStableId { get; }

        public string Fingerprint { get; }

        public static HoldingProvenance Create(
            StableId grantStableId,
            StableId sourceStableId)
        {
            return new HoldingProvenance(
                grantStableId,
                sourceStableId);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(HoldingProvenance other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as HoldingProvenance);
        }

        public override int GetHashCode()
        {
            return HoldingsFormat.DeterministicHash(canonicalText);
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
    public sealed class UniqueHoldingSnapshot :
        IEquatable<UniqueHoldingSnapshot>,
        IComparable<UniqueHoldingSnapshot>
    {
        private readonly string canonicalText;

        private UniqueHoldingSnapshot(
            RewardGrantKind rewardKind,
            StableId definitionStableId,
            StableId instanceStableId,
            EquipmentInstance equipmentInstance,
            HoldingProvenance provenance)
        {
            if (rewardKind != RewardGrantKind.Strongbox
                && rewardKind != RewardGrantKind.EquipmentReference)
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

            if (rewardKind == RewardGrantKind.EquipmentReference)
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
            HoldingsFormat.AppendToken(
                builder,
                "reward_kind",
                ((int)RewardKind).ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "definition_stable_id",
                DefinitionStableId.ToString());
            HoldingsFormat.AppendToken(
                builder,
                "instance_stable_id",
                InstanceStableId.ToString());
            HoldingsFormat.AppendToken(
                builder,
                "equipment_instance",
                EquipmentInstance == null
                    ? "none"
                    : EquipmentInstance.ToCanonicalString());
            HoldingsFormat.AppendToken(
                builder,
                "provenance",
                Provenance.ToCanonicalString());
            canonicalText = builder.ToString();
            Fingerprint = HoldingsFormat.ComputeSha256(canonicalText);
        }

        public RewardGrantKind RewardKind { get; }

        public StableId DefinitionStableId { get; }

        public StableId InstanceStableId { get; }

        public EquipmentInstance EquipmentInstance { get; }

        public HoldingProvenance Provenance { get; }

        public string Fingerprint { get; }

        public static UniqueHoldingSnapshot Create(
            RewardGrantKind rewardKind,
            StableId definitionStableId,
            StableId instanceStableId,
            EquipmentInstance equipmentInstance,
            HoldingProvenance provenance)
        {
            return new UniqueHoldingSnapshot(
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

        public int CompareTo(UniqueHoldingSnapshot other)
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

        public bool Equals(UniqueHoldingSnapshot other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as UniqueHoldingSnapshot);
        }

        public override int GetHashCode()
        {
            return HoldingsFormat.DeterministicHash(canonicalText);
        }

        public override string ToString()
        {
            return canonicalText;
        }
    }

    /// <summary>
    /// Immutable current quantity projection for one typed stackable holding.
    /// </summary>
    public sealed class StackHoldingSnapshot :
        IEquatable<StackHoldingSnapshot>,
        IComparable<StackHoldingSnapshot>
    {
        private readonly string canonicalText;

        private StackHoldingSnapshot(
            RewardGrantKind rewardKind,
            StableId itemStableId,
            long quantity)
        {
            if (rewardKind != RewardGrantKind.PremiumAmmo
                && rewardKind != RewardGrantKind.Miscellaneous)
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
            HoldingsFormat.AppendToken(
                builder,
                "reward_kind",
                ((int)RewardKind).ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "item_stable_id",
                ItemStableId.ToString());
            HoldingsFormat.AppendToken(
                builder,
                "quantity",
                Quantity.ToString(CultureInfo.InvariantCulture));
            canonicalText = builder.ToString();
            Fingerprint = HoldingsFormat.ComputeSha256(canonicalText);
        }

        public RewardGrantKind RewardKind { get; }

        public StableId ItemStableId { get; }

        public long Quantity { get; }

        public string Fingerprint { get; }

        public static StackHoldingSnapshot Create(
            RewardGrantKind rewardKind,
            StableId itemStableId,
            long quantity)
        {
            return new StackHoldingSnapshot(
                rewardKind,
                itemStableId,
                quantity);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public int CompareTo(StackHoldingSnapshot other)
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

        public bool Equals(StackHoldingSnapshot other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StackHoldingSnapshot);
        }

        public override int GetHashCode()
        {
            return HoldingsFormat.DeterministicHash(canonicalText);
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
    public static class HoldingsFormat
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
