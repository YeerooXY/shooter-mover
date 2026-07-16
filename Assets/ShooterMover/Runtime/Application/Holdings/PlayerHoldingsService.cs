using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Holdings
{
    /// <summary>
    /// Sole engine-independent authority for durable unique and stackable player
    /// holdings. It composes LED-001 for exact-once admission and retains typed
    /// immutable ownership/provenance projections outside Unity state.
    /// </summary>
    public sealed partial class PlayerHoldingsService : IPlayerHoldingsAuthorityV1
    {
        private readonly object sync = new object();
        private readonly IEquipmentInstanceValidator equipmentValidator;
        private IdempotentLedger<HoldingsLedgerVocabularyV1> ledger;
        private Dictionary<StableId, UniqueHoldingSnapshotV1> uniqueHoldings;
        private Dictionary<StableId, UniqueIdentityHistory> uniqueHistory;
        private Dictionary<StableId, StackState> stackHoldings;
        private Dictionary<StableId, RewardGrantKindV1> stackKindHistory;
        private Dictionary<StableId, PlayerHoldingsTransactionRecordV1> transactionRecords;
        private PlayerHoldingsCommandV1 pendingCommand;

        public PlayerHoldingsService(
            StableId authorityStableId,
            long maximumStackQuantity,
            IEquipmentInstanceValidator equipmentValidator)
        {
            AuthorityStableId = authorityStableId
                ?? throw new ArgumentNullException(nameof(authorityStableId));
            if (maximumStackQuantity < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumStackQuantity),
                    maximumStackQuantity,
                    "Maximum stack quantity must be positive.");
            }

            this.equipmentValidator = equipmentValidator
                ?? throw new ArgumentNullException(nameof(equipmentValidator));
            MaximumStackQuantity = maximumStackQuantity;
            ledger = CreateLedger();
            uniqueHoldings = new Dictionary<StableId, UniqueHoldingSnapshotV1>();
            uniqueHistory = new Dictionary<StableId, UniqueIdentityHistory>();
            stackHoldings = new Dictionary<StableId, StackState>();
            stackKindHistory = new Dictionary<StableId, RewardGrantKindV1>();
            transactionRecords =
                new Dictionary<StableId, PlayerHoldingsTransactionRecordV1>();
        }

        public StableId AuthorityStableId { get; }

        public long MaximumStackQuantity { get; }

        public long Sequence
        {
            get
            {
                lock (sync)
                {
                    return ledger.Sequence;
                }
            }
        }

        public PlayerHoldingsMutationResultV1 Apply(
            PlayerHoldingsCommandV1 command)
        {
            lock (sync)
            {
                if (command == null)
                {
                    return PlayerHoldingsMutationResultV1.Create(
                        null,
                        PlayerHoldingsMutationStatusV1.InvalidRequest,
                        PlayerHoldingsMutationStatusV1.InvalidRequest,
                        null,
                        ledger.Sequence,
                        ledger.Sequence,
                        0L,
                        0L,
                        "command-null");
                }

                long currentHoldingQuantity = GetHoldingQuantity(command);
                LedgerMutation<HoldingsLedgerVocabularyV1> mutation =
                    BuildLedgerMutation(command);
                pendingCommand = command;
                LedgerMutationResult<HoldingsLedgerVocabularyV1> ledgerResult;
                try
                {
                    ledgerResult = ledger.Apply(mutation);
                }
                finally
                {
                    pendingCommand = null;
                }

                if (ledgerResult.Status == LedgerMutationStatus.DuplicateNoChange)
                {
                    PlayerHoldingsTransactionRecordV1 existing;
                    if (!transactionRecords.TryGetValue(
                        command.Transaction.TransactionStableId,
                        out existing))
                    {
                        return PlayerHoldingsMutationResultV1.Create(
                            command.Transaction.TransactionStableId,
                            PlayerHoldingsMutationStatusV1.InvalidRequest,
                            PlayerHoldingsMutationStatusV1.InvalidRequest,
                            command.PayloadFingerprint,
                            ledger.Sequence,
                            ledger.Sequence,
                            currentHoldingQuantity,
                            currentHoldingQuantity,
                            "duplicate-record-missing");
                    }

                    return PlayerHoldingsMutationResultV1.Create(
                        command.Transaction.TransactionStableId,
                        PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange,
                        existing.OriginalStatus,
                        command.PayloadFingerprint,
                        existing.SequenceBefore,
                        existing.SequenceAfter,
                        existing.HoldingPreviousQuantity,
                        existing.HoldingCurrentQuantity,
                        existing.RejectionCode);
                }

                if (ledgerResult.Status == LedgerMutationStatus.ConflictingDuplicate)
                {
                    PlayerHoldingsTransactionRecordV1 existing;
                    if (!transactionRecords.TryGetValue(
                        command.Transaction.TransactionStableId,
                        out existing))
                    {
                        return PlayerHoldingsMutationResultV1.Create(
                            command.Transaction.TransactionStableId,
                            PlayerHoldingsMutationStatusV1.InvalidRequest,
                            PlayerHoldingsMutationStatusV1.InvalidRequest,
                            command.PayloadFingerprint,
                            ledger.Sequence,
                            ledger.Sequence,
                            currentHoldingQuantity,
                            currentHoldingQuantity,
                            "conflict-record-missing");
                    }

                    return PlayerHoldingsMutationResultV1.Create(
                        command.Transaction.TransactionStableId,
                        PlayerHoldingsMutationStatusV1.ConflictingDuplicate,
                        existing.OriginalStatus,
                        command.PayloadFingerprint,
                        existing.SequenceBefore,
                        existing.SequenceAfter,
                        existing.HoldingPreviousQuantity,
                        existing.HoldingCurrentQuantity,
                        ledgerResult.RejectionCode);
                }

                PlayerHoldingsMutationStatusV1 status =
                    MapStatus(ledgerResult);
                long resultingHoldingQuantity = currentHoldingQuantity;
                if (ledgerResult.Status == LedgerMutationStatus.Applied)
                {
                    resultingHoldingQuantity = CommitHoldingMutation(command);
                }

                var record = PlayerHoldingsTransactionRecordV1.Create(
                    command,
                    status,
                    ledgerResult.OriginalStatus,
                    ledgerResult.SequenceBefore,
                    ledgerResult.SequenceAfter,
                    ledgerResult.PreviousQuantity,
                    ledgerResult.CurrentQuantity,
                    currentHoldingQuantity,
                    resultingHoldingQuantity,
                    ledgerResult.RejectionCode);
                transactionRecords.Add(
                    command.Transaction.TransactionStableId,
                    record);

                return PlayerHoldingsMutationResultV1.Create(
                    command.Transaction.TransactionStableId,
                    status,
                    status,
                    command.PayloadFingerprint,
                    ledgerResult.SequenceBefore,
                    ledgerResult.SequenceAfter,
                    currentHoldingQuantity,
                    resultingHoldingQuantity,
                    ledgerResult.RejectionCode);
            }
        }

        public bool TryGetUnique(
            StableId instanceStableId,
            out UniqueHoldingSnapshotV1 holding)
        {
            lock (sync)
            {
                if (instanceStableId == null)
                {
                    holding = null;
                    return false;
                }

                return uniqueHoldings.TryGetValue(instanceStableId, out holding);
            }
        }

        public long GetStackQuantity(
            RewardGrantKindV1 rewardKind,
            StableId itemStableId)
        {
            lock (sync)
            {
                if (itemStableId == null)
                {
                    return 0L;
                }

                StackState state;
                return stackHoldings.TryGetValue(itemStableId, out state)
                    && state.RewardKind == rewardKind
                    ? state.Quantity
                    : 0L;
            }
        }

    }
}
