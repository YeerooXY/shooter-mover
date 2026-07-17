using System;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Menu;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Menu
{
    public sealed class MainMenuFlowStateTests
    {
        [Test]
        public void Armory_SelectsEverySlotIndependently()
        {
            MainMenuFlowState state = CreateState();
            MenuWeaponOption[] options = state.Armory.Options.ToArray();

            Assert.That(state.Armory.TrySelectInstance(0, options[0].InstanceStableId), Is.False);
            Assert.That(state.Armory.TrySelectInstance(1, options[1].InstanceStableId), Is.True);
            Assert.That(state.Armory.TrySelectInstance(2, options[2].InstanceStableId), Is.True);
            Assert.That(state.Armory.TrySelectInstance(3, options[3].InstanceStableId), Is.True);

            Assert.That(
                state.Armory.GetSelectedWeapon(0).InstanceStableId,
                Is.EqualTo(options[0].InstanceStableId));
            Assert.That(
                state.Armory.GetSelectedWeapon(1).InstanceStableId,
                Is.EqualTo(options[1].InstanceStableId));
            Assert.That(
                state.Armory.GetSelectedWeapon(2).InstanceStableId,
                Is.EqualTo(options[2].InstanceStableId));
            Assert.That(
                state.Armory.GetSelectedWeapon(3).InstanceStableId,
                Is.EqualTo(options[3].InstanceStableId));
        }

        [Test]
        public void Armory_DuplicateDefinitionsRemainDistinctAndSelectable()
        {
            MainMenuFlowState state = CreateState();
            MenuWeaponOption first = state.Armory.Options[0];
            MenuWeaponOption duplicate = state.Armory.Options[1];

            Assert.That(
                duplicate.DefinitionStableId,
                Is.EqualTo(first.DefinitionStableId));
            Assert.That(
                duplicate.InstanceStableId,
                Is.Not.EqualTo(first.InstanceStableId));

            Assert.That(
                state.Armory.TrySelectInstance(0, first.InstanceStableId),
                Is.False);
            Assert.That(
                state.Armory.TrySelectInstance(1, duplicate.InstanceStableId),
                Is.True);
            Assert.That(
                state.Armory.GetSelectedWeapon(0).DefinitionStableId,
                Is.EqualTo(state.Armory.GetSelectedWeapon(1).DefinitionStableId));
            Assert.That(
                state.Armory.GetSelectedWeapon(0).InstanceStableId,
                Is.Not.EqualTo(state.Armory.GetSelectedWeapon(1).InstanceStableId));
        }

        [Test]
        public void Armory_ReplacementPreservesSelectionsByInstanceNotDefinition()
        {
            MainMenuFlowState state = CreateState();
            MenuWeaponOption first = state.Armory.Options[0];
            MenuWeaponOption duplicate = state.Armory.Options[1];
            Assert.That(
                state.Armory.TrySelectInstance(2, duplicate.InstanceStableId),
                Is.True);

            MenuWeaponOption replacementDuplicate = new MenuWeaponOption(
                StableId.Parse("menu-test.blaster-c"),
                first.DefinitionStableId,
                "Blaster C");
            state.Armory.ReplaceOptions(new[]
            {
                duplicate,
                replacementDuplicate,
                first,
            });

            Assert.That(
                state.Armory.GetSelectedWeapon(2).InstanceStableId,
                Is.EqualTo(duplicate.InstanceStableId));
            Assert.That(
                state.Armory.Options.Count(
                    option => option.DefinitionStableId == first.DefinitionStableId),
                Is.EqualTo(3));
        }

        [Test]
        public void Navigation_BackReturnsToTitleThenRequestsQuit()
        {
            MainMenuFlowState state = CreateState();

            Assert.That(state.OpenScreen(MainMenuScreen.Settings), Is.True);
            Assert.That(state.CurrentScreen, Is.EqualTo(MainMenuScreen.Settings));
            Assert.That(state.NavigateBack(), Is.True);
            Assert.That(state.CurrentScreen, Is.EqualTo(MainMenuScreen.Title));
            Assert.That(state.QuitRequested, Is.False);

            Assert.That(state.NavigateBack(), Is.False);
            Assert.That(state.QuitRequested, Is.True);
        }

        [Test]
        public void PlayAndSettings_ExposeAcceptedTargetAndFlags()
        {
            MainMenuFlowState state = CreateState();

            Assert.That(
                MainMenuFlowState.PlayScenePath,
                Is.EqualTo(
                    "Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity"));
            Assert.That(state.Settings.SetReducedEffects(true), Is.True);
            Assert.That(state.Settings.SetGrayscale(true), Is.True);

            state.RequestPlay();

            Assert.That(state.PlayRequested, Is.True);
            Assert.That(state.Settings.ReducedEffects, Is.True);
            Assert.That(state.Settings.Grayscale, Is.True);
        }

        [Test]
        public void RuntimeConnections_ArePresentationFactsOnly()
        {
            MainMenuFlowState state = CreateState();

            state.SetRuntimeConnections(true, true, false);

            Assert.That(state.HoldingsConnected, Is.True);
            Assert.That(state.ShopConnected, Is.True);
            Assert.That(state.CraftingConnected, Is.False);
        }

        [Test]
        public void Armory_RejectsDuplicateInstanceIdentity()
        {
            MenuWeaponOption option = new MenuWeaponOption(
                StableId.Parse("menu-test.same-instance"),
                StableId.Parse("menu-test.definition"),
                "One");

            Assert.Throws<ArgumentException>(
                delegate
                {
                    new ArmoryLoadoutState(new[]
                    {
                        option,
                        new MenuWeaponOption(
                            option.InstanceStableId,
                            StableId.Parse("menu-test.other-definition"),
                            "Two"),
                    });
                });
        }

        private static MainMenuFlowState CreateState()
        {
            StableId blasterDefinition =
                StableId.Parse("menu-test.blaster-definition");
            return new MainMenuFlowState(new[]
            {
                new MenuWeaponOption(
                    StableId.Parse("menu-test.blaster-instance-a"),
                    blasterDefinition,
                    "Blaster A"),
                new MenuWeaponOption(
                    StableId.Parse("menu-test.blaster-instance-b"),
                    blasterDefinition,
                    "Blaster B"),
                new MenuWeaponOption(
                    StableId.Parse("menu-test.shotgun-instance"),
                    StableId.Parse("menu-test.shotgun-definition"),
                    "Shotgun"),
                new MenuWeaponOption(
                    StableId.Parse("menu-test.rocket-instance"),
                    StableId.Parse("menu-test.rocket-definition"),
                    "Rocket Launcher"),
            });
        }
    }
}
