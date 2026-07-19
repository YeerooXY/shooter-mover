using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;

namespace ShooterMover.Contracts.Missions.Rooms
{
    public enum RoomLivePlacementKindV1
    {
        Enemy = 1,
        Prop = 2,
    }

    public enum RoomSpawnPointKindV1
    {
        ForwardEntry = 1,
        ReturnEntry = 2,
        Player = 3,
        Auxiliary = 4,
    }

    public enum RoomCompletionConditionKindV1
    {
        AlwaysSatisfied = 1,
        AllBlockingOccupantsTerminal = 2,
        CollectedDrop = 3,
    }

    public enum RoomLiveLinkKindV1
    {
        Room = 1,
        FinalExit = 2,
    }

    public sealed class RoomVector2V1
    {
        public RoomVector2V1(double x, double y)
        {
            RequireFinite(x, nameof(x));
            RequireFinite(y, nameof(y));
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }

        internal void AppendCanonicalJson(StringBuilder builder)
        {
            builder.Append("{\"x\":")
                .Append(X.ToString("R", CultureInfo.InvariantCulture))
                .Append(",\"y\":")
                .Append(Y.ToString("R", CultureInfo.InvariantCulture))
                .Append('}');
        }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class RoomBoundsV1
    {
        public RoomBoundsV1(RoomVector2V1 center, RoomVector2V1 size)
        {
            Center = center ?? throw new ArgumentNullException(nameof(center));
            Size = size ?? throw new ArgumentNullException(nameof(size));
            if (size.X <= 0d || size.Y <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(size),
                    "Room bounds require positive width and height.");
            }
        }

        public RoomVector2V1 Center { get; }

        public RoomVector2V1 Size { get; }

        internal void AppendCanonicalJson(StringBuilder builder)
        {
            builder.Append("{\"center\":");
            Center.AppendCanonicalJson(builder);
            builder.Append(",\"size\":");
            Size.AppendCanonicalJson(builder);
            builder.Append('}');
        }
    }

