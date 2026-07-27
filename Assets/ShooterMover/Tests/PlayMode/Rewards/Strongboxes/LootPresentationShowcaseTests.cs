using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UI.StrongboxOpening;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Rewards.Strongboxes
{
    public sealed partial class LootPresentationTests
    {
        [Test]
        public void ShowcaseSourceContainsNoProductionMutationAuthority()
        {
            string sourcePath = Path.Combine(
                UnityEngine.Application.dataPath,
                "ShooterMover/UI/StrongboxOpening/LootPresentationShowcaseController.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.DoesNotContain("StrongboxOpeningServiceV1", source);
            StringAssert.DoesNotContain("RewardApplicationServiceV1", source);
            StringAssert.DoesNotContain("PlayerHoldings", source);
            StringAssert.DoesNotContain("ProductionGameSave", source);
            StringAssert.Contains("DevelopmentPickupAuthorityFixtureV1", source);
            StringAssert.Contains("immutableFixtureResult", source);
        }

        [Test]
        public void ShowcaseSceneBindsOnlyTheDevelopmentPresentationController()
        {
            string scenePath = Path.Combine(
                UnityEngine.Application.dataPath,
                "ShooterMover/Scenes/LootPresentation/LootPresentationShowcase.unity");
            string scene = File.ReadAllText(scenePath);

            StringAssert.Contains("m_Name: LootPresentationShowcase", scene);
            StringAssert.Contains("guid: 7a2fbd85f7c84e7e8e277aee8197b3f4", scene);
            StringAssert.DoesNotContain("ProductionCharacterStrongboxCompositionV1", scene);
            StringAssert.DoesNotContain("ProductionPlayableLevelControllerV1", scene);
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
                LootPickupVisual2D visual = gameObject.AddComponent<LootPickupVisual2D>();
                visual.Bind(Pickup("first"));
                Assert.Throws<InvalidOperationException>(delegate { visual.Bind(Pickup("second")); });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
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
                    ProductionStrongboxCatalogV1.GetByNumber(1).TierStableId,
                    1L,
                    out pickup,
                    out diagnostic),
                Is.True,
                diagnostic);
            return pickup;
        }
    }
}
