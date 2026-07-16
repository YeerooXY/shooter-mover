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
        private readonly object sync = new object();
        private readonly MoneyWalletService moneyWallet;
        private readonly PlayerHoldingsService holdings;
        private readonly RewardApplicationServiceV1 rewardApplication;
        private readonly IEquipmentCatalogProvider catalogProvider;
        private readonly IEquipmentInstanceValidator equipmentValidator;
        private readonly AugmentUpgradeCostPolicyV1 costPolicy;
        private readonly AugmentUpgradeIdentityContextV1 identityContext;
        private readonly Dictionary<StableId, UpgradeRecord> records;

        public AugmentUpgradeServiceV1(
            MoneyWalletService moneyWallet,
            PlayerHoldingsService holdings,
            RewardApplicationServiceV1 rewardApplication,
            IEquipmentCatalogProvider catalogProvider,
            IEquipmentInstanceValidator equipmentValidator,
            AugmentUpgradeCostPolicyV1 costPolicy,
            AugmentUpgradeIdentityContextV1 identityContext)
        {
            this.moneyWallet = moneyWallet
                ?? throw new ArgumentNullException(nameof(moneyWallet));
            this.holdings = holdings
                ?? throw new ArgumentNullException(nameof(holdings));
            this.rewardApplication = rewardApplication
                ?? throw new ArgumentNullException(nameof(rewardApplication));
            this.catalogProvider = catalogProvider
                ?? throw new ArgumentNullException(nameof(catalogProvider));
            this.equipmentValidator = equipmentValidator
                ?? throw new ArgumentNullException(nameof(equipmentValidator));
            this.costPolicy = costPolicy
                ?? throw new ArgumentNullException(nameof(costPolicy));
            this.identityContext = identityContext
                ?? throw new ArgumentNullException(nameof(identityContext));
            records = new Dictionary<StableId, UpgradeRecord>();
        }

        public AugmentUpgradeCostPolicyV1 CostPolicy
        {
            get { return costPolicy; }
        }

        public AugmentUpgradeQuoteResultV1 Quote(
            AugmentUpgradeQuoteRequestV1 request)
        {
            lock (sync)
            {
                if (request == null
                    || request.EquipmentInstanceStableId == null
                    || request.AugmentInstanceStableId == null
                    || request.TargetLevel < 1)
                {
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatusV1.InvalidRequest,
                        "upgrade-quote-invalid");
                }

                EquipmentCatalog catalog = catalogProvider.Catalog;
                if (catalog == null)
                {
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatusV1.InvalidCatalog,
                        "upgrade-catalog-missing");
                }

                UniqueHoldingSnapshotV1 holding;
                if (!holdings.TryGetUnique(
                    request.EquipmentInstanceStableId,
                    out holding)
                    || holding == null
                    || holding.RewardKind != RewardGrantKindV1.EquipmentReference
                    || holding.EquipmentInstance == null)
                {
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatusV1.MissingEquipment,
                        "upgrade-equipment-missing");
                }

                EquipmentInstance equipment = holding.EquipmentInstance;
                int slotIndex;
                AugmentInstance augment = FindAugment(
                    equipment,
                    request.AugmentInstanceStableId,
                    out slotIndex);
                if (augment == null)
                {
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatusV1.MissingAugment,
                        "upgrade-augment-missing");
                }

                AugmentDefinition definition = catalog.FindAugmentDefinition(
                    augment.DefinitionId);
                if (definition == null || definition.LevelRange == null)
                {
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatusV1.UnknownAugmentDefinition,
                        "upgrade-augment-definition-missing");
                }

                if (augment.Level >= definition.LevelRange.Maximum
                    || request.TargetLevel > definition.LevelRange.Maximum)
                {
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatusV1.MaximumLevel,
                        "upgrade-maximum-level");
                }

                long cost;
                AugmentUpgradeCostStatusV1 costStatus = costPolicy.TryCalculateCost(
                    augment.Tier,
                    augment.Level,
                    request.TargetLevel,
                    out cost);
                if (costStatus != AugmentUpgradeCostStatusV1.Calculated)
                {
                    return QuoteCostFailure(costStatus);
                }

                var quote = AugmentUpgradeQuoteV1.Create(
                    equipment.InstanceId,
                    equipment.Fingerprint,
                    slotIndex,
                    augment.InstanceId,
                    augment.DefinitionId,
                    augment.Tier,
                    augment.Level,
                    request.TargetLevel,
                    moneyWallet.Balance,
                    moneyWallet.Sequence,
                    holdings.Sequence,
                    cost,
                    catalog.Fingerprint,
                    costPolicy.Fingerprint);
                return AugmentUpgradeQuoteResultV1.Create(
                    AugmentUpgradeQuoteStatusV1.Quoted,
                    quote,
                    null);
            }
        }
    }
}
