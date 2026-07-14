using System;
using NUnit.Framework;
using ShooterMover.Contracts.Input;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;

namespace ShooterMover.Tests.EditMode.Movement
{
    public sealed class BaseLocomotionStepperTests
    {
        private const double Tolerance = 0.000000001d;

        [Test]
        public void FullMoveIntent_AcceleratesResponsivelyAndCapsAtMaximumVelocity()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            PlayerIntentFrame intent = BuildIntent(1f, 0f, 0f, 1f);

            BaseLocomotionState state = Step(
                BaseLocomotionState.Stationary,
                intent,
                0.1d,
                tuning);

            Assert.That(state.VelocityX, Is.EqualTo(5d).Within(Tolerance));
            Assert.That(state.VelocityY, Is.EqualTo(0d).Within(Tolerance));

            for (int index = 0; index < 20; index++)
            {
                state = Step(state, intent, 0.1d, tuning);
            }

            Assert.That(state.VelocityX, Is.EqualTo(tuning.BaseMaximumSpeed).Within(Tolerance));
            Assert.That(state.VelocityY, Is.EqualTo(0d).Within(Tolerance));
            Assert.That(state.Speed, Is.LessThanOrEqualTo(tuning.BaseMaximumSpeed + Tolerance));
        }

        [Test]
        public void ZeroMoveIntent_AfterMaximumVelocity_BrakesToExactStopWithoutDrift()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            BaseLocomotionState maximumVelocity = BaseLocomotionState.Create(
                tuning.BaseMaximumSpeed,
                0d);

            BaseLocomotionState stopped = Step(
                maximumVelocity,
                PlayerIntentFrame.Neutral,
                tuning.BaseMaximumSpeed / tuning.BaseBraking,
                tuning);
            BaseLocomotionState stillStopped = Step(
                stopped,
                PlayerIntentFrame.Neutral,
                1d,
                tuning);

