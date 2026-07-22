using System;
using System.Globalization;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Rewards.Generation;
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
                overrides);
            batchDelivery = new Stage1PersonalRewardBatchDeliveryV1(
                terminalDrops,
                admissionBridge);
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
        /// Called synchronously before the existing final-exit handler. The result is
        /// delivered through the same exact pending-pickup transport as every other
        /// personal reward and exact replay is harmless.
        /// </summary>
        internal bool TryGenerateRunMinimum(out string resultDiagnostic)
        {
            resultDiagnostic = string.Empty;
            if (run == null
                || terminalDrops == null
                || batchDelivery == null
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
            completedMinimumLifecycle = run.LifecycleGeneration;
            resultDiagnostic = delivery.Diagnostic;
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
        }
    }
}
