using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Missions.Rooms
{
    public sealed class RoomAccessJsonImporterV1Tests
    {
        [Test]
        public void ReadableKeyAndDoorDefinition_ImportsWithoutProductionBranches()
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            string json = Header(graph)
                + "\"conditions\":[{"
                + "\"id\":\"access.blue-key\","
                + "\"kind\":\"holding-present\","
                + "\"subject\":\"holding.blue-key\"}],"
                + "\"doors\":[{"
                + "\"room\":\""
                + Level1AuthorableRoomDefinitionV1.EntryRoomStableId
                + "\",\"exit_type\":\"progression\","
                + "\"condition\":\"access.blue-key\","
                + "\"consume_holding\":\"holding.blue-key\"}]}";

            RoomAccessImportResultV1 result = RoomAccessJsonImporterV1.Import(
                json,
                graph);

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            Assert.That(result.Definition.Doors, Has.Count.EqualTo(1));
            RoomDoorAccessDefinitionV1 door = result.Definition.Doors[0];
            Assert.That(
                door.DoorStableId,
                Is.EqualTo(Level1AuthorableRoomDefinitionV1.ForwardDoorStableId));
            Assert.That(
                door.ConsumeHoldingStableId,
                Is.EqualTo(StableId.Parse("holding.blue-key")));
        }

        [Test]
        public void ReturnProgressionAndFinalSelectors_RetainAuthoredMeanings()
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            string json = Header(graph)
                + "\"conditions\":["
                + Always("access.entry-open")
                + ","
                + Always("access.return-open")
                + ","
                + Always("access.final-open")
                + "],\"doors\":["
                + DoorByExitType(
                    Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                    "progression",
                    "access.entry-open")
                + ","
                + DoorByExitType(
                    Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                    "return",
                    "access.return-open")
                + ","
                + DoorByLinkKind(
                    Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                    "final-exit",
                    "access.final-open")
                + "]}";

            RoomAccessImportResultV1 result = RoomAccessJsonImporterV1.Import(
                json,
                graph);

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            AssertDoor(result.Definition, Level1AuthorableRoomDefinitionV1.ForwardDoorStableId);
            AssertDoor(result.Definition, Level1AuthorableRoomDefinitionV1.ReturnDoorStableId);
            AssertDoor(result.Definition, Level1AuthorableRoomDefinitionV1.FinalDoorStableId);
        }

        [Test]
        public void CanonicalJson_RoundTripsWithIdenticalFingerprint()
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            string json = CompoundJson(graph, reverseOrder: false);
            RoomAccessImportResultV1 first = RoomAccessJsonImporterV1.Import(json, graph);

            Assert.That(first.IsValid, Is.True, FirstIssue(first));
            RoomAccessImportResultV1 roundTrip = RoomAccessJsonImporterV1.Import(
                first.Definition.ToCanonicalJson(),
                graph);

            Assert.That(roundTrip.IsValid, Is.True, FirstIssue(roundTrip));
            Assert.That(
                roundTrip.Definition.Fingerprint,
                Is.EqualTo(first.Definition.Fingerprint));
            Assert.That(
                roundTrip.Definition.ToCanonicalJson(),
                Is.EqualTo(first.Definition.ToCanonicalJson()));
        }

        [Test]
        public void AuthoredOrdering_DoesNotChangeFingerprint()
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            RoomAccessImportResultV1 first = RoomAccessJsonImporterV1.Import(
                CompoundJson(graph, reverseOrder: false),
                graph);
            RoomAccessImportResultV1 second = RoomAccessJsonImporterV1.Import(
                CompoundJson(graph, reverseOrder: true),
                graph);

            Assert.That(first.IsValid, Is.True, FirstIssue(first));
            Assert.That(second.IsValid, Is.True, FirstIssue(second));
            Assert.That(
                second.Definition.Fingerprint,
                Is.EqualTo(first.Definition.Fingerprint));
        }

        [Test]
        public void UnknownChildReference_RejectsWithPrecisePath()
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            string json = Header(graph)
                + "\"conditions\":[{"
                + "\"id\":\"access.root\",\"kind\":\"all\","
                + "\"children\":[\"access.missing\"]}],"
                + "\"doors\":[]}";

            RoomAccessImportResultV1 result = RoomAccessJsonImporterV1.Import(
                json,
                graph);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Issues[0].Code,
                Is.EqualTo("room-access-condition-reference-unknown"));
            Assert.That(result.Issues[0].Path, Is.EqualTo("$.conditions[0].children[0]"));
        }

        [Test]
        public void CircularConditionGraph_RejectsWithoutDefinition()
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            string json = Header(graph)
                + "\"conditions\":["
                + "{\"id\":\"access.a\",\"kind\":\"all\","
                + "\"children\":[\"access.b\"]},"
                + "{\"id\":\"access.b\",\"kind\":\"not\","
                + "\"children\":[\"access.a\"]}],"
                + "\"doors\":[]}";

            RoomAccessImportResultV1 result = RoomAccessJsonImporterV1.Import(
                json,
                graph);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Definition, Is.Null);
            Assert.That(
                result.Issues[0].Code,
                Is.EqualTo("room-access-condition-cycle"));
            Assert.That(result.Issues[0].Path, Does.Contain("conditions"));
        }

        [Test]
        public void UnknownExactTerminal_RejectsWithSubjectDiagnostic()
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            string json = Header(graph)
                + "\"conditions\":[{"
                + "\"id\":\"access.terminal\","
                + "\"kind\":\"exact-terminal\","
                + "\"subject\":\"entity.not-authored\"}],"
                + "\"doors\":[]}";

            RoomAccessImportResultV1 result = RoomAccessJsonImporterV1.Import(
                json,
                graph);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Issues[0].Code,
                Is.EqualTo("room-access-terminal-reference-unknown"));
            Assert.That(result.Issues[0].Path, Is.EqualTo("$.conditions[0].subject"));
        }

        private static string CompoundJson(
            AuthorableRoomGraphDefinitionV1 graph,
            bool reverseOrder)
        {
            string switchCondition = "{\"id\":\"access.switch\","
                + "\"kind\":\"switch-active\","
                + "\"subject\":\"switch.main-power\"}";
            string difficulty = "{\"id\":\"access.difficulty\","
                + "\"kind\":\"difficulty-at-least\","
                + "\"minimum_difficulty\":3}";
            string all = "{\"id\":\"access.root\",\"kind\":\"all\","
                + "\"children\":[\"access.switch\",\"access.difficulty\"]}";
            string forward = DoorByExitType(
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                "progression",
                "access.root");
            string final = DoorByLinkKind(
                Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                "final-exit",
                "access.root");
            string conditions = reverseOrder
                ? all + "," + difficulty + "," + switchCondition
                : switchCondition + "," + difficulty + "," + all;
            string doors = reverseOrder
                ? final + "," + forward
                : forward + "," + final;
            return Header(graph)
                + "\"conditions\":["
                + conditions
                + "],\"doors\":["
                + doors
                + "]}";
        }

        private static string Header(AuthorableRoomGraphDefinitionV1 graph)
        {
            return "{\"version\":1,\"layout\":\""
                + graph.LayoutStableId
                + "\",";
        }

        private static string Always(string id)
        {
            return "{\"id\":\"" + id + "\",\"kind\":\"always\"}";
        }

        private static string DoorByExitType(
            StableId room,
            string exitType,
            string condition)
        {
            return "{\"room\":\""
                + room
                + "\",\"exit_type\":\""
                + exitType
                + "\",\"condition\":\""
                + condition
                + "\"}";
        }

        private static string DoorByLinkKind(
            StableId room,
            string linkKind,
            string condition)
        {
            return "{\"room\":\""
                + room
                + "\",\"link_kind\":\""
                + linkKind
                + "\",\"condition\":\""
                + condition
                + "\"}";
        }

        private static void AssertDoor(
            RoomAccessDefinitionV1 definition,
            StableId doorStableId)
        {
            RoomDoorAccessDefinitionV1 door;
            Assert.That(definition.TryGetDoor(doorStableId, out door), Is.True);
            Assert.That(door, Is.Not.Null);
        }

        private static string FirstIssue(RoomAccessImportResultV1 result)
        {
            return result.Issues.Count == 0
                ? string.Empty
                : result.Issues[0].Code
                    + ":"
                    + result.Issues[0].Path
                    + ":"
                    + result.Issues[0].Message;
        }
    }
}
