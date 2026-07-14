using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;

namespace ShooterMover.Tests.EditMode.Movement
{
    public sealed class WallReflectionPolicyTests
    {
        private const double Tolerance = 0.000000001d;

        [Test]
        public void HeadOnContact_ReflectsAndRetainsAuthoredSpeed()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(
                wallSpeedRetention: 0.75d,
                wallInputInfluence: 0d,
                wallMinimumSpeed: 0d);
            ThrusterBurstState burst = CreateBurstState(-1d, 0d, tuning);

            WallReflectionResult result = WallReflectionPolicy.Reflect(
                burst,
                2d,
                0d,
                0d,
                0d,
                0,
                tuning);

            Assert.That(result.Outcome, Is.EqualTo(WallReflectionOutcome.Reflected));
            Assert.That(result.WasReflected, Is.True);
            Assert.That(result.ContactsProcessed, Is.EqualTo(1));
            Assert.That(result.ContactNormalX, Is.EqualTo(1d).Within(Tolerance));
            Assert.That(result.ContactNormalY, Is.Zero.Within(Tolerance));
            Assert.That(result.IncomingVelocityX, Is.EqualTo(-30d).Within(Tolerance));
            Assert.That(result.OutgoingVelocityX, Is.EqualTo(22.5d).Within(Tolerance));
            Assert.That(result.OutgoingVelocityY, Is.Zero.Within(Tolerance));
            Assert.That(result.OutgoingSpeed, Is.EqualTo(22.5d).Within(Tolerance));
            Assert.That(result.State.Phase, Is.EqualTo(ThrusterBurstPhase.Burst));
        }

        [Test]
        public void GrazingContact_PreservesTangentialVelocityWithoutArtificialLoss()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(
                wallSpeedRetention: 0.2d,
                wallMinimumSpeed: 10d);
            ThrusterBurstState burst = CreateBurstState(0d, 1d, tuning);

            WallReflectionResult result = WallReflectionPolicy.Reflect(
                burst,
                1d,
                0d,
                1d,
                0d,
                0,
                tuning);

