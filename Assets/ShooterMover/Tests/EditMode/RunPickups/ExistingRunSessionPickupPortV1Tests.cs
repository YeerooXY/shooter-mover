#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.RunPickups;

namespace ShooterMover.Tests.EditMode.RunPickups
{
    public sealed partial class RunLocalPickupAuthorityV1Tests
    {
        private sealed class FakeCollectedRewardAuthority :
            IRunSessionCollectedRewardAuthorityV1
        {
            public StableId RunStableId { get; set; } = RunId;
            public long LifecycleGeneration { get; set; } = 1L;
            public long AuthoritativeTick { get; set; } = 50L;
            public bool IsActive { get; set; } = true;
            public StableId PlayerActorStableId { get; set; } = PlayerActorId;
            public StableId PlayerParticipantStableId { get; set; } = PlayerParticipantId;
            public long NextCollectedRewardOrder { get; set; } = 1L;
            public RunSessionCollectedRewardV1 LastReward { get; private set; }
            public RunSessionRewardCollectionStatusV1 Status { get; set; } =
                RunSessionRewardCollectionStatusV1.Collected;

            public RunSessionRewardCollectionResultV1 RecordCollectedRunReward(
                RunSessionCollectedRewardV1 reward)
            {
                LastReward = reward;
                return new RunSessionRewardCollectionResultV1(
                    Status,
                    reward,
                    Status == RunSessionRewardCollectionStatusV1.Collected
                        || Status == RunSessionRewardCollectionStatusV1.ExactReplay
                        ? string.Empty
                        : "forced-run-session-record-result");
            }

            public IReadOnlyList<RunSessionCollectedRewardV1>
                ExportCollectedRunRewards()
            {
                return LastReward == null
                    ? Array.Empty<RunSessionCollectedRewardV1>()
                    : new[] { LastReward };
            }
        }

        [Test]
        public void ExistingRunSessionPort_RecordsExactTypedRewardChild()
        {
            RunPickupGeneratedBatchV1 batch = Batch(
                Child(
                    "exact-box-instance",
                    RewardGrantKindV1.Strongbox,
                    "emerald",
                    1L));
            RunPickupGeneratedRewardV1 child = batch.GeneratedRewards[0];
            var world = new RunPickupWorldSpawnContextV1(
                RoomId,
                4d,
                7d,
                "position-fingerprint");
            var pickup = new RunPickupSnapshotV1(
                RunPickupIdentityV1.DerivePickupStableId(batch, child),
                batch,
                child,
                RunPickupStateV1.Available,
                world,
                null,
                null,
                null,
                0L,
                0L,
                string.Empty);
            RunPickupCollectionCommandV1 command = Command(pickup);
            var fact = new RunPickupCollectionFactV1(
                pickup,
                command,
                1L,
                50L);
            var runSession = new FakeCollectedRewardAuthority();
            var port = new ExistingRunSessionPickupPortV1(runSession);

            RunPickupSessionRecordResultV1 result = port.RecordCollection(fact);
            RunSessionCollectedRewardV1 recorded = runSession.LastReward;

            Assert.That(result.Status,
                Is.EqualTo(RunPickupSessionRecordStatusV1.Accepted));
            Assert.That(recorded, Is.Not.Null);
            Assert.That(recorded.PickupStableId, Is.EqualTo(pickup.PickupStableId));
            Assert.That(recorded.GeneratedRewardChildStableId,
                Is.EqualTo(child.RewardInstanceStableId));
            Assert.That(recorded.SourceGrantStableId,
                Is.EqualTo(child.SourceGrantStableId));
            Assert.That(recorded.DropOperationStableId,
                Is.EqualTo(batch.DropOperationStableId));
            Assert.That(recorded.TerminalEventStableId,
                Is.EqualTo(batch.TerminalEventStableId));
            Assert.That(recorded.TriggeringEventStableId,
                Is.EqualTo(batch.TriggeringEventStableId));
            Assert.That(recorded.RunStableId, Is.EqualTo(batch.RunStableId));
            Assert.That(recorded.RunLifecycleGeneration,
                Is.EqualTo(batch.RunLifecycleGeneration));
            Assert.That(recorded.SourceEntityStableId,
                Is.EqualTo(batch.SourceEntityStableId));
            Assert.That(recorded.SourcePlacementStableId,
                Is.EqualTo(batch.SourcePlacementStableId));
            Assert.That(recorded.SourceLifecycleGeneration,
                Is.EqualTo(batch.SourceLifecycleGeneration));
            Assert.That(recorded.AttributedParticipantStableId,
                Is.EqualTo(batch.AttributedParticipantStableId));
            Assert.That(recorded.RewardKind, Is.EqualTo(child.Kind));
            Assert.That(recorded.ContentStableId, Is.EqualTo(child.ContentStableId));
            Assert.That(recorded.Quantity, Is.EqualTo(child.Quantity));
            Assert.That(recorded.GeneratedBatchFingerprint,
                Is.EqualTo(batch.BatchFingerprint));
            Assert.That(recorded.GeneratedRewardFingerprint,
                Is.EqualTo(child.GeneratedRewardFingerprint));
            Assert.That(recorded.RoomStableId, Is.EqualTo(RoomId));
            Assert.That(recorded.WorldPositionX, Is.EqualTo(4d));
            Assert.That(recorded.WorldPositionY, Is.EqualTo(7d));
            Assert.That(recorded.WorldSpawnFingerprint,
                Is.EqualTo(world.Fingerprint));
            Assert.That(recorded.AvailablePickupFingerprint,
                Is.EqualTo(pickup.Fingerprint));
            Assert.That(recorded.CollectorEntityStableId,
                Is.EqualTo(PlayerActorId));
            Assert.That(recorded.CollectorParticipantStableId,
                Is.EqualTo(PlayerParticipantId));
            Assert.That(recorded.CollectionOperationStableId,
                Is.EqualTo(command.CollectionOperationStableId));
            Assert.That(recorded.CollectionOrder, Is.EqualTo(1L));
            Assert.That(recorded.CollectedAtAuthoritativeTick, Is.EqualTo(50L));
            Assert.That(recorded.Fingerprint, Is.Not.Empty);
        }

