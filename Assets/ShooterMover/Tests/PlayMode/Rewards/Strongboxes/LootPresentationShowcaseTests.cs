using System;
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
            };
            var combined = new System.Text.StringBuilder();
            for (int index = 0; index < paths.Length; index++)
            {
                combined.AppendLine(File.ReadAllText(Path.Combine(root, paths[index])));
            }
            return combined.ToString();
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
