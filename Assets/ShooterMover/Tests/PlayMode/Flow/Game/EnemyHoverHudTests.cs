using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.UI.Game;

namespace ShooterMover.Tests.PlayMode.Flow.Game
{
    public sealed class EnemyHoverHudTests
    {
        [TestCase("enemy.mobile-blaster-droid", "Mobile Blaster Droid")]
        [TestCase("enemy.ram-pouncer", "Ram Pouncer")]
        [TestCase("enemy.blaster-turret", "Blaster Turret")]
        [TestCase("enemy.pursuer-drone", "Pursuer Drone")]
        [TestCase("enemy.hybrid-sentinel", "Hybrid Sentinel")]
        public void FormatDroidName_HumanizesAuthoredEnemyIdentity(
            string definitionId,
            string expected)
        {
            Assert.That(
                EnemyHoverHud.FormatDroidName(StableId.Parse(definitionId)),
                Is.EqualTo(expected));
        }

        [Test]
        public void FormatDroidName_UsesSafeFallbackForMissingIdentity()
        {
            Assert.That(
                EnemyHoverHud.FormatDroidName(null),
                Is.EqualTo("Unknown Droid"));
        }
    }
}
