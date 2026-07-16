using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Application;

namespace ShooterMover.Contracts.Rewards.Application
{
    public enum RewardApplicationResultStatusV1
    {
        Generated = 1,
        Applied = 2,
        ExactDuplicateNoChange = 3,
        ConflictingDuplicate = 4,
        AlreadyAppliedNoChange = 5,
        Projected = 6,
        ClaimedPendingApplication = 7,
        Cancelled = 8,
        InvalidCommand = 9,
        UnknownCommitment = 10,
        InvalidStateTransition = 11,
        AuthorityMismatch = 12,
        ExpectedSequenceConflict = 13,
        InsufficientFunds = 14,
        CapacityRejected = 15,
        ChildAuthorityRejected = 16,
        SnapshotRejected = 17,
    }

    public enum RewardAuthorityAdmissionStatusV1
    {
        Accepted = 1,
        AlreadyApplied = 2,
        ConflictingDuplicate = 3,
        InvalidCommand = 4,
        AuthorityMismatch = 5,
        ExpectedSequenceConflict = 6,
        InsufficientFunds = 7,
        CapacityRejected = 8,
        Rejected = 9,
    }

    public enum RewardChildApplyStatusV1
    {
        Applied = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        InvalidCommand = 4,
        AuthorityMismatch = 5,
        ExpectedSequenceConflict = 6,
        InsufficientFunds = 7,
        CapacityRejected = 8,
        Rejected = 9,
    }

    public enum RewardApplicationImportStatusV1
    {
        Imported = 1,
        SnapshotRejected = 2,
        UnsupportedSchemaVersion = 3,
        AuthorityMismatch = 4,
        FingerprintMismatch = 5,
    }