            Assert.That(stopped, Is.EqualTo(BaseLocomotionState.Stationary));
            Assert.That(stillStopped, Is.EqualTo(BaseLocomotionState.Stationary));
        }

        [Test]
        public void ReverseMoveIntent_UsesCounterSteerBeforeOppositeAcceleration()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            PlayerIntentFrame reverse = BuildIntent(-1f, 0f, 1f, 0f);
            BaseLocomotionState state = BaseLocomotionState.Create(
                tuning.BaseMaximumSpeed,
                0d);

            BaseLocomotionState first = Step(state, reverse, 0.1d, tuning);
            BaseLocomotionState second = Step(first, reverse, 0.1d, tuning);
            BaseLocomotionState third = Step(second, reverse, 0.1d, tuning);

            Assert.That(first.VelocityX, Is.EqualTo(3d).Within(Tolerance));
            Assert.That(second.VelocityX, Is.EqualTo(-6d).Within(Tolerance));
            Assert.That(third.VelocityX, Is.EqualTo(-11d).Within(Tolerance));
            Assert.That(first.VelocityY, Is.EqualTo(0d).Within(Tolerance));
            Assert.That(second.VelocityY, Is.EqualTo(0d).Within(Tolerance));
            Assert.That(third.VelocityY, Is.EqualTo(0d).Within(Tolerance));
        }

        [Test]
        public void DiagonalMoveIntent_ReachesSameBoundedSpeedAsCardinalIntent()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            PlayerIntentFrame diagonal = BuildIntent(1f, 1f, 0f, 0f);
            PlayerIntentFrame cardinal = BuildIntent(1f, 0f, 0f, 0f);

            BaseLocomotionState diagonalState = Step(
                BaseLocomotionState.Stationary,
                diagonal,
                1d,
                tuning);
            BaseLocomotionState cardinalState = Step(
                BaseLocomotionState.Stationary,
                cardinal,
                1d,
                tuning);

            double expectedComponent = tuning.BaseMaximumSpeed / Math.Sqrt(2d);
            Assert.That(diagonalState.VelocityX, Is.EqualTo(expectedComponent).Within(0.000001d));
            Assert.That(diagonalState.VelocityY, Is.EqualTo(expectedComponent).Within(0.000001d));
            Assert.That(diagonalState.Speed, Is.EqualTo(cardinalState.Speed).Within(0.000001d));
            Assert.That(diagonalState.Speed, Is.EqualTo(tuning.BaseMaximumSpeed).Within(0.000001d));
        }

        [Test]
        public void EquivalentElapsedTime_IsStableAcrossFixedTimestepPartitions()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            PlayerIntentFrame intent = BuildIntent(1f, 0f, 0f, 0f);

            BaseLocomotionState oneStep = Step(
                BaseLocomotionState.Stationary,
                intent,
                0.1d,
                tuning);
            BaseLocomotionState tenSteps = BaseLocomotionState.Stationary;
            for (int index = 0; index < 10; index++)
            {
                tenSteps = Step(tenSteps, intent, 0.01d, tuning);
            }

            Assert.That(tenSteps.VelocityX, Is.EqualTo(oneStep.VelocityX).Within(Tolerance));
            Assert.That(tenSteps.VelocityY, Is.EqualTo(oneStep.VelocityY).Within(Tolerance));
        }

        [Test]
        public void PlayerIntentFrame_AimAndNonMovementActions_DoNotAffectLocomotion()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            BaseLocomotionState current = BaseLocomotionState.Create(2d, -1d);
            NormalizedIntentVector2 move = NormalizedIntentVector2.Create(0.75f, 0.25f);

            PlayerIntentFrame quiet = new PlayerIntentFrame(
                move,
                NormalizedIntentVector2.Create(0f, 1f),
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                NormalizedIntentVector2.Zero);
            PlayerIntentFrame busy = new PlayerIntentFrame(
                move,
                NormalizedIntentVector2.Create(-1f, 0f),
                ButtonIntent.Pressed,
                ButtonIntent.Held,
                ButtonIntent.Tap,
                ButtonIntent.Released,
                ButtonIntent.Held,
                ButtonIntent.Pressed,
                NormalizedIntentVector2.Create(-0.75f, 0.5f));

            BaseLocomotionState quietResult = Step(current, quiet, 0.125d, tuning);
            BaseLocomotionState busyResult = Step(current, busy, 0.125d, tuning);

            Assert.That(busyResult, Is.EqualTo(quietResult));
        }

        [Test]
        public void AnalogueMoveIntent_UsesConfiguredResponseExponent()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            PlayerIntentFrame halfMove = BuildIntent(0.5f, 0f, 0f, 0f);

            BaseLocomotionState result = Step(
                BaseLocomotionState.Stationary,
                halfMove,
                1d,
                tuning);

            double expectedSpeed = tuning.BaseMaximumSpeed
                * Math.Pow(0.5d, tuning.BaseVelocityResponseExponent);
            Assert.That(result.VelocityX, Is.EqualTo(expectedSpeed).Within(Tolerance));
            Assert.That(result.VelocityY, Is.EqualTo(0d).Within(Tolerance));
        }

        [Test]
        public void InvalidTimestepAndNonNormalizedMove_AreRejected()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => BaseLocomotionStepper.Step(
                    BaseLocomotionState.Stationary,
                    1.1d,
                    0d,
                    0.02d,
                    tuning));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BaseLocomotionStepper.Step(
                    BaseLocomotionState.Stationary,
                    0d,
                    0d,
                    -0.02d,
                    tuning));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BaseLocomotionStepper.Step(
                    BaseLocomotionState.Stationary,
                    double.NaN,
                    0d,
                    0.02d,
                    tuning));
            Assert.Throws<ArgumentNullException>(
                () => BaseLocomotionStepper.Step(
                    BaseLocomotionState.Stationary,
                    0d,
                    0d,
                    0.02d,
                    null));
        }

        [Test]
        public void ZeroTimestep_ReturnsCurrentStateUnchanged()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            BaseLocomotionState current = BaseLocomotionState.Create(20d, -5d);

            BaseLocomotionState result = BaseLocomotionStepper.Step(
                current,
                1d,
                0d,
                0d,
                tuning);

            Assert.That(result, Is.EqualTo(current));
        }

        private static BaseLocomotionState Step(
            BaseLocomotionState current,
            PlayerIntentFrame intent,
            double deltaTimeSeconds,
            MovementThrusterTuningProfile tuning)
        {
            return BaseLocomotionStepper.Step(
                current,
                intent.Move.X,
                intent.Move.Y,
                deltaTimeSeconds,
                tuning);
        }

        private static PlayerIntentFrame BuildIntent(
            float moveX,
            float moveY,
            float aimX,
            float aimY)
        {
            return new PlayerIntentFrame(
                NormalizedIntentVector2.Create(moveX, moveY),
                NormalizedIntentVector2.Create(aimX, aimY),
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                NormalizedIntentVector2.Zero);
        }

        private static MovementThrusterTuningProfile BuildTuning()
        {
            return MovementThrusterTuningProfile.Create(
                MovementThrusterTuningProfile.CurrentProfileVersion,
                StableId.Parse("tuning.movement-prototype"),
                12d,
                50d,
                60d,
                90d,
                1.25d,
                2,
                1,
                1.75d,
                2.5d,
                0.3d,
                0.1d,
                0.05d,
                120d,
                0.04d,
                0.2d,
                0.75d,
                2d,
                0.8d,
                0.15d,
                5d,
                4,
                0.8d,
                0.9d,
                0.1d,
                0.5d,
                0.02d,
                128);
        }
    }
}
