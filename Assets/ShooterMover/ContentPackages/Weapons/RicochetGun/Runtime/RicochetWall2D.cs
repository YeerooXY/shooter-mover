using UnityEngine;

namespace ShooterMover.ContentPackages.Weapons.RicochetGun.Runtime
{
    /// <summary>
    /// Explicit package-owned marker for a collider that may reflect Ricochet Gun
    /// projectiles. Triggers, disabled colliders, and unmarked Collider2D objects are
    /// never treated as valid walls.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RicochetWall2D : MonoBehaviour
    {
        [SerializeField]
        private Collider2D wallCollider;

        public Collider2D WallCollider
        {
            get
            {
                CacheCollider();
                return wallCollider;
            }
        }

        public bool IsValidWallCollider(Collider2D candidate)
        {
            CacheCollider();
            return isActiveAndEnabled
                && wallCollider != null
                && candidate == wallCollider
                && wallCollider.enabled
                && !wallCollider.isTrigger;
        }

        public bool TryConfigure(Collider2D configuredWallCollider)
        {
            if (configuredWallCollider == null
                || configuredWallCollider.gameObject != gameObject
                || configuredWallCollider.isTrigger)
            {
                return false;
            }

            wallCollider = configuredWallCollider;
            return true;
        }

        private void Awake()
        {
            CacheCollider();
        }

        private void OnValidate()
        {
            CacheCollider();
            if (wallCollider != null && wallCollider.isTrigger)
            {
                wallCollider = null;
            }
        }

        private void CacheCollider()
        {
            if (wallCollider == null)
            {
                wallCollider = GetComponent<Collider2D>();
            }
        }
    }
}
