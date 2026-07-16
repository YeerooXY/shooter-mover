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
        private sealed class UpgradeRecord
        {
            public UpgradeRecord(PreparedUpgrade prepared)
            {
                Prepared = prepared ?? throw new ArgumentNullException(nameof(prepared));
                Confirmation = prepared.Confirmation;
            }

            public PreparedUpgrade Prepared { get; }
            public AugmentUpgradeConfirmationV1 Confirmation { get; }
            public bool ClaimBound { get; set; }
            public AugmentUpgradeFactV1 Fact { get; set; }
        }

        private sealed class PreparedUpgrade
        {
            private PreparedUpgrade(
                AugmentUpgradeConfirmationV1 confirmation,
                AugmentUpgradeQuoteV1 quote,
                EquipmentInstance replacement,
                StableId moneyTransactionStableId,
                StableId removeTransactionStableId,
                StableId commitmentStableId,
                StableId claimStableId,
                MoneyTransactionCommand moneyCommand,
                PlayerHoldingsCommandV1 removeCommand,
                RewardCommitCommandV1 commitCommand,
                RewardClaimCommandV1 claimCommand)
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

            public AugmentUpgradeConfirmationV1 Confirmation { get; }
            public AugmentUpgradeQuoteV1 Quote { get; }
            public EquipmentInstance Replacement { get; }
            public StableId MoneyTransactionStableId { get; }
            public StableId RemoveTransactionStableId { get; }
            public StableId CommitmentStableId { get; }
            public StableId ClaimStableId { get; }
            public MoneyTransactionCommand MoneyCommand { get; }
            public PlayerHoldingsCommandV1 RemoveCommand { get; }
            public RewardCommitCommandV1 CommitCommand { get; }
            public RewardClaimCommandV1 ClaimCommand { get; }

            public static PreparedUpgrade Create(
                AugmentUpgradeConfirmationV1 confirmation,
                AugmentUpgradeQuoteV1 quote,
                UniqueHoldingSnapshotV1 holding,
                EquipmentInstance replacement,
                AugmentUpgradeIdentityContextV1 identityContext,
                StableId holdingsAuthorityStableId)
            {
                string identityInput = confirmation.Fingerprint
                    + "|"
                    + quote.QuoteFingerprint
                    + "|"
                    + replacement.Fingerprint;
                StableId moneyTransactionId = AugmentUpgradeCanonicalV1.DeriveStableId(
                    "augmoney",
                    identityInput + "|transaction");
                StableId moneyOperationId = AugmentUpgradeCanonicalV1.DeriveStableId(
                    "augop",
                    identityInput + "|money-operation");
                StableId removeTransactionId = AugmentUpgradeCanonicalV1.DeriveStableId(
                    "augremove",
                    identityInput + "|transaction");
                StableId removeOperationId = AugmentUpgradeCanonicalV1.DeriveStableId(
                    "augop",
                    identityInput + "|remove-operation");
                StableId sourceOperationId = AugmentUpgradeCanonicalV1.DeriveStableId(
                    "augsource",
                    identityInput + "|reward-source");
                StableId commitmentId = AugmentUpgradeCanonicalV1.DeriveStableId(
                    "augcommit",
                    identityInput + "|commitment");
                StableId claimId = AugmentUpgradeCanonicalV1.DeriveStableId(
                    "augclaim",
                    identityInput + "|claim");
                StableId grantId = AugmentUpgradeCanonicalV1.DeriveStableId(
                    "auggrant",
                    identityInput + "|grant");

                MoneyTransactionCommand moneyCommand =
                    MoneyTransactionCommand.CreateSpend(
                        moneyTransactionId,
                        moneyOperationId,
                        quote.MoneyCost,
                        quote.WalletSequence);
                PlayerHoldingsCommandV1 removeCommand =
                    PlayerHoldingsCommandV1.RemoveEquipment(
                        removeTransactionId,
                        removeOperationId,
                        holdingsAuthorityStableId,
                        holding.DefinitionStableId,
                        holding.InstanceStableId,
                        holding.Provenance,
                        quote.HoldingsSequence);

                RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                    identityContext.RunStableId,
                    identityContext.SourceInstanceStableId,
                    sourceOperationId,
                    commitmentId,
                    identityContext.RewardProfileStableId,
                    quote.QuoteFingerprint);
                RewardGrantV1 grant = RewardGrantV1.Create(
                    grantId,
                    RewardGrantKindV1.EquipmentReference,
                    replacement.DefinitionId,
                    1L);
                RewardCommitCommandV1 commitCommand = RewardCommitCommandV1.Create(
                    operation,
                    RewardResultV1.CreateGrants(
                        commitmentId,
                        sourceOperationId,
                        new[] { grant }),
                    AugmentUpgradeCanonicalV1.Fingerprint(
                        "augment-upgrade-generation|" + replacement.Fingerprint),
                    new[]
                    {
                        RewardGrantApplicationPayloadV1.ForEquipment(
                            grant,
                            new[] { replacement }),
                    });
                RewardClaimCommandV1 claimCommand = RewardClaimCommandV1.Create(
                    claimId,
                    commitmentId,
                    identityContext.ClaimantStableId,
                    MoneyWalletIdsV1.AuthorityStableId,
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

            public AugmentUpgradeFactV1 CreateFact(
                AugmentUpgradeConfirmationStatusV1 status,
                AugmentUpgradeConfirmationStatusV1 originalStatus,
                long walletSequenceAfter,
                long holdingsSequenceAfter,
                string rejectionCode)
            {
                return AugmentUpgradeFactV1.Create(
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
