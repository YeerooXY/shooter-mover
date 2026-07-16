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
    public sealed partial class ShopRuntimeServiceV1Tests
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
                IRewardChildAuthorityV1 holdingsAuthority = null,
                IEquipmentInstanceValidator validator = null)
            {
                Catalog = BuildCatalog();
                Definition = BuildDefinition(
                    inventorySize,
                    maximumRefreshes,
                    baseLockCapacity);
                Money = new MoneyWalletService();
                if (startingMoney > 0L)
                {
                    Money.Grant(
                        Id("shop-money-fixture.initial"),
                        Id("shop-money-fixture.initial-operation"),
                        startingMoney);
                }

                ScrapWalletServiceV1 scrap = new ScrapWalletServiceV1(
                    ScrapAuthority,
                    ScrapCurrency);
                Validator = validator ?? new AcceptingEquipmentValidator();
                Holdings = new PlayerHoldingsService(
                    HoldingsAuthority,
                    1000L,
                    Validator);
                RewardApplicationServiceV1 rap = new RewardApplicationServiceV1(
                    RapAuthority,
                    new MoneyRewardChildAuthorityV1(Money),
                    new ScrapRewardChildAuthorityV1(scrap),
                    holdingsAuthority ?? new PlayerHoldingsRewardChildAuthorityV1(Holdings, Validator));
                Service = new ShopRuntimeServiceV1(
                    new RewardGenerationServiceV1(),
                    Money,
                    rap,
                    ScrapAuthority,
                    holdingsAuthority == null ? HoldingsAuthority : holdingsAuthority.AuthorityStableId);
            }

            public EquipmentCatalog Catalog { get; }
            public ShopDefinitionV1 Definition { get; }
            public MoneyWalletService Money { get; }
            public PlayerHoldingsService Holdings { get; }
            public IEquipmentInstanceValidator Validator { get; }
            public ShopRuntimeServiceV1 Service { get; }

            public ShopInventoryViewV1 Open(
                string runId,
                ProgressionContext context = null)
            {
                ShopInventoryOpenResultV1 result = Service.Open(
                    Id(runId),
                    Definition,
                    Catalog,
                    context ?? Context(10));
                Assert.That(result.Succeeded, Is.True, result.RejectionCode);
                return result.Inventory;
            }

            public ShopPurchaseCommandV1 PurchaseCommand(
                string transactionId,
                ShopInventoryViewV1 inventory,
                ShopStockEntryV1 entry)
            {
                return ShopPurchaseCommandV1.Create(
                    Id(transactionId),
                    inventory.RunStableId,
                    inventory.ShopStableId,
                    entry.StockEntryStableId,
                    Id("player.fixture"),
                    inventory.InventoryFingerprint,
                    entry.Price);
            }
        }

        private static ShopDefinitionV1 BuildDefinition(
            int inventorySize,
            int maximumRefreshes,
            int baseLockCapacity)
        {
            EquipmentGenerationPolicyV1 generation = EquipmentGenerationPolicyV1.Create(
                Id("shop-generation.fixture"),
                new[]
                {
                    Candidate("equipment.armor-energy-a"),
                    Candidate("equipment.armor-energy-b"),
                    Candidate("equipment.armor-forbidden"),
                    Candidate("equipment.weapon-energy"),
                },
                new[]
                {
                    EquipmentQualityCandidateV1.Create(Id("quality.common"), 0L, 3UL),
                    EquipmentQualityCandidateV1.Create(Id("quality.rare"), 5L, 1UL),
                },
                Array.Empty<AugmentGenerationCandidateV1>(),
                0,
                0,
                true,
                new SoftActivationCurveParameters(0.1, 5L, 5L),
                new ObsolescenceCurveParameters(25L, 15.0, 0.2));
            ShopPricingPolicyV1 pricing = ShopPricingPolicyV1.Create(
                Id("shop-pricing.fixture"),
                1L,
                20L,
                3L,
                11L,
                17L,
                5L,
                2L);
            return ShopDefinitionV1.Create(
                Id("shop.fixture"),
                inventorySize,
                new[] { EquipmentCategoryIds.Armor },
                new[] { Id("equipment-tag.energy") },
                new[] { Id("equipment-tag.forbidden") },
                generation,
                ShopProgressionContextPolicyV1.FreezeOnFirstOpen,
                pricing,
                maximumRefreshes == 0
                    ? ShopRefreshPolicyV1.Disabled
                    : ShopRefreshPolicyV1.ExplicitRunBound,
                maximumRefreshes,
                baseLockCapacity,
                DeterministicRandom.AlgorithmVersion1);
        }

        private static EquipmentGenerationCandidateV1 Candidate(string definitionId)
        {
            return EquipmentGenerationCandidateV1.Create(
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
            EquipmentDefinition weapon = EquipmentDefinition.Create(
                Id("equipment.weapon-energy"),
                EquipmentCategoryIds.Weapon,
                Id("equipment-family.weapon-energy"),
                "Weapon Energy",
                Id("weapon.shop-test"),
                InclusiveIntRange.Create(1, 20),
                0,
                new[] { common, rare },
                new[] { energy });
            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[] { weapon, forbiddenArmor, armorB, armorA },
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

        private sealed class TransientHoldingsAuthority : IRewardChildAuthorityV1
        {
            private readonly HashSet<StableId> applied = new HashSet<StableId>();

            public StableId AuthorityStableId { get; } = Id("holdings.transient-shop-test");
            public long Sequence { get; private set; }
            public int ApplyCalls { get; private set; }
            public int ConfirmedApplications { get; private set; }

            public RewardAuthorityPreflightResultV1 Preflight(
                IReadOnlyList<RewardChildGrantCommandV1> commands)
            {
                List<RewardAuthorityPreflightFactV1> facts =
                    new List<RewardAuthorityPreflightFactV1>();
                for (int index = 0; index < commands.Count; index++)
                {
                    facts.Add(new RewardAuthorityPreflightFactV1(
                        commands[index].TransactionStableId,
                        applied.Contains(commands[index].TransactionStableId)
                            ? RewardAuthorityAdmissionStatusV1.AlreadyApplied
                            : RewardAuthorityAdmissionStatusV1.Accepted,
                        null));
                }

                return new RewardAuthorityPreflightResultV1(facts);
            }

            public RewardChildApplyResultV1 Apply(RewardChildGrantCommandV1 command)
            {
                ApplyCalls++;
                if (ApplyCalls == 1)
                {
                    return new RewardChildApplyResultV1(
                        command.TransactionStableId,
                        RewardChildApplyStatusV1.Rejected,
                        false,
                        "transient-shop-test-rejection");
                }

                if (applied.Add(command.TransactionStableId))
                {
                    Sequence++;
                    ConfirmedApplications++;
                    return new RewardChildApplyResultV1(
                        command.TransactionStableId,
                        RewardChildApplyStatusV1.Applied,
                        true,
                        null);
                }

                return new RewardChildApplyResultV1(
                    command.TransactionStableId,
                    RewardChildApplyStatusV1.ExactDuplicateNoChange,
                    true,
                    null);
            }
        }
    }
}
