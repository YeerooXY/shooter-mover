#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.RunPickups;
using ShooterMover.TerminalDropBinding;

namespace ShooterMover.Tests.EditMode.RunPickups
{
    public sealed partial class RunLocalPickupStateTests
    {
        private static readonly StableId RunId = Id("run", "pickup-live");
        private static readonly StableId PlayerActorId = Id("actor", "player");
        private static readonly StableId PlayerParticipantId = Id("participant", "player");
        private static readonly StableId SourceEntityId = Id("entity", "terminal-source");
        private static readonly StableId SourcePlacementId = Id("placement", "terminal-source");
        private static readonly StableId RoomId = Id("room", "pickup-room");

        private sealed class FakeSourcePositionPort : IRunPickupSourcePositionPort
        {
            public bool Resolve = true;
            public int CallCount;
            public string Diagnostic = "source-position-unavailable";
            public RunPickupWorldSpawnContext Context =
                new RunPickupWorldSpawnContext(RoomId, 4d, 7d, "position-fingerprint");

            public bool TryResolve(
                StableId runStableId,
                long runLifecycleGeneration,
                StableId sourceEntityStableId,
                StableId sourcePlacementStableId,
                out RunPickupWorldSpawnContext worldSpawnContext,
                out string diagnostic)
            {
                CallCount++;
                worldSpawnContext = Resolve ? Context : null;
                diagnostic = Resolve ? string.Empty : Diagnostic;
                return Resolve;
            }
        }

        private sealed class FakeRunSessionPort : IRunPickupRunSessionPort
        {
            private sealed class Record
            {
                public Record(string fingerprint, RunPickupCollectionFact fact)
                {
                    Fingerprint = fingerprint;
                    Fact = fact;
                }

                public string Fingerprint { get; }
                public RunPickupCollectionFact Fact { get; }
            }

            private readonly object gate = new object();
            private readonly Dictionary<StableId, Record> records =
                new Dictionary<StableId, Record>();

            public StableId RunStableId { get; set; } = RunId;
            public long LifecycleGeneration { get; set; } = 1L;
            public long AuthoritativeTick { get; set; } = 50L;
            public bool IsActive { get; set; } = true;
            public StableId PlayerActorStableId { get; set; } = PlayerActorId;
            public StableId PlayerParticipantStableId { get; set; } = PlayerParticipantId;
            public int RecordCallCount { get; private set; }
            public int PermanentMutationCount { get; private set; }
            public RunPickupCollectionFact LastFact { get; private set; }
            public RunPickupSessionRecordStatus? ForcedStatus { get; set; }
            public bool ContextAvailable { get; set; } = true;
            public bool ThrowOnContextRead { get; set; }

            public bool TryReadContext(
                out RunPickupRunSessionContext context,
                out string diagnostic)
            {
                if (ThrowOnContextRead)
                    throw new InvalidOperationException("fake-session-context-failure");
                if (!ContextAvailable)
                {
                    context = null;
                    diagnostic = "fake-session-context-unavailable";
                    return false;
                }

                long currentLifecycleCount;
                lock (gate)
                {
                    currentLifecycleCount = records.Values.LongCount(record =>
                        record.Fact.AvailablePickup.Batch.RunStableId == RunStableId
                        && record.Fact.AvailablePickup.Batch.RunLifecycleGeneration
                            == LifecycleGeneration);
                }
                context = new RunPickupRunSessionContext(
                    RunStableId,
                    LifecycleGeneration,
                    AuthoritativeTick,
                    IsActive,
                    PlayerActorStableId,
                    PlayerParticipantStableId,
                    checked(currentLifecycleCount + 1L));
                diagnostic = string.Empty;
                return true;
            }

            public RunPickupSessionRecordResult RecordCollection(
                RunPickupCollectionFact fact)
            {
                lock (gate)
                {
                    RecordCallCount++;
                    LastFact = fact;
                    if (ForcedStatus.HasValue)
                    {
                        return new RunPickupSessionRecordResult(
                            ForcedStatus.Value,
                            fact,
                            "forced-session-result");
                    }

                    StableId operation = fact.Command.CollectionOperationStableId;
                    Record existing;
                    if (records.TryGetValue(operation, out existing))
                    {
                        return new RunPickupSessionRecordResult(
                            string.Equals(
                                existing.Fingerprint,
                                fact.Fingerprint,
                                StringComparison.Ordinal)
                                ? RunPickupSessionRecordStatus.ExactReplay
                                : RunPickupSessionRecordStatus.ConflictingDuplicate,
                            existing.Fact,
                            string.Empty);
                    }
                    records.Add(operation, new Record(fact.Fingerprint, fact));
                    return new RunPickupSessionRecordResult(
                        RunPickupSessionRecordStatus.Accepted,
                        fact,
                        string.Empty);
                }
            }
        }

        [Test]
        public void OneGeneratedChild_CreatesOneExactAvailablePickup()
        {
            Fixture fixture = CreateFixture();
            RunPickupGeneratedBatch batch = Batch(Child("money-a", RewardGrantKind.Money, "credits", 5L));

            RunPickupRealizationResult result = fixture.Authority.Realize(batch);

            Assert.That(result.Status, Is.EqualTo(RunPickupRealizationStatus.Realized));
            Assert.That(result.Pickups.Count, Is.EqualTo(1));
            Assert.That(result.Pickups[0].State, Is.EqualTo(RunPickupState.Available));
            Assert.That(result.Pickups[0].Reward.RewardInstanceStableId,
                Is.EqualTo(batch.GeneratedRewards[0].RewardInstanceStableId));
            Assert.That(fixture.Authority.PickupCount, Is.EqualTo(1));
        }

