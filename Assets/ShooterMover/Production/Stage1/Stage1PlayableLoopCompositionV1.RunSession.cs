using System;
using System.Globalization;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.RunConditionIntegration;
using ShooterMover.UI.ProductionFlow;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Production owner for the one Stage 1 Run Session aggregate. Feature partials consume this
    /// aggregate through narrow adapters; they never start another run or rebuild its runtime graph.
    /// </summary>
    public sealed partial class Stage1PlayableLoopCompositionV1
    {
        private RunSessionAuthorityV1 sharedRunSessionAuthority;
        private RunSessionAggregateV1 sharedRunSession;
        private RunRewardRuntimeSnapshotV1 retainedRewardRuntimeSnapshot;
        private long sharedRunSessionSimulationTick;
        private StableId sharedRunSessionObservedStableId;
        private long sharedRunSessionObservedPlayerGeneration = -1L;
        private bool sharedRunSessionFailed;

        internal bool IsSharedRunSessionReady
        {
            get
            {
                return sharedRunSession != null
                    && sharedRunSession.LifecycleState
                        == RunSessionLifecycleStateV1.Active
                    && sharedRunSession.RunStableId == runStableId;
            }
        }

        internal bool TryResolveSharedRunSession(
            out RunSessionAggregateV1 run)
        {
            run = IsSharedRunSessionReady ? sharedRunSession : null;
            return run != null;
        }

        private void FixedUpdate()
        {
            if (!initialized
                || ending
                || controller == null
                || sharedRunSessionFailed)
            {
                return;
            }

            try
            {
                EnsureSharedRunSession();
                AdvanceSharedRunSessionTime();
                TickEnemyAttackPatterns();
            }
            catch (Exception exception)
            {
                sharedRunSessionFailed = true;
                diagnostic = "Shared Stage 1 Run Session failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message;
                Debug.LogException(exception, this);
                TeardownEnemyAttackPatterns();
                TeardownSharedRunSession();
            }
        }

        private void OnDisable()
        {
            TeardownEnemyAttackPatterns();
            TeardownSharedRunSession();
        }

        private void EnsureSharedRunSession()
        {
            if (controller.PlayerLiveAuthority == null
                || !controller.PlayerLiveAuthority.IsInitialized)
            {
                throw new InvalidOperationException(
                    "The accepted player runtime is unavailable for Run Session composition.");
            }

            long playerGeneration = controller.PlayerLiveAuthority
                .ExportSnapshot()
                .Player
                .LifecycleGeneration;
            if (sharedRunSession == null)
            {
                ComposeSharedRunSession(playerGeneration);
                return;
            }

            bool sameRun = sharedRunSessionObservedStableId == runStableId
                && sharedRunSession.RunStableId == runStableId;
            if (!sameRun)
            {
                TeardownEnemyAttackPatterns();
                TeardownSharedRunSession();
                ComposeSharedRunSession(playerGeneration);
                return;
            }
            if (sharedRunSession.LifecycleState
                != RunSessionLifecycleStateV1.Active)
            {
                throw new InvalidOperationException(
                    "The shared Run Session ended before Stage 1 left the mission.");
            }
            if (playerGeneration != sharedRunSession.LifecycleGeneration)
            {
                throw new InvalidOperationException(
                    "The player lifecycle advanced outside the authoritative shared Run Session.");
            }

            sharedRunSessionObservedPlayerGeneration = playerGeneration;
        }

        private void ComposeSharedRunSession(long playerGeneration)
        {
            if (runStableId == null
                || missionResults == null
                || rooms == null
                || effectEmitter == null)
            {
                throw new InvalidOperationException(
                    "Stage 1 shared Run Session prerequisites are unavailable.");
            }

            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 selectedProfile;
            ShooterMover.Application.Persistence.Composition
                .CharacterCompositionCoordinatorV1 characterComposition;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out graph,
                    out selectedProfile,
                    out characterComposition)
                || graph == null
                || selectedProfile == null
                || characterComposition == null)
            {
                throw new InvalidOperationException(
                    "The selected account-backed character graph is unavailable.");
            }

            var missionResultPort = new ExistingMissionResultRunPortV1(
                missionResults,
                graph.LoadoutRuntime.Holdings,
                graph.StrongboxAuthority.ExportSnapshot);
            var nonConditionFactory =
                new Stage1SharedRunSessionNonConditionRuntimePortFactoryV1(
                    controller.PlayerLiveAuthority,
                    rooms,
                    missionResultPort,
                    effectEmitter.ClearEmittedEffects);
            var startSource =
                new ProductionConditionBoundRunSessionStartSourceV1(
                    characterComposition,
                    new Stage1ProductionRunStatInputResolverV1(
                        controller.PlayerLiveAuthority),
                    nonConditionFactory,
                    new Stage1ProductionConditionDefinitionProviderV1());
            sharedRunSessionAuthority = new RunSessionAuthorityV1(startSource);

            var startCommand = new StartRunSessionCommandV1(
                StableId.Create(
                    "operation",
                    "stage1-production-run-g"
                        + playerGeneration.ToString(
                            CultureInfo.InvariantCulture)),
                runStableId,
                Stage1ProductionFingerprintV1.Hash(
                    "stage1-production-run-material-v1|" + runStableId),
                graph.Character.CharacterInstanceStableId,
                graph.Character.Revision,
                graph.Character.Fingerprint,
                Level1AuthorableRoomDefinitionV1.LayoutStableId,
                StableId.Parse("difficulty.normal"),
                controller.RestartGeneration + 1L,
                0L,
                Stage1ProductionFingerprintV1.Hash(
                    "stage1-production-event-context-v1"));
            RunSessionStartResultV1 started =
                sharedRunSessionAuthority.Start(startCommand);
            if (started == null
                || (started.Status != RunSessionStartStatusV1.Started
                    && started.Status != RunSessionStartStatusV1.ExactReplay)
                || !sharedRunSessionAuthority.TryGetRun(
                    runStableId,
                    out sharedRunSession)
                || sharedRunSession == null)
            {
                throw new InvalidOperationException(
                    "The shared Stage 1 Run Session could not start: "
                    + (started == null
                        ? "result-null"
                        : started.RejectionCode));
            }
            if (sharedRunSession.LifecycleGeneration != playerGeneration)
            {
                throw new InvalidOperationException(
                    "The shared Run Session and player lifecycle are split.");
            }
            if (!(sharedRunSession.RuntimePorts.ConditionalFacts
                    is ExistingConditionRuntimeRunPortV1)
                || !(sharedRunSession.RuntimePorts.StatusEffects
                    is ConditionOwnedStatusEffectRunPortV1))
            {
                throw new InvalidOperationException(
                    "The shared Run Session did not compose the canonical condition/effect owner.");
            }

            RestoreRetainedRewardRuntimeIfCompatible(sharedRunSession);
            sharedRunSessionSimulationTick =
                sharedRunSession.AuthoritativeTick;
            sharedRunSessionObservedStableId = runStableId;
            sharedRunSessionObservedPlayerGeneration = playerGeneration;
            sharedRunSessionFailed = false;
        }

        private RunSessionRestartResultV1 RestartSharedRunSession()
        {
            RunSessionAggregateV1 run;
            if (!TryResolveSharedRunSession(out run))
            {
                throw new InvalidOperationException(
                    "Stage 1 cannot restart without the shared production Run Session.");
            }
            if (run.LifecycleGeneration == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "The shared Run Session lifecycle generation overflowed.");
            }

            RunSessionAggregateV1 originalAggregate = run;
            StableId originalRunStableId = run.RunStableId;
            string originalFrozenInputFingerprint = run.FrozenInputs.Fingerprint;
            long retiringGeneration = run.LifecycleGeneration;
            long replacementGeneration = retiringGeneration + 1L;
            var command = new RestartRunSessionCommandV1(
                StableId.Create(
                    "operation",
                    "stage1-production-run-restart-g"
                        + replacementGeneration.ToString(
                            CultureInfo.InvariantCulture)),
                originalRunStableId,
                retiringGeneration,
                replacementGeneration,
                run.AuthoritativeTick,
                RunRestartPolicyV1.FullTransientReset());

            TeardownEnemyAttackPatterns();
            RunSessionRestartResultV1 result = run.Restart(command);
            if (result == null || !result.Succeeded)
            {
                throw new InvalidOperationException(
                    "The shared Run Session rejected restart: "
                    + (result == null
                        ? "result-null"
                        : result.RejectionCode));
            }
            if (!ReferenceEquals(originalAggregate, sharedRunSession)
                || sharedRunSession.RunStableId != originalRunStableId
                || !string.Equals(
                    sharedRunSession.FrozenInputs.Fingerprint,
                    originalFrozenInputFingerprint,
                    StringComparison.Ordinal)
                || sharedRunSession.LifecycleGeneration
                    != replacementGeneration)
            {
                throw new InvalidOperationException(
                    "The shared Run Session restart replaced or corrupted mission identity.");
            }

            retainedRewardRuntimeSnapshot = null;
            sharedRunSessionSimulationTick =
                sharedRunSession.AuthoritativeTick;
            sharedRunSessionObservedPlayerGeneration = replacementGeneration;
            restartObserved = replacementGeneration;
            rewardedEnemies.Clear();
            participantStats.Clear();
            pendingEnemyRewards.Clear();
            preparedEffects.Clear();
            preparedPools.Clear();
            fireSequence = 0L;
            enemyDamageOrder = 0L;
            playerDeathProjected = false;
            ProjectCurrentRoom(true);
            return result;
        }

        private void AdvanceSharedRunSessionTime()
        {
            if (sharedRunSession == null)
            {
                throw new InvalidOperationException(
                    "The shared Run Session is not composed.");
            }
            if (sharedRunSessionSimulationTick == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "The shared Run Session simulation tick overflowed.");
            }

            sharedRunSessionSimulationTick++;
            string fingerprint = Stage1ProductionFingerprintV1.Hash(
                sharedRunSession.RunStableId
                + "|"
                + sharedRunSession.LifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture)
                + "|"
                + sharedRunSessionSimulationTick.ToString(
                    CultureInfo.InvariantCulture));
            RunConditionAdvanceResultV1 result =
                sharedRunSession.AdvanceConditionRuntime(
                    new RunConditionAdvanceCommandV1(
                        StableId.Create(
                            "run-condition-advance",
                            "stage1-" + fingerprint.Substring(0, 24)),
                        sharedRunSession.RunStableId,
                        sharedRunSession.LifecycleGeneration,
                        sharedRunSessionSimulationTick));
            if (result == null || !result.Succeeded)
            {
                throw new InvalidOperationException(
                    "The shared Run Session rejected authoritative time: "
                    + (result == null
                        ? "result-null"
                        : result.DiagnosticCode));
            }
        }

        private void TeardownSharedRunSession()
        {
            CaptureRewardRuntimeForReconnect();
            sharedRunSession = null;
            sharedRunSessionAuthority = null;
            sharedRunSessionSimulationTick = 0L;
            sharedRunSessionObservedStableId = null;
            sharedRunSessionObservedPlayerGeneration = -1L;
        }

        private void CaptureRewardRuntimeForReconnect()
        {
            if (sharedRunSession == null
                || sharedRunSession.LifecycleState
                    != RunSessionLifecycleStateV1.Active)
            {
                return;
            }
            try
            {
                retainedRewardRuntimeSnapshot =
                    sharedRunSession.ExportRewardRuntimeSnapshot();
            }
            catch (InvalidOperationException)
            {
                // Reward composition may not have installed its environment yet.
            }
        }

        private void RestoreRetainedRewardRuntimeIfCompatible(
            RunSessionAggregateV1 run)
        {
            if (run == null || retainedRewardRuntimeSnapshot == null)
            {
                return;
            }
            if (retainedRewardRuntimeSnapshot.RunStableId != run.RunStableId
                || retainedRewardRuntimeSnapshot.RunLifecycleGeneration
                    != run.LifecycleGeneration)
            {
                retainedRewardRuntimeSnapshot = null;
                return;
            }
            run.RestoreRewardRuntimeSnapshot(retainedRewardRuntimeSnapshot);
            retainedRewardRuntimeSnapshot = null;
        }
    }
}
