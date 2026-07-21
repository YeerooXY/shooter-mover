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
        public StatusEffectCommandResultV1 Advance(
            AdvanceStatusEffectTickCommandV1 command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            StatusEffectCommandResultV1 replayResult;
            if (TryResolveReplay(command, out replayResult))
            {
                return replayResult;
            }

            StatusEffectCommandResultV1 rejected;
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
                    ? StatusEffectCommandStatusV1.Accepted
                    : StatusEffectCommandStatusV1.AcceptedNoChange,
                expired > 0
                    ? StatusEffectCommandActionV1.Expired
                    : StatusEffectCommandActionV1.Advanced,
                expired,
                expired);
            StoreReplay(command, result);
            return result;
        }

        public StatusEffectCommandResultV1 Dispel(
            DispelStatusEffectsCommandV1 command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            StatusEffectCommandResultV1 replayResult;
            if (TryResolveReplay(command, out replayResult))
            {
                return replayResult;
            }

            StatusEffectCommandResultV1 rejected;
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
                StatusEffectDefinitionV1 definition =
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
                    ? StatusEffectCommandStatusV1.Accepted
                    : StatusEffectCommandStatusV1.AcceptedNoChange,
                removed > 0
                    ? StatusEffectCommandActionV1.Dispelled
                    : (expired > 0
                        ? StatusEffectCommandActionV1.Expired
                        : StatusEffectCommandActionV1.NoChange),
                removed,
                expired);
            StoreReplay(command, result);
            return result;
        }

        public StatusEffectCommandResultV1 Restart(
            RestartStatusEffectLifecycleCommandV1 command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            StatusEffectCommandResultV1 replayResult;
            if (TryResolveReplay(command, out replayResult))
            {
                return replayResult;
            }

            StatusEffectCommandResultV1 rejected;
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
                StatusEffectCommandStatusV1.Accepted,
                StatusEffectCommandActionV1.Restarted,
                removed,
                0);
            StoreReplay(command, result);
            return result;
        }

        public StatusEffectAuthoritySnapshotV1 ExportSnapshot()
        {
            var records =
                new List<StatusEffectReplayRecordSnapshotV1>();
            foreach (KeyValuePair<string, ReplayRecord> pair in replay
                .OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                records.Add(
                    new StatusEffectReplayRecordSnapshotV1(
                        pair.Key,
                        pair.Value.CommandFingerprint,
                        pair.Value.Result));
            }

            return new StatusEffectAuthoritySnapshotV1(
                BuildStateSnapshot(),
                records);
        }

        public static StatusEffectAuthorityV1 Restore(
            StatusEffectCatalogV1 catalog,
            StatusEffectAuthoritySnapshotV1 snapshot)
        {
            return new StatusEffectAuthorityV1(catalog, snapshot);
        }

        private bool TryResolveReplay(
            StatusEffectCommandV1 command,
            out StatusEffectCommandResultV1 result)
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

            result = new StatusEffectCommandResultV1(
                command.OperationId,
                command.Fingerprint,
                StatusEffectCommandStatusV1.ConflictingDuplicate,
                StatusEffectCommandActionV1.Rejected,
                "status-effect-operation-conflicting-duplicate",
                0,
                0,
                BuildStateSnapshot());
            return true;
        }

    }
}
