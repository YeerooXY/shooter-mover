using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.TerminalDropBinding
{
    /// <summary>
    /// Compatibility-shaped facade over the production personal reward authority.
    /// Live consumers must use GenerateBatch so every eligible participant result is
    /// preserved. Generate remains for single-participant legacy callers and rejects
    /// rather than silently discarding a multiplayer batch.
    /// </summary>
    public sealed class TerminalDropGenerationAuthorityV1
    {
        private readonly TerminalDropFactAdapterRegistryV1 adapters;
        private readonly TerminalPersonalRewardGenerationAuthorityV1 personal;
        private readonly HashSet<StableId> acceptedOperations =
            new HashSet<StableId>();

        public TerminalDropGenerationAuthorityV1(
            TerminalDropFactAdapterRegistryV1 adapters,
            TerminalPersonalRewardGenerationAuthorityV1 personal)
        {
            this.adapters = adapters
                ?? throw new ArgumentNullException(nameof(adapters));
            this.personal = personal
                ?? throw new ArgumentNullException(nameof(personal));
        }

        /// <summary>
        /// Retained constructor for older single-player fixtures. Production composition
        /// injects run-backed participant, environment and override resolvers instead.
        /// </summary>
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

        /// <summary>
        /// Retained constructor for compatibility tests. The legacy DROP/GEN arguments
        /// do not execute reward logic.
        /// </summary>
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

        public TerminalPersonalRewardBatchV1 GenerateBatch(object terminalFact)
        {
            TerminalDropAdaptationResultV1 adaptation;
            try
            {
                adaptation = adapters.Adapt(terminalFact);
            }
            catch (Exception exception)
            {
                return RejectedBatch(
                    null,
                    "terminal-personal-facade-adaptation-exception:"
                        + exception.GetType().Name
                        + ":"
                        + exception.Message);
            }
            if (adaptation == null || !adaptation.Succeeded)
            {
                return RejectedBatch(
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
                return RejectedBatch(
                    adaptation.SourceFact,
                    placementDiagnostic);
            }

            TerminalPersonalRewardBatchV1 batch =
                personal.GenerateForEligibleParticipants(
                    terminalFact,
                    placement);
            if (batch == null)
            {
                return RejectedBatch(
                    adaptation.SourceFact,
                    "terminal-personal-facade-batch-null");
            }
            if (batch.IsAccepted)
            {
                for (int index = 0; index < batch.Results.Count; index++)
                {
                    GeneratedTerminalDropResultV1 result = batch.Results[index];
                    if (result != null
                        && result.IsAccepted
                        && result.OperationRequest != null)
                    {
                        acceptedOperations.Add(
                            result.OperationRequest.SourceOperationStableId);
                    }
                }
            }
            return batch;
        }

        public GeneratedTerminalDropResultV1 Generate(object terminalFact)
        {
            TerminalPersonalRewardBatchV1 batch = GenerateBatch(terminalFact);
            if (batch == null || !batch.IsAccepted || batch.Results.Count == 0)
            {
                return GeneratedTerminalDropResultV1.Rejected(
                    TerminalDropRejectionCodeV1.GenerationFailed,
                    batch == null ? null : batch.Source,
                    batch == null
                        ? "terminal-personal-facade-batch-null"
                        : batch.Diagnostic);
            }
            if (batch.Results.Count != 1)
            {
                return GeneratedTerminalDropResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidGeneratedBatch,
                    batch.Source,
                    "terminal-personal-facade-requires-batch-consumer");
            }
            return batch.Results[0];
        }

        private static TerminalPersonalRewardBatchV1 RejectedBatch(
            TerminalDropSourceFactV1 source,
            string diagnostic)
        {
            return new TerminalPersonalRewardBatchV1(
                TerminalPersonalRewardBatchStatusV1.Rejected,
                source,
                Array.Empty<GeneratedTerminalDropResultV1>(),
                diagnostic);
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
