using System;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Contracts.Flow.Session;

namespace ShooterMover.Application.Inventory.LoadoutScreen
{
    public enum InventoryLoadoutScreenStatus
    {
        Ready = 1,
        Refreshed = 2,
        SelectionChanged = 3,
        NoChange = 4,
        InvalidSlot = 5,
        MissingEquipment = 6,
        InvalidEquipment = 7,
        DuplicateEquipmentInstance = 8,
        AuthorityRejected = 9,
        HoldingsChangedDuringApply = 10,
        Confirmed = 11,
        Cancelled = 12,
        AlreadyCompleted = 13,
    }

    /// <summary>
    /// Result contract for the canonical gun Inventory screen. The snapshot is the
    /// current exact-instance gun projection; no generic fixed-slot or armor state exists.
    /// </summary>
    public sealed class InventoryLoadoutScreenResult
    {
        public InventoryLoadoutScreenResult(
            InventoryLoadoutScreenStatus status,
            string rejectionCode,
            InventoryMenuState snapshot,
            PlayerRouteProfilePayload routePayload)
        {
            if (!Enum.IsDefined(
                typeof(InventoryLoadoutScreenStatus),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot
                ?? throw new ArgumentNullException(nameof(snapshot));
            RoutePayload = routePayload;
        }

        public InventoryLoadoutScreenStatus Status { get; }
        public string RejectionCode { get; }
        public InventoryMenuState Snapshot { get; }
        public PlayerRouteProfilePayload RoutePayload { get; }

        public bool ChangedSelection
        {
            get
            {
                return Status
                    == InventoryLoadoutScreenStatus.SelectionChanged;
            }
        }

        public bool LeavesScreen
        {
            get
            {
                return Status == InventoryLoadoutScreenStatus.Confirmed
                    || Status == InventoryLoadoutScreenStatus.Cancelled;
            }
        }
    }
}
