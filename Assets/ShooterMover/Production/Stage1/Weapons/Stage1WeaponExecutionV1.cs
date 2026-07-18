using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Production.Stage1.Weapons
{
    public enum Stage1WeaponExecutionStatusV1
    {
        Accepted = 1,
        UnknownRuntimeWeapon = 2,
        MissingActiveEquipment = 3,
        InvalidAim = 4,
        DuplicateOperation = 5,
        CooldownActive = 6,
        ExecutorRejected = 7,
    }

    public sealed class Stage1WeaponExecutionResultV1
    {
        private Stage1WeaponExecutionResultV1(
            Stage1WeaponExecutionStatusV1 status,
            string rejectionCode,
            int effectRequestCount)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            EffectRequestCount = effectRequestCount;
        }

        public Stage1WeaponExecutionStatusV1 Status { get; }
        public string RejectionCode { get; }
        public int EffectRequestCount { get; }
        public bool Succeeded { get { return Status == Stage1WeaponExecutionStatusV1.Accepted; } }

        public static Stage1WeaponExecutionResultV1 Accept(int effectRequestCount)
        {
            return new Stage1WeaponExecutionResultV1(
                Stage1WeaponExecutionStatusV1.Accepted,
                string.Empty,
                effectRequestCount);
        }

        public static Stage1WeaponExecutionResultV1 Reject(
            Stage1WeaponExecutionStatusV1 status,
            string rejectionCode)
        {
            return new Stage1WeaponExecutionResultV1(status, rejectionCode, 0);
        }
    }

    public sealed class Stage1WeaponExecutionRequestV1
    {
        public Stage1WeaponExecutionRequestV1(
            StableId operationStableId,
            object equipmentInstance,
            StableId runtimeWeaponStableId,
            Vector3 origin,
            Vector3 aimDirection,
            object shooterContext,
            double timestampSeconds)
        {
            OperationStableId = operationStableId;
            EquipmentInstance = equipmentInstance;
            RuntimeWeaponStableId = runtimeWeaponStableId;
            Origin = origin;
            AimDirection = aimDirection;
            ShooterContext = shooterContext;
            TimestampSeconds = timestampSeconds;
        }

        public StableId OperationStableId { get; }
        public object EquipmentInstance { get; }
        public StableId RuntimeWeaponStableId { get; }
        public Vector3 Origin { get; }
        public Vector3 AimDirection { get; }
        public object ShooterContext { get; }
        public double TimestampSeconds { get; }
    }

    public interface IStage1WeaponExecutorV1
    {
        StableId RuntimeWeaponStableId { get; }
        Stage1WeaponExecutionResultV1 TryExecute(Stage1WeaponExecutionRequestV1 request);
        void ResetTransientState();
    }

    public sealed class Stage1WeaponExecutionRegistryV1
    {
        private readonly Dictionary<StableId, IStage1WeaponExecutorV1> executors =
            new Dictionary<StableId, IStage1WeaponExecutorV1>();

        public int Count { get { return executors.Count; } }

        public void Register(IStage1WeaponExecutorV1 executor)
        {
            if (executor == null)
            {
                throw new ArgumentNullException(nameof(executor));
            }

            if (executor.RuntimeWeaponStableId == null)
            {
                throw new ArgumentException("Executor runtime weapon ID is required.", nameof(executor));
            }

            if (executors.ContainsKey(executor.RuntimeWeaponStableId))
            {
                throw new InvalidOperationException(
                    "Duplicate Stage 1 runtime weapon registration: "
                    + executor.RuntimeWeaponStableId);
            }

            executors.Add(executor.RuntimeWeaponStableId, executor);
        }

        public bool TryResolve(
            StableId runtimeWeaponStableId,
            out IStage1WeaponExecutorV1 executor)
        {
            if (runtimeWeaponStableId == null)
            {
                executor = null;
                return false;
            }

            return executors.TryGetValue(runtimeWeaponStableId, out executor);
        }

        public void ResetTransientState()
        {
            foreach (IStage1WeaponExecutorV1 executor in executors.Values)
            {
                executor.ResetTransientState();
            }
        }
    }

    public sealed class Stage1WeaponExecutionDispatcherV1
    {
        private readonly Stage1WeaponExecutionRegistryV1 registry;

        public Stage1WeaponExecutionDispatcherV1(Stage1WeaponExecutionRegistryV1 registry)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public Stage1WeaponExecutionResultV1 TryExecute(Stage1WeaponExecutionRequestV1 request)
        {
            if (request == null || request.EquipmentInstance == null)
            {
                return Stage1WeaponExecutionResultV1.Reject(
                    Stage1WeaponExecutionStatusV1.MissingActiveEquipment,
                    "stage1-weapon-missing-active-equipment");
            }

            if (request.OperationStableId == null)
            {
                return Stage1WeaponExecutionResultV1.Reject(
                    Stage1WeaponExecutionStatusV1.ExecutorRejected,
                    "stage1-weapon-operation-id-missing");
            }

            if (request.AimDirection.sqrMagnitude <= 0.000001f)
            {
                return Stage1WeaponExecutionResultV1.Reject(
                    Stage1WeaponExecutionStatusV1.InvalidAim,
                    "stage1-weapon-invalid-aim");
            }

            IStage1WeaponExecutorV1 executor;
            if (!registry.TryResolve(request.RuntimeWeaponStableId, out executor))
            {
                return Stage1WeaponExecutionResultV1.Reject(
                    Stage1WeaponExecutionStatusV1.UnknownRuntimeWeapon,
                    "stage1-weapon-unknown-runtime-id:" + request.RuntimeWeaponStableId);
            }

            return executor.TryExecute(request);
        }

        public void ResetTransientState()
        {
            registry.ResetTransientState();
        }
    }

    public enum Stage1WeaponEffectKindV1
    {
        Projectile = 1,
        Arc = 2,
    }

    public sealed class Stage1WeaponEffectRequestV1
    {
        public Stage1WeaponEffectRequestV1(
            StableId operationStableId,
            object equipmentInstance,
            Stage1WeaponEffectKindV1 kind,
            Vector3 origin,
            Vector3 direction,
            float projectileSpeed,
            float lifetimeSeconds,
            float directDamage,
            float areaDamage,
            float explosionRadius,
            int chainCount,
            float chainRange)
        {
            OperationStableId = operationStableId;
            EquipmentInstance = equipmentInstance;
            Kind = kind;
            Origin = origin;
            Direction = direction.normalized;
            ProjectileSpeed = projectileSpeed;
            LifetimeSeconds = lifetimeSeconds;
            DirectDamage = directDamage;
            AreaDamage = areaDamage;
            ExplosionRadius = explosionRadius;
            ChainCount = chainCount;
            ChainRange = chainRange;
        }

        public StableId OperationStableId { get; }
        public object EquipmentInstance { get; }
        public Stage1WeaponEffectKindV1 Kind { get; }
        public Vector3 Origin { get; }
        public Vector3 Direction { get; }
        public float ProjectileSpeed { get; }
        public float LifetimeSeconds { get; }
        public float DirectDamage { get; }
        public float AreaDamage { get; }
        public float ExplosionRadius { get; }
        public int ChainCount { get; }
        public float ChainRange { get; }
    }

    public interface IStage1WeaponEffectSinkV1
    {
        bool TryRequest(Stage1WeaponEffectRequestV1 request);
    }

    public sealed class Stage1WeaponTuningV1
    {
        public Stage1WeaponTuningV1(
            double fireIntervalSeconds,
            int projectileCount,
            float spreadDegrees,
            float projectileSpeed,
            float projectileLifetimeSeconds,
            float directDamage,
            float areaDamage,
            float explosionRadius,
            int chainCount,
            float chainRange)
        {
            if (fireIntervalSeconds < 0d || projectileCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(fireIntervalSeconds));
            }

            FireIntervalSeconds = fireIntervalSeconds;
            ProjectileCount = projectileCount;
            SpreadDegrees = spreadDegrees;
            ProjectileSpeed = projectileSpeed;
            ProjectileLifetimeSeconds = projectileLifetimeSeconds;
            DirectDamage = directDamage;
            AreaDamage = areaDamage;
            ExplosionRadius = explosionRadius;
            ChainCount = chainCount;
            ChainRange = chainRange;
        }

        public double FireIntervalSeconds { get; }
        public int ProjectileCount { get; }
        public float SpreadDegrees { get; }
        public float ProjectileSpeed { get; }
        public float ProjectileLifetimeSeconds { get; }
        public float DirectDamage { get; }
        public float AreaDamage { get; }
        public float ExplosionRadius { get; }
        public int ChainCount { get; }
        public float ChainRange { get; }
    }

    public abstract class Stage1ConfiguredWeaponExecutorV1 : IStage1WeaponExecutorV1
    {
        private readonly IStage1WeaponEffectSinkV1 effectSink;
        private readonly HashSet<StableId> acceptedOperations = new HashSet<StableId>();
        private double nextAcceptedTime;

        protected Stage1ConfiguredWeaponExecutorV1(
            StableId runtimeWeaponStableId,
            Stage1WeaponTuningV1 tuning,
            IStage1WeaponEffectSinkV1 effectSink)
        {
            RuntimeWeaponStableId = runtimeWeaponStableId
                ?? throw new ArgumentNullException(nameof(runtimeWeaponStableId));
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            this.effectSink = effectSink ?? throw new ArgumentNullException(nameof(effectSink));
        }

        public StableId RuntimeWeaponStableId { get; }
        protected Stage1WeaponTuningV1 Tuning { get; }
        protected virtual Stage1WeaponEffectKindV1 EffectKind
        {
            get { return Stage1WeaponEffectKindV1.Projectile; }
        }

        public Stage1WeaponExecutionResultV1 TryExecute(Stage1WeaponExecutionRequestV1 request)
        {
            if (request == null
                || request.OperationStableId == null
                || request.EquipmentInstance == null
                || request.RuntimeWeaponStableId != RuntimeWeaponStableId)
            {
                return Stage1WeaponExecutionResultV1.Reject(
                    Stage1WeaponExecutionStatusV1.ExecutorRejected,
                    "stage1-weapon-executor-request-invalid");
            }

            if (acceptedOperations.Contains(request.OperationStableId))
            {
                return Stage1WeaponExecutionResultV1.Reject(
                    Stage1WeaponExecutionStatusV1.DuplicateOperation,
                    "stage1-weapon-duplicate-operation");
            }

            if (request.TimestampSeconds < nextAcceptedTime)
            {
                return Stage1WeaponExecutionResultV1.Reject(
                    Stage1WeaponExecutionStatusV1.CooldownActive,
                    "stage1-weapon-cooldown-active");
            }

            var effects = BuildEffects(request);
            for (int index = 0; index < effects.Count; index++)
            {
                if (!effectSink.TryRequest(effects[index]))
                {
                    return Stage1WeaponExecutionResultV1.Reject(
                        Stage1WeaponExecutionStatusV1.ExecutorRejected,
                        "stage1-weapon-effect-sink-rejected");
                }
            }

            acceptedOperations.Add(request.OperationStableId);
            nextAcceptedTime = request.TimestampSeconds + Tuning.FireIntervalSeconds;
            return Stage1WeaponExecutionResultV1.Accept(effects.Count);
        }

        public void ResetTransientState()
        {
            acceptedOperations.Clear();
            nextAcceptedTime = 0d;
        }

        protected virtual List<Stage1WeaponEffectRequestV1> BuildEffects(
            Stage1WeaponExecutionRequestV1 request)
        {
            var effects = new List<Stage1WeaponEffectRequestV1>(Tuning.ProjectileCount);
            for (int index = 0; index < Tuning.ProjectileCount; index++)
            {
                float offset = Tuning.ProjectileCount == 1
                    ? 0f
                    : Mathf.Lerp(
                        -Tuning.SpreadDegrees * 0.5f,
                        Tuning.SpreadDegrees * 0.5f,
                        index / (float)(Tuning.ProjectileCount - 1));
                Vector3 direction = Quaternion.AngleAxis(offset, Vector3.forward)
                    * request.AimDirection.normalized;
                effects.Add(new Stage1WeaponEffectRequestV1(
                    request.OperationStableId,
                    request.EquipmentInstance,
                    EffectKind,
                    request.Origin,
                    direction,
                    Tuning.ProjectileSpeed,
                    Tuning.ProjectileLifetimeSeconds,
                    Tuning.DirectDamage,
                    Tuning.AreaDamage,
                    Tuning.ExplosionRadius,
                    Tuning.ChainCount,
                    Tuning.ChainRange));
            }

            return effects;
        }
    }

    public sealed class BlasterMachineGunExecutorV1 : Stage1ConfiguredWeaponExecutorV1
    {
        public static readonly StableId WeaponStableId =
            StableId.Parse("weapon.blaster-machine-gun");

        public BlasterMachineGunExecutorV1(IStage1WeaponEffectSinkV1 sink)
            : base(WeaponStableId, new Stage1WeaponTuningV1(
                0.09d, 1, 0f, 18f, 2f, 1f, 0f, 0f, 0, 0f), sink) { }
    }

    public sealed class ShotgunWeaponExecutorV1 : Stage1ConfiguredWeaponExecutorV1
    {
        public static readonly StableId WeaponStableId = StableId.Parse("weapon.shotgun");

        public ShotgunWeaponExecutorV1(IStage1WeaponEffectSinkV1 sink)
            : base(WeaponStableId, new Stage1WeaponTuningV1(
                0.7d, 7, 18f, 15f, 1.1f, 1f, 0f, 0f, 0, 0f), sink) { }
    }

    public sealed class RocketLauncherWeaponExecutorV1 : Stage1ConfiguredWeaponExecutorV1
    {
        public static readonly StableId WeaponStableId =
            StableId.Parse("weapon.rocket-launcher");

        public RocketLauncherWeaponExecutorV1(IStage1WeaponEffectSinkV1 sink)
            : base(WeaponStableId, new Stage1WeaponTuningV1(
                1.2d, 1, 0f, 8f, 3f, 2f, 6f, 2.5f, 0, 0f), sink) { }
    }

    public sealed class ArcGunWeaponExecutorV1 : Stage1ConfiguredWeaponExecutorV1
    {
        public static readonly StableId WeaponStableId = StableId.Parse("weapon.arc-gun");

        protected override Stage1WeaponEffectKindV1 EffectKind
        {
            get { return Stage1WeaponEffectKindV1.Arc; }
        }

        public ArcGunWeaponExecutorV1(IStage1WeaponEffectSinkV1 sink)
            : base(WeaponStableId, new Stage1WeaponTuningV1(
                0.5d, 1, 0f, 0f, 0f, 2f, 0f, 0f, 3, 5f), sink) { }
    }

    public static class Stage1WeaponCompositionV1
    {
        public static Stage1WeaponExecutionRegistryV1 CreateDefault(
            IStage1WeaponEffectSinkV1 effectSink)
        {
            var registry = new Stage1WeaponExecutionRegistryV1();
            registry.Register(new BlasterMachineGunExecutorV1(effectSink));
            registry.Register(new ShotgunWeaponExecutorV1(effectSink));
            registry.Register(new RocketLauncherWeaponExecutorV1(effectSink));
            registry.Register(new ArcGunWeaponExecutorV1(effectSink));
            return registry;
        }
    }
}
