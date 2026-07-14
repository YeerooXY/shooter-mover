using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;

namespace ShooterMover.Tests.EditMode.Movement
{
    public sealed class ThrusterBankStepperTests
    {
        [Test]
        public void CreateFull_UsesBaselineAndAuthoredAdditionalCapacityAndIsImmutable()
        {
            MovementThrusterTuningProfile tuning = BuildProfile(
                baselineChargeCount: 2,
                maximumAdditionalCharges: 1,
                rechargeSeconds: 2d);

            ThrusterBankState baseline = ThrusterBankState.CreateFull(tuning);
            ThrusterBankState authoredAdditional = ThrusterBankState.CreateFull(tuning, 1);

            Assert.That(baseline.BaselineChargeCount, Is.EqualTo(2));
            Assert.That(baseline.AdditionalChargeCount, Is.Zero);
            Assert.That(baseline.MaximumCharges, Is.EqualTo(2));
            Assert.That(baseline.AvailableCharges, Is.EqualTo(2));
            Assert.That(baseline.RegeneratingChargeCount, Is.Zero);
            Assert.That(baseline.IsActivationEligible, Is.True);
            Assert.That(baseline.IsFull, Is.True);
            Assert.That(baseline.RechargeSeconds, Is.EqualTo(2d));
            Assert.That(baseline.TuningIdentity, Is.EqualTo(tuning.DeterministicIdentity));

            Assert.That(authoredAdditional.AdditionalChargeCount, Is.EqualTo(1));
            Assert.That(authoredAdditional.MaximumCharges, Is.EqualTo(3));
            Assert.That(authoredAdditional.AvailableCharges, Is.EqualTo(3));

            Type stateType = typeof(ThrusterBankState);
            Assert.That(stateType.IsSealed, Is.True);
            Assert.That(
                stateType.GetConstructors(BindingFlags.Instance | BindingFlags.Public),
                Is.Empty);
            Assert.That(
                stateType
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property => property.CanWrite),
                Is.Empty);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => ThrusterBankState.CreateFull(tuning, -1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ThrusterBankState.CreateFull(tuning, 2));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ThrusterBankState.CreateFull(
                    BuildProfile(maximumAdditionalCharges: 0),
                    1));
        }

        [Test]
        public void TryConsume_StartsOrderedRechargeAndNeverUnderflows()
        {
            MovementThrusterTuningProfile tuning = BuildProfile(
                baselineChargeCount: 2,
                maximumAdditionalCharges: 1,
                rechargeSeconds: 2d);
            ThrusterBankState full = ThrusterBankState.CreateFull(tuning, 1);

            ThrusterBankState first = Consume(full, tuning);
            ThrusterBankState second = Consume(first, tuning);
            ThrusterBankState third = Consume(second, tuning);

            Assert.That(full.AvailableCharges, Is.EqualTo(3), "The immutable source state changed.");
            Assert.That(first.AvailableCharges, Is.EqualTo(2));
            Assert.That(second.AvailableCharges, Is.EqualTo(1));
            Assert.That(third.AvailableCharges, Is.Zero);
            Assert.That(third.RegeneratingChargeCount, Is.EqualTo(3));
            Assert.That(third.IsActivationEligible, Is.False);
            Assert.That(third.IsFull, Is.False);
            Assert.That(third.GetRechargeElapsedSeconds(0), Is.Zero);
            Assert.That(third.GetRechargeElapsedSeconds(1), Is.Zero);
            Assert.That(third.GetRechargeElapsedSeconds(2), Is.Zero);

            bool consumed;
            ThrusterBankState rejected = ThrusterBankStepper.TryConsume(third, tuning, out consumed);

            Assert.That(consumed, Is.False);
            Assert.That(rejected, Is.SameAs(third));
            Assert.That(rejected.AvailableCharges, Is.Zero);
            Assert.That(rejected.RegeneratingChargeCount, Is.EqualTo(3));
        }

        [Test]
        public void Regenerate_PreservesFractionalProgressAndCompletesAtExactBoundary()
        {
            MovementThrusterTuningProfile tuning = BuildProfile(
                baselineChargeCount: 1,
                maximumAdditionalCharges: 0,
                rechargeSeconds: 2d);
            ThrusterBankState empty = Consume(ThrusterBankState.CreateFull(tuning), tuning);

            ThrusterBankState partial = ThrusterBankStepper.Regenerate(empty, tuning, 0.5d);

            Assert.That(partial.AvailableCharges, Is.Zero);
            Assert.That(partial.RegeneratingChargeCount, Is.EqualTo(1));
            Assert.That(partial.GetRechargeElapsedSeconds(0), Is.EqualTo(0.5d));
            Assert.That(partial.GetRechargeFraction(0), Is.EqualTo(0.25d));
            Assert.That(partial.IsActivationEligible, Is.False);

            ThrusterBankState exactBoundary =
                ThrusterBankStepper.Regenerate(partial, tuning, 1.5d);

            Assert.That(exactBoundary.AvailableCharges, Is.EqualTo(1));
            Assert.That(exactBoundary.RegeneratingChargeCount, Is.Zero);
            Assert.That(exactBoundary.IsActivationEligible, Is.True);
            Assert.That(exactBoundary.IsFull, Is.True);
            Assert.That(partial.GetRechargeElapsedSeconds(0), Is.EqualTo(0.5d));
        }

        [Test]
        public void Regenerate_AdvancesIndependentTimersInConsumptionOrder()
        {
            MovementThrusterTuningProfile tuning = BuildProfile(
                baselineChargeCount: 2,
                maximumAdditionalCharges: 0,
                rechargeSeconds: 2d);
            ThrusterBankState bank = ThrusterBankState.CreateFull(tuning);

            bank = Consume(bank, tuning);
            bank = ThrusterBankStepper.Regenerate(bank, tuning, 0.5d);
            bank = Consume(bank, tuning);

            Assert.That(bank.AvailableCharges, Is.Zero);
            Assert.That(bank.RegeneratingChargeCount, Is.EqualTo(2));
            Assert.That(bank.GetRechargeElapsedSeconds(0), Is.EqualTo(0.5d));
            Assert.That(bank.GetRechargeElapsedSeconds(1), Is.Zero);

            ThrusterBankState firstRechargeCompletes =
                ThrusterBankStepper.Regenerate(bank, tuning, 1.5d);

            Assert.That(firstRechargeCompletes.AvailableCharges, Is.EqualTo(1));
            Assert.That(firstRechargeCompletes.RegeneratingChargeCount, Is.EqualTo(1));
            Assert.That(firstRechargeCompletes.GetRechargeElapsedSeconds(0), Is.EqualTo(1.5d));

            ThrusterBankState bothComplete =
                ThrusterBankStepper.Regenerate(firstRechargeCompletes, tuning, 0.5d);

            Assert.That(bothComplete.AvailableCharges, Is.EqualTo(2));
            Assert.That(bothComplete.RegeneratingChargeCount, Is.Zero);
            Assert.That(bothComplete.IsFull, Is.True);
        }

        [Test]
        public void Step_RegeneratesBeforeConsumeAtExactBoundary()
        {
            MovementThrusterTuningProfile tuning = BuildProfile(
                baselineChargeCount: 1,
                maximumAdditionalCharges: 0,
                rechargeSeconds: 2d);
            ThrusterBankState empty = Consume(ThrusterBankState.CreateFull(tuning), tuning);

            bool consumedBeforeBoundary;
            ThrusterBankState beforeBoundary = ThrusterBankStepper.Step(
                empty,
                tuning,
                1.5d,
                true,
                out consumedBeforeBoundary);

            Assert.That(consumedBeforeBoundary, Is.False);
            Assert.That(beforeBoundary.AvailableCharges, Is.Zero);
            Assert.That(beforeBoundary.GetRechargeElapsedSeconds(0), Is.EqualTo(1.5d));

            bool consumedOnBoundary;
            ThrusterBankState consumedAgain = ThrusterBankStepper.Step(
                beforeBoundary,
                tuning,
                0.5d,
                true,
                out consumedOnBoundary);

            Assert.That(consumedOnBoundary, Is.True);
            Assert.That(consumedAgain.AvailableCharges, Is.Zero);
            Assert.That(consumedAgain.RegeneratingChargeCount, Is.EqualTo(1));
            Assert.That(consumedAgain.GetRechargeElapsedSeconds(0), Is.Zero);
        }

        [Test]
        public void Regenerate_TimestepPartitionsProduceIdenticalState()
        {
            MovementThrusterTuningProfile tuning = BuildProfile(
                baselineChargeCount: 2,
                maximumAdditionalCharges: 0,
                rechargeSeconds: 2d);
            ThrusterBankState start = ThrusterBankState.CreateFull(tuning);
            start = Consume(start, tuning);
            start = ThrusterBankStepper.Regenerate(start, tuning, 0.5d);
            start = Consume(start, tuning);

            ThrusterBankState singleStep =
                ThrusterBankStepper.Regenerate(start, tuning, 1.5d);

            ThrusterBankState partitioned =
                ThrusterBankStepper.Regenerate(start, tuning, 0.25d);
            partitioned = ThrusterBankStepper.Regenerate(partitioned, tuning, 0.5d);
            partitioned = ThrusterBankStepper.Regenerate(partitioned, tuning, 0.75d);

            Assert.That(partitioned, Is.EqualTo(singleStep));
            Assert.That(partitioned == singleStep, Is.True);
            Assert.That(partitioned != singleStep, Is.False);
            Assert.That(partitioned.GetHashCode(), Is.EqualTo(singleStep.GetHashCode()));
            Assert.That(singleStep.AvailableCharges, Is.EqualTo(1));
            Assert.That(singleStep.GetRechargeElapsedSeconds(0), Is.EqualTo(1.5d));
        }

        [Test]
        public void Regenerate_ClampsAtCapacityAndHandlesVeryLargeTimestep()
        {
            MovementThrusterTuningProfile tuning = BuildProfile(
                baselineChargeCount: 2,
                maximumAdditionalCharges: 1,
                rechargeSeconds: 2d);
            ThrusterBankState full = ThrusterBankState.CreateFull(tuning, 1);

            ThrusterBankState unchangedFull =
                ThrusterBankStepper.Regenerate(full, tuning, double.MaxValue);
            ThrusterBankState depleted = Consume(Consume(full, tuning), tuning);
            ThrusterBankState refilled =
                ThrusterBankStepper.Regenerate(depleted, tuning, double.MaxValue);

            Assert.That(unchangedFull, Is.SameAs(full));
            Assert.That(refilled, Is.EqualTo(full));
            Assert.That(refilled.MaximumCharges, Is.EqualTo(3));
            Assert.That(refilled.AvailableCharges, Is.EqualTo(3));
            Assert.That(refilled.RegeneratingChargeCount, Is.Zero);
        }

        [Test]
        public void EquivalentTuningIsAcceptedAndMismatchedTuningIsRejected()
        {
            MovementThrusterTuningProfile original = BuildProfile();
            MovementThrusterTuningProfile equivalent = BuildProfile();
            MovementThrusterTuningProfile changed = BuildProfile(baseMaximumSpeed: 13d);
            ThrusterBankState state = ThrusterBankState.CreateFull(original);

            bool consumed;
            Assert.DoesNotThrow(
                () => state = ThrusterBankStepper.TryConsume(state, equivalent, out consumed));
            Assert.That(consumed, Is.True);

            Assert.Throws<InvalidOperationException>(
                () => ThrusterBankStepper.Regenerate(state, changed, 0.1d));
            Assert.Throws<InvalidOperationException>(
                () => ThrusterBankStepper.TryConsume(state, changed, out consumed));
        }

        [Test]
        public void InvalidStateTuningTimestepAndRechargeIndexAreRejected()
        {
            MovementThrusterTuningProfile tuning = BuildProfile();
            ThrusterBankState full = ThrusterBankState.CreateFull(tuning);
            bool consumed;

            Assert.Throws<ArgumentNullException>(
                () => ThrusterBankStepper.TryConsume(null, tuning, out consumed));
            Assert.Throws<ArgumentNullException>(
                () => ThrusterBankStepper.TryConsume(full, null, out consumed));
            Assert.Throws<ArgumentNullException>(
                () => ThrusterBankState.CreateFull(null));

            foreach (double invalid in new[]
            {
                -0.001d,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
            })
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => ThrusterBankStepper.Regenerate(full, tuning, invalid),
                    invalid.ToString());
            }

            Assert.Throws<ArgumentOutOfRangeException>(
                () => full.GetRechargeElapsedSeconds(-1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => full.GetRechargeElapsedSeconds(0));
        }

        [Test]
        public void ZeroTimestepWithoutConsumeRequestReturnsSameState()
        {
            MovementThrusterTuningProfile tuning = BuildProfile();
            ThrusterBankState state = Consume(ThrusterBankState.CreateFull(tuning), tuning);

            bool consumed;
            ThrusterBankState result =
                ThrusterBankStepper.Step(state, tuning, 0d, false, out consumed);

            Assert.That(consumed, Is.False);
            Assert.That(result, Is.SameAs(state));
        }

        private static ThrusterBankState Consume(
            ThrusterBankState state,
            MovementThrusterTuningProfile tuning)
        {
            bool consumed;
            ThrusterBankState next = ThrusterBankStepper.TryConsume(state, tuning, out consumed);
            Assert.That(consumed, Is.True);
            return next;
        }

        private static MovementThrusterTuningProfile BuildProfile(
            int baselineChargeCount = 2,
            int maximumAdditionalCharges = 1,
            double rechargeSeconds = 2d,
            double baseMaximumSpeed = 12d)
        {
            return MovementThrusterTuningProfile.Create(
                MovementThrusterTuningProfile.CurrentProfileVersion,
                StableId.Parse("tuning.mt-003-tests"),
                baseMaximumSpeed,
                50d,
                60d,
                90d,
                1.25d,
                baselineChargeCount,
                maximumAdditionalCharges,
                rechargeSeconds,
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
