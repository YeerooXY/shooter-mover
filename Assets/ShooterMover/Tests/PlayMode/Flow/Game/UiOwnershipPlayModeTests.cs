using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.UI.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Flow.Game
{
    public sealed class UiOwnershipPlayModeTests
    {
        private const string LegacyMenuNamespace =
            "ShooterMover.UI.MainMenu.";

        private static readonly HashSet<string> CanonicalOwnerTypeNames =
            new HashSet<string>
            {
                "ShooterMover.UI.Game.MainMenu",
                "ShooterMover.UI.Game.CharacterMenu",
                "ShooterMover.UI.Hub.Hub",
                "ShooterMover.UI.PlaySelection.PlayMenu",
                "ShooterMover.UI.LevelSelection.LevelMenu",
                "ShooterMover.UI.InventoryLoadout.InventoryMenu",
                "ShooterMover.UI.Skills.SkillsMenu",
                "ShooterMover.UI.Shop.ShopMenu",
                "ShooterMover.UI.Crafting.CraftingMenu",
                "ShooterMover.UI.Game.Results",
                "ShooterMover.UI.StrongboxOpening.StrongboxOpening",
            };

        [UnityTest]
        public IEnumerator CanonicalScenesOwnExactlyOneLayout()
        {
            yield return EnsureCoordinator();

            SceneExpectation[] expectations =
            {
                new SceneExpectation(
                    FlowScenePaths.MainMenu,
                    "ShooterMover.UI.Game.MainMenu"),
                new SceneExpectation(
                    FlowScenePaths.CharacterSelection,
                    "ShooterMover.UI.Game.CharacterMenu"),
                new SceneExpectation(
                    FlowScenePaths.Hub,
                    "ShooterMover.UI.Hub.Hub"),
                new SceneExpectation(
                    FlowScenePaths.PlaySelection,
                    "ShooterMover.UI.PlaySelection.PlayMenu"),
                new SceneExpectation(
                    FlowScenePaths.LevelSelection,
                    "ShooterMover.UI.LevelSelection.LevelMenu"),
                new SceneExpectation(
                    FlowScenePaths.Inventory,
                    "ShooterMover.UI.InventoryLoadout.InventoryMenu"),
                new SceneExpectation(
                    FlowScenePaths.Skills,
                    "ShooterMover.UI.Skills.SkillsMenu"),
                new SceneExpectation(
                    FlowScenePaths.Shop,
                    "ShooterMover.UI.Shop.ShopMenu"),
                new SceneExpectation(
                    FlowScenePaths.Crafting,
                    "ShooterMover.UI.Crafting.CraftingMenu"),
                new SceneExpectation(
                    FlowScenePaths.Results,
                    "ShooterMover.UI.Game.Results"),
                new SceneExpectation(
                    FlowScenePaths.StrongboxOpening,
                    "ShooterMover.UI.StrongboxOpening.StrongboxOpening"),
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
            if (GameFlow.HasInstance) yield break;

            AsyncOperation load = SceneManager.LoadSceneAsync(
                FlowScenePaths.Bootstrap,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;

            int remainingFrames = 180;
            while (remainingFrames-- > 0
                && !GameFlow.HasInstance)
            {
                yield return null;
            }

            Assert.That(GameFlow.HasInstance, Is.True);
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
