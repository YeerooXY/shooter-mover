using System;
using System.Globalization;

namespace ShooterMover.Domain.Combat
{
    /// <summary>
    /// Immutable state for exactly four independently simulated gun mounts.
    /// Stable index zero through three corresponds to authored mount slot one through four.
    /// </summary>
    public sealed class FourMountCombatState
    {
        public const int MountCount = GunLiveProfile.SupportedMountCount;

        private readonly GunMountState[] mountStates;
        private readonly GunPowerBankState[] powerBankStates;

        public FourMountCombatState(
            GunMountState[] mountStates,
            GunPowerBankState[] powerBankStates)
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

            this.mountStates = new GunMountState[MountCount];
            this.powerBankStates = new GunPowerBankState[MountCount];
            for (int index = 0; index < MountCount; index++)
            {
                this.mountStates[index] = mountStates[index]
                    ?? throw new ArgumentException("Mount states cannot contain null.", nameof(mountStates));
                this.powerBankStates[index] = powerBankStates[index]
                    ?? throw new ArgumentException("Power-bank states cannot contain null.", nameof(powerBankStates));
            }
        }

        public static FourMountCombatState Initial(
            GunLiveProfile[] profiles,
            double[] initialPowerUnits)
        {
            ValidateFour(profiles, nameof(profiles));
            if (initialPowerUnits == null || initialPowerUnits.Length != MountCount)
            {
                throw new ArgumentException("Exactly four initial power values are required.", nameof(initialPowerUnits));
            }

            GunMountState[] mounts = new GunMountState[MountCount];
            GunPowerBankState[] banks = new GunPowerBankState[MountCount];
            for (int index = 0; index < MountCount; index++)
            {
                mounts[index] = GunMountState.Initial(profiles[index]);
                banks[index] = GunPowerBankState.FromProfile(profiles[index], initialPowerUnits[index]);
            }

            return new FourMountCombatState(mounts, banks);
        }

        public GunMountState GetMountByStableIndex(int stableIndex)
        {
            ValidateIndex(stableIndex);
            return mountStates[stableIndex];
        }

        public GunPowerBankState GetPowerBankByStableIndex(int stableIndex)
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
