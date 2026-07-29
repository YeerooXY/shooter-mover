#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.RunPickups;

namespace ShooterMover.Tests.EditMode.RunPickups
{
    public sealed partial class RunLocalPickupStateTests
    {
        private sealed class FakeCollectedRewardState :
            IRunSessionCollectedRewardState
        {
            public StableId RunStableId { get; set; } = RunId;
            public long LifecycleGeneration { get; set; } = 1L;
            public long AuthoritativeTick { get; set; } = 50L;
            public bool IsActive { get; set; } = true;
            public StableId PlayerActorStableId { get; set; } = PlayerActorId;
            public StableId PlayerParticipantStableId { get; set; } = PlayerParticipantId;
            public long NextCollectedRewardOrder { get; set; } = 1L;
            public RunSessionCollectedReward LastReward { get; private set; }
            public RunSessionRewardCollectionStatus Status { get; set; } =
                RunSessionRewardCollectionStatus.Collected;

            public RunSessionRewardCollectionResult RecordCollectedRunReward(
                RunSessionCollectedReward reward)
            {
                LastReward = reward;
                return new RunSessionRewardCollectionResult(
                    Status,
                    reward,
                    Status == RunSessionRewardCollectionStatus.Collected
                        || Status == RunSessionRewardCollectionStatus.ExactReplay
                        ? string.Empty
                        : "forced-run-session-record-result");
            }

            public IReadOnlyList<RunSessionCollectedReward>
                ExportCollectedRunRewards()
            {
                return LastReward == null
                    ? Array.Empty<RunSessionCollectedReward>()
                    : new[] { LastReward };
            }
        }

        [Test]
        public void ExistingRunSessionPort_RecordsExactTypedRewardChild()
        {
            RunPickupGeneratedBatch batch = Batch(
                Child(
                    "exact-box-instance",
                    RewardGrantKind.Strongbox,
                    "emerald",
                    1L));
            RunPickupGeneratedReward child = batch.GeneratedRewards[0];
            var world = new RunPickupWorldSpawnContext(
                RoomId,
                4d,
                7d,
                "position-fingerprint");
            var pickup = new RunPickupSnapshot(
                RunPickupIdentity.DerivePickupStableId(batch, child),
                batch,
                child,
                RunPickupState.Available,
                world,
                null,
                null,
                null,
                0L,
                0L,
                string.Empty);
            RunPickupCollectionCommand command = Command(pickup);
            var fact = new RunPickupCollectionFact(
                pickup,
                command,
                1L,
                50L);
            var runSession = new FakeCollectedRewardState();
            var port = new ExistingRunSessionPickupPort(runSession);

            RunPickupSessionRecordResult result = port.RecordCollection(fact);
            RunSessionCollectedReward recorded = runSession.LastReward;

            Assert.That(result.Status,
                Is.EqualTo(RunPickupSessionRecordStatus.Accepted));
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
            var runSession = new FakeCollectedRewardState
            {
                LifecycleGeneration = 2L,
                NextCollectedRewardOrder = 1L,
            };
            var port = new ExistingRunSessionPickupPort(runSession);

            RunPickupRunSessionContext context;
            string diagnostic;
            bool resolved = port.TryReadContext(out context, out diagnostic);

            Assert.That(resolved, Is.True, diagnostic);
            Assert.That(context.LifecycleGeneration, Is.EqualTo(2L));
            Assert.That(context.NextCollectionOrder, Is.EqualTo(1L));
        }

        [TestCase(
            RunSessionRewardCollectionStatus.ExactReplay,
            RunPickupSessionRecordStatus.ExactReplay)]
        [TestCase(
            RunSessionRewardCollectionStatus.ConflictingDuplicate,
            RunPickupSessionRecordStatus.ConflictingDuplicate)]
        [TestCase(
            RunSessionRewardCollectionStatus.StaleLifecycle,
            RunPickupSessionRecordStatus.StaleLifecycle)]
        [TestCase(
            RunSessionRewardCollectionStatus.UnauthorizedCollector,
            RunPickupSessionRecordStatus.UnauthorizedCollector)]
        public void ExistingRunSessionPort_MapsExactJournalResult(
            RunSessionRewardCollectionStatus runStatus,
            RunPickupSessionRecordStatus expectedPickupStatus)
        {
            RunPickupGeneratedBatch batch = Batch(
                Child("money-map", RewardGrantKind.Money, "credits", 5L));
            RunPickupGeneratedReward child = batch.GeneratedRewards[0];
            var world = new RunPickupWorldSpawnContext(
                RoomId,
                1d,
                2d,
                "map-position");
            var pickup = new RunPickupSnapshot(
                RunPickupIdentity.DerivePickupStableId(batch, child),
                batch,
                child,
                RunPickupState.Available,
                world,
                null,
                null,
                null,
                0L,
                0L,
                string.Empty);
            var runSession = new FakeCollectedRewardState { Status = runStatus };
            var port = new ExistingRunSessionPickupPort(runSession);

            RunPickupSessionRecordResult result = port.RecordCollection(
                new RunPickupCollectionFact(
                    pickup,
                    Command(pickup),
                    1L,
                    50L));

            Assert.That(result.Status, Is.EqualTo(expectedPickupStatus));
        }
    }
}
#endif
