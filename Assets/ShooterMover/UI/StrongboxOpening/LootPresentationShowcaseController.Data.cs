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
            runHudView = GetOrAddComponent<LootRunHudView>(gameObject);
            ownedGroupsView =
                GetOrAddComponent<OwnedStrongboxGroupsView>(gameObject);
            rewardCardsView =
                GetOrAddComponent<StrongboxRewardCardsView>(gameObject);
            openingPresentationView =
                GetOrAddComponent<StrongboxOpeningPresentationView>(gameObject);

            runHudView.Bind(runTotals);
            gallery = BuildPickupGallery();
            SpawnGallery(gallery);
            ownedGroupsView.Bind(BuildGroups());
            immutableFixtureResult = BuildImmutableFixtureResult();
            openingSession = CreateOpeningSession("strongbox-tier.steel", "Steel");
            openingPresentationView.Bind(openingSession, rewardCardsView);

            LootPickupPresentation fixturePickup;
            string fixtureDiagnostic;
            StrongboxTier steel =
                StrongboxCatalog.GetByNumber(1);
            if (!LootPickupPresentation.TryCreate(
                StableId.Parse("development-pickup.authoritative-steel"),
                StableId.Parse("development-reward.authoritative-steel"),
                RewardGrantKind.Strongbox,
                steel.TierStableId,
                1L,
                out fixturePickup,
                out fixtureDiagnostic))
            {
                throw new InvalidOperationException(fixtureDiagnostic);
            }

            pickupFixture =
                new DevelopmentPickupStateFixture(fixturePickup);
            initialized = true;
            ReconstructPickupFixtureView();
        }

        private IReadOnlyList<LootPickupPresentation> BuildPickupGallery()
        {
            var result = new List<LootPickupPresentation>();
            AddPickup(
                result,
                "credits",
                RewardGrantKind.Money,
                StableId.Parse("currency.money"),
                125L);
            AddPickup(
                result,
                "scrap",
                RewardGrantKind.Scrap,
                StableId.Parse("currency.scrap"),
                18L);
            for (int index = 0;
                 index < StrongboxCatalog.Tiers.Count;
                 index++)
            {
                StrongboxTier tier =
                    StrongboxCatalog.Tiers[index];
                AddPickup(
                    result,
                    "box-" + tier.Slug,
                    RewardGrantKind.Strongbox,
                    tier.TierStableId,
                    1L);
            }
            return new ReadOnlyCollection<LootPickupPresentation>(result);
        }

        private static void AddPickup(
            ICollection<LootPickupPresentation> result,
            string suffix,
            RewardGrantKind kind,
            StableId contentStableId,
            long quantity)
        {
            LootPickupPresentation pickup;
            string diagnostic;
            if (!LootPickupPresentation.TryCreate(
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
            IReadOnlyList<LootPickupPresentation> pickups)
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

        private LootVisual CreateVisual(
            LootPickupPresentation pickup,
            Vector3 position,
            string objectName)
        {
            GameObject instance = new GameObject(objectName);
            instance.transform.SetParent(transform, false);
            instance.transform.position = position;
            LootVisual visual =
                instance.AddComponent<LootVisual>();
            visual.Bind(pickup);
            return visual;
        }

        private static IReadOnlyList<OwnedStrongboxGroupPresentation>
            BuildGroups()
        {
            var exactInstances =
                new List<OwnedStrongboxInstancePresentation>();
            for (int tierIndex = 0;
                 tierIndex < StrongboxCatalog.Tiers.Count;
                 tierIndex++)
            {
                StrongboxTier tier =
                    StrongboxCatalog.Tiers[tierIndex];
                int quantity = tier.TierNumber == 1 ? 10 : 2;
                for (int instanceIndex = 1;
                     instanceIndex <= quantity;
                     instanceIndex++)
                {
                    OwnedStrongboxInstancePresentation instance;
                    string diagnostic;
                    if (!OwnedStrongboxInstancePresentation.TryCreate(
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

            IReadOnlyList<OwnedStrongboxGroupPresentation> projected;
            string projectionDiagnostic;
            if (!StrongboxGroupingProjector.TryProject(
                exactInstances,
                out projected,
                out projectionDiagnostic))
            {
                throw new InvalidOperationException(projectionDiagnostic);
            }
            return projected;
        }

        private static StrongboxOpeningPresentationResult
            BuildImmutableFixtureResult()
        {
            return StrongboxOpeningPresentationResult.Success(
                new[]
                {
                    new StrongboxRewardRevealItem(
                        StrongboxRewardPresentationKind.Money,
                        "CREDITS",
                        "currency.money",
                        null,
                        275L,
                        "Immutable fixture value"),
                    new StrongboxRewardRevealItem(
                        StrongboxRewardPresentationKind.Scrap,
                        "SCRAP",
                        "currency.scrap",
                        null,
                        48L,
                        "Immutable fixture value"),
                    new StrongboxRewardRevealItem(
                        StrongboxRewardPresentationKind.Equipment,
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

        private StrongboxOpeningSceneSession CreateOpeningSession(
            string tierId,
            string tierLabel)
        {
            var configuration =
                new StrongboxOpeningPreviewConfiguration(
                    tierId,
                    tierLabel,
                    9001001UL,
                    Mathf.Max(0.05f, openingDurationSeconds),
                    Mathf.Max(0.05f, revealIntervalSeconds),
                    0.35f);
            return new StrongboxOpeningSceneSession(
                configuration,
                delegate { return immutableFixtureResult; });
        }
    }
}
