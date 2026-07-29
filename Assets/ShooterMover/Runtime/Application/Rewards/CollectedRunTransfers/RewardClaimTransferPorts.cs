using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    public enum RewardClaimTransferStateStatus
    {
        Applied = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
    }

    public enum RewardClaimTransferPersistenceStatus
    {
        NotAttempted = 1,
        PreparedAndVerified = 2,
        PersistedAndVerified = 3,
        AlreadyPersisted = 4,
        RejectedBeforeReplacement = 5,
        Rejected = RejectedBeforeReplacement,
        DurableStateUncertain = 6,
    }

    public enum RewardClaimTransferStatus
    {
        Applied = 1,
        ExactReplay = 2,
        ConflictingDuplicate = 3,
        Rejected = 4,
        FatalCompensationFailure = 5,
        PreparationFailed = 6,
    }

    public sealed class PermanentRewardTransferState
    {
        private readonly ReadOnlyDictionary<string, string> authorityFingerprints;
        private readonly string canonicalText;

        public PermanentRewardTransferState(
            StableId selectedCharacterStableId,
            long characterRevision,
            string characterFingerprint,
            long accountRevision,
            string accountFingerprint,
            IDictionary<string, string> authorityFingerprints)
        {
            SelectedCharacterStableId = selectedCharacterStableId
                ?? throw new ArgumentNullException(nameof(selectedCharacterStableId));
            if (characterRevision < 0L)
                throw new ArgumentOutOfRangeException(nameof(characterRevision));
            if (accountRevision < 0L)
                throw new ArgumentOutOfRangeException(nameof(accountRevision));
            if (string.IsNullOrWhiteSpace(characterFingerprint))
                throw new ArgumentException(
                    "A character fingerprint is required.",
                    nameof(characterFingerprint));
            if (string.IsNullOrWhiteSpace(accountFingerprint))
                throw new ArgumentException(
                    "An account fingerprint is required.",
                    nameof(accountFingerprint));

            var copy = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in authorityFingerprints
                ?? throw new ArgumentNullException(nameof(authorityFingerprints)))
            {
                if (string.IsNullOrWhiteSpace(pair.Key)
                    || string.IsNullOrWhiteSpace(pair.Value))
                {
                    throw new ArgumentException(
                        "Authority keys and fingerprints must be non-empty.",
                        nameof(authorityFingerprints));
                }
                copy.Add(pair.Key.Trim(), pair.Value.Trim());
            }
            CharacterRevision = characterRevision;
            CharacterFingerprint = characterFingerprint.Trim();
            AccountRevision = accountRevision;
            AccountFingerprint = accountFingerprint.Trim();
            this.authorityFingerprints =
                new ReadOnlyDictionary<string, string>(copy);

            var builder = new StringBuilder(
                "schema=permanent-reward-transfer-state-v1");
            RewardClaimTransfer.Append(
                builder,
                "character",
                SelectedCharacterStableId);
            RewardClaimTransfer.Append(
                builder,
                "character-revision",
                CharacterRevision);
            RewardClaimTransfer.Append(
                builder,
                "character-fingerprint",
                CharacterFingerprint);
            RewardClaimTransfer.Append(
                builder,
                "account-revision",
                AccountRevision);
            RewardClaimTransfer.Append(
                builder,
                "account-fingerprint",
                AccountFingerprint);
            foreach (KeyValuePair<string, string> pair in
                this.authorityFingerprints)
            {
                RewardClaimTransfer.Append(
                    builder,
                    "authority:" + pair.Key,
                    pair.Value);
            }
            canonicalText = builder.ToString();
            Fingerprint =
                RewardClaimTransfer.Hash(canonicalText);
        }

        public StableId SelectedCharacterStableId { get; }
        public long CharacterRevision { get; }
        public string CharacterFingerprint { get; }
        public long AccountRevision { get; }
        public string AccountFingerprint { get; }
        public IReadOnlyDictionary<string, string> AuthorityFingerprints
        {
            get { return authorityFingerprints; }
        }
        public string Fingerprint { get; }
        public string ToCanonicalString() { return canonicalText; }
    }

    public sealed class RewardClaimTransferPreflightResult
    {
        private RewardClaimTransferPreflightResult(
            bool succeeded,
            string diagnostic)
        {
            Succeeded = succeeded;
            Diagnostic = diagnostic ?? string.Empty;
        }
        public bool Succeeded { get; }
        public string Diagnostic { get; }
        public static RewardClaimTransferPreflightResult Accepted()
        {
            return new RewardClaimTransferPreflightResult(
                true,
                string.Empty);
        }
        public static RewardClaimTransferPreflightResult Rejected(
            string diagnostic)
        {
            return new RewardClaimTransferPreflightResult(
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
    public sealed class RewardClaimAtomicApplyResult
    {
        private readonly ReadOnlyCollection<StableId> appliedRewardStableIds;
        private readonly ReadOnlyDictionary<string, string> authorityFingerprints;

        public RewardClaimAtomicApplyResult(
            RewardClaimTransferStateStatus status,
            IEnumerable<StableId> appliedRewardStableIds,
            IDictionary<string, string> authorityFingerprints,
            string diagnostic)
        {
            if (!Enum.IsDefined(
                typeof(RewardClaimTransferStateStatus),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            var ids = new List<StableId>(
                appliedRewardStableIds ?? Array.Empty<StableId>());
            if (ids.Exists(item => item == null))
            {
                throw new ArgumentException(
                    "Applied reward identities cannot contain null.",
                    nameof(appliedRewardStableIds));
            }
            ids.Sort();
            for (int index = 1; index < ids.Count; index++)
            {
                if (ids[index - 1] == ids[index])
                {
                    throw new ArgumentException(
                        "Applied reward identities must be unique.",
                        nameof(appliedRewardStableIds));
                }
            }
            var fingerprints = new SortedDictionary<string, string>(
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in
                authorityFingerprints ?? new Dictionary<string, string>())
            {
                if (string.IsNullOrWhiteSpace(pair.Key)
                    || string.IsNullOrWhiteSpace(pair.Value))
                {
                    throw new ArgumentException(
                        "Authority fingerprints must be non-empty.",
                        nameof(authorityFingerprints));
                }
                fingerprints.Add(pair.Key.Trim(), pair.Value.Trim());
            }
            Status = status;
            this.appliedRewardStableIds =
                new ReadOnlyCollection<StableId>(ids);
            this.authorityFingerprints =
                new ReadOnlyDictionary<string, string>(fingerprints);
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RewardClaimTransferStateStatus Status { get; }
        public IReadOnlyList<StableId> AppliedRewardStableIds
        {
            get { return appliedRewardStableIds; }
        }
        public IReadOnlyDictionary<string, string> AuthorityFingerprints
        {
            get { return authorityFingerprints; }
        }
        public string Diagnostic { get; }
        public bool Succeeded
        {
            get
            {
                return Status
                        == RewardClaimTransferStateStatus.Applied
                    || Status
                        == RewardClaimTransferStateStatus
                            .ExactReplay;
            }
        }
    }

    public sealed class RewardClaimTransferReceiptRecordResult
    {
        public RewardClaimTransferReceiptRecordResult(
            RewardClaimTransferStateStatus status,
            RewardClaimTransferReceipt receipt,
            string diagnostic)
        {
            if (!Enum.IsDefined(
                typeof(RewardClaimTransferStateStatus),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            Receipt = receipt;
            Diagnostic = diagnostic ?? string.Empty;
        }
        public RewardClaimTransferStateStatus Status { get; }
        public RewardClaimTransferReceipt Receipt { get; }
        public string Diagnostic { get; }
        public bool Succeeded
        {
            get
            {
                return Status
                        == RewardClaimTransferStateStatus.Applied
                    || Status
                        == RewardClaimTransferStateStatus
                            .ExactReplay;
            }
        }
    }

    public sealed class RewardClaimTransferRestoreResult
    {
        public RewardClaimTransferRestoreResult(
            bool restored,
            string diagnostic)
        {
            Restored = restored;
            Diagnostic = diagnostic ?? string.Empty;
        }
        public bool Restored { get; }
        public string Diagnostic { get; }
    }

    public sealed class RewardClaimTransferPersistenceResult
    {
        public RewardClaimTransferPersistenceResult(
            RewardClaimTransferPersistenceStatus status,
            long accountRevision,
            string accountFingerprint,
            long characterRevision,
            string characterFingerprint,
            string diagnostic)
        {
            if (!Enum.IsDefined(
                typeof(RewardClaimTransferPersistenceStatus),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (accountRevision < 0L)
                throw new ArgumentOutOfRangeException(nameof(accountRevision));
            if (characterRevision < 0L)
                throw new ArgumentOutOfRangeException(nameof(characterRevision));
            Status = status;
            AccountRevision = accountRevision;
            AccountFingerprint = accountFingerprint ?? string.Empty;
            CharacterRevision = characterRevision;
            CharacterFingerprint = characterFingerprint ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RewardClaimTransferPersistenceStatus Status { get; }
        public long AccountRevision { get; }
        public string AccountFingerprint { get; }
        public long CharacterRevision { get; }
        public string CharacterFingerprint { get; }
        public string Diagnostic { get; }
        public bool Succeeded
        {
            get
            {
                return Status
                        == RewardClaimTransferPersistenceStatus
                            .PreparedAndVerified
                    || Status
                        == RewardClaimTransferPersistenceStatus
                            .PersistedAndVerified
                    || Status
                        == RewardClaimTransferPersistenceStatus
                            .AlreadyPersisted;
            }
        }
        public bool RejectedBeforeReplacement
        {
            get
            {
                return Status
                    == RewardClaimTransferPersistenceStatus
                        .RejectedBeforeReplacement;
            }
        }
        public bool DurableStateUncertain
        {
            get
            {
                return Status
                    == RewardClaimTransferPersistenceStatus
                        .DurableStateUncertain;
            }
        }
        public static RewardClaimTransferPersistenceResult
            NotAttempted(string diagnostic)
        {
            return new RewardClaimTransferPersistenceResult(
                RewardClaimTransferPersistenceStatus.NotAttempted,
                0L,
                string.Empty,
                0L,
                string.Empty,
                diagnostic);
        }
    }

    public interface IRewardClaimTransferCompensation
    {
        string Fingerprint { get; }
    }

    public interface IRewardClaimAtomicBatchStatePort
    {
        PermanentRewardTransferState ExportState();
        bool TryGetDurableReceipt(
            StableId transferOperationStableId,
            out RewardClaimTransferReceipt receipt);
        bool TryGetDurableReceiptForReward(
            StableId rewardInstanceStableId,
            out RewardClaimTransferReceipt receipt);
        RewardClaimTransferPreflightResult Preflight(
            RewardClaimAtomicPlan plan);
        IRewardClaimTransferCompensation CaptureCompensation();
        RewardClaimAtomicApplyResult ApplyAtomicBatch(
            RewardClaimAtomicPlan plan);
        RewardClaimTransferReceiptRecordResult RecordReceipt(
            RewardClaimTransferReceipt receipt);
        RewardClaimTransferRestoreResult Restore(
            IRewardClaimTransferCompensation compensation);
    }

    public interface IRewardClaimTransferPersistencePort
    {
        bool IsAvailable { get; }
        RewardClaimTransferPersistenceResult PersistPreparedCustody(
            RewardClaimPreparedTransfer prepared);
        RewardClaimTransferPersistenceResult PersistAppliedAndVerify(
            RewardClaimPreparedTransfer persisted,
            RewardClaimTransferReceipt receipt);
    }

    public sealed class RewardClaimTransferResult
    {
        public RewardClaimTransferResult(
            RewardClaimTransferStatus status,
            StableId operationStableId,
            string batchFingerprint,
            StableId runStableId,
            StableId selectedCharacterStableId,
            RewardClaimTransferReceipt receipt,
            PermanentRewardTransferState resultingState,
            RewardClaimTransferPersistenceResult persistence,
            string diagnostic,
            string compensationDiagnostic,
            bool exactRetryAllowed)
        {
            if (!Enum.IsDefined(
                typeof(RewardClaimTransferStatus),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            OperationStableId = operationStableId;
            BatchFingerprint = batchFingerprint ?? string.Empty;
            RunStableId = runStableId;
            SelectedCharacterStableId = selectedCharacterStableId;
            Receipt = receipt;
            ResultingState = resultingState;
            Persistence = persistence
                ?? RewardClaimTransferPersistenceResult
                    .NotAttempted(string.Empty);
            Diagnostic = diagnostic ?? string.Empty;
            CompensationDiagnostic = compensationDiagnostic ?? string.Empty;
            ExactRetryAllowed = exactRetryAllowed;
        }

        public RewardClaimTransferStatus Status { get; }
        public StableId OperationStableId { get; }
        public string BatchFingerprint { get; }
        public StableId RunStableId { get; }
        public StableId SelectedCharacterStableId { get; }
        public RewardClaimTransferReceipt Receipt { get; }
        public PermanentRewardTransferState ResultingState { get; }
        public RewardClaimTransferPersistenceResult Persistence { get; }
        public string Diagnostic { get; }
        public string CompensationDiagnostic { get; }
        public bool ExactRetryAllowed { get; }
        public bool Succeeded
        {
            get
            {
                return Status == RewardClaimTransferStatus.Applied
                    || Status
                        == RewardClaimTransferStatus.ExactReplay;
            }
        }
    }
}
