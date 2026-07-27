using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [DisallowMultipleComponent]
    public sealed class LevelRoomAuthoring2D : MonoBehaviour
    {
        [Header("Stable identity")]
        [SerializeField] private string roomId = "room.unassigned";

        [Header("Optional presentation")]
        [Tooltip("Optional designer-facing label. Leave empty for an automatic coordinate label.")]
        [SerializeField] private string displayName = string.Empty;

        [Header("Grid and folder placement")]
        [SerializeField] private Vector2Int gridCoordinate = Vector2Int.zero;
        [Tooltip("Per-coordinate room slot used by Room_X_Y_01 folder naming.")]
        [SerializeField] [Min(1)] private int folderSlot = 1;
        [SerializeField] private Vector2 cellSize = Vector2.one;
        [SerializeField] private Vector2Int footprintCells = Vector2Int.one;
        [SerializeField] private LevelRoomAlignment alignment =
            LevelRoomAlignment.GridOrigin;
        [SerializeField] private Vector2 customAlignmentOffset = Vector2.zero;

        [Header("Room metadata")]
        [SerializeField] private Collider2D roomBounds;
        [SerializeField] private int sortingOrder;
        [SerializeField] private Vector2Int mapCoordinate = Vector2Int.zero;
        [SerializeField] private bool visibleOnMap = true;

        public string RoomIdText
        {
            get { return roomId; }
        }

        public string DisplayName
        {
            get
            {
                return string.IsNullOrWhiteSpace(displayName)
                    ? string.Empty
                    : displayName.Trim();
            }
        }

        public string EditorLabel
        {
            get
            {
                return string.IsNullOrWhiteSpace(displayName)
                    ? "Room " + gridCoordinate.x + "," + gridCoordinate.y
                        + " [" + folderSlot.ToString("00") + "]"
                    : displayName.Trim();
            }
        }

        public Vector2Int GridCoordinate
        {
            get { return gridCoordinate; }
        }

        public int FolderSlot
        {
            get { return folderSlot; }
        }

        public Vector2 CellSize
        {
            get { return cellSize; }
        }

        public Vector2Int FootprintCells
        {
            get { return footprintCells; }
        }

        public LevelRoomAlignment Alignment
        {
            get { return alignment; }
        }

        public Vector2 CustomAlignmentOffset
        {
            get { return customAlignmentOffset; }
        }

        public Collider2D RoomBounds
        {
            get { return roomBounds; }
        }

        public int SortingOrder
        {
            get { return sortingOrder; }
        }

        public Vector2Int MapCoordinate
        {
            get { return mapCoordinate; }
        }

        public bool VisibleOnMap
        {
            get { return visibleOnMap; }
        }

        public bool TryGetRoomId(out StableId parsed)
        {
            return StableId.TryParse(roomId, out parsed);
        }

        public Vector3 GridAlignedWorldPosition
        {
            get
            {
                Vector2 offset = alignment == LevelRoomAlignment.Centered
                    ? new Vector2(
                        footprintCells.x * cellSize.x * 0.5f,
                        footprintCells.y * cellSize.y * 0.5f)
                    : alignment == LevelRoomAlignment.Custom
                        ? customAlignmentOffset
                        : Vector2.zero;
                return new Vector3(
                    gridCoordinate.x * cellSize.x + offset.x,
                    gridCoordinate.y * cellSize.y + offset.y,
                    transform.position.z);
            }
        }

        public LevelRoomRecord BuildRecord()
        {
            Rect bounds = roomBounds == null
                ? new Rect((Vector2)transform.position, Vector2.zero)
                : RectFromBounds(roomBounds.bounds);
            return new LevelRoomRecord(
                roomId,
                gridCoordinate,
                cellSize,
                footprintCells,
                alignment,
                bounds,
                roomBounds != null,
                sortingOrder,
                mapCoordinate,
                visibleOnMap,
                BuildDiagnosticLocation());
        }

        public LevelGridRoomRecordV2 BuildGridRecord()
        {
            return new LevelGridRoomRecordV2(
                roomId,
                gridCoordinate,
                footprintCells,
                folderSlot,
                BuildDiagnosticLocation());
        }

        [ContextMenu("Assign New Stable ID")]
        public void AssignNewStableId()
        {
            roomId = LevelDesignAuthoringId.New("room");
        }

        [ContextMenu("Snap Room To Authored Grid")]
        public void SnapToAuthoredGrid()
        {
            transform.position = GridAlignedWorldPosition;
        }

        public void ConfigureForTests(
            string configuredRoomId,
            Vector2Int configuredGridCoordinate,
            Vector2 configuredCellSize,
            Vector2Int configuredFootprintCells,
            Collider2D configuredRoomBounds)
        {
            roomId = configuredRoomId;
            gridCoordinate = configuredGridCoordinate;
            folderSlot = 1;
            cellSize = configuredCellSize;
            footprintCells = configuredFootprintCells;
            roomBounds = configuredRoomBounds;
        }

        public void ConfigureDisplayNameForTests(string configuredDisplayName)
        {
            displayName = configuredDisplayName ?? string.Empty;
        }

        public void ConfigureFolderSlotForTests(int configuredFolderSlot)
        {
            folderSlot = configuredFolderSlot;
        }

        private void Reset()
        {
            roomBounds = GetComponent<Collider2D>();
        }

        private void OnValidate()
        {
            folderSlot = Mathf.Max(1, folderSlot);
            cellSize.x = Mathf.Max(0.01f, cellSize.x);
            cellSize.y = Mathf.Max(0.01f, cellSize.y);
            footprintCells.x = Mathf.Max(1, footprintCells.x);
            footprintCells.y = Mathf.Max(1, footprintCells.y);
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

        private static Rect RectFromBounds(Bounds bounds)
        {
            return new Rect(
                bounds.min.x,
                bounds.min.y,
                bounds.size.x,
                bounds.size.y);
        }
    }
}
