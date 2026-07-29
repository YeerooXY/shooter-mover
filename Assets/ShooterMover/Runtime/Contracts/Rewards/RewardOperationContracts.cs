using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Contracts.Rewards
{
    /// <summary>
    /// Immutable identity envelope for one logical reward resolution. A retry reuses
    /// these identities and the same request fingerprint.
    /// </summary>
    public sealed class RewardOperationRequest : IEquatable<RewardOperationRequest>
    {
        private readonly string canonicalText;
        private readonly string fingerprint;

        private RewardOperationRequest(
            StableId runStableId,
            StableId sourceInstanceStableId,
            StableId sourceOperationStableId,
            StableId commitmentStableId,
            StableId rewardProfileStableId,
            string contentFingerprint)
        {
            this.RunStableId = RewardContractFormat.RequireStableId(runStableId, nameof(runStableId));
            this.SourceInstanceStableId = RewardContractFormat.RequireStableId(
                sourceInstanceStableId,
                nameof(sourceInstanceStableId));
            this.SourceOperationStableId = RewardContractFormat.RequireStableId(
                sourceOperationStableId,
                nameof(sourceOperationStableId));
            this.CommitmentStableId = RewardContractFormat.RequireStableId(
                commitmentStableId,
                nameof(commitmentStableId));
            this.RewardProfileStableId = RewardContractFormat.RequireStableId(
                rewardProfileStableId,
                nameof(rewardProfileStableId));
            this.ContentFingerprint = RewardContractFormat.RequireFingerprint(
                contentFingerprint,
                nameof(contentFingerprint));
            this.canonicalText = "run_stable_id="
                + this.RunStableId
                + "\nsource_instance_stable_id="
                + this.SourceInstanceStableId
                + "\nsource_operation_stable_id="
                + this.SourceOperationStableId
                + "\ncommitment_stable_id="
                + this.CommitmentStableId
                + "\nreward_profile_stable_id="
                + this.RewardProfileStableId
                + "\ncontent_fingerprint="
                + this.ContentFingerprint;
            this.fingerprint = RewardContractFormat.Fingerprint(this.canonicalText);
        }

        public StableId RunStableId { get; }

        public StableId SourceInstanceStableId { get; }

        public StableId SourceOperationStableId { get; }

        public StableId CommitmentStableId { get; }

        public StableId RewardProfileStableId { get; }

        public string ContentFingerprint { get; }

        public string Fingerprint
        {
            get { return this.fingerprint; }
        }

        public static RewardOperationRequest Create(
            StableId runStableId,
            StableId sourceInstanceStableId,
            StableId sourceOperationStableId,
            StableId commitmentStableId,
            StableId rewardProfileStableId,
            string contentFingerprint)
        {
            return new RewardOperationRequest(
                runStableId,
                sourceInstanceStableId,
                sourceOperationStableId,
                commitmentStableId,
                rewardProfileStableId,
                contentFingerprint);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(RewardOperationRequest other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as RewardOperationRequest);
        }

        public override int GetHashCode()
        {
            return RewardContractFormat.DeterministicHash(this.canonicalText);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }
    }

    public enum RewardOperationIdentityComparison
    {
        DistinctOperation = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
    }

    /// <summary>
    /// Pure operation-identity comparison. A reused operation ID is exact only when
    /// the complete canonical request fingerprint is unchanged.
    /// </summary>
    public static class RewardOperationIdentity
    {
        public static RewardOperationIdentityComparison Classify(
            RewardOperationRequest existingRequest,
            RewardOperationRequest incomingRequest)
        {
            if (existingRequest == null)
            {
                throw new ArgumentNullException(nameof(existingRequest));
            }

            if (incomingRequest == null)
            {
                throw new ArgumentNullException(nameof(incomingRequest));
            }

            if (existingRequest.SourceOperationStableId != incomingRequest.SourceOperationStableId)
            {
                return RewardOperationIdentityComparison.DistinctOperation;
            }

            if (string.Equals(
                existingRequest.Fingerprint,
                incomingRequest.Fingerprint,
                StringComparison.Ordinal))
            {
                return RewardOperationIdentityComparison.ExactDuplicateNoChange;
            }

            return RewardOperationIdentityComparison.ConflictingDuplicate;
        }
    }

    /// <summary>
    /// One concrete immutable grant produced by a later generator.
    /// </summary>
    public sealed class RewardGrant :
        IEquatable<RewardGrant>,
        IComparable<RewardGrant>,
        IComparable
    {
        private readonly string canonicalText;
        private readonly string fingerprint;

        private RewardGrant(
            StableId grantStableId,
            RewardGrantKind kind,
            StableId contentStableId,
            long quantity)
        {
            this.GrantStableId = RewardContractFormat.RequireStableId(
                grantStableId,
                nameof(grantStableId));
            RewardContractFormat.RequireDefinedEnum(kind, nameof(kind));
            this.Kind = kind;
            this.ContentStableId = RewardContractFormat.RequireStableId(
                contentStableId,
                nameof(contentStableId));
            if (quantity < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    quantity,
                    "Generated reward quantities must be positive.");
            }

            this.Quantity = quantity;
            this.canonicalText = "grant_stable_id="
                + this.GrantStableId
                + "\nkind="
                + ((int)this.Kind).ToString(CultureInfo.InvariantCulture)
                + "\ncontent_stable_id="
                + this.ContentStableId
                + "\nquantity="
                + this.Quantity.ToString(CultureInfo.InvariantCulture);
            this.fingerprint = RewardContractFormat.Fingerprint(this.canonicalText);
        }

        public StableId GrantStableId { get; }

        public RewardGrantKind Kind { get; }

        public StableId ContentStableId { get; }

        public long Quantity { get; }

        public string Fingerprint
        {
            get { return this.fingerprint; }
        }

        public static RewardGrant Create(
            StableId grantStableId,
            RewardGrantKind kind,
            StableId contentStableId,
            long quantity)
        {
            return new RewardGrant(grantStableId, kind, contentStableId, quantity);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(RewardGrant other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as RewardGrant);
        }

        public override int GetHashCode()
        {
            return RewardContractFormat.DeterministicHash(this.canonicalText);
        }

        public int CompareTo(RewardGrant other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            return this.GrantStableId.CompareTo(other.GrantStableId);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            RewardGrant other = obj as RewardGrant;
            if (other == null)
            {
                throw new ArgumentException("Object must be a RewardGrant.", nameof(obj));
            }

            return this.CompareTo(other);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }
    }

    public enum RewardResultDisposition
    {
        Grants = 1,
        ExplicitNoDrop = 2,
    }

    /// <summary>
    /// Immutable generated reward set. Empty accidental results are rejected; no-drop
    /// must be explicit.
    /// </summary>
    public sealed class RewardResult : IEquatable<RewardResult>
    {
        private readonly ReadOnlyCollection<RewardGrant> grants;
        private readonly string canonicalText;
        private readonly string fingerprint;

        private RewardResult(
            StableId commitmentStableId,
            StableId sourceOperationStableId,
            RewardResultDisposition disposition,
            IEnumerable<RewardGrant> grants)
        {
            this.CommitmentStableId = RewardContractFormat.RequireStableId(
                commitmentStableId,
                nameof(commitmentStableId));
            this.SourceOperationStableId = RewardContractFormat.RequireStableId(
                sourceOperationStableId,
                nameof(sourceOperationStableId));
            RewardContractFormat.RequireDefinedEnum(disposition, nameof(disposition));
            this.Disposition = disposition;
            this.grants = RewardContractFormat.CopyAndSortUnique(
                grants,
                nameof(grants),
                delegate(RewardGrant item) { return item.GrantStableId; });
            if (this.Disposition == RewardResultDisposition.Grants && this.grants.Count == 0)
            {
                throw new ArgumentException(
                    "Grant reward results must contain at least one grant; use explicit no-drop instead.",
                    nameof(grants));
            }

            if (this.Disposition == RewardResultDisposition.ExplicitNoDrop && this.grants.Count != 0)
            {
                throw new ArgumentException(
                    "Explicit no-drop reward results must not contain grants.",
                    nameof(grants));
            }

            this.canonicalText = this.BuildCanonicalText();
            this.fingerprint = RewardContractFormat.Fingerprint(this.canonicalText);
        }

        public StableId CommitmentStableId { get; }

        public StableId SourceOperationStableId { get; }

        public RewardResultDisposition Disposition { get; }

        public IReadOnlyList<RewardGrant> Grants
        {
            get { return this.grants; }
        }

        public string Fingerprint
        {
            get { return this.fingerprint; }
        }

        public static RewardResult CreateGrants(
            StableId commitmentStableId,
            StableId sourceOperationStableId,
            IEnumerable<RewardGrant> grants)
        {
            return new RewardResult(
                commitmentStableId,
                sourceOperationStableId,
                RewardResultDisposition.Grants,
                grants);
        }

        public static RewardResult CreateExplicitNoDrop(
            StableId commitmentStableId,
            StableId sourceOperationStableId)
        {
            return new RewardResult(
                commitmentStableId,
                sourceOperationStableId,
                RewardResultDisposition.ExplicitNoDrop,
                Array.Empty<RewardGrant>());
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(RewardResult other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as RewardResult);
        }

        public override int GetHashCode()
        {
            return RewardContractFormat.DeterministicHash(this.canonicalText);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("commitment_stable_id=")
                .Append(this.CommitmentStableId)
                .Append("\nsource_operation_stable_id=")
                .Append(this.SourceOperationStableId)
                .Append("\ndisposition=")
                .Append(((int)this.Disposition).ToString(CultureInfo.InvariantCulture))
                .Append("\ngrant_count=")
                .Append(this.grants.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.grants.Count; index++)
            {
                builder.Append("\ngrant_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append(":\n")
                    .Append(this.grants[index].ToCanonicalString());
            }

            return builder.ToString();
        }
    }

    public enum RewardTraceDecisionKind
    {
        Guaranteed = 1,
        IndependentChance = 2,
        ExclusiveSelection = 3,
        Quantity = 4,
        ScalingInput = 5,
        ExplicitNoDrop = 6,
        GrantProduced = 7,
    }

    /// <summary>
    /// Explainable immutable trace fact. Values are recorded by a later generator;
    /// constructing a trace never consumes random state.
    /// </summary>
    public sealed class RewardTraceEntry :
        IEquatable<RewardTraceEntry>,
        IComparable<RewardTraceEntry>,
        IComparable
    {
        private readonly string canonicalText;

        private RewardTraceEntry(
            StableId traceEntryStableId,
            int ordinal,
            StableId stepStableId,
            StableId subjectStableId,
            RewardTraceDecisionKind decisionKind,
            long inputValue,
            long outputValue)
        {
            this.TraceEntryStableId = RewardContractFormat.RequireStableId(
                traceEntryStableId,
                nameof(traceEntryStableId));
            if (ordinal < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ordinal),
                    ordinal,
                    "Trace ordinals must be non-negative.");
            }

            this.Ordinal = ordinal;
            this.StepStableId = RewardContractFormat.RequireStableId(
                stepStableId,
                nameof(stepStableId));
            this.SubjectStableId = RewardContractFormat.RequireStableId(
                subjectStableId,
                nameof(subjectStableId));
            RewardContractFormat.RequireDefinedEnum(decisionKind, nameof(decisionKind));
            this.DecisionKind = decisionKind;
            this.InputValue = inputValue;
            this.OutputValue = outputValue;
            this.canonicalText = "trace_entry_stable_id="
                + this.TraceEntryStableId
                + "\nordinal="
                + this.Ordinal.ToString(CultureInfo.InvariantCulture)
                + "\nstep_stable_id="
                + this.StepStableId
                + "\nsubject_stable_id="
                + this.SubjectStableId
                + "\ndecision_kind="
                + ((int)this.DecisionKind).ToString(CultureInfo.InvariantCulture)
                + "\ninput_value="
                + this.InputValue.ToString(CultureInfo.InvariantCulture)
                + "\noutput_value="
                + this.OutputValue.ToString(CultureInfo.InvariantCulture);
        }

        public StableId TraceEntryStableId { get; }

        public int Ordinal { get; }

        public StableId StepStableId { get; }

        public StableId SubjectStableId { get; }

        public RewardTraceDecisionKind DecisionKind { get; }

        public long InputValue { get; }

        public long OutputValue { get; }

        public static RewardTraceEntry Create(
            StableId traceEntryStableId,
            int ordinal,
            StableId stepStableId,
            StableId subjectStableId,
            RewardTraceDecisionKind decisionKind,
            long inputValue,
            long outputValue)
        {
            return new RewardTraceEntry(
                traceEntryStableId,
                ordinal,
                stepStableId,
                subjectStableId,
                decisionKind,
                inputValue,
                outputValue);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(RewardTraceEntry other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as RewardTraceEntry);
        }

        public override int GetHashCode()
        {
            return RewardContractFormat.DeterministicHash(this.canonicalText);
        }

        public int CompareTo(RewardTraceEntry other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int ordinalComparison = this.Ordinal.CompareTo(other.Ordinal);
            if (ordinalComparison != 0)
            {
                return ordinalComparison;
            }

            return this.TraceEntryStableId.CompareTo(other.TraceEntryStableId);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            RewardTraceEntry other = obj as RewardTraceEntry;
            if (other == null)
            {
                throw new ArgumentException("Object must be a RewardTraceEntry.", nameof(obj));
            }

            return this.CompareTo(other);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }
    }

    public sealed class RewardTrace : IEquatable<RewardTrace>
    {
        private readonly ReadOnlyCollection<RewardTraceEntry> entries;
        private readonly string canonicalText;
        private readonly string fingerprint;

        private RewardTrace(
            StableId sourceOperationStableId,
            IEnumerable<RewardTraceEntry> entries)
        {
            this.SourceOperationStableId = RewardContractFormat.RequireStableId(
                sourceOperationStableId,
                nameof(sourceOperationStableId));
            this.entries = RewardContractFormat.CopyAndSortUnique(
                entries,
                nameof(entries),
                delegate(RewardTraceEntry item) { return item.TraceEntryStableId; });
            HashSet<int> ordinals = new HashSet<int>();
            for (int index = 0; index < this.entries.Count; index++)
            {
                if (!ordinals.Add(this.entries[index].Ordinal))
                {
                    throw new ArgumentException(
                        "Reward trace contains duplicate ordinal "
                        + this.entries[index].Ordinal.ToString(CultureInfo.InvariantCulture)
                        + ".",
                        nameof(entries));
                }
            }

            this.canonicalText = this.BuildCanonicalText();
            this.fingerprint = RewardContractFormat.Fingerprint(this.canonicalText);
        }

        public StableId SourceOperationStableId { get; }

        public IReadOnlyList<RewardTraceEntry> Entries
        {
            get { return this.entries; }
        }

        public string Fingerprint
        {
            get { return this.fingerprint; }
        }

        public static RewardTrace Create(
            StableId sourceOperationStableId,
            IEnumerable<RewardTraceEntry> entries)
        {
            return new RewardTrace(sourceOperationStableId, entries);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(RewardTrace other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as RewardTrace);
        }

        public override int GetHashCode()
        {
            return RewardContractFormat.DeterministicHash(this.canonicalText);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("source_operation_stable_id=")
                .Append(this.SourceOperationStableId)
                .Append("\nentry_count=")
                .Append(this.entries.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.entries.Count; index++)
            {
                builder.Append("\nentry_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append(":\n")
                    .Append(this.entries[index].ToCanonicalString());
            }

            return builder.ToString();
        }
    }
}

