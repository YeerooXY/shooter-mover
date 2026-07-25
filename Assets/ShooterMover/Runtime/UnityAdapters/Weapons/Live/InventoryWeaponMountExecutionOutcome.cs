using System;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    public enum InventoryWeaponMountOutcomeSource
    {
        SuccessfulScheduling = 1,
        SchedulerRejection = 2,
        IntegrationRejection = 3,
    }

    /// <summary>
    /// Immutable observation of one enabled mount attempt. It reports the mount identity, exact
    /// scheduler/integration outcome, and whether the scheduler/pending pair was safely represented.
    /// It owns no firing, replay, delivery, or effect policy.
    /// </summary>
    public sealed class InventoryWeaponMountExecutionOutcome
    {
        internal InventoryWeaponMountExecutionOutcome(
            StableId mountStableId,
            int mountOrdinal,
            EquipmentInstanceId equipmentInstanceId,
            InventoryWeaponExecutionOutcomeKind outcomeKind,
            WeaponExecutionStatus status,
            WeaponFiringScheduleStatus? schedulerStatus,
            string rejectionCode,
            bool isExactReplay,
            int scheduledEmissionCount,
            bool statePairPublished,
            bool retryable,
            InventoryWeaponMountOutcomeSource source)
        {
            if (mountOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(mountOrdinal));
            }
            if (scheduledEmissionCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scheduledEmissionCount));
            }
            if (!Enum.IsDefined(typeof(InventoryWeaponExecutionOutcomeKind), outcomeKind))
            {
                throw new ArgumentOutOfRangeException(nameof(outcomeKind));
            }
            if (!Enum.IsDefined(typeof(WeaponExecutionStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (schedulerStatus.HasValue
                && !Enum.IsDefined(
                    typeof(WeaponFiringScheduleStatus),
                    schedulerStatus.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(schedulerStatus));
            }
            if (!Enum.IsDefined(typeof(InventoryWeaponMountOutcomeSource), source))
            {
                throw new ArgumentOutOfRangeException(nameof(source));
            }
            if (source == InventoryWeaponMountOutcomeSource.SuccessfulScheduling
                && !string.IsNullOrEmpty(rejectionCode))
            {
                throw new ArgumentException(
                    "A successful mount outcome cannot contain a rejection code.",
                    nameof(rejectionCode));
            }
            if (source != InventoryWeaponMountOutcomeSource.SuccessfulScheduling
                && string.IsNullOrWhiteSpace(rejectionCode))
            {
                throw new ArgumentException(
                    "A rejected mount outcome requires an exact diagnostic.",
                    nameof(rejectionCode));
            }

            MountStableId = mountStableId;
            MountOrdinal = mountOrdinal;
            EquipmentInstanceId = equipmentInstanceId;
            OutcomeKind = outcomeKind;
            Status = status;
            SchedulerStatus = schedulerStatus;
            RejectionCode = rejectionCode ?? string.Empty;
            IsExactReplay = isExactReplay;
            ScheduledEmissionCount = scheduledEmissionCount;
            StatePairPublished = statePairPublished;
            IsRetryable = retryable;
            Source = source;
        }

        public StableId MountStableId { get; }
        public int MountOrdinal { get; }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public InventoryWeaponExecutionOutcomeKind OutcomeKind { get; }
        public WeaponExecutionStatus Status { get; }
        public WeaponFiringScheduleStatus? SchedulerStatus { get; }
        public string RejectionCode { get; }
        public bool IsExactReplay { get; }
        public int ScheduledEmissionCount { get; }
        public bool StatePairPublished { get; }
        public bool IsRetryable { get; }
        public InventoryWeaponMountOutcomeSource Source { get; }
        public bool Succeeded
        {
            get { return Source == InventoryWeaponMountOutcomeSource.SuccessfulScheduling; }
        }
        public bool HasSafeCanonicalRepresentation
        {
            get { return StatePairPublished || IsExactReplay; }
        }
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
    }
}
