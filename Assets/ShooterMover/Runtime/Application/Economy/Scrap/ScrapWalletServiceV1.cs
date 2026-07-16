using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Contracts.Economy;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Economy.Scrap;

namespace ShooterMover.Application.Economy.Scrap
{
    public sealed class ScrapChangeFactV1
    {
        private readonly string _fingerprint;

        internal ScrapChangeFactV1(
            ScrapTransactionCommandV1 command,
            EconomyTransactionStatusV1 status,
            LedgerMutationStatus originalLedgerStatus,
            long originalSequenceBefore,
            long originalSequenceAfter,
            long originalPreviousBalance,
            long originalResultingBalance,
            long authoritySequence,
            long authorityBalance,
            string rejectionCode)
        {
            TransactionStableId = command == null ? null : command.TransactionStableId;
            OperationStableId = command == null ? null : command.OperationStableId;
            AuthorityStableId = command == null ? null : command.AuthorityStableId;
            CurrencyStableId = command == null ? null : command.CurrencyStableId;
            MutationKind = command == null ? (ScrapMutationKindV1)0 : command.MutationKind;
            Amount = command == null ? 0L : command.Amount;
            ReasonStableId = command == null ? null : command.ReasonStableId;
            Provenance = command == null ? null : command.Provenance;
            CommandFingerprint = command == null
                ? ScrapFingerprintV1.Compute("format=scrap-command-unavailable")
                : command.Fingerprint;
            Status = status;
            OriginalLedgerStatus = originalLedgerStatus;
            OriginalSequenceBefore = originalSequenceBefore;
            OriginalSequenceAfter = originalSequenceAfter;
            OriginalPreviousBalance = originalPreviousBalance;
            OriginalResultingBalance = originalResultingBalance;
            AuthoritySequence = authoritySequence;
            AuthorityBalance = authorityBalance;
            RejectionCode = rejectionCode;
            _fingerprint = ScrapFingerprintV1.Compute(ToCanonicalString());
        }

        public StableId TransactionStableId { get; }

        public StableId OperationStableId { get; }

        public StableId AuthorityStableId { get; }

        public StableId CurrencyStableId { get; }

        public ScrapMutationKindV1 MutationKind { get; }

        public long Amount { get; }

        public StableId ReasonStableId { get; }

        public ScrapProvenanceV1 Provenance { get; }

        public string CommandFingerprint { get; }

        public EconomyTransactionStatusV1 Status { get; }

        public LedgerMutationStatus OriginalLedgerStatus { get; }

        public long OriginalSequenceBefore { get; }

        public long OriginalSequenceAfter { get; }

        public long OriginalPreviousBalance { get; }

        public long OriginalResultingBalance { get; }

        public long AuthoritySequence { get; }

        public long AuthorityBalance { get; }

        public string RejectionCode { get; }

        public string Fingerprint => _fingerprint;

        public string ToCanonicalString()
        {
            return "format=scrap-change-v1"
                + "\ntransaction_id=" + CanonicalId(TransactionStableId)
                + "\noperation_id=" + CanonicalId(OperationStableId)
                + "\nauthority_id=" + CanonicalId(AuthorityStableId)
                + "\ncurrency_id=" + CanonicalId(CurrencyStableId)
                + "\nkind=" + ((int)MutationKind).ToString(CultureInfo.InvariantCulture)
                + "\namount=" + Amount.ToString(CultureInfo.InvariantCulture)
                + "\nreason_id=" + CanonicalId(ReasonStableId)
                + "\nsource_kind_id="
                + CanonicalId(Provenance == null ? null : Provenance.SourceKindStableId)
                + "\nsource_operation_id="
                + CanonicalId(Provenance == null ? null : Provenance.SourceOperationStableId)
                + "\nsubject_id="
                + CanonicalId(Provenance == null ? null : Provenance.SubjectStableId)
                + "\ncommand_fingerprint=" + CommandFingerprint
                + "\nstatus=" + ((int)Status).ToString(CultureInfo.InvariantCulture)
                + "\noriginal_ledger_status="
                + ((int)OriginalLedgerStatus).ToString(CultureInfo.InvariantCulture)
                + "\noriginal_sequence_before="
                + OriginalSequenceBefore.ToString(CultureInfo.InvariantCulture)
                + "\noriginal_sequence_after="
                + OriginalSequenceAfter.ToString(CultureInfo.InvariantCulture)
                + "\noriginal_previous_balance="
                + OriginalPreviousBalance.ToString(CultureInfo.InvariantCulture)
                + "\noriginal_resulting_balance="
                + OriginalResultingBalance.ToString(CultureInfo.InvariantCulture)
                + "\nauthority_sequence="
                + AuthoritySequence.ToString(CultureInfo.InvariantCulture)
                + "\nauthority_balance="
                + AuthorityBalance.ToString(CultureInfo.InvariantCulture)
                + "\nrejection_code=" + (RejectionCode ?? "none");
        }

