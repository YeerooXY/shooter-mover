using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ShooterMover.Editor.BalanceSimulator.Tests
{
    public sealed class StrongboxLevelComparisonWindowTests
    {
        [Test]
        public void SameTierAndLevelProduceSameQueueFingerprint()
        {
            var first = new StrongboxLevelQueueEntryV1(1, 1);
            var replay = new StrongboxLevelQueueEntryV1(1, 1);

            Assert.That(replay.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(replay.ToCanonicalString(), Is.EqualTo(first.ToCanonicalString()));
        }

        [Test]
        public void SameTierAtDifferentPlayerLevelsProducesDifferentInputIdentity()
        {
            var levelOne = new StrongboxLevelQueueEntryV1(1, 1);
            var levelTwo = new StrongboxLevelQueueEntryV1(1, 2);

            Assert.That(levelOne.Tier.TierStableId, Is.EqualTo(levelTwo.Tier.TierStableId));
            Assert.That(levelOne.PlayerLevel, Is.EqualTo(1));
            Assert.That(levelTwo.PlayerLevel, Is.EqualTo(2));
            Assert.That(levelTwo.Fingerprint, Is.Not.EqualTo(levelOne.Fingerprint));
            Assert.That(levelTwo.ToCanonicalString(), Is.Not.EqualTo(levelOne.ToCanonicalString()));
        }

        [Test]
        public void MixedLevelQueuePreservesInsertionOrder()
        {
            var queue = new List<StrongboxLevelQueueEntryV1>
            {
                new StrongboxLevelQueueEntryV1(1, 1),
                new StrongboxLevelQueueEntryV1(1, 2),
                new StrongboxLevelQueueEntryV1(11, 50),
            };

            Assert.That(queue[0].Tier.TierNumber, Is.EqualTo(1));
            Assert.That(queue[0].PlayerLevel, Is.EqualTo(1));
            Assert.That(queue[1].Tier.TierNumber, Is.EqualTo(1));
            Assert.That(queue[1].PlayerLevel, Is.EqualTo(2));
            Assert.That(queue[2].Tier.TierNumber, Is.EqualTo(11));
            Assert.That(queue[2].PlayerLevel, Is.EqualTo(50));
        }

        [Test]
        public void NegativePlayerLevelIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                new StrongboxLevelQueueEntryV1(1, -1);
            });
        }
    }
}
