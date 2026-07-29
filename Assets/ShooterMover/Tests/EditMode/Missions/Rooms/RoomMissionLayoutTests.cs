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
            RoomGraphDefinition definition =
                Level1RoomGraphDefinition.Create();

            Assert.That(definition.LayoutStableId, Is.EqualTo(
                Level1RoomGraphDefinition.LayoutStableId));
            Assert.That(definition.Rooms.Count, Is.EqualTo(2));
            Assert.That(definition.Rooms[0].RoomStableId, Is.EqualTo(
                Level1RoomGraphDefinition.EntryRoomStableId));
            Assert.That(definition.Rooms[1].RoomStableId, Is.EqualTo(
                Level1RoomGraphDefinition.TerminalRoomStableId));
            Assert.That(definition.Connections.Count, Is.EqualTo(1));
            Assert.That(
                definition.Connections[0].Directionality,
                Is.EqualTo(RoomConnectionDirectionality.Bidirectional));
            Assert.That(
                definition.GetExitsFromRoom(
                    Level1RoomGraphDefinition.EntryRoomStableId)[0].ExitType,
                Is.EqualTo(RoomExitType.Progression));
            Assert.That(
                definition.GetExitsFromRoom(
                    Level1RoomGraphDefinition.TerminalRoomStableId)[0].ExitType,
                Is.EqualTo(RoomExitType.Return));
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
            RoomConnectionDefinition connection = second.Connections[0];
            second.Connections[0] = new RoomConnectionDefinition(
                connection.ConnectionStableId,
                connection.Directionality,
                connection.DoorLinkStableId,
                new[] { connection.Exits[1], connection.Exits[0] });

            RoomGraphValidationResult firstResult = first.Validate();
            RoomGraphValidationResult secondResult = second.Validate();

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
            fixture.Rooms.Add(new RoomDefinition(
                fixture.StartRoomId,
                2,
                RoomInitialAvailability.Locked,
                false));

            RoomGraphValidationResult result = fixture.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.HasCode(RoomGraphValidationCode.DuplicateRoomStableId),
                Is.True,
                Describe(result));
        }

        [Test]
        public void MissingExitReferences_AreRejected()
        {
            GraphFixture fixture = GraphFixture.Create();
            RoomExitDefinition invalid = new RoomExitDefinition(
                StableId.Parse("exit.invalid-reference"),
                StableId.Parse("room.missing-source"),
                StableId.Parse("entry.missing-target"),
                0,
                RoomExitType.Progression,
                false,
                null);
            fixture.Connections[0] = new RoomConnectionDefinition(
                fixture.ConnectionId,
                RoomConnectionDirectionality.OneWay,
                fixture.DoorLinkId,
                new[] { invalid });

            RoomGraphValidationResult result = fixture.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.HasCode(
                RoomGraphValidationCode.MissingExitSourceRoomReference),
                Is.True,
                Describe(result));
            Assert.That(result.HasCode(
                RoomGraphValidationCode.MissingExitTargetEntryReference),
                Is.True,
                Describe(result));
        }

        [Test]
        public void DanglingDoorLink_IsRejected()
        {
            GraphFixture fixture = GraphFixture.Create();
            fixture.Connections[0] = new RoomConnectionDefinition(
                fixture.ConnectionId,
                RoomConnectionDirectionality.Bidirectional,
                StableId.Parse("door-link.undefined"),
                fixture.Connections[0].Exits);
            fixture.DoorLinks.Clear();

            RoomGraphValidationResult result = fixture.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.HasCode(RoomGraphValidationCode.DanglingDoorLink),
                Is.True,
                Describe(result));
        }

        [Test]
        public void SelfLink_IsRejected()
        {
            GraphFixture fixture = GraphFixture.Create();
            RoomExitDefinition self = new RoomExitDefinition(
                fixture.ForwardExitId,
                fixture.StartRoomId,
                fixture.StartEntryId,
                0,
                RoomExitType.Progression,
                false,
                null);
            fixture.Connections[0] = new RoomConnectionDefinition(
                fixture.ConnectionId,
                RoomConnectionDirectionality.OneWay,
                fixture.DoorLinkId,
                new[] { self });

            RoomGraphValidationResult result = fixture.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.HasCode(RoomGraphValidationCode.SelfLink),
                Is.True,
                Describe(result));
        }

        [Test]
        public void MismatchedBidirectionalExits_AreRejected()
        {
            GraphFixture fixture = GraphFixture.Create();
            RoomExitDefinition first = fixture.Connections[0].Exits[0];
            RoomExitDefinition second = new RoomExitDefinition(
                fixture.ReturnExitId,
                fixture.StartRoomId,
                fixture.TerminalEntryId,
                1,
                RoomExitType.Return,
                false,
                null);
            fixture.Connections[0] = new RoomConnectionDefinition(
                fixture.ConnectionId,
                RoomConnectionDirectionality.Bidirectional,
                fixture.DoorLinkId,
                new[] { first, second });

            RoomGraphValidationResult result = fixture.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.HasCode(
                RoomGraphValidationCode.MismatchedReverseLink),
                Is.True,
                Describe(result));
        }

        [Test]
        public void UnreachableRequiredAndTerminalRoom_AreRejected()
        {
            GraphFixture fixture = GraphFixture.Create();
            fixture.Connections.Clear();
            fixture.DoorLinks.Clear();

            RoomGraphValidationResult result = fixture.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.HasCode(
                RoomGraphValidationCode.UnreachableRequiredRoom),
                Is.True,
                Describe(result));
            Assert.That(result.HasCode(
                RoomGraphValidationCode.UnreachableTerminalRoom),
                Is.True,
                Describe(result));
        }

        [Test]
        public void InvalidStartAndTerminalRooms_AreRejected()
        {
            GraphFixture fixture = GraphFixture.Create();
            fixture.StartRoomId = StableId.Parse("room.undefined-start");
            fixture.TerminalRoomId = StableId.Parse("room.undefined-terminal");

            RoomGraphValidationResult result = fixture.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.HasCode(RoomGraphValidationCode.InvalidStartRoom),
                Is.True,
                Describe(result));
            Assert.That(
                result.HasCode(RoomGraphValidationCode.InvalidTerminalRoom),
                Is.True,
                Describe(result));
        }

        [Test]
        public void StateTransitions_TrackLockedAvailableCurrentVisitedAndCompleted()
        {
            var layout = new RoomMissionLayout(
                Level1RoomGraphDefinition.Create());

            AssertRoom(
                layout.GetRoomState(
                    Level1RoomGraphDefinition.EntryRoomStableId),
                RoomAvailabilityState.Available,
                true,
                true,
                false);
            AssertRoom(
                layout.GetRoomState(
                    Level1RoomGraphDefinition.TerminalRoomStableId),
                RoomAvailabilityState.Locked,
                false,
                false,
                false);
            Assert.That(
                layout.GetExitState(
                    Level1RoomGraphDefinition.ForwardExitStableId).IsAvailable,
                Is.False);
            Assert.That(
                layout.Traverse(
                    Level1RoomGraphDefinition.ForwardExitStableId).Status,
                Is.EqualTo(RoomGraphOperationStatus.ExitLocked));

            RoomGraphOperationResult completed = layout.CompleteCurrentRoom();
            RoomGraphOperationResult traversed = layout.Traverse(
                Level1RoomGraphDefinition.ForwardExitStableId);

            Assert.That(completed.Status, Is.EqualTo(
                RoomGraphOperationStatus.Applied));
            Assert.That(traversed.Status, Is.EqualTo(
                RoomGraphOperationStatus.Applied));
            AssertRoom(
                layout.GetRoomState(
                    Level1RoomGraphDefinition.EntryRoomStableId),
                RoomAvailabilityState.Available,
                false,
                true,
                true);
            AssertRoom(
                layout.GetRoomState(
                    Level1RoomGraphDefinition.TerminalRoomStableId),
                RoomAvailabilityState.Available,
                true,
                true,
                false);
            Assert.That(
                layout.GetExitState(
                    Level1RoomGraphDefinition.ForwardExitStableId).IsAvailable,
                Is.True);
            Assert.That(
                layout.GetExitState(
                    Level1RoomGraphDefinition.ReturnExitStableId).IsAvailable,
                Is.True);
            Assert.That(layout.CurrentSnapshot.Sequence, Is.EqualTo(2L));
        }

        [Test]
        public void Restart_RestoresExactInitialSnapshot()
        {
            var fresh = new RoomMissionLayout(
                Level1RoomGraphDefinition.Create());
            string initialFingerprint = fresh.CurrentSnapshot.Fingerprint;
            fresh.CompleteCurrentRoom();
            fresh.Traverse(
                Level1RoomGraphDefinition.ForwardExitStableId);
            fresh.CompleteCurrentRoom();

            RoomGraphOperationResult restart = fresh.Restart();

            Assert.That(restart.Status, Is.EqualTo(
                RoomGraphOperationStatus.Applied));
            Assert.That(fresh.CurrentSnapshot.Sequence, Is.Zero);
            Assert.That(
                fresh.CurrentSnapshot.Fingerprint,
                Is.EqualTo(initialFingerprint));
            Assert.That(
                fresh.CurrentRoomState.RoomStableId,
                Is.EqualTo(Level1RoomGraphDefinition.EntryRoomStableId));
            Assert.That(
                fresh.GetRoomState(
                    Level1RoomGraphDefinition.TerminalRoomStableId).Availability,
                Is.EqualTo(RoomAvailabilityState.Locked));
            Assert.That(fresh.Restart().Status, Is.EqualTo(
                RoomGraphOperationStatus.NoChange));
        }

        [Test]
        public void SnapshotRoundTrip_IsCanonicalAndRestartSafe()
        {
            RoomGraphDefinition definition =
                Level1RoomGraphDefinition.Create();
            var original = new RoomMissionLayout(definition);
            original.CompleteCurrentRoom();
            original.Traverse(
                Level1RoomGraphDefinition.ForwardExitStableId);
            RoomGraphSnapshot exported = original.CurrentSnapshot;
            var reversedRooms = new List<RoomStateSnapshot>(exported.Rooms);
            var reversedExits = new List<RoomExitStateSnapshot>(exported.Exits);
            reversedRooms.Reverse();
            reversedExits.Reverse();
            RoomGraphSnapshot reordered = RoomGraphSnapshot.CreateCanonical(
                exported.LayoutStableId,
                exported.DefinitionFingerprint,
                exported.Sequence,
                reversedRooms,
                reversedExits);
            var restored = new RoomMissionLayout(definition);

            RoomGraphImportResult result = restored.TryImport(reordered);

            Assert.That(reordered.Fingerprint, Is.EqualTo(exported.Fingerprint));
            Assert.That(result.Status, Is.EqualTo(RoomGraphImportStatus.Imported));
            Assert.That(
                restored.CurrentSnapshot.Fingerprint,
                Is.EqualTo(exported.Fingerprint));
            Assert.That(
                restored.CurrentRoomState.RoomStableId,
                Is.EqualTo(Level1RoomGraphDefinition.TerminalRoomStableId));
            restored.Restart();
            Assert.That(restored.CurrentSnapshot.Sequence, Is.Zero);
            Assert.That(
                restored.CurrentRoomState.RoomStableId,
                Is.EqualTo(Level1RoomGraphDefinition.EntryRoomStableId));
        }

        [Test]
        public void CorruptSnapshot_IsRejectedAtomically()
        {
            var layout = new RoomMissionLayout(
                Level1RoomGraphDefinition.Create());
            layout.CompleteCurrentRoom();
            RoomGraphSnapshot before = layout.CurrentSnapshot;
            var corrupt = new RoomGraphSnapshot(
                before.SchemaVersion,
                before.LayoutStableId,
                before.DefinitionFingerprint,
                before.Sequence + 1L,
                before.Rooms,
                before.Exits,
                before.Fingerprint);

            RoomGraphImportResult result = layout.TryImport(corrupt);

            Assert.That(
                result.Status,
                Is.EqualTo(RoomGraphImportStatus.FingerprintMismatch));
            Assert.That(layout.CurrentSnapshot, Is.SameAs(before));
            Assert.That(
                layout.CurrentRoomState.RoomStableId,
                Is.EqualTo(Level1RoomGraphDefinition.EntryRoomStableId));
        }

        [Test]
        public void SnapshotFromDifferentDefinition_IsRejectedBeforeMutation()
        {
            RoomGraphDefinition sourceDefinition =
                Level1RoomGraphDefinition.Create();
            var source = new RoomMissionLayout(sourceDefinition);
            source.CompleteCurrentRoom();
            RoomGraphSnapshot snapshot = source.CurrentSnapshot;
            GraphFixture alteredFixture = GraphFixture.Create();
            RoomConnectionDefinition originalConnection =
                alteredFixture.Connections[0];
            RoomExitDefinition originalForward = originalConnection.Exits[0];
            RoomExitDefinition changedForward = new RoomExitDefinition(
                originalForward.ExitStableId,
                originalForward.SourceRoomStableId,
                originalForward.TargetEntryStableId,
                originalForward.Order,
                RoomExitType.Optional,
                originalForward.InitiallyLocked,
                originalForward.UnlockRequiredCompletedRoomStableId);
            alteredFixture.Connections[0] = new RoomConnectionDefinition(
                originalConnection.ConnectionStableId,
                originalConnection.Directionality,
                originalConnection.DoorLinkStableId,
                new[] { changedForward, originalConnection.Exits[1] });
            RoomGraphValidationResult alteredResult = alteredFixture.Validate();
            Assert.That(alteredResult.IsValid, Is.True, Describe(alteredResult));
            var target = new RoomMissionLayout(alteredResult.Definition);
            RoomGraphSnapshot before = target.CurrentSnapshot;

            RoomGraphImportResult result = target.TryImport(snapshot);

            Assert.That(result.Status, Is.EqualTo(
                RoomGraphImportStatus.DefinitionFingerprintMismatch));
            Assert.That(target.CurrentSnapshot, Is.SameAs(before));
        }

        [Test]
        public void DebugProjection_ContainsStableTopologyAndStateFacts()
        {
            var layout = new RoomMissionLayout(
                Level1RoomGraphDefinition.Create());
            layout.CompleteCurrentRoom();

            string projection = layout.CreateDebugProjection();

            Assert.That(projection, Does.Contain(
                Level1RoomGraphDefinition.EntryRoomStableId.ToString()));
            Assert.That(projection, Does.Contain(
                Level1RoomGraphDefinition.TerminalRoomStableId.ToString()));
            Assert.That(projection, Does.Contain("type=Progression"));
            Assert.That(projection, Does.Contain(
                Level1RoomGraphDefinition.DoorLinkStableId.ToString()));
            Assert.That(projection, Does.Contain("completed=1"));
        }

        [Test]
        public void RoomGraphAssemblies_HaveNoUnityEngineDependency()
        {
            AssertNoUnityReference(typeof(RoomGraphDefinition).Assembly);
            AssertNoUnityReference(typeof(IRoomMissionLayout).Assembly);
            AssertNoUnityReference(typeof(RoomMissionLayout).Assembly);
        }

        private static void AssertRoom(
            RoomLiveState state,
            RoomAvailabilityState availability,
            bool current,
            bool visited,
            bool completed)
        {
            Assert.That(state.Availability, Is.EqualTo(availability));
            Assert.That(state.IsCurrent, Is.EqualTo(current));
            Assert.That(state.IsVisited, Is.EqualTo(visited));
            Assert.That(state.IsCompleted, Is.EqualTo(completed));
        }

        private static string Describe(RoomGraphValidationResult result)
        {
            if (result == null)
            {
                return "validation result was null";
            }

            var messages = new List<string>();
            for (int index = 0; index < result.Issues.Count; index++)
            {
                RoomGraphValidationIssue issue = result.Issues[index];
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

            public List<RoomDefinition> Rooms { get; private set; }

            public List<RoomEntryDefinition> Entries { get; private set; }

            public List<RoomConnectionDefinition> Connections { get; private set; }

            public List<RoomDoorLinkDefinition> DoorLinks { get; private set; }

            public static GraphFixture Create()
            {
                var fixture = new GraphFixture
                {
                    LayoutId = Level1RoomGraphDefinition.LayoutStableId,
                    StartRoomId = Level1RoomGraphDefinition.EntryRoomStableId,
                    TerminalRoomId = Level1RoomGraphDefinition.TerminalRoomStableId,
                    StartEntryId = Level1RoomGraphDefinition.EntryRoomEntryStableId,
                    TerminalEntryId = Level1RoomGraphDefinition.TerminalRoomEntryStableId,
                    ForwardExitId = Level1RoomGraphDefinition.ForwardExitStableId,
                    ReturnExitId = Level1RoomGraphDefinition.ReturnExitStableId,
                    ConnectionId = Level1RoomGraphDefinition.ConnectionStableId,
                    DoorLinkId = Level1RoomGraphDefinition.DoorLinkStableId,
                };
                fixture.Rooms = new List<RoomDefinition>
                {
                    new RoomDefinition(
                        fixture.StartRoomId,
                        0,
                        RoomInitialAvailability.Available,
                        true),
                    new RoomDefinition(
                        fixture.TerminalRoomId,
                        1,
                        RoomInitialAvailability.Locked,
                        true),
                };
                fixture.Entries = new List<RoomEntryDefinition>
                {
                    new RoomEntryDefinition(
                        fixture.StartEntryId,
                        fixture.StartRoomId,
                        0),
                    new RoomEntryDefinition(
                        fixture.TerminalEntryId,
                        fixture.TerminalRoomId,
                        0),
                };
                var forward = new RoomExitDefinition(
                    fixture.ForwardExitId,
                    fixture.StartRoomId,
                    fixture.TerminalEntryId,
                    0,
                    RoomExitType.Progression,
                    true,
                    fixture.StartRoomId);
                var reverse = new RoomExitDefinition(
                    fixture.ReturnExitId,
                    fixture.TerminalRoomId,
                    fixture.StartEntryId,
                    0,
                    RoomExitType.Return,
                    true,
                    fixture.StartRoomId);
                fixture.Connections = new List<RoomConnectionDefinition>
                {
                    new RoomConnectionDefinition(
                        fixture.ConnectionId,
                        RoomConnectionDirectionality.Bidirectional,
                        fixture.DoorLinkId,
                        new[] { forward, reverse }),
                };
                fixture.DoorLinks = new List<RoomDoorLinkDefinition>
                {
                    new RoomDoorLinkDefinition(fixture.DoorLinkId),
                };
                return fixture;
            }

            public RoomGraphValidationResult Validate()
            {
                return RoomGraphDefinition.ValidateAndCreate(
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