        private static string CanonicalId(StableId stableId)
        {
            return stableId == null ? "none" : stableId.ToString();
        }
    }

    public sealed class ScrapTransactionResultV1
    {
        internal ScrapTransactionResultV1(
            EconomyTransactionStatusV1 status,
            EconomyTransactionResultV1 economyResult,
            ScrapChangeFactV1 changeFact)
        {
            Status = status;
            EconomyResult = economyResult;
            ChangeFact = changeFact ?? throw new ArgumentNullException(nameof(changeFact));
        }

        public EconomyTransactionStatusV1 Status { get; }

        public EconomyTransactionResultV1 EconomyResult { get; }

        public ScrapChangeFactV1 ChangeFact { get; }

        public bool ChangedState => Status == EconomyTransactionStatusV1.Applied;
    }

    public sealed class ScrapSnapshotImportResultV1
    {
        internal ScrapSnapshotImportResultV1(
            LedgerImportStatus status,
            string rejectionCode,
            long importedSequence,
            long importedBalance)
        {
            Status = status;
            RejectionCode = rejectionCode;
            ImportedSequence = importedSequence;
            ImportedBalance = importedBalance;
        }

        public LedgerImportStatus Status { get; }

        public string RejectionCode { get; }

        public long ImportedSequence { get; }

        public long ImportedBalance { get; }

        public bool Succeeded => Status == LedgerImportStatus.Imported;
    }

    public sealed class ScrapWalletServiceV1
    {
        private readonly StableId _authorityStableId;
        private readonly StableId _currencyStableId;
        private readonly IdempotentLedger<ScrapLedgerVocabulary> _ledger;

        public ScrapWalletServiceV1(
            StableId authorityStableId,
            StableId currencyStableId)
        {
            _authorityStableId = authorityStableId
                ?? throw new ArgumentNullException(nameof(authorityStableId));
            _currencyStableId = currencyStableId
                ?? throw new ArgumentNullException(nameof(currencyStableId));
            _ledger = new IdempotentLedger<ScrapLedgerVocabulary>(
                ValidateMutation,
                ValidatePolicy);
        }

        public StableId AuthorityStableId => _authorityStableId;

        public StableId CurrencyStableId => _currencyStableId;

        public long Sequence => _ledger.Sequence;

        public int TransactionCount => _ledger.TransactionCount;

        public long Balance => ReadCurrentBalance();

        public ScrapTransactionResultV1 Apply(ScrapTransactionCommandV1 command)
        {
            if (command == null)
            {
                return CreateUnrecordedInvalid(command, "command-null");
            }

            if (command.TransactionStableId == null)
            {
                return CreateUnrecordedInvalid(command, "transaction-id-null");
            }

            if (command.CurrencyStableId == null)
            {
                return CreateUnrecordedInvalid(command, "currency-id-null");
            }

            var entry = new LedgerEntry<ScrapLedgerVocabulary>(
                ScrapIdentityV1.BalanceEntryType,
                command.CurrencyStableId,
                command.LedgerPayload);
            var mutation = new LedgerMutation<ScrapLedgerVocabulary>(
                command.TransactionStableId,
                entry,
                command.GetAdmissionDelta(),
                command.ExpectedSequence);
            LedgerMutationResult<ScrapLedgerVocabulary> ledgerResult =
                _ledger.Apply(mutation);
            EconomyTransactionStatusV1 status = MapStatus(ledgerResult);
            return CreateResult(command, ledgerResult, status);
        }

        public ScrapSnapshotV1 ExportSnapshot()
        {
            LedgerSnapshot<ScrapLedgerVocabulary> ledgerSnapshot =
                _ledger.ExportSnapshot();
            return ScrapSnapshotV1.CreateCanonical(
                _authorityStableId,
                _currencyStableId,
                SumEntries(ledgerSnapshot.Entries),
                ledgerSnapshot);
        }

