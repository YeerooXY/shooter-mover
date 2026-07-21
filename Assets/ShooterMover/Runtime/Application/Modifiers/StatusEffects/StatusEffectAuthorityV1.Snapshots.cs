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
    public sealed partial class StatusEffectAuthorityV1
    {
        private void StoreReplay(
            StatusEffectCommandV1 command,
            StatusEffectCommandResultV1 result)
        {
            replay.Add(
                command.OperationId,
                new ReplayRecord(command.Fingerprint, result));
        }

        private StatusEffectStateSnapshotV1 BuildStateSnapshot()
        {
            var effects = new List<ActiveStatusEffectSnapshotV1>();
            var modifiers = new List<RuntimeModifierDefinitionV1>();

            foreach (KeyValuePair<
                string,
                List<ActiveStatusEffectStackSnapshotV1>> pair in
                stacksByEffect.OrderBy(
                    item => item.Key,
                    StringComparer.Ordinal))
            {
                StatusEffectDefinitionV1 definition =
                    catalog.RequireDefinition(pair.Key);
                List<ActiveStatusEffectStackSnapshotV1> stacks =
                    pair.Value
                        .OrderBy(
                            item => item.ExpiresAtTickExclusive)
                        .ThenBy(
                            item => item.StackId,
                            StringComparer.Ordinal)
                        .ToList();
                effects.Add(
                    new ActiveStatusEffectSnapshotV1(
                        definition.EffectId,
                        definition.Fingerprint,
                        definition.StackingPolicy,
                        definition.DispelCategoryId,
                        stacks));

                foreach (ActiveStatusEffectStackSnapshotV1 stack in stacks)
                {
                    foreach (RuntimeModifierDefinitionV1 contribution in
                        definition.ModifierContributions)
                    {
                        modifiers.Add(
                            new RuntimeModifierDefinitionV1(
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

            return new StatusEffectStateSnapshotV1(
                subjectId,
                lifecycleGeneration,
                latestAcceptedTick,
                catalog.Fingerprint,
                effects,
                new RuntimeModifierSnapshotV1(modifiers));
        }

        private static string BuildModifierSourceId(
            ActiveStatusEffectStackSnapshotV1 stack,
            RuntimeModifierDefinitionV1 contribution)
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
            StatusEffectCatalogV1 catalog,
            StatusEffectAuthoritySnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ValidateState(catalog, snapshot.State, nameof(snapshot));
            foreach (StatusEffectReplayRecordSnapshotV1 record in
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
            StatusEffectCatalogV1 catalog,
            StatusEffectStateSnapshotV1 state,
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
            var projected = new List<RuntimeModifierDefinitionV1>();
            foreach (ActiveStatusEffectSnapshotV1 effect in
                state.ActiveEffects)
            {
                StatusEffectDefinitionV1 definition;
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
                        != StatusEffectStackingPolicyV1.Add
                    && effect.Stacks.Count != 1)
                {
                    throw new ArgumentException(
                        "A shared-stack status-effect checkpoint must contain exactly one stack.",
                        parameterName);
                }

                foreach (ActiveStatusEffectStackSnapshotV1 stack in
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

                    foreach (RuntimeModifierDefinitionV1 contribution in
                        definition.ModifierContributions)
                    {
                        projected.Add(
                            new RuntimeModifierDefinitionV1(
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
                new RuntimeModifierSnapshotV1(projected);
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
