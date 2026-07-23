using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.RunPickups;
using ShooterMover.TerminalDropBinding;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    public sealed partial class Stage1RunPickupBootstrap2D
    {
        private static readonly StableId Stage1GameModeStableId =
            StableId.Parse("game-mode.campaign");

        private Stage1PersonalRewardBatchDeliveryV1 batchDelivery;
        private long completedMinimumLifecycle = -1L;

        private void ConfigureTerminalRewardComposition(
            EnemyCatalogV1 enemyCatalog,
            PropCatalogV1 propCatalog,
            IRewardProfileResolverV1 legacyRewardProfiles)
        {
            run.ConfigureRewardEnvironment(
                new RunRewardEnvironmentSnapshotV1(
                    Stage1GameModeStableId,
                    Array.Empty<StableId>(),
                    1000,
                    1000,
                    ProductionRunDropPacingCatalogV1.Resolve(
                        Stage1GameModeStableId,
                        null)));

            ConfigureDurableEquipmentPayloadRetention();

            var pacing = new ParticipantDropPacingAuthorityV1(
                new RunSessionParticipantDropPacingStateStoreV1(run));
            var generation = new PersonalRewardGenerationServiceV1(pacing);
            var participants =
                new RunSessionTerminalRewardParticipantResolverV1(
                    () => run,
                    new TerminalRewardEligibilityPolicyV1(
                        false,
                        false,
                        false));
            var environment =
                new RunSessionTerminalRewardEnvironmentResolverV1(
                    () => run);
            var overrides =
                new RunSessionTerminalRewardOverrideResolverV1(
                    () => run);
            var deliveryOutbox =
                new RunSessionPersonalRewardDeliveryOutboxV1(run);

            terminalDrops = TerminalDropBindingCompositionV1.Create(
                enemyCatalog,
                new Stage1EnemyTerminalSourceContextResolverV1(() => run),
                propCatalog,
                new Stage1MissingPropTerminalSourceContextResolverV1(),
                dropRunContext,
                legacyRewardProfiles,
                null,
                new PendingTerminalDropAdmissionAuthorityV1(),
                new ITerminalDropFactAdapterV1[]
                {
                    new Stage1CanonicalPropTerminalDropFactAdapterV1(),
                },
                null,
                generation,
                participants,
                environment,
                overrides,
                deliveryOutbox);
            RunPlayerRuntimeSnapshotV1 player =
                run.RuntimePorts.Player.ExportSnapshot();
            batchDelivery = new Stage1PersonalRewardBatchDeliveryV1(
                terminalDrops,
                admissionBridge,
                player.ParticipantStableId,
                deliveryOutbox);
            completedMinimumLifecycle = -1L;
        }

        internal Stage1PersonalRewardBatchDeliveryResultV1
            DeliverPersonalRewardBatch(
                TerminalPersonalRewardBatchV1 batch,
                StableId roomStableId,
                Vector2 position,
                string positionFingerprint)
        {
            if (run == null || batchDelivery == null)
            {
                return new Stage1PersonalRewardBatchDeliveryResultV1(
                    false,
                    false,
                    null,
                    string.Empty,
                    "stage1-personal-reward-delivery-unavailable");
            }
            return batchDelivery.Deliver(
                batch,
                run.RunStableId,
                run.LifecycleGeneration,
                roomStableId,
                position,
                positionFingerprint);
        }

        /// <summary>
        /// Called synchronously before the existing final-exit handler. The guaranteed
        /// local minimum is realized through the normal pickup authority and immediately
        /// collected through its exact collection command, preventing exit timing from
        /// leaving the promised reward behind. Remote participant minimums remain in the
        /// run outbox for their participant-specific transport.
        /// </summary>
        internal bool TryGenerateRunMinimum(out string resultDiagnostic)
        {
            resultDiagnostic = string.Empty;
            if (run == null
                || terminalDrops == null
                || batchDelivery == null
                || pickups == null
                || stage1 == null
                || stage1.RunPickupRooms == null
                || stage1.RunPickupController == null
                || stage1.RunPickupController.PlayerTransform == null)
            {
                resultDiagnostic = "stage1-run-minimum-authority-unavailable";
                return false;
            }
            if (completedMinimumLifecycle == run.LifecycleGeneration)
            {
                return true;
            }

            StableId roomStableId =
                stage1.RunPickupRooms.CurrentRoomStableId;
            StableId placementStableId = StableId.Create(
                "reward-placement",
                "stage1-run-minimum-g"
                    + run.LifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture));
            StableId terminalEventStableId = StableId.Create(
                "terminal-event",
                "stage1-run-minimum-g"
                    + run.LifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture));
            Vector2 position =
                stage1.RunPickupController.PlayerTransform.position;
            string positionFingerprint =
                RewardGenerationFingerprintV1.Compute(
                    run.RunStableId
                    + "|"
                    + run.LifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture)
                    + "|"
                    + roomStableId
                    + "|"
                    + placementStableId
                    + "|"
                    + position.x.ToString(
                        "R",
                        CultureInfo.InvariantCulture)
                    + "|"
                    + position.y.ToString(
                        "R",
                        CultureInfo.InvariantCulture));
            var placement = new TerminalRewardPlacementContextV1(
                terminalEventStableId,
                roomStableId,
                checked((int)Math.Max(1L, run.LifecycleGeneration)),
                placementStableId,
                positionFingerprint);

            TerminalPersonalRewardBatchV1 batch;
            try
            {
                batch = terminalDrops.RunMinimumAuthority.Generate(
                    run.RunStableId,
                    run.LifecycleGeneration,
                    placement);
            }
            catch (Exception exception)
            {
                resultDiagnostic = "stage1-run-minimum-generation-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
                return false;
            }
            Stage1PersonalRewardBatchDeliveryResultV1 delivery =
                DeliverPersonalRewardBatch(
                    batch,
                    roomStableId,
                    position,
                    positionFingerprint);
            if (!delivery.Succeeded)
            {
                resultDiagnostic = delivery.Diagnostic;
                return false;
            }
            if (!TryCollectLocalRunMinimum(batch, out resultDiagnostic))
            {
                return false;
            }
            completedMinimumLifecycle = run.LifecycleGeneration;
            return true;
        }

        private bool TryCollectLocalRunMinimum(
            TerminalPersonalRewardBatchV1 batch,
            out string resultDiagnostic)
        {
            resultDiagnostic = string.Empty;
            RunPlayerRuntimeSnapshotV1 player =
                run.RuntimePorts.Player.ExportSnapshot();
            var localOperations = new HashSet<StableId>();
            for (int index = 0; index < batch.Results.Count; index++)
            {
                GeneratedTerminalDropResultV1 result = batch.Results[index];
                if (result != null
                    && result.IsAccepted
                    && result.SourceFact.AttributedParticipantStableId
                        == player.ParticipantStableId)
                {
                    localOperations.Add(
                        result.OperationRequest.SourceOperationStableId);
                }
            }
            if (localOperations.Count == 0)
            {
                return true;
            }

            var collectedOperations = new HashSet<StableId>();
            IReadOnlyList<RunPickupSnapshotV1> available =
                pickups.Authority.ExportAvailablePickups();
            for (int index = 0; index < available.Count; index++)
            {
                RunPickupSnapshotV1 pickup = available[index];
                StableId dropOperationStableId =
                    pickup.Batch.DropOperationStableId;
                if (!localOperations.Contains(dropOperationStableId))
                {
                    continue;
                }
                StableId collectionOperation =
                    RewardGenerationFingerprintV1.DeriveStableId(
                        "runminimumcollection",
                        run.RunStableId.ToString(),
                        run.LifecycleGeneration.ToString(
                            CultureInfo.InvariantCulture),
                        pickup.PickupStableId.ToString());
                RunPickupCollectionResultV1 collected =
                    pickups.Authority.Collect(
                        new RunPickupCollectionCommandV1(
                            collectionOperation,
                            pickup.PickupStableId,
                            pickup.Reward.RewardInstanceStableId,
                            run.RunStableId,
                            run.LifecycleGeneration,
                            player.ActorInstanceStableId,
                            player.ParticipantStableId,
                            pickup.Fingerprint));
                if (collected == null || !collected.IsCollected)
                {
                    resultDiagnostic = collected == null
                        ? "stage1-run-minimum-collection-null"
                        : collected.Diagnostic;
                    return false;
                }
                collectedOperations.Add(dropOperationStableId);
            }

            if (collectedOperations.Count != localOperations.Count)
            {
                resultDiagnostic =
                    "stage1-run-minimum-local-pickup-not-realized";
                return false;
            }
            return true;
        }

        private void RefreshTerminalRewardLifecycle()
        {
            completedMinimumLifecycle = -1L;
        }

        private void ReleaseTerminalRewardComposition()
        {
            batchDelivery = null;
            completedMinimumLifecycle = -1L;
            ReleaseDurableEquipmentPayloadRetention();
        }
    }
}
