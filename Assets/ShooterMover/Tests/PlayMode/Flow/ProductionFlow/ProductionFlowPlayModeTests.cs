using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NUnit.Framework;
using ShooterMover.Application.Characters.Selection;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.UI.Crafting;
using ShooterMover.UI.Hub;
using ShooterMover.UI.ProductionFlow;
using ShooterMover.UI.Shop;
using ShooterMover.UI.Skills;
using ShooterMover.UI.StrongboxOpening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Flow.ProductionFlow
{
    public sealed class ProductionFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator EmptyProfileAutomaticallyOpensCreationAndValidates()
        {
            GameObject host = new GameObject("Character creation test");
            ProductionCharacterSelectionControllerV1 controller =
                host.AddComponent<ProductionCharacterSelectionControllerV1>();
            PlayerRouteProfilePayloadV1 draft = Route("creation");
            CharacterSelectionRouteResultV1 created = null;
            string createdName = null;
            controller.Configure(
                draft,
                null,
                delegate { return false; },
                delegate(
                    string name,
                    CharacterSelectionRouteResultV1 result)
                {
                    createdName = name;
                    created = result;
                    return true;
                },
                delegate { return true; });

            Assert.That(
                controller.Stage,
                Is.EqualTo(
                    ProductionCharacterSelectionStageV1.CharacterCreation));
            Assert.That(controller.ConfirmCreation(), Is.False);
            controller.SetCharacterName("Nova");
            Assert.That(controller.ConfirmCreation(), Is.False);
            Assert.That(controller.SelectClassByIndex(1), Is.True);
            Assert.That(controller.ConfirmCreation(), Is.True);
            Assert.That(createdName, Is.EqualTo("Nova"));
            Assert.That(
                created.Status,
                Is.EqualTo(CharacterSelectionRouteStatusV1.Confirmed));
            for (int index = 0; index < draft.WeaponSlots.Count; index++)
            {
                Assert.That(
                    created.Payload.WeaponSlots[index]
                        .EquipmentInstanceStableId,
                    Is.EqualTo(
                        draft.WeaponSlots[index]
                            .EquipmentInstanceStableId));
            }

            UnityEngine.Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExistingProfileRoutesExactPayloadDirectly()
        {
            GameObject host = new GameObject("Existing profile test");
            ProductionCharacterSelectionControllerV1 controller =
                host.AddComponent<ProductionCharacterSelectionControllerV1>();
            PlayerRouteProfilePayloadV1 payload = Route("existing");
            var record = new ProductionFlowProfileRecordV1(
                "Existing Pilot",
                payload);
            PlayerRouteProfilePayloadV1 received = null;
            controller.Configure(
                payload,
                record,
                delegate(PlayerRouteProfilePayloadV1 value)
                {
                    received = value;
                    return true;
                },
                delegate { return false; },
                delegate { return true; });

            Assert.That(
                controller.Stage,
                Is.EqualTo(
                    ProductionCharacterSelectionStageV1.CharacterSlots));
            Assert.That(controller.ChooseExisting(), Is.True);
            Assert.That(received, Is.SameAs(payload));

            UnityEngine.Object.Destroy(host);
            yield return null;
        }


        [UnityTest]
        public IEnumerator PlayerPrefsProfileReloadsExistingImmutableRoute()
        {
            var store = new PlayerPrefsProductionFlowProfileStoreV1();
            store.Clear();
            PlayerRouteProfilePayloadV1 payload =
                Route("persisted-existing");
            store.Save(new ProductionFlowProfileRecordV1(
                "Persisted Pilot",
                payload));

            ProductionFlowProfileRecordV1 loaded;
            Assert.That(store.TryLoad(out loaded), Is.True);
            Assert.That(loaded.DisplayName, Is.EqualTo("Persisted Pilot"));
            Assert.That(loaded.Payload, Is.EqualTo(payload));
            Assert.That(
                loaded.Payload.SelectedCharacterStableId,
                Is.EqualTo(payload.SelectedCharacterStableId));
            for (int index = 0; index < payload.WeaponSlots.Count; index++)
            {
                Assert.That(
                    loaded.Payload.WeaponSlots[index]
                        .EquipmentInstanceStableId,
                    Is.EqualTo(
                        payload.WeaponSlots[index]
                            .EquipmentInstanceStableId));
            }

            store.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExactResultsSelectionBindsExistingStrongboxController()
        {
            MissionRunStrongboxCollectionV1 collection = Collection("playmode");
            var exact = new MissionRunStrongboxResultV1(
                collection,
                MissionRunStrongboxStateV1.Unopened,
                null,
                null);
            PlayerRouteProfilePayloadV1 route = Route("strongbox");
            MissionResultPayloadV1 result = MissionResultPayloadV1.Create(
                StableId.Parse("run.flow-playmode-strongbox"),
                route,
                MissionRunCompletionStateV1.Completed,
                new[] { exact },
                1L,
                2L,
                MissionRunCanonicalV1.Fingerprint("holdings-playmode"),
                3L,
                MissionRunCanonicalV1.Fingerprint("opening-playmode"));

#pragma warning disable SYSLIB0050
            var service = (StrongboxOpeningServiceV1)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpeningServiceV1));
            var command = (StrongboxOpenCommandV1)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpenCommandV1));
