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
    public sealed partial class ShopScreenControllerV1 Tests
    {
        [TearDown]
        public void ClearHandoff()
        {
            ShopScreenRuntimeHandoffV1.Clear();
        }

        private sealed class Fixture
        {
            private static readonly StableId RapAuthority = Id("authority.shopui-playmode-rap");
            private static readonly StableId ScrapAuthority = Id("authority.shopui-playmode-scrap");
            private static readonly StableId ScrapCurrency = Id("currency.shopui-playmode-scrap");
            private static readonly StableId HoldingsAuthority = Id("holdings.shopui-playmode-player");

            public Fixture(
                long startingMoney,
                IRewardChildAuthorityV1 holdingsAuthority = null)
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

                EquipmentGenerationPolicyV1 generation = EquipmentGenerationPolicyV1.Create(
                    Id("shop-generation.shopui-playmode"),
                    new[]
                    {
                        EquipmentGenerationCandidateV1.Create(
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
                        EquipmentQualityCandidateV1.Create(
                            common.QualityId,
                            0L,
                            1UL),
                    },
                    Array.Empty<AugmentGenerationCandidateV1>(),
                    0,
                    0,
                    true,
                    new SoftActivationCurveParameters(0.1, 5L, 5L),
                    new ObsolescenceCurveParameters(25L, 15.0, 0.2));
                Definition = ShopDefinitionV1.Create(
                    Id("shop.hub-shopui-playmode"),
                    2,
                    new[] { EquipmentCategoryIds.Weapon },
                    Array.Empty<StableId>(),
                    Array.Empty<StableId>(),
                    generation,
                    ShopProgressionContextPolicyV1.FreezeOnFirstOpen,
                    ShopPricingPolicyV1.Create(
                        Id("shop-pricing.shopui-playmode"),
                        1L,
                        20L,
                        3L,
                        11L,
                        17L,
                        5L,
                        2L),
                    ShopRefreshPolicyV1.Disabled,
                    0,
                    0,
                    DeterministicRandom.AlgorithmVersion1);

                Money = new MoneyWalletService();
                Money.Grant(
                    Id("shopui-playmode.initial-money"),
                    Id("shopui-playmode.initial-money-operation"),
                    startingMoney);
                var scrap = new ScrapWalletServiceV1(
                    ScrapAuthority,
                    ScrapCurrency);
                var validator = new AcceptingEquipmentValidator();
                var holdings = new PlayerHoldingsService(
                    HoldingsAuthority,
                    1000L,
                    validator);
                var rap = new RewardApplicationServiceV1(
                    RapAuthority,
                    new MoneyRewardChildAuthorityV1(Money),
                    new ScrapRewardChildAuthorityV1(scrap),
                    holdingsAuthority
                        ?? new PlayerHoldingsRewardChildAuthorityV1(
                            holdings,
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

            public MoneyWalletService Money { get; }
            public EquipmentCatalog Catalog { get; }
            public ShopDefinitionV1 Definition { get; }
            public ShopRuntimeServiceV1 Runtime { get; }
            public PlayerRouteProfilePayloadV1 RoutePayload { get; }

            public ShopScreenSessionV1 Session(string runId)
            {
                return new ShopScreenSessionV1(
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

        private sealed class TransientHoldingsAuthority : IRewardChildAuthorityV1
        {
            private readonly HashSet<StableId> applied = new HashSet<StableId>();

            public StableId AuthorityStableId { get; } =
                Id("holdings.shopui-playmode-transient");

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
                        "shopui-playmode-transient-rejection");
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
