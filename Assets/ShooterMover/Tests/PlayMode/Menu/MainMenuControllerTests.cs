using System.Collections;
using NUnit.Framework;
using ShooterMover.Application.Menu;
using ShooterMover.Domain.Common;
using ShooterMover.UI.MainMenu;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Menu
{
    public sealed class MainMenuControllerTests
    {
        [UnityTest]
        public IEnumerator Play_ForwardsExactSceneAndCurrentSettings()
        {
            FakePlatformActions platform = new FakePlatformActions();
            MainMenuController controller = CreateController(platform);

            Assert.That(controller.SetReducedEffects(true), Is.True);
            Assert.That(controller.SetGrayscale(true), Is.True);
            controller.RequestPlay();

            Assert.That(platform.LoadCount, Is.EqualTo(1));
            Assert.That(
                platform.ScenePath,
                Is.EqualTo(MainMenuFlowState.PlayScenePath));
            Assert.That(platform.ReducedEffects, Is.True);
            Assert.That(platform.Grayscale, Is.True);
            Assert.That(controller.State.PlayRequested, Is.True);

            Object.Destroy(controller.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EscapeFlow_BackThenQuitUsesPlatformBoundary()
        {
            FakePlatformActions platform = new FakePlatformActions();
            MainMenuController controller = CreateController(platform);

            Assert.That(
                controller.OpenScreen(MainMenuScreen.Crafting),
                Is.True);
            Assert.That(controller.NavigateBack(), Is.True);
            Assert.That(
                controller.State.CurrentScreen,
                Is.EqualTo(MainMenuScreen.Title));
            Assert.That(platform.QuitCount, Is.Zero);

            Assert.That(controller.NavigateBack(), Is.False);
            Assert.That(platform.QuitCount, Is.EqualTo(1));
            Assert.That(controller.State.QuitRequested, Is.True);

            Object.Destroy(controller.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Armory_KeepsDuplicateDefinitionsAcrossIndependentSlots()
        {
            FakePlatformActions platform = new FakePlatformActions();
            MainMenuController controller = CreateController(platform);
            MenuWeaponOption first = controller.State.Armory.Options[0];
            MenuWeaponOption duplicate = controller.State.Armory.Options[1];

            Assert.That(
                first.DefinitionStableId,
                Is.EqualTo(duplicate.DefinitionStableId));
            Assert.That(controller.SelectArmorySlot(1), Is.True);
            Assert.That(
                controller.SelectArmoryWeapon(duplicate.InstanceStableId),
                Is.True);

            Assert.That(
                controller.State.Armory.GetSelectedWeapon(0).InstanceStableId,
                Is.EqualTo(first.InstanceStableId));
            Assert.That(
                controller.State.Armory.GetSelectedWeapon(1).InstanceStableId,
                Is.EqualTo(duplicate.InstanceStableId));
            Assert.That(
                controller.State.Armory.GetSelectedWeapon(0).DefinitionStableId,
                Is.EqualTo(
                    controller.State.Armory.GetSelectedWeapon(1).DefinitionStableId));

            Object.Destroy(controller.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ServiceShells_StartDisconnectedAndDoNotInvokeTransactions()
        {
            FakePlatformActions platform = new FakePlatformActions();
            MainMenuController controller = CreateController(platform);

            controller.BindRuntimeServices(null, null, null, null);

            Assert.That(controller.State.HoldingsConnected, Is.False);
            Assert.That(controller.State.ShopConnected, Is.False);
            Assert.That(controller.State.CraftingConnected, Is.False);
            Assert.That(controller.BoundShopService, Is.Null);
            Assert.That(controller.BoundCraftingService, Is.Null);
            Assert.That(platform.LoadCount, Is.Zero);
            Assert.That(platform.QuitCount, Is.Zero);

            Object.Destroy(controller.gameObject);
            yield return null;
        }

        private static MainMenuController CreateController(
            FakePlatformActions platform)
        {
            GameObject gameObject = new GameObject("MENU-001 Test Controller");
            MainMenuController controller =
                gameObject.AddComponent<MainMenuController>();
            StableId definition =
                StableId.Parse("menu-playtest.blaster-definition");
            controller.ConfigureForTests(
                platform,
                new[]
                {
                    new MenuWeaponOption(
                        StableId.Parse("menu-playtest.blaster-a"),
                        definition,
                        "Blaster A"),
                    new MenuWeaponOption(
                        StableId.Parse("menu-playtest.blaster-b"),
                        definition,
                        "Blaster B"),
                    new MenuWeaponOption(
                        StableId.Parse("menu-playtest.shotgun"),
                        StableId.Parse("menu-playtest.shotgun-definition"),
                        "Shotgun"),
                    new MenuWeaponOption(
                        StableId.Parse("menu-playtest.rocket"),
                        StableId.Parse("menu-playtest.rocket-definition"),
                        "Rocket Launcher"),
                });
            return controller;
        }

        private sealed class FakePlatformActions : IMainMenuPlatformActions
        {
            public int LoadCount { get; private set; }

            public int QuitCount { get; private set; }

            public string ScenePath { get; private set; }

            public bool ReducedEffects { get; private set; }

            public bool Grayscale { get; private set; }

            public void LoadPlayScene(
                string scenePath,
                bool reducedEffects,
                bool grayscale)
            {
                LoadCount++;
                ScenePath = scenePath;
                ReducedEffects = reducedEffects;
                Grayscale = grayscale;
            }

            public void Quit()
            {
                QuitCount++;
            }
        }
    }
}