        [Test]
        public void ExactRealizationReplay_CreatesNoSecondPickup()
        {
            Fixture fixture = CreateFixture();
            RunPickupGeneratedBatch batch = Batch(Child("money-a", RewardGrantKind.Money, "credits", 5L));
            RunPickupRealizationResult first = fixture.Authority.Realize(batch);

            RunPickupRealizationResult replay = fixture.Authority.Realize(batch);

            Assert.That(replay.Status, Is.EqualTo(RunPickupRealizationStatus.ExactReplay));
            Assert.That(replay.Pickups.Count, Is.EqualTo(1));
            Assert.That(replay.Pickups[0].PickupStableId, Is.EqualTo(first.Pickups[0].PickupStableId));
            Assert.That(fixture.Authority.PickupCount, Is.EqualTo(1));
        }

        [Test]
        public void ConflictingRealizationIdentity_RejectsWithoutMutation()
        {
            Fixture fixture = CreateFixture();
            RunPickupGeneratedReward child = Child("money-a", RewardGrantKind.Money, "credits", 5L);
            RunPickupGeneratedBatch accepted = Batch(child, "drop-operation-a", "batch-a");
            fixture.Authority.Realize(accepted);
            RunPickupGeneratedBatch conflict = Batch(child, "drop-operation-a", "batch-b");

            RunPickupRealizationResult result = fixture.Authority.Realize(conflict);

            Assert.That(result.Status, Is.EqualTo(RunPickupRealizationStatus.ConflictingDuplicate));
            Assert.That(fixture.Authority.PickupCount, Is.EqualTo(1));
        }

        [Test]
        public void SameContentDefinition_DifferentChildrenCreateDistinctPickups()
        {
            Fixture fixture = CreateFixture();
            RunPickupGeneratedBatch batch = Batch(
                Child("box-a", RewardGrantKind.Strongbox, "emerald", 1L, 0),
                Child("box-b", RewardGrantKind.Strongbox, "emerald", 1L, 1));

            RunPickupRealizationResult result = fixture.Authority.Realize(batch);

            Assert.That(result.Pickups.Count, Is.EqualTo(2));
            Assert.That(result.Pickups[0].PickupStableId,
                Is.Not.EqualTo(result.Pickups[1].PickupStableId));
            Assert.That(result.Pickups[0].Reward.ContentStableId,
                Is.EqualTo(result.Pickups[1].Reward.ContentStableId));
        }

        [Test]
        public void Strongbox_PreservesExactGeneratedInstanceIdentity()
        {
            Fixture fixture = CreateFixture();
            RunPickupGeneratedReward child =
                Child("strongbox-instance-42", RewardGrantKind.Strongbox, "emerald", 1L);

            RunPickupSnapshot pickup = fixture.Authority.Realize(Batch(child)).Pickups[0];

            Assert.That(pickup.Reward.RewardInstanceStableId,
                Is.EqualTo(child.RewardInstanceStableId));
            Assert.That(pickup.Reward.SourceGrantStableId, Is.EqualTo(child.SourceGrantStableId));
        }

        [TestCase(RewardGrantKind.Money, "credits", 73L)]
        [TestCase(RewardGrantKind.Scrap, "scrap", 19L)]
        public void StackableReward_PreservesExactQuantity(
            RewardGrantKind kind,
            string content,
            long quantity)
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot pickup = fixture.Authority.Realize(
                Batch(Child("stack", kind, content, quantity))).Pickups[0];

            Assert.That(pickup.Reward.Quantity, Is.EqualTo(quantity));
            Assert.That(fixture.Authority.PickupCount, Is.EqualTo(1));
        }

        [Test]
        public void RealizationBoundary_HasNoDropOrGenerationDependency()
        {
            Fixture fixture = CreateFixture();

            fixture.Authority.Realize(
                Batch(Child("money-a", RewardGrantKind.Money, "credits", 5L)));

            Assert.That(fixture.Position.CallCount, Is.EqualTo(1));
            Assert.That(fixture.Authority.GetType().GetConstructors().Single()
                .GetParameters().Select(parameter => parameter.ParameterType.Name),
                Does.Not.Contain("RewardGenerationActions"));
        }

        [Test]
        public void FirstValidCollection_IsAcceptedExactlyOnce()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot pickup = RealizeOne(fixture);

            RunPickupCollectionResult result = fixture.Authority.Collect(Command(pickup));

            Assert.That(result.Status, Is.EqualTo(RunPickupCollectionStatus.Collected));
            Assert.That(fixture.Authority.CollectedPickupCount, Is.EqualTo(1));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedCollectionOperation_ReturnsExactReplay()
        {
            Fixture fixture = CreateFixture();
            RunPickupSnapshot pickup = RealizeOne(fixture);
            RunPickupCollectionCommand command = Command(pickup);
            RunPickupCollectionResult first = fixture.Authority.Collect(command);

            RunPickupCollectionResult replay = fixture.Authority.Collect(command);

            Assert.That(first.Status, Is.EqualTo(RunPickupCollectionStatus.Collected));
            Assert.That(replay.Status, Is.EqualTo(RunPickupCollectionStatus.ExactReplay));
            Assert.That(replay.CollectionFact.Fingerprint,
                Is.EqualTo(first.CollectionFact.Fingerprint));
            Assert.That(fixture.Session.RecordCallCount, Is.EqualTo(1));
        }
    }
}
#endif
