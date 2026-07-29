using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Presentation
{
    /// <summary>
    /// One immutable HUD row for a stable mount slot. Presentation consumes this
    /// read model; it does not mutate gun state or decide fire behavior.
    /// </summary>
    public sealed class GunHudSlotState
    {
        public GunHudSlotState(
            GunMountState mount,
            GunMountFireResult latestFireResult)
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

                if (latestFireResult.GunId != mount.GunId)
                {
                    throw new ArgumentException(
                        "HUD fire result gun identity must match the mount snapshot.",
                        nameof(latestFireResult));
                }
            }

            Mount = mount;
            LatestFireResult = latestFireResult;
        }

        public GunMountState Mount { get; }

        public GunMountFireResult LatestFireResult { get; }

        public GunMountSlot Slot => Mount.Slot;

        public int HudIndex => GunMountContractRules.GetHudIndex(Slot);

        public bool IsEquipped => Mount.IsEquipped;

        public StableId GunId => Mount.GunId;

        public GunMountReadiness Readiness => Mount.Readiness;

        public GunCadenceState Cadence => Mount.Cadence;

        public GunCycleResourceState CycleResource => Mount.CycleResource;

        public GunRecoilState Recoil => Mount.Recoil;

        public GunPowerBankState PowerBank => Mount.PowerBank;
    }

    /// <summary>
    /// Deterministically ordered four-row gun HUD snapshot.
    /// </summary>
    public sealed class GunHudState
    {
        private readonly GunHudSlotState[] slots;

        public GunHudState(FourMountGunState mounts)
            : this(mounts, null)
        {
        }

        public GunHudState(
            FourMountGunState mounts,
            FourMountFireResult latestFireResult)
        {
            if (mounts == null)
            {
                throw new ArgumentNullException(nameof(mounts));
            }

            slots = new GunHudSlotState[GunMountContractRules.MountCount];
            for (int hudIndex = 0; hudIndex < slots.Length; hudIndex++)
            {
                GunMountState mount = mounts.GetByHudIndex(hudIndex);
                GunMountFireResult fireResult = latestFireResult == null
                    ? null
                    : latestFireResult.GetByHudIndex(hudIndex);

                slots[hudIndex] = new GunHudSlotState(mount, fireResult);
            }
        }

        public int Count => GunMountContractRules.MountCount;

        public GunHudSlotState GetByHudIndex(int hudIndex)
        {
            if (hudIndex < 0 || hudIndex >= slots.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(hudIndex));
            }

            return slots[hudIndex];
        }

        public GunHudSlotState GetBySlot(GunMountSlot slot)
        {
            return slots[GunMountContractRules.GetHudIndex(slot)];
        }
    }
}
