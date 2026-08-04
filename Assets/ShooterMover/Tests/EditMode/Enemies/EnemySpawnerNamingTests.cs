using System;
using NUnit.Framework;
using ShooterMover.UnityAdapters.Enemies;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class EnemySpawnerNamingTests
    {
        [Test]
        public void ExposesGameFacingSetupNames()
        {
            Type type = typeof(EnemySpawner);

            Assert.That(type.GetMethod("SetSetup"), Is.Not.Null);
            Assert.That(type.GetMethod("SetCollisionRules"), Is.Not.Null);
            Assert.That(type.GetMethod("SetUp"), Is.Not.Null);
            Assert.That(type.GetMethod("Register"), Is.Null);
            Assert.That(type.GetMethod("Configure"), Is.Null);
        }

        [Test]
        public void KeepsOldFactoryAsObsoleteBridgeOnly()
        {
#pragma warning disable 618
            Type legacy = typeof(CompactEnemySceneFactory);
#pragma warning restore 618

            Assert.That(
                legacy.GetCustomAttributes(typeof(ObsoleteAttribute), false),
                Is.Not.Empty);
        }
    }
}
