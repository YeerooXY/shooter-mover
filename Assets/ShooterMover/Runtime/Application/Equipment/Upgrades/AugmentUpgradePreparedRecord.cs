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
        private sealed class UpgradeRecord
        {
            public UpgradeRecord(PreparedUpgrade prepared)
            {
                Prepared = prepared ?? throw new ArgumentNullException(nameof(prepared));
                Confirmation = prepared.Confirmation;
            }

            public PreparedUpgrade Prepared { get; }
            public AugmentUpgradeConfirmation Confirmation { get; }
            public bool ClaimBound { get; set; }
            public AugmentUpgradeFact Fact { get; set; }
        }

        private sealed class PreparedUpgrade
        {
            private PreparedUpgrade(
                AugmentUpgradeConfirmation confirmation,
                AugmentUpgradeQuote quote,
                EquipmentInstance replacement,
                StableId moneyTransactionStableId,
                StableId removeTransactionStableId,
                StableId commitmentStableId,
                StableId claimStableId,
                MoneyTransactionCommand moneyCommand,
                PlayerHoldingsCommand removeCommand,
                RewardCommitCommand commitCommand,
                RewardClaimCommand claimCommand)
            {
                Confirmation = confirmation;
                Quote = quote;
                Replacement = replacement;
                MoneyTransactionStableId = moneyTransactionStableId;
                RemoveTransactionStableId = removeTransactionStableId;
                CommitmentStableId = commitmentStableId;
                ClaimStableId = claimStableId;
                MoneyCommand = moneyCommand;
                RemoveCommand = removeCommand;
                CommitCommand = commitCommand;
                ClaimCommand = claimCommand;
            }

            public AugmentUpgradeConfirmation Confirmation { get; }
            public AugmentUpgradeQuote Quote { get; }
            public EquipmentInstance Replacement { get; }
            public StableId MoneyTransactionStableId { get; }
            public StableId RemoveTransactionStableId { get; }
            public StableId CommitmentStableId { get; }
            public StableId ClaimStableId { get; }
            public MoneyTransactionCommand MoneyCommand { get; }
            public PlayerHoldingsCommand RemoveCommand { get; }
            public RewardCommitCommand CommitCommand { get; }
            public RewardClaimCommand ClaimCommand { get; }

            public static PreparedUpgrade Create(
                AugmentUpgradeConfirmation confirmation,
                AugmentUpgradeQuote quote,
                UniqueHoldingSnapshot holding,
                EquipmentInstance replacement,
                AugmentUpgradeIdentityContext identityContext,
                StableId holdingsAuthorityStableId)
            {
                string identityInput = confirmation.Fingerprint
                    + "|"
                    + quote.QuoteFingerprint
                    + "|"
                    + replacement.Fingerprint;
                StableId moneyTransactionId = AugmentUpgrade.DeriveStableId(
                    "augmoney",
                    identityInput + "|transaction");
                StableId moneyOperationId = AugmentUpgrade.DeriveStableId(
                    "augop",
                    identityInput + "|money-operation");
                StableId removeTransactionId = AugmentUpgrade.DeriveStableId(
                    "augremove",
                    identityInput + "|transaction");
                StableId removeOperationId = AugmentUpgrade.DeriveStableId(
                    "augop",
                    identityInput + "|remove-operation");
                StableId sourceOperationId = AugmentUpgrade.DeriveStableId(
                    "augsource",
                    identityInput + "|reward-source");
                StableId commitmentId = AugmentUpgrade.DeriveStableId(
                    "augcommit",
                    identityInput + "|commitment");
                StableId claimId = AugmentUpgrade.DeriveStableId(
                    "augclaim",
                    identityInput + "|claim");
                StableId grantId = AugmentUpgrade.DeriveStableId(
                    "auggrant",
                    identityInput + "|grant");

                MoneyTransactionCommand moneyCommand =
                    MoneyTransactionCommand.CreateSpend(
                        moneyTransactionId,
                        moneyOperationId,
                        quote.MoneyCost,
                        quote.WalletSequence);
                PlayerHoldingsCommand removeCommand =
                    PlayerHoldingsCommand.RemoveEquipment(
                        removeTransactionId,
                        removeOperationId,
                        holdingsAuthorityStableId,
                        holding.DefinitionStableId,
                        holding.InstanceStableId,
                        holding.Provenance,
                        quote.HoldingsSequence);

                RewardOperationRequest operation = RewardOperationRequest.Create(
                    identityContext.RunStableId,
                    identityContext.SourceInstanceStableId,
                    sourceOperationId,
                    commitmentId,
                    identityContext.RewardProfileStableId,
                    quote.QuoteFingerprint);
                RewardGrant grant = RewardGrant.Create(
                    grantId,
                    RewardGrantKind.EquipmentReference,
                    replacement.DefinitionId,
                    1L);
                RewardCommitCommand commitCommand = RewardCommitCommand.Create(
                    operation,
                    RewardResult.CreateGrants(
                        commitmentId,
                        sourceOperationId,
                        new[] { grant }),
                    AugmentUpgrade.Fingerprint(
                        "augment-upgrade-generation|" + replacement.Fingerprint),
                    new[]
                    {
                        RewardGrantApplicationPayload.ForEquipment(
                            grant,
                            new[] { replacement }),
                    });
                RewardClaimCommand claimCommand = RewardClaimCommand.Create(
                    claimId,
                    commitmentId,
                    identityContext.ClaimantStableId,
                    MoneyWalletIds.AuthorityStableId,
                    identityContext.ScrapAuthorityStableId,
                    holdingsAuthorityStableId,
                    quote.WalletSequence + 1L,
                    null,
                    quote.HoldingsSequence + 1L);

                return new PreparedUpgrade(
                    confirmation,
                    quote,
                    replacement,
                    moneyTransactionId,
                    removeTransactionId,
                    commitmentId,
                    claimId,
                    moneyCommand,
                    removeCommand,
                    commitCommand,
                    claimCommand);
            }

            public AugmentUpgradeFact CreateFact(
                AugmentUpgradeConfirmationStatus status,
                AugmentUpgradeConfirmationStatus originalStatus,
                long walletSequenceAfter,
                long holdingsSequenceAfter,
                string rejectionCode)
            {
                return AugmentUpgradeFact.Create(
                    status,
                    originalStatus,
                    Confirmation.ConfirmationStableId,
                    Confirmation.Fingerprint,
                    Quote.QuoteFingerprint,
                    MoneyTransactionStableId,
                    RemoveTransactionStableId,
                    Replacement.InstanceId,
                    Replacement.Fingerprint,
                    CommitmentStableId,
                    ClaimStableId,
                    Quote.MoneyCost,
                    Quote.WalletSequence,
                    walletSequenceAfter,
                    Quote.HoldingsSequence,
                    holdingsSequenceAfter,
                    rejectionCode);
            }
        }
    }
}
