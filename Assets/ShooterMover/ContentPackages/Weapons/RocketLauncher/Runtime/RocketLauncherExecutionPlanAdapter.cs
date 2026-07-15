using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.ContentPackages.Weapons.RocketLauncher.Presentation;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Weapons.RocketLauncher.Runtime
{
    public enum RocketDetonationReason
    {
        Impact = 1,
        AuthoredExpiry = 2,
    }

    /// <summary>
    /// Closed deterministic race policy. A valid impact wins when impact and authored
    /// expiry are observed in the same simulation frame.
    /// </summary>
    public static class RocketDetonationRacePolicy
    {
        public static RocketDetonationReason Resolve(
            bool validImpactObserved,
            bool authoredExpiryObserved)
        {
            if (validImpactObserved)
            {
                return RocketDetonationReason.Impact;
            }

            if (authoredExpiryObserved)
            {
                return RocketDetonationReason.AuthoredExpiry;
            }

            throw new ArgumentException(
                "At least one valid rocket completion signal is required.");
        }
    }

    /// <summary>
    /// Explicit session-local target binding used by the bounded area query. The
    /// lifecycle owner supplies both collider and stable identity; there is no scene
    /// search, tag lookup, global registry, or enemy-state ownership.
    /// </summary>
    public sealed class RocketAreaTarget2D : IComparable<RocketAreaTarget2D>
    {
        public RocketAreaTarget2D(Collider2D collider, StableId targetId)
        {
            Collider = collider ?? throw new ArgumentNullException(nameof(collider));
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
        }

        public Collider2D Collider { get; }

        public StableId TargetId { get; }

        public int CompareTo(RocketAreaTarget2D other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int idComparison = TargetId.CompareTo(other.TargetId);
            if (idComparison != 0)
            {
                return idComparison;
            }

            return Collider.GetInstanceID().CompareTo(other.Collider.GetInstanceID());
        }
    }

    public sealed class RocketDetonationTargetResult
    {
        internal RocketDetonationTargetResult(
            StableId targetId,
            double distanceSquared,
            CombatHit2DTranslationResult translation)
        {
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
            DistanceSquared = distanceSquared;
            Translation =
                translation ?? throw new ArgumentNullException(nameof(translation));
        }

        public StableId TargetId { get; }

        public double DistanceSquared { get; }

        public CombatHit2DTranslationResult Translation { get; }
    }

    /// <summary>
    /// Immutable evidence snapshot for one and only one bounded detonation.
    /// </summary>
    public sealed class RocketDetonationResult
    {
        private readonly ReadOnlyCollection<RocketDetonationTargetResult> targets;

        internal RocketDetonationResult(
            StableId operationId,
            RocketDetonationReason reason,
            Vector2 center,
            double damage,
            double radius,
            IList<RocketDetonationTargetResult> targets)
        {
            OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
            Reason = reason;
            Center = center;
            Damage = damage;
            Radius = radius;
            this.targets = new ReadOnlyCollection<RocketDetonationTargetResult>(
                new List<RocketDetonationTargetResult>(
                    targets ?? throw new ArgumentNullException(nameof(targets))));
        }

        public StableId OperationId { get; }

        public RocketDetonationReason Reason { get; }

        public Vector2 Center { get; }

        /// <summary>
        /// Authored damage intent carried for the later damage authority. This adapter
        /// only emits CS-004 confirmed-hit facts.
        /// </summary>
        public double Damage { get; }

        public double Radius { get; }

        public IReadOnlyList<RocketDetonationTargetResult> Targets
        {
            get { return targets; }
        }
    }

    /// <summary>
    /// Package-owned lifetime/detonation gate attached beside the shared WP-002
    /// projectile shell. It resolves impact/expiry exactly once and lets impact win a
    /// same-frame race. Cancellation and session reset never detonate.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoundedProjectile2D))]
    public sealed class RocketProjectileDetonationDriver2D : MonoBehaviour
    {
        [SerializeField]
        private BoundedProjectile2D projectile;

        [SerializeField]
        private RocketImpactWarning2D impactWarning;

        private float remainingLifetimeSeconds;
        private bool expiryPending;
        private bool isInitialized;
        private bool isResolved;
        private int detonationRequestCount;

        public event Action<RocketProjectileDetonationDriver2D, RocketDetonationReason, Vector2>
            DetonationRequested;

        public BoundedProjectile2D Projectile
        {
            get
            {
                CacheComponents();
                return projectile;
            }
        }

        public RocketImpactWarning2D ImpactWarning
        {
            get
            {
                CacheComponents();
                return impactWarning;
            }
        }

        public bool IsInitialized
        {
            get { return isInitialized; }
        }

        public bool IsResolved
        {
            get { return isResolved; }
        }

        public int DetonationRequestCount
        {
            get { return detonationRequestCount; }
        }

        public float RemainingLifetimeSeconds
        {
            get { return remainingLifetimeSeconds; }
        }

        public bool TryInitialize(
            float authoredLifetimeSeconds,
            float warningRadius,
            bool enableWarning,
            float warningLifetimeSeconds)
        {
            CacheComponents();
            if (isInitialized
                || projectile == null
                || !BoundedProjectile2D.IsValidLifetime(authoredLifetimeSeconds)
                || (enableWarning
                    && (impactWarning == null
                        || !RocketImpactWarning2D.IsValidRadius(warningRadius)
                        || !RocketImpactWarning2D.IsValidLifetime(
                            warningLifetimeSeconds))))
            {
                return false;
            }

            remainingLifetimeSeconds = authoredLifetimeSeconds;
            expiryPending = false;
            isResolved = false;
            detonationRequestCount = 0;
            isInitialized = true;
            projectile.Completed += HandleProjectileCompleted;

            if (enableWarning)
            {
                impactWarning.TryArm(warningRadius, warningLifetimeSeconds);
            }

            return true;
        }

        /// <summary>
        /// Marks authored expiry for deterministic resolution in LateUpdate. A collision
        /// observed before that phase wins the race.
        /// </summary>
        public void RequestAuthoredExpiry()
        {
            if (!isInitialized || isResolved)
            {
                return;
            }

            expiryPending = true;
            remainingLifetimeSeconds = 0f;
        }

        public void Cancel()
        {
            if (!isInitialized)
            {
                return;
            }

            Unsubscribe();
            isInitialized = false;
            expiryPending = false;
            remainingLifetimeSeconds = 0f;
            DetonationRequested = null;
            if (impactWarning != null)
            {
                impactWarning.Cancel();
            }

            if (projectile != null && !projectile.IsComplete)
            {
                projectile.Cancel();
            }
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void Update()
        {
            if (!isInitialized || isResolved || expiryPending)
            {
                return;
            }

            remainingLifetimeSeconds -= Time.deltaTime;
            if (remainingLifetimeSeconds <= 0f)
            {
                RequestAuthoredExpiry();
            }
        }

        private void LateUpdate()
        {
            if (expiryPending && !isResolved)
            {
                Resolve(false, true);
            }
        }

        private void OnDisable()
        {
            if (isInitialized && !isResolved)
            {
                Cancel();
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            DetonationRequested = null;
        }

        private void HandleProjectileCompleted(BoundedProjectile2D completedProjectile)
        {
            if (!isInitialized
                || isResolved
                || completedProjectile == null
                || completedProjectile != projectile)
            {
                return;
            }

            switch (completedProjectile.CompletionReason)
            {
                case BoundedProjectile2DCompletionReason.ConfirmedHit:
                case BoundedProjectile2DCompletionReason.CollisionWithoutConfirmedTarget:
                    Resolve(true, expiryPending);
                    break;

                case BoundedProjectile2DCompletionReason.LifetimeExpired:
                    Resolve(false, true);
                    break;

                case BoundedProjectile2DCompletionReason.Cancelled:
                    Cancel();
                    break;
            }
        }

        private void Resolve(bool validImpactObserved, bool authoredExpiryObserved)
        {
            if (!isInitialized || isResolved)
            {
                return;
            }

            RocketDetonationReason reason = RocketDetonationRacePolicy.Resolve(
                validImpactObserved,
                authoredExpiryObserved);
            isResolved = true;
            expiryPending = false;
            remainingLifetimeSeconds = 0f;
            detonationRequestCount++;
            Unsubscribe();

            Action<RocketProjectileDetonationDriver2D, RocketDetonationReason, Vector2>
                requested = DetonationRequested;
            DetonationRequested = null;
            if (requested != null)
            {
                try
                {
                    requested(this, reason, transform.position);
                }
                catch (Exception)
                {
                    // Observers cannot cause a second detonation or keep the rocket alive.
                }
            }

            if (impactWarning != null)
            {
                impactWarning.Cancel();
            }

            if (projectile != null && !projectile.IsComplete)
            {
                projectile.Cancel();
            }
        }

        private void CacheComponents()
        {
            if (projectile == null)
            {
                projectile = GetComponent<BoundedProjectile2D>();
            }

            if (impactWarning == null)
            {
                impactWarning = GetComponentInChildren<RocketImpactWarning2D>(true);
            }
        }

        private void Unsubscribe()
        {
            if (projectile != null)
            {
                projectile.Completed -= HandleProjectileCompleted;
            }
        }
    }

    /// <summary>
    /// Explicit WP-005 handler behind WeaponMount2DAdapter. It spawns one paced rocket,
    /// performs one bounded stable-ID-ordered area query, and emits confirmed hit facts
    /// through the accepted CombatHit2DAdapter.
    /// </summary>
    public sealed class RocketLauncherExecutionPlanAdapter :
        IWeaponFireExecutionOperation2DHandler,
        IDisposable
    {
        public const float MaximumAreaRadius = 10f;
        public const int MaximumAreaTargetCount = 64;
        public const float BoundaryEpsilon = 0.00001f;

        private const string ContactEventNamespace = "rocket-contact";
        private const string AreaHitEventNamespace = "rocket-area-hit";

        private sealed class ActiveRocket
        {
            public ActiveRocket(
                RocketProjectileDetonationDriver2D driver,
                RocketLauncherExecutionOperation operation)
            {
                Driver = driver;
                Operation = operation;
            }

            public RocketProjectileDetonationDriver2D Driver { get; }

            public RocketLauncherExecutionOperation Operation { get; }
        }

        private readonly RocketProjectileDetonationDriver2D rocketPrefab;
        private readonly CombatHit2DAdapter hitAdapter;
        private readonly RocketAreaTarget2D[] targets;
        private readonly Collider2D[] ownerColliders;
        private readonly HashSet<int> ownerColliderInstanceIds;
        private readonly Transform projectileParent;
        private readonly bool enableImpactWarning;
        private readonly float impactWarningLifetimeSeconds;
        private readonly List<ActiveRocket> activeRockets = new List<ActiveRocket>();

        private bool isDisposed;
        private int detonationCount;
        private RocketProjectileDetonationDriver2D lastSpawnedRocket;
        private RocketDetonationResult lastDetonationResult;

        public RocketLauncherExecutionPlanAdapter(
            StableId operationKindId,
            RocketProjectileDetonationDriver2D rocketPrefab,
            CombatHit2DAdapter hitAdapter,
            IEnumerable<RocketAreaTarget2D> targets,
            IEnumerable<Collider2D> ownerColliders,
            Transform projectileParent,
            bool enableImpactWarning,
            float impactWarningLifetimeSeconds)
        {
            OperationKindId =
                operationKindId ?? throw new ArgumentNullException(nameof(operationKindId));
            this.rocketPrefab =
                rocketPrefab ?? throw new ArgumentNullException(nameof(rocketPrefab));
            this.hitAdapter =
                hitAdapter ?? throw new ArgumentNullException(nameof(hitAdapter));
            this.targets = CopyAndValidateTargets(targets);
            this.ownerColliders = CopyOwnerColliders(ownerColliders);
            ownerColliderInstanceIds = new HashSet<int>();
            for (int index = 0; index < this.ownerColliders.Length; index++)
            {
                ownerColliderInstanceIds.Add(this.ownerColliders[index].GetInstanceID());
            }

            this.projectileParent = projectileParent;
            this.enableImpactWarning = enableImpactWarning;
            if (enableImpactWarning
                && !RocketImpactWarning2D.IsValidLifetime(
                    impactWarningLifetimeSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(impactWarningLifetimeSeconds));
            }

            this.impactWarningLifetimeSeconds = impactWarningLifetimeSeconds;
        }

        public StableId OperationKindId { get; }

        public bool IsDisposed
        {
            get { return isDisposed; }
        }

        public int ActiveRocketCount
        {
            get
            {
                PruneDestroyedRockets();
                return activeRockets.Count;
            }
        }

        public int DetonationCount
        {
            get { return detonationCount; }
        }

        public RocketProjectileDetonationDriver2D LastSpawnedRocket
        {
            get
            {
                if (lastSpawnedRocket == null)
                {
                    lastSpawnedRocket = null;
                }

                return lastSpawnedRocket;
            }
        }

        public RocketDetonationResult LastDetonationResult
        {
            get { return lastDetonationResult; }
        }

        public bool TryExecute(
            WeaponFireExecutionOperationEntry operation,
            WeaponMount2DExecutionContext context)
        {
            if (isDisposed || !TryValidateEnvelope(operation, context))
            {
                return false;
            }

            RocketLauncherExecutionOperation rocketOperation =
                operation.Operation as RocketLauncherExecutionOperation;
            if (rocketOperation == null
                || rocketOperation.OperationKindId != operation.OperationKindId
                || rocketOperation.OperationId != operation.OperationId
                || rocketOperation.AreaRadius > MaximumAreaRadius)
            {
                return false;
            }

            float speed;
            float authoredLifetime;
            float projectileRadius;
            float warningRadius;
            if (!TryConvertFinite(rocketOperation.ProjectileSpeed, out speed)
                || !TryConvertFinite(
                    rocketOperation.ProjectileLifetimeSeconds,
                    out authoredLifetime)
                || !TryConvertFinite(rocketOperation.ProjectileRadius, out projectileRadius)
                || !TryConvertFinite(rocketOperation.AreaRadius, out warningRadius)
                || !BoundedProjectile2D.IsValidSpeed(speed)
                || !BoundedProjectile2D.IsValidLifetime(authoredLifetime)
                || !BoundedProjectile2D.IsValidRadius(projectileRadius)
                || !RocketImpactWarning2D.IsValidRadius(warningRadius)
                || rocketOperation.Damage <= 0d
                || double.IsNaN(rocketOperation.Damage)
                || double.IsInfinity(rocketOperation.Damage)
                || !Enum.IsDefined(typeof(CombatChannel), rocketOperation.Channel)
                || rocketOperation.Channel == CombatChannel.System)
            {
                return false;
            }

            StableId contactEventId = CreateDerivedEventId(
                ContactEventNamespace,
                operation.OperationId,
                null);
            RocketProjectileDetonationDriver2D instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(rocketPrefab, projectileParent);
                instance.gameObject.name = "RocketLauncherProjectile2D";
                if (!instance.gameObject.activeSelf)
                {
                    instance.gameObject.SetActive(true);
                }

                BoundedProjectile2D projectile = instance.Projectile;
                if (projectile == null)
                {
                    UnityEngine.Object.Destroy(instance.gameObject);
                    return false;
                }

                CombatHit2DAdapter contactProbe = new CombatHit2DAdapter(context.SourceId);
                float shellLifetime = Math.Min(
                    BoundedProjectile2D.MaximumLifetimeSeconds,
                    authoredLifetime + 1f);
                if (!projectile.TryInitialize(
                        contactEventId,
                        context.Origin,
                        context.Direction,
                        speed,
                        shellLifetime,
                        projectileRadius,
                        rocketOperation.Channel,
                        contactProbe,
                        ownerColliders,
                        false,
                        0f)
                    || !instance.TryInitialize(
                        authoredLifetime,
                        warningRadius,
                        enableImpactWarning,
                        impactWarningLifetimeSeconds))
                {
                    instance.Cancel();
                    UnityEngine.Object.Destroy(instance.gameObject);
                    return false;
                }

                instance.DetonationRequested += HandleDetonationRequested;
                activeRockets.Add(new ActiveRocket(instance, rocketOperation));
                lastSpawnedRocket = instance;
                return true;
            }
            catch (Exception)
            {
                if (instance != null)
                {
                    instance.DetonationRequested -= HandleDetonationRequested;
                    instance.Cancel();
                    UnityEngine.Object.Destroy(instance.gameObject);
                }

                return false;
            }
        }

        public void ResetSession()
        {
            ActiveRocket[] snapshot = activeRockets.ToArray();
            activeRockets.Clear();
            lastSpawnedRocket = null;
            lastDetonationResult = null;
            detonationCount = 0;

            for (int index = 0; index < snapshot.Length; index++)
            {
                ActiveRocket active = snapshot[index];
                if (active == null || active.Driver == null)
                {
                    continue;
                }

                active.Driver.DetonationRequested -= HandleDetonationRequested;
                active.Driver.Cancel();
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            ResetSession();
            isDisposed = true;
        }

        private void HandleDetonationRequested(
            RocketProjectileDetonationDriver2D driver,
            RocketDetonationReason reason,
            Vector2 center)
        {
            ActiveRocket active = FindActive(driver);
            if (active == null)
            {
                return;
            }

            driver.DetonationRequested -= HandleDetonationRequested;
            activeRockets.Remove(active);
            if (lastSpawnedRocket == driver)
            {
                lastSpawnedRocket = null;
            }

            List<RocketDetonationTargetResult> targetResults =
                BuildTargetResults(active.Operation, center);
            lastDetonationResult = new RocketDetonationResult(
                active.Operation.OperationId,
                reason,
                center,
                active.Operation.Damage,
                active.Operation.AreaRadius,
                targetResults);
            detonationCount++;
        }

        private List<RocketDetonationTargetResult> BuildTargetResults(
            RocketLauncherExecutionOperation operation,
            Vector2 center)
        {
            List<RocketDetonationTargetResult> results =
                new List<RocketDetonationTargetResult>();
            double radiusSquared = operation.AreaRadius * operation.AreaRadius;
            double threshold = radiusSquared + BoundaryEpsilon;

            for (int index = 0; index < targets.Length; index++)
            {
                RocketAreaTarget2D target = targets[index];
                Collider2D collider = target.Collider;
                if (collider == null
                    || !collider.enabled
                    || !collider.gameObject.activeInHierarchy
                    || ownerColliderInstanceIds.Contains(collider.GetInstanceID()))
                {
                    continue;
                }

                Vector2 closestPoint = collider.ClosestPoint(center);
                Vector2 delta = closestPoint - center;
                double distanceSquared = delta.sqrMagnitude;
                if (distanceSquared > threshold)
                {
                    continue;
                }

                StableId eventId = CreateDerivedEventId(
                    AreaHitEventNamespace,
                    operation.OperationId,
                    target.TargetId);
                CombatHit2DTranslationResult translation =
                    hitAdapter.TranslateConfirmedHit(
                        eventId,
                        collider,
                        operation.Channel,
                        false);
                results.Add(
                    new RocketDetonationTargetResult(
                        target.TargetId,
                        distanceSquared,
                        translation));
            }

            return results;
        }

        private ActiveRocket FindActive(RocketProjectileDetonationDriver2D driver)
        {
            for (int index = 0; index < activeRockets.Count; index++)
            {
                ActiveRocket active = activeRockets[index];
                if (active != null && active.Driver == driver)
                {
                    return active;
                }
            }

            return null;
        }

        private void PruneDestroyedRockets()
        {
            for (int index = activeRockets.Count - 1; index >= 0; index--)
            {
                ActiveRocket active = activeRockets[index];
                if (active == null || active.Driver == null)
                {
                    activeRockets.RemoveAt(index);
                }
            }

            if (lastSpawnedRocket == null)
            {
                lastSpawnedRocket = null;
            }
        }

        private bool TryValidateEnvelope(
            WeaponFireExecutionOperationEntry operation,
            WeaponMount2DExecutionContext context)
        {
            return operation != null
                && operation.Operation != null
                && operation.OperationKindId == OperationKindId
                && operation.OperationId != null
                && context != null
                && context.PhysicsScene.IsValid()
                && context.SourceId != null
                && context.SourceId == hitAdapter.SourceId
                && context.CombatEventId != null
                && context.WeaponId == RocketLauncherPackage.WeaponId
                && context.MountId != null
                && context.PlanId != null
                && context.PlanOperationIndex >= 0
                && IsFinite(context.Origin.x)
                && IsFinite(context.Origin.y)
                && IsFinite(context.Direction.x)
                && IsFinite(context.Direction.y)
                && context.Direction.sqrMagnitude > 0f;
        }

        private static RocketAreaTarget2D[] CopyAndValidateTargets(
            IEnumerable<RocketAreaTarget2D> source)
        {
            if (source == null)
            {
                return new RocketAreaTarget2D[0];
            }

            List<RocketAreaTarget2D> copy = new List<RocketAreaTarget2D>();
            HashSet<int> colliderIds = new HashSet<int>();
            HashSet<StableId> targetIds = new HashSet<StableId>();
            foreach (RocketAreaTarget2D target in source)
            {
                if (target == null)
                {
                    throw new ArgumentException(
                        "Rocket area targets cannot contain null.",
                        nameof(source));
                }

                if (!colliderIds.Add(target.Collider.GetInstanceID()))
                {
                    throw new ArgumentException(
                        "A collider cannot appear twice in one rocket target set.",
                        nameof(source));
                }

                if (!targetIds.Add(target.TargetId))
                {
                    throw new ArgumentException(
                        "A target identity cannot appear twice in one rocket target set.",
                        nameof(source));
                }

                copy.Add(target);
            }

            if (copy.Count > MaximumAreaTargetCount)
            {
                throw new ArgumentException(
                    "Rocket area target count exceeds the bounded Stage 1 limit.",
                    nameof(source));
            }

            copy.Sort();
            return copy.ToArray();
        }

        private static Collider2D[] CopyOwnerColliders(
            IEnumerable<Collider2D> source)
        {
            if (source == null)
            {
                return new Collider2D[0];
            }

            List<Collider2D> copy = new List<Collider2D>();
            HashSet<int> ids = new HashSet<int>();
            foreach (Collider2D collider in source)
            {
                if (collider == null || !ids.Add(collider.GetInstanceID()))
                {
                    continue;
                }

                copy.Add(collider);
            }

            return copy.ToArray();
        }

        private static StableId CreateDerivedEventId(
            string eventNamespace,
            StableId operationId,
            StableId targetId)
        {
            string canonical = operationId
                + "\ntarget="
                + (targetId == null ? "contact" : targetId.ToString());
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                StringBuilder builder = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                {
                    builder.Append(
                        digest[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return StableId.Create(eventNamespace, builder.ToString());
            }
        }

        private static bool TryConvertFinite(double value, out float converted)
        {
            converted = (float)value;
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && IsFinite(converted);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
