using System;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Equipment.Upgrades;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.Domain.Equipment.Upgrades;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Guns;
using ShooterMover.UnityAdapters.Guns.Live;

namespace ShooterMover.Tests.EditMode.Equipment.Upgrades
{
    public sealed class InventoryEconomySafetyGateTests
    {
        [Test]
        public void CanonicalReceiptCannotReceiveGenericUpgradeQuote()
        {
            var fixture = new ReceiptFixture();
            long moneyBefore = fixture.Money.Balance;
            long walletSequenceBefore = fixture.Money.Sequence;
            long holdingsSequenceBefore = fixture.Holdings.Sequence;

            AugmentUpgradeQuoteResult result = fixture.Service.Quote(
                new AugmentUpgradeQuoteRequest(
                    fixture.Equipment.InstanceId,
                    StableId.Parse("augment-instance.unsupported"),
                    2));

            Assert.That(result.Status,
                Is.EqualTo(AugmentUpgradeQuoteStatus.InvalidRequest));
            Assert.That(result.RejectionCode,
                Is.EqualTo("canonical-gun-upgrade-route-unsupported"));
            Assert.That(result.Quote, Is.Null);
            Assert.That(fixture.Money.Balance, Is.EqualTo(moneyBefore));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(walletSequenceBefore));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsSequenceBefore));
            Assert.That(fixture.ContainsOriginal(), Is.True);
        }

        [Test]
        public void StaleCanonicalQuoteCannotBypassPreparationGuard()
        {
            var fixture = new ReceiptFixture();
            long moneyBefore = fixture.Money.Balance;
            long walletSequenceBefore = fixture.Money.Sequence;
            long holdingsSequenceBefore = fixture.Holdings.Sequence;
            AugmentUpgradeQuote staleQuote = AugmentUpgradeQuote.Create(
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

            AugmentUpgradeFact fact = fixture.Service.Confirm(
                AugmentUpgradeConfirmation.Create(
                    StableId.Parse("confirmation.canonical-blocked"),
                    staleQuote));

            Assert.That(fact.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatus.InvalidRequest));
            Assert.That(fact.RejectionCode,
                Is.EqualTo("canonical-gun-upgrade-route-unsupported"));
            Assert.That(fixture.Money.Balance, Is.EqualTo(moneyBefore));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(walletSequenceBefore));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsSequenceBefore));
            Assert.That(fixture.ContainsOriginal(), Is.True);
        }

        [Test]
        public void OverclockBearingFixtureFailsClosedAtLiveProjection()
        {
            GunMark mark = FirstProductionMark();
            GunItem instance = GunItem.Create(
                StableId.Parse("instance.overclock-fixture"),
                mark.Blueprint.DefinitionId,
                Array.Empty<StableId>(),
                new[] { StableId.Parse("overclock-instance.unsupported") });
            var holdings = new GunInventoryState(
                GunInventorySnapshot.CreateCanonical(
                    0L,
                    new[] { instance }));
            var lookup = new GunEquipmentViewLookup(
                holdings,
                GunCatalogProvider.EquipmentCatalog);

            EquipmentInstance ignored;
            bool resolved = lookup.TryResolve(
                new EquipmentInstanceId(instance.InstanceId),
                out ignored);

            Assert.That(resolved, Is.False);
            Assert.That(ignored, Is.Null);
            Assert.That(lookup.LastAvailability.IsAvailable, Is.False);
            Assert.That(lookup.LastAvailability.RejectionCode,
                Is.EqualTo("canonical-gun-overclock-policy-unsupported"));
        }

        [Test]
        public void UnmodifiedCanonicalGunRemainsLiveEligible()
        {
            GunMark mark = FirstProductionMark();
            GunItem instance = GunItem.CreateUnmodified(
                StableId.Parse("instance.unmodified-fixture"),
                mark.Blueprint.DefinitionId);

            GunOperationAvailability decision =
                GunSafetyPolicy.EvaluateLiveExecution(
                    instance,
                    true);

            Assert.That(decision.IsAvailable, Is.True);
            Assert.That(decision.RejectionCode, Is.Empty);
        }

        [Test]
        public void DirectCanonicalAddRejectsUnsupportedOverclockWithoutMutation()
        {
            GunMark mark = FirstProductionMark();
            GunItem instance = GunItem.Create(
                StableId.Parse("instance.direct-overclock-add"),
                mark.Blueprint.DefinitionId,
                Array.Empty<StableId>(),
                new[] { StableId.Parse("overclock-instance.direct-add") });
            var holdings = new GunInventoryState();
            long sequenceBefore = holdings.Sequence;

            string rejectionCode;
            bool accepted = holdings.TryAdd(instance, out rejectionCode);

            Assert.That(accepted, Is.False);
            Assert.That(rejectionCode,
                Is.EqualTo("canonical-gun-overclock-policy-unsupported"));
            Assert.That(holdings.Sequence, Is.EqualTo(sequenceBefore));
            Assert.That(holdings.Count, Is.Zero);
            Assert.That(holdings.Contains(instance.InstanceId), Is.False);
        }

        [Test]
        public void DirectCanonicalRemoveRejectsUnresolvedDefinitionWithoutMutation()
        {
            GunItem unresolved = GunItem.CreateUnmodified(
                StableId.Parse("instance.direct-unresolved-remove"),
                new GunDefinitionId("gun-definition.missing"));
            var holdings = new GunInventoryState(
                GunInventorySnapshot.CreateCanonical(
                    7L,
                    new[] { unresolved }));
            long sequenceBefore = holdings.Sequence;

            string rejectionCode;
            bool accepted = holdings.TryRemove(
                unresolved.InstanceId,
                out rejectionCode);

            Assert.That(accepted, Is.False);
            Assert.That(rejectionCode,
                Is.EqualTo("canonical-gun-definition-unresolved"));
            Assert.That(holdings.Sequence, Is.EqualTo(sequenceBefore));
            Assert.That(holdings.Count, Is.EqualTo(1));
            Assert.That(holdings.Contains(unresolved.InstanceId), Is.True);
        }

        [Test]
        public void AmbiguousRetainedReceiptCannotFallThroughToGenericRemoval()
        {
            StableId authorityId = StableId.Parse("holdings.ambiguous-receipt");
            EquipmentCatalog syntheticCatalog = BuildSyntheticUnknownGunCatalog();
            EquipmentDefinition definition = syntheticCatalog.FindEquipmentDefinition(
                StableId.Parse("equipment.synthetic-unresolved"));
            Assert.That(definition, Is.Not.Null);
            EquipmentInstance receipt = EquipmentInstance.Create(
                StableId.Parse("instance.synthetic-unresolved"),
                definition.DefinitionId,
                definition.ItemLevelRange.Minimum,
                definition.QualityTiers[0].QualityId,
                Array.Empty<AugmentInstance>());
            var validator = new CatalogValidator(syntheticCatalog);
            var receipts = new PlayerHoldingsActions(authorityId, 10L, validator);
            HoldingProvenance provenance = HoldingProvenance.Create(
                StableId.Parse("grant.synthetic-unresolved"),
                StableId.Parse("source.synthetic-unresolved"));
            PlayerHoldingsMutationResult added = receipts.Apply(
                PlayerHoldingsCommand.AddEquipment(
                    StableId.Parse("transaction.synthetic-unresolved-add"),
                    StableId.Parse("operation.synthetic-unresolved-add"),
                    authorityId,
                    receipt,
                    provenance));
            Assert.That(added.Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            var canonical = new GunInventoryState();
            var boundary = new FirstPlayerHoldingsState(
                receipts,
                canonical);
            long receiptSequenceBefore = receipts.Sequence;
            long canonicalSequenceBefore = canonical.Sequence;
            PlayerHoldingsCommand remove = PlayerHoldingsCommand.RemoveEquipment(
                StableId.Parse("transaction.synthetic-unresolved-remove"),
                StableId.Parse("operation.synthetic-unresolved-remove"),
                authorityId,
                receipt.DefinitionId,
                receipt.InstanceId,
                provenance);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                delegate { boundary.Apply(remove); });

            Assert.That(exception.Message,
                Does.StartWith("canonical-gun-definition-unresolved:"));
            Assert.That(receipts.Sequence, Is.EqualTo(receiptSequenceBefore));
            Assert.That(canonical.Sequence, Is.EqualTo(canonicalSequenceBefore));
            UniqueHoldingSnapshot retained;
            Assert.That(receipts.TryGetUnique(receipt.InstanceId, out retained), Is.True);
            Assert.That(retained, Is.Not.Null);
        }

        private static GunMark FirstProductionMark()
        {
            return GunCatalogProvider.Current.Families[0].Marks[0];
        }

        private static EquipmentCatalog BuildSyntheticUnknownGunCatalog()
        {
            EquipmentQualityTier quality = EquipmentQualityTier.Create(
                StableId.Parse("quality.synthetic-unresolved"),
                "Synthetic",
                1);
            EquipmentDefinition definition = EquipmentDefinition.Create(
                StableId.Parse("equipment.synthetic-unresolved"),
                EquipmentCategoryIds.Gun,
                StableId.Parse("equipment-archetype.synthetic-unresolved"),
                "Synthetic unresolved gun",
                StableId.Parse("gun-definition.synthetic-unresolved"),
                InclusiveIntRange.Create(1, 1),
                0,
                new[] { quality },
                Array.Empty<StableId>());
            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[] { definition },
                Array.Empty<AugmentDefinition>());
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Catalog, Is.Not.Null);
            return result.Catalog;
        }

        private sealed class ReceiptFixture
        {
            private static readonly StableId HoldingsAuthority =
                StableId.Parse("holdings.inventory-economy-safety");
            private static readonly StableId ScrapAuthority =
                StableId.Parse("authority.scrap.inventory-economy-safety");
            private static readonly StableId ScrapCurrency =
                StableId.Parse("currency.scrap");

            public ReceiptFixture()
            {
                GunMark mark = FirstProductionMark();
                Catalog = GunCatalogProvider.EquipmentCatalog;
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
                Holdings = new PlayerHoldingsActions(
                    HoldingsAuthority,
                    100L,
                    Validator);
                HoldingProvenance provenance = HoldingProvenance.Create(
                    StableId.Parse("grant.canonical-receipt"),
                    StableId.Parse("source.canonical-receipt"));
                PlayerHoldingsMutationResult holdingResult = Holdings.Apply(
                    PlayerHoldingsCommand.AddEquipment(
                        StableId.Parse("transaction.canonical-receipt"),
                        StableId.Parse("operation.canonical-receipt"),
                        HoldingsAuthority,
                        Equipment,
                        provenance));
                Assert.That(holdingResult.Status,
                    Is.EqualTo(PlayerHoldingsMutationStatus.Applied));

                Money = new MoneyWalletActions();
                MoneyWalletChangeFact moneyResult = Money.Grant(
                    StableId.Parse("transaction.initial-money.safety"),
                    StableId.Parse("operation.initial-money.safety"),
                    1000L);
                Assert.That(moneyResult.Status,
                    Is.EqualTo(MoneyWalletTransactionStatus.Applied));

                var scrap = new ScrapWalletActions(
                    ScrapAuthority,
                    ScrapCurrency);
                var rewardApplication = new RewardApplicationActions(
                    StableId.Parse("authority.reward-application.safety"),
                    new MoneyRewardChildState(Money),
                    new ScrapRewardChildState(scrap),
                    new PlayerHoldingsRewardChildState(Holdings, Validator));
                Policy = AugmentUpgradeCostPolicy.Create(
                    StableId.Parse("policy.inventory-economy-safety"),
                    1,
                    false,
                    new[] { AugmentTierCostCurve.Create(1, 100L, 10L) });
                Service = new AugmentUpgradeActions(
                    Money,
                    Holdings,
                    rewardApplication,
                    new CatalogProvider(Catalog),
                    Validator,
                    Policy,
                    new AugmentUpgradeIdentityContext(
                        StableId.Parse("run.inventory-economy-safety"),
                        StableId.Parse("source-instance.inventory-economy-safety"),
                        StableId.Parse("player.inventory-economy-safety"),
                        StableId.Parse("reward-profile.inventory-economy-safety"),
                        ScrapAuthority));
            }

            public MoneyWalletActions Money { get; }
            public PlayerHoldingsActions Holdings { get; }
            public EquipmentCatalog Catalog { get; }
            public EquipmentInstance Equipment { get; }
            public CatalogValidator Validator { get; }
            public AugmentUpgradeCostPolicy Policy { get; }
            public AugmentUpgradeActions Service { get; }

            public bool ContainsOriginal()
            {
                UniqueHoldingSnapshot holding;
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
