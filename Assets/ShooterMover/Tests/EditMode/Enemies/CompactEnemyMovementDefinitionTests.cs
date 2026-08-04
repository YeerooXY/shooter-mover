using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Enemies;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class CompactEnemyMovementDefinitionTests
    {
        [Test]
        public void GunnerDroidPursuesInsteadOfOrbiting()
        {
            CompactEnemyDefinition definition;

            Assert.That(
                CompactEnemyCatalog.TryResolve(
                    StableId.Parse("enemy.gunner-droid"),
                    out definition),
                Is.True);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.movement, Is.Not.Null);
            Assert.That(definition.movement.kind, Is.EqualTo("direct"));
            Assert.That(definition.movement.speed, Is.EqualTo(3.5d));
        }
    }
}
