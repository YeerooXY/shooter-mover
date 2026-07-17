using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    public static partial class LevelDesignFoundationValidator
    {
        public static LevelDesignValidationResult Validate(
            string levelId,
            IReadOnlyList<LevelRoomRecord> rooms,
            IReadOnlyList<LevelPlacementRecord> placements,
            IReadOnlyList<LevelDoorRecord> doors,
            IReadOnlyList<LevelVoidRecord> voids)
        {
            rooms = rooms ?? Array.Empty<LevelRoomRecord>();
            placements = placements ?? Array.Empty<LevelPlacementRecord>();
            doors = doors ?? Array.Empty<LevelDoorRecord>();
            voids = voids ?? Array.Empty<LevelVoidRecord>();

            List<LevelDesignValidationIssue> issues =
                new List<LevelDesignValidationIssue>();

            if (!LevelDesignAuthoringId.IsCanonical(levelId))
            {
                Add(
                    issues,
                    LevelDesignValidationSeverity.Error,
                    LevelDesignValidationCode.InvalidLevelIdentity,
                    levelId,
                    "level-root",
                    "Level identity must be a canonical StableId.");
            }

            Dictionary<string, LevelRoomRecord> roomById =
                ValidateRooms(rooms, issues);
            Dictionary<string, LevelPlacementRecord> placementBySocketId =
                ValidatePlacements(placements, roomById, voids, issues);
            ValidateDoors(doors, roomById, placementBySocketId, issues);
            ValidateVoids(voids, roomById, issues);
            ValidateGlobalIdentityUniqueness(rooms, placements, doors, voids, issues);
            ValidateRoomOverlaps(rooms, issues);
            ValidatePlacementOverlaps(placements, issues);

            List<LevelDesignValidationIssue> ordered = issues
                .OrderByDescending(issue => issue.Severity)
                .ThenBy(issue => issue.Code)
                .ThenBy(issue => issue.AuthoredId, StringComparer.Ordinal)
                .ThenBy(issue => issue.DiagnosticLocation, StringComparer.Ordinal)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                .ToList();

            return new LevelDesignValidationResult(ordered);
        }

        private static Dictionary<string, LevelRoomRecord> ValidateRooms(
            IReadOnlyList<LevelRoomRecord> rooms,
            ICollection<LevelDesignValidationIssue> issues)
        {
            Dictionary<string, LevelRoomRecord> roomById =
                new Dictionary<string, LevelRoomRecord>(StringComparer.Ordinal);

            for (int index = 0; index < rooms.Count; index++)
            {
                LevelRoomRecord room = rooms[index];
                if (room == null)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.InvalidAuthoredIdentity,
                        string.Empty,
                        "rooms[" + index + "]",
                        "Room record is missing.");
                    continue;
                }

                if (!LevelDesignAuthoringId.IsCanonical(room.RoomId))
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.InvalidAuthoredIdentity,
                        room.RoomId,
                        room.DiagnosticLocation,
                        "Room identity must be a canonical StableId.");
                    continue;
                }

                if (!roomById.ContainsKey(room.RoomId))
                {
                    roomById.Add(room.RoomId, room);
                }

                if (room.CellSize.x <= 0f
                    || room.CellSize.y <= 0f
                    || room.FootprintCells.x <= 0
                    || room.FootprintCells.y <= 0)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.InvalidGridMetadata,
                        room.RoomId,
                        room.DiagnosticLocation,
                        "Cell size and footprint dimensions must be positive.");
                }

                if (!room.HasBoundsCollider
                    || room.WorldBounds.width <= 0f
                    || room.WorldBounds.height <= 0f)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.MissingCollider,
                        room.RoomId,
                        room.DiagnosticLocation,
                        "Room requires an explicit non-empty bounds Collider2D.");
                }
            }

            return roomById;
        }

        private static Dictionary<string, LevelPlacementRecord> ValidatePlacements(
            IReadOnlyList<LevelPlacementRecord> placements,
            IReadOnlyDictionary<string, LevelRoomRecord> roomById,
            IReadOnlyList<LevelVoidRecord> voids,
            ICollection<LevelDesignValidationIssue> issues)
        {
            Dictionary<string, LevelPlacementRecord> placementBySocketId =
                new Dictionary<string, LevelPlacementRecord>(StringComparer.Ordinal);
            Dictionary<string, List<LevelPlacementRecord>> placementsBySocketId =
                new Dictionary<string, List<LevelPlacementRecord>>(StringComparer.Ordinal);
            for (int index = 0; index < placements.Count; index++)
            {
                LevelPlacementRecord placement = placements[index];
                if (placement == null)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.InvalidAuthoredIdentity,
                        string.Empty,
                        "placements[" + index + "]",
                        "Placement record is missing.");
                    continue;
                }

                if (!LevelDesignAuthoringId.IsCanonical(placement.AuthoredId))
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.InvalidAuthoredIdentity,
                        placement.AuthoredId,
                        placement.DiagnosticLocation,
                        "Placement identity must be a canonical StableId.");
                }

                if (!LevelDesignAuthoringId.IsCanonical(placement.SocketId))
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.InvalidSocketIdentity,
                        placement.AuthoredId,
                        placement.DiagnosticLocation,
                        "Placement socket identity must be a canonical StableId.");
                }
                else
                {
                    List<LevelPlacementRecord> socketPlacements;
                    if (!placementsBySocketId.TryGetValue(
                        placement.SocketId,
                        out socketPlacements))
                    {
                        socketPlacements = new List<LevelPlacementRecord>();
                        placementsBySocketId.Add(
                            placement.SocketId,
                            socketPlacements);
                        placementBySocketId.Add(placement.SocketId, placement);
                    }

                    socketPlacements.Add(placement);
                }

                if (string.IsNullOrEmpty(placement.RoomId)
                    || !roomById.ContainsKey(placement.RoomId))
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.MissingRoomReference,
                        placement.AuthoredId,
                        placement.DiagnosticLocation,
                        "Placement must reference a room in this authored foundation.");
                }

                bool needsDefinition = placement.Kind == LevelPlacementKind.EnemySpawn
                    || placement.Kind == LevelPlacementKind.PropPlacement
                    || placement.Kind == LevelPlacementKind.PickupSpawn
                    || placement.Kind == LevelPlacementKind.RewardSocket;
                if (needsDefinition && !placement.HasDefinition)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.MissingDefinitionReference,
                        placement.AuthoredId,
                        placement.DiagnosticLocation,
                        "This placement kind requires an existing definition/profile reference.");
                }

                bool needsPrefab = placement.Kind == LevelPlacementKind.PlayerSpawn
                    || placement.Kind == LevelPlacementKind.EnemySpawn
                    || placement.Kind == LevelPlacementKind.PropPlacement
                    || placement.Kind == LevelPlacementKind.PickupSpawn;
                if (needsPrefab && !placement.HasPrefab)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.MissingPrefabReference,
                        placement.AuthoredId,
                        placement.DiagnosticLocation,
                        "This placement kind requires an existing prefab reference.");
                }

                if (needsPrefab && !placement.HasPresentation)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Warning,
                        LevelDesignValidationCode.MissingPresentation,
                        placement.AuthoredId,
                        placement.DiagnosticLocation,
                        "Assign a presentation root for sorting and scene preview.");
                }

                if (placement.CollisionPolicy != LevelCollisionPolicy.None
                    && !placement.HasCollider)
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.MissingCollider,
                        placement.AuthoredId,
                        placement.DiagnosticLocation,
                        "The selected collision policy requires a Collider2D.");
                }

                if (!string.IsNullOrEmpty(placement.RewardOverrideId)
                    && !LevelDesignAuthoringId.IsCanonical(
                        placement.RewardOverrideId))
                {
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.InvalidRewardOverride,
                        placement.AuthoredId,
                        placement.DiagnosticLocation,
                        "Reward override must be empty or a canonical StableId.");
                }

                if (placement.Kind == LevelPlacementKind.PlayerSpawn
                    || placement.Kind == LevelPlacementKind.EnemySpawn)
                {
                    for (int voidIndex = 0; voidIndex < voids.Count; voidIndex++)
                    {
                        LevelVoidRecord region = voids[voidIndex];
                        if (region == null
                            || !string.Equals(
                                placement.RoomId,
                                region.RoomId,
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (placement.WorldBounds.Overlaps(region.WorldBounds))
                        {
                            Add(
                                issues,
                                LevelDesignValidationSeverity.Error,
                                LevelDesignValidationCode.SpawnInsideVoid,
                                placement.AuthoredId,
                                placement.DiagnosticLocation,
                                "Player and enemy spawn sockets cannot overlap a void region.");
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, List<LevelPlacementRecord>> pair
                in placementsBySocketId)
            {
                if (pair.Value.Count < 2)
                {
                    continue;
                }

                string locations = string.Join(
                    ", ",
                    pair.Value
                        .Select(value => value.DiagnosticLocation)
                        .OrderBy(value => value, StringComparer.Ordinal));
                for (int index = 0; index < pair.Value.Count; index++)
                {
                    LevelPlacementRecord placement = pair.Value[index];
                    Add(
                        issues,
                        LevelDesignValidationSeverity.Error,
                        LevelDesignValidationCode.DuplicateSocketIdentity,
                        placement.AuthoredId,
                        placement.DiagnosticLocation,
                        "Socket ID '" + pair.Key
                        + "' is duplicated at: " + locations + ".");
                }
            }

            return placementBySocketId;
        }

    }
}
