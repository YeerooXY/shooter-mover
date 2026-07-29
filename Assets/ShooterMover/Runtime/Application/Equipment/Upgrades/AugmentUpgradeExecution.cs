using System;
using System.Collections.Generic;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Equipment.Upgrades;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Equipment.Upgrades
{
    public sealed partial class AugmentUpgradeActions
    {
        private AugmentUpgradeFact Execute(UpgradeRecord record)
        {
            PreparedUpgrade prepared = record.Prepared;
            MoneyWalletChangeFact moneyFact = moneyWallet.Apply(
                prepared.MoneyCommand);
            if (!IsApplied(moneyFact))
            {
                AugmentUpgradeConfirmationStatus status =
                    moneyFact.Status == MoneyWalletTransactionStatus.SequenceConflict
                        ? AugmentUpgradeConfirmationStatus.WalletSequenceConflict
                        : moneyFact.Status == MoneyWalletTransactionStatus.InsufficientFunds
                            ? AugmentUpgradeConfirmationStatus.InsufficientFunds
                            : AugmentUpgradeConfirmationStatus.MoneyAuthorityRejected;
                record.Fact = prepared.CreateFact(
                    status,
                    status,
                    moneyWallet.Sequence,
                    holdings.Sequence,
                    moneyFact.RejectionCode ?? "upgrade-money-rejected");
                return record.Fact;
            }

            PlayerHoldingsMutationResult removeFact = holdings.Apply(
                prepared.RemoveCommand);
            if (!IsApplied(removeFact))
            {
                AugmentUpgradeConfirmationStatus status =
                    removeFact.Status
                        == PlayerHoldingsMutationStatus.ExpectedSequenceConflict
                        ? AugmentUpgradeConfirmationStatus.HoldingsSequenceConflict
                        : AugmentUpgradeConfirmationStatus.HoldingsAuthorityRejected;
                record.Fact = prepared.CreateFact(
                    status,
                    status,
                    moneyWallet.Sequence,
                    holdings.Sequence,
                    removeFact.RejectionCode ?? "upgrade-holdings-remove-rejected");
                return record.Fact;
            }

            RewardApplicationResult rewardFact;
            if (record.ClaimBound)
            {
                rewardFact = rewardApplication.Retry(
                    RewardRetryClaimCommand.Create(
                        prepared.CommitmentStableId,
                        prepared.ClaimStableId));
            }
            else
            {
                rewardFact = rewardApplication.Claim(prepared.ClaimCommand);
            }

            if (rewardFact.Status == RewardApplicationResultStatus.Applied
                || rewardFact.Status
                    == RewardApplicationResultStatus.AlreadyAppliedNoChange)
            {
                record.ClaimBound = true;
                record.Fact = prepared.CreateFact(
                    AugmentUpgradeConfirmationStatus.Applied,
                    AugmentUpgradeConfirmationStatus.Applied,
                    moneyWallet.Sequence,
                    holdings.Sequence,
                    null);
                return record.Fact;
            }

            if (rewardFact.Status
                == RewardApplicationResultStatus.ClaimedPendingApplication)
            {
                record.ClaimBound = true;
                record.Fact = prepared.CreateFact(
                    AugmentUpgradeConfirmationStatus.PendingRetry,
                    AugmentUpgradeConfirmationStatus.PendingRetry,
                    moneyWallet.Sequence,
                    holdings.Sequence,
                    rewardFact.RejectionCode ?? "upgrade-reward-pending");
                return record.Fact;
            }

            if (rewardFact.Status
                    == RewardApplicationResultStatus.ExpectedSequenceConflict
                || rewardFact.Status
                    == RewardApplicationResultStatus.ChildAuthorityRejected
                || rewardFact.Status
                    == RewardApplicationResultStatus.CapacityRejected)
            {
                record.Fact = prepared.CreateFact(
                    AugmentUpgradeConfirmationStatus.PendingRetry,
                    AugmentUpgradeConfirmationStatus.PendingRetry,
                    moneyWallet.Sequence,
                    holdings.Sequence,
                    rewardFact.RejectionCode ?? "upgrade-reward-retryable");
                return record.Fact;
            }

            record.Fact = prepared.CreateFact(
                AugmentUpgradeConfirmationStatus.RewardApplicationRejected,
                AugmentUpgradeConfirmationStatus.RewardApplicationRejected,
                moneyWallet.Sequence,
                holdings.Sequence,
                rewardFact.RejectionCode ?? "upgrade-reward-application-rejected");
            return record.Fact;
        }

        private AugmentUpgradeFact Replay(UpgradeRecord record)
        {
            AugmentUpgradeFact original = record.Fact;
            if (original == null)
            {
                return record.Prepared.CreateFact(
                    AugmentUpgradeConfirmationStatus.PendingRetry,
                    AugmentUpgradeConfirmationStatus.PendingRetry,
                    moneyWallet.Sequence,
                    holdings.Sequence,
                    "upgrade-record-pending");
            }

            return AugmentUpgradeFact.Create(
                AugmentUpgradeConfirmationStatus.ExactDuplicateNoChange,
                original.OriginalStatus,
                original.ConfirmationStableId,
                original.ConfirmationFingerprint,
                original.QuoteFingerprint,
                original.MoneyTransactionStableId,
                original.HoldingsRemoveTransactionStableId,
                original.ReplacementEquipmentInstanceStableId,
                original.ReplacementEquipmentFingerprint,
                original.RewardCommitmentStableId,
                original.RewardClaimStableId,
                original.MoneyCost,
                original.WalletSequenceBefore,
                original.WalletSequenceAfter,
                original.HoldingsSequenceBefore,
                original.HoldingsSequenceAfter,
                original.RejectionCode);
        }

        private AugmentUpgradeFact Conflict(
            UpgradeRecord existing,
            AugmentUpgradeConfirmation incoming)
        {
            AugmentUpgradeFact original = existing.Fact;
            PreparedUpgrade prepared = existing.Prepared;
            return AugmentUpgradeFact.Create(
                AugmentUpgradeConfirmationStatus.ConflictingDuplicate,
                original == null
                    ? AugmentUpgradeConfirmationStatus.PendingRetry
                    : original.OriginalStatus,
                incoming.ConfirmationStableId,
                incoming.Fingerprint,
                incoming.Quote == null ? null : incoming.Quote.QuoteFingerprint,
                prepared.MoneyTransactionStableId,
                prepared.RemoveTransactionStableId,
                prepared.Replacement.InstanceId,
                prepared.Replacement.Fingerprint,
                prepared.CommitmentStableId,
                prepared.ClaimStableId,
                prepared.Quote.MoneyCost,
                prepared.Quote.WalletSequence,
                moneyWallet.Sequence,
                prepared.Quote.HoldingsSequence,
                holdings.Sequence,
                "upgrade-confirmation-conflicting-duplicate");
        }

        private AugmentUpgradeFact Failure(
            AugmentUpgradeConfirmationStatus status,
            StableId confirmationStableId,
            string confirmationFingerprint,
            string rejectionCode,
            AugmentUpgradeQuote quote = null)
        {
            return AugmentUpgradeFact.Create(
                status,
                status,
                confirmationStableId,
                confirmationFingerprint,
                quote == null ? null : quote.QuoteFingerprint,
                null,
                null,
                null,
                null,
                null,
                null,
                quote == null ? 0L : quote.MoneyCost,
                quote == null ? moneyWallet.Sequence : quote.WalletSequence,
                moneyWallet.Sequence,
                quote == null ? holdings.Sequence : quote.HoldingsSequence,
                holdings.Sequence,
                rejectionCode);
        }

        private static EquipmentInstance CreateReplacement(
            EquipmentInstance equipment,
            AugmentInstance augment,
            int targetLevel,
            StableId replacementId)
        {
            AugmentInstance upgraded = augment.WithLevel(targetLevel);
            var augments = new List<AugmentInstance>(equipment.Augments.Count);
            for (int index = 0; index < equipment.Augments.Count; index++)
            {
                AugmentInstance current = equipment.Augments[index];
                augments.Add(
                    current != null && current.InstanceId == augment.InstanceId
                        ? upgraded
                        : current);
            }

            return EquipmentInstance.Create(
                replacementId,
                equipment.DefinitionId,
                equipment.ItemLevel,
                equipment.QualityId,
                augments);
        }

        private static AugmentInstance FindAugment(
            EquipmentInstance equipment,
            StableId augmentInstanceStableId,
            out int slotIndex)
        {
            slotIndex = -1;
            if (equipment == null
                || equipment.Augments == null
                || augmentInstanceStableId == null)
            {
                return null;
            }

            for (int index = 0; index < equipment.Augments.Count; index++)
            {
                AugmentInstance augment = equipment.Augments[index];
                if (augment != null
                    && augment.InstanceId == augmentInstanceStableId)
                {
                    slotIndex = index;
                    return augment;
                }
            }

            return null;
        }

        private static bool IsApplied(MoneyWalletChangeFact fact)
        {
            return fact != null
                && (fact.Status == MoneyWalletTransactionStatus.Applied
                    || (fact.Status == MoneyWalletTransactionStatus.DuplicateNoChange
                        && fact.OriginalStatus
                            == MoneyWalletTransactionStatus.Applied));
        }

        private static bool IsApplied(PlayerHoldingsMutationResult fact)
        {
            return fact != null
                && (fact.Status == PlayerHoldingsMutationStatus.Applied
                    || (fact.Status
                            == PlayerHoldingsMutationStatus.ExactDuplicateNoChange
                        && fact.OriginalStatus
                            == PlayerHoldingsMutationStatus.Applied));
        }

        private static AugmentUpgradeQuoteResult QuoteFailure(
            AugmentUpgradeQuoteStatus status,
            string rejectionCode)
        {
            return AugmentUpgradeQuoteResult.Create(status, null, rejectionCode);
        }

        private static AugmentUpgradeQuoteResult QuoteCostFailure(
            AugmentUpgradeCostStatus status)
        {
            switch (status)
            {
                case AugmentUpgradeCostStatus.InvalidTarget:
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatus.InvalidLevelJump,
                        "upgrade-level-jump-invalid");
                case AugmentUpgradeCostStatus.TierNotConfigured:
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatus.MissingCostCurve,
                        "upgrade-tier-cost-curve-missing");
                case AugmentUpgradeCostStatus.ArithmeticOverflow:
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatus.CostOverflow,
                        "upgrade-cost-overflow");
                default:
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatus.InvalidRequest,
                        "upgrade-cost-invalid");
            }
        }
    }
}
