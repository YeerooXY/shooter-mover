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
        public StatusEffectCommandResult Advance(
            AdvanceStatusEffectTickCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            StatusEffectCommandResult replayResult;
            if (TryResolveReplay(command, out replayResult))
            {
                return replayResult;
            }

            StatusEffectCommandResult rejected;
            if (!TryValidateCommon(command, out rejected))
            {
                StoreReplay(command, rejected);
                return rejected;
            }

            int expired = ExpireAt(command.SimulationTick);
            latestAcceptedTick = command.SimulationTick;
            var result = Accepted(
                command,
                expired > 0
                    ? StatusEffectCommandStatus.Accepted
                    : StatusEffectCommandStatus.AcceptedNoChange,
                expired > 0
                    ? StatusEffectCommandAction.Expired
                    : StatusEffectCommandAction.Advanced,
                expired,
                expired);
            StoreReplay(command, result);
            return result;
        }

        public StatusEffectCommandResult Dispel(
            DispelStatusEffectsCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            StatusEffectCommandResult replayResult;
            if (TryResolveReplay(command, out replayResult))
            {
                return replayResult;
            }

            StatusEffectCommandResult rejected;
            if (!TryValidateCommon(command, out rejected))
            {
                StoreReplay(command, rejected);
                return rejected;
            }

            int expired = ExpireAt(command.SimulationTick);
            int removed = 0;
            List<string> effectIds = stacksByEffect.Keys
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToList();
            foreach (string effectId in effectIds)
            {
                StatusEffectDefinition definition =
                    catalog.RequireDefinition(effectId);
                if (!string.Equals(
                    definition.DispelCategoryId,
                    command.DispelCategoryId,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                removed += stacksByEffect[effectId].Count;
                stacksByEffect.Remove(effectId);
            }

            latestAcceptedTick = command.SimulationTick;
            bool changed = expired > 0 || removed > 0;
            var result = Accepted(
                command,
                changed
                    ? StatusEffectCommandStatus.Accepted
                    : StatusEffectCommandStatus.AcceptedNoChange,
                removed > 0
                    ? StatusEffectCommandAction.Dispelled
                    : (expired > 0
                        ? StatusEffectCommandAction.Expired
                        : StatusEffectCommandAction.NoChange),
                removed,
                expired);
            StoreReplay(command, result);
            return result;
        }

        public StatusEffectCommandResult Restart(
            RestartStatusEffectLifecycleCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            StatusEffectCommandResult replayResult;
            if (TryResolveReplay(command, out replayResult))
            {
                return replayResult;
            }

            StatusEffectCommandResult rejected;
            if (!TryValidateCommon(command, out rejected))
            {
                StoreReplay(command, rejected);
                return rejected;
            }

            int removed = stacksByEffect.Values.Sum(item => item.Count);
            stacksByEffect.Clear();
            lifecycleGeneration = command.NextLifecycleGeneration;
            latestAcceptedTick = command.SimulationTick;
            var result = Accepted(
                command,
                StatusEffectCommandStatus.Accepted,
                StatusEffectCommandAction.Restarted,
                removed,
                0);
            StoreReplay(command, result);
            return result;
        }

        public StatusEffectLedgerSnapshot ExportSnapshot()
        {
            var records =
                new List<StatusEffectReplayRecordSnapshot>();
            foreach (KeyValuePair<string, ReplayRecord> pair in replay
                .OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                records.Add(
                    new StatusEffectReplayRecordSnapshot(
                        pair.Key,
                        pair.Value.CommandFingerprint,
                        pair.Value.Result));
            }

            return new StatusEffectLedgerSnapshot(
                BuildStateSnapshot(),
                records);
        }

        public static StatusEffectState Restore(
            StatusEffectCatalog catalog,
            StatusEffectLedgerSnapshot snapshot)
        {
            return new StatusEffectState(catalog, snapshot);
        }

        private bool TryResolveReplay(
            LegacyStatusEffectCommand command,
            out StatusEffectCommandResult result)
        {
            result = null;
            if (command == null)
            {
                return false;
            }

            ReplayRecord prior;
            if (!replay.TryGetValue(command.OperationId, out prior))
            {
                return false;
            }

            if (string.Equals(
                prior.CommandFingerprint,
                command.Fingerprint,
                StringComparison.Ordinal))
            {
                result = prior.Result;
                return true;
            }

            result = new StatusEffectCommandResult(
                command.OperationId,
                command.Fingerprint,
                StatusEffectCommandStatus.ConflictingDuplicate,
                StatusEffectCommandAction.Rejected,
                "status-effect-operation-conflicting-duplicate",
                0,
                0,
                BuildStateSnapshot());
            return true;
        }

    }
}
