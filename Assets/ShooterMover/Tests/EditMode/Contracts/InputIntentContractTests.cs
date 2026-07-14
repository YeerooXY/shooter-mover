using System;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Contracts.Input;

namespace ShooterMover.Tests.EditMode.Contracts
{
    public sealed class InputIntentContractTests
    {
        [Test]
        public void NormalizedVector_OverUnitMagnitude_IsNormalized()
        {
            NormalizedIntentVector2 value = NormalizedIntentVector2.Create(3f, 4f);

            Assert.That(value.X, Is.EqualTo(0.6f).Within(0.000001f));
            Assert.That(value.Y, Is.EqualTo(0.8f).Within(0.000001f));
            Assert.That(value.MagnitudeSquared, Is.EqualTo(1f).Within(0.000001f));
        }

        [Test]
        public void NormalizedVector_InsideUnitCircle_PreservesAnalogueMagnitude()
        {
            NormalizedIntentVector2 value = NormalizedIntentVector2.Create(0.25f, -0.5f);

            Assert.That(value.X, Is.EqualTo(0.25f));
            Assert.That(value.Y, Is.EqualTo(-0.5f));
            Assert.That(value.MagnitudeSquared, Is.EqualTo(0.3125f));
        }

        [Test]
        public void MoveAndAim_RemainIndependentNormalizedIntents()
        {
            PlayerIntentFrame frame = CreateFrame(
                move: NormalizedIntentVector2.Create(4f, 0f),
                aim: NormalizedIntentVector2.Create(0f, -2f));

            Assert.That(frame.Move.X, Is.EqualTo(1f).Within(0.000001f));
            Assert.That(frame.Move.Y, Is.EqualTo(0f));
            Assert.That(frame.Aim.X, Is.EqualTo(0f));
            Assert.That(frame.Aim.Y, Is.EqualTo(-1f).Within(0.000001f));
        }

        [Test]
        public void NormalizedVector_NonFiniteComponents_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => NormalizedIntentVector2.Create(float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => NormalizedIntentVector2.Create(float.PositiveInfinity, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => NormalizedIntentVector2.Create(0f, float.NegativeInfinity));
        }

        [Test]
        public void ButtonIntent_ExposesHeldPressedAndReleasedWithoutAmbiguity()
        {
            ButtonIntent pressed = ButtonIntent.Pressed;
            ButtonIntent held = ButtonIntent.Held;
            ButtonIntent released = ButtonIntent.Released;
            ButtonIntent tap = ButtonIntent.Tap;
            ButtonIntent releaseThenPress = ButtonIntent.ReleaseThenPress;

            Assert.That(pressed.IsHeld, Is.True);
            Assert.That(pressed.WasPressed, Is.True);
            Assert.That(pressed.WasReleased, Is.False);

            Assert.That(held.IsHeld, Is.True);
            Assert.That(held.WasPressed, Is.False);
            Assert.That(held.WasReleased, Is.False);

            Assert.That(released.IsHeld, Is.False);
            Assert.That(released.WasPressed, Is.False);
            Assert.That(released.WasReleased, Is.True);

            Assert.That(tap.IsHeld, Is.False);
            Assert.That(tap.WasPressed, Is.True);
            Assert.That(tap.WasReleased, Is.True);

            Assert.That(releaseThenPress.IsHeld, Is.True);
            Assert.That(releaseThenPress.WasPressed, Is.True);
            Assert.That(releaseThenPress.WasReleased, Is.True);
        }

        [Test]
        public void ButtonIntent_ImpossibleSingleEdgeFinalStates_AreRejected()
        {
            Assert.Throws<ArgumentException>(() => new ButtonIntent(false, true, false));
            Assert.Throws<ArgumentException>(() => new ButtonIntent(true, false, true));
        }

        [Test]
        public void PlayerIntentFrame_RepresentsSimultaneousPauseAndFire()
        {
            PlayerIntentFrame frame = new PlayerIntentFrame(
                NormalizedIntentVector2.Create(1f, 0f),
                NormalizedIntentVector2.Create(0f, 1f),
                ButtonIntent.Pressed,
                ButtonIntent.Held,
                ButtonIntent.Pressed,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Pressed,
                NormalizedIntentVector2.Create(-1f, 0f));

            Assert.That(frame.Fire, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(frame.PowerModifier, Is.EqualTo(ButtonIntent.Held));
            Assert.That(frame.Thruster, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(frame.PauseMenu, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(frame.Move.X, Is.EqualTo(1f));
        }

        [Test]
        public void FocusLoss_NeutralizesAxesAndReleasesEveryHeldAction()
        {
            PlayerIntentFrame previous = new PlayerIntentFrame(
                NormalizedIntentVector2.Create(1f, 0f),
                NormalizedIntentVector2.Create(0f, -1f),
                ButtonIntent.Held,
                ButtonIntent.Pressed,
                ButtonIntent.Inactive,
                ButtonIntent.Released,
                ButtonIntent.Tap,
                ButtonIntent.Held,
                NormalizedIntentVector2.Create(1f, 1f));

            PlayerIntentFrame focusLoss = PlayerIntentFrame.FromFocusLoss(previous);

            Assert.That(focusLoss.WasFocusLost, Is.True);
            Assert.That(focusLoss.Move, Is.EqualTo(NormalizedIntentVector2.Zero));
            Assert.That(focusLoss.Aim, Is.EqualTo(NormalizedIntentVector2.Zero));
            Assert.That(focusLoss.UiNavigation, Is.EqualTo(NormalizedIntentVector2.Zero));
            Assert.That(focusLoss.Fire, Is.EqualTo(ButtonIntent.Released));
            Assert.That(focusLoss.PowerModifier, Is.EqualTo(ButtonIntent.Released));
            Assert.That(focusLoss.PauseMenu, Is.EqualTo(ButtonIntent.Released));
            Assert.That(focusLoss.Thruster, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(focusLoss.Interact, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(focusLoss.Map, Is.EqualTo(ButtonIntent.Inactive));
        }

        [Test]
        public void NeutralFrame_IsAllocationFreeAndContainsNoActiveIntent()
        {
            PlayerIntentFrame neutral = PlayerIntentFrame.Neutral;

            Assert.That(typeof(PlayerIntentFrame).IsValueType, Is.True);
            Assert.That(neutral.WasFocusLost, Is.False);
            Assert.That(neutral.Move, Is.EqualTo(NormalizedIntentVector2.Zero));
            Assert.That(neutral.Aim, Is.EqualTo(NormalizedIntentVector2.Zero));
            Assert.That(neutral.UiNavigation, Is.EqualTo(NormalizedIntentVector2.Zero));
            Assert.That(neutral.Fire, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(neutral.PowerModifier, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(neutral.Thruster, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(neutral.Interact, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(neutral.Map, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(neutral.PauseMenu, Is.EqualTo(ButtonIntent.Inactive));
        }

        [Test]
        public void ContractsAssembly_DoesNotReferenceUnityEngine()
        {
            bool hasUnityReference = typeof(PlayerIntentFrame)
                .Assembly
                .GetReferencedAssemblies()
                .Any(name => name.Name.StartsWith("UnityEngine", StringComparison.Ordinal));

            Assert.That(hasUnityReference, Is.False);
        }

        private static PlayerIntentFrame CreateFrame(
            NormalizedIntentVector2 move,
            NormalizedIntentVector2 aim)
        {
            return new PlayerIntentFrame(
                move,
                aim,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                NormalizedIntentVector2.Zero);
        }
    }
}
