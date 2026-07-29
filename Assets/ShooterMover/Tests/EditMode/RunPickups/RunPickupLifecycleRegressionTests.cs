#if UNITY_EDITOR
using NUnit.Framework;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.RunPickups;

namespace ShooterMover.Tests.EditMode.RunPickups
{
    public sealed partial class RunLocalPickupStateTests
    {
        [Test]
        public void RestartedLifecycle_FirstCollectionUsesOrderOne()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot lifecycleOnePickup = RealizeOne(fixture);
            RunPickupCollectionResult lifecycleOneCollection =
                fixture.Authority.Collect(Command(lifecycleOnePickup));
            Assert.That(lifecycleOneCollection.Status,
                Is.EqualTo(RunPickupCollectionStatus.Collected));
            Assert.That(lifecycleOneCollection.CollectionFact.CollectionOrder,
                Is.EqualTo(1L));

            fixture.Session.LifecycleGeneration = 2L;
            RunPickupGeneratedBatch lifecycleTwoBatch = BatchForLifecycle(
                2L,
                new[]
                {
                    Child(
                        "money-lifecycle-two",
                        RewardGrantKind.Money,
                        "credits",
                        7L),
                },
                "drop-operation-lifecycle-two",
                "batch-lifecycle-two");
            RunPickupRealizationResult realized =
                fixture.Authority.Realize(lifecycleTwoBatch);
            Assert.That(realized.Status,
                Is.EqualTo(RunPickupRealizationStatus.Realized));

            RunPickupCollectionResult lifecycleTwoCollection =
                fixture.Authority.Collect(Command(realized.Pickups[0]));

            Assert.That(lifecycleTwoCollection.Status,
                Is.EqualTo(RunPickupCollectionStatus.Collected));
            Assert.That(lifecycleTwoCollection.CollectionFact.CollectionOrder,
                Is.EqualTo(1L));
            Assert.That(lifecycleTwoCollection.Pickup.CollectionOrder,
                Is.EqualTo(1L));
        }

        [Test]
        public void Realize_WhenRunSessionContextThrows_RejectsWithoutMutationAndCanRetry()
        {
            Fixture fixture = CreateFixture();
            RunPickupGeneratedBatch batch = Batch(
                Child("context-retry", RewardGrantKind.Scrap, "scrap", 3L));
            fixture.Session.ThrowOnContextRead = true;

            RunPickupRealizationResult rejected = fixture.Authority.Realize(batch);
            fixture.Session.ThrowOnContextRead = false;
            RunPickupRealizationResult retried = fixture.Authority.Realize(batch);

            Assert.That(rejected.Status,
                Is.EqualTo(RunPickupRealizationStatus.Rejected));
            Assert.That(rejected.Diagnostic,
                Does.StartWith("run-pickup-session-context-exception:"));
            Assert.That(retried.Status,
                Is.EqualTo(RunPickupRealizationStatus.Realized));
            Assert.That(fixture.Authority.PickupCount, Is.EqualTo(1));
        }

        [Test]
        public void Collect_WhenRunSessionContextUnavailable_LeavesPickupRetryable()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot pickup = RealizeOne(fixture);
            RunPickupCollectionCommand command = Command(pickup);
            fixture.Session.ContextAvailable = false;

            RunPickupCollectionResult rejected = fixture.Authority.Collect(command);
            fixture.Session.ContextAvailable = true;
            RunPickupCollectionResult retried = fixture.Authority.Collect(command);

            Assert.That(rejected.Status,
                Is.EqualTo(RunPickupCollectionStatus.Rejected));
            Assert.That(rejected.Diagnostic,
                Is.EqualTo("fake-session-context-unavailable"));
            Assert.That(retried.Status,
                Is.EqualTo(RunPickupCollectionStatus.Collected));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(1));
        }
    }
}
#endif
