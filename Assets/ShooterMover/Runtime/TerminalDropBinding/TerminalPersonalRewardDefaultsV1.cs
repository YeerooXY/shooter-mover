using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Domain.Common;

namespace ShooterMover.TerminalDropBinding
{
    internal sealed class AttributedTerminalRewardParticipantResolverV1 :
        ITerminalRewardParticipantResolverV1
    {
        public bool TryResolve(
            TerminalDropSourceFactV1 source,
            TerminalDropRunGenerationContextV1 runContext,
            TerminalRewardPlacementContextV1 placementContext,
            out IReadOnlyList<TerminalRewardParticipantV1> participants,
            out TerminalRewardEligibilityPolicyV1 eligibilityPolicy,
            out string diagnostic)
        {
            if (source == null
                || runContext == null
                || source.AttributedParticipantStableId == null)
            {
                participants = Array.Empty<TerminalRewardParticipantV1>();
                eligibilityPolicy = new TerminalRewardEligibilityPolicyV1(
                    false,
                    false,
                    false);
                diagnostic = "terminal-personal-attributed-participant-missing";
                return false;
            }

            participants = new[]
            {
                new TerminalRewardParticipantV1(
                    source.AttributedParticipantStableId,
                    runContext.ProgressionContext.CharacterLevel,
                    true,
                    true,
                    true,
                    true,
                    false),
            };
            eligibilityPolicy = new TerminalRewardEligibilityPolicyV1(
                false,
                false,
                false);
            diagnostic = string.Empty;
            return true;
        }
    }

    internal sealed class DefaultTerminalRewardEnvironmentResolverV1 :
        ITerminalRewardEnvironmentResolverV1
    {
        private static readonly StableId CampaignModeId =
            StableId.Parse("game-mode.campaign");

        public bool TryResolve(
            TerminalDropSourceFactV1 source,
            TerminalDropRunGenerationContextV1 runContext,
            out TerminalRewardEnvironmentV1 environment,
            out string diagnostic)
        {
            if (source == null || runContext == null)
            {
                environment = null;
                diagnostic = "terminal-personal-default-environment-invalid";
                return false;
            }
            environment = new TerminalRewardEnvironmentV1(
                CampaignModeId,
                runContext.ProgressionContext.ProgressionTags,
                1000,
                1000,
                ProductionRunDropPacingCatalogV1.Resolve(
                    CampaignModeId,
                    null));
            diagnostic = string.Empty;
            return true;
        }
    }

    internal sealed class EmptyTerminalRewardOverrideResolverV1 :
        ITerminalRewardOverrideResolverV1
    {
        public bool TryResolve(
            TerminalDropSourceFactV1 source,
            TerminalDropRunGenerationContextV1 runContext,
            TerminalRewardEnvironmentV1 environment,
            TerminalRewardPlacementContextV1 placementContext,
            out TerminalRewardOverrideSetV1 overrides,
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
            overrides = TerminalRewardOverrideSetV1.Empty();
            diagnostic = string.Empty;
            return true;
        }
    }
}
