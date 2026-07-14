using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Movement
{
    /// <summary>
    /// Immutable, engine-independent movement and thruster tuning used by deterministic domain systems.
    /// </summary>
    public sealed class MovementThrusterTuningProfile : IEquatable<MovementThrusterTuningProfile>
    {
        public const int CurrentProfileVersion = 1;
        public const string FingerprintPrefix = "sha256:";
        public const string DeterministicIdentityNamespace = "movement-tuning";

        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const int CanonicalFieldCount = 29;

        private readonly string canonicalText;

        private MovementThrusterTuningProfile(
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
            ProfileVersion = profileVersion;
            ProfileId = profileId;
            BaseMaximumSpeed = baseMaximumSpeed;
            BaseAcceleration = baseAcceleration;
            BaseBraking = baseBraking;
            BaseCounterSteerBraking = baseCounterSteerBraking;
            BaseVelocityResponseExponent = baseVelocityResponseExponent;
            ThrusterBaselineChargeCount = thrusterBaselineChargeCount;
            ThrusterMaximumAdditionalCharges = thrusterMaximumAdditionalCharges;
            ThrusterRechargeSeconds = thrusterRechargeSeconds;
            ThrusterSpeedMultiplier = thrusterSpeedMultiplier;
            ThrusterBurstDurationSeconds = thrusterBurstDurationSeconds;
            ThrusterDirectionInputThreshold = thrusterDirectionInputThreshold;
            ThrusterMinimumChainIntervalSeconds = thrusterMinimumChainIntervalSeconds;
            ThrusterSteeringDegreesPerSecond = thrusterSteeringDegreesPerSecond;
            ThrusterStartupForgivenessSeconds = thrusterStartupForgivenessSeconds;
            ThrusterExitMomentumSeconds = thrusterExitMomentumSeconds;
            ThrusterExitSpeedRetention = thrusterExitSpeedRetention;
            ThrusterExitDecayExponent = thrusterExitDecayExponent;
            WallReflectionSpeedRetention = wallReflectionSpeedRetention;
            WallReflectionInputInfluence = wallReflectionInputInfluence;
            WallReflectionMinimumSpeed = wallReflectionMinimumSpeed;
            WallReflectionMaximumContacts = wallReflectionMaximumContacts;
            LightContactMomentumRetention = lightContactMomentumRetention;
            LightContactSteeringRetention = lightContactSteeringRetention;
            HeavyContactMomentumRetention = heavyContactMomentumRetention;
            PerEnemyContactGraceSeconds = perEnemyContactGraceSeconds;
            SimultaneousContactWindowSeconds = simultaneousContactWindowSeconds;
            ContactGraceCapacity = contactGraceCapacity;

            canonicalText = BuildCanonicalText();
            Fingerprint = ComputeSha256(canonicalText);
            DeterministicIdentity = StableId.Create(
                DeterministicIdentityNamespace,
                Fingerprint.Substring(FingerprintPrefix.Length));
        }

        public int ProfileVersion { get; }

        public StableId ProfileId { get; }

        public double BaseMaximumSpeed { get; }

        public double BaseAcceleration { get; }

        public double BaseBraking { get; }

        public double BaseCounterSteerBraking { get; }

        public double BaseVelocityResponseExponent { get; }

        public int ThrusterBaselineChargeCount { get; }

        public int ThrusterMaximumAdditionalCharges { get; }

        public double ThrusterRechargeSeconds { get; }

        public double ThrusterSpeedMultiplier { get; }

        public double ThrusterBurstDurationSeconds { get; }

        public double ThrusterDirectionInputThreshold { get; }

        public double ThrusterMinimumChainIntervalSeconds { get; }

        public double ThrusterSteeringDegreesPerSecond { get; }

        public double ThrusterStartupForgivenessSeconds { get; }

        public double ThrusterExitMomentumSeconds { get; }

        public double ThrusterExitSpeedRetention { get; }

        public double ThrusterExitDecayExponent { get; }

        public double WallReflectionSpeedRetention { get; }

        public double WallReflectionInputInfluence { get; }

        public double WallReflectionMinimumSpeed { get; }

        public int WallReflectionMaximumContacts { get; }

        public double LightContactMomentumRetention { get; }

        public double LightContactSteeringRetention { get; }

        public double HeavyContactMomentumRetention { get; }

        public double PerEnemyContactGraceSeconds { get; }

        public double SimultaneousContactWindowSeconds { get; }

        public int ContactGraceCapacity { get; }

        /// <summary>
        /// SHA-256 of the complete canonical profile text.
        /// </summary>
        public string Fingerprint { get; }

        /// <summary>
        /// StableId derived only from the canonical profile fingerprint.
        /// </summary>
        public StableId DeterministicIdentity { get; }

        public static MovementThrusterTuningProfile Create(
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
            MovementThrusterTuningProfileValidator.ValidateValues(
                profileVersion,
                profileId,
                baseMaximumSpeed,
                baseAcceleration,
                baseBraking,
                baseCounterSteerBraking,
                baseVelocityResponseExponent,
                thrusterBaselineChargeCount,
                thrusterMaximumAdditionalCharges,
                thrusterRechargeSeconds,
                thrusterSpeedMultiplier,
                thrusterBurstDurationSeconds,
                thrusterDirectionInputThreshold,
                thrusterMinimumChainIntervalSeconds,
                thrusterSteeringDegreesPerSecond,
                thrusterStartupForgivenessSeconds,
                thrusterExitMomentumSeconds,
                thrusterExitSpeedRetention,
                thrusterExitDecayExponent,
                wallReflectionSpeedRetention,
                wallReflectionInputInfluence,
                wallReflectionMinimumSpeed,
                wallReflectionMaximumContacts,
                lightContactMomentumRetention,
                lightContactSteeringRetention,
                heavyContactMomentumRetention,
                perEnemyContactGraceSeconds,
                simultaneousContactWindowSeconds,
                contactGraceCapacity);

            return new MovementThrusterTuningProfile(
                profileVersion,
                profileId,
                baseMaximumSpeed,
                baseAcceleration,
                baseBraking,
                baseCounterSteerBraking,
                baseVelocityResponseExponent,
                thrusterBaselineChargeCount,
                thrusterMaximumAdditionalCharges,
                thrusterRechargeSeconds,
                thrusterSpeedMultiplier,
                thrusterBurstDurationSeconds,
                thrusterDirectionInputThreshold,
                thrusterMinimumChainIntervalSeconds,
                thrusterSteeringDegreesPerSecond,
                thrusterStartupForgivenessSeconds,
                thrusterExitMomentumSeconds,
                thrusterExitSpeedRetention,
                thrusterExitDecayExponent,
                wallReflectionSpeedRetention,
                wallReflectionInputInfluence,
                wallReflectionMinimumSpeed,
                wallReflectionMaximumContacts,
                lightContactMomentumRetention,
                lightContactSteeringRetention,
                heavyContactMomentumRetention,
                perEnemyContactGraceSeconds,
                simultaneousContactWindowSeconds,
                contactGraceCapacity);
        }

        public static MovementThrusterTuningProfile ParseCanonical(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (text.Length == 0)
            {
                throw new FormatException("Movement-thruster tuning profile cannot be empty.");
            }

            if (text.IndexOf('\r') >= 0 || text.EndsWith("\n", StringComparison.Ordinal))
            {
                throw new FormatException(
                    "Canonical movement-thruster tuning text must use LF separators and no trailing newline.");
            }

            string[] lines = text.Split('\n');
            if (lines.Length != CanonicalFieldCount)
            {
                throw new FormatException(
                    "Movement-thruster tuning profile must contain exactly "
                    + CanonicalFieldCount.ToString(CultureInfo.InvariantCulture)
                    + " canonical fields.");
            }

            MovementThrusterTuningProfile profile = Create(
                ParseInteger(ReadField(lines[0], "profile_version"), "profile_version"),
                StableId.Parse(ReadField(lines[1], "profile_id")),
                ParseDouble(ReadField(lines[2], "base_maximum_speed"), "base_maximum_speed"),
                ParseDouble(ReadField(lines[3], "base_acceleration"), "base_acceleration"),
                ParseDouble(ReadField(lines[4], "base_braking"), "base_braking"),
                ParseDouble(
                    ReadField(lines[5], "base_counter_steer_braking"),
                    "base_counter_steer_braking"),
                ParseDouble(
                    ReadField(lines[6], "base_velocity_response_exponent"),
                    "base_velocity_response_exponent"),
                ParseInteger(
                    ReadField(lines[7], "thruster_baseline_charge_count"),
                    "thruster_baseline_charge_count"),
                ParseInteger(
                    ReadField(lines[8], "thruster_maximum_additional_charges"),
                    "thruster_maximum_additional_charges"),
                ParseDouble(
                    ReadField(lines[9], "thruster_recharge_seconds"),
                    "thruster_recharge_seconds"),
                ParseDouble(
                    ReadField(lines[10], "thruster_speed_multiplier"),
                    "thruster_speed_multiplier"),
                ParseDouble(
                    ReadField(lines[11], "thruster_burst_duration_seconds"),
                    "thruster_burst_duration_seconds"),
                ParseDouble(
                    ReadField(lines[12], "thruster_direction_input_threshold"),
                    "thruster_direction_input_threshold"),
                ParseDouble(
                    ReadField(lines[13], "thruster_minimum_chain_interval_seconds"),
                    "thruster_minimum_chain_interval_seconds"),
                ParseDouble(
                    ReadField(lines[14], "thruster_steering_degrees_per_second"),
                    "thruster_steering_degrees_per_second"),
                ParseDouble(
                    ReadField(lines[15], "thruster_startup_forgiveness_seconds"),
                    "thruster_startup_forgiveness_seconds"),
                ParseDouble(
                    ReadField(lines[16], "thruster_exit_momentum_seconds"),
                    "thruster_exit_momentum_seconds"),
                ParseDouble(
                    ReadField(lines[17], "thruster_exit_speed_retention"),
                    "thruster_exit_speed_retention"),
                ParseDouble(
                    ReadField(lines[18], "thruster_exit_decay_exponent"),
                    "thruster_exit_decay_exponent"),
                ParseDouble(
                    ReadField(lines[19], "wall_reflection_speed_retention"),
                    "wall_reflection_speed_retention"),
                ParseDouble(
                    ReadField(lines[20], "wall_reflection_input_influence"),
                    "wall_reflection_input_influence"),
                ParseDouble(
                    ReadField(lines[21], "wall_reflection_minimum_speed"),
                    "wall_reflection_minimum_speed"),
                ParseInteger(
                    ReadField(lines[22], "wall_reflection_maximum_contacts"),
                    "wall_reflection_maximum_contacts"),
                ParseDouble(
                    ReadField(lines[23], "light_contact_momentum_retention"),
                    "light_contact_momentum_retention"),
                ParseDouble(
                    ReadField(lines[24], "light_contact_steering_retention"),
                    "light_contact_steering_retention"),
                ParseDouble(
                    ReadField(lines[25], "heavy_contact_momentum_retention"),
                    "heavy_contact_momentum_retention"),
                ParseDouble(
                    ReadField(lines[26], "per_enemy_contact_grace_seconds"),
                    "per_enemy_contact_grace_seconds"),
                ParseDouble(
                    ReadField(lines[27], "simultaneous_contact_window_seconds"),
                    "simultaneous_contact_window_seconds"),
                ParseInteger(
                    ReadField(lines[28], "contact_grace_capacity"),
                    "contact_grace_capacity"));

            if (!string.Equals(profile.ToCanonicalString(), text, StringComparison.Ordinal))
            {
                throw new FormatException(
                    "Movement-thruster tuning profile is valid but not in canonical form.");
            }

            return profile;
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(MovementThrusterTuningProfile other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MovementThrusterTuningProfile);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }

        public override string ToString()
        {
            return canonicalText;
        }

        public static bool operator ==(
            MovementThrusterTuningProfile left,
            MovementThrusterTuningProfile right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(
            MovementThrusterTuningProfile left,
            MovementThrusterTuningProfile right)
        {
            return !(left == right);
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder(1024);
            Append(builder, "profile_version", ProfileVersion);
            Append(builder, "profile_id", ProfileId.ToString());
            Append(builder, "base_maximum_speed", BaseMaximumSpeed);
            Append(builder, "base_acceleration", BaseAcceleration);
            Append(builder, "base_braking", BaseBraking);
            Append(builder, "base_counter_steer_braking", BaseCounterSteerBraking);
            Append(builder, "base_velocity_response_exponent", BaseVelocityResponseExponent);
            Append(builder, "thruster_baseline_charge_count", ThrusterBaselineChargeCount);
            Append(
                builder,
                "thruster_maximum_additional_charges",
                ThrusterMaximumAdditionalCharges);
            Append(builder, "thruster_recharge_seconds", ThrusterRechargeSeconds);
            Append(builder, "thruster_speed_multiplier", ThrusterSpeedMultiplier);
            Append(builder, "thruster_burst_duration_seconds", ThrusterBurstDurationSeconds);
            Append(
                builder,
                "thruster_direction_input_threshold",
                ThrusterDirectionInputThreshold);
            Append(
                builder,
                "thruster_minimum_chain_interval_seconds",
                ThrusterMinimumChainIntervalSeconds);
            Append(
                builder,
                "thruster_steering_degrees_per_second",
                ThrusterSteeringDegreesPerSecond);
            Append(
                builder,
                "thruster_startup_forgiveness_seconds",
                ThrusterStartupForgivenessSeconds);
            Append(builder, "thruster_exit_momentum_seconds", ThrusterExitMomentumSeconds);
            Append(builder, "thruster_exit_speed_retention", ThrusterExitSpeedRetention);
            Append(builder, "thruster_exit_decay_exponent", ThrusterExitDecayExponent);
            Append(
                builder,
                "wall_reflection_speed_retention",
                WallReflectionSpeedRetention);
            Append(
                builder,
                "wall_reflection_input_influence",
                WallReflectionInputInfluence);
            Append(builder, "wall_reflection_minimum_speed", WallReflectionMinimumSpeed);
            Append(
                builder,
                "wall_reflection_maximum_contacts",
                WallReflectionMaximumContacts);
            Append(
                builder,
                "light_contact_momentum_retention",
                LightContactMomentumRetention);
            Append(
                builder,
                "light_contact_steering_retention",
                LightContactSteeringRetention);
            Append(
                builder,
                "heavy_contact_momentum_retention",
                HeavyContactMomentumRetention);
            Append(
                builder,
                "per_enemy_contact_grace_seconds",
                PerEnemyContactGraceSeconds);
            Append(
                builder,
                "simultaneous_contact_window_seconds",
                SimultaneousContactWindowSeconds);
            Append(builder, "contact_grace_capacity", ContactGraceCapacity, false);
            return builder.ToString();
        }

        private static void Append(
            StringBuilder builder,
            string fieldName,
            double value,
            bool appendNewline = true)
        {
            Append(
                builder,
                fieldName,
                value.ToString("R", CultureInfo.InvariantCulture),
                appendNewline);
        }

        private static void Append(
            StringBuilder builder,
            string fieldName,
            int value,
            bool appendNewline = true)
        {
            Append(
                builder,
                fieldName,
                value.ToString(CultureInfo.InvariantCulture),
                appendNewline);
        }

        private static void Append(
            StringBuilder builder,
            string fieldName,
            string value,
            bool appendNewline = true)
        {
            builder.Append(fieldName);
            builder.Append('=');
            builder.Append(value);
            if (appendNewline)
            {
                builder.Append('\n');
            }
        }

        private static string ReadField(string line, string expectedName)
        {
            string prefix = expectedName + "=";
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new FormatException(
                    "Expected canonical field '" + expectedName + "' in its declared position.");
            }

            string value = line.Substring(prefix.Length);
            if (value.Length == 0)
            {
                throw new FormatException("Canonical field '" + expectedName + "' cannot be empty.");
            }

            return value;
        }

        private static int ParseInteger(string text, string fieldName)
        {
            int value;
            if (!int.TryParse(
                text,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value))
            {
                throw new FormatException(
                    "Canonical field '" + fieldName + "' must be a decimal integer.");
            }

            return value;
        }

        private static double ParseDouble(string text, string fieldName)
        {
            double value;
            if (!double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value))
            {
                throw new FormatException(
                    "Canonical field '" + fieldName + "' must be a finite decimal number.");
            }

            return value;
        }

        private static string ComputeSha256(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(bytes);
            }

            StringBuilder builder = new StringBuilder(FingerprintPrefix.Length + (digest.Length * 2));
            builder.Append(FingerprintPrefix);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
