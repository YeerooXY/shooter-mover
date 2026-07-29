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
    public sealed partial class PlayerHoldingsActions
    {
        private IdempotentLedger<HoldingsLedgerVocabulary> CreateLedger()
        {
            return new IdempotentLedger<HoldingsLedgerVocabulary>(
                ValidatePendingMutation,
                delegate(LedgerMutationContext<HoldingsLedgerVocabulary> context)
                {
                    return LedgerDecision.Accept();
                });
        }

        private LedgerDecision ValidatePendingMutation(
            LedgerMutationContext<HoldingsLedgerVocabulary> context)
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
            PlayerHoldingsCommand command,
            IDictionary<StableId, UniqueHoldingSnapshot> currentUnique,
            IDictionary<StableId, UniqueIdentityHistory> historicalUnique,
            IDictionary<StableId, StackState> currentStacks,
            IDictionary<StableId, RewardGrantKind> historicalStackKinds,
            out string rejectionCode)
        {
            EconomyTransactionCommand transaction = command.Transaction;
            if (transaction.AuthorityStableId != AuthorityStableId)
            {
                rejectionCode = "wrong-authority";
                return false;
            }

            bool isEquipment =
                command.RewardKind == RewardGrantKind.EquipmentReference;
            bool isStrongbox =
                command.RewardKind == RewardGrantKind.Strongbox;
            bool isStack =
                command.RewardKind == RewardGrantKind.PremiumAmmo
                || command.RewardKind == RewardGrantKind.Miscellaneous;
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
            PlayerHoldingsCommand command,
            IDictionary<StableId, UniqueHoldingSnapshot> currentUnique,
            IDictionary<StableId, UniqueIdentityHistory> historicalUnique,
            out string rejectionCode)
        {
            EconomyTransactionCommand transaction = command.Transaction;
            EconomyResourceKind expectedResourceKind =
                command.RewardKind == RewardGrantKind.EquipmentReference
                    ? EconomyResourceKind.EquipmentReference
                    : EconomyResourceKind.Strongbox;
            bool isAdd =
                transaction.Operation == EconomyTransactionOperation.AddUnique;
            bool isRemove =
                transaction.Operation == EconomyTransactionOperation.RemoveUnique;
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

                if (command.RewardKind == RewardGrantKind.EquipmentReference)
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

            UniqueHoldingSnapshot existing;
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
            PlayerHoldingsCommand command,
            IDictionary<StableId, StackState> currentStacks,
            IDictionary<StableId, RewardGrantKind> historicalStackKinds,
            out string rejectionCode)
        {
            EconomyTransactionCommand transaction = command.Transaction;
            bool isAdd =
                transaction.Operation == EconomyTransactionOperation.AddStack;
            bool isRemove =
                transaction.Operation == EconomyTransactionOperation.RemoveStack;
            if (transaction.ResourceKind != EconomyResourceKind.Item
                || (!isAdd && !isRemove)
                || transaction.InstanceStableId != null
                || command.EquipmentInstance != null)
            {
                rejectionCode = "type-mismatch";
                return false;
            }

            RewardGrantKind historicalKind;
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

        private long CommitHoldingMutation(PlayerHoldingsCommand command)
        {
            EconomyTransactionCommand transaction = command.Transaction;
            switch (transaction.Operation)
            {
                case EconomyTransactionOperation.AddUnique:
                {
                    var holding = UniqueHoldingSnapshot.Create(
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

                case EconomyTransactionOperation.RemoveUnique:
                    uniqueHoldings.Remove(transaction.InstanceStableId);
                    return 0L;

                case EconomyTransactionOperation.AddStack:
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

                case EconomyTransactionOperation.RemoveStack:
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

        private long GetHoldingQuantity(PlayerHoldingsCommand command)
        {
            if (command == null || command.Transaction == null)
            {
                return 0L;
            }

            EconomyTransactionCommand transaction = command.Transaction;
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

        private static LedgerMutation<HoldingsLedgerVocabulary>
            BuildLedgerMutation(PlayerHoldingsCommand command)
        {
            EconomyTransactionCommand transaction = command.Transaction;
            StableId targetId = transaction.InstanceStableId
                ?? transaction.ResourceStableId;
            long delta = transaction.Operation
                    == EconomyTransactionOperation.RemoveStack
                || transaction.Operation
                    == EconomyTransactionOperation.RemoveUnique
                    ? -transaction.Quantity
                    : transaction.Quantity;
            var entry = new LedgerEntry<HoldingsLedgerVocabulary>(
                HoldingsEntryTypeIds.FromRewardKind(command.RewardKind),
                targetId,
                command.PayloadFingerprint);
            return new LedgerMutation<HoldingsLedgerVocabulary>(
                transaction.TransactionStableId,
                entry,
                delta,
                transaction.ExpectedSequence);
        }

        private static PlayerHoldingsMutationStatus MapStatus(
            LedgerMutationResult<HoldingsLedgerVocabulary> result)
        {
            switch (result.Status)
            {
                case LedgerMutationStatus.Applied:
                    return PlayerHoldingsMutationStatus.Applied;
                case LedgerMutationStatus.SequenceConflict:
                    return PlayerHoldingsMutationStatus.ExpectedSequenceConflict;
                case LedgerMutationStatus.ValidationRejected:
                case LedgerMutationStatus.PolicyRejected:
                    return MapRejection(result.RejectionCode);
                default:
                    return PlayerHoldingsMutationStatus.InvalidRequest;
            }
        }

        private static PlayerHoldingsMutationStatus MapRejection(
            string rejectionCode)
        {
            switch (rejectionCode)
            {
                case "wrong-authority":
                    return PlayerHoldingsMutationStatus.WrongAuthority;
                case "wrong-reward-type":
                    return PlayerHoldingsMutationStatus.WrongRewardType;
                case "type-mismatch":
                    return PlayerHoldingsMutationStatus.TypeMismatch;
                case "unique-instance-collision":
                    return PlayerHoldingsMutationStatus.UniqueInstanceCollision;
                case "missing-item":
                    return PlayerHoldingsMutationStatus.MissingItem;
                case "insufficient-value":
                    return PlayerHoldingsMutationStatus.InsufficientValue;
                case "insufficient-capacity":
                    return PlayerHoldingsMutationStatus.InsufficientCapacity;
                case "equipment-validation-rejected":
                    return PlayerHoldingsMutationStatus.EquipmentValidationRejected;
                case "quantity-overflow":
                case "arithmetic-overflow":
                case "sequence-overflow":
                    return PlayerHoldingsMutationStatus.ArithmeticOverflow;
                default:
                    return PlayerHoldingsMutationStatus.InvalidRequest;
            }
        }

    }
}
