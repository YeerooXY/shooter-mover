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
            CraftingScreenControllerV1 controller = CreateController(fixture, out List<PlayerRouteProfilePayloadV1> returns);

            controller.Present(HubRouteV1.Crafting, fixture.RoutePayload);
            EquipmentInstance preview = controller.Snapshot.SelectedRecipe.PreviewEquipment;
            CraftingScreenResultV1 crafted = controller.Craft();
            CraftingScreenResultV1 back = controller.Back();
            controller.Back();

            Assert.That(crafted.Status, Is.EqualTo(CraftingScreenStatusV1.Crafted));
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
            CraftingScreenControllerV1 controller = CreateController(fixture, out _);
            controller.Present(HubRouteV1.Crafting, fixture.RoutePayload);
            string fingerprint = controller.Snapshot.SelectedRecipe.Command.Fingerprint;

            CraftingScreenResultV1 pending = controller.Craft();
            CraftingScreenResultV1 applied = controller.Retry();

            Assert.That(pending.Status, Is.EqualTo(CraftingScreenStatusV1.RetryRequired));
            Assert.That(applied.Status, Is.EqualTo(CraftingScreenStatusV1.Crafted));
            Assert.That(fixture.Authority.CommandFingerprints,
                Is.EqualTo(new[] { fingerprint, fingerprint }));
            Assert.That(fixture.Authority.ScrapBalance, Is.EqualTo(75));
            Assert.That(fixture.Authority.GrantCount, Is.EqualTo(1));
        }

        [Test]
        public void ControllerRevisitReadsAuthorityStateAndPreservesRoutePayload()
        {
            ControllerFixture fixture = ControllerFixture.Create(100);
            CraftingScreenControllerV1 controller = CreateController(fixture, out _);

            controller.Present(HubRouteV1.Crafting, fixture.RoutePayload);
            controller.Craft();
            controller.Back();
            controller.Present(HubRouteV1.Crafting, fixture.RoutePayload);

            Assert.That(controller.Snapshot.ScrapBalance, Is.EqualTo(75));
            Assert.That(controller.Snapshot.HoldingsSequence, Is.EqualTo(1));
            Assert.That(controller.IncomingPayload, Is.SameAs(fixture.RoutePayload));
            Assert.That(fixture.Authority.GrantCount, Is.EqualTo(1));
        }

        [Test]
        public void ControllerRejectsNonCraftingHubRoute()
        {
            ControllerFixture fixture = ControllerFixture.Create(100);
            CraftingScreenControllerV1 controller = CreateController(fixture, out _);

            Assert.Throws<ArgumentOutOfRangeException>(
                delegate { controller.Present(HubRouteV1.Shop, fixture.RoutePayload); });
        }

        private CraftingScreenControllerV1 CreateController(
            ControllerFixture fixture,
            out List<PlayerRouteProfilePayloadV1> returns)
        {
            root = new GameObject("CraftingScreenControllerTests");
            CraftingScreenControllerV1 controller =
                root.AddComponent<CraftingScreenControllerV1>();
            returns = new List<PlayerRouteProfilePayloadV1>();
            List<PlayerRouteProfilePayloadV1> captured = returns;
            controller.ConfigureForTests(
                fixture.Authority,
                fixture.Progression,
                12345UL,
                StableId.Parse("crafting-screen.playmode"),
                StableId.Parse("run.playmode"),
                StableId.Parse("claimant.playmode"),
                delegate(PlayerRouteProfilePayloadV1 payload) { captured.Add(payload); });
            return controller;
        }

        private sealed class ControllerFixture
        {
            private ControllerFixture(
                PlayerRouteProfilePayloadV1 routePayload,
                ProgressionContext progression,
                ControllerFakeAuthority authority)
            {
                RoutePayload = routePayload;
                Progression = progression;
                Authority = authority;
            }

            public PlayerRouteProfilePayloadV1 RoutePayload { get; }
            public ProgressionContext Progression { get; }
            public ControllerFakeAuthority Authority { get; }

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

                CraftingRecipeV1 recipe = new CraftingRecipeV1(
                    1,
                    StableId.Parse("recipe.playmode"),
                    weapon.DefinitionId,
                    StableId.Parse("discovery.playmode"),
                    2,
                    2,
                    3,
                    new CraftingDelayVarianceV1(0, 0),
                    25,
                    CraftingQualityPolicyKindV1.Fixed,
                    new[]
                    {
                        new CraftingWeightedDefinitionV1(quality.QualityId, 1UL),
                    },
                    1,
                    20,
                    0,
                    0,
                    1,
                    1,
                    Array.Empty<CraftingWeightedDefinitionV1>(),
                    new CraftingGeneratorPolicyV1(
                        StableId.Parse("crafting-policy.playmode"),
                        1,
                        new SoftActivationCurveParameters(0.1, 2, 2),
                        new ObsolescenceCurveParameters(2, 4.0, 0.1)));
                CraftingRecipeCatalogV1 recipes = new CraftingRecipeCatalogV1(
                    new[] { recipe });
                ControllerFakeAuthority authority = new ControllerFakeAuthority(
                    balance,
                    recipes,
                    built.Catalog,
                    quality.QualityId);
                ProgressionContext progression = ProgressionContext.Create(
                    10,
                    10,
                    StableId.Parse("difficulty.playmode"),
                    1);
                PlayerRouteProfilePayloadV1 route = PlayerRouteProfilePayloadV1.Create(
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

        private sealed class ControllerFakeAuthority : ICraftingPresentationAuthorityPortV1
        {
            private readonly CraftingRecipeCatalogV1 recipes;
            private readonly EquipmentCatalog equipment;
            private readonly StableId qualityStableId;
            private readonly Dictionary<StableId, EquipmentInstance> applied =
                new Dictionary<StableId, EquipmentInstance>();
            private bool returnedRetry;

            public ControllerFakeAuthority(
                long balance,
                CraftingRecipeCatalogV1 recipes,
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

            public CraftingPresentationAuthoritySnapshotV1 ExportSnapshot()
            {
                return new CraftingPresentationAuthoritySnapshotV1(
                    ScrapBalance,
                    ScrapSequence,
                    HoldingsSequence,
                    recipes,
                    equipment,
                    "playmode|" + ScrapSequence + "|" + HoldingsSequence);
            }

            public CraftingPresentationAuthorityResultV1 Preview(
                CraftEquipmentCommandV1 command)
            {
                return Result(
                    command,
                    CraftingResultStatusV1.Crafted,
                    CreateEquipment(command),
                    string.Empty);
            }

            public CraftingPresentationAuthorityResultV1 Craft(
                CraftEquipmentCommandV1 command)
            {
                CommandFingerprints.Add(command.Fingerprint);
                EquipmentInstance existing;
                if (applied.TryGetValue(command.CraftTransactionStableId, out existing))
                {
                    return Result(
                        command,
                        CraftingResultStatusV1.ExactDuplicateNoChange,
                        existing,
                        string.Empty);
                }

                if (ReturnRetryOnce && !returnedRetry)
                {
                    returnedRetry = true;
                    return Result(
                        command,
                        CraftingResultStatusV1.RewardApplicationRetryRequired,
                        CreateEquipment(command),
                        "pending");
                }

                CraftingRecipeV1 recipe = recipes.Find(command.RecipeStableId);
                EquipmentInstance generated = CreateEquipment(command);
                ScrapBalance -= recipe.ScrapCost;
                ScrapSequence++;
                HoldingsSequence++;
                GrantCount++;
                applied.Add(command.CraftTransactionStableId, generated);
                return Result(
                    command,
                    CraftingResultStatusV1.Crafted,
                    generated,
                    string.Empty);
            }

            private CraftingPresentationAuthorityResultV1 Result(
                CraftEquipmentCommandV1 command,
                CraftingResultStatusV1 status,
                EquipmentInstance generated,
                string rejectionCode)
            {
                CraftingRecipeV1 recipe = recipes.Find(command.RecipeStableId);
                return new CraftingPresentationAuthorityResultV1(
                    status,
                    recipe.RecipeStableId,
                    recipe.ResolveUnlockLevel(command.RootSeed),
                    recipe.ScrapCost,
                    generated,
                    command.Fingerprint,
                    rejectionCode);
            }

            private EquipmentInstance CreateEquipment(CraftEquipmentCommandV1 command)
            {
                CraftingRecipeV1 recipe = recipes.Find(command.RecipeStableId);
                return EquipmentInstance.Create(
                    CraftingCanonicalV1.DeriveStableId(
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
