using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UI.ProductionFlow;

namespace ShooterMover.Tests.EditMode.ProductionFlow
{
    public sealed class FirstCombatRoomCombatIntegrationV1Tests
    {
        [Test]
        public void KnownEnemyKineticChannelMapsExactly()
        {
            CombatChannel channel;
            bool mapped = FirstCombatRoomEnemyDamageChannelMapV1.TryMap(
                StableId.Parse("damage.kinetic"),
                out channel);

            Assert.That(mapped, Is.True);
            Assert.That(channel, Is.EqualTo(CombatChannel.Kinetic));
        }

        [Test]
        public void UnknownOrMissingEnemyChannelFailsClosed()
        {
            CombatChannel channel;
            bool unknownMapped = FirstCombatRoomEnemyDamageChannelMapV1.TryMap(
                StableId.Parse("damage.unmapped-test"),
                out channel);

            Assert.That(unknownMapped, Is.False);
            Assert.That(channel, Is.EqualTo(default(CombatChannel)));

            bool missingMapped = FirstCombatRoomEnemyDamageChannelMapV1.TryMap(
                null,
                out channel);

            Assert.That(missingMapped, Is.False);
            Assert.That(channel, Is.EqualTo(default(CombatChannel)));
        }
    }
}
