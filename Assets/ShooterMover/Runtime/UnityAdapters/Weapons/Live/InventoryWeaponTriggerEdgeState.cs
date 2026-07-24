using System;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    public enum InventoryWeaponTriggerEdgeStatus
    {
        TriggerRequest = 1,
        ExactReplay = 2,
        NoSchedulerRequest = 3,
        ConflictingDuplicate = 4,
        InvalidInput = 5,
    }

    public sealed class InventoryWeaponTriggerEdgeDecision
    {
        internal InventoryWeaponTriggerEdgeDecision(
            InventoryWeaponTriggerEdgeStatus status,
            InventoryWeaponTriggerEdgeState nextState,
            WeaponTriggerSignal? triggerSignal,
            string rejectionCode)
        {
            Status = status;
            NextState = nextState
                ?? throw new ArgumentNullException(nameof(nextState));
            TriggerSignal = triggerSignal;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public InventoryWeaponTriggerEdgeStatus Status { get; }
        public InventoryWeaponTriggerEdgeState NextState { get; }
        public WeaponTriggerSignal? TriggerSignal { get; }
        public string RejectionCode { get; }
        public bool Succeeded
        {
            get
            {
                return Status == InventoryWeaponTriggerEdgeStatus.TriggerRequest
                    || Status == InventoryWeaponTriggerEdgeStatus.ExactReplay
                    || Status == InventoryWeaponTriggerEdgeStatus.NoSchedulerRequest;
            }
        }
        public bool HasSchedulerRequest
        {
            get
            {
                return Status == InventoryWeaponTriggerEdgeStatus.TriggerRequest
                    || Status == InventoryWeaponTriggerEdgeStatus.ExactReplay;
            }
        }
        public bool IsExactReplay
        {
            get { return Status == InventoryWeaponTriggerEdgeStatus.ExactReplay; }
        }
    }

    /// <summary>
    /// Immutable input-edge memory only. It classifies physical held state into Pressed, Held, or
    /// Released and remembers the last exact input operation for retry. It owns no cadence,
    /// cooldown, burst, pulse, or firing-admission rule.
    /// </summary>
    public sealed class InventoryWeaponTriggerEdgeState
    {
        private InventoryWeaponTriggerEdgeState(
            bool initialized,
            bool isHeld,
            FireOperationId lastOperationId,
            string lastInputFingerprint,
            WeaponTriggerSignal? lastTriggerSignal)
        {
            IsInitialized = initialized;
            IsHeld = isHeld;
            LastOperationId = lastOperationId;
            LastInputFingerprint = lastInputFingerprint ?? string.Empty;
            LastTriggerSignal = lastTriggerSignal;
        }

        public static InventoryWeaponTriggerEdgeState Empty
        {
            get
            {
                return new InventoryWeaponTriggerEdgeState(
                    false,
                    false,
                    null,
                    string.Empty,
                    null);
            }
        }

        public bool IsInitialized { get; }
        public bool IsHeld { get; }
        public FireOperationId LastOperationId { get; }
        public string LastInputFingerprint { get; }
        public WeaponTriggerSignal? LastTriggerSignal { get; }

        public InventoryWeaponTriggerEdgeDecision Resolve(
            bool heldNow,
            FireOperationId operationId,
            long simulationTick,
            ulong deterministicSeed,
            WeaponVector2 origin,
            WeaponVector2 aimDirection)
        {
            if (simulationTick < 0L
                || origin == null
                || !origin.IsFinite
                || aimDirection == null
                || !aimDirection.IsFinite
                || aimDirection.LengthSquared <= 0.000000000001d)
            {
                return Reject(
                    InventoryWeaponTriggerEdgeStatus.InvalidInput,
                    "weapon-live-trigger-input-invalid");
            }

            bool previousHeld = IsInitialized && IsHeld;
            bool hasSchedulerRequest = heldNow || previousHeld;
            if (!hasSchedulerRequest)
            {
                return new InventoryWeaponTriggerEdgeDecision(
                    InventoryWeaponTriggerEdgeStatus.NoSchedulerRequest,
                    IsInitialized
                        ? this
                        : new InventoryWeaponTriggerEdgeState(
                            true,
                            false,
                            null,
                            string.Empty,
                            null),
                    null,
                    string.Empty);
            }

            if (operationId == null)
            {
                return Reject(
                    InventoryWeaponTriggerEdgeStatus.InvalidInput,
                    "weapon-live-trigger-operation-required");
            }

            string inputFingerprint = WeaponExecutionFingerprint.Compute(
                "held=" + (heldNow ? "1" : "0") + "\n"
                + "simulation_tick=" + simulationTick + "\n"
                + "deterministic_seed=" + deterministicSeed + "\n"
                + "origin=" + origin + "\n"
                + "aim_direction=" + aimDirection + "\n");

            if (LastOperationId != null
                && LastOperationId.Equals(operationId))
            {
                if (string.Equals(
                        LastInputFingerprint,
                        inputFingerprint,
                        StringComparison.Ordinal)
                    && LastTriggerSignal.HasValue)
                {
                    return new InventoryWeaponTriggerEdgeDecision(
                        InventoryWeaponTriggerEdgeStatus.ExactReplay,
                        this,
                        LastTriggerSignal,
                        string.Empty);
                }

                return Reject(
                    InventoryWeaponTriggerEdgeStatus.ConflictingDuplicate,
                    "weapon-live-trigger-operation-conflicting-duplicate");
            }

            WeaponTriggerSignal signal;
            if (!previousHeld && heldNow)
            {
                signal = WeaponTriggerSignal.Pressed;
            }
            else if (previousHeld && heldNow)
            {
                signal = WeaponTriggerSignal.Held;
            }
            else
            {
                signal = WeaponTriggerSignal.Released;
            }

            return new InventoryWeaponTriggerEdgeDecision(
                InventoryWeaponTriggerEdgeStatus.TriggerRequest,
                new InventoryWeaponTriggerEdgeState(
                    true,
                    heldNow,
                    operationId,
                    inputFingerprint,
                    signal),
                signal,
                string.Empty);
        }

        private InventoryWeaponTriggerEdgeDecision Reject(
            InventoryWeaponTriggerEdgeStatus status,
            string rejectionCode)
        {
            return new InventoryWeaponTriggerEdgeDecision(
                status,
                this,
                null,
                rejectionCode);
        }
    }
}
