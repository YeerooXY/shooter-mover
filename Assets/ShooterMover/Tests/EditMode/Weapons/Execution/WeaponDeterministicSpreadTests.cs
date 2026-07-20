using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Tests.EditMode.Weapons.Execution
{
    public sealed partial class WeaponExecutionCoreTests
    {
        [Test]
        public void ShotgunPellets_FormDistinctOrderedFanAcrossConfiguredSpread()
        {
            WeaponDefinitionData definition = Definition(
                "weapon.shotgun",
                7,
                24d,
                2d);
            EquipmentInstance equipment = Equipment(
                "equipment-instance.shotgun-readable");
            Harness harness = HarnessFor(
                definition,
                new[] { equipment });

            Assert.That(
                harness.Core.TryExecute(
                    Command(
                        equipment,
                        "fire.shotgun-readable",
                        0L,
                        seed: 123UL)).Succeeded,
                Is.True);

            WeaponEffectBatch batch = harness.Sink.Batches[0];
            Assert.That(batch.EffectCount, Is.EqualTo(7));
            var angles = new List<double>(batch.EffectCount);
            var directionKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < batch.Effects.Count; index++)
            {
                DirectProjectileEffect pellet =
                    batch.Effects[index] as DirectProjectileEffect;
                Assert.That(pellet, Is.Not.Null);
                double angle = Math.Atan2(
                    pellet.Direction.Y,
                    pellet.Direction.X) * 180d / Math.PI;
                angles.Add(angle);
                directionKeys.Add(
                    pellet.Direction.X.ToString("R")
                    + "|"
                    + pellet.Direction.Y.ToString("R"));
                Assert.That(angle, Is.InRange(-12.000001d, 12.000001d));
            }

            Assert.That(directionKeys.Count, Is.EqualTo(7));
            Assert.That(angles[0], Is.LessThanOrEqualTo(-11d));
            Assert.That(angles[angles.Count - 1], Is.GreaterThanOrEqualTo(11d));
            for (int index = 1; index < angles.Count; index++)
            {
                Assert.That(
                    angles[index],
                    Is.GreaterThan(angles[index - 1] + 3d),
                    "Pellet lanes must remain visibly separated and ordered.");
            }
        }

        [Test]
        public void SameShotgunCommand_RebuildsByteEquivalentDistinctFan()
        {
            WeaponDefinitionData definition = Definition(
                "weapon.shotgun",
                7,
                24d,
                2d);
            EquipmentInstance equipment = Equipment(
                "equipment-instance.shotgun-repeatable-fan");
            RecordingSink sink = new RecordingSink { Reject = true };
            Harness harness = HarnessFor(
                definition,
                new[] { equipment },
                sink: sink);
            var command = Command(
                equipment,
                "fire.shotgun-repeatable-fan",
                0L,
                seed: 987654321UL);

            Assert.That(
                harness.Core.TryExecute(command).Status,
                Is.EqualTo(WeaponExecutionStatus.SinkRejected));
            Assert.That(
                harness.Core.TryExecute(command).Status,
                Is.EqualTo(WeaponExecutionStatus.SinkRejected));
            Assert.That(sink.Batches.Count, Is.EqualTo(2));
            Assert.That(
                sink.Batches[0].Fingerprint,
                Is.EqualTo(sink.Batches[1].Fingerprint));

            var firstDirections = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0;
                index < sink.Batches[0].Effects.Count;
                index++)
            {
                DirectProjectileEffect pellet =
                    (DirectProjectileEffect)sink.Batches[0].Effects[index];
                firstDirections.Add(
                    pellet.Direction.X.ToString("R")
                    + "|"
                    + pellet.Direction.Y.ToString("R"));
            }
            Assert.That(firstDirections.Count, Is.EqualTo(7));
        }
    }
}
