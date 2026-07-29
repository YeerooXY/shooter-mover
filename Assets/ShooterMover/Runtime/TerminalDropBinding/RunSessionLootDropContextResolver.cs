using System;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;

namespace ShooterMover.LootDropBinding
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
    public sealed class RunSessionLootDropContextResolver :
        ILootDropRunContextResolver
    {
        private readonly RunSessionState runSessions;
        private readonly IRunRewardProgressionContextProvider progressionContexts;
        private readonly int generationAlgorithmVersion;

        public RunSessionLootDropContextResolver(
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
            out LootDropRunGenerationContext context,
            out LootDropRejectionCode rejectionCode,
            out string diagnostic)
        {
            context = null;
            rejectionCode = LootDropRejectionCode.None;
            diagnostic = string.Empty;
            RunSessionAggregate run;
            if (runStableId == null || !runSessions.TryGetRun(runStableId, out run) || run == null)
            {
                rejectionCode = LootDropRejectionCode.MissingRun;
                diagnostic = "terminal-drop-run-missing:" + (runStableId == null
                    ? "none"
                    : runStableId.ToString());
                return false;
            }
            if (run.LifecycleGeneration != expectedLifecycleGeneration)
            {
                rejectionCode = LootDropRejectionCode.WrongRunLifecycle;
                diagnostic = expectedLifecycleGeneration < run.LifecycleGeneration
                    ? "terminal-drop-run-stale-lifecycle"
                    : "terminal-drop-run-future-lifecycle";
                return false;
            }
            if (run.LifecycleState == RunSessionLifecycleState.Ended)
            {
                rejectionCode = LootDropRejectionCode.RunEnded;
                diagnostic = "terminal-drop-run-ended";
                return false;
            }

            ProgressionContext progression;
            string progressionDiagnostic;
            if (!progressionContexts.TryResolve(run, out progression, out progressionDiagnostic)
                || progression == null)
            {
                rejectionCode = LootDropRejectionCode.GenerationFailed;
                diagnostic = string.IsNullOrWhiteSpace(progressionDiagnostic)
                    ? "terminal-drop-progression-context-missing"
                    : progressionDiagnostic;
                return false;
            }

            context = new LootDropRunGenerationContext(
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
