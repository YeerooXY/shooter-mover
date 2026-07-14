using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;

namespace ShooterMover.Tests.EditMode.Movement
{
    public sealed class MovementContactPolicyTests
    {
        private const double Tolerance = 0.000000001d;

        [Test]
        public void LightEnemy_Cs004SourceHeavierResultProducesBoundedShoveThrough()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            ThrusterBurstState movement = BuildMovement(tuning, 10d, 4d);
            int weightResultValue = (int)WeightMessage.DetermineResult(
                CombatWeightClass.Standard,
                CombatWeightClass.Light);

            MovementContactResolution resolved = MovementContactPolicy.Resolve(
                movement,
                weightResultValue,
                -1d,
                0d,
                tuning);

            Assert.That(weightResultValue, Is.EqualTo(MovementContactPolicy.SourceHeavierWeightResultValue));
            Assert.That(resolved.Outcome, Is.EqualTo(MovementContactOutcome.ShoveThrough));
            Assert.That(resolved.AllowsShoveThrough, Is.True);
            Assert.That(resolved.BlocksApproach, Is.False);
            Assert.That(resolved.VelocityX, Is.EqualTo(8d).Within(Tolerance));
            Assert.That(resolved.VelocityY, Is.EqualTo(3.6d).Within(Tolerance));
            Assert.That(movement.VelocityX, Is.EqualTo(10d), "The immutable MT-004 source state changed.");
            Assert.That(movement.VelocityY, Is.EqualTo(4d), "The immutable MT-004 source state changed.");
        }

        [Test]
        public void HeavyEqualAndImmovableEnemies_BlockApproachWithBoundedTangentialMomentum()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            ThrusterBurstState movement = BuildMovement(tuning, 10d, 4d);
            WeightResult[] contractResults =
            {
                WeightMessage.DetermineResult(CombatWeightClass.Standard, CombatWeightClass.Heavy),
                WeightMessage.DetermineResult(CombatWeightClass.Standard, CombatWeightClass.Standard),
                WeightMessage.DetermineResult(CombatWeightClass.Standard, CombatWeightClass.Immovable),
            };

