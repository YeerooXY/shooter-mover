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
    public static class DerivedStatTargetIds
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
        public const string GunCapacity = "loadout.gun-capacity";
        public const string AbilityCapacity = "loadout.ability-capacity";
        public const string GunDamageMultiplier =
            "gun.damage-multiplier";
        public const string GunFireRateMultiplier =
            "gun.fire-rate-multiplier";
        public const string GunReloadSpeedMultiplier =
            "gun.reload-speed-multiplier";
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

    public static class DerivedStatSourcePriorities
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

    public sealed class DerivedStatRule
    {
        public DerivedStatRule(
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
            Fingerprint = DerivedStatFingerprint.Hash(ToCanonicalString());
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
            DerivedStatFingerprint.Append(builder, "target", TargetId);
            DerivedStatFingerprint.AppendDecimal(
                builder,
                "default",
                DefaultBaseValue);
            DerivedStatFingerprint.AppendNullableDecimal(
                builder,
                "minimum",
                Minimum);
            DerivedStatFingerprint.AppendNullableDecimal(
                builder,
                "maximum",
                Maximum);
            DerivedStatFingerprint.Append(
                builder,
                "explicit-base",
                RequiresExplicitBaseValue ? "1" : "0");
            DerivedStatFingerprint.Append(
                builder,
                "whole-number",
                RequiresWholeNumber ? "1" : "0");
            return builder.ToString();
        }
    }

    public sealed class DerivedStatPolicy
    {
        private readonly IReadOnlyDictionary<string, DerivedStatRule> rulesById;

        public DerivedStatPolicy(
            string policyId,
            string policyVersion,
            IEnumerable<DerivedStatRule> rules)
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

            List<DerivedStatRule> items = (rules
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
            Rules = new ReadOnlyCollection<DerivedStatRule>(
                items.OrderBy(item => item.TargetId, StringComparer.Ordinal)
                    .ToList());
            rulesById = new ReadOnlyDictionary<string, DerivedStatRule>(
                Rules.ToDictionary(
                    item => item.TargetId,
                    StringComparer.Ordinal));
            Fingerprint = DerivedStatFingerprint.Hash(ToCanonicalString());
        }

        public string PolicyId { get; }

        public string PolicyVersion { get; }

        public IReadOnlyList<DerivedStatRule> Rules { get; }

        public string Fingerprint { get; }

        public bool TryGetRule(string targetId, out DerivedStatRule rule)
        {
            return rulesById.TryGetValue(targetId ?? string.Empty, out rule);
        }

        public DerivedStatRule RequireRule(string targetId)
        {
            DerivedStatRule rule;
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
            DerivedStatFingerprint.Append(builder, "policy", PolicyId);
            DerivedStatFingerprint.Append(
                builder,
                "version",
                PolicyVersion);
            foreach (DerivedStatRule rule in Rules)
            {
                DerivedStatFingerprint.Append(
                    builder,
                    "rule",
                    rule.ToCanonicalString());
            }

            return builder.ToString();
        }

        public static DerivedStatPolicy CreateDefault()
        {
            return new DerivedStatPolicy(
                "derived-stats.default",
                "1",
                new[]
                {
                    Rule(
                        DerivedStatTargetIds.MaximumHealth,
                        1m,
                        1m,
                        1000000m,
                        true),
                    Rule(
                        DerivedStatTargetIds.MovementSpeed,
                        0m,
                        0m,
                        1000m,
                        true),
                    Rule(DerivedStatTargetIds.Armor, 0m, 0m, 1000000m),
                    Resistance(
                        DerivedStatTargetIds.PhysicalDamageResistance),
                    Resistance(DerivedStatTargetIds.EnergyDamageResistance),
                    Resistance(DerivedStatTargetIds.ThermalDamageResistance),
                    Resistance(DerivedStatTargetIds.ChemicalDamageResistance),
                    Multiplier(
                        DerivedStatTargetIds.OutgoingDamageMultiplier),
                    Rule(
                        DerivedStatTargetIds.CriticalChance,
                        0m,
                        0m,
                        1m),
                    Rule(
                        DerivedStatTargetIds.CriticalMultiplier,
                        1m,
                        1m,
                        100m),
                    Multiplier(
                        DerivedStatTargetIds.HealingOutputMultiplier),
                    Multiplier(
                        DerivedStatTargetIds.HealingReceivedMultiplier),
                    Rule(
                        DerivedStatTargetIds.ContactDamage,
                        0m,
                        0m,
                        1000000m),
                    Multiplier(DerivedStatTargetIds.KnockbackMultiplier),
                    Capacity(DerivedStatTargetIds.GunCapacity),
                    Capacity(DerivedStatTargetIds.AbilityCapacity),
                    Multiplier(
                        DerivedStatTargetIds.GunDamageMultiplier),
                    Multiplier(
                        DerivedStatTargetIds.GunFireRateMultiplier),
                    Multiplier(
                        DerivedStatTargetIds.GunReloadSpeedMultiplier),
                    Multiplier(DerivedStatTargetIds.RewardMultiplier),
                    Multiplier(DerivedStatTargetIds.DropMultiplier),
                    Multiplier(DerivedStatTargetIds.StrongboxDropWeight),
                });
        }

        private static DerivedStatRule Rule(
            string targetId,
            decimal defaultBaseValue,
            decimal? minimum,
            decimal? maximum,
            bool requiresExplicitBaseValue = false)
        {
            return new DerivedStatRule(
                targetId,
                defaultBaseValue,
                minimum,
                maximum,
                requiresExplicitBaseValue);
        }

        private static DerivedStatRule Resistance(string targetId)
        {
            return Rule(targetId, 0m, -1m, 0.95m);
        }

        private static DerivedStatRule Multiplier(string targetId)
        {
            return Rule(targetId, 1m, 0m, 1000m);
        }

        private static DerivedStatRule Capacity(string targetId)
        {
            return new DerivedStatRule(
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
    public sealed class DerivedStatModifierSource
    {
        public DerivedStatModifierSource(
            string sourceId,
            int priority,
            string inputFingerprint,
            LiveModifierSnapshot modifiers)
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
            Fingerprint = DerivedStatFingerprint.Hash(ToCanonicalString());
        }

        public string SourceId { get; }

        public int Priority { get; }

        public string InputFingerprint { get; }

        public LiveModifierSnapshot Modifiers { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            DerivedStatFingerprint.Append(builder, "source", SourceId);
            DerivedStatFingerprint.Append(
                builder,
                "priority",
                Priority.ToString(CultureInfo.InvariantCulture));
            DerivedStatFingerprint.Append(
                builder,
                "input",
                InputFingerprint);
            DerivedStatFingerprint.Append(
                builder,
                "modifiers",
                Modifiers.Fingerprint);
            return builder.ToString();
        }
    }

    public sealed class CharacterBaseStatProfile
    {
        public CharacterBaseStatProfile(
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
            Fingerprint = DerivedStatFingerprint.Hash(ToCanonicalString());
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
            DerivedStatFingerprint.Append(builder, "profile", ProfileId);
            DerivedStatFingerprint.Append(builder, "class", ClassId);
            DerivedStatFingerprint.Append(
                builder,
                "level",
                Level.ToString(CultureInfo.InvariantCulture));
            DerivedStatFingerprint.Append(
                builder,
                "definition",
                DefinitionFingerprint);
            foreach (KeyValuePair<string, decimal> pair in BaseValues)
            {
                DerivedStatFingerprint.AppendDecimal(
                    builder,
                    pair.Key,
                    pair.Value);
            }

            return builder.ToString();
        }
    }

    public sealed class DerivedCharacterStatInput
    {
        public DerivedCharacterStatInput(
            string characterInstanceId,
            CharacterBaseStatProfile baseProfile,
            IEnumerable<DerivedStatModifierSource> permanentSources,
            DerivedStatPolicy policy)
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
            InputFingerprint = DerivedStatFingerprint.Hash(
                ToCanonicalString());
        }

        public string CharacterInstanceId { get; }

        public CharacterBaseStatProfile BaseProfile { get; }

        public IReadOnlyList<DerivedStatModifierSource> PermanentSources
        {
            get;
        }

        public DerivedStatPolicy Policy { get; }

        public string InputFingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            DerivedStatFingerprint.Append(
                builder,
                "character",
                CharacterInstanceId);
            DerivedStatFingerprint.Append(
                builder,
                "base-profile",
                BaseProfile.Fingerprint);
            DerivedStatFingerprint.Append(
                builder,
                "policy",
                Policy.Fingerprint);
            foreach (DerivedStatModifierSource source in PermanentSources)
            {
                DerivedStatFingerprint.Append(
                    builder,
                    "permanent-source",
                    source.Fingerprint);
            }

            return builder.ToString();
        }

        internal static IReadOnlyList<DerivedStatModifierSource> FreezeSources(
            IEnumerable<DerivedStatModifierSource> sources,
            string parameterName)
        {
            List<DerivedStatModifierSource> items = (sources
                ?? Array.Empty<DerivedStatModifierSource>()).ToList();
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

            return new ReadOnlyCollection<DerivedStatModifierSource>(
                items.OrderBy(item => item.Priority)
                    .ThenBy(item => item.SourceId, StringComparer.Ordinal)
                    .ThenBy(item => item.InputFingerprint, StringComparer.Ordinal)
                    .ThenBy(item => item.Modifiers.Fingerprint, StringComparer.Ordinal)
                    .ToList());
        }

        private static void ValidatePermanentSources(
            IEnumerable<DerivedStatModifierSource> sources)
        {
            LiveModifierDefinition conditional = sources
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

    public sealed class RunCombatProfileInput
    {
        public RunCombatProfileInput(
            string runId,
            string runContextFingerprint,
            DerivedCharacterStatsSnapshot characterStats,
            IEnumerable<DerivedStatModifierSource> runSources,
            IEnumerable<string> activeConditionIds,
            DerivedStatPolicy policy)
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
            RunSources = DerivedCharacterStatInput.FreezeSources(
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

            InputFingerprint = DerivedStatFingerprint.Hash(
                ToCanonicalString());
        }

        public string RunId { get; }

        public string RunContextFingerprint { get; }

        public DerivedCharacterStatsSnapshot CharacterStats { get; }

        public IReadOnlyList<DerivedStatModifierSource> RunSources { get; }

        public IReadOnlyList<string> ActiveConditionIds { get; }

        public DerivedStatPolicy Policy { get; }

        public string InputFingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            DerivedStatFingerprint.Append(builder, "run", RunId);
            DerivedStatFingerprint.Append(
                builder,
                "run-context",
                RunContextFingerprint);
            DerivedStatFingerprint.Append(
                builder,
                "character-stats",
                CharacterStats.Fingerprint);
            DerivedStatFingerprint.Append(
                builder,
                "policy",
                Policy.Fingerprint);
            foreach (DerivedStatModifierSource source in RunSources)
            {
                DerivedStatFingerprint.Append(
                    builder,
                    "run-source",
                    source.Fingerprint);
            }
            foreach (string conditionId in ActiveConditionIds)
            {
                DerivedStatFingerprint.Append(
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

    public sealed class DerivedCharacterStatsSnapshot
    {
        internal DerivedCharacterStatsSnapshot(
            DerivedCharacterStatInput input,
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
            Fingerprint = DerivedStatFingerprint.Hash(ToCanonicalString());
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
            DerivedStatTargetIds.MaximumHealth);

        public decimal MovementSpeed => GetValue(
            DerivedStatTargetIds.MovementSpeed);

        public decimal Armor => GetValue(DerivedStatTargetIds.Armor);

        public decimal OutgoingDamageMultiplier => GetValue(
            DerivedStatTargetIds.OutgoingDamageMultiplier);

        public decimal CriticalChance => GetValue(
            DerivedStatTargetIds.CriticalChance);

        public decimal CriticalMultiplier => GetValue(
            DerivedStatTargetIds.CriticalMultiplier);

        public int GunCapacity => Decimal.ToInt32(GetValue(
            DerivedStatTargetIds.GunCapacity));

        public int AbilityCapacity => Decimal.ToInt32(GetValue(
            DerivedStatTargetIds.AbilityCapacity));

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
            DerivedStatFingerprint.Append(
                builder,
                "character",
                CharacterInstanceId);
            DerivedStatFingerprint.Append(
                builder,
                "base-profile-id",
                BaseProfileId);
            DerivedStatFingerprint.Append(builder, "class", ClassId);
            DerivedStatFingerprint.Append(
                builder,
                "level",
                Level.ToString(CultureInfo.InvariantCulture));
            DerivedStatFingerprint.Append(
                builder,
                "input",
                InputFingerprint);
            DerivedStatFingerprint.Append(
                builder,
                "base-profile",
                BaseProfileFingerprint);
            DerivedStatFingerprint.Append(
                builder,
                "policy",
                PolicyFingerprint);
            DerivedStatFingerprint.Append(
                builder,
                "modifiers",
                ModifierFingerprint);
            foreach (string sourceFingerprint in SourceFingerprints)
            {
                DerivedStatFingerprint.Append(
                    builder,
                    "source",
                    sourceFingerprint);
            }
            foreach (KeyValuePair<string, decimal> pair in Values)
            {
                DerivedStatFingerprint.AppendDecimal(
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

    public sealed class RunCombatProfile
    {
        internal RunCombatProfile(
            RunCombatProfileInput input,
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
            Values = DerivedCharacterStatsSnapshot.FreezeValues(values);
            Fingerprint = DerivedStatFingerprint.Hash(ToCanonicalString());
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
            DerivedStatTargetIds.MaximumHealth);

        public decimal MovementSpeed => GetValue(
            DerivedStatTargetIds.MovementSpeed);

        public decimal Armor => GetValue(DerivedStatTargetIds.Armor);

        public decimal OutgoingDamageMultiplier => GetValue(
            DerivedStatTargetIds.OutgoingDamageMultiplier);

        public decimal CriticalChance => GetValue(
            DerivedStatTargetIds.CriticalChance);

        public decimal CriticalMultiplier => GetValue(
            DerivedStatTargetIds.CriticalMultiplier);

        public int GunCapacity => Decimal.ToInt32(GetValue(
            DerivedStatTargetIds.GunCapacity));

        public int AbilityCapacity => Decimal.ToInt32(GetValue(
            DerivedStatTargetIds.AbilityCapacity));

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
            DerivedStatFingerprint.Append(builder, "run", RunId);
            DerivedStatFingerprint.Append(
                builder,
                "character",
                CharacterInstanceId);
            DerivedStatFingerprint.Append(builder, "class", ClassId);
            DerivedStatFingerprint.Append(
                builder,
                "level",
                Level.ToString(CultureInfo.InvariantCulture));
            DerivedStatFingerprint.Append(
                builder,
                "character-stats",
                CharacterStatsFingerprint);
            DerivedStatFingerprint.Append(
                builder,
                "run-context",
                RunContextFingerprint);
            DerivedStatFingerprint.Append(
                builder,
                "input",
                InputFingerprint);
            DerivedStatFingerprint.Append(
                builder,
                "policy",
                PolicyFingerprint);
            DerivedStatFingerprint.Append(
                builder,
                "modifiers",
                ModifierFingerprint);
            foreach (string sourceFingerprint in SourceFingerprints)
            {
                DerivedStatFingerprint.Append(
                    builder,
                    "source",
                    sourceFingerprint);
            }
            foreach (string conditionId in ActiveConditionIds)
            {
                DerivedStatFingerprint.Append(
                    builder,
                    "condition",
                    conditionId);
            }
            foreach (KeyValuePair<string, decimal> pair in Values)
            {
                DerivedStatFingerprint.AppendDecimal(
                    builder,
                    pair.Key,
                    pair.Value);
            }

            return builder.ToString();
        }
    }

    public interface IDerivedCharacterStatComposer
    {
        DerivedCharacterStatsSnapshot DeriveCharacter(
            DerivedCharacterStatInput input);

        RunCombatProfile BuildRunProfile(RunCombatProfileInput input);
    }

    /// <summary>
    /// Stateless, engine-neutral full recomputation. Callers may cache by
    /// InputFingerprint at lifecycle boundaries, but this calculator owns no mutable
    /// character truth and performs no per-frame polling.
    /// </summary>
    public sealed class DefaultDerivedCharacterStatComposer :
        IDerivedCharacterStatComposer
    {
        public DerivedCharacterStatsSnapshot DeriveCharacter(
            DerivedCharacterStatInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            ValidateTargets(
                input.BaseProfile.BaseValues.Keys,
                input.PermanentSources,
                input.Policy);
            LiveModifierSnapshot combined = Combine(
                input.PermanentSources);
            var values = new SortedDictionary<string, decimal>(
                StringComparer.Ordinal);
            foreach (DerivedStatRule rule in input.Policy.Rules)
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

                LiveModifierEvaluation evaluation = combined.Evaluate(
                    rule.TargetId,
                    baseValue,
                    null,
                    rule.Minimum,
                    rule.Maximum);
                ValidateWholeNumber(rule, evaluation.FinalValue);
                values.Add(rule.TargetId, evaluation.FinalValue);
            }

            return new DerivedCharacterStatsSnapshot(
                input,
                values,
                combined.Fingerprint);
        }

        public RunCombatProfile BuildRunProfile(
            RunCombatProfileInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            ValidateTargets(
                input.CharacterStats.Values.Keys,
                input.RunSources,
                input.Policy);
            LiveModifierSnapshot combined = Combine(input.RunSources);
            var values = new SortedDictionary<string, decimal>(
                StringComparer.Ordinal);
            foreach (DerivedStatRule rule in input.Policy.Rules)
            {
                decimal baseValue = input.CharacterStats.GetValue(rule.TargetId);
                LiveModifierEvaluation evaluation = combined.Evaluate(
                    rule.TargetId,
                    baseValue,
                    input.ActiveConditionIds,
                    rule.Minimum,
                    rule.Maximum);
                ValidateWholeNumber(rule, evaluation.FinalValue);
                values.Add(rule.TargetId, evaluation.FinalValue);
            }

            return new RunCombatProfile(
                input,
                values,
                combined.Fingerprint);
        }

        private static LiveModifierSnapshot Combine(
            IEnumerable<DerivedStatModifierSource> sources)
        {
            return new LiveModifierSnapshot(
                sources.SelectMany(source => source.Modifiers.Modifiers));
        }

        private static void ValidateTargets(
            IEnumerable<string> baseTargetIds,
            IEnumerable<DerivedStatModifierSource> sources,
            DerivedStatPolicy policy)
        {
            foreach (string targetId in baseTargetIds)
            {
                policy.RequireRule(targetId);
            }
            foreach (LiveModifierDefinition modifier in sources
                .SelectMany(source => source.Modifiers.Modifiers))
            {
                policy.RequireRule(modifier.TargetId);
            }
        }

        private static void ValidateWholeNumber(
            DerivedStatRule rule,
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

    internal static class DerivedStatFingerprint
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
