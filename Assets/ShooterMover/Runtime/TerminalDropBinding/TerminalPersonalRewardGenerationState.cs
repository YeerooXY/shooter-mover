using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.TerminalDropBinding
{
    /// <summary>
    /// Authoritative engine-neutral cutover from one shared terminal event to one
    /// independent deterministic personal reward batch per eligible participant.
    /// Generation is recorded in the run outbox before transport projection, so remote
    /// participant results cannot be discarded by a local pickup consumer.
    /// </summary>
    public sealed class TerminalPersonalRewardGenerationState
    {
        private readonly TerminalDropFactBridgeRegistry adapters;
        private readonly ITerminalDropRunContextResolver runContexts;
        private readonly ITerminalRewardParticipantResolver participants;
        private readonly ITerminalRewardEnvironmentResolver environments;
        private readonly ITerminalRewardOverrideResolver overrides;
        private readonly RewardProfileResolver profileResolver;
        private readonly PersonalRewardGenerationActions generation;
        private readonly IPersonalRewardDeliveryOutbox deliveryOutbox;

        public TerminalPersonalRewardGenerationState(
            TerminalDropFactBridgeRegistry adapters,
            ITerminalDropRunContextResolver runContexts,
            ITerminalRewardParticipantResolver participants,
            ITerminalRewardEnvironmentResolver environments,
            ITerminalRewardOverrideResolver overrides,
            RewardProfileResolver profileResolver,
            PersonalRewardGenerationActions generation,
            IPersonalRewardDeliveryOutbox deliveryOutbox = null)
        {
            this.adapters = adapters
                ?? throw new ArgumentNullException(nameof(adapters));
            this.runContexts = runContexts
                ?? throw new ArgumentNullException(nameof(runContexts));
            this.participants = participants
                ?? throw new ArgumentNullException(nameof(participants));
            this.environments = environments
                ?? throw new ArgumentNullException(nameof(environments));
            this.overrides = overrides
                ?? throw new ArgumentNullException(nameof(overrides));
            this.profileResolver = profileResolver
                ?? throw new ArgumentNullException(nameof(profileResolver));
            this.generation = generation
                ?? throw new ArgumentNullException(nameof(generation));
            this.deliveryOutbox = deliveryOutbox;
        }

        public TerminalPersonalRewardBatch GenerateForEligibleParticipants(
            object terminalFact,
            TerminalRewardPlacementContext placementContext)
        {
            if (placementContext == null)
            {
                throw new ArgumentNullException(nameof(placementContext));
            }

            TerminalDropAdaptationResult adaptation;
            try
            {
                adaptation = adapters.Adapt(terminalFact);
            }
            catch (Exception exception)
            {
                return Reject(null, "terminal-personal-adaptation-exception:"
                    + exception.GetType().Name + ":" + exception.Message);
            }
            if (adaptation == null || !adaptation.Succeeded)
            {
                return Reject(
                    null,
                    adaptation == null
                        ? "terminal-personal-adaptation-null"
                        : adaptation.Diagnostic);
            }

            TerminalDropSourceFact source = adaptation.SourceFact;
            if (placementContext.TerminalEventStableId
                    != source.TerminalEventStableId
                || placementContext.PlacementStableId
                    != source.SourcePlacementStableId)
            {
                return Reject(source, "terminal-personal-placement-context-mismatch");
            }

            TerminalDropRunGenerationContext runContext;
            TerminalDropRejectionCode runRejection;
            string diagnostic;
            if (!runContexts.TryResolve(
                    source.RunStableId,
                    source.RunLifecycleGeneration,
                    out runContext,
                    out runRejection,
                    out diagnostic)
                || runContext == null)
            {
                return Reject(
                    source,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "terminal-personal-run-context-missing"
                        : diagnostic);
            }

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
                        ? "terminal-personal-environment-missing"
                        : diagnostic);
            }

            RewardSourceProfile sourceProfile;
            StableId declaredReferenceId;
            if (!TryResolveSourceProfile(
                    source,
                    out declaredReferenceId,
                    out sourceProfile,
                    out diagnostic))
            {
                return Reject(source, diagnostic);
            }

            TerminalRewardOverrideSet overrideSet;
            if (!overrides.TryResolve(
                    source,
                    runContext,
                    environment,
                    placementContext,
                    out overrideSet,
                    out diagnostic)
                || overrideSet == null)
            {
                return Reject(
                    source,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "terminal-personal-overrides-missing"
                        : diagnostic);
            }

            RewardProfileResolution resolution;
            try
            {
                resolution = profileResolver.Resolve(
                    declaredReferenceId,
                    sourceProfile,
                    overrideSet.GameModeOverride,
                    overrideSet.MissionOverride,
                    overrideSet.DifficultyOverride,
                    overrideSet.EventOverrides,
                    overrideSet.PlacementOverride);
            }
            catch (Exception exception)
            {
                return Reject(
                    source,
                    "terminal-personal-profile-resolution-exception:"
                        + exception.GetType().Name + ":" + exception.Message);
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
                        ? "terminal-personal-participants-missing"
                        : diagnostic);
            }

            var eligible = new List<TerminalRewardParticipant>();
            for (int index = 0; index < resolvedParticipants.Count; index++)
            {
                if (eligibilityPolicy.IsEligible(resolvedParticipants[index]))
                {
                    eligible.Add(resolvedParticipants[index]);
                }
            }
            eligible.Sort();
            if (eligible.Count == 0)
            {
                return new TerminalPersonalRewardBatch(
                    TerminalPersonalRewardBatchStatus.NoEligibleParticipants,
                    source,
                    Array.Empty<GeneratedTerminalDropResult>(),
                    "terminal-personal-no-eligible-participants");
            }

            var contexts = new List<PersonalRewardRollContext>(eligible.Count);
            string terminalFingerprint = TerminalDrop.Hash(
                source.Fingerprint + "|" + placementContext.Fingerprint
                + "|" + runContext.Fingerprint);
            for (int index = 0; index < eligible.Count; index++)
            {
                TerminalRewardParticipant participant = eligible[index];
                ulong participantSeed = TerminalDrop.DeriveSeed(
                    runContext.RootSeed,
                    terminalFingerprint + "|"
                        + participant.ParticipantStableId + "|"
                        + resolution.Fingerprint);
                contexts.Add(new PersonalRewardRollContext(
                    source.RunStableId,
                    checked((int)source.RunLifecycleGeneration),
                    source.TerminalEventStableId,
                    checked((int)source.SourceLifecycleGeneration),
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
                    terminalFingerprint,
                    participantSeed,
                    runContext.GenerationAlgorithmVersion));
            }

            IReadOnlyList<PersonalRewardGenerationResult> personalResults;
            try
            {
                personalResults = generation.GenerateForParticipants(contexts);
            }
            catch (Exception exception)
            {
                return Reject(source, "terminal-personal-generation-exception:"
                    + exception.GetType().Name + ":" + exception.Message);
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
                                ? "terminal-personal-outbox-rejected"
                                : diagnostic);
                    }
                }
            }

            var results = new List<GeneratedTerminalDropResult>(
                personalResults.Count);
            bool anyRewards = false;
            for (int index = 0; index < personalResults.Count; index++)
            {
                GeneratedTerminalDropResult adapted =
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

        private static bool TryResolveSourceProfile(
            TerminalDropSourceFact source,
            out StableId declaredReferenceId,
            out RewardSourceProfile sourceProfile,
            out string diagnostic)
        {
            declaredReferenceId = source.DeclaredDropProfileStableId
                ?? RewardSourceCatalog.ExplicitNoDropId;
            if (RewardSourceCatalog.TryResolve(
                    declaredReferenceId,
                    out sourceProfile))
            {
                diagnostic = string.Empty;
                return true;
            }

            StableId migrated;
            if (RewardSourceCatalog.TryMigrateLegacyProfileId(
                    declaredReferenceId,
                    out migrated)
                && RewardSourceCatalog.TryResolve(
                    migrated,
                    out sourceProfile))
            {
                declaredReferenceId = migrated;
                diagnostic = string.Empty;
                return true;
            }

            sourceProfile = null;
            diagnostic = "terminal-personal-profile-missing:"
                + declaredReferenceId;
            return false;
        }

        private static TerminalPersonalRewardBatch Reject(
            TerminalDropSourceFact source,
            string diagnostic)
        {
            return new TerminalPersonalRewardBatch(
                TerminalPersonalRewardBatchStatus.Rejected,
                source,
                Array.Empty<GeneratedTerminalDropResult>(),
                diagnostic);
        }
    }
}
