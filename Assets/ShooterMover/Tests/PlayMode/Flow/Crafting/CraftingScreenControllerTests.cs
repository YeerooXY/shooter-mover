using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Crafting;
using ShooterMover.Application.Crafting.Presentation;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Crafting;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.UI.Crafting;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Flow.Crafting
{
    public sealed class CraftingScreenControllerTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
            }
        }

        [Test]
        public void ControllerCraftsExactPreviewAndReturnsSamePayloadOnce()
        {
            ControllerFixture fixture = ControllerFixture.Create(100);
            CraftingScreenController controller = CreateController(fixture, out List<PlayerRouteProfilePayload> returns);

            controller.Present(HubRoute.Crafting, fixture.RoutePayload);
            EquipmentInstance preview = controller.Snapshot.SelectedRecipe.PreviewEquipment;
            CraftingScreenResult crafted = controller.Craft();
            CraftingScreenResult back = controller.Back();
            controller.Back();

            Assert.That(crafted.Status, Is.EqualTo(CraftingScreenStatus.Crafted));
            Assert.That(crafted.AuthorityResult.Equipment.Fingerprint, Is.EqualTo(preview.Fingerprint));
            Assert.That(fixture.Authority.ScrapBalance, Is.EqualTo(75));
            Assert.That(fixture.Authority.GrantCount, Is.EqualTo(1));
            Assert.That(back.RoutePayload, Is.SameAs(fixture.RoutePayload));
            Assert.That(returns.Count, Is.EqualTo(1));
            Assert.That(returns[0], Is.SameAs(fixture.RoutePayload));
            Assert.That(controller.ReturnCount, Is.EqualTo(1));
            Assert.That(controller.LastReturnedPayload, Is.SameAs(fixture.RoutePayload));
        }

        [Test]
        public void ControllerRetryUsesSameOperationAndDoesNotDoubleSpend()
        {
            ControllerFixture fixture = ControllerFixture.Create(100);
            fixture.Authority.ReturnRetryOnce = true;
            CraftingScreenController controller = CreateController(fixture, out _);
            controller.Present(HubRoute.Crafting, fixture.RoutePayload);
            string fingerprint = controller.Snapshot.SelectedRecipe.Command.Fingerprint;

            CraftingScreenResult pending = controller.Craft();
            CraftingScreenResult applied = controller.Retry();

            Assert.That(pending.Status, Is.EqualTo(CraftingScreenStatus.RetryRequired));
            Assert.That(applied.Status, Is.EqualTo(CraftingScreenStatus.Crafted));
            Assert.That(fixture.Authority.CommandFingerprints,
                Is.EqualTo(new[] { fingerprint, fingerprint }));
            Assert.That(fixture.Authority.ScrapBalance, Is.EqualTo(75));
            Assert.That(fixture.Authority.GrantCount, Is.EqualTo(1));
        }

        [Test]
        public void ControllerRevisitReadsAuthorityStateAndPreservesRoutePayload()
        {
            ControllerFixture fixture = ControllerFixture.Create(100);
            CraftingScreenController controller = CreateController(fixture, out _);

            controller.Present(HubRoute.Crafting, fixture.RoutePayload);
            controller.Craft();
            controller.Back();
            controller.Present(HubRoute.Crafting, fixture.RoutePayload);

            Assert.That(controller.Snapshot.ScrapBalance, Is.EqualTo(75));
            Assert.That(controller.Snapshot.HoldingsSequence, Is.EqualTo(1));
            Assert.That(controller.IncomingPayload, Is.SameAs(fixture.RoutePayload));
            Assert.That(fixture.Authority.GrantCount, Is.EqualTo(1));
        }

        [Test]
        public void ControllerRejectsNonCraftingHubRoute()
        {
            ControllerFixture fixture = ControllerFixture.Create(100);
            CraftingScreenController controller = CreateController(fixture, out _);

            Assert.Throws<ArgumentOutOfRangeException>(
                delegate { controller.Present(HubRoute.Shop, fixture.RoutePayload); });
        }

        private CraftingScreenController CreateController(
            ControllerFixture fixture,
            out List<PlayerRouteProfilePayload> returns)
        {
            root = new GameObject("CraftingScreenControllerTests");
            CraftingScreenController controller =
                root.AddComponent<CraftingScreenController>();
            returns = new List<PlayerRouteProfilePayload>();
            List<PlayerRouteProfilePayload> captured = returns;
            controller.ConfigureForTests(
                fixture.Authority,
                fixture.Progression,
                12345UL,
                StableId.Parse("crafting-screen.playmode"),
                StableId.Parse("run.playmode"),
                StableId.Parse("claimant.playmode"),
                delegate(PlayerRouteProfilePayload payload) { captured.Add(payload); });
            return controller;
        }

        private sealed class ControllerFixture
        {
            private ControllerFixture(
                PlayerRouteProfilePayload routePayload,
                ProgressionContext progression,
                ControllerFakeState authority)
            {
                RoutePayload = routePayload;
                Progression = progression;
                Authority = authority;
            }

            public PlayerRouteProfilePayload RoutePayload { get; }
            public ProgressionContext Progression { get; }
            public ControllerFakeState Authority { get; }

            public static ControllerFixture Create(long balance)
            {
                EquipmentQualityTier quality = EquipmentQualityTier.Create(
                    StableId.Parse("quality.playmode"),
                    "PlayMode",
                    1);
                EquipmentDefinition weapon = EquipmentDefinition.Create(
                    StableId.Parse("weapon.playmode"),
                    EquipmentCategoryIds.Weapon,
                    StableId.Parse("weapon-family.playmode"),
                    "PlayMode Weapon",
                    StableId.Parse("weapon.runtime-playmode"),
                    InclusiveIntRange.Create(1, 20),
                    0,
                    new[] { quality },
                    Array.Empty<StableId>());
                EquipmentCatalogBuildResult built = EquipmentCatalog.Build(
                    new[] { weapon },
                    Array.Empty<AugmentDefinition>());
                Assert.That(built.IsValid, Is.True);

                CraftingRecipe recipe = new CraftingRecipe(
                    1,
                    StableId.Parse("recipe.playmode"),
                    weapon.DefinitionId,
                    StableId.Parse("discovery.playmode"),
                    2,
                    2,
                    3,
                    new CraftingDelayVariance(0, 0),
                    25,
                    CraftingQualityPolicyKind.Fixed,
                    new[]
                    {
                        new CraftingWeightedDefinition(quality.QualityId, 1UL),
                    },
                    1,
                    20,
                    0,
                    0,
                    1,
                    1,
                    Array.Empty<CraftingWeightedDefinition>(),
                    new CraftingGeneratorPolicy(
                        StableId.Parse("crafting-policy.playmode"),
                        1,
                        new SoftActivationCurveParameters(0.1, 2, 2),
                        new ObsolescenceCurveParameters(2, 4.0, 0.1)));
                CraftingRecipeCatalog recipes = new CraftingRecipeCatalog(
                    new[] { recipe });
                ControllerFakeState authority = new ControllerFakeState(
                    balance,
                    recipes,
                    built.Catalog,
                    quality.QualityId);
                ProgressionContext progression = ProgressionContext.Create(
                    10,
                    10,
                    StableId.Parse("difficulty.playmode"),
                    1);
                PlayerRouteProfilePayload route = PlayerRouteProfilePayload.Create(
                    StableId.Parse("character.playmode"),
                    StableId.Parse("loadout.playmode"),
                    new[]
                    {
                        StableId.Parse("equipment.playmode-1"),
                        StableId.Parse("equipment.playmode-2"),
                        StableId.Parse("equipment.playmode-3"),
                        StableId.Parse("equipment.playmode-4"),
                    });
                return new ControllerFixture(route, progression, authority);
            }
        }

        private sealed class ControllerFakeState : ICraftingPresentationStatePort
        {
            private readonly CraftingRecipeCatalog recipes;
            private readonly EquipmentCatalog equipment;
            private readonly StableId qualityStableId;
            private readonly Dictionary<StableId, EquipmentInstance> applied =
                new Dictionary<StableId, EquipmentInstance>();
            private bool returnedRetry;

            public ControllerFakeState(
                long balance,
                CraftingRecipeCatalog recipes,
                EquipmentCatalog equipment,
                StableId qualityStableId)
            {
                ScrapBalance = balance;
                this.recipes = recipes;
                this.equipment = equipment;
                this.qualityStableId = qualityStableId;
            }

            public long ScrapBalance { get; private set; }
            public long ScrapSequence { get; private set; }
            public long HoldingsSequence { get; private set; }
            public int GrantCount { get; private set; }
            public bool ReturnRetryOnce { get; set; }
            public List<string> CommandFingerprints { get; } = new List<string>();

            public CraftingPresentationStateSnapshot ExportSnapshot()
            {
                return new CraftingPresentationStateSnapshot(
                    ScrapBalance,
                    ScrapSequence,
                    HoldingsSequence,
                    recipes,
                    equipment,
                    "playmode|" + ScrapSequence + "|" + HoldingsSequence);
            }

            public CraftingPresentationStateResult Preview(
                CraftEquipmentCommand command)
            {
                return Result(
                    command,
                    CraftingResultStatus.Crafted,
                    CreateEquipment(command),
                    string.Empty);
            }

            public CraftingPresentationStateResult Craft(
                CraftEquipmentCommand command)
            {
                CommandFingerprints.Add(command.Fingerprint);
                EquipmentInstance existing;
                if (applied.TryGetValue(command.CraftTransactionStableId, out existing))
                {
                    return Result(
                        command,
                        CraftingResultStatus.ExactDuplicateNoChange,
                        existing,
                        string.Empty);
                }

                if (ReturnRetryOnce && !returnedRetry)
                {
                    returnedRetry = true;
                    return Result(
                        command,
                        CraftingResultStatus.RewardApplicationRetryRequired,
                        CreateEquipment(command),
                        "pending");
                }

                CraftingRecipe recipe = recipes.Find(command.RecipeStableId);
                EquipmentInstance generated = CreateEquipment(command);
                ScrapBalance -= recipe.ScrapCost;
                ScrapSequence++;
                HoldingsSequence++;
                GrantCount++;
                applied.Add(command.CraftTransactionStableId, generated);
                return Result(
                    command,
                    CraftingResultStatus.Crafted,
                    generated,
                    string.Empty);
            }

            private CraftingPresentationStateResult Result(
                CraftEquipmentCommand command,
                CraftingResultStatus status,
                EquipmentInstance generated,
                string rejectionCode)
            {
                CraftingRecipe recipe = recipes.Find(command.RecipeStableId);
                return new CraftingPresentationStateResult(
                    status,
                    recipe.RecipeStableId,
                    recipe.ResolveUnlockLevel(command.RootSeed),
                    recipe.ScrapCost,
                    generated,
                    command.Fingerprint,
                    rejectionCode);
            }

            private EquipmentInstance CreateEquipment(CraftEquipmentCommand command)
            {
                CraftingRecipe recipe = recipes.Find(command.RecipeStableId);
                return EquipmentInstance.Create(
                    CraftingFormat.DeriveStableId(
                        "craftitem",
                        command.CraftTransactionStableId.ToString()),
                    recipe.TargetEquipmentDefinitionStableId,
                    8,
                    qualityStableId,
                    Array.Empty<AugmentInstance>());
            }
        }
    }
}