#pragma warning restore SYSLIB0050

            var context = new ProductionResultsContextV1(
                result,
                service,
                delegate(MissionRunStrongboxResultV1 selected)
                {
                    Assert.That(selected, Is.SameAs(exact));
                    return command;
                },
                (EquipmentCatalog)null,
                delegate { return result; });

            GameObject openingHost = new GameObject("Strongbox opening binding");
            StrongboxOpeningController opening =
                openingHost.AddComponent<StrongboxOpeningController>();
            GameObject resultsHost = new GameObject("Results exact selection");
            ProductionResultsControllerV1 results =
                resultsHost.AddComponent<ProductionResultsControllerV1>();
            MissionRunStrongboxResultV1 routed = null;
            results.Configure(
                result,
                delegate(MissionRunStrongboxResultV1 selected)
                {
                    routed = selected;
                    ProductionStrongboxOpeningBindingV1 binding =
                        context.BindExact(selected);
                    opening.BindRuntime(
                        binding.OpeningService,
                        binding.Command,
                        binding.EquipmentCatalog);
                    return true;
                },
                delegate { return true; });

            Assert.That(results.OpenExact(exact), Is.True);
            Assert.That(routed, Is.SameAs(exact));
            Assert.That(opening.RuntimePort, Is.Not.Null);
            Assert.That(opening.IsPreviewOnly, Is.False);

            UnityEngine.Object.Destroy(resultsHost);
            UnityEngine.Object.Destroy(openingHost);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BootstrapRoutesToMainMenuWithOneCameraThroughout()
        {
            bool bootstrapObserved = false;
            int bootstrapCameraCount = 0;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> handler =
                delegate(Scene loadedScene, LoadSceneMode mode)
                {
                    if (string.Equals(
                        loadedScene.path,
                        ProductionFlowScenePathsV1.Bootstrap,
                        StringComparison.Ordinal))
                    {
                        bootstrapObserved = true;
                        bootstrapCameraCount = Camera.allCamerasCount;
                    }
                };

            SceneManager.sceneLoaded += handler;
            try
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(
                    ProductionFlowScenePathsV1.Bootstrap,
                    LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null);
                while (!load.isDone) yield return null;

                int remainingFrames = 180;
                while (remainingFrames-- > 0
                    && !string.Equals(
                        SceneManager.GetActiveScene().path,
                        ProductionFlowScenePathsV1.MainMenu,
                        StringComparison.Ordinal))
                {
                    yield return null;
                }

                Assert.That(bootstrapObserved, Is.True);
                Assert.That(bootstrapCameraCount, Is.EqualTo(1));
                Assert.That(
                    SceneManager.GetActiveScene().path,
                    Is.EqualTo(ProductionFlowScenePathsV1.MainMenu));
                Assert.That(Camera.allCamerasCount, Is.EqualTo(1));
            }
            finally
            {
                SceneManager.sceneLoaded -= handler;
            }
        }

        [UnityTest]
        public IEnumerator CanonicalScenesRetainRealControllersAndArtwork()
        {
            yield return EnsureCoordinator();
            yield return Load(ProductionFlowScenePathsV1.MainMenu);
            ProductionMainMenuControllerV1 main =
                FindOne<ProductionMainMenuControllerV1>();
            Assert.That(main, Is.Not.Null);
            Assert.That(main.HasBackgroundAsset, Is.True);

            yield return Load(
                ProductionFlowScenePathsV1.CharacterSelection);
            Assert.That(
                Resources.Load<TextAsset>(
                    "CharacterSelect/character_choice_screen"),
                Is.Not.Null);
            Assert.That(
                Resources.Load<TextAsset>(
                    "CharacterSelect/character_creation_choice_screen"),
                Is.Not.Null);
            Assert.That(
                Resources.Load<TextAsset>("CharacterSelect/aggressive_class"),
                Is.Not.Null);

            yield return Load(ProductionFlowScenePathsV1.Skills);
            SkillsSceneController skills =
                FindOne<SkillsSceneController>();
            Assert.That(skills, Is.Not.Null);
            Assert.That(skills.HasBackplateAsset, Is.True);

            yield return Load(ProductionFlowScenePathsV1.Shop);
            ShopScreenControllerV1 shop =
                FindOne<ShopScreenControllerV1>();
            Assert.That(shop, Is.Not.Null);
            Assert.That(shop.ShopTemplate, Is.Not.Null);

            yield return Load(ProductionFlowScenePathsV1.Crafting);
            CraftingScreenControllerV1 crafting =
                FindOne<CraftingScreenControllerV1>();
            Assert.That(crafting, Is.Not.Null);
            Assert.That(crafting.HasBackplateAsset, Is.True);

            yield return Load(ProductionFlowScenePathsV1.Results);
            ProductionResultsControllerV1 results =
                FindOne<ProductionResultsControllerV1>();
            Assert.That(results, Is.Not.Null);
            Assert.That(results.HasBackgroundAsset, Is.True);

            yield return Load(
                ProductionFlowScenePathsV1.StrongboxOpening);
            Assert.That(
                UnityEngine.Object.FindObjectsByType<
                    StrongboxOpeningController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator EveryCanonicalSceneHasOneActiveCamera()
        {
            yield return EnsureCoordinator();
            string[] paths =
            {
                ProductionFlowScenePathsV1.MainMenu,
                ProductionFlowScenePathsV1.CharacterSelection,
                ProductionFlowScenePathsV1.Hub,
                ProductionFlowScenePathsV1.PlaySelection,
                ProductionFlowScenePathsV1.LevelSelection,
                ProductionFlowScenePathsV1.Inventory,
                ProductionFlowScenePathsV1.Skills,
                ProductionFlowScenePathsV1.Shop,
                ProductionFlowScenePathsV1.Crafting,
                ProductionFlowScenePathsV1.Results,
                ProductionFlowScenePathsV1.StrongboxOpening,
            };

            for (int index = 0; index < paths.Length; index++)
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(
                    paths[index],
                    LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null, paths[index]);
                while (!load.isDone) yield return null;
                yield return null;

                Assert.That(
                    Camera.allCamerasCount,
                    Is.EqualTo(1),
                    paths[index]);
            }
        }

        [UnityTest]
        public IEnumerator MainCharacterAndHubScenesOwnOneCanonicalController()
        {
            yield return EnsureCoordinator();
            yield return Load(ProductionFlowScenePathsV1.MainMenu);
            Assert.That(
                UnityEngine.Object.FindObjectsByType<ProductionMainMenuControllerV1>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(1));

            yield return Load(
                ProductionFlowScenePathsV1.CharacterSelection);
            Assert.That(
                UnityEngine.Object.FindObjectsByType<
                    ProductionCharacterSelectionControllerV1>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(1));

            yield return Load(ProductionFlowScenePathsV1.Hub);
            Assert.That(
                UnityEngine.Object.FindObjectsByType<HubFlowControllerV1>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
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

        private static T FindOne<T>()
            where T : Component
        {
            T[] values = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(values, Has.Length.EqualTo(1));
            return values[0];
        }

        private static IEnumerator Load(string path)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                path,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, path);
            while (!load.isDone) yield return null;
            yield return null;
        }


        private static MissionRunStrongboxCollectionV1 Collection(
            string suffix)
        {
            return new MissionRunStrongboxCollectionV1(
                StableId.Parse("strongbox-definition." + suffix),
                StableId.Parse("strongbox-instance." + suffix),
                StableId.Parse("grant." + suffix),
                StableId.Parse("source." + suffix),
                StableId.Parse("operation." + suffix),
                1L,
                MissionRunCanonicalV1.Fingerprint("collection-" + suffix));
        }

        private static PlayerRouteProfilePayloadV1 Route(string suffix)
        {
            return PlayerRouteProfilePayloadV1.Create(
                StableId.Parse("character." + suffix),
                StableId.Parse("loadout-profile." + suffix),
                new List<StableId>
                {
                    StableId.Parse("equipment-instance." + suffix + "-1"),
                    StableId.Parse("equipment-instance." + suffix + "-2"),
                    StableId.Parse("equipment-instance." + suffix + "-3"),
                    StableId.Parse("equipment-instance." + suffix + "-4"),
                });
        }
    }
}
