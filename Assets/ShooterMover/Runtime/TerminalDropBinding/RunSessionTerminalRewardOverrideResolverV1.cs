using System;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Runs.Session;

namespace ShooterMover.TerminalDropBinding
{
    /// <summary>
    /// Resolves production mode, mission, difficulty, event and placement overrides
    /// from the exact run snapshot. Precedence remains owned by RewardProfileResolverV1.
    /// </summary>
    public sealed class RunSessionTerminalRewardOverrideResolverV1 :
        ITerminalRewardOverrideResolverV1
    {
        private readonly Func<RunSessionAggregateV1> runResolver;

        public RunSessionTerminalRewardOverrideResolverV1(
            Func<RunSessionAggregateV1> runResolver)
        {
            this.runResolver = runResolver
                ?? throw new ArgumentNullException(nameof(runResolver));
        }

        public bool TryResolve(
            TerminalDropSourceFactV1 source,
            TerminalDropRunGenerationContextV1 runContext,
            TerminalRewardEnvironmentV1 environment,
            TerminalRewardPlacementContextV1 placementContext,
            out TerminalRewardOverrideSetV1 overrides,
            out string diagnostic)
        {
            overrides = null;
            RunSessionAggregateV1 run = runResolver();
            if (source == null
                || runContext == null
                || environment == null
                || placementContext == null
                || run == null
                || run.IsEnded)
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

            RewardContextOverrideResolutionV1 resolved =
                ProductionRewardOverrideCatalogV1.Resolve(
                    source.DeclaredDropProfileStableId,
                    environment.GameModeStableId,
                    run.StartCommand.MissionLayoutStableId,
                    run.StartCommand.DifficultyStableId,
                    environment.EventModifierIds,
                    placementContext.PlacementStableId);
            overrides = new TerminalRewardOverrideSetV1(
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