namespace ShooterMover.Contracts
{
    internal static class RewardContractFormat
    {
        private const string FingerprintPrefix = "sha256:";
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static StableId RequireStableId(StableId value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return value;
        }

        public static void RequireDefinedEnum<TEnum>(TEnum value, string parameterName)
            where TEnum : struct
        {
            if (!Enum.IsDefined(typeof(TEnum), value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be a defined contract enum member.");
            }
        }

        public static string RequireFingerprint(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (value.Length != FingerprintPrefix.Length + 64
                || !value.StartsWith(FingerprintPrefix, StringComparison.Ordinal))
            {
                throw new FormatException(
                    parameterName + " must use sha256:<64 lowercase hex characters> form.");
            }

            for (int index = FingerprintPrefix.Length; index < value.Length; index++)
            {
                char current = value[index];
                bool isDigit = current >= '0' && current <= '9';
                bool isLowerHex = current >= 'a' && current <= 'f';
                if (!isDigit && !isLowerHex)
                {
                    throw new FormatException(
                        parameterName + " must use lowercase hexadecimal SHA-256 text.");
                }
            }

            return value;
        }

        public static ReadOnlyCollection<T> CopyAndSortUnique<T>(
            IEnumerable<T> source,
            string parameterName,
            Func<T, StableId> identitySelector)
            where T : IComparable<T>
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            List<T> copy = new List<T>();
            HashSet<StableId> identities = new HashSet<StableId>();
            foreach (T item in source)
            {
                if (ReferenceEquals(item, null))
                {
                    throw new ArgumentException(
                        parameterName + " must not contain null entries.",
                        parameterName);
                }

                StableId identity = identitySelector(item);
                if (!identities.Add(identity))
                {
                    throw new ArgumentException(
                        parameterName + " contains duplicate identity " + identity + ".",
                        parameterName);
                }

                copy.Add(item);
            }

            copy.Sort();
            return new ReadOnlyCollection<T>(copy);
        }

        public static string Fingerprint(string canonicalText)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(canonicalText);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(bytes);
            }

            StringBuilder builder = new StringBuilder(FingerprintPrefix, 71);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static int DeterministicHash(string canonicalText)
        {
            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }
    }
}
