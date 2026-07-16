using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Holdings
{
    public sealed partial class PlayerHoldingsService
    {
        private PlayerHoldingsImportResultV1 ImportFailure(
            PlayerHoldingsImportStatusV1 status,
            string rejectionCode)
        {
            return PlayerHoldingsImportResultV1.Create(
                status,
                rejectionCode,
                ledger.Sequence);
        }

        private bool TryValidateTransactionRecords(
            PlayerHoldingsSnapshotV1 snapshot,
            out Dictionary<StableId, PlayerHoldingsTransactionRecordV1> records,
            out string rejectionCode)
        {
            records =
                new Dictionary<StableId, PlayerHoldingsTransactionRecordV1>();
            var ledgerRecords =
                new Dictionary<string, LedgerTransactionSnapshot>(
                    StringComparer.Ordinal);
            for (int index = 0;
                index < snapshot.LedgerSnapshot.Transactions.Count;
                index++)
            {
                LedgerTransactionSnapshot ledgerRecord =
                    snapshot.LedgerSnapshot.Transactions[index];
                if (ledgerRecord == null
                    || ledgerRecords.ContainsKey(ledgerRecord.TransactionId))
                {
                    rejectionCode = "duplicate-ledger-transaction";
                    return false;
                }

                ledgerRecords.Add(
                    ledgerRecord.TransactionId,
                    ledgerRecord);
            }

            for (int index = 0; index < snapshot.Transactions.Count; index++)
            {
                PlayerHoldingsTransactionRecordV1 record =
                    snapshot.Transactions[index];
                StableId transactionId =
                    record.Command.Transaction.TransactionStableId;
                if (records.ContainsKey(transactionId))
                {
                    rejectionCode = "duplicate-holdings-transaction";
                    return false;
                }

                LedgerTransactionSnapshot ledgerRecord;
                if (!ledgerRecords.TryGetValue(
                    transactionId.ToString(),
                    out ledgerRecord))
                {
                    rejectionCode = "ledger-transaction-missing";
                    return false;
                }

                LedgerMutation<HoldingsLedgerVocabularyV1> mutation =
                    BuildLedgerMutation(record.Command);
                if (!LedgerRecordMatches(
                    mutation,
                    record,
                    ledgerRecord))
                {
                    rejectionCode = "transaction-record-mismatch";
                    return false;
                }

                records.Add(transactionId, record);
            }

            if (records.Count != ledgerRecords.Count)
            {
                rejectionCode = "transaction-count-mismatch";
                return false;
            }

            rejectionCode = null;
            return true;
        }

        private static bool LedgerRecordMatches(
            LedgerMutation<HoldingsLedgerVocabularyV1> mutation,
            PlayerHoldingsTransactionRecordV1 record,
            LedgerTransactionSnapshot ledgerRecord)
        {
            return string.Equals(
                    ledgerRecord.TransactionId,
                    mutation.TransactionId.ToString(),
                    StringComparison.Ordinal)
                && string.Equals(
                    ledgerRecord.EntryTypeId,
                    mutation.Entry.EntryTypeId.ToString(),
                    StringComparison.Ordinal)
                && string.Equals(
                    ledgerRecord.TargetId,
                    mutation.Entry.TargetId.ToString(),
                    StringComparison.Ordinal)
                && string.Equals(
                    ledgerRecord.CanonicalPayload,
                    mutation.Entry.CanonicalPayload,
                    StringComparison.Ordinal)
                && ledgerRecord.QuantityDelta == mutation.QuantityDelta
                && ledgerRecord.ExpectedSequence == mutation.ExpectedSequence
                && string.Equals(
                    ledgerRecord.PayloadFingerprint,
                    mutation.PayloadFingerprint,
                    StringComparison.Ordinal)
                && ledgerRecord.OriginalStatus == record.LedgerOriginalStatus
                && ledgerRecord.SequenceBefore == record.SequenceBefore
                && ledgerRecord.SequenceAfter == record.SequenceAfter
                && ledgerRecord.PreviousQuantity
                    == record.LedgerPreviousQuantity
                && ledgerRecord.CurrentQuantity
                    == record.LedgerCurrentQuantity
                && string.Equals(
                    ledgerRecord.RejectionCode,
                    record.RejectionCode,
                    StringComparison.Ordinal)
                && MapImportedOriginalStatus(
                    ledgerRecord.OriginalStatus,
                    ledgerRecord.RejectionCode)
                    == record.OriginalStatus;
        }

        private static PlayerHoldingsMutationStatusV1 MapImportedOriginalStatus(
            LedgerMutationStatus status,
            string rejectionCode)
        {
            switch (status)
            {
                case LedgerMutationStatus.Applied:
                    return PlayerHoldingsMutationStatusV1.Applied;
                case LedgerMutationStatus.SequenceConflict:
                    return PlayerHoldingsMutationStatusV1.ExpectedSequenceConflict;
                case LedgerMutationStatus.ValidationRejected:
                case LedgerMutationStatus.PolicyRejected:
                    return MapRejection(rejectionCode);
                default:
                    return PlayerHoldingsMutationStatusV1.InvalidRequest;
            }
        }

        private bool TryRebuildAppliedState(
            PlayerHoldingsSnapshotV1 snapshot,
            out Dictionary<StableId, UniqueHoldingSnapshotV1> rebuiltUnique,
            out Dictionary<StableId, UniqueIdentityHistory> rebuiltUniqueHistory,
            out Dictionary<StableId, StackState> rebuiltStacks,
            out Dictionary<StableId, RewardGrantKindV1> rebuiltStackHistory,
            out string rejectionCode)
        {
            rebuiltUnique =
                new Dictionary<StableId, UniqueHoldingSnapshotV1>();
            rebuiltUniqueHistory =
                new Dictionary<StableId, UniqueIdentityHistory>();
            rebuiltStacks = new Dictionary<StableId, StackState>();
            rebuiltStackHistory =
                new Dictionary<StableId, RewardGrantKindV1>();

            var applied = new List<PlayerHoldingsTransactionRecordV1>();
            for (int index = 0; index < snapshot.Transactions.Count; index++)
            {
                PlayerHoldingsTransactionRecordV1 record =
                    snapshot.Transactions[index];
                if (record.OriginalStatus
                    == PlayerHoldingsMutationStatusV1.Applied)
                {
                    applied.Add(record);
                }
            }

            applied.Sort(delegate(
                PlayerHoldingsTransactionRecordV1 left,
                PlayerHoldingsTransactionRecordV1 right)
            {
                int sequenceComparison =
                    left.SequenceAfter.CompareTo(right.SequenceAfter);
                return sequenceComparison != 0
                    ? sequenceComparison
                    : left.CompareTo(right);
            });

            if (applied.Count != snapshot.LedgerSnapshot.Sequence)
            {
                rejectionCode = "applied-sequence-count-mismatch";
                return false;
            }

            long expectedSequence = 0L;
            for (int index = 0; index < applied.Count; index++)
            {
                PlayerHoldingsTransactionRecordV1 record = applied[index];
                if (record.SequenceBefore != expectedSequence
                    || record.SequenceAfter != expectedSequence + 1L
                    || record.LedgerOriginalStatus
                        != LedgerMutationStatus.Applied)
                {
                    rejectionCode = "applied-sequence-gap";
                    return false;
                }

                string validationCode;
                if (!TryValidateCommandAgainstState(
                    record.Command,
                    rebuiltUnique,
                    rebuiltUniqueHistory,
                    rebuiltStacks,
                    rebuiltStackHistory,
                    out validationCode))
                {
                    rejectionCode = "applied-command-invalid-" + validationCode;
                    return false;
                }

                long previousQuantity = GetRebuiltQuantity(
                    record.Command,
                    rebuiltUnique,
                    rebuiltStacks);
                long currentQuantity = CommitRebuiltMutation(
                    record.Command,
                    rebuiltUnique,
                    rebuiltUniqueHistory,
                    rebuiltStacks,
                    rebuiltStackHistory);
                if (previousQuantity != record.HoldingPreviousQuantity
                    || currentQuantity != record.HoldingCurrentQuantity)
                {
                    rejectionCode = "holding-quantity-fact-mismatch";
                    return false;
                }

                expectedSequence++;
            }

            rejectionCode = null;
            return true;
        }

        private static long GetRebuiltQuantity(
            PlayerHoldingsCommandV1 command,
            IDictionary<StableId, UniqueHoldingSnapshotV1> rebuiltUnique,
            IDictionary<StableId, StackState> rebuiltStacks)
        {
            EconomyTransactionCommandV1 transaction = command.Transaction;
            if (transaction.InstanceStableId != null)
            {
                return rebuiltUnique.ContainsKey(transaction.InstanceStableId)
                    ? 1L
                    : 0L;
            }

            StackState state;
            return rebuiltStacks.TryGetValue(
                transaction.ResourceStableId,
                out state)
                ? state.Quantity
                : 0L;
        }

        private static long CommitRebuiltMutation(
            PlayerHoldingsCommandV1 command,
            IDictionary<StableId, UniqueHoldingSnapshotV1> rebuiltUnique,
            IDictionary<StableId, UniqueIdentityHistory> rebuiltUniqueHistory,
            IDictionary<StableId, StackState> rebuiltStacks,
            IDictionary<StableId, RewardGrantKindV1> rebuiltStackHistory)
        {
            EconomyTransactionCommandV1 transaction = command.Transaction;
            switch (transaction.Operation)
            {
                case EconomyTransactionOperationV1.AddUnique:
                    rebuiltUnique.Add(
                        transaction.InstanceStableId,
                        UniqueHoldingSnapshotV1.Create(
                            command.RewardKind,
                            transaction.ResourceStableId,
                            transaction.InstanceStableId,
                            command.EquipmentInstance,
                            command.Provenance));
                    rebuiltUniqueHistory.Add(
                        transaction.InstanceStableId,
                        new UniqueIdentityHistory(
                            command.RewardKind,
                            transaction.ResourceStableId));
                    return 1L;

                case EconomyTransactionOperationV1.RemoveUnique:
                    rebuiltUnique.Remove(transaction.InstanceStableId);
                    return 0L;

                case EconomyTransactionOperationV1.AddStack:
                {
                    StackState current;
                    long previous = rebuiltStacks.TryGetValue(
                        transaction.ResourceStableId,
                        out current)
                        ? current.Quantity
                        : 0L;
                    long next = checked(previous + transaction.Quantity);
                    rebuiltStacks[transaction.ResourceStableId] =
                        new StackState(command.RewardKind, next);
                    if (!rebuiltStackHistory.ContainsKey(
                        transaction.ResourceStableId))
                    {
                        rebuiltStackHistory.Add(
                            transaction.ResourceStableId,
                            command.RewardKind);
                    }

                    return next;
                }

                case EconomyTransactionOperationV1.RemoveStack:
                {
                    StackState current =
                        rebuiltStacks[transaction.ResourceStableId];
                    long next = current.Quantity - transaction.Quantity;
                    if (next == 0L)
                    {
                        rebuiltStacks.Remove(transaction.ResourceStableId);
                    }
                    else
                    {
                        rebuiltStacks[transaction.ResourceStableId] =
                            new StackState(command.RewardKind, next);
                    }

                    return next;
                }

                default:
                    throw new InvalidOperationException();
            }
        }

        private static bool CurrentProjectionMatches(
            PlayerHoldingsSnapshotV1 snapshot,
            IDictionary<StableId, UniqueHoldingSnapshotV1> rebuiltUnique,
            IDictionary<StableId, StackState> rebuiltStacks)
        {
            if (snapshot.UniqueHoldings.Count != rebuiltUnique.Count
                || snapshot.StackHoldings.Count != rebuiltStacks.Count)
            {
                return false;
            }

            for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshotV1 expected =
                    snapshot.UniqueHoldings[index];
                UniqueHoldingSnapshotV1 actual;
                if (!rebuiltUnique.TryGetValue(
                        expected.InstanceStableId,
                        out actual)
                    || !actual.Equals(expected))
                {
                    return false;
                }
            }

            for (int index = 0; index < snapshot.StackHoldings.Count; index++)
            {
                StackHoldingSnapshotV1 expected =
                    snapshot.StackHoldings[index];
                StackState actual;
                if (!rebuiltStacks.TryGetValue(expected.ItemStableId, out actual)
                    || actual.RewardKind != expected.RewardKind
                    || actual.Quantity != expected.Quantity)
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class UniqueIdentityHistory
        {
            public UniqueIdentityHistory(
                RewardGrantKindV1 rewardKind,
                StableId definitionStableId)
            {
                RewardKind = rewardKind;
                DefinitionStableId = definitionStableId;
            }

            public RewardGrantKindV1 RewardKind { get; }

            public StableId DefinitionStableId { get; }
        }

        private sealed class StackState
        {
            public StackState(
                RewardGrantKindV1 rewardKind,
                long quantity)
            {
                RewardKind = rewardKind;
                Quantity = quantity;
            }

            public RewardGrantKindV1 RewardKind { get; }

            public long Quantity { get; }
        }
    }
}
