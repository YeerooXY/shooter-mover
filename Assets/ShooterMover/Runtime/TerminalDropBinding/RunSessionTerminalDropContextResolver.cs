using System;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;

namespace ShooterMover.TerminalDropBinding
{
    public interface IRunRewardProgressionContextProvider
    {
        bool TryResolve(
            RunSessionAggregate run,
            out ProgressionContext progressionContext,
            out string diagnostic);
    }

    /// <summary>
    /// Narrow read-only bridge to the existing Run Session authority. It validates the
    /// exact run/lifecycle and exposes frozen generation context without mutating the run.
    /// </summary>
    public sealed class RunSessionTerminalDropContextResolver :
        ITerminalDropRunContextResolver
    {
        private readonly RunSessionState runSessions;
        private readonly IRunRewardProgressionContextProvider progressionContexts;
        private readonly int generationAlgorithmVersion;

        public RunSessionTerminalDropContextResolver(
            RunSessionState runSessions,
            IRunRewardProgressionContextProvider progressionContexts,
            int generationAlgorithmVersion)
        {
            this.runSessions = runSessions
                ?? throw new ArgumentNullException(nameof(runSessions));
            this.progressionContexts = progressionContexts
                ?? throw new ArgumentNullException(nameof(progressionContexts));
            if (generationAlgorithmVersion < 1)
                throw new ArgumentOutOfRangeException(nameof(generationAlgorithmVersion));
            this.generationAlgorithmVersion = generationAlgorithmVersion;
        }

        public bool TryResolve(
            StableId runStableId,
            long expectedLifecycleGeneration,
            out TerminalDropRunGenerationContext context,
            out TerminalDropRejectionCode rejectionCode,
            out string diagnostic)
        {
            context = null;
            rejectionCode = TerminalDropRejectionCode.None;
            diagnostic = string.Empty;
            RunSessionAggregate run;
            if (runStableId == null || !runSessions.TryGetRun(runStableId, out run) || run == null)
            {
                rejectionCode = TerminalDropRejectionCode.MissingRun;
                diagnostic = "terminal-drop-run-missing:" + (runStableId == null
                    ? "none"
                    : runStableId.ToString());
                return false;
            }
            if (run.LifecycleGeneration != expectedLifecycleGeneration)
            {
                rejectionCode = TerminalDropRejectionCode.WrongRunLifecycle;
                diagnostic = expectedLifecycleGeneration < run.LifecycleGeneration
                    ? "terminal-drop-run-stale-lifecycle"
                    : "terminal-drop-run-future-lifecycle";
                return false;
            }
            if (run.LifecycleState == RunSessionLifecycleState.Ended)
            {
                rejectionCode = TerminalDropRejectionCode.RunEnded;
                diagnostic = "terminal-drop-run-ended";
                return false;
            }

            ProgressionContext progression;
            string progressionDiagnostic;
            if (!progressionContexts.TryResolve(run, out progression, out progressionDiagnostic)
                || progression == null)
            {
                rejectionCode = TerminalDropRejectionCode.GenerationFailed;
                diagnostic = string.IsNullOrWhiteSpace(progressionDiagnostic)
                    ? "terminal-drop-progression-context-missing"
                    : progressionDiagnostic;
                return false;
            }

            context = new TerminalDropRunGenerationContext(
                run.RunStableId,
                run.LifecycleGeneration,
                unchecked((ulong)run.StartCommand.DeterministicSeed),
                generationAlgorithmVersion,
                progression,
                run.StartCommand.EventModifierContextFingerprint);
            return true;
        }
    }
}
