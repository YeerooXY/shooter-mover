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
    public sealed class RunSessionTerminalRewardParticipantResolverV1 :
        ITerminalRewardParticipantResolverV1
    {
        private readonly Func<RunSessionAggregateV1> runResolver;
        private readonly TerminalRewardEligibilityPolicyV1 eligibilityPolicy;

        public RunSessionTerminalRewardParticipantResolverV1(
            Func<RunSessionAggregateV1> runResolver,
            TerminalRewardEligibilityPolicyV1 eligibilityPolicy)
        {
            this.runResolver = runResolver
                ?? throw new ArgumentNullException(nameof(runResolver));
            this.eligibilityPolicy = eligibilityPolicy
                ?? throw new ArgumentNullException(nameof(eligibilityPolicy));
        }

        public bool TryResolve(
            TerminalDropSourceFactV1 source,
            TerminalDropRunGenerationContextV1 runContext,
            TerminalRewardPlacementContextV1 placementContext,
            out IReadOnlyList<TerminalRewardParticipantV1> participants,
            out TerminalRewardEligibilityPolicyV1 resolvedEligibilityPolicy,
            out string diagnostic)
        {
            participants = Array.Empty<TerminalRewardParticipantV1>();
            resolvedEligibilityPolicy = eligibilityPolicy;
            RunSessionAggregateV1 run = runResolver();
            if (source == null
                || runContext == null
                || placementContext == null
                || run == null
                || run.IsEnded)
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

            IReadOnlyList<RunRewardParticipantStateV1> roster =
                run.ExportRewardParticipants();
            var values = new List<TerminalRewardParticipantV1>(roster.Count);
            for (int index = 0; index < roster.Count; index++)
            {
                RunRewardParticipantStateV1 participant = roster[index];
                values.Add(new TerminalRewardParticipantV1(
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
