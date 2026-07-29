using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ShooterMover.Application.Modifiers;
using ShooterMover.Domain.Modifiers.StatusEffects;

namespace ShooterMover.Application.Modifiers.StatusEffects
{
    public sealed class FactWindowStatusEffectBinding
    {
        public FactWindowStatusEffectBinding(
            string conditionId,
            string effectId,
            string sourceId)
        {
            if (string.IsNullOrWhiteSpace(conditionId))
            {
                throw new ArgumentException(
                    "A fact-window condition identity is required.",
                    nameof(conditionId));
            }
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

            ConditionId = conditionId.Trim();
            EffectId = effectId.Trim();
            SourceId = sourceId.Trim();
        }

        public string ConditionId { get; }

        public string EffectId { get; }

        public string SourceId { get; }
    }

    /// <summary>
    /// Narrow generic bridge from accepted fact-window activation facts to ordinary
    /// status-effect commands. A killing-spree skill is data plus one binding; the
    /// bridge contains no killing-spree or skill-specific branch.
    /// </summary>
    public sealed class FactWindowStatusEffectBridge
    {
        private readonly IReadOnlyDictionary<
            string,
            FactWindowStatusEffectBinding> bindingsByCondition;

        public FactWindowStatusEffectBridge(
            IEnumerable<FactWindowStatusEffectBinding> bindings)
        {
            List<FactWindowStatusEffectBinding> items =
                (bindings
                    ?? throw new ArgumentNullException(nameof(bindings)))
                .ToList();
            if (items.Count == 0 || items.Any(item => item == null))
            {
                throw new ArgumentException(
                    "At least one non-null condition-to-effect binding is required.",
                    nameof(bindings));
            }
            if (items.Select(item => item.ConditionId)
                .Distinct(StringComparer.Ordinal)
                .Count() != items.Count)
            {
                throw new ArgumentException(
                    "Fact-window condition bindings must be unique.",
                    nameof(bindings));
            }

            bindingsByCondition =
                new ReadOnlyDictionary<
                    string,
                    FactWindowStatusEffectBinding>(
                    items.ToDictionary(
                        item => item.ConditionId,
                        StringComparer.Ordinal));
        }

        public bool TryCreateApplyCommand(
            LiveConditionActivationFact activation,
            string operationId,
            int lifecycleGeneration,
            out ApplyStatusEffectCommand command)
        {
            command = null;
            if (activation == null)
            {
                return false;
            }

            FactWindowStatusEffectBinding binding;
            if (!bindingsByCondition.TryGetValue(
                activation.ConditionId,
                out binding))
            {
                return false;
            }

            command = new ApplyStatusEffectCommand(
                operationId,
                activation.SubjectId,
                lifecycleGeneration,
                activation.ActivationTick,
                binding.EffectId,
                binding.SourceId);
            return true;
        }
    }
}
