using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.LootDropBinding
{
    /// <summary>
    /// Mission-completion authority for the configured minimum strongbox count. It
    /// shares the live participant roster, run environment, pacing state and personal
    /// delivery outbox with terminal drops.
    /// </summary>
    public sealed class TerminalRunMinimumGenerationState
    {
        private static readonly StableId CompletionFactKindId =
            StableId.Parse("terminal-drop-fact.run-completion-minimum");
        private static readonly StableId CompletionDefinitionId =
            StableId.Parse("reward-source.run-completion-minimum");

        private readonly ILootDropRunContextResolver runContexts;
        private readonly ITerminalRewardParticipantResolver participants;
        private readonly ITerminalRewardEnvironmentResolver environments;
        private readonly RewardProfileResolver profileResolver;
        private readonly PersonalRewardGenerationActions generation;
        private readonly IPersonalRewardDeliveryOutbox deliveryOutbox;

        public TerminalRunMinimumGenerationState(
            ILootDropRunContextResolver runContexts,
            ITerminalRewardParticipantResolver participants,
            ITerminalRewardEnvironmentResolver environments,
            RewardProfileResolver profileResolver,
            PersonalRewardGenerationActions generation,
            IPersonalRewardDeliveryOutbox deliveryOutbox = null)
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
            this.deliveryOutbox = deliveryOutbox;
        }

        public TerminalPersonalRewardBatch Generate(
            StableId runStableId,
            long runLifecycleGeneration,
            TerminalRewardPlacementContext placementContext)
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

            LootDropRunGenerationContext runContext;
            LootDropRejectionCode rejection;
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

            string generationText = runLifecycleGeneration.ToString(
                CultureInfo.InvariantCulture);
            StableId terminalEventId =
                RewardGenerationFingerprint.DeriveStableId(
                    "runminimumterminal",
                    runStableId.ToString(),
                    generationText,
                    placementContext.RoomStableId.ToString(),
                    placementContext.PlacementStableId.ToString());
            StableId sourceEntityId =
                RewardGenerationFingerprint.DeriveStableId(
                    "runminimumsource",
                    runStableId.ToString(),
                    generationText);
            string sourceContextFingerprint = LootDrop.Hash(
                runContext.Fingerprint
                + "|"
                + placementContext.Fingerprint
                + "|run-minimum");
            LootDropSourceFact source = new LootDropSourceFact(
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
                LootSourceCatalog.ExplicitNoDropId,
                sourceContextFingerprint,
                LootDrop.Hash(
                    CompletionDefinitionId.ToString()),
                LootDrop.Hash(
                    terminalEventId + "|" + sourceContextFingerprint));

            TerminalRewardEnvironment environment;
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

            IReadOnlyList<TerminalRewardParticipant> resolvedParticipants;
            TerminalRewardEligibilityPolicy eligibilityPolicy;
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

            LootSourceProfile emptyProfile =
                LootSourceCatalog.Get(
                    LootSourceCatalog.ExplicitNoDropId);
            RewardProfileResolution resolution = profileResolver.Resolve(
                LootSourceCatalog.ExplicitNoDropId,
                emptyProfile,
                null,
                null,
                null,
                Array.Empty<RewardProfileOverride>(),
                null);
            string completionFingerprint = LootDrop.Hash(
                source.Fingerprint
                + "|"
                + placementContext.Fingerprint
                + "|"
                + runContext.Fingerprint);

            var personalResults = new List<
                PersonalRewardGenerationResult>();
            for (int index = 0; index < resolvedParticipants.Count; index++)
            {
                TerminalRewardParticipant participant =
                    resolvedParticipants[index];
                if (!eligibilityPolicy.IsEligible(participant))
                {
                    continue;
                }
                ulong seed = LootDrop.DeriveSeed(
                    runContext.RootSeed,
                    completionFingerprint
                        + "|"
                        + participant.ParticipantStableId
                        + "|run-minimum");
                var context = new PersonalRewardRollContext(
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
                return new TerminalPersonalRewardBatch(
                    TerminalPersonalRewardBatchStatus.NoEligibleParticipants,
                    source,
                    Array.Empty<GeneratedLootDropResult>(),
                    "run-minimum-no-eligible-participants");
            }

            if (deliveryOutbox != null)
            {
                for (int index = 0; index < personalResults.Count; index++)
                {
                    PersonalRewardDeliveryEnvelope envelope;
                    if (!deliveryOutbox.TryEnqueue(
                            personalResults[index],
                            out envelope,
                            out diagnostic))
                    {
                        return Reject(
                            source,
                            string.IsNullOrWhiteSpace(diagnostic)
                                ? "run-minimum-outbox-rejected"
                                : diagnostic);
                    }
                }
            }

            var results = new List<GeneratedLootDropResult>(
                personalResults.Count);
            bool anyRewards = false;
            for (int index = 0; index < personalResults.Count; index++)
            {
                GeneratedLootDropResult adapted =
                    TerminalPersonalRewardTransportBridge.Adapt(
                        source,
                        personalResults[index]);
                results.Add(adapted);
                anyRewards |= adapted.GeneratedRewards.Count > 0;
            }
            return new TerminalPersonalRewardBatch(
                anyRewards
                    ? TerminalPersonalRewardBatchStatus.Generated
                    : TerminalPersonalRewardBatchStatus.ExplicitNoDrop,
                source,
                results,
                string.Empty);
        }

        private static TerminalPersonalRewardBatch Reject(
            LootDropSourceFact source,
            string diagnostic)
        {
            return new TerminalPersonalRewardBatch(
                TerminalPersonalRewardBatchStatus.Rejected,
                source,
                Array.Empty<GeneratedLootDropResult>(),
                diagnostic);
        }
    }
}
