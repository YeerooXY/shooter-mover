using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Missions.Results
{
    public enum MissionRunCompletionStateV1
    {
        Completed = 1,
        Failed = 2,
        Abandoned = 3,
    }

    public enum MissionRunStrongboxStateV1
    {
        Unopened = 1,
        Opened = 2,
    }

    public enum MissionRunAuthorityStatusV1
    {
        StrongboxCollected = 1,
        RunEnded = 2,
        ExactDuplicateNoChange = 3,
        ConflictingDuplicate = 4,
        StaleInput = 5,
        RouteMismatch = 6,
        RunAlreadyEnded = 7,
        ExternalAuthorityRejected = 8,
        InvalidRequest = 9,
    }

    public static class MissionRunCanonicalV1
    {
        private const string FingerprintPrefix = "sha256:";

        public static void AppendToken(StringBuilder builder, string name, string value)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (name == null) throw new ArgumentNullException(nameof(name));
            string normalized = value ?? "null";
            builder.Append(name)
                .Append(':')
                .Append(normalized.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(normalized)
                .Append('\n');
        }

        public static string Fingerprint(string canonicalText)
        {
            if (canonicalText == null) throw new ArgumentNullException(nameof(canonicalText));
            byte[] digest;
            using (SHA256 algorithm = SHA256.Create())
            {
                digest = algorithm.ComputeHash(Encoding.UTF8.GetBytes(canonicalText));
            }

            StringBuilder builder = new StringBuilder(FingerprintPrefix, 71);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }

        public static bool IsFingerprint(string value)
        {
            if (value == null || value.Length != 71 || !value.StartsWith(FingerprintPrefix, StringComparison.Ordinal))
            {
                return false;
            }
            for (int index = FingerprintPrefix.Length; index < value.Length; index++)
            {
                char current = value[index];
                if (!((current >= '0' && current <= '9') || (current >= 'a' && current <= 'f')))
                {
                    return false;
                }
            }
            return true;
        }

        public static int DeterministicHash(string canonicalText)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string source = canonicalText ?? string.Empty;
                for (int index = 0; index < source.Length; index++)
                {
                    hash ^= source[index];
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }
    }

    public sealed class MissionRunStrongboxCollectionV1 :
        IEquatable<MissionRunStrongboxCollectionV1>,
        IComparable<MissionRunStrongboxCollectionV1>
    {
        private readonly string canonicalText;

        public MissionRunStrongboxCollectionV1(
            StableId definitionStableId,
            StableId instanceStableId,
            StableId grantStableId,
            StableId sourceStableId,
            StableId collectionOperationStableId,
            long holdingsSequenceAtCollection,
            string holdingsFingerprintAtCollection)
        {
            DefinitionStableId = definitionStableId ?? throw new ArgumentNullException(nameof(definitionStableId));
            InstanceStableId = instanceStableId ?? throw new ArgumentNullException(nameof(instanceStableId));
            GrantStableId = grantStableId ?? throw new ArgumentNullException(nameof(grantStableId));
            SourceStableId = sourceStableId ?? throw new ArgumentNullException(nameof(sourceStableId));
            CollectionOperationStableId = collectionOperationStableId
                ?? throw new ArgumentNullException(nameof(collectionOperationStableId));
            if (holdingsSequenceAtCollection < 0L) throw new ArgumentOutOfRangeException(nameof(holdingsSequenceAtCollection));
            if (!MissionRunCanonicalV1.IsFingerprint(holdingsFingerprintAtCollection))
            {
                throw new ArgumentException("Holdings fingerprint must be canonical.", nameof(holdingsFingerprintAtCollection));
            }
            HoldingsSequenceAtCollection = holdingsSequenceAtCollection;
            HoldingsFingerprintAtCollection = holdingsFingerprintAtCollection;

            StringBuilder builder = new StringBuilder();
            MissionRunCanonicalV1.AppendToken(builder, "definition_stable_id", DefinitionStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "instance_stable_id", InstanceStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "grant_stable_id", GrantStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "source_stable_id", SourceStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "collection_operation_stable_id", CollectionOperationStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "holdings_sequence_at_collection", HoldingsSequenceAtCollection.ToString(CultureInfo.InvariantCulture));
            MissionRunCanonicalV1.AppendToken(builder, "holdings_fingerprint_at_collection", HoldingsFingerprintAtCollection);
            canonicalText = builder.ToString();
            Fingerprint = MissionRunCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId DefinitionStableId { get; }
        public StableId InstanceStableId { get; }
        public StableId GrantStableId { get; }
        public StableId SourceStableId { get; }
        public StableId CollectionOperationStableId { get; }
        public long HoldingsSequenceAtCollection { get; }
        public string HoldingsFingerprintAtCollection { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString() { return canonicalText; }

        public int CompareTo(MissionRunStrongboxCollectionV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : InstanceStableId.CompareTo(other.InstanceStableId);
        }

        public bool Equals(MissionRunStrongboxCollectionV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return Equals(obj as MissionRunStrongboxCollectionV1); }
        public override int GetHashCode() { return MissionRunCanonicalV1.DeterministicHash(canonicalText); }
    }

    public sealed class MissionRunStrongboxResultV1 :
        IEquatable<MissionRunStrongboxResultV1>,
        IComparable<MissionRunStrongboxResultV1>
    {
        private readonly string canonicalText;

        public MissionRunStrongboxResultV1(
            MissionRunStrongboxCollectionV1 collection,
            MissionRunStrongboxStateV1 state,
            StableId openingStableId,
            string openingResultFingerprint)
        {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
            if (!Enum.IsDefined(typeof(MissionRunStrongboxStateV1), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }
            if (state == MissionRunStrongboxStateV1.Unopened)
            {
                if (openingStableId != null || openingResultFingerprint != null)
                {
                    throw new ArgumentException("Unopened strongboxes must not carry opening facts.");
                }
            }
            else
            {
                OpeningStableId = openingStableId ?? throw new ArgumentNullException(nameof(openingStableId));
                if (!MissionRunCanonicalV1.IsFingerprint(openingResultFingerprint))
                {
                    throw new ArgumentException("Opened strongboxes require a canonical opening result fingerprint.", nameof(openingResultFingerprint));
                }
            }

            State = state;
            OpeningStableId = openingStableId;
            OpeningResultFingerprint = openingResultFingerprint;
            StringBuilder builder = new StringBuilder();
            MissionRunCanonicalV1.AppendToken(builder, "collection", Collection.ToCanonicalString());
            MissionRunCanonicalV1.AppendToken(builder, "state", ((int)State).ToString(CultureInfo.InvariantCulture));
            MissionRunCanonicalV1.AppendToken(builder, "opening_stable_id", OpeningStableId == null ? "none" : OpeningStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "opening_result_fingerprint", OpeningResultFingerprint ?? "none");
            canonicalText = builder.ToString();
            Fingerprint = MissionRunCanonicalV1.Fingerprint(canonicalText);
        }

        public MissionRunStrongboxCollectionV1 Collection { get; }
        public MissionRunStrongboxStateV1 State { get; }
        public StableId OpeningStableId { get; }
        public string OpeningResultFingerprint { get; }
        public string Fingerprint { get; }
        public StableId DefinitionStableId { get { return Collection.DefinitionStableId; } }
        public StableId InstanceStableId { get { return Collection.InstanceStableId; } }
        public bool IsUnopened { get { return State == MissionRunStrongboxStateV1.Unopened; } }

        public string ToCanonicalString() { return canonicalText; }
        public int CompareTo(MissionRunStrongboxResultV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : InstanceStableId.CompareTo(other.InstanceStableId);
        }
        public bool Equals(MissionRunStrongboxResultV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as MissionRunStrongboxResultV1); }
        public override int GetHashCode() { return MissionRunCanonicalV1.DeterministicHash(canonicalText); }
    }
}
