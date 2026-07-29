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
    public sealed partial class RunLocalPickupStateTests
    {
        [Test]
        public void ConflictingCollectionOperationReuse_Rejects()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot pickup = RealizeOne(fixture);
            RunPickupCollectionCommand first = Command(pickup);
            fixture.Authority.Collect(first);
            var conflict = new RunPickupCollectionCommand(
                first.CollectionOperationStableId,
                pickup.PickupStableId,
                pickup.Reward.RewardInstanceStableId,
                RunId,
                1L,
                PlayerActorId,
                PlayerParticipantId,
                "different-fingerprint");

            RunPickupCollectionResult result = fixture.Authority.Collect(conflict);

            Assert.That(result.Status, Is.EqualTo(RunPickupCollectionStatus.ConflictingDuplicate));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(1));
        }

        [Test]
        public void TwoCollisionDeliveries_CannotCollectTwice()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot pickup = RealizeOne(fixture);
            RunPickupCollectionCommand command = Command(pickup);
            RunPickupCollectionResult left = null;
            RunPickupCollectionResult right = null;

            Parallel.Invoke(
                () => left = fixture.Authority.Collect(command),
                () => right = fixture.Authority.Collect(command));

            CollectionAssert.AreEquivalent(
                new[]
                {
                    RunPickupCollectionStatus.Collected,
                    RunPickupCollectionStatus.ExactReplay
                },
                new[] { left.Status, right.Status });
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(1));
            Assert.That(fixture.Authority.CollectedPickupCount, Is.EqualTo(1));
        }

        [Test]
        public void WrongRun_Rejects()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot pickup = RealizeOne(fixture);
            RunPickupCollectionCommand command = Command(
                pickup,
                runId: Id("run", "wrong"));

            Assert.That(fixture.Authority.Collect(command).Status,
                Is.EqualTo(RunPickupCollectionStatus.WrongRun));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(0));
        }

        [Test]
        public void StaleRunSessionLifecycle_Rejects()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot pickup = RealizeOne(fixture);
            fixture.Session.LifecycleGeneration = 2L;

            RunPickupCollectionResult result = fixture.Authority.Collect(Command(pickup));

            Assert.That(result.Status, Is.EqualTo(RunPickupCollectionStatus.StaleLifecycle));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(0));
        }

        [Test]
        public void ExactReplayAfterLifecycleAdvance_RejectsStale()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot pickup = RealizeOne(fixture);
            RunPickupCollectionCommand command = Command(pickup);
            Assert.That(fixture.Authority.Collect(command).Status,
                Is.EqualTo(RunPickupCollectionStatus.Collected));
            fixture.Session.LifecycleGeneration = 2L;

            RunPickupCollectionResult staleReplay =
                fixture.Authority.Collect(command);

            Assert.That(staleReplay.Status,
                Is.EqualTo(RunPickupCollectionStatus.StaleLifecycle));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(1));
        }

        [Test]
        public void WrongChildPickupPairing_Rejects()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot pickup = RealizeOne(fixture);
            RunPickupCollectionCommand command = Command(
                pickup,
                childId: Id("terminaldropchild", "wrong"));

            Assert.That(fixture.Authority.Collect(command).Status,
                Is.EqualTo(RunPickupCollectionStatus.WrongPickupChildPairing));
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void MissingOrWrongCollector_Rejects(bool entityPresent, bool participantPresent)
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot pickup = RealizeOne(fixture);
            StableId entity = entityPresent ? Id("actor", "wrong") : null;
            StableId participant = participantPresent ? Id("participant", "wrong") : null;
            RunPickupCollectionCommand command = Command(
                pickup,
                collectorEntity: entity,
                collectorParticipant: participant);

            Assert.That(fixture.Authority.Collect(command).Status,
                Is.EqualTo(RunPickupCollectionStatus.UnauthorizedCollector));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(0));
        }

        [Test]
        public void Collection_RecordsExactChildInRunSession()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot pickup = RealizeOne(fixture);
            RunPickupCollectionCommand command = Command(pickup);

            RunPickupCollectionResult result = fixture.Authority.Collect(command);

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

            IReadOnlyList<RunPickupSnapshot> available =
                fixture.Authority.ExportAvailablePickups();

            Assert.That(available, Is.Empty);
            Assert.That(fixture.Authority.ExportPickups().Single().State,
                Is.EqualTo(RunPickupState.Collected));
        }

        [Test]
        public void UncollectedPickup_ReappearsWithIdenticalIdentity()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot first = RealizeOne(fixture);

            RunPickupSnapshot rebuilt = fixture.Authority.ExportAvailablePickups().Single();

            Assert.That(rebuilt.PickupStableId, Is.EqualTo(first.PickupStableId));
            Assert.That(rebuilt.Fingerprint, Is.EqualTo(first.Fingerprint));
        }

        [Test]
        public void RoomExitAndReturn_RedeliveryDoesNotDuplicateAvailablePickup()
        {
            Fixture fixture = CreateFixture();
            RunPickupGeneratedBatch batch = Batch(Child("money-a", RewardGrantKind.Money, "credits", 5L));
            StableId firstId = fixture.Authority.Realize(batch).Pickups.Single().PickupStableId;

            RunPickupRealizationResult returned = fixture.Authority.Realize(batch);

            Assert.That(returned.Pickups.Single().PickupStableId, Is.EqualTo(firstId));
            Assert.That(fixture.Authority.AvailablePickupCount, Is.EqualTo(1));
        }

        [Test]
        public void SourcePositionFailure_RetainsRecoverablePendingReward()
        {
            Fixture fixture = CreateFixture();
            fixture.Position.Resolve = false;

            RunPickupRealizationResult result = fixture.Authority.Realize(
                Batch(Child("money-a", RewardGrantKind.Money, "credits", 5L)));

            Assert.That(result.Status,
                Is.EqualTo(RunPickupRealizationStatus.PendingSourcePosition));
            Assert.That(result.Pickups.Single().State,
                Is.EqualTo(RunPickupState.PendingSourcePosition));
            Assert.That(fixture.Authority.AvailablePickupCount, Is.EqualTo(0));
            Assert.That(fixture.Authority.PickupCount, Is.EqualTo(1));
        }

        [Test]
        public void PresentationFailure_CannotMarkPickupCollected()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot pickup = RealizeOne(fixture);

            Assert.That(pickup.State, Is.EqualTo(RunPickupState.Available));
            Assert.That(fixture.Authority.CollectedPickupCount, Is.EqualTo(0));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(0));
        }

        [Test]
        public void RetryAfterTransientSourceFailure_UsesSamePickupIdentity()
        {
            Fixture fixture = CreateFixture();
            fixture.Position.Resolve = false;
            RunPickupGeneratedBatch batch = Batch(Child("money-a", RewardGrantKind.Money, "credits", 5L));
            StableId pendingId = fixture.Authority.Realize(batch).Pickups.Single().PickupStableId;
            fixture.Position.Resolve = true;

            RunPickupRealizationResult retry = fixture.Authority.Realize(batch);

            Assert.That(retry.Status, Is.EqualTo(RunPickupRealizationStatus.Realized));
            Assert.That(retry.Pickups.Single().PickupStableId, Is.EqualTo(pendingId));
            Assert.That(retry.Pickups.Single().State, Is.EqualTo(RunPickupState.Available));
        }

        [Test]
        public void TwoPendingBatchRoutes_CreateOnePickupSet()
        {
            Fixture fixture = CreateFixture();
            GeneratedTerminalDropResult generated = GeneratedTerminalResult();
            var pending = new PendingTerminalDropAdmissionState();
            PendingTerminalDropAdmissionResult first = pending.Admit(generated);
            PendingTerminalDropAdmissionResult replay = pending.Admit(generated);
            var firstRoute = new PendingTerminalDropPickupConsumer(fixture.Authority);
            var secondRoute = new PendingTerminalDropPickupConsumer(fixture.Authority);

            RunPickupRealizationResult firstResult = firstRoute.Consume(first);
            RunPickupRealizationResult secondResult = secondRoute.Consume(replay);

            Assert.That(firstResult.Status, Is.EqualTo(RunPickupRealizationStatus.Realized));
            Assert.That(secondResult.Status, Is.EqualTo(RunPickupRealizationStatus.ExactReplay));
            Assert.That(fixture.Authority.PickupCount, Is.EqualTo(1));
        }

        [Test]
        public void DifferentUniqueRewardsUsingOneDefinition_RemainDistinctInstances()
        {
            Fixture fixture = CreateFixture();
            RunPickupGeneratedBatch batch = Batch(
                Child("equipment-instance-a", RewardGrantKind.EquipmentReference, "laser", 1L, 0),
                Child("equipment-instance-b", RewardGrantKind.EquipmentReference, "laser", 1L, 1));

            IReadOnlyList<RunPickupSnapshot> pickups = fixture.Authority.Realize(batch).Pickups;

            Assert.That(pickups.Select(item => item.Reward.RewardInstanceStableId).Distinct().Count(),
                Is.EqualTo(2));
            Assert.That(pickups.Select(item => item.PickupStableId).Distinct().Count(),
                Is.EqualTo(2));
        }

        [Test]
        public void RejectedSessionRecording_LeavesPickupAvailableAndRetryable()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot pickup = RealizeOne(fixture);
            fixture.Session.ForcedStatus = RunPickupSessionRecordStatus.Rejected;

            RunPickupCollectionResult rejected = fixture.Authority.Collect(Command(pickup));
            fixture.Session.ForcedStatus = null;
            RunPickupCollectionResult retry = fixture.Authority.Collect(Command(pickup));

            Assert.That(rejected.IsCollected, Is.False);
            Assert.That(retry.Status, Is.EqualTo(RunPickupCollectionStatus.Collected));
            Assert.That(fixture.Authority.CollectedPickupCount, Is.EqualTo(1));
        }
    }
}
#endif