        public ScrapSnapshotImportResultV1 ImportSnapshot(ScrapSnapshotV1 snapshot)
        {
            string rejectionCode;
            LedgerImportStatus rejectedStatus;
            if (!TryValidateOuterSnapshot(
                snapshot,
                out rejectedStatus,
                out rejectionCode))
            {
                return new ScrapSnapshotImportResultV1(
                    rejectedStatus,
                    rejectionCode,
                    _ledger.Sequence,
                    ReadCurrentBalance());
            }

            var verifier = new IdempotentLedger<ScrapLedgerVocabulary>(
                context => LedgerDecision.Accept(),
                context => LedgerDecision.Accept());
            LedgerImportResult verification = verifier.ImportSnapshot(snapshot.LedgerSnapshot);
            if (!verification.Succeeded)
            {
                return new ScrapSnapshotImportResultV1(
                    verification.Status,
                    verification.RejectionCode,
                    _ledger.Sequence,
                    ReadCurrentBalance());
            }

            if (!TryValidateScrapSnapshotSemantics(snapshot, out rejectionCode))
            {
                return new ScrapSnapshotImportResultV1(
                    LedgerImportStatus.ValidationRejected,
                    rejectionCode,
                    _ledger.Sequence,
                    ReadCurrentBalance());
            }

            LedgerImportResult imported = _ledger.ImportSnapshot(snapshot.LedgerSnapshot);
            return new ScrapSnapshotImportResultV1(
                imported.Status,
                imported.RejectionCode,
                imported.Succeeded ? imported.ImportedSequence : _ledger.Sequence,
                ReadCurrentBalance());
        }

        private LedgerDecision ValidateMutation(
            LedgerMutationContext<ScrapLedgerVocabulary> context)
        {
            LedgerMutation<ScrapLedgerVocabulary> mutation = context.Mutation;
            if (mutation.Entry.EntryTypeId != ScrapIdentityV1.BalanceEntryType)
            {
                return LedgerDecision.Reject("wrong-entry-type");
            }

            if (mutation.Entry.TargetId != _currencyStableId)
            {
                return LedgerDecision.Reject("wrong-currency");
            }

            ScrapLedgerPayloadV1 payload;
            string rejectionCode;
            if (!ScrapLedgerPayloadV1.TryParse(
                mutation.Entry.CanonicalPayload,
                out payload,
                out rejectionCode))
            {
                return LedgerDecision.Reject("payload-malformed");
            }

            if (payload.AuthorityStableId != _authorityStableId)
            {
                return LedgerDecision.Reject("wrong-authority");
            }

            if (payload.OperationStableId == null)
            {
                return LedgerDecision.Reject("operation-id-null");
            }

            if (!Enum.IsDefined(typeof(ScrapMutationKindV1), payload.MutationKind))
            {
                return LedgerDecision.Reject("invalid-kind");
            }

            if (payload.Amount <= 0L)
            {
                return LedgerDecision.Reject("invalid-amount");
            }

            if (payload.Provenance == null
                || !payload.Provenance.TryValidateFor(
                    payload.ReasonStableId,
                    out rejectionCode))
            {
                return LedgerDecision.Reject(rejectionCode ?? "provenance-null");
            }

            if (mutation.QuantityDelta != payload.GetAdmissionDelta())
            {
                return LedgerDecision.Reject("delta-mismatch");
            }

            try
            {
                EconomyTransactionCommandV1.Create(
                    mutation.TransactionId,
                    payload.OperationStableId,
                    payload.AuthorityStableId,
                    payload.MutationKind == ScrapMutationKindV1.Grant
                        ? EconomyTransactionOperationV1.Credit
                        : EconomyTransactionOperationV1.Debit,
                    EconomyResourceKindV1.Currency,
                    mutation.Entry.TargetId,
                    null,
                    payload.Amount,
                    mutation.ExpectedSequence);
            }
            catch (ArgumentException)
            {
                return LedgerDecision.Reject("economy-command-invalid");
            }

            return LedgerDecision.Accept();
        }