            Assert.That(result.Outcome, Is.EqualTo(WallReflectionOutcome.NoIncomingImpact));
            Assert.That(result.State, Is.SameAs(burst));
            Assert.That(result.ContactsProcessed, Is.EqualTo(1));
            Assert.That(result.OutgoingVelocityX, Is.Zero.Within(Tolerance));
            Assert.That(result.OutgoingVelocityY, Is.EqualTo(30d).Within(Tolerance));
        }

        [Test]
        public void InputInfluence_BlendsTowardIntentWhileRemainingOutsideWall()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(
                wallSpeedRetention: 1d,
                wallInputInfluence: 0.5d,
                wallMinimumSpeed: 0d);
            ThrusterBurstState burst = CreateBurstState(-1d, 0d, tuning);

            WallReflectionResult result = WallReflectionPolicy.Reflect(
                burst,
                1d,
                0d,
                0d,
                1d,
                0,
                tuning);

            double expectedComponent = Math.Sqrt(0.5d);
            Assert.That(result.State.DirectionX, Is.EqualTo(expectedComponent).Within(Tolerance));
            Assert.That(result.State.DirectionY, Is.EqualTo(expectedComponent).Within(Tolerance));
            Assert.That(result.OutgoingSpeed, Is.EqualTo(30d).Within(Tolerance));
            Assert.That(
                (result.OutgoingVelocityX * result.ContactNormalX)
                + (result.OutgoingVelocityY * result.ContactNormalY),
                Is.GreaterThanOrEqualTo(-Tolerance));
        }

        [Test]
        public void InputInfluence_CannotSteerTheReflectionBackIntoTheWall()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(
                wallSpeedRetention: 1d,
                wallInputInfluence: 1d,
                wallMinimumSpeed: 0d);
            ThrusterBurstState burst = CreateBurstState(-1d, 0d, tuning);

            WallReflectionResult result = WallReflectionPolicy.Reflect(
                burst,
                1d,
                0d,
                -1d,
                0d,
                0,
                tuning);

            Assert.That(result.State.DirectionX, Is.EqualTo(1d).Within(Tolerance));
            Assert.That(result.State.DirectionY, Is.Zero.Within(Tolerance));
            Assert.That(result.OutgoingVelocityX, Is.GreaterThan(0d));
        }

        [Test]
        public void RepeatedContacts_StopAtTheAuthoredLimitAndRemainBounded()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(
                wallSpeedRetention: 0.5d,
                wallInputInfluence: 0d,
                wallMinimumSpeed: 2d,
                wallMaximumContacts: 3);
            ThrusterBurstState state = CreateBurstState(-1d, 0d, tuning);

            WallReflectionResult first = Reflect(state, 1d, 0d, 0, tuning);
            WallReflectionResult second = Reflect(first.State, -1d, 0d, first.ContactsProcessed, tuning);
            WallReflectionResult third = Reflect(second.State, 1d, 0d, second.ContactsProcessed, tuning);
            WallReflectionResult limited = Reflect(third.State, -1d, 0d, third.ContactsProcessed, tuning);

            Assert.That(first.OutgoingSpeed, Is.EqualTo(15d).Within(Tolerance));
            Assert.That(second.OutgoingSpeed, Is.EqualTo(7.5d).Within(Tolerance));
            Assert.That(third.OutgoingSpeed, Is.EqualTo(3.75d).Within(Tolerance));
            Assert.That(limited.Outcome, Is.EqualTo(WallReflectionOutcome.ContactLimitReached));
            Assert.That(limited.ReachedContactLimit, Is.True);
            Assert.That(limited.ContactsProcessed, Is.EqualTo(3));
            Assert.That(limited.State, Is.SameAs(third.State));
            Assert.That(double.IsNaN(limited.OutgoingSpeed), Is.False);
            Assert.That(double.IsInfinity(limited.OutgoingSpeed), Is.False);
            Assert.That(limited.OutgoingSpeed, Is.LessThanOrEqualTo(30d + Tolerance));
        }

        [Test]
        public void OrthogonalCornerContacts_ProduceTheSameOutgoingStateInEitherOrder()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(
                wallSpeedRetention: 1d,
                wallInputInfluence: 0d,
                wallMinimumSpeed: 0d);
            double diagonal = -Math.Sqrt(0.5d);
            ThrusterBurstState burst = CreateBurstState(diagonal, diagonal, tuning);

            WallReflectionResult xThen = Reflect(burst, 1d, 0d, 0, tuning);
            WallReflectionResult xThenY = Reflect(
                xThen.State,
                0d,
                1d,
                xThen.ContactsProcessed,
                tuning);

            WallReflectionResult yThen = Reflect(burst, 0d, 1d, 0, tuning);
            WallReflectionResult yThenX = Reflect(
                yThen.State,
                1d,
                0d,
                yThen.ContactsProcessed,
                tuning);

            double expected = Math.Sqrt(0.5d);
            Assert.That(xThenY.State, Is.EqualTo(yThenX.State));
            Assert.That(xThenY.ContactsProcessed, Is.EqualTo(2));
            Assert.That(yThenX.ContactsProcessed, Is.EqualTo(2));
            Assert.That(xThenY.State.DirectionX, Is.EqualTo(expected).Within(Tolerance));
            Assert.That(xThenY.State.DirectionY, Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void InvalidAndNearZeroContactNormals_AreRejectedDeterministically()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            ThrusterBurstState burst = CreateBurstState(-1d, 0d, tuning);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => WallReflectionPolicy.Reflect(burst, 0d, 0d, 0d, 0d, 0, tuning));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WallReflectionPolicy.Reflect(burst, 0.0000001d, 0d, 0d, 0d, 0, tuning));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WallReflectionPolicy.Reflect(burst, double.NaN, 1d, 0d, 0d, 0, tuning));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WallReflectionPolicy.Reflect(burst, double.PositiveInfinity, 1d, 0d, 0d, 0, tuning));
        }

        [Test]
        public void InvalidMoveIntentAndContactCount_AreRejected()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(wallMaximumContacts: 2);
            ThrusterBurstState burst = CreateBurstState(-1d, 0d, tuning);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => WallReflectionPolicy.Reflect(burst, 1d, 0d, 1d, 1d, 0, tuning));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WallReflectionPolicy.Reflect(burst, 1d, 0d, double.NaN, 0d, 0, tuning));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WallReflectionPolicy.Reflect(burst, 1d, 0d, 0d, 0d, -1, tuning));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WallReflectionPolicy.Reflect(burst, 1d, 0d, 0d, 0d, 3, tuning));
        }

        [Test]
        public void ReadyPhase_UsesBaseSpeedBoundEvenWhenMinimumExceedsIt()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(
                wallSpeedRetention: 0d,
                wallMinimumSpeed: 25d);
            ThrusterBurstState ready = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Create(-12d, 0d),
                tuning);

            WallReflectionResult result = Reflect(ready, 1d, 0d, 0, tuning);

            Assert.That(result.State.Phase, Is.EqualTo(ThrusterBurstPhase.Ready));
            Assert.That(result.OutgoingSpeed, Is.EqualTo(tuning.BaseMaximumSpeed).Within(Tolerance));
            Assert.That(result.OutgoingVelocityX, Is.EqualTo(tuning.BaseMaximumSpeed).Within(Tolerance));
        }

        [Test]
        public void StationaryContact_DoesNotManufactureMinimumReflectionSpeed()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(wallMinimumSpeed: 10d);
            ThrusterBurstState ready = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Stationary,
                tuning);

            WallReflectionResult result = Reflect(ready, 1d, 0d, 0, tuning);

            Assert.That(result.Outcome, Is.EqualTo(WallReflectionOutcome.NoIncomingImpact));
            Assert.That(result.State, Is.SameAs(ready));
            Assert.That(result.OutgoingSpeed, Is.Zero);
        }

        [Test]
        public void Reflection_PreservesBurstPhaseAndElapsedSemantics()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            ThrusterBurstState burst = CreateBurstState(-1d, 0d, tuning);
            ThrusterBankState bank = ThrusterBankState.CreateFull(tuning);
            bool activated;
            ThrusterBankState nextBank;
            ThrusterBurstState advanced = ThrusterBurstStepper.Step(
                burst,
                bank,
                -1d,
                0d,
                0.1d,
                false,
                tuning,
                out nextBank,
                out activated);

            WallReflectionResult result = Reflect(advanced, 1d, 0d, 0, tuning);

            Assert.That(activated, Is.False);
            Assert.That(result.State.Phase, Is.EqualTo(advanced.Phase));
            Assert.That(result.State.BurstElapsedSeconds, Is.EqualTo(advanced.BurstElapsedSeconds));
            Assert.That(result.State.ExitElapsedSeconds, Is.EqualTo(advanced.ExitElapsedSeconds));
            Assert.That(result.State.ChainElapsedSeconds, Is.EqualTo(advanced.ChainElapsedSeconds));
        }

        [Test]
        public void StateCreatedForDifferentTuning_IsRejected()
        {
            MovementThrusterTuningProfile sourceTuning = BuildTuning(profileId: "tuning.mt-005-source");
            MovementThrusterTuningProfile otherTuning = BuildTuning(profileId: "tuning.mt-005-other");
            ThrusterBurstState burst = CreateBurstState(-1d, 0d, sourceTuning);

            Assert.Throws<InvalidOperationException>(
                () => WallReflectionPolicy.Reflect(
                    burst,
                    1d,
                    0d,
                    0d,
                    0d,
                    0,
                    otherTuning));
        }

        [Test]
        public void IdenticalInputs_ProduceEqualImmutableEngineFreeResults()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            ThrusterBurstState burst = CreateBurstState(-1d, 0d, tuning);

            WallReflectionResult first = WallReflectionPolicy.Reflect(
                burst,
                1d,
                0d,
                0d,
                1d,
                0,
                tuning);
            WallReflectionResult second = WallReflectionPolicy.Reflect(
                burst,
                1d,
                0d,
                0d,
                1d,
                0,
                tuning);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(typeof(WallReflectionResult).IsSealed, Is.True);
            Assert.That(
                typeof(WallReflectionResult)
                    .GetConstructors(BindingFlags.Instance | BindingFlags.Public),
                Is.Empty);
            Assert.That(
                typeof(WallReflectionResult)
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property => property.CanWrite),
                Is.Empty);
            Assert.That(
                typeof(WallReflectionPolicy).Assembly.GetReferencedAssemblies()
                    .Any(reference => reference.Name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                Is.False);
        }

        private static WallReflectionResult Reflect(
            ThrusterBurstState state,
            double normalX,
            double normalY,
            int contactsAlreadyProcessed,
            MovementThrusterTuningProfile tuning)
        {
            return WallReflectionPolicy.Reflect(
                state,
                normalX,
                normalY,
                0d,
                0d,
                contactsAlreadyProcessed,
                tuning);
        }

        private static ThrusterBurstState CreateBurstState(
            double directionX,
            double directionY,
            MovementThrusterTuningProfile tuning)
        {
            ThrusterBurstState ready = ThrusterBurstState.CreateReady(
                BaseLocomotionState.Stationary,
                tuning);
            ThrusterBankState bank = ThrusterBankState.CreateFull(tuning);
            bool activated;
            ThrusterBankState nextBank;
            ThrusterBurstState burst = ThrusterBurstStepper.TryActivate(
                ready,
                bank,
                directionX,
                directionY,
                tuning,
                out nextBank,
                out activated);

            Assert.That(activated, Is.True, "The test fixture must create an active burst state.");
            Assert.That(nextBank.AvailableCharges, Is.EqualTo(bank.AvailableCharges - 1));
            return burst;
        }

        private static MovementThrusterTuningProfile BuildTuning(
            string profileId = "tuning.mt-005-tests",
            double wallSpeedRetention = 0.8d,
            double wallInputInfluence = 0.15d,
            double wallMinimumSpeed = 5d,
            int wallMaximumContacts = 4)
        {
            return MovementThrusterTuningProfile.Create(
                MovementThrusterTuningProfile.CurrentProfileVersion,
                StableId.Parse(profileId),
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
                wallSpeedRetention,
                wallInputInfluence,
                wallMinimumSpeed,
                wallMaximumContacts,
                0.8d,
                0.9d,
                0.1d,
                0.5d,
                0.02d,
                128);
        }
    }
}
