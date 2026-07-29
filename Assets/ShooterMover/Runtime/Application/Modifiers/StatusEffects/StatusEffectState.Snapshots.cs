using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Modifiers.StatusEffects;

namespace ShooterMover.Application.Modifiers.StatusEffects
{
    public sealed partial class StatusEffectState
    {
        private void StoreReplay(
            LegacyStatusEffectCommand command,
            StatusEffectCommandResult result)
        {
            replay.Add(
                command.OperationId,
                new ReplayRecord(command.Fingerprint, result));
        }

        private StatusEffectStateSnapshot BuildStateSnapshot()
        {
            var effects = new List<ActiveStatusEffectSnapshot>();
            var modifiers = new List<LiveModifierDefinition>();

            foreach (KeyValuePair<
                string,
                List<ActiveStatusEffectStackSnapshot>> pair in
                stacksByEffect.OrderBy(
                    item => item.Key,
                    StringComparer.Ordinal))
            {
                StatusEffectDefinition definition =
                    catalog.RequireDefinition(pair.Key);
                List<ActiveStatusEffectStackSnapshot> stacks =
                    pair.Value
                        .OrderBy(
                            item => item.ExpiresAtTickExclusive)
                        .ThenBy(
                            item => item.StackId,
                            StringComparer.Ordinal)
                        .ToList();
                effects.Add(
                    new ActiveStatusEffectSnapshot(
                        definition.EffectId,
                        definition.Fingerprint,
                        definition.StackingPolicy,
                        definition.DispelCategoryId,
                        stacks));

                foreach (ActiveStatusEffectStackSnapshot stack in stacks)
                {
                    foreach (LiveModifierDefinition contribution in
                        definition.ModifierContributions)
                    {
                        modifiers.Add(
                            new LiveModifierDefinition(
                                BuildModifierSourceId(
                                    stack,
                                    contribution),
                                contribution.TargetId,
                                contribution.Operation,
                                contribution.Value,
                                contribution.ConditionId));
                    }
                }
            }

            return new StatusEffectStateSnapshot(
                subjectId,
                lifecycleGeneration,
                latestAcceptedTick,
                catalog.Fingerprint,
                effects,
                new LiveModifierSnapshot(modifiers));
        }

        private static string BuildModifierSourceId(
            ActiveStatusEffectStackSnapshot stack,
            LiveModifierDefinition contribution)
        {
            return "status-effect:"
                + stack.EffectId
                + ":"
                + stack.StackId
                + ":"
                + stack.SourceId
                + ":"
                + contribution.SourceId;
        }

        private static void ValidateSnapshot(
            StatusEffectCatalog catalog,
            StatusEffectLedgerSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ValidateState(catalog, snapshot.State, nameof(snapshot));
            foreach (StatusEffectReplayRecordSnapshot record in
                snapshot.ReplayHistory)
            {
                if (!string.Equals(
                    record.Result.State.SubjectId,
                    snapshot.State.SubjectId,
                    StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Status-effect replay history belongs to a different subject.",
                        nameof(snapshot));
                }

                ValidateState(
                    catalog,
                    record.Result.State,
                    nameof(snapshot));
            }
        }

        private static void ValidateState(
            StatusEffectCatalog catalog,
            StatusEffectStateSnapshot state,
            string parameterName)
        {
            if (!string.Equals(
                state.CatalogFingerprint,
                catalog.Fingerprint,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Status-effect checkpoint catalog fingerprint mismatch.",
                    parameterName);
            }

            var stackIds = new HashSet<string>(StringComparer.Ordinal);
            var projected = new List<LiveModifierDefinition>();
            foreach (ActiveStatusEffectSnapshot effect in
                state.ActiveEffects)
            {
                StatusEffectDefinition definition;
                if (!catalog.TryGetDefinition(
                    effect.EffectId,
                    out definition))
                {
                    throw new ArgumentException(
                        "Status-effect checkpoint references an unknown definition.",
                        parameterName);
                }
                if (!string.Equals(
                    effect.DefinitionFingerprint,
                    definition.Fingerprint,
                    StringComparison.Ordinal)
                    || effect.StackingPolicy != definition.StackingPolicy
                    || !string.Equals(
                        effect.DispelCategoryId,
                        definition.DispelCategoryId,
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Status-effect checkpoint definition facts do not match the catalog.",
                        parameterName);
                }
                if (effect.Stacks.Count > definition.MaximumStacks)
                {
                    throw new ArgumentException(
                        "Status-effect checkpoint exceeds the authored maximum stack count.",
                        parameterName);
                }
                if (definition.StackingPolicy
                        != StatusEffectStackingPolicy.Add
                    && effect.Stacks.Count != 1)
                {
                    throw new ArgumentException(
                        "A shared-stack status-effect checkpoint must contain exactly one stack.",
                        parameterName);
                }

                foreach (ActiveStatusEffectStackSnapshot stack in
                    effect.Stacks)
                {
                    if (!stackIds.Add(stack.StackId))
                    {
                        throw new ArgumentException(
                            "Status-effect checkpoint stack identities must be globally unique.",
                            parameterName);
                    }
                    if (stack.ExpiresAtTickExclusive
                        <= state.LatestAcceptedTick)
                    {
                        throw new ArgumentException(
                            "Status-effect checkpoint contains an already expired stack.",
                            parameterName);
                    }

                    foreach (LiveModifierDefinition contribution in
                        definition.ModifierContributions)
                    {
                        projected.Add(
                            new LiveModifierDefinition(
                                BuildModifierSourceId(
                                    stack,
                                    contribution),
                                contribution.TargetId,
                                contribution.Operation,
                                contribution.Value,
                                contribution.ConditionId));
                    }
                }
            }

            var expectedProjection =
                new LiveModifierSnapshot(projected);
            if (!string.Equals(
                expectedProjection.Fingerprint,
                state.ModifierProjection.Fingerprint,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Status-effect checkpoint modifier projection mismatch.",
                    parameterName);
            }
        }
    }
}
