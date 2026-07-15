using NUnit.Framework;
using ShooterMover.Presentation.LoadoutPreview;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.Tests.PlayMode.Presentation.LoadoutPreview
{
    public sealed class TemporaryLoadoutMenuInputTests : InputTestFixture
    {
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
        public void KeyboardAndControllerMapToDeterministicMenuCommands()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            Gamepad gamepad = AddDevice<Gamepad>();

            Press(keyboard.rightArrowKey);
            Assert.That(
                TemporaryLoadoutInputSource.Read(keyboard, gamepad),
                Is.EqualTo(TemporaryLoadoutMenuCommand.Next));
            Release(keyboard.rightArrowKey);

            Press(gamepad.dpad.left);
            Assert.That(
                TemporaryLoadoutInputSource.Read(keyboard, gamepad),
                Is.EqualTo(TemporaryLoadoutMenuCommand.Previous));
            Release(gamepad.dpad.left);

            Press(gamepad.buttonSouth);
            Assert.That(
                TemporaryLoadoutInputSource.Read(keyboard, gamepad),
                Is.EqualTo(TemporaryLoadoutMenuCommand.Confirm));
            Release(gamepad.buttonSouth);

            Press(keyboard.escapeKey);
            Assert.That(
                TemporaryLoadoutInputSource.Read(keyboard, gamepad),
                Is.EqualTo(TemporaryLoadoutMenuCommand.Cancel));
        }

        [Test]
        public void ViewForwardsConfirmAndRestartRestoresDefault()
        {
            GameObject owner = new GameObject("temporary-loadout-menu-test");
            try
            {
                TemporaryLoadoutMenuView view = owner.AddComponent<TemporaryLoadoutMenuView>();
                view.ResetForRestart();

                TemporaryLoadoutChoice confirmed = null;
                view.Confirmed += choice => confirmed = choice;

                Assert.That(view.ApplyCommand(TemporaryLoadoutMenuCommand.Next), Is.True);
                Assert.That(view.Presenter.SelectedIndex, Is.EqualTo(1));
                Assert.That(view.ApplyCommand(TemporaryLoadoutMenuCommand.Confirm), Is.True);
                Assert.That(confirmed, Is.SameAs(view.Presenter.ConfirmedChoice));
                Assert.That(view.Visible, Is.False);

                view.ResetForRestart();
                Assert.That(view.Visible, Is.True);
                Assert.That(view.Presenter.Phase, Is.EqualTo(TemporaryLoadoutMenuPhase.Browsing));
                Assert.That(view.Presenter.SelectedIndex, Is.EqualTo(TemporaryLoadoutCatalog.DefaultIndex));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
