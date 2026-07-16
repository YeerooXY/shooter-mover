using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Economy.Ledger
{
    public enum LedgerMutationStatus
    {
        Applied = 0,
        DuplicateNoChange = 1,
        ConflictingDuplicate = 2,
        SequenceConflict = 3,
        ValidationRejected = 4,
        PolicyRejected = 5,
    }

    public enum LedgerImportStatus
    {
        Imported = 0,
        ValidationRejected = 1,
        UnsupportedSchemaVersion = 2,
        FingerprintMismatch = 3,
    }

    public sealed class LedgerDecision
    {
        private LedgerDecision(bool isAccepted, string rejectionCode)
        {
            IsAccepted = isAccepted;
            RejectionCode = rejectionCode;
        }

        public bool IsAccepted { get; }

        public string RejectionCode { get; }

        public static LedgerDecision Accept()
        {
            return new LedgerDecision(true, null);
        }

        public static LedgerDecision Reject(string rejectionCode)
        {
            if (!LedgerCanonicalText.IsValidRejectionCode(rejectionCode))
            {
                throw new ArgumentException(
                    "A rejection code must be 1-128 printable ASCII characters.",
                    nameof(rejectionCode));
            }

            return new LedgerDecision(false, rejectionCode);
        }
    }

    public sealed class LedgerEntry<TVocabulary>
    {
        public const int MaximumCanonicalPayloadLength = 1024;

        public LedgerEntry(
            StableId entryTypeId,
            StableId targetId,
            string canonicalPayload)
        {
            EntryTypeId = entryTypeId;
            TargetId = targetId;
            CanonicalPayload = canonicalPayload;
        }

        public StableId EntryTypeId { get; }

        public StableId TargetId { get; }

        public string CanonicalPayload { get; }
    }

    public sealed class LedgerMutation<TVocabulary>
    {
        public LedgerMutation(
            StableId transactionId,
            LedgerEntry<TVocabulary> entry,
            long quantityDelta,
            long? expectedSequence = null)
        {
            TransactionId = transactionId;
            Entry = entry;
            QuantityDelta = quantityDelta;
            ExpectedSequence = expectedSequence;
            PayloadFingerprint = LedgerFingerprint.ComputeMutationFingerprint(
                entry == null || entry.EntryTypeId == null ? null : entry.EntryTypeId.ToString(),
                entry == null || entry.TargetId == null ? null : entry.TargetId.ToString(),
                entry == null ? null : entry.CanonicalPayload,
                quantityDelta,
                expectedSequence);
        }

        public StableId TransactionId { get; }

        public LedgerEntry<TVocabulary> Entry { get; }

        public long QuantityDelta { get; }

        public long? ExpectedSequence { get; }

        public string PayloadFingerprint { get; }
    }

    public sealed class LedgerMutationContext<TVocabulary>
    {
        internal LedgerMutationContext(
            LedgerMutation<TVocabulary> mutation,
            long sequence,
            long currentQuantity,
            long proposedQuantity)
        {
            Mutation = mutation;
            Sequence = sequence;
            CurrentQuantity = currentQuantity;
            ProposedQuantity = proposedQuantity;
        }

        public LedgerMutation<TVocabulary> Mutation { get; }

        public long Sequence { get; }

        public long CurrentQuantity { get; }

        public long ProposedQuantity { get; }
    }

    public delegate LedgerDecision LedgerMutationValidator<TVocabulary>(
        LedgerMutationContext<TVocabulary> context);

    public delegate LedgerDecision LedgerMutationPolicy<TVocabulary>(
        LedgerMutationContext<TVocabulary> context);

    public sealed class LedgerMutationResult<TVocabulary>
    {
        internal LedgerMutationResult(
            LedgerMutationStatus status,
            LedgerMutationStatus originalStatus,
            StableId transactionId,
            string payloadFingerprint,
            long sequenceBefore,
            long sequenceAfter,
            long previousQuantity,
            long currentQuantity,
            string rejectionCode)
        {
            Status = status;
            OriginalStatus = originalStatus;
            TransactionId = transactionId;
            PayloadFingerprint = payloadFingerprint;
            SequenceBefore = sequenceBefore;
            SequenceAfter = sequenceAfter;
            PreviousQuantity = previousQuantity;
            CurrentQuantity = currentQuantity;
            RejectionCode = rejectionCode;
        }

        public LedgerMutationStatus Status { get; }

        public LedgerMutationStatus OriginalStatus { get; }

        public StableId TransactionId { get; }

        public string PayloadFingerprint { get; }

        public long SequenceBefore { get; }

        public long SequenceAfter { get; }

        public long PreviousQuantity { get; }

        public long CurrentQuantity { get; }

        public string RejectionCode { get; }

        public bool ChangedState => Status == LedgerMutationStatus.Applied;
    }

    public sealed class LedgerImportResult
    {
        internal LedgerImportResult(
            LedgerImportStatus status,
            string rejectionCode,
            long importedSequence)
        {
            Status = status;
            RejectionCode = rejectionCode;
            ImportedSequence = importedSequence;
        }

        public LedgerImportStatus Status { get; }

        public string RejectionCode { get; }

        public long ImportedSequence { get; }

        public bool Succeeded => Status == LedgerImportStatus.Imported;
    }

    public sealed class LedgerSnapshotEntry
    {
        public LedgerSnapshotEntry(
            string entryTypeId,
            string targetId,
            string canonicalPayload,
            long quantity)
        {
            EntryTypeId = entryTypeId;
            TargetId = targetId;
            CanonicalPayload = canonicalPayload;
            Quantity = quantity;
        }

        public string EntryTypeId { get; }

        public string TargetId { get; }

        public string CanonicalPayload { get; }

        public long Quantity { get; }
    }

    public sealed class LedgerTransactionSnapshot
    {
        public LedgerTransactionSnapshot(
            string transactionId,
            string entryTypeId,
            string targetId,
            string canonicalPayload,
            long quantityDelta,
            long? expectedSequence,
            string payloadFingerprint,
            LedgerMutationStatus originalStatus,
            long sequenceBefore,
            long sequenceAfter,
            long previousQuantity,
            long currentQuantity,
            string rejectionCode)
        {
            TransactionId = transactionId;
            EntryTypeId = entryTypeId;
            TargetId = targetId;
            CanonicalPayload = canonicalPayload;
            QuantityDelta = quantityDelta;
            ExpectedSequence = expectedSequence;
            PayloadFingerprint = payloadFingerprint;
            OriginalStatus = originalStatus;
            SequenceBefore = sequenceBefore;
            SequenceAfter = sequenceAfter;
            PreviousQuantity = previousQuantity;
            CurrentQuantity = currentQuantity;
            RejectionCode = rejectionCode;
        }

        public string TransactionId { get; }

        public string EntryTypeId { get; }

        public string TargetId { get; }

        public string CanonicalPayload { get; }

        public long QuantityDelta { get; }

        public long? ExpectedSequence { get; }

        public string PayloadFingerprint { get; }

        public LedgerMutationStatus OriginalStatus { get; }

        public long SequenceBefore { get; }

        public long SequenceAfter { get; }

        public long PreviousQuantity { get; }

        public long CurrentQuantity { get; }

        public string RejectionCode { get; }
    }

    public sealed class LedgerSnapshot<TVocabulary>
    {
        public const int CurrentSchemaVersion = 1;

        public LedgerSnapshot(
            int schemaVersion,
            long sequence,
            IEnumerable<LedgerSnapshotEntry> entries,
            IEnumerable<LedgerTransactionSnapshot> transactions,
            string fingerprint)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            if (transactions == null)
            {
                throw new ArgumentNullException(nameof(transactions));
            }

            SchemaVersion = schemaVersion;
            Sequence = sequence;
            Entries = LedgerSnapshotOrdering.CopyAndOrderEntries(entries);
            Transactions = LedgerSnapshotOrdering.CopyAndOrderTransactions(transactions);
            Fingerprint = fingerprint;
        }

        public int SchemaVersion { get; }

        public long Sequence { get; }

        public IReadOnlyList<LedgerSnapshotEntry> Entries { get; }

        public IReadOnlyList<LedgerTransactionSnapshot> Transactions { get; }

        public string Fingerprint { get; }

        public static LedgerSnapshot<TVocabulary> CreateCanonical(
            int schemaVersion,
            long sequence,
            IEnumerable<LedgerSnapshotEntry> entries,
            IEnumerable<LedgerTransactionSnapshot> transactions)
        {
            LedgerSnapshot<TVocabulary> withoutFingerprint =
                new LedgerSnapshot<TVocabulary>(
                    schemaVersion,
                    sequence,
                    entries,
                    transactions,
                    string.Empty);

            string fingerprint = LedgerFingerprint.ComputeSnapshotFingerprint(
                withoutFingerprint.SchemaVersion,
                withoutFingerprint.Sequence,
                withoutFingerprint.Entries,
                withoutFingerprint.Transactions);

            return new LedgerSnapshot<TVocabulary>(
                schemaVersion,
                sequence,
                withoutFingerprint.Entries,
                withoutFingerprint.Transactions,
                fingerprint);
        }
    }

    public sealed class IdempotentLedger<TVocabulary>
    {
        private readonly LedgerMutationValidator<TVocabulary> _validator;
        private readonly LedgerMutationPolicy<TVocabulary> _policy;
        private Dictionary<string, BalanceRecord> _balances;
        private Dictionary<string, LedgerTransactionSnapshot> _transactions;
        private long _sequence;

        public IdempotentLedger(
            LedgerMutationValidator<TVocabulary> validator,
            LedgerMutationPolicy<TVocabulary> policy)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _balances = new Dictionary<string, BalanceRecord>(StringComparer.Ordinal);
            _transactions =
                new Dictionary<string, LedgerTransactionSnapshot>(StringComparer.Ordinal);
        }

        public long Sequence => _sequence;

        public int EntryCount => _balances.Count;

        public int TransactionCount => _transactions.Count;

        public long GetQuantity(LedgerEntry<TVocabulary> entry)
        {
            string validationCode;
            if (!TryValidateEntry(entry, out validationCode))
            {
                throw new ArgumentException(validationCode, nameof(entry));
            }

            BalanceRecord record;
            return _balances.TryGetValue(LedgerFingerprint.BuildEntryKey(
                entry.EntryTypeId.ToString(),
                entry.TargetId.ToString(),
                entry.CanonicalPayload), out record)
                ? record.Quantity
                : 0L;
        }

        public LedgerMutationResult<TVocabulary> Apply(
            LedgerMutation<TVocabulary> mutation)
        {
            if (mutation == null)
            {
                return CreateUnrecordedValidationRejection(
                    null,
                    null,
                    "mutation-null");
            }

            if (mutation.TransactionId == null)
            {
                return CreateUnrecordedValidationRejection(
                    null,
                    mutation.PayloadFingerprint,
                    "transaction-id-null");
            }

            string transactionKey = mutation.TransactionId.ToString();
            LedgerTransactionSnapshot existing;
            if (_transactions.TryGetValue(transactionKey, out existing))
            {
                if (!string.Equals(
                    existing.PayloadFingerprint,
                    mutation.PayloadFingerprint,
                    StringComparison.Ordinal))
                {
                    return new LedgerMutationResult<TVocabulary>(
                        LedgerMutationStatus.ConflictingDuplicate,
                        existing.OriginalStatus,
                        mutation.TransactionId,
                        mutation.PayloadFingerprint,
                        existing.SequenceBefore,
                        existing.SequenceAfter,
                        existing.PreviousQuantity,
                        existing.CurrentQuantity,
                        "transaction-payload-conflict");
                }

                return new LedgerMutationResult<TVocabulary>(
                    LedgerMutationStatus.DuplicateNoChange,
                    existing.OriginalStatus,
                    mutation.TransactionId,
                    existing.PayloadFingerprint,
                    existing.SequenceBefore,
                    existing.SequenceAfter,
                    existing.PreviousQuantity,
                    existing.CurrentQuantity,
                    existing.RejectionCode);
            }

            string structuralValidationCode;
            if (!TryValidateMutationStructure(mutation, out structuralValidationCode))
            {
                return CreateUnrecordedValidationRejection(
                    mutation.TransactionId,
                    mutation.PayloadFingerprint,
                    structuralValidationCode);
            }

            string entryTypeId = mutation.Entry.EntryTypeId.ToString();
            string targetId = mutation.Entry.TargetId.ToString();
            string canonicalPayload = mutation.Entry.CanonicalPayload;
            string entryKey =
                LedgerFingerprint.BuildEntryKey(entryTypeId, targetId, canonicalPayload);

            BalanceRecord currentRecord;
            long currentQuantity =
                _balances.TryGetValue(entryKey, out currentRecord)
                    ? currentRecord.Quantity
                    : 0L;

            if (mutation.ExpectedSequence.HasValue
                && mutation.ExpectedSequence.Value != _sequence)
            {
                return RecordTerminalOutcome(
                    mutation,
                    LedgerMutationStatus.SequenceConflict,
                    _sequence,
                    _sequence,
                    currentQuantity,
                    currentQuantity,
                    "expected-sequence-conflict");
            }

            long proposedQuantity;
            try
            {
                proposedQuantity = checked(currentQuantity + mutation.QuantityDelta);
            }
            catch (OverflowException)
            {
                return RecordTerminalOutcome(
                    mutation,
                    LedgerMutationStatus.ValidationRejected,
                    _sequence,
                    _sequence,
                    currentQuantity,
                    currentQuantity,
                    "quantity-overflow");
            }

            var context = new LedgerMutationContext<TVocabulary>(
                mutation,
                _sequence,
                currentQuantity,
                proposedQuantity);

            LedgerDecision validationDecision = _validator(context);
            if (validationDecision == null)
            {
                return RecordTerminalOutcome(
                    mutation,
                    LedgerMutationStatus.ValidationRejected,
                    _sequence,
                    _sequence,
                    currentQuantity,
                    currentQuantity,
                    "validator-returned-null");
            }

            if (!validationDecision.IsAccepted)
            {
                return RecordTerminalOutcome(
                    mutation,
                    LedgerMutationStatus.ValidationRejected,
                    _sequence,
                    _sequence,
                    currentQuantity,
                    currentQuantity,
                    validationDecision.RejectionCode);
            }

            LedgerDecision policyDecision = _policy(context);
            if (policyDecision == null)
            {
                return RecordTerminalOutcome(
                    mutation,
                    LedgerMutationStatus.PolicyRejected,
                    _sequence,
                    _sequence,
                    currentQuantity,
                    currentQuantity,
                    "policy-returned-null");
            }

            if (!policyDecision.IsAccepted)
            {
                return RecordTerminalOutcome(
                    mutation,
                    LedgerMutationStatus.PolicyRejected,
                    _sequence,
                    _sequence,
                    currentQuantity,
                    currentQuantity,
                    policyDecision.RejectionCode);
            }

            long sequenceBefore = _sequence;
            long sequenceAfter;
            try
            {
                sequenceAfter = checked(sequenceBefore + 1L);
            }
            catch (OverflowException)
            {
                return RecordTerminalOutcome(
                    mutation,
                    LedgerMutationStatus.ValidationRejected,
                    sequenceBefore,
                    sequenceBefore,
                    currentQuantity,
                    currentQuantity,
                    "sequence-overflow");
            }

            if (proposedQuantity == 0L)
            {
                _balances.Remove(entryKey);
            }
            else
            {
                _balances[entryKey] = new BalanceRecord(
                    entryTypeId,
                    targetId,
                    canonicalPayload,
                    proposedQuantity);
            }

            _sequence = sequenceAfter;
            return RecordTerminalOutcome(
                mutation,
                LedgerMutationStatus.Applied,
                sequenceBefore,
                sequenceAfter,
                currentQuantity,
                proposedQuantity,
                null);
        }

        public LedgerSnapshot<TVocabulary> ExportSnapshot()
        {
            var entries = new List<LedgerSnapshotEntry>(_balances.Count);
            foreach (BalanceRecord balance in _balances.Values)
            {
                entries.Add(new LedgerSnapshotEntry(
                    balance.EntryTypeId,
                    balance.TargetId,
                    balance.CanonicalPayload,
                    balance.Quantity));
            }

            var transactions =
                new List<LedgerTransactionSnapshot>(_transactions.Count);
            foreach (LedgerTransactionSnapshot transaction in _transactions.Values)
            {
                transactions.Add(transaction);
            }

            return LedgerSnapshot<TVocabulary>.CreateCanonical(
                LedgerSnapshot<TVocabulary>.CurrentSchemaVersion,
                _sequence,
                entries,
                transactions);
        }

        public LedgerImportResult ImportSnapshot(
            LedgerSnapshot<TVocabulary> snapshot)
        {
            if (snapshot == null)
            {
                return new LedgerImportResult(
                    LedgerImportStatus.ValidationRejected,
                    "snapshot-null",
                    _sequence);
            }

            if (snapshot.SchemaVersion
                != LedgerSnapshot<TVocabulary>.CurrentSchemaVersion)
            {
                return new LedgerImportResult(
                    LedgerImportStatus.UnsupportedSchemaVersion,
                    "unsupported-schema-version",
                    _sequence);
            }

            if (snapshot.Sequence < 0L)
            {
                return new LedgerImportResult(
                    LedgerImportStatus.ValidationRejected,
                    "negative-sequence",
                    _sequence);
            }

            Dictionary<string, BalanceRecord> importedBalances;
            string validationCode;
            if (!TryBuildBalances(
                snapshot.Entries,
                out importedBalances,
                out validationCode))
            {
                return new LedgerImportResult(
                    LedgerImportStatus.ValidationRejected,
                    validationCode,
                    _sequence);
            }

            Dictionary<string, LedgerTransactionSnapshot> importedTransactions;
            if (!TryBuildTransactions(
                snapshot.Transactions,
                snapshot.Sequence,
                out importedTransactions,
                out validationCode))
            {
                return new LedgerImportResult(
                    LedgerImportStatus.ValidationRejected,
                    validationCode,
                    _sequence);
            }

            if (!TryReplayAppliedTransactions(
                snapshot.Sequence,
                importedTransactions,
                importedBalances,
                out validationCode))
            {
                return new LedgerImportResult(
                    LedgerImportStatus.ValidationRejected,
                    validationCode,
                    _sequence);
            }

            string computedFingerprint =
                LedgerFingerprint.ComputeSnapshotFingerprint(
                    snapshot.SchemaVersion,
                    snapshot.Sequence,
                    snapshot.Entries,
                    snapshot.Transactions);

            if (!LedgerFingerprint.IsCanonicalFingerprint(snapshot.Fingerprint)
                || !string.Equals(
                    computedFingerprint,
                    snapshot.Fingerprint,
                    StringComparison.Ordinal))
            {
                return new LedgerImportResult(
                    LedgerImportStatus.FingerprintMismatch,
                    "snapshot-fingerprint-mismatch",
                    _sequence);
            }

            _balances = importedBalances;
            _transactions = importedTransactions;
            _sequence = snapshot.Sequence;

            return new LedgerImportResult(
                LedgerImportStatus.Imported,
                null,
                _sequence);
        }

        private LedgerMutationResult<TVocabulary> RecordTerminalOutcome(
            LedgerMutation<TVocabulary> mutation,
            LedgerMutationStatus originalStatus,
            long sequenceBefore,
            long sequenceAfter,
            long previousQuantity,
            long currentQuantity,
            string rejectionCode)
        {
            var transaction = new LedgerTransactionSnapshot(
                mutation.TransactionId.ToString(),
                mutation.Entry.EntryTypeId.ToString(),
                mutation.Entry.TargetId.ToString(),
                mutation.Entry.CanonicalPayload,
                mutation.QuantityDelta,
                mutation.ExpectedSequence,
                mutation.PayloadFingerprint,
                originalStatus,
                sequenceBefore,
                sequenceAfter,
                previousQuantity,
                currentQuantity,
                rejectionCode);

            _transactions.Add(transaction.TransactionId, transaction);

            return new LedgerMutationResult<TVocabulary>(
                originalStatus,
                originalStatus,
                mutation.TransactionId,
                mutation.PayloadFingerprint,
                sequenceBefore,
                sequenceAfter,
                previousQuantity,
                currentQuantity,
                rejectionCode);
        }

        private LedgerMutationResult<TVocabulary>
            CreateUnrecordedValidationRejection(
                StableId transactionId,
                string payloadFingerprint,
                string rejectionCode)
        {
            return new LedgerMutationResult<TVocabulary>(
                LedgerMutationStatus.ValidationRejected,
                LedgerMutationStatus.ValidationRejected,
                transactionId,
                payloadFingerprint,
                _sequence,
                _sequence,
                0L,
                0L,
                rejectionCode);
        }

        private static bool TryValidateMutationStructure(
            LedgerMutation<TVocabulary> mutation,
            out string rejectionCode)
        {
            if (mutation.QuantityDelta == 0L)
            {
                rejectionCode = "quantity-delta-zero";
                return false;
            }

            if (mutation.ExpectedSequence.HasValue
                && mutation.ExpectedSequence.Value < 0L)
            {
                rejectionCode = "negative-expected-sequence";
                return false;
            }

            return TryValidateEntry(mutation.Entry, out rejectionCode);
        }

        private static bool TryValidateEntry(
            LedgerEntry<TVocabulary> entry,
            out string rejectionCode)
        {
            if (entry == null)
            {
                rejectionCode = "entry-null";
                return false;
            }

            if (entry.EntryTypeId == null)
            {
                rejectionCode = "entry-type-id-null";
                return false;
            }

            if (entry.TargetId == null)
            {
                rejectionCode = "target-id-null";
                return false;
            }

            if (!LedgerCanonicalText.IsValidPayload(entry.CanonicalPayload))
            {
                rejectionCode = "canonical-payload-invalid";
                return false;
            }

            rejectionCode = null;
            return true;
        }

        private static bool TryBuildBalances(
            IReadOnlyList<LedgerSnapshotEntry> entries,
            out Dictionary<string, BalanceRecord> balances,
            out string rejectionCode)
        {
            balances = new Dictionary<string, BalanceRecord>(StringComparer.Ordinal);

            for (int index = 0; index < entries.Count; index++)
            {
                LedgerSnapshotEntry entry = entries[index];
                if (entry == null)
                {
                    rejectionCode = "snapshot-entry-null";
                    return false;
                }

                StableId entryTypeId;
                StableId targetId;
                if (!StableId.TryParse(entry.EntryTypeId, out entryTypeId)
                    || !StableId.TryParse(entry.TargetId, out targetId))
                {
                    rejectionCode = "snapshot-entry-identity-invalid";
                    return false;
                }

                if (!LedgerCanonicalText.IsValidPayload(entry.CanonicalPayload))
                {
                    rejectionCode = "snapshot-entry-payload-invalid";
                    return false;
                }

                if (entry.Quantity == 0L)
                {
                    rejectionCode = "snapshot-entry-zero-quantity";
                    return false;
                }

                string key = LedgerFingerprint.BuildEntryKey(
                    entryTypeId.ToString(),
                    targetId.ToString(),
                    entry.CanonicalPayload);

                if (balances.ContainsKey(key))
                {
                    rejectionCode = "snapshot-entry-duplicate";
                    return false;
                }

                balances.Add(
                    key,
                    new BalanceRecord(
                        entryTypeId.ToString(),
                        targetId.ToString(),
                        entry.CanonicalPayload,
                        entry.Quantity));
            }

            rejectionCode = null;
            return true;
        }

        private static bool TryBuildTransactions(
            IReadOnlyList<LedgerTransactionSnapshot> transactions,
            long snapshotSequence,
            out Dictionary<string, LedgerTransactionSnapshot> imported,
            out string rejectionCode)
        {
            imported =
                new Dictionary<string, LedgerTransactionSnapshot>(StringComparer.Ordinal);

            for (int index = 0; index < transactions.Count; index++)
            {
                LedgerTransactionSnapshot transaction = transactions[index];
                if (transaction == null)
                {
                    rejectionCode = "snapshot-transaction-null";
                    return false;
                }

                StableId transactionId;
                StableId entryTypeId;
                StableId targetId;
                if (!StableId.TryParse(transaction.TransactionId, out transactionId)
                    || !StableId.TryParse(transaction.EntryTypeId, out entryTypeId)
                    || !StableId.TryParse(transaction.TargetId, out targetId))
                {
                    rejectionCode = "snapshot-transaction-identity-invalid";
                    return false;
                }

                if (!LedgerCanonicalText.IsValidPayload(
                    transaction.CanonicalPayload))
                {
                    rejectionCode = "snapshot-transaction-payload-invalid";
                    return false;
                }

                if (transaction.QuantityDelta == 0L)
                {
                    rejectionCode = "snapshot-transaction-zero-delta";
                    return false;
                }

                if (transaction.ExpectedSequence.HasValue
                    && transaction.ExpectedSequence.Value < 0L)
                {
                    rejectionCode = "snapshot-transaction-negative-expected-sequence";
                    return false;
                }

                if (!LedgerFingerprint.IsCanonicalFingerprint(
                    transaction.PayloadFingerprint))
                {
                    rejectionCode = "snapshot-transaction-fingerprint-invalid";
                    return false;
                }

                string computedMutationFingerprint =
                    LedgerFingerprint.ComputeMutationFingerprint(
                        entryTypeId.ToString(),
                        targetId.ToString(),
                        transaction.CanonicalPayload,
                        transaction.QuantityDelta,
                        transaction.ExpectedSequence);

                if (!string.Equals(
                    computedMutationFingerprint,
                    transaction.PayloadFingerprint,
                    StringComparison.Ordinal))
                {
                    rejectionCode = "snapshot-transaction-payload-fingerprint-mismatch";
                    return false;
                }

                if (!IsOriginalStatus(transaction.OriginalStatus))
                {
                    rejectionCode = "snapshot-transaction-status-invalid";
                    return false;
                }

                if (transaction.SequenceBefore < 0L
                    || transaction.SequenceAfter < 0L
                    || transaction.SequenceBefore > snapshotSequence
                    || transaction.SequenceAfter > snapshotSequence)
                {
                    rejectionCode = "snapshot-transaction-sequence-invalid";
                    return false;
                }

                if (transaction.OriginalStatus == LedgerMutationStatus.Applied)
                {
                    if (transaction.SequenceAfter
                        != transaction.SequenceBefore + 1L)
                    {
                        rejectionCode = "snapshot-applied-sequence-invalid";
                        return false;
                    }

                    long expectedCurrentQuantity;
                    try
                    {
                        expectedCurrentQuantity = checked(
                            transaction.PreviousQuantity
                            + transaction.QuantityDelta);
                    }
                    catch (OverflowException)
                    {
                        rejectionCode = "snapshot-transaction-quantity-overflow";
                        return false;
                    }

                    if (expectedCurrentQuantity != transaction.CurrentQuantity
                        || transaction.RejectionCode != null)
                    {
                        rejectionCode = "snapshot-applied-result-invalid";
                        return false;
                    }
                }
                else
                {
                    if (transaction.SequenceAfter != transaction.SequenceBefore
                        || transaction.CurrentQuantity
                        != transaction.PreviousQuantity
                        || !LedgerCanonicalText.IsValidRejectionCode(
                            transaction.RejectionCode))
                    {
                        rejectionCode = "snapshot-rejected-result-invalid";
                        return false;
                    }
                }

                string transactionKey = transactionId.ToString();
                if (imported.ContainsKey(transactionKey))
                {
                    rejectionCode = "snapshot-transaction-duplicate";
                    return false;
                }

                imported.Add(transactionKey, transaction);
            }

            rejectionCode = null;
            return true;
        }

        private static bool TryReplayAppliedTransactions(
            long snapshotSequence,
            Dictionary<string, LedgerTransactionSnapshot> transactions,
            Dictionary<string, BalanceRecord> expectedBalances,
            out string rejectionCode)
        {
            var applied = new List<LedgerTransactionSnapshot>();
            foreach (LedgerTransactionSnapshot transaction in transactions.Values)
            {
                if (transaction.OriginalStatus == LedgerMutationStatus.Applied)
                {
                    applied.Add(transaction);
                }
            }

            applied.Sort((left, right) =>
            {
                int sequenceComparison =
                    left.SequenceAfter.CompareTo(right.SequenceAfter);
                return sequenceComparison != 0
                    ? sequenceComparison
                    : string.CompareOrdinal(left.TransactionId, right.TransactionId);
            });

            if (applied.Count != snapshotSequence)
            {
                rejectionCode = "snapshot-applied-count-sequence-mismatch";
                return false;
            }

            var replayed =
                new Dictionary<string, BalanceRecord>(StringComparer.Ordinal);

            for (int index = 0; index < applied.Count; index++)
            {
                LedgerTransactionSnapshot transaction = applied[index];
                long expectedSequenceAfter = index + 1L;
                if (transaction.SequenceBefore != index
                    || transaction.SequenceAfter != expectedSequenceAfter)
                {
                    rejectionCode = "snapshot-applied-sequence-gap";
                    return false;
                }

                string entryKey = LedgerFingerprint.BuildEntryKey(
                    transaction.EntryTypeId,
                    transaction.TargetId,
                    transaction.CanonicalPayload);

                BalanceRecord previous;
                long replayPrevious =
                    replayed.TryGetValue(entryKey, out previous)
                        ? previous.Quantity
                        : 0L;

                if (replayPrevious != transaction.PreviousQuantity)
                {
                    rejectionCode = "snapshot-transaction-previous-quantity-mismatch";
                    return false;
                }

                long replayCurrent;
                try
                {
                    replayCurrent = checked(
                        replayPrevious + transaction.QuantityDelta);
                }
                catch (OverflowException)
                {
                    rejectionCode = "snapshot-replay-overflow";
                    return false;
                }

                if (replayCurrent != transaction.CurrentQuantity)
                {
                    rejectionCode = "snapshot-transaction-current-quantity-mismatch";
                    return false;
                }

                if (replayCurrent == 0L)
                {
                    replayed.Remove(entryKey);
                }
                else
                {
                    replayed[entryKey] = new BalanceRecord(
                        transaction.EntryTypeId,
                        transaction.TargetId,
                        transaction.CanonicalPayload,
                        replayCurrent);
                }
            }

            if (replayed.Count != expectedBalances.Count)
            {
                rejectionCode = "snapshot-replayed-entry-count-mismatch";
                return false;
            }

            foreach (KeyValuePair<string, BalanceRecord> pair in replayed)
            {
                BalanceRecord expected;
                if (!expectedBalances.TryGetValue(pair.Key, out expected)
                    || expected.Quantity != pair.Value.Quantity)
                {
                    rejectionCode = "snapshot-replayed-balance-mismatch";
                    return false;
                }
            }

            rejectionCode = null;
            return true;
        }

        private static bool IsOriginalStatus(LedgerMutationStatus status)
        {
            return status == LedgerMutationStatus.Applied
                || status == LedgerMutationStatus.SequenceConflict
                || status == LedgerMutationStatus.ValidationRejected
                || status == LedgerMutationStatus.PolicyRejected;
        }

        private sealed class BalanceRecord
        {
            public BalanceRecord(
                string entryTypeId,
                string targetId,
                string canonicalPayload,
                long quantity)
            {
                EntryTypeId = entryTypeId;
                TargetId = targetId;
                CanonicalPayload = canonicalPayload;
                Quantity = quantity;
            }

            public string EntryTypeId { get; }

            public string TargetId { get; }

            public string CanonicalPayload { get; }

            public long Quantity { get; }
        }
    }

    internal static class LedgerSnapshotOrdering
    {
        public static IReadOnlyList<LedgerSnapshotEntry> CopyAndOrderEntries(
            IEnumerable<LedgerSnapshotEntry> entries)
        {
            var copy = new List<LedgerSnapshotEntry>(entries);
            copy.Sort(CompareEntries);
            return new ReadOnlyCollection<LedgerSnapshotEntry>(copy);
        }

        public static IReadOnlyList<LedgerTransactionSnapshot>
            CopyAndOrderTransactions(
                IEnumerable<LedgerTransactionSnapshot> transactions)
        {
            var copy = new List<LedgerTransactionSnapshot>(transactions);
            copy.Sort((left, right) =>
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (left == null)
                {
                    return -1;
                }

                if (right == null)
                {
                    return 1;
                }

                return string.CompareOrdinal(
                    left.TransactionId,
                    right.TransactionId);
            });

            return new ReadOnlyCollection<LedgerTransactionSnapshot>(copy);
        }

        private static int CompareEntries(
            LedgerSnapshotEntry left,
            LedgerSnapshotEntry right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            int comparison =
                string.CompareOrdinal(left.EntryTypeId, right.EntryTypeId);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.CompareOrdinal(left.TargetId, right.TargetId);
            return comparison != 0
                ? comparison
                : string.CompareOrdinal(
                    left.CanonicalPayload,
                    right.CanonicalPayload);
        }
    }

    internal static class LedgerCanonicalText
    {
        public static bool IsValidPayload(string value)
        {
            if (value == null
                || value.Length > LedgerEntry<object>.MaximumCanonicalPayloadLength)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (current < 0x20 || current > 0x7e)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsValidRejectionCode(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (current < 0x21 || current > 0x7e)
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal static class LedgerFingerprint
    {
        private const int FingerprintHexLength = 64;

        public static string ComputeMutationFingerprint(
            string entryTypeId,
            string targetId,
            string canonicalPayload,
            long quantityDelta,
            long? expectedSequence)
        {
            var builder = new StringBuilder();
            AppendToken(builder, "mutation-v1");
            AppendToken(builder, entryTypeId);
            AppendToken(builder, targetId);
            AppendToken(builder, canonicalPayload);
            AppendToken(
                builder,
                quantityDelta.ToString(CultureInfo.InvariantCulture));
            AppendToken(
                builder,
                expectedSequence.HasValue
                    ? expectedSequence.Value.ToString(CultureInfo.InvariantCulture)
                    : null);
            return ComputeSha256(builder.ToString());
        }

        public static string ComputeSnapshotFingerprint(
            int schemaVersion,
            long sequence,
            IReadOnlyList<LedgerSnapshotEntry> entries,
            IReadOnlyList<LedgerTransactionSnapshot> transactions)
        {
            var builder = new StringBuilder();
            AppendToken(builder, "idempotent-ledger-snapshot");
            AppendToken(
                builder,
                schemaVersion.ToString(CultureInfo.InvariantCulture));
            AppendToken(
                builder,
                sequence.ToString(CultureInfo.InvariantCulture));

            AppendToken(
                builder,
                entries.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < entries.Count; index++)
            {
                LedgerSnapshotEntry entry = entries[index];
                if (entry == null)
                {
                    AppendToken(builder, null);
                    continue;
                }

                AppendToken(builder, entry.EntryTypeId);
                AppendToken(builder, entry.TargetId);
                AppendToken(builder, entry.CanonicalPayload);
                AppendToken(
                    builder,
                    entry.Quantity.ToString(CultureInfo.InvariantCulture));
            }

            AppendToken(
                builder,
                transactions.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < transactions.Count; index++)
            {
                LedgerTransactionSnapshot transaction = transactions[index];
                if (transaction == null)
                {
                    AppendToken(builder, null);
                    continue;
                }

                AppendToken(builder, transaction.TransactionId);
                AppendToken(builder, transaction.EntryTypeId);
                AppendToken(builder, transaction.TargetId);
                AppendToken(builder, transaction.CanonicalPayload);
                AppendToken(
                    builder,
                    transaction.QuantityDelta.ToString(
                        CultureInfo.InvariantCulture));
                AppendToken(
                    builder,
                    transaction.ExpectedSequence.HasValue
                        ? transaction.ExpectedSequence.Value.ToString(
                            CultureInfo.InvariantCulture)
                        : null);
                AppendToken(builder, transaction.PayloadFingerprint);
                AppendToken(
                    builder,
                    ((int)transaction.OriginalStatus).ToString(
                        CultureInfo.InvariantCulture));
                AppendToken(
                    builder,
                    transaction.SequenceBefore.ToString(
                        CultureInfo.InvariantCulture));
                AppendToken(
                    builder,
                    transaction.SequenceAfter.ToString(
                        CultureInfo.InvariantCulture));
                AppendToken(
                    builder,
                    transaction.PreviousQuantity.ToString(
                        CultureInfo.InvariantCulture));
                AppendToken(
                    builder,
                    transaction.CurrentQuantity.ToString(
                        CultureInfo.InvariantCulture));
                AppendToken(builder, transaction.RejectionCode);
            }

            return ComputeSha256(builder.ToString());
        }

        public static string BuildEntryKey(
            string entryTypeId,
            string targetId,
            string canonicalPayload)
        {
            var builder = new StringBuilder();
            AppendToken(builder, entryTypeId);
            AppendToken(builder, targetId);
            AppendToken(builder, canonicalPayload);
            return builder.ToString();
        }

        public static bool IsCanonicalFingerprint(string value)
        {
            if (value == null || value.Length != FingerprintHexLength)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                bool digit = current >= '0' && current <= '9';
                bool lowerHex = current >= 'a' && current <= 'f';
                if (!digit && !lowerHex)
                {
                    return false;
                }
            }

            return true;
        }

        private static void AppendToken(StringBuilder builder, string value)
        {
            if (value == null)
            {
                builder.Append("-1:");
                return;
            }

            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
        }

        private static string ComputeSha256(string canonicalText)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(canonicalText);
            byte[] hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(bytes);
            }

            var builder = new StringBuilder(hash.Length * 2);
            for (int index = 0; index < hash.Length; index++)
            {
                builder.Append(
                    hash[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
