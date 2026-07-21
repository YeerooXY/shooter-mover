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
        private sealed class ReplayRecord
        {
            public ReplayRecord(
                string commandFingerprint,
                StatusEffectCommandResultV1 result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }

            public StatusEffectCommandResultV1 Result { get; }
        }

        private readonly StatusEffectCatalogV1 catalog;
        private readonly string subjectId;
        private readonly Dictionary<
            string,
            List<ActiveStatusEffectStackSnapshotV1>> stacksByEffect =
                new Dictionary<
                    string,
                    List<ActiveStatusEffectStackSnapshotV1>>(
                        StringComparer.Ordinal);
        private readonly Dictionary<string, ReplayRecord> replay =
            new Dictionary<string, ReplayRecord>(StringComparer.Ordinal);
        private int lifecycleGeneration;
        private long latestAcceptedTick;

        public StatusEffectAuthorityV1(
            string subjectId,
            int lifecycleGeneration,
            StatusEffectCatalogV1 catalog)
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

        private StatusEffectAuthorityV1(
            StatusEffectCatalogV1 catalog,
            StatusEffectAuthoritySnapshotV1 snapshot)
        {
            this.catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
            ValidateSnapshot(catalog, snapshot);

            subjectId = snapshot.State.SubjectId;
            lifecycleGeneration = snapshot.State.LifecycleGeneration;
            latestAcceptedTick = snapshot.State.LatestAcceptedTick;

            foreach (ActiveStatusEffectSnapshotV1 effect in
                snapshot.State.ActiveEffects)
            {
                stacksByEffect.Add(
                    effect.EffectId,
                    effect.Stacks.ToList());
            }

            foreach (StatusEffectReplayRecordSnapshotV1 record in
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

        public StatusEffectStateSnapshotV1 Snapshot
        {
            get { return BuildStateSnapshot(); }
        }

        public StatusEffectCommandResultV1 Apply(
            ApplyStatusEffectCommandV1 command)
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

            StatusEffectDefinitionV1 definition;
            if (!catalog.TryGetDefinition(
                command.EffectId,
                out definition))
            {
                rejected = Reject(
                    command,
                    StatusEffectCommandStatusV1.Rejected,
                    "status-effect-definition-unknown");
                StoreReplay(command, rejected);
                return rejected;
            }

            int expired = ExpireAt(command.SimulationTick);
            List<ActiveStatusEffectStackSnapshotV1> stacks;
            if (!stacksByEffect.TryGetValue(
                definition.EffectId,
                out stacks))
            {
                stacks = new List<ActiveStatusEffectStackSnapshotV1>();
                stacksByEffect.Add(definition.EffectId, stacks);
            }

            StatusEffectCommandActionV1 action;
            int affected;
            switch (definition.StackingPolicy)
            {
                case StatusEffectStackingPolicyV1.Add:
                    ApplyAdd(
                        command,
                        definition,
                        stacks,
                        out action,
                        out affected);
                    break;
                case StatusEffectStackingPolicyV1.Refresh:
                    ApplyRefresh(
                        command,
                        definition,
                        stacks,
                        out action,
                        out affected);
                    break;
                case StatusEffectStackingPolicyV1.Replace:
                    ApplyReplace(
                        command,
                        definition,
                        stacks,
                        out action,
                        out affected);
                    break;
                case StatusEffectStackingPolicyV1.Ignore:
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
                    ? StatusEffectCommandStatusV1.Accepted
                    : StatusEffectCommandStatusV1.AcceptedNoChange,
                action,
                affected,
                expired);
            StoreReplay(command, result);
            return result;
        }

    }
}
