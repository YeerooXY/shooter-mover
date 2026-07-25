using System;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    /// <summary>
    /// Immutable observation of one mount's scheduler result. It reports what the canonical
    /// scheduler decided and does not participate in admission, delivery, or effect authority.
    /// </summary>
    public sealed class InventoryWeaponSchedulingOutcome
    {
        internal InventoryWeaponSchedulingOutcome(
            EquipmentInstanceId equipmentInstanceId,
            InventoryWeaponExecutionOutcomeKind outcomeKind,
            WeaponExecutionStatus status,
            WeaponFiringScheduleStatus schedulerStatus,
            bool isExactReplay,
            int scheduledEmissionCount)
        {
            if (scheduledEmissionCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scheduledEmissionCount));
            }
            if (outcomeKind
                    != InventoryWeaponExecutionOutcomeKind.AcceptedScheduleQueued
                && outcomeKind
                    != InventoryWeaponExecutionOutcomeKind.ReplayedScheduleRetained
                && outcomeKind
                    != InventoryWeaponExecutionOutcomeKind.AcceptedNoEmissionTransition
                && outcomeKind
                    != InventoryWeaponExecutionOutcomeKind.ReplayedNoEmissionTransition)
            {
                throw new ArgumentOutOfRangeException(nameof(outcomeKind));
            }

            EquipmentInstanceId = equipmentInstanceId;
            OutcomeKind = outcomeKind;
            Status = status;
            SchedulerStatus = schedulerStatus;
            IsExactReplay = isExactReplay;
            ScheduledEmissionCount = scheduledEmissionCount;
        }

        public EquipmentInstanceId EquipmentInstanceId { get; }
        public InventoryWeaponExecutionOutcomeKind OutcomeKind { get; }
        public WeaponExecutionStatus Status { get; }
        public WeaponFiringScheduleStatus SchedulerStatus { get; }
        public bool IsExactReplay { get; }
        public int ScheduledEmissionCount { get; }
        public bool IsNoEmissionTransition
        {
            get
            {
                return OutcomeKind
                        == InventoryWeaponExecutionOutcomeKind.AcceptedNoEmissionTransition
                    || OutcomeKind
                        == InventoryWeaponExecutionOutcomeKind.ReplayedNoEmissionTransition;
            }
        }
        public bool IsWaitingForCadence
        {
            get
            {
                return IsNoEmissionTransition
                    && SchedulerStatus == WeaponFiringScheduleStatus.WaitingForCadence;
            }
        }
        public bool IsReleaseTransition
        {
            get
            {
                return IsNoEmissionTransition
                    && SchedulerStatus == WeaponFiringScheduleStatus.Released;
            }
        }
    }
}
