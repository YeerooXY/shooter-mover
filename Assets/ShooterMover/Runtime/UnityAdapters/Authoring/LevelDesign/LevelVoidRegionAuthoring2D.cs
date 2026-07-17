using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [DisallowMultipleComponent]
    public sealed class LevelVoidRegionAuthoring2D : MonoBehaviour
    {
        [Header("Stable identity")]
        [SerializeField] private string voidRegionId = "void.unassigned";
        [SerializeField] private LevelRoomAuthoring2D room;

        [Header("Explicit region")]
        [SerializeField] private Collider2D regionCollider;
        [SerializeField] private LevelVoidEffect effect =
            LevelVoidEffect.RespawnAtCheckpoint;
        [SerializeField] private LevelRestartPolicy restartPolicy =
            LevelRestartPolicy.ResetProjection;

        [Header("Editor metadata")]
        [SerializeField] private bool showWarningFill = true;

        public string VoidRegionIdText
        {
            get { return voidRegionId; }
        }

        public LevelRoomAuthoring2D Room
        {
            get { return room; }
        }

        public Collider2D RegionCollider
        {
            get { return regionCollider; }
        }

        public bool ShowWarningFill
        {
            get { return showWarningFill; }
        }

        public LevelVoidRecord BuildRecord()
        {
            Rect bounds = regionCollider == null
                ? new Rect((Vector2)transform.position, Vector2.zero)
                : RectFromBounds(regionCollider.bounds);
            return new LevelVoidRecord(
                voidRegionId,
                room == null ? null : room.RoomIdText,
                bounds,
                regionCollider != null,
                regionCollider != null && regionCollider.isTrigger,
                effect,
                restartPolicy,
                BuildDiagnosticLocation());
        }

        [ContextMenu("Assign New Stable ID")]
        public void AssignNewStableId()
        {
            voidRegionId = LevelDesignAuthoringId.New("void");
        }

        public void ConfigureForTests(
            string configuredId,
            LevelRoomAuthoring2D configuredRoom,
            Collider2D configuredCollider,
            LevelVoidEffect configuredEffect)
        {
            voidRegionId = configuredId;
            room = configuredRoom;
            regionCollider = configuredCollider;
            effect = configuredEffect;
        }

        private void Reset()
        {
            room = GetComponentInParent<LevelRoomAuthoring2D>();
            regionCollider = GetComponent<Collider2D>();
            if (regionCollider != null)
            {
                regionCollider.isTrigger = true;
            }
        }

        private void OnValidate()
        {
            if (regionCollider != null)
            {
                regionCollider.isTrigger = true;
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
