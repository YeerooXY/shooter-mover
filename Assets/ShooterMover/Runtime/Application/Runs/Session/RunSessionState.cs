using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    public sealed class RunSessionState
    {
        private sealed class StartReplayRecord
        {
            public StartReplayRecord(
                string commandFingerprint,
                RunSessionStartResult result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }
            public RunSessionStartResult Result { get; }
        }

        private readonly IRunSessionStartSource startSource;
        private readonly Dictionary<StableId, StartReplayRecord> startReplay =
            new Dictionary<StableId, StartReplayRecord>();
        private readonly Dictionary<StableId, RunSessionAggregate> runs =
            new Dictionary<StableId, RunSessionAggregate>();

        public RunSessionState(IRunSessionStartSource source)
        {
            startSource = source
                ?? throw new ArgumentNullException(nameof(source));
        }

        public int RunCount
        {
            get { return runs.Count; }
        }

        public RunSessionStartResult Start(
            StartRunSessionCommand command)
        {
            if (command == null)
            {
                return new RunSessionStartResult(
                    RunSessionStartStatus.Rejected,
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
                return new RunSessionStartResult(
                    RunSessionStartStatus.ConflictingDuplicate,
                    command.OperationStableId,
                    command.Fingerprint,
                    existing.Result.RunStableId,
                    existing.Result.RunSnapshotFingerprint,
                    "run-start-operation-conflict");
            }

            StableId runStableId = ResolveRunStableId(command);
            if (runs.ContainsKey(runStableId))
            {
                RunSessionStartResult collision =
                    new RunSessionStartResult(
                        RunSessionStartStatus.Rejected,
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

            RunSessionStartMaterial material =
                startSource.Resolve(command, runStableId);
            if (material == null || !material.Succeeded)
            {
                RunSessionStartResult rejected =
                    new RunSessionStartResult(
                        RunSessionStartStatus.Rejected,
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

            var aggregate = new RunSessionAggregate(
                command,
                runStableId,
                material.FrozenInputs,
                material.RuntimePorts);
            runs.Add(runStableId, aggregate);
            RunSessionStartResult result =
                new RunSessionStartResult(
                    RunSessionStartStatus.Started,
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
            out RunSessionAggregate aggregate)
        {
            aggregate = null;
            return runStableId != null
                && runs.TryGetValue(runStableId, out aggregate);
        }

        private static StableId ResolveRunStableId(
            StartRunSessionCommand command)
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
            string hash = RunSessionFingerprint.Hash(material);
            return StableId.Create("run-instance", hash.Substring(0, 40));
        }
    }

    public sealed partial class RunSessionAggregate
    {
        private sealed class RestartReplayRecord
        {
            public RestartReplayRecord(
                string commandFingerprint,
                RunSessionRestartResult result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }
            public RunSessionRestartResult Result { get; }
        }

        private sealed class EndReplayRecord
        {
            public EndReplayRecord(
                string commandFingerprint,
                RunSessionEndResult result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }
            public RunSessionEndResult Result { get; }
        }

        private sealed class FactReplayRecord
        {
            public FactReplayRecord(
                string factFingerprint,
                RunSessionFactAdmissionResult result)
            {
                FactFingerprint = factFingerprint;
                Result = result;
            }

            public string FactFingerprint { get; }
            public RunSessionFactAdmissionResult Result { get; }
        }

        private sealed class LocalReplayRecord
        {
            public LocalReplayRecord(
                string commandFingerprint,
                RunLocalMutationResult result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }
            public RunLocalMutationResult Result { get; }
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
        private RunSessionLifecycleState lifecycleState;
        private RunSessionEndReceipt terminalReceipt;

        internal RunSessionAggregate(
            StartRunSessionCommand startCommand,
            StableId runStableId,
            FrozenCharacterRunInputs frozenInputs,
            RunSessionLivePorts runtimePorts)
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
            lifecycleState = RunSessionLifecycleState.Active;

            IRunConditionLivePort conditionRuntime =
                RuntimePorts.ConditionalFacts as IRunConditionLivePort;
            if (conditionRuntime != null)
            {
                conditionRuntime.Bind(this);
            }
        }

        public StartRunSessionCommand StartCommand { get; }
        public StableId RunStableId { get; }
        public FrozenCharacterRunInputs FrozenInputs { get; }
        public RunSessionLivePorts RuntimePorts { get; }
        public long LifecycleGeneration
        {
            get { return lifecycleGeneration; }
        }
        public long AuthoritativeTick
        {
            get { return authoritativeTick; }
        }
        public RunSessionLifecycleState LifecycleState
        {
            get { return lifecycleState; }
        }
        public RunSessionEndReceipt TerminalReceipt
        {
            get { return terminalReceipt; }
        }

        public RunSessionFactAdmissionResult AdmitFact(
            RunSessionFactEnvelope fact)
        {
            if (fact == null)
            {
                return new RunSessionFactAdmissionResult(
                    RunSessionFactAdmissionStatus.ConflictingDuplicate,
                    null,
                    "run-fact-null");
            }
            if (fact.RunStableId != RunStableId)
            {
                return new RunSessionFactAdmissionResult(
                    RunSessionFactAdmissionStatus.WrongRun,
                    fact,
                    "run-fact-wrong-run");
            }
            if (fact.LifecycleGeneration != lifecycleGeneration)
            {
                return new RunSessionFactAdmissionResult(
                    RunSessionFactAdmissionStatus.StaleLifecycle,
                    fact,
                    fact.LifecycleGeneration < lifecycleGeneration
                        ? "run-fact-stale-generation"
                        : "run-fact-future-generation");
            }
            if (lifecycleState == RunSessionLifecycleState.Ended)
            {
                return new RunSessionFactAdmissionResult(
                    RunSessionFactAdmissionStatus.RunEnded,
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
                    ? new RunSessionFactAdmissionResult(
                        RunSessionFactAdmissionStatus.ExactReplay,
                        fact,
                        string.Empty)
                    : new RunSessionFactAdmissionResult(
                        RunSessionFactAdmissionStatus.ConflictingDuplicate,
                        fact,
                        "run-fact-operation-conflict");
            }

            var accepted = new RunSessionFactAdmissionResult(
                RunSessionFactAdmissionStatus.Accepted,
                fact,
                string.Empty);
            factReplay.Add(
                fact.OperationStableId,
                new FactReplayRecord(fact.Fingerprint, accepted));
            return accepted;
        }

        public RunLocalMutationResult ApplyLocalMutation(
            RunLocalMutationCommand command)
        {
            RunLocalStateSnapshot before = ExportLocalState();
            if (command == null)
            {
                return new RunLocalMutationResult(
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
            if (lifecycleState == RunSessionLifecycleState.Ended)
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
                    return new RunLocalMutationResult(
                        true,
                        true,
                        false,
                        command,
                        existing.Result.State,
                        string.Empty);
                }
                return new RunLocalMutationResult(
                    false,
                    false,
                    true,
                    command,
                    before,
                    "run-local-operation-conflict");
            }

            switch (command.Kind)
            {
                case RunLocalMutationKind.AddTemporaryPickup:
                    Add(temporaryPickups, command.Key, command.Amount);
                    break;
                case RunLocalMutationKind.AddRunCash:
                    runCash = checked(runCash + command.Amount);
                    break;
                case RunLocalMutationKind.IncrementCounter:
                    Add(counters, command.Key, command.Amount);
                    break;
                case RunLocalMutationKind.IncrementStatistic:
                    Add(missionStatistics, command.Key, command.Amount);
                    break;
                default:
                    return RejectLocal(command, before, "run-local-kind-invalid");
            }

            RunLocalStateSnapshot after = ExportLocalState();
            var accepted = new RunLocalMutationResult(
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

        public MissionRunStateResult RecordCollectedStrongbox(
            RunStrongboxCollectionRequest request)
        {
            if (request == null
                || request.RunStableId != RunStableId
                || request.LifecycleGeneration != lifecycleGeneration
                || lifecycleState == RunSessionLifecycleState.Ended)
            {
                return new MissionRunStateResult(
                    MissionRunStateStatus.InvalidRequest,
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

        public RunSessionRestartResult Restart(
            RestartRunSessionCommand command)
        {
            if (command == null)
            {
                return RestartResult(
                    RunSessionRestartStatus.Rejected,
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
                    RunSessionRestartStatus.ConflictingDuplicate,
                    command,
                    "run-restart-operation-conflict");
            }

            string rejection = ValidateRestart(command);
            if (!string.IsNullOrEmpty(rejection))
            {
                RunSessionRestartResult rejected = RestartResult(
                    RunSessionRestartStatus.Rejected,
                    command,
                    rejection);
                restartReplay.Add(
                    command.OperationStableId,
                    new RestartReplayRecord(command.Fingerprint, rejected));
                return rejected;
            }

            IReadOnlyList<IRunLifecycleLivePort> ports =
                RuntimePorts.LifecyclePorts;
            for (int index = 0; index < ports.Count; index++)
            {
                rejection = ports[index].ValidateRestart(
                    command.RetiringLifecycleGeneration,
                    command.ReplacementLifecycleGeneration,
                    command.AuthoritativeTick);
                if (!string.IsNullOrEmpty(rejection))
                {
                    RunSessionRestartResult rejected = RestartResult(
                        RunSessionRestartStatus.Rejected,
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
                RunLivePortRestartResult portResult = ports[index].Restart(
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
            RunSessionRestartResult applied = RestartResult(
                RunSessionRestartStatus.Applied,
                command,
                string.Empty);
            restartReplay.Add(
                command.OperationStableId,
                new RestartReplayRecord(command.Fingerprint, applied));
            return applied;
        }

        public RunSessionEndResult End(EndRunSessionCommand command)
        {
            if (command == null)
            {
                return new RunSessionEndResult(
                    RunSessionEndStatus.Rejected,
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
                return new RunSessionEndResult(
                    RunSessionEndStatus.ConflictingDuplicate,
                    command,
                    terminalReceipt,
                    "run-end-operation-conflict");
            }

            string rejection = ValidateEnd(command);
            if (!string.IsNullOrEmpty(rejection))
            {
                RunSessionEndResult rejected =
                    new RunSessionEndResult(
                        RunSessionEndStatus.Rejected,
                        command,
                        terminalReceipt,
                        rejection);
                endReplay.Add(
                    command.OperationStableId,
                    new EndReplayRecord(command.Fingerprint, rejected));
                return rejected;
            }

            MissionRunStateResult existingResult =
                RuntimePorts.MissionResults.EndRun(
                    command,
                    FrozenInputs.RoutePayload);
            if (existingResult == null
                || !existingResult.Succeeded
                || existingResult.ResultPayload == null)
            {
                RunSessionEndResult rejected =
                    new RunSessionEndResult(
                        RunSessionEndStatus.Rejected,
                        command,
                        null,
                        existingResult == null
                            ? "mission-result-port-null"
                            : existingResult.RejectionCode);
                endReplay.Add(
                    command.OperationStableId,
                    new EndReplayRecord(
                        command.Fingerprint,
                        rejected));
                return rejected;
            }

            authoritativeTick = command.AuthoritativeTick;
            lifecycleState = RunSessionLifecycleState.Ended;
            terminalReceipt = new RunSessionEndReceipt(
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
            RunSessionEndResult ended = new RunSessionEndResult(
                RunSessionEndStatus.Ended,
                command,
                terminalReceipt,
                string.Empty);
            endReplay.Add(
                command.OperationStableId,
                new EndReplayRecord(command.Fingerprint, ended));
            return ended;
        }

        public RunLocalStateSnapshot ExportLocalState()
        {
            return new RunLocalStateSnapshot(
                runCash,
                temporaryPickups,
                counters,
                missionStatistics);
        }

        public RunHudSnapshot ExportHudSnapshot()
        {
            RunPlayerSnapshot player =
                RuntimePorts.Player.ExportSnapshot();
            MissionRunPayload runPayload;
            long strongboxCount = RuntimePorts.MissionResults.TryGetRun(
                RunStableId,
                out runPayload)
                && runPayload != null
                    ? runPayload.CollectedStrongboxes.Count
                    : 0L;
            return new RunHudSnapshot(
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

        public RunDebugSnapshot ExportDebugSnapshot()
        {
            var fingerprints = new Dictionary<string, string>(
                StringComparer.Ordinal);
            foreach (IRunLifecycleLivePort port in
                RuntimePorts.LifecyclePorts)
            {
                fingerprints.Add(port.PortId, port.SnapshotFingerprint);
            }
            return new RunDebugSnapshot(
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

        public RunRecoveryDiagnosticSnapshot ExportRecoveryDiagnostics()
        {
            return new RunRecoveryDiagnosticSnapshot(
                ExportDebugSnapshot(),
                FrozenInputs.Character.Fingerprint,
                FrozenInputs.Character.Revision,
                false);
        }

        public RunCheckpoint ExportCheckpoint()
        {
            return new RunCheckpoint(
                ExportRecoveryDiagnostics(),
                ExportLocalState());
        }

        private string ValidateRestart(RestartRunSessionCommand command)
        {
            if (command.RunStableId != RunStableId)
            {
                return "run-restart-wrong-run";
            }
            if (lifecycleState == RunSessionLifecycleState.Ended)
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

        private string ValidateEnd(EndRunSessionCommand command)
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
            if (lifecycleState == RunSessionLifecycleState.Ended)
            {
                return "run-already-ended";
            }
            if (command.AuthoritativeTick < authoritativeTick)
            {
                return "run-end-stale-tick";
            }
            return string.Empty;
        }

        private RunSessionRestartResult RestartResult(
            RunSessionRestartStatus status,
            RestartRunSessionCommand command,
            string rejectionCode)
        {
            return new RunSessionRestartResult(
                status,
                command,
                lifecycleGeneration,
                ExportDebugSnapshot().Fingerprint,
                rejectionCode);
        }

        private RunLocalMutationResult RejectLocal(
            RunLocalMutationCommand command,
            RunLocalStateSnapshot state,
            string rejectionCode)
        {
            return new RunLocalMutationResult(
                false,
                false,
                false,
                command,
                state,
                rejectionCode);
        }

        private static void Add(
            IDictionary<string, long> target,
            string key,
            long amount)
        {
            long current;
            target.TryGetValue(key, out current);
            target[key] = checked(current + amount);
        }

        private void ResetLocalState(RunRestartPolicy policy)
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
