using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.ContentPackages.Weapons.Shared.Presentation;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Weapons.Shotgun
{
    /// <summary>
    /// Explicit package-owned 2D handler for ordered pellet operations. It reuses the
    /// WP-002 bounded projectile shell, reserves a complete spread before pellet zero,
    /// and never applies damage or owns target state.
    /// </summary>
    public sealed class ShotgunPellet2DHandler :
        IWeaponFireExecutionOperation2DHandler,
        IDisposable
    {
        private const string HitIdentityNamespace = "shotgun-hit";

        private readonly BoundedProjectile2D projectilePrefab;
        private readonly CombatHit2DAdapter hitAdapter;
        private readonly Collider2D[] ownerColliders;
        private readonly Transform projectileParent;
        private readonly bool enableHitPresentation;
        private readonly float hitPresentationLifetimeSeconds;
        private readonly List<BoundedProjectile2D> activeProjectiles =
            new List<BoundedProjectile2D>();
        private readonly Dictionary<StableId, PlanReservation> reservations =
            new Dictionary<StableId, PlanReservation>();

        private bool isDisposed;
        private BoundedProjectile2D lastSpawnedProjectile;

        public ShotgunPellet2DHandler(
            BoundedProjectile2D projectilePrefab,
            CombatHit2DAdapter hitAdapter)
            : this(
                projectilePrefab,
                hitAdapter,
                null,
                null,
                true,
                TemporaryHitPresentation.DefaultLifetimeSeconds)
        {
        }

        public ShotgunPellet2DHandler(
            BoundedProjectile2D projectilePrefab,
            CombatHit2DAdapter hitAdapter,
            IEnumerable<Collider2D> ownerColliders,
            Transform projectileParent,
            bool enableHitPresentation,
            float hitPresentationLifetimeSeconds)
        {
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

            this.projectilePrefab = projectilePrefab;
            this.hitAdapter = hitAdapter;
            this.ownerColliders = CopyOwnerColliders(ownerColliders);
            this.projectileParent = projectileParent;
            this.enableHitPresentation = enableHitPresentation;
            this.hitPresentationLifetimeSeconds = hitPresentationLifetimeSeconds;
        }

        public StableId OperationKindId
        {
            get { return ShotgunSpreadBehaviorModule.PelletOperationKindId; }
        }

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

        public int ReservedPelletCount
        {
            get
            {
                int total = 0;
                foreach (KeyValuePair<StableId, PlanReservation> pair in reservations)
                {
                    total += pair.Value.RemainingPelletCount;
                }

                return total;
            }
        }

        public BoundedProjectile2D LastSpawnedProjectile
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

        public BoundedProjectile2D[] CopyActiveProjectiles()
        {
            PruneDestroyedProjectiles();
            return activeProjectiles.ToArray();
        }

        public bool TryExecute(
            WeaponFireExecutionOperationEntry operationEntry,
            WeaponMount2DExecutionContext context)
        {
            if (isDisposed
                || !TryValidateEnvelope(operationEntry, context))
            {
                return false;
            }

            ShotgunPelletExecutionOperation operation =
                operationEntry.Operation as ShotgunPelletExecutionOperation;
            if (!IsValidOperation(operationEntry, context, operation))
            {
                return false;
            }

            if (!TryReservePellet(context.PlanId, operation))
            {
                return false;
            }

            BoundedProjectile2D instance = null;
            try
            {
                StableId hitEventId = CreateHitEventId(
                    context.PlanId,
                    operation.PelletIndex);
                instance = UnityEngine.Object.Instantiate(
                    projectilePrefab,
                    projectileParent);
                instance.gameObject.name = "ShotgunPellet2D-"
                    + operation.PelletIndex.ToString("D2", CultureInfo.InvariantCulture);
                if (!instance.gameObject.activeSelf)
                {
                    instance.gameObject.SetActive(true);
                }

                instance.Completed += HandleProjectileCompleted;
                if (!instance.TryInitialize(
                    hitEventId,
                    context.Origin,
                    new Vector2(
                        (float)operation.DirectionX,
                        (float)operation.DirectionY),
                    (float)operation.ProjectileSpeed,
                    (float)operation.ProjectileLifetimeSeconds,
                    (float)operation.ProjectileRadius,
                    operation.Channel,
                    hitAdapter,
                    ownerColliders,
                    enableHitPresentation,
                    hitPresentationLifetimeSeconds))
                {
                    instance.Completed -= HandleProjectileCompleted;
                    UnityEngine.Object.Destroy(instance.gameObject);
                    ReleaseReservation(context.PlanId);
                    return false;
                }

                activeProjectiles.Add(instance);
                lastSpawnedProjectile = instance;
                AdvanceReservation(context.PlanId);
                return true;
            }
            catch (Exception)
            {
                if (instance != null)
                {
                    instance.Completed -= HandleProjectileCompleted;
                    UnityEngine.Object.Destroy(instance.gameObject);
                }

                ReleaseReservation(context.PlanId);
                return false;
            }
        }

        public void ResetSession()
        {
            reservations.Clear();
            BoundedProjectile2D[] snapshot = activeProjectiles.ToArray();
            activeProjectiles.Clear();
            lastSpawnedProjectile = null;

            for (int index = 0; index < snapshot.Length; index++)
            {
                BoundedProjectile2D projectile = snapshot[index];
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
            isDisposed = true;
        }

        private bool TryValidateEnvelope(
            WeaponFireExecutionOperationEntry operationEntry,
            WeaponMount2DExecutionContext context)
        {
            return operationEntry != null
                && operationEntry.Operation != null
                && operationEntry.OperationKindId == OperationKindId
                && operationEntry.OperationId != null
                && context != null
                && context.PhysicsScene.IsValid()
                && context.SourceId != null
                && context.SourceId == hitAdapter.SourceId
                && context.CombatEventId != null
                && context.WeaponId == ShotgunPackageDefinition.WeaponId
                && context.MountId != null
                && context.PlanId != null
                && context.PlanOperationIndex == operationEntry.PlanOperationIndex
                && IsFinite(context.Origin.x)
                && IsFinite(context.Origin.y);
        }

        private static bool IsValidOperation(
            WeaponFireExecutionOperationEntry operationEntry,
            WeaponMount2DExecutionContext context,
            ShotgunPelletExecutionOperation operation)
        {
            if (operation == null
                || operation.OperationKindId != operationEntry.OperationKindId
                || operation.OperationId != operationEntry.OperationId
                || operation.PelletCount < ShotgunTuning.MinimumPelletCount
                || operation.PelletCount > ShotgunTuning.MaximumPelletCount
                || operation.PelletIndex < 0
                || operation.PelletIndex >= operation.PelletCount
                || operationEntry.ModuleOperationIndex != operation.PelletIndex
                || context.PlanOperationIndex != operation.PelletIndex
                || !IsFinite(operation.DirectionX)
                || !IsFinite(operation.DirectionY)
                || !IsFinite(operation.Damage)
                || operation.Damage <= 0d
                || operation.Channel != CombatChannel.Kinetic)
            {
                return false;
            }

            double directionLengthSquared =
                (operation.DirectionX * operation.DirectionX)
                + (operation.DirectionY * operation.DirectionY);
            return IsFinite(directionLengthSquared)
                && directionLengthSquared > 0d
                && BoundedProjectile2D.IsValidSpeed(
                    (float)operation.ProjectileSpeed)
                && BoundedProjectile2D.IsValidLifetime(
                    (float)operation.ProjectileLifetimeSeconds)
                && BoundedProjectile2D.IsValidRadius(
                    (float)operation.ProjectileRadius);
        }

        private bool TryReservePellet(
            StableId planId,
            ShotgunPelletExecutionOperation operation)
        {
            PruneDestroyedProjectiles();

            PlanReservation reservation;
            if (operation.PelletIndex == 0)
            {
                if (reservations.ContainsKey(planId)
                    || activeProjectiles.Count
                        + ReservedPelletCount
                        + operation.PelletCount
                        > ShotgunTuning.MaximumConcurrentPelletCount)
                {
                    return false;
                }

                reservation = new PlanReservation(operation.PelletCount);
                reservations.Add(planId, reservation);
            }
            else if (!reservations.TryGetValue(planId, out reservation))
            {
                return false;
            }

            return reservation.PelletCount == operation.PelletCount
                && reservation.NextPelletIndex == operation.PelletIndex
                && reservation.RemainingPelletCount > 0;
        }

        private void AdvanceReservation(StableId planId)
        {
            PlanReservation reservation;
            if (!reservations.TryGetValue(planId, out reservation))
            {
                throw new InvalidOperationException(
                    "Shotgun pellet reservation disappeared during execution.");
            }

            reservation.NextPelletIndex++;
            reservation.RemainingPelletCount--;
            if (reservation.RemainingPelletCount == 0)
            {
                reservations.Remove(planId);
            }
        }

        private void ReleaseReservation(StableId planId)
        {
            reservations.Remove(planId);
        }

        private static StableId CreateHitEventId(
            StableId planId,
            int pelletIndex)
        {
            string canonical = "plan_id="
                + planId
                + "\npellet_index="
                + pelletIndex.ToString(CultureInfo.InvariantCulture);
            return StableId.Create(
                HitIdentityNamespace,
                ShotgunSpreadBehaviorModule.ComputeSha256(canonical));
        }

        private void HandleProjectileCompleted(BoundedProjectile2D projectile)
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

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private sealed class PlanReservation
        {
            public PlanReservation(int pelletCount)
            {
                PelletCount = pelletCount;
                NextPelletIndex = 0;
                RemainingPelletCount = pelletCount;
            }

            public int PelletCount { get; }

            public int NextPelletIndex { get; set; }

            public int RemainingPelletCount { get; set; }
        }
    }
}
