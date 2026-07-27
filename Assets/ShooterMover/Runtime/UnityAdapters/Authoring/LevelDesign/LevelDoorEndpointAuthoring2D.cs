using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [DisallowMultipleComponent]
    public sealed class LevelDoorEndpointAuthoring2D : MonoBehaviour
    {
        [Header("Stable identity")]
        [SerializeField] private string doorId = "door.unassigned";

        [Header("Owning room")]
        [SerializeField] private LevelRoomAuthoring2D owningRoom;

        [Header("Placement")]
        [SerializeField] private LevelDoorSideV2 side = LevelDoorSideV2.North;
        [SerializeField] private LevelDoorPlacementModeV2 placementMode =
            LevelDoorPlacementModeV2.EdgeManaged;
        [SerializeField] [Range(0f, 1f)] private float edgeOffset = 0.5f;
        [SerializeField] private Vector2 fixedLocalPosition = Vector2.zero;

        [Header("Traversal and map")]
        [SerializeField] private bool traversable = true;
        [SerializeField] private bool visibleOnMap = true;

        public string DoorIdText
        {
            get { return doorId; }
        }

        public LevelRoomAuthoring2D OwningRoom
        {
            get { return owningRoom; }
        }

        public LevelDoorSideV2 Side
        {
            get { return side; }
        }

        public LevelDoorPlacementModeV2 PlacementMode
        {
            get { return placementMode; }
        }

        public float EdgeOffset
        {
            get { return edgeOffset; }
        }

        public Vector2 FixedLocalPosition
        {
            get { return fixedLocalPosition; }
        }

        public bool Traversable
        {
            get { return traversable; }
        }

        public bool VisibleOnMap
        {
            get { return visibleOnMap; }
        }

        public LevelGridDoorRecordV2 BuildRecord()
        {
            return new LevelGridDoorRecordV2(
                doorId,
                owningRoom == null ? null : owningRoom.RoomIdText,
                side,
                placementMode,
                edgeOffset,
                fixedLocalPosition,
                traversable,
                visibleOnMap,
                BuildDiagnosticLocation());
        }

        public Vector3 ResolveTargetLocalPosition()
        {
            if (placementMode == LevelDoorPlacementModeV2.Fixed)
            {
                return new Vector3(
                    fixedLocalPosition.x,
                    fixedLocalPosition.y,
                    transform.localPosition.z);
            }

            if (owningRoom == null || owningRoom.RoomBounds == null)
            {
                return transform.localPosition;
            }

            Bounds worldBounds = owningRoom.RoomBounds.bounds;
            Vector3 worldPosition;
            switch (side)
            {
                case LevelDoorSideV2.North:
                    worldPosition = new Vector3(
                        Mathf.Lerp(worldBounds.min.x, worldBounds.max.x, edgeOffset),
                        worldBounds.max.y,
                        transform.position.z);
                    break;
                case LevelDoorSideV2.East:
                    worldPosition = new Vector3(
                        worldBounds.max.x,
                        Mathf.Lerp(worldBounds.min.y, worldBounds.max.y, edgeOffset),
                        transform.position.z);
                    break;
                case LevelDoorSideV2.South:
                    worldPosition = new Vector3(
                        Mathf.Lerp(worldBounds.min.x, worldBounds.max.x, edgeOffset),
                        worldBounds.min.y,
                        transform.position.z);
                    break;
                default:
                    worldPosition = new Vector3(
                        worldBounds.min.x,
                        Mathf.Lerp(worldBounds.min.y, worldBounds.max.y, edgeOffset),
                        transform.position.z);
                    break;
            }

            Transform localParent = transform.parent;
            return localParent == null
                ? worldPosition
                : localParent.InverseTransformPoint(worldPosition);
        }

        [ContextMenu("Assign New Stable ID")]
        public void AssignNewStableId()
        {
            doorId = LevelDesignAuthoringId.New("door");
        }

        [ContextMenu("Snap Door To Placement")]
        public void SnapToPlacement()
        {
            transform.localPosition = ResolveTargetLocalPosition();
        }

        public void ConfigureForTests(
            string configuredDoorId,
            LevelRoomAuthoring2D configuredOwningRoom,
            LevelDoorSideV2 configuredSide,
            LevelDoorPlacementModeV2 configuredPlacementMode,
            float configuredEdgeOffset,
            Vector2 configuredFixedLocalPosition,
            bool configuredTraversable)
        {
            doorId = configuredDoorId;
            owningRoom = configuredOwningRoom;
            side = configuredSide;
            placementMode = configuredPlacementMode;
            edgeOffset = configuredEdgeOffset;
            fixedLocalPosition = configuredFixedLocalPosition;
            traversable = configuredTraversable;
        }

        private void Reset()
        {
            owningRoom = GetComponentInParent<LevelRoomAuthoring2D>();
            fixedLocalPosition = transform.localPosition;
        }

        private void OnValidate()
        {
            edgeOffset = Mathf.Clamp01(edgeOffset);
            if (owningRoom == null)
            {
                owningRoom = GetComponentInParent<LevelRoomAuthoring2D>();
            }
        }

        private string BuildDiagnosticLocation()
        {
            return gameObject.scene.name + ":" + GetHierarchyPath(transform);
        }

        private static string GetHierarchyPath(Transform current)
        {
            string path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }

            return path;
        }
    }
}
