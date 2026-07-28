using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using UnityEngine;

namespace ShooterMover.UI.StrongboxOpening
{
    public sealed partial class LootPresentationShowcaseController
    {
        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            EnsureCamera();
            runHudView = GetOrAddComponent<LootRunHudViewV1>(gameObject);
            ownedGroupsView =
                GetOrAddComponent<OwnedStrongboxGroupsViewV1>(gameObject);
            rewardCardsView =
                GetOrAddComponent<StrongboxRewardCardsViewV1>(gameObject);
            openingPresentationView =
                GetOrAddComponent<StrongboxOpeningPresentationViewV1>(gameObject);

            runHudView.Bind(runTotals);
            gallery = BuildPickupGallery();
            SpawnGallery(gallery);
            ownedGroupsView.Bind(BuildGroups());
            immutableFixtureResult = BuildImmutableFixtureResult();
            openingSession = CreateOpeningSession("strongbox-tier.steel", "Steel");
            openingPresentationView.Bind(openingSession, rewardCardsView);

            LootPickupPresentationV1 fixturePickup;
            string fixtureDiagnostic;
            ProductionStrongboxTierV1 steel =
                ProductionStrongboxCatalogV1.GetByNumber(1);
            if (!LootPickupPresentationV1.TryCreate(
                StableId.Parse("development-pickup.authoritative-steel"),
                StableId.Parse("development-reward.authoritative-steel"),
                RewardGrantKindV1.Strongbox,
                steel.TierStableId,
                1L,
                out fixturePickup,
                out fixtureDiagnostic))
            {
                throw new InvalidOperationException(fixtureDiagnostic);
            }

            pickupFixture =
                new DevelopmentPickupAuthorityFixtureV1(fixturePickup);
            initialized = true;
            ReconstructPickupFixtureView();
        }

        private IReadOnlyList<LootPickupPresentationV1> BuildPickupGallery()
        {
            var result = new List<LootPickupPresentationV1>();
            AddPickup(
                result,
                "credits",
                RewardGrantKindV1.Money,
                StableId.Parse("currency.money"),
                125L);
            AddPickup(
                result,
                "scrap",
                RewardGrantKindV1.Scrap,
                StableId.Parse("currency.scrap"),
                18L);
            for (int index = 0;
                 index < ProductionStrongboxCatalogV1.Tiers.Count;
                 index++)
            {
                ProductionStrongboxTierV1 tier =
                    ProductionStrongboxCatalogV1.Tiers[index];
                AddPickup(
                    result,
                    "box-" + tier.Slug,
                    RewardGrantKindV1.Strongbox,
                    tier.TierStableId,
                    1L);
            }
            return new ReadOnlyCollection<LootPickupPresentationV1>(result);
        }

        private static void AddPickup(
            ICollection<LootPickupPresentationV1> result,
            string suffix,
            RewardGrantKindV1 kind,
            StableId contentStableId,
            long quantity)
        {
            LootPickupPresentationV1 pickup;
            string diagnostic;
            if (!LootPickupPresentationV1.TryCreate(
                StableId.Create("development-pickup", suffix),
                StableId.Create("development-reward", suffix),
                kind,
                contentStableId,
                quantity,
                out pickup,
                out diagnostic))
            {
                throw new InvalidOperationException(diagnostic);
            }
            result.Add(pickup);
        }

        private void SpawnGallery(
            IReadOnlyList<LootPickupPresentationV1> pickups)
        {
            const int columns = 7;
            for (int index = 0; index < pickups.Count; index++)
            {
                int row = index / columns;
                int column = index % columns;
                Vector3 position = new Vector3(
                    -6.6f + column * 2.2f,
                    3.5f - row * 2.3f,
                    0f);
                galleryViews.Add(
                    CreateVisual(
                        pickups[index],
                        position,
                        "LootGallery_" + index));
            }
        }

        private LootPickupVisual2D CreateVisual(
            LootPickupPresentationV1 pickup,
            Vector3 position,
            string objectName)
        {
            GameObject instance = new GameObject(objectName);
            instance.transform.SetParent(transform, false);
            instance.transform.position = position;
            LootPickupVisual2D visual =
                instance.AddComponent<LootPickupVisual2D>();
            visual.Bind(pickup);
            return visual;
        }

        private static IReadOnlyList<OwnedStrongboxGroupPresentationV1>
            BuildGroups()
        {
            var exactInstances =
                new List<OwnedStrongboxInstancePresentationV1>();
            for (int tierIndex = 0;
                 tierIndex < ProductionStrongboxCatalogV1.Tiers.Count;
                 tierIndex++)
            {
                ProductionStrongboxTierV1 tier =
                    ProductionStrongboxCatalogV1.Tiers[tierIndex];
                int quantity = tier.TierNumber == 1 ? 10 : 2;
                for (int instanceIndex = 1;
                     instanceIndex <= quantity;
                     instanceIndex++)
                {
                    OwnedStrongboxInstancePresentationV1 instance;
                    string diagnostic;
                    if (!OwnedStrongboxInstancePresentationV1.TryCreate(
                        StableId.Create(
                            "development-strongbox",
                            tier.Slug
                            + "-"
                            + instanceIndex.ToString(
                                "00",
                                CultureInfo.InvariantCulture)),
                        tier.TierStableId,
                        out instance,
                        out diagnostic))
                    {
                        throw new InvalidOperationException(diagnostic);
                    }
                    exactInstances.Add(instance);
                }
            }

            IReadOnlyList<OwnedStrongboxGroupPresentationV1> projected;
            string projectionDiagnostic;
            if (!StrongboxGroupingProjectorV1.TryProject(
                exactInstances,
                out projected,
                out projectionDiagnostic))
            {
                throw new InvalidOperationException(projectionDiagnostic);
            }
            return projected;
        }

        private static StrongboxOpeningPresentationResultV1
            BuildImmutableFixtureResult()
        {
            return StrongboxOpeningPresentationResultV1.Success(
                new[]
                {
                    new StrongboxRewardRevealItemV1(
                        StrongboxRewardPresentationKindV1.Money,
                        "CREDITS",
                        "currency.money",
                        null,
                        275L,
                        "Immutable fixture value"),
                    new StrongboxRewardRevealItemV1(
                        StrongboxRewardPresentationKindV1.Scrap,
                        "SCRAP",
                        "currency.scrap",
                        null,
                        48L,
                        "Immutable fixture value"),
                    new StrongboxRewardRevealItemV1(
                        StrongboxRewardPresentationKindV1.Equipment,
                        "Arc Blaster",
                        "equipment.arc-blaster",
                        "development-equipment.arc-blaster-0001",
                        1L,
                        "Item level 12 | Quality Rare | 0 installed augments"),
                },
                false,
                true,
                "IMMUTABLE DEVELOPMENT RESULT");
        }

        private StrongboxOpeningSceneSessionV1 CreateOpeningSession(
            string tierId,
            string tierLabel)
        {
            var configuration =
                new StrongboxOpeningPreviewConfigurationV1(
                    tierId,
                    tierLabel,
                    9001001UL,
                    Mathf.Max(0.05f, openingDurationSeconds),
                    Mathf.Max(0.05f, revealIntervalSeconds),
                    0.35f);
            return new StrongboxOpeningSceneSessionV1(
                configuration,
                delegate { return immutableFixtureResult; });
        }
    }
}
