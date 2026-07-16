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
        private bool TryPrepare(
            AugmentUpgradeConfirmationV1 confirmation,
            out PreparedUpgrade prepared,
            out AugmentUpgradeFactV1 failure)
        {
            prepared = null;
            failure = null;
            AugmentUpgradeQuoteV1 quote = confirmation.Quote;
            if (quote == null
                || quote.EquipmentInstanceStableId == null
                || quote.AugmentInstanceStableId == null
                || quote.AugmentDefinitionStableId == null
                || quote.MoneyCost < 1L)
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.InvalidRequest,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-quote-invalid");
                return false;
            }

            if (!string.Equals(
                confirmation.QuotedFingerprint,
                quote.QuoteFingerprint,
                StringComparison.Ordinal))
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.StaleQuote,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-quote-fingerprint-stale",
                    quote);
                return false;
            }

            EquipmentCatalog catalog = catalogProvider.Catalog;
            if (catalog == null)
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.StaleCatalog,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-catalog-missing",
                    quote);
                return false;
            }

            if (!string.Equals(
                quote.CatalogFingerprint,
                catalog.Fingerprint,
                StringComparison.Ordinal))
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.StaleCatalog,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-catalog-stale",
                    quote);
                return false;
            }

            if (!string.Equals(
                quote.CostPolicyFingerprint,
                costPolicy.Fingerprint,
                StringComparison.Ordinal))
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.StaleCostPolicy,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-cost-policy-stale",
                    quote);
                return false;
            }

            UniqueHoldingSnapshotV1 holding;
            if (!holdings.TryGetUnique(quote.EquipmentInstanceStableId, out holding)
                || holding == null
                || holding.RewardKind != RewardGrantKindV1.EquipmentReference
                || holding.EquipmentInstance == null)
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.MissingEquipment,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-equipment-missing",
                    quote);
                return false;
            }

            EquipmentInstance equipment = holding.EquipmentInstance;
            if (!string.Equals(
                quote.EquipmentFingerprint,
                equipment.Fingerprint,
                StringComparison.Ordinal))
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.StaleEquipmentFingerprint,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-equipment-fingerprint-stale",
                    quote);
                return false;
            }

            int slotIndex;
            AugmentInstance augment = FindAugment(
                equipment,
                quote.AugmentInstanceStableId,
                out slotIndex);
            if (augment == null
                || augment.DefinitionId != quote.AugmentDefinitionStableId
                || slotIndex != quote.AugmentSlotIndex)
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.MissingAugment,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-augment-missing",
                    quote);
                return false;
            }

            if (augment.Level != quote.CurrentLevel
                || augment.Tier != quote.AugmentTier)
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.CurrentLevelMismatch,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-current-level-stale",
                    quote);
                return false;
            }

            AugmentDefinition definition = catalog.FindAugmentDefinition(
                augment.DefinitionId);
            if (definition == null || definition.LevelRange == null)
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.MissingAugment,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-augment-definition-missing",
                    quote);
                return false;
            }

            if (augment.Level >= definition.LevelRange.Maximum
                || quote.TargetLevel > definition.LevelRange.Maximum)
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.MaximumLevel,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-maximum-level",
                    quote);
                return false;
            }

            long currentCost;
            AugmentUpgradeCostStatusV1 costStatus = costPolicy.TryCalculateCost(
                augment.Tier,
                augment.Level,
                quote.TargetLevel,
                out currentCost);
            if (costStatus == AugmentUpgradeCostStatusV1.InvalidTarget)
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.InvalidLevelJump,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-level-jump-invalid",
                    quote);
                return false;
            }

            if (costStatus != AugmentUpgradeCostStatusV1.Calculated
                || currentCost != quote.MoneyCost)
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.StaleQuote,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-cost-stale",
                    quote);
                return false;
            }

            if (moneyWallet.Sequence != quote.WalletSequence)
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.WalletSequenceConflict,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-wallet-sequence-conflict",
                    quote);
                return false;
            }

            if (holdings.Sequence != quote.HoldingsSequence)
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.HoldingsSequenceConflict,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-holdings-sequence-conflict",
                    quote);
                return false;
            }

            if (moneyWallet.Balance < quote.MoneyCost)
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.InsufficientFunds,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-insufficient-funds",
                    quote);
                return false;
            }

            StableId replacementId = AugmentUpgradeCanonicalV1.DeriveStableId(
                "augitem",
                confirmation.Fingerprint + "|replacement");
            EquipmentInstance replacement = CreateReplacement(
                equipment,
                augment,
                quote.TargetLevel,
                replacementId);
            EquipmentInstanceValidationResponse validation = equipmentValidator.Validate(
                new EquipmentInstanceValidationRequest(replacement));
            if (validation == null
                || !validation.IsValid
                || !string.Equals(
                    validation.InstanceFingerprint,
                    replacement.Fingerprint,
                    StringComparison.Ordinal))
            {
                failure = Failure(
                    AugmentUpgradeConfirmationStatusV1.EquipmentValidationRejected,
                    confirmation.ConfirmationStableId,
                    confirmation.Fingerprint,
                    "upgrade-replacement-validation-rejected",
                    quote);
                return false;
            }

            prepared = PreparedUpgrade.Create(
                confirmation,
                quote,
                holding,
                replacement,
                identityContext,
                holdings.AuthorityStableId);
            return true;
        }
    }
}
