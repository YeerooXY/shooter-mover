using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Missions.Rooms
{
    public sealed class RoomAccessJsonImporterTests
    {
        [Test]
        public void KnownHoldingKey_ImportsAndCanBeConsumedByAuthoredDoor()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            string json = Header(graph)
                + "\"conditions\":[{"
                + "\"id\":\"access.blue-key\","
                + "\"kind\":\"holding-present\","
                + "\"subject\":\"holding.blue-key\"}],"
                + "\"doors\":[{"
                + "\"room\":\""
                + Level1AuthorableRoomDefinition.EntryRoomStableId
                + "\",\"exit_type\":\"progression\","
                + "\"condition\":\"access.blue-key\","
                + "\"consume_holding\":\"holding.blue-key\"}]}";

            RoomAccessImportResult result = Import(json, graph, Registry());

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            Assert.That(result.Definition.Doors, Has.Count.EqualTo(1));
            RoomDoorAccessDefinition door = result.Definition.Doors[0];
            Assert.That(
                door.DoorStableId,
                Is.EqualTo(Level1AuthorableRoomDefinition.ForwardDoorStableId));
            Assert.That(
                door.ConsumeHoldingStableId,
                Is.EqualTo(Id("holding.blue-key")));
            Assert.That(
                result.Definition.ReferenceRegistryFingerprint,
                Is.EqualTo(Registry().Fingerprint));
        }

        [Test]
        public void UnknownHoldingPresent_RejectsAtSubject()
        {
            RoomAccessImportResult result = ImportLeaf(
                "holding-present",
                "holding.misspelled-key",
                Registry());

            AssertIssue(
                result,
                "room-access-holding-reference-unknown",
                "$.conditions[0].subject");
        }

        [Test]
        public void UnknownHoldingConsumed_RejectsAtSubject()
        {
            RoomAccessImportResult result = ImportLeaf(
                "holding-consumed",
                "holding.misspelled-key",
                Registry());

            AssertIssue(
                result,
                "room-access-holding-reference-unknown",
                "$.conditions[0].subject");
        }

        [Test]
        public void UnknownConsumeHolding_RejectsAtConsumeHolding()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            string json = Header(graph)
                + "\"conditions\":["
                + Always("access.open")
                + "],\"doors\":[{\"room\":\""
                + Level1AuthorableRoomDefinition.EntryRoomStableId
                + "\",\"exit_type\":\"progression\","
                + "\"condition\":\"access.open\","
                + "\"consume_holding\":\"holding.misspelled-key\"}]}";

            RoomAccessImportResult result = Import(json, graph, Registry());

            AssertIssue(
                result,
                "room-access-consume-holding-reference-unknown",
                "$.doors[0].consume_holding");
        }

        [Test]
        public void KnownSwitch_Imports()
        {
            RoomAccessImportResult result = ImportLeaf(
                "switch-active",
                "switch.main-power",
                Registry());

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            Assert.That(
                result.Definition.Conditions[0].SubjectStableId,
                Is.EqualTo(Id("switch.main-power")));
        }

        [Test]
        public void MisspelledSwitch_RejectsAtSubject()
        {
            RoomAccessImportResult result = ImportLeaf(
                "switch-active",
                "switch.main-pwoer",
                Registry());

            AssertIssue(
                result,
                "room-access-switch-reference-unknown",
                "$.conditions[0].subject");
        }

        [Test]
        public void UnknownObjective_RejectsAtSubject()
        {
            RoomAccessImportResult result = ImportLeaf(
                "objective-complete",
                "objective.not-registered",
                Registry());

            AssertIssue(
                result,
                "room-access-objective-reference-unknown",
                "$.conditions[0].subject");
        }

        [Test]
        public void UnknownCollectedDrop_RejectsAtSubject()
        {
            RoomAccessImportResult result = ImportLeaf(
                "collected-drop",
                "drop.not-registered",
                Registry());

            AssertIssue(
                result,
                "room-access-drop-reference-unknown",
                "$.conditions[0].subject");
        }

        [Test]
        public void RegistrationOrder_DoesNotChangeRegistryOrDefinitionFingerprint()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            string json = CompoundJson(graph, reverseOrder: false);
            RoomAccessReferenceCatalog firstRegistry = Registry(reverse: false);
            RoomAccessReferenceCatalog secondRegistry = Registry(reverse: true);

            RoomAccessImportResult first = Import(json, graph, firstRegistry);
            RoomAccessImportResult second = Import(json, graph, secondRegistry);

            Assert.That(first.IsValid, Is.True, FirstIssue(first));
            Assert.That(second.IsValid, Is.True, FirstIssue(second));
            Assert.That(secondRegistry.Fingerprint, Is.EqualTo(firstRegistry.Fingerprint));
            Assert.That(
                second.Definition.Fingerprint,
                Is.EqualTo(first.Definition.Fingerprint));
            Assert.That(
                second.Definition.ReferenceRegistryFingerprint,
                Is.EqualTo(first.Definition.ReferenceRegistryFingerprint));
        }

        [Test]
        public void ReturnProgressionAndFinalSelectors_RetainAuthoredMeanings()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            string json = Header(graph)
                + "\"conditions\":["
                + Always("access.entry-open")
                + ","
                + Always("access.return-open")
                + ","
                + Always("access.final-open")
                + "],\"doors\":["
                + DoorByExitType(
                    Level1AuthorableRoomDefinition.EntryRoomStableId,
                    "progression",
                    "access.entry-open")
                + ","
                + DoorByExitType(
                    Level1AuthorableRoomDefinition.TerminalRoomStableId,
                    "return",
                    "access.return-open")
                + ","
                + DoorByLinkKind(
                    Level1AuthorableRoomDefinition.TerminalRoomStableId,
                    "final-exit",
                    "access.final-open")
                + "]}";

            RoomAccessImportResult result = Import(json, graph, Registry());

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            AssertDoor(result.Definition, Level1AuthorableRoomDefinition.ForwardDoorStableId);
            AssertDoor(result.Definition, Level1AuthorableRoomDefinition.ReturnDoorStableId);
            AssertDoor(result.Definition, Level1AuthorableRoomDefinition.FinalDoorStableId);
        }

        [Test]
        public void CanonicalJson_RoundTripsWithIdenticalFingerprintAndProvenance()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            RoomAccessReferenceCatalog references = Registry();
            RoomAccessImportResult first = Import(
                CompoundJson(graph, reverseOrder: false),
                graph,
                references);

            Assert.That(first.IsValid, Is.True, FirstIssue(first));
            RoomAccessImportResult roundTrip = Import(
                first.Definition.ToCanonicalJson(),
                graph,
                references);

            Assert.That(roundTrip.IsValid, Is.True, FirstIssue(roundTrip));
            Assert.That(
                roundTrip.Definition.Fingerprint,
                Is.EqualTo(first.Definition.Fingerprint));
            Assert.That(
                roundTrip.Definition.ToCanonicalJson(),
                Is.EqualTo(first.Definition.ToCanonicalJson()));
            Assert.That(
                roundTrip.Definition.ReferenceRegistryFingerprint,
                Is.EqualTo(references.Fingerprint));
        }

        [Test]
        public void AuthoredOrdering_DoesNotChangeFingerprint()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            RoomAccessReferenceCatalog references = Registry();
            RoomAccessImportResult first = Import(
                CompoundJson(graph, reverseOrder: false),
                graph,
                references);
            RoomAccessImportResult second = Import(
                CompoundJson(graph, reverseOrder: true),
                graph,
                references);

            Assert.That(first.IsValid, Is.True, FirstIssue(first));
            Assert.That(second.IsValid, Is.True, FirstIssue(second));
            Assert.That(
                second.Definition.Fingerprint,
                Is.EqualTo(first.Definition.Fingerprint));
        }

        [Test]
        public void UnknownChildReference_RejectsWithPrecisePath()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            string json = Header(graph)
                + "\"conditions\":[{"
                + "\"id\":\"access.root\",\"kind\":\"all\","
                + "\"children\":[\"access.missing\"]}],"
                + "\"doors\":[]}";

            RoomAccessImportResult result = Import(json, graph, Registry());

            AssertIssue(
                result,
                "room-access-condition-reference-unknown",
                "$.conditions[0].children[0]");
        }

        [Test]
        public void CircularConditionGraph_RejectsWithoutDefinition()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            string json = Header(graph)
                + "\"conditions\":["
                + "{\"id\":\"access.a\",\"kind\":\"all\","
                + "\"children\":[\"access.b\"]},"
                + "{\"id\":\"access.b\",\"kind\":\"not\","
                + "\"children\":[\"access.a\"]}],"
                + "\"doors\":[]}";

            RoomAccessImportResult result = Import(json, graph, Registry());

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Definition, Is.Null);
            Assert.That(
                result.Issues[0].Code,
                Is.EqualTo("room-access-condition-cycle"));
            Assert.That(result.Issues[0].Path, Does.Contain("conditions"));
        }

        [Test]
        public void UnknownRoomReference_RejectsAtSubject()
        {
            RoomAccessImportResult result = ImportLeaf(
                "room-entered",
                "room.not-authored",
                Registry());

            AssertIssue(
                result,
                "room-access-room-reference-unknown",
                "$.conditions[0].subject");
        }

        [Test]
        public void ExactTerminal_KnownPlacementImportsAndUnknownPlacementRejects()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            string knownJson = Header(graph)
                + "\"conditions\":[{\"id\":\"access.terminal\","
                + "\"kind\":\"exact-terminal\",\"subject\":\""
                + Level1AuthorableRoomDefinition.MovingDroidInstanceStableId
                + "\"}],\"doors\":[]}";
            RoomAccessImportResult known = Import(knownJson, graph, Registry());
            RoomAccessImportResult unknown = ImportLeaf(
                "exact-terminal",
                "entity.not-authored",
                Registry());

            Assert.That(known.IsValid, Is.True, FirstIssue(known));
            AssertIssue(
                unknown,
                "room-access-terminal-reference-unknown",
                "$.conditions[0].subject");
        }

        [Test]
        public void VersionTwoCanonicalDocument_RejectsMismatchedRegistryFingerprint()
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            RoomAccessReferenceCatalog references = Registry();
            RoomAccessImportResult imported = Import(
                CompoundJson(graph, reverseOrder: false),
                graph,
                references);
            Assert.That(imported.IsValid, Is.True, FirstIssue(imported));

            RoomAccessReferenceCatalog different = new RoomAccessReferenceCatalog(
                new[]
                {
                    Registration(
                        "switch.different",
                        RoomAccessReferenceKind.Switch,
                        RoomAccessReferenceSource.SwitchDefinition),
                });
            RoomAccessImportResult result = Import(
                imported.Definition.ToCanonicalJson(),
                graph,
                different);

            AssertIssue(
                result,
                "room-access-reference-registry-fingerprint-mismatch",
                "$.reference_registry_fingerprint");
        }

        private static RoomAccessImportResult ImportLeaf(
            string kind,
            string subject,
            IRoomAccessReferenceRegistry references)
        {
            AuthorableRoomGraphDefinition graph =
                Level1AuthorableRoomDefinition.Create();
            string json = Header(graph)
                + "\"conditions\":[{\"id\":\"access.leaf\","
                + "\"kind\":\""
                + kind
                + "\",\"subject\":\""
                + subject
                + "\"}],\"doors\":[]}";
            return Import(json, graph, references);
        }

        private static RoomAccessImportResult Import(
            string json,
            AuthorableRoomGraphDefinition graph,
            IRoomAccessReferenceRegistry references)
        {
            return RoomAccessJsonImporter.Import(json, graph, references);
        }

        private static string CompoundJson(
            AuthorableRoomGraphDefinition graph,
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
                Level1AuthorableRoomDefinition.EntryRoomStableId,
                "progression",
                "access.root");
            string final = DoorByLinkKind(
                Level1AuthorableRoomDefinition.TerminalRoomStableId,
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

        private static RoomAccessReferenceCatalog Registry(bool reverse = false)
        {
            var registrations = new List<RoomAccessReferenceRegistration>
            {
                Registration(
                    "holding.blue-key",
                    RoomAccessReferenceKind.Holding,
                    RoomAccessReferenceSource.RunHolding),
                Registration(
                    "holding.consumed-key",
                    RoomAccessReferenceKind.Holding,
                    RoomAccessReferenceSource.RunHolding),
                Registration(
                    "switch.main-power",
                    RoomAccessReferenceKind.Switch,
                    RoomAccessReferenceSource.SwitchDefinition),
                Registration(
                    "objective.restore-power",
                    RoomAccessReferenceKind.Objective,
                    RoomAccessReferenceSource.ObjectiveDefinition),
                Registration(
                    "drop.mission-key",
                    RoomAccessReferenceKind.CollectedDrop,
                    RoomAccessReferenceSource.ExternalDropReference),
            };
            if (reverse) registrations.Reverse();
            return new RoomAccessReferenceCatalog(registrations);
        }

        private static RoomAccessReferenceRegistration Registration(
            string id,
            RoomAccessReferenceKind kind,
            RoomAccessReferenceSource source)
        {
            return new RoomAccessReferenceRegistration(Id(id), kind, source);
        }

        private static string Header(AuthorableRoomGraphDefinition graph)
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
            RoomAccessDefinition definition,
            StableId doorStableId)
        {
            RoomDoorAccessDefinition door;
            Assert.That(definition.TryGetDoor(doorStableId, out door), Is.True);
            Assert.That(door, Is.Not.Null);
        }

        private static void AssertIssue(
            RoomAccessImportResult result,
            string code,
            string path)
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Definition, Is.Null);
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(result.Issues[0].Code, Is.EqualTo(code));
            Assert.That(result.Issues[0].Path, Is.EqualTo(path));
        }

        private static string FirstIssue(RoomAccessImportResult result)
        {
            return result.Issues.Count == 0
                ? string.Empty
                : result.Issues[0].Code
                    + ":"
                    + result.Issues[0].Path
                    + ":"
                    + result.Issues[0].Message;
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
