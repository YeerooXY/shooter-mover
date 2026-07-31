using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Tests.EditMode.Guns.Execution
{
    public sealed partial class GunExecutionCoreTests
    {
        [Test]
        public void MultiProjectileSpread_UsesDistinctDeterministicAngles()
        {
            var operationId = new FireOperationId(
                StableId.Parse("fire.sweeper-spread-regression"));
            var equipmentId = new EquipmentInstanceId(
                StableId.Parse("equipment-instance.sweeper-spread-regression"));
            var firstPass = new HashSet<string>();

            for (int index = 0; index < 3; index++)
            {
                GunVector2 direction = GunDeterministicSpread.DirectionFor(
                    new GunVector2(1d, 0d),
                    24d,
                    42UL,
                    operationId,
                    equipmentId,
                    0L,
                    new ProjectileOrdinal(index));
                firstPass.Add(direction.ToString());

                GunVector2 replay = GunDeterministicSpread.DirectionFor(
                    new GunVector2(1d, 0d),
                    24d,
                    42UL,
                    operationId,
                    equipmentId,
                    0L,
                    new ProjectileOrdinal(index));
                Assert.That(replay, Is.EqualTo(direction));
            }

            Assert.That(firstPass.Count, Is.EqualTo(3));
        }
    }
}
