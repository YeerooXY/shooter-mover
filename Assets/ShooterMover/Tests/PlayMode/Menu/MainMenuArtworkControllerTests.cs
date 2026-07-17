using System.Collections;
using NUnit.Framework;
using ShooterMover.Application.Menu;
using ShooterMover.Domain.Common;
using ShooterMover.UI.MainMenu;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Menu
{
    public sealed class MainMenuArtworkControllerTests
    {
        [UnityTest]
        public IEnumerator EveryArtworkScreen_CanBeEnteredAndExited()
        {
            FakePlatformActions platform = new FakePlatformActions();
            MainMenuArtworkController artwork = CreateArtwork(platform);
            MenuArtworkScreen[] screens =
            {
                MenuArtworkScreen.LevelSelection,
                MenuArtworkScreen.Skills,
                MenuArtworkScreen.Inventory,
                MenuArtworkScreen.Shop,
                MenuArtworkScreen.Crafting,
                MenuArtworkScreen.Settings,
                MenuArtworkScreen.Results,
            };

            foreach (MenuArtworkScreen screen in screens)
            {
                Assert.That(artwork.OpenScreen(screen), Is.True, screen.ToString());
                Assert.That(artwork.CurrentScreen, Is.EqualTo(screen));
                Assert.That(artwork.NavigateBack(), Is.True, screen.ToString());
                Assert.That(artwork.CurrentScreen, Is.EqualTo(MenuArtworkScreen.Title));
                Assert.That(platform.QuitCount, Is.Zero);
            }

            Object.Destroy(artwork.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LevelCardsAndSkillNodes_AreProjectionOnly()
        {
            FakePlatformActions platform = new FakePlatformActions();
            MainMenuArtworkController artwork = CreateArtwork(platform);

            Assert.That(artwork.OpenScreen(MenuArtworkScreen.Skills), Is.True);
            Assert.That(artwork.ActivateSkillNode(0), Is.True);
            Assert.That(artwork.ActivateSkillNode(19), Is.True);
            Assert.That(artwork.IsSkillNodeHighlighted(0), Is.True);
            Assert.That(artwork.IsSkillNodeHighlighted(19), Is.True);
            Assert.That(artwork.ResetSkillPreview(), Is.True);
            Assert.That(artwork.IsSkillNodeHighlighted(0), Is.False);
            Assert.That(platform.LoadCount, Is.Zero);

            Assert.That(artwork.OpenScreen(MenuArtworkScreen.LevelSelection), Is.True);
            Assert.That(artwork.ActivateLevelCard(1), Is.True);
            Assert.That(artwork.SelectedLevelCardIndex, Is.EqualTo(1));
            Assert.That(platform.LoadCount, Is.Zero);

            Assert.That(artwork.ActivateLevelCard(0), Is.True);
            Assert.That(platform.LoadCount, Is.EqualTo(1));
            Assert.That(platform.ScenePath, Is.EqualTo(MainMenuFlowState.PlayScenePath));

            Object.Destroy(artwork.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Inventory_PreservesDuplicateDefinitionsAsSeparateInstances()
        {
            FakePlatformActions platform = new FakePlatformActions();
            MainMenuArtworkController artwork = CreateArtwork(platform);
            MainMenuController backend = artwork.GetComponent<MainMenuController>();
            MenuWeaponOption first = backend.State.Armory.Options[0];
            MenuWeaponOption duplicate = backend.State.Armory.Options[1];

            Assert.That(first.DefinitionStableId, Is.EqualTo(duplicate.DefinitionStableId));
            Assert.That(first.InstanceStableId, Is.Not.EqualTo(duplicate.InstanceStableId));
            Assert.That(artwork.OpenScreen(MenuArtworkScreen.Inventory), Is.True);
            Assert.That(backend.SelectArmorySlot(1), Is.True);
            Assert.That(backend.SelectArmoryWeapon(duplicate.InstanceStableId), Is.True);
            Assert.That(
                backend.State.Armory.GetSelectedWeapon(0).InstanceStableId,
                Is.EqualTo(first.InstanceStableId));
            Assert.That(
                backend.State.Armory.GetSelectedWeapon(1).InstanceStableId,
                Is.EqualTo(duplicate.InstanceStableId));

            Object.Destroy(artwork.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BackFromTitle_UsesExistingQuitBoundary()
        {
            FakePlatformActions platform = new FakePlatformActions();
            MainMenuArtworkController artwork = CreateArtwork(platform);

            Assert.That(artwork.NavigateBack(), Is.False);
            Assert.That(platform.QuitCount, Is.EqualTo(1));

            Object.Destroy(artwork.gameObject);
            yield return null;
        }

        private static MainMenuArtworkController CreateArtwork(
            FakePlatformActions platform)
        {
            GameObject gameObject = new GameObject("MENU-001 Artwork Test");
            MainMenuController backend = gameObject.AddComponent<MainMenuController>();
            StableId definition = StableId.Parse("menu-arttest.blaster-definition");
            backend.ConfigureForTests(
                platform,
                new[]
                {
                    new MenuWeaponOption(
                        StableId.Parse("menu-arttest.blaster-a"),
                        definition,
                        "Blaster A"),
                    new MenuWeaponOption(
                        StableId.Parse("menu-arttest.blaster-b"),
                        definition,
                        "Blaster B"),
                    new MenuWeaponOption(
                        StableId.Parse("menu-arttest.shotgun"),
                        StableId.Parse("menu-arttest.shotgun-definition"),
                        "Shotgun"),
                    new MenuWeaponOption(
                        StableId.Parse("menu-arttest.rocket"),
                        StableId.Parse("menu-arttest.rocket-definition"),
                        "Rocket Launcher"),
                });
            MainMenuArtworkController artwork =
                gameObject.AddComponent<MainMenuArtworkController>();
            artwork.ConfigureForTests(backend);
            return artwork;
        }

        private sealed class FakePlatformActions : IMainMenuPlatformActions
        {
            public int LoadCount { get; private set; }
            public int QuitCount { get; private set; }
            public string ScenePath { get; private set; }

            public void LoadPlayScene(
                string scenePath,
                bool reducedEffects,
                bool grayscale)
            {
                LoadCount++;
                ScenePath = scenePath;
            }

            public void Quit()
            {
                QuitCount++;
            }
        }
    }
}
