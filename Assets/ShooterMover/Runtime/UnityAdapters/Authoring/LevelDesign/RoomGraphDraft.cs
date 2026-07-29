using System;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [Serializable]
    public sealed class RoomSpawn
    {
        [SerializeField] private string stableId = "entry.unassigned";
        [SerializeField] private RoomSpawnPointKind kind =
            RoomSpawnPointKind.ForwardEntry;
        [SerializeField] private Vector2 localPosition;
        [SerializeField] private float localRotationDegrees;

        public RoomSpawnPointDefinition Build()
        {
            return new RoomSpawnPointDefinition(
                StableId.Parse(stableId),
                kind,
                new RoomVector2(localPosition.x, localPosition.y),
                localRotationDegrees);
        }
    }

    [Serializable]
    public sealed class RoomEntity
    {
        [SerializeField] private string instanceStableId = "entity-instance.unassigned";
        [SerializeField] private RoomLivePlacementKind placementKind =
            RoomLivePlacementKind.Enemy;
        [SerializeField] private string definitionStableId = "entity.unassigned";
        [SerializeField] private string presentationStableId = "presentation.unassigned";
        [SerializeField] private RoomOccupantClearRole clearRole =
            RoomOccupantClearRole.RequiredEnemy;
        [SerializeField] private Vector2 localPosition;
        [SerializeField] private float localRotationDegrees;

        public RoomPlacedEntityDefinition Build()
        {
            return new RoomPlacedEntityDefinition(
                StableId.Parse(instanceStableId),
                placementKind,
                StableId.Parse(definitionStableId),
                StableId.Parse(presentationStableId),
                clearRole,
                new RoomVector2(localPosition.x, localPosition.y),
                localRotationDegrees);
        }
    }

    [Serializable]
    public sealed class RoomDoorMarker
    {
        [SerializeField] private string doorInstanceStableId = "door-instance.unassigned";
        [SerializeField] private string presentationStableId =
            "presentation.environment-room-door";
        [SerializeField] private string exitStableId = "exit.unassigned";
        [SerializeField] private string[] requiredConditionStableIds =
            Array.Empty<string>();
        [SerializeField] private Vector2 localPosition;
        [SerializeField] private float localRotationDegrees;

        public RoomDoorDefinition Build()
        {
            string[] authoredConditions = requiredConditionStableIds
                ?? Array.Empty<string>();
            var conditionIds = new StableId[authoredConditions.Length];
            for (int index = 0; index < authoredConditions.Length; index++)
            {
                conditionIds[index] = StableId.Parse(authoredConditions[index]);
            }

            return new RoomDoorDefinition(
                StableId.Parse(doorInstanceStableId),
                StableId.Parse(presentationStableId),
                StableId.Parse(exitStableId),
                conditionIds,
                new RoomVector2(localPosition.x, localPosition.y),
                localRotationDegrees);
        }
    }

    [Serializable]
    public sealed class RoomExit
    {
        [SerializeField] private string exitStableId = "exit.unassigned";
        [SerializeField] private string doorInstanceStableId = "door-instance.unassigned";
        [SerializeField] private RoomLiveLinkKind linkKind = RoomLiveLinkKind.Room;
        [SerializeField] private RoomExitType exitType = RoomExitType.Progression;
        [SerializeField] private string targetRoomStableId = "room.unassigned";
        [SerializeField] private string targetSpawnPointStableId = "entry.unassigned";

        public RoomExitLinkDefinition Build()
        {
            return new RoomExitLinkDefinition(
                StableId.Parse(exitStableId),
                StableId.Parse(doorInstanceStableId),
                linkKind,
                exitType,
                linkKind == RoomLiveLinkKind.Room
                    ? StableId.Parse(targetRoomStableId)
                    : null,
                linkKind == RoomLiveLinkKind.Room
                    ? StableId.Parse(targetSpawnPointStableId)
                    : null);
        }
    }

    [Serializable]
    public sealed class RoomCompletion
    {
        [SerializeField] private string stableId = "completion.unassigned";
        [SerializeField] private RoomCompletionConditionKind kind =
            RoomCompletionConditionKind.AllBlockingOccupantsTerminal;
        [SerializeField] private string subjectStableId = string.Empty;
        [SerializeField] private bool requiredForRoomCompletion = true;

        public RoomCompletionConditionDefinition Build()
        {
            StableId subject = string.IsNullOrWhiteSpace(subjectStableId)
                ? null
                : StableId.Parse(subjectStableId);
            return new RoomCompletionConditionDefinition(
                StableId.Parse(stableId),
                kind,
                subject,
                requiredForRoomCompletion);
        }
    }

    [Serializable]
    public sealed class RoomDraftRecord
    {
        [SerializeField] private string roomStableId = "room.unassigned";
        [SerializeField] private int order;
        [SerializeField] private string displayName = "UNASSIGNED ROOM";
        [SerializeField] private Vector2 boundsCenter;
        [SerializeField] private Vector2 boundsSize = new Vector2(20f, 12f);
        [SerializeField] private RoomSpawn[] spawnPoints =
            Array.Empty<RoomSpawn>();
        [SerializeField] private RoomEntity[] enemyPlacements =
            Array.Empty<RoomEntity>();
        [SerializeField] private RoomEntity[] propPlacements =
            Array.Empty<RoomEntity>();
        [SerializeField] private RoomDoorMarker[] doors =
            Array.Empty<RoomDoorMarker>();
        [SerializeField] private RoomExit[] exits =
            Array.Empty<RoomExit>();
        [SerializeField] private RoomCompletion[] completionConditions =
            Array.Empty<RoomCompletion>();

        public AuthorableRoomDefinition Build()
        {
            RoomPlacedEntityDefinition[] enemies = BuildArray(
                enemyPlacements,
                item => item.Build());
            RoomPlacedEntityDefinition[] props = BuildArray(
                propPlacements,
                item => item.Build());
            RequirePlacementKind(
                enemies,
                RoomLivePlacementKind.Enemy,
                "room-live-enemy-placement-kind-mismatch");
            RequirePlacementKind(
                props,
                RoomLivePlacementKind.Prop,
                "room-live-prop-placement-kind-mismatch");
            var placements = new RoomPlacedEntityDefinition[
                enemies.Length + props.Length];
            Array.Copy(enemies, 0, placements, 0, enemies.Length);
            Array.Copy(props, 0, placements, enemies.Length, props.Length);

            return new AuthorableRoomDefinition(
                StableId.Parse(roomStableId),
                order,
                displayName,
                new RoomBounds(
                    new RoomVector2(boundsCenter.x, boundsCenter.y),
                    new RoomVector2(boundsSize.x, boundsSize.y)),
                BuildArray(spawnPoints, item => item.Build()),
                placements,
                BuildArray(doors, item => item.Build()),
                BuildArray(exits, item => item.Build()),
                BuildArray(completionConditions, item => item.Build()));
        }

        private static void RequirePlacementKind(
            RoomPlacedEntityDefinition[] placements,
            RoomLivePlacementKind expectedKind,
            string rejectionCode)
        {
            for (int index = 0; index < placements.Length; index++)
            {
                if (placements[index].PlacementKind != expectedKind)
                {
                    throw new InvalidOperationException(
                        rejectionCode + ":" + placements[index].InstanceStableId);
                }
            }
        }

        private static TResult[] BuildArray<TSource, TResult>(
            TSource[] source,
            Func<TSource, TResult> build)
            where TSource : class
        {
            TSource[] values = source ?? Array.Empty<TSource>();
            var result = new TResult[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == null)
                {
                    throw new InvalidOperationException(
                        "Authorable room arrays cannot contain null entries.");
                }

                result[index] = build(values[index]);
            }

            return result;
        }
    }

    [CreateAssetMenu(
        fileName = "RoomGraphDraft",
        menuName = "Shooter Mover/Level Design/Authorable Room Graph 2D")]
    public sealed class RoomGraphDraft : ScriptableObject
    {
        [SerializeField] private string layoutStableId = "layout.unassigned";
        [SerializeField] private string startRoomStableId = "room.unassigned-start";
        [SerializeField] private string terminalRoomStableId = "room.unassigned-terminal";
        [SerializeField] private RoomDraftRecord[] rooms =
            Array.Empty<RoomDraftRecord>();

        public AuthorableRoomGraphDefinition BuildDefinition()
        {
            RoomDraftRecord[] authoredRooms = rooms
                ?? Array.Empty<RoomDraftRecord>();
            var builtRooms = new AuthorableRoomDefinition[authoredRooms.Length];
            for (int index = 0; index < authoredRooms.Length; index++)
            {
                if (authoredRooms[index] == null)
                {
                    throw new InvalidOperationException(
                        "Authorable room graph cannot contain null room records.");
                }

                builtRooms[index] = authoredRooms[index].Build();
            }

            return new AuthorableRoomGraphDefinition(
                StableId.Parse(layoutStableId),
                StableId.Parse(startRoomStableId),
                StableId.Parse(terminalRoomStableId),
                builtRooms);
        }

        public bool TryBuildDefinition(
            out AuthorableRoomGraphDefinition definition,
            out string error)
        {
            try
            {
                definition = BuildDefinition();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                definition = null;
                error = exception.Message;
                return false;
            }
        }
    }
}
