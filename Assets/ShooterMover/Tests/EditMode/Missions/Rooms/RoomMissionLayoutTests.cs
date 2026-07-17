using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;

namespace ShooterMover.Tests.EditMode.Missions.Rooms
{
    public sealed class RoomMissionLayoutTests
    {
        [Test]
        public void LevelOneDefinition_IsValidOrderedAndTraversable()
        {
            RoomGraphDefinitionV1 definition =
                Level1RoomGraphDefinitionV1.Create();

            Assert.That(definition.LayoutStableId, Is.EqualTo(
                Level1RoomGraphDefinitionV1.LayoutStableId));
            Assert.That(definition.Rooms.Count, Is.EqualTo(2));
            Assert.That(definition.Rooms[0].RoomStableId, Is.EqualTo(
                Level1RoomGraphDefinitionV1.EntryRoomStableId));
            Assert.That(definition.Rooms[1].RoomStableId, Is.EqualTo(
                Level1RoomGraphDefinitionV1.TerminalRoomStableId));
            Assert.That(definition.Connections.Count, Is.EqualTo(1));
            Assert.That(
                definition.Connections[0].Directionality,
                Is.EqualTo(RoomConnectionDirectionalityV1.Bidirectional));
            Assert.That(
                definition.GetExitsFromRoom(
                    Level1RoomGraphDefinitionV1.EntryRoomStableId)[0].ExitType,
                Is.EqualTo(RoomExitTypeV1.Progression));
            Assert.That(
                definition.GetExitsFromRoom(
                    Level1RoomGraphDefinitionV1.TerminalRoomStableId)[0].ExitType,
                Is.EqualTo(RoomExitTypeV1.Return));
            Assert.That(definition.Fingerprint, Does.StartWith("sha256:"));
        }

        [Test]
        public void EquivalentDefinitions_HaveDeterministicFingerprint()
        {
            GraphFixture first = GraphFixture.Create();
            GraphFixture second = GraphFixture.Create();
            second.Rooms.Reverse();
            second.Entries.Reverse();
            second.Connections.Reverse();
            second.DoorLinks.Reverse();
            RoomConnectionDefinitionV1 connection = second.Connections[0];
            second.Connections[0] = new RoomConnectionDefinitionV1(
                connection.ConnectionStableId,
                connection.Directionality,
                connection.DoorLinkStableId,
                new[] { connection.Exits[1], connection.Exits[0] });

            RoomGraphValidationResultV1 firstResult = first.Validate();
            RoomGraphValidationResultV1 secondResult = second.Validate();

            Assert.That(firstResult.IsValid, Is.True, Describe(firstResult));
            Assert.That(secondResult.IsValid, Is.True, Describe(secondResult));
            Assert.That(
                secondResult.Definition.Fingerprint,
                Is.EqualTo(firstResult.Definition.Fingerprint));
            Assert.That(
                secondResult.Definition.ToCanonicalString(),
                Is.EqualTo(firstResult.Definition.ToCanonicalString()));
        }

        [Test]
        public void DuplicateRoomIdentity_IsRejected()
        {
            GraphFixture fixture = GraphFixture.Create();
            fixture.Rooms.Add(new RoomDefinitionV1(
                fixture.StartRoomId,
                2,
                RoomInitialAvailabilityV1.Locked,
                false));

            RoomGraphValidationResultV1 result = fixture.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.HasCode(RoomGraphValidationCodeV1.DuplicateRoomStableId),
                Is.True,
                Describe(result));
        }

