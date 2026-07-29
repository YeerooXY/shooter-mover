using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;
using ShooterMover.UnityAdapters.Missions.Rooms;

namespace ShooterMover.Tests.EditMode.Missions.Rooms
{
    public sealed partial class RoomFlowStateTests
    {
[Test]
        public void UnknownRuntimeAndDefinitionLinks_FailClosed()
        {
            RoomFlowState authority = CreateAuthority("unknown-link");
            string before = authority.CurrentProjection.Fingerprint;
            RoomLiveOperationResult runtimeResult = authority.Traverse(
                Operation("unknown-link-traverse"),
                StableId.Parse("exit.unknown-room-link"));

            Assert.That(runtimeResult.Status, Is.EqualTo(RoomLiveOperationStatus.Rejected));
            Assert.That(runtimeResult.RejectionCode, Is.EqualTo("room-live-exit-unknown"));
            Assert.That(authority.CurrentProjection.Fingerprint, Is.EqualTo(before));

            AuthorableRoomGraphDefinition source =
                Level1AuthorableRoomDefinition.Create();
            AuthorableRoomDefinition entrySource = source.GetRoom(
                Level1AuthorableRoomDefinition.EntryRoomStableId);
            var invalidExit = new RoomExitLinkDefinition(
                Level1AuthorableRoomDefinition.ForwardExitStableId,
                Level1AuthorableRoomDefinition.ForwardDoorStableId,
                RoomLiveLinkKind.Room,
                RoomExitType.Progression,
                StableId.Parse("room.unknown-target"),
                Level1AuthorableRoomDefinition.TerminalSpawnStableId);
            var invalidEntry = new AuthorableRoomDefinition(
                entrySource.RoomStableId,
                entrySource.Order,
                entrySource.DisplayName,
                entrySource.Bounds,
                entrySource.SpawnPoints,
                entrySource.Placements,
                entrySource.Doors,
                new[] { invalidExit },
                entrySource.CompletionConditions);

            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                new AuthorableRoomGraphDefinition(
                    source.LayoutStableId,
                    source.StartRoomStableId,
                    source.TerminalRoomStableId,
                    new[]
                    {
                        invalidEntry,
                        source.GetRoom(
                            Level1AuthorableRoomDefinition.TerminalRoomStableId),
                    }));
            Assert.That(
                error.Message,
                Does.Contain("room-live-link-target-room-unknown"));
        }

[Test]
        public void GraphSerialization_IsDeterministicAndExitMeaningIsAuthored()
        {
            AuthorableRoomGraphDefinition first =
                Level1AuthorableRoomDefinition.Create();
            AuthorableRoomGraphDefinition second =
                new AuthorableRoomGraphDefinition(
                    first.LayoutStableId,
                    first.StartRoomStableId,
                    first.TerminalRoomStableId,
                    new[] { first.Rooms[1], first.Rooms[0] });

            Assert.That(second.ToCanonicalJson(), Is.EqualTo(first.ToCanonicalJson()));
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            RoomExitLinkDefinition returnExit;
            Assert.That(
                first.GetRoom(Level1AuthorableRoomDefinition.TerminalRoomStableId)
                    .TryGetExit(
                        Level1AuthorableRoomDefinition.ReturnExitStableId,
                        out returnExit),
                Is.True);
            Assert.That(
                returnExit.ExitType,
                Is.EqualTo(RoomExitType.Return));
            Assert.That(
                first.ToCanonicalJson(),
                Does.Contain("\"required_condition_ids\""));
            Assert.That(
                first.ToCanonicalJson(),
                Does.Contain("\"exit_type\":2"));
        }

[Test]
        public void MutableUnderlyingAuthorities_AreNotPubliclyExposed()
        {
            Type authorityType = typeof(RoomFlowState);
            Assert.That(
                authorityType.GetProperty(
                    "OccupancyAuthority",
                    BindingFlags.Public | BindingFlags.Instance),
                Is.Null);
            Assert.That(
                authorityType.GetProperty(
                    "MissionLayout",
                    BindingFlags.Public | BindingFlags.Instance),
                Is.Null);
            Assert.That(
                typeof(RoomLiveSetup2D).GetProperty(
                    "Authority",
                    BindingFlags.Public | BindingFlags.Instance),
                Is.Null);
            Assert.That(
                typeof(RoomLiveSetup2D).GetProperty("Query").PropertyType,
                Is.EqualTo(typeof(IRoomLiveQuery)));
        }

private static RoomFlowState CreateAuthority(string suffix)
        {
            return new RoomFlowState(
                StableId.Parse("room-runtime-instance.test-" + suffix),
                Level1AuthorableRoomDefinition.Create());
        }

private static void CompleteEntryAndTraverse(
            RoomFlowState authority,
            string suffix)
        {
            authority.ReportOccupantTerminal(
                Operation(suffix + "-entry"),
                Level1AuthorableRoomDefinition.EntryRoomStableId,
                Level1AuthorableRoomDefinition.MovingDroidInstanceStableId);
            RoomLiveOperationResult traversal = authority.Traverse(
                Operation(suffix + "-forward"),
                Level1AuthorableRoomDefinition.ForwardExitStableId);
            Assert.That(traversal.Status, Is.EqualTo(RoomLiveOperationStatus.Applied));
        }

private static StableId Operation(string suffix)
        {
            return StableId.Parse("operation.room-live-test-" + suffix);
        }

private static AuthorableRoomGraphDefinition CreateTenEnemyDefinition()
        {
            AuthorableRoomGraphDefinition source =
                Level1AuthorableRoomDefinition.Create();
            AuthorableRoomDefinition sourceEntry = source.GetRoom(
                Level1AuthorableRoomDefinition.EntryRoomStableId);
            var enemies = new List<RoomPlacedEntityDefinition>();
            for (int index = 0; index < 10; index++)
            {
                enemies.Add(new RoomPlacedEntityDefinition(
                    StableId.Parse("enemy-instance.test-ten-" + index.ToString("D2")),
                    RoomLivePlacementKind.Enemy,
                    StableId.Parse("enemy.test-shared-definition"),
                    Level1AuthorableRoomDefinition.MovingDroidPresentationStableId,
                    RoomOccupantClearRole.RequiredEnemy,
                    new RoomVector2(index, 0d),
                    0d));
            }

            var entry = new AuthorableRoomDefinition(
                sourceEntry.RoomStableId,
                sourceEntry.Order,
                sourceEntry.DisplayName,
                sourceEntry.Bounds,
                sourceEntry.SpawnPoints,
                enemies,
                sourceEntry.Doors,
                sourceEntry.Exits,
                sourceEntry.CompletionConditions);
            return new AuthorableRoomGraphDefinition(
                source.LayoutStableId,
                source.StartRoomStableId,
                source.TerminalRoomStableId,
                new[]
                {
                    entry,
                    source.GetRoom(Level1AuthorableRoomDefinition.TerminalRoomStableId),
                });
        }
    }
}
