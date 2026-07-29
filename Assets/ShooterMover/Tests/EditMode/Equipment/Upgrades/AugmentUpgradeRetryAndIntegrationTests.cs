using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Equipment.Upgrades;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Equipment.Upgrades;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Equipment.Upgrades
{
    public sealed partial class AugmentUpgradeActionsTests
    {
        [Test]
        public void RetryUsesIdenticalTransactionAndReplacementIdentities()
        {
            var fixture = new Fixture(interruptFirstRewardApply: true);
            AugmentUpgradeQuote quote = fixture.Quote(2);

            AugmentUpgradeFact pending = fixture.Confirm(
                quote,
                "confirmation.retry");
            long balanceAfterPending = fixture.Money.Balance;
            long holdingsAfterPending = fixture.Holdings.Sequence;
            AugmentUpgradeFact applied = fixture.Service.Retry(
                new AugmentUpgradeRetryCommand(
                    Id("confirmation.retry")));

            Assert.That(pending.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatus.PendingRetry));
            Assert.That(applied.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatus.Applied));
            Assert.That(applied.MoneyTransactionStableId,
                Is.EqualTo(pending.MoneyTransactionStableId));
            Assert.That(applied.HoldingsRemoveTransactionStableId,
                Is.EqualTo(pending.HoldingsRemoveTransactionStableId));
            Assert.That(applied.ReplacementEquipmentInstanceStableId,
                Is.EqualTo(pending.ReplacementEquipmentInstanceStableId));
            Assert.That(applied.RewardCommitmentStableId,
                Is.EqualTo(pending.RewardCommitmentStableId));
            Assert.That(applied.RewardClaimStableId,
                Is.EqualTo(pending.RewardClaimStableId));
            Assert.That(fixture.Money.Balance, Is.EqualTo(balanceAfterPending));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsAfterPending + 1L));
            Assert.That(
                fixture.GetUnique(applied.ReplacementEquipmentInstanceStableId),
                Is.Not.Null);
        }

        [Test]
        public void RealMonInvAndRapIntegrationPasses()
        {
            var fixture = new Fixture();
            AugmentUpgradeQuote quote = fixture.Quote(2);
            long rapBefore = fixture.Rap.Sequence;

            AugmentUpgradeFact fact = fixture.Confirm(
                quote,
                "confirmation.real-integration");
            UniqueHoldingSnapshot replacement = fixture.GetUnique(
                fact.ReplacementEquipmentInstanceStableId);

            Assert.That(fact.Status, Is.EqualTo(AugmentUpgradeConfirmationStatus.Applied));
            Assert.That(fixture.Money.Balance,
                Is.EqualTo(quote.CurrentWalletBalance - quote.MoneyCost));
            Assert.That(fixture.Holdings.Sequence,
                Is.EqualTo(quote.HoldingsSequence + 2L));
            Assert.That(fixture.Rap.Sequence, Is.GreaterThan(rapBefore));
            Assert.That(replacement.Provenance.GrantStableId, Is.Not.Null);
            Assert.That(replacement.Provenance.SourceStableId, Is.Not.Null);
            Assert.That(fixture.Validator.Validate(
                new EquipmentInstanceValidationRequest(
                    replacement.EquipmentInstance)).IsValid,
                Is.True);
        }

        private static AugmentUpgradeQuote CopyQuote(
            AugmentUpgradeQuote source,
            string equipmentFingerprint = null,
            int? augmentSlotIndex = null,
            StableId augmentInstanceStableId = null,
            int? targetLevel = null,
            long? moneyCost = null)
        {
            return AugmentUpgradeQuote.Create(
                source.EquipmentInstanceStableId,
                equipmentFingerprint ?? source.EquipmentFingerprint,
                augmentSlotIndex ?? source.AugmentSlotIndex,
                augmentInstanceStableId ?? source.AugmentInstanceStableId,
                source.AugmentDefinitionStableId,
                source.AugmentTier,
                source.CurrentLevel,
                targetLevel ?? source.TargetLevel,
                source.CurrentWalletBalance,
                source.WalletSequence,
                source.HoldingsSequence,
                moneyCost ?? source.MoneyCost,
                source.CatalogFingerprint,
                source.CostPolicyFingerprint);
        }

        private static AugmentInstance FindAugment(
            EquipmentInstance equipment,
            StableId instanceStableId)
        {
            for (int index = 0; index < equipment.Augments.Count; index++)
            {
                if (equipment.Augments[index].InstanceId == instanceStableId)
                {
                    return equipment.Augments[index];
                }
            }

            return null;
        }

        private static AugmentUpgradeCostPolicy Policy(
            int version = 1,
            long tierOneBase = 100L)
        {
            return AugmentUpgradeCostPolicy.Create(
                Id("augment-upgrade-policy.standard"),
                version,
                false,
                new[]
                {
                    AugmentTierCostCurve.Create(1, tierOneBase, 10L),
                    AugmentTierCostCurve.Create(2, 250L, 25L),
                    AugmentTierCostCurve.Create(3, 500L, 50L),
                });
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private static string Hash(char value)
        {
            return AugmentUpgrade.Fingerprint(value.ToString());
        }

        private sealed class Fixture
        {
            public Fixture(
                long initialMoney = 10000L,
                int maximumLevel = 10,
                int currentLevel = 1,
                int augmentTier = 1,
                bool interruptFirstRewardApply = false,
                AugmentUpgradeCostPolicy policy = null)
            {
                Catalog = BuildCatalog(maximumLevel);
                Provider = new CatalogProvider(Catalog);
                Validator = new CatalogValidator(Catalog);
                Money = new MoneyWalletActions();
                Scrap = new ScrapWalletActions(ScrapAuthority, ScrapCurrency);
                Holdings = new PlayerHoldingsActions(
                    HoldingsAuthority,
                    1000L,
                    Validator);
                Equipment = EquipmentInstance.Create(
                    EquipmentInstanceId,
                    EquipmentDefinitionId,
                    17,
                    Id("quality.rare"),
                    new[]
                    {
                        AugmentInstance.Create(
                            PrimaryAugmentInstanceId,
                            PrimaryAugmentDefinitionId,
                            augmentTier,
                            currentLevel),
                        AugmentInstance.Create(
                            SecondaryAugmentInstanceId,
                            SecondaryAugmentDefinitionId,
                            2,
                            1),
                    });
                HoldingProvenance provenance = HoldingProvenance.Create(
                    Id("grant.initial-equipment"),
                    Id("source.initial-equipment"));
                PlayerHoldingsMutationResult holdingResult = Holdings.Apply(
                    PlayerHoldingsCommand.AddEquipment(
                        Id("initial-equipment.transaction"),
                        Id("initial-equipment.operation"),
                        HoldingsAuthority,
                        Equipment,
                        provenance));
                Assert.That(holdingResult.Status,
                    Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
                MoneyWalletChangeFact moneyResult = Money.Grant(
                    Id("initial-money.transaction"),
                    Id("initial-money.operation"),
                    initialMoney);
                Assert.That(moneyResult.Status,
                    Is.EqualTo(MoneyWalletTransactionStatus.Applied));

                IRewardChildState holdingsChild =
                    new PlayerHoldingsRewardChildState(Holdings, Validator);
                if (interruptFirstRewardApply)
                {
                    holdingsChild = new ThrowOnceRewardChildState(holdingsChild);
                }

                Rap = new RewardApplicationActions(
                    RapAuthority,
                    new MoneyRewardChildState(Money),
                    new ScrapRewardChildState(Scrap),
                    holdingsChild);
                CostPolicy = policy ?? Policy();
                Service = CreateService(CostPolicy);
            }

            public MoneyWalletActions Money { get; }
            public ScrapWalletActions Scrap { get; }
            public PlayerHoldingsActions Holdings { get; }
            public RewardApplicationActions Rap { get; }
            public EquipmentCatalog Catalog { get; }
            public CatalogProvider Provider { get; }
            public CatalogValidator Validator { get; }
            public EquipmentInstance Equipment { get; }
            public AugmentUpgradeCostPolicy CostPolicy { get; }
            public AugmentUpgradeActions Service { get; }

            public AugmentUpgradeQuote Quote(int targetLevel)
            {
                AugmentUpgradeQuoteResult result = Service.Quote(
                    new AugmentUpgradeQuoteRequest(
                        EquipmentInstanceId,
                        PrimaryAugmentInstanceId,
                        targetLevel));
                Assert.That(result.Status, Is.EqualTo(AugmentUpgradeQuoteStatus.Quoted));
                Assert.That(result.Quote, Is.Not.Null);
                return result.Quote;
            }

            public AugmentUpgradeFact Confirm(
                AugmentUpgradeQuote quote,
                string confirmationId)
            {
                return Service.Confirm(AugmentUpgradeConfirmation.Create(
                    Id(confirmationId),
                    quote));
            }

            public UniqueHoldingSnapshot GetUnique(StableId instanceId)
            {
                UniqueHoldingSnapshot holding;
                Assert.That(Holdings.TryGetUnique(instanceId, out holding), Is.True);
                return holding;
            }

            public void RemoveOriginal(string identityPrefix)
            {
                UniqueHoldingSnapshot holding = GetUnique(EquipmentInstanceId);
                PlayerHoldingsMutationResult result = Holdings.Apply(
                    PlayerHoldingsCommand.RemoveEquipment(
                        Id(identityPrefix + "-tx"),
                        Id(identityPrefix + "-op"),
                        HoldingsAuthority,
                        holding.DefinitionStableId,
                        holding.InstanceStableId,
                        holding.Provenance,
                        Holdings.Sequence));
                Assert.That(result.Status,
                    Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            }

            public AugmentUpgradeQuote CreateManualQuote(
                int targetLevel,
                long moneyCost)
            {
                AugmentInstance augment = FindAugment(Equipment, PrimaryAugmentInstanceId);
                return AugmentUpgradeQuote.Create(
                    Equipment.InstanceId,
                    Equipment.Fingerprint,
                    0,
                    augment.InstanceId,
                    augment.DefinitionId,
                    augment.Tier,
                    augment.Level,
                    targetLevel,
                    Money.Balance,
                    Money.Sequence,
                    Holdings.Sequence,
                    moneyCost,
                    Catalog.Fingerprint,
                    CostPolicy.Fingerprint);
            }

            public AugmentUpgradeActions CreateService(
                AugmentUpgradeCostPolicy policy)
            {
                return new AugmentUpgradeActions(
                    Money,
                    Holdings,
                    Rap,
                    Provider,
                    Validator,
                    policy,
                    new AugmentUpgradeIdentityContext(
                        Id("run.upgrade-tests"),
                        Id("source-instance.upgrade-tests"),
                        Id("player.upgrade-tests"),
                        Id("reward-profile.upgrade-tests"),
                        ScrapAuthority));
            }

            private static EquipmentCatalog BuildCatalog(int maximumLevel)
            {
                EquipmentDefinition equipment = EquipmentDefinition.Create(
                    EquipmentDefinitionId,
                    EquipmentCategoryIds.Gun,
                    Id("equipment-family.blaster"),
                    "Blaster",
                    Id("gun.blaster"),
                    InclusiveIntRange.Create(1, 100),
                    3,
                    new[]
                    {
                        EquipmentQualityTier.Create(Id("quality.rare"), "Rare", 2),
                    },
                    new[] { Id("equipment-tag.energy") });
                AugmentCompatibility compatibility = AugmentCompatibility.Create(
                    new[] { EquipmentCategoryIds.Gun },
                    new[] { Id("equipment-family.blaster") },
                    new[] { Id("equipment-tag.energy") },
                    Array.Empty<StableId>());
                AugmentDefinition primary = AugmentDefinition.Create(
                    PrimaryAugmentDefinitionId,
                    Id("augment-family.offense"),
                    "Power",
                    compatibility,
                    Array.Empty<StableId>(),
                    AugmentDuplicatePolicy.DisallowSameDefinition,
                    InclusiveIntRange.Create(1, 3),
                    InclusiveIntRange.Create(1, maximumLevel));
                AugmentDefinition secondary = AugmentDefinition.Create(
                    SecondaryAugmentDefinitionId,
                    Id("augment-family.utility"),
                    "Cooldown",
                    compatibility,
                    Array.Empty<StableId>(),
                    AugmentDuplicatePolicy.DisallowSameDefinition,
                    InclusiveIntRange.Create(1, 3),
                    InclusiveIntRange.Create(1, maximumLevel));
                EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                    new[] { equipment },
                    new[] { primary, secondary });
                Assert.That(result.IsValid, Is.True);
                return result.Catalog;
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

        private sealed class ThrowOnceRewardChildState : IRewardChildState
        {
            private readonly IRewardChildState inner;
            private bool shouldThrow = true;

            public ThrowOnceRewardChildState(IRewardChildState inner)
            {
                this.inner = inner;
            }

            public StableId AuthorityStableId
            {
                get { return inner.AuthorityStableId; }
            }

            public long Sequence
            {
                get { return inner.Sequence; }
            }

            public RewardStatePreflightResult Preflight(
                IReadOnlyList<RewardChildGrantCommand> commands)
            {
                return inner.Preflight(commands);
            }

            public RewardChildApplyResult Apply(
                RewardChildGrantCommand command)
            {
                if (shouldThrow
                    && command != null
                    && command.GrantKind == RewardGrantKind.EquipmentReference)
                {
                    shouldThrow = false;
                    throw new InvalidOperationException("forced-upgrade-retry");
                }

                return inner.Apply(command);
            }
        }
    }
}
