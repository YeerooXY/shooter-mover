using System;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Runs.Session;

namespace ShooterMover.TerminalDropBinding
{
    /// <summary>
    /// Resolves production mode, mission, difficulty, event and placement overrides
    /// from the exact run snapshot. Precedence remains owned by RewardProfileResolver.
    /// </summary>
    public sealed class RunSessionTerminalRewardOverrideResolver :
        ITerminalRewardOverrideResolver
    {
        private readonly Func<RunSessionAggregate> runResolver;

        public RunSessionTerminalRewardOverrideResolver(
            Func<RunSessionAggregate> runResolver)
        {
            this.runResolver = runResolver
                ?? throw new ArgumentNullException(nameof(runResolver));
        }

        public bool TryResolve(
            TerminalDropSourceFact source,
            TerminalDropRunGenerationContext runContext,
            TerminalRewardEnvironment environment,
            TerminalRewardPlacementContext placementContext,
            out TerminalRewardOverrideSet overrides,
            out string diagnostic)
        {
            overrides = null;
            RunSessionAggregate run = runResolver();
            if (source == null
                || runContext == null
                || environment == null
                || placementContext == null
                || run == null
                || run.LifecycleState == RunSessionLifecycleState.Ended)
            {
                diagnostic = "terminal-personal-run-overrides-unavailable";
                return false;
            }
            if (source.RunStableId != run.RunStableId
                || source.RunLifecycleGeneration != run.LifecycleGeneration
                || runContext.RunStableId != run.RunStableId
                || runContext.LifecycleGeneration != run.LifecycleGeneration)
            {
                diagnostic = "terminal-personal-run-overrides-lifecycle-mismatch";
                return false;
            }

            RewardContextOverrideResolution resolved =
                RewardOverrideCatalog.Resolve(
                    source.DeclaredDropProfileStableId,
                    environment.GameModeStableId,
                    run.StartCommand.MissionLayoutStableId,
                    run.StartCommand.DifficultyStableId,
                    environment.EventModifierIds,
                    placementContext.PlacementStableId);
            overrides = new TerminalRewardOverrideSet(
                resolved.GameModeOverride,
                resolved.MissionOverride,
                resolved.DifficultyOverride,
                resolved.EventOverrides,
                resolved.PlacementOverride);
            diagnostic = string.Empty;
            return true;
        }
    }
}
