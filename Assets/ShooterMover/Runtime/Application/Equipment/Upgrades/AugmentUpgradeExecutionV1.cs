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
    public sealed partial class AugmentUpgradeServiceV1
    {
        private AugmentUpgradeFactV1 Execute(UpgradeRecord record)
        {
            PreparedUpgrade prepared = record.Prepared;
            MoneyWalletChangeFact moneyFact = moneyWallet.Apply(
                prepared.MoneyCommand);
            if (!IsApplied(moneyFact))
            {
                AugmentUpgradeConfirmationStatusV1 status =
                    moneyFact.Status == MoneyWalletTransactionStatus.SequenceConflict
                        ? AugmentUpgradeConfirmationStatusV1.WalletSequenceConflict
                        : moneyFact.Status == MoneyWalletTransactionStatus.InsufficientFunds
                            ? AugmentUpgradeConfirmationStatusV1.InsufficientFunds
                            : AugmentUpgradeConfirmationStatusV1.MoneyAuthorityRejected;
                record.Fact = prepared.CreateFact(
                    status,
                    status,
                    moneyWallet.Sequence,
                    holdings.Sequence,
                    moneyFact.RejectionCode ?? "upgrade-money-rejected");
                return record.Fact;
            }

            PlayerHoldingsMutationResultV1 removeFact = holdings.Apply(
                prepared.RemoveCommand);
            if (!IsApplied(removeFact))
            {
                AugmentUpgradeConfirmationStatusV1 status =
                    removeFact.Status
                        == PlayerHoldingsMutationStatusV1.ExpectedSequenceConflict
                        ? AugmentUpgradeConfirmationStatusV1.HoldingsSequenceConflict
                        : AugmentUpgradeConfirmationStatusV1.HoldingsAuthorityRejected;
                record.Fact = prepared.CreateFact(
                    status,
                    status,
                    moneyWallet.Sequence,
                    holdings.Sequence,
                    removeFact.RejectionCode ?? "upgrade-holdings-remove-rejected");
                return record.Fact;
            }

            RewardApplicationResultV1 rewardFact;
            if (record.ClaimBound)
            {
                rewardFact = rewardApplication.Retry(
                    RewardRetryClaimCommandV1.Create(
                        prepared.CommitmentStableId,
                        prepared.ClaimStableId));
            }
            else
            {
                rewardFact = rewardApplication.Claim(prepared.ClaimCommand);
            }

            if (rewardFact.Status == RewardApplicationResultStatusV1.Applied
                || rewardFact.Status
                    == RewardApplicationResultStatusV1.AlreadyAppliedNoChange)
            {
                record.ClaimBound = true;
                record.Fact = prepared.CreateFact(
                    AugmentUpgradeConfirmationStatusV1.Applied,
                    AugmentUpgradeConfirmationStatusV1.Applied,
                    moneyWallet.Sequence,
                    holdings.Sequence,
                    null);
                return record.Fact;
            }

            if (rewardFact.Status
                == RewardApplicationResultStatusV1.ClaimedPendingApplication)
            {
                record.ClaimBound = true;
                record.Fact = prepared.CreateFact(
                    AugmentUpgradeConfirmationStatusV1.PendingRetry,
                    AugmentUpgradeConfirmationStatusV1.PendingRetry,
                    moneyWallet.Sequence,
                    holdings.Sequence,
                    rewardFact.RejectionCode ?? "upgrade-reward-pending");
                return record.Fact;
            }

            if (rewardFact.Status
                    == RewardApplicationResultStatusV1.ExpectedSequenceConflict
                || rewardFact.Status
                    == RewardApplicationResultStatusV1.ChildAuthorityRejected
                || rewardFact.Status
                    == RewardApplicationResultStatusV1.CapacityRejected)
            {
                record.Fact = prepared.CreateFact(
                    AugmentUpgradeConfirmationStatusV1.PendingRetry,
                    AugmentUpgradeConfirmationStatusV1.PendingRetry,
                    moneyWallet.Sequence,
                    holdings.Sequence,
                    rewardFact.RejectionCode ?? "upgrade-reward-retryable");
                return record.Fact;
            }

            record.Fact = prepared.CreateFact(
                AugmentUpgradeConfirmationStatusV1.RewardApplicationRejected,
                AugmentUpgradeConfirmationStatusV1.RewardApplicationRejected,
                moneyWallet.Sequence,
                holdings.Sequence,
                rewardFact.RejectionCode ?? "upgrade-reward-application-rejected");
            return record.Fact;
        }

        private AugmentUpgradeFactV1 Replay(UpgradeRecord record)
        {
            AugmentUpgradeFactV1 original = record.Fact;
            if (original == null)
            {
                return record.Prepared.CreateFact(
                    AugmentUpgradeConfirmationStatusV1.PendingRetry,
                    AugmentUpgradeConfirmationStatusV1.PendingRetry,
                    moneyWallet.Sequence,
                    holdings.Sequence,
                    "upgrade-record-pending");
            }

            return AugmentUpgradeFactV1.Create(
                AugmentUpgradeConfirmationStatusV1.ExactDuplicateNoChange,
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

        private AugmentUpgradeFactV1 Conflict(
            UpgradeRecord existing,
            AugmentUpgradeConfirmationV1 incoming)
        {
            AugmentUpgradeFactV1 original = existing.Fact;
            PreparedUpgrade prepared = existing.Prepared;
            return AugmentUpgradeFactV1.Create(
                AugmentUpgradeConfirmationStatusV1.ConflictingDuplicate,
                original == null
                    ? AugmentUpgradeConfirmationStatusV1.PendingRetry
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

        private AugmentUpgradeFactV1 Failure(
            AugmentUpgradeConfirmationStatusV1 status,
            StableId confirmationStableId,
            string confirmationFingerprint,
            string rejectionCode,
            AugmentUpgradeQuoteV1 quote = null)
        {
            return AugmentUpgradeFactV1.Create(
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

        private static bool IsApplied(PlayerHoldingsMutationResultV1 fact)
        {
            return fact != null
                && (fact.Status == PlayerHoldingsMutationStatusV1.Applied
                    || (fact.Status
                            == PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange
                        && fact.OriginalStatus
                            == PlayerHoldingsMutationStatusV1.Applied));
        }

        private static AugmentUpgradeQuoteResultV1 QuoteFailure(
            AugmentUpgradeQuoteStatusV1 status,
            string rejectionCode)
        {
            return AugmentUpgradeQuoteResultV1.Create(status, null, rejectionCode);
        }

        private static AugmentUpgradeQuoteResultV1 QuoteCostFailure(
            AugmentUpgradeCostStatusV1 status)
        {
            switch (status)
            {
                case AugmentUpgradeCostStatusV1.InvalidTarget:
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatusV1.InvalidLevelJump,
                        "upgrade-level-jump-invalid");
                case AugmentUpgradeCostStatusV1.TierNotConfigured:
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatusV1.MissingCostCurve,
                        "upgrade-tier-cost-curve-missing");
                case AugmentUpgradeCostStatusV1.ArithmeticOverflow:
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatusV1.CostOverflow,
                        "upgrade-cost-overflow");
                default:
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatusV1.InvalidRequest,
                        "upgrade-cost-invalid");
            }
        }
    }
}
