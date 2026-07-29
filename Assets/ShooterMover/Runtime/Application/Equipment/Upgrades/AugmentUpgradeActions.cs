using System;
using System.Collections.Generic;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Weapons.Catalog;
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
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Equipment.Upgrades
{
    public sealed partial class AugmentUpgradeActions
    {
        private readonly object sync = new object();
        private readonly MoneyWalletActions moneyWallet;
        private readonly PlayerHoldingsActions holdings;
        private readonly RewardApplicationActions rewardApplication;
        private readonly IEquipmentCatalogProvider catalogProvider;
        private readonly IEquipmentInstanceValidator equipmentValidator;
        private readonly AugmentUpgradeCostPolicy costPolicy;
        private readonly AugmentUpgradeIdentityContext identityContext;
        private readonly Dictionary<StableId, UpgradeRecord> records;

        public AugmentUpgradeActions(
            MoneyWalletActions moneyWallet,
            PlayerHoldingsActions holdings,
            RewardApplicationActions rewardApplication,
            IEquipmentCatalogProvider catalogProvider,
            IEquipmentInstanceValidator equipmentValidator,
            AugmentUpgradeCostPolicy costPolicy,
            AugmentUpgradeIdentityContext identityContext)
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

        public AugmentUpgradeCostPolicy CostPolicy
        {
            get { return costPolicy; }
        }

        public AugmentUpgradeQuoteResult Quote(
            AugmentUpgradeQuoteRequest request)
        {
            lock (sync)
            {
                if (request == null
                    || request.EquipmentInstanceStableId == null
                    || request.AugmentInstanceStableId == null
                    || request.TargetLevel < 1)
                {
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatus.InvalidRequest,
                        "upgrade-quote-invalid");
                }

                EquipmentCatalog catalog = catalogProvider.Catalog;
                if (catalog == null)
                {
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatus.InvalidCatalog,
                        "upgrade-catalog-missing");
                }

                UniqueHoldingSnapshot holding;
                if (!holdings.TryGetUnique(
                    request.EquipmentInstanceStableId,
                    out holding)
                    || holding == null
                    || holding.RewardKind != RewardGrantKind.EquipmentReference
                    || holding.EquipmentInstance == null)
                {
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatus.MissingEquipment,
                        "upgrade-equipment-missing");
                }

                EquipmentInstance equipment = holding.EquipmentInstance;
                WeaponOperationAvailability upgradeAvailability =
                    EvaluateGenericUpgradeAvailability(catalog, equipment);
                if (!upgradeAvailability.IsAvailable)
                {
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatus.InvalidRequest,
                        upgradeAvailability.RejectionCode);
                }

                int slotIndex;
                AugmentInstance augment = FindAugment(
                    equipment,
                    request.AugmentInstanceStableId,
                    out slotIndex);
                if (augment == null)
                {
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatus.MissingAugment,
                        "upgrade-augment-missing");
                }

                AugmentDefinition definition = catalog.FindAugmentDefinition(
                    augment.DefinitionId);
                if (definition == null || definition.LevelRange == null)
                {
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatus.UnknownAugmentDefinition,
                        "upgrade-augment-definition-missing");
                }

                if (augment.Level >= definition.LevelRange.Maximum
                    || request.TargetLevel > definition.LevelRange.Maximum)
                {
                    return QuoteFailure(
                        AugmentUpgradeQuoteStatus.MaximumLevel,
                        "upgrade-maximum-level");
                }

                long cost;
                AugmentUpgradeCostStatus costStatus = costPolicy.TryCalculateCost(
                    augment.Tier,
                    augment.Level,
                    request.TargetLevel,
                    out cost);
                if (costStatus != AugmentUpgradeCostStatus.Calculated)
                {
                    return QuoteCostFailure(costStatus);
                }

                var quote = AugmentUpgradeQuote.Create(
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
                return AugmentUpgradeQuoteResult.Create(
                    AugmentUpgradeQuoteStatus.Quoted,
                    quote,
                    null);
            }
        }

        private static WeaponOperationAvailability
            EvaluateGenericUpgradeAvailability(
                EquipmentCatalog catalog,
                EquipmentInstance equipment)
        {
            if (catalog == null || equipment == null)
            {
                return WeaponOperationAvailability.Available();
            }

            EquipmentDefinition definition = catalog.FindEquipmentDefinition(
                equipment.DefinitionId);
            bool isWeaponReceipt = definition != null
                && definition.CategoryId == EquipmentCategoryIds.Weapon;
            bool canonicalDefinitionResolved = false;
            if (isWeaponReceipt && definition.RuntimeWeaponReferenceId != null)
            {
                WeaponMark mark;
                canonicalDefinitionResolved = WeaponCatalogProvider.Current
                    .TryGetMark(
                        WeaponDefinitionId.FromRuntimeReference(
                            definition.RuntimeWeaponReferenceId).Value,
                        out mark)
                    && mark != null;
            }

            if (!isWeaponReceipt)
            {
                return WeaponOperationAvailability.Available();
            }

            bool authoritativeProductionCatalog = ReferenceEquals(
                catalog,
                WeaponCatalogProvider.EquipmentCatalog);
            if (!authoritativeProductionCatalog && !canonicalDefinitionResolved)
            {
                // Isolated synthetic/legacy catalogues keep the historical generic route. They do
                // not identify an exact production canonical weapon receipt.
                return WeaponOperationAvailability.Available();
            }

            return WeaponSafetyPolicy.EvaluateGenericUpgrade(
                true,
                canonicalDefinitionResolved);
        }
    }
}
