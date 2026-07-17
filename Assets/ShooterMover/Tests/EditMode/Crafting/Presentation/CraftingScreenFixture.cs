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
        private CraftingScreenFixture(PlayerRouteProfilePayloadV1 route, ProgressionContext progression, FakeCraftingAuthority authority)
        {
            Route = route;
            Progression = progression;
            Authority = authority;
        }

        public PlayerRouteProfilePayloadV1 Route { get; }
        public ProgressionContext Progression { get; }
        public FakeCraftingAuthority Authority { get; }

        public CraftingScreenServiceV1 Service()
        {
            return new CraftingScreenServiceV1(Route, Progression, 991827UL,
                StableId.Parse("crafting-screen.session-1"), StableId.Parse("run.test-1"),
                StableId.Parse("claimant.player-1"), Authority);
        }

        public static CraftingScreenFixture Create(int level, long scrap, bool includeLocked = false)
        {
            EquipmentQualityTier quality = EquipmentQualityTier.Create(StableId.Parse("quality.standard"), "Standard", 1);
            EquipmentDefinition weapon = EquipmentDefinition.Create(
                StableId.Parse("weapon.shared"), EquipmentCategoryIds.Weapon, StableId.Parse("weapon-family.test"),
                "Shared Weapon", StableId.Parse("runtime-weapon.test"), InclusiveIntRange.Create(1, 20), 0,
                new[] { quality }, Array.Empty<StableId>());
            EquipmentCatalogBuildResult equipment = EquipmentCatalog.Build(new[] { weapon }, Array.Empty<AugmentDefinition>());
            Assert.That(equipment.IsValid, Is.True);

            CraftingRecipeV1 available = Recipe("recipe.available", 2, 3, 25);
            CraftingRecipeV1[] recipes = includeLocked
                ? new[] { available, Recipe("recipe.locked", 8, 3, 25) }
                : new[] { available };
            FakeCraftingAuthority authority = new FakeCraftingAuthority(
                scrap, new CraftingRecipeCatalogV1(recipes), equipment.Catalog);
            ProgressionContext progression = ProgressionContext.Create(
                level, level, StableId.Parse("difficulty.test"), 1);
            PlayerRouteProfilePayloadV1 route = PlayerRouteProfilePayloadV1.Create(
                StableId.Parse("character.test"), StableId.Parse("loadout.test"),
                new[] { StableId.Parse("equipment.route-1"), StableId.Parse("equipment.route-2"),
                    StableId.Parse("equipment.route-3"), StableId.Parse("equipment.route-4") });
            return new CraftingScreenFixture(route, progression, authority);
        }

        private static CraftingRecipeV1 Recipe(string id, int natural, int delay, long cost)
        {
            return new CraftingRecipeV1(
                1, StableId.Parse(id), StableId.Parse("weapon.shared"), StableId.Parse("discovery.test-source"),
                natural, natural, delay, new CraftingDelayVarianceV1(0, 0), cost,
                CraftingQualityPolicyKindV1.Fixed,
                new[] { new CraftingWeightedDefinitionV1(StableId.Parse("quality.standard"), 1UL) },
                1, 20, 0, 0, 1, 1, Array.Empty<CraftingWeightedDefinitionV1>(),
                new CraftingGeneratorPolicyV1(StableId.Parse("crafting-policy.test"), 1,
                    new SoftActivationCurveParameters(0.1, 2, 2),
                    new ObsolescenceCurveParameters(2, 4.0, 0.1)));
        }
    }
}
