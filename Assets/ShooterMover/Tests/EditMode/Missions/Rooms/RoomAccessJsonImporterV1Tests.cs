using System;
using System.Collections.Generic;
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
        public void KnownHoldingKey_ImportsAndCanBeConsumedByAuthoredDoor()
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

            RoomAccessImportResultV1 result = Import(json, graph, Registry());

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            Assert.That(result.Definition.Doors, Has.Count.EqualTo(1));
            RoomDoorAccessDefinitionV1 door = result.Definition.Doors[0];
            Assert.That(
                door.DoorStableId,
                Is.EqualTo(Level1AuthorableRoomDefinitionV1.ForwardDoorStableId));
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
            RoomAccessImportResultV1 result = ImportLeaf(
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
            RoomAccessImportResultV1 result = ImportLeaf(
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
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            string json = Header(graph)
                + "\"conditions\":["
                + Always("access.open")
                + "],\"doors\":[{\"room\":\""
                + Level1AuthorableRoomDefinitionV1.EntryRoomStableId
                + "\",\"exit_type\":\"progression\","
                + "\"condition\":\"access.open\","
                + "\"consume_holding\":\"holding.misspelled-key\"}]}";

            RoomAccessImportResultV1 result = Import(json, graph, Registry());

            AssertIssue(
                result,
                "room-access-consume-holding-reference-unknown",
                "$.doors[0].consume_holding");
        }

        [Test]
        public void KnownSwitch_Imports()
        {
            RoomAccessImportResultV1 result = ImportLeaf(
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
            RoomAccessImportResultV1 result = ImportLeaf(
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
            RoomAccessImportResultV1 result = ImportLeaf(
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
            RoomAccessImportResultV1 result = ImportLeaf(
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
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            string json = CompoundJson(graph, reverseOrder: false);
            RoomAccessReferenceCatalogV1 firstRegistry = Registry(reverse: false);
            RoomAccessReferenceCatalogV1 secondRegistry = Registry(reverse: true);

            RoomAccessImportResultV1 first = Import(json, graph, firstRegistry);
            RoomAccessImportResultV1 second = Import(json, graph, secondRegistry);

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

            RoomAccessImportResultV1 result = Import(json, graph, Registry());

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            AssertDoor(result.Definition, Level1AuthorableRoomDefinitionV1.ForwardDoorStableId);
            AssertDoor(result.Definition, Level1AuthorableRoomDefinitionV1.ReturnDoorStableId);
            AssertDoor(result.Definition, Level1AuthorableRoomDefinitionV1.FinalDoorStableId);
        }

        [Test]
        public void CanonicalJson_RoundTripsWithIdenticalFingerprintAndProvenance()
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            RoomAccessReferenceCatalogV1 references = Registry();
            RoomAccessImportResultV1 first = Import(
                CompoundJson(graph, reverseOrder: false),
                graph,
                references);

            Assert.That(first.IsValid, Is.True, FirstIssue(first));
            RoomAccessImportResultV1 roundTrip = Import(
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
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            RoomAccessReferenceCatalogV1 references = Registry();
            RoomAccessImportResultV1 first = Import(
                CompoundJson(graph, reverseOrder: false),
                graph,
                references);
            RoomAccessImportResultV1 second = Import(
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
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            string json = Header(graph)
                + "\"conditions\":[{"
                + "\"id\":\"access.root\",\"kind\":\"all\","
                + "\"children\":[\"access.missing\"]}],"
                + "\"doors\":[]}";

            RoomAccessImportResultV1 result = Import(json, graph, Registry());

            AssertIssue(
                result,
                "room-access-condition-reference-unknown",
                "$.conditions[0].children[0]");
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

            RoomAccessImportResultV1 result = Import(json, graph, Registry());

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
            RoomAccessImportResultV1 result = ImportLeaf(
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
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            string knownJson = Header(graph)
                + "\"conditions\":[{\"id\":\"access.terminal\","
                + "\"kind\":\"exact-terminal\",\"subject\":\""
                + Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId
                + "\"}],\"doors\":[]}";
            RoomAccessImportResultV1 known = Import(knownJson, graph, Registry());
            RoomAccessImportResultV1 unknown = ImportLeaf(
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
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            RoomAccessReferenceCatalogV1 references = Registry();
            RoomAccessImportResultV1 imported = Import(
                CompoundJson(graph, reverseOrder: false),
                graph,
                references);
            Assert.That(imported.IsValid, Is.True, FirstIssue(imported));

            RoomAccessReferenceCatalogV1 different = new RoomAccessReferenceCatalogV1(
                new[]
                {
                    Registration(
                        "switch.different",
                        RoomAccessReferenceKindV1.Switch,
                        RoomAccessReferenceSourceV1.SwitchDefinition),
                });
            RoomAccessImportResultV1 result = Import(
                imported.Definition.ToCanonicalJson(),
                graph,
                different);

            AssertIssue(
                result,
                "room-access-reference-registry-fingerprint-mismatch",
                "$.reference_registry_fingerprint");
        }

        private static RoomAccessImportResultV1 ImportLeaf(
            string kind,
            string subject,
            IRoomAccessReferenceRegistryV1 references)
        {
            AuthorableRoomGraphDefinitionV1 graph =
                Level1AuthorableRoomDefinitionV1.Create();
            string json = Header(graph)
                + "\"conditions\":[{\"id\":\"access.leaf\","
                + "\"kind\":\""
                + kind
                + "\",\"subject\":\""
                + subject
                + "\"}],\"doors\":[]}";
            return Import(json, graph, references);
        }

        private static RoomAccessImportResultV1 Import(
            string json,
            AuthorableRoomGraphDefinitionV1 graph,
            IRoomAccessReferenceRegistryV1 references)
        {
            return RoomAccessJsonImporterV1.Import(json, graph, references);
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

        private static RoomAccessReferenceCatalogV1 Registry(bool reverse = false)
        {
            var registrations = new List<RoomAccessReferenceRegistrationV1>
            {
                Registration(
                    "holding.blue-key",
                    RoomAccessReferenceKindV1.Holding,
                    RoomAccessReferenceSourceV1.RunHolding),
                Registration(
                    "holding.consumed-key",
                    RoomAccessReferenceKindV1.Holding,
                    RoomAccessReferenceSourceV1.RunHolding),
                Registration(
                    "switch.main-power",
                    RoomAccessReferenceKindV1.Switch,
                    RoomAccessReferenceSourceV1.SwitchDefinition),
                Registration(
                    "objective.restore-power",
                    RoomAccessReferenceKindV1.Objective,
                    RoomAccessReferenceSourceV1.ObjectiveDefinition),
                Registration(
                    "drop.mission-key",
                    RoomAccessReferenceKindV1.CollectedDrop,
                    RoomAccessReferenceSourceV1.ExternalDropReference),
            };
            if (reverse) registrations.Reverse();
            return new RoomAccessReferenceCatalogV1(registrations);
        }

        private static RoomAccessReferenceRegistrationV1 Registration(
            string id,
            RoomAccessReferenceKindV1 kind,
            RoomAccessReferenceSourceV1 source)
        {
            return new RoomAccessReferenceRegistrationV1(Id(id), kind, source);
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

        private static void AssertIssue(
            RoomAccessImportResultV1 result,
            string code,
            string path)
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Definition, Is.Null);
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(result.Issues[0].Code, Is.EqualTo(code));
            Assert.That(result.Issues[0].Path, Is.EqualTo(path));
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

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
