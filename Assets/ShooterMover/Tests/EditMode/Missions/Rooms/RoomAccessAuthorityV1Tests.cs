using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Missions.Rooms
{
    public sealed class RoomAccessAuthorityV1Tests
    {
        [Test]
        public void HoldingPresentDoor_IsClosedBeforePickupAndOpenAfterPickup()
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            StableId key = Id("holding.blue-key");
            StableId condition = Id("access.blue-key-present");
            RoomAccessDefinitionV1 definition = Definition(
                graph,
                new[]
                {
                    Leaf(
                        condition,
                        RoomAccessConditionKindV1.HoldingPresent,
                        key),
                },
                new[]
                {
                    Door(
                        Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                        Level1AuthorableRoomDefinitionV1.ForwardDoorStableId,
                        condition),
                });
            var facts = new FakeFactPort();
            var holdings = new FakeHoldingPort();
            var authority = Authority(definition, facts, holdings);

            Assert.That(
                authority.CurrentSnapshot.GetDoor(
                    Level1AuthorableRoomDefinitionV1.ForwardDoorStableId).IsOpen,
                Is.False);

            holdings.SetQuantity(key, 1);

            RoomDoorAccessProjectionV1 projection = authority.CurrentSnapshot.GetDoor(
                Level1AuthorableRoomDefinitionV1.ForwardDoorStableId);
            Assert.That(projection.IsConditionSatisfied, Is.True);
            Assert.That(projection.IsOpen, Is.True);
            Assert.That(projection.IsUnlocked, Is.False);
        }

        [Test]
        public void ConsumingKey_UnlocksOnceAndExactReplayDoesNotConsumeTwice()
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            StableId key = Id("holding.consumable-key");
            StableId keyPresent = Id("access.consumable-key-present");
            StableId keyConsumed = Id("access.consumable-key-consumed");
            RoomAccessDefinitionV1 definition = Definition(
                graph,
                new[]
                {
                    Leaf(
                        keyPresent,
                        RoomAccessConditionKindV1.HoldingPresent,
                        key),
                    Leaf(
                        keyConsumed,
                        RoomAccessConditionKindV1.HoldingConsumed,
                        key),
                },
                new[]
                {
                    Door(
                        Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                        Level1AuthorableRoomDefinitionV1.ForwardDoorStableId,
                        keyPresent,
                        key),
                });
            var facts = new FakeFactPort();
            var holdings = new FakeHoldingPort();
            holdings.SetQuantity(key, 1);
            RoomAccessAuthorityV1 authority = Authority(definition, facts, holdings);
            var command = new UnlockRoomDoorCommandV1(
                RuntimeId,
                Id("operation.unlock-forward"),
                1L,
                Level1AuthorableRoomDefinitionV1.ForwardDoorStableId);

            RoomAccessOperationResultV1 first = authority.TryUnlock(command);
            RoomAccessOperationResultV1 replay = authority.TryUnlock(command);

            Assert.That(first.Status, Is.EqualTo(RoomAccessOperationStatusV1.Applied));
            Assert.That(
                replay.Status,
                Is.EqualTo(RoomAccessOperationStatusV1.DuplicateNoChange));
            Assert.That(holdings.ConsumeCallCount, Is.EqualTo(1));
            Assert.That(holdings.Quantity(key), Is.EqualTo(0));
            Assert.That(authority.IsConditionSatisfied(keyConsumed), Is.True);
            RoomDoorAccessProjectionV1 door = replay.Snapshot.GetDoor(
                Level1AuthorableRoomDefinitionV1.ForwardDoorStableId);
            Assert.That(door.IsUnlocked, Is.True);
            Assert.That(door.IsOpen, Is.True);
        }

        [Test]
        public void ConflictingOperationId_RejectsWithoutAdditionalConsumption()
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            StableId key = Id("holding.conflict-key");
            StableId condition = Id("access.conflict-key-present");
            RoomAccessDefinitionV1 definition = Definition(
                graph,
                new[]
                {
                    Leaf(
                        condition,
                        RoomAccessConditionKindV1.HoldingPresent,
                        key),
                },
                new[]
                {
                    Door(
                        Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                        Level1AuthorableRoomDefinitionV1.ForwardDoorStableId,
                        condition,
                        key),
                    Door(
                        Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                        Level1AuthorableRoomDefinitionV1.FinalDoorStableId,
                        condition,
                        key),
                });
            var holdings = new FakeHoldingPort();
            holdings.SetQuantity(key, 2);
            RoomAccessAuthorityV1 authority = Authority(
                definition,
                new FakeFactPort(),
                holdings);
            StableId operation = Id("operation.unlock-conflict");

            RoomAccessOperationResultV1 first = authority.TryUnlock(
                new UnlockRoomDoorCommandV1(
                    RuntimeId,
                    operation,
                    1L,
                    Level1AuthorableRoomDefinitionV1.ForwardDoorStableId));
            RoomAccessOperationResultV1 conflict = authority.TryUnlock(
                new UnlockRoomDoorCommandV1(
                    RuntimeId,
                    operation,
                    1L,
                    Level1AuthorableRoomDefinitionV1.FinalDoorStableId));

            Assert.That(first.Status, Is.EqualTo(RoomAccessOperationStatusV1.Applied));
            Assert.That(conflict.Status, Is.EqualTo(RoomAccessOperationStatusV1.Rejected));
            Assert.That(conflict.RejectionCode, Is.EqualTo("room-access-operation-conflict"));
            Assert.That(holdings.ConsumeCallCount, Is.EqualTo(1));
            Assert.That(holdings.Quantity(key), Is.EqualTo(1));
        }

        [Test]
        public void AllAnyAndNotTrees_EvaluateDeterministically()
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            StableId switchA = Id("switch.power-a");
            StableId switchB = Id("switch.power-b");
            StableId a = Id("access.switch-a");
            StableId b = Id("access.switch-b");
            StableId difficulty = Id("access.difficulty-three");
            StableId notB = Id("access.not-switch-b");
            StableId any = Id("access.either-switch");
            StableId all = Id("access.compound-gate");
            RoomAccessDefinitionV1 definition = Definition(
                graph,
                new[]
                {
                    Leaf(a, RoomAccessConditionKindV1.SwitchActive, switchA),
                    Leaf(b, RoomAccessConditionKindV1.SwitchActive, switchB),
                    Difficulty(difficulty, 3),
                    Composite(notB, RoomAccessConditionKindV1.Not, b),
                    Composite(any, RoomAccessConditionKindV1.Any, a, b),
                    Composite(all, RoomAccessConditionKindV1.All, any, notB, difficulty),
                },
                new[]
                {
                    Door(
                        Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                        Level1AuthorableRoomDefinitionV1.ForwardDoorStableId,
                        all),
                });
            var facts = new FakeFactPort();
            RoomAccessAuthorityV1 authority = Authority(
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
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
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
            RoomAccessDefinitionV1 definition = Definition(
                graph,
                new[]
                {
                    Leaf(
                        roomEntered,
                        RoomAccessConditionKindV1.RoomEntered,
                        Level1AuthorableRoomDefinitionV1.EntryRoomStableId),
                    Leaf(
                        roomComplete,
                        RoomAccessConditionKindV1.RoomComplete,
                        Level1AuthorableRoomDefinitionV1.EntryRoomStableId),
                    Leaf(
                        terminal,
                        RoomAccessConditionKindV1.ExactEntityTerminal,
                        Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId),
                    Leaf(drop, RoomAccessConditionKindV1.CollectedDrop, dropId),
                    Leaf(
                        objective,
                        RoomAccessConditionKindV1.ObjectiveComplete,
                        objectiveId),
                    Leaf(
                        switchCondition,
                        RoomAccessConditionKindV1.SwitchActive,
                        switchId),
                    Composite(
                        root,
                        RoomAccessConditionKindV1.All,
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
                        Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                        Level1AuthorableRoomDefinitionV1.ForwardDoorStableId,
                        root),
                });
            var facts = new FakeFactPort();
            RoomAccessAuthorityV1 authority = Authority(
                definition,
                facts,
                new FakeHoldingPort());

            facts.Set(
                enteredRooms: new[] { Level1AuthorableRoomDefinitionV1.EntryRoomStableId },
                completedRooms: new[] { Level1AuthorableRoomDefinitionV1.EntryRoomStableId },
                terminalEntities: new[]
                {
                    Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId,
                },
                collectedDrops: new[] { dropId },
                completedObjectives: new[] { objectiveId },
                activeSwitches: new[] { switchId });

            Assert.That(authority.IsConditionSatisfied(root), Is.True);
            Assert.That(
                authority.CurrentSnapshot.GetDoor(
                    Level1AuthorableRoomDefinitionV1.ForwardDoorStableId).IsOpen,
                Is.True);
        }

        [Test]
        public void DifferentDoors_HaveIndependentAuthoredConditions()
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            StableId key = Id("holding.independent-key");
            StableId switchId = Id("switch.independent-power");
            StableId keyCondition = Id("access.independent-key");
            StableId switchCondition = Id("access.independent-switch");
            RoomAccessDefinitionV1 definition = Definition(
                graph,
                new[]
                {
                    Leaf(
                        keyCondition,
                        RoomAccessConditionKindV1.HoldingPresent,
                        key),
                    Leaf(
                        switchCondition,
                        RoomAccessConditionKindV1.SwitchActive,
                        switchId),
                },
                new[]
                {
                    Door(
                        Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                        Level1AuthorableRoomDefinitionV1.ReturnDoorStableId,
                        keyCondition),
                    Door(
                        Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                        Level1AuthorableRoomDefinitionV1.FinalDoorStableId,
                        switchCondition),
                });
            var facts = new FakeFactPort();
            var holdings = new FakeHoldingPort();
            RoomAccessAuthorityV1 authority = Authority(definition, facts, holdings);

            holdings.SetQuantity(key, 1);
            RoomAccessSnapshotV1 keySnapshot = authority.CurrentSnapshot;
            Assert.That(
                keySnapshot.GetDoor(
                    Level1AuthorableRoomDefinitionV1.ReturnDoorStableId).IsOpen,
                Is.True);
            Assert.That(
                keySnapshot.GetDoor(
                    Level1AuthorableRoomDefinitionV1.FinalDoorStableId).IsOpen,
                Is.False);

            holdings.SetQuantity(key, 0);
            facts.Set(activeSwitches: new[] { switchId });
            RoomAccessSnapshotV1 switchSnapshot = authority.CurrentSnapshot;
            Assert.That(
                switchSnapshot.GetDoor(
                    Level1AuthorableRoomDefinitionV1.ReturnDoorStableId).IsOpen,
                Is.False);
            Assert.That(
                switchSnapshot.GetDoor(
                    Level1AuthorableRoomDefinitionV1.FinalDoorStableId).IsOpen,
                Is.True);
        }

        [Test]
        public void RoomLiveProjectionBridge_PreservesEnteredCompletedTerminalAndDropFacts()
        {
            StableId drop = Id("drop.bridge-test");
            var liveProjection = new RoomLiveRuntimeProjectionV1(
                RuntimeId,
                "definition-fingerprint",
                1L,
                4L,
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                Level1AuthorableRoomDefinitionV1.EntrySpawnStableId,
                false,
                new[]
                {
                    new RoomLiveRoomProjectionV1(
                        Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                        "ENTRY",
                        true,
                        true,
                        true,
                        true,
                        true,
                        Array.Empty<RoomOccupantProjectionV1>(),
                        new[]
                        {
                            new RoomOccupantProjectionV1(
                                Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId,
                                Id("enemy.mobile-blaster-droid"),
                                RoomOccupantClearRoleV1.RequiredEnemy,
                                true),
                        },
                        Array.Empty<StableId>(),
                        new[] { drop },
                        Array.Empty<StableId>()),
                    new RoomLiveRoomProjectionV1(
                        Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                        "TERMINAL",
                        false,
                        false,
                        false,
                        false,
                        false,
                        Array.Empty<RoomOccupantProjectionV1>(),
                        Array.Empty<RoomOccupantProjectionV1>(),
                        Array.Empty<StableId>(),
                        Array.Empty<StableId>(),
                        Array.Empty<StableId>()),
                });

            RoomAccessFactSnapshotV1 facts = RoomLiveAccessFactProjectionV1.Build(
                liveProjection,
                2,
                null,
                null,
                null);

            Assert.That(
                facts.Contains(
                    facts.EnteredRooms,
                    Level1AuthorableRoomDefinitionV1.EntryRoomStableId),
                Is.True);
            Assert.That(
                facts.Contains(
                    facts.CompletedRooms,
                    Level1AuthorableRoomDefinitionV1.EntryRoomStableId),
                Is.True);
            Assert.That(
                facts.Contains(
                    facts.TerminalEntities,
                    Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId),
                Is.True);
            Assert.That(facts.Contains(facts.CollectedDrops, drop), Is.True);
            Assert.That(facts.Difficulty, Is.EqualTo(2));
        }

        [Test]
        public void Definition_UnknownExternalReferenceRejectsFailClosed()
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            var condition = Leaf(
                Id("access.unknown-holding"),
                RoomAccessConditionKindV1.HoldingPresent,
                Id("holding.not-registered"));

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                new RoomAccessDefinitionV1(
                    graph,
                    RoomAccessReferenceCatalogV1.Empty,
                    new[] { condition },
                    Array.Empty<RoomDoorAccessDefinitionV1>()));

            Assert.That(
                exception.Message,
                Does.Contain("room-access-holding-reference-unknown"));
        }

        [Test]
        public void Definition_UnknownConsumeHoldingRejectsFailClosed()
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            StableId conditionId = Id("access.always-open");
            var condition = new RoomAccessConditionDefinitionV1(
                conditionId,
                RoomAccessConditionKindV1.Always,
                null,
                0,
                Array.Empty<StableId>());
            var door = Door(
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                Level1AuthorableRoomDefinitionV1.ForwardDoorStableId,
                conditionId,
                Id("holding.not-registered"));

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                new RoomAccessDefinitionV1(
                    graph,
                    RoomAccessReferenceCatalogV1.Empty,
                    new[] { condition },
                    new[] { door }));

            Assert.That(
                exception.Message,
                Does.Contain("room-access-consume-holding-reference-unknown"));
        }

        private static RoomAccessAuthorityV1 Authority(
            RoomAccessDefinitionV1 definition,
            IRoomAccessFactPortV1 facts,
            IRoomRunHoldingPortV1 holdings)
        {
            return new RoomAccessAuthorityV1(
                RuntimeId,
                1L,
                definition,
                facts,
                holdings);
        }

        private static RoomAccessDefinitionV1 Definition(
            AuthorableRoomGraphDefinitionV1 graph,
            IEnumerable<RoomAccessConditionDefinitionV1> conditions,
            IEnumerable<RoomDoorAccessDefinitionV1> doors)
        {
            var conditionList = new List<RoomAccessConditionDefinitionV1>(conditions);
            var doorList = new List<RoomDoorAccessDefinitionV1>(doors);
            RoomAccessReferenceCatalogV1 references = ReferencesFor(
                conditionList,
                doorList);
            return new RoomAccessDefinitionV1(
                graph,
                references,
                conditionList,
                doorList);
        }

        private static RoomAccessReferenceCatalogV1 ReferencesFor(
            IReadOnlyList<RoomAccessConditionDefinitionV1> conditions,
            IReadOnlyList<RoomDoorAccessDefinitionV1> doors)
        {
            var registrations = new List<RoomAccessReferenceRegistrationV1>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < conditions.Count; index++)
            {
                RoomAccessConditionDefinitionV1 condition = conditions[index];
                RoomAccessReferenceKindV1 kind;
                RoomAccessReferenceSourceV1 source;
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
                    RoomAccessReferenceKindV1.Holding,
                    RoomAccessReferenceSourceV1.RunHolding);
            }
            return new RoomAccessReferenceCatalogV1(registrations);
        }

        private static bool TryReferenceKind(
            RoomAccessConditionKindV1 conditionKind,
            out RoomAccessReferenceKindV1 referenceKind,
            out RoomAccessReferenceSourceV1 source)
        {
            switch (conditionKind)
            {
                case RoomAccessConditionKindV1.HoldingPresent:
                case RoomAccessConditionKindV1.HoldingConsumed:
                    referenceKind = RoomAccessReferenceKindV1.Holding;
                    source = RoomAccessReferenceSourceV1.RunHolding;
                    return true;
                case RoomAccessConditionKindV1.ObjectiveComplete:
                    referenceKind = RoomAccessReferenceKindV1.Objective;
                    source = RoomAccessReferenceSourceV1.ObjectiveDefinition;
                    return true;
                case RoomAccessConditionKindV1.SwitchActive:
                    referenceKind = RoomAccessReferenceKindV1.Switch;
                    source = RoomAccessReferenceSourceV1.SwitchDefinition;
                    return true;
                case RoomAccessConditionKindV1.CollectedDrop:
                    referenceKind = RoomAccessReferenceKindV1.CollectedDrop;
                    source = RoomAccessReferenceSourceV1.ExternalDropReference;
                    return true;
                default:
                    referenceKind = default(RoomAccessReferenceKindV1);
                    source = default(RoomAccessReferenceSourceV1);
                    return false;
            }
        }

        private static void AddReference(
            ICollection<RoomAccessReferenceRegistrationV1> registrations,
            ISet<string> seen,
            StableId id,
            RoomAccessReferenceKindV1 kind,
            RoomAccessReferenceSourceV1 source)
        {
            string key = ((int)kind) + "|" + id;
            if (!seen.Add(key)) return;
            registrations.Add(new RoomAccessReferenceRegistrationV1(
                id,
                kind,
                source));
        }

        private static RoomAccessConditionDefinitionV1 Leaf(
            StableId id,
            RoomAccessConditionKindV1 kind,
            StableId subject)
        {
            return new RoomAccessConditionDefinitionV1(
                id,
                kind,
                subject,
                0,
                Array.Empty<StableId>());
        }

        private static RoomAccessConditionDefinitionV1 Difficulty(
            StableId id,
            int minimum)
        {
            return new RoomAccessConditionDefinitionV1(
                id,
                RoomAccessConditionKindV1.DifficultyAtLeast,
                null,
                minimum,
                Array.Empty<StableId>());
        }

        private static RoomAccessConditionDefinitionV1 Composite(
            StableId id,
            RoomAccessConditionKindV1 kind,
            params StableId[] children)
        {
            return new RoomAccessConditionDefinitionV1(
                id,
                kind,
                null,
                0,
                children);
        }

        private static RoomDoorAccessDefinitionV1 Door(
            StableId room,
            StableId door,
            StableId condition,
            StableId consumeHolding = null)
        {
            return new RoomDoorAccessDefinitionV1(
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

        private sealed class FakeFactPort : IRoomAccessFactPortV1
        {
            private RoomAccessFactSnapshotV1 snapshot = EmptyFacts();

            public RoomAccessFactSnapshotV1 CurrentSnapshot => snapshot;

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
                snapshot = new RoomAccessFactSnapshotV1(
                    difficulty,
                    enteredRooms,
                    completedRooms,
                    terminalEntities,
                    collectedDrops,
                    completedObjectives,
                    activeSwitches,
                    consumedHoldings);
            }

            private static RoomAccessFactSnapshotV1 EmptyFacts()
            {
                return new RoomAccessFactSnapshotV1(
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

        private sealed class FakeHoldingPort : IRoomRunHoldingPortV1
        {
            private readonly Dictionary<StableId, int> quantities =
                new Dictionary<StableId, int>();
            private readonly Dictionary<StableId, string> operations =
                new Dictionary<StableId, string>();

            public int ConsumeCallCount { get; private set; }

            public RoomRunHoldingSnapshotV1 CurrentSnapshot =>
                new RoomRunHoldingSnapshotV1(quantities);

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

            public RoomHoldingConsumeResultV1 Consume(
                RoomHoldingConsumeCommandV1 command)
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
                        ? new RoomHoldingConsumeResultV1(
                            RoomHoldingConsumeStatusV1.DuplicateAccepted,
                            string.Empty)
                        : new RoomHoldingConsumeResultV1(
                            RoomHoldingConsumeStatusV1.Rejected,
                            "room-holding-operation-conflict");
                }

                int current = Quantity(command.HoldingStableId);
                if (current < command.Quantity)
                {
                    operations.Add(command.OperationStableId, payload);
                    return new RoomHoldingConsumeResultV1(
                        RoomHoldingConsumeStatusV1.Rejected,
                        "room-holding-insufficient");
                }

                quantities[command.HoldingStableId] = current - command.Quantity;
                operations.Add(command.OperationStableId, payload);
                return new RoomHoldingConsumeResultV1(
                    RoomHoldingConsumeStatusV1.Applied,
                    string.Empty);
            }
        }
    }
}
