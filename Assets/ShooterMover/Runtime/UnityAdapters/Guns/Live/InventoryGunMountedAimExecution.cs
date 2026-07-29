using System;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    /// <summary>
    /// Executes a concurrent mounted volley against one locked world-space target.
    /// Physical muzzle origins remain distinct, while every mount derives its own
    /// immutable direction from that muzzle to the same target point.
    /// </summary>
    public static class InventoryGunMountedAimExecution
    {
        // Keep the four muzzle origins close to the player silhouette while retaining
        // distinct physical mount identities and deterministic per-mount aim.
        public const double LiveMountOffsetScale = 0.3d;

        public static InventoryGunExecutionResult TryFireAtTarget(
            this InventoryGunLiveSetup runtime,
            FireOperationId fireOperationId,
            long simulationTick,
            ulong deterministicSeed,
            GunVector2 actorOrigin,
            GunVector2 targetPoint)
        {
            if (runtime == null
                || fireOperationId == null
                || actorOrigin == null
                || targetPoint == null)
            {
                return Reject("gun-live-target-intent-invalid");
            }

            GunVector2 centerDirection = Direction(
                actorOrigin,
                targetPoint);
            if (centerDirection == null)
            {
                return Reject("gun-live-target-direction-invalid");
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

            InventoryGunFireRequest admission;
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

            InventoryGunExecutionResult firstAccepted = null;
            InventoryGunExecutionResult firstCooldown = null;
            InventoryGunExecutionResult firstFailure = null;
            for (int index = 0;
                index < runtime.EnabledMounts.Count;
                index++)
            {
                InventoryGunMountedLive mount =
                    runtime.EnabledMounts[index];
                GunVector2 mountOrigin = ResolveMountOrigin(
                    actorOrigin,
                    centerDirection,
                    mount.LateralOffset * LiveMountOffsetScale);
                GunVector2 mountDirection = Direction(
                    mountOrigin,
                    targetPoint);
                if (mountDirection == null)
                {
                    if (firstFailure == null)
                    {
                        firstFailure = Reject(
                            "gun-live-mount-target-direction-invalid");
                    }
                    continue;
                }

                InventoryGunExecutionResult result = runtime.TryExecute(
                    new InventoryGunFireRequest(
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
                if (result.Status == GunExecutionStatus.Accepted
                    || result.Status
                        == GunExecutionStatus.ReplayAccepted)
                {
                    if (firstAccepted == null)
                    {
                        firstAccepted = result;
                    }
                }
                else if (result.Status
                    == GunExecutionStatus.CooldownActive)
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
                ?? Reject("gun-live-no-enabled-mounts");
        }

        private static GunVector2 ResolveMountOrigin(
            GunVector2 actorOrigin,
            GunVector2 centerDirection,
            double lateralOffset)
        {
            return new GunVector2(
                actorOrigin.X - (centerDirection.Y * lateralOffset),
                actorOrigin.Y + (centerDirection.X * lateralOffset));
        }

        private static GunVector2 Direction(
            GunVector2 origin,
            GunVector2 target)
        {
            double deltaX = target.X - origin.X;
            double deltaY = target.Y - origin.Y;
            double length = Math.Sqrt(
                (deltaX * deltaX) + (deltaY * deltaY));
            return length <= 0.0000001d
                ? null
                : new GunVector2(
                    deltaX / length,
                    deltaY / length);
        }

        private static FireOperationId DerivedOperationId(
            FireOperationId baseOperationId,
            StableId mountStableId)
        {
            string fingerprint = GunExecutionFingerprint.Compute(
                baseOperationId + "|" + mountStableId);
            return new FireOperationId(
                StableId.Create(
                    "fire-operation",
                    fingerprint.Substring(
                        GunExecutionFingerprint.Prefix.Length)));
        }

        private static ulong DerivedSeed(
            ulong deterministicSeed,
            int mountOrdinal)
        {
            return deterministicSeed
                ^ (unchecked((ulong)(mountOrdinal + 1))
                    * 11400714819323198485UL);
        }

        private static InventoryGunExecutionResult Reject(
            string rejectionCode)
        {
            return new InventoryGunExecutionResult(
                null,
                GunExecutionResult.Reject(
                    GunExecutionStatus.InvalidCommand,
                    string.IsNullOrEmpty(rejectionCode)
                        ? "gun-live-target-intent-rejected"
                        : rejectionCode,
                    0L),
                null);
        }
    }
}
