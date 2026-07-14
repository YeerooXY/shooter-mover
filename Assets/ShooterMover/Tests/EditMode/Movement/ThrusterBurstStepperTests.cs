using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Input;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;

namespace ShooterMover.Tests.EditMode.Movement
{
    public sealed class ThrusterBurstStepperTests
    {
        private const double Tolerance = 0.000000001d;

        [Test]
        public void ActivationPress_ConsumesChargeAndImmediatelyReplacesVelocity()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            ThrusterBurstState ready = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Create(3d, 4d),
                tuning);
            ThrusterBankState bank = ThrusterBankState.CreateFull(tuning);
            PlayerIntentFrame intent = BuildIntent(0f, 1f, ButtonIntent.Pressed);

            bool activated;
            ThrusterBankState nextBank;
            ThrusterBurstState burst = Step(
                ready,
                bank,
                intent,
                0d,
                tuning,
                out nextBank,
                out activated);

            double expectedBurstSpeed =
                tuning.BaseMaximumSpeed * tuning.ThrusterSpeedMultiplier;
            Assert.That(activated, Is.True);
            Assert.That(nextBank.AvailableCharges, Is.EqualTo(bank.AvailableCharges - 1));
            Assert.That(burst.Phase, Is.EqualTo(ThrusterBurstPhase.Burst));
            Assert.That(burst.BurstElapsedSeconds, Is.Zero);
            Assert.That(burst.ChainElapsedSeconds, Is.Zero);
            Assert.That(burst.DirectionX, Is.EqualTo(0d).Within(Tolerance));
            Assert.That(burst.DirectionY, Is.EqualTo(1d).Within(Tolerance));
            Assert.That(burst.VelocityX, Is.EqualTo(0d).Within(Tolerance));
            Assert.That(burst.VelocityY, Is.EqualTo(expectedBurstSpeed).Within(Tolerance));
            Assert.That(burst.Speed, Is.EqualTo(expectedBurstSpeed).Within(Tolerance));
            Assert.That(ready.Speed, Is.EqualTo(5d).Within(Tolerance), "The immutable source state changed.");
            Assert.That(bank.IsFull, Is.True, "The immutable source bank changed.");
        }

        [Test]
        public void DirectionlessActivation_FromRestIsRejectedButExistingMomentumIsAStableFallback()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            ThrusterBankState bank = ThrusterBankState.CreateFull(tuning);
            ThrusterBurstState stationary = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Stationary,
                tuning);

            bool rejectedActivation;
            ThrusterBankState rejectedBank;
            ThrusterBurstState rejected = ThrusterBurstStepper.TryActivate(
                stationary,
                bank,
                0d,
                0d,
                tuning,
                out rejectedBank,
                out rejectedActivation);

            Assert.That(rejectedActivation, Is.False);
            Assert.That(rejected, Is.SameAs(stationary));
            Assert.That(rejectedBank, Is.SameAs(bank));
            Assert.That(rejectedBank.AvailableCharges, Is.EqualTo(bank.MaximumCharges));

            ThrusterBurstState moving = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Create(0d, -4d),
                tuning);
            bool fallbackActivation;
            ThrusterBankState consumedBank;
            ThrusterBurstState fallback = ThrusterBurstStepper.TryActivate(
                moving,
                bank,
                0d,
                0d,
                tuning,
                out consumedBank,
                out fallbackActivation);

            Assert.That(fallbackActivation, Is.True);
            Assert.That(consumedBank.AvailableCharges, Is.EqualTo(bank.MaximumCharges - 1));
            Assert.That(fallback.DirectionX, Is.EqualTo(0d).Within(Tolerance));
            Assert.That(fallback.DirectionY, Is.EqualTo(-1d).Within(Tolerance));
        }

        [Test]
        public void StartupForgiveness_CorrectsImmediatelyThenSteeringIsAngularlyBounded()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(
                steeringDegreesPerSecond: 90d,
                startupForgivenessSeconds: 0.04d);
            ThrusterBankState bank = ThrusterBankState.CreateFull(tuning);
            ThrusterBurstState state = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Stationary,
                tuning);

            bool activated;
            state = Activate(state, ref bank, 1d, 0d, tuning, out activated);
            Assert.That(activated, Is.True);

            state = StepWithoutActivation(state, ref bank, 0d, 1d, 0.04d, tuning);
            Assert.That(state.DirectionX, Is.EqualTo(0d).Within(Tolerance));
            Assert.That(state.DirectionY, Is.EqualTo(1d).Within(Tolerance));
            Assert.That(state.BurstElapsedSeconds, Is.EqualTo(0.04d).Within(Tolerance));

            state = StepWithoutActivation(state, ref bank, -1d, 0d, 0.1d, tuning);

            double expectedAngleRadians = 99d * (Math.PI / 180d);
            Assert.That(state.DirectionX, Is.EqualTo(Math.Cos(expectedAngleRadians)).Within(Tolerance));
            Assert.That(state.DirectionY, Is.EqualTo(Math.Sin(expectedAngleRadians)).Within(Tolerance));
            Assert.That(
                Math.Atan2(state.DirectionY, state.DirectionX) * (180d / Math.PI),
                Is.EqualTo(99d).Within(0.0000001d));
            Assert.That(
                state.Speed,
                Is.EqualTo(tuning.BaseMaximumSpeed * tuning.ThrusterSpeedMultiplier)
                    .Within(Tolerance));
        }

        [Test]
        public void ChainRequest_BeforeIntervalIsRejectedAndExactBoundaryIsAccepted()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(
                minimumChainIntervalSeconds: 0.05d,
                baselineChargeCount: 2);
            ThrusterBankState bank = ThrusterBankState.CreateFull(tuning);
            ThrusterBurstState state = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Stationary,
                tuning);

            bool firstActivation;
            state = Activate(state, ref bank, 1d, 0d, tuning, out firstActivation);
            Assert.That(firstActivation, Is.True);
            Assert.That(bank.AvailableCharges, Is.EqualTo(1));

            bool earlyActivation;
            ThrusterBankState earlyBank;
            state = ThrusterBurstStepper.Step(
                state,
                bank,
                0d,
                1d,
                0.049d,
                true,
                tuning,
                out earlyBank,
                out earlyActivation);
            bank = earlyBank;

            Assert.That(earlyActivation, Is.False);
            Assert.That(bank.AvailableCharges, Is.EqualTo(1));
            Assert.That(state.ChainElapsedSeconds, Is.EqualTo(0.049d).Within(Tolerance));

            bool boundaryActivation;
            ThrusterBankState boundaryBank;
            state = ThrusterBurstStepper.Step(
                state,
                bank,
                -1d,
                0d,
                0.001d,
                true,
                tuning,
                out boundaryBank,
                out boundaryActivation);
            bank = boundaryBank;

            Assert.That(boundaryActivation, Is.True);
            Assert.That(bank.AvailableCharges, Is.Zero);
            Assert.That(state.Phase, Is.EqualTo(ThrusterBurstPhase.Burst));
            Assert.That(state.BurstElapsedSeconds, Is.Zero);
            Assert.That(state.ChainElapsedSeconds, Is.Zero);
            Assert.That(state.DirectionX, Is.EqualTo(-1d).Within(Tolerance));
            Assert.That(state.DirectionY, Is.EqualTo(0d).Within(Tolerance));
        }

        [Test]
        public void ActivationOnExactRechargeBoundary_RegeneratesBeforeConsume()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(
                baselineChargeCount: 1,
                rechargeSeconds: 0.2d,
                minimumChainIntervalSeconds: 0.05d);
            ThrusterBankState bank = ThrusterBankState.CreateFull(tuning);
            ThrusterBurstState state = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Stationary,
                tuning);

            bool firstActivation;
            state = Activate(state, ref bank, 1d, 0d, tuning, out firstActivation);
            Assert.That(firstActivation, Is.True);
            Assert.That(bank.AvailableCharges, Is.Zero);

            bool boundaryActivation;
            ThrusterBankState boundaryBank;
            state = ThrusterBurstStepper.Step(
                state,
                bank,
                0d,
                1d,
                0.2d,
                true,
                tuning,
                out boundaryBank,
                out boundaryActivation);
            bank = boundaryBank;

            Assert.That(boundaryActivation, Is.True);
            Assert.That(bank.AvailableCharges, Is.Zero);
            Assert.That(bank.RegeneratingChargeCount, Is.EqualTo(1));
            Assert.That(bank.GetRechargeElapsedSeconds(0), Is.Zero);
            Assert.That(state.BurstElapsedSeconds, Is.Zero);
            Assert.That(state.DirectionX, Is.EqualTo(0d).Within(Tolerance));
            Assert.That(state.DirectionY, Is.EqualTo(1d).Within(Tolerance));
        }

        [Test]
        public void BurstTransitionsThroughControlledExitMomentumToBaseSpeedHandoff()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(
                burstDurationSeconds: 0.3d,
                exitMomentumSeconds: 0.2d,
                exitSpeedRetention: 0.75d,
                exitDecayExponent: 2d);
            ThrusterBankState bank = ThrusterBankState.CreateFull(tuning);
            ThrusterBurstState state = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Stationary,
                tuning);

            bool activated;
            state = Activate(state, ref bank, 1d, 0d, tuning, out activated);
            Assert.That(activated, Is.True);

            state = StepWithoutActivation(state, ref bank, 1d, 0d, 0.3d, tuning);
            Assert.That(state.Phase, Is.EqualTo(ThrusterBurstPhase.ExitMomentum));
            Assert.That(state.ExitElapsedSeconds, Is.Zero);
            Assert.That(state.Speed, Is.EqualTo(22.5d).Within(Tolerance));

            state = StepWithoutActivation(state, ref bank, 0d, 1d, 0.1d, tuning);
            Assert.That(state.Phase, Is.EqualTo(ThrusterBurstPhase.ExitMomentum));
            Assert.That(state.ExitElapsedSeconds, Is.EqualTo(0.1d).Within(Tolerance));
            Assert.That(state.Speed, Is.EqualTo(14.625d).Within(Tolerance));
            Assert.That(state.DirectionX, Is.EqualTo(1d).Within(Tolerance));
            Assert.That(state.DirectionY, Is.EqualTo(0d).Within(Tolerance));

            state = StepWithoutActivation(state, ref bank, -1d, 0d, 0.1d, tuning);
            Assert.That(state.Phase, Is.EqualTo(ThrusterBurstPhase.Ready));
            Assert.That(state.Speed, Is.EqualTo(tuning.BaseMaximumSpeed).Within(Tolerance));
            Assert.That(state.VelocityX, Is.EqualTo(tuning.BaseMaximumSpeed).Within(Tolerance));
            Assert.That(state.VelocityY, Is.EqualTo(0d).Within(Tolerance));
        }

        [Test]
        public void ExitMomentum_TimestepPartitionsProduceTheSameTrajectoryPoint()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            ThrusterBankState bank = ThrusterBankState.CreateFull(tuning);
            ThrusterBurstState burst = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Stationary,
                tuning);
            bool activated;
            burst = Activate(burst, ref bank, 1d, 0d, tuning, out activated);
            Assert.That(activated, Is.True);

            ThrusterBankState exitBank = bank;
            ThrusterBurstState exit = StepWithoutActivation(
                burst,
                ref exitBank,
                1d,
                0d,
                tuning.ThrusterBurstDurationSeconds,
                tuning);

            ThrusterBankState singleBank;
            bool singleActivated;
            ThrusterBurstState single = ThrusterBurstStepper.Step(
                exit,
                exitBank,
                0d,
                1d,
                0.1d,
                false,
                tuning,
                out singleBank,
                out singleActivated);

            ThrusterBankState partitionedBank = exitBank;
            ThrusterBurstState partitioned = StepWithoutActivation(
                exit,
                ref partitionedBank,
                0d,
                1d,
                0.04d,
                tuning);
            partitioned = StepWithoutActivation(
                partitioned,
                ref partitionedBank,
                -1d,
                0d,
                0.06d,
                tuning);

            Assert.That(singleActivated, Is.False);
            Assert.That(partitioned.Phase, Is.EqualTo(single.Phase));
            Assert.That(partitioned.ExitElapsedSeconds, Is.EqualTo(single.ExitElapsedSeconds).Within(Tolerance));
            Assert.That(partitioned.VelocityX, Is.EqualTo(single.VelocityX).Within(Tolerance));
            Assert.That(partitioned.VelocityY, Is.EqualTo(single.VelocityY).Within(Tolerance));
            Assert.That(partitionedBank, Is.EqualTo(singleBank));
        }

        [Test]
        public void PlayerIntentFrame_AimAndUnrelatedActionsDoNotAffectBurstResolution()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            NormalizedIntentVector2 move = NormalizedIntentVector2.Create(0.75f, 0.25f);
            PlayerIntentFrame quiet = new PlayerIntentFrame(
                move,
                NormalizedIntentVector2.Create(0f, 1f),
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Pressed,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                NormalizedIntentVector2.Zero);
            PlayerIntentFrame busy = new PlayerIntentFrame(
                move,
                NormalizedIntentVector2.Create(-1f, 0f),
                ButtonIntent.Pressed,
                ButtonIntent.Held,
                ButtonIntent.Pressed,
                ButtonIntent.Released,
                ButtonIntent.Held,
                ButtonIntent.Tap,
                NormalizedIntentVector2.Create(-0.75f, 0.5f));
            ThrusterBurstState ready = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Create(1d, -2d),
                tuning);
            ThrusterBankState bank = ThrusterBankState.CreateFull(tuning);

            bool quietActivated;
            ThrusterBankState quietBank;
            ThrusterBurstState quietResult = Step(
                ready,
                bank,
                quiet,
                0d,
                tuning,
                out quietBank,
                out quietActivated);

            bool busyActivated;
            ThrusterBankState busyBank;
            ThrusterBurstState busyResult = Step(
                ready,
                bank,
                busy,
                0d,
                tuning,
                out busyBank,
                out busyActivated);

            Assert.That(quietActivated, Is.True);
            Assert.That(busyActivated, Is.True);
            Assert.That(busyResult, Is.EqualTo(quietResult));
            Assert.That(busyBank, Is.EqualTo(quietBank));
        }

        [Test]
        public void InitialAndEveryAdvancedPhaseRemainInsideAuthoredSpeedBounds()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            ThrusterBurstState state = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Create(100d, -100d),
                tuning);
            ThrusterBankState bank = ThrusterBankState.CreateFull(tuning);

            Assert.That(state.Speed, Is.EqualTo(tuning.BaseMaximumSpeed).Within(Tolerance));

            bool activated;
            double diagonalComponent = 1d / Math.Sqrt(2d);
            state = Activate(
                state,
                ref bank,
                diagonalComponent,
                diagonalComponent,
                tuning,
                out activated);
            Assert.That(activated, Is.True);
            Assert.That(
                state.Speed,
                Is.EqualTo(tuning.BaseMaximumSpeed * tuning.ThrusterSpeedMultiplier)
                    .Within(Tolerance));

            state = StepWithoutActivation(state, ref bank, -1d, 0d, 0.15d, tuning);
            Assert.That(
                state.Speed,
                Is.LessThanOrEqualTo(
                    (tuning.BaseMaximumSpeed * tuning.ThrusterSpeedMultiplier) + Tolerance));

            state = StepWithoutActivation(state, ref bank, 0d, 0d, 100d, tuning);
            Assert.That(state.Phase, Is.EqualTo(ThrusterBurstPhase.Ready));
            Assert.That(state.Speed, Is.LessThanOrEqualTo(tuning.BaseMaximumSpeed + Tolerance));
        }

        [Test]
        public void InvalidInputsAndMismatchedTuningAreRejected()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            MovementThrusterTuningProfile changed = BuildTuning(baseMaximumSpeed: 13d);
            ThrusterBurstState state = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Stationary,
                tuning);
            ThrusterBankState bank = ThrusterBankState.CreateFull(tuning);
            ThrusterBankState nextBank;
            bool activated;

            Assert.Throws<InvalidOperationException>(
                () => ThrusterBurstStepper.Step(
                    state,
                    bank,
                    1d,
                    0d,
                    0.1d,
                    false,
                    changed,
                    out nextBank,
                    out activated));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ThrusterBurstStepper.Step(
                    state,
                    bank,
                    1.1d,
                    0d,
                    0.1d,
                    false,
                    tuning,
                    out nextBank,
                    out activated));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ThrusterBurstStepper.Step(
                    state,
                    bank,
                    double.NaN,
                    0d,
                    0.1d,
                    false,
                    tuning,
                    out nextBank,
                    out activated));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ThrusterBurstStepper.Step(
                    state,
                    bank,
                    0d,
                    0d,
                    -0.1d,
                    false,
                    tuning,
                    out nextBank,
                    out activated));
            Assert.Throws<ArgumentNullException>(
                () => ThrusterBurstStepper.Step(
                    null,
                    bank,
                    0d,
                    0d,
                    0d,
                    false,
                    tuning,
                    out nextBank,
                    out activated));
            Assert.Throws<ArgumentNullException>(
                () => ThrusterBurstStepper.Step(
                    state,
                    null,
                    0d,
                    0d,
                    0d,
                    false,
                    tuning,
                    out nextBank,
                    out activated));
        }

        [Test]
        public void BurstState_IsImmutableAndZeroStepWithoutActivationReturnsSameObjects()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            ThrusterBurstState state = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Create(2d, -1d),
                tuning);
            ThrusterBankState bank = ThrusterBankState.CreateFull(tuning);

            Assert.That(typeof(ThrusterBurstState).IsSealed, Is.True);
            Assert.That(
                typeof(ThrusterBurstState)
                    .GetConstructors(BindingFlags.Instance | BindingFlags.Public),
                Is.Empty);
            Assert.That(
                typeof(ThrusterBurstState)
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property => property.CanWrite),
                Is.Empty);

            bool activated;
            ThrusterBankState nextBank;
            ThrusterBurstState result = ThrusterBurstStepper.Step(
                state,
                bank,
                0d,
                0d,
                0d,
                false,
                tuning,
                out nextBank,
                out activated);

            Assert.That(activated, Is.False);
            Assert.That(result, Is.SameAs(state));
            Assert.That(nextBank, Is.SameAs(bank));
        }

        private static ThrusterBurstState Step(
            ThrusterBurstState state,
            ThrusterBankState bank,
            PlayerIntentFrame intent,
            double deltaTimeSeconds,
            MovementThrusterTuningProfile tuning,
            out ThrusterBankState nextBank,
            out bool activated)
        {
            return ThrusterBurstStepper.Step(
                state,
                bank,
                intent.Move.X,
                intent.Move.Y,
                deltaTimeSeconds,
                intent.Thruster.WasPressed,
                tuning,
                out nextBank,
                out activated);
        }

        private static ThrusterBurstState Activate(
            ThrusterBurstState state,
            ref ThrusterBankState bank,
            double moveX,
            double moveY,
            MovementThrusterTuningProfile tuning,
            out bool activated)
        {
            ThrusterBankState nextBank;
            ThrusterBurstState result = ThrusterBurstStepper.TryActivate(
                state,
                bank,
                moveX,
                moveY,
                tuning,
                out nextBank,
                out activated);
            bank = nextBank;
            return result;
        }

        private static ThrusterBurstState StepWithoutActivation(
            ThrusterBurstState state,
            ref ThrusterBankState bank,
            double moveX,
            double moveY,
            double deltaTimeSeconds,
            MovementThrusterTuningProfile tuning)
        {
            bool activated;
            ThrusterBankState nextBank;
            ThrusterBurstState result = ThrusterBurstStepper.Step(
                state,
                bank,
                moveX,
                moveY,
                deltaTimeSeconds,
                false,
                tuning,
                out nextBank,
                out activated);
            Assert.That(activated, Is.False);
            bank = nextBank;
            return result;
        }

        private static PlayerIntentFrame BuildIntent(
            float moveX,
            float moveY,
            ButtonIntent thruster)
        {
            return new PlayerIntentFrame(
                NormalizedIntentVector2.Create(moveX, moveY),
                NormalizedIntentVector2.Zero,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                thruster,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                NormalizedIntentVector2.Zero);
        }

        private static MovementThrusterTuningProfile BuildTuning(
            int baselineChargeCount = 2,
            double rechargeSeconds = 1.75d,
            double baseMaximumSpeed = 12d,
            double speedMultiplier = 2.5d,
            double burstDurationSeconds = 0.3d,
            double directionInputThreshold = 0.1d,
            double minimumChainIntervalSeconds = 0.05d,
            double steeringDegreesPerSecond = 120d,
            double startupForgivenessSeconds = 0.04d,
            double exitMomentumSeconds = 0.2d,
            double exitSpeedRetention = 0.75d,
            double exitDecayExponent = 2d)
        {
            return MovementThrusterTuningProfile.Create(
                MovementThrusterTuningProfile.CurrentProfileVersion,
                StableId.Parse("tuning.mt-004-tests"),
                baseMaximumSpeed,
                50d,
                60d,
                90d,
                1.25d,
                baselineChargeCount,
                1,
                rechargeSeconds,
                speedMultiplier,
                burstDurationSeconds,
                directionInputThreshold,
                minimumChainIntervalSeconds,
                steeringDegreesPerSecond,
                startupForgivenessSeconds,
                exitMomentumSeconds,
                exitSpeedRetention,
                exitDecayExponent,
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
