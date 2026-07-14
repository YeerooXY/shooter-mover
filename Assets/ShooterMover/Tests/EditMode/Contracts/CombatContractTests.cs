using System;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Contracts
{
    public sealed class CombatContractTests
    {
        [Test]
        public void DamageMessage_ShieldOverflow_IsExplicitAndConsistent()
        {
            VitalState before = new VitalState(20d, 20d, 5d, 5d);
            VitalState after = new VitalState(12d, 20d, 0d, 5d);

            var message = new DamageMessage(
                EventId("shield-overflow"),
                SourceId,
                TargetId,
                CombatChannel.Kinetic,
                13d,
                DamageResult.Applied,
                before,
                after,
                5d,
                8d,
                8d,
                0d);

            Assert.That(message.ShieldDamageApplied, Is.EqualTo(5d));
            Assert.That(message.ShieldOverflowAmount, Is.EqualTo(8d));
            Assert.That(message.HealthDamageApplied, Is.EqualTo(8d));
            Assert.That(message.UnappliedAmount, Is.Zero);
            Assert.That(message.Before, Is.EqualTo(before));
            Assert.That(message.After, Is.EqualTo(after));
        }

        [Test]
        public void DamageMessage_Overkill_ReportsUnappliedOverflow()
        {
            VitalState before = new VitalState(20d, 20d, 5d, 5d);
            VitalState after = new VitalState(0d, 20d, 0d, 5d);

            var message = new DamageMessage(
                EventId("overkill"),
                SourceId,
                TargetId,
                CombatChannel.Explosive,
                30d,
                DamageResult.Applied,
                before,
                after,
                5d,
                25d,
                20d,
                5d);

            Assert.That(message.Result, Is.EqualTo(DamageResult.Applied));
            Assert.That(message.After.IsDestroyed, Is.True);
            Assert.That(message.UnappliedAmount, Is.EqualTo(5d));
        }

        [Test]
        public void CombatEventIdentity_SameEnvelope_IsDuplicate()
        {
            StableId eventId = EventId("duplicate-hit");
            var first = new HitMessage(
                eventId,
                SourceId,
                TargetId,
                CombatChannel.Kinetic,
                HitResult.Confirmed);
            var retry = new HitMessage(
                StableId.Parse(eventId.ToString()),
                StableId.Parse(SourceId.ToString()),
                StableId.Parse(TargetId.ToString()),
                CombatChannel.Kinetic,
                HitResult.Confirmed);

            Assert.That(
                CombatEventIdentity.Classify(first, retry),
                Is.EqualTo(CombatEventIdentityResult.Duplicate));
        }

        [Test]
        public void CombatEventIdentity_ReusedIdWithDifferentEnvelope_IsConflict()
        {
            StableId eventId = EventId("conflicting-hit");
            var first = new HitMessage(
                eventId,
                SourceId,
                TargetId,
                CombatChannel.Kinetic,
                HitResult.Confirmed);
            var conflicting = new HitMessage(
                eventId,
                SourceId,
                StableId.Parse("enemy.ram-droid"),
                CombatChannel.Kinetic,
                HitResult.Confirmed);

            Assert.That(
                CombatEventIdentity.Classify(first, conflicting),
                Is.EqualTo(CombatEventIdentityResult.ConflictingDuplicate));
        }

        [TestCase(ContactClassification.BodyImpact)]
        [TestCase(ContactClassification.SustainedBodyContact)]
        [TestCase(ContactClassification.ProjectileImpact)]
        [TestCase(ContactClassification.AreaOverlap)]
        [TestCase(ContactClassification.HazardOverlap)]
        public void ContactMessage_AllV1ContactClasses_AreAccepted(
            ContactClassification classification)
        {
            var message = new ContactMessage(
                EventId("contact-class"),
                SourceId,
                TargetId,
                CombatChannel.Contact,
                classification,
                ContactResult.Accepted);

            Assert.That(message.Classification, Is.EqualTo(classification));
            Assert.That(message.Result, Is.EqualTo(ContactResult.Accepted));
        }

        [Test]
        public void WeightMessage_ValidatesTheDeclaredComparison()
        {
            var message = new WeightMessage(
                EventId("weight"),
                SourceId,
                TargetId,
                CombatChannel.Contact,
                CombatWeightClass.Light,
                CombatWeightClass.Heavy,
                WeightResult.SourceLighter);

            Assert.That(message.Result, Is.EqualTo(WeightResult.SourceLighter));
            Assert.That(
                WeightMessage.DetermineResult(
                    CombatWeightClass.Heavy,
                    CombatWeightClass.Immovable),
                Is.EqualTo(WeightResult.TargetImmovable));

            Assert.Throws<ArgumentException>(() => new WeightMessage(
                EventId("weight-conflict"),
                SourceId,
                TargetId,
                CombatChannel.Contact,
                CombatWeightClass.Light,
                CombatWeightClass.Heavy,
                WeightResult.SourceHeavier));
        }

        [Test]
        public void VitalState_RejectsNegativeNonFiniteAndAmbiguousValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new VitalState(-1d, 10d, 0d, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new VitalState(double.NaN, 10d, 0d, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new VitalState(1d, double.PositiveInfinity, 0d, 0d));
            Assert.Throws<ArgumentException>(
                () => new VitalState(11d, 10d, 0d, 0d));
            Assert.Throws<ArgumentException>(
                () => new VitalState(10d, 10d, 2d, 1d));
            Assert.Throws<ArgumentException>(
                () => new VitalState(0d, 10d, 1d, 1d));
        }

        [Test]
        public void DamageMessage_RejectsNonFiniteNegativeAndContradictoryResults()
        {
            VitalState before = new VitalState(20d, 20d, 5d, 5d);
            VitalState after = new VitalState(12d, 20d, 0d, 5d);

            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageMessage(
                EventId("negative-damage"),
                SourceId,
                TargetId,
                CombatChannel.Kinetic,
                -1d,
                DamageResult.Applied,
                before,
                after,
                5d,
                8d,
                8d,
                0d));

            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageMessage(
                EventId("infinite-damage"),
                SourceId,
                TargetId,
                CombatChannel.Kinetic,
                double.PositiveInfinity,
                DamageResult.Applied,
                before,
                after,
                5d,
                8d,
                8d,
                0d));

            Assert.Throws<ArgumentException>(() => new DamageMessage(
                EventId("contradictory-overflow"),
                SourceId,
                TargetId,
                CombatChannel.Kinetic,
                13d,
                DamageResult.Applied,
                before,
                after,
                5d,
                7d,
                8d,
                0d));
        }

        [Test]
        public void UnknownChannelAndUnknownStatus_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HitMessage(
                EventId("unknown-channel"),
                SourceId,
                TargetId,
                (CombatChannel)999,
                HitResult.Confirmed));

            Assert.Throws<ArgumentOutOfRangeException>(() => new StatusMessage(
                EventId("unknown-status"),
                SourceId,
                TargetId,
                CombatChannel.Thermal,
                (CombatStatus)999,
                StatusResult.Applied,
                2d,
                1d));

            Assert.Throws<ArgumentOutOfRangeException>(() => new StatusMessage(
                EventId("unknown-status-result"),
                SourceId,
                TargetId,
                CombatChannel.Thermal,
                CombatStatus.Burning,
                (StatusResult)999,
                0d,
                0d));
        }

        [Test]
        public void LateHit_TargetAlreadyDestroyed_IsExplicitAndDoesNotChangeVitals()
        {
            VitalState destroyed = new VitalState(0d, 20d, 0d, 5d);

            var message = new DamageMessage(
                EventId("late-hit"),
                SourceId,
                TargetId,
                CombatChannel.Kinetic,
                4d,
                DamageResult.TargetAlreadyDestroyed,
                destroyed,
                destroyed,
                0d,
                0d,
                0d,
                4d);

            Assert.That(message.Result, Is.EqualTo(DamageResult.TargetAlreadyDestroyed));
            Assert.That(message.Before, Is.EqualTo(message.After));
            Assert.That(message.UnappliedAmount, Is.EqualTo(message.RequestedAmount));
        }

        [Test]
        public void TargetAlreadyDestroyed_RejectsAnActiveBeforeState()
        {
            VitalState active = new VitalState(20d, 20d, 0d, 5d);

            Assert.Throws<ArgumentException>(() => new DamageMessage(
                EventId("false-late-hit"),
                SourceId,
                TargetId,
                CombatChannel.Kinetic,
                4d,
                DamageResult.TargetAlreadyDestroyed,
                active,
                active,
                0d,
                0d,
                0d,
                4d));
        }

        [Test]
        public void StatusMessage_ValidatesResultShape()
        {
            var applied = new StatusMessage(
                EventId("status-applied"),
                SourceId,
                TargetId,
                CombatChannel.Electrical,
                CombatStatus.Stunned,
                StatusResult.Applied,
                2d,
                0d);

            Assert.That(applied.DurationSeconds, Is.EqualTo(2d));

            Assert.Throws<ArgumentException>(() => new StatusMessage(
                EventId("status-zero-duration"),
                SourceId,
                TargetId,
                CombatChannel.Electrical,
                CombatStatus.Stunned,
                StatusResult.Applied,
                0d,
                0d));

            Assert.Throws<ArgumentException>(() => new StatusMessage(
                EventId("status-removed-payload"),
                SourceId,
                TargetId,
                CombatChannel.Electrical,
                CombatStatus.Stunned,
                StatusResult.Removed,
                1d,
                0d));
        }

        [Test]
        public void VitalMessage_ResultMustMatchTheSnapshot()
        {
            VitalState active = new VitalState(10d, 10d, 0d, 0d);
            VitalState destroyed = new VitalState(0d, 10d, 0d, 0d);

            Assert.That(
                new VitalMessage(
                    EventId("vital-active"),
                    TargetId,
                    TargetId,
                    CombatChannel.System,
                    VitalResult.Active,
                    active).Result,
                Is.EqualTo(VitalResult.Active));

            Assert.Throws<ArgumentException>(() => new VitalMessage(
                EventId("vital-contradiction"),
                TargetId,
                TargetId,
                CombatChannel.System,
                VitalResult.Active,
                destroyed));
        }

        [Test]
        public void PublicContractValues_AreImmutable()
        {
            Type[] immutableTypes =
            {
                typeof(VitalState),
                typeof(DamageMessage),
                typeof(HitMessage),
                typeof(VitalMessage),
                typeof(ContactMessage),
                typeof(WeightMessage),
                typeof(StatusMessage),
            };

            foreach (Type type in immutableTypes)
            {
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.That(
                        property.CanWrite,
                        Is.False,
                        $"{type.Name}.{property.Name} must not expose a setter.");
                }
            }
        }

        [Test]
        public void ContractsAssembly_DoesNotReferenceUnityEngine()
        {
            AssemblyName[] references = typeof(DamageMessage).Assembly.GetReferencedAssemblies();

            foreach (AssemblyName reference in references)
            {
                Assert.That(reference.Name, Does.Not.StartWith("UnityEngine"));
            }
        }

        private static StableId SourceId => StableId.Parse("weapon.blaster-machine-gun");

        private static StableId TargetId => StableId.Parse("enemy.pursuer-drone");

        private static StableId EventId(string value)
        {
            return StableId.Create("event", value);
        }
    }
}
