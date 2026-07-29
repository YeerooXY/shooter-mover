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

            StringAssert.DoesNotContain("StrongboxOpeningActions", source);
            StringAssert.DoesNotContain("RewardApplicationActions", source);
            StringAssert.DoesNotContain("PlayerHoldings", source);
            StringAssert.DoesNotContain("ProductionGameSave", source);
            StringAssert.DoesNotContain("RunLocalPickupState", source);
            StringAssert.Contains("DevelopmentPickupStateFixture", source);
            StringAssert.Contains("immutableFixtureResult", source);
            StringAssert.Contains(
                "private readonly RunLootTotalsPresentation runTotals",
                source);
            StringAssert.Contains("LootRunHudView", source);
            StringAssert.Contains("OwnedStrongboxGroupsView", source);
            StringAssert.Contains("StrongboxOpeningPresentationView", source);
            StringAssert.Contains("StrongboxRewardCardsView", source);
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
                "CharacterStrongboxSetup",
                scene);
            StringAssert.DoesNotContain(
                "LevelGame",
                scene);
        }

        [Test]
        public void DevelopmentPickupFixtureRetainsRejectedPickupAndAcceptsExactlyOnce()
        {
            LootPickupPresentation pickup = Pickup("fixture");
            var fixture = new DevelopmentPickupStateFixture(pickup);
            fixture.RejectNextCollection();

            DevelopmentPickupCollectionResult rejected = fixture.Collect();
            Assert.That(rejected.Accepted, Is.False);
            Assert.That(fixture.ExportAvailable(), Is.SameAs(pickup));

            DevelopmentPickupCollectionResult accepted = fixture.Collect();
            Assert.That(accepted.Accepted, Is.True);
            Assert.That(accepted.ExactReplay, Is.False);
            Assert.That(fixture.ExportAvailable(), Is.Null);

            DevelopmentPickupCollectionResult replay = fixture.Collect();
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
                LootVisual visual =
                    gameObject.AddComponent<LootVisual>();
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
                OwnedStrongboxGroupsView view =
                    gameObject.AddComponent<OwnedStrongboxGroupsView>();
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
                StrongboxOpeningPresentationResult frozen =
                    StrongboxOpeningPresentationResult.Success(
                        new[]
                        {
                            new StrongboxRewardRevealItem(
                                StrongboxRewardPresentationKind.Money,
                                "CREDITS",
                                "currency.money",
                                null,
                                25L,
                                string.Empty),
                        },
                        false,
                        true,
                        "FROZEN");
                var session = new StrongboxOpeningSceneSession(
                    new StrongboxOpeningPreviewConfiguration(
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
                StrongboxRewardCardsView cards =
                    gameObject.AddComponent<StrongboxRewardCardsView>();
                StrongboxOpeningPresentationView opening =
                    gameObject.AddComponent<StrongboxOpeningPresentationView>();
                opening.Bind(session, cards);

                Assert.That(session.RequestOpen(), Is.True);
                Assert.That(
                    StrongboxPresentationPlayback.SkipToComplete(session),
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
                var totals = new RunLootTotalsPresentation(12L, 7L, 3L);
                LootRunHudView view =
                    gameObject.AddComponent<LootRunHudView>();
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
                "LootPresentationDevelopmentPickupFixture.cs",
                "LootVisual.cs",
                "LootRunHudView.cs",
                "OwnedStrongboxGroupsView.cs",
                "StrongboxRewardCardsView.cs",
                "StrongboxOpeningPresentationView.cs",
            };
            var combined = new System.Text.StringBuilder();
            for (int index = 0; index < paths.Length; index++)
            {
                combined.AppendLine(
                    File.ReadAllText(Path.Combine(root, paths[index])));
            }
            return combined.ToString();
        }

        private static IReadOnlyList<OwnedStrongboxGroupPresentation>
            GroupWithCount(int count)
        {
            StrongboxTier steel =
                StrongboxCatalog.GetByNumber(1);
            var instances =
                new List<OwnedStrongboxInstancePresentation>();
            for (int index = 1; index <= count; index++)
            {
                OwnedStrongboxInstancePresentation instance;
                string diagnostic;
                Assert.That(
                    OwnedStrongboxInstancePresentation.TryCreate(
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

            IReadOnlyList<OwnedStrongboxGroupPresentation> groups;
            string projectionDiagnostic;
            Assert.That(
                StrongboxGroupingProjector.TryProject(
                    instances,
                    out groups,
                    out projectionDiagnostic),
                Is.True,
                projectionDiagnostic);
            return groups;
        }

        private static LootPickupPresentation Pickup(string suffix)
        {
            LootPickupPresentation pickup;
            string diagnostic;
            Assert.That(
                LootPickupPresentation.TryCreate(
                    StableId.Create("development-pickup", suffix),
                    StableId.Create("development-reward", suffix),
                    RewardGrantKind.Strongbox,
                    StrongboxCatalog
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
