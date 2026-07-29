using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Application;

namespace ShooterMover.Contracts.Rewards.Application
{
    public enum RewardApplicationResultStatus
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

    public enum RewardStateAdmissionStatus
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

    public enum RewardChildApplyStatus
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

    public enum RewardApplicationImportStatus
    {
        Imported = 1,
        SnapshotRejected = 2,
        UnsupportedSchemaVersion = 3,
        AuthorityMismatch = 4,
        FingerprintMismatch = 5,
    }

    public sealed class RewardStatePreflightFact :
        IComparable<RewardStatePreflightFact>
    {
        public RewardStatePreflightFact(
            StableId transactionStableId,
            RewardStateAdmissionStatus status,
            string rejectionCode)
        {
            TransactionStableId = transactionStableId
                ?? throw new ArgumentNullException(nameof(transactionStableId));
            if (!Enum.IsDefined(typeof(RewardStateAdmissionStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            RejectionCode = rejectionCode;
        }

        public StableId TransactionStableId { get; }
        public RewardStateAdmissionStatus Status { get; }
        public string RejectionCode { get; }

        public bool CanProceed
        {
            get
            {
                return Status == RewardStateAdmissionStatus.Accepted
                    || Status == RewardStateAdmissionStatus.AlreadyApplied;
            }
        }

        public int CompareTo(RewardStatePreflightFact other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : TransactionStableId.CompareTo(other.TransactionStableId);
        }
    }

    public sealed class RewardStatePreflightResult
    {
        private readonly ReadOnlyCollection<RewardStatePreflightFact> facts;

        public RewardStatePreflightResult(
            IEnumerable<RewardStatePreflightFact> facts)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            var copy = new List<RewardStatePreflightFact>();
            var ids = new HashSet<StableId>();
            foreach (RewardStatePreflightFact fact in facts)
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
            this.facts = new ReadOnlyCollection<RewardStatePreflightFact>(copy);
        }

        public IReadOnlyList<RewardStatePreflightFact> Facts
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

    public sealed class RewardChildApplyResult
    {
        public RewardChildApplyResult(
            StableId transactionStableId,
            RewardChildApplyStatus status,
            bool originalApplied,
            string rejectionCode)
        {
            TransactionStableId = transactionStableId
                ?? throw new ArgumentNullException(nameof(transactionStableId));
            if (!Enum.IsDefined(typeof(RewardChildApplyStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            OriginalApplied = originalApplied;
            RejectionCode = rejectionCode;
        }

        public StableId TransactionStableId { get; }
        public RewardChildApplyStatus Status { get; }
        public bool OriginalApplied { get; }
        public string RejectionCode { get; }

        public bool IsConfirmedApplied
        {
            get
            {
                return Status == RewardChildApplyStatus.Applied
                    || (Status == RewardChildApplyStatus.ExactDuplicateNoChange
                        && OriginalApplied);
            }
        }
    }

    /// <summary>
    /// Defined preflight/apply port. Test doubles and real adapters both implement
    /// this contract; RAP never reaches into another authority's private state.
    /// </summary>
    public interface IRewardChildState
    {
        StableId AuthorityStableId { get; }
        long Sequence { get; }

        RewardStatePreflightResult Preflight(
            IReadOnlyList<RewardChildGrantCommand> commands);

        RewardChildApplyResult Apply(RewardChildGrantCommand command);
    }

    public sealed class RewardChildApplicationSnapshot :
        IComparable<RewardChildApplicationSnapshot>
    {
        private readonly string canonicalText;

        public RewardChildApplicationSnapshot(
            RewardChildGrantCommand command,
            RewardChildResolutionState resolutionState,
            RewardChildApplyStatus? lastApplyStatus,
            string rejectionCode)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            if (!Enum.IsDefined(typeof(RewardChildResolutionState), resolutionState))
            {
                throw new ArgumentOutOfRangeException(nameof(resolutionState));
            }

            if (lastApplyStatus.HasValue
                && !Enum.IsDefined(typeof(RewardChildApplyStatus), lastApplyStatus.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(lastApplyStatus));
            }

            ResolutionState = resolutionState;
            LastApplyStatus = lastApplyStatus;
            RejectionCode = rejectionCode;

            var builder = new StringBuilder();
            RewardApplication.AppendToken(builder, "command", Command.ToCanonicalString());
            RewardApplication.AppendToken(
                builder,
                "resolution_state",
                ((int)ResolutionState).ToString(CultureInfo.InvariantCulture));
            RewardApplication.AppendToken(
                builder,
                "last_apply_status",
                LastApplyStatus.HasValue
                    ? ((int)LastApplyStatus.Value).ToString(CultureInfo.InvariantCulture)
                    : "none");
            RewardApplication.AppendToken(builder, "rejection_code", RejectionCode ?? "none");
            canonicalText = builder.ToString();
            Fingerprint = RewardApplication.Fingerprint(canonicalText);
        }

        public RewardChildGrantCommand Command { get; }
        public RewardChildResolutionState ResolutionState { get; }
        public RewardChildApplyStatus? LastApplyStatus { get; }
        public string RejectionCode { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString() { return canonicalText; }

        public int CompareTo(RewardChildApplicationSnapshot other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : Command.TransactionStableId.CompareTo(other.Command.TransactionStableId);
        }
    }

    public sealed class RewardCommitmentSnapshot :
        IComparable<RewardCommitmentSnapshot>
    {
        private readonly ReadOnlyCollection<RewardProjectCommand> projections;
        private readonly ReadOnlyCollection<RewardChildApplicationSnapshot> children;

        public RewardCommitmentSnapshot(
            RewardCommitCommand commitCommand,
            RewardCommitmentState state,
            IEnumerable<RewardProjectCommand> projections,
            RewardClaimCommand claimCommand,
            IEnumerable<RewardChildApplicationSnapshot> children,
            RewardCancelCommand cancelCommand,
            string fingerprint)
        {
            CommitCommand = commitCommand
                ?? throw new ArgumentNullException(nameof(commitCommand));
            if (!Enum.IsDefined(typeof(RewardCommitmentState), state))
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

        public RewardCommitCommand CommitCommand { get; }
        public RewardCommitmentState State { get; }
        public IReadOnlyList<RewardProjectCommand> Projections { get { return projections; } }
        public RewardClaimCommand ClaimCommand { get; }
        public IReadOnlyList<RewardChildApplicationSnapshot> Children { get { return children; } }
        public RewardCancelCommand CancelCommand { get; }
        public string Fingerprint { get; }

        public static RewardCommitmentSnapshot CreateCanonical(
            RewardCommitCommand commitCommand,
            RewardCommitmentState state,
            IEnumerable<RewardProjectCommand> projections,
            RewardClaimCommand claimCommand,
            IEnumerable<RewardChildApplicationSnapshot> children,
            RewardCancelCommand cancelCommand)
        {
            var provisional = new RewardCommitmentSnapshot(
                commitCommand,
                state,
                projections,
                claimCommand,
                children,
                cancelCommand,
                string.Empty);
            string fingerprint = ComputeFingerprint(provisional);
            return new RewardCommitmentSnapshot(
                provisional.CommitCommand,
                provisional.State,
                provisional.Projections,
                provisional.ClaimCommand,
                provisional.Children,
                provisional.CancelCommand,
                fingerprint);
        }

        public static string ComputeFingerprint(RewardCommitmentSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var builder = new StringBuilder();
            RewardApplication.AppendToken(builder, "commit_command", snapshot.CommitCommand.ToCanonicalString());
            RewardApplication.AppendToken(builder, "state", ((int)snapshot.State).ToString(CultureInfo.InvariantCulture));
            RewardApplication.AppendToken(builder, "projection_count", snapshot.Projections.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.Projections.Count; index++)
            {
                RewardApplication.AppendToken(
                    builder,
                    "projection_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    snapshot.Projections[index].ToCanonicalString());
            }

            RewardApplication.AppendToken(
                builder,
                "claim_command",
                snapshot.ClaimCommand == null ? "none" : snapshot.ClaimCommand.ToCanonicalString());
            RewardApplication.AppendToken(builder, "child_count", snapshot.Children.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.Children.Count; index++)
            {
                RewardApplication.AppendToken(
                    builder,
                    "child_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    snapshot.Children[index].ToCanonicalString());
            }

            RewardApplication.AppendToken(
                builder,
                "cancel_command",
                snapshot.CancelCommand == null ? "none" : snapshot.CancelCommand.ToCanonicalString());
            return RewardApplication.Fingerprint(builder.ToString());
        }

        public int CompareTo(RewardCommitmentSnapshot other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : CommitCommand.CommitmentStableId.CompareTo(
                    other.CommitCommand.CommitmentStableId);
        }

        private static ReadOnlyCollection<RewardProjectCommand> CopyProjections(
            IEnumerable<RewardProjectCommand> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<RewardProjectCommand>();
            var ids = new HashSet<StableId>();
            foreach (RewardProjectCommand value in source)
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
            return new ReadOnlyCollection<RewardProjectCommand>(copy);
        }

        private static ReadOnlyCollection<RewardChildApplicationSnapshot> CopyChildren(
            IEnumerable<RewardChildApplicationSnapshot> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<RewardChildApplicationSnapshot>();
            var ids = new HashSet<StableId>();
            foreach (RewardChildApplicationSnapshot value in source)
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
            return new ReadOnlyCollection<RewardChildApplicationSnapshot>(copy);
        }
    }

    public sealed class RewardApplicationSnapshot
    {
        public const int CurrentSchemaVersion = 1;
        private readonly ReadOnlyCollection<RewardCommitmentSnapshot> commitments;

        public RewardApplicationSnapshot(
            int schemaVersion,
            StableId authorityStableId,
            long sequence,
            IEnumerable<RewardCommitmentSnapshot> commitments,
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
        public IReadOnlyList<RewardCommitmentSnapshot> Commitments { get { return commitments; } }
        public string Fingerprint { get; }

        public static RewardApplicationSnapshot CreateCanonical(
            StableId authorityStableId,
            long sequence,
            IEnumerable<RewardCommitmentSnapshot> commitments)
        {
            var provisional = new RewardApplicationSnapshot(
                CurrentSchemaVersion,
                authorityStableId,
                sequence,
                commitments,
                string.Empty);
            string fingerprint = ComputeFingerprint(provisional);
            return new RewardApplicationSnapshot(
                CurrentSchemaVersion,
                authorityStableId,
                sequence,
                provisional.Commitments,
                fingerprint);
        }

        public static string ComputeFingerprint(RewardApplicationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var builder = new StringBuilder();
            RewardApplication.AppendToken(builder, "schema_version", snapshot.SchemaVersion.ToString(CultureInfo.InvariantCulture));
            RewardApplication.AppendToken(builder, "authority_stable_id", snapshot.AuthorityStableId.ToString());
            RewardApplication.AppendToken(builder, "sequence", snapshot.Sequence.ToString(CultureInfo.InvariantCulture));
            RewardApplication.AppendToken(builder, "commitment_count", snapshot.Commitments.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.Commitments.Count; index++)
            {
                RewardApplication.AppendToken(
                    builder,
                    "commitment_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    snapshot.Commitments[index].Fingerprint);
            }

            return RewardApplication.Fingerprint(builder.ToString());
        }

        private static ReadOnlyCollection<RewardCommitmentSnapshot> CopyCommitments(
            IEnumerable<RewardCommitmentSnapshot> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<RewardCommitmentSnapshot>();
            var ids = new HashSet<StableId>();
            foreach (RewardCommitmentSnapshot value in source)
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
            return new ReadOnlyCollection<RewardCommitmentSnapshot>(copy);
        }
    }

    public sealed class RewardApplicationResult
    {
        public RewardApplicationResult(
            RewardApplicationResultStatus status,
            StableId commitmentStableId,
            RewardCommitmentState? commitmentState,
            long previousSequence,
            long currentSequence,
            string commandFingerprint,
            string rejectionCode,
            RewardCommitmentSnapshot commitmentSnapshot)
        {
            if (!Enum.IsDefined(typeof(RewardApplicationResultStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (commitmentState.HasValue
                && !Enum.IsDefined(typeof(RewardCommitmentState), commitmentState.Value))
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

        public RewardApplicationResultStatus Status { get; }
        public StableId CommitmentStableId { get; }
        public RewardCommitmentState? CommitmentState { get; }
        public long PreviousSequence { get; }
        public long CurrentSequence { get; }
        public string CommandFingerprint { get; }
        public string RejectionCode { get; }
        public RewardCommitmentSnapshot CommitmentSnapshot { get; }

        public bool ChangedState { get { return CurrentSequence > PreviousSequence; } }
    }

    public sealed class RewardApplicationImportResult
    {
        public RewardApplicationImportResult(
            RewardApplicationImportStatus status,
            string rejectionCode,
            long importedSequence)
        {
            if (!Enum.IsDefined(typeof(RewardApplicationImportStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            RejectionCode = rejectionCode;
            ImportedSequence = importedSequence;
        }

        public RewardApplicationImportStatus Status { get; }
        public string RejectionCode { get; }
        public long ImportedSequence { get; }
        public bool Succeeded { get { return Status == RewardApplicationImportStatus.Imported; } }
    }
}