        [Test]
        public void MissingExitReferences_AreRejected()
        {
            GraphFixture fixture = GraphFixture.Create();
            RoomExitDefinitionV1 invalid = new RoomExitDefinitionV1(
                StableId.Parse("exit.invalid-reference"),
                StableId.Parse("room.missing-source"),
                StableId.Parse("entry.missing-target"),
                0,
                RoomExitTypeV1.Progression,
                false,
                null);
            fixture.Connections[0] = new RoomConnectionDefinitionV1(
                fixture.ConnectionId,
                RoomConnectionDirectionalityV1.OneWay,
                fixture.DoorLinkId,
                new[] { invalid });

            RoomGraphValidationResultV1 result = fixture.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.HasCode(
                RoomGraphValidationCodeV1.MissingExitSourceRoomReference),
                Is.True,
                Describe(result));
            Assert.That(result.HasCode(
                RoomGraphValidationCodeV1.MissingExitTargetEntryReference),
                Is.True,
                Describe(result));
        }

        [Test]
        public void DanglingDoorLink_IsRejected()
        {
            GraphFixture fixture = GraphFixture.Create();
            fixture.Connections[0] = new RoomConnectionDefinitionV1(
                fixture.ConnectionId,
                RoomConnectionDirectionalityV1.Bidirectional,
                StableId.Parse("door-link.undefined"),
                fixture.Connections[0].Exits);
            fixture.DoorLinks.Clear();

            RoomGraphValidationResultV1 result = fixture.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.HasCode(RoomGraphValidationCodeV1.DanglingDoorLink),
                Is.True,
                Describe(result));
        }

        [Test]
        public void SelfLink_IsRejected()
        {
            GraphFixture fixture = GraphFixture.Create();
            RoomExitDefinitionV1 self = new RoomExitDefinitionV1(
                fixture.ForwardExitId,
                fixture.StartRoomId,
                fixture.StartEntryId,
                0,
                RoomExitTypeV1.Progression,
                false,
                null);
            fixture.Connections[0] = new RoomConnectionDefinitionV1(
                fixture.ConnectionId,
                RoomConnectionDirectionalityV1.OneWay,
                fixture.DoorLinkId,
                new[] { self });

            RoomGraphValidationResultV1 result = fixture.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.HasCode(RoomGraphValidationCodeV1.SelfLink),
                Is.True,
                Describe(result));
        }

        [Test]
        public void MismatchedBidirectionalExits_AreRejected()
        {
            GraphFixture fixture = GraphFixture.Create();
            RoomExitDefinitionV1 first = fixture.Connections[0].Exits[0];
            RoomExitDefinitionV1 second = new RoomExitDefinitionV1(
                fixture.ReturnExitId,
                fixture.StartRoomId,
                fixture.TerminalEntryId,
                1,
                RoomExitTypeV1.Return,
                false,
                null);
            fixture.Connections[0] = new RoomConnectionDefinitionV1(
                fixture.ConnectionId,
                RoomConnectionDirectionalityV1.Bidirectional,
                fixture.DoorLinkId,
                new[] { first, second });

            RoomGraphValidationResultV1 result = fixture.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.HasCode(
                RoomGraphValidationCodeV1.MismatchedReverseLink),
                Is.True,
                Describe(result));
        }

        [Test]
        public void UnreachableRequiredAndTerminalRoom_AreRejected()
        {
            GraphFixture fixture = GraphFixture.Create();
            fixture.Connections.Clear();
            fixture.DoorLinks.Clear();

            RoomGraphValidationResultV1 result = fixture.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.HasCode(
                RoomGraphValidationCodeV1.UnreachableRequiredRoom),
                Is.True,
                Describe(result));
            Assert.That(result.HasCode(
                RoomGraphValidationCodeV1.UnreachableTerminalRoom),
                Is.True,
                Describe(result));
        }

        [Test]
        public void InvalidStartAndTerminalRooms_AreRejected()
        {
            GraphFixture fixture = GraphFixture.Create();
            fixture.StartRoomId = StableId.Parse("room.undefined-start");
            fixture.TerminalRoomId = StableId.Parse("room.undefined-terminal");

            RoomGraphValidationResultV1 result = fixture.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.HasCode(RoomGraphValidationCodeV1.InvalidStartRoom),
                Is.True,
                Describe(result));
            Assert.That(
                result.HasCode(RoomGraphValidationCodeV1.InvalidTerminalRoom),
                Is.True,
                Describe(result));
        }

        [Test]
        public void StateTransitions_TrackLockedAvailableCurrentVisitedAndCompleted()
        {
            var layout = new RoomMissionLayoutV1(
                Level1RoomGraphDefinitionV1.Create());

            AssertRoom(
                layout.GetRoomState(
                    Level1RoomGraphDefinitionV1.EntryRoomStableId),
                RoomAvailabilityStateV1.Available,
                true,
                true,
                false);
            AssertRoom(
                layout.GetRoomState(
                    Level1RoomGraphDefinitionV1.TerminalRoomStableId),
                RoomAvailabilityStateV1.Locked,
                false,
                false,
                false);
            Assert.That(
                layout.GetExitState(
                    Level1RoomGraphDefinitionV1.ForwardExitStableId).IsAvailable,
                Is.False);
            Assert.That(
                layout.Traverse(
                    Level1RoomGraphDefinitionV1.ForwardExitStableId).Status,
                Is.EqualTo(RoomGraphOperationStatusV1.ExitLocked));

            RoomGraphOperationResultV1 completed = layout.CompleteCurrentRoom();
            RoomGraphOperationResultV1 traversed = layout.Traverse(
                Level1RoomGraphDefinitionV1.ForwardExitStableId);

            Assert.That(completed.Status, Is.EqualTo(
                RoomGraphOperationStatusV1.Applied));
            Assert.That(traversed.Status, Is.EqualTo(
                RoomGraphOperationStatusV1.Applied));
            AssertRoom(
                layout.GetRoomState(
                    Level1RoomGraphDefinitionV1.EntryRoomStableId),
                RoomAvailabilityStateV1.Available,
                false,
                true,
                true);
            AssertRoom(
                layout.GetRoomState(
                    Level1RoomGraphDefinitionV1.TerminalRoomStableId),
                RoomAvailabilityStateV1.Available,
                true,
                true,
                false);
            Assert.That(
                layout.GetExitState(
                    Level1RoomGraphDefinitionV1.ForwardExitStableId).IsAvailable,
                Is.True);
            Assert.That(
                layout.GetExitState(
                    Level1RoomGraphDefinitionV1.ReturnExitStableId).IsAvailable,
                Is.True);
            Assert.That(layout.CurrentSnapshot.Sequence, Is.EqualTo(2L));
        }

        [Test]
        public void Restart_RestoresExactInitialSnapshot()
        {
            var fresh = new RoomMissionLayoutV1(
                Level1RoomGraphDefinitionV1.Create());
            string initialFingerprint = fresh.CurrentSnapshot.Fingerprint;
            fresh.CompleteCurrentRoom();
            fresh.Traverse(
                Level1RoomGraphDefinitionV1.ForwardExitStableId);
            fresh.CompleteCurrentRoom();

            RoomGraphOperationResultV1 restart = fresh.Restart();

            Assert.That(restart.Status, Is.EqualTo(
                RoomGraphOperationStatusV1.Applied));
            Assert.That(fresh.CurrentSnapshot.Sequence, Is.Zero);
            Assert.That(
                fresh.CurrentSnapshot.Fingerprint,
                Is.EqualTo(initialFingerprint));
            Assert.That(
                fresh.CurrentRoomState.RoomStableId,
                Is.EqualTo(Level1RoomGraphDefinitionV1.EntryRoomStableId));
            Assert.That(
                fresh.GetRoomState(
                    Level1RoomGraphDefinitionV1.TerminalRoomStableId).Availability,
                Is.EqualTo(RoomAvailabilityStateV1.Locked));
            Assert.That(fresh.Restart().Status, Is.EqualTo(
                RoomGraphOperationStatusV1.NoChange));
        }

        [Test]
        public void SnapshotRoundTrip_IsCanonicalAndRestartSafe()
        {
            RoomGraphDefinitionV1 definition =
                Level1RoomGraphDefinitionV1.Create();
            var original = new RoomMissionLayoutV1(definition);
            original.CompleteCurrentRoom();
            original.Traverse(
                Level1RoomGraphDefinitionV1.ForwardExitStableId);
            RoomGraphSnapshotV1 exported = original.CurrentSnapshot;
            var reversedRooms = new List<RoomStateSnapshotV1>(exported.Rooms);
            var reversedExits = new List<RoomExitStateSnapshotV1>(exported.Exits);
            reversedRooms.Reverse();
            reversedExits.Reverse();
            RoomGraphSnapshotV1 reordered = RoomGraphSnapshotV1.CreateCanonical(
                exported.LayoutStableId,
                exported.DefinitionFingerprint,
                exported.Sequence,
                reversedRooms,
                reversedExits);
            var restored = new RoomMissionLayoutV1(definition);

            RoomGraphImportResultV1 result = restored.TryImport(reordered);

            Assert.That(reordered.Fingerprint, Is.EqualTo(exported.Fingerprint));
            Assert.That(result.Status, Is.EqualTo(RoomGraphImportStatusV1.Imported));
            Assert.That(
                restored.CurrentSnapshot.Fingerprint,
                Is.EqualTo(exported.Fingerprint));
            Assert.That(
                restored.CurrentRoomState.RoomStableId,
                Is.EqualTo(Level1RoomGraphDefinitionV1.TerminalRoomStableId));
            restored.Restart();
            Assert.That(restored.CurrentSnapshot.Sequence, Is.Zero);
            Assert.That(
                restored.CurrentRoomState.RoomStableId,
                Is.EqualTo(Level1RoomGraphDefinitionV1.EntryRoomStableId));
        }

        [Test]
        public void CorruptSnapshot_IsRejectedAtomically()
        {
            var layout = new RoomMissionLayoutV1(
                Level1RoomGraphDefinitionV1.Create());
            layout.CompleteCurrentRoom();
            RoomGraphSnapshotV1 before = layout.CurrentSnapshot;
            var corrupt = new RoomGraphSnapshotV1(
                before.SchemaVersion,
                before.LayoutStableId,
                before.DefinitionFingerprint,
                before.Sequence + 1L,
                before.Rooms,
                before.Exits,
                before.Fingerprint);

            RoomGraphImportResultV1 result = layout.TryImport(corrupt);

            Assert.That(
                result.Status,
                Is.EqualTo(RoomGraphImportStatusV1.FingerprintMismatch));
            Assert.That(layout.CurrentSnapshot, Is.SameAs(before));
            Assert.That(
                layout.CurrentRoomState.RoomStableId,
                Is.EqualTo(Level1RoomGraphDefinitionV1.EntryRoomStableId));
        }

        [Test]
        public void SnapshotFromDifferentDefinition_IsRejectedBeforeMutation()
        {
            RoomGraphDefinitionV1 sourceDefinition =
                Level1RoomGraphDefinitionV1.Create();
            var source = new RoomMissionLayoutV1(sourceDefinition);
            source.CompleteCurrentRoom();
            RoomGraphSnapshotV1 snapshot = source.CurrentSnapshot;
            GraphFixture alteredFixture = GraphFixture.Create();
            RoomConnectionDefinitionV1 originalConnection =
                alteredFixture.Connections[0];
            RoomExitDefinitionV1 originalForward = originalConnection.Exits[0];
            RoomExitDefinitionV1 changedForward = new RoomExitDefinitionV1(
                originalForward.ExitStableId,
                originalForward.SourceRoomStableId,
                originalForward.TargetEntryStableId,
                originalForward.Order,
                RoomExitTypeV1.Optional,
                originalForward.InitiallyLocked,
                originalForward.UnlockRequiredCompletedRoomStableId);
            alteredFixture.Connections[0] = new RoomConnectionDefinitionV1(
                originalConnection.ConnectionStableId,
                originalConnection.Directionality,
                originalConnection.DoorLinkStableId,
                new[] { changedForward, originalConnection.Exits[1] });
            RoomGraphValidationResultV1 alteredResult = alteredFixture.Validate();
            Assert.That(alteredResult.IsValid, Is.True, Describe(alteredResult));
            var target = new RoomMissionLayoutV1(alteredResult.Definition);
            RoomGraphSnapshotV1 before = target.CurrentSnapshot;

            RoomGraphImportResultV1 result = target.TryImport(snapshot);

            Assert.That(result.Status, Is.EqualTo(
                RoomGraphImportStatusV1.DefinitionFingerprintMismatch));
            Assert.That(target.CurrentSnapshot, Is.SameAs(before));
        }

        [Test]
        public void DebugProjection_ContainsStableTopologyAndStateFacts()
        {
            var layout = new RoomMissionLayoutV1(
                Level1RoomGraphDefinitionV1.Create());
            layout.CompleteCurrentRoom();

            string projection = layout.CreateDebugProjection();

            Assert.That(projection, Does.Contain(
                Level1RoomGraphDefinitionV1.EntryRoomStableId.ToString()));
            Assert.That(projection, Does.Contain(
                Level1RoomGraphDefinitionV1.TerminalRoomStableId.ToString()));
            Assert.That(projection, Does.Contain("type=Progression"));
            Assert.That(projection, Does.Contain(
                Level1RoomGraphDefinitionV1.DoorLinkStableId.ToString()));
            Assert.That(projection, Does.Contain("completed=1"));
        }

        [Test]
        public void RoomGraphAssemblies_HaveNoUnityEngineDependency()
        {
            AssertNoUnityReference(typeof(RoomGraphDefinitionV1).Assembly);
            AssertNoUnityReference(typeof(IRoomMissionLayoutV1).Assembly);
            AssertNoUnityReference(typeof(RoomMissionLayoutV1).Assembly);
        }

        private static void AssertRoom(
            RoomRuntimeStateV1 state,
            RoomAvailabilityStateV1 availability,
            bool current,
            bool visited,
            bool completed)
        {
            Assert.That(state.Availability, Is.EqualTo(availability));
            Assert.That(state.IsCurrent, Is.EqualTo(current));
            Assert.That(state.IsVisited, Is.EqualTo(visited));
            Assert.That(state.IsCompleted, Is.EqualTo(completed));
        }

        private static string Describe(RoomGraphValidationResultV1 result)
        {
            if (result == null)
            {
                return "validation result was null";
            }

            var messages = new List<string>();
            for (int index = 0; index < result.Issues.Count; index++)
            {
                RoomGraphValidationIssueV1 issue = result.Issues[index];
                messages.Add(issue.Code + "[" + issue.Subject + "]: " + issue.Message);
            }

            return string.Join("; ", messages.ToArray());
        }

        private static void AssertNoUnityReference(Assembly assembly)
        {
            AssemblyName[] references = assembly.GetReferencedAssemblies();
            for (int index = 0; index < references.Length; index++)
            {
                Assert.That(
                    references[index].Name,
                    Does.Not.StartWith("UnityEngine"),
                    assembly.GetName().Name + " must stay engine-independent.");
            }
        }

        private sealed class GraphFixture
        {
            private GraphFixture()
            {
            }

            public StableId LayoutId { get; private set; }

            public StableId StartRoomId { get; set; }

            public StableId TerminalRoomId { get; set; }

            public StableId StartEntryId { get; private set; }

            public StableId TerminalEntryId { get; private set; }

            public StableId ForwardExitId { get; private set; }

            public StableId ReturnExitId { get; private set; }

            public StableId ConnectionId { get; private set; }

            public StableId DoorLinkId { get; private set; }

            public List<RoomDefinitionV1> Rooms { get; private set; }

            public List<RoomEntryDefinitionV1> Entries { get; private set; }

            public List<RoomConnectionDefinitionV1> Connections { get; private set; }

            public List<RoomDoorLinkDefinitionV1> DoorLinks { get; private set; }

            public static GraphFixture Create()
            {
                var fixture = new GraphFixture
                {
                    LayoutId = Level1RoomGraphDefinitionV1.LayoutStableId,
                    StartRoomId = Level1RoomGraphDefinitionV1.EntryRoomStableId,
                    TerminalRoomId = Level1RoomGraphDefinitionV1.TerminalRoomStableId,
                    StartEntryId = Level1RoomGraphDefinitionV1.EntryRoomEntryStableId,
                    TerminalEntryId = Level1RoomGraphDefinitionV1.TerminalRoomEntryStableId,
                    ForwardExitId = Level1RoomGraphDefinitionV1.ForwardExitStableId,
                    ReturnExitId = Level1RoomGraphDefinitionV1.ReturnExitStableId,
                    ConnectionId = Level1RoomGraphDefinitionV1.ConnectionStableId,
                    DoorLinkId = Level1RoomGraphDefinitionV1.DoorLinkStableId,
                };
                fixture.Rooms = new List<RoomDefinitionV1>
                {
                    new RoomDefinitionV1(
                        fixture.StartRoomId,
                        0,
                        RoomInitialAvailabilityV1.Available,
                        true),
                    new RoomDefinitionV1(
                        fixture.TerminalRoomId,
                        1,
                        RoomInitialAvailabilityV1.Locked,
                        true),
                };
                fixture.Entries = new List<RoomEntryDefinitionV1>
                {
                    new RoomEntryDefinitionV1(
                        fixture.StartEntryId,
                        fixture.StartRoomId,
                        0),
                    new RoomEntryDefinitionV1(
                        fixture.TerminalEntryId,
                        fixture.TerminalRoomId,
                        0),
                };
                var forward = new RoomExitDefinitionV1(
                    fixture.ForwardExitId,
                    fixture.StartRoomId,
                    fixture.TerminalEntryId,
                    0,
                    RoomExitTypeV1.Progression,
                    true,
                    fixture.StartRoomId);
                var reverse = new RoomExitDefinitionV1(
                    fixture.ReturnExitId,
                    fixture.TerminalRoomId,
                    fixture.StartEntryId,
                    0,
                    RoomExitTypeV1.Return,
                    true,
                    fixture.StartRoomId);
                fixture.Connections = new List<RoomConnectionDefinitionV1>
                {
                    new RoomConnectionDefinitionV1(
                        fixture.ConnectionId,
                        RoomConnectionDirectionalityV1.Bidirectional,
                        fixture.DoorLinkId,
                        new[] { forward, reverse }),
                };
                fixture.DoorLinks = new List<RoomDoorLinkDefinitionV1>
                {
                    new RoomDoorLinkDefinitionV1(fixture.DoorLinkId),
                };
                return fixture;
            }

            public RoomGraphValidationResultV1 Validate()
            {
                return RoomGraphDefinitionV1.ValidateAndCreate(
                    LayoutId,
                    StartRoomId,
                    TerminalRoomId,
                    Rooms,
                    Entries,
                    Connections,
                    DoorLinks);
            }
        }
    }
}
