using System;
using System.Collections.Generic;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Shops;
using ShooterMover.Application.Shops.Presentation;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Tests.EditMode.Shops.Presentation
{
    public sealed partial class ShopScreenPresentationV1Tests
    {
        private static ShopScreenPurchaseInputV1 Input(
            string inputId,
            ShopScreenStockCardV1 card)
        {
            return new ShopScreenPurchaseInputV1(
                Id(inputId),
                card.StockEntryStableId);
        }

        private sealed class Fixture
        {
            private static readonly StableId RapAuthority = Id("authority.shopui-test-rap");
            private static readonly StableId ScrapAuthority = Id("authority.shopui-test-scrap");
            private static readonly StableId ScrapCurrency = Id("currency.shopui-test-scrap");
            private static readonly StableId HoldingsAuthority = Id("holdings.shopui-test-player");

            public Fixture(
                long startingMoney,
                int inventorySize,
                IRewardChildAuthorityV1 holdingsAuthority = null)
            {
                Catalog = BuildCatalog();
                Definition = BuildDefinition(inventorySize);
                Money = new MoneyWalletService();
                if (startingMoney > 0L)
                {
                    Money.Grant(
                        Id("shopui-fixture.initial-money"),
                        Id("shopui-fixture.initial-money-operation"),
                        startingMoney);
                }

                var scrap = new ScrapWalletServiceV1(
                    ScrapAuthority,
                    ScrapCurrency);
                var validator = new AcceptingEquipmentValidator();
                Holdings = new PlayerHoldingsService(
                    HoldingsAuthority,
                    1000L,
                    validator);
                var rap = new RewardApplicationServiceV1(
                    RapAuthority,
                    new MoneyRewardChildAuthorityV1(Money),
                    new ScrapRewardChildAuthorityV1(scrap),
                    holdingsAuthority
                        ?? new PlayerHoldingsRewardChildAuthorityV1(
                            Holdings,
                            validator));
                Runtime = new ShopRuntimeServiceV1(
                    new RewardGenerationServiceV1(),
                    Money,
                    rap,
                    ScrapAuthority,
                    holdingsAuthority == null
                        ? HoldingsAuthority
                        : holdingsAuthority.AuthorityStableId);
                RoutePayload = PlayerRouteProfilePayloadV1.Create(
                    Id("character.shopui-test"),
                    Id("loadout-profile.shopui-test"),
                    new[]
                    {
                        Id("equipment-instance.shopui-route-1"),
                        Id("equipment-instance.shopui-route-2"),
                        Id("equipment-instance.shopui-route-3"),
                        Id("equipment-instance.shopui-route-4"),
                    });
            }

            public EquipmentCatalog Catalog { get; }
            public ShopDefinitionV1 Definition { get; }
            public MoneyWalletService Money { get; }
            public PlayerHoldingsService Holdings { get; }
            public ShopRuntimeServiceV1 Runtime { get; }
            public PlayerRouteProfilePayloadV1 RoutePayload { get; }

            public ShopScreenSessionV1 Session(string runId)
            {
                return new ShopScreenSessionV1(
                    RoutePayload,
                    Id(runId),
                    Id("player.shopui-test"),
                    Runtime,
                    Money,
                    Definition,
                    Catalog,
                    ProgressionContext.Create(
                        10,
                        1,
                        Id("difficulty.normal"),
                        1,
                        Array.Empty<StableId>()));
            }
        }

        private static ShopDefinitionV1 BuildDefinition(int inventorySize)
        {
            EquipmentGenerationPolicyV1 generation = EquipmentGenerationPolicyV1.Create(
                Id("shop-generation.shopui-test"),
                new[]
                {
                    EquipmentGenerationCandidateV1.Create(
                        Id("equipment.shopui-test-armor"),
                        0,
                        100,
                        0,
                        100,
                        Array.Empty<StableId>(),
                        0L,
                        InclusiveIntRange.Create(1, 20),
                        1.0,
                        1.0),
                },
                new[]
                {
                    EquipmentQualityCandidateV1.Create(
                        Id("quality.common"),
                        0L,
                        1UL),
                },
                Array.Empty<AugmentGenerationCandidateV1>(),
                0,
                0,
                true,
                new SoftActivationCurveParameters(0.1, 5L, 5L),
                new ObsolescenceCurveParameters(25L, 15.0, 0.2));
            ShopPricingPolicyV1 pricing = ShopPricingPolicyV1.Create(
                Id("shop-pricing.shopui-test"),
                1L,
                20L,
                3L,
                11L,
                17L,
                5L,
                2L);
            return ShopDefinitionV1.Create(
                Id("shop.hub-shopui-test"),
                inventorySize,
                new[] { EquipmentCategoryIds.Armor },
                Array.Empty<StableId>(),
                Array.Empty<StableId>(),
                generation,
                ShopProgressionContextPolicyV1.FreezeOnFirstOpen,
                pricing,
                ShopRefreshPolicyV1.Disabled,
                0,
                0,
                DeterministicRandom.AlgorithmVersion1);
        }

        private static EquipmentCatalog BuildCatalog()
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(
                Id("quality.common"),
                "Common",
                1);
            EquipmentDefinition armor = EquipmentDefinition.Create(
                Id("equipment.shopui-test-armor"),
                EquipmentCategoryIds.Armor,
                Id("equipment-family.shopui-test-armor"),
                "Shop UI Test Armor",
                null,
                InclusiveIntRange.Create(1, 20),
                0,
                new[] { common },
                Array.Empty<StableId>());
            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[] { armor },
                Array.Empty<AugmentDefinition>());
            if (!result.IsValid)
            {
                throw new InvalidOperationException("Shop UI fixture catalog is invalid.");
            }

            return result.Catalog;
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
                    "shopui-test-catalog",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    Array.Empty<EquipmentModelIssue>());
            }
        }

        private sealed class TransientHoldingsAuthority : IRewardChildAuthorityV1
        {
            private readonly HashSet<StableId> applied = new HashSet<StableId>();

            public StableId AuthorityStableId { get; } =
                Id("holdings.shopui-transient-test");

            public long Sequence { get; private set; }

            public int ApplyCalls { get; private set; }

            public int ConfirmedApplications { get; private set; }

            public RewardAuthorityPreflightResultV1 Preflight(
                IReadOnlyList<RewardChildGrantCommandV1> commands)
            {
                var facts = new List<RewardAuthorityPreflightFactV1>();
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

            public RewardChildApplyResultV1 Apply(
                RewardChildGrantCommandV1 command)
            {
                ApplyCalls++;
                if (ApplyCalls == 1)
                {
                    return new RewardChildApplyResultV1(
                        command.TransactionStableId,
                        RewardChildApplyStatusV1.Rejected,
                        false,
                        "shopui-transient-rejection");
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
