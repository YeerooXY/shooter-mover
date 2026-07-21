using System;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;

namespace ShooterMover.TerminalDropBinding
{
    public interface IRunRewardProgressionContextProviderV1
    {
        bool TryResolve(
            RunSessionAggregateV1 run,
            out ProgressionContext progressionContext,
            out string diagnostic);
    }

    /// <summary>
    /// Narrow read-only bridge to the existing Run Session authority. It validates the
    /// exact run/lifecycle and exposes frozen generation context without mutating the run.
    /// </summary>
    public sealed class RunSessionTerminalDropContextResolverV1 :
        ITerminalDropRunContextResolverV1
    {
        private readonly RunSessionAuthorityV1 runSessions;
        private readonly IRunRewardProgressionContextProviderV1 progressionContexts;
        private readonly int generationAlgorithmVersion;

        public RunSessionTerminalDropContextResolverV1(
            RunSessionAuthorityV1 runSessions,
            IRunRewardProgressionContextProviderV1 progressionContexts,
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
            out TerminalDropRunGenerationContextV1 context,
            out TerminalDropRejectionCodeV1 rejectionCode,
            out string diagnostic)
        {
            context = null;
            rejectionCode = TerminalDropRejectionCodeV1.None;
            diagnostic = string.Empty;
            RunSessionAggregateV1 run;
            if (runStableId == null || !runSessions.TryGetRun(runStableId, out run) || run == null)
            {
                rejectionCode = TerminalDropRejectionCodeV1.MissingRun;
                diagnostic = "terminal-drop-run-missing:" + (runStableId == null
                    ? "none"
                    : runStableId.ToString());
                return false;
            }
            if (run.LifecycleGeneration != expectedLifecycleGeneration)
            {
                rejectionCode = TerminalDropRejectionCodeV1.WrongRunLifecycle;
                diagnostic = expectedLifecycleGeneration < run.LifecycleGeneration
                    ? "terminal-drop-run-stale-lifecycle"
                    : "terminal-drop-run-future-lifecycle";
                return false;
            }
            if (run.LifecycleState == RunSessionLifecycleStateV1.Ended)
            {
                rejectionCode = TerminalDropRejectionCodeV1.RunEnded;
                diagnostic = "terminal-drop-run-ended";
                return false;
            }

            ProgressionContext progression;
            string progressionDiagnostic;
            if (!progressionContexts.TryResolve(run, out progression, out progressionDiagnostic)
                || progression == null)
            {
                rejectionCode = TerminalDropRejectionCodeV1.GenerationFailed;
                diagnostic = string.IsNullOrWhiteSpace(progressionDiagnostic)
                    ? "terminal-drop-progression-context-missing"
                    : progressionDiagnostic;
                return false;
            }

            context = new TerminalDropRunGenerationContextV1(
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
