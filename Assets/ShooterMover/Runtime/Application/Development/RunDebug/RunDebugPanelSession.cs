using System;
using ShooterMover.Contracts.Missions.Results;

namespace ShooterMover.Application.Development.RunDebug
{
    /// <summary>
    /// Presentation-facing session. It delegates all mutation to the runtime port and
    /// guarantees that repeated End Run input never calls RUN-001 more than once.
    /// </summary>
    public sealed class RunDebugPanelSession
    {
        private readonly IRunDebugLivePort runtime;
        private RunDebugEndResult terminalEndResult;

        public RunDebugPanelSession(IRunDebugLivePort runtime)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public RunDebugSnapshot Snapshot { get; private set; }
        public RunDebugSpawnBatchResult LastSpawnResult { get; private set; }
        public RunDebugEndResult LastEndResult { get { return terminalEndResult; } }

        public RunDebugSpawnBatchResult Spawn(RunDebugSpawnRequest request)
        {
            if (terminalEndResult != null)
            {
                LastSpawnResult = new RunDebugSpawnBatchResult(
                    RunDebugSpawnBatchStatus.Rejected,
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

        public RunDebugSnapshot Refresh()
        {
            Snapshot = runtime.RefreshSnapshot();
            return Snapshot;
        }

        public RunDebugEndResult EndRun(
            MissionRunCompletionState completionState)
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
