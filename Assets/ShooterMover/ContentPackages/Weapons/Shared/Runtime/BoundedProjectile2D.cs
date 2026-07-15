using System;
using System.Collections.Generic;
using ShooterMover.ContentPackages.Weapons.Shared.Presentation;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Weapons.Shared.Runtime
{
    public enum BoundedProjectile2DCompletionReason
    {
        None = 0,
        ConfirmedHit = 1,
        CollisionWithoutConfirmedTarget = 2,
        LifetimeExpired = 3,
        Cancelled = 4,
    }

    /// <summary>
    /// One finite package-neutral Physics2D projectile shell. It translates a physical
    /// contact through the accepted CombatHit2DAdapter and never applies damage or owns
    /// target state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class BoundedProjectile2D : MonoBehaviour
    {
        public const float MinimumLifetimeSeconds = 0.01f;
        public const float MaximumLifetimeSeconds = 30f;
        public const float MinimumSpeed = 0.01f;
        public const float MaximumSpeed = 250f;
        public const float MinimumRadius = 0.01f;
        public const float MaximumRadius = 10f;

        [SerializeField]
        private Rigidbody2D body;

        [SerializeField]
        private CircleCollider2D projectileCollider;

        [SerializeField]
        private TemporaryHitPresentation temporaryHitPresentation;

        private readonly List<Collider2D> ignoredOwnerColliders = new List<Collider2D>();
        private CombatHit2DAdapter hitAdapter;
        private StableId hitEventId;
        private CombatChannel channel;
        private float remainingLifetimeSeconds;
        private float presentationLifetimeSeconds;
        private bool presentationEnabled;
        private bool isInitialized;
        private bool isComplete;
        private CombatHit2DTranslationResult lastHitTranslation;
        private BoundedProjectile2DCompletionReason completionReason;

        public event Action<BoundedProjectile2D> Completed;

        public bool IsInitialized
        {
            get { return isInitialized; }
        }

        public bool IsComplete
        {
            get { return isComplete; }
        }

        public float RemainingLifetimeSeconds
        {
            get { return remainingLifetimeSeconds; }
        }

        public StableId HitEventId
        {
            get { return hitEventId; }
        }

        public BoundedProjectile2DCompletionReason CompletionReason
        {
            get { return completionReason; }
        }

        public CombatHit2DTranslationResult LastHitTranslation
        {
            get { return lastHitTranslation; }
        }

        public bool TryInitialize(
            StableId configuredHitEventId,
            Vector2 worldPosition,
            Vector2 direction,
            float speed,
            float lifetimeSeconds,
            float radius,
            CombatChannel configuredChannel,
            CombatHit2DAdapter configuredHitAdapter,
            IEnumerable<Collider2D> ownerColliders,
            bool enablePresentation,
            float configuredPresentationLifetimeSeconds)
        {
            CacheComponents();
            if (isInitialized
                || body == null
                || projectileCollider == null
                || configuredHitEventId == null
                || configuredHitAdapter == null
                || !IsFinite(worldPosition.x)
                || !IsFinite(worldPosition.y)
                || !IsFinite(direction.x)
                || !IsFinite(direction.y)
                || direction.sqrMagnitude <= 0f
                || !IsValidSpeed(speed)
                || !IsValidLifetime(lifetimeSeconds)
                || !IsValidRadius(radius)
                || !Enum.IsDefined(typeof(CombatChannel), configuredChannel)
                || configuredChannel == CombatChannel.System
                || (enablePresentation
                    && !TemporaryHitPresentation.IsValidLifetime(
                        configuredPresentationLifetimeSeconds)))
            {
                return false;
            }

            ignoredOwnerColliders.Clear();
            CopyOwnerColliders(ownerColliders);

            hitAdapter = configuredHitAdapter;
            hitEventId = configuredHitEventId;
            channel = configuredChannel;
            remainingLifetimeSeconds = lifetimeSeconds;
            presentationLifetimeSeconds = configuredPresentationLifetimeSeconds;
            presentationEnabled = enablePresentation;
            completionReason = BoundedProjectile2DCompletionReason.None;
            lastHitTranslation = null;
            isComplete = false;
            isInitialized = true;

            body.simulated = true;
            body.position = worldPosition;
            body.angularVelocity = 0f;
            projectileCollider.radius = radius;
            projectileCollider.enabled = true;
            ApplyOwnerCollisionIgnore(true);
            body.linearVelocity = direction.normalized * speed;
            return true;
        }

        public void Cancel()
        {
            if (!isInitialized || isComplete)
            {
                return;
            }

            Complete(BoundedProjectile2DCompletionReason.Cancelled, null);
        }

        public static bool IsValidLifetime(float lifetimeSeconds)
        {
            return IsFinite(lifetimeSeconds)
                && lifetimeSeconds >= MinimumLifetimeSeconds
                && lifetimeSeconds <= MaximumLifetimeSeconds;
        }

        public static bool IsValidSpeed(float speed)
        {
            return IsFinite(speed) && speed >= MinimumSpeed && speed <= MaximumSpeed;
        }

        public static bool IsValidRadius(float radius)
        {
            return IsFinite(radius) && radius >= MinimumRadius && radius <= MaximumRadius;
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void Update()
        {
            if (!isInitialized || isComplete)
            {
                return;
            }

            remainingLifetimeSeconds -= Time.deltaTime;
            if (remainingLifetimeSeconds <= 0f)
            {
                Complete(BoundedProjectile2DCompletionReason.LifetimeExpired, null);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            Vector2 point = body == null ? (Vector2)transform.position : body.position;
            if (body != null)
            {
                point = other.ClosestPoint(body.position);
            }

            TryCompleteCollision(other, point);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            Vector2 point = body == null ? (Vector2)transform.position : body.position;
            if (collision.contactCount > 0)
            {
                point = collision.GetContact(0).point;
            }

            TryCompleteCollision(collision.collider, point);
        }

        private void OnDisable()
        {
            if (isInitialized && !isComplete)
            {
                Complete(BoundedProjectile2DCompletionReason.Cancelled, null);
            }
        }

        private void OnDestroy()
        {
            ApplyOwnerCollisionIgnore(false);
            ignoredOwnerColliders.Clear();
            Completed = null;
        }

        private void TryCompleteCollision(Collider2D other, Vector2 contactPoint)
        {
            if (!isInitialized
                || isComplete
                || other == null
                || other == projectileCollider
                || IsIgnoredOwnerCollider(other))
            {
                return;
            }

            CombatHit2DTranslationResult translation = hitAdapter.TranslateConfirmedHit(
                hitEventId,
                other,
                channel,
                false);
            bool confirmed = translation != null
                && translation.Status == CombatHit2DTranslationStatus.Confirmed;

            if (confirmed
                && presentationEnabled
                && temporaryHitPresentation != null)
            {
                temporaryHitPresentation.TryPlay(
                    contactPoint,
                    presentationLifetimeSeconds);
            }

            Complete(
                confirmed
                    ? BoundedProjectile2DCompletionReason.ConfirmedHit
                    : BoundedProjectile2DCompletionReason.CollisionWithoutConfirmedTarget,
                translation);
        }

        private void Complete(
            BoundedProjectile2DCompletionReason reason,
            CombatHit2DTranslationResult translation)
        {
            if (isComplete)
            {
                return;
            }

            isComplete = true;
            completionReason = reason;
            lastHitTranslation = translation;
            remainingLifetimeSeconds = 0f;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
            }

            if (projectileCollider != null)
            {
                projectileCollider.enabled = false;
            }

            ApplyOwnerCollisionIgnore(false);

            Action<BoundedProjectile2D> completed = Completed;
            Completed = null;
            if (completed != null)
            {
                try
                {
                    completed(this);
                }
                catch (Exception)
                {
                    // Completion observers are optional and cannot keep the projectile alive.
                }
            }

            Destroy(gameObject);
        }

        private void CacheComponents()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (projectileCollider == null)
            {
                projectileCollider = GetComponent<CircleCollider2D>();
            }

            if (temporaryHitPresentation == null)
            {
                temporaryHitPresentation = GetComponentInChildren<TemporaryHitPresentation>(true);
            }
        }

        private void CopyOwnerColliders(IEnumerable<Collider2D> ownerColliders)
        {
            if (ownerColliders == null)
            {
                return;
            }

            HashSet<int> instanceIds = new HashSet<int>();
            foreach (Collider2D ownerCollider in ownerColliders)
            {
                if (ownerCollider == null
                    || ownerCollider == projectileCollider
                    || !instanceIds.Add(ownerCollider.GetInstanceID()))
                {
                    continue;
                }

                ignoredOwnerColliders.Add(ownerCollider);
            }
        }

        private bool IsIgnoredOwnerCollider(Collider2D candidate)
        {
            for (int index = 0; index < ignoredOwnerColliders.Count; index++)
            {
                if (ignoredOwnerColliders[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyOwnerCollisionIgnore(bool ignore)
        {
            if (projectileCollider == null)
            {
                return;
            }

            for (int index = 0; index < ignoredOwnerColliders.Count; index++)
            {
                Collider2D ownerCollider = ignoredOwnerColliders[index];
                if (ownerCollider != null)
                {
                    Physics2D.IgnoreCollision(projectileCollider, ownerCollider, ignore);
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
