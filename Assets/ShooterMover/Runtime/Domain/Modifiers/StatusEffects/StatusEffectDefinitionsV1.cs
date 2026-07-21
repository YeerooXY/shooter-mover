using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Domain.Modifiers;

namespace ShooterMover.Domain.Modifiers.StatusEffects
{
    public enum StatusEffectStackingPolicyV1
    {
        Add = 1,
        Refresh = 2,
        Replace = 3,
        Ignore = 4,
    }

    public sealed class StatusEffectDefinitionV1
    {
        public const int CurrentSchemaVersion = 1;

        public StatusEffectDefinitionV1(
            string effectId,
            string contentVersion,
            long durationTicks,
            int maximumStacks,
            StatusEffectStackingPolicyV1 stackingPolicy,
            string dispelCategoryId,
            IEnumerable<RuntimeModifierDefinitionV1> modifierContributions,
            int schemaVersion = CurrentSchemaVersion)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion),
                    "Unsupported status-effect schema version.");
            }
            if (string.IsNullOrWhiteSpace(effectId))
            {
                throw new ArgumentException(
                    "A status-effect identity is required.",
                    nameof(effectId));
            }
            if (string.IsNullOrWhiteSpace(contentVersion))
            {
                throw new ArgumentException(
                    "A status-effect content version is required.",
                    nameof(contentVersion));
            }
            if (durationTicks < 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(durationTicks));
            }
            if (maximumStacks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumStacks));
            }
            if (!Enum.IsDefined(typeof(StatusEffectStackingPolicyV1), stackingPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(stackingPolicy));
            }
            if (stackingPolicy != StatusEffectStackingPolicyV1.Add
                && maximumStacks != 1)
            {
                throw new ArgumentException(
                    "Refresh, replace, and ignore effects must use exactly one shared stack.",
                    nameof(maximumStacks));
            }
            if (string.IsNullOrWhiteSpace(dispelCategoryId))
            {
                throw new ArgumentException(
                    "A dispel category identity is required.",
                    nameof(dispelCategoryId));
            }

            List<RuntimeModifierDefinitionV1> contributions =
                (modifierContributions
                    ?? Array.Empty<RuntimeModifierDefinitionV1>())
                .ToList();
            if (contributions.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Status-effect modifier contributions must be non-null.",
                    nameof(modifierContributions));
            }

            SchemaVersion = schemaVersion;
            EffectId = effectId.Trim();
            ContentVersion = contentVersion.Trim();
            DurationTicks = durationTicks;
            MaximumStacks = maximumStacks;
            StackingPolicy = stackingPolicy;
            DispelCategoryId = dispelCategoryId.Trim();
            ModifierContributions =
                new ReadOnlyCollection<RuntimeModifierDefinitionV1>(
                    contributions
                        .OrderBy(item => item.TargetId, StringComparer.Ordinal)
                        .ThenBy(item => item.ConditionId, StringComparer.Ordinal)
                        .ThenBy(item => item.Operation)
                        .ThenBy(item => item.SourceId, StringComparer.Ordinal)
                        .ToList());
            Fingerprint = StatusEffectFingerprintV1.Hash(ToCanonicalString());
        }

        public int SchemaVersion { get; }

        public string EffectId { get; }

        public string ContentVersion { get; }

        public long DurationTicks { get; }

        public int MaximumStacks { get; }

        public StatusEffectStackingPolicyV1 StackingPolicy { get; }

        public string DispelCategoryId { get; }

        public IReadOnlyList<RuntimeModifierDefinitionV1> ModifierContributions
        {
            get;
        }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            StatusEffectFingerprintV1.Append(
                builder,
                "schema",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprintV1.Append(builder, "effect", EffectId);
            StatusEffectFingerprintV1.Append(
                builder,
                "content-version",
                ContentVersion);
            StatusEffectFingerprintV1.Append(
                builder,
                "duration",
                DurationTicks.ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprintV1.Append(
                builder,
                "maximum-stacks",
                MaximumStacks.ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprintV1.Append(
                builder,
                "stacking-policy",
                ((int)StackingPolicy).ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprintV1.Append(
                builder,
                "dispel-category",
                DispelCategoryId);
            foreach (RuntimeModifierDefinitionV1 contribution in
                ModifierContributions)
            {
                StatusEffectFingerprintV1.Append(
                    builder,
                    "modifier",
                    contribution.ToCanonicalString());
            }

            return builder.ToString();
        }
    }

    public sealed class StatusEffectCatalogV1
    {
        public const int CurrentSchemaVersion = 1;

        private readonly IReadOnlyDictionary<string, StatusEffectDefinitionV1>
            definitionsById;

        public StatusEffectCatalogV1(
            string catalogId,
            string contentVersion,
            IEnumerable<StatusEffectDefinitionV1> definitions,
            int schemaVersion = CurrentSchemaVersion)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion),
                    "Unsupported status-effect catalog schema version.");
            }
            if (string.IsNullOrWhiteSpace(catalogId))
            {
                throw new ArgumentException(
                    "A status-effect catalog identity is required.",
                    nameof(catalogId));
            }
            if (string.IsNullOrWhiteSpace(contentVersion))
            {
                throw new ArgumentException(
                    "A status-effect catalog content version is required.",
                    nameof(contentVersion));
            }

            List<StatusEffectDefinitionV1> items =
                (definitions
                    ?? throw new ArgumentNullException(nameof(definitions)))
                .ToList();
            if (items.Count == 0 || items.Any(item => item == null))
            {
                throw new ArgumentException(
                    "At least one non-null status-effect definition is required.",
                    nameof(definitions));
            }
            if (items.Select(item => item.EffectId)
                .Distinct(StringComparer.Ordinal)
                .Count() != items.Count)
            {
                throw new ArgumentException(
                    "Status-effect identities must be unique.",
                    nameof(definitions));
            }

            SchemaVersion = schemaVersion;
            CatalogId = catalogId.Trim();
            ContentVersion = contentVersion.Trim();
            Definitions = new ReadOnlyCollection<StatusEffectDefinitionV1>(
                items.OrderBy(item => item.EffectId, StringComparer.Ordinal)
                    .ToList());
            definitionsById =
                new ReadOnlyDictionary<string, StatusEffectDefinitionV1>(
                    Definitions.ToDictionary(
                        item => item.EffectId,
                        StringComparer.Ordinal));
            Fingerprint = StatusEffectFingerprintV1.Hash(ToCanonicalString());
        }

        public int SchemaVersion { get; }

        public string CatalogId { get; }

        public string ContentVersion { get; }

        public IReadOnlyList<StatusEffectDefinitionV1> Definitions { get; }

        public string Fingerprint { get; }

        public bool TryGetDefinition(
            string effectId,
            out StatusEffectDefinitionV1 definition)
        {
            return definitionsById.TryGetValue(
                effectId ?? string.Empty,
                out definition);
        }

        public StatusEffectDefinitionV1 RequireDefinition(string effectId)
        {
            StatusEffectDefinitionV1 definition;
            if (!TryGetDefinition(effectId, out definition))
            {
                throw new InvalidOperationException(
                    "Unknown status-effect definition '"
                    + (effectId ?? string.Empty)
                    + "'.");
            }

            return definition;
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            StatusEffectFingerprintV1.Append(
                builder,
                "schema",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprintV1.Append(builder, "catalog", CatalogId);
            StatusEffectFingerprintV1.Append(
                builder,
                "content-version",
                ContentVersion);
            foreach (StatusEffectDefinitionV1 definition in Definitions)
            {
                StatusEffectFingerprintV1.Append(
                    builder,
                    "definition",
                    definition.ToCanonicalString());
            }

            return builder.ToString();
        }
    }

    public abstract class StatusEffectCommandV1
    {
        protected StatusEffectCommandV1(
            string operationId,
            string subjectId,
            int lifecycleGeneration,
            long simulationTick,
            string commandKind)
        {
            if (string.IsNullOrWhiteSpace(operationId))
            {
                throw new ArgumentException(
                    "A status-effect operation identity is required.",
                    nameof(operationId));
            }
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                throw new ArgumentException(
                    "A status-effect subject identity is required.",
                    nameof(subjectId));
            }
            if (lifecycleGeneration < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lifecycleGeneration));
            }
            if (simulationTick < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationTick));
            }
            if (string.IsNullOrWhiteSpace(commandKind))
            {
                throw new ArgumentException(
                    "A status-effect command kind is required.",
                    nameof(commandKind));
            }

            OperationId = operationId.Trim();
            SubjectId = subjectId.Trim();
            LifecycleGeneration = lifecycleGeneration;
            SimulationTick = simulationTick;
            CommandKind = commandKind.Trim();
        }

        public string OperationId { get; }

        public string SubjectId { get; }

        public int LifecycleGeneration { get; }

        public long SimulationTick { get; }

        public string CommandKind { get; }

        public abstract string Fingerprint { get; }
    }

    public sealed class ApplyStatusEffectCommandV1 : StatusEffectCommandV1
    {
        private readonly string fingerprint;

        public ApplyStatusEffectCommandV1(
            string operationId,
            string subjectId,
            int lifecycleGeneration,
            long simulationTick,
            string effectId,
            string sourceId)
            : base(
                operationId,
                subjectId,
                lifecycleGeneration,
                simulationTick,
                "status-effect.apply")
        {
            if (string.IsNullOrWhiteSpace(effectId))
            {
                throw new ArgumentException(
                    "A status-effect definition identity is required.",
                    nameof(effectId));
            }
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException(
                    "A status-effect source identity is required.",
                    nameof(sourceId));
            }

            EffectId = effectId.Trim();
            SourceId = sourceId.Trim();
            fingerprint = StatusEffectFingerprintV1.Hash(ToCanonicalString());
        }

        public string EffectId { get; }

        public string SourceId { get; }

        public override string Fingerprint
        {
            get { return fingerprint; }
        }

        public string ToCanonicalString()
        {
            var builder = CommandCanonicalPrefix();
            StatusEffectFingerprintV1.Append(builder, "effect", EffectId);
            StatusEffectFingerprintV1.Append(builder, "source", SourceId);
            return builder.ToString();
        }

        private StringBuilder CommandCanonicalPrefix()
        {
            return StatusEffectCommandCanonicalV1.BuildPrefix(this);
        }
    }

    public sealed class AdvanceStatusEffectTickCommandV1 :
        StatusEffectCommandV1
    {
        private readonly string fingerprint;

        public AdvanceStatusEffectTickCommandV1(
            string operationId,
            string subjectId,
            int lifecycleGeneration,
            long simulationTick)
            : base(
                operationId,
                subjectId,
                lifecycleGeneration,
                simulationTick,
                "status-effect.advance")
        {
            fingerprint = StatusEffectFingerprintV1.Hash(
                StatusEffectCommandCanonicalV1.BuildPrefix(this).ToString());
        }

        public override string Fingerprint
        {
            get { return fingerprint; }
        }
    }

    public sealed class DispelStatusEffectsCommandV1 : StatusEffectCommandV1
    {
        private readonly string fingerprint;

        public DispelStatusEffectsCommandV1(
            string operationId,
            string subjectId,
            int lifecycleGeneration,
            long simulationTick,
            string dispelCategoryId)
            : base(
                operationId,
                subjectId,
                lifecycleGeneration,
                simulationTick,
                "status-effect.dispel")
        {
            if (string.IsNullOrWhiteSpace(dispelCategoryId))
            {
                throw new ArgumentException(
                    "A dispel category identity is required.",
                    nameof(dispelCategoryId));
            }

            DispelCategoryId = dispelCategoryId.Trim();
            var builder = StatusEffectCommandCanonicalV1.BuildPrefix(this);
            StatusEffectFingerprintV1.Append(
                builder,
                "dispel-category",
                DispelCategoryId);
            fingerprint = StatusEffectFingerprintV1.Hash(builder.ToString());
        }

        public string DispelCategoryId { get; }

        public override string Fingerprint
        {
            get { return fingerprint; }
        }
    }

    public sealed class RestartStatusEffectLifecycleCommandV1 :
        StatusEffectCommandV1
    {
        private readonly string fingerprint;

        public RestartStatusEffectLifecycleCommandV1(
            string operationId,
            string subjectId,
            int lifecycleGeneration,
            int nextLifecycleGeneration,
            long simulationTick)
            : base(
                operationId,
                subjectId,
                lifecycleGeneration,
                simulationTick,
                "status-effect.restart")
        {
            if (nextLifecycleGeneration != lifecycleGeneration + 1)
            {
                throw new ArgumentException(
                    "A status-effect lifecycle restart must increment generation exactly once.",
                    nameof(nextLifecycleGeneration));
            }

            NextLifecycleGeneration = nextLifecycleGeneration;
            var builder = StatusEffectCommandCanonicalV1.BuildPrefix(this);
            StatusEffectFingerprintV1.Append(
                builder,
                "next-generation",
                NextLifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture));
            fingerprint = StatusEffectFingerprintV1.Hash(builder.ToString());
        }

        public int NextLifecycleGeneration { get; }

        public override string Fingerprint
        {
            get { return fingerprint; }
        }
    }

    internal static class StatusEffectCommandCanonicalV1
    {
        internal static StringBuilder BuildPrefix(StatusEffectCommandV1 command)
        {
            var builder = new StringBuilder();
            StatusEffectFingerprintV1.Append(
                builder,
                "kind",
                command.CommandKind);
            StatusEffectFingerprintV1.Append(
                builder,
                "operation",
                command.OperationId);
            StatusEffectFingerprintV1.Append(
                builder,
                "subject",
                command.SubjectId);
            StatusEffectFingerprintV1.Append(
                builder,
                "generation",
                command.LifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture));
            StatusEffectFingerprintV1.Append(
                builder,
                "tick",
                command.SimulationTick.ToString(
                    CultureInfo.InvariantCulture));
            return builder;
        }
    }
}
