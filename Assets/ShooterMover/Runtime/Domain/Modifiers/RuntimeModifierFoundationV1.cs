using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ShooterMover.Domain.Modifiers
{
    public enum RuntimeModifierOperationV1
    {
        Flat = 1,
        Percentage = 2,
        Multiplicative = 3,
    }

    /// <summary>
    /// One reusable numerical contribution. Target identities are deliberately open
    /// content IDs: adding combat.critical-chance, rewards.strongbox-drop-weight, or a
    /// future stat does not require extending an enum or editing this evaluator.
    /// </summary>
    public sealed class RuntimeModifierDefinitionV1
    {
        public RuntimeModifierDefinitionV1(
            string sourceId,
            string targetId,
            RuntimeModifierOperationV1 operation,
            decimal value,
            string conditionId = "")
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException(
                    "A modifier source identity is required.",
                    nameof(sourceId));
            }
            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException(
                    "A modifier target identity is required.",
                    nameof(targetId));
            }
            if (operation == RuntimeModifierOperationV1.Multiplicative
                && value <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            SourceId = sourceId.Trim();
            TargetId = targetId.Trim();
            Operation = operation;
            Value = value;
            ConditionId = (conditionId ?? string.Empty).Trim();
            Fingerprint = RuntimeModifierFingerprintV1.Hash(
                ToCanonicalString());
        }

        public string SourceId { get; }

        public string TargetId { get; }

        public RuntimeModifierOperationV1 Operation { get; }

        public decimal Value { get; }

        public string ConditionId { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return SourceId
                + "|"
                + TargetId
                + "|"
                + Operation
                + "|"
                + Value.ToString(CultureInfo.InvariantCulture)
                + "|"
                + ConditionId;
        }
    }

    public sealed class RuntimeModifierEvaluationV1
    {
        public RuntimeModifierEvaluationV1(
            string targetId,
            decimal baseValue,
            decimal unclampedValue,
            decimal finalValue,
            IEnumerable<RuntimeModifierDefinitionV1> appliedModifiers)
        {
            TargetId = targetId
                ?? throw new ArgumentNullException(nameof(targetId));
            BaseValue = baseValue;
            UnclampedValue = unclampedValue;
            FinalValue = finalValue;
            AppliedModifiers = new ReadOnlyCollection<
                RuntimeModifierDefinitionV1>(
                    (appliedModifiers
                        ?? Array.Empty<RuntimeModifierDefinitionV1>())
                    .ToList());
            Fingerprint = RuntimeModifierFingerprintV1.Hash(
                TargetId
                    + "|"
                    + BaseValue.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + UnclampedValue.ToString(
                        CultureInfo.InvariantCulture)
                    + "|"
                    + FinalValue.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + string.Join(
                        ";",
                        AppliedModifiers.Select(item => item.Fingerprint)));
        }

        public string TargetId { get; }

        public decimal BaseValue { get; }

        public decimal UnclampedValue { get; }

        public decimal FinalValue { get; }

        public IReadOnlyList<RuntimeModifierDefinitionV1> AppliedModifiers
        {
            get;
        }

        public string Fingerprint { get; }
    }

    /// <summary>
    /// Immutable set of contributions from skills, gear, class definitions, active
    /// events, temporary run effects, party auras, or future content sources.
    /// </summary>
    public sealed class RuntimeModifierSnapshotV1
    {
        public RuntimeModifierSnapshotV1(
            IEnumerable<RuntimeModifierDefinitionV1> modifiers)
        {
            var items = (modifiers
                ?? Array.Empty<RuntimeModifierDefinitionV1>()).ToList();
            if (items.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Modifier definitions must be non-null.",
                    nameof(modifiers));
            }

            Modifiers = new ReadOnlyCollection<RuntimeModifierDefinitionV1>(
                items.OrderBy(item => item.TargetId, StringComparer.Ordinal)
                    .ThenBy(item => item.ConditionId, StringComparer.Ordinal)
                    .ThenBy(item => item.Operation)
                    .ThenBy(item => item.SourceId, StringComparer.Ordinal)
                    .ToList());
            Fingerprint = RuntimeModifierFingerprintV1.Hash(
                string.Join(";", Modifiers.Select(item => item.Fingerprint)));
        }

        public IReadOnlyList<RuntimeModifierDefinitionV1> Modifiers { get; }

        public string Fingerprint { get; }

        public RuntimeModifierEvaluationV1 Evaluate(
            string targetId,
            decimal baseValue,
            IEnumerable<string> activeConditionIds = null,
            decimal? minimum = null,
            decimal? maximum = null)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException(
                    "A modifier target identity is required.",
                    nameof(targetId));
            }
            if (minimum.HasValue
                && maximum.HasValue
                && minimum.Value > maximum.Value)
            {
                throw new ArgumentException(
                    "The modifier minimum cannot exceed the maximum.");
            }

            var active = new HashSet<string>(
                activeConditionIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            List<RuntimeModifierDefinitionV1> applied = Modifiers
                .Where(item => string.Equals(
                    item.TargetId,
                    targetId,
                    StringComparison.Ordinal))
                .Where(item => string.IsNullOrEmpty(item.ConditionId)
                    || active.Contains(item.ConditionId))
                .ToList();

            decimal flat = applied
                .Where(item => item.Operation
                    == RuntimeModifierOperationV1.Flat)
                .Sum(item => item.Value);
            decimal percentage = applied
                .Where(item => item.Operation
                    == RuntimeModifierOperationV1.Percentage)
                .Sum(item => item.Value);
            decimal multiplier = applied
                .Where(item => item.Operation
                    == RuntimeModifierOperationV1.Multiplicative)
                .Aggregate(1m, (current, item) => current * item.Value);

            decimal value = checked(
                checked(baseValue + flat)
                    * checked(1m + percentage)
                    * multiplier);
            decimal final = value;
            if (minimum.HasValue && final < minimum.Value)
            {
                final = minimum.Value;
            }
            if (maximum.HasValue && final > maximum.Value)
            {
                final = maximum.Value;
            }

            return new RuntimeModifierEvaluationV1(
                targetId.Trim(),
                baseValue,
                value,
                final,
                applied);
        }
    }

    public sealed class FactWindowConditionDefinitionV1
    {
        public FactWindowConditionDefinitionV1(
            string conditionId,
            string observedFactTypeId,
            int requiredFactCount,
            long windowTicks,
            long activeDurationTicks,
            bool consumeWindowOnActivation = true)
        {
            if (string.IsNullOrWhiteSpace(conditionId))
            {
                throw new ArgumentException(
                    "A condition identity is required.",
                    nameof(conditionId));
            }
            if (string.IsNullOrWhiteSpace(observedFactTypeId))
            {
                throw new ArgumentException(
                    "An observed fact type is required.",
                    nameof(observedFactTypeId));
            }
            if (requiredFactCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredFactCount));
            }
            if (windowTicks < 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(windowTicks));
            }
            if (activeDurationTicks < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(activeDurationTicks));
            }

            ConditionId = conditionId.Trim();
            ObservedFactTypeId = observedFactTypeId.Trim();
            RequiredFactCount = requiredFactCount;
            WindowTicks = windowTicks;
            ActiveDurationTicks = activeDurationTicks;
            ConsumeWindowOnActivation = consumeWindowOnActivation;
            Fingerprint = RuntimeModifierFingerprintV1.Hash(
                ToCanonicalString());
        }

        public string ConditionId { get; }

        public string ObservedFactTypeId { get; }

        public int RequiredFactCount { get; }

        public long WindowTicks { get; }

        public long ActiveDurationTicks { get; }

        public bool ConsumeWindowOnActivation { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return ConditionId
                + "|"
                + ObservedFactTypeId
                + "|"
                + RequiredFactCount.ToString(CultureInfo.InvariantCulture)
                + "|"
                + WindowTicks.ToString(CultureInfo.InvariantCulture)
                + "|"
                + ActiveDurationTicks.ToString(
                    CultureInfo.InvariantCulture)
                + "|"
                + ConsumeWindowOnActivation;
        }
    }

    internal static class RuntimeModifierFingerprintV1
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
    }
}
