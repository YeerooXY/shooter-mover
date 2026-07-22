using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.TerminalDropBinding
{
    /// <summary>
    /// Backward-shaped single-participant entry point over the production personal
    /// reward authority. The legacy profile resolver and REW-001 executor parameters
    /// are retained only so existing composition call sites can migrate atomically;
    /// they are never used to select, roll, pace or construct rewards.
    /// </summary>
    public sealed class TerminalDropGenerationAuthorityV1
    {
        private readonly TerminalDropFactAdapterRegistryV1 adapters;
        private readonly TerminalPersonalRewardGenerationAuthorityV1 personal;
        private readonly HashSet<StableId> acceptedOperations =
            new HashSet<StableId>();

        public TerminalDropGenerationAuthorityV1(
            TerminalDropFactAdapterRegistryV1 adapters,
            ITerminalDropRunContextResolverV1 runContexts,
            IRewardProfileResolverV1 legacyProfiles,
            IRewardGenerationExecutorV1 legacyGenerator)
            : this(
                adapters,
                runContexts,
                legacyProfiles,
                legacyGenerator,
                null)
        {
        }

        public TerminalDropGenerationAuthorityV1(
            TerminalDropFactAdapterRegistryV1 adapters,
            ITerminalDropRunContextResolverV1 runContexts,
            IRewardProfileResolverV1 legacyProfiles,
            IRewardGenerationExecutorV1 legacyGenerator,
            PersonalRewardGenerationServiceV1 personalGenerationService)
        {
            this.adapters = adapters
                ?? throw new ArgumentNullException(nameof(adapters));
            if (runContexts == null)
            {
                throw new ArgumentNullException(nameof(runContexts));
            }

            // Deliberately ignored. Keeping the constructor shape avoids a half-migrated
            // Stage 1 composition while ensuring there is only one live reward authority.
            _ = legacyProfiles;
            _ = legacyGenerator;

            PersonalRewardGenerationServiceV1 generation =
                personalGenerationService
                ?? new PersonalRewardGenerationServiceV1(
                    new ParticipantDropPacingAuthorityV1());
            personal = new TerminalPersonalRewardGenerationAuthorityV1(
                adapters,
                runContexts,
                new AttributedTerminalRewardParticipantResolverV1(),
                new DefaultTerminalRewardEnvironmentResolverV1(),
                new EmptyTerminalRewardOverrideResolverV1(),
                new RewardProfileResolverV1(),
                generation);
        }

        public int AcceptedBatchCount
        {
            get { return acceptedOperations.Count; }
        }

        public GeneratedTerminalDropResultV1 Generate(object terminalFact)
        {
            TerminalDropAdaptationResultV1 adaptation;
            try
            {
                adaptation = adapters.Adapt(terminalFact);
            }
            catch (Exception exception)
            {
                return GeneratedTerminalDropResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    null,
                    "terminal-personal-facade-adaptation-exception:"
                        + exception.GetType().Name
                        + ":"
                        + exception.Message);
            }
            if (adaptation == null || !adaptation.Succeeded)
            {
                return GeneratedTerminalDropResultV1.Rejected(
                    adaptation == null
                        ? TerminalDropRejectionCodeV1.InvalidTerminalFact
                        : adaptation.RejectionCode,
                    adaptation == null ? null : adaptation.SourceFact,
                    adaptation == null
                        ? "terminal-personal-facade-adaptation-null"
                        : adaptation.Diagnostic);
            }

            TerminalRewardPlacementContextV1 placement;
            string placementDiagnostic;
            if (!TryResolvePlacement(
                    terminalFact,
                    adaptation.SourceFact,
                    out placement,
                    out placementDiagnostic))
            {
                return GeneratedTerminalDropResultV1.Rejected(
                    TerminalDropRejectionCodeV1.MissingSourceContext,
                    adaptation.SourceFact,
                    placementDiagnostic);
            }

            TerminalPersonalRewardBatchV1 batch =
                personal.GenerateForEligibleParticipants(
                    terminalFact,
                    placement);
            if (batch == null || !batch.IsAccepted || batch.Results.Count == 0)
            {
                return GeneratedTerminalDropResultV1.Rejected(
                    TerminalDropRejectionCodeV1.GenerationFailed,
                    adaptation.SourceFact,
                    batch == null
                        ? "terminal-personal-facade-batch-null"
                        : batch.Diagnostic);
            }

            GeneratedTerminalDropResultV1 result = batch.Results[0];
            if (result.IsAccepted && result.Operation != null)
            {
                acceptedOperations.Add(result.Operation.SourceOperationStableId);
            }
            return result;
        }

        private static bool TryResolvePlacement(
            object terminalFact,
            TerminalDropSourceFactV1 source,
            out TerminalRewardPlacementContextV1 placement,
            out string diagnostic)
        {
            var explicitPlacement = terminalFact
                as ITerminalRewardPlacementFactV1;
            if (explicitPlacement != null)
            {
                placement = new TerminalRewardPlacementContextV1(
                    explicitPlacement.RewardTerminalEventStableId,
                    explicitPlacement.RewardRoomStableId,
                    explicitPlacement.RewardRoomLifecycleGeneration,
                    explicitPlacement.RewardPlacementStableId,
                    explicitPlacement.RewardPlacementFingerprint);
                diagnostic = string.Empty;
                return true;
            }

            var enemyDeath = terminalFact as EnemyDeathFactV1;
            if (enemyDeath != null
                && enemyDeath.Identity != null
                && enemyDeath.Identity.RoomStableId != null
                && enemyDeath.Identity.PlacementStableId != null)
            {
                placement = new TerminalRewardPlacementContextV1(
                    source.TerminalEventStableId,
                    enemyDeath.Identity.RoomStableId,
                    checked((int)Math.Max(1L, source.RunLifecycleGeneration)),
                    enemyDeath.Identity.PlacementStableId,
                    TerminalDropCanonicalV1.Hash(
                        source.Fingerprint
                        + "|"
                        + enemyDeath.Identity.RoomStableId
                        + "|"
                        + enemyDeath.Identity.PlacementStableId));
                diagnostic = string.Empty;
                return true;
            }

            if (source.SourcePlacementStableId != null)
            {
                StableId fallbackRoom = StableId.Create(
                    "terminal-room-placement",
                    source.SourcePlacementStableId.ToString());
                placement = new TerminalRewardPlacementContextV1(
                    source.TerminalEventStableId,
                    fallbackRoom,
                    checked((int)Math.Max(1L, source.RunLifecycleGeneration)),
                    source.SourcePlacementStableId,
                    TerminalDropCanonicalV1.Hash(
                        source.Fingerprint
                        + "|"
                        + fallbackRoom
                        + "|"
                        + source.SourcePlacementStableId));
                diagnostic = string.Empty;
                return true;
            }

            placement = null;
            diagnostic = "terminal-personal-facade-placement-missing";
            return false;
        }
    }
}
