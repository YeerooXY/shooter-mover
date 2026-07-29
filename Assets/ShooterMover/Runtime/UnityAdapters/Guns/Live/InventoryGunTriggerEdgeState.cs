using System;
using System.Globalization;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    public enum InventoryGunTriggerEdgeStatus
    {
        TriggerRequest = 1,
        ExactReplay = 2,
        NoSchedulerRequest = 3,
        ConflictingDuplicate = 4,
        InvalidInput = 5,
    }

    public sealed class InventoryGunTriggerEdgeDecision
    {
        internal InventoryGunTriggerEdgeDecision(
            InventoryGunTriggerEdgeStatus status,
            InventoryGunTriggerEdgeState nextState,
            GunTriggerSignal? triggerSignal,
            string rejectionCode)
        {
            Status = status;
            NextState = nextState
                ?? throw new ArgumentNullException(nameof(nextState));
            TriggerSignal = triggerSignal;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public InventoryGunTriggerEdgeStatus Status { get; }
        public InventoryGunTriggerEdgeState NextState { get; }
        public GunTriggerSignal? TriggerSignal { get; }
        public string RejectionCode { get; }
        public bool Succeeded
        {
            get
            {
                return Status == InventoryGunTriggerEdgeStatus.TriggerRequest
                    || Status == InventoryGunTriggerEdgeStatus.ExactReplay
                    || Status == InventoryGunTriggerEdgeStatus.NoSchedulerRequest;
            }
        }
        public bool HasSchedulerRequest
        {
            get
            {
                return Status == InventoryGunTriggerEdgeStatus.TriggerRequest
                    || Status == InventoryGunTriggerEdgeStatus.ExactReplay;
            }
        }
        public bool IsExactReplay
        {
            get { return Status == InventoryGunTriggerEdgeStatus.ExactReplay; }
        }
    }

    /// <summary>
    /// Immutable input-edge memory only. It classifies physical held state into Pressed, Held, or
    /// Released and remembers the last exact input operation for retry. It owns no cadence,
    /// cooldown, burst, pulse, or firing-admission rule.
    /// </summary>
    public sealed class InventoryGunTriggerEdgeState
    {
        private InventoryGunTriggerEdgeState(
            bool initialized,
            bool isHeld,
            FireOperationId lastOperationId,
            string lastInputFingerprint,
            GunTriggerSignal? lastTriggerSignal)
        {
            IsInitialized = initialized;
            IsHeld = isHeld;
            LastOperationId = lastOperationId;
            LastInputFingerprint = lastInputFingerprint ?? string.Empty;
            LastTriggerSignal = lastTriggerSignal;
        }

        public static InventoryGunTriggerEdgeState Empty
        {
            get
            {
                return new InventoryGunTriggerEdgeState(
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
        public GunTriggerSignal? LastTriggerSignal { get; }

        public InventoryGunTriggerEdgeDecision Resolve(
            bool heldNow,
            FireOperationId operationId,
            long simulationTick,
            ulong deterministicSeed,
            GunVector2 origin,
            GunVector2 aimDirection)
        {
            if (simulationTick < 0L
                || origin == null
                || !origin.IsFinite
                || aimDirection == null
                || !aimDirection.IsFinite
                || aimDirection.LengthSquared <= 0.000000000001d)
            {
                return Reject(
                    InventoryGunTriggerEdgeStatus.InvalidInput,
                    "gun-live-trigger-input-invalid");
            }

            bool previousHeld = IsInitialized && IsHeld;
            bool hasSchedulerRequest = heldNow || previousHeld;
            if (!hasSchedulerRequest)
            {
                return new InventoryGunTriggerEdgeDecision(
                    InventoryGunTriggerEdgeStatus.NoSchedulerRequest,
                    IsInitialized
                        ? this
                        : new InventoryGunTriggerEdgeState(
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
                    InventoryGunTriggerEdgeStatus.InvalidInput,
                    "gun-live-trigger-operation-required");
            }

            string inputFingerprint = GunExecutionFingerprint.Compute(
                "held=" + (heldNow ? "1" : "0") + "\n"
                + "simulation_tick="
                + simulationTick.ToString(CultureInfo.InvariantCulture)
                + "\n"
                + "deterministic_seed="
                + deterministicSeed.ToString(CultureInfo.InvariantCulture)
                + "\n"
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
                    return new InventoryGunTriggerEdgeDecision(
                        InventoryGunTriggerEdgeStatus.ExactReplay,
                        this,
                        LastTriggerSignal,
                        string.Empty);
                }

                return Reject(
                    InventoryGunTriggerEdgeStatus.ConflictingDuplicate,
                    "gun-live-trigger-operation-conflicting-duplicate");
            }

            GunTriggerSignal signal;
            if (!previousHeld && heldNow)
            {
                signal = GunTriggerSignal.Pressed;
            }
            else if (previousHeld && heldNow)
            {
                signal = GunTriggerSignal.Held;
            }
            else
            {
                signal = GunTriggerSignal.Released;
            }

            return new InventoryGunTriggerEdgeDecision(
                InventoryGunTriggerEdgeStatus.TriggerRequest,
                new InventoryGunTriggerEdgeState(
                    true,
                    heldNow,
                    operationId,
                    inputFingerprint,
                    signal),
                signal,
                string.Empty);
        }

        private InventoryGunTriggerEdgeDecision Reject(
            InventoryGunTriggerEdgeStatus status,
            string rejectionCode)
        {
            return new InventoryGunTriggerEdgeDecision(
                status,
                this,
                null,
                rejectionCode);
        }
    }
}
