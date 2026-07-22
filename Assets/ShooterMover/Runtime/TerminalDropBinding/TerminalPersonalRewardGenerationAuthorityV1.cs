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
    public sealed class TerminalPersonalRewardGenerationAuthorityV1
    {
        private readonly TerminalDropFactAdapterRegistryV1 adapters;
        private readonly ITerminalDropRunContextResolverV1 runContexts;
        private readonly ITerminalRewardParticipantResolverV1 participants;
        private readonly ITerminalRewardEnvironmentResolverV1 environments;
        private readonly ITerminalRewardOverrideResolverV1 overrides;
        private readonly RewardProfileResolverV1 profileResolver;
        private readonly PersonalRewardGenerationServiceV1 generation;
        private readonly IPersonalRewardDeliveryOutboxV1 deliveryOutbox;

        public TerminalPersonalRewardGenerationAuthorityV1(
            TerminalDropFactAdapterRegistryV1 adapters,
            ITerminalDropRunContextResolverV1 runContexts,
            ITerminalRewardParticipantResolverV1 participants,
            ITerminalRewardEnvironmentResolverV1 environments,
            ITerminalRewardOverrideResolverV1 overrides,
            RewardProfileResolverV1 profileResolver,
            PersonalRewardGenerationServiceV1 generation,
            IPersonalRewardDeliveryOutboxV1 deliveryOutbox = null)
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

        public TerminalPersonalRewardBatchV1 GenerateForEligibleParticipants(
            object terminalFact,
            TerminalRewardPlacementContextV1 placementContext)
        {
            if (placementContext == null)
            {
                throw new ArgumentNullException(nameof(placementContext));
            }

            TerminalDropAdaptationResultV1 adaptation;
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

            TerminalDropSourceFactV1 source = adaptation.SourceFact;
            if (placementContext.TerminalEventStableId
                    != source.TerminalEventStableId
                || placementContext.PlacementStableId
                    != source.SourcePlacementStableId)
            {
                return Reject(source, "terminal-personal-placement-context-mismatch");
            }

            TerminalDropRunGenerationContextV1 runContext;
            TerminalDropRejectionCodeV1 runRejection;
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
                        ? "terminal-personal-environment-missing"
                        : diagnostic);
            }

            RewardSourceProfileV1 sourceProfile;
            StableId declaredReferenceId;
            if (!TryResolveSourceProfile(
                    source,
                    out declaredReferenceId,
                    out sourceProfile,
                    out diagnostic))
            {
                return Reject(source, diagnostic);
            }

            TerminalRewardOverrideSetV1 overrideSet;
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

            RewardProfileResolutionV1 resolution;
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
                        ? "terminal-personal-participants-missing"
                        : diagnostic);
            }

            var eligible = new List<TerminalRewardParticipantV1>();
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
                return new TerminalPersonalRewardBatchV1(
                    TerminalPersonalRewardBatchStatusV1.NoEligibleParticipants,
                    source,
                    Array.Empty<GeneratedTerminalDropResultV1>(),
                    "terminal-personal-no-eligible-participants");
            }

            var contexts = new List<PersonalRewardRollContextV1>(eligible.Count);
            string terminalFingerprint = TerminalDropCanonicalV1.Hash(
                source.Fingerprint + "|" + placementContext.Fingerprint
                + "|" + runContext.Fingerprint);
            for (int index = 0; index < eligible.Count; index++)
            {
                TerminalRewardParticipantV1 participant = eligible[index];
                ulong participantSeed = TerminalDropCanonicalV1.DeriveSeed(
                    runContext.RootSeed,
                    terminalFingerprint + "|"
                        + participant.ParticipantStableId + "|"
                        + resolution.Fingerprint);
                contexts.Add(new PersonalRewardRollContextV1(
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

            IReadOnlyList<PersonalRewardGenerationResultV1> personalResults;
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
                    PersonalRewardDeliveryEnvelopeV1 envelope;
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
                anyRewards |= adapted.GeneratedRewards.Count > 0;
            }
            return new TerminalPersonalRewardBatchV1(
                anyRewards
                    ? TerminalPersonalRewardBatchStatusV1.Generated
                    : TerminalPersonalRewardBatchStatusV1.ExplicitNoDrop,
                source,
                results,
                string.Empty);
        }

        private static bool TryResolveSourceProfile(
            TerminalDropSourceFactV1 source,
            out StableId declaredReferenceId,
            out RewardSourceProfileV1 sourceProfile,
            out string diagnostic)
        {
            declaredReferenceId = source.DeclaredDropProfileStableId
                ?? ProductionRewardSourceCatalogV1.ExplicitNoDropId;
            if (ProductionRewardSourceCatalogV1.TryResolve(
                    declaredReferenceId,
                    out sourceProfile))
            {
                diagnostic = string.Empty;
                return true;
            }

            StableId migrated;
            if (ProductionRewardSourceCatalogV1.TryMigrateLegacyProfileId(
                    declaredReferenceId,
                    out migrated)
                && ProductionRewardSourceCatalogV1.TryResolve(
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
