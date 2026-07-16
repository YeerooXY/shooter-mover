using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Holdings
{
    public sealed partial class PlayerHoldingsService
    {
        private IdempotentLedger<HoldingsLedgerVocabularyV1> CreateLedger()
        {
            return new IdempotentLedger<HoldingsLedgerVocabularyV1>(
                ValidatePendingMutation,
                delegate(LedgerMutationContext<HoldingsLedgerVocabularyV1> context)
                {
                    return LedgerDecision.Accept();
                });
        }

        private LedgerDecision ValidatePendingMutation(
            LedgerMutationContext<HoldingsLedgerVocabularyV1> context)
        {
            if (pendingCommand == null)
            {
                return LedgerDecision.Reject("pending-command-missing");
            }

            string rejectionCode;
            if (!TryValidateCommandAgainstState(
                pendingCommand,
                uniqueHoldings,
                uniqueHistory,
                stackHoldings,
                stackKindHistory,
                out rejectionCode))
            {
                return LedgerDecision.Reject(rejectionCode);
            }

            return LedgerDecision.Accept();
        }

        private bool TryValidateCommandAgainstState(
            PlayerHoldingsCommandV1 command,
            IDictionary<StableId, UniqueHoldingSnapshotV1> currentUnique,
            IDictionary<StableId, UniqueIdentityHistory> historicalUnique,
            IDictionary<StableId, StackState> currentStacks,
            IDictionary<StableId, RewardGrantKindV1> historicalStackKinds,
            out string rejectionCode)
        {
            EconomyTransactionCommandV1 transaction = command.Transaction;
            if (transaction.AuthorityStableId != AuthorityStableId)
            {
                rejectionCode = "wrong-authority";
                return false;
            }

            bool isEquipment =
                command.RewardKind == RewardGrantKindV1.EquipmentReference;
            bool isStrongbox =
                command.RewardKind == RewardGrantKindV1.Strongbox;
            bool isStack =
                command.RewardKind == RewardGrantKindV1.PremiumAmmo
                || command.RewardKind == RewardGrantKindV1.Miscellaneous;
            if (!isEquipment && !isStrongbox && !isStack)
            {
                rejectionCode = "wrong-reward-type";
                return false;
            }

            if (isEquipment || isStrongbox)
            {
                return TryValidateUniqueCommand(
                    command,
                    currentUnique,
                    historicalUnique,
                    out rejectionCode);
            }

            return TryValidateStackCommand(
                command,
                currentStacks,
                historicalStackKinds,
                out rejectionCode);
        }

        private bool TryValidateUniqueCommand(
            PlayerHoldingsCommandV1 command,
            IDictionary<StableId, UniqueHoldingSnapshotV1> currentUnique,
            IDictionary<StableId, UniqueIdentityHistory> historicalUnique,
            out string rejectionCode)
        {
            EconomyTransactionCommandV1 transaction = command.Transaction;
            EconomyResourceKindV1 expectedResourceKind =
                command.RewardKind == RewardGrantKindV1.EquipmentReference
                    ? EconomyResourceKindV1.EquipmentReference
                    : EconomyResourceKindV1.Strongbox;
            bool isAdd =
                transaction.Operation == EconomyTransactionOperationV1.AddUnique;
            bool isRemove =
                transaction.Operation == EconomyTransactionOperationV1.RemoveUnique;
            if (transaction.ResourceKind != expectedResourceKind
                || (!isAdd && !isRemove)
                || transaction.InstanceStableId == null
                || transaction.Quantity != 1L)
            {
                rejectionCode = "type-mismatch";
                return false;
            }

            if (isAdd)
            {
                if (historicalUnique.ContainsKey(transaction.InstanceStableId))
                {
                    rejectionCode = "unique-instance-collision";
                    return false;
                }

                if (command.RewardKind == RewardGrantKindV1.EquipmentReference)
                {
                    if (command.EquipmentInstance == null
                        || command.EquipmentInstance.InstanceId
                            != transaction.InstanceStableId
                        || command.EquipmentInstance.DefinitionId
                            != transaction.ResourceStableId)
                    {
                        rejectionCode = "type-mismatch";
                        return false;
                    }

                    EquipmentInstanceValidationResponse response;
                    try
                    {
                        response = equipmentValidator.Validate(
                            new EquipmentInstanceValidationRequest(
                                command.EquipmentInstance));
                    }
                    catch (Exception)
                    {
                        rejectionCode = "equipment-validation-rejected";
                        return false;
                    }

                    if (response == null || !response.IsValid)
                    {
                        rejectionCode = "equipment-validation-rejected";
                        return false;
                    }
                }
                else if (command.EquipmentInstance != null)
                {
                    rejectionCode = "type-mismatch";
                    return false;
                }

                rejectionCode = null;
                return true;
            }

            UniqueHoldingSnapshotV1 existing;
            if (!currentUnique.TryGetValue(
                transaction.InstanceStableId,
                out existing))
            {
                rejectionCode = "missing-item";
                return false;
            }

            if (existing.RewardKind != command.RewardKind
                || existing.DefinitionStableId != transaction.ResourceStableId)
            {
                rejectionCode = "type-mismatch";
                return false;
            }

            if (command.EquipmentInstance != null)
            {
                rejectionCode = "type-mismatch";
                return false;
            }

            rejectionCode = null;
            return true;
        }

        private bool TryValidateStackCommand(
            PlayerHoldingsCommandV1 command,
            IDictionary<StableId, StackState> currentStacks,
            IDictionary<StableId, RewardGrantKindV1> historicalStackKinds,
            out string rejectionCode)
        {
            EconomyTransactionCommandV1 transaction = command.Transaction;
            bool isAdd =
                transaction.Operation == EconomyTransactionOperationV1.AddStack;
            bool isRemove =
                transaction.Operation == EconomyTransactionOperationV1.RemoveStack;
            if (transaction.ResourceKind != EconomyResourceKindV1.Item
                || (!isAdd && !isRemove)
                || transaction.InstanceStableId != null
                || command.EquipmentInstance != null)
            {
                rejectionCode = "type-mismatch";
                return false;
            }

            RewardGrantKindV1 historicalKind;
            if (historicalStackKinds.TryGetValue(
                    transaction.ResourceStableId,
                    out historicalKind)
                && historicalKind != command.RewardKind)
            {
                rejectionCode = "type-mismatch";
                return false;
            }

            StackState existing;
            long current = currentStacks.TryGetValue(
                transaction.ResourceStableId,
                out existing)
                ? existing.Quantity
                : 0L;

            if (isRemove)
            {
                if (current == 0L)
                {
                    rejectionCode = "missing-item";
                    return false;
                }

                if (existing.RewardKind != command.RewardKind)
                {
                    rejectionCode = "type-mismatch";
                    return false;
                }

                if (transaction.Quantity > current)
                {
                    rejectionCode = "insufficient-value";
                    return false;
                }

                rejectionCode = null;
                return true;
            }

            long proposed;
            try
            {
                proposed = checked(current + transaction.Quantity);
            }
            catch (OverflowException)
            {
                rejectionCode = "arithmetic-overflow";
                return false;
            }

            if (proposed > MaximumStackQuantity)
            {
                rejectionCode = "insufficient-capacity";
                return false;
            }

            rejectionCode = null;
            return true;
        }

        private long CommitHoldingMutation(PlayerHoldingsCommandV1 command)
        {
            EconomyTransactionCommandV1 transaction = command.Transaction;
            switch (transaction.Operation)
            {
                case EconomyTransactionOperationV1.AddUnique:
                {
                    var holding = UniqueHoldingSnapshotV1.Create(
                        command.RewardKind,
                        transaction.ResourceStableId,
                        transaction.InstanceStableId,
                        command.EquipmentInstance,
                        command.Provenance);
                    uniqueHoldings.Add(transaction.InstanceStableId, holding);
                    uniqueHistory.Add(
                        transaction.InstanceStableId,
                        new UniqueIdentityHistory(
                            command.RewardKind,
                            transaction.ResourceStableId));
                    return 1L;
                }

                case EconomyTransactionOperationV1.RemoveUnique:
                    uniqueHoldings.Remove(transaction.InstanceStableId);
                    return 0L;

                case EconomyTransactionOperationV1.AddStack:
                {
                    StackState current;
                    long previous = stackHoldings.TryGetValue(
                        transaction.ResourceStableId,
                        out current)
                        ? current.Quantity
                        : 0L;
                    long next = checked(previous + transaction.Quantity);
                    stackHoldings[transaction.ResourceStableId] =
                        new StackState(command.RewardKind, next);
                    if (!stackKindHistory.ContainsKey(
                        transaction.ResourceStableId))
                    {
                        stackKindHistory.Add(
                            transaction.ResourceStableId,
                            command.RewardKind);
                    }

                    return next;
                }

                case EconomyTransactionOperationV1.RemoveStack:
                {
                    StackState current =
                        stackHoldings[transaction.ResourceStableId];
                    long next = current.Quantity - transaction.Quantity;
                    if (next == 0L)
                    {
                        stackHoldings.Remove(transaction.ResourceStableId);
                    }
                    else
                    {
                        stackHoldings[transaction.ResourceStableId] =
                            new StackState(command.RewardKind, next);
                    }

                    return next;
                }

                default:
                    throw new InvalidOperationException(
                        "Validated holdings command has an unsupported operation.");
            }
        }

        private long GetHoldingQuantity(PlayerHoldingsCommandV1 command)
        {
            if (command == null || command.Transaction == null)
            {
                return 0L;
            }

            EconomyTransactionCommandV1 transaction = command.Transaction;
            if (transaction.InstanceStableId != null)
            {
                return uniqueHoldings.ContainsKey(transaction.InstanceStableId)
                    ? 1L
                    : 0L;
            }

            StackState stack;
            return stackHoldings.TryGetValue(
                transaction.ResourceStableId,
                out stack)
                ? stack.Quantity
                : 0L;
        }

        private static LedgerMutation<HoldingsLedgerVocabularyV1>
            BuildLedgerMutation(PlayerHoldingsCommandV1 command)
        {
            EconomyTransactionCommandV1 transaction = command.Transaction;
            StableId targetId = transaction.InstanceStableId
                ?? transaction.ResourceStableId;
            long delta = transaction.Operation
                    == EconomyTransactionOperationV1.RemoveStack
                || transaction.Operation
                    == EconomyTransactionOperationV1.RemoveUnique
                    ? -transaction.Quantity
                    : transaction.Quantity;
            var entry = new LedgerEntry<HoldingsLedgerVocabularyV1>(
                HoldingsEntryTypeIdsV1.FromRewardKind(command.RewardKind),
                targetId,
                command.PayloadFingerprint);
            return new LedgerMutation<HoldingsLedgerVocabularyV1>(
                transaction.TransactionStableId,
                entry,
                delta,
                transaction.ExpectedSequence);
        }

        private static PlayerHoldingsMutationStatusV1 MapStatus(
            LedgerMutationResult<HoldingsLedgerVocabularyV1> result)
        {
            switch (result.Status)
            {
                case LedgerMutationStatus.Applied:
                    return PlayerHoldingsMutationStatusV1.Applied;
                case LedgerMutationStatus.SequenceConflict:
                    return PlayerHoldingsMutationStatusV1.ExpectedSequenceConflict;
                case LedgerMutationStatus.ValidationRejected:
                case LedgerMutationStatus.PolicyRejected:
                    return MapRejection(result.RejectionCode);
                default:
                    return PlayerHoldingsMutationStatusV1.InvalidRequest;
            }
        }

        private static PlayerHoldingsMutationStatusV1 MapRejection(
            string rejectionCode)
        {
            switch (rejectionCode)
            {
                case "wrong-authority":
                    return PlayerHoldingsMutationStatusV1.WrongAuthority;
                case "wrong-reward-type":
                    return PlayerHoldingsMutationStatusV1.WrongRewardType;
                case "type-mismatch":
                    return PlayerHoldingsMutationStatusV1.TypeMismatch;
                case "unique-instance-collision":
                    return PlayerHoldingsMutationStatusV1.UniqueInstanceCollision;
                case "missing-item":
                    return PlayerHoldingsMutationStatusV1.MissingItem;
                case "insufficient-value":
                    return PlayerHoldingsMutationStatusV1.InsufficientValue;
                case "insufficient-capacity":
                    return PlayerHoldingsMutationStatusV1.InsufficientCapacity;
                case "equipment-validation-rejected":
                    return PlayerHoldingsMutationStatusV1.EquipmentValidationRejected;
                case "quantity-overflow":
                case "arithmetic-overflow":
                case "sequence-overflow":
                    return PlayerHoldingsMutationStatusV1.ArithmeticOverflow;
                default:
                    return PlayerHoldingsMutationStatusV1.InvalidRequest;
            }
        }

    }
}