        private LedgerDecision ValidatePolicy(
            LedgerMutationContext<ScrapLedgerVocabulary> context)
        {
            long proposedBalance;
            try
            {
                proposedBalance = checked(ReadCurrentBalance() + context.Mutation.QuantityDelta);
            }
            catch (OverflowException)
            {
                return LedgerDecision.Reject("balance-overflow");
            }

            if (proposedBalance < 0L)
            {
                return LedgerDecision.Reject("insufficient-scrap");
            }

            return LedgerDecision.Accept();
        }

        private ScrapTransactionResultV1 CreateResult(
            ScrapTransactionCommandV1 command,
            LedgerMutationResult<ScrapLedgerVocabulary> ledgerResult,
            EconomyTransactionStatusV1 status)
        {
            long authoritySequence = _ledger.Sequence;
            long authorityBalance = ReadCurrentBalance();
            long originalPreviousBalance =
                ReadBalanceAtSequence(ledgerResult.SequenceBefore);
            long originalResultingBalance =
                ReadBalanceAtSequence(ledgerResult.SequenceAfter);

            long publicPreviousSequence;
            long publicCurrentSequence;
            long publicResultingBalance;
            if (status == EconomyTransactionStatusV1.Applied)
            {
                publicPreviousSequence = ledgerResult.SequenceBefore;
                publicCurrentSequence = ledgerResult.SequenceAfter;
                publicResultingBalance = originalResultingBalance;
            }
            else
            {
                publicPreviousSequence = authoritySequence;
                publicCurrentSequence = authoritySequence;
                publicResultingBalance = authorityBalance;
            }

            EconomyTransactionResultV1 economyResult =
                EconomyTransactionResultV1.Create(
                    command.TransactionStableId,
                    status,
                    command.Fingerprint,
                    publicPreviousSequence,
                    publicCurrentSequence,
                    publicResultingBalance);
            var changeFact = new ScrapChangeFactV1(
                command,
                status,
                ledgerResult.OriginalStatus,
                ledgerResult.SequenceBefore,
                ledgerResult.SequenceAfter,
                originalPreviousBalance,
                originalResultingBalance,
                authoritySequence,
                authorityBalance,
                ledgerResult.RejectionCode);
            return new ScrapTransactionResultV1(status, economyResult, changeFact);
        }

        private ScrapTransactionResultV1 CreateUnrecordedInvalid(
            ScrapTransactionCommandV1 command,
            string rejectionCode)
        {
            long balance = ReadCurrentBalance();
            long sequence = _ledger.Sequence;
            EconomyTransactionResultV1 economyResult = null;
            if (command != null && command.TransactionStableId != null)
            {
                economyResult = EconomyTransactionResultV1.Create(
                    command.TransactionStableId,
                    EconomyTransactionStatusV1.InvalidRequest,
                    command.Fingerprint,
                    sequence,
                    sequence,
                    balance);
            }

            var changeFact = new ScrapChangeFactV1(
                command,
                EconomyTransactionStatusV1.InvalidRequest,
                LedgerMutationStatus.ValidationRejected,
                sequence,
                sequence,
                balance,
                balance,
                sequence,
                balance,
                rejectionCode);
            return new ScrapTransactionResultV1(
                EconomyTransactionStatusV1.InvalidRequest,
                economyResult,
                changeFact);
        }

        private static EconomyTransactionStatusV1 MapStatus(
            LedgerMutationResult<ScrapLedgerVocabulary> result)
        {
            switch (result.Status)
            {
                case LedgerMutationStatus.Applied:
                    return EconomyTransactionStatusV1.Applied;
                case LedgerMutationStatus.DuplicateNoChange:
                    return EconomyTransactionStatusV1.ExactDuplicateNoChange;
                case LedgerMutationStatus.ConflictingDuplicate:
                    return EconomyTransactionStatusV1.ConflictingDuplicate;
                case LedgerMutationStatus.SequenceConflict:
                    return EconomyTransactionStatusV1.ExpectedSequenceConflict;
                case LedgerMutationStatus.PolicyRejected:
                    return string.Equals(
                        result.RejectionCode,
                        "insufficient-scrap",
                        StringComparison.Ordinal)
                        ? EconomyTransactionStatusV1.InsufficientValue
                        : EconomyTransactionStatusV1.InvalidRequest;
                default:
                    return EconomyTransactionStatusV1.InvalidRequest;
            }
        }