        [Test]
        public void ExistingRunSessionPort_ReadsLifecycleScopedNextOrder()
        {
            var runSession = new FakeCollectedRewardAuthority
            {
                LifecycleGeneration = 2L,
                NextCollectedRewardOrder = 1L,
            };
            var port = new ExistingRunSessionPickupPortV1(runSession);

            RunPickupRunSessionContextV1 context;
            string diagnostic;
            bool resolved = port.TryReadContext(out context, out diagnostic);

            Assert.That(resolved, Is.True, diagnostic);
            Assert.That(context.LifecycleGeneration, Is.EqualTo(2L));
            Assert.That(context.NextCollectionOrder, Is.EqualTo(1L));
        }

        [TestCase(
            RunSessionRewardCollectionStatusV1.ExactReplay,
            RunPickupSessionRecordStatusV1.ExactReplay)]
        [TestCase(
            RunSessionRewardCollectionStatusV1.ConflictingDuplicate,
            RunPickupSessionRecordStatusV1.ConflictingDuplicate)]
        [TestCase(
            RunSessionRewardCollectionStatusV1.StaleLifecycle,
            RunPickupSessionRecordStatusV1.StaleLifecycle)]
        [TestCase(
            RunSessionRewardCollectionStatusV1.UnauthorizedCollector,
            RunPickupSessionRecordStatusV1.UnauthorizedCollector)]
        public void ExistingRunSessionPort_MapsExactJournalResult(
            RunSessionRewardCollectionStatusV1 runStatus,
            RunPickupSessionRecordStatusV1 expectedPickupStatus)
        {
            RunPickupGeneratedBatchV1 batch = Batch(
                Child("money-map", RewardGrantKindV1.Money, "credits", 5L));
            RunPickupGeneratedRewardV1 child = batch.GeneratedRewards[0];
            var world = new RunPickupWorldSpawnContextV1(
                RoomId,
                1d,
                2d,
                "map-position");
            var pickup = new RunPickupSnapshotV1(
                RunPickupIdentityV1.DerivePickupStableId(batch, child),
                batch,
                child,
                RunPickupStateV1.Available,
                world,
                null,
                null,
                null,
                0L,
                0L,
                string.Empty);
            var runSession = new FakeCollectedRewardAuthority { Status = runStatus };
            var port = new ExistingRunSessionPickupPortV1(runSession);

            RunPickupSessionRecordResultV1 result = port.RecordCollection(
                new RunPickupCollectionFactV1(
                    pickup,
                    Command(pickup),
                    1L,
                    50L));

            Assert.That(result.Status, Is.EqualTo(expectedPickupStatus));
        }
    }
}
#endif
