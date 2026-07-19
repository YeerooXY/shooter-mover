using System;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [Serializable]
    public sealed class RoomSpawnPointAuthoring2D
    {
        [SerializeField] private string stableId = "entry.unassigned";
        [SerializeField] private RoomSpawnPointKindV1 kind =
            RoomSpawnPointKindV1.ForwardEntry;
        [SerializeField] private Vector2 localPosition;
        [SerializeField] private float localRotationDegrees;

        public RoomSpawnPointDefinitionV1 Build()
        {
            return new RoomSpawnPointDefinitionV1(
                StableId.Parse(stableId),
                kind,
                new RoomVector2V1(localPosition.x, localPosition.y),
                localRotationDegrees);
        }
    }

    [Serializable]
    public sealed class RoomPlacedEntityAuthoring2D
    {
        [SerializeField] private string instanceStableId = "entity-instance.unassigned";
        [SerializeField] private RoomLivePlacementKindV1 placementKind =
            RoomLivePlacementKindV1.Enemy;
        [SerializeField] private string definitionStableId = "entity.unassigned";
        [SerializeField] private string presentationStableId = "presentation.unassigned";
        [SerializeField] private RoomOccupantClearRoleV1 clearRole =
            RoomOccupantClearRoleV1.RequiredEnemy;
        [SerializeField] private Vector2 localPosition;
        [SerializeField] private float localRotationDegrees;

        public RoomPlacedEntityDefinitionV1 Build()
        {
            return new RoomPlacedEntityDefinitionV1(
                StableId.Parse(instanceStableId),
                placementKind,
                StableId.Parse(definitionStableId),
                StableId.Parse(presentationStableId),
                clearRole,
                new RoomVector2V1(localPosition.x, localPosition.y),
                localRotationDegrees);
        }
    }

    [Serializable]
    public sealed class RoomDoorAuthoring2D
    {
        [SerializeField] private string doorInstanceStableId = "door-instance.unassigned";
        [SerializeField] private string presentationStableId =
            "presentation.environment-room-door";
        [SerializeField] private string exitStableId = "exit.unassigned";
        [SerializeField] private Vector2 localPosition;
        [SerializeField] private float localRotationDegrees;

        public RoomDoorDefinitionV1 Build()
        {
            return new RoomDoorDefinitionV1(
                StableId.Parse(doorInstanceStableId),
                StableId.Parse(presentationStableId),
                StableId.Parse(exitStableId),
                new RoomVector2V1(localPosition.x, localPosition.y),
                localRotationDegrees);
        }
    }

    [Serializable]
    public sealed class RoomExitLinkAuthoring2D
    {
        [SerializeField] private string exitStableId = "exit.unassigned";
        [SerializeField] private string doorInstanceStableId = "door-instance.unassigned";
        [SerializeField] private RoomLiveLinkKindV1 linkKind = RoomLiveLinkKindV1.Room;
        [SerializeField] private string targetRoomStableId = "room.unassigned";
        [SerializeField] private string targetSpawnPointStableId = "entry.unassigned";

        public RoomExitLinkDefinitionV1 Build()
        {
            return new RoomExitLinkDefinitionV1(
                StableId.Parse(exitStableId),
                StableId.Parse(doorInstanceStableId),
                linkKind,
                linkKind == RoomLiveLinkKindV1.Room
                    ? StableId.Parse(targetRoomStableId)
                    : null,
                linkKind == RoomLiveLinkKindV1.Room
                    ? StableId.Parse(targetSpawnPointStableId)
                    : null);
        }
    }

    [Serializable]
    public sealed class RoomCompletionConditionAuthoring2D
    {
        [SerializeField] private string stableId = "completion.unassigned";
        [SerializeField] private RoomCompletionConditionKindV1 kind =
            RoomCompletionConditionKindV1.AllBlockingOccupantsTerminal;

        public RoomCompletionConditionDefinitionV1 Build()
        {
            return new RoomCompletionConditionDefinitionV1(
                StableId.Parse(stableId),
                kind);
        }
    }

    [Serializable]
    public sealed class AuthorableRoomRecord2D
    {
        [SerializeField] private string roomStableId = "room.unassigned";
        [SerializeField] private int order;
        [SerializeField] private string displayName = "UNASSIGNED ROOM";
        [SerializeField] private Vector2 boundsCenter;
        [SerializeField] private Vector2 boundsSize = new Vector2(20f, 12f);
        [SerializeField] private RoomSpawnPointAuthoring2D[] spawnPoints =
            Array.Empty<RoomSpawnPointAuthoring2D>();
        [SerializeField] private RoomPlacedEntityAuthoring2D[] enemyPlacements =
            Array.Empty<RoomPlacedEntityAuthoring2D>();
        [SerializeField] private RoomPlacedEntityAuthoring2D[] propPlacements =
            Array.Empty<RoomPlacedEntityAuthoring2D>();
        [SerializeField] private RoomDoorAuthoring2D[] doors =
            Array.Empty<RoomDoorAuthoring2D>();
        [SerializeField] private RoomExitLinkAuthoring2D[] exits =
            Array.Empty<RoomExitLinkAuthoring2D>();
        [SerializeField] private RoomCompletionConditionAuthoring2D[] completionConditions =
            Array.Empty<RoomCompletionConditionAuthoring2D>();

        public AuthorableRoomDefinitionV1 Build()
        {
            RoomPlacedEntityDefinitionV1[] enemies = BuildArray(
                enemyPlacements,
                item => item.Build());
            RoomPlacedEntityDefinitionV1[] props = BuildArray(
                propPlacements,
                item => item.Build());
            RequirePlacementKind(
                enemies,
                RoomLivePlacementKindV1.Enemy,
                "room-live-enemy-placement-kind-mismatch");
            RequirePlacementKind(
                props,
                RoomLivePlacementKindV1.Prop,
                "room-live-prop-placement-kind-mismatch");
            var placements = new RoomPlacedEntityDefinitionV1[
                enemies.Length + props.Length];
            Array.Copy(enemies, 0, placements, 0, enemies.Length);
            Array.Copy(props, 0, placements, enemies.Length, props.Length);

            return new AuthorableRoomDefinitionV1(
                StableId.Parse(roomStableId),
                order,
                displayName,
                new RoomBoundsV1(
                    new RoomVector2V1(boundsCenter.x, boundsCenter.y),
                    new RoomVector2V1(boundsSize.x, boundsSize.y)),
                BuildArray(spawnPoints, item => item.Build()),
                placements,
                BuildArray(doors, item => item.Build()),
                BuildArray(exits, item => item.Build()),
                BuildArray(completionConditions, item => item.Build()));
        }

        private static void RequirePlacementKind(
            RoomPlacedEntityDefinitionV1[] placements,
            RoomLivePlacementKindV1 expectedKind,
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

    /// <summary>
    /// Inspector-authorable whole-level room graph. Unity references are intentionally
    /// excluded from the durable definition; presentationStableId values resolve through
    /// RoomPresentationCatalog2D at the composition boundary.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AuthorableRoomGraphDefinition2D",
        menuName = "Shooter Mover/Level Design/Authorable Room Graph 2D")]
    public sealed class AuthorableRoomGraphDefinition2D : ScriptableObject
    {
        [SerializeField] private string layoutStableId = "layout.unassigned";
        [SerializeField] private string startRoomStableId = "room.unassigned-start";
        [SerializeField] private string terminalRoomStableId = "room.unassigned-terminal";
        [SerializeField] private AuthorableRoomRecord2D[] rooms =
            Array.Empty<AuthorableRoomRecord2D>();

        public AuthorableRoomGraphDefinitionV1 BuildDefinition()
        {
            AuthorableRoomRecord2D[] authoredRooms = rooms
                ?? Array.Empty<AuthorableRoomRecord2D>();
            var builtRooms = new AuthorableRoomDefinitionV1[authoredRooms.Length];
            for (int index = 0; index < authoredRooms.Length; index++)
            {
                if (authoredRooms[index] == null)
                {
                    throw new InvalidOperationException(
                        "Authorable room graph cannot contain null room records.");
                }

                builtRooms[index] = authoredRooms[index].Build();
            }

            return new AuthorableRoomGraphDefinitionV1(
                StableId.Parse(layoutStableId),
                StableId.Parse(startRoomStableId),
                StableId.Parse(terminalRoomStableId),
                builtRooms);
        }

        public bool TryBuildDefinition(
            out AuthorableRoomGraphDefinitionV1 definition,
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
