using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    public enum CollectedRunRewardTransferAuthorityStatusV1
    {
        Applied = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
    }

    public enum CollectedRunRewardTransferPersistenceStatusV1
    {
        NotAttempted = 1,
        PreparedAndVerified = 2,
        PersistedAndVerified = 3,
        AlreadyPersisted = 4,
        Rejected = 5,
        DurableStateUncertain = 6,
    }

    public enum CollectedRunRewardTransferStatusV1
    {
        Applied = 1,
        ExactReplay = 2,
        ConflictingDuplicate = 3,
        Rejected = 4,
        FatalCompensationFailure = 5,
        PreparationFailed = 6,
    }

    public sealed class PermanentRewardTransferStateV1
    {
        private readonly ReadOnlyDictionary<string, string> authorityFingerprints;
        private readonly string canonicalText;

        public PermanentRewardTransferStateV1(
            StableId selectedCharacterStableId,
            long characterRevision,
            string characterFingerprint,
            long accountRevision,
            string accountFingerprint,
            IDictionary<string, string> authorityFingerprints)
        {
            SelectedCharacterStableId = selectedCharacterStableId
                ?? throw new ArgumentNullException(nameof(selectedCharacterStableId));
            if (characterRevision < 0L) throw new ArgumentOutOfRangeException(nameof(characterRevision));
            if (accountRevision < 0L) throw new ArgumentOutOfRangeException(nameof(accountRevision));
            if (string.IsNullOrWhiteSpace(characterFingerprint))
                throw new ArgumentException("A character fingerprint is required.", nameof(characterFingerprint));
            if (string.IsNullOrWhiteSpace(accountFingerprint))
                throw new ArgumentException("An account fingerprint is required.", nameof(accountFingerprint));

            var copy = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in authorityFingerprints
                ?? throw new ArgumentNullException(nameof(authorityFingerprints)))
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                    throw new ArgumentException("Authority keys and fingerprints must be non-empty.", nameof(authorityFingerprints));
                copy.Add(pair.Key.Trim(), pair.Value.Trim());
            }
            CharacterRevision = characterRevision;
            CharacterFingerprint = characterFingerprint.Trim();
            AccountRevision = accountRevision;
            AccountFingerprint = accountFingerprint.Trim();
            this.authorityFingerprints = new ReadOnlyDictionary<string, string>(copy);

            var builder = new StringBuilder("schema=permanent-reward-transfer-state-v1");
            CollectedRunRewardTransferCanonicalV1.Append(builder, "character", SelectedCharacterStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "character-revision", CharacterRevision);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "character-fingerprint", CharacterFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "account-revision", AccountRevision);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "account-fingerprint", AccountFingerprint);
            foreach (KeyValuePair<string, string> pair in this.authorityFingerprints)
                CollectedRunRewardTransferCanonicalV1.Append(builder, "authority:" + pair.Key, pair.Value);
            canonicalText = builder.ToString();
            Fingerprint = CollectedRunRewardTransferCanonicalV1.Hash(canonicalText);
        }

        public StableId SelectedCharacterStableId { get; }
        public long CharacterRevision { get; }
        public string CharacterFingerprint { get; }
        public long AccountRevision { get; }
        public string AccountFingerprint { get; }
        public IReadOnlyDictionary<string, string> AuthorityFingerprints { get { return authorityFingerprints; } }
        public string Fingerprint { get; }
        public string ToCanonicalString() { return canonicalText; }
    }

    public sealed class CollectedRunRewardTransferPreflightResultV1
    {
        private CollectedRunRewardTransferPreflightResultV1(bool succeeded, string diagnostic)
        {
            Succeeded = succeeded;
            Diagnostic = diagnostic ?? string.Empty;
        }
        public bool Succeeded { get; }
        public string Diagnostic { get; }
        public static CollectedRunRewardTransferPreflightResultV1 Accepted()
        {
            return new CollectedRunRewardTransferPreflightResultV1(true, string.Empty);
        }
        public static CollectedRunRewardTransferPreflightResultV1 Rejected(string diagnostic)
        {
            return new CollectedRunRewardTransferPreflightResultV1(
                false,
                string.IsNullOrWhiteSpace(diagnostic)
                    ? "collected-run-transfer-preflight-rejected"
                    : diagnostic.Trim());
        }
    }

    /// <summary>
    /// Result of the one honest whole-plan permanent mutation. RAP commits and claims the
    /// complete immutable plan once; BOX registers all exact unopened contexts in the same
    /// compensation boundary. Reward identities remain audit facts, not fake child calls.
    /// </summary>
    public sealed class CollectedRunRewardAtomicApplyResultV1
    {
        private readonly ReadOnlyCollection<StableId> appliedRewardStableIds;
        private readonly ReadOnlyDictionary<string, string> authorityFingerprints;

        public CollectedRunRewardAtomicApplyResultV1(
            CollectedRunRewardTransferAuthorityStatusV1 status,
            IEnumerable<StableId> appliedRewardStableIds,
            IDictionary<string, string> authorityFingerprints,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(CollectedRunRewardTransferAuthorityStatusV1), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            var ids = new List<StableId>(appliedRewardStableIds ?? Array.Empty<StableId>());
            if (ids.Exists(item => item == null))
                throw new ArgumentException("Applied reward identities cannot contain null.", nameof(appliedRewardStableIds));
            ids.Sort();
            for (int index = 1; index < ids.Count; index++)
            {
                if (ids[index - 1] == ids[index])
                    throw new ArgumentException("Applied reward identities must be unique.", nameof(appliedRewardStableIds));
            }
            var fingerprints = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in authorityFingerprints
                ?? new Dictionary<string, string>())
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                    throw new ArgumentException("Authority fingerprints must be non-empty.", nameof(authorityFingerprints));
                fingerprints.Add(pair.Key.Trim(), pair.Value.Trim());
            }
            Status = status;
            this.appliedRewardStableIds = new ReadOnlyCollection<StableId>(ids);
            this.authorityFingerprints = new ReadOnlyDictionary<string, string>(fingerprints);
            Diagnostic = diagnostic ?? string.Empty;
        }

        public CollectedRunRewardTransferAuthorityStatusV1 Status { get; }
        public IReadOnlyList<StableId> AppliedRewardStableIds { get { return appliedRewardStableIds; } }
        public IReadOnlyDictionary<string, string> AuthorityFingerprints { get { return authorityFingerprints; } }
        public string Diagnostic { get; }
        public bool Succeeded
        {
            get
            {
                return Status == CollectedRunRewardTransferAuthorityStatusV1.Applied
                    || Status == CollectedRunRewardTransferAuthorityStatusV1.ExactReplay;
            }
        }
    }

    public sealed class CollectedRunRewardTransferReceiptRecordResultV1
    {
        public CollectedRunRewardTransferReceiptRecordResultV1(
            CollectedRunRewardTransferAuthorityStatusV1 status,
            CollectedRunRewardTransferReceiptV1 receipt,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(CollectedRunRewardTransferAuthorityStatusV1), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            Receipt = receipt;
            Diagnostic = diagnostic ?? string.Empty;
        }
        public CollectedRunRewardTransferAuthorityStatusV1 Status { get; }
        public CollectedRunRewardTransferReceiptV1 Receipt { get; }
        public string Diagnostic { get; }
        public bool Succeeded
        {
            get
            {
                return Status == CollectedRunRewardTransferAuthorityStatusV1.Applied
                    || Status == CollectedRunRewardTransferAuthorityStatusV1.ExactReplay;
            }
        }
    }

    public sealed class CollectedRunRewardTransferRestoreResultV1
    {
        public CollectedRunRewardTransferRestoreResultV1(bool restored, string diagnostic)
        {
            Restored = restored;
            Diagnostic = diagnostic ?? string.Empty;
        }
        public bool Restored { get; }
        public string Diagnostic { get; }
    }

    public sealed class CollectedRunRewardTransferPersistenceResultV1
    {
        public CollectedRunRewardTransferPersistenceResultV1(
            CollectedRunRewardTransferPersistenceStatusV1 status,
            long accountRevision,
            string accountFingerprint,
            long characterRevision,
            string characterFingerprint,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(CollectedRunRewardTransferPersistenceStatusV1), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (accountRevision < 0L) throw new ArgumentOutOfRangeException(nameof(accountRevision));
            if (characterRevision < 0L) throw new ArgumentOutOfRangeException(nameof(characterRevision));
            Status = status;
            AccountRevision = accountRevision;
            AccountFingerprint = accountFingerprint ?? string.Empty;
            CharacterRevision = characterRevision;
            CharacterFingerprint = characterFingerprint ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public CollectedRunRewardTransferPersistenceStatusV1 Status { get; }
        public long AccountRevision { get; }
        public string AccountFingerprint { get; }
        public long CharacterRevision { get; }
        public string CharacterFingerprint { get; }
        public string Diagnostic { get; }
        public bool Succeeded
        {
            get
            {
                return Status == CollectedRunRewardTransferPersistenceStatusV1.PreparedAndVerified
                    || Status == CollectedRunRewardTransferPersistenceStatusV1.PersistedAndVerified
                    || Status == CollectedRunRewardTransferPersistenceStatusV1.AlreadyPersisted;
            }
        }
        public bool DurableStateUncertain
        {
            get { return Status == CollectedRunRewardTransferPersistenceStatusV1.DurableStateUncertain; }
        }
        public static CollectedRunRewardTransferPersistenceResultV1 NotAttempted(string diagnostic)
        {
            return new CollectedRunRewardTransferPersistenceResultV1(
                CollectedRunRewardTransferPersistenceStatusV1.NotAttempted,
                0L, string.Empty, 0L, string.Empty, diagnostic);
        }
    }

    public interface ICollectedRunRewardTransferCompensationV1
    {
        string Fingerprint { get; }
    }

    public interface ICollectedRunRewardAtomicBatchAuthorityPortV1
    {
        PermanentRewardTransferStateV1 ExportState();
        bool TryGetDurableReceipt(
            StableId transferOperationStableId,
            out CollectedRunRewardTransferReceiptV1 receipt);
        bool TryGetDurableReceiptForReward(
            StableId rewardInstanceStableId,
            out CollectedRunRewardTransferReceiptV1 receipt);
        CollectedRunRewardTransferPreflightResultV1 Preflight(
            CollectedRunRewardAtomicPlanV2 plan);
        ICollectedRunRewardTransferCompensationV1 CaptureCompensation();
        CollectedRunRewardAtomicApplyResultV1 ApplyAtomicBatch(
            CollectedRunRewardAtomicPlanV2 plan);
        CollectedRunRewardTransferReceiptRecordResultV1 RecordReceipt(
            CollectedRunRewardTransferReceiptV1 receipt);
        CollectedRunRewardTransferRestoreResultV1 Restore(
            ICollectedRunRewardTransferCompensationV1 compensation);
    }

    public interface ICollectedRunRewardTransferPersistencePortV1
    {
        bool IsAvailable { get; }
        CollectedRunRewardTransferPersistenceResultV1 PersistPreparedCustody(
            CollectedRunRewardPreparedTransferV1 prepared);
        CollectedRunRewardTransferPersistenceResultV1 PersistAppliedAndVerify(
            CollectedRunRewardPreparedTransferV1 persisted,
            CollectedRunRewardTransferReceiptV1 receipt);
    }

    public sealed class CollectedRunRewardTransferResultV1
    {
        public CollectedRunRewardTransferResultV1(
            CollectedRunRewardTransferStatusV1 status,
            StableId operationStableId,
            string batchFingerprint,
            StableId runStableId,
            StableId selectedCharacterStableId,
            CollectedRunRewardTransferReceiptV1 receipt,
            PermanentRewardTransferStateV1 resultingState,
            CollectedRunRewardTransferPersistenceResultV1 persistence,
            string diagnostic,
            string compensationDiagnostic,
            bool exactRetryAllowed)
        {
            if (!Enum.IsDefined(typeof(CollectedRunRewardTransferStatusV1), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            OperationStableId = operationStableId;
            BatchFingerprint = batchFingerprint ?? string.Empty;
            RunStableId = runStableId;
            SelectedCharacterStableId = selectedCharacterStableId;
            Receipt = receipt;
            ResultingState = resultingState;
            Persistence = persistence ?? CollectedRunRewardTransferPersistenceResultV1.NotAttempted(string.Empty);
            Diagnostic = diagnostic ?? string.Empty;
            CompensationDiagnostic = compensationDiagnostic ?? string.Empty;
            ExactRetryAllowed = exactRetryAllowed;
        }

        public CollectedRunRewardTransferStatusV1 Status { get; }
        public StableId OperationStableId { get; }
        public string BatchFingerprint { get; }
        public StableId RunStableId { get; }
        public StableId SelectedCharacterStableId { get; }
        public CollectedRunRewardTransferReceiptV1 Receipt { get; }
        public PermanentRewardTransferStateV1 ResultingState { get; }
        public CollectedRunRewardTransferPersistenceResultV1 Persistence { get; }
        public string Diagnostic { get; }
        public string CompensationDiagnostic { get; }
        public bool ExactRetryAllowed { get; }
        public bool Succeeded
        {
            get
            {
                return Status == CollectedRunRewardTransferStatusV1.Applied
                    || Status == CollectedRunRewardTransferStatusV1.ExactReplay;
            }
        }
    }
}
