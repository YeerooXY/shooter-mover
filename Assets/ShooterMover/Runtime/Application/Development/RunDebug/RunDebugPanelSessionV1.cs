using System;
using ShooterMover.Contracts.Missions.Results;

namespace ShooterMover.Application.Development.RunDebug
{
    /// <summary>
    /// Presentation-facing session. It delegates all mutation to the runtime port and
    /// guarantees that repeated End Run input never calls RUN-001 more than once.
    /// </summary>
    public sealed class RunDebugPanelSessionV1
    {
        private readonly IRunDebugRuntimePortV1 runtime;
        private RunDebugEndResultV1 terminalEndResult;

        public RunDebugPanelSessionV1(IRunDebugRuntimePortV1 runtime)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public RunDebugSnapshotV1 Snapshot { get; private set; }
        public RunDebugSpawnBatchResultV1 LastSpawnResult { get; private set; }
        public RunDebugEndResultV1 LastEndResult { get { return terminalEndResult; } }

        public RunDebugSpawnBatchResultV1 Spawn(RunDebugSpawnRequestV1 request)
        {
            if (terminalEndResult != null)
            {
                LastSpawnResult = new RunDebugSpawnBatchResultV1(
                    RunDebugSpawnBatchStatusV1.Rejected,
                    Snapshot,
                    "The mission run is already terminal.");
                return LastSpawnResult;
            }

            LastSpawnResult = runtime.Spawn(request);
            Snapshot = LastSpawnResult == null
                ? runtime.RefreshSnapshot()
                : LastSpawnResult.Snapshot;
            return LastSpawnResult;
        }

        public RunDebugSnapshotV1 Refresh()
        {
            Snapshot = runtime.RefreshSnapshot();
            return Snapshot;
        }

        public RunDebugEndResultV1 EndRun(
            MissionRunCompletionStateV1 completionState)
        {
            if (terminalEndResult != null)
            {
                return terminalEndResult;
            }

            terminalEndResult = runtime.EndRun(completionState);
            Snapshot = runtime.RefreshSnapshot();
            return terminalEndResult;
        }
    }
}
