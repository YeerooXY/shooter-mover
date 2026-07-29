using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.LootDropBinding
{
    /// <summary>
    /// Compatibility-shaped facade over the production personal reward authority.
    /// Live consumers must use GenerateBatch so every eligible participant result is
    /// preserved. Generate remains for single-participant legacy callers and rejects
    /// rather than silently discarding a multiplayer batch.
    /// </summary>
    public sealed class LootDropGenerationState
    {
        private readonly LootDropFactBridgeRegistry adapters;
        private readonly TerminalPersonalRewardGenerationState personal;
        private readonly HashSet<StableId> acceptedOperations =
            new HashSet<StableId>();

        public LootDropGenerationState(
            LootDropFactBridgeRegistry adapters,
            TerminalPersonalRewardGenerationState personal)
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
        public LootDropGenerationState(
            LootDropFactBridgeRegistry adapters,
            ILootDropRunContextResolver runContexts,
            IRewardProfileResolver legacyProfiles,
            IRewardGenerationExecutor legacyGenerator)
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
        public LootDropGenerationState(
            LootDropFactBridgeRegistry adapters,
            ILootDropRunContextResolver runContexts,
            IRewardProfileResolver legacyProfiles,
            IRewardGenerationExecutor legacyGenerator,
            PersonalRewardGenerationActions personalGenerationService)
        {
            this.adapters = adapters
                ?? throw new ArgumentNullException(nameof(adapters));
            if (runContexts == null)
            {
                throw new ArgumentNullException(nameof(runContexts));
            }
            _ = legacyProfiles;
            _ = legacyGenerator;

            PersonalRewardGenerationActions generation =
                personalGenerationService
                ?? new PersonalRewardGenerationActions(
                    new ParticipantDropPacing());
            personal = new TerminalPersonalRewardGenerationState(
                adapters,
                runContexts,
                new AttributedTerminalRewardParticipantResolver(),
                new DefaultTerminalRewardEnvironmentResolver(),
                new EmptyTerminalRewardOverrideResolver(),
                new RewardProfileResolver(),
                generation);
        }

        public int AcceptedBatchCount
        {
            get { return acceptedOperations.Count; }
        }

        public TerminalPersonalRewardBatch GenerateBatch(object terminalFact)
        {
            LootDropAdaptationResult adaptation;
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

            TerminalRewardPlacementContext placement;
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

            TerminalPersonalRewardBatch batch =
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
                    GeneratedLootDropResult result = batch.Results[index];
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

        public GeneratedLootDropResult Generate(object terminalFact)
        {
            TerminalPersonalRewardBatch batch = GenerateBatch(terminalFact);
            if (batch == null || !batch.IsAccepted || batch.Results.Count == 0)
            {
                return GeneratedLootDropResult.Rejected(
                    LootDropRejectionCode.GenerationFailed,
                    batch == null ? null : batch.Source,
                    batch == null
                        ? "terminal-personal-facade-batch-null"
                        : batch.Diagnostic);
            }
            if (batch.Results.Count != 1)
            {
                return GeneratedLootDropResult.Rejected(
                    LootDropRejectionCode.InvalidGeneratedBatch,
                    batch.Source,
                    "terminal-personal-facade-requires-batch-consumer");
            }
            return batch.Results[0];
        }

        private static TerminalPersonalRewardBatch RejectedBatch(
            LootDropSourceFact source,
            string diagnostic)
        {
            return new TerminalPersonalRewardBatch(
                TerminalPersonalRewardBatchStatus.Rejected,
                source,
                Array.Empty<GeneratedLootDropResult>(),
                diagnostic);
        }

        private static bool TryResolvePlacement(
            object terminalFact,
            LootDropSourceFact source,
            out TerminalRewardPlacementContext placement,
            out string diagnostic)
        {
            var explicitPlacement = terminalFact
                as ITerminalRewardPlacementFact;
            if (explicitPlacement != null)
            {
                placement = new TerminalRewardPlacementContext(
                    explicitPlacement.RewardTerminalEventStableId,
                    explicitPlacement.RewardRoomStableId,
                    explicitPlacement.RewardRoomLifecycleGeneration,
                    explicitPlacement.RewardPlacementStableId,
                    explicitPlacement.RewardPlacementFingerprint);
                diagnostic = string.Empty;
                return true;
            }

            var enemyDeath = terminalFact as EnemyDeathFact;
            if (enemyDeath != null
                && enemyDeath.Identity != null
                && enemyDeath.Identity.RoomStableId != null
                && enemyDeath.Identity.PlacementStableId != null)
            {
                placement = new TerminalRewardPlacementContext(
                    source.TerminalEventStableId,
                    enemyDeath.Identity.RoomStableId,
                    checked((int)Math.Max(1L, source.RunLifecycleGeneration)),
                    enemyDeath.Identity.PlacementStableId,
                    LootDrop.Hash(
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
                placement = new TerminalRewardPlacementContext(
                    source.TerminalEventStableId,
                    fallbackRoom,
                    checked((int)Math.Max(1L, source.RunLifecycleGeneration)),
                    source.SourcePlacementStableId,
                    LootDrop.Hash(
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