        private long ReadCurrentBalance()
        {
            return SumEntries(_ledger.ExportSnapshot().Entries);
        }

        private long ReadBalanceAtSequence(long sequence)
        {
            LedgerSnapshot<ScrapLedgerVocabulary> snapshot = _ledger.ExportSnapshot();
            long balance = 0L;
            for (int index = 0; index < snapshot.Transactions.Count; index++)
            {
                LedgerTransactionSnapshot transaction = snapshot.Transactions[index];
                if (transaction.OriginalStatus == LedgerMutationStatus.Applied
                    && transaction.SequenceAfter <= sequence)
                {
                    balance = checked(balance + transaction.QuantityDelta);
                }
            }

            return balance;
        }

        private static long SumEntries(IReadOnlyList<LedgerSnapshotEntry> entries)
        {
            long balance = 0L;
            for (int index = 0; index < entries.Count; index++)
            {
                balance = checked(balance + entries[index].Quantity);
            }

            return balance;
        }

        private bool TryValidateOuterSnapshot(
            ScrapSnapshotV1 snapshot,
            out LedgerImportStatus status,
            out string rejectionCode)
        {
            if (snapshot == null)
            {
                status = LedgerImportStatus.ValidationRejected;
                rejectionCode = "snapshot-null";
                return false;
            }

            if (snapshot.SchemaVersion != ScrapSnapshotV1.CurrentSchemaVersion)
            {
                status = LedgerImportStatus.UnsupportedSchemaVersion;
                rejectionCode = "unsupported-scrap-schema-version";
                return false;
            }

            if (snapshot.LedgerSnapshot == null || snapshot.Balance < 0L)
            {
                status = LedgerImportStatus.ValidationRejected;
                rejectionCode = "snapshot-shape-invalid";
                return false;
            }

            StableId authorityStableId;
            StableId currencyStableId;
            if (!StableId.TryParse(snapshot.AuthorityStableId, out authorityStableId)
                || !StableId.TryParse(snapshot.CurrencyStableId, out currencyStableId)
                || authorityStableId != _authorityStableId
                || currencyStableId != _currencyStableId)
            {
                status = LedgerImportStatus.ValidationRejected;
                rejectionCode = "snapshot-authority-or-currency-mismatch";
                return false;
            }

            string expectedFingerprint = ScrapSnapshotV1.ComputeFingerprint(
                snapshot.SchemaVersion,
                snapshot.AuthorityStableId,
                snapshot.CurrencyStableId,
                snapshot.Balance,
                snapshot.LedgerSnapshot);
            if (!ScrapFingerprintV1.IsCanonical(snapshot.Fingerprint)
                || !string.Equals(
                    expectedFingerprint,
                    snapshot.Fingerprint,
                    StringComparison.Ordinal))
            {
                status = LedgerImportStatus.FingerprintMismatch;
                rejectionCode = "scrap-snapshot-fingerprint-mismatch";
                return false;
            }

            status = LedgerImportStatus.Imported;
            rejectionCode = null;
            return true;
        }

