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
        private sealed class ReplayRecord
        {
            public ReplayRecord(
                string commandFingerprint,
                StatusEffectCommandResult result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }

            public StatusEffectCommandResult Result { get; }
        }

        private readonly StatusEffectCatalog catalog;
        private readonly string subjectId;
        private readonly Dictionary<
            string,
            List<ActiveStatusEffectStackSnapshot>> stacksByEffect =
                new Dictionary<
                    string,
                    List<ActiveStatusEffectStackSnapshot>>(
                        StringComparer.Ordinal);
        private readonly Dictionary<string, ReplayRecord> replay =
            new Dictionary<string, ReplayRecord>(StringComparer.Ordinal);
        private int lifecycleGeneration;
        private long latestAcceptedTick;

        public StatusEffectState(
            string subjectId,
            int lifecycleGeneration,
            StatusEffectCatalog catalog)
        {
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

            this.catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
            this.subjectId = subjectId.Trim();
            this.lifecycleGeneration = lifecycleGeneration;
        }

        private StatusEffectState(
            StatusEffectCatalog catalog,
            StatusEffectLedgerSnapshot snapshot)
        {
            this.catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
            ValidateSnapshot(catalog, snapshot);

            subjectId = snapshot.State.SubjectId;
            lifecycleGeneration = snapshot.State.LifecycleGeneration;
            latestAcceptedTick = snapshot.State.LatestAcceptedTick;

            foreach (ActiveStatusEffectSnapshot effect in
                snapshot.State.ActiveEffects)
            {
                stacksByEffect.Add(
                    effect.EffectId,
                    effect.Stacks.ToList());
            }

            foreach (StatusEffectReplayRecordSnapshot record in
                snapshot.ReplayHistory)
            {
                replay.Add(
                    record.OperationId,
                    new ReplayRecord(
                        record.CommandFingerprint,
                        record.Result));
            }
        }

        public string SubjectId
        {
            get { return subjectId; }
        }

        public int LifecycleGeneration
        {
            get { return lifecycleGeneration; }
        }

        public long LatestAcceptedTick
        {
            get { return latestAcceptedTick; }
        }

        public StatusEffectStateSnapshot Snapshot
        {
            get { return BuildStateSnapshot(); }
        }

        public StatusEffectCommandResult Apply(
            ApplyStatusEffectCommand command)
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

            StatusEffectDefinition definition;
            if (!catalog.TryGetDefinition(
                command.EffectId,
                out definition))
            {
                rejected = Reject(
                    command,
                    StatusEffectCommandStatus.Rejected,
                    "status-effect-definition-unknown");
                StoreReplay(command, rejected);
                return rejected;
            }

            int expired = ExpireAt(command.SimulationTick);
            List<ActiveStatusEffectStackSnapshot> stacks;
            if (!stacksByEffect.TryGetValue(
                definition.EffectId,
                out stacks))
            {
                stacks = new List<ActiveStatusEffectStackSnapshot>();
                stacksByEffect.Add(definition.EffectId, stacks);
            }

            StatusEffectCommandAction action;
            int affected;
            switch (definition.StackingPolicy)
            {
                case StatusEffectStackingPolicy.Add:
                    ApplyAdd(
                        command,
                        definition,
                        stacks,
                        out action,
                        out affected);
                    break;
                case StatusEffectStackingPolicy.Refresh:
                    ApplyRefresh(
                        command,
                        definition,
                        stacks,
                        out action,
                        out affected);
                    break;
                case StatusEffectStackingPolicy.Replace:
                    ApplyReplace(
                        command,
                        definition,
                        stacks,
                        out action,
                        out affected);
                    break;
                case StatusEffectStackingPolicy.Ignore:
                    ApplyIgnore(
                        command,
                        definition,
                        stacks,
                        out action,
                        out affected);
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unsupported status-effect stacking policy.");
            }

            if (stacks.Count == 0)
            {
                stacksByEffect.Remove(definition.EffectId);
            }

            latestAcceptedTick = command.SimulationTick;
            bool changed = expired > 0 || affected > 0;
            var result = Accepted(
                command,
                changed
                    ? StatusEffectCommandStatus.Accepted
                    : StatusEffectCommandStatus.AcceptedNoChange,
                action,
                affected,
                expired);
            StoreReplay(command, result);
            return result;
        }

    }
}
