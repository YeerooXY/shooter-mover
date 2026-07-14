using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Movement
{
    /// <summary>
    /// Structural validation for MovementThrusterTuningProfile v1.
    /// Bounds reject malformed or unsafe coefficients without providing a mature balance preset.
    /// </summary>
    public static class MovementThrusterTuningProfileValidator
    {
        public const int MaximumBaselineChargeCount = 8;
        public const int MaximumAdditionalChargeCount = 1;
        public const int MaximumWallReflectionContacts = 32;
        public const int MaximumContactGraceCapacity = 4096;

        public static void Validate(MovementThrusterTuningProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            ValidateValues(
                profile.ProfileVersion,
                profile.ProfileId,
                profile.BaseMaximumSpeed,
                profile.BaseAcceleration,
                profile.BaseBraking,
                profile.BaseCounterSteerBraking,
                profile.BaseVelocityResponseExponent,
                profile.ThrusterBaselineChargeCount,
                profile.ThrusterMaximumAdditionalCharges,
                profile.ThrusterRechargeSeconds,
                profile.ThrusterSpeedMultiplier,
                profile.ThrusterBurstDurationSeconds,
                profile.ThrusterDirectionInputThreshold,
                profile.ThrusterMinimumChainIntervalSeconds,
                profile.ThrusterSteeringDegreesPerSecond,
                profile.ThrusterStartupForgivenessSeconds,
                profile.ThrusterExitMomentumSeconds,
                profile.ThrusterExitSpeedRetention,
                profile.ThrusterExitDecayExponent,
                profile.WallReflectionSpeedRetention,
                profile.WallReflectionInputInfluence,
                profile.WallReflectionMinimumSpeed,
                profile.WallReflectionMaximumContacts,
                profile.LightContactMomentumRetention,
                profile.LightContactSteeringRetention,
                profile.HeavyContactMomentumRetention,
                profile.PerEnemyContactGraceSeconds,
                profile.SimultaneousContactWindowSeconds,
                profile.ContactGraceCapacity);
        }

        internal static void ValidateValues(
            int profileVersion,
            StableId profileId,
            double baseMaximumSpeed,
            double baseAcceleration,
            double baseBraking,
            double baseCounterSteerBraking,
            double baseVelocityResponseExponent,
            int thrusterBaselineChargeCount,
            int thrusterMaximumAdditionalCharges,
            double thrusterRechargeSeconds,
            double thrusterSpeedMultiplier,
            double thrusterBurstDurationSeconds,
            double thrusterDirectionInputThreshold,
            double thrusterMinimumChainIntervalSeconds,
            double thrusterSteeringDegreesPerSecond,
            double thrusterStartupForgivenessSeconds,
            double thrusterExitMomentumSeconds,
            double thrusterExitSpeedRetention,
            double thrusterExitDecayExponent,
            double wallReflectionSpeedRetention,
            double wallReflectionInputInfluence,
            double wallReflectionMinimumSpeed,
            int wallReflectionMaximumContacts,
            double lightContactMomentumRetention,
            double lightContactSteeringRetention,
            double heavyContactMomentumRetention,
            double perEnemyContactGraceSeconds,
            double simultaneousContactWindowSeconds,
            int contactGraceCapacity)
        {
            if (profileVersion != MovementThrusterTuningProfile.CurrentProfileVersion)
            {
                throw new NotSupportedException(
                    "Unsupported movement-thruster tuning profile version: " + profileVersion + ".");
            }

            if (profileId == null)
            {
                throw new ArgumentNullException(nameof(profileId));
            }

            RequirePositive(nameof(baseMaximumSpeed), baseMaximumSpeed);
            RequirePositive(nameof(baseAcceleration), baseAcceleration);
            RequirePositive(nameof(baseBraking), baseBraking);
            RequirePositive(nameof(baseCounterSteerBraking), baseCounterSteerBraking);
            if (baseCounterSteerBraking < baseBraking)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(baseCounterSteerBraking),
                    baseCounterSteerBraking,
                    "Counter-steer braking must be at least as strong as ordinary braking.");
            }

            RequireInRange(
                nameof(baseVelocityResponseExponent),
                baseVelocityResponseExponent,
                0.01d,
                16d);

            RequireIntegerInRange(
                nameof(thrusterBaselineChargeCount),
                thrusterBaselineChargeCount,
                1,
                MaximumBaselineChargeCount);
            RequireIntegerInRange(
                nameof(thrusterMaximumAdditionalCharges),
                thrusterMaximumAdditionalCharges,
                0,
                MaximumAdditionalChargeCount);
            RequireInRange(nameof(thrusterRechargeSeconds), thrusterRechargeSeconds, 0.001d, 600d);
            RequireInRange(nameof(thrusterSpeedMultiplier), thrusterSpeedMultiplier, 0.01d, 16d);
            RequireInRange(
                nameof(thrusterBurstDurationSeconds),
                thrusterBurstDurationSeconds,
                0.001d,
                10d);
            RequireInRange(
                nameof(thrusterDirectionInputThreshold),
                thrusterDirectionInputThreshold,
                0d,
                0.999999d);
            RequireInRange(
                nameof(thrusterMinimumChainIntervalSeconds),
                thrusterMinimumChainIntervalSeconds,
                0d,
                thrusterBurstDurationSeconds);
            RequireInRange(
                nameof(thrusterSteeringDegreesPerSecond),
                thrusterSteeringDegreesPerSecond,
                0d,
                1440d);
            RequireInRange(
                nameof(thrusterStartupForgivenessSeconds),
                thrusterStartupForgivenessSeconds,
                0d,
                thrusterBurstDurationSeconds);
            RequireInRange(
                nameof(thrusterExitMomentumSeconds),
                thrusterExitMomentumSeconds,
                0d,
                10d);
            RequireInRange(
                nameof(thrusterExitSpeedRetention),
                thrusterExitSpeedRetention,
                0d,
                1d);
            RequireInRange(
                nameof(thrusterExitDecayExponent),
                thrusterExitDecayExponent,
                0.01d,
                16d);

            RequireInRange(
                nameof(wallReflectionSpeedRetention),
                wallReflectionSpeedRetention,
                0d,
                1d);
            RequireInRange(
                nameof(wallReflectionInputInfluence),
                wallReflectionInputInfluence,
                0d,
                1d);
            RequireNonNegative(nameof(wallReflectionMinimumSpeed), wallReflectionMinimumSpeed);
            double maximumBurstSpeed = baseMaximumSpeed * thrusterSpeedMultiplier;
            RequireFinite(nameof(maximumBurstSpeed), maximumBurstSpeed);
            if (wallReflectionMinimumSpeed > maximumBurstSpeed)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wallReflectionMinimumSpeed),
                    wallReflectionMinimumSpeed,
                    "Wall reflection minimum speed cannot exceed the profile's maximum burst speed.");
            }

            RequireIntegerInRange(
                nameof(wallReflectionMaximumContacts),
                wallReflectionMaximumContacts,
                1,
                MaximumWallReflectionContacts);

            RequireInRange(
                nameof(lightContactMomentumRetention),
                lightContactMomentumRetention,
                0d,
                1d);
            RequireInRange(
                nameof(lightContactSteeringRetention),
                lightContactSteeringRetention,
                0d,
                1d);
            RequireInRange(
                nameof(heavyContactMomentumRetention),
                heavyContactMomentumRetention,
                0d,
                lightContactMomentumRetention);

            RequireInRange(
                nameof(perEnemyContactGraceSeconds),
                perEnemyContactGraceSeconds,
                0.001d,
                10d);
            RequireInRange(
                nameof(simultaneousContactWindowSeconds),
                simultaneousContactWindowSeconds,
                0d,
                perEnemyContactGraceSeconds);
            RequireIntegerInRange(
                nameof(contactGraceCapacity),
                contactGraceCapacity,
                1,
                MaximumContactGraceCapacity);
        }

        private static void RequirePositive(string fieldName, double value)
        {
            RequireFinite(fieldName, value);
            if (value <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    fieldName,
                    value,
                    fieldName + " must be greater than zero.");
            }
        }

        private static void RequireNonNegative(string fieldName, double value)
        {
            RequireFinite(fieldName, value);
            if (value < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    fieldName,
                    value,
                    fieldName + " must be zero or greater.");
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
