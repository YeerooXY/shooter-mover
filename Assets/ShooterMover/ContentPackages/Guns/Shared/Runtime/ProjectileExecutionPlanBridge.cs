using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.ContentPackages.Guns.Shared.Presentation;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Guns.Shared.Runtime
{
    /// <summary>
    /// Immutable package-neutral projectile intent emitted by a package-owned CB-004
    /// behavior module. It carries only bounded geometry/presentation inputs and no
    /// damage amount or target-selection authority.
    /// </summary>
    public sealed class BoundedProjectileExecutionOperation : IGunFireExecutionOperation
    {
        public BoundedProjectileExecutionOperation(
            StableId operationKindId,
            StableId operationId,
            double projectileSpeed,
            double projectileLifetimeSeconds,
            double projectileRadius,
            CombatChannel channel)
        {
            if (operationKindId == null)
            {
                throw new ArgumentNullException(nameof(operationKindId));
            }

            if (operationId == null)
            {
                throw new ArgumentNullException(nameof(operationId));
            }

            OperationKindId = operationKindId;
            OperationId = operationId;
            ProjectileSpeed = projectileSpeed;
            ProjectileLifetimeSeconds = projectileLifetimeSeconds;
            ProjectileRadius = projectileRadius;
            Channel = channel;
        }

        public StableId OperationKindId { get; }

        public StableId OperationId { get; }

        public double ProjectileSpeed { get; }

        public double ProjectileLifetimeSeconds { get; }

        public double ProjectileRadius { get; }

        public CombatChannel Channel { get; }
    }

    /// <summary>
    /// Immutable package-neutral emission fact. The original combat event retains
    /// lifecycle/operation context while HitEventId identifies the physical collision.
    /// </summary>
    public sealed class ProjectileEmission
    {
        public ProjectileEmission(
            BoundedProjectile projectile,
            StableId combatEventId,
            StableId hitEventId)
        {
            Projectile = projectile
                ?? throw new ArgumentNullException(nameof(projectile));
            CombatEventId = combatEventId
                ?? throw new ArgumentNullException(nameof(combatEventId));
            HitEventId = hitEventId
                ?? throw new ArgumentNullException(nameof(hitEventId));
        }

        public BoundedProjectile Projectile { get; }

        public StableId CombatEventId { get; }

        public StableId HitEventId { get; }
    }

    /// <summary>
    /// Explicit handler registered behind GunMount for one package-owned
    /// operation kind. The mount adapter is the only producer of its execution context,
    /// so projectile spawning cannot bypass a validated GunFireExecutionPlan.
    /// </summary>
    public sealed class ProjectileExecutionPlanBridge :
        IGunFireExecutionHandler,
        IDisposable
    {
        private const string HitEventNamespace = "projectile-hit";

        private readonly BoundedProjectile projectilePrefab;
        private readonly HitResolver hitAdapter;
        private readonly Collider2D[] ownerColliders;
        private readonly Transform projectileParent;
        private readonly bool enableHitPresentation;
        private readonly float hitPresentationLifetimeSeconds;
        private readonly List<BoundedProjectile> activeProjectiles =
            new List<BoundedProjectile>();

        private bool isDisposed;
        private BoundedProjectile lastSpawnedProjectile;

        public event Action<ProjectileEmission> ProjectileSpawned;

        public ProjectileExecutionPlanBridge(
            StableId operationKindId,
            BoundedProjectile projectilePrefab,
            HitResolver hitAdapter)
            : this(
                operationKindId,
                projectilePrefab,
                hitAdapter,
                null,
                null,
                true,
                TemporaryHitPresentation.DefaultLifetimeSeconds)
        {
        }

        public ProjectileExecutionPlanBridge(
            StableId operationKindId,
            BoundedProjectile projectilePrefab,
            HitResolver hitAdapter,
            IEnumerable<Collider2D> ownerColliders,
            Transform projectileParent,
            bool enableHitPresentation,
            float hitPresentationLifetimeSeconds)
        {
            if (operationKindId == null)
            {
                throw new ArgumentNullException(nameof(operationKindId));
            }

            if (projectilePrefab == null)
            {
                throw new ArgumentNullException(nameof(projectilePrefab));
            }

            if (hitAdapter == null)
            {
                throw new ArgumentNullException(nameof(hitAdapter));
            }

            if (enableHitPresentation
                && !TemporaryHitPresentation.IsValidLifetime(
                    hitPresentationLifetimeSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(hitPresentationLifetimeSeconds));
            }

            OperationKindId = operationKindId;
            this.projectilePrefab = projectilePrefab;
            this.hitAdapter = hitAdapter;
            this.ownerColliders = CopyOwnerColliders(ownerColliders);
            this.projectileParent = projectileParent;
            this.enableHitPresentation = enableHitPresentation;
            this.hitPresentationLifetimeSeconds = hitPresentationLifetimeSeconds;
        }

        public StableId OperationKindId { get; }

        public bool IsDisposed
        {
            get { return isDisposed; }
        }

        public int ActiveProjectileCount
        {
            get
            {
                PruneDestroyedProjectiles();
                return activeProjectiles.Count;
            }
        }

        public BoundedProjectile LastSpawnedProjectile
        {
            get
            {
                if (lastSpawnedProjectile == null)
                {
                    lastSpawnedProjectile = null;
                }

                return lastSpawnedProjectile;
            }
        }

        public bool TryExecute(
            GunFireExecutionOperationEntry operation,
            GunMountExecutionContext context)
        {
            if (isDisposed
                || !TryValidateEnvelope(operation, context))
            {
                return false;
            }

            BoundedProjectileExecutionOperation projectileOperation =
                operation.Operation as BoundedProjectileExecutionOperation;
            if (projectileOperation == null
                || projectileOperation.OperationKindId != operation.OperationKindId
                || projectileOperation.OperationId != operation.OperationId)
            {
                return false;
            }

            float speed;
            float lifetimeSeconds;
            float radius;
            if (!TryConvertFinite(projectileOperation.ProjectileSpeed, out speed)
                || !TryConvertFinite(
                    projectileOperation.ProjectileLifetimeSeconds,
                    out lifetimeSeconds)
                || !TryConvertFinite(projectileOperation.ProjectileRadius, out radius)
                || !BoundedProjectile.IsValidSpeed(speed)
                || !BoundedProjectile.IsValidLifetime(lifetimeSeconds)
                || !BoundedProjectile.IsValidRadius(radius)
                || !Enum.IsDefined(typeof(CombatChannel), projectileOperation.Channel)
                || projectileOperation.Channel == CombatChannel.System)
            {
                return false;
            }

            StableId hitEventId;
            if (!TryCreateHitEventId(context, out hitEventId))
            {
                return false;
            }

            BoundedProjectile instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(projectilePrefab, projectileParent);
                instance.gameObject.name = "BoundedProjectile";
                if (!instance.gameObject.activeSelf)
                {
                    instance.gameObject.SetActive(true);
                }

                instance.Completed += HandleProjectileCompleted;
                if (!instance.TryInitialize(
                    hitEventId,
                    context.Origin,
                    context.Direction,
                    speed,
                    lifetimeSeconds,
                    radius,
                    projectileOperation.Channel,
                    hitAdapter,
                    ownerColliders,
                    enableHitPresentation,
                    hitPresentationLifetimeSeconds))
                {
                    instance.Completed -= HandleProjectileCompleted;
                    UnityEngine.Object.Destroy(instance.gameObject);
                    return false;
                }

                activeProjectiles.Add(instance);
                lastSpawnedProjectile = instance;
                PublishProjectileSpawned(
                    new ProjectileEmission(
                        instance,
                        context.CombatEventId,
                        hitEventId));
                return true;
            }
            catch (Exception)
            {
                if (instance != null)
                {
                    instance.Completed -= HandleProjectileCompleted;
                    UnityEngine.Object.Destroy(instance.gameObject);
                }

                return false;
            }
        }

        public void ResetSession()
        {
            BoundedProjectile[] snapshot = activeProjectiles.ToArray();
            activeProjectiles.Clear();
            lastSpawnedProjectile = null;

            for (int index = 0; index < snapshot.Length; index++)
            {
                BoundedProjectile projectile = snapshot[index];
                if (projectile == null)
                {
                    continue;
                }

                projectile.Completed -= HandleProjectileCompleted;
                projectile.Cancel();
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            ResetSession();
            ProjectileSpawned = null;
            isDisposed = true;
        }

        private void PublishProjectileSpawned(
            ProjectileEmission emission)
        {
            Action<ProjectileEmission> handler = ProjectileSpawned;
            if (handler == null)
            {
                return;
            }
            try
            {
                handler(emission);
            }
            catch (Exception)
            {
                // Emission observers are optional and cannot invalidate execution.
            }
        }

        private bool TryValidateEnvelope(
            GunFireExecutionOperationEntry operation,
            GunMountExecutionContext context)
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
                && context.GunId != null
                && context.MountId != null
                && context.PlanId != null
                && context.PlanOperationIndex >= 0
                && IsFinite(context.Origin.x)
                && IsFinite(context.Origin.y)
                && IsFinite(context.Direction.x)
                && IsFinite(context.Direction.y)
                && context.Direction.sqrMagnitude > 0f;
        }

        private static bool TryCreateHitEventId(
            GunMountExecutionContext context,
            out StableId hitEventId)
        {
            hitEventId = null;
            string value = context.PlanId.Value
                + "-"
                + context.PlanOperationIndex.ToString(CultureInfo.InvariantCulture);
            if (value.Length > StableId.MaxValueLength)
            {
                return false;
            }

            try
            {
                hitEventId = StableId.Create(HitEventNamespace, value);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void HandleProjectileCompleted(BoundedProjectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            projectile.Completed -= HandleProjectileCompleted;
            activeProjectiles.Remove(projectile);
            if (lastSpawnedProjectile == projectile)
            {
                lastSpawnedProjectile = null;
            }
        }

        private void PruneDestroyedProjectiles()
        {
            for (int index = activeProjectiles.Count - 1; index >= 0; index--)
            {
                if (activeProjectiles[index] == null)
                {
                    activeProjectiles.RemoveAt(index);
                }
            }

            if (lastSpawnedProjectile == null)
            {
                lastSpawnedProjectile = null;
            }
        }

        private static Collider2D[] CopyOwnerColliders(
            IEnumerable<Collider2D> ownerColliders)
        {
            if (ownerColliders == null)
            {
                return new Collider2D[0];
            }

            List<Collider2D> copy = new List<Collider2D>();
            HashSet<int> instanceIds = new HashSet<int>();
            foreach (Collider2D ownerCollider in ownerColliders)
            {
                if (ownerCollider == null
                    || !instanceIds.Add(ownerCollider.GetInstanceID()))
                {
                    continue;
                }

                copy.Add(ownerCollider);
            }

            return copy.ToArray();
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
