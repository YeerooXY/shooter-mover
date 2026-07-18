using System;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [Serializable]
    public sealed class RoomContentPlacement2D
    {
        [SerializeField] private string instanceStableId = "spawn.unassigned";
        [SerializeField] private LevelPlacementKind placementKind =
            LevelPlacementKind.EnemySpawn;
        [SerializeField] private string contentStableId = "enemy.unassigned";
        [SerializeField] private GameObject prefab;
        [SerializeField] private Vector2 localPosition;
        [SerializeField] private float localRotationDegrees;

        public string InstanceStableIdText => instanceStableId;

        public StableId InstanceStableId => StableId.Parse(instanceStableId);

        public LevelPlacementKind PlacementKind => placementKind;

        public string ContentStableIdText => contentStableId;

        public StableId ContentStableId => StableId.Parse(contentStableId);

        public GameObject Prefab => prefab;

        public Vector2 LocalPosition => localPosition;

        public float LocalRotationDegrees => localRotationDegrees;

        public bool TryValidate(out string error)
        {
            StableId ignored;
            if (!StableId.TryParse(instanceStableId, out ignored))
            {
                error = "room-content-instance-id-invalid:" + instanceStableId;
                return false;
            }

            if (!Enum.IsDefined(typeof(LevelPlacementKind), placementKind))
            {
                error = "room-content-placement-kind-invalid:" + instanceStableId;
                return false;
            }

            if (!StableId.TryParse(contentStableId, out ignored))
            {
                error = "room-content-content-id-invalid:" + instanceStableId;
                return false;
            }

            if (prefab == null)
            {
                error = "room-content-prefab-missing:" + instanceStableId;
                return false;
            }

            if (float.IsNaN(localRotationDegrees)
                || float.IsInfinity(localRotationDegrees))
            {
                error = "room-content-rotation-invalid:" + instanceStableId;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void ConfigureForTests(
            string configuredInstanceStableId,
            LevelPlacementKind configuredPlacementKind,
            string configuredContentStableId,
            GameObject configuredPrefab,
            Vector2 configuredLocalPosition,
            float configuredLocalRotationDegrees)
        {
            instanceStableId = configuredInstanceStableId;
            placementKind = configuredPlacementKind;
            contentStableId = configuredContentStableId;
            prefab = configuredPrefab;
            localPosition = configuredLocalPosition;
            localRotationDegrees = configuredLocalRotationDegrees;
        }
    }

    /// <summary>
    /// Designer-authored contents for one room. Stable content IDs are kept beside
    /// Unity prefab references so a future JSON/UGC format can serialize IDs and
    /// transforms without treating Unity object references as durable save data.
    /// Room connectivity remains owned by the engine-independent room graph.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RoomContentDefinition2D",
        menuName = "Shooter Mover/Level Design/Room Content Definition 2D")]
    public sealed class RoomContentDefinition2D : ScriptableObject
    {
        [SerializeField] private string roomStableId = "room.unassigned";
        [SerializeField] private string displayName = "UNASSIGNED ROOM";
        [SerializeField] private Vector2 forwardEntryPosition = new Vector2(-11f, 0f);
        [SerializeField] private Vector2 returnEntryPosition = new Vector2(11f, 0f);
        [SerializeField] private RoomContentPlacement2D[] placements =
            Array.Empty<RoomContentPlacement2D>();

        public string RoomStableIdText => roomStableId;

        public StableId RoomStableId => StableId.Parse(roomStableId);

        public string DisplayName => displayName ?? string.Empty;

        public Vector2 ForwardEntryPosition => forwardEntryPosition;

        public Vector2 ReturnEntryPosition => returnEntryPosition;

        public RoomContentPlacement2D[] Placements =>
            placements == null
                ? Array.Empty<RoomContentPlacement2D>()
                : (RoomContentPlacement2D[])placements.Clone();

        public bool TryValidate(out string error)
        {
            StableId ignored;
            if (!StableId.TryParse(roomStableId, out ignored))
            {
                error = "room-content-room-id-invalid:" + roomStableId;
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                error = "room-content-display-name-missing:" + roomStableId;
                return false;
            }

            var seen = new System.Collections.Generic.HashSet<StableId>();
            RoomContentPlacement2D[] authoredPlacements = placements
                ?? Array.Empty<RoomContentPlacement2D>();
            for (int index = 0; index < authoredPlacements.Length; index++)
            {
                RoomContentPlacement2D placement = authoredPlacements[index];
                if (placement == null)
                {
                    error = "room-content-placement-null:" + index;
                    return false;
                }

                if (!placement.TryValidate(out error))
                {
                    return false;
                }

                if (!seen.Add(placement.InstanceStableId))
                {
                    error = "room-content-instance-id-duplicate:"
                        + placement.InstanceStableIdText;
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public void ValidateOrThrow()
        {
            if (!TryValidate(out string error))
            {
                throw new InvalidOperationException(error);
            }
        }

        public void ConfigureForTests(
            string configuredRoomStableId,
            string configuredDisplayName,
            Vector2 configuredForwardEntryPosition,
            Vector2 configuredReturnEntryPosition,
            RoomContentPlacement2D[] configuredPlacements)
        {
            roomStableId = configuredRoomStableId;
            displayName = configuredDisplayName;
            forwardEntryPosition = configuredForwardEntryPosition;
            returnEntryPosition = configuredReturnEntryPosition;
            placements = configuredPlacements == null
                ? Array.Empty<RoomContentPlacement2D>()
                : (RoomContentPlacement2D[])configuredPlacements.Clone();
        }
    }
}
