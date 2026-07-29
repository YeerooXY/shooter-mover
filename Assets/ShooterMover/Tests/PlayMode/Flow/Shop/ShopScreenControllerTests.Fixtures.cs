using System;
using System.Collections.Generic;
using NUnit.Framework;
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
using ShooterMover.UI.Shop;

namespace ShooterMover.Tests.PlayMode.Flow.Shop
{
    public sealed partial class ShopScreenControllerTests
    {
        [TearDown]
        public void ClearHandoff()
        {
            ShopScreenLiveHandoff.Clear();
        }

        private sealed class Fixture
        {
            private static readonly StableId RapAuthority = Id("authority.shopui-playmode-rap");
            private static readonly StableId ScrapAuthority = Id("authority.shopui-playmode-scrap");
            private static readonly StableId ScrapCurrency = Id("currency.shopui-playmode-scrap");
            private static readonly StableId HoldingsAuthority = Id("holdings.shopui-playmode-player");

            public Fixture(
                long startingMoney,
                IRewardChildState holdingsAuthority = null)
            {
                EquipmentQualityTier common = EquipmentQualityTier.Create(
                    Id("quality.common"),
                    "Common",
                    1);
                EquipmentDefinition equipment = EquipmentDefinition.Create(
                    Id("equipment.shopui-playmode"),
                    EquipmentCategoryIds.Weapon,
                    Id("equipment-family.shopui-playmode"),
                    "Pulse Rifle",
                    Id("weapon.shopui-playmode"),
                    InclusiveIntRange.Create(1, 20),
                    0,
                    new[] { common },
                    Array.Empty<StableId>());
                EquipmentCatalogBuildResult catalogResult = EquipmentCatalog.Build(
                    new[] { equipment },
                    Array.Empty<AugmentDefinition>());
                Catalog = catalogResult.Catalog;

                EquipmentGenerationPolicy generation = EquipmentGenerationPolicy.Create(
                    Id("shop-generation.shopui-playmode"),
                    new[]
                    {
                        EquipmentGenerationCandidate.Create(
                            equipment.DefinitionId,
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
                        EquipmentQualityCandidate.Create(
                            common.QualityId,
                            0L,
                            1UL),
                    },
                    Array.Empty<AugmentGenerationCandidate>(),
                    0,
                    0,
                    true,
                    new SoftActivationCurveParameters(0.1, 5L, 5L),
                    new ObsolescenceCurveParameters(25L, 15.0, 0.2));
                Definition = ShopDefinition.Create(
                    Id("shop.hub-shopui-playmode"),
                    2,
                    new[] { EquipmentCategoryIds.Weapon },
                    Array.Empty<StableId>(),
                    Array.Empty<StableId>(),
                    generation,
                    ShopProgressionContextPolicy.FreezeOnFirstOpen,
                    ShopPricingPolicy.Create(
                        Id("shop-pricing.shopui-playmode"),
                        1L,
                        20L,
                        3L,
                        11L,
                        17L,
                        5L,
                        2L),
                    ShopRefreshPolicy.Disabled,
                    0,
                    0,
                    DeterministicRandom.AlgorithmVersion1);

                Money = new MoneyWalletActions();
                Money.Grant(
                    Id("shopui-playmode.initial-money"),
                    Id("shopui-playmode.initial-money-operation"),
                    startingMoney);
                var scrap = new ScrapWalletActions(
                    ScrapAuthority,
                    ScrapCurrency);
                var validator = new AcceptingEquipmentValidator();
                var holdings = new PlayerHoldingsActions(
                    HoldingsAuthority,
                    1000L,
                    validator);
                var rap = new RewardApplicationActions(
                    RapAuthority,
                    new MoneyRewardChildState(Money),
                    new ScrapRewardChildState(scrap),
                    holdingsAuthority
                        ?? new PlayerHoldingsRewardChildState(
                            holdings,
                            validator));
                Runtime = new ShopLiveActions(
                    new RewardGenerationActions(),
                    Money,
                    rap,
                    ScrapAuthority,
                    holdingsAuthority == null
                        ? HoldingsAuthority
                        : holdingsAuthority.AuthorityStableId);
                RoutePayload = PlayerRouteProfilePayload.Create(
                    Id("character.shopui-playmode"),
                    Id("loadout-profile.shopui-playmode"),
                    new[]
                    {
                        Id("equipment-instance.shopui-playmode-route-1"),
                        Id("equipment-instance.shopui-playmode-route-2"),
                        Id("equipment-instance.shopui-playmode-route-3"),
                        Id("equipment-instance.shopui-playmode-route-4"),
                    });
            }

            public MoneyWalletActions Money { get; }
            public EquipmentCatalog Catalog { get; }
            public ShopDefinition Definition { get; }
            public ShopLiveActions Runtime { get; }
            public PlayerRouteProfilePayload RoutePayload { get; }

            public ShopScreenSession Session(string runId)
            {
                return new ShopScreenSession(
                    RoutePayload,
                    Id(runId),
                    Id("player.shopui-playmode"),
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
                    "shopui-playmode-catalog",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    Array.Empty<EquipmentModelIssue>());
            }
        }

        private sealed class TransientHoldingsState : IRewardChildState
        {
            private readonly HashSet<StableId> applied = new HashSet<StableId>();

            public StableId AuthorityStableId { get; } =
                Id("holdings.shopui-playmode-transient");

            public long Sequence { get; private set; }
            public int ApplyCalls { get; private set; }
            public int ConfirmedApplications { get; private set; }

            public RewardStatePreflightResult Preflight(
                IReadOnlyList<RewardChildGrantCommand> commands)
            {
                var facts = new List<RewardStatePreflightFact>();
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

            public RewardChildApplyResult Apply(
                RewardChildGrantCommand command)
            {
                ApplyCalls++;
                if (ApplyCalls == 1)
                {
                    return new RewardChildApplyResult(
                        command.TransactionStableId,
                        RewardChildApplyStatus.Rejected,
                        false,
                        "shopui-playmode-transient-rejection");
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
