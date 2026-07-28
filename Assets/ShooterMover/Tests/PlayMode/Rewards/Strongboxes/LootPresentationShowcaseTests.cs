using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UI.StrongboxOpening;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Rewards.Strongboxes
{
    public sealed class LootPresentationShowcaseTests
    {
        [Test]
        public void ShowcaseSourcesContainNoProductionMutationAuthority()
        {
            string source = ReadShowcaseSources();

            StringAssert.DoesNotContain("StrongboxOpeningServiceV1", source);
            StringAssert.DoesNotContain("RewardApplicationServiceV1", source);
            StringAssert.DoesNotContain("PlayerHoldings", source);
            StringAssert.DoesNotContain("ProductionGameSave", source);
            StringAssert.DoesNotContain("RunLocalPickupAuthorityV1", source);
            StringAssert.Contains("DevelopmentPickupAuthorityFixtureV1", source);
            StringAssert.Contains("immutableFixtureResult", source);
            StringAssert.Contains(
                "private readonly RunLootTotalsPresentationV1 runTotals",
                source);
            StringAssert.Contains("LootRunHudViewV1", source);
            StringAssert.Contains("OwnedStrongboxGroupsViewV1", source);
            StringAssert.Contains("StrongboxOpeningPresentationViewV1", source);
            StringAssert.Contains("StrongboxRewardCardsViewV1", source);
        }

        [Test]
        public void ShowcaseSceneBindsOnlyTheDevelopmentPresentationController()
        {
            string scenePath = Path.Combine(
                UnityEngine.Application.dataPath,
                "ShooterMover/Scenes/LootPresentation/LootPresentationShowcase.unity");
            string scene = File.ReadAllText(scenePath);

            StringAssert.Contains("m_Name: LootPresentationShowcase", scene);
            StringAssert.Contains(
                "guid: 7a2fbd85f7c84e7e8e277aee8197b3f4",
                scene);
            StringAssert.DoesNotContain(
                "ProductionCharacterStrongboxCompositionV1",
                scene);
            StringAssert.DoesNotContain(
                "ProductionPlayableLevelControllerV1",
                scene);
        }

        [Test]
        public void DevelopmentPickupFixtureRetainsRejectedPickupAndAcceptsExactlyOnce()
        {
            LootPickupPresentationV1 pickup = Pickup("fixture");
            var fixture = new DevelopmentPickupAuthorityFixtureV1(pickup);
            fixture.RejectNextCollection();

            DevelopmentPickupCollectionResultV1 rejected = fixture.Collect();
            Assert.That(rejected.Accepted, Is.False);
            Assert.That(fixture.ExportAvailable(), Is.SameAs(pickup));

            DevelopmentPickupCollectionResultV1 accepted = fixture.Collect();
            Assert.That(accepted.Accepted, Is.True);
            Assert.That(accepted.ExactReplay, Is.False);
            Assert.That(fixture.ExportAvailable(), Is.Null);

            DevelopmentPickupCollectionResultV1 replay = fixture.Collect();
            Assert.That(replay.Accepted, Is.True);
            Assert.That(replay.ExactReplay, Is.True);
            Assert.That(fixture.ExportAvailable(), Is.Null);
        }

        [Test]
        public void PhysicalVisualRejectsRebindingToAnotherExactPickup()
        {
            GameObject gameObject = new GameObject("LootPickupVisualTest");
            try
            {
                LootPickupVisual2D visual =
                    gameObject.AddComponent<LootPickupVisual2D>();
                visual.Bind(Pickup("first"));
                Assert.Throws<InvalidOperationException>(
                    delegate { visual.Bind(Pickup("second")); });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void BoundGroupsViewFailsClosedWhenOpenFiveHasOnlyTwoExactInstances()
        {
            GameObject gameObject = new GameObject("OwnedStrongboxGroupsViewTest");
            try
            {
                OwnedStrongboxGroupsViewV1 view =
                    gameObject.AddComponent<OwnedStrongboxGroupsViewV1>();
                view.Bind(GroupWithCount(2));

                IReadOnlyList<StableId> batch;
                string diagnostic;
                Assert.That(
                    view.TryResolveBatchExact(5, out batch, out diagnostic),
                    Is.False);
                Assert.That(batch, Is.Empty);
                Assert.That(diagnostic, Does.Contain("insufficient"));
                Assert.That(view.Groups[0].Quantity, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void BoundOpeningViewReadsOneImmutableResultWithoutSecondInvocation()
        {
            GameObject gameObject = new GameObject("StrongboxOpeningPresentationViewTest");
            try
            {
                int calls = 0;
                StrongboxOpeningPresentationResultV1 frozen =
                    StrongboxOpeningPresentationResultV1.Success(
                        new[]
                        {
                            new StrongboxRewardRevealItemV1(
                                StrongboxRewardPresentationKindV1.Money,
                                "CREDITS",
                                "currency.money",
                                null,
                                25L,
                                string.Empty),
                        },
                        false,
                        true,
                        "FROZEN");
                var session = new StrongboxOpeningSceneSessionV1(
                    new StrongboxOpeningPreviewConfigurationV1(
                        "strongbox-tier.steel",
                        "Steel",
                        100UL,
                        0.1f,
                        0.1f,
                        0f),
                    delegate
                    {
                        calls++;
                        return frozen;
                    });
                StrongboxRewardCardsViewV1 cards =
                    gameObject.AddComponent<StrongboxRewardCardsViewV1>();
                StrongboxOpeningPresentationViewV1 opening =
                    gameObject.AddComponent<StrongboxOpeningPresentationViewV1>();
                opening.Bind(session, cards);

                Assert.That(session.RequestOpen(), Is.True);
                Assert.That(
                    StrongboxPresentationPlaybackV1.SkipToComplete(session),
                    Is.True);
                opening.Synchronize();

                Assert.That(opening.Session, Is.SameAs(session));
                Assert.That(cards.Result, Is.SameAs(frozen));
                Assert.That(cards.VisibleRewardCount, Is.EqualTo(1));
                Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void BoundRunHudRetainsSuppliedImmutableProjection()
        {
            GameObject gameObject = new GameObject("LootRunHudViewTest");
            try
            {
                var totals = new RunLootTotalsPresentationV1(12L, 7L, 3L);
                LootRunHudViewV1 view =
                    gameObject.AddComponent<LootRunHudViewV1>();
                view.Bind(totals);

                Assert.That(view.Projection, Is.SameAs(totals));
                Assert.That(view.Projection.Credits, Is.EqualTo(12L));
                Assert.That(view.Projection.Scrap, Is.EqualTo(7L));
                Assert.That(view.Projection.Strongboxes, Is.EqualTo(3L));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static string ReadShowcaseSources()
        {
            string root = Path.Combine(
                UnityEngine.Application.dataPath,
                "ShooterMover/UI/StrongboxOpening");
            string[] paths =
            {
                "LootPresentationShowcaseController.cs",
                "LootPresentationShowcaseController.Data.cs",
                "LootPresentationShowcaseController.GUI.cs",
                "LootPresentationDevelopmentPickupFixtureV1.cs",
                "LootPickupVisual2D.cs",
                "LootRunHudViewV1.cs",
                "OwnedStrongboxGroupsViewV1.cs",
                "StrongboxRewardCardsViewV1.cs",
                "StrongboxOpeningPresentationViewV1.cs",
            };
            var combined = new System.Text.StringBuilder();
            for (int index = 0; index < paths.Length; index++)
            {
                combined.AppendLine(
                    File.ReadAllText(Path.Combine(root, paths[index])));
            }
            return combined.ToString();
        }

        private static IReadOnlyList<OwnedStrongboxGroupPresentationV1>
            GroupWithCount(int count)
        {
            ProductionStrongboxTierV1 steel =
                ProductionStrongboxCatalogV1.GetByNumber(1);
            var instances =
                new List<OwnedStrongboxInstancePresentationV1>();
            for (int index = 1; index <= count; index++)
            {
                OwnedStrongboxInstancePresentationV1 instance;
                string diagnostic;
                Assert.That(
                    OwnedStrongboxInstancePresentationV1.TryCreate(
                        StableId.Create(
                            "development-strongbox",
                            "bound-view-" + index),
                        steel.TierStableId,
                        out instance,
                        out diagnostic),
                    Is.True,
                    diagnostic);
                instances.Add(instance);
            }

            IReadOnlyList<OwnedStrongboxGroupPresentationV1> groups;
            string projectionDiagnostic;
            Assert.That(
                StrongboxGroupingProjectorV1.TryProject(
                    instances,
                    out groups,
                    out projectionDiagnostic),
                Is.True,
                projectionDiagnostic);
            return groups;
        }

        private static LootPickupPresentationV1 Pickup(string suffix)
        {
            LootPickupPresentationV1 pickup;
            string diagnostic;
            Assert.That(
                LootPickupPresentationV1.TryCreate(
                    StableId.Create("development-pickup", suffix),
                    StableId.Create("development-reward", suffix),
                    RewardGrantKindV1.Strongbox,
                    ProductionStrongboxCatalogV1
                        .GetByNumber(1)
                        .TierStableId,
                    1L,
                    out pickup,
                    out diagnostic),
                Is.True,
                diagnostic);
            return pickup;
        }
    }
}
