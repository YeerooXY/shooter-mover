using System;
using ShooterMover.Application.Runs.Session;

namespace ShooterMover.LootDropBinding
{
    /// <summary>
    /// Reads the authored game-mode, event, economy and pacing inputs frozen on the
    /// exact transient run. No generic terminal authority assumes Campaign mode.
    /// </summary>
    public sealed class RunSessionTerminalRewardEnvironmentResolver :
        ITerminalRewardEnvironmentResolver
    {
        private readonly Func<RunSessionAggregate> runResolver;

        public RunSessionTerminalRewardEnvironmentResolver(
            Func<RunSessionAggregate> runResolver)
        {
            this.runResolver = runResolver
                ?? throw new ArgumentNullException(nameof(runResolver));
        }

        public bool TryResolve(
            LootDropSourceFact source,
            LootDropRunGenerationContext runContext,
            out TerminalRewardEnvironment environment,
            out string diagnostic)
        {
            environment = null;
            RunSessionAggregate run = runResolver();
            if (source == null
                || runContext == null
                || run == null
                || run.LifecycleState == RunSessionLifecycleState.Ended)
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

            RunRewardEnvironmentSnapshot snapshot;
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
            environment = new TerminalRewardEnvironment(
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
