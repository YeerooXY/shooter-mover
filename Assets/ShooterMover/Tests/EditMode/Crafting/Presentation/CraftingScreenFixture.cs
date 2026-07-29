using System;
using NUnit.Framework;
using ShooterMover.Application.Crafting.Presentation;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Crafting;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;

namespace ShooterMover.Tests.EditMode.Crafting.Presentation
{
    internal sealed class CraftingScreenFixture
    {
        private CraftingScreenFixture(PlayerRouteProfilePayload route, ProgressionContext progression, FakeCraftingState authority)
        {
            Route = route;
            Progression = progression;
            Authority = authority;
        }

        public PlayerRouteProfilePayload Route { get; }
        public ProgressionContext Progression { get; }
        public FakeCraftingState Authority { get; }

        public CraftingScreenActions Service()
        {
            return new CraftingScreenActions(Route, Progression, 991827UL,
                StableId.Parse("crafting-screen.session-1"), StableId.Parse("run.test-1"),
                StableId.Parse("claimant.player-1"), Authority);
        }

        public static CraftingScreenFixture Create(int level, long scrap, bool includeLocked = false)
        {
            EquipmentQualityTier quality = EquipmentQualityTier.Create(StableId.Parse("quality.standard"), "Standard", 1);
            EquipmentDefinition weapon = EquipmentDefinition.Create(
                StableId.Parse("weapon.shared"), EquipmentCategoryIds.Weapon, StableId.Parse("weapon-family.test"),
                "Shared Weapon", StableId.Parse("weapon.runtime-test"), InclusiveIntRange.Create(1, 20), 0,
                new[] { quality }, Array.Empty<StableId>());
            EquipmentCatalogBuildResult equipment = EquipmentCatalog.Build(new[] { weapon }, Array.Empty<AugmentDefinition>());
            Assert.That(equipment.IsValid, Is.True);

            CraftingRecipe available = Recipe("recipe.available", 2, 3, 25);
            CraftingRecipe[] recipes = includeLocked
                ? new[] { available, Recipe("recipe.locked", 8, 3, 25) }
                : new[] { available };
            FakeCraftingState authority = new FakeCraftingState(
                scrap, new CraftingRecipeCatalog(recipes), equipment.Catalog);
            ProgressionContext progression = ProgressionContext.Create(
                level, level, StableId.Parse("difficulty.test"), 1);
            PlayerRouteProfilePayload route = PlayerRouteProfilePayload.Create(
                StableId.Parse("character.test"), StableId.Parse("loadout.test"),
                new[] { StableId.Parse("equipment.route-1"), StableId.Parse("equipment.route-2"),
                    StableId.Parse("equipment.route-3"), StableId.Parse("equipment.route-4") });
            return new CraftingScreenFixture(route, progression, authority);
        }

        private static CraftingRecipe Recipe(string id, int natural, int delay, long cost)
        {
            return new CraftingRecipe(
                1, StableId.Parse(id), StableId.Parse("weapon.shared"), StableId.Parse("discovery.test-source"),
                natural, natural, delay, new CraftingDelayVariance(0, 0), cost,
                CraftingQualityPolicyKind.Fixed,
                new[] { new CraftingWeightedDefinition(StableId.Parse("quality.standard"), 1UL) },
                1, 20, 0, 0, 1, 1, Array.Empty<CraftingWeightedDefinition>(),
                new CraftingGeneratorPolicy(StableId.Parse("crafting-policy.test"), 1,
                    new SoftActivationCurveParameters(0.1, 2, 2),
                    new ObsolescenceCurveParameters(2, 4.0, 0.1)));
        }
    }
}
