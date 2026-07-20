using System;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    /// <summary>
    /// Executes a concurrent mounted volley against one locked world-space target.
    /// Physical muzzle origins remain distinct, while every mount derives its own
    /// immutable direction from that muzzle to the same target point.
    /// </summary>
    public static class InventoryWeaponMountedAimExecutionV1
    {
        public const double LiveMountOffsetScale = 0.5d;

        public static InventoryWeaponExecutionResult TryFireAtTarget(
            this InventoryWeaponRuntimeComposition runtime,
            FireOperationId fireOperationId,
            long simulationTick,
            ulong deterministicSeed,
            WeaponVector2 actorOrigin,
            WeaponVector2 targetPoint)
        {
            if (runtime == null
                || fireOperationId == null
                || actorOrigin == null
                || targetPoint == null)
            {
                return Reject("weapon-live-target-intent-invalid");
            }

            WeaponVector2 centerDirection = Direction(
                actorOrigin,
                targetPoint);
            if (centerDirection == null)
            {
                return Reject("weapon-live-target-direction-invalid");
            }

            if (!runtime.IsConcurrentMountMode)
            {
                return runtime.TryFire(
                    fireOperationId,
                    simulationTick,
                    deterministicSeed,
                    actorOrigin,
                    centerDirection);
            }

            InventoryWeaponFireRequest admission;
            string rejectionCode;
            if (!runtime.TryCreateFireIntent(
                fireOperationId,
                simulationTick,
                deterministicSeed,
                actorOrigin,
                centerDirection,
                out admission,
                out rejectionCode)
                || admission == null)
            {
                return Reject(rejectionCode);
            }

            InventoryWeaponExecutionResult firstAccepted = null;
            InventoryWeaponExecutionResult firstCooldown = null;
            InventoryWeaponExecutionResult firstFailure = null;
            for (int index = 0;
                index < runtime.EnabledMounts.Count;
                index++)
            {
                InventoryWeaponMountedRuntimeV1 mount =
                    runtime.EnabledMounts[index];
                WeaponVector2 mountOrigin = ResolveMountOrigin(
                    actorOrigin,
                    centerDirection,
                    mount.LateralOffset * LiveMountOffsetScale);
                WeaponVector2 mountDirection = Direction(
                    mountOrigin,
                    targetPoint);
                if (mountDirection == null)
                {
                    if (firstFailure == null)
                    {
                        firstFailure = Reject(
                            "weapon-live-mount-target-direction-invalid");
                    }
                    continue;
                }

                InventoryWeaponExecutionResult result = runtime.TryExecute(
                    new InventoryWeaponFireRequest(
                        admission.ActorId,
                        mount.EquipmentInstanceId,
                        DerivedOperationId(
                            fireOperationId,
                            mount.MountStableId),
                        admission.LifecycleGeneration,
                        simulationTick,
                        DerivedSeed(deterministicSeed, index),
                        mountOrigin,
                        mountDirection));
                if (result.Status == WeaponExecutionStatus.Accepted
                    || result.Status
                        == WeaponExecutionStatus.ReplayAccepted)
                {
                    if (firstAccepted == null)
                    {
                        firstAccepted = result;
                    }
                }
                else if (result.Status
                    == WeaponExecutionStatus.CooldownActive)
                {
                    if (firstCooldown == null)
                    {
                        firstCooldown = result;
                    }
                }
                else if (firstFailure == null)
                {
                    firstFailure = result;
                }
            }

            return firstAccepted
                ?? firstCooldown
                ?? firstFailure
                ?? Reject("weapon-live-no-enabled-mounts");
        }

        private static WeaponVector2 ResolveMountOrigin(
            WeaponVector2 actorOrigin,
            WeaponVector2 centerDirection,
            double lateralOffset)
        {
            return new WeaponVector2(
                actorOrigin.X - (centerDirection.Y * lateralOffset),
                actorOrigin.Y + (centerDirection.X * lateralOffset));
        }

        private static WeaponVector2 Direction(
            WeaponVector2 origin,
            WeaponVector2 target)
        {
            double deltaX = target.X - origin.X;
            double deltaY = target.Y - origin.Y;
            double length = Math.Sqrt(
                (deltaX * deltaX) + (deltaY * deltaY));
            return length <= 0.0000001d
                ? null
                : new WeaponVector2(
                    deltaX / length,
                    deltaY / length);
        }

        private static FireOperationId DerivedOperationId(
            FireOperationId baseOperationId,
            StableId mountStableId)
        {
            string fingerprint = WeaponExecutionFingerprint.Compute(
                baseOperationId + "|" + mountStableId);
            return new FireOperationId(
                StableId.Create(
                    "fire-operation",
                    fingerprint.Substring(
                        WeaponExecutionFingerprint.Prefix.Length)));
        }

        private static ulong DerivedSeed(
            ulong deterministicSeed,
            int mountOrdinal)
        {
            return deterministicSeed
                ^ (unchecked((ulong)(mountOrdinal + 1))
                    * 11400714819323198485UL);
        }

        private static InventoryWeaponExecutionResult Reject(
            string rejectionCode)
        {
            return new InventoryWeaponExecutionResult(
                null,
                WeaponExecutionResult.Reject(
                    WeaponExecutionStatus.InvalidCommand,
                    string.IsNullOrEmpty(rejectionCode)
                        ? "weapon-live-target-intent-rejected"
                        : rejectionCode,
                    0L),
                null);
        }
    }
}
