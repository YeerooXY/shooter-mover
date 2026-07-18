using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Missions.Run;
using ShooterMover.Domain.Common;

namespace ShooterMover.UnityAdapters.Missions.Run
{
    public enum Stage1WeaponSlotSelectionStatusV1
    {
        Selected = 1,
        ExactDuplicateNoChange = 2,
        InvalidSlot = 3,
        InvalidLoadout = 4,
    }

    /// <summary>
    /// Four-slot production selection projection. When created from a run coordinator,
    /// the coordinator remains the sole active-slot authority; this type only exposes
    /// concrete equipment-instance identities to Unity presentation.
    /// </summary>
    public sealed class Stage1ProductionWeaponSlotSelectionV1
    {
        public const int SlotCount = 4;

        private readonly ReadOnlyCollection<StableId> equipmentInstanceStableIds;
        private readonly LevelRunCoordinatorV1 coordinator;
        private int selectedSlotIndex;

        public Stage1ProductionWeaponSlotSelectionV1(
            IEnumerable<StableId> equipmentInstanceStableIds,
            int initialSlotIndex = 0)
        {
            var slots = new List<StableId>(
                equipmentInstanceStableIds
                ?? throw new ArgumentNullException(nameof(equipmentInstanceStableIds)));
            ValidateSlots(slots, initialSlotIndex);
            this.equipmentInstanceStableIds = new ReadOnlyCollection<StableId>(slots);
            selectedSlotIndex = initialSlotIndex;
        }

        public Stage1ProductionWeaponSlotSelectionV1(
            LevelRunCoordinatorV1 coordinator)
            : this(ReadEquipmentInstanceIds(coordinator), coordinator.ActiveSlotIndex)
        {
            this.coordinator = coordinator;
        }

        public IReadOnlyList<StableId> EquipmentInstanceStableIds
        {
            get { return equipmentInstanceStableIds; }
        }

        public int SelectedSlotIndex
        {
            get { return coordinator == null ? selectedSlotIndex : coordinator.ActiveSlotIndex; }
        }

        public StableId SelectedEquipmentInstanceStableId
        {
            get { return equipmentInstanceStableIds[SelectedSlotIndex]; }
        }

        public Stage1WeaponSlotSelectionStatusV1 SelectSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                return Stage1WeaponSlotSelectionStatusV1.InvalidSlot;
            }

            if (slotIndex == SelectedSlotIndex)
            {
                return Stage1WeaponSlotSelectionStatusV1.ExactDuplicateNoChange;
            }

            if (coordinator != null)
            {
                return coordinator.TrySelectActiveSlot(slotIndex)
                    ? Stage1WeaponSlotSelectionStatusV1.Selected
                    : Stage1WeaponSlotSelectionStatusV1.InvalidLoadout;
            }

            selectedSlotIndex = slotIndex;
            return Stage1WeaponSlotSelectionStatusV1.Selected;
        }

        private static List<StableId> ReadEquipmentInstanceIds(
            LevelRunCoordinatorV1 coordinator)
        {
            if (coordinator == null)
            {
                throw new ArgumentNullException(nameof(coordinator));
            }

            var ids = new List<StableId>(coordinator.Loadout.Slots.Count);
            for (int index = 0; index < coordinator.Loadout.Slots.Count; index++)
            {
                ids.Add(coordinator.Loadout.Slots[index].EquipmentInstanceStableId);
            }

            return ids;
        }

        private static void ValidateSlots(
            IReadOnlyList<StableId> slots,
            int initialSlotIndex)
        {
            if (slots.Count != SlotCount)
            {
                throw new ArgumentException(
                    "Exactly four equipment-instance identities are required.",
                    nameof(slots));
            }

            var unique = new HashSet<StableId>();
            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index] == null)
                {
                    throw new ArgumentException(
                        "Equipment-instance identities cannot contain null.",
                        nameof(slots));
                }

                if (!unique.Add(slots[index]))
                {
                    throw new ArgumentException(
                        "Each routed weapon slot must reference a distinct equipment instance.",
                        nameof(slots));
                }
            }

            if (initialSlotIndex < 0 || initialSlotIndex >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(initialSlotIndex));
            }
        }
    }
}
