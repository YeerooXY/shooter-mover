#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.RunPickups;
using ShooterMover.TerminalDropBinding;

namespace ShooterMover.Tests.EditMode.RunPickups
{
    public sealed partial class RunLocalPickupAuthorityV1Tests
    {
        [Test]
        public void ConflictingCollectionOperationReuse_Rejects()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshotV1 pickup = RealizeOne(fixture);
            RunPickupCollectionCommandV1 first = Command(pickup);
            fixture.Authority.Collect(first);
            var conflict = new RunPickupCollectionCommandV1(
                first.CollectionOperationStableId,
                pickup.PickupStableId,
                pickup.Reward.RewardInstanceStableId,
                RunId,
                1L,
                PlayerActorId,
                PlayerParticipantId,
                "different-fingerprint");

            RunPickupCollectionResultV1 result = fixture.Authority.Collect(conflict);

            Assert.That(result.Status, Is.EqualTo(RunPickupCollectionStatusV1.ConflictingDuplicate));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(1));
        }

        [Test]
        public void TwoCollisionDeliveries_CannotCollectTwice()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshotV1 pickup = RealizeOne(fixture);
            RunPickupCollectionCommandV1 command = Command(pickup);
            RunPickupCollectionResultV1 left = null;
            RunPickupCollectionResultV1 right = null;

            Parallel.Invoke(
                () => left = fixture.Authority.Collect(command),
                () => right = fixture.Authority.Collect(command));

            CollectionAssert.AreEquivalent(
                new[]
                {
                    RunPickupCollectionStatusV1.Collected,
                    RunPickupCollectionStatusV1.ExactReplay
                },
                new[] { left.Status, right.Status });
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(1));
            Assert.That(fixture.Authority.CollectedPickupCount, Is.EqualTo(1));
        }

        [Test]
        public void WrongRun_Rejects()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshotV1 pickup = RealizeOne(fixture);
            RunPickupCollectionCommandV1 command = Command(
                pickup,
                runId: Id("run", "wrong"));

            Assert.That(fixture.Authority.Collect(command).Status,
                Is.EqualTo(RunPickupCollectionStatusV1.WrongRun));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(0));
        }

        [Test]
        public void StaleRunSessionLifecycle_Rejects()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshotV1 pickup = RealizeOne(fixture);
            fixture.Session.LifecycleGeneration = 2L;

            RunPickupCollectionResultV1 result = fixture.Authority.Collect(Command(pickup));

            Assert.That(result.Status, Is.EqualTo(RunPickupCollectionStatusV1.StaleLifecycle));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(0));
        }

        [Test]
        public void ExactReplayAfterLifecycleAdvance_RejectsStale()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshotV1 pickup = RealizeOne(fixture);
            RunPickupCollectionCommandV1 command = Command(pickup);
            Assert.That(fixture.Authority.Collect(command).Status,
                Is.EqualTo(RunPickupCollectionStatusV1.Collected));
            fixture.Session.LifecycleGeneration = 2L;

            RunPickupCollectionResultV1 staleReplay =
                fixture.Authority.Collect(command);

            Assert.That(staleReplay.Status,
                Is.EqualTo(RunPickupCollectionStatusV1.StaleLifecycle));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(1));
        }

        [Test]
        public void WrongChildPickupPairing_Rejects()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshotV1 pickup = RealizeOne(fixture);
            RunPickupCollectionCommandV1 command = Command(
                pickup,
                childId: Id("terminaldropchild", "wrong"));

            Assert.That(fixture.Authority.Collect(command).Status,
                Is.EqualTo(RunPickupCollectionStatusV1.WrongPickupChildPairing));
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void MissingOrWrongCollector_Rejects(bool entityPresent, bool participantPresent)
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshotV1 pickup = RealizeOne(fixture);
            StableId entity = entityPresent ? Id("actor", "wrong") : null;
            StableId participant = participantPresent ? Id("participant", "wrong") : null;
            RunPickupCollectionCommandV1 command = Command(
                pickup,
                collectorEntity: entity,
                collectorParticipant: participant);

            Assert.That(fixture.Authority.Collect(command).Status,
                Is.EqualTo(RunPickupCollectionStatusV1.UnauthorizedCollector));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(0));
        }

        [Test]
        public void Collection_RecordsExactChildInRunSession()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshotV1 pickup = RealizeOne(fixture);
            RunPickupCollectionCommandV1 command = Command(pickup);

            RunPickupCollectionResultV1 result = fixture.Authority.Collect(command);

            Assert.That(fixture.Session.LastFact, Is.SameAs(result.CollectionFact));
            Assert.That(fixture.Session.LastFact.AvailablePickup.Reward.RewardInstanceStableId,
                Is.EqualTo(pickup.Reward.RewardInstanceStableId));
            Assert.That(fixture.Session.LastFact.AvailablePickup.Batch.DropOperationStableId,
                Is.EqualTo(pickup.Batch.DropOperationStableId));
            Assert.That(result.Pickup.CollectionOrder, Is.EqualTo(1L));
            Assert.That(result.Pickup.CollectedAtAuthoritativeTick, Is.EqualTo(50L));
        }

        [Test]
        public void Collection_DoesNotMutatePermanentHoldingsOrWallets()
        {
            Fixture fixture = CreateFixture();

            fixture.Authority.Collect(Command(RealizeOne(fixture)));

            Assert.That(fixture.Session.PermanentMutationCount, Is.EqualTo(0));
            Assert.That(fixture.Authority.GetType().Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name),
                Does.Not.Contain("ShooterMover.Persistence"));
        }

        [Test]
        public void CollectedPickup_DoesNotReappearAfterPresentationReconstructionQuery()
        {
            Fixture fixture = CreateFixture();
            fixture.Authority.Collect(Command(RealizeOne(fixture)));

            IReadOnlyList<RunPickupSnapshotV1> available =
                fixture.Authority.ExportAvailablePickups();

            Assert.That(available, Is.Empty);
            Assert.That(fixture.Authority.ExportPickups().Single().State,
                Is.EqualTo(RunPickupStateV1.Collected));
        }

        [Test]
        public void UncollectedPickup_ReappearsWithIdenticalIdentity()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshotV1 first = RealizeOne(fixture);

            RunPickupSnapshotV1 rebuilt = fixture.Authority.ExportAvailablePickups().Single();

            Assert.That(rebuilt.PickupStableId, Is.EqualTo(first.PickupStableId));
            Assert.That(rebuilt.Fingerprint, Is.EqualTo(first.Fingerprint));
        }

        [Test]
        public void RoomExitAndReturn_RedeliveryDoesNotDuplicateAvailablePickup()
        {
            Fixture fixture = CreateFixture();
            RunPickupGeneratedBatchV1 batch = Batch(Child("money-a", RewardGrantKindV1.Money, "credits", 5L));
            StableId firstId = fixture.Authority.Realize(batch).Pickups.Single().PickupStableId;

            RunPickupRealizationResultV1 returned = fixture.Authority.Realize(batch);

            Assert.That(returned.Pickups.Single().PickupStableId, Is.EqualTo(firstId));
            Assert.That(fixture.Authority.AvailablePickupCount, Is.EqualTo(1));
        }

        [Test]
        public void SourcePositionFailure_RetainsRecoverablePendingReward()
        {
            Fixture fixture = CreateFixture();
            fixture.Position.Resolve = false;

            RunPickupRealizationResultV1 result = fixture.Authority.Realize(
                Batch(Child("money-a", RewardGrantKindV1.Money, "credits", 5L)));

            Assert.That(result.Status,
                Is.EqualTo(RunPickupRealizationStatusV1.PendingSourcePosition));
            Assert.That(result.Pickups.Single().State,
                Is.EqualTo(RunPickupStateV1.PendingSourcePosition));
            Assert.That(fixture.Authority.AvailablePickupCount, Is.EqualTo(0));
            Assert.That(fixture.Authority.PickupCount, Is.EqualTo(1));
        }

        [Test]
        public void PresentationFailure_CannotMarkPickupCollected()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshotV1 pickup = RealizeOne(fixture);

            Assert.That(pickup.State, Is.EqualTo(RunPickupStateV1.Available));
            Assert.That(fixture.Authority.CollectedPickupCount, Is.EqualTo(0));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(0));
        }

        [Test]
        public void RetryAfterTransientSourceFailure_UsesSamePickupIdentity()
        {
            Fixture fixture = CreateFixture();
            fixture.Position.Resolve = false;
            RunPickupGeneratedBatchV1 batch = Batch(Child("money-a", RewardGrantKindV1.Money, "credits", 5L));
            StableId pendingId = fixture.Authority.Realize(batch).Pickups.Single().PickupStableId;
            fixture.Position.Resolve = true;

            RunPickupRealizationResultV1 retry = fixture.Authority.Realize(batch);

            Assert.That(retry.Status, Is.EqualTo(RunPickupRealizationStatusV1.Realized));
            Assert.That(retry.Pickups.Single().PickupStableId, Is.EqualTo(pendingId));
            Assert.That(retry.Pickups.Single().State, Is.EqualTo(RunPickupStateV1.Available));
        }

        [Test]
        public void TwoPendingBatchRoutes_CreateOnePickupSet()
        {
            Fixture fixture = CreateFixture();
            GeneratedTerminalDropResultV1 generated = GeneratedTerminalResult();
            var pending = new PendingTerminalDropAdmissionAuthorityV1();
            PendingTerminalDropAdmissionResultV1 first = pending.Admit(generated);
            PendingTerminalDropAdmissionResultV1 replay = pending.Admit(generated);
            var firstRoute = new PendingTerminalDropPickupConsumerV1(fixture.Authority);
            var secondRoute = new PendingTerminalDropPickupConsumerV1(fixture.Authority);

            RunPickupRealizationResultV1 firstResult = firstRoute.Consume(first);
            RunPickupRealizationResultV1 secondResult = secondRoute.Consume(replay);

            Assert.That(firstResult.Status, Is.EqualTo(RunPickupRealizationStatusV1.Realized));
            Assert.That(secondResult.Status, Is.EqualTo(RunPickupRealizationStatusV1.ExactReplay));
            Assert.That(fixture.Authority.PickupCount, Is.EqualTo(1));
        }

        [Test]
        public void DifferentUniqueRewardsUsingOneDefinition_RemainDistinctInstances()
        {
            Fixture fixture = CreateFixture();
            RunPickupGeneratedBatchV1 batch = Batch(
                Child("equipment-instance-a", RewardGrantKindV1.EquipmentReference, "laser", 1L, 0),
                Child("equipment-instance-b", RewardGrantKindV1.EquipmentReference, "laser", 1L, 1));

            IReadOnlyList<RunPickupSnapshotV1> pickups = fixture.Authority.Realize(batch).Pickups;

            Assert.That(pickups.Select(item => item.Reward.RewardInstanceStableId).Distinct().Count(),
                Is.EqualTo(2));
            Assert.That(pickups.Select(item => item.PickupStableId).Distinct().Count(),
                Is.EqualTo(2));
        }

        [Test]
        public void RejectedSessionRecording_LeavesPickupAvailableAndRetryable()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshotV1 pickup = RealizeOne(fixture);
            fixture.Session.ForcedStatus = RunPickupSessionRecordStatusV1.Rejected;

            RunPickupCollectionResultV1 rejected = fixture.Authority.Collect(Command(pickup));
            fixture.Session.ForcedStatus = null;
            RunPickupCollectionResultV1 retry = fixture.Authority.Collect(Command(pickup));

            Assert.That(rejected.IsCollected, Is.False);
            Assert.That(retry.Status, Is.EqualTo(RunPickupCollectionStatusV1.Collected));
            Assert.That(fixture.Authority.CollectedPickupCount, Is.EqualTo(1));
        }
    }
}
#endif
