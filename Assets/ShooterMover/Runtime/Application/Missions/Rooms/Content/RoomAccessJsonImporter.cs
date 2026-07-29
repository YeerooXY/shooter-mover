using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;

namespace ShooterMover.Application.Missions.Rooms.Content
{
    public sealed class RoomAccessImportIssue
    {
        public RoomAccessImportIssue(string code, string path, string message)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException(nameof(code));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(nameof(path));
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException(nameof(message));
            Code = code;
            Path = path;
            Message = message;
        }

        public string Code { get; }

        public string Path { get; }

        public string Message { get; }
    }

    public sealed class RoomAccessImportResult
    {
        private readonly ReadOnlyCollection<RoomAccessImportIssue> issues;

        public RoomAccessImportResult(
            RoomAccessDefinition definition,
            IEnumerable<RoomAccessImportIssue> issues)
        {
            Definition = definition;
            this.issues = new ReadOnlyCollection<RoomAccessImportIssue>(
                new List<RoomAccessImportIssue>(
                    issues ?? Array.Empty<RoomAccessImportIssue>()));
        }

        public RoomAccessDefinition Definition { get; }

        public IReadOnlyList<RoomAccessImportIssue> Issues => issues;

        public bool IsValid => Definition != null && issues.Count == 0;
    }

    /// <summary>
    /// Imports a readable companion access document for one authored room graph.
    /// External holding, objective, switch, and collected-drop references must be
    /// present in an immutable authoring-time registry; runtime authorities are not
    /// consulted during import.
    /// </summary>
    public static class RoomAccessJsonImporter
    {
        public const int CurrentVersion = 2;
        public const int MinimumSupportedVersion = 1;

        public static RoomAccessImportResult Import(
            string json,
            AuthorableRoomGraphDefinition roomGraph)
        {
            return Import(json, roomGraph, RoomAccessReferenceCatalog.Empty);
        }

        public static RoomAccessImportResult Import(
            string json,
            AuthorableRoomGraphDefinition roomGraph,
            IRoomAccessReferenceRegistry referenceRegistry)
        {
            if (roomGraph == null)
            {
                return Failure(
                    "room-access-graph-missing",
                    "$",
                    "An authored room graph is required.");
            }
            if (referenceRegistry == null)
            {
                return Failure(
                    "room-access-reference-registry-missing",
                    "$.reference_registry",
                    "An immutable room access reference registry is required.");
            }
            if (string.IsNullOrWhiteSpace(referenceRegistry.Fingerprint))
            {
                return Failure(
                    "room-access-reference-registry-fingerprint-missing",
                    "$.reference_registry",
                    "The room access reference registry requires a deterministic fingerprint.");
            }
            RoomAccessReferenceCatalog immutableReferenceRegistry;
            try
            {
                immutableReferenceRegistry =
                    RoomAccessReferenceCatalog.Snapshot(referenceRegistry);
            }
            catch (Exception exception)
            {
                return Failure(
                    "room-access-reference-registry-invalid",
                    "$.reference_registry",
                    exception.Message);
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return Failure(
                    "room-access-json-empty",
                    "$",
                    "A room access JSON document is required.");
            }

            try
            {
                RootDto root = Deserialize(json);
                if (root.Version < MinimumSupportedVersion
                    || root.Version > CurrentVersion)
                {
                    throw Mapping(
                        "room-access-version-unsupported",
                        "$.version",
                        "Supported room access versions are "
                        + MinimumSupportedVersion
                        + " through "
                        + CurrentVersion
                        + "; received "
                        + root.Version
                        + ".");
                }

                ValidateRegistryProvenance(root, immutableReferenceRegistry);

                StableId layout = ParseStableId(root.Layout, "$.layout");
                if (layout != roomGraph.LayoutStableId)
                {
                    throw Mapping(
                        "room-access-layout-mismatch",
                        "$.layout",
                        "Expected layout "
                        + roomGraph.LayoutStableId
                        + " but received "
                        + layout
                        + ".");
                }

                List<ConditionDto> authoredConditions = RequireList(
                    root.Conditions,
                    "$.conditions");
                if (authoredConditions.Count == 0)
                {
                    throw Mapping(
                        "room-access-condition-list-empty",
                        "$.conditions",
                        "At least one access condition is required.");
                }

                var conditions = new List<RoomAccessConditionDefinition>();
                var conditionSources = new Dictionary<StableId, ConditionSource>();
                for (int index = 0; index < authoredConditions.Count; index++)
                {
                    string path = "$.conditions[" + index + "]";
                    ConditionDto dto = Require(authoredConditions[index], path);
                    StableId id = ParseStableId(dto.Id, path + ".id");
                    if (conditionSources.ContainsKey(id))
                    {
                        throw Mapping(
                            "room-access-condition-duplicate",
                            path + ".id",
                            "Condition identity is duplicated: " + id);
                    }

                    RoomAccessConditionKind kind = ParseConditionKind(
                        dto.Kind,
                        path + ".kind");
                    StableId subject = string.IsNullOrWhiteSpace(dto.Subject)
                        ? null
                        : ParseStableId(dto.Subject, path + ".subject");
                    List<string> childValues = dto.Children ?? new List<string>();
                    var children = new List<StableId>();
                    var seenChildren = new HashSet<StableId>();
                    for (int childIndex = 0;
                        childIndex < childValues.Count;
                        childIndex++)
                    {
                        StableId child = ParseStableId(
                            childValues[childIndex],
                            path + ".children[" + childIndex + "]");
                        if (!seenChildren.Add(child))
                        {
                            throw Mapping(
                                "room-access-condition-child-duplicate",
                                path + ".children[" + childIndex + "]",
                                "Child condition identity is duplicated: " + child);
                        }
                        children.Add(child);
                    }

                    RoomAccessConditionDefinition definition;
                    try
                    {
                        definition = new RoomAccessConditionDefinition(
                            id,
                            kind,
                            subject,
                            dto.MinimumDifficulty,
                            children);
                    }
                    catch (Exception exception)
                    {
                        throw Mapping(
                            "room-access-condition-invalid",
                            path,
                            exception.Message);
                    }

                    conditions.Add(definition);
                    conditionSources.Add(id, new ConditionSource(definition, path));
                }

                ValidateConditionReferences(conditionSources);
                ValidateConditionSubjects(
                    roomGraph,
                    immutableReferenceRegistry,
                    conditionSources);
                ValidateAcyclic(conditionSources);

                List<DoorDto> authoredDoors = RequireList(root.Doors, "$.doors");
                var doors = new List<RoomDoorAccessDefinition>();
                var seenDoors = new HashSet<StableId>();
                for (int index = 0; index < authoredDoors.Count; index++)
                {
                    string path = "$.doors[" + index + "]";
                    DoorDto dto = Require(authoredDoors[index], path);
                    StableId roomId = ParseStableId(dto.Room, path + ".room");
                    AuthorableRoomDefinition room;
                    if (!roomGraph.TryGetRoom(roomId, out room))
                    {
                        throw Mapping(
                            "room-access-door-room-unknown",
                            path + ".room",
                            "Unknown room identity: " + roomId);
                    }

                    StableId doorId = ResolveDoor(room, dto, path);
                    if (!seenDoors.Add(doorId))
                    {
                        throw Mapping(
                            "room-access-door-duplicate",
                            path,
                            "Door access definition is duplicated: " + doorId);
                    }

                    StableId conditionId = ParseStableId(
                        dto.Condition,
                        path + ".condition");
                    if (!conditionSources.ContainsKey(conditionId))
                    {
                        throw Mapping(
                            "room-access-door-condition-unknown",
                            path + ".condition",
                            "Unknown access condition: " + conditionId);
                    }

                    StableId consumeHolding = string.IsNullOrWhiteSpace(
                        dto.ConsumeHolding)
                        ? null
                        : ParseStableId(
                            dto.ConsumeHolding,
                            path + ".consume_holding");
                    if (consumeHolding != null
                        && !immutableReferenceRegistry.ContainsHolding(consumeHolding))
                    {
                        throw Mapping(
                            "room-access-consume-holding-reference-unknown",
                            path + ".consume_holding",
                            "Unknown run holding reference: " + consumeHolding);
                    }

                    doors.Add(new RoomDoorAccessDefinition(
                        roomId,
                        doorId,
                        conditionId,
                        consumeHolding));
                }

                RoomAccessDefinition definitionResult;
                try
                {
                    definitionResult = new RoomAccessDefinition(
                        roomGraph,
                        immutableReferenceRegistry,
                        conditions,
                        doors);
                }
                catch (Exception exception)
                {
                    throw Mapping(
                        "room-access-definition-invalid",
                        "$",
                        exception.Message);
                }

                return new RoomAccessImportResult(definitionResult, null);
            }
            catch (RoomAccessMappingException exception)
            {
                return Failure(exception.Code, exception.Path, exception.Message);
            }
            catch (Exception exception)
            {
                if (exception is OutOfMemoryException
                    || exception is StackOverflowException
                    || exception is AccessViolationException)
                {
                    throw;
                }

                return Failure(
                    "room-access-invalid",
                    "$",
                    exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static void ValidateRegistryProvenance(
            RootDto root,
            IRoomAccessReferenceRegistry referenceRegistry)
        {
            if (root.Version >= 2
                && string.IsNullOrWhiteSpace(root.ReferenceRegistryFingerprint))
            {
                throw Mapping(
                    "room-access-reference-registry-fingerprint-required",
                    "$.reference_registry_fingerprint",
                    "Version 2 room access documents must include the immutable reference registry fingerprint.");
            }

            if (!string.IsNullOrWhiteSpace(root.ReferenceRegistryFingerprint)
                && !string.Equals(
                    root.ReferenceRegistryFingerprint.Trim(),
                    referenceRegistry.Fingerprint,
                    StringComparison.Ordinal))
            {
                throw Mapping(
                    "room-access-reference-registry-fingerprint-mismatch",
                    "$.reference_registry_fingerprint",
                    "Expected reference registry fingerprint "
                    + referenceRegistry.Fingerprint
                    + " but received "
                    + root.ReferenceRegistryFingerprint.Trim()
                    + ".");
            }
        }

        private static RootDto Deserialize(string json)
        {
            try
            {
                var serializer = new DataContractJsonSerializer(
                    typeof(RootDto),
                    new DataContractJsonSerializerSettings
                    {
                        UseSimpleDictionaryFormat = true,
                    });
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    RootDto value = serializer.ReadObject(stream) as RootDto;
                    if (value == null)
                    {
                        throw Mapping(
                            "room-access-json-root-invalid",
                            "$",
                            "JSON root must be an object.");
                    }
                    return value;
                }
            }
            catch (RoomAccessMappingException)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (!(exception is SerializationException)
                    && !(exception is FormatException)
                    && !(exception is InvalidDataContractException))
                {
                    throw;
                }

                throw Mapping(
                    "room-access-json-invalid",
                    "$",
                    "Malformed room access JSON: " + exception.Message);
            }
        }

        private static void ValidateConditionReferences(
            Dictionary<StableId, ConditionSource> conditions)
        {
            foreach (KeyValuePair<StableId, ConditionSource> pair in conditions)
            {
                RoomAccessConditionDefinition condition = pair.Value.Definition;
                for (int index = 0;
                    index < condition.ChildConditionStableIds.Count;
                    index++)
                {
                    StableId child = condition.ChildConditionStableIds[index];
                    if (!conditions.ContainsKey(child))
                    {
                        throw Mapping(
                            "room-access-condition-reference-unknown",
                            pair.Value.Path + ".children[" + index + "]",
                            "Unknown child condition: " + child);
                    }
                }
            }
        }

        private static void ValidateConditionSubjects(
            AuthorableRoomGraphDefinition graph,
            IRoomAccessReferenceRegistry referenceRegistry,
            Dictionary<StableId, ConditionSource> conditions)
        {
            var placements = new HashSet<StableId>();
            for (int roomIndex = 0; roomIndex < graph.Rooms.Count; roomIndex++)
            {
                AuthorableRoomDefinition room = graph.Rooms[roomIndex];
                for (int placementIndex = 0;
                    placementIndex < room.Placements.Count;
                    placementIndex++)
                {
                    placements.Add(room.Placements[placementIndex].InstanceStableId);
                }
            }

            foreach (KeyValuePair<StableId, ConditionSource> pair in conditions)
            {
                RoomAccessConditionDefinition condition = pair.Value.Definition;
                string path = pair.Value.Path + ".subject";
                if (condition.Kind == RoomAccessConditionKind.RoomEntered
                    || condition.Kind == RoomAccessConditionKind.RoomComplete)
                {
                    AuthorableRoomDefinition ignored;
                    if (!graph.TryGetRoom(condition.SubjectStableId, out ignored))
                    {
                        throw Mapping(
                            "room-access-room-reference-unknown",
                            path,
                            "Unknown room identity: " + condition.SubjectStableId);
                    }
                }
                else if (condition.Kind == RoomAccessConditionKind.ExactEntityTerminal)
                {
                    if (!placements.Contains(condition.SubjectStableId))
                    {
                        throw Mapping(
                            "room-access-terminal-reference-unknown",
                            path,
                            "Unknown enemy or prop placement identity: "
                            + condition.SubjectStableId);
                    }
                }
                else if (condition.Kind == RoomAccessConditionKind.HoldingPresent
                    || condition.Kind == RoomAccessConditionKind.HoldingConsumed)
                {
                    if (!referenceRegistry.ContainsHolding(condition.SubjectStableId))
                    {
                        throw Mapping(
                            "room-access-holding-reference-unknown",
                            path,
                            "Unknown run holding reference: "
                            + condition.SubjectStableId);
                    }
                }
                else if (condition.Kind == RoomAccessConditionKind.ObjectiveComplete)
                {
                    if (!referenceRegistry.ContainsObjective(condition.SubjectStableId))
                    {
                        throw Mapping(
                            "room-access-objective-reference-unknown",
                            path,
                            "Unknown objective reference: "
                            + condition.SubjectStableId);
                    }
                }
                else if (condition.Kind == RoomAccessConditionKind.SwitchActive)
                {
                    if (!referenceRegistry.ContainsSwitch(condition.SubjectStableId))
                    {
                        throw Mapping(
                            "room-access-switch-reference-unknown",
                            path,
                            "Unknown switch reference: "
                            + condition.SubjectStableId);
                    }
                }
                else if (condition.Kind == RoomAccessConditionKind.CollectedDrop)
                {
                    if (!referenceRegistry.ContainsCollectedDrop(
                        condition.SubjectStableId))
                    {
                        throw Mapping(
                            "room-access-drop-reference-unknown",
                            path,
                            "Unknown explicitly registered collected-drop reference: "
                            + condition.SubjectStableId);
                    }
                }
            }
        }

        private static void ValidateAcyclic(
            Dictionary<StableId, ConditionSource> conditions)
        {
            var states = new Dictionary<StableId, int>();
            foreach (StableId id in conditions.Keys)
            {
                Visit(id, conditions, states);
            }
        }

        private static void Visit(
            StableId id,
            Dictionary<StableId, ConditionSource> conditions,
            Dictionary<StableId, int> states)
        {
            int state;
            if (states.TryGetValue(id, out state))
            {
                if (state == 1)
                {
                    throw Mapping(
                        "room-access-condition-cycle",
                        conditions[id].Path + ".children",
                        "Circular condition reference includes " + id + ".");
                }
                if (state == 2) return;
            }

            states[id] = 1;
            RoomAccessConditionDefinition condition = conditions[id].Definition;
            for (int index = 0;
                index < condition.ChildConditionStableIds.Count;
                index++)
            {
                Visit(condition.ChildConditionStableIds[index], conditions, states);
            }
            states[id] = 2;
        }

        private static StableId ResolveDoor(
            AuthorableRoomDefinition room,
            DoorDto dto,
            string path)
        {
            bool hasDoor = !string.IsNullOrWhiteSpace(dto.Door);
            bool hasExitType = !string.IsNullOrWhiteSpace(dto.ExitType);
            bool hasLinkKind = !string.IsNullOrWhiteSpace(dto.LinkKind);
            if (hasDoor && (hasExitType || hasLinkKind))
            {
                throw Mapping(
                    "room-access-door-selector-conflict",
                    path,
                    "Select a door by exact door ID or by exit/link meaning, not both.");
            }
            if (!hasDoor && !hasExitType && !hasLinkKind)
            {
                throw Mapping(
                    "room-access-door-selector-empty",
                    path,
                    "A door requires door, exit_type, or link_kind selection.");
            }

            if (hasDoor)
            {
                StableId id = ParseStableId(dto.Door, path + ".door");
                RoomDoorDefinition definition;
                if (!room.TryGetDoor(id, out definition))
                {
                    throw Mapping(
                        "room-access-door-unknown",
                        path + ".door",
                        "Unknown door identity in room "
                        + room.RoomStableId
                        + ": "
                        + id);
                }
                return id;
            }

            RoomExitType? exitType = hasExitType
                ? ParseExitType(dto.ExitType, path + ".exit_type")
                : (RoomExitType?)null;
            RoomLiveLinkKind? linkKind = hasLinkKind
                ? ParseLinkKind(dto.LinkKind, path + ".link_kind")
                : (RoomLiveLinkKind?)null;
            var matches = new List<StableId>();
            for (int index = 0; index < room.Doors.Count; index++)
            {
                RoomDoorDefinition door = room.Doors[index];
                RoomExitLinkDefinition exit;
                if (!room.TryGetExit(door.ExitStableId, out exit)) continue;
                if (exitType.HasValue && exit.ExitType != exitType.Value) continue;
                if (linkKind.HasValue && exit.LinkKind != linkKind.Value) continue;
                matches.Add(door.DoorInstanceStableId);
            }

            if (matches.Count == 0)
            {
                throw Mapping(
                    "room-access-door-selector-no-match",
                    path,
                    "No door in "
                    + room.RoomStableId
                    + " matches the authored selector.");
            }
            if (matches.Count != 1)
            {
                throw Mapping(
                    "room-access-door-selector-ambiguous",
                    path,
                    "More than one door in "
                    + room.RoomStableId
                    + " matches the authored selector; use an exact door ID.");
            }
            return matches[0];
        }

        private static RoomAccessConditionKind ParseConditionKind(
            string value,
            string path)
        {
            string token = RequireText(value, path);
            switch (token)
            {
                case "always": return RoomAccessConditionKind.Always;
                case "room-entered": return RoomAccessConditionKind.RoomEntered;
                case "room-complete": return RoomAccessConditionKind.RoomComplete;
                case "exact-terminal": return RoomAccessConditionKind.ExactEntityTerminal;
                case "holding-present": return RoomAccessConditionKind.HoldingPresent;
                case "holding-consumed": return RoomAccessConditionKind.HoldingConsumed;
                case "collected-drop": return RoomAccessConditionKind.CollectedDrop;
                case "objective-complete": return RoomAccessConditionKind.ObjectiveComplete;
                case "switch-active": return RoomAccessConditionKind.SwitchActive;
                case "difficulty-at-least": return RoomAccessConditionKind.DifficultyAtLeast;
                case "all": return RoomAccessConditionKind.All;
                case "any": return RoomAccessConditionKind.Any;
                case "not": return RoomAccessConditionKind.Not;
                default:
                    throw Mapping(
                        "room-access-condition-kind-unsupported",
                        path,
                        "Unsupported room access condition kind: " + token);
            }
        }

        private static RoomExitType ParseExitType(string value, string path)
        {
            string token = RequireText(value, path);
            switch (token)
            {
                case "progression": return RoomExitType.Progression;
                case "return": return RoomExitType.Return;
                case "optional": return RoomExitType.Optional;
                case "secret": return RoomExitType.Secret;
                default:
                    throw Mapping(
                        "room-access-exit-type-unsupported",
                        path,
                        "Supported exit_type values are progression, return, optional, and secret.");
            }
        }

        private static RoomLiveLinkKind ParseLinkKind(string value, string path)
        {
            string token = RequireText(value, path);
            switch (token)
            {
                case "room": return RoomLiveLinkKind.Room;
                case "final-exit": return RoomLiveLinkKind.FinalExit;
                default:
                    throw Mapping(
                        "room-access-link-kind-unsupported",
                        path,
                        "Supported link_kind values are room and final-exit.");
            }
        }

        private static StableId ParseStableId(string value, string path)
        {
            try
            {
                return StableId.Parse(RequireText(value, path));
            }
            catch (RoomAccessMappingException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Mapping(
                    "room-access-stable-id-invalid",
                    path,
                    exception.Message);
            }
        }

        private static string RequireText(string value, string path)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw Mapping(
                    "room-access-value-required",
                    path,
                    "A non-empty value is required.");
            }
            return value.Trim();
        }

        private static T Require<T>(T value, string path)
            where T : class
        {
            if (value == null)
            {
                throw Mapping(
                    "room-access-value-required",
                    path,
                    "A value is required.");
            }
            return value;
        }

        private static List<T> RequireList<T>(List<T> values, string path)
        {
            if (values == null)
            {
                throw Mapping(
                    "room-access-array-required",
                    path,
                    "An array is required. Use an empty array when no entries exist.");
            }
            return values;
        }

        private static RoomAccessImportResult Failure(
            string code,
            string path,
            string message)
        {
            return new RoomAccessImportResult(
                null,
                new[] { new RoomAccessImportIssue(code, path, message) });
        }

        private static RoomAccessMappingException Mapping(
            string code,
            string path,
            string message)
        {
            return new RoomAccessMappingException(code, path, message);
        }

        private sealed class ConditionSource
        {
            public ConditionSource(
                RoomAccessConditionDefinition definition,
                string path)
            {
                Definition = definition;
                Path = path;
            }

            public RoomAccessConditionDefinition Definition { get; }

            public string Path { get; }
        }

        private sealed class RoomAccessMappingException : Exception
        {
            public RoomAccessMappingException(
                string code,
                string path,
                string message)
                : base(message)
            {
                Code = code;
                Path = path;
            }

            public string Code { get; }

            public string Path { get; }
        }

        [DataContract]
        private sealed class RootDto
        {
            [DataMember(Name = "version", IsRequired = true)]
            public int Version { get; set; }

            [DataMember(Name = "layout", IsRequired = true)]
            public string Layout { get; set; }

            [DataMember(
                Name = "reference_registry_fingerprint",
                EmitDefaultValue = false)]
            public string ReferenceRegistryFingerprint { get; set; }

            [DataMember(Name = "conditions", IsRequired = true)]
            public List<ConditionDto> Conditions { get; set; }

            [DataMember(Name = "doors", IsRequired = true)]
            public List<DoorDto> Doors { get; set; }
        }

        [DataContract]
        private sealed class ConditionDto
        {
            [DataMember(Name = "id", IsRequired = true)]
            public string Id { get; set; }

            [DataMember(Name = "kind", IsRequired = true)]
            public string Kind { get; set; }

            [DataMember(Name = "subject", EmitDefaultValue = false)]
            public string Subject { get; set; }

            [DataMember(Name = "minimum_difficulty", EmitDefaultValue = false)]
            public int MinimumDifficulty { get; set; }

            [DataMember(Name = "children", EmitDefaultValue = false)]
            public List<string> Children { get; set; }
        }

        [DataContract]
        private sealed class DoorDto
        {
            [DataMember(Name = "room", IsRequired = true)]
            public string Room { get; set; }

            [DataMember(Name = "door", EmitDefaultValue = false)]
            public string Door { get; set; }

            [DataMember(Name = "exit_type", EmitDefaultValue = false)]
            public string ExitType { get; set; }

            [DataMember(Name = "link_kind", EmitDefaultValue = false)]
            public string LinkKind { get; set; }

            [DataMember(Name = "condition", IsRequired = true)]
            public string Condition { get; set; }

            [DataMember(Name = "consume_holding", EmitDefaultValue = false)]
            public string ConsumeHolding { get; set; }
        }
    }
}
