using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Presentation
{
    /// <summary>
    /// One immutable HUD row for a stable mount slot. Presentation consumes this
    /// read model; it does not mutate weapon state or decide fire behavior.
    /// </summary>
    public sealed class WeaponHudSlotState
    {
        public WeaponHudSlotState(
            WeaponMountState mount,
            WeaponMountFireResult latestFireResult)
        {
            if (mount == null)
            {
                throw new ArgumentNullException(nameof(mount));
            }

            if (latestFireResult != null)
            {
                if (latestFireResult.Slot != mount.Slot)
                {
                    throw new ArgumentException(
                        "HUD fire result slot must match the mount slot.",
                        nameof(latestFireResult));
                }

                if (latestFireResult.WeaponId != mount.WeaponId)
                {
                    throw new ArgumentException(
                        "HUD fire result weapon identity must match the mount snapshot.",
                        nameof(latestFireResult));
                }
            }

            Mount = mount;
            LatestFireResult = latestFireResult;
        }

        public WeaponMountState Mount { get; }

        public WeaponMountFireResult LatestFireResult { get; }

        public WeaponMountSlot Slot => Mount.Slot;

        public int HudIndex => WeaponMountContractRules.GetHudIndex(Slot);

        public bool IsEquipped => Mount.IsEquipped;

        public StableId WeaponId => Mount.WeaponId;

        public WeaponMountReadiness Readiness => Mount.Readiness;

        public WeaponCadenceState Cadence => Mount.Cadence;

        public WeaponCycleResourceState CycleResource => Mount.CycleResource;

        public WeaponRecoilState Recoil => Mount.Recoil;

        public WeaponPowerBankState PowerBank => Mount.PowerBank;
    }

    /// <summary>
    /// Deterministically ordered four-row weapon HUD snapshot.
    /// </summary>
    public sealed class WeaponHudState
    {
        private readonly WeaponHudSlotState[] slots;

        public WeaponHudState(FourMountWeaponState mounts)
            : this(mounts, null)
        {
        }

        public WeaponHudState(
            FourMountWeaponState mounts,
            FourMountFireResult latestFireResult)
        {
            if (mounts == null)
            {
                throw new ArgumentNullException(nameof(mounts));
            }

            slots = new WeaponHudSlotState[WeaponMountContractRules.MountCount];
            for (int hudIndex = 0; hudIndex < slots.Length; hudIndex++)
            {
                WeaponMountState mount = mounts.GetByHudIndex(hudIndex);
                WeaponMountFireResult fireResult = latestFireResult == null
                    ? null
                    : latestFireResult.GetByHudIndex(hudIndex);

                slots[hudIndex] = new WeaponHudSlotState(mount, fireResult);
            }
        }

        public int Count => WeaponMountContractRules.MountCount;

        public WeaponHudSlotState GetByHudIndex(int hudIndex)
        {
            if (hudIndex < 0 || hudIndex >= slots.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(hudIndex));
            }

            return slots[hudIndex];
        }

        public WeaponHudSlotState GetBySlot(WeaponMountSlot slot)
        {
            return slots[WeaponMountContractRules.GetHudIndex(slot)];
        }
    }
}
