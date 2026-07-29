using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [DisallowMultipleComponent]
    public sealed class DoorEndpoint : MonoBehaviour
    {
        private const int OwningRoomFixedPositionSpaceVersion = 1;

        [Header("Stable identity")]
        [SerializeField] private string doorId = "door.unassigned";

        [Header("Owning room")]
        [SerializeField] private LevelRoom owningRoom;

        [Header("Placement")]
        [SerializeField] private LevelDoorSide side = LevelDoorSide.North;
        [SerializeField] private LevelDoorPlacementMode placementMode =
            LevelDoorPlacementMode.EdgeManaged;
        [SerializeField] [Range(0f, 1f)] private float edgeOffset = 0.5f;
        [Tooltip(
            "Stored relative to the owning room. Legacy parent-relative values are "
                + "migrated without moving the endpoint.")]
        [SerializeField] private Vector2 fixedLocalPosition = Vector2.zero;
        [SerializeField] [HideInInspector] private int fixedPositionSpaceVersion;
        [Tooltip("When enabled, connected edge-managed doors follow the relative room direction.")]
        [SerializeField] private bool autoFaceConnection = true;

        [Header("Traversal and map")]
        [SerializeField] private bool traversable = true;
        [SerializeField] private bool visibleOnMap = true;

        public string DoorIdText
        {
            get { return doorId; }
        }

        public LevelRoom OwningRoom
        {
            get { return owningRoom; }
        }

        public LevelDoorSide Side
        {
            get { return side; }
        }

        public LevelDoorPlacementMode PlacementMode
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

        public bool UsesOwningRoomFixedPositionSpace
        {
            get
            {
                return fixedPositionSpaceVersion
                    >= OwningRoomFixedPositionSpaceVersion;
            }
        }

        public bool AutoFaceConnection
        {
            get { return autoFaceConnection; }
        }

        public bool Traversable
        {
            get { return traversable; }
        }

        public bool VisibleOnMap
        {
            get { return visibleOnMap; }
        }

        public LevelGridDoorRecord BuildRecord()
        {
            Vector2 recordFixedPosition = placementMode == LevelDoorPlacementMode.Fixed
                    && !UsesOwningRoomFixedPositionSpace
                ? ResolveCurrentRoomRelativePosition()
                : fixedLocalPosition;
            return new LevelGridDoorRecord(
                doorId,
                owningRoom == null ? null : owningRoom.RoomIdText,
                side,
                placementMode,
                edgeOffset,
                recordFixedPosition,
                traversable,
                visibleOnMap,
                autoFaceConnection,
                BuildDiagnosticLocation());
        }

        public Vector3 ResolveTargetLocalPosition()
        {
            if (placementMode == LevelDoorPlacementMode.Fixed)
            {
                if (!UsesOwningRoomFixedPositionSpace)
                {
                    return new Vector3(
                        fixedLocalPosition.x,
                        fixedLocalPosition.y,
                        transform.localPosition.z);
                }

                Vector3 roomRelative = new Vector3(
                    fixedLocalPosition.x,
                    fixedLocalPosition.y,
                    0f);
                Vector3 worldPosition = owningRoom == null
                    ? roomRelative
                    : owningRoom.transform.TransformPoint(roomRelative);
                worldPosition.z = transform.position.z;
                Transform localParent = transform.parent;
                return localParent == null
                    ? worldPosition
                    : localParent.InverseTransformPoint(worldPosition);
            }

            if (owningRoom == null || owningRoom.RoomBounds == null)
            {
                return transform.localPosition;
            }

            Bounds worldBounds = owningRoom.RoomBounds.bounds;
            Vector3 edgeWorldPosition;
            switch (side)
            {
                case LevelDoorSide.North:
                    edgeWorldPosition = new Vector3(
                        Mathf.Lerp(worldBounds.min.x, worldBounds.max.x, edgeOffset),
                        worldBounds.max.y,
                        transform.position.z);
                    break;
                case LevelDoorSide.East:
                    edgeWorldPosition = new Vector3(
                        worldBounds.max.x,
                        Mathf.Lerp(worldBounds.min.y, worldBounds.max.y, edgeOffset),
                        transform.position.z);
                    break;
                case LevelDoorSide.South:
                    edgeWorldPosition = new Vector3(
                        Mathf.Lerp(worldBounds.min.x, worldBounds.max.x, edgeOffset),
                        worldBounds.min.y,
                        transform.position.z);
                    break;
                default:
                    edgeWorldPosition = new Vector3(
                        worldBounds.min.x,
                        Mathf.Lerp(worldBounds.min.y, worldBounds.max.y, edgeOffset),
                        transform.position.z);
                    break;
            }

            Transform edgeLocalParent = transform.parent;
            return edgeLocalParent == null
                ? edgeWorldPosition
                : edgeLocalParent.InverseTransformPoint(edgeWorldPosition);
        }

        public void AssignNewStableId()
        {
            doorId = LevelDesignAuthoringId.New("door");
        }

        public void SnapToPlacement()
        {
            transform.localPosition = ResolveTargetLocalPosition();
        }

        public void CaptureCurrentPositionAsFixedPlacement()
        {
            placementMode = LevelDoorPlacementMode.Fixed;
            fixedLocalPosition = ResolveCurrentRoomRelativePosition();
            fixedPositionSpaceVersion = OwningRoomFixedPositionSpaceVersion;
        }

        public void CaptureCurrentFixedPosition()
        {
            if (placementMode == LevelDoorPlacementMode.Fixed)
            {
                fixedLocalPosition = ResolveCurrentRoomRelativePosition();
                fixedPositionSpaceVersion = OwningRoomFixedPositionSpaceVersion;
            }
        }

        public bool MigrateFixedPositionSpaceForAuthoring()
        {
            if (placementMode != LevelDoorPlacementMode.Fixed
                || UsesOwningRoomFixedPositionSpace
                || owningRoom == null)
            {
                return false;
            }

            fixedLocalPosition = ResolveCurrentRoomRelativePosition();
            fixedPositionSpaceVersion = OwningRoomFixedPositionSpaceVersion;
            return true;
        }

        public void ConfigureAuthoring(
            string configuredDoorId,
            LevelRoom configuredOwningRoom,
            LevelDoorSide configuredSide,
            LevelDoorPlacementMode configuredPlacementMode,
            float configuredEdgeOffset,
            Vector2 configuredFixedLocalPosition,
            bool configuredTraversable,
            bool configuredAutoFaceConnection = true)
        {
            doorId = configuredDoorId;
            owningRoom = configuredOwningRoom;
            side = configuredSide;
            placementMode = configuredPlacementMode;
            edgeOffset = configuredEdgeOffset;
            fixedLocalPosition = configuredFixedLocalPosition;
            fixedPositionSpaceVersion = OwningRoomFixedPositionSpaceVersion;
            traversable = configuredTraversable;
            autoFaceConnection = configuredAutoFaceConnection;
        }

        public void SetEdgeSideForAuthoring(LevelDoorSide configuredSide)
        {
            if (placementMode != LevelDoorPlacementMode.EdgeManaged)
            {
                return;
            }

            side = configuredSide;
            SnapToPlacement();
        }

        public void SetAutoFaceConnectionForAuthoring(bool enabled)
        {
            autoFaceConnection = enabled;
        }

        public void ConfigureForTests(
            string configuredDoorId,
            LevelRoom configuredOwningRoom,
            LevelDoorSide configuredSide,
            LevelDoorPlacementMode configuredPlacementMode,
            float configuredEdgeOffset,
            Vector2 configuredFixedLocalPosition,
            bool configuredTraversable)
        {
            ConfigureAuthoring(
                configuredDoorId,
                configuredOwningRoom,
                configuredSide,
                configuredPlacementMode,
                configuredEdgeOffset,
                configuredFixedLocalPosition,
                configuredTraversable);
        }

        public void ConfigureLegacyFixedPositionForTests(
            Vector2 legacyParentRelativePosition)
        {
            placementMode = LevelDoorPlacementMode.Fixed;
            fixedLocalPosition = legacyParentRelativePosition;
            fixedPositionSpaceVersion = 0;
            transform.localPosition = new Vector3(
                legacyParentRelativePosition.x,
                legacyParentRelativePosition.y,
                transform.localPosition.z);
        }

        private void Reset()
        {
            owningRoom = GetComponentInParent<LevelRoom>();
            fixedLocalPosition = ResolveCurrentRoomRelativePosition();
            fixedPositionSpaceVersion = OwningRoomFixedPositionSpaceVersion;
        }

        private void OnValidate()
        {
            edgeOffset = Mathf.Clamp01(edgeOffset);
            if (owningRoom == null)
            {
                owningRoom = GetComponentInParent<LevelRoom>();
            }
        }

        private Vector2 ResolveCurrentRoomRelativePosition()
        {
            if (owningRoom == null)
            {
                return transform.localPosition;
            }

            Vector3 roomRelative =
                owningRoom.transform.InverseTransformPoint(transform.position);
            return new Vector2(roomRelative.x, roomRelative.y);
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
