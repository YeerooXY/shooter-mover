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
            bool current = sharedRunSession != null
                && sharedRunSessionObservedStableId == runStableId
                && sharedRunSessionObservedPlayerGeneration == playerGeneration
                && sharedRunSession.RunStableId == runStableId
                && sharedRunSession.LifecycleGeneration == playerGeneration
                && sharedRunSession.LifecycleState
                    == RunSessionLifecycleStateV1.Active;
            if (current)
            {
                return;
            }

            TeardownEnemyAttackPatterns();
            TeardownSharedRunSession();
            ComposeSharedRunSession(playerGeneration);
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
                RunSessionFingerprintV1.Hash(
                    "stage1-production-run-material-v1|" + runStableId),
                graph.Character.CharacterInstanceStableId,
                graph.Character.Revision,
                graph.Character.Fingerprint,
                Level1AuthorableRoomDefinitionV1.LayoutStableId,
                StableId.Parse("difficulty.normal"),
                controller.RestartGeneration + 1L,
                0L,
                RunSessionFingerprintV1.Hash(
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

            sharedRunSessionSimulationTick =
                sharedRunSession.AuthoritativeTick;
            sharedRunSessionObservedStableId = runStableId;
            sharedRunSessionObservedPlayerGeneration = playerGeneration;
            sharedRunSessionFailed = false;
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
            string fingerprint = RunSessionFingerprintV1.Hash(
                sharedRunSession.RunStableId
                + "|"
                + sharedRunSession.LifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture)
                + "|"
                + sharedRunSessionSimulationTick.ToString(
                    CultureInfo.InvariantCulture));
            RunSessionTimeAdvanceResultV1 result =
                sharedRunSession.AdvanceTime(
                    new AdvanceRunSessionTimeCommandV1(
                        StableId.Create(
                            "run-time-advance",
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
                        : result.RejectionCode));
            }
        }

        private void TeardownSharedRunSession()
        {
            sharedRunSession = null;
            sharedRunSessionAuthority = null;
            sharedRunSessionSimulationTick = 0L;
            sharedRunSessionObservedStableId = null;
            sharedRunSessionObservedPlayerGeneration = -1L;
        }
    }
}
