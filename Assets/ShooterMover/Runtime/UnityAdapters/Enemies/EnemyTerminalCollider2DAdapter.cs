using System;
using ShooterMover.Domain.Enemies;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    /// <summary>
    /// Generic terminal presentation adapter. It observes the existing enemy authority and
    /// projects active/destroyed lifecycle onto physical hitboxes without owning health or
    /// handling damage. SpriteRenderer and other presentation components are left untouched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyTerminalCollider2DAdapter : MonoBehaviour
    {
        [SerializeField] private Collider2D[] hitboxes = new Collider2D[0];

        private IEnemyActor2DAuthority authority;
        private bool[] enabledWhenActive = new bool[0];

        public bool IsConfigured
        {
            get
            {
                return authority != null
                    && hitboxes != null
                    && hitboxes.Length > 0
                    && enabledWhenActive.Length == hitboxes.Length;
            }
        }

        public int HitboxCount { get { return hitboxes == null ? 0 : hitboxes.Length; } }

        public void Configure(
            IEnemyActor2DAuthority actorAuthority,
            Collider2D[] physicalHitboxes)
        {
            if (actorAuthority == null) throw new ArgumentNullException(nameof(actorAuthority));
            if (physicalHitboxes == null || physicalHitboxes.Length == 0)
            {
                throw new ArgumentException(
                    "At least one enemy hitbox is required.",
                    nameof(physicalHitboxes));
            }

            if (IsConfigured)
            {
                if (ReferenceEquals(authority, actorAuthority)
                    && SameHitboxes(hitboxes, physicalHitboxes))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "EnemyTerminalCollider2DAdapter is already configured differently.");
            }

            Collider2D[] copy = new Collider2D[physicalHitboxes.Length];
            bool[] activeState = new bool[physicalHitboxes.Length];
            for (int index = 0; index < physicalHitboxes.Length; index++)
            {
                Collider2D hitbox = physicalHitboxes[index];
                if (hitbox == null || hitbox.gameObject != gameObject)
                {
                    throw new ArgumentException(
                        "All enemy hitboxes must be non-null and belong to this GameObject.",
                        nameof(physicalHitboxes));
                }

                copy[index] = hitbox;
                activeState[index] = hitbox.enabled;
            }

            authority = actorAuthority;
            hitboxes = copy;
            enabledWhenActive = activeState;
            SyncNow();
        }

        public bool SyncNow()
        {
            EnsureAutomaticConfiguration();
            if (!IsConfigured)
            {
                return false;
            }

            EnemyActorState state;
            try
            {
                if (!authority.TryReadState(out state) || state == null)
                {
                    return false;
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            bool active = state.IsActive;
            for (int index = 0; index < hitboxes.Length; index++)
            {
                Collider2D hitbox = hitboxes[index];
                if (hitbox != null)
                {
                    hitbox.enabled = active && enabledWhenActive[index];
                }
            }

            return true;
        }

        private void Awake()
        {
            EnsureAutomaticConfiguration();
            SyncNow();
        }

        private void FixedUpdate()
        {
            SyncNow();
        }

        private void EnsureAutomaticConfiguration()
        {
            if (IsConfigured)
            {
                return;
            }

            IEnemyActor2DAuthority discoveredAuthority = authority ?? FindAuthority();
            Collider2D[] discoveredHitboxes = hitboxes;
            if (discoveredHitboxes == null || discoveredHitboxes.Length == 0)
            {
                discoveredHitboxes = GetComponents<Collider2D>();
            }

            if (discoveredAuthority == null
                || discoveredHitboxes == null
                || discoveredHitboxes.Length == 0)
            {
                return;
            }

            authority = discoveredAuthority;
            hitboxes = discoveredHitboxes;
            enabledWhenActive = new bool[hitboxes.Length];
            for (int index = 0; index < hitboxes.Length; index++)
            {
                Collider2D hitbox = hitboxes[index];
                if (hitbox == null || hitbox.gameObject != gameObject)
                {
                    authority = null;
                    enabledWhenActive = new bool[0];
                    return;
                }

                enabledWhenActive[index] = hitbox.enabled;
            }
        }

        private IEnemyActor2DAuthority FindAuthority()
        {
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                IEnemyActor2DAuthority candidate = components[index] as IEnemyActor2DAuthority;
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool SameHitboxes(Collider2D[] left, Collider2D[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (!ReferenceEquals(left[index], right[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
