using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Domain.Common;

namespace ShooterMover.TerminalDropBinding
{
    internal sealed class AttributedTerminalRewardParticipantResolver :
        ITerminalRewardParticipantResolver
    {
        public bool TryResolve(
            TerminalDropSourceFact source,
            TerminalDropRunGenerationContext runContext,
            TerminalRewardPlacementContext placementContext,
            out IReadOnlyList<TerminalRewardParticipant> participants,
            out TerminalRewardEligibilityPolicy eligibilityPolicy,
            out string diagnostic)
        {
            if (source == null
                || runContext == null
                || source.AttributedParticipantStableId == null)
            {
                participants = Array.Empty<TerminalRewardParticipant>();
                eligibilityPolicy = new TerminalRewardEligibilityPolicy(
                    false,
                    false,
                    false);
                diagnostic = "terminal-personal-attributed-participant-missing";
                return false;
            }

            participants = new[]
            {
                new TerminalRewardParticipant(
                    source.AttributedParticipantStableId,
                    runContext.ProgressionContext.CharacterLevel,
                    true,
                    true,
                    true,
                    true,
                    false),
            };
            eligibilityPolicy = new TerminalRewardEligibilityPolicy(
                false,
                false,
                false);
            diagnostic = string.Empty;
            return true;
        }
    }

    internal sealed class DefaultTerminalRewardEnvironmentResolver :
        ITerminalRewardEnvironmentResolver
    {
        private static readonly StableId CampaignModeId =
            StableId.Parse("game-mode.campaign");

        public bool TryResolve(
            TerminalDropSourceFact source,
            TerminalDropRunGenerationContext runContext,
            out TerminalRewardEnvironment environment,
            out string diagnostic)
        {
            if (source == null || runContext == null)
            {
                environment = null;
                diagnostic = "terminal-personal-default-environment-invalid";
                return false;
            }
            environment = new TerminalRewardEnvironment(
                CampaignModeId,
                runContext.ProgressionContext.ProgressionTags,
                1000,
                1000,
                RunDropPacingCatalog.Resolve(
                    CampaignModeId,
                    null));
            diagnostic = string.Empty;
            return true;
        }
    }

    internal sealed class EmptyTerminalRewardOverrideResolver :
        ITerminalRewardOverrideResolver
    {
        public bool TryResolve(
            TerminalDropSourceFact source,
            TerminalDropRunGenerationContext runContext,
            TerminalRewardEnvironment environment,
            TerminalRewardPlacementContext placementContext,
            out TerminalRewardOverrideSet overrides,
            out string diagnostic)
        {
            if (source == null
                || runContext == null
                || environment == null
                || placementContext == null)
            {
                overrides = null;
                diagnostic = "terminal-personal-empty-overrides-invalid";
                return false;
            }
            overrides = TerminalRewardOverrideSet.Empty();
            diagnostic = string.Empty;
            return true;
        }
    }
}
