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
        private bool TryValidateCommon(
            StatusEffectCommandV1 command,
            out StatusEffectCommandResultV1 rejected)
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
                    StatusEffectCommandStatusV1.Rejected,
                    "status-effect-subject-mismatch");
                return false;
            }
            if (command.LifecycleGeneration != lifecycleGeneration)
            {
                rejected = Reject(
                    command,
                    StatusEffectCommandStatusV1.LifecycleMismatch,
                    "status-effect-lifecycle-mismatch");
                return false;
            }
            if (command.SimulationTick < latestAcceptedTick)
            {
                rejected = Reject(
                    command,
                    StatusEffectCommandStatusV1.StaleSimulationTick,
                    "status-effect-simulation-tick-stale");
                return false;
            }

            rejected = null;
            return true;
        }

        private void ApplyAdd(
            ApplyStatusEffectCommandV1 command,
            StatusEffectDefinitionV1 definition,
            List<ActiveStatusEffectStackSnapshotV1> stacks,
            out StatusEffectCommandActionV1 action,
            out int affected)
        {
            if (stacks.Count >= definition.MaximumStacks)
            {
                action = StatusEffectCommandActionV1.Ignored;
                affected = 0;
                return;
            }

            bool hadExisting = stacks.Count > 0;
            stacks.Add(CreateStack(command, definition));
            action = hadExisting
                ? StatusEffectCommandActionV1.Stacked
                : StatusEffectCommandActionV1.Applied;
            affected = 1;
        }

        private void ApplyRefresh(
            ApplyStatusEffectCommandV1 command,
            StatusEffectDefinitionV1 definition,
            List<ActiveStatusEffectStackSnapshotV1> stacks,
            out StatusEffectCommandActionV1 action,
            out int affected)
        {
            if (stacks.Count == 0)
            {
                stacks.Add(CreateStack(command, definition));
                action = StatusEffectCommandActionV1.Applied;
                affected = 1;
                return;
            }

            string existingStackId = stacks[0].StackId;
            stacks[0] = CreateStack(
                command,
                definition,
                existingStackId);
            action = StatusEffectCommandActionV1.Refreshed;
            affected = 1;
        }

        private void ApplyReplace(
            ApplyStatusEffectCommandV1 command,
            StatusEffectDefinitionV1 definition,
            List<ActiveStatusEffectStackSnapshotV1> stacks,
            out StatusEffectCommandActionV1 action,
            out int affected)
        {
            bool hadExisting = stacks.Count > 0;
            int removed = stacks.Count;
            stacks.Clear();
            stacks.Add(CreateStack(command, definition));
            action = hadExisting
                ? StatusEffectCommandActionV1.Replaced
                : StatusEffectCommandActionV1.Applied;
            affected = removed + 1;
        }

        private void ApplyIgnore(
            ApplyStatusEffectCommandV1 command,
            StatusEffectDefinitionV1 definition,
            List<ActiveStatusEffectStackSnapshotV1> stacks,
            out StatusEffectCommandActionV1 action,
            out int affected)
        {
            if (stacks.Count > 0)
            {
                action = StatusEffectCommandActionV1.Ignored;
                affected = 0;
                return;
            }

            stacks.Add(CreateStack(command, definition));
            action = StatusEffectCommandActionV1.Applied;
            affected = 1;
        }

        private ActiveStatusEffectStackSnapshotV1 CreateStack(
            ApplyStatusEffectCommandV1 command,
            StatusEffectDefinitionV1 definition,
            string existingStackId = null)
        {
            string stackId = string.IsNullOrWhiteSpace(existingStackId)
                ? "status-stack."
                    + StatusEffectLocalHashV1.Hash(
                        command.OperationId
                        + "|"
                        + definition.EffectId
                        + "|"
                        + command.SourceId)
                        .Substring(0, 24)
                : existingStackId;
            long expiresAtExclusive = checked(
                command.SimulationTick + definition.DurationTicks);
            return new ActiveStatusEffectStackSnapshotV1(
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
                List<ActiveStatusEffectStackSnapshotV1> stacks =
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

        private StatusEffectCommandResultV1 Accepted(
            StatusEffectCommandV1 command,
            StatusEffectCommandStatusV1 status,
            StatusEffectCommandActionV1 action,
            int affectedStacks,
            int expiredStacks)
        {
            return new StatusEffectCommandResultV1(
                command.OperationId,
                command.Fingerprint,
                status,
                action,
                string.Empty,
                affectedStacks,
                expiredStacks,
                BuildStateSnapshot());
        }

        private StatusEffectCommandResultV1 Reject(
            StatusEffectCommandV1 command,
            StatusEffectCommandStatusV1 status,
            string rejectionCode)
        {
            return new StatusEffectCommandResultV1(
                command.OperationId,
                command.Fingerprint,
                status,
                StatusEffectCommandActionV1.Rejected,
                rejectionCode,
                0,
                0,
                BuildStateSnapshot());
        }

    }
}
