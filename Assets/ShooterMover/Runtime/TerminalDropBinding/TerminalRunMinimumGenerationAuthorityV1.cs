using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.TerminalDropBinding
{
    /// <summary>
    /// Mission-completion authority for the configured minimum strongbox count. It
    /// shares the live participant roster, run environment and pacing service with
    /// terminal drops, but creates a distinct immutable completion source rather than
    /// pretending that an enemy or prop dropped the fallback box.
    /// </summary>
    public sealed class TerminalRunMinimumGenerationAuthorityV1
    {
        private static readonly StableId CompletionFactKindId =
            StableId.Parse("terminal-drop-fact.run-completion-minimum");
        private static readonly StableId CompletionDefinitionId =
            StableId.Parse("reward-source.run-completion-minimum");

        private readonly ITerminalDropRunContextResolverV1 runContexts;
        private readonly ITerminalRewardParticipantResolverV1 participants;
        private readonly ITerminalRewardEnvironmentResolverV1 environments;
        private readonly RewardProfileResolverV1 profileResolver;
        private readonly PersonalRewardGenerationServiceV1 generation;

        public TerminalRunMinimumGenerationAuthorityV1(
            ITerminalDropRunContextResolverV1 runContexts,
            ITerminalRewardParticipantResolverV1 participants,
            ITerminalRewardEnvironmentResolverV1 environments,
            RewardProfileResolverV1 profileResolver,
            PersonalRewardGenerationServiceV1 generation)
        {
            this.runContexts = runContexts
                ?? throw new ArgumentNullException(nameof(runContexts));
            this.participants = participants
                ?? throw new ArgumentNullException(nameof(participants));
            this.environments = environments
                ?? throw new ArgumentNullException(nameof(environments));
            this.profileResolver = profileResolver
                ?? throw new ArgumentNullException(nameof(profileResolver));
            this.generation = generation
                ?? throw new ArgumentNullException(nameof(generation));
        }

        public TerminalPersonalRewardBatchV1 Generate(
            StableId runStableId,
            long runLifecycleGeneration,
            TerminalRewardPlacementContextV1 placementContext)
        {
            if (runStableId == null)
            {
                throw new ArgumentNullException(nameof(runStableId));
            }
            if (runLifecycleGeneration < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runLifecycleGeneration));
            }
            if (placementContext == null)
            {
                throw new ArgumentNullException(nameof(placementContext));
            }

            TerminalDropRunGenerationContextV1 runContext;
            TerminalDropRejectionCodeV1 rejection;
            string diagnostic;
            if (!runContexts.TryResolve(
                    runStableId,
                    runLifecycleGeneration,
                    out runContext,
                    out rejection,
                    out diagnostic)
                || runContext == null)
            {
                return Reject(
                    null,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "run-minimum-run-context-missing"
                        : diagnostic);
            }

            StableId terminalEventId =
                RewardGenerationFingerprintV1.DeriveStableId(
                    "runminimumterminal",
                    runStableId.ToString(),
                    runLifecycleGeneration.ToString(),
                    placementContext.RoomStableId.ToString(),
                    placementContext.PlacementStableId.ToString());
            StableId sourceEntityId =
                RewardGenerationFingerprintV1.DeriveStableId(
                    "runminimumsource",
                    runStableId.ToString(),
                    runLifecycleGeneration.ToString());
            string sourceContextFingerprint = TerminalDropCanonicalV1.Hash(
                runContext.Fingerprint
                + "|"
                + placementContext.Fingerprint
                + "|run-minimum");
            TerminalDropSourceFactV1 source = new TerminalDropSourceFactV1(
                CompletionFactKindId,
                terminalEventId,
                null,
                runStableId,
                runLifecycleGeneration,
                sourceEntityId,
                placementContext.PlacementStableId,
                1L,
                CompletionDefinitionId,
                null,
                null,
                null,
                ProductionRewardSourceCatalogV1.ExplicitNoDropId,
                sourceContextFingerprint,
                TerminalDropCanonicalV1.Hash(
                    CompletionDefinitionId.ToString()),
                TerminalDropCanonicalV1.Hash(
                    terminalEventId + "|" + sourceContextFingerprint));

            TerminalRewardEnvironmentV1 environment;
            if (!environments.TryResolve(
                    source,
                    runContext,
                    out environment,
                    out diagnostic)
                || environment == null)
            {
                return Reject(
                    source,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "run-minimum-environment-missing"
                        : diagnostic);
            }

            IReadOnlyList<TerminalRewardParticipantV1> resolvedParticipants;
            TerminalRewardEligibilityPolicyV1 eligibilityPolicy;
            if (!participants.TryResolve(
                    source,
                    runContext,
                    placementContext,
                    out resolvedParticipants,
                    out eligibilityPolicy,
                    out diagnostic)
                || resolvedParticipants == null
                || eligibilityPolicy == null)
            {
                return Reject(
                    source,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "run-minimum-participants-missing"
                        : diagnostic);
            }

            RewardSourceProfileV1 emptyProfile =
                ProductionRewardSourceCatalogV1.Get(
                    ProductionRewardSourceCatalogV1.ExplicitNoDropId);
            RewardProfileResolutionV1 resolution = profileResolver.Resolve(
                ProductionRewardSourceCatalogV1.ExplicitNoDropId,
                emptyProfile,
                null,
                null,
                null,
                Array.Empty<RewardProfileOverrideV1>(),
                null);
            string completionFingerprint = TerminalDropCanonicalV1.Hash(
                source.Fingerprint
                + "|"
                + placementContext.Fingerprint
                + "|"
                + runContext.Fingerprint);

            var personalResults = new List<
                PersonalRewardGenerationResultV1>();
            for (int index = 0; index < resolvedParticipants.Count; index++)
            {
                TerminalRewardParticipantV1 participant =
                    resolvedParticipants[index];
                if (!eligibilityPolicy.IsEligible(participant))
                {
                    continue;
                }
                ulong seed = TerminalDropCanonicalV1.DeriveSeed(
                    runContext.RootSeed,
                    completionFingerprint
                        + "|"
                        + participant.ParticipantStableId
                        + "|run-minimum");
                var context = new PersonalRewardRollContextV1(
                    runStableId,
                    checked((int)runLifecycleGeneration),
                    terminalEventId,
                    1,
                    placementContext.RoomStableId,
                    placementContext.RoomLifecycleGeneration,
                    placementContext.PlacementStableId,
                    participant.ParticipantStableId,
                    true,
                    participant.PlayerLevel,
                    runContext.ProgressionContext.RegionLevel,
                    runContext.ProgressionContext.DifficultyId,
                    environment.GameModeStableId,
                    environment.EventModifierIds,
                    environment.MoneyQuantityMultiplierPermille,
                    environment.ScrapQuantityMultiplierPermille,
                    resolution,
                    environment.PacingPolicy,
                    completionFingerprint,
                    seed,
                    runContext.GenerationAlgorithmVersion);
                personalResults.Add(generation.GenerateRunMinimum(context));
            }
            if (personalResults.Count == 0)
            {
                return new TerminalPersonalRewardBatchV1(
                    TerminalPersonalRewardBatchStatusV1.NoEligibleParticipants,
                    source,
                    Array.Empty<GeneratedTerminalDropResultV1>(),
                    "run-minimum-no-eligible-participants");
            }

            var results = new List<GeneratedTerminalDropResultV1>(
                personalResults.Count);
            bool anyRewards = false;
            for (int index = 0; index < personalResults.Count; index++)
            {
                GeneratedTerminalDropResultV1 adapted =
                    TerminalPersonalRewardTransportAdapterV1.Adapt(
                        source,
                        personalResults[index]);
                results.Add(adapted);
                anyRewards |= adapted.Rewards.Count > 0;
            }
            return new TerminalPersonalRewardBatchV1(
                anyRewards
                    ? TerminalPersonalRewardBatchStatusV1.Generated
                    : TerminalPersonalRewardBatchStatusV1.ExplicitNoDrop,
                source,
                results,
                string.Empty);
        }

        private static TerminalPersonalRewardBatchV1 Reject(
            TerminalDropSourceFactV1 source,
            string diagnostic)
        {
            return new TerminalPersonalRewardBatchV1(
                TerminalPersonalRewardBatchStatusV1.Rejected,
                source,
                Array.Empty<GeneratedTerminalDropResultV1>(),
                diagnostic);
        }
    }
}
