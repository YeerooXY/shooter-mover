using System;
using System.Collections.Generic;
using ShooterMover.Application.Runs.Session;

namespace ShooterMover.TerminalDropBinding
{
    /// <summary>
    /// Resolves the complete one-to-four-player run roster. Kill credit remains part of
    /// the immutable source fact but does not collapse a personal multiplayer roll to
    /// the credited participant.
    /// </summary>
    public sealed class RunSessionTerminalRewardParticipantResolver :
        ITerminalRewardParticipantResolver
    {
        private readonly Func<RunSessionAggregate> runResolver;
        private readonly TerminalRewardEligibilityPolicy eligibilityPolicy;

        public RunSessionTerminalRewardParticipantResolver(
            Func<RunSessionAggregate> runResolver,
            TerminalRewardEligibilityPolicy eligibilityPolicy)
        {
            this.runResolver = runResolver
                ?? throw new ArgumentNullException(nameof(runResolver));
            this.eligibilityPolicy = eligibilityPolicy
                ?? throw new ArgumentNullException(nameof(eligibilityPolicy));
        }

        public bool TryResolve(
            TerminalDropSourceFact source,
            TerminalDropRunGenerationContext runContext,
            TerminalRewardPlacementContext placementContext,
            out IReadOnlyList<TerminalRewardParticipant> participants,
            out TerminalRewardEligibilityPolicy resolvedEligibilityPolicy,
            out string diagnostic)
        {
            participants = Array.Empty<TerminalRewardParticipant>();
            resolvedEligibilityPolicy = eligibilityPolicy;
            RunSessionAggregate run = runResolver();
            if (source == null
                || runContext == null
                || placementContext == null
                || run == null
                || run.LifecycleState == RunSessionLifecycleState.Ended)
            {
                diagnostic = "terminal-personal-run-roster-unavailable";
                return false;
            }
            if (source.RunStableId != run.RunStableId
                || source.RunLifecycleGeneration != run.LifecycleGeneration
                || runContext.RunStableId != run.RunStableId
                || runContext.LifecycleGeneration != run.LifecycleGeneration)
            {
                diagnostic = "terminal-personal-run-roster-lifecycle-mismatch";
                return false;
            }

            IReadOnlyList<RunRewardParticipantState> roster =
                run.ExportRewardParticipants();
            var values = new List<TerminalRewardParticipant>(roster.Count);
            for (int index = 0; index < roster.Count; index++)
            {
                RunRewardParticipantState participant = roster[index];
                values.Add(new TerminalRewardParticipant(
                    participant.ParticipantStableId,
                    participant.PlayerLevel,
                    participant.ActiveInRun,
                    participant.ConnectedOrReconnectReserved,
                    participant.PresentInCurrentRoom,
                    participant.ContributionEligible,
                    participant.Spectator));
            }
            participants = values.AsReadOnly();
            diagnostic = string.Empty;
            return true;
        }
    }
}
