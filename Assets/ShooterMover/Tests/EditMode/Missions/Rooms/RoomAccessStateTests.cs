using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Missions.Rooms
{
    public sealed class RoomAccessStateTests
    {
        [Test]
        public void HoldingPresentDoor_IsClosedBeforePickupAndOpenAfterPickup()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            StableId key = Id("holding.blue-key");
            StableId condition = Id("access.blue-key-present");
            RoomAccessDefinition definition = Definition(
                graph,
                new[]
                {
                    Leaf(
                        condition,
                        RoomAccessConditionKind.HoldingPresent,
                        key),
                },
                new[]
                {
                    Door(
                        Level1AuthorableRoomDefinition.EntryRoomStableId,
                        Level1AuthorableRoomDefinition.ForwardDoorStableId,
                        condition),
                });
            var facts = new FakeFactPort();
            var holdings = new FakeHoldingPort();
            var authority = Authority(definition, facts, holdings);

            Assert.That(
                authority.CurrentSnapshot.GetDoor(
                    Level1AuthorableRoomDefinition.ForwardDoorStableId).IsOpen,
                Is.False);

            holdings.SetQuantity(key, 1);

            RoomDoorAccessView projection = authority.CurrentSnapshot.GetDoor(
                Level1AuthorableRoomDefinition.ForwardDoorStableId);
            Assert.That(projection.IsConditionSatisfied, Is.True);
            Assert.That(projection.IsOpen, Is.True);
            Assert.That(projection.IsUnlocked, Is.False);
        }

        [Test]
        public void ConsumingKey_UnlocksOnceAndExactReplayDoesNotConsumeTwice()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            StableId key = Id("holding.consumable-key");
            StableId keyPresent = Id("access.consumable-key-present");
            StableId keyConsumed = Id("access.consumable-key-consumed");
            RoomAccessDefinition definition = Definition(
                graph,
                new[]
                {
                    Leaf(
                        keyPresent,
                        RoomAccessConditionKind.HoldingPresent,
                        key),
                    Leaf(
                        keyConsumed,
                        RoomAccessConditionKind.HoldingConsumed,
                        key),
                },
                new[]
                {
                    Door(
                        Level1AuthorableRoomDefinition.EntryRoomStableId,
                        Level1AuthorableRoomDefinition.ForwardDoorStableId,
                        keyPresent,
                        key),
                });
            var facts = new FakeFactPort();
            var holdings = new FakeHoldingPort();
            holdings.SetQuantity(key, 1);
            RoomAccessState authority = Authority(definition, facts, holdings);
            var command = new UnlockRoomDoorCommand(
                RuntimeId,
                Id("operation.unlock-forward"),
                1L,
                Level1AuthorableRoomDefinition.ForwardDoorStableId);

            RoomAccessOperationResult first = authority.TryUnlock(command);
            RoomAccessOperationResult replay = authority.TryUnlock(command);

            Assert.That(first.Status, Is.EqualTo(RoomAccessOperationStatus.Applied));
            Assert.That(
                replay.Status,
                Is.EqualTo(RoomAccessOperationStatus.DuplicateNoChange));
            Assert.That(holdings.ConsumeCallCount, Is.EqualTo(1));
            Assert.That(holdings.Quantity(key), Is.EqualTo(0));
            Assert.That(authority.IsConditionSatisfied(keyConsumed), Is.True);
            RoomDoorAccessView door = replay.Snapshot.GetDoor(
                Level1AuthorableRoomDefinition.ForwardDoorStableId);
            Assert.That(door.IsUnlocked, Is.True);
            Assert.That(door.IsOpen, Is.True);
        }

        [Test]
        public void ConflictingOperationId_RejectsWithoutAdditionalConsumption()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            StableId key = Id("holding.conflict-key");
            StableId condition = Id("access.conflict-key-present");
            RoomAccessDefinition definition = Definition(
                graph,
                new[]
                {
                    Leaf(
                        condition,
                        RoomAccessConditionKind.HoldingPresent,
                        key),
                },
                new[]
                {
                    Door(
                        Level1AuthorableRoomDefinition.EntryRoomStableId,
                        Level1AuthorableRoomDefinition.ForwardDoorStableId,
                        condition,
                        key),
                    Door(
                        Level1AuthorableRoomDefinition.TerminalRoomStableId,
                        Level1AuthorableRoomDefinition.FinalDoorStableId,
                        condition,
                        key),
                });
            var holdings = new FakeHoldingPort();
            holdings.SetQuantity(key, 2);
            RoomAccessState authority = Authority(
                definition,
                new FakeFactPort(),
                holdings);
            StableId operation = Id("operation.unlock-conflict");

            RoomAccessOperationResult first = authority.TryUnlock(
                new UnlockRoomDoorCommand(
                    RuntimeId,
                    operation,
                    1L,
                    Level1AuthorableRoomDefinition.ForwardDoorStableId));
            RoomAccessOperationResult conflict = authority.TryUnlock(
                new UnlockRoomDoorCommand(
                    RuntimeId,
                    operation,
                    1L,
                    Level1AuthorableRoomDefinition.FinalDoorStableId));

            Assert.That(first.Status, Is.EqualTo(RoomAccessOperationStatus.Applied));
            Assert.That(conflict.Status, Is.EqualTo(RoomAccessOperationStatus.Rejected));
            Assert.That(conflict.RejectionCode, Is.EqualTo("room-access-operation-conflict"));
            Assert.That(holdings.ConsumeCallCount, Is.EqualTo(1));
            Assert.That(holdings.Quantity(key), Is.EqualTo(1));
        }

        [Test]
        public void AllAnyAndNotTrees_EvaluateDeterministically()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            StableId switchA = Id("switch.power-a");
            StableId switchB = Id("switch.power-b");
            StableId a = Id("access.switch-a");
            StableId b = Id("access.switch-b");
            StableId difficulty = Id("access.difficulty-three");
            StableId notB = Id("access.not-switch-b");
            StableId any = Id("access.either-switch");
            StableId all = Id("access.compound-gate");
            RoomAccessDefinition definition = Definition(
                graph,
                new[]
                {
                    Leaf(a, RoomAccessConditionKind.SwitchActive, switchA),
                    Leaf(b, RoomAccessConditionKind.SwitchActive, switchB),
                    Difficulty(difficulty, 3),
                    Composite(notB, RoomAccessConditionKind.Not, b),
                    Composite(any, RoomAccessConditionKind.Any, a, b),
                    Composite(all, RoomAccessConditionKind.All, any, notB, difficulty),
                },
                new[]
                {
                    Door(
                        Level1AuthorableRoomDefinition.EntryRoomStableId,
                        Level1AuthorableRoomDefinition.ForwardDoorStableId,
                        all),
                });
            var facts = new FakeFactPort();
            RoomAccessState authority = Authority(
                definition,
                facts,
                new FakeHoldingPort());

            facts.Set(difficulty: 3, activeSwitches: new[] { switchA });
            string firstFingerprint = authority.CurrentSnapshot.SourceFingerprint;
            Assert.That(authority.IsConditionSatisfied(all), Is.True);
            Assert.That(authority.IsConditionSatisfied(all), Is.True);
            Assert.That(
                authority.CurrentSnapshot.SourceFingerprint,
                Is.EqualTo(firstFingerprint));

            facts.Set(difficulty: 3, activeSwitches: new[] { switchA, switchB });
            Assert.That(authority.IsConditionSatisfied(all), Is.False);

            facts.Set(difficulty: 2, activeSwitches: new[] { switchA });
            Assert.That(authority.IsConditionSatisfied(all), Is.False);
        }

        [Test]
        public void ExactRoomTerminalDropObjectiveAndSwitchFacts_PreserveExistingSemantics()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            StableId roomEntered = Id("access.entry-entered");
            StableId roomComplete = Id("access.entry-complete");
            StableId terminal = Id("access.droid-terminal");
            StableId drop = Id("access.drop-collected");
            StableId objective = Id("access.objective-complete");
            StableId switchCondition = Id("access.switch-active");
            StableId root = Id("access.all-existing-facts");
            StableId dropId = Id("drop.mission-key");
            StableId objectiveId = Id("objective.restore-power");
            StableId switchId = Id("switch.power-main");
            RoomAccessDefinition definition = Definition(
                graph,
                new[]
                {
                    Leaf(
                        roomEntered,
                        RoomAccessConditionKind.RoomEntered,
                        Level1AuthorableRoomDefinition.EntryRoomStableId),
                    Leaf(
                        roomComplete,
                        RoomAccessConditionKind.RoomComplete,
                        Level1AuthorableRoomDefinition.EntryRoomStableId),
                    Leaf(
                        terminal,
                        RoomAccessConditionKind.ExactEntityTerminal,
                        Level1AuthorableRoomDefinition.MovingDroidInstanceStableId),
                    Leaf(drop, RoomAccessConditionKind.CollectedDrop, dropId),
                    Leaf(
                        objective,
                        RoomAccessConditionKind.ObjectiveComplete,
                        objectiveId),
                    Leaf(
                        switchCondition,
                        RoomAccessConditionKind.SwitchActive,
                        switchId),
                    Composite(
                        root,
                        RoomAccessConditionKind.All,
                        roomEntered,
                        roomComplete,
                        terminal,
                        drop,
                        objective,
                        switchCondition),
                },
                new[]
                {
                    Door(
                        Level1AuthorableRoomDefinition.EntryRoomStableId,
                        Level1AuthorableRoomDefinition.ForwardDoorStableId,
                        root),
                });
            var facts = new FakeFactPort();
            RoomAccessState authority = Authority(
                definition,
                facts,
                new FakeHoldingPort());

            facts.Set(
                enteredRooms: new[] { Level1AuthorableRoomDefinition.EntryRoomStableId },
                completedRooms: new[] { Level1AuthorableRoomDefinition.EntryRoomStableId },
                terminalEntities: new[]
                {
                    Level1AuthorableRoomDefinition.MovingDroidInstanceStableId,
                },
                collectedDrops: new[] { dropId },
                completedObjectives: new[] { objectiveId },
                activeSwitches: new[] { switchId });

            Assert.That(authority.IsConditionSatisfied(root), Is.True);
            Assert.That(
                authority.CurrentSnapshot.GetDoor(
                    Level1AuthorableRoomDefinition.ForwardDoorStableId).IsOpen,
                Is.True);
        }

        [Test]
        public void DifferentDoors_HaveIndependentAuthoredConditions()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            StableId key = Id("holding.independent-key");
            StableId switchId = Id("switch.independent-power");
            StableId keyCondition = Id("access.independent-key");
            StableId switchCondition = Id("access.independent-switch");
            RoomAccessDefinition definition = Definition(
                graph,
                new[]
                {
                    Leaf(
                        keyCondition,
                        RoomAccessConditionKind.HoldingPresent,
                        key),
                    Leaf(
                        switchCondition,
                        RoomAccessConditionKind.SwitchActive,
                        switchId),
                },
                new[]
                {
                    Door(
                        Level1AuthorableRoomDefinition.TerminalRoomStableId,
                        Level1AuthorableRoomDefinition.ReturnDoorStableId,
                        keyCondition),
                    Door(
                        Level1AuthorableRoomDefinition.TerminalRoomStableId,
                        Level1AuthorableRoomDefinition.FinalDoorStableId,
                        switchCondition),
                });
            var facts = new FakeFactPort();
            var holdings = new FakeHoldingPort();
            RoomAccessState authority = Authority(definition, facts, holdings);

            holdings.SetQuantity(key, 1);
            RoomAccessSnapshot keySnapshot = authority.CurrentSnapshot;
            Assert.That(
                keySnapshot.GetDoor(
                    Level1AuthorableRoomDefinition.ReturnDoorStableId).IsOpen,
                Is.True);
            Assert.That(
                keySnapshot.GetDoor(
                    Level1AuthorableRoomDefinition.FinalDoorStableId).IsOpen,
                Is.False);

            holdings.SetQuantity(key, 0);
            facts.Set(activeSwitches: new[] { switchId });
            RoomAccessSnapshot switchSnapshot = authority.CurrentSnapshot;
            Assert.That(
                switchSnapshot.GetDoor(
                    Level1AuthorableRoomDefinition.ReturnDoorStableId).IsOpen,
                Is.False);
            Assert.That(
                switchSnapshot.GetDoor(
                    Level1AuthorableRoomDefinition.FinalDoorStableId).IsOpen,
                Is.True);
        }

        [Test]
        public void RoomLiveProjectionBridge_PreservesEnteredCompletedTerminalAndDropFacts()
        {
            StableId drop = Id("drop.bridge-test");
            var liveProjection = new RoomLiveView(
                RuntimeId,
                "definition-fingerprint",
                1L,
                4L,
                Level1AuthorableRoomDefinition.EntryRoomStableId,
                Level1AuthorableRoomDefinition.EntrySpawnStableId,
                false,
                new[]
                {
                    new RoomLiveRoomView(
                        Level1AuthorableRoomDefinition.EntryRoomStableId,
                        "ENTRY",
                        true,
                        true,
                        true,
                        true,
                        true,
                        Array.Empty<RoomOccupantView>(),
                        new[]
                        {
                            new RoomOccupantView(
                                Level1AuthorableRoomDefinition.MovingDroidInstanceStableId,
                                Id("enemy.mobile-blaster-droid"),
                                RoomOccupantClearRole.RequiredEnemy,
                                true),
                        },
                        Array.Empty<StableId>(),
                        new[] { drop },
                        Array.Empty<StableId>()),
                    new RoomLiveRoomView(
                        Level1AuthorableRoomDefinition.TerminalRoomStableId,
                        "TERMINAL",
                        false,
                        false,
                        false,
                        false,
                        false,
                        Array.Empty<RoomOccupantView>(),
                        Array.Empty<RoomOccupantView>(),
                        Array.Empty<StableId>(),
                        Array.Empty<StableId>(),
                        Array.Empty<StableId>()),
                });

            RoomAccessFactSnapshot facts = RoomLiveAccessFactView.Build(
                liveProjection,
                2,
                null,
                null,
                null);

            Assert.That(
                facts.Contains(
                    facts.EnteredRooms,
                    Level1AuthorableRoomDefinition.EntryRoomStableId),
                Is.True);
            Assert.That(
                facts.Contains(
                    facts.CompletedRooms,
                    Level1AuthorableRoomDefinition.EntryRoomStableId),
                Is.True);
            Assert.That(
                facts.Contains(
                    facts.TerminalEntities,
                    Level1AuthorableRoomDefinition.MovingDroidInstanceStableId),
                Is.True);
            Assert.That(facts.Contains(facts.CollectedDrops, drop), Is.True);
            Assert.That(facts.Difficulty, Is.EqualTo(2));
        }

        [Test]
        public void Definition_UnknownExternalReferenceRejectsFailClosed()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            var condition = Leaf(
                Id("access.unknown-holding"),
                RoomAccessConditionKind.HoldingPresent,
                Id("holding.not-registered"));

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                new RoomAccessDefinition(
                    graph,
                    RoomAccessReferenceCatalog.Empty,
                    new[] { condition },
                    Array.Empty<RoomDoorAccessDefinition>()));

            Assert.That(
                exception.Message,
                Does.Contain("room-access-holding-reference-unknown"));
        }

        [Test]
        public void Definition_UnknownConsumeHoldingRejectsFailClosed()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            StableId conditionId = Id("access.always-open");
            var condition = new RoomAccessConditionDefinition(
                conditionId,
                RoomAccessConditionKind.Always,
                null,
                0,
                Array.Empty<StableId>());
            var door = Door(
                Level1AuthorableRoomDefinition.EntryRoomStableId,
                Level1AuthorableRoomDefinition.ForwardDoorStableId,
                conditionId,
                Id("holding.not-registered"));

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                new RoomAccessDefinition(
                    graph,
                    RoomAccessReferenceCatalog.Empty,
                    new[] { condition },
                    new[] { door }));

            Assert.That(
                exception.Message,
                Does.Contain("room-access-consume-holding-reference-unknown"));
        }

        private static RoomAccessState Authority(
            RoomAccessDefinition definition,
            IRoomAccessFactPort facts,
            IRoomRunHoldingPort holdings)
        {
            return new RoomAccessState(
                RuntimeId,
                1L,
                definition,
                facts,
                holdings);
        }

        private static RoomAccessDefinition Definition(
            AuthorableRoomGraphDefinition graph,
            IEnumerable<RoomAccessConditionDefinition> conditions,
            IEnumerable<RoomDoorAccessDefinition> doors)
        {
            var conditionList = new List<RoomAccessConditionDefinition>(conditions);
            var doorList = new List<RoomDoorAccessDefinition>(doors);
            RoomAccessReferenceCatalog references = ReferencesFor(
                conditionList,
                doorList);
            return new RoomAccessDefinition(
                graph,
                references,
                conditionList,
                doorList);
        }

        private static RoomAccessReferenceCatalog ReferencesFor(
            IReadOnlyList<RoomAccessConditionDefinition> conditions,
            IReadOnlyList<RoomDoorAccessDefinition> doors)
        {
            var registrations = new List<RoomAccessReferenceRegistration>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < conditions.Count; index++)
            {
                RoomAccessConditionDefinition condition = conditions[index];
                RoomAccessReferenceKind kind;
                RoomAccessReferenceSource source;
                if (!TryReferenceKind(condition.Kind, out kind, out source)) continue;
                AddReference(
                    registrations,
                    seen,
                    condition.SubjectStableId,
                    kind,
                    source);
            }
            for (int index = 0; index < doors.Count; index++)
            {
                if (doors[index].ConsumeHoldingStableId == null) continue;
                AddReference(
                    registrations,
                    seen,
                    doors[index].ConsumeHoldingStableId,
                    RoomAccessReferenceKind.Holding,
                    RoomAccessReferenceSource.RunHolding);
            }
            return new RoomAccessReferenceCatalog(registrations);
        }

        private static bool TryReferenceKind(
            RoomAccessConditionKind conditionKind,
            out RoomAccessReferenceKind referenceKind,
            out RoomAccessReferenceSource source)
        {
            switch (conditionKind)
            {
                case RoomAccessConditionKind.HoldingPresent:
                case RoomAccessConditionKind.HoldingConsumed:
                    referenceKind = RoomAccessReferenceKind.Holding;
                    source = RoomAccessReferenceSource.RunHolding;
                    return true;
                case RoomAccessConditionKind.ObjectiveComplete:
                    referenceKind = RoomAccessReferenceKind.Objective;
                    source = RoomAccessReferenceSource.ObjectiveDefinition;
                    return true;
                case RoomAccessConditionKind.SwitchActive:
                    referenceKind = RoomAccessReferenceKind.Switch;
                    source = RoomAccessReferenceSource.SwitchDefinition;
                    return true;
                case RoomAccessConditionKind.CollectedDrop:
                    referenceKind = RoomAccessReferenceKind.CollectedDrop;
                    source = RoomAccessReferenceSource.ExternalDropReference;
                    return true;
                default:
                    referenceKind = default(RoomAccessReferenceKind);
                    source = default(RoomAccessReferenceSource);
                    return false;
            }
        }

        private static void AddReference(
            ICollection<RoomAccessReferenceRegistration> registrations,
            ISet<string> seen,
            StableId id,
            RoomAccessReferenceKind kind,
            RoomAccessReferenceSource source)
        {
            string key = ((int)kind) + "|" + id;
            if (!seen.Add(key)) return;
            registrations.Add(new RoomAccessReferenceRegistration(
                id,
                kind,
                source));
        }

        private static RoomAccessConditionDefinition Leaf(
            StableId id,
            RoomAccessConditionKind kind,
            StableId subject)
        {
            return new RoomAccessConditionDefinition(
                id,
                kind,
                subject,
                0,
                Array.Empty<StableId>());
        }

        private static RoomAccessConditionDefinition Difficulty(
            StableId id,
            int minimum)
        {
            return new RoomAccessConditionDefinition(
                id,
                RoomAccessConditionKind.DifficultyAtLeast,
                null,
                minimum,
                Array.Empty<StableId>());
        }

        private static RoomAccessConditionDefinition Composite(
            StableId id,
            RoomAccessConditionKind kind,
            params StableId[] children)
        {
            return new RoomAccessConditionDefinition(
                id,
                kind,
                null,
                0,
                children);
        }

        private static RoomDoorAccessDefinition Door(
            StableId room,
            StableId door,
            StableId condition,
            StableId consumeHolding = null)
        {
            return new RoomDoorAccessDefinition(
                room,
                door,
                condition,
                consumeHolding);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private static readonly StableId RuntimeId = Id("run.room-access-test");

        private sealed class FakeFactPort : IRoomAccessFactPort
        {
            private RoomAccessFactSnapshot snapshot = EmptyFacts();

            public RoomAccessFactSnapshot CurrentSnapshot => snapshot;

            public void Set(
                int difficulty = 0,
                IEnumerable<StableId> enteredRooms = null,
                IEnumerable<StableId> completedRooms = null,
                IEnumerable<StableId> terminalEntities = null,
                IEnumerable<StableId> collectedDrops = null,
                IEnumerable<StableId> completedObjectives = null,
                IEnumerable<StableId> activeSwitches = null,
                IEnumerable<StableId> consumedHoldings = null)
            {
                snapshot = new RoomAccessFactSnapshot(
                    difficulty,
                    enteredRooms,
                    completedRooms,
                    terminalEntities,
                    collectedDrops,
                    completedObjectives,
                    activeSwitches,
                    consumedHoldings);
            }

            private static RoomAccessFactSnapshot EmptyFacts()
            {
                return new RoomAccessFactSnapshot(
                    0,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }
        }

        private sealed class FakeHoldingPort : IRoomRunHoldingPort
        {
            private readonly Dictionary<StableId, int> quantities =
                new Dictionary<StableId, int>();
            private readonly Dictionary<StableId, string> operations =
                new Dictionary<StableId, string>();

            public int ConsumeCallCount { get; private set; }

            public RoomRunHoldingSnapshot CurrentSnapshot =>
                new RoomRunHoldingSnapshot(quantities);

            public void SetQuantity(StableId holdingStableId, int quantity)
            {
                if (quantity < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(quantity));
                }
                quantities[holdingStableId] = quantity;
            }

            public int Quantity(StableId holdingStableId)
            {
                int value;
                return quantities.TryGetValue(holdingStableId, out value)
                    ? value
                    : 0;
            }

            public RoomHoldingConsumeResult Consume(
                RoomHoldingConsumeCommand command)
            {
                ConsumeCallCount++;
                string payload = command.RuntimeInstanceStableId
                    + "|"
                    + command.HoldingStableId
                    + "|"
                    + command.Quantity;
                string existing;
                if (operations.TryGetValue(command.OperationStableId, out existing))
                {
                    return string.Equals(existing, payload, StringComparison.Ordinal)
                        ? new RoomHoldingConsumeResult(
                            RoomHoldingConsumeStatus.DuplicateAccepted,
                            string.Empty)
                        : new RoomHoldingConsumeResult(
                            RoomHoldingConsumeStatus.Rejected,
                            "room-holding-operation-conflict");
                }

                int current = Quantity(command.HoldingStableId);
                if (current < command.Quantity)
                {
                    operations.Add(command.OperationStableId, payload);
                    return new RoomHoldingConsumeResult(
                        RoomHoldingConsumeStatus.Rejected,
                        "room-holding-insufficient");
                }

                quantities[command.HoldingStableId] = current - command.Quantity;
                operations.Add(command.OperationStableId, payload);
                return new RoomHoldingConsumeResult(
                    RoomHoldingConsumeStatus.Applied,
                    string.Empty);
            }
        }
    }
}
