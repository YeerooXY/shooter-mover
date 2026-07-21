using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    public sealed class RunSessionAuthorityV1
    {
        private sealed class StartReplayRecord
        {
            public StartReplayRecord(
                string commandFingerprint,
                RunSessionStartResultV1 result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }
            public RunSessionStartResultV1 Result { get; }
        }

        private readonly IRunSessionStartSourceV1 startSource;
        private readonly Dictionary<StableId, StartReplayRecord> startReplay =
            new Dictionary<StableId, StartReplayRecord>();
        private readonly Dictionary<StableId, RunSessionAggregateV1> runs =
            new Dictionary<StableId, RunSessionAggregateV1>();

        public RunSessionAuthorityV1(IRunSessionStartSourceV1 source)
        {
            startSource = source
                ?? throw new ArgumentNullException(nameof(source));
        }

        public int RunCount
        {
            get { return runs.Count; }
        }

        public RunSessionStartResultV1 Start(
            StartRunSessionCommandV1 command)
        {
            if (command == null)
            {
                return new RunSessionStartResultV1(
                    RunSessionStartStatusV1.Rejected,
                    null,
                    string.Empty,
                    null,
                    string.Empty,
                    "run-start-command-null");
            }

            StartReplayRecord existing;
            if (startReplay.TryGetValue(command.OperationStableId, out existing))
            {
                if (string.Equals(
                    existing.CommandFingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return existing.Result;
                }
                return new RunSessionStartResultV1(
                    RunSessionStartStatusV1.ConflictingDuplicate,
                    command.OperationStableId,
                    command.Fingerprint,
                    existing.Result.RunStableId,
                    existing.Result.RunSnapshotFingerprint,
                    "run-start-operation-conflict");
            }

            StableId runStableId = ResolveRunStableId(command);
            if (runs.ContainsKey(runStableId))
            {
                RunSessionStartResultV1 collision =
                    new RunSessionStartResultV1(
                        RunSessionStartStatusV1.Rejected,
                        command.OperationStableId,
                        command.Fingerprint,
                        runStableId,
                        string.Empty,
                        "run-identity-already-exists");
                startReplay.Add(
                    command.OperationStableId,
                    new StartReplayRecord(command.Fingerprint, collision));
                return collision;
            }

            RunSessionStartMaterialV1 material =
                startSource.Resolve(command, runStableId);
            if (material == null || !material.Succeeded)
            {
                RunSessionStartResultV1 rejected =
                    new RunSessionStartResultV1(
                        RunSessionStartStatusV1.Rejected,
                        command.OperationStableId,
                        command.Fingerprint,
                        runStableId,
                        string.Empty,
                        material == null
                            ? "run-start-source-returned-null"
                            : material.RejectionCode);
                startReplay.Add(
                    command.OperationStableId,
                    new StartReplayRecord(command.Fingerprint, rejected));
                return rejected;
            }

            var aggregate = new RunSessionAggregateV1(
                command,
                runStableId,
                material.FrozenInputs,
                material.RuntimePorts);
            runs.Add(runStableId, aggregate);
            RunSessionStartResultV1 result =
                new RunSessionStartResultV1(
                    RunSessionStartStatusV1.Started,
                    command.OperationStableId,
                    command.Fingerprint,
                    runStableId,
                    aggregate.ExportDebugSnapshot().Fingerprint,
                    string.Empty);
            startReplay.Add(
                command.OperationStableId,
                new StartReplayRecord(command.Fingerprint, result));
            return result;
        }

        public bool TryGetRun(
            StableId runStableId,
            out RunSessionAggregateV1 aggregate)
        {
            aggregate = null;
            return runStableId != null
                && runs.TryGetValue(runStableId, out aggregate);
        }

        private static StableId ResolveRunStableId(
            StartRunSessionCommandV1 command)
        {
            if (command.RequestedRunStableId != null)
            {
                return command.RequestedRunStableId;
            }
            string material = command.OperationStableId
                + "|"
                + command.RunInstanceIdentityMaterial
                + "|"
                + command.SelectedCharacterInstanceStableId
                + "|"
                + command.MissionLayoutStableId
                + "|"
                + command.DifficultyStableId
                + "|"
                + command.DeterministicSeed.ToString(
                    CultureInfo.InvariantCulture)
                + "|"
                + command.EventModifierContextFingerprint;
            string hash = RunSessionFingerprintV1.Hash(material);
            return StableId.Create("run-instance", hash.Substring(0, 40));
        }
    }

    public sealed class RunSessionAggregateV1
    {
        private sealed class RestartReplayRecord
        {
            public RestartReplayRecord(
                string commandFingerprint,
                RunSessionRestartResultV1 result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }
            public RunSessionRestartResultV1 Result { get; }
        }

        private sealed class EndReplayRecord
        {
            public EndReplayRecord(
                string commandFingerprint,
                RunSessionEndResultV1 result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }
            public RunSessionEndResultV1 Result { get; }
        }

        private sealed class FactReplayRecord
        {
            public FactReplayRecord(
                string factFingerprint,
                RunSessionFactAdmissionResultV1 result)
            {
                FactFingerprint = factFingerprint;
                Result = result;
            }

            public string FactFingerprint { get; }
            public RunSessionFactAdmissionResultV1 Result { get; }
        }

        private sealed class LocalReplayRecord
        {
            public LocalReplayRecord(
                string commandFingerprint,
                RunLocalMutationResultV1 result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }
            public RunLocalMutationResultV1 Result { get; }
        }

        private readonly Dictionary<StableId, RestartReplayRecord> restartReplay =
            new Dictionary<StableId, RestartReplayRecord>();
        private readonly Dictionary<StableId, EndReplayRecord> endReplay =
            new Dictionary<StableId, EndReplayRecord>();
        private readonly Dictionary<StableId, FactReplayRecord> factReplay =
            new Dictionary<StableId, FactReplayRecord>();
        private readonly Dictionary<StableId, LocalReplayRecord> localReplay =
            new Dictionary<StableId, LocalReplayRecord>();
        private readonly Dictionary<string, long> temporaryPickups =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> counters =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> missionStatistics =
            new Dictionary<string, long>(StringComparer.Ordinal);

        private long runCash;
        private long lifecycleGeneration;
        private long authoritativeTick;
        private RunSessionLifecycleStateV1 lifecycleState;
        private RunSessionEndReceiptV1 terminalReceipt;

        internal RunSessionAggregateV1(
            StartRunSessionCommandV1 startCommand,
            StableId runStableId,
            FrozenCharacterRunInputsV1 frozenInputs,
            RunSessionRuntimePortsV1 runtimePorts)
        {
            StartCommand = startCommand
                ?? throw new ArgumentNullException(nameof(startCommand));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            FrozenInputs = frozenInputs
                ?? throw new ArgumentNullException(nameof(frozenInputs));
            RuntimePorts = runtimePorts
                ?? throw new ArgumentNullException(nameof(runtimePorts));
            if (!string.Equals(
                FrozenInputs.CombatProfile.RunId,
                RunStableId.ToString(),
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The frozen combat profile must belong to the exact run identity.",
                    nameof(frozenInputs));
            }
            if (RuntimePorts.Player.LifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(runtimePorts));
            }
            lifecycleGeneration = RuntimePorts.Player.LifecycleGeneration;
            authoritativeTick = StartCommand.AuthoritativeInitialTick;
            lifecycleState = RunSessionLifecycleStateV1.Active;
        }

        public StartRunSessionCommandV1 StartCommand { get; }
        public StableId RunStableId { get; }
        public FrozenCharacterRunInputsV1 FrozenInputs { get; }
        public RunSessionRuntimePortsV1 RuntimePorts { get; }
        public long LifecycleGeneration
        {
            get { return lifecycleGeneration; }
        }
        public long AuthoritativeTick
        {
            get { return authoritativeTick; }
        }
        public RunSessionLifecycleStateV1 LifecycleState
        {
            get { return lifecycleState; }
        }
        public RunSessionEndReceiptV1 TerminalReceipt
        {
            get { return terminalReceipt; }
        }

        public RunSessionFactAdmissionResultV1 AdmitFact(
            RunSessionFactEnvelopeV1 fact)
        {
            if (fact == null)
            {
                return new RunSessionFactAdmissionResultV1(
                    RunSessionFactAdmissionStatusV1.ConflictingDuplicate,
                    null,
                    "run-fact-null");
            }
            if (fact.RunStableId != RunStableId)
            {
                return new RunSessionFactAdmissionResultV1(
                    RunSessionFactAdmissionStatusV1.WrongRun,
                    fact,
                    "run-fact-wrong-run");
            }
            if (fact.LifecycleGeneration != lifecycleGeneration)
            {
                return new RunSessionFactAdmissionResultV1(
                    RunSessionFactAdmissionStatusV1.StaleLifecycle,
                    fact,
                    fact.LifecycleGeneration < lifecycleGeneration
                        ? "run-fact-stale-generation"
                        : "run-fact-future-generation");
            }
            if (lifecycleState == RunSessionLifecycleStateV1.Ended)
            {
                return new RunSessionFactAdmissionResultV1(
                    RunSessionFactAdmissionStatusV1.RunEnded,
                    fact,
                    "run-fact-after-end");
            }

            FactReplayRecord existing;
            if (factReplay.TryGetValue(fact.OperationStableId, out existing))
            {
                return string.Equals(
                    existing.FactFingerprint,
                    fact.Fingerprint,
                    StringComparison.Ordinal)
                    ? new RunSessionFactAdmissionResultV1(
                        RunSessionFactAdmissionStatusV1.ExactReplay,
                        fact,
                        string.Empty)
                    : new RunSessionFactAdmissionResultV1(
                        RunSessionFactAdmissionStatusV1.ConflictingDuplicate,
                        fact,
                        "run-fact-operation-conflict");
            }

            var accepted = new RunSessionFactAdmissionResultV1(
                RunSessionFactAdmissionStatusV1.Accepted,
                fact,
                string.Empty);
            factReplay.Add(
                fact.OperationStableId,
                new FactReplayRecord(fact.Fingerprint, accepted));
            return accepted;
        }

        public RunLocalMutationResultV1 ApplyLocalMutation(
            RunLocalMutationCommandV1 command)
        {
            RunLocalStateSnapshotV1 before = ExportLocalState();
            if (command == null)
            {
                return new RunLocalMutationResultV1(
                    false,
                    false,
                    false,
                    null,
                    before,
                    "run-local-command-null");
            }
            if (command.RunStableId != RunStableId)
            {
                return RejectLocal(command, before, "run-local-wrong-run");
            }
            if (command.LifecycleGeneration != lifecycleGeneration)
            {
                return RejectLocal(
                    command,
                    before,
                    command.LifecycleGeneration < lifecycleGeneration
                        ? "run-local-stale-generation"
                        : "run-local-future-generation");
            }
            if (lifecycleState == RunSessionLifecycleStateV1.Ended)
            {
                return RejectLocal(command, before, "run-local-after-end");
            }

            LocalReplayRecord existing;
            if (localReplay.TryGetValue(command.OperationStableId, out existing))
            {
                if (string.Equals(
                    existing.CommandFingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return new RunLocalMutationResultV1(
                        true,
                        true,
                        false,
                        command,
                        existing.Result.State,
                        string.Empty);
                }
                return new RunLocalMutationResultV1(
                    false,
                    false,
                    true,
                    command,
                    before,
                    "run-local-operation-conflict");
            }

            switch (command.Kind)
            {
                case RunLocalMutationKindV1.AddTemporaryPickup:
                    Add(temporaryPickups, command.Key, command.Amount);
                    break;
                case RunLocalMutationKindV1.AddRunCash:
                    runCash = checked(runCash + command.Amount);
                    break;
                case RunLocalMutationKindV1.IncrementCounter:
                    Add(counters, command.Key, command.Amount);
                    break;
                case RunLocalMutationKindV1.IncrementStatistic:
                    Add(missionStatistics, command.Key, command.Amount);
                    break;
                default:
                    return RejectLocal(command, before, "run-local-kind-invalid");
            }

            RunLocalStateSnapshotV1 after = ExportLocalState();
            var accepted = new RunLocalMutationResultV1(
                true,
                false,
                false,
                command,
                after,
                string.Empty);
            localReplay.Add(
                command.OperationStableId,
                new LocalReplayRecord(command.Fingerprint, accepted));
            return accepted;
        }

        public MissionRunAuthorityResultV1 RecordCollectedStrongbox(
            RunStrongboxCollectionRequestV1 request)
        {
            if (request == null
                || request.RunStableId != RunStableId
                || request.LifecycleGeneration != lifecycleGeneration
                || lifecycleState == RunSessionLifecycleStateV1.Ended)
            {
                return new MissionRunAuthorityResultV1(
                    MissionRunAuthorityStatusV1.InvalidRequest,
                    RuntimePorts.MissionResults.Sequence,
                    RuntimePorts.MissionResults.Sequence,
                    request == null ? null : request.OperationStableId,
                    request == null ? string.Empty : request.Fingerprint,
                    null,
                    null,
                    null,
                    request == null
                        ? "run-strongbox-command-null"
                        : (request.RunStableId != RunStableId
                            ? "run-strongbox-wrong-run"
                            : (request.LifecycleGeneration != lifecycleGeneration
                                ? "run-strongbox-lifecycle-mismatch"
                                : "run-strongbox-after-end")));
            }
            return RuntimePorts.MissionResults.RecordCollectedStrongbox(
                request,
                FrozenInputs.RoutePayload);
        }

        public RunSessionRestartResultV1 Restart(
            RestartRunSessionCommandV1 command)
        {
            if (command == null)
            {
                return RestartResult(
                    RunSessionRestartStatusV1.Rejected,
                    null,
                    "run-restart-command-null");
            }

            RestartReplayRecord existing;
            if (restartReplay.TryGetValue(command.OperationStableId, out existing))
            {
                if (string.Equals(
                    existing.CommandFingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return existing.Result;
                }
                return RestartResult(
                    RunSessionRestartStatusV1.ConflictingDuplicate,
                    command,
                    "run-restart-operation-conflict");
            }

            string rejection = ValidateRestart(command);
            if (!string.IsNullOrEmpty(rejection))
            {
                RunSessionRestartResultV1 rejected = RestartResult(
                    RunSessionRestartStatusV1.Rejected,
                    command,
                    rejection);
                restartReplay.Add(
                    command.OperationStableId,
                    new RestartReplayRecord(command.Fingerprint, rejected));
                return rejected;
            }

            IReadOnlyList<IRunLifecycleRuntimePortV1> ports =
                RuntimePorts.LifecyclePorts;
            for (int index = 0; index < ports.Count; index++)
            {
                rejection = ports[index].ValidateRestart(
                    command.RetiringLifecycleGeneration,
                    command.ReplacementLifecycleGeneration,
                    command.AuthoritativeTick);
                if (!string.IsNullOrEmpty(rejection))
                {
                    RunSessionRestartResultV1 rejected = RestartResult(
                        RunSessionRestartStatusV1.Rejected,
                        command,
                        ports[index].PortId + ":" + rejection);
                    restartReplay.Add(
                        command.OperationStableId,
                        new RestartReplayRecord(command.Fingerprint, rejected));
                    return rejected;
                }
            }

            for (int index = 0; index < ports.Count; index++)
            {
                RunRuntimePortRestartResultV1 portResult = ports[index].Restart(
                    command.OperationStableId,
                    command.RetiringLifecycleGeneration,
                    command.ReplacementLifecycleGeneration,
                    command.AuthoritativeTick);
                if (portResult == null
                    || !portResult.Succeeded
                    || portResult.LifecycleGeneration
                        != command.ReplacementLifecycleGeneration)
                {
                    throw new InvalidOperationException(
                        "A run runtime port rejected restart after successful preflight: "
                        + ports[index].PortId
                        + ":"
                        + (portResult == null
                            ? "null-result"
                            : portResult.RejectionCode));
                }
            }

            lifecycleGeneration = command.ReplacementLifecycleGeneration;
            authoritativeTick = command.AuthoritativeTick;
            ResetLocalState(command.Policy);
            RunSessionRestartResultV1 applied = RestartResult(
                RunSessionRestartStatusV1.Applied,
                command,
                string.Empty);
            restartReplay.Add(
                command.OperationStableId,
                new RestartReplayRecord(command.Fingerprint, applied));
            return applied;
        }

        public RunSessionEndResultV1 End(EndRunSessionCommandV1 command)
        {
            if (command == null)
            {
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.Rejected,
                    null,
                    null,
                    "run-end-command-null");
            }

            EndReplayRecord existing;
            if (endReplay.TryGetValue(command.OperationStableId, out existing))
            {
                if (string.Equals(
                    existing.CommandFingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return existing.Result;
                }
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.ConflictingDuplicate,
                    command,
                    terminalReceipt,
                    "run-end-operation-conflict");
            }

            string rejection = ValidateEnd(command);
            if (!string.IsNullOrEmpty(rejection))
            {
                RunSessionEndResultV1 rejected =
                    new RunSessionEndResultV1(
                        RunSessionEndStatusV1.Rejected,
                        command,
                        terminalReceipt,
                        rejection);
                endReplay.Add(
                    command.OperationStableId,
                    new EndReplayRecord(command.Fingerprint, rejected));
                return rejected;
            }

            MissionRunAuthorityResultV1 existingResult =
                RuntimePorts.MissionResults.EndRun(
                    command,
                    FrozenInputs.RoutePayload);
            if (existingResult == null
                || !existingResult.Succeeded
                || existingResult.ResultPayload == null)
            {
                RunSessionEndResultV1 rejected =
                    new RunSessionEndResultV1(
                        RunSessionEndStatusV1.Rejected,
                        command,
                        null,
                        existingResult == null
                            ? "mission-result-port-null"
                            : existingResult.RejectionCode);
                endReplay.Add(
                    command.OperationStableId,
                    new EndReplayRecord(command.Fingerprint, rejected));
                return rejected;
            }

            authoritativeTick = command.AuthoritativeTick;
            lifecycleState = RunSessionLifecycleStateV1.Ended;
            terminalReceipt = new RunSessionEndReceiptV1(
                RunStableId,
                FrozenInputs.Character.CharacterInstanceStableId,
                FrozenInputs.Character.Revision,
                FrozenInputs.Character.Fingerprint,
                StartCommand.MissionLayoutStableId,
                StartCommand.DifficultyStableId,
                StartCommand.DeterministicSeed,
                FrozenInputs.Fingerprint,
                FrozenInputs.CombatProfile.Fingerprint,
                ExportLocalState(),
                existingResult.ResultPayload);
            RunSessionEndResultV1 ended = new RunSessionEndResultV1(
                RunSessionEndStatusV1.Ended,
                command,
                terminalReceipt,
                string.Empty);
            endReplay.Add(
                command.OperationStableId,
                new EndReplayRecord(command.Fingerprint, ended));
            return ended;
        }

        public RunLocalStateSnapshotV1 ExportLocalState()
        {
            return new RunLocalStateSnapshotV1(
                runCash,
                temporaryPickups,
                counters,
                missionStatistics);
        }

        public RunHudSnapshotV1 ExportHudSnapshot()
        {
            RunPlayerRuntimeSnapshotV1 player =
                RuntimePorts.Player.ExportSnapshot();
            MissionRunPayloadV1 runPayload;
            long strongboxCount = RuntimePorts.MissionResults.TryGetRun(
                RunStableId,
                out runPayload)
                && runPayload != null
                    ? runPayload.CollectedStrongboxes.Count
                    : 0L;
            return new RunHudSnapshotV1(
                RunStableId,
                FrozenInputs.Character.CharacterInstanceStableId,
                player.ParticipantStableId,
                lifecycleState,
                lifecycleGeneration,
                player.CurrentHealth,
                player.MaximumHealth,
                runCash,
                strongboxCount,
                FrozenInputs.CombatProfile.Fingerprint);
        }

        public RunDebugSnapshotV1 ExportDebugSnapshot()
        {
            var fingerprints = new Dictionary<string, string>(
                StringComparer.Ordinal);
            foreach (IRunLifecycleRuntimePortV1 port in
                RuntimePorts.LifecyclePorts)
            {
                fingerprints.Add(port.PortId, port.SnapshotFingerprint);
            }
            return new RunDebugSnapshotV1(
                RunStableId,
                lifecycleState,
                lifecycleGeneration,
                authoritativeTick,
                StartCommand.Fingerprint,
                FrozenInputs.Fingerprint,
                ExportLocalState().Fingerprint,
                fingerprints,
                terminalReceipt == null
                    ? string.Empty
                    : terminalReceipt.Fingerprint);
        }

        public RunRecoveryDiagnosticSnapshotV1 ExportRecoveryDiagnostics()
        {
            return new RunRecoveryDiagnosticSnapshotV1(
                ExportDebugSnapshot(),
                FrozenInputs.Character.Fingerprint,
                FrozenInputs.Character.Revision,
                false);
        }

        public RunCheckpointV1 ExportCheckpoint()
        {
            return new RunCheckpointV1(
                ExportRecoveryDiagnostics(),
                ExportLocalState());
        }

        private string ValidateRestart(RestartRunSessionCommandV1 command)
        {
            if (command.RunStableId != RunStableId)
            {
                return "run-restart-wrong-run";
            }
            if (lifecycleState == RunSessionLifecycleStateV1.Ended)
            {
                return "run-restart-after-end";
            }
            if (command.RetiringLifecycleGeneration < lifecycleGeneration)
            {
                return "run-restart-stale-generation";
            }
            if (command.RetiringLifecycleGeneration > lifecycleGeneration)
            {
                return "run-restart-future-generation";
            }
            if (command.ReplacementLifecycleGeneration
                != lifecycleGeneration + 1L)
            {
                return "run-restart-generation-not-incremented";
            }
            if (command.AuthoritativeTick < authoritativeTick)
            {
                return "run-restart-stale-tick";
            }
            return string.Empty;
        }

        private string ValidateEnd(EndRunSessionCommandV1 command)
        {
            if (command.RunStableId != RunStableId)
            {
                return "run-end-wrong-run";
            }
            if (command.LifecycleGeneration != lifecycleGeneration)
            {
                return command.LifecycleGeneration < lifecycleGeneration
                    ? "run-end-stale-generation"
                    : "run-end-future-generation";
            }
            if (lifecycleState == RunSessionLifecycleStateV1.Ended)
            {
                return "run-already-ended";
            }
            if (command.AuthoritativeTick < authoritativeTick)
            {
                return "run-end-stale-tick";
            }
            return string.Empty;
        }

        private RunSessionRestartResultV1 RestartResult(
            RunSessionRestartStatusV1 status,
            RestartRunSessionCommandV1 command,      }

            string rCode)
        {
            return new RunSessionRestartResultV1(
                status,
                command,
                lifecycleGeneration,
                ExportDebugSnapshot().Fingerprint,
                rejectionCode);
        }

        private RunLocalMutationResultV1 RejectLocal(
            RunLocalMutationCommandV1 command,
            RunLocalStateSnapshotV1 state,
                    string rCode)
        {
            return new RunLocalMutationResultV1(
                false,
                false,
                false,
                command,
                state,
                rejectionCode);
        }

        private static void Add(
            IDictionary<string, long> target,
                   key,
            long amount)
        {
            long current;
            target.TryGetValue(key, out current);
            target[key] = checked(current + amount);
        }

        private void ResetLocalState(RunRestartPolicyV1 policy)
        {
            if (!policy.RetainTemporaryPickups)
            {
                temporaryPickups.Clear();
            }
            if (!policy.RetainRunCounters)
            {
                counters.Clear();
            }
            if (!policy.RetainMissionStatistics)
            {
                missionStatistics.Clear();
            }
            if (!policy.RetainRunCash)
            {
                runCash = 0L;
            }
        }
    }
}
