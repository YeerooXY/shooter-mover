using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Engine-independent four-slot selection state for the production Stage 1 loadout.
    /// It retains concrete equipment-instance identities and owns no inventory truth.
    /// </summary>
    public sealed class Stage1ProductionWeaponSlotSelectionV1
    {
        public const int SlotCount = 4;

        private readonly ReadOnlyCollection<StableId> equipmentInstanceStableIds;
        private int selectedSlotIndex;

        public Stage1ProductionWeaponSlotSelectionV1(
            IEnumerable<StableId> equipmentInstanceStableIds,
            int initialSlotIndex = 0)
        {
            var slots = new List<StableId>(
                equipmentInstanceStableIds
                ?? throw new ArgumentNullException(nameof(equipmentInstanceStableIds)));
            if (slots.Count != SlotCount)
            {
                throw new ArgumentException(
                    "Exactly four equipment-instance identities are required.",
                    nameof(equipmentInstanceStableIds));
            }

            var unique = new HashSet<StableId>();
            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index] == null)
                {
                    throw new ArgumentException(
                        "Equipment-instance identities cannot contain null.",
                        nameof(equipmentInstanceStableIds));
                }

                if (!unique.Add(slots[index]))
                {
                    throw new ArgumentException(
                        "Each routed weapon slot must reference a distinct equipment instance.",
                        nameof(equipmentInstanceStableIds));
                }
            }

            if (initialSlotIndex < 0 || initialSlotIndex >= SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(initialSlotIndex));
            }

            this.equipmentInstanceStableIds = new ReadOnlyCollection<StableId>(slots);
            selectedSlotIndex = initialSlotIndex;
        }

        public IReadOnlyList<StableId> EquipmentInstanceStableIds
        {
            get { return equipmentInstanceStableIds; }
        }

        public int SelectedSlotIndex { get { return selectedSlotIndex; } }

        public StableId SelectedEquipmentInstanceStableId
        {
            get { return equipmentInstanceStableIds[selectedSlotIndex]; }
        }

        public Stage1WeaponSlotSelectionStatusV1 SelectSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                return Stage1WeaponSlotSelectionStatusV1.InvalidSlot;
            }

            if (slotIndex == selectedSlotIndex)
            {
                return Stage1WeaponSlotSelectionStatusV1.ExactDuplicateNoChange;
            }

            selectedSlotIndex = slotIndex;
            return Stage1WeaponSlotSelectionStatusV1.Selected;
        }
    }
}