    public sealed class RewardAuthorityPreflightFactV1 :
        IComparable<RewardAuthorityPreflightFactV1>
    {
        public RewardAuthorityPreflightFactV1(
            StableId transactionStableId,
            RewardAuthorityAdmissionStatusV1 status,
            string rejectionCode)
        {
            TransactionStableId = transactionStableId
                ?? throw new ArgumentNullException(nameof(transactionStableId));
            if (!Enum.IsDefined(typeof(RewardAuthorityAdmissionStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            RejectionCode = rejectionCode;
        }

        public StableId TransactionStableId { get; }
        public RewardAuthorityAdmissionStatusV1 Status { get; }
        public string RejectionCode { get; }

        public bool CanProceed
        {
            get
            {
                return Status == RewardAuthorityAdmissionStatusV1.Accepted
                    || Status == RewardAuthorityAdmissionStatusV1.AlreadyApplied;
            }
        }

        public int CompareTo(RewardAuthorityPreflightFactV1 other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : TransactionStableId.CompareTo(other.TransactionStableId);
        }
    }

    public sealed class RewardAuthorityPreflightResultV1
    {
        private readonly ReadOnlyCollection<RewardAuthorityPreflightFactV1> facts;

        public RewardAuthorityPreflightResultV1(
            IEnumerable<RewardAuthorityPreflightFactV1> facts)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            var copy = new List<RewardAuthorityPreflightFactV1>();
            var ids = new HashSet<StableId>();
            foreach (RewardAuthorityPreflightFactV1 fact in facts)
            {
                if (fact == null)
                {
                    throw new ArgumentException(
                        "Preflight facts must not contain null entries.",
                        nameof(facts));
                }

                if (!ids.Add(fact.TransactionStableId))
                {
                    throw new ArgumentException(
                        "Preflight facts contain duplicate transaction identity "+ fact.TransactionStableId + ".",
                        nameof(facts));
                }

                copy.Add(fact);
            }

            copy.Sort();
            this.facts = new ReadOnlyCollection<RewardAuthorityPreflightFactV1>(copy);
        }

        public IReadOnlyList<RewardAuthorityPreflightFactV1> Facts
        {
            get { return facts; }
        }

        public bool Succeeded
        {
            get
            {
                for (int index = 0; index < facts.Count; index++)
                {
                    if (!facts[index].CanProceed)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }

    public sealed class RewardChildApplyResultV1
    {
        public RewardChildApplyResultV1(
            StableId transactionStableId,
            RewardChildApplyStatusV1 status,
            bool originalApplied,
            string rejectionCode)
        {
            TransactionStableId = transactionStableId
                ?? throw new ArgumentNullException(nameof(transactionStableId));
            if (!Enum.IsDefined(typeof(RewardChildApplyStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            OriginalApplied = originalApplied;
            RejectionCode = rejectionCode;
        }

        public StableId TransactionStableId { get; }
        public RewardChildApplyStatusV1 Status { get; }
        public bool OriginalApplied { get; }
        public string RejectionCode { get; }

        public bool IsConfirmedApplied
        {
            get
            {
                return Status == RewardChildApplyStatusV1.Applied
                    || (Status == RewardChildApplyStatusV1.ExactDuplicateNoChange
                        && OriginalApplied);
            }
        }
    }

    /// <summary>
    /// Defined preflight/apply port. Test doubles and real adapters both implement
    /// this contract; RAP never reaches into another authority's private state.
    /// </summary>
    public interface IRewardChildAuthorityV1
    {
        StableId AuthorityStableId { get; }
        long Sequence { get; }

        RewardAuthorityPreflightResultV1 Preflight(
            IReadOnlyList<RewardChildGrantCommandV1> commands);

        RewardChildApplyResultV1 Apply(RewardChildGrantCommandV1 command);
    }

    public sealed class RewardChildApplicationSnapshotV1 :
        IComparable<RewardChildApplicationSnapshotV1>
    {
        private readonly string canonicalText;

        public RewardChildApplicationSnapshotV1(
            RewardChildGrantCommandV1 command,
            RewardChildResolutionStateV1 resolutionState,
            RewardChildApplyStatusV1? lastApplyStatus,
            string rejectionCode)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            if (!Enum.IsDefined(typeof(RewardChildResolutionStateV1), resolutionState))
            {
                throw new ArgumentOutOfRangeException(nameof(resolutionState));
            }

            if (lastApplyStatus.HasValue
                && !Enum.IsDefined(typeof(RewardChildApplyStatusV1), lastApplyStatus.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(lastApplyStatus));
            }

            ResolutionState = resolutionState;
            LastApplyStatus = lastApplyStatus;
            RejectionCode = rejectionCode;

            var builder = new StringBuilder();
            RewardApplicationCanonicalV1.AppendToken(builder, "command", Command.ToCanonicalString());
            RewardApplicationCanonicalV1.AppendToken(
                builder,
                "resolution_state",
                ((int)ResolutionState).ToString(CultureInfo.InvariantCulture));
            RewardApplicationCanonicalV1.AppendToken(
                builder,
                "last_apply_status",
                LastApplyStatus.HasValue
                    ? ((int)LastApplyStatus.Value).ToString(CultureInfo.InvariantCulture)
                    : "none");
            RewardApplicationCanonicalV1.AppendToken(builder, "rejection_code", RejectionCode ?? "none");
            canonicalText = builder.ToString();
            Fingerprint = RewardApplicationCanonicalV1.Fingerprint(canonicalText);
        }

        public RewardChildGrantCommandV1 Command { get; }
        public RewardChildResolutionStateV1 ResolutionState { get; }
        public RewardChildApplyStatusV1? LastApplyStatus { get; }
        public string RejectionCode { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString() { return canonicalText; }

        public int CompareTo(RewardChildApplicationSnapshotV1 other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : Command.TransactionStableId.CompareTo(other.Command.TransactionStableId);
        }
    }

    public sealed class RewardCommitmentSnapshotV1 :
        IComparable<RewardCommitmentSnapshotV1>
    {
        private readonly ReadOnlyCollection<RewardProjectCommandV1> projections;
        private readonly ReadOnlyCollection<RewardChildApplicationSnapshotV1> children;

        public RewardCommitmentSnapshotV1(
            RewardCommitCommandV1 commitCommand,
            RewardCommitmentStateV1 state,
            IEnumerable<RewardProjectCommandV1> projections,
            RewardClaimCommandV1 claimCommand,
            IEnumerable<RewardChildApplicationSnapshotV1> children,
            RewardCancelCommandV1 cancelCommand,
            string fingerprint)
        {
            CommitCommand = commitCommand
                ?? throw new ArgumentNullException(nameof(commitCommand));
            if (!Enum.IsDefined(typeof(RewardCommitmentStateV1), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            State = state;
            this.projections = CopyProjections(projections);
            ClaimCommand = claimCommand;
            this.children = CopyChildren(children);
            CancelCommand = cancelCommand;
            Fingerprint = fingerprint;
        }

        public RewardCommitCommandV1 CommitCommand { get; }
        public RewardCommitmentStateV1 State { get; }
        public IReadOnlyList<RewardProjectCommandV1> Projections { get { return projections; } }
        public RewardClaimCommandV1 ClaimCommand { get; }
        public IReadOnlyList<RewardChildApplicationSnapshotV1> Children { get { return children; } }
        public RewardCancelCommandV1 CancelCommand { get; }
        public string Fingerprint { get; }

        public static RewardCommitmentSnapshotV1 CreateCanonical(
            RewardCommitCommandV1 commitCommand,
            RewardCommitmentStateV1 state,
            IEnumerable<RewardProjectCommandV1> projections,
            RewardClaimCommandV1 claimCommand,
            IEnumerable<RewardChildApplicationSnapshotV1> children,
            RewardCancelCommandV1 cancelCommand)
        {
            var provisional = new RewardCommitmentSnapshotV1(
                commitCommand,
                state,
                projections,
                claimCommand,
                children,
                cancelCommand,
                string.Empty);
            string fingerprint = ComputeFingerprint(provisional);
            return new RewardCommitmentSnapshotV1(
                provisional.CommitCommand,
                provisional.State,
                provisional.Projections,
                provisional.ClaimCommand,
                provisional.Children,
                provisional.CancelCommand,
                fingerprint);
        }

        public static string ComputeFingerprint(RewardCommitmentSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var builder = new StringBuilder();
            RewardApplicationCanonicalV1.AppendToken(builder, "commit_command", snapshot.CommitCommand.ToCanonicalString());
            RewardApplicationCanonicalV1.AppendToken(builder, "state", ((int)snapshot.State).ToString(CultureInfo.InvariantCulture));
            RewardApplicationCanonicalV1.AppendToken(builder, "projection_count", snapshot.Projections.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.Projections.Count; index++)
            {
                RewardApplicationCanonicalV1.AppendToken(
                    builder,
                    "projection_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    snapshot.Projections[index].ToCanonicalString());
            }

            RewardApplicationCanonicalV1.AppendToken(
                builder,
                "claim_command",
                snapshot.ClaimCommand == null ? "none" : snapshot.ClaimCommand.ToCanonicalString());
            RewardApplicationCanonicalV1.AppendToken(builder, "child_count", snapshot.Children.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.Children.Count; index++)
            {
                RewardApplicationCanonicalV1.AppendToken(
                    builder,
                    "child_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    snapshot.Children[index].ToCanonicalString());
            }

            RewardApplicationCanonicalV1.AppendToken(
                builder,
                "cancel_command",
                snapshot.CancelCommand == null ? "none" : snapshot.CancelCommand.ToCanonicalString());
            return RewardApplicationCanonicalV1.Fingerprint(builder.ToString());
        }

        public int CompareTo(RewardCommitmentSnapshotV1 other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : CommitCommand.CommitmentStableId.CompareTo(
                    other.CommitCommand.CommitmentStableId);
        }

        private static ReadOnlyCollection<RewardProjectCommandV1> CopyProjections(
            IEnumerable<RewardProjectCommandV1> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<RewardProjectCommandV1>();
            var ids = new HashSet<StableId>();
            foreach (RewardProjectCommandV1 value in source)
            {
                if (value == null || !ids.Add(value.ProjectionStableId))
                {
                    throw new ArgumentException(
                        "Projection snapshots must be non-null with unique identities.",
                        nameof(source));
                }

                copy.Add(value);
            }

            copy.Sort();
            return new ReadOnlyCollection<RewardProjectCommandV1>(copy);
        }

        private static ReadOnlyCollection<RewardChildApplicationSnapshotV1> CopyChildren(
            IEnumerable<RewardChildApplicationSnapshotV1> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<RewardChildApplicationSnapshotV1>();
            var ids = new HashSet<StableId>();
            foreach (RewardChildApplicationSnapshotV1 value in source)
            {
                if (value == null || !ids.Add(value.Command.TransactionStableId))
                {
                    throw new ArgumentException(
                        "Child snapshots must be non-null with unique transaction identities.",
                        nameof(source));
                }

                copy.Add(value);
            }

            copy.Sort();
            return new ReadOnlyCollection<RewardChildApplicationSnapshotV1>(copy);
        }
    }

    public sealed class RewardApplicationSnapshotV1
    {
        public const int CurrentSchemaVersion = 1;
        private readonly ReadOnlyCollection<RewardCommitmentSnapshotV1> commitments;

        public RewardApplicationSnapshotV1(
            int schemaVersion,
            StableId authorityStableId,
            long sequence,
            IEnumerable<RewardCommitmentSnapshotV1> commitments,
            string fingerprint)
        {
            SchemaVersion = schemaVersion;
            AuthorityStableId = authorityStableId
                ?? throw new ArgumentNullException(nameof(authorityStableId));
            Sequence = sequence;
            this.commitments = CopyCommitments(commitments);
            Fingerprint = fingerprint;
        }

        public int SchemaVersion { get; }
        public StableId AuthorityStableId { get; }
        public long Sequence { get; }
        public IReadOnlyList<RewardCommitmentSnapshotV1> Commitments { get { return commitments; } }
        public string Fingerprint { get; }

        public static RewardApplicationSnapshotV1 CreateCanonical(
            StableId authorityStableId,
            long sequence,
            IEnumerable<RewardCommitmentSnapshotV1> commitments)
        {
            var provisional = new RewardApplicationSnapshotV1(
                CurrentSchemaVersion,
                authorityStableId,
                sequence,
                commitments,
                string.Empty);
            string fingerprint = ComputeFingerprint(provisional);
            return new RewardApplicationSnapshotV1(
                CurrentSchemaVersion,
                authorityStableId,
                sequence,
                provisional.Commitments,
                fingerprint);
        }

        public static string ComputeFingerprint(RewardApplicationSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var builder = new StringBuilder();
            RewardApplicationCanonicalV1.AppendToken(builder, "schema_version", snapshot.SchemaVersion.ToString(CultureInfo.InvariantCulture));
            RewardApplicationCanonicalV1.AppendToken(builder, "authority_stable_id", snapshot.AuthorityStableId.ToString());
            RewardApplicationCanonicalV1.AppendToken(builder, "sequence", snapshot.Sequence.ToString(CultureInfo.InvariantCulture));
            RewardApplicationCanonicalV1.AppendToken(builder, "commitment_count", snapshot.Commitments.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.Commitments.Count; index++)
            {
                RewardApplicationCanonicalV1.AppendToken(
                    builder,
                    "commitment_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    snapshot.Commitments[index].Fingerprint);
            }

            return RewardApplicationCanonicalV1.Fingerprint(builder.ToString());
        }

        private static ReadOnlyCollection<RewardCommitmentSnapshotV1> CopyCommitments(
            IEnumerable<RewardCommitmentSnapshotV1> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<RewardCommitmentSnapshotV1>();
            var ids = new HashSet<StableId>();
            foreach (RewardCommitmentSnapshotV1 value in source)
            {
                if (value == null
                    || !ids.Add(value.CommitCommand.CommitmentStableId))
                {
                    throw new ArgumentException(
                        "Commitment snapshots must be non-null with unique identities.",
                        nameof(source));
                }

                copy.Add(value);
            }

            copy.Sort();
            return new ReadOnlyCollection<RewardCommitmentSnapshotV1>(copy);
        }
    }

    public sealed class RewardApplicationResultV1
    {
        public RewardApplicationResultV1(
            RewardApplicationResultStatusV1 status,
            StableId commitmentStableId,
            RewardCommitmentStateV1? commitmentState,
            long previousSequence,
            long currentSequence,
            string commandFingerprint,
            string rejectionCode,
            RewardCommitmentSnapshotV1 commitmentSnapshot)
        {
            if (!Enum.IsDefined(typeof(RewardApplicationResultStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (commitmentState.HasValue
                && !Enum.IsDefined(typeof(RewardCommitmentStateV1), commitmentState.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(commitmentState));
            }

            if (previousSequence < 0L || currentSequence < previousSequence)
            {
                throw new ArgumentOutOfRangeException(nameof(currentSequence));
            }

            Status = status;
            CommitmentStableId = commitmentStableId;
            CommitmentState = commitmentState;
            PreviousSequence = previousSequence;
            CurrentSequence = currentSequence;
            CommandFingerprint = commandFingerprint;
            RejectionCode = rejectionCode;
            CommitmentSnapshot = commitmentSnapshot;
        }

        public RewardApplicationResultStatusV1 Status { get; }
        public StableId CommitmentStableId { get; }
        public RewardCommitmentStateV1? CommitmentState { get; }
        public long PreviousSequence { get; }
        public long CurrentSequence { get; }
        public string CommandFingerprint { get; }
        public string RejectionCode { get; }
        public RewardCommitmentSnapshotV1 CommitmentSnapshot { get; }

        public bool ChangedState { get { return CurrentSequence > PreviousSequence; } }
    }

    public sealed class RewardApplicationImportResultV1
    {
        public RewardApplicationImportResultV1(
            RewardApplicationImportStatusV1 status,
            string rejectionCode,
            long importedSequence)
        {
            if (!Enum.IsDefined(typeof(RewardApplicationImportStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            RejectionCode = rejectionCode;
            ImportedSequence = importedSequence;
        }

        public RewardApplicationImportStatusV1 Status { get; }
        public string RejectionCode { get; }
        public long ImportedSequence { get; }
        public bool Succeeded { get { return Status == RewardApplicationImportStatusV1.Imported; } }
    }
}
