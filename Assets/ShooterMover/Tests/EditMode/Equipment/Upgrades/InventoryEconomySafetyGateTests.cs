using System;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Equipment.Upgrades;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Equipment.Upgrades;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons;
using ShooterMover.UnityAdapters.Weapons.Live;

namespace ShooterMover.Tests.EditMode.Equipment.Upgrades
{
    public sealed class InventoryEconomySafetyGateTests
    {
        [Test]
        public void CanonicalReceiptCannotReceiveGenericUpgradeQuote()
        {
            var fixture = new CanonicalReceiptFixture();
            long moneyBefore = fixture.Money.Balance;
            long walletSequenceBefore = fixture.Money.Sequence;
            long holdingsSequenceBefore = fixture.Holdings.Sequence;

            AugmentUpgradeQuoteResultV1 result = fixture.Service.Quote(
                new AugmentUpgradeQuoteRequestV1(
                    fixture.Equipment.InstanceId,
                    StableId.Parse("augment-instance.unsupported"),
                    2));

            Assert.That(result.Status,
                Is.EqualTo(AugmentUpgradeQuoteStatusV1.InvalidRequest));
            Assert.That(result.RejectionCode,
                Is.EqualTo("canonical-weapon-upgrade-route-unsupported"));
            Assert.That(result.Quote, Is.Null);
            Assert.That(fixture.Money.Balance, Is.EqualTo(moneyBefore));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(walletSequenceBefore));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsSequenceBefore));
            Assert.That(fixture.ContainsOriginal(), Is.True);
        }

        [Test]
        public void StaleCanonicalQuoteCannotBypassPreparationGuard()
        {
            var fixture = new CanonicalReceiptFixture();
            long moneyBefore = fixture.Money.Balance;
            long walletSequenceBefore = fixture.Money.Sequence;
            long holdingsSequenceBefore = fixture.Holdings.Sequence;
            AugmentUpgradeQuoteV1 staleQuote = AugmentUpgradeQuoteV1.Create(
                fixture.Equipment.InstanceId,
                fixture.Equipment.Fingerprint,
                0,
                StableId.Parse("augment-instance.unsupported"),
                StableId.Parse("augment.unsupported"),
                1,
                1,
                2,
                fixture.Money.Balance,
                fixture.Money.Sequence,
                fixture.Holdings.Sequence,
                100L,
                fixture.Catalog.Fingerprint,
                fixture.Policy.Fingerprint);

            AugmentUpgradeFactV1 fact = fixture.Service.Confirm(
                AugmentUpgradeConfirmationV1.Create(
                    StableId.Parse("confirmation.canonical-blocked"),
                    staleQuote));

            Assert.That(fact.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatusV1.InvalidRequest));
            Assert.That(fact.RejectionCode,
                Is.EqualTo("canonical-weapon-upgrade-route-unsupported"));
            Assert.That(fixture.Money.Balance, Is.EqualTo(moneyBefore));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(walletSequenceBefore));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsSequenceBefore));
            Assert.That(fixture.ContainsOriginal(), Is.True);
        }

        [Test]
        public void OverclockBearingFixtureFailsClosedAtLiveProjection()
        {
            ProductionWeaponMarkV1 mark = FirstProductionMark();
            WeaponEquipmentInstance instance = WeaponEquipmentInstance.Create(
                StableId.Parse("instance.overclock-fixture"),
                mark.Blueprint.DefinitionId,
                Array.Empty<StableId>(),
                new[] { StableId.Parse("overclock-instance.unsupported") });
            var holdings = new ProductionWeaponHoldingsAuthorityV2(
                WeaponHoldingsSnapshotV2.CreateCanonical(
                    0L,
                    new[] { instance }));
            var lookup = new CanonicalWeaponEquipmentProjectionLookupV2(
                holdings,
                ProductionWeaponCatalogProvider.EquipmentCatalog);

            EquipmentInstance ignored;
            bool resolved = lookup.TryResolve(
                new EquipmentInstanceId(instance.InstanceId),
                out ignored);

            Assert.That(resolved, Is.False);
            Assert.That(ignored, Is.Null);
            Assert.That(lookup.LastAvailability.IsAvailable, Is.False);
            Assert.That(lookup.LastAvailability.RejectionCode,
                Is.EqualTo("canonical-weapon-overclock-policy-unsupported"));
        }

        [Test]
        public void UnmodifiedCanonicalWeaponRemainsLiveEligible()
        {
            ProductionWeaponMarkV1 mark = FirstProductionMark();
            WeaponEquipmentInstance instance = WeaponEquipmentInstance.CreateUnmodified(
                StableId.Parse("instance.unmodified-fixture"),
                mark.Blueprint.DefinitionId);

            CanonicalWeaponOperationAvailabilityV1 decision =
                CanonicalWeaponSafetyPolicyV1.EvaluateLiveExecution(
                    instance,
                    true);

            Assert.That(decision.IsAvailable, Is.True);
            Assert.That(decision.RejectionCode, Is.Empty);
        }

        private static ProductionWeaponMarkV1 FirstProductionMark()
        {
            return ProductionWeaponCatalogProvider.Current.Families[0].Marks[0];
        }

        private sealed class CanonicalReceiptFixture
        {
            private static readonly StableId HoldingsAuthority =
                StableId.Parse("holdings.inventory-economy-safety");
            private static readonly StableId ScrapAuthority =
                StableId.Parse("authority.scrap.inventory-economy-safety");
            private static readonly StableId ScrapCurrency =
                StableId.Parse("currency.scrap");

            public CanonicalReceiptFixture()
            {
                ProductionWeaponMarkV1 mark = FirstProductionMark();
                Catalog = ProductionWeaponCatalogProvider.EquipmentCatalog;
                EquipmentDefinition definition = Catalog.FindEquipmentDefinition(
                    mark.EquipmentDefinitionId);
                Assert.That(definition, Is.Not.Null);
                Assert.That(definition.QualityTiers.Count, Is.GreaterThan(0));

                Equipment = EquipmentInstance.Create(
                    StableId.Parse("instance.canonical-receipt"),
                    definition.DefinitionId,
                    definition.ItemLevelRange.Minimum,
                    definition.QualityTiers[0].QualityId,
                    Array.Empty<AugmentInstance>());
                Validator = new CatalogValidator(Catalog);
                Holdings = new PlayerHoldingsService(
                    HoldingsAuthority,
                    100L,
                    Validator);
                HoldingProvenanceV1 provenance = HoldingProvenanceV1.Create(
                    StableId.Parse("grant.canonical-receipt"),
                    StableId.Parse("source.canonical-receipt"));
                PlayerHoldingsMutationResultV1 holdingResult = Holdings.Apply(
                    PlayerHoldingsCommandV1.AddEquipment(
                        StableId.Parse("transaction.canonical-receipt"),
                        StableId.Parse("operation.canonical-receipt"),
                        HoldingsAuthority,
                        Equipment,
                        provenance));
                Assert.That(holdingResult.Status,
                    Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));

                Money = new MoneyWalletService();
                MoneyWalletChangeFact moneyResult = Money.Grant(
                    StableId.Parse("transaction.initial-money.safety"),
                    StableId.Parse("operation.initial-money.safety"),
                    1000L);
                Assert.That(moneyResult.Status,
                    Is.EqualTo(MoneyWalletTransactionStatus.Applied));

                var scrap = new ScrapWalletServiceV1(
                    ScrapAuthority,
                    ScrapCurrency);
                var rewardApplication = new RewardApplicationServiceV1(
                    StableId.Parse("authority.reward-application.safety"),
                    new MoneyRewardChildAuthorityV1(Money),
                    new ScrapRewardChildAuthorityV1(scrap),
                    new PlayerHoldingsRewardChildAuthorityV1(Holdings, Validator));
                Policy = AugmentUpgradeCostPolicyV1.Create(
                    StableId.Parse("policy.inventory-economy-safety"),
                    1,
                    false,
                    new[] { AugmentTierCostCurveV1.Create(1, 100L, 10L) });
                Service = new AugmentUpgradeServiceV1(
                    Money,
                    Holdings,
                    rewardApplication,
                    new CatalogProvider(Catalog),
                    Validator,
                    Policy,
                    new AugmentUpgradeIdentityContextV1(
                        StableId.Parse("run.inventory-economy-safety"),
                        StableId.Parse("source-instance.inventory-economy-safety"),
                        StableId.Parse("player.inventory-economy-safety"),
                        StableId.Parse("reward-profile.inventory-economy-safety"),
                        ScrapAuthority));
            }

            public MoneyWalletService Money { get; }
            public PlayerHoldingsService Holdings { get; }
            public EquipmentCatalog Catalog { get; }
            public EquipmentInstance Equipment { get; }
            public CatalogValidator Validator { get; }
            public AugmentUpgradeCostPolicyV1 Policy { get; }
            public AugmentUpgradeServiceV1 Service { get; }

            public bool ContainsOriginal()
            {
                UniqueHoldingSnapshotV1 holding;
                return Holdings.TryGetUnique(Equipment.InstanceId, out holding)
                    && holding != null;
            }
        }

        private sealed class CatalogProvider : IEquipmentCatalogProvider
        {
            public CatalogProvider(EquipmentCatalog catalog)
            {
                Catalog = catalog;
            }

            public EquipmentCatalog Catalog { get; }
        }

        private sealed class CatalogValidator : IEquipmentInstanceValidator
        {
            private readonly EquipmentCatalog catalog;

            public CatalogValidator(EquipmentCatalog catalog)
            {
                this.catalog = catalog;
            }

            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                EquipmentInstance instance = request == null ? null : request.Instance;
                return EquipmentInstanceValidationResponse.From(
                    catalog,
                    instance,
                    catalog.ValidateInstance(instance));
            }
        }
    }
}
