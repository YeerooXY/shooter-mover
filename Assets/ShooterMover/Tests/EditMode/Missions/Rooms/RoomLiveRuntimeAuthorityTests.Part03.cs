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
    public sealed partial class RoomLiveRuntimeAuthorityTests
    {
[Test]
        public void UnknownRuntimeAndDefinitionLinks_FailClosed()
        {
            RoomLiveRuntimeAuthorityV1 authority = CreateAuthority("unknown-link");
            string before = authority.CurrentProjection.Fingerprint;
            RoomLiveOperationResultV1 runtimeResult = authority.Traverse(
                Operation("unknown-link-traverse"),
                StableId.Parse("exit.unknown-room-link"));

            Assert.That(runtimeResult.Status, Is.EqualTo(RoomLiveOperationStatusV1.Rejected));
            Assert.That(runtimeResult.RejectionCode, Is.EqualTo("room-live-exit-unknown"));
            Assert.That(authority.CurrentProjection.Fingerprint, Is.EqualTo(before));

            AuthorableRoomGraphDefinitionV1 source =
                Level1AuthorableRoomDefinitionV1.Create();
            AuthorableRoomDefinitionV1 entrySource = source.GetRoom(
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId);
            var invalidExit = new RoomExitLinkDefinitionV1(
                Level1AuthorableRoomDefinitionV1.ForwardExitStableId,
                Level1AuthorableRoomDefinitionV1.ForwardDoorStableId,
                RoomLiveLinkKindV1.Room,
                RoomExitTypeV1.Progression,
                StableId.Parse("room.unknown-target"),
                Level1AuthorableRoomDefinitionV1.TerminalSpawnStableId);
            var invalidEntry = new AuthorableRoomDefinitionV1(
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
                new AuthorableRoomGraphDefinitionV1(
                    source.LayoutStableId,
                    source.StartRoomStableId,
                    source.TerminalRoomStableId,
                    new[]
                    {
                        invalidEntry,
                        source.GetRoom(
                            Level1AuthorableRoomDefinitionV1.TerminalRoomStableId),
                    }));
            Assert.That(
                error.Message,
                Does.Contain("room-live-link-target-room-unknown"));
        }

[Test]
        public void GraphSerialization_IsDeterministicAndExitMeaningIsAuthored()
        {
            AuthorableRoomGraphDefinitionV1 first =
                Level1AuthorableRoomDefinitionV1.Create();
            AuthorableRoomGraphDefinitionV1 second =
                new AuthorableRoomGraphDefinitionV1(
                    first.LayoutStableId,
                    first.StartRoomStableId,
                    first.TerminalRoomStableId,
                    new[] { first.Rooms[1], first.Rooms[0] });

            Assert.That(second.ToCanonicalJson(), Is.EqualTo(first.ToCanonicalJson()));
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(
                first.GetRoom(Level1AuthorableRoomDefinitionV1.TerminalRoomStableId)
                    .Exits[0].ExitType,
                Is.EqualTo(RoomExitTypeV1.Return));
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
            Type authorityType = typeof(RoomLiveRuntimeAuthorityV1);
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
                typeof(RoomRuntimeComposition2D).GetProperty(
                    "Authority",
                    BindingFlags.Public | BindingFlags.Instance),
                Is.Null);
            Assert.That(
                typeof(RoomRuntimeComposition2D).GetProperty("Query").PropertyType,
                Is.EqualTo(typeof(IRoomLiveRuntimeQueryV1)));
        }

private static RoomLiveRuntimeAuthorityV1 CreateAuthority(string suffix)
        {
            return new RoomLiveRuntimeAuthorityV1(
                StableId.Parse("room-runtime-instance.test-" + suffix),
                Level1AuthorableRoomDefinitionV1.Create());
        }

private static void CompleteEntryAndTraverse(
            RoomLiveRuntimeAuthorityV1 authority,
            string suffix)
        {
            authority.ReportOccupantTerminal(
                Operation(suffix + "-entry"),
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId);
            RoomLiveOperationResultV1 traversal = authority.Traverse(
                Operation(suffix + "-forward"),
                Level1AuthorableRoomDefinitionV1.ForwardExitStableId);
            Assert.That(traversal.Status, Is.EqualTo(RoomLiveOperationStatusV1.Applied));
        }

private static StableId Operation(string suffix)
        {
            return StableId.Parse("operation.room-live-test-" + suffix);
        }

private static AuthorableRoomGraphDefinitionV1 CreateTenEnemyDefinition()
        {
            AuthorableRoomGraphDefinitionV1 source =
                Level1AuthorableRoomDefinitionV1.Create();
            AuthorableRoomDefinitionV1 sourceEntry = source.GetRoom(
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId);
            var enemies = new List<RoomPlacedEntityDefinitionV1>();
            for (int index = 0; index < 10; index++)
            {
                enemies.Add(new RoomPlacedEntityDefinitionV1(
                    StableId.Parse("enemy-instance.test-ten-" + index.ToString("D2")),
                    RoomLivePlacementKindV1.Enemy,
                    StableId.Parse("enemy.test-shared-definition"),
                    Level1AuthorableRoomDefinitionV1.MovingDroidPresentationStableId,
                    RoomOccupantClearRoleV1.RequiredEnemy,
                    new RoomVector2V1(index, 0d),
                    0d));
            }

            var entry = new AuthorableRoomDefinitionV1(
                sourceEntry.RoomStableId,
                sourceEntry.Order,
                sourceEntry.DisplayName,
                sourceEntry.Bounds,
                sourceEntry.SpawnPoints,
                enemies,
                sourceEntry.Doors,
                sourceEntry.Exits,
                sourceEntry.CompletionConditions);
            return new AuthorableRoomGraphDefinitionV1(
                source.LayoutStableId,
                source.StartRoomStableId,
                source.TerminalRoomStableId,
                new[]
                {
                    entry,
                    source.GetRoom(Level1AuthorableRoomDefinitionV1.TerminalRoomStableId),
                });
        }
    }
}