    public sealed class RoomSpawnPointDefinitionV1
    {
        public RoomSpawnPointDefinitionV1(
            StableId spawnPointStableId,
            RoomSpawnPointKindV1 kind,
            RoomVector2V1 localPosition,
            double localRotationDegrees)
        {
            SpawnPointStableId = spawnPointStableId
                ?? throw new ArgumentNullException(nameof(spawnPointStableId));
            if (!Enum.IsDefined(typeof(RoomSpawnPointKindV1), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            LocalPosition = localPosition
                ?? throw new ArgumentNullException(nameof(localPosition));
            RequireFinite(localRotationDegrees, nameof(localRotationDegrees));
            Kind = kind;
            LocalRotationDegrees = localRotationDegrees;
        }

        public StableId SpawnPointStableId { get; }

        public RoomSpawnPointKindV1 Kind { get; }

        public RoomVector2V1 LocalPosition { get; }

        public double LocalRotationDegrees { get; }

        internal void AppendCanonicalJson(StringBuilder builder)
        {
            builder.Append("{\"id\":");
            RoomLiveJsonV1.AppendString(builder, SpawnPointStableId.ToString());
            builder.Append(",\"kind\":")
                .Append(((int)Kind).ToString(CultureInfo.InvariantCulture))
                .Append(",\"position\":");
            LocalPosition.AppendCanonicalJson(builder);
            builder.Append(",\"rotation\":")
                .Append(LocalRotationDegrees.ToString("R", CultureInfo.InvariantCulture))
                .Append('}');
        }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class RoomPlacedEntityDefinitionV1
    {
        public RoomPlacedEntityDefinitionV1(
            StableId instanceStableId,
            RoomLivePlacementKindV1 placementKind,
            StableId definitionStableId,
            StableId presentationStableId,
            RoomOccupantClearRoleV1 clearRole,
            RoomVector2V1 localPosition,
            double localRotationDegrees)
        {
            InstanceStableId = instanceStableId
                ?? throw new ArgumentNullException(nameof(instanceStableId));
            DefinitionStableId = definitionStableId
                ?? throw new ArgumentNullException(nameof(definitionStableId));
            PresentationStableId = presentationStableId
                ?? throw new ArgumentNullException(nameof(presentationStableId));
            if (!Enum.IsDefined(typeof(RoomLivePlacementKindV1), placementKind))
            {
                throw new ArgumentOutOfRangeException(nameof(placementKind));
            }

            if (!Enum.IsDefined(typeof(RoomOccupantClearRoleV1), clearRole))
            {
                throw new ArgumentOutOfRangeException(nameof(clearRole));
            }

            LocalPosition = localPosition
                ?? throw new ArgumentNullException(nameof(localPosition));
            RequireFinite(localRotationDegrees, nameof(localRotationDegrees));
            PlacementKind = placementKind;
            ClearRole = clearRole;
            LocalRotationDegrees = localRotationDegrees;
        }

        public StableId InstanceStableId { get; }

        public RoomLivePlacementKindV1 PlacementKind { get; }

        public StableId DefinitionStableId { get; }

        public StableId PresentationStableId { get; }

        public RoomOccupantClearRoleV1 ClearRole { get; }

        public RoomVector2V1 LocalPosition { get; }

        public double LocalRotationDegrees { get; }

        internal void AppendCanonicalJson(StringBuilder builder)
        {
            builder.Append("{\"instance_id\":");
            RoomLiveJsonV1.AppendString(builder, InstanceStableId.ToString());
            builder.Append(",\"placement_kind\":")
                .Append(((int)PlacementKind).ToString(CultureInfo.InvariantCulture))
                .Append(",\"definition_id\":");
            RoomLiveJsonV1.AppendString(builder, DefinitionStableId.ToString());
            builder.Append(",\"presentation_id\":");
            RoomLiveJsonV1.AppendString(builder, PresentationStableId.ToString());
            builder.Append(",\"clear_role\":")
                .Append(((int)ClearRole).ToString(CultureInfo.InvariantCulture))
                .Append(",\"position\":");
            LocalPosition.AppendCanonicalJson(builder);
            builder.Append(",\"rotation\":")
                .Append(LocalRotationDegrees.ToString("R", CultureInfo.InvariantCulture))
                .Append('}');
        }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class RoomDoorDefinitionV1
    {
        private readonly ReadOnlyCollection<StableId> requiredConditionStableIds;

        public RoomDoorDefinitionV1(
            StableId doorInstanceStableId,
            StableId presentationStableId,
            StableId exitStableId,
            IEnumerable<StableId> requiredConditionStableIds,
            RoomVector2V1 localPosition,
            double localRotationDegrees)
        {
            DoorInstanceStableId = doorInstanceStableId
                ?? throw new ArgumentNullException(nameof(doorInstanceStableId));
            PresentationStableId = presentationStableId
                ?? throw new ArgumentNullException(nameof(presentationStableId));
            ExitStableId = exitStableId
                ?? throw new ArgumentNullException(nameof(exitStableId));
            LocalPosition = localPosition
                ?? throw new ArgumentNullException(nameof(localPosition));
            RequireFinite(localRotationDegrees, nameof(localRotationDegrees));
            this.requiredConditionStableIds = CopyUniqueIds(
                requiredConditionStableIds,
                nameof(requiredConditionStableIds));
            if (this.requiredConditionStableIds.Count == 0)
            {
                throw new ArgumentException(
                    "Every room door must reference at least one authored gate condition.",
                    nameof(requiredConditionStableIds));
            }

            LocalRotationDegrees = localRotationDegrees;
        }

        public StableId DoorInstanceStableId { get; }

        public StableId PresentationStableId { get; }

        public StableId ExitStableId { get; }

        public IReadOnlyList<StableId> RequiredConditionStableIds
        {
            get { return requiredConditionStableIds; }
        }

        public RoomVector2V1 LocalPosition { get; }

        public double LocalRotationDegrees { get; }

        internal void AppendCanonicalJson(StringBuilder builder)
        {
            builder.Append("{\"instance_id\":");
            RoomLiveJsonV1.AppendString(builder, DoorInstanceStableId.ToString());
            builder.Append(",\"presentation_id\":");
            RoomLiveJsonV1.AppendString(builder, PresentationStableId.ToString());
            builder.Append(",\"exit_id\":");
            RoomLiveJsonV1.AppendString(builder, ExitStableId.ToString());
            builder.Append(",\"required_condition_ids\":[");
            for (int index = 0; index < requiredConditionStableIds.Count; index++)
            {
                if (index != 0) builder.Append(',');
                RoomLiveJsonV1.AppendString(
                    builder,
                    requiredConditionStableIds[index].ToString());
            }

            builder.Append("],\"position\":");
            LocalPosition.AppendCanonicalJson(builder);
            builder.Append(",\"rotation\":")
                .Append(LocalRotationDegrees.ToString("R", CultureInfo.InvariantCulture))
                .Append('}');
        }

        private static ReadOnlyCollection<StableId> CopyUniqueIds(
            IEnumerable<StableId> source,
            string parameterName)
        {
            var copy = new List<StableId>();
            var seen = new HashSet<StableId>();
            foreach (StableId value in source ?? throw new ArgumentNullException(parameterName))
            {
                if (value == null)
                {
                    throw new ArgumentException(
                        "Door condition identities cannot contain null values.",
                        parameterName);
                }

                if (!seen.Add(value))
                {
                    throw new ArgumentException(
                        "room-live-door-condition-duplicate:" + value,
                        parameterName);
                }

                copy.Add(value);
            }

            copy.Sort();
            return new ReadOnlyCollection<StableId>(copy);
        }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class RoomExitLinkDefinitionV1
    {
        public RoomExitLinkDefinitionV1(
            StableId exitStableId,
            StableId doorInstanceStableId,
            RoomLiveLinkKindV1 linkKind,
            RoomExitTypeV1 exitType,
            StableId targetRoomStableId,
            StableId targetSpawnPointStableId)
        {
            ExitStableId = exitStableId
                ?? throw new ArgumentNullException(nameof(exitStableId));
            DoorInstanceStableId = doorInstanceStableId
                ?? throw new ArgumentNullException(nameof(doorInstanceStableId));
            if (!Enum.IsDefined(typeof(RoomLiveLinkKindV1), linkKind))
            {
                throw new ArgumentOutOfRangeException(nameof(linkKind));
            }

            if (!Enum.IsDefined(typeof(RoomExitTypeV1), exitType))
            {
                throw new ArgumentOutOfRangeException(nameof(exitType));
            }

            if (linkKind == RoomLiveLinkKindV1.Room)
            {
                TargetRoomStableId = targetRoomStableId
                    ?? throw new ArgumentNullException(nameof(targetRoomStableId));
                TargetSpawnPointStableId = targetSpawnPointStableId
                    ?? throw new ArgumentNullException(nameof(targetSpawnPointStableId));
            }
            else if (targetRoomStableId != null || targetSpawnPointStableId != null)
            {
                throw new ArgumentException(
                    "Final exits cannot reference a target room or spawn point.");
            }

            LinkKind = linkKind;
            ExitType = exitType;
        }

        public StableId ExitStableId { get; }

        public StableId DoorInstanceStableId { get; }

        public RoomLiveLinkKindV1 LinkKind { get; }

        public RoomExitTypeV1 ExitType { get; }

        public StableId TargetRoomStableId { get; }

        public StableId TargetSpawnPointStableId { get; }

        internal void AppendCanonicalJson(StringBuilder builder)
        {
            builder.Append("{\"exit_id\":");
            RoomLiveJsonV1.AppendString(builder, ExitStableId.ToString());
            builder.Append(",\"door_instance_id\":");
            RoomLiveJsonV1.AppendString(builder, DoorInstanceStableId.ToString());
            builder.Append(",\"link_kind\":")
                .Append(((int)LinkKind).ToString(CultureInfo.InvariantCulture))
                .Append(",\"exit_type\":")
                .Append(((int)ExitType).ToString(CultureInfo.InvariantCulture))
                .Append(",\"target_room_id\":");
            RoomLiveJsonV1.AppendNullableStableId(builder, TargetRoomStableId);
            builder.Append(",\"target_spawn_point_id\":");
            RoomLiveJsonV1.AppendNullableStableId(builder, TargetSpawnPointStableId);
            builder.Append('}');
        }
    }

    public sealed class RoomCompletionConditionDefinitionV1
    {
        public RoomCompletionConditionDefinitionV1(
            StableId conditionStableId,
            RoomCompletionConditionKindV1 kind,
            StableId subjectStableId,
            bool isRequiredForRoomCompletion)
        {
            ConditionStableId = conditionStableId
                ?? throw new ArgumentNullException(nameof(conditionStableId));
            if (!Enum.IsDefined(typeof(RoomCompletionConditionKindV1), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (kind == RoomCompletionConditionKindV1.CollectedDrop)
            {
                SubjectStableId = subjectStableId
                    ?? throw new ArgumentNullException(nameof(subjectStableId));
            }
            else if (subjectStableId != null)
            {
                throw new ArgumentException(
                    "Only subject-based conditions may carry a subject identity.",
                    nameof(subjectStableId));
            }

            Kind = kind;
            IsRequiredForRoomCompletion = isRequiredForRoomCompletion;
        }

        public StableId ConditionStableId { get; }

        public RoomCompletionConditionKindV1 Kind { get; }

        public StableId SubjectStableId { get; }

        public bool IsRequiredForRoomCompletion { get; }

        internal void AppendCanonicalJson(StringBuilder builder)
        {
            builder.Append("{\"id\":");
            RoomLiveJsonV1.AppendString(builder, ConditionStableId.ToString());
            builder.Append(",\"kind\":")
                .Append(((int)Kind).ToString(CultureInfo.InvariantCulture))
                .Append(",\"subject_id\":");
            RoomLiveJsonV1.AppendNullableStableId(builder, SubjectStableId);
            builder.Append(",\"required_for_room_completion\":")
                .Append(IsRequiredForRoomCompletion ? "true" : "false")
                .Append('}');
        }
    }

    internal static class RoomLiveJsonV1
    {
        public static void AppendNullableStableId(
            StringBuilder builder,
            StableId value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            AppendString(builder, value.ToString());
        }

        public static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            string source = value ?? string.Empty;
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < 32)
                        {
                            builder.Append("\\u")
                                .Append(((int)character).ToString(
                                    "x4",
                                    CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }

        public static string ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }
}
