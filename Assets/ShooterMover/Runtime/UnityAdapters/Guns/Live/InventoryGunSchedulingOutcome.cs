using System;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    /// <summary>
    /// Immutable observation of one mount's scheduler result. It reports what the canonical
    /// scheduler decided and does not participate in admission, delivery, or effect authority.
    /// </summary>
    public sealed class InventoryGunSchedulingOutcome
    {
        internal InventoryGunSchedulingOutcome(
            EquipmentInstanceId equipmentInstanceId,
            InventoryGunExecutionOutcomeKind outcomeKind,
            GunExecutionStatus status,
            GunFiringScheduleStatus schedulerStatus,
            bool isExactReplay,
            int scheduledEmissionCount)
        {
            if (scheduledEmissionCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scheduledEmissionCount));
            }
            if (outcomeKind
                    != InventoryGunExecutionOutcomeKind.AcceptedScheduleQueued
                && outcomeKind
                    != InventoryGunExecutionOutcomeKind.ReplayedScheduleRetained
                && outcomeKind
                    != InventoryGunExecutionOutcomeKind.AcceptedNoEmissionTransition
                && outcomeKind
                    != InventoryGunExecutionOutcomeKind.ReplayedNoEmissionTransition)
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
        public InventoryGunExecutionOutcomeKind OutcomeKind { get; }
        public GunExecutionStatus Status { get; }
        public GunFiringScheduleStatus SchedulerStatus { get; }
        public bool IsExactReplay { get; }
        public int ScheduledEmissionCount { get; }
        public bool IsNoEmissionTransition
        {
            get
            {
                return OutcomeKind
                        == InventoryGunExecutionOutcomeKind.AcceptedNoEmissionTransition
                    || OutcomeKind
                        == InventoryGunExecutionOutcomeKind.ReplayedNoEmissionTransition;
            }
        }
        public bool IsWaitingForCadence
        {
            get
            {
                return IsNoEmissionTransition
                    && SchedulerStatus == GunFiringScheduleStatus.WaitingForCadence;
            }
        }
        public bool IsReleaseTransition
        {
            get
            {
                return IsNoEmissionTransition
                    && SchedulerStatus == GunFiringScheduleStatus.Released;
            }
        }
    }
}
