using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterMover.UI.Common
{
    /// <summary>
    /// Presentation-only augment capacity grid. Empty cells represent available
    /// capacity; filled cells represent installed augments. It never mutates the
    /// equipment or augment authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AugmentSlotGrid : MonoBehaviour
    {
        [SerializeField] private RectTransform slotsRoot;
        [SerializeField] private Image slotPrefab;
        [SerializeField] private Sprite emptySprite;
        [SerializeField] private Sprite filledSprite;
        [SerializeField] private int columns = 7;

        private readonly List<Image> slots = new List<Image>();

        public void Show(WeaponCardView view)
        {
            if (view == null || !view.HasAugmentSlots)
            {
                Clear();
                return;
            }

            Show(
                view.AugmentSlotCapacity,
                view.InstalledAugmentCount);
        }

        public void Show(int capacity, int installed)
        {
            if (capacity < 0 || installed < 0 || installed > capacity)
            {
                Clear();
                return;
            }
            EnsureSlots(capacity);
            for (int index = 0; index < slots.Count; index++)
            {
                bool visible = index < capacity;
                Image slot = slots[index];
                slot.gameObject.SetActive(visible);
                if (visible)
                {
                    slot.sprite = index < installed
                        ? filledSprite
                        : emptySprite;
                    slot.enabled = slot.sprite != null;
                }
            }
            gameObject.SetActive(capacity > 0);
        }

        public void Clear()
        {
            for (int index = 0; index < slots.Count; index++)
            {
                slots[index].gameObject.SetActive(false);
            }
            gameObject.SetActive(false);
        }

        public void Configure(
            RectTransform root,
            Image prefab,
            Sprite empty,
            Sprite filled)
        {
            slotsRoot = root;
            slotPrefab = prefab;
            emptySprite = empty;
            filledSprite = filled;
        }

        private void Awake()
        {
            if (slotsRoot == null)
            {
                slotsRoot = transform as RectTransform;
            }
        }

        private void EnsureSlots(int count)
        {
            if (slotsRoot == null || slotPrefab == null)
            {
                throw new InvalidOperationException(
                    "AugmentSlotGrid requires a slots root and slot prefab.");
            }

            GridLayoutGroup grid = slotsRoot.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Math.Max(1, columns);
            }

            while (slots.Count < count)
            {
                Image slot = Instantiate(slotPrefab, slotsRoot);
                slot.name = "AugmentSlot_" + (slots.Count + 1);
                slots.Add(slot);
            }
        }
    }
}
