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
    public sealed class FlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator EmptyProfileAutomaticallyOpensCreationAndValidates()
        {
            GameObject host = new GameObject("Character creation test");
            CharacterSelectionController controller =
                host.AddComponent<CharacterSelectionController>();
            PlayerRouteProfilePayload draft = Route("creation");
            CharacterSelectionRouteResult created = null;
            string createdName = null;
            controller.Configure(
                draft,
                null,
                delegate { return false; },
                delegate(
                    string name,
                    CharacterSelectionRouteResult result)
                {
                    createdName = name;
                    created = result;
                    return true;
                },
                delegate { return true; });

            Assert.That(
                controller.Stage,
                Is.EqualTo(
                    CharacterSelectionStage.CharacterCreation));
            Assert.That(controller.ConfirmCreation(), Is.False);
            controller.SetCharacterName("Nova");
            Assert.That(controller.ConfirmCreation(), Is.False);
            Assert.That(controller.SelectClassByIndex(1), Is.True);
            Assert.That(controller.ConfirmCreation(), Is.True);
            Assert.That(createdName, Is.EqualTo("Nova"));
            Assert.That(
                created.Status,
                Is.EqualTo(CharacterSelectionRouteStatus.Confirmed));
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
        public IEnumerator ExplicitClickedSlotSurvivesCharacterCreation()
        {
            GameObject host = new GameObject("Exact character slot test");
            CharacterSelectionController controller =
                host.AddComponent<CharacterSelectionController>();
            PlayerRouteProfilePayload draft = Route("exact-slot-draft");
            var profiles = new FlowProfileRecord[6];
            profiles[5] = new FlowProfileRecord(
                "Occupied Last Slot",
                Route("exact-slot-occupied"));
            int createdSlot = -1;

            controller.Configure(
                draft,
                profiles,
                delegate { return false; },
                delegate(
                    int slotIndex,
                    string name,
                    CharacterSelectionRouteResult result)
                {
                    createdSlot = slotIndex;
                    return result != null
                        && result.Status
                            == CharacterSelectionRouteStatus.Confirmed;
                },
                delegate { return false; },
                delegate { return true; });

            Assert.That(
                controller.Stage,
                Is.EqualTo(
                    CharacterSelectionStage.CharacterSlots));
            Assert.That(controller.ChooseEmptySlot(1), Is.True);
            Assert.That(controller.SelectedSlotIndex, Is.EqualTo(1));
            controller.SetCharacterName("Exact Slot Pilot");
            Assert.That(controller.SelectClassByIndex(0), Is.True);
            Assert.That(controller.ConfirmCreation(), Is.True);
            Assert.That(createdSlot, Is.EqualTo(1));

            UnityEngine.Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProfileDeletionRequiresConfirmationAndUsesExactSlot()
        {
            GameObject host = new GameObject("Exact profile deletion test");
            CharacterSelectionController controller =
                host.AddComponent<CharacterSelectionController>();
            var profiles = new FlowProfileRecord[6];
            profiles[1] = new FlowProfileRecord(
                "Delete Me",
                Route("delete-selected"));
            FlowProfileRecord survivor =
                new FlowProfileRecord(
                    "Keep Me",
                    Route("delete-survivor"));
            profiles[4] = survivor;
            int deletedSlot = -1;

            controller.Configure(
                profiles[1].Payload,
                profiles,
                delegate { return false; },
                delegate { return false; },
                delegate(int slotIndex)
                {
                    deletedSlot = slotIndex;
                    profiles[slotIndex] = null;
                    return true;
                },
                delegate { return true; });

            Assert.That(controller.RequestDeleteProfile(1), Is.False);
            Assert.That(controller.PendingDeleteSlotIndex, Is.EqualTo(1));
            Assert.That(profiles[1], Is.Not.Null);
            Assert.That(controller.RequestDeleteProfile(1), Is.True);
            Assert.That(deletedSlot, Is.EqualTo(1));
            Assert.That(profiles[1], Is.Null);
            Assert.That(profiles[4], Is.SameAs(survivor));

            UnityEngine.Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExistingProfileRoutesExactPayloadDirectly()
        {
            GameObject host = new GameObject("Existing profile test");
            CharacterSelectionController controller =
                host.AddComponent<CharacterSelectionController>();
            PlayerRouteProfilePayload payload = Route("existing");
            var record = new FlowProfileRecord(
                "Existing Pilot",
                payload);
            PlayerRouteProfilePayload received = null;
            controller.Configure(
                payload,
                record,
                delegate(PlayerRouteProfilePayload value)
                {
                    received = value;
                    return true;
                },
                delegate { return false; },
                delegate { return true; });

            Assert.That(
                controller.Stage,
                Is.EqualTo(
                    CharacterSelectionStage.CharacterSlots));
            Assert.That(controller.ChooseExisting(), Is.True);
            Assert.That(received, Is.SameAs(payload));

            UnityEngine.Object.Destroy(host);
            yield return null;
        }


        [UnityTest]
        public IEnumerator PlayerPrefsProfileReloadsExistingImmutableRoute()
        {
            var store = new PlayerPrefsFlowProfileStore();
            store.Clear();
            PlayerRouteProfilePayload payload =
                Route("persisted-existing");
            store.Save(new FlowProfileRecord(
                "Persisted Pilot",
                payload));

            FlowProfileRecord loaded;
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
        public IEnumerator PlayerPrefsDeletionClearsOnlyExactProfileSlot()
        {
            var store = new PlayerPrefsFlowProfileStore();
            store.Clear();
            var removed = new FlowProfileRecord(
                "Removed Pilot",
                Route("persisted-delete-removed"));
            var survivor = new FlowProfileRecord(
                "Surviving Pilot",
                Route("persisted-delete-survivor"));
            store.Save(1, removed);
            store.Save(4, survivor);

            store.Clear(1);

            FlowProfileRecord loadedRemoved;
            FlowProfileRecord loadedSurvivor;
            Assert.That(store.TryLoad(1, out loadedRemoved), Is.False);
            Assert.That(store.TryLoad(4, out loadedSurvivor), Is.True);
            Assert.That(loadedSurvivor.DisplayName, Is.EqualTo("Surviving Pilot"));
            Assert.That(loadedSurvivor.Payload, Is.EqualTo(survivor.Payload));

            store.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExactResultsSelectionBindsExistingStrongboxController()
        {
            MissionRunStrongboxCollection collection = Collection("playmode");
            var exact = new MissionRunStrongboxResult(
                collection,
                MissionRunStrongboxState.Unopened,
                null,
                null);
            PlayerRouteProfilePayload route = Route("strongbox");
            MissionResultPayload result = MissionResultPayload.Create(
                StableId.Parse("run.flow-playmode-strongbox"),
                route,
                MissionRunCompletionState.Completed,
                new[] { exact },
                1L,
                2L,
                MissionRun.Fingerprint("holdings-playmode"),
                3L,
                MissionRun.Fingerprint("opening-playmode"));

#pragma warning disable SYSLIB0050
            var service = (StrongboxOpeningActions)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpeningActions));
            var command = (StrongboxOpenCommand)
                FormatterServices.GetUninitializedObject(
                    typeof(StrongboxOpenCommand));
#pragma warning restore SYSLIB0050

            var context = new ResultsContext(
                result,
                service,
                delegate(MissionRunStrongboxResult selected)
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
            ResultsController results =
                resultsHost.AddComponent<ResultsController>();
            MissionRunStrongboxResult routed = null;
            results.Configure(
                result,
                delegate(MissionRunStrongboxResult selected)
                {
                    routed = selected;
                    StrongboxOpeningBinding binding =
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
                        FlowScenePaths.Bootstrap,
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
                    FlowScenePaths.Bootstrap,
                    LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null);
                while (!load.isDone) yield return null;

                int remainingFrames = 180;
                while (remainingFrames-- > 0
                    && !string.Equals(
                        SceneManager.GetActiveScene().path,
                        FlowScenePaths.MainMenu,
                        StringComparison.Ordinal))
                {
                    yield return null;
                }

                Assert.That(bootstrapObserved, Is.True);
                Assert.That(bootstrapCameraCount, Is.EqualTo(1));
                Assert.That(
                    SceneManager.GetActiveScene().path,
                    Is.EqualTo(FlowScenePaths.MainMenu));
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
            yield return Load(FlowScenePaths.MainMenu);
            MainMenuController main =
                FindOne<MainMenuController>();
            Assert.That(main, Is.Not.Null);
            Assert.That(main.HasBackgroundAsset, Is.True);

            yield return Load(
                FlowScenePaths.CharacterSelection);
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

            yield return Load(FlowScenePaths.Skills);
            SkillsSceneController skills =
                FindOne<SkillsSceneController>();
            Assert.That(skills, Is.Not.Null);
            Assert.That(skills.HasBackplateAsset, Is.True);

            yield return Load(FlowScenePaths.Shop);
            ShopScreenController shop =
                FindOne<ShopScreenController>();
            Assert.That(shop, Is.Not.Null);
            Assert.That(shop.ShopTemplate, Is.Not.Null);

            yield return Load(FlowScenePaths.Crafting);
            CraftingScreenController crafting =
                FindOne<CraftingScreenController>();
            Assert.That(crafting, Is.Not.Null);
            Assert.That(crafting.HasBackplateAsset, Is.True);

            yield return Load(FlowScenePaths.Results);
            ResultsController results =
                FindOne<ResultsController>();
            Assert.That(results, Is.Not.Null);
            Assert.That(results.HasBackgroundAsset, Is.True);

            yield return Load(
                FlowScenePaths.StrongboxOpening);
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
                FlowScenePaths.MainMenu,
                FlowScenePaths.CharacterSelection,
                FlowScenePaths.Hub,
                FlowScenePaths.PlaySelection,
                FlowScenePaths.LevelSelection,
                FlowScenePaths.Inventory,
                FlowScenePaths.Skills,
                FlowScenePaths.Shop,
                FlowScenePaths.Crafting,
                FlowScenePaths.Results,
                FlowScenePaths.StrongboxOpening,
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
            yield return Load(FlowScenePaths.MainMenu);
            Assert.That(
                UnityEngine.Object.FindObjectsByType<MainMenuController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(1));

            yield return Load(
                FlowScenePaths.CharacterSelection);
            Assert.That(
                UnityEngine.Object.FindObjectsByType<
                    CharacterSelectionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(1));

            yield return Load(FlowScenePaths.Hub);
            Assert.That(
                UnityEngine.Object.FindObjectsByType<HubFlowController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
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


        private static MissionRunStrongboxCollection Collection(
            string suffix)
        {
            return new MissionRunStrongboxCollection(
                StableId.Parse("strongbox-definition." + suffix),
                StableId.Parse("strongbox-instance." + suffix),
                StableId.Parse("grant." + suffix),
                StableId.Parse("source." + suffix),
                StableId.Parse("operation." + suffix),
                1L,
                MissionRun.Fingerprint("collection-" + suffix));
        }

        private static PlayerRouteProfilePayload Route(string suffix)
        {
            return PlayerRouteProfilePayload.Create(
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
