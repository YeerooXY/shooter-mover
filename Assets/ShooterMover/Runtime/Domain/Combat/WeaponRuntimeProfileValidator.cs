using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Combat
{
    /// <summary>
    /// Structural validation for WeaponRuntimeProfile v1. These bounds reject malformed
    /// runtime tuning without choosing final balance for any weapon package.
    /// </summary>
    public static class WeaponRuntimeProfileValidator
    {
        public const int MaximumBurstShotCount = 64;
        public const int MaximumBehaviorModuleCount = 16;
        public const int MaximumPresentationPriority = 1024;

        private const double MinimumPositiveSeconds = 0.000001d;
        private const double MaximumDurationSeconds = 600d;
        private const double MaximumResourceUnits = 1000000000d;

        public static void Validate(
            WeaponRuntimeProfile profile,
            IEnumerable<StableId> knownBehaviorModuleIds)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            ValidateValues(
                profile.ProfileVersion,
                profile.ProfileId,
                profile.CadenceSeconds,
                profile.BurstShotCount,
                profile.BurstShotIntervalSeconds,
                profile.RecoverySeconds,
                profile.CycleMode,
                profile.HeatCapacityUnits,
                profile.HeatPerShotUnits,
                profile.HeatRecoveryUnitsPerSecond,
                profile.ChargeSeconds,
                profile.HasIndependentPowerBank,
                profile.PowerBankCapacityUnits,
                profile.EmpoweredCostUnits,
                profile.RecoilInfluence,
                profile.CopyBehaviorModuleIdsForValidation(),
                knownBehaviorModuleIds,
                profile.PresentationPriority);
        }

        internal static void ValidateValues(
            int profileVersion,
            StableId profileId,
            double cadenceSeconds,
            int burstShotCount,
            double burstShotIntervalSeconds,
            double recoverySeconds,
            WeaponCycleMode cycleMode,
            double heatCapacityUnits,
            double heatPerShotUnits,
            double heatRecoveryUnitsPerSecond,
            double chargeSeconds,
            bool hasIndependentPowerBank,
            double powerBankCapacityUnits,
            double empoweredCostUnits,
            double recoilInfluence,
            StableId[] behaviorModuleIds,
            IEnumerable<StableId> knownBehaviorModuleIds,
            int presentationPriority)
        {
            if (profileVersion != WeaponRuntimeProfile.CurrentProfileVersion)
            {
                throw new NotSupportedException(
                    "Unsupported weapon runtime profile version: " + profileVersion + ".");
            }

            if (profileId == null)
            {
                throw new ArgumentNullException(nameof(profileId));
            }

            RequireInRange(
                nameof(cadenceSeconds),
                cadenceSeconds,
                MinimumPositiveSeconds,
                MaximumDurationSeconds);
            RequireIntegerInRange(
                nameof(burstShotCount),
                burstShotCount,
                1,
                MaximumBurstShotCount);
            ValidateBurstTiming(cadenceSeconds, burstShotCount, burstShotIntervalSeconds);
            RequireInRange(nameof(recoverySeconds), recoverySeconds, 0d, MaximumDurationSeconds);

            if (!Enum.IsDefined(typeof(WeaponCycleMode), cycleMode))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cycleMode),
                    cycleMode,
                    "Unknown weapon cycle mode.");
            }

            ValidateCycleMode(
                cycleMode,
                heatCapacityUnits,
                heatPerShotUnits,
                heatRecoveryUnitsPerSecond,
                chargeSeconds);
            ValidatePowerBank(
                hasIndependentPowerBank,
                powerBankCapacityUnits,
                empoweredCostUnits);
            RequireInRange(nameof(recoilInfluence), recoilInfluence, 0d, 1d);
            ValidateBehaviorModules(behaviorModuleIds, knownBehaviorModuleIds);
            RequireIntegerInRange(
                nameof(presentationPriority),
                presentationPriority,
                0,
                MaximumPresentationPriority);
        }

        private static void ValidateBurstTiming(
            double cadenceSeconds,
            int burstShotCount,
            double burstShotIntervalSeconds)
        {
            RequireFinite(nameof(burstShotIntervalSeconds), burstShotIntervalSeconds);

            if (burstShotCount == 1)
            {
                if (burstShotIntervalSeconds != 0d)
                {
                    throw new ArgumentException(
                        "A non-burst profile must use a zero burst-shot interval.",
                        nameof(burstShotIntervalSeconds));
                }

                return;
            }

            if (burstShotIntervalSeconds < MinimumPositiveSeconds
                || burstShotIntervalSeconds > cadenceSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(burstShotIntervalSeconds),
                    burstShotIntervalSeconds,
                    "A burst interval must be positive and cannot exceed cadence.");
            }

            double burstDurationSeconds = (burstShotCount - 1) * burstShotIntervalSeconds;
            RequireFinite(nameof(burstDurationSeconds), burstDurationSeconds);
            if (burstDurationSeconds > MaximumDurationSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(burstShotIntervalSeconds),
                    burstShotIntervalSeconds,
                    "The complete authored burst cannot exceed the maximum supported duration.");
            }
        }

        private static void ValidateCycleMode(
            WeaponCycleMode cycleMode,
            double heatCapacityUnits,
            double heatPerShotUnits,
            double heatRecoveryUnitsPerSecond,
            double chargeSeconds)
        {
            RequireInRange(
                nameof(heatCapacityUnits),
                heatCapacityUnits,
                0d,
                MaximumResourceUnits);
            RequireInRange(
                nameof(heatPerShotUnits),
                heatPerShotUnits,
                0d,
                MaximumResourceUnits);
            RequireInRange(
                nameof(heatRecoveryUnitsPerSecond),
                heatRecoveryUnitsPerSecond,
                0d,
                MaximumResourceUnits);
            RequireInRange(nameof(chargeSeconds), chargeSeconds, 0d, MaximumDurationSeconds);

            if (cycleMode == WeaponCycleMode.None)
            {
                if (heatCapacityUnits != 0d
                    || heatPerShotUnits != 0d
                    || heatRecoveryUnitsPerSecond != 0d
                    || chargeSeconds != 0d)
                {
                    throw new ArgumentException(
                        "A profile without a cycle resource must use zero heat and charge values.");
                }

                return;
            }

            if (cycleMode == WeaponCycleMode.Heat)
            {
                if (chargeSeconds != 0d)
                {
                    throw new ArgumentException(
                        "Heat and charge modes are mutually exclusive.",
                        nameof(chargeSeconds));
                }

                RequirePositiveResource(nameof(heatCapacityUnits), heatCapacityUnits);
                RequirePositiveResource(nameof(heatPerShotUnits), heatPerShotUnits);
                RequirePositiveResource(
                    nameof(heatRecoveryUnitsPerSecond),
                    heatRecoveryUnitsPerSecond);

                if (heatPerShotUnits > heatCapacityUnits)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(heatPerShotUnits),
                        heatPerShotUnits,
                        "Heat per shot cannot exceed heat capacity.");
                }

                return;
            }

            if (heatCapacityUnits != 0d
                || heatPerShotUnits != 0d
                || heatRecoveryUnitsPerSecond != 0d)
            {
                throw new ArgumentException(
                    "Charge and heat modes are mutually exclusive.",
                    nameof(heatCapacityUnits));
            }

            if (chargeSeconds < MinimumPositiveSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chargeSeconds),
                    chargeSeconds,
                    "A charge profile requires a positive charge duration.");
            }
        }

        private static void ValidatePowerBank(
            bool hasIndependentPowerBank,
            double powerBankCapacityUnits,
            double empoweredCostUnits)
        {
            RequireInRange(
                nameof(powerBankCapacityUnits),
                powerBankCapacityUnits,
                0d,
                MaximumResourceUnits);
            RequireInRange(
                nameof(empoweredCostUnits),
                empoweredCostUnits,
                0d,
                MaximumResourceUnits);

            if (!hasIndependentPowerBank)
            {
                if (powerBankCapacityUnits != 0d || empoweredCostUnits != 0d)
                {
                    throw new ArgumentException(
                        "A profile without an independent power bank must use zero capacity and cost.");
                }

                return;
            }

            RequirePositiveResource(nameof(powerBankCapacityUnits), powerBankCapacityUnits);
            RequirePositiveResource(nameof(empoweredCostUnits), empoweredCostUnits);

            if (empoweredCostUnits > powerBankCapacityUnits)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(empoweredCostUnits),
                    empoweredCostUnits,
                    "Empowered cost cannot exceed independent power-bank capacity.");
            }
        }

        private static void ValidateBehaviorModules(
            StableId[] behaviorModuleIds,
            IEnumerable<StableId> knownBehaviorModuleIds)
        {
            if (behaviorModuleIds == null)
            {
                throw new ArgumentNullException(nameof(behaviorModuleIds));
            }

            if (knownBehaviorModuleIds == null)
            {
                throw new ArgumentNullException(nameof(knownBehaviorModuleIds));
            }

            RequireIntegerInRange(
                nameof(behaviorModuleIds),
                behaviorModuleIds.Length,
                1,
                MaximumBehaviorModuleCount);

            HashSet<StableId> knownIds = new HashSet<StableId>();
            foreach (StableId knownId in knownBehaviorModuleIds)
            {
                if (knownId == null)
                {
                    throw new ArgumentException(
                        "Known behavior-module IDs cannot contain null.",
                        nameof(knownBehaviorModuleIds));
                }

                knownIds.Add(knownId);
            }

            HashSet<StableId> authoredIds = new HashSet<StableId>();
            for (int index = 0; index < behaviorModuleIds.Length; index++)
            {
                StableId moduleId = behaviorModuleIds[index];
                if (moduleId == null)
                {
                    throw new ArgumentException(
                        "Behavior-module IDs cannot contain null.",
                        nameof(behaviorModuleIds));
                }

                if (!authoredIds.Add(moduleId))
                {
                    throw new ArgumentException(
                        "Behavior-module IDs cannot repeat: " + moduleId + ".",
                        nameof(behaviorModuleIds));
                }

                if (!knownIds.Contains(moduleId))
                {
                    throw new ArgumentException(
                        "Unknown behavior-module StableId: " + moduleId + ".",
                        nameof(behaviorModuleIds));
                }
            }
        }

        private static void RequirePositiveResource(string fieldName, double value)
        {
            if (value <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    fieldName,
                    value,
                    fieldName + " must be greater than zero when configured.");
            }
        }

        private static void RequireInRange(
            string fieldName,
            double value,
            double minimum,
            double maximum)
        {
            RequireFinite(fieldName, value);
            if (value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    fieldName,
                    value,
                    fieldName + " must be between " + minimum + " and " + maximum + ".");
            }
        }

        private static void RequireIntegerInRange(
            string fieldName,
            int value,
            int minimum,
            int maximum)
        {
            if (value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    fieldName,
                    value,
                    fieldName + " must be between " + minimum + " and " + maximum + ".");
            }
        }

        private static void RequireFinite(string fieldName, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    fieldName,
                    value,
                    fieldName + " must be finite.");
            }
        }
    }
}
