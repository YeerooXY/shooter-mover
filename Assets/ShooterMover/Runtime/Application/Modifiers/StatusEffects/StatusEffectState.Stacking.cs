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
        private bool TryValidateCommon(
            LegacyStatusEffectCommand command,
            out StatusEffectCommandResult rejected)
        {
            if (command == null)
            {
                rejected = null;
                return false;
            }
            if (!string.Equals(
                command.SubjectId,
                subjectId,
                StringComparison.Ordinal))
            {
                rejected = Reject(
                    command,
                    StatusEffectCommandStatus.Rejected,
                    "status-effect-subject-mismatch");
                return false;
            }
            if (command.LifecycleGeneration != lifecycleGeneration)
            {
                rejected = Reject(
                    command,
                    StatusEffectCommandStatus.LifecycleMismatch,
                    "status-effect-lifecycle-mismatch");
                return false;
            }
            if (command.SimulationTick < latestAcceptedTick)
            {
                rejected = Reject(
                    command,
                    StatusEffectCommandStatus.StaleSimulationTick,
                    "status-effect-simulation-tick-stale");
                return false;
            }

            rejected = null;
            return true;
        }

        private void ApplyAdd(
            ApplyStatusEffectCommand command,
            StatusEffectDefinition definition,
            List<ActiveStatusEffectStackSnapshot> stacks,
            out StatusEffectCommandAction action,
            out int affected)
        {
            if (stacks.Count >= definition.MaximumStacks)
            {
                action = StatusEffectCommandAction.Ignored;
                affected = 0;
                return;
            }

            bool hadExisting = stacks.Count > 0;
            stacks.Add(CreateStack(command, definition));
            action = hadExisting
                ? StatusEffectCommandAction.Stacked
                : StatusEffectCommandAction.Applied;
            affected = 1;
        }

        private void ApplyRefresh(
            ApplyStatusEffectCommand command,
            StatusEffectDefinition definition,
            List<ActiveStatusEffectStackSnapshot> stacks,
            out StatusEffectCommandAction action,
            out int affected)
        {
            if (stacks.Count == 0)
            {
                stacks.Add(CreateStack(command, definition));
                action = StatusEffectCommandAction.Applied;
                affected = 1;
                return;
            }

            string existingStackId = stacks[0].StackId;
            stacks[0] = CreateStack(
                command,
                definition,
                existingStackId);
            action = StatusEffectCommandAction.Refreshed;
            affected = 1;
        }

        private void ApplyReplace(
            ApplyStatusEffectCommand command,
            StatusEffectDefinition definition,
            List<ActiveStatusEffectStackSnapshot> stacks,
            out StatusEffectCommandAction action,
            out int affected)
        {
            bool hadExisting = stacks.Count > 0;
            int removed = stacks.Count;
            stacks.Clear();
            stacks.Add(CreateStack(command, definition));
            action = hadExisting
                ? StatusEffectCommandAction.Replaced
                : StatusEffectCommandAction.Applied;
            affected = removed + 1;
        }

        private void ApplyIgnore(
            ApplyStatusEffectCommand command,
            StatusEffectDefinition definition,
            List<ActiveStatusEffectStackSnapshot> stacks,
            out StatusEffectCommandAction action,
            out int affected)
        {
            if (stacks.Count > 0)
            {
                action = StatusEffectCommandAction.Ignored;
                affected = 0;
                return;
            }

            stacks.Add(CreateStack(command, definition));
            action = StatusEffectCommandAction.Applied;
            affected = 1;
        }

        private ActiveStatusEffectStackSnapshot CreateStack(
            ApplyStatusEffectCommand command,
            StatusEffectDefinition definition,
            string existingStackId = null)
        {
            string stackId = string.IsNullOrWhiteSpace(existingStackId)
                ? "status-stack."
                    + StatusEffectLocalHash.Hash(
                        command.OperationId
                        + "|"
                        + definition.EffectId
                        + "|"
                        + command.SourceId)
                        .Substring(0, 24)
                : existingStackId;
            long expiresAtExclusive = checked(
                command.SimulationTick + definition.DurationTicks);
            return new ActiveStatusEffectStackSnapshot(
                stackId,
                definition.EffectId,
                command.SourceId,
                command.SimulationTick,
                expiresAtExclusive);
        }

        private int ExpireAt(long simulationTick)
        {
            int removed = 0;
            List<string> effectIds = stacksByEffect.Keys
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToList();
            foreach (string effectId in effectIds)
            {
                List<ActiveStatusEffectStackSnapshot> stacks =
                    stacksByEffect[effectId];
                removed += stacks.RemoveAll(
                    item => item.ExpiresAtTickExclusive <= simulationTick);
                if (stacks.Count == 0)
                {
                    stacksByEffect.Remove(effectId);
                }
            }

            return removed;
        }

        private StatusEffectCommandResult Accepted(
            LegacyStatusEffectCommand command,
            StatusEffectCommandStatus status,
            StatusEffectCommandAction action,
            int affectedStacks,
            int expiredStacks)
        {
            return new StatusEffectCommandResult(
                command.OperationId,
                command.Fingerprint,
                status,
                action,
                string.Empty,
                affectedStacks,
                expiredStacks,
                BuildStateSnapshot());
        }

        private StatusEffectCommandResult Reject(
            LegacyStatusEffectCommand command,
            StatusEffectCommandStatus status,
            string rejectionCode)
        {
            return new StatusEffectCommandResult(
                command.OperationId,
                command.Fingerprint,
                status,
                StatusEffectCommandAction.Rejected,
                rejectionCode,
                0,
                0,
                BuildStateSnapshot());
        }

    }
}
