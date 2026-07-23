using System;
using ShooterMover.Application.Runs.Session;

namespace ShooterMover.TerminalDropBinding
{
    /// <summary>
    /// Reads the authored game-mode, event, economy and pacing inputs frozen on the
    /// exact transient run. No generic terminal authority assumes Campaign mode.
    /// </summary>
    public sealed class RunSessionTerminalRewardEnvironmentResolverV1 :
        ITerminalRewardEnvironmentResolverV1
    {
        private readonly Func<RunSessionAggregateV1> runResolver;

        public RunSessionTerminalRewardEnvironmentResolverV1(
            Func<RunSessionAggregateV1> runResolver)
        {
            this.runResolver = runResolver
                ?? throw new ArgumentNullException(nameof(runResolver));
        }

        public bool TryResolve(
            TerminalDropSourceFactV1 source,
            TerminalDropRunGenerationContextV1 runContext,
            out TerminalRewardEnvironmentV1 environment,
            out string diagnostic)
        {
            environment = null;
            RunSessionAggregateV1 run = runResolver();
            if (source == null
                || runContext == null
                || run == null
                || run.IsEnded)
            {
                diagnostic = "terminal-personal-run-environment-unavailable";
                return false;
            }
            if (source.RunStableId != run.RunStableId
                || source.RunLifecycleGeneration != run.LifecycleGeneration
                || runContext.RunStableId != run.RunStableId
                || runContext.LifecycleGeneration != run.LifecycleGeneration)
            {
                diagnostic = "terminal-personal-run-environment-lifecycle-mismatch";
                return false;
            }

            RunRewardEnvironmentSnapshotV1 snapshot;
            try
            {
                snapshot = run.ExportRewardEnvironment();
            }
            catch (InvalidOperationException exception)
            {
                diagnostic = "terminal-personal-run-environment-not-configured:"
                    + exception.Message;
                return false;
            }
            environment = new TerminalRewardEnvironmentV1(
                snapshot.GameModeStableId,
                snapshot.EventModifierIds,
                snapshot.MoneyQuantityMultiplierPermille,
                snapshot.ScrapQuantityMultiplierPermille,
                snapshot.PacingPolicy);
            diagnostic = string.Empty;
            return true;
        }
    }
}
