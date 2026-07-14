using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Combat
{
    /// <summary>
    /// Declares the mutually exclusive cycle-resource model used by one weapon mount.
    /// </summary>
    public enum WeaponCycleMode
    {
        None = 1,
        Heat = 2,
        Charge = 3,
    }

    /// <summary>
    /// Immutable, engine-independent runtime tuning for one weapon used by the accepted
    /// four-mount combat model. Runtime state remains independent per mount.
    /// </summary>
    public sealed class WeaponRuntimeProfile : IEquatable<WeaponRuntimeProfile>
    {
        public const int CurrentProfileVersion = 1;
        public const int SupportedMountCount = 4;
        public const bool NormalFireConsumesConsumable = false;
        public const string FingerprintPrefix = "sha256:";
        public const string DeterministicIdentityNamespace = "weapon-runtime";

        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const int CanonicalFixedFieldCount = 17;

        private readonly ReadOnlyCollection<StableId> behaviorModuleIds;
        private readonly string canonicalText;

        private WeaponRuntimeProfile(
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
            StableId[] copiedBehaviorModuleIds,
            int presentationPriority)
        {
            ProfileVersion = profileVersion;
            ProfileId = profileId;
            CadenceSeconds = cadenceSeconds;
            BurstShotCount = burstShotCount;
            BurstShotIntervalSeconds = burstShotIntervalSeconds;
            RecoverySeconds = recoverySeconds;
            CycleMode = cycleMode;
            HeatCapacityUnits = heatCapacityUnits;
            HeatPerShotUnits = heatPerShotUnits;
            HeatRecoveryUnitsPerSecond = heatRecoveryUnitsPerSecond;
            ChargeSeconds = chargeSeconds;
            HasIndependentPowerBank = hasIndependentPowerBank;
            PowerBankCapacityUnits = powerBankCapacityUnits;
            EmpoweredCostUnits = empoweredCostUnits;
            RecoilInfluence = recoilInfluence;
            behaviorModuleIds = Array.AsReadOnly((StableId[])copiedBehaviorModuleIds.Clone());
            PresentationPriority = presentationPriority;

            canonicalText = BuildCanonicalText();
            Fingerprint = ComputeSha256(canonicalText);
            DeterministicIdentity = StableId.Create(
                DeterministicIdentityNamespace,
                Fingerprint.Substring(FingerprintPrefix.Length));
        }

        public int ProfileVersion { get; }

        public StableId ProfileId { get; }

        /// <summary>
        /// Minimum authored interval between weapon cycles.
        /// </summary>
        public double CadenceSeconds { get; }

        /// <summary>
        /// Number of shots emitted by one cycle. A value of one is a non-burst cycle.
        /// </summary>
        public int BurstShotCount { get; }

        /// <summary>
        /// Interval between shots inside a burst. It is zero when BurstShotCount is one.
        /// </summary>
        public double BurstShotIntervalSeconds { get; }

        /// <summary>
        /// Authored post-cycle recovery before the mount can become ready again.
        /// </summary>
        public double RecoverySeconds { get; }

        public WeaponCycleMode CycleMode { get; }

        public double HeatCapacityUnits { get; }

        public double HeatPerShotUnits { get; }

        public double HeatRecoveryUnitsPerSecond { get; }

        public double ChargeSeconds { get; }

        /// <summary>
        /// True when each mounted runtime instance owns its own empowered-fire bank.
        /// </summary>
        public bool HasIndependentPowerBank { get; }

        public double PowerBankCapacityUnits { get; }

        public double EmpoweredCostUnits { get; }

        /// <summary>
        /// Normalized authored movement influence. Cross-mount aggregation and caps are
        /// applied by later combat/movement integration, not by this profile.
        /// </summary>
        public double RecoilInfluence { get; }

        public int BehaviorModuleCount
        {
            get { return behaviorModuleIds.Count; }
        }

        public int PresentationPriority { get; }

        /// <summary>
        /// SHA-256 of the complete canonical profile text.
        /// </summary>
        public string Fingerprint { get; }

        /// <summary>
        /// StableId derived only from the canonical profile fingerprint.
        /// </summary>
        public StableId DeterministicIdentity { get; }

        public StableId GetBehaviorModuleId(int index)
        {
            if (index < 0 || index >= behaviorModuleIds.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return behaviorModuleIds[index];
        }

        public static WeaponRuntimeProfile Create(
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
            IEnumerable<StableId> behaviorModuleIds,
            IEnumerable<StableId> knownBehaviorModuleIds,
            int presentationPriority)
        {
            StableId[] copiedBehaviorModuleIds = CopyBehaviorModuleIds(behaviorModuleIds);

            WeaponRuntimeProfileValidator.ValidateValues(
                profileVersion,
                profileId,
                cadenceSeconds,
                burstShotCount,
                burstShotIntervalSeconds,
                recoverySeconds,
                cycleMode,
                heatCapacityUnits,
                heatPerShotUnits,
                heatRecoveryUnitsPerSecond,
                chargeSeconds,
                hasIndependentPowerBank,
                powerBankCapacityUnits,
                empoweredCostUnits,
                recoilInfluence,
                copiedBehaviorModuleIds,
                knownBehaviorModuleIds,
                presentationPriority);

            return new WeaponRuntimeProfile(
                profileVersion,
                profileId,
                cadenceSeconds,
                burstShotCount,
                burstShotIntervalSeconds,
                recoverySeconds,
                cycleMode,
                heatCapacityUnits,
                heatPerShotUnits,
                heatRecoveryUnitsPerSecond,
                chargeSeconds,
                hasIndependentPowerBank,
                powerBankCapacityUnits,
                empoweredCostUnits,
                recoilInfluence,
                copiedBehaviorModuleIds,
                presentationPriority);
        }

        public static WeaponRuntimeProfile ParseCanonical(
            string text,
            IEnumerable<StableId> knownBehaviorModuleIds)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (text.Length == 0)
            {
                throw new FormatException("Weapon runtime profile cannot be empty.");
            }

            if (text.IndexOf('\r') >= 0 || text.EndsWith("\n", StringComparison.Ordinal))
            {
                throw new FormatException(
                    "Canonical weapon runtime profile text must use LF separators and no trailing newline.");
            }

            string[] lines = text.Split('\n');
            if (lines.Length < CanonicalFixedFieldCount + 1)
            {
                throw new FormatException("Weapon runtime profile is missing required canonical fields.");
            }

            int behaviorModuleCount = ParseInteger(
                ReadField(lines[15], "behavior_module_count"),
                "behavior_module_count");
            if (behaviorModuleCount < 0
                || behaviorModuleCount > WeaponRuntimeProfileValidator.MaximumBehaviorModuleCount)
            {
                throw new FormatException("Canonical behavior_module_count is outside the supported range.");
            }

            int expectedLineCount = CanonicalFixedFieldCount + behaviorModuleCount;
            if (lines.Length != expectedLineCount)
            {
                throw new FormatException(
                    "Weapon runtime profile does not contain the declared number of behavior modules.");
            }

            StableId[] parsedModuleIds = new StableId[behaviorModuleCount];
            for (int index = 0; index < behaviorModuleCount; index++)
            {
                parsedModuleIds[index] = StableId.Parse(
                    ReadField(lines[16 + index], "behavior_module_" + index.ToString(CultureInfo.InvariantCulture)));
            }

            WeaponRuntimeProfile profile = Create(
                ParseInteger(ReadField(lines[0], "profile_version"), "profile_version"),
                StableId.Parse(ReadField(lines[1], "profile_id")),
                ParseDouble(ReadField(lines[2], "cadence_seconds"), "cadence_seconds"),
                ParseInteger(ReadField(lines[3], "burst_shot_count"), "burst_shot_count"),
                ParseDouble(
                    ReadField(lines[4], "burst_shot_interval_seconds"),
                    "burst_shot_interval_seconds"),
                ParseDouble(ReadField(lines[5], "recovery_seconds"), "recovery_seconds"),
                ParseCycleMode(ReadField(lines[6], "cycle_mode")),
                ParseDouble(
                    ReadField(lines[7], "heat_capacity_units"),
                    "heat_capacity_units"),
                ParseDouble(
                    ReadField(lines[8], "heat_per_shot_units"),
                    "heat_per_shot_units"),
                ParseDouble(
                    ReadField(lines[9], "heat_recovery_units_per_second"),
                    "heat_recovery_units_per_second"),
                ParseDouble(ReadField(lines[10], "charge_seconds"), "charge_seconds"),
                ParseBoolean(
                    ReadField(lines[11], "has_independent_power_bank"),
                    "has_independent_power_bank"),
                ParseDouble(
                    ReadField(lines[12], "power_bank_capacity_units"),
                    "power_bank_capacity_units"),
                ParseDouble(
                    ReadField(lines[13], "empowered_cost_units"),
                    "empowered_cost_units"),
                ParseDouble(ReadField(lines[14], "recoil_influence"), "recoil_influence"),
                parsedModuleIds,
                knownBehaviorModuleIds,
                ParseInteger(
                    ReadField(lines[16 + behaviorModuleCount], "presentation_priority"),
                    "presentation_priority"));

            if (!string.Equals(profile.ToCanonicalString(), text, StringComparison.Ordinal))
            {
                throw new FormatException(
                    "Weapon runtime profile is valid but not in canonical form.");
            }

            return profile;
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(WeaponRuntimeProfile other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WeaponRuntimeProfile);
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

        public static bool operator ==(WeaponRuntimeProfile left, WeaponRuntimeProfile right)
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

        public static bool operator !=(WeaponRuntimeProfile left, WeaponRuntimeProfile right)
        {
            return !(left == right);
        }

        internal StableId[] CopyBehaviorModuleIdsForValidation()
        {
            StableId[] copy = new StableId[behaviorModuleIds.Count];
            for (int index = 0; index < behaviorModuleIds.Count; index++)
            {
                copy[index] = behaviorModuleIds[index];
            }

            return copy;
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder(768);
            Append(builder, "profile_version", ProfileVersion);
            Append(builder, "profile_id", ProfileId.ToString());
            Append(builder, "cadence_seconds", CadenceSeconds);
            Append(builder, "burst_shot_count", BurstShotCount);
            Append(builder, "burst_shot_interval_seconds", BurstShotIntervalSeconds);
            Append(builder, "recovery_seconds", RecoverySeconds);
            Append(builder, "cycle_mode", FormatCycleMode(CycleMode));
            Append(builder, "heat_capacity_units", HeatCapacityUnits);
            Append(builder, "heat_per_shot_units", HeatPerShotUnits);
            Append(builder, "heat_recovery_units_per_second", HeatRecoveryUnitsPerSecond);
            Append(builder, "charge_seconds", ChargeSeconds);
            Append(builder, "has_independent_power_bank", HasIndependentPowerBank ? "true" : "false");
            Append(builder, "power_bank_capacity_units", PowerBankCapacityUnits);
            Append(builder, "empowered_cost_units", EmpoweredCostUnits);
            Append(builder, "recoil_influence", RecoilInfluence);
            Append(builder, "behavior_module_count", behaviorModuleIds.Count);

            for (int index = 0; index < behaviorModuleIds.Count; index++)
            {
                Append(
                    builder,
                    "behavior_module_" + index.ToString(CultureInfo.InvariantCulture),
                    behaviorModuleIds[index].ToString());
            }

            Append(builder, "presentation_priority", PresentationPriority, false);
            return builder.ToString();
        }

        private static StableId[] CopyBehaviorModuleIds(IEnumerable<StableId> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            List<StableId> copied = new List<StableId>();
            foreach (StableId moduleId in source)
            {
                copied.Add(moduleId);
            }

            return copied.ToArray();
        }

        private static string FormatCycleMode(WeaponCycleMode cycleMode)
        {
            switch (cycleMode)
            {
                case WeaponCycleMode.None:
                    return "none";
                case WeaponCycleMode.Heat:
                    return "heat";
                case WeaponCycleMode.Charge:
                    return "charge";
                default:
                    throw new ArgumentOutOfRangeException(nameof(cycleMode));
            }
        }

        private static WeaponCycleMode ParseCycleMode(string text)
        {
            if (string.Equals(text, "none", StringComparison.Ordinal))
            {
                return WeaponCycleMode.None;
            }

            if (string.Equals(text, "heat", StringComparison.Ordinal))
            {
                return WeaponCycleMode.Heat;
            }

            if (string.Equals(text, "charge", StringComparison.Ordinal))
            {
                return WeaponCycleMode.Charge;
            }

            throw new FormatException("Canonical cycle_mode must be none, heat, or charge.");
        }

        private static bool ParseBoolean(string text, string fieldName)
        {
            if (string.Equals(text, "true", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(text, "false", StringComparison.Ordinal))
            {
                return false;
            }

            throw new FormatException("Canonical field '" + fieldName + "' must be true or false.");
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
