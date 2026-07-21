using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Modifiers;

namespace ShooterMover.Domain.Characters.Stats
{
    /// <summary>
    /// Stable open target identities consumed by character and run stat composition.
    /// New ordinary statistics should normally add a target and a policy rule, not a
    /// branch inside the calculator.
    /// </summary>
    public static class DerivedStatTargetIdsV1
    {
        public const string MaximumHealth = "combat.maximum-health";
        public const string MovementSpeed = "combat.movement-speed";
        public const string Armor = "combat.armor";
        public const string PhysicalDamageResistance =
            "combat.damage-resistance.physical";
        public const string EnergyDamageResistance =
            "combat.damage-resistance.energy";
        public const string ThermalDamageResistance =
            "combat.damage-resistance.thermal";
        public const string ChemicalDamageResistance =
            "combat.damage-resistance.chemical";
        public const string OutgoingDamageMultiplier =
            "combat.damage-multiplier";
        public const string CriticalChance = "combat.critical-chance";
        public const string CriticalMultiplier = "combat.critical-multiplier";
        public const string HealingOutputMultiplier =
            "combat.healing-output-multiplier";
        public const string HealingReceivedMultiplier =
            "combat.healing-received-multiplier";
        public const string ContactDamage = "combat.contact-damage";
        public const string KnockbackMultiplier =
            "combat.knockback-multiplier";
        public const string WeaponCapacity = "loadout.weapon-capacity";
        public const string AbilityCapacity = "loadout.ability-capacity";
        public const string WeaponDamageMultiplier =
            "weapon.damage-multiplier";
        public const string WeaponFireRateMultiplier =
            "weapon.fire-rate-multiplier";
        public const string WeaponReloadSpeedMultiplier =
            "weapon.reload-speed-multiplier";
        public const string RewardMultiplier = "rewards.reward-multiplier";
        public const string DropMultiplier = "rewards.drop-multiplier";
        public const string StrongboxDropWeight =
            "rewards.strongbox-drop-weight";

        public static string DamageResistance(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                throw new ArgumentException(
                    "A damage-resistance channel identity is required.",
                    nameof(channelId));
            }

