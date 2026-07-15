using System;
using System.Collections.Generic;
using ShooterMover.ContentPackages.Weapons.Shared.Presentation;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Weapons.RicochetGun.Runtime
{
    public enum RicochetProjectile2DCompletionReason
    {
        None = 0,
        ConfirmedHit = 1,
        CollisionWithoutConfirmedTarget = 2,
        ThirdWallCollision = 3,
        LifetimeExpired = 4,
        Cancelled = 5,
    }

    /// <summary>
    /// Finite Physics2D projectile with package-owned two-bounce policy. Only an
    /// explicit RicochetWall2D marker may reflect it; every non-wall contact terminates.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class RicochetProjectile2D : MonoBehaviour
    {
        private const float CollisionSeparationDistance = 0.002f;

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
        private RicochetProjectilePolicy policy;
        private float speed;
        private Vector2 travelDirection;
        private float presentationLifetimeSeconds;
        private bool presentationEnabled;
        private bool isInitialized;
        private bool isComplete;
        private CombatHit2DTranslationResult lastHitTranslation;
        private RicochetProjectile2DCompletionReason completionReason;

        public event Action<RicochetProjectile2D> Completed;

        public bool IsInitialized
        {
            get { return isInitialized; }
        }

        public bool IsComplete
        {
            get { return isComplete; }
        }

        public int WallBounceCount
        {
            get { return policy == null ? 0 : policy.WallBounceCount; }
        }

        public float RemainingLifetimeSeconds
        {
            get { return policy == null ? 0f : (float)policy.RemainingLifetimeSeconds; }
        }

        public RicochetProjectile2DCompletionReason CompletionReason
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
            float configuredSpeed,
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
                || !IsValidSpeed(configuredSpeed)
                || !RicochetProjectilePolicy.IsValidLifetime(lifetimeSeconds)
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
            policy = new RicochetProjectilePolicy(lifetimeSeconds);
            speed = configuredSpeed;
            travelDirection = direction.normalized;
            presentationEnabled = enablePresentation;
            presentationLifetimeSeconds = configuredPresentationLifetimeSeconds;
            completionReason = RicochetProjectile2DCompletionReason.None;
            lastHitTranslation = null;
            isComplete = false;
            isInitialized = true;

            body.simulated = true;
            body.position = worldPosition;
            body.rotation = 0f;
            body.angularVelocity = 0f;
            body.linearVelocity = travelDirection * speed;
            projectileCollider.radius = radius;
            projectileCollider.isTrigger = false;
            projectileCollider.enabled = true;
            ApplyOwnerCollisionIgnore(true);
            return true;
        }

        public void Cancel()
        {
            if (!isInitialized || isComplete)
            {
                return;
            }

            policy.Terminate(RicochetProjectileTerminationReason.Cancelled);
            Complete(RicochetProjectile2DCompletionReason.Cancelled, null);
        }

        public static bool IsValidSpeed(float value)
        {
            return IsFinite(value)
                && value >= RicochetGunPackage.MinimumProjectileSpeed
                && value <= RicochetGunPackage.MaximumProjectileSpeed;
        }

        public static bool IsValidRadius(float value)
        {
            return IsFinite(value)
                && value >= RicochetGunPackage.MinimumProjectileRadius
                && value <= RicochetGunPackage.MaximumProjectileRadius;
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void FixedUpdate()
        {
            if (!isInitialized || isComplete)
            {
                return;
            }

            if (!policy.AdvanceLifetime(Time.fixedDeltaTime))
            {
                Complete(RicochetProjectile2DCompletionReason.LifetimeExpired, null);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            Vector2 contactPoint = body == null
                ? (Vector2)transform.position
                : body.position;
            Vector2[] normals = new Vector2[Math.Max(1, collision.contactCount)];
            if (collision.contactCount == 0)
            {
                normals[0] = -CurrentDirection();
            }
            else
            {
                for (int index = 0; index < collision.contactCount; index++)
                {
                    ContactPoint2D contact = collision.GetContact(index);
                    contactPoint = contact.point;
                    normals[index] = OrientAgainstTravel(contact.normal);
                }
            }

            ProcessCollision(collision.collider, normals, contactPoint);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            Vector2 contactPoint = body == null
                ? (Vector2)transform.position
                : body.position;
            if (body != null)
            {
                contactPoint = other.ClosestPoint(body.position);
            }

            ProcessCollision(other, new Vector2[0], contactPoint);
        }

        private void OnDisable()
        {
            if (isInitialized && !isComplete)
            {
                Cancel();
            }
        }

        private void OnDestroy()
        {
            ApplyOwnerCollisionIgnore(false);
            ignoredOwnerColliders.Clear();
            Completed = null;
        }

        private void ProcessCollision(
            Collider2D other,
            Vector2[] contactNormals,
            Vector2 contactPoint)
        {
            if (!isInitialized
                || isComplete
                || other == null
                || other == projectileCollider
                || IsIgnoredOwnerCollider(other))
            {
                return;
            }

            RicochetWall2D wall = other.GetComponent<RicochetWall2D>();
            if (wall != null && wall.IsValidWallCollider(other))
            {
                ResolveWallContact(contactNormals);
                return;
            }

            CompleteNonWallCollision(other, contactPoint);
        }

        private void ResolveWallContact(Vector2[] contactNormals)
        {
            List<RicochetVector2> normals = new List<RicochetVector2>();
            if (contactNormals != null)
            {
                for (int index = 0; index < contactNormals.Length; index++)
                {
                    Vector2 normal = contactNormals[index];
                    if (!IsFinite(normal.x)
                        || !IsFinite(normal.y)
                        || normal.sqrMagnitude <= 0f)
                    {
                        continue;
                    }

                    normals.Add(new RicochetVector2(normal.x, normal.y));
                }
            }

            Vector2 incoming = CurrentDirection();
            RicochetWallContactResult result = policy.ResolveWallContact(
                new RicochetVector2(incoming.x, incoming.y),
                normals);

            if (result.Kind == RicochetWallContactResultKind.Reflected)
            {
                Vector2 reflected = new Vector2(
                    (float)result.Direction.X,
                    (float)result.Direction.Y).normalized;
                travelDirection = reflected;
                body.linearVelocity = reflected * speed;
                body.position += reflected * CollisionSeparationDistance;
                return;
            }

            if (result.Kind == RicochetWallContactResultKind.GrazingIgnored)
            {
                Vector2 direction = CurrentDirection();
                body.linearVelocity = direction * speed;
                body.position += direction * CollisionSeparationDistance;
                return;
            }

            Complete(RicochetProjectile2DCompletionReason.ThirdWallCollision, null);
        }

        private void CompleteNonWallCollision(Collider2D other, Vector2 contactPoint)
        {
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

            if (policy != null && !policy.IsTerminated)
            {
                policy.Terminate(
                    confirmed
                        ? RicochetProjectileTerminationReason.ConfirmedTargetHit
                        : RicochetProjectileTerminationReason.CollisionWithoutConfirmedTarget);
            }

            Complete(
                confirmed
                    ? RicochetProjectile2DCompletionReason.ConfirmedHit
                    : RicochetProjectile2DCompletionReason.CollisionWithoutConfirmedTarget,
                translation);
        }

        private void Complete(
            RicochetProjectile2DCompletionReason reason,
            CombatHit2DTranslationResult translation)
        {
            if (isComplete)
            {
                return;
            }

            isComplete = true;
            completionReason = reason;
            lastHitTranslation = translation;

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

            Action<RicochetProjectile2D> completed = Completed;
            Completed = null;
            if (completed != null)
            {
                try
                {
                    completed(this);
                }
                catch (Exception)
                {
                    // Optional observers cannot keep a projectile alive.
                }
            }

            Destroy(gameObject);
        }

        private Vector2 CurrentDirection()
        {
            if (travelDirection.sqrMagnitude > 0f)
            {
                return travelDirection.normalized;
            }

            if (body != null && body.linearVelocity.sqrMagnitude > 0f)
            {
                return body.linearVelocity.normalized;
            }

            return Vector2.right;
        }

        private Vector2 OrientAgainstTravel(Vector2 normal)
        {
            Vector2 direction = CurrentDirection();
            return Vector2.Dot(direction, normal) > 0f ? -normal : normal;
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
                temporaryHitPresentation =
                    GetComponentInChildren<TemporaryHitPresentation>(true);
            }
        }

        private void CopyOwnerColliders(IEnumerable<Collider2D> ownerColliders)
        {
            if (ownerColliders == null)
            {
                return;
            }

            HashSet<int> ids = new HashSet<int>();
            foreach (Collider2D ownerCollider in ownerColliders)
            {
                if (ownerCollider == null
                    || ownerCollider == projectileCollider
                    || !ids.Add(ownerCollider.GetInstanceID()))
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
