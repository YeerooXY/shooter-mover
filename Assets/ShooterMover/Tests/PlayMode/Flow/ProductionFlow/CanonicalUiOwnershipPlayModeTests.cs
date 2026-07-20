using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.UI.ProductionFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Flow.ProductionFlow
{
    public sealed class CanonicalUiOwnershipPlayModeTests
    {
        private const string LegacyMenuNamespace =
            "ShooterMover.UI.MainMenu.";

        private static readonly HashSet<string> CanonicalOwnerTypeNames =
            new HashSet<string>
            {
                "ShooterMover.UI.ProductionFlow.ProductionMainMenuControllerV1",
                "ShooterMover.UI.ProductionFlow.ProductionCharacterSelectionControllerV1",
                "ShooterMover.UI.Hub.HubFlowControllerV1",
                "ShooterMover.UI.PlaySelection.PlaySelectionControllerV1",
                "ShooterMover.UI.LevelSelection.LevelSelectionControllerV1",
                "ShooterMover.UI.InventoryLoadout.InventoryLoadoutScreenControllerV1",
                "ShooterMover.UI.Skills.SkillsSceneController",
                "ShooterMover.UI.Shop.ShopScreenControllerV1",
                "ShooterMover.UI.Crafting.CraftingScreenControllerV1",
                "ShooterMover.UI.ProductionFlow.ProductionResultsControllerV1",
                "ShooterMover.UI.StrongboxOpening.StrongboxOpeningController",
            };

        [UnityTest]
        public IEnumerator CanonicalScenesOwnExactlyOneLayout()
        {
            yield return EnsureCoordinator();

            SceneExpectation[] expectations =
            {
                new SceneExpectation(
                    ProductionFlowScenePathsV1.MainMenu,
                    "ShooterMover.UI.ProductionFlow.ProductionMainMenuControllerV1"),
                new SceneExpectation(
                    ProductionFlowScenePathsV1.CharacterSelection,
                    "ShooterMover.UI.ProductionFlow.ProductionCharacterSelectionControllerV1"),
                new SceneExpectation(
                    ProductionFlowScenePathsV1.Hub,
                    "ShooterMover.UI.Hub.HubFlowControllerV1"),
                new SceneExpectation(
                    ProductionFlowScenePathsV1.PlaySelection,
                    "ShooterMover.UI.PlaySelection.PlaySelectionControllerV1"),
                new SceneExpectation(
                    ProductionFlowScenePathsV1.LevelSelection,
                    "ShooterMover.UI.LevelSelection.LevelSelectionControllerV1"),
                new SceneExpectation(
                    ProductionFlowScenePathsV1.Inventory,
                    "ShooterMover.UI.InventoryLoadout.InventoryLoadoutScreenControllerV1"),
                new SceneExpectation(
                    ProductionFlowScenePathsV1.Skills,
                    "ShooterMover.UI.Skills.SkillsSceneController"),
                new SceneExpectation(
                    ProductionFlowScenePathsV1.Shop,
                    "ShooterMover.UI.Shop.ShopScreenControllerV1"),
                new SceneExpectation(
                    ProductionFlowScenePathsV1.Crafting,
                    "ShooterMover.UI.Crafting.CraftingScreenControllerV1"),
                new SceneExpectation(
                    ProductionFlowScenePathsV1.Results,
                    "ShooterMover.UI.ProductionFlow.ProductionResultsControllerV1"),
                new SceneExpectation(
                    ProductionFlowScenePathsV1.StrongboxOpening,
                    "ShooterMover.UI.StrongboxOpening.StrongboxOpeningController"),
            };

            for (int index = 0; index < expectations.Length; index++)
            {
                SceneExpectation expectation = expectations[index];
                yield return Load(expectation.ScenePath);
                AssertSingleLayout(expectation);
            }
        }

        private static void AssertSingleLayout(SceneExpectation expectation)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            Assert.That(
                activeScene.path,
                Is.EqualTo(expectation.ScenePath),
                expectation.ScenePath);
            Assert.That(
                SceneManager.sceneCount,
                Is.EqualTo(1),
                expectation.ScenePath + " must replace the prior screen instead of stacking scenes.");
            Assert.That(
                Camera.allCamerasCount,
                Is.EqualTo(1),
                expectation.ScenePath + " must expose one active UI camera.");

            int expectedOwnerCount = 0;
            int canonicalOwnerCount = 0;
            int legacyOwnerCount = 0;
            GameObject[] roots = activeScene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours =
                    roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int behaviourIndex = 0;
                    behaviourIndex < behaviours.Length;
                    behaviourIndex++)
                {
                    MonoBehaviour behaviour = behaviours[behaviourIndex];
                    if (behaviour == null) continue;

                    string typeName = behaviour.GetType().FullName ?? string.Empty;
                    if (string.Equals(
                        typeName,
                        expectation.OwnerTypeName,
                        System.StringComparison.Ordinal))
                    {
                        expectedOwnerCount++;
                    }
                    if (CanonicalOwnerTypeNames.Contains(typeName))
                    {
                        canonicalOwnerCount++;
                    }
                    if (typeName.StartsWith(
                        LegacyMenuNamespace,
                        System.StringComparison.Ordinal))
                    {
                        legacyOwnerCount++;
                    }
                }
            }

            Assert.That(
                expectedOwnerCount,
                Is.EqualTo(1),
                expectation.ScenePath + " must contain its one expected screen owner.");
            Assert.That(
                canonicalOwnerCount,
                Is.EqualTo(1),
                expectation.ScenePath + " must not render another canonical screen layout simultaneously.");
            Assert.That(
                legacyOwnerCount,
                Is.Zero,
                expectation.ScenePath + " must not contain the retired embedded Main Menu UI shell.");
        }

        private static IEnumerator EnsureCoordinator()
        {
            if (ProductionFlowCoordinatorV1.HasInstance) yield break;

            AsyncOperation load = SceneManager.LoadSceneAsync(
                ProductionFlowScenePathsV1.Bootstrap,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;

            int remainingFrames = 180;
            while (remainingFrames-- > 0
                && !ProductionFlowCoordinatorV1.HasInstance)
            {
                yield return null;
            }

            Assert.That(ProductionFlowCoordinatorV1.HasInstance, Is.True);
        }

        private static IEnumerator Load(string scenePath)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                scenePath,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, scenePath);
            while (!load.isDone) yield return null;
            yield return null;
        }

        private sealed class SceneExpectation
        {
            public SceneExpectation(string scenePath, string ownerTypeName)
            {
                ScenePath = scenePath;
                OwnerTypeName = ownerTypeName;
            }

            public string ScenePath { get; }

            public string OwnerTypeName { get; }
        }
    }
}
