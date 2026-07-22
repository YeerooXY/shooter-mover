using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
        PersistedAndVerified = 2,
        AlreadyPersisted = 3,
        Rejected = 4,
        VerificationMismatch = 5,
    }

    public enum CollectedRunRewardTransferStatusV1
    {
        Applied = 1,
        ExactReplay = 2,
        ConflictingDuplicate = 3,
        Rejected = 4,
        FatalCompensationFailure = 5,
    }

    public sealed class PermanentRewardTransferStateV1
    {
        private readonly ReadOnlyDictionary<string, string>
            authorityFingerprints;
        private readonly string canonicalText;

        public PermanentRewardTransferStateV1(
            StableId selectedCharacterStableId,
            long characterRevision,
            string characterFingerprint,
            long accountRevision,
            string accountFingerprint,
            IDictionary<string, string> authorityFingerprints)
        {
            SelectedCharacterStableId =
                selectedCharacterStableId
                ?? throw new ArgumentNullException(
                    nameof(selectedCharacterStableId));
            if (characterRevision < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(characterRevision));
            if (accountRevision < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(accountRevision));
            if (string.IsNullOrWhiteSpace(
                characterFingerprint))
            {
                throw new ArgumentException(
                    "A character fingerprint is required.",
                    nameof(characterFingerprint));
            }
            if (string.IsNullOrWhiteSpace(accountFingerprint))
                throw new ArgumentException(
                    "An account fingerprint is required.",
                    nameof(accountFingerprint));

            var copy = new SortedDictionary<string, string>(
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in
                authorityFingerprints
                ?? throw new ArgumentNullException(
                    nameof(authorityFingerprints)))
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
            CharacterFingerprint =
                characterFingerprint.Trim();
            AccountRevision = accountRevision;
            AccountFingerprint = accountFingerprint.Trim();
            this.authorityFingerprints =
                new ReadOnlyDictionary<string, string>(copy);

            var builder = new StringBuilder(
                "schema=permanent-reward-transfer-state-v1");
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "character",
                SelectedCharacterStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "character-revision",
                CharacterRevision);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "character-fingerprint",
                CharacterFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "account-revision",
                AccountRevision);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "account-fingerprint",
                AccountFingerprint);
            foreach (KeyValuePair<string, string> pair in
                this.authorityFingerprints)
            {
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "authority:" + pair.Key,
                    pair.Value);
            }
            canonicalText = builder.ToString();
            Fingerprint =
                CollectedRunRewardTransferCanonicalV1.Hash(
                    canonicalText);
        }

        public StableId SelectedCharacterStableId { get; }
        public long CharacterRevision { get; }
        public string CharacterFingerprint { get; }
        public long AccountRevision { get; }
        public string AccountFingerprint { get; }
        public IReadOnlyDictionary<string, string>
            AuthorityFingerprints
        {
            get { return authorityFingerprints; }
        }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }
    }

    public sealed class CollectedRunRewardTransferPreflightResultV1
    {
        private CollectedRunRewardTransferPreflightResultV1(
            bool succeeded,
            string diagnostic)
        {
            Succeeded = succeeded;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Diagnostic { get; }

        public static CollectedRunRewardTransferPreflightResultV1
            Accepted()
        {
            return new CollectedRunRewardTransferPreflightResultV1(
                true,
                string.Empty);
        }

        public static CollectedRunRewardTransferPreflightResultV1
            Rejected(string diagnostic)
        {
            return new CollectedRunRewardTransferPreflightResultV1(
                false,
                string.IsNullOrWhiteSpace(diagnostic)
                    ? "collected-run-transfer-preflight-rejected"
                    : diagnostic.Trim());
        }
    }

    public sealed class CollectedRunRewardTransferChildCommandV1
    {
        private readonly string canonicalText;

        public CollectedRunRewardTransferChildCommandV1(
            CollectedRunRewardTransferBatchV1 batch,
            CollectedRunRewardTransferItemV1 reward,
            int canonicalOrdinal,
            string authorityTarget)
        {
            Batch = batch
                ?? throw new ArgumentNullException(nameof(batch));
            Reward = reward
                ?? throw new ArgumentNullException(nameof(reward));
            if (canonicalOrdinal < 0
                || canonicalOrdinal >= batch.Rewards.Count
                || batch.Rewards[canonicalOrdinal]
                    .RewardInstanceStableId
                    != reward.RewardInstanceStableId
                || !string.Equals(
                    batch.Rewards[canonicalOrdinal].Fingerprint,
                    reward.Fingerprint,
                    StringComparison.Ordinal))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(canonicalOrdinal));
            }
            if (string.IsNullOrWhiteSpace(authorityTarget))
                throw new ArgumentException(
                    "An authority target is required.",
                    nameof(authorityTarget));

            CanonicalOrdinal = canonicalOrdinal;
            AuthorityTarget = authorityTarget.Trim();
            OperationStableId =
                batch.DeriveChildOperationStableId(
                    reward,
                    AuthorityTarget);
            TransactionStableId =
                batch.DeriveChildTransactionStableId(
                    reward,
                    AuthorityTarget);

            var builder = new StringBuilder(
                "schema=collected-run-reward-transfer-child-v1");
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "batch",
                Batch.Fingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "reward",
                Reward.Fingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "ordinal",
                CanonicalOrdinal);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "authority",
                AuthorityTarget);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "operation",
                OperationStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "transaction",
                TransactionStableId);
            canonicalText = builder.ToString();
            Fingerprint =
                CollectedRunRewardTransferCanonicalV1.Hash(
                    canonicalText);
        }

        public CollectedRunRewardTransferBatchV1 Batch { get; }
        public CollectedRunRewardTransferItemV1 Reward { get; }
        public int CanonicalOrdinal { get; }
        public string AuthorityTarget { get; }
        public StableId OperationStableId { get; }
        public StableId TransactionStableId { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }
    }

    public sealed class CollectedRunRewardTransferChildResultV1
    {
        public CollectedRunRewardTransferChildResultV1(
            CollectedRunRewardTransferAuthorityStatusV1 status,
            CollectedRunRewardTransferChildCommandV1 command,
            string resultingAuthorityFingerprint,
            string diagnostic)
        {
            if (!Enum.IsDefined(
                typeof(
                    CollectedRunRewardTransferAuthorityStatusV1),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            Command = command;
            ResultingAuthorityFingerprint =
                resultingAuthorityFingerprint
                ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public CollectedRunRewardTransferAuthorityStatusV1
            Status { get; }
        public CollectedRunRewardTransferChildCommandV1 Command
        {
            get;
        }
        public string ResultingAuthorityFingerprint { get; }
        public string Diagnostic { get; }
        public bool Succeeded
        {
            get
            {
                return Status
                        == CollectedRunRewardTransferAuthorityStatusV1
                            .Applied
                    || Status
                        == CollectedRunRewardTransferAuthorityStatusV1
                            .ExactReplay;
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
            if (!Enum.IsDefined(
                typeof(
                    CollectedRunRewardTransferAuthorityStatusV1),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            Receipt = receipt;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public CollectedRunRewardTransferAuthorityStatusV1
            Status { get; }
        public CollectedRunRewardTransferReceiptV1 Receipt { get; }
        public string Diagnostic { get; }
        public bool Succeeded
        {
            get
            {
                return Status
                        == CollectedRunRewardTransferAuthorityStatusV1
                            .Applied
                    || Status
                        == CollectedRunRewardTransferAuthorityStatusV1
                            .ExactReplay;
            }
        }
    }

    public sealed class CollectedRunRewardTransferRestoreResultV1
    {
        public CollectedRunRewardTransferRestoreResultV1(
            bool restored,
            string diagnostic)
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
            if (!Enum.IsDefined(
                typeof(
                    CollectedRunRewardTransferPersistenceStatusV1),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (accountRevision < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(accountRevision));
            if (characterRevision < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(characterRevision));

            Status = status;
            AccountRevision = accountRevision;
            AccountFingerprint = accountFingerprint
                ?? string.Empty;
            CharacterRevision = characterRevision;
            CharacterFingerprint = characterFingerprint
                ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public CollectedRunRewardTransferPersistenceStatusV1
            Status { get; }
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
                        == CollectedRunRewardTransferPersistenceStatusV1
                            .PersistedAndVerified
                    || Status
                        == CollectedRunRewardTransferPersistenceStatusV1
                            .AlreadyPersisted;
            }
        }

        public static CollectedRunRewardTransferPersistenceResultV1
            NotAttempted(string diagnostic)
        {
            return new CollectedRunRewardTransferPersistenceResultV1(
                CollectedRunRewardTransferPersistenceStatusV1
                    .NotAttempted,
                0L,
                string.Empty,
                0L,
                string.Empty,
                diagnostic);
        }
    }

    public interface ICollectedRunRewardTransferCompensationV1
    {
        string Fingerprint { get; }
    }

    /// <summary>
    /// Narrow orchestration port over the existing money, scrap, holdings/equipment,
    /// strongbox and durable receipt authorities. Implementations must not own replacement
    /// state; they only validate, apply and restore the existing selected-character graph.
    /// </summary>
    public interface ICollectedRunRewardTransferAuthorityPortV1
    {
        PermanentRewardTransferStateV1 ExportState();

        bool TryGetDurableReceipt(
            StableId transferOperationStableId,
            out CollectedRunRewardTransferReceiptV1 receipt);

        bool TryGetDurableReceiptForReward(
            StableId rewardInstanceStableId,
            out CollectedRunRewardTransferReceiptV1 receipt);

        CollectedRunRewardTransferPreflightResultV1 Preflight(
            CollectedRunRewardTransferBatchV1 batch);

        string ResolveAuthorityTarget(
            CollectedRunRewardTransferItemV1 reward);

        ICollectedRunRewardTransferCompensationV1
            CaptureCompensation();

        CollectedRunRewardTransferChildResultV1 Apply(
            CollectedRunRewardTransferChildCommandV1 command);

        CollectedRunRewardTransferReceiptRecordResultV1
            RecordReceipt(
                CollectedRunRewardTransferReceiptV1 receipt);

        CollectedRunRewardTransferRestoreResultV1 Restore(
            ICollectedRunRewardTransferCompensationV1
                compensation);
    }

    /// <summary>
    /// Existing account-save seam. PersistAndVerify must atomically save the complete
    /// selected character, verify the active persisted account, and prove the exact receipt
    /// is present before reporting success.
    /// </summary>
    public interface ICollectedRunRewardTransferPersistencePortV1
    {
        bool IsAvailable { get; }

        CollectedRunRewardTransferPersistenceResultV1
            PersistAndVerify(
                StableId saveOperationStableId,
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
            CollectedRunRewardTransferPersistenceResultV1
                persistence,
            string diagnostic,
            string compensationDiagnostic,
            bool exactRetryAllowed)
        {
            if (!Enum.IsDefined(
                typeof(CollectedRunRewardTransferStatusV1),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            OperationStableId = operationStableId;
            BatchFingerprint = batchFingerprint ?? string.Empty;
            RunStableId = runStableId;
            SelectedCharacterStableId =
                selectedCharacterStableId;
            Receipt = receipt;
            ResultingState = resultingState;
            Persistence = persistence
                ?? CollectedRunRewardTransferPersistenceResultV1
                    .NotAttempted(string.Empty);
            Diagnostic = diagnostic ?? string.Empty;
            CompensationDiagnostic =
                compensationDiagnostic ?? string.Empty;
            ExactRetryAllowed = exactRetryAllowed;
        }

        public CollectedRunRewardTransferStatusV1 Status { get; }
        public StableId OperationStableId { get; }
        public string BatchFingerprint { get; }
        public StableId RunStableId { get; }
        public StableId SelectedCharacterStableId { get; }
        public CollectedRunRewardTransferReceiptV1 Receipt { get; }
        public PermanentRewardTransferStateV1 ResultingState { get; }
        public CollectedRunRewardTransferPersistenceResultV1
            Persistence { get; }
        public string Diagnostic { get; }
        public string CompensationDiagnostic { get; }
        public bool ExactRetryAllowed { get; }
        public bool Succeeded
        {
            get
            {
                return Status
                        == CollectedRunRewardTransferStatusV1
                            .Applied
                    || Status
                        == CollectedRunRewardTransferStatusV1
                            .ExactReplay;
            }
        }

        public CollectedRunRewardTransferResultV1
            AsExactReplay(
                PermanentRewardTransferStateV1 currentState,
                CollectedRunRewardTransferPersistenceResultV1
                    persistence)
        {
            return new CollectedRunRewardTransferResultV1(
                CollectedRunRewardTransferStatusV1.ExactReplay,
                OperationStableId,
                BatchFingerprint,
                RunStableId,
                SelectedCharacterStableId,
                Receipt,
                currentState ?? ResultingState,
                persistence ?? Persistence,
                string.Empty,
                string.Empty,
                false);
        }
    }
}
