using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Domain.Modifiers;

namespace ShooterMover.Domain.Modifiers.StatusEffects
{
    public enum StatusEffectStackingPolicy
    {
        Add = 1,
        Refresh = 2,
        Replace = 3,
        Ignore = 4,
    }

    public sealed class StatusEffectDefinition
    {
        public const int CurrentSchemaVersion = 1;

        public StatusEffectDefinition(
            string effectId,
            string contentVersion,
            long durationTicks,
            int maximumStacks,
            StatusEffectStackingPolicy stackingPolicy,
            string dispelCategoryId,
            IEnumerable<LiveModifierDefinition> modifierContributions,
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
            if (!Enum.IsDefined(typeof(StatusEffectStackingPolicy), stackingPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(stackingPolicy));
            }
            if (stackingPolicy != StatusEffectStackingPolicy.Add
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

            List<LiveModifierDefinition> contributions =
                (modifierContributions
                    ?? Array.Empty<LiveModifierDefinition>())
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
                new ReadOnlyCollection<LiveModifierDefinition>(
                    contributions
                        .OrderBy(item => item.TargetId, StringComparer.Ordinal)
                        .ThenBy(item => item.ConditionId, StringComparer.Ordinal)
                        .ThenBy(item => item.Operation)
                        .ThenBy(item => item.SourceId, StringComparer.Ordinal)
                        .ToList());
            Fingerprint = StatusEffectFingerprint.Hash(ToCanonicalString());
        }

        public int SchemaVersion { get; }

        public string EffectId { get; }

        public string ContentVersion { get; }

        public long DurationTicks { get; }

        public int MaximumStacks { get; }

        public StatusEffectStackingPolicy StackingPolicy { get; }

        public string DispelCategoryId { get; }

        public IReadOnlyList<LiveModifierDefinition> ModifierContributions
        {
            get;
        }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            StatusEffectFingerprint.Append(
                builder,
                "schema",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprint.Append(builder, "effect", EffectId);
            StatusEffectFingerprint.Append(
                builder,
                "content-version",
                ContentVersion);
            StatusEffectFingerprint.Append(
                builder,
                "duration",
                DurationTicks.ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprint.Append(
                builder,
                "maximum-stacks",
                MaximumStacks.ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprint.Append(
                builder,
                "stacking-policy",
                ((int)StackingPolicy).ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprint.Append(
                builder,
                "dispel-category",
                DispelCategoryId);
            foreach (LiveModifierDefinition contribution in
                ModifierContributions)
            {
                StatusEffectFingerprint.Append(
                    builder,
                    "modifier",
                    contribution.ToCanonicalString());
            }

            return builder.ToString();
        }
    }

    public sealed class StatusEffectCatalog
    {
        public const int CurrentSchemaVersion = 1;

        private readonly IReadOnlyDictionary<string, StatusEffectDefinition>
            definitionsById;

        public StatusEffectCatalog(
            string catalogId,
            string contentVersion,
            IEnumerable<StatusEffectDefinition> definitions,
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

            List<StatusEffectDefinition> items =
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
            Definitions = new ReadOnlyCollection<StatusEffectDefinition>(
                items.OrderBy(item => item.EffectId, StringComparer.Ordinal)
                    .ToList());
            definitionsById =
                new ReadOnlyDictionary<string, StatusEffectDefinition>(
                    Definitions.ToDictionary(
                        item => item.EffectId,
                        StringComparer.Ordinal));
            Fingerprint = StatusEffectFingerprint.Hash(ToCanonicalString());
        }

        public int SchemaVersion { get; }

        public string CatalogId { get; }

        public string ContentVersion { get; }

        public IReadOnlyList<StatusEffectDefinition> Definitions { get; }

        public string Fingerprint { get; }

        public bool TryGetDefinition(
            string effectId,
            out StatusEffectDefinition definition)
        {
            return definitionsById.TryGetValue(
                effectId ?? string.Empty,
                out definition);
        }

        public StatusEffectDefinition RequireDefinition(string effectId)
        {
            StatusEffectDefinition definition;
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
            StatusEffectFingerprint.Append(
                builder,
                "schema",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprint.Append(builder, "catalog", CatalogId);
            StatusEffectFingerprint.Append(
                builder,
                "content-version",
                ContentVersion);
            foreach (StatusEffectDefinition definition in Definitions)
            {
                StatusEffectFingerprint.Append(
                    builder,
                    "definition",
                    definition.ToCanonicalString());
            }

            return builder.ToString();
        }
    }

    public abstract class LegacyStatusEffectCommand
    {
        protected LegacyStatusEffectCommand(
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

    public sealed class ApplyStatusEffectCommand : LegacyStatusEffectCommand
    {
        private readonly string fingerprint;

        public ApplyStatusEffectCommand(
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
            fingerprint = StatusEffectFingerprint.Hash(ToCanonicalString());
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
            StatusEffectFingerprint.Append(builder, "effect", EffectId);
            StatusEffectFingerprint.Append(builder, "source", SourceId);
            return builder.ToString();
        }

        private StringBuilder CommandCanonicalPrefix()
        {
            return StatusEffectCommand.BuildPrefix(this);
        }
    }

    public sealed class AdvanceStatusEffectTickCommand :
        LegacyStatusEffectCommand
    {
        private readonly string fingerprint;

        public AdvanceStatusEffectTickCommand(
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
            fingerprint = StatusEffectFingerprint.Hash(
                StatusEffectCommand.BuildPrefix(this).ToString());
        }

        public override string Fingerprint
        {
            get { return fingerprint; }
        }
    }

    public sealed class DispelStatusEffectsCommand : LegacyStatusEffectCommand
    {
        private readonly string fingerprint;

        public DispelStatusEffectsCommand(
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
            var builder = StatusEffectCommand.BuildPrefix(this);
            StatusEffectFingerprint.Append(
                builder,
                "dispel-category",
                DispelCategoryId);
            fingerprint = StatusEffectFingerprint.Hash(builder.ToString());
        }

        public string DispelCategoryId { get; }

        public override string Fingerprint
        {
            get { return fingerprint; }
        }
    }

    public sealed class RestartStatusEffectLifecycleCommand :
        LegacyStatusEffectCommand
    {
        private readonly string fingerprint;

        public RestartStatusEffectLifecycleCommand(
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
            var builder = StatusEffectCommand.BuildPrefix(this);
            StatusEffectFingerprint.Append(
                builder,
                "next-generation",
                NextLifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture));
            fingerprint = StatusEffectFingerprint.Hash(builder.ToString());
        }

        public int NextLifecycleGeneration { get; }

        public override string Fingerprint
        {
            get { return fingerprint; }
        }
    }

    internal static class StatusEffectCommand
    {
        internal static StringBuilder BuildPrefix(LegacyStatusEffectCommand command)
        {
            var builder = new StringBuilder();
            StatusEffectFingerprint.Append(
                builder,
                "kind",
                command.CommandKind);
            StatusEffectFingerprint.Append(
                builder,
                "operation",
                command.OperationId);
            StatusEffectFingerprint.Append(
                builder,
                "subject",
                command.SubjectId);
            StatusEffectFingerprint.Append(
                builder,
                "generation",
                command.LifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture));
            StatusEffectFingerprint.Append(
                builder,
                "tick",
                command.SimulationTick.ToString(
                    CultureInfo.InvariantCulture));
            return builder;
        }
    }
}