        private bool TryValidateScrapSnapshotSemantics(
            ScrapSnapshotV1 snapshot,
            out string rejectionCode)
        {
            LedgerSnapshot<ScrapLedgerVocabulary> ledger = snapshot.LedgerSnapshot;
            long entryBalance;
            try
            {
                entryBalance = SumEntries(ledger.Entries);
            }
            catch (OverflowException)
            {
                rejectionCode = "snapshot-balance-overflow";
                return false;
            }

            if (entryBalance != snapshot.Balance || entryBalance < 0L)
            {
                rejectionCode = "snapshot-balance-mismatch";
                return false;
            }

            for (int index = 0; index < ledger.Entries.Count; index++)
            {
                LedgerSnapshotEntry entry = ledger.Entries[index];
                if (!string.Equals(
                    entry.EntryTypeId,
                    ScrapIdentityV1.BalanceEntryType.ToString(),
                    StringComparison.Ordinal)
                    || !string.Equals(
                        entry.TargetId,
                        _currencyStableId.ToString(),
                        StringComparison.Ordinal))
                {
                    rejectionCode = "snapshot-entry-not-scrap";
                    return false;
                }

                ScrapLedgerPayloadV1 ignoredPayload;
                if (!ScrapLedgerPayloadV1.TryParse(
                    entry.CanonicalPayload,
                    out ignoredPayload,
                    out rejectionCode))
                {
                    rejectionCode = "snapshot-entry-payload-invalid";
                    return false;
                }
            }

            var applied = new List<LedgerTransactionSnapshot>();
            for (int index = 0; index < ledger.Transactions.Count; index++)
            {
                LedgerTransactionSnapshot transaction = ledger.Transactions[index];
                ScrapLedgerPayloadV1 payload;
                if (!ScrapLedgerPayloadV1.TryParse(
                    transaction.CanonicalPayload,
                    out payload,
                    out rejectionCode))
                {
                    rejectionCode = "snapshot-transaction-payload-invalid";
                    return false;
                }

                if (!string.Equals(
                    transaction.EntryTypeId,
                    ScrapIdentityV1.BalanceEntryType.ToString(),
                    StringComparison.Ordinal)
                    || transaction.QuantityDelta != payload.GetAdmissionDelta())
                {
                    rejectionCode = "snapshot-transaction-shape-invalid";
                    return false;
                }

                if (transaction.OriginalStatus == LedgerMutationStatus.Applied)
                {
                    if (!IsValidAppliedPayload(transaction.TargetId, payload)
                        || transaction.RejectionCode != null)
                    {
                        rejectionCode = "snapshot-applied-transaction-invalid";
                        return false;
                    }

                    applied.Add(transaction);
                }
                else if (!IsKnownRejectedTransaction(transaction))
                {
                    rejectionCode = "snapshot-rejection-code-invalid";
                    return false;
                }
            }

            applied.Sort((left, right) => left.SequenceAfter.CompareTo(right.SequenceAfter));
            long replayedBalance = 0L;
            for (int index = 0; index < applied.Count; index++)
            {
                try
                {
                    replayedBalance = checked(replayedBalance + applied[index].QuantityDelta);
                }
                catch (OverflowException)
                {
                    rejectionCode = "snapshot-replay-overflow";
                    return false;
                }

                if (replayedBalance < 0L)
                {
                    rejectionCode = "snapshot-replay-negative-balance";
                    return false;
                }
            }

            if (replayedBalance != snapshot.Balance)
            {
                rejectionCode = "snapshot-replay-balance-mismatch";
                return false;
            }

            rejectionCode = null;
            return true;
        }

        private bool IsValidAppliedPayload(
            string targetId,
            ScrapLedgerPayloadV1 payload)
        {
            string rejectionCode;
            return string.Equals(targetId, _currencyStableId.ToString(), StringComparison.Ordinal)
                && payload.AuthorityStableId == _authorityStableId
                && payload.OperationStableId != null
                && Enum.IsDefined(typeof(ScrapMutationKindV1), payload.MutationKind)
                && payload.Amount > 0L
                && payload.Provenance != null
                && payload.Provenance.TryValidateFor(payload.ReasonStableId, out rejectionCode);
        }

        private static bool IsKnownRejectedTransaction(
            LedgerTransactionSnapshot transaction)
        {
            if (transaction.OriginalStatus == LedgerMutationStatus.SequenceConflict)
            {
                return string.Equals(
                    transaction.RejectionCode,
                    "expected-sequence-conflict",
                    StringComparison.Ordinal);
            }

            if (transaction.OriginalStatus == LedgerMutationStatus.PolicyRejected)
            {
                return string.Equals(transaction.RejectionCode, "insufficient-scrap", StringComparison.Ordinal)
                    || string.Equals(transaction.RejectionCode, "balance-overflow", StringComparison.Ordinal);
            }

            if (transaction.OriginalStatus != LedgerMutationStatus.ValidationRejected)
            {
                return false;
            }

            switch (transaction.RejectionCode)
            {
                case "wrong-entry-type":
                case "wrong-currency":
                case "payload-malformed":
                case "wrong-authority":
                case "operation-id-null":
                case "invalid-kind":
                case "invalid-amount":
                case "reason-invalid":
                case "provenance-source-kind-null":
                case "provenance-operation-id-null":
                case "provenance-subject-id-null":
                case "provenance-source-kind-mismatch":
                case "provenance-null":
                case "delta-mismatch":
                case "economy-command-invalid":
                case "quantity-overflow":
                case "sequence-overflow":
                    return true;
                default:
                    return false;
            }
        }
    }
}