            foreach (WeightResult contractResult in contractResults)
            {
                MovementContactResolution resolved = MovementContactPolicy.Resolve(
                    movement,
                    (int)contractResult,
                    -1d,
                    0d,
                    tuning);

                Assert.That(resolved.Outcome, Is.EqualTo(MovementContactOutcome.BlockedByWeight), contractResult.ToString());
                Assert.That(resolved.AllowsShoveThrough, Is.False, contractResult.ToString());
                Assert.That(resolved.BlocksApproach, Is.True, contractResult.ToString());
                Assert.That(resolved.VelocityX, Is.Zero.Within(Tolerance), contractResult.ToString());
                Assert.That(resolved.VelocityY, Is.EqualTo(0.4d).Within(Tolerance), contractResult.ToString());
            }
        }

        [Test]
        public void UnknownWeightResult_FailsClosedAndRemainsExplicit()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            ThrusterBurstState movement = BuildMovement(tuning, 10d, 4d);

            MovementContactResolution resolved = MovementContactPolicy.Resolve(
                movement,
                999,
                -1d,
                0d,
                tuning);

            Assert.That(resolved.Outcome, Is.EqualTo(MovementContactOutcome.UnknownWeightBlocked));
            Assert.That(resolved.BlocksApproach, Is.True);
            Assert.That(resolved.VelocityX, Is.Zero.Within(Tolerance));
            Assert.That(resolved.VelocityY, Is.EqualTo(0.4d).Within(Tolerance));
        }

        [Test]
        public void SeparatingContact_DoesNotInventASecondImpulse()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            ThrusterBurstState movement = BuildMovement(tuning, 10d, 4d);

            MovementContactResolution resolved = MovementContactPolicy.Resolve(
                movement,
                MovementContactPolicy.SourceLighterWeightResultValue,
                1d,
                0d,
                tuning);

            Assert.That(resolved.Outcome, Is.EqualTo(MovementContactOutcome.BlockedByWeight));
            Assert.That(resolved.VelocityX, Is.EqualTo(movement.VelocityX));
            Assert.That(resolved.VelocityY, Is.EqualTo(movement.VelocityY));
        }

        [Test]
        public void Policy_RejectsInvalidNormalAndMismatchedTuningIdentity()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            ThrusterBurstState movement = BuildMovement(tuning, 10d, 4d);
            MovementThrusterTuningProfile otherTuning = BuildTuning(
                profileId: StableId.Parse("tuning.mt-006-other"));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => MovementContactPolicy.Resolve(
                    movement,
                    MovementContactPolicy.SourceHeavierWeightResultValue,
                    0d,
                    0d,
                    tuning));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => MovementContactPolicy.Resolve(
                    movement,
                    MovementContactPolicy.SourceHeavierWeightResultValue,
                    2d,
                    0d,
                    tuning));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => MovementContactPolicy.Resolve(
                    movement,
                    MovementContactPolicy.SourceHeavierWeightResultValue,
                    double.NaN,
                    0d,
                    tuning));
            Assert.Throws<ArgumentException>(
                () => MovementContactPolicy.Resolve(
                    movement,
                    MovementContactPolicy.SourceHeavierWeightResultValue,
                    -1d,
                    0d,
                    otherTuning));
        }

        [Test]
        public void GraceTracker_DistinguishesSimultaneousDuplicateGraceAndOtherEnemy()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(
                graceSeconds: 0.5d,
                simultaneousWindowSeconds: 0.02d,
                capacity: 4);
            PerContactGraceTracker tracker = PerContactGraceTracker.Create(tuning);
            StableId firstEnemy = StableId.Parse("enemy.light-a");
            StableId secondEnemy = StableId.Parse("enemy.light-b");

            ContactGraceDecision firstDecision;
            tracker = tracker.Register(firstEnemy, 1d, out firstDecision);
            ContactGraceDecision duplicateDecision;
            tracker = tracker.Register(firstEnemy, 1.02d, out duplicateDecision);
            ContactGraceDecision graceDecision;
            tracker = tracker.Register(firstEnemy, 1.2d, out graceDecision);
            ContactGraceDecision otherDecision;
            tracker = tracker.Register(secondEnemy, 1.2d, out otherDecision);

            Assert.That(firstDecision, Is.EqualTo(ContactGraceDecision.Accepted));
            Assert.That(duplicateDecision, Is.EqualTo(ContactGraceDecision.DuplicateWithinSimultaneousWindow));
            Assert.That(graceDecision, Is.EqualTo(ContactGraceDecision.GraceActive));
            Assert.That(otherDecision, Is.EqualTo(ContactGraceDecision.Accepted));
            Assert.That(tracker.Count, Is.EqualTo(2));
            Assert.That(tracker.IsGraceActive(firstEnemy, 1.49d), Is.True);
            Assert.That(tracker.IsGraceActive(secondEnemy, 1.49d), Is.True);
        }

        [Test]
        public void GraceTracker_ExactExpiryBoundaryAcceptsAgain()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(
                graceSeconds: 0.5d,
                simultaneousWindowSeconds: 0.02d,
                capacity: 2);
            PerContactGraceTracker tracker = PerContactGraceTracker.Create(tuning);
            StableId enemyId = StableId.Parse("enemy.expiry-boundary");

            ContactGraceDecision firstDecision;
            tracker = tracker.Register(enemyId, 0d, out firstDecision);
            Assert.That(tracker.IsGraceActive(enemyId, 0.499d), Is.True);
            Assert.That(tracker.IsGraceActive(enemyId, 0.5d), Is.False);

            ContactGraceDecision boundaryDecision;
            tracker = tracker.Register(enemyId, 0.5d, out boundaryDecision);

            Assert.That(firstDecision, Is.EqualTo(ContactGraceDecision.Accepted));
            Assert.That(boundaryDecision, Is.EqualTo(ContactGraceDecision.Accepted));
            Assert.That(tracker.Count, Is.EqualTo(1));
            Assert.That(tracker.GetTrackedEnemyId(0), Is.EqualTo(enemyId));
            Assert.That(tracker.GetGraceExpiresAtSeconds(0), Is.EqualTo(1d).Within(Tolerance));
        }

        [Test]
        public void GraceTracker_PurgesExpiredEntriesBeforeCapacityCheck()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(
                graceSeconds: 0.5d,
                simultaneousWindowSeconds: 0.02d,
                capacity: 2);
            StableId first = StableId.Parse("enemy.first");
            StableId second = StableId.Parse("enemy.second");
            StableId replacement = StableId.Parse("enemy.replacement");
            PerContactGraceTracker tracker = PerContactGraceTracker.Create(tuning);
            ContactGraceRegistration[] initial;
            tracker = tracker.RegisterMany(new[] { first, second }, 0d, out initial);

            ContactGraceDecision replacementDecision;
            tracker = tracker.Register(replacement, 0.5d, out replacementDecision);

            Assert.That(initial.Select(item => item.Decision), Is.All.EqualTo(ContactGraceDecision.Accepted));
            Assert.That(replacementDecision, Is.EqualTo(ContactGraceDecision.Accepted));
            Assert.That(tracker.Count, Is.EqualTo(1));
            Assert.That(tracker.GetTrackedEnemyId(0), Is.EqualTo(replacement));
        }

        [Test]
        public void GraceTracker_SameStepDuplicatesAreCanonicalAndExplicit()
        {
            MovementThrusterTuningProfile tuning = BuildTuning(capacity: 4);
            StableId first = StableId.Parse("enemy.alpha");
            StableId second = StableId.Parse("enemy.beta");
            PerContactGraceTracker tracker = PerContactGraceTracker.Create(tuning);

            ContactGraceRegistration[] registrations;
            tracker = tracker.RegisterMany(
                new[] { second, first, second },
                0.25d,
                out registrations);

            Assert.That(registrations.Select(item => item.EnemyId), Is.EqualTo(new[] { first, second, second }));
            Assert.That(
                registrations.Select(item => item.Decision),
                Is.EqualTo(new[]
                {
                    ContactGraceDecision.Accepted,
                    ContactGraceDecision.Accepted,
                    ContactGraceDecision.DuplicateWithinSimultaneousWindow,
                }));
            Assert.That(tracker.Count, Is.EqualTo(2));
            Assert.That(tracker.GetTrackedEnemyId(0), Is.EqualTo(first));
            Assert.That(tracker.GetTrackedEnemyId(1), Is.EqualTo(second));
        }

        [Test]
        public void GraceTracker_ManySameStepContactsAreOrderIndependentAndCapacityBounded()
        {
            const int capacity = 16;
            const int contactCount = 64;
            MovementThrusterTuningProfile tuning = BuildTuning(capacity: capacity);
            StableId[] ascending = Enumerable.Range(0, contactCount)
                .Select(index => StableId.Create(
                    "enemy",
                    "contact-" + index.ToString("D3", CultureInfo.InvariantCulture)))
                .ToArray();
            StableId[] descending = ascending.Reverse().ToArray();

            ContactGraceRegistration[] ascendingRegistrations;
            PerContactGraceTracker ascendingTracker = PerContactGraceTracker.Create(tuning)
                .RegisterMany(ascending, 2d, out ascendingRegistrations);
            ContactGraceRegistration[] descendingRegistrations;
            PerContactGraceTracker descendingTracker = PerContactGraceTracker.Create(tuning)
                .RegisterMany(descending, 2d, out descendingRegistrations);

            Assert.That(ascendingTracker, Is.EqualTo(descendingTracker));
            Assert.That(ascendingTracker.GetHashCode(), Is.EqualTo(descendingTracker.GetHashCode()));
            Assert.That(ascendingRegistrations, Is.EqualTo(descendingRegistrations));
            Assert.That(ascendingTracker.Count, Is.EqualTo(capacity));
            Assert.That(
                ascendingRegistrations.Count(item => item.Decision == ContactGraceDecision.Accepted),
                Is.EqualTo(capacity));
            Assert.That(
                ascendingRegistrations.Count(item => item.Decision == ContactGraceDecision.CapacityRejected),
                Is.EqualTo(contactCount - capacity));
            Assert.That(ascendingTracker.GetTrackedEnemyId(0), Is.EqualTo(ascending[0]));
            Assert.That(ascendingTracker.GetTrackedEnemyId(capacity - 1), Is.EqualTo(ascending[capacity - 1]));
        }

        [Test]
        public void GraceTracker_RejectsBackwardNonFiniteAndNullInputs()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            PerContactGraceTracker tracker = PerContactGraceTracker.Create(tuning);
            StableId enemyId = StableId.Parse("enemy.validation");
            ContactGraceDecision decision;
            tracker = tracker.Register(enemyId, 1d, out decision);

            Assert.That(decision, Is.EqualTo(ContactGraceDecision.Accepted));
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Register(enemyId, 0.9d, out decision));
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Register(enemyId, double.NaN, out decision));
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Register(enemyId, double.PositiveInfinity, out decision));
            Assert.Throws<ArgumentException>(() => tracker.RegisterMany(new StableId[] { null }, 1d, out _));
            Assert.Throws<ArgumentNullException>(() => tracker.RegisterMany(null, 1d, out _));
        }

        [Test]
        public void ContactStateAndResults_AreSealedAndExposeNoPublicSetters()
        {
            Type[] immutableTypes =
            {
                typeof(MovementContactResolution),
                typeof(ContactGraceRegistration),
                typeof(PerContactGraceTracker),
            };

            foreach (Type type in immutableTypes)
            {
                Assert.That(type.IsSealed, Is.True, type.FullName);
                PropertyInfo[] mutableProperties = type
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property => property.CanWrite)
                    .ToArray();
                Assert.That(mutableProperties, Is.Empty, type.FullName);
            }
        }

        private static ThrusterBurstState BuildMovement(
            MovementThrusterTuningProfile tuning,
            double velocityX,
            double velocityY)
        {
            return ThrusterBurstState.CreateReady(
                BaseLocomotionState.Create(velocityX, velocityY),
                tuning);
        }

        private static MovementThrusterTuningProfile BuildTuning(
            StableId profileId = null,
            double lightMomentumRetention = 0.8d,
            double lightSteeringRetention = 0.9d,
            double heavyMomentumRetention = 0.1d,
            double graceSeconds = 0.5d,
            double simultaneousWindowSeconds = 0.02d,
            int capacity = 128)
        {
            return MovementThrusterTuningProfile.Create(
                MovementThrusterTuningProfile.CurrentProfileVersion,
                profileId ?? StableId.Parse("tuning.mt-006-contact"),
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
                lightMomentumRetention,
                lightSteeringRetention,
                heavyMomentumRetention,
                graceSeconds,
                simultaneousWindowSeconds,
                capacity);
        }
    }
}
