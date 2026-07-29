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
    public sealed partial class PlayerHoldingsActions : IPlayerHoldingsState
    {
        private readonly object sync = new object();
        private readonly IEquipmentInstanceValidator equipmentValidator;
        private IdempotentLedger<HoldingsLedgerVocabulary> ledger;
        private Dictionary<StableId, UniqueHoldingSnapshot> uniqueHoldings;
        private Dictionary<StableId, UniqueIdentityHistory> uniqueHistory;
        private Dictionary<StableId, StackState> stackHoldings;
        private Dictionary<StableId, RewardGrantKind> stackKindHistory;
        private Dictionary<StableId, PlayerHoldingsTransactionRecord> transactionRecords;
        private PlayerHoldingsCommand pendingCommand;

        public PlayerHoldingsActions(
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
            uniqueHoldings = new Dictionary<StableId, UniqueHoldingSnapshot>();
            uniqueHistory = new Dictionary<StableId, UniqueIdentityHistory>();
            stackHoldings = new Dictionary<StableId, StackState>();
            stackKindHistory = new Dictionary<StableId, RewardGrantKind>();
            transactionRecords =
                new Dictionary<StableId, PlayerHoldingsTransactionRecord>();
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

        public PlayerHoldingsMutationResult Apply(
            PlayerHoldingsCommand command)
        {
            lock (sync)
            {
                if (command == null)
                {
                    return PlayerHoldingsMutationResult.Create(
                        null,
                        PlayerHoldingsMutationStatus.InvalidRequest,
                        PlayerHoldingsMutationStatus.InvalidRequest,
                        null,
                        ledger.Sequence,
                        ledger.Sequence,
                        0L,
                        0L,
                        "command-null");
                }

                long currentHoldingQuantity = GetHoldingQuantity(command);
                LedgerMutation<HoldingsLedgerVocabulary> mutation =
                    BuildLedgerMutation(command);
                pendingCommand = command;
                LedgerMutationResult<HoldingsLedgerVocabulary> ledgerResult;
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
                    PlayerHoldingsTransactionRecord existing;
                    if (!transactionRecords.TryGetValue(
                        command.Transaction.TransactionStableId,
                        out existing))
                    {
                        return PlayerHoldingsMutationResult.Create(
                            command.Transaction.TransactionStableId,
                            PlayerHoldingsMutationStatus.InvalidRequest,
                            PlayerHoldingsMutationStatus.InvalidRequest,
                            command.PayloadFingerprint,
                            ledger.Sequence,
                            ledger.Sequence,
                            currentHoldingQuantity,
                            currentHoldingQuantity,
                            "duplicate-record-missing");
                    }

                    return PlayerHoldingsMutationResult.Create(
                        command.Transaction.TransactionStableId,
                        PlayerHoldingsMutationStatus.ExactDuplicateNoChange,
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
                    PlayerHoldingsTransactionRecord existing;
                    if (!transactionRecords.TryGetValue(
                        command.Transaction.TransactionStableId,
                        out existing))
                    {
                        return PlayerHoldingsMutationResult.Create(
                            command.Transaction.TransactionStableId,
                            PlayerHoldingsMutationStatus.InvalidRequest,
                            PlayerHoldingsMutationStatus.InvalidRequest,
                            command.PayloadFingerprint,
                            ledger.Sequence,
                            ledger.Sequence,
                            currentHoldingQuantity,
                            currentHoldingQuantity,
                            "conflict-record-missing");
                    }

                    return PlayerHoldingsMutationResult.Create(
                        command.Transaction.TransactionStableId,
                        PlayerHoldingsMutationStatus.ConflictingDuplicate,
                        existing.OriginalStatus,
                        command.PayloadFingerprint,
                        existing.SequenceBefore,
                        existing.SequenceAfter,
                        existing.HoldingPreviousQuantity,
                        existing.HoldingCurrentQuantity,
                        ledgerResult.RejectionCode);
                }

                PlayerHoldingsMutationStatus status =
                    MapStatus(ledgerResult);
                long resultingHoldingQuantity = currentHoldingQuantity;
                if (ledgerResult.Status == LedgerMutationStatus.Applied)
                {
                    resultingHoldingQuantity = CommitHoldingMutation(command);
                }

                var record = PlayerHoldingsTransactionRecord.Create(
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

                return PlayerHoldingsMutationResult.Create(
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
            out UniqueHoldingSnapshot holding)
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
            RewardGrantKind rewardKind,
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
