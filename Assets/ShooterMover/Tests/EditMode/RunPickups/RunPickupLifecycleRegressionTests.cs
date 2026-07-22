#if UNITY_EDITOR
using NUnit.Framework;
using ShooterMover.Contracts.Rewards;
using ShooterMover.RunPickups;

namespace ShooterMover.Tests.EditMode.RunPickups
{
    public sealed partial class RunLocalPickupAuthorityV1Tests
    {
        [Test]
        public void RestartedLifecycle_FirstCollectionUsesOrderOne()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshotV1 lifecycleOnePickup = RealizeOne(fixture);
            RunPickupCollectionResultV1 lifecycleOneCollection =
                fixture.Authority.Collect(Command(lifecycleOnePickup));
            Assert.That(lifecycleOneCollection.Status,
                Is.EqualTo(RunPickupCollectionStatusV1.Collected));
            Assert.That(lifecycleOneCollection.CollectionFact.CollectionOrder,
                Is.EqualTo(1L));

            fixture.Session.LifecycleGeneration = 2L;
            RunPickupGeneratedBatchV1 lifecycleTwoBatch = BatchForLifecycle(
                2L,
                new[]
                {
                    Child(
                        "money-lifecycle-two",
                        RewardGrantKindV1.Money,
                        "credits",
                        7L),
                },
                "drop-operation-lifecycle-two",
                "batch-lifecycle-two");
            RunPickupRealizationResultV1 realized =
                fixture.Authority.Realize(lifecycleTwoBatch);
            Assert.That(realized.Status,
                Is.EqualTo(RunPickupRealizationStatusV1.Realized));

            RunPickupCollectionResultV1 lifecycleTwoCollection =
                fixture.Authority.Collect(Command(realized.Pickups[0]));

            Assert.That(lifecycleTwoCollection.Status,
                Is.EqualTo(RunPickupCollectionStatusV1.Collected));
            Assert.That(lifecycleTwoCollection.CollectionFact.CollectionOrder,
                Is.EqualTo(1L));
            Assert.That(lifecycleTwoCollection.Pickup.CollectionOrder,
                Is.EqualTo(1L));
        }

        [Test]
        public void Realize_WhenRunSessionContextThrows_RejectsWithoutMutationAndCanRetry()
        {
            Fixture fixture = CreateFixture();
            RunPickupGeneratedBatchV1 batch = Batch(
                Child("context-retry", RewardGrantKindV1.Scrap, "scrap", 3L));
            fixture.Session.ThrowOnContextRead = true;

            RunPickupRealizationResultV1 rejected = fixture.Authority.Realize(batch);
            fixture.Session.ThrowOnContextRead = false;
            RunPickupRealizationResultV1 retried = fixture.Authority.Realize(batch);

            Assert.That(rejected.Status,
                Is.EqualTo(RunPickupRealizationStatusV1.Rejected));
            Assert.That(rejected.Diagnostic,
                Does.StartWith("run-pickup-session-context-exception:"));
            Assert.That(retried.Status,
                Is.EqualTo(RunPickupRealizationStatusV1.Realized));
            Assert.That(fixture.Authority.PickupCount, Is.EqualTo(1));
        }

        [Test]
        public void Collect_WhenRunSessionContextUnavailable_LeavesPickupRetryable()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshotV1 pickup = RealizeOne(fixture);
            RunPickupCollectionCommandV1 command = Command(pickup);
            fixture.Session.ContextAvailable = false;

            RunPickupCollectionResultV1 rejected = fixture.Authority.Collect(command);
            fixture.Session.ContextAvailable = true;
            RunPickupCollectionResultV1 retried = fixture.Authority.Collect(command);

            Assert.That(rejected.Status,
                Is.EqualTo(RunPickupCollectionStatusV1.Rejected));
            Assert.That(rejected.Diagnostic,
                Is.EqualTo("fake-session-context-unavailable"));
            Assert.That(retried.Status,
                Is.EqualTo(RunPickupCollectionStatusV1.Collected));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(1));
        }
    }
}
#endif
