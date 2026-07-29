using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Shops;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Tests.EditMode.Shops
{
    public sealed partial class ShopLiveActionsTests
    {
        private sealed class Fixture
        {
            private static readonly StableId RapAuthority = Id("authority.shop-test-rap");
            private static readonly StableId ScrapAuthority = Id("authority.shop-test-scrap");
            private static readonly StableId ScrapCurrency = Id("currency.shop-test-scrap");
            private static readonly StableId HoldingsAuthority = Id("holdings.shop-test-player");

            public Fixture(
                long startingMoney = 0L,
                int inventorySize = 3,
                int maximumRefreshes = 0,
                int baseLockCapacity = 0,
                IRewardChildState holdingsAuthority = null,
                IEquipmentInstanceValidator validator = null)
            {
                Catalog = BuildCatalog();
                Definition = BuildDefinition(
                    inventorySize,
                    maximumRefreshes,
                    baseLockCapacity);
                Money = new MoneyWalletActions();
                if (startingMoney > 0L)
                {
                    Money.Grant(
                        Id("shop-money-fixture.initial"),
                        Id("shop-money-fixture.initial-operation"),
                        startingMoney);
                }

                ScrapWalletActions scrap = new ScrapWalletActions(
                    ScrapAuthority,
                    ScrapCurrency);
                Validator = validator ?? new AcceptingEquipmentValidator();
                Holdings = new PlayerHoldingsActions(
                    HoldingsAuthority,
                    1000L,
                    Validator);
                RewardApplicationActions rap = new RewardApplicationActions(
                    RapAuthority,
                    new MoneyRewardChildState(Money),
                    new ScrapRewardChildState(scrap),
                    holdingsAuthority ?? new PlayerHoldingsRewardChildState(Holdings, Validator));
                Service = new ShopLiveActions(
                    new RewardGenerationActions(),
                    Money,
                    rap,
                    ScrapAuthority,
                    holdingsAuthority == null ? HoldingsAuthority : holdingsAuthority.AuthorityStableId);
            }

            public EquipmentCatalog Catalog { get; }
            public ShopDefinition Definition { get; }
            public MoneyWalletActions Money { get; }
            public PlayerHoldingsActions Holdings { get; }
            public IEquipmentInstanceValidator Validator { get; }
            public ShopLiveActions Service { get; }

            public ShopInventoryView Open(
                string runId,
                ProgressionContext context = null)
            {
                ShopInventoryOpenResult result = Service.Open(
                    Id(runId),
                    Definition,
                    Catalog,
                    context ?? Context(10));
                Assert.That(result.Succeeded, Is.True, result.RejectionCode);
                return result.Inventory;
            }

            public ShopPurchaseCommand PurchaseCommand(
                string transactionId,
                ShopInventoryView inventory,
                ShopStockEntry entry)
            {
                return ShopPurchaseCommand.Create(
                    Id(transactionId),
                    inventory.RunStableId,
                    inventory.ShopStableId,
                    entry.StockEntryStableId,
                    Id("player.fixture"),
                    inventory.InventoryFingerprint,
                    entry.Price);
            }
        }

        private static ShopDefinition BuildDefinition(
            int inventorySize,
            int maximumRefreshes,
            int baseLockCapacity)
        {
            EquipmentGenerationPolicy generation = EquipmentGenerationPolicy.Create(
                Id("shop-generation.fixture"),
                new[]
                {
                    Candidate("equipment.armor-energy-a"),
                    Candidate("equipment.armor-energy-b"),
                    Candidate("equipment.armor-forbidden"),
                    Candidate("equipment.gun-energy"),
                },
                new[]
                {
                    EquipmentQualityCandidate.Create(Id("quality.common"), 0L, 3UL),
                    EquipmentQualityCandidate.Create(Id("quality.rare"), 5L, 1UL),
                },
                Array.Empty<AugmentGenerationCandidate>(),
                0,
                0,
                true,
                new SoftActivationCurveParameters(0.1, 5L, 5L),
                new ObsolescenceCurveParameters(25L, 15.0, 0.2));
            ShopPricingPolicy pricing = ShopPricingPolicy.Create(
                Id("shop-pricing.fixture"),
                1L,
                20L,
                3L,
                11L,
                17L,
                5L,
                2L);
            return ShopDefinition.Create(
                Id("shop.fixture"),
                inventorySize,
                new[] { EquipmentCategoryIds.Armor },
                new[] { Id("equipment-tag.energy") },
                new[] { Id("equipment-tag.forbidden") },
                generation,
                ShopProgressionContextPolicy.FreezeOnFirstOpen,
                pricing,
                maximumRefreshes == 0
                    ? ShopRefreshPolicy.Disabled
                    : ShopRefreshPolicy.ExplicitRunBound,
                maximumRefreshes,
                baseLockCapacity,
                DeterministicRandom.AlgorithmVersion1);
        }

        private static EquipmentGenerationCandidate Candidate(string definitionId)
        {
            return EquipmentGenerationCandidate.Create(
                Id(definitionId),
                0,
                100,
                0,
                100,
                Array.Empty<StableId>(),
                0L,
                InclusiveIntRange.Create(1, 20),
                1.0,
                1.0);
        }

        private static EquipmentCatalog BuildCatalog()
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(
                Id("quality.common"),
                "Common",
                1);
            EquipmentQualityTier rare = EquipmentQualityTier.Create(
                Id("quality.rare"),
                "Rare",
                2);
            StableId energy = Id("equipment-tag.energy");
            StableId forbidden = Id("equipment-tag.forbidden");
            EquipmentDefinition armorA = EquipmentDefinition.Create(
                Id("equipment.armor-energy-a"),
                EquipmentCategoryIds.Armor,
                Id("equipment-family.armor-a"),
                "Armor Energy A",
                null,
                InclusiveIntRange.Create(1, 20),
                0,
                new[] { common, rare },
                new[] { energy });
            EquipmentDefinition armorB = EquipmentDefinition.Create(
                Id("equipment.armor-energy-b"),
                EquipmentCategoryIds.Armor,
                Id("equipment-family.armor-b"),
                "Armor Energy B",
                null,
                InclusiveIntRange.Create(1, 20),
                0,
                new[] { common, rare },
                new[] { energy });
            EquipmentDefinition forbiddenArmor = EquipmentDefinition.Create(
                Id("equipment.armor-forbidden"),
                EquipmentCategoryIds.Armor,
                Id("equipment-family.armor-forbidden"),
                "Armor Forbidden",
                null,
                InclusiveIntRange.Create(1, 20),
                0,
                new[] { common, rare },
                new[] { energy, forbidden });
            EquipmentDefinition gun = EquipmentDefinition.Create(
                Id("equipment.gun-energy"),
                EquipmentCategoryIds.Gun,
                Id("equipment-family.gun-energy"),
                "Gun Energy",
                Id("gun.shop-test"),
                InclusiveIntRange.Create(1, 20),
                0,
                new[] { common, rare },
                new[] { energy });
            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[] { gun, forbiddenArmor, armorB, armorA },
                Array.Empty<AugmentDefinition>());
            if (!result.IsValid)
            {
                throw new InvalidOperationException("Shop fixture catalog is invalid.");
            }

            return result.Catalog;
        }

        private static ProgressionContext Context(int characterLevel)
        {
            return ProgressionContext.Create(
                characterLevel,
                1,
                Id("difficulty.normal"),
                1,
                Array.Empty<StableId>());
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class AcceptingEquipmentValidator : IEquipmentInstanceValidator
        {
            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                return new EquipmentInstanceValidationResponse(
                    request != null && request.Instance != null,
                    "shop-test-catalog",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    Array.Empty<EquipmentModelIssue>());
            }
        }

        private sealed class RejectingEquipmentValidator : IEquipmentInstanceValidator
        {
            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                return new EquipmentInstanceValidationResponse(
                    false,
                    "shop-test-catalog",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    Array.Empty<EquipmentModelIssue>());
            }
        }

        private sealed class TransientHoldingsState : IRewardChildState
        {
            private readonly HashSet<StableId> applied = new HashSet<StableId>();

            public StableId AuthorityStableId { get; } = Id("holdings.transient-shop-test");
            public long Sequence { get; private set; }
            public int ApplyCalls { get; private set; }
            public int ConfirmedApplications { get; private set; }

            public RewardStatePreflightResult Preflight(
                IReadOnlyList<RewardChildGrantCommand> commands)
            {
                List<RewardStatePreflightFact> facts =
                    new List<RewardStatePreflightFact>();
                for (int index = 0; index < commands.Count; index++)
                {
                    facts.Add(new RewardStatePreflightFact(
                        commands[index].TransactionStableId,
                        applied.Contains(commands[index].TransactionStableId)
                            ? RewardStateAdmissionStatus.AlreadyApplied
                            : RewardStateAdmissionStatus.Accepted,
                        null));
                }

                return new RewardStatePreflightResult(facts);
            }

            public RewardChildApplyResult Apply(RewardChildGrantCommand command)
            {
                ApplyCalls++;
                if (ApplyCalls == 1)
                {
                    return new RewardChildApplyResult(
                        command.TransactionStableId,
                        RewardChildApplyStatus.Rejected,
                        false,
                        "transient-shop-test-rejection");
                }

                if (applied.Add(command.TransactionStableId))
                {
                    Sequence++;
                    ConfirmedApplications++;
                    return new RewardChildApplyResult(
                        command.TransactionStableId,
                        RewardChildApplyStatus.Applied,
                        true,
                        null);
                }

                return new RewardChildApplyResult(
                    command.TransactionStableId,
                    RewardChildApplyStatus.ExactDuplicateNoChange,
                    true,
                    null);
            }
        }
    }
}
