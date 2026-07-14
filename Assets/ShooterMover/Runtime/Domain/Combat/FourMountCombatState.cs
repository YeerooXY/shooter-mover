using System;
using System.Globalization;

namespace ShooterMover.Domain.Combat
{
    /// <summary>
    /// Immutable state for exactly four independently simulated weapon mounts.
    /// Stable index zero through three corresponds to authored mount slot one through four.
    /// </summary>
    public sealed class FourMountCombatState
    {
        public const int MountCount = WeaponRuntimeProfile.SupportedMountCount;

        private readonly WeaponMountState[] mountStates;
        private readonly WeaponPowerBankState[] powerBankStates;

        public FourMountCombatState(
            WeaponMountState[] mountStates,
            WeaponPowerBankState[] powerBankStates)
        {
            if (mountStates == null)
            {
                throw new ArgumentNullException(nameof(mountStates));
            }

            if (powerBankStates == null)
            {
                throw new ArgumentNullException(nameof(powerBankStates));
            }

            if (mountStates.Length != MountCount || powerBankStates.Length != MountCount)
            {
                throw new ArgumentException("Exactly four mount states and four power banks are required.");
            }

            this.mountStates = new WeaponMountState[MountCount];
            this.powerBankStates = new WeaponPowerBankState[MountCount];
            for (int index = 0; index < MountCount; index++)
            {
                this.mountStates[index] = mountStates[index]
                    ?? throw new ArgumentException("Mount states cannot contain null.", nameof(mountStates));
                this.powerBankStates[index] = powerBankStates[index]
                    ?? throw new ArgumentException("Power-bank states cannot contain null.", nameof(powerBankStates));
            }
        }

        public static FourMountCombatState Initial(
            WeaponRuntimeProfile[] profiles,
            double[] initialPowerUnits)
        {
            ValidateFour(profiles, nameof(profiles));
            if (initialPowerUnits == null || initialPowerUnits.Length != MountCount)
            {
                throw new ArgumentException("Exactly four initial power values are required.", nameof(initialPowerUnits));
            }

            WeaponMountState[] mounts = new WeaponMountState[MountCount];
            WeaponPowerBankState[] banks = new WeaponPowerBankState[MountCount];
            for (int index = 0; index < MountCount; index++)
            {
                mounts[index] = WeaponMountState.Initial(profiles[index]);
                banks[index] = WeaponPowerBankState.FromProfile(profiles[index], initialPowerUnits[index]);
            }

            return new FourMountCombatState(mounts, banks);
        }

        public WeaponMountState GetMountByStableIndex(int stableIndex)
        {
            ValidateIndex(stableIndex);
            return mountStates[stableIndex];
        }

        public WeaponPowerBankState GetPowerBankByStableIndex(int stableIndex)
        {
            ValidateIndex(stableIndex);
            return powerBankStates[stableIndex];
        }

        public string ToTraceString()
        {
            string[] lanes = new string[MountCount];
            for (int index = 0; index < MountCount; index++)
            {
                lanes[index] = string.Format(
                    CultureInfo.InvariantCulture,
                    "slot={0};power={1:R};{2}",
                    index + 1,
                    powerBankStates[index].AvailableUnits,
                    mountStates[index].ToTraceString());
            }

            return string.Join("\n", lanes);
        }

        internal static void ValidateFour<T>(T[] values, string parameterName)
            where T : class
        {
            if (values == null || values.Length != MountCount)
            {
                throw new ArgumentException("Exactly four values are required.", parameterName);
            }

            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == null)
                {
                    throw new ArgumentException("The four-slot array cannot contain null.", parameterName);
                }
            }
        }

        private static void ValidateIndex(int stableIndex)
        {
            if (stableIndex < 0 || stableIndex >= MountCount)
            {
                throw new ArgumentOutOfRangeException(nameof(stableIndex));
            }
        }
    }
}
