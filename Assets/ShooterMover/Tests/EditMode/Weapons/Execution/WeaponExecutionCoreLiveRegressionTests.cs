using NUnit.Framework;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Tests.EditMode.Weapons.Execution
{
    public sealed partial class WeaponExecutionCoreTests
    {
        [Test]
        public void AcceptedOperationId_IsGlobalAcrossConcreteEquipmentInstances()
        {
            WeaponDefinitionData definition = Definition(
                "weapon.rifle",
                1,
                0d,
                5d);
            EquipmentInstance first = Equipment("equipment-instance.global-operation-a");
            EquipmentInstance second = Equipment("equipment-instance.global-operation-b");
            Harness harness = HarnessFor(definition, new[] { first, second });
            const string operation = "fire.global-operation";

            Assert.That(
                harness.Core.TryExecute(
                    Command(first, operation, 0L)).Status,
                Is.EqualTo(WeaponExecutionStatus.Accepted));
            Assert.That(
                harness.Core.TryExecute(
                    Command(second, operation, 0L)).Status,
                Is.EqualTo(WeaponExecutionStatus.ConflictingDuplicate));
            Assert.That(harness.Sink.Batches.Count, Is.EqualTo(1));
        }

        [Test]
        public void DamageOverTimePool_IsCanonicalValidatedAndFingerprinted()
        {
            WeaponDefinitionData definition = SupportedDamageOverTimeDefinition();
            EquipmentInstance equipment = Equipment("equipment-instance.dot-pool");
            Harness harness = HarnessFor(definition, new[] { equipment });

            WeaponExecutionResult result = harness.Core.TryExecute(
                Command(equipment, "fire.dot-pool", 0L));

            Assert.That(result.Status, Is.EqualTo(WeaponExecutionStatus.Accepted));
            Assert.That(harness.Sink.Batches[0].EffectCount, Is.EqualTo(4));
            var effect = (DamageOverTimeProjectileEffect)
                harness.Sink.Batches[0].Effects[0];
            Assert.That(effect.DotDps, Is.EqualTo(4d));
            Assert.That(effect.DotDuration, Is.EqualTo(2d));
            Assert.That(effect.PoolRadius, Is.EqualTo(2d));
            Assert.That(effect.PoolDuration, Is.EqualTo(3d));
            Assert.That(
                harness.Sink.Batches[0].CanonicalText,
                Does.Contain("|4|2|2|3|"));
        }

        private static WeaponDefinitionData SupportedDamageOverTimeDefinition()
        {
            return new WeaponDefinitionData(
                "weapon.flamethrower",
                "Flamethrower",
                "test-family",
                1,
                "Thermal",
                "Test",
                "Universal",
                1,
                1,
                1,
                "Common",
                1000d,
                1d,
                1000d,
                4d,
                13d,
                "Standard",
                false,
                "Standard",
                1d,
                100d,
                10d,
                0.2d,
                0d,
                0.8d,
                5d,
                4,
                1,
                1d,
                12d,
                10d,
                8d,
                0,
                0d,
                0d,
                4d,
                2d,
                2d,
                3d,
                0,
                0d,
                0.5d,
                1d,
                0d,
                "Burning pool",
                "Test",
                WeaponCatalogAvailability.Live,
                new string[0]);
        }
    }
}
