using NUnit.Framework;
using ShooterMover.UI.VisibleSliceLoadoutSelector.Core;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ShooterMover.Tests.PlayMode.VisibleSliceLoadoutSelector
{
    public sealed class FixedLoadoutInputTests : InputTestFixture
    {
        private const string DefaultFixtureId = "loadout.stage1-default-comparison";
        private const string RicochetFixtureId = "loadout.stage1-ricochet-comparison";

        [SetUp]
        public override void Setup()
        {
            base.Setup();
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test]
        public void VirtualKeyboardNavigatesWrapsAndConfirms()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            FixedLoadoutSelectionState state = CreateState();

            Press(keyboard.rightArrowKey);
            Assert.That(FixedLoadoutInput.Read(keyboard, null), Is.EqualTo(LoadoutSelectorCommand.Next));
            Assert.That(state.Apply(FixedLoadoutInput.Read(keyboard, null)), Is.True);
            Assert.That(state.Current.FixtureId, Is.EqualTo(RicochetFixtureId));
            Release(keyboard.rightArrowKey);

            Press(keyboard.rightArrowKey);
            Assert.That(state.Apply(FixedLoadoutInput.Read(keyboard, null)), Is.True);
            Assert.That(state.Current.FixtureId, Is.EqualTo(DefaultFixtureId));
            Release(keyboard.rightArrowKey);

            Press(keyboard.enterKey);
            Assert.That(FixedLoadoutInput.Read(keyboard, null), Is.EqualTo(LoadoutSelectorCommand.Confirm));
            Assert.That(state.Apply(FixedLoadoutInput.Read(keyboard, null)), Is.True);
            Assert.That(state.Phase, Is.EqualTo(LoadoutSelectorPhase.Confirmed));
            Assert.That(state.Confirmed.FixtureId, Is.EqualTo(DefaultFixtureId));

            TestContext.WriteLine(
                "keyboard-trace right:ricochet right:default enter:confirmed-default");
        }

        [Test]
        public void VirtualControllerNavigatesWrapsConfirmsAndCancels()
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            FixedLoadoutSelectionState confirmed = CreateState();

            Press(gamepad.dpad.left);
            Assert.That(FixedLoadoutInput.Read(null, gamepad), Is.EqualTo(LoadoutSelectorCommand.Previous));
            Assert.That(confirmed.Apply(FixedLoadoutInput.Read(null, gamepad)), Is.True);
            Assert.That(confirmed.Current.FixtureId, Is.EqualTo(RicochetFixtureId));
            Release(gamepad.dpad.left);

            Press(gamepad.buttonSouth);
            Assert.That(FixedLoadoutInput.Read(null, gamepad), Is.EqualTo(LoadoutSelectorCommand.Confirm));
            Assert.That(confirmed.Apply(FixedLoadoutInput.Read(null, gamepad)), Is.True);
            Assert.That(confirmed.Confirmed.FixtureId, Is.EqualTo(RicochetFixtureId));
            Release(gamepad.buttonSouth);

            FixedLoadoutSelectionState cancelled = CreateState();
            Press(gamepad.buttonEast);
            Assert.That(FixedLoadoutInput.Read(null, gamepad), Is.EqualTo(LoadoutSelectorCommand.Cancel));
            Assert.That(cancelled.Apply(FixedLoadoutInput.Read(null, gamepad)), Is.True);
            Assert.That(cancelled.Phase, Is.EqualTo(LoadoutSelectorPhase.Cancelled));
            Assert.That(cancelled.Confirmed, Is.Null);

            TestContext.WriteLine(
                "controller-trace dpad-left:ricochet south:confirmed east:cancelled");
        }

        [Test]
        public void SimultaneousConfirmAndNavigationUsesConfirmPriority()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(Key.Enter, Key.RightArrow));
            InputSystem.Update();

            Assert.That(
                FixedLoadoutInput.Read(keyboard, null),
                Is.EqualTo(LoadoutSelectorCommand.Confirm));
        }

        [Test]
        public void SimultaneousCancelConfirmAndNavigationUsesCancelPriority()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(Key.Escape, Key.Enter, Key.RightArrow));
            InputSystem.Update();

            Assert.That(
                FixedLoadoutInput.Read(keyboard, null),
                Is.EqualTo(LoadoutSelectorCommand.Cancel));
        }

        [Test]
        public void UnmappedKeyboardInputIsNeutral()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            FixedLoadoutSelectionState state = CreateState();

            Press(keyboard.aKey);
            LoadoutSelectorCommand command = FixedLoadoutInput.Read(keyboard, null);

            Assert.That(command, Is.EqualTo(LoadoutSelectorCommand.None));
            Assert.That(state.Apply(command), Is.False);
            Assert.That(state.SelectedIndex, Is.Zero);
            Assert.That(state.Phase, Is.EqualTo(LoadoutSelectorPhase.Browsing));
        }

        [Test]
        public void MissingOrDisconnectedDevicesAreNeutral()
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.RemoveDevice(gamepad);

            Assert.That(Gamepad.current, Is.Null);
            Assert.That(FixedLoadoutInput.Read(null, null), Is.EqualTo(LoadoutSelectorCommand.None));
            Assert.That(FixedLoadoutInput.ReadCurrent(), Is.EqualTo(LoadoutSelectorCommand.None));
        }

        private static FixedLoadoutSelectionState CreateState()
        {
            return new FixedLoadoutSelectionState(
                new[]
                {
                    new FixedLoadoutOption(
                        DefaultFixtureId,
                        new[]
                        {
                            "weapon.blaster-machine-gun",
                            "weapon.shotgun",
                            "weapon.rocket-launcher",
                            "weapon.arc-gun",
                        }),
                    new FixedLoadoutOption(
                        RicochetFixtureId,
                        new[]
                        {
                            "weapon.blaster-machine-gun",
                            "weapon.ricochet-gun",
                            "weapon.shotgun",
                            "weapon.rocket-launcher",
                        }),
                },
                DefaultFixtureId);
        }
    }
}
