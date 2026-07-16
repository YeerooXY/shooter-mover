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
    public sealed partial class AugmentUpgradeServiceV1Tests
    {
        [Test]
        public void RetryUsesIdenticalTransactionAndReplacementIdentities()
        {
            var fixture = new Fixture(interruptFirstRewardApply: true);
            AugmentUpgradeQuoteV1 quote = fixture.Quote(2);

            AugmentUpgradeFactV1 pending = fixture.Confirm(
                quote,
                "confirmation.retry");
            long balanceAfterPending = fixture.Money.Balance;
            long holdingsAfterPending = fixture.Holdings.Sequence;
            AugmentUpgradeFactV1 applied = fixture.Service.Retry(
                new AugmentUpgradeRetryCommandV1(
                    Id("confirmation.retry")));

            Assert.That(pending.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatusV1.PendingRetry));
            Assert.That(applied.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatusV1.Applied));
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
            AugmentUpgradeQuoteV1 quote = fixture.Quote(2);
            long rapBefore = fixture.Rap.Sequence;

            AugmentUpgradeFactV1 fact = fixture.Confirm(
                quote,
                "confirmation.real-integration");
            UniqueHoldingSnapshotV1 replacement = fixture.GetUnique(
                fact.ReplacementEquipmentInstanceStableId);

            Assert.That(fact.Status, Is.EqualTo(AugmentUpgradeConfirmationStatusV1.Applied));
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

        private static AugmentUpgradeQuoteV1 CopyQuote(
            AugmentUpgradeQuoteV1 source,
            string equipmentFingerprint = null,
            int? augmentSlotIndex = null,
            StableId augmentInstanceStableId = null,
            int? targetLevel = null,
            long? moneyCost = null)
        {
            return AugmentUpgradeQuoteV1.Create(
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

        private static AugmentUpgradeCostPolicyV1 Policy(
            int version = 1,
            long tierOneBase = 100L)
        {
            return AugmentUpgradeCostPolicyV1.Create(
                Id("augment-upgrade-policy.standard"),
                version,
                false,
                new[]
                {
                    AugmentTierCostCurveV1.Create(1, tierOneBase, 10L),
                    AugmentTierCostCurveV1.Create(2, 250L, 25L),
                    AugmentTierCostCurveV1.Create(3, 500L, 50L),
                });
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private static string Hash(char value)
        {
            return AugmentUpgradeCanonicalV1.Fingerprint(value.ToString());
        }

        private sealed class Fixture
        {
            public Fixture(
                long initialMoney = 10000L,
                int maximumLevel = 10,
                int currentLevel = 1,
                int augmentTier = 1,
                bool interruptFirstRewardApply = false,
                AugmentUpgradeCostPolicyV1 policy = null)
            {
                Catalog = BuildCatalog(maximumLevel);
                Provider = new CatalogProvider(Catalog);
                Validator = new CatalogValidator(Catalog);
                Money = new MoneyWalletService();
                Scrap = new ScrapWalletServiceV1(ScrapAuthority, ScrapCurrency);
                Holdings = new PlayerHoldingsService(
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
                HoldingProvenanceV1 provenance = HoldingProvenanceV1.Create(
                    Id("grant.initial-equipment"),
                    Id("source.initial-equipment"));
                PlayerHoldingsMutationResultV1 holdingResult = Holdings.Apply(
                    PlayerHoldingsCommandV1.AddEquipment(
                        Id("initial-equipment.transaction"),
                        Id("initial-equipment.operation"),
                        HoldingsAuthority,
                        Equipment,
                        provenance));
                Assert.That(holdingResult.Status,
                    Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
                MoneyWalletChangeFact moneyResult = Money.Grant(
                    Id("initial-money.transaction"),
                    Id("initial-money.operation"),
                    initialMoney);
                Assert.That(moneyResult.Status,
                    Is.EqualTo(MoneyWalletTransactionStatus.Applied));

                IRewardChildAuthorityV1 holdingsChild =
                    new PlayerHoldingsRewardChildAuthorityV1(Holdings, Validator);
                if (interruptFirstRewardApply)
                {
                    holdingsChild = new ThrowOnceRewardChildAuthority(holdingsChild);
                }

                Rap = new RewardApplicationServiceV1(
                    RapAuthority,
                    new MoneyRewardChildAuthorityV1(Money),
                    new ScrapRewardChildAuthorityV1(Scrap),
                    holdingsChild);
                CostPolicy = policy ?? Policy();
                Service = CreateService(CostPolicy);
            }

            public MoneyWalletService Money { get; }
            public ScrapWalletServiceV1 Scrap { get; }
            public PlayerHoldingsService Holdings { get; }
            public RewardApplicationServiceV1 Rap { get; }
            public EquipmentCatalog Catalog { get; }
            public CatalogProvider Provider { get; }
            public CatalogValidator Validator { get; }
            public EquipmentInstance Equipment { get; }
            public AugmentUpgradeCostPolicyV1 CostPolicy { get; }
            public AugmentUpgradeServiceV1 Service { get; }

            public AugmentUpgradeQuoteV1 Quote(int targetLevel)
            {
                AugmentUpgradeQuoteResultV1 result = Service.Quote(
                    new AugmentUpgradeQuoteRequestV1(
                        EquipmentInstanceId,
                        PrimaryAugmentInstanceId,
                        targetLevel));
                Assert.That(result.Status, Is.EqualTo(AugmentUpgradeQuoteStatusV1.Quoted));
                Assert.That(result.Quote, Is.Not.Null);
                return result.Quote;
            }

            public AugmentUpgradeFactV1 Confirm(
                AugmentUpgradeQuoteV1 quote,
                string confirmationId)
            {
                return Service.Confirm(AugmentUpgradeConfirmationV1.Create(
                    Id(confirmationId),
                    quote));
            }

            public UniqueHoldingSnapshotV1 GetUnique(StableId instanceId)
            {
                UniqueHoldingSnapshotV1 holding;
                Assert.That(Holdings.TryGetUnique(instanceId, out holding), Is.True);
                return holding;
            }

            public void RemoveOriginal(string identityPrefix)
            {
                UniqueHoldingSnapshotV1 holding = GetUnique(EquipmentInstanceId);
                PlayerHoldingsMutationResultV1 result = Holdings.Apply(
                    PlayerHoldingsCommandV1.RemoveEquipment(
                        Id(identityPrefix + "-tx"),
                        Id(identityPrefix + "-op"),
                        HoldingsAuthority,
                        holding.DefinitionStableId,
                        holding.InstanceStableId,
                        holding.Provenance,
                        Holdings.Sequence));
                Assert.That(result.Status,
                    Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            }

            public AugmentUpgradeQuoteV1 CreateManualQuote(
                int targetLevel,
                long moneyCost)
            {
                AugmentInstance augment = FindAugment(Equipment, PrimaryAugmentInstanceId);
                return AugmentUpgradeQuoteV1.Create(
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

            public AugmentUpgradeServiceV1 CreateService(
                AugmentUpgradeCostPolicyV1 policy)
            {
                return new AugmentUpgradeServiceV1(
                    Money,
                    Holdings,
                    Rap,
                    Provider,
                    Validator,
                    policy,
                    new AugmentUpgradeIdentityContextV1(
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
                    EquipmentCategoryIds.Weapon,
                    Id("equipment-family.blaster"),
                    "Blaster",
                    Id("weapon-runtime.blaster"),
                    InclusiveIntRange.Create(1, 100),
                    3,
                    new[]
                    {
                        EquipmentQualityTier.Create(Id("quality.rare"), "Rare", 2),
                    },
                    new[] { Id("equipment-tag.energy") });
                AugmentCompatibility compatibility = AugmentCompatibility.Create(
                    new[] { EquipmentCategoryIds.Weapon },
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

        private sealed class ThrowOnceRewardChildAuthority : IRewardChildAuthorityV1
        {
            private readonly IRewardChildAuthorityV1 inner;
            private bool shouldThrow = true;

            public ThrowOnceRewardChildAuthority(IRewardChildAuthorityV1 inner)
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

            public RewardAuthorityPreflightResultV1 Preflight(
                IReadOnlyList<RewardChildGrantCommandV1> commands)
            {
                return inner.Preflight(commands);
            }

            public RewardChildApplyResultV1 Apply(
                RewardChildGrantCommandV1 command)
            {
                if (shouldThrow
                    && command != null
                    && command.GrantKind == RewardGrantKindV1.EquipmentReference)
                {
                    shouldThrow = false;
                    throw new InvalidOperationException("forced-upgrade-retry");
                }

                return inner.Apply(command);
            }
        }
    }
}
