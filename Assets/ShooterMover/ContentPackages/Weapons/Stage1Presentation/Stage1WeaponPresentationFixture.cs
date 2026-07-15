using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Weapons.Stage1Presentation
{
    public static class Stage1WeaponPresentationFixture
    {
        public static FourMountStatusSnapshot CreateBeforeSpendSnapshot()
        {
            return new FourMountStatusSnapshot(
                Slot(1, "weapon.blaster-machine-gun", WeaponMountPhase.Ready, 0d, 0d, 4d, 4d, true, FourMountFireMode.NoRecentAttempt),
                Slot(2, "weapon.shotgun", WeaponMountPhase.Ready, 0d, 0d, 9d, 12d, true, FourMountFireMode.NoRecentAttempt),
                Slot(3, "weapon.rocket-launcher", WeaponMountPhase.Ready, 0d, 0d, 0d, 10d, false, FourMountFireMode.NoRecentAttempt),
                Slot(4, "weapon.arc-gun", WeaponMountPhase.Ready, 0d, 0d, 2d, 12d, false, FourMountFireMode.NoRecentAttempt));
        }

        public static FourMountStatusSnapshot CreateRepresentativeSnapshot()
        {
            return new FourMountStatusSnapshot(
                Slot(1, "weapon.blaster-machine-gun", WeaponMountPhase.Ready, 0d, 0d, 3d, 4d, true, FourMountFireMode.Empowered),
                Slot(2, "weapon.shotgun", WeaponMountPhase.Recovering, 0.18d, 0.35d, 6d, 12d, true, FourMountFireMode.Empowered),
                Slot(3, "weapon.rocket-launcher", WeaponMountPhase.Ready, 0d, 0d, 0d, 10d, false, FourMountFireMode.NormalFallbackPowerUnavailable),
                Fault(4, "weapon.arc-gun"));
        }

        public static FourMountStatusSnapshot CreateRicochetIdentitySnapshot()
        {
            return new FourMountStatusSnapshot(
                Slot(1, "weapon.ricochet-gun", WeaponMountPhase.Firing, 0.42d, 0d, 8d, 10d, true, FourMountFireMode.Normal),
                FourMountSlotStatusSnapshot.Unequipped(2),
                FourMountSlotStatusSnapshot.Unequipped(3),
                FourMountSlotStatusSnapshot.Unequipped(4));
        }

        public static FourMountSlotStatusSnapshot Slot(
            int number, string weaponId, WeaponMountPhase phase,
            double cadence, double recovery, double power, double capacity,
            bool canEmpower, FourMountFireMode mode)
        {
            return new FourMountSlotStatusSnapshot(
                number, true, StableId.Parse(weaponId), phase,
                phase == WeaponMountPhase.Ready, cadence, 0, recovery,
                WeaponCycleMode.None, 0d, 0d, true, power, capacity,
                canEmpower, mode, null, null);
        }

        public static FourMountSlotStatusSnapshot Fault(int number, string weaponId)
        {
            return new FourMountSlotStatusSnapshot(
                number, true, StableId.Parse(weaponId), WeaponMountPhase.Faulted,
                false, 0d, 0, 0d, WeaponCycleMode.None, 0d, 0d,
                true, 2d, 12d, false, FourMountFireMode.Faulted,
                WeaponMountFaultKind.ExternalFault, "representative fixture fault");
        }
    }
}
