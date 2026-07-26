using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.UI.ProductionFlow;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.ProductionFlow
{
    public sealed class FirstCombatRoomEnemyPublisherReconciliationV1Tests
    {
        [Test]
        public void AuthoritativelyEmptyRoomResolvesWithZeroPublishers()
        {
            string diagnostic;
            FirstCombatRoomEnemyPublisherResolutionStatusV1 status =
                FirstCombatRoomEnemyPublisherReconciliationV1.Classify(
                    0,
                    0,
                    false,
                    out diagnostic);

            Assert.That(
                status,
                Is.EqualTo(
                    FirstCombatRoomEnemyPublisherResolutionStatusV1.Ready));
            Assert.That(diagnostic, Is.Empty);
        }

        [Test]
        public void RoomExpectingOneEnemyRemainsPendingUntilPublisherBinds()
        {
            string diagnostic;
            FirstCombatRoomEnemyPublisherResolutionStatusV1 missing =
                FirstCombatRoomEnemyPublisherReconciliationV1.Classify(
                    1,
                    0,
                    false,
                    out diagnostic);

            Assert.That(
                missing,
                Is.EqualTo(
                    FirstCombatRoomEnemyPublisherResolutionStatusV1.Pending));
            Assert.That(
                diagnostic,
                Does.Contain("enemy-publisher-pending"));

            FirstCombatRoomEnemyPublisherResolutionStatusV1 unbound =
                FirstCombatRoomEnemyPublisherReconciliationV1.Classify(
                    1,
                    0,
                    true,
                    out diagnostic);

            Assert.That(
                unbound,
                Is.EqualTo(
                    FirstCombatRoomEnemyPublisherResolutionStatusV1.Pending));
            Assert.That(
                diagnostic,
                Does.Contain("binding-pending"));
        }

        [Test]
        public void RebuildFromZeroToOnePublisherSubscribesOnce()
        {
            GameObject firstObject = new GameObject("Publisher One");
            try
            {
                EnemyAttack2D first = firstObject.AddComponent<EnemyAttack2D>();
                var set = new FirstCombatRoomEnemyHitSubscriptionSetV1();

                set.Replace(
                    new List<EnemyAttack2D> { first },
                    (publisher, hit) => { });

                Assert.That(set.Count, Is.EqualTo(1));
                Assert.That(set.Contains(first), Is.True);

                set.Replace(
                    new List<EnemyAttack2D> { first },
                    (publisher, hit) => { });

                Assert.That(set.Count, Is.EqualTo(1));
                Assert.That(set.Contains(first), Is.True);
                set.Clear();
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
            }
        }

        [Test]
        public void RebuildFromOneToTwoRemovesStaleAndSubscribesTwice()
        {
            GameObject staleObject = new GameObject("Stale Publisher");
            GameObject secondObject = new GameObject("Publisher Two");
            GameObject thirdObject = new GameObject("Publisher Three");
            try
            {
                EnemyAttack2D stale = staleObject.AddComponent<EnemyAttack2D>();
                EnemyAttack2D second = secondObject.AddComponent<EnemyAttack2D>();
                EnemyAttack2D third = thirdObject.AddComponent<EnemyAttack2D>();
                var set = new FirstCombatRoomEnemyHitSubscriptionSetV1();

                set.Replace(
                    new List<EnemyAttack2D> { stale },
                    (publisher, hit) => { });
                set.Replace(
                    new List<EnemyAttack2D> { second, third },
                    (publisher, hit) => { });

                Assert.That(set.Count, Is.EqualTo(2));
                Assert.That(set.Contains(stale), Is.False);
                Assert.That(set.Contains(second), Is.True);
                Assert.That(set.Contains(third), Is.True);
                set.Clear();
            }
            finally
            {
                Object.DestroyImmediate(staleObject);
                Object.DestroyImmediate(secondObject);
                Object.DestroyImmediate(thirdObject);
            }
        }

        [Test]
        public void RebuildFromTwoBackToZeroLeavesNoStaleSubscriptions()
        {
            GameObject firstObject = new GameObject("Publisher One");
            GameObject secondObject = new GameObject("Publisher Two");
            try
            {
                EnemyAttack2D first = firstObject.AddComponent<EnemyAttack2D>();
                EnemyAttack2D second = secondObject.AddComponent<EnemyAttack2D>();
                var set = new FirstCombatRoomEnemyHitSubscriptionSetV1();

                set.Replace(
                    new List<EnemyAttack2D> { first, second },
                    (publisher, hit) => { });
                set.Replace(
                    new List<EnemyAttack2D>(),
                    (publisher, hit) => { });

                Assert.That(set.Count, Is.Zero);
                Assert.That(set.Contains(first), Is.False);
                Assert.That(set.Contains(second), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }
    }
}