            return "combat.damage-resistance." + channelId.Trim();
        }
    }

    public static class DerivedStatSourcePrioritiesV1
    {
        public const int ClassAndLevel = 100;
        public const int Equipment = 200;
        public const int Augments = 300;
        public const int Skills = 400;
        public const int Account = 500;
        public const int Achievements = 600;
        public const int Events = 700;
        public const int RunConditions = 800;
    }

    public sealed class DerivedStatRuleV1
    {
        public DerivedStatRuleV1(
            string targetId,
            decimal defaultBaseValue,
            decimal? minimum,
            decimal? maximum,
            bool requiresExplicitBaseValue = false,
            bool requiresWholeNumber = false)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException(
                    "A derived-stat target identity is required.",
                    nameof(targetId));
            }
            if (minimum.HasValue
                && maximum.HasValue
                && minimum.Value > maximum.Value)
            {
                throw new ArgumentException(
                    "The derived-stat minimum cannot exceed the maximum.");
            }
            if (requiresWholeNumber
                && defaultBaseValue != decimal.Truncate(defaultBaseValue))
            {
                throw new ArgumentException(
                    "Whole-number stat defaults must be integral.",
                    nameof(defaultBaseValue));
            }

            TargetId = targetId.Trim();
            DefaultBaseValue = defaultBaseValue;
            Minimum = minimum;
            Maximum = maximum;
            RequiresExplicitBaseValue = requiresExplicitBaseValue;
            RequiresWholeNumber = requiresWholeNumber;
            Fingerprint = DerivedStatFingerprintV1.Hash(ToCanonicalString());
        }

        public string TargetId { get; }

        public decimal DefaultBaseValue { get; }

        public decimal? Minimum { get; }

        public decimal? Maximum { get; }

        public bool RequiresExplicitBaseValue { get; }

        public bool RequiresWholeNumber { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            DerivedStatFingerprintV1.Append(builder, "target", TargetId);
            DerivedStatFingerprintV1.AppendDecimal(
                builder,
                "default",
                DefaultBaseValue);
            DerivedStatFingerprintV1.AppendNullableDecimal(
                builder,
                "minimum",
                Minimum);
            DerivedStatFingerprintV1.AppendNullableDecimal(
                builder,
                "maximum",
                Maximum);
            DerivedStatFingerprintV1.Append(
                builder,
                "explicit-base",
                RequiresExplicitBaseValue ? "1" : "0");
            DerivedStatFingerprintV1.Append(
                builder,
                "whole-number",
                RequiresWholeNumber ? "1" : "0");
            return builder.ToString();
        }
    }

    public sealed class DerivedStatPolicyV1
    {
        private readonly IReadOnlyDictionary<string, DerivedStatRuleV1> rulesById;

        public DerivedStatPolicyV1(
            string policyId,
            string policyVersion,
            IEnumerable<DerivedStatRuleV1> rules)
        {
            if (string.IsNullOrWhiteSpace(policyId))
            {
                throw new ArgumentException(
                    "A derived-stat policy identity is required.",
                    nameof(policyId));
            }
            if (string.IsNullOrWhiteSpace(policyVersion))
            {
                throw new ArgumentException(
                    "A derived-stat policy version is required.",
                    nameof(policyVersion));
            }

            List<DerivedStatRuleV1> items = (rules
                ?? throw new ArgumentNullException(nameof(rules))).ToList();
            if (items.Count == 0 || items.Any(item => item == null))
            {
                throw new ArgumentException(
                    "At least one non-null derived-stat rule is required.",
                    nameof(rules));
            }
            if (items.Select(item => item.TargetId)
                .Distinct(StringComparer.Ordinal)
                .Count() != items.Count)
            {
                throw new ArgumentException(
                    "Derived-stat target identities must be unique.",
                    nameof(rules));
            }

            PolicyId = policyId.Trim();
            PolicyVersion = policyVersion.Trim();
            Rules = new ReadOnlyCollection<DerivedStatRuleV1>(
                items.OrderBy(item => item.TargetId, StringComparer.Ordinal)
                    .ToList());
            rulesById = new ReadOnlyDictionary<string, DerivedStatRuleV1>(
                Rules.ToDictionary(
                    item => item.TargetId,
                    StringComparer.Ordinal));
            Fingerprint = DerivedStatFingerprintV1.Hash(ToCanonicalString());
        }

        public string PolicyId { get; }

        public string PolicyVersion { get; }

        public IReadOnlyList<DerivedStatRuleV1> Rules { get; }

        public string Fingerprint { get; }

        public bool TryGetRule(string targetId, out DerivedStatRuleV1 rule)
        {
            return rulesById.TryGetValue(targetId ?? string.Empty, out rule);
        }

        public DerivedStatRuleV1 RequireRule(string targetId)
        {
            DerivedStatRuleV1 rule;
            if (!TryGetRule(targetId, out rule))
            {
                throw new InvalidOperationException(
                    "No derived-stat policy rule exists for target '"
                    + (targetId ?? string.Empty)
                    + "'.");
            }

            return rule;
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            DerivedStatFingerprintV1.Append(builder, "policy", PolicyId);
            DerivedStatFingerprintV1.Append(
                builder,
                "version",
                PolicyVersion);
            foreach (DerivedStatRuleV1 rule in Rules)
            {
                DerivedStatFingerprintV1.Append(
                    builder,
                    "rule",
                    rule.ToCanonicalString());
            }

            return builder.ToString();
        }

        public static DerivedStatPolicyV1 CreateDefault()
        {
            return new DerivedStatPolicyV1(
                "derived-stats.default",
                "1",
                new[]
                {
                    Rule(
                        DerivedStatTargetIdsV1.MaximumHealth,
                        1m,
                        1m,
                        1000000m,
                        true),
                    Rule(
                        DerivedStatTargetIdsV1.MovementSpeed,
                        0m,
                        0m,
                        1000m,
                        true),
                    Rule(DerivedStatTargetIdsV1.Armor, 0m, 0m, 1000000m),
                    Resistance(
                        DerivedStatTargetIdsV1.PhysicalDamageResistance),
                    Resistance(DerivedStatTargetIdsV1.EnergyDamageResistance),
                    Resistance(DerivedStatTargetIdsV1.ThermalDamageResistance),
                    Resistance(DerivedStatTargetIdsV1.ChemicalDamageResistance),
                    Multiplier(
                        DerivedStatTargetIdsV1.OutgoingDamageMultiplier),
                    Rule(
                        DerivedStatTargetIdsV1.CriticalChance,
                        0m,
                        0m,
                        1m),
                    Rule(
                        DerivedStatTargetIdsV1.CriticalMultiplier,
                        1m,
                        1m,
                        100m),
                    Multiplier(
                        DerivedStatTargetIdsV1.HealingOutputMultiplier),
                    Multiplier(
                        DerivedStatTargetIdsV1.HealingReceivedMultiplier),
                    Rule(
                        DerivedStatTargetIdsV1.ContactDamage,
                        0m,
                        0m,
                        1000000m),
                    Multiplier(DerivedStatTargetIdsV1.KnockbackMultiplier),
                    Capacity(DerivedStatTargetIdsV1.WeaponCapacity),
                    Capacity(DerivedStatTargetIdsV1.AbilityCapacity),
                    Multiplier(
                        DerivedStatTargetIdsV1.WeaponDamageMultiplier),
                    Multiplier(
                        DerivedStatTargetIdsV1.WeaponFireRateMultiplier),
                    Multiplier(
                        DerivedStatTargetIdsV1.WeaponReloadSpeedMultiplier),
                    Multiplier(DerivedStatTargetIdsV1.RewardMultiplier),
                    Multiplier(DerivedStatTargetIdsV1.DropMultiplier),
                    Multiplier(DerivedStatTargetIdsV1.StrongboxDropWeight),
                });
        }

        private static DerivedStatRuleV1 Rule(
            string targetId,
            decimal defaultBaseValue,
            decimal? minimum,
            decimal? maximum,
            bool requiresExplicitBaseValue = false)
        {
            return new DerivedStatRuleV1(
                targetId,
                defaultBaseValue,
                minimum,
                maximum,
                requiresExplicitBaseValue);
        }

        private static DerivedStatRuleV1 Resistance(string targetId)
        {
            return Rule(targetId, 0m, -1m, 0.95m);
        }

        private static DerivedStatRuleV1 Multiplier(string targetId)
        {
            return Rule(targetId, 1m, 0m, 1000m);
        }

        private static DerivedStatRuleV1 Capacity(string targetId)
        {
            return new DerivedStatRuleV1(
                targetId,
                0m,
                0m,
                64m,
                false,
                true);
        }
    }

    /// <summary>
    /// One immutable projection from an existing authority into the shared runtime
    /// modifier language. InputFingerprint must be the upstream authority snapshot or
    /// exact-instance composition fingerprint; this class does not duplicate that state.
    /// </summary>
    public sealed class DerivedStatModifierSourceV1
    {
        public DerivedStatModifierSourceV1(
            string sourceId,
            int priority,
            string inputFingerprint,
            RuntimeModifierSnapshotV1 modifiers)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException(
                    "A derived-stat source identity is required.",
                    nameof(sourceId));
            }
            if (priority < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(priority));
            }
            if (string.IsNullOrWhiteSpace(inputFingerprint))
            {
                throw new ArgumentException(
                    "An upstream input fingerprint is required.",
                    nameof(inputFingerprint));
            }

            SourceId = sourceId.Trim();
            Priority = priority;
            InputFingerprint = inputFingerprint.Trim();
            Modifiers = modifiers
                ?? throw new ArgumentNullException(nameof(modifiers));
            Fingerprint = DerivedStatFingerprintV1.Hash(ToCanonicalString());
        }

        public string SourceId { get; }

        public int Priority { get; }

        public string InputFingerprint { get; }

        public RuntimeModifierSnapshotV1 Modifiers { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            DerivedStatFingerprintV1.Append(builder, "source", SourceId);
            DerivedStatFingerprintV1.Append(
                builder,
                "priority",
                Priority.ToString(CultureInfo.InvariantCulture));
            DerivedStatFingerprintV1.Append(
                builder,
                "input",
                InputFingerprint);
            DerivedStatFingerprintV1.Append(
                builder,
                "modifiers",
                Modifiers.Fingerprint);
            return builder.ToString();
        }
    }

    public sealed class CharacterBaseStatProfileV1
    {
        public CharacterBaseStatProfileV1(
            string profileId,
            string classId,
            int level,
            string definitionFingerprint,
            IDictionary<string, decimal> baseValues)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                throw new ArgumentException(
                    "A base-stat profile identity is required.",
                    nameof(profileId));
            }
            if (string.IsNullOrWhiteSpace(classId))
            {
                throw new ArgumentException(
                    "A data-defined class identity is required.",
                    nameof(classId));
            }
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }
            if (string.IsNullOrWhiteSpace(definitionFingerprint))
            {
                throw new ArgumentException(
                    "A base-definition fingerprint is required.",
                    nameof(definitionFingerprint));
            }

            var copy = new SortedDictionary<string, decimal>(
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, decimal> pair in baseValues
                ?? throw new ArgumentNullException(nameof(baseValues)))
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    throw new ArgumentException(
                        "Base-stat target identities must be non-empty.",
                        nameof(baseValues));
                }

                copy.Add(pair.Key.Trim(), pair.Value);
            }

            ProfileId = profileId.Trim();
            ClassId = classId.Trim();
            Level = level;
            DefinitionFingerprint = definitionFingerprint.Trim();
            BaseValues = new ReadOnlyDictionary<string, decimal>(copy);
            Fingerprint = DerivedStatFingerprintV1.Hash(ToCanonicalString());
        }

        public string ProfileId { get; }

        public string ClassId { get; }

        public int Level { get; }

        public string DefinitionFingerprint { get; }

        public IReadOnlyDictionary<string, decimal> BaseValues { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            DerivedStatFingerprintV1.Append(builder, "profile", ProfileId);
            DerivedStatFingerprintV1.Append(builder, "class", ClassId);
            DerivedStatFingerprintV1.Append(
                builder,
                "level",
                Level.ToString(CultureInfo.InvariantCulture));
            DerivedStatFingerprintV1.Append(
                builder,
                "definition",
                DefinitionFingerprint);
            foreach (KeyValuePair<string, decimal> pair in BaseValues)
            {
                DerivedStatFingerprintV1.AppendDecimal(
                    builder,
                    pair.Key,
                    pair.Value);
            }

            return builder.ToString();
        }
    }

    public sealed class DerivedCharacterStatInputV1
    {
        public DerivedCharacterStatInputV1(
            string characterInstanceId,
            CharacterBaseStatProfileV1 baseProfile,
            IEnumerable<DerivedStatModifierSourceV1> permanentSources,
            DerivedStatPolicyV1 policy)
        {
            if (string.IsNullOrWhiteSpace(characterInstanceId))
            {
                throw new ArgumentException(
                    "A character instance identity is required.",
                    nameof(characterInstanceId));
            }

            CharacterInstanceId = characterInstanceId.Trim();
            BaseProfile = baseProfile
                ?? throw new ArgumentNullException(nameof(baseProfile));
            PermanentSources = FreezeSources(
                permanentSources,
                nameof(permanentSources));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            ValidatePermanentSources(PermanentSources);
            InputFingerprint = DerivedStatFingerprintV1.Hash(
                ToCanonicalString());
        }

        public string CharacterInstanceId { get; }

        public CharacterBaseStatProfileV1 BaseProfile { get; }

        public IReadOnlyList<DerivedStatModifierSourceV1> PermanentSources
        {
            get;
        }

        public DerivedStatPolicyV1 Policy { get; }

        public string InputFingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            DerivedStatFingerprintV1.Append(
                builder,
                "character",
                CharacterInstanceId);
            DerivedStatFingerprintV1.Append(
                builder,
                "base-profile",
                BaseProfile.Fingerprint);
            DerivedStatFingerprintV1.Append(
                builder,
                "policy",
                Policy.Fingerprint);
            foreach (DerivedStatModifierSourceV1 source in PermanentSources)
            {
                DerivedStatFingerprintV1.Append(
                    builder,
                    "permanent-source",
                    source.Fingerprint);
            }

            return builder.ToString();
        }

        internal static IReadOnlyList<DerivedStatModifierSourceV1> FreezeSources(
            IEnumerable<DerivedStatModifierSourceV1> sources,
            string parameterName)
        {
            List<DerivedStatModifierSourceV1> items = (sources
                ?? Array.Empty<DerivedStatModifierSourceV1>()).ToList();
            if (items.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Derived-stat sources must be non-null.",
                    parameterName);
            }
            if (items.Select(item => item.SourceId)
                .Distinct(StringComparer.Ordinal)
                .Count() != items.Count)
            {
                throw new ArgumentException(
                    "Derived-stat source identities must be unique.",
                    parameterName);
            }

            return new ReadOnlyCollection<DerivedStatModifierSourceV1>(
                items.OrderBy(item => item.Priority)
                    .ThenBy(item => item.SourceId, StringComparer.Ordinal)
                    .ThenBy(item => item.InputFingerprint, StringComparer.Ordinal)
                    .ThenBy(item => item.Modifiers.Fingerprint, StringComparer.Ordinal)
                    .ToList());
        }

        private static void ValidatePermanentSources(
            IEnumerable<DerivedStatModifierSourceV1> sources)
        {
            RuntimeModifierDefinitionV1 conditional = sources
                .SelectMany(source => source.Modifiers.Modifiers)
                .FirstOrDefault(modifier =>
                    !string.IsNullOrEmpty(modifier.ConditionId));
            if (conditional != null)
            {
                throw new ArgumentException(
                    "Permanent character sources cannot contain conditional "
                    + "modifiers. Place condition-owned modifiers in the run "
                    + "profile input. Source: "
                    + conditional.SourceId);
            }
        }
    }

    public sealed class RunCombatProfileInputV1
    {
        public RunCombatProfileInputV1(
            string runId,
            string runContextFingerprint,
            DerivedCharacterStatsSnapshotV1 characterStats,
            IEnumerable<DerivedStatModifierSourceV1> runSources,
            IEnumerable<string> activeConditionIds,
            DerivedStatPolicyV1 policy)
        {
            if (string.IsNullOrWhiteSpace(runId))
            {
                throw new ArgumentException(
                    "A run identity is required.",
                    nameof(runId));
            }
            if (string.IsNullOrWhiteSpace(runContextFingerprint))
            {
                throw new ArgumentException(
                    "A run-context fingerprint is required.",
                    nameof(runContextFingerprint));
            }

            RunId = runId.Trim();
            RunContextFingerprint = runContextFingerprint.Trim();
            CharacterStats = characterStats
                ?? throw new ArgumentNullException(nameof(characterStats));
            RunSources = DerivedCharacterStatInputV1.FreezeSources(
                runSources,
                nameof(runSources));
            ActiveConditionIds = FreezeConditionIds(activeConditionIds);
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            if (!string.Equals(
                CharacterStats.PolicyFingerprint,
                Policy.Fingerprint,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Run composition must use the same explicit stat policy "
                    + "that produced the character snapshot.",
                    nameof(policy));
            }

            InputFingerprint = DerivedStatFingerprintV1.Hash(
                ToCanonicalString());
        }

        public string RunId { get; }

        public string RunContextFingerprint { get; }

        public DerivedCharacterStatsSnapshotV1 CharacterStats { get; }

        public IReadOnlyList<DerivedStatModifierSourceV1> RunSources { get; }

        public IReadOnlyList<string> ActiveConditionIds { get; }

        public DerivedStatPolicyV1 Policy { get; }

        public string InputFingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            DerivedStatFingerprintV1.Append(builder, "run", RunId);
            DerivedStatFingerprintV1.Append(
                builder,
                "run-context",
                RunContextFingerprint);
            DerivedStatFingerprintV1.Append(
                builder,
                "character-stats",
                CharacterStats.Fingerprint);
            DerivedStatFingerprintV1.Append(
                builder,
                "policy",
                Policy.Fingerprint);
            foreach (DerivedStatModifierSourceV1 source in RunSources)
            {
                DerivedStatFingerprintV1.Append(
                    builder,
                    "run-source",
                    source.Fingerprint);
            }
            foreach (string conditionId in ActiveConditionIds)
            {
                DerivedStatFingerprintV1.Append(
                    builder,
                    "condition",
                    conditionId);
            }

            return builder.ToString();
        }

        private static IReadOnlyList<string> FreezeConditionIds(
            IEnumerable<string> conditionIds)
        {
            List<string> items = (conditionIds ?? Array.Empty<string>())
                .Select(item => (item ?? string.Empty).Trim())
                .ToList();
            if (items.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(
                    "Active condition identities must be non-empty.",
                    nameof(conditionIds));
            }

            return new ReadOnlyCollection<string>(
                items.Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToList());
        }
    }

    public sealed class DerivedCharacterStatsSnapshotV1
    {
        internal DerivedCharacterStatsSnapshotV1(
            DerivedCharacterStatInputV1 input,
            IDictionary<string, decimal> values,
            string modifierFingerprint)
        {
            CharacterInstanceId = input.CharacterInstanceId;
            BaseProfileId = input.BaseProfile.ProfileId;
            ClassId = input.BaseProfile.ClassId;
            Level = input.BaseProfile.Level;
            InputFingerprint = input.InputFingerprint;
            BaseProfileFingerprint = input.BaseProfile.Fingerprint;
            PolicyFingerprint = input.Policy.Fingerprint;
            ModifierFingerprint = modifierFingerprint;
            SourceFingerprints = new ReadOnlyCollection<string>(
                input.PermanentSources.Select(source => source.Fingerprint)
                    .ToList());
            Values = FreezeValues(values);
            Fingerprint = DerivedStatFingerprintV1.Hash(ToCanonicalString());
        }

        public string CharacterInstanceId { get; }

        public string BaseProfileId { get; }

        public string ClassId { get; }

        public int Level { get; }

        public string InputFingerprint { get; }

        public string BaseProfileFingerprint { get; }

        public string PolicyFingerprint { get; }

        public string ModifierFingerprint { get; }

        public IReadOnlyList<string> SourceFingerprints { get; }

        public IReadOnlyDictionary<string, decimal> Values { get; }

        public string Fingerprint { get; }

        public decimal MaximumHealth => GetValue(
            DerivedStatTargetIdsV1.MaximumHealth);

        public decimal MovementSpeed => GetValue(
            DerivedStatTargetIdsV1.MovementSpeed);

        public decimal Armor => GetValue(DerivedStatTargetIdsV1.Armor);

        public decimal OutgoingDamageMultiplier => GetValue(
            DerivedStatTargetIdsV1.OutgoingDamageMultiplier);

        public decimal CriticalChance => GetValue(
            DerivedStatTargetIdsV1.CriticalChance);

        public decimal CriticalMultiplier => GetValue(
            DerivedStatTargetIdsV1.CriticalMultiplier);

        public int WeaponCapacity => Decimal.ToInt32(GetValue(
            DerivedStatTargetIdsV1.WeaponCapacity));

        public int AbilityCapacity => Decimal.ToInt32(GetValue(
            DerivedStatTargetIdsV1.AbilityCapacity));

        public decimal GetValue(string targetId)
        {
            decimal value;
            if (!Values.TryGetValue(targetId ?? string.Empty, out value))
            {
                throw new KeyNotFoundException(
                    "Derived stat target was not present: "
                    + (targetId ?? string.Empty));
            }

            return value;
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            DerivedStatFingerprintV1.Append(
                builder,
                "character",
                CharacterInstanceId);
            DerivedStatFingerprintV1.Append(
                builder,
                "base-profile-id",
                BaseProfileId);
            DerivedStatFingerprintV1.Append(builder, "class", ClassId);
            DerivedStatFingerprintV1.Append(
                builder,
                "level",
                Level.ToString(CultureInfo.InvariantCulture));
            DerivedStatFingerprintV1.Append(
                builder,
                "input",
                InputFingerprint);
            DerivedStatFingerprintV1.Append(
                builder,
                "base-profile",
                BaseProfileFingerprint);
            DerivedStatFingerprintV1.Append(
                builder,
                "policy",
                PolicyFingerprint);
            DerivedStatFingerprintV1.Append(
                builder,
                "modifiers",
                ModifierFingerprint);
            foreach (string sourceFingerprint in SourceFingerprints)
            {
                DerivedStatFingerprintV1.Append(
                    builder,
                    "source",
                    sourceFingerprint);
            }
            foreach (KeyValuePair<string, decimal> pair in Values)
            {
                DerivedStatFingerprintV1.AppendDecimal(
                    builder,
                    pair.Key,
                    pair.Value);
            }

            return builder.ToString();
        }

        internal static IReadOnlyDictionary<string, decimal> FreezeValues(
            IDictionary<string, decimal> values)
        {
            var copy = new SortedDictionary<string, decimal>(
                values ?? throw new ArgumentNullException(nameof(values)),
                StringComparer.Ordinal);
            return new ReadOnlyDictionary<string, decimal>(copy);
        }
    }

    public sealed class RunCombatProfileV1
    {
        internal RunCombatProfileV1(
            RunCombatProfileInputV1 input,
            IDictionary<string, decimal> values,
            string modifierFingerprint)
        {
            RunId = input.RunId;
            CharacterInstanceId = input.CharacterStats.CharacterInstanceId;
            ClassId = input.CharacterStats.ClassId;
            Level = input.CharacterStats.Level;
            CharacterStatsFingerprint = input.CharacterStats.Fingerprint;
            RunContextFingerprint = input.RunContextFingerprint;
            InputFingerprint = input.InputFingerprint;
            PolicyFingerprint = input.Policy.Fingerprint;
            ModifierFingerprint = modifierFingerprint;
            SourceFingerprints = new ReadOnlyCollection<string>(
                input.RunSources.Select(source => source.Fingerprint).ToList());
            ActiveConditionIds = new ReadOnlyCollection<string>(
                input.ActiveConditionIds.ToList());
            Values = DerivedCharacterStatsSnapshotV1.FreezeValues(values);
            Fingerprint = DerivedStatFingerprintV1.Hash(ToCanonicalString());
        }

        public string RunId { get; }

        public string CharacterInstanceId { get; }

        public string ClassId { get; }

        public int Level { get; }

        public string CharacterStatsFingerprint { get; }

        public string RunContextFingerprint { get; }

        public string InputFingerprint { get; }

        public string PolicyFingerprint { get; }

        public string ModifierFingerprint { get; }

        public IReadOnlyList<string> SourceFingerprints { get; }

        public IReadOnlyList<string> ActiveConditionIds { get; }

        public IReadOnlyDictionary<string, decimal> Values { get; }

        public string Fingerprint { get; }

        public decimal MaximumHealth => GetValue(
            DerivedStatTargetIdsV1.MaximumHealth);

        public decimal MovementSpeed => GetValue(
            DerivedStatTargetIdsV1.MovementSpeed);

        public decimal Armor => GetValue(DerivedStatTargetIdsV1.Armor);

        public decimal OutgoingDamageMultiplier => GetValue(
            DerivedStatTargetIdsV1.OutgoingDamageMultiplier);

        public decimal CriticalChance => GetValue(
            DerivedStatTargetIdsV1.CriticalChance);

        public decimal CriticalMultiplier => GetValue(
            DerivedStatTargetIdsV1.CriticalMultiplier);

        public int WeaponCapacity => Decimal.ToInt32(GetValue(
            DerivedStatTargetIdsV1.WeaponCapacity));

        public int AbilityCapacity => Decimal.ToInt32(GetValue(
            DerivedStatTargetIdsV1.AbilityCapacity));

        public decimal GetValue(string targetId)
        {
            decimal value;
            if (!Values.TryGetValue(targetId ?? string.Empty, out value))
            {
                throw new KeyNotFoundException(
                    "Run combat stat target was not present: "
                    + (targetId ?? string.Empty));
            }

            return value;
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            DerivedStatFingerprintV1.Append(builder, "run", RunId);
            DerivedStatFingerprintV1.Append(
                builder,
                "character",
                CharacterInstanceId);
            DerivedStatFingerprintV1.Append(builder, "class", ClassId);
            DerivedStatFingerprintV1.Append(
                builder,
                "level",
                Level.ToString(CultureInfo.InvariantCulture));
            DerivedStatFingerprintV1.Append(
                builder,
                "character-stats",
                CharacterStatsFingerprint);
            DerivedStatFingerprintV1.Append(
                builder,
                "run-context",
                RunContextFingerprint);
            DerivedStatFingerprintV1.Append(
                builder,
                "input",
                InputFingerprint);
            DerivedStatFingerprintV1.Append(
                builder,
                "policy",
                PolicyFingerprint);
            DerivedStatFingerprintV1.Append(
                builder,
                "modifiers",
                ModifierFingerprint);
            foreach (string sourceFingerprint in SourceFingerprints)
            {
                DerivedStatFingerprintV1.Append(
                    builder,
                    "source",
                    sourceFingerprint);
            }
            foreach (string conditionId in ActiveConditionIds)
            {
                DerivedStatFingerprintV1.Append(
                    builder,
                    "condition",
                    conditionId);
            }
            foreach (KeyValuePair<string, decimal> pair in Values)
            {
                DerivedStatFingerprintV1.AppendDecimal(
                    builder,
                    pair.Key,
                    pair.Value);
            }

            return builder.ToString();
        }
    }

    public interface IDerivedCharacterStatComposerV1
    {
        DerivedCharacterStatsSnapshotV1 DeriveCharacter(
            DerivedCharacterStatInputV1 input);

        RunCombatProfileV1 BuildRunProfile(RunCombatProfileInputV1 input);
    }

    /// <summary>
    /// Stateless, engine-neutral full recomputation. Callers may cache by
    /// InputFingerprint at lifecycle boundaries, but this calculator owns no mutable
    /// character truth and performs no per-frame polling.
    /// </summary>
    public sealed class DefaultDerivedCharacterStatComposerV1 :
        IDerivedCharacterStatComposerV1
    {
        public DerivedCharacterStatsSnapshotV1 DeriveCharacter(
            DerivedCharacterStatInputV1 input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            ValidateTargets(
                input.BaseProfile.BaseValues.Keys,
                input.PermanentSources,
                input.Policy);
            RuntimeModifierSnapshotV1 combined = Combine(
                input.PermanentSources);
            var values = new SortedDictionary<string, decimal>(
                StringComparer.Ordinal);
            foreach (DerivedStatRuleV1 rule in input.Policy.Rules)
            {
                decimal baseValue;
                bool hasBase = input.BaseProfile.BaseValues.TryGetValue(
                    rule.TargetId,
                    out baseValue);
                if (!hasBase && rule.RequiresExplicitBaseValue)
                {
                    throw new InvalidOperationException(
                        "Base-stat profile '"
                        + input.BaseProfile.ProfileId
                        + "' must explicitly define target '"
                        + rule.TargetId
                        + "'.");
                }
                if (!hasBase)
                {
                    baseValue = rule.DefaultBaseValue;
                }

                RuntimeModifierEvaluationV1 evaluation = combined.Evaluate(
                    rule.TargetId,
                    baseValue,
                    null,
                    rule.Minimum,
                    rule.Maximum);
                ValidateWholeNumber(rule, evaluation.FinalValue);
                values.Add(rule.TargetId, evaluation.FinalValue);
            }

            return new DerivedCharacterStatsSnapshotV1(
                input,
                values,
                combined.Fingerprint);
        }

        public RunCombatProfileV1 BuildRunProfile(
            RunCombatProfileInputV1 input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            ValidateTargets(
                input.CharacterStats.Values.Keys,
                input.RunSources,
                input.Policy);
            RuntimeModifierSnapshotV1 combined = Combine(input.RunSources);
            var values = new SortedDictionary<string, decimal>(
                StringComparer.Ordinal);
            foreach (DerivedStatRuleV1 rule in input.Policy.Rules)
            {
                decimal baseValue = input.CharacterStats.GetValue(rule.TargetId);
                RuntimeModifierEvaluationV1 evaluation = combined.Evaluate(
                    rule.TargetId,
                    baseValue,
                    input.ActiveConditionIds,
                    rule.Minimum,
                    rule.Maximum);
                ValidateWholeNumber(rule, evaluation.FinalValue);
                values.Add(rule.TargetId, evaluation.FinalValue);
            }

            return new RunCombatProfileV1(
                input,
                values,
                combined.Fingerprint);
        }

        private static RuntimeModifierSnapshotV1 Combine(
            IEnumerable<DerivedStatModifierSourceV1> sources)
        {
            return new RuntimeModifierSnapshotV1(
                sources.SelectMany(source => source.Modifiers.Modifiers));
        }

        private static void ValidateTargets(
            IEnumerable<string> baseTargetIds,
            IEnumerable<DerivedStatModifierSourceV1> sources,
            DerivedStatPolicyV1 policy)
        {
            foreach (string targetId in baseTargetIds)
            {
                policy.RequireRule(targetId);
            }
            foreach (RuntimeModifierDefinitionV1 modifier in sources
                .SelectMany(source => source.Modifiers.Modifiers))
            {
                policy.RequireRule(modifier.TargetId);
            }
        }

        private static void ValidateWholeNumber(
            DerivedStatRuleV1 rule,
            decimal value)
        {
            if (rule.RequiresWholeNumber
                && value != decimal.Truncate(value))
            {
                throw new InvalidOperationException(
                    "Derived target '"
                    + rule.TargetId
                    + "' must resolve to a whole number but resolved to "
                    + value.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }
        }
    }

    internal static class DerivedStatFingerprintV1
    {
        internal static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                return BitConverter.ToString(bytes)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        internal static void Append(
            StringBuilder builder,
            string name,
            string value)
        {
            string safe = value ?? string.Empty;
            builder.Append(name)
                .Append('=')
                .Append(safe.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(safe)
                .Append('\n');
        }

        internal static void AppendDecimal(
            StringBuilder builder,
            string name,
            decimal value)
        {
            Append(
                builder,
                name,
                value.ToString(CultureInfo.InvariantCulture));
        }

        internal static void AppendNullableDecimal(
            StringBuilder builder,
            string name,
            decimal? value)
        {
            Append(
                builder,
                name,
                value.HasValue
                    ? value.Value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty);
        }
    }
}
