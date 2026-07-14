using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Combat
{
    /// <summary>
    /// One immutable, engine-independent operation emitted by a behavior module.
    /// Concrete modules own their operation types; the shared pipeline never switches
    /// on weapon IDs or operation kinds.
    /// </summary>
    public interface IWeaponFireExecutionOperation
    {
        /// <summary>
        /// Stable type identity used by the eventual explicitly registered operation handler.
        /// </summary>
        StableId OperationKindId { get; }

        /// <summary>
        /// Deterministic identity of the complete immutable operation payload.
        /// Equal operation IDs must mean byte-for-byte equivalent execution intent.
        /// </summary>
        StableId OperationId { get; }
    }

    /// <summary>
    /// Immutable validated per-fire input shared by every module in one weapon profile.
    /// Geometry is represented as plain scalar values so the domain remains Unity-free.
    /// </summary>
    public sealed class WeaponBehaviorInput
    {
        public WeaponBehaviorInput(
            StableId combatEventId,
            StableId weaponId,
            StableId mountId,
            long simulationStep,
            WeaponRuntimeProfile runtimeProfile,
            bool isEmpowered,
            double originX,
            double originY,
            double directionX,
            double directionY,
            double cycleStrength)
        {
            if (combatEventId == null)
            {
                throw new ArgumentNullException(nameof(combatEventId));
            }

            if (weaponId == null)
            {
                throw new ArgumentNullException(nameof(weaponId));
            }

            if (mountId == null)
            {
                throw new ArgumentNullException(nameof(mountId));
            }

            if (simulationStep < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(simulationStep),
                    simulationStep,
                    "Simulation step cannot be negative.");
            }

            if (runtimeProfile == null)
            {
                throw new ArgumentNullException(nameof(runtimeProfile));
            }

            RequireFinite(originX, nameof(originX));
            RequireFinite(originY, nameof(originY));
            RequireFinite(directionX, nameof(directionX));
            RequireFinite(directionY, nameof(directionY));
            RequireFinite(cycleStrength, nameof(cycleStrength));

            double directionLengthSquared = (directionX * directionX) + (directionY * directionY);
            if (double.IsNaN(directionLengthSquared)
                || double.IsInfinity(directionLengthSquared)
                || directionLengthSquared <= 0d)
            {
                throw new ArgumentException(
                    "Fire direction must be finite and non-zero.",
                    nameof(directionX));
            }

            if (cycleStrength < 0d || cycleStrength > 1d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cycleStrength),
                    cycleStrength,
                    "Cycle strength must be between zero and one.");
            }

            CombatEventId = combatEventId;
            WeaponId = weaponId;
            MountId = mountId;
            SimulationStep = simulationStep;
            RuntimeProfile = runtimeProfile;
            IsEmpowered = isEmpowered;
            OriginX = originX;
            OriginY = originY;
            DirectionX = directionX;
            DirectionY = directionY;
            CycleStrength = cycleStrength;
        }

        public StableId CombatEventId { get; }

        public StableId WeaponId { get; }

        public StableId MountId { get; }

        public long SimulationStep { get; }

        public WeaponRuntimeProfile RuntimeProfile { get; }

        public bool IsEmpowered { get; }

        public double OriginX { get; }

        public double OriginY { get; }

        public double DirectionX { get; }

        public double DirectionY { get; }

        /// <summary>
        /// Normalized release strength supplied by the independent mount state machine.
        /// Non-charge behavior normally receives one.
        /// </summary>
        public double CycleStrength { get; }

        internal string ToCanonicalString()
        {
            return string.Join(
                "\n",
                new[]
                {
                    "combat_event_id=" + CombatEventId,
                    "weapon_id=" + WeaponId,
                    "mount_id=" + MountId,
                    "simulation_step=" + SimulationStep.ToString(CultureInfo.InvariantCulture),
                    "runtime_profile_identity=" + RuntimeProfile.DeterministicIdentity,
                    "is_empowered=" + (IsEmpowered ? "true" : "false"),
                    "origin_x=" + OriginX.ToString("R", CultureInfo.InvariantCulture),
                    "origin_y=" + OriginY.ToString("R", CultureInfo.InvariantCulture),
                    "direction_x=" + DirectionX.ToString("R", CultureInfo.InvariantCulture),
                    "direction_y=" + DirectionY.ToString("R", CultureInfo.InvariantCulture),
                    "cycle_strength=" + CycleStrength.ToString("R", CultureInfo.InvariantCulture),
                });
        }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be finite.");
            }
        }
    }

    /// <summary>
    /// Atomic plan fragment returned by one explicitly registered behavior module.
    /// Empty fragments are valid. Input arrays are defensively copied.
    /// </summary>
    public sealed class WeaponBehaviorModulePlan
    {
        public const int MaximumOperationCount = 64;

        private readonly IWeaponFireExecutionOperation[] operations;

        public WeaponBehaviorModulePlan(
            StableId moduleId,
            params IWeaponFireExecutionOperation[] operations)
        {
            if (moduleId == null)
            {
                throw new ArgumentNullException(nameof(moduleId));
            }

            if (operations == null)
            {
                throw new ArgumentNullException(nameof(operations));
            }

            if (operations.Length > MaximumOperationCount)
            {
                throw new ArgumentException(
                    "A behavior-module plan cannot exceed "
                    + MaximumOperationCount.ToString(CultureInfo.InvariantCulture)
                    + " operations.",
                    nameof(operations));
            }

            this.operations = new IWeaponFireExecutionOperation[operations.Length];
            HashSet<StableId> operationIds = new HashSet<StableId>();
            for (int index = 0; index < operations.Length; index++)
            {
                IWeaponFireExecutionOperation operation = operations[index];
                if (operation == null)
                {
                    throw new ArgumentException(
                        "Behavior-module plans cannot contain null operations.",
                        nameof(operations));
                }

                if (operation.OperationKindId == null)
                {
                    throw new ArgumentException(
                        "Execution operations require a stable operation-kind ID.",
                        nameof(operations));
                }

                if (operation.OperationId == null)
                {
                    throw new ArgumentException(
                        "Execution operations require a deterministic operation ID.",
                        nameof(operations));
                }

                if (!operationIds.Add(operation.OperationId))
                {
                    throw new ArgumentException(
                        "A module plan cannot repeat operation ID " + operation.OperationId + ".",
                        nameof(operations));
                }

                this.operations[index] = operation;
            }

            ModuleId = moduleId;
        }

        public StableId ModuleId { get; }

        public int OperationCount
        {
            get { return operations.Length; }
        }

        public IWeaponFireExecutionOperation GetOperation(int index)
        {
            if (index < 0 || index >= operations.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return operations[index];
        }
    }

    /// <summary>
    /// Explicit pure-function boundary for one reusable weapon behavior.
    /// Implementations must not mutate input, global state, or engine objects.
    /// </summary>
    public interface IWeaponBehaviorModule
    {
        StableId ModuleId { get; }

        WeaponBehaviorModulePlan BuildExecutionPlan(WeaponBehaviorInput input);
    }
}
