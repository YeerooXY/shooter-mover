using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Runs.Session;
using ShooterMover.ContentPackages.Props.DestructibleProps;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.TerminalDropBinding;
using ShooterMover.TestSupport.VisibleSlice;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Retained transactional adapter for Stage 1 destructible props. The one-shot package
    /// Destroyed callback only records the immutable terminal fact. Generation, pending
    /// admission, source registration, and pickup enqueue are retried until acknowledged.
    /// </summary>
    [DefaultExecutionOrder(21100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Stage1RunPickupBootstrap2D))]
    public sealed class Stage1RunPickupPropBootstrap2D : MonoBehaviour
    {
        private sealed class PendingPropTerminal
        {
            public PendingPropTerminal(
                DestructiblePropDestructionResult destruction,
                DestructibleProp2D source,
                StableId runStableId,
                long lifecycleGeneration)
            {
                Destruction = destruction;
                Source = source;
                RunStableId = runStableId;
                LifecycleGeneration = lifecycleGeneration;
            }

            public DestructiblePropDestructionResult Destruction { get; }
            public DestructibleProp2D Source { get; }
            public StableId RunStableId { get; }
            public long LifecycleGeneration { get; }
        }

        private Stage1VisibleSliceController controller;
        private Stage1RunPickupBootstrap2D pickupBootstrap;
        private RunSessionAggregateV1 observedRun;
        private TerminalDropGenerationAuthorityV1 generation;
        private PendingTerminalDropAdmissionAuthorityV1 pending;
        private readonly List<DestructibleProp2D> subscribedProps =
            new List<DestructibleProp2D>();
        private readonly Dictionary<StableId, PendingPropTerminal>
            pendingTerminalByEvent =
                new Dictionary<StableId, PendingPropTerminal>();
        private string diagnostic = string.Empty;

        public bool IsComposed
        {
            get
            {
                return generation != null
                    && observedRun != null
                    && subscribedProps.Count > 0;
            }
        }

        public string Diagnostic { get { return diagnostic; } }
        public int PendingTerminalCount { get { return pendingTerminalByEvent.Count; } }
        public PendingTerminalDropAdmissionResultV1 LastAdmission { get; private set; }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Install(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Install(scene);
        }

        private static void Install(Scene scene)
        {
            if (!scene.IsValid()) return;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Stage1VisibleSliceController[] controllers =
                    roots[rootIndex].GetComponentsInChildren<
                        Stage1VisibleSliceController>(true);
                for (int index = 0; index < controllers.Length; index++)
                {
                    Stage1VisibleSliceController value = controllers[index];
                    if (value == null) continue;
                    if (value.GetComponent<Stage1RunPickupBootstrap2D>() == null)
                        value.gameObject.AddComponent<
                            Stage1RunPickupBootstrap2D>();
                    if (value.GetComponent<
                            Stage1RunPickupPropBootstrap2D>() == null)
                    {
                        value.gameObject.AddComponent<
                            Stage1RunPickupPropBootstrap2D>();
                    }
                }
            }
        }

        private IEnumerator Start()
        {
            controller = GetComponent<Stage1VisibleSliceController>();
            pickupBootstrap = GetComponent<Stage1RunPickupBootstrap2D>();
            while (pickupBootstrap == null || !pickupBootstrap.IsComposed)
            {
                if (pickupBootstrap == null)
                    pickupBootstrap = GetComponent<Stage1RunPickupBootstrap2D>();
                yield return null;
            }
            TryCompose();
        }

        private void LateUpdate()
        {
            if (pickupBootstrap == null)
                pickupBootstrap = GetComponent<Stage1RunPickupBootstrap2D>();
            if (pickupBootstrap == null || !pickupBootstrap.IsComposed) return;
            if (!ReferenceEquals(observedRun, pickupBootstrap.RunSession))
                TryCompose();
            ProcessPendingTerminals();
        }

        private void TryCompose()
        {
            try
            {
                Compose();
                diagnostic = string.Empty;
            }
            catch (Exception exception)
            {
                diagnostic = "Stage 1 prop pickup integration failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message;
                Debug.LogException(exception, this);
                ReleaseBindings();
            }
        }

        private void Compose()
        {
            if (controller == null)
                controller = GetComponent<Stage1VisibleSliceController>();
            if (pickupBootstrap == null)
                pickupBootstrap = GetComponent<Stage1RunPickupBootstrap2D>();
            if (pickupBootstrap == null
                || !pickupBootstrap.IsComposed
                || pickupBootstrap.RunSession == null)
            {
                throw new InvalidOperationException(
                    "The shared Stage 1 pickup composition is unavailable.");
            }

            RunSessionAggregateV1 nextRun = pickupBootstrap.RunSession;
            bool changedRun = observedRun != null
                && !ReferenceEquals(observedRun, nextRun);
            ReleaseBindings();
            if (changedRun)
            {
                pendingTerminalByEvent.Clear();
                generation = null;
                pending = null;
                LastAdmission = null;
            }
            observedRun = nextRun;

            if (pending == null)
                pending = new PendingTerminalDropAdmissionAuthorityV1();
            if (generation == null)
            {
                generation = new TerminalDropGenerationAuthorityV1(
                    new TerminalDropFactAdapterRegistryV1(
                        new ITerminalDropFactAdapterV1[]
                        {
                            new Stage1DestructiblePropTerminalDropFactAdapterV1(
                                () => observedRun),
                        }),
                    new Stage1PickupTerminalDropRunContextResolverV1(
                        () => observedRun,
                        () => 1),
                    BuildPropRewardProfiles(),
                    new ExistingRewardGenerationExecutorV1(
                        new RewardGenerationServiceV1()));
            }

            DestructibleProp2D[] props =
                controller.GetComponentsInChildren<DestructibleProp2D>(true);
            for (int index = 0; index < props.Length; index++)
            {
                DestructibleProp2D prop = props[index];
                if (prop == null || !prop.IsConfigured || prop.PropId == null)
                    continue;
                prop.Destroyed += HandleDestroyed;
                subscribedProps.Add(prop);
            }
            if (subscribedProps.Count == 0)
            {
                throw new InvalidOperationException(
                    "No configured Stage 1 destructible prop was available.");
            }
        }

        private void HandleDestroyed(
            DestructiblePropDestructionResult destruction)
        {
            if (destruction == null || destruction.EventId == null) return;
            RunSessionAggregateV1 current = observedRun
                ?? (pickupBootstrap == null ? null : pickupBootstrap.RunSession);
            if (current == null)
            {
                diagnostic = "stage1-prop-terminal-shared-run-unavailable";
                return;
            }

            DestructibleProp2D source = FindSource(destruction.PropId);
            if (source == null)
            {
                diagnostic = "stage1-prop-terminal-source-unavailable";
                return;
            }

            PendingPropTerminal existing;
            if (!pendingTerminalByEvent.TryGetValue(
                destruction.EventId,
                out existing))
            {
                pendingTerminalByEvent.Add(
                    destruction.EventId,
                    new PendingPropTerminal(
                        destruction,
                        source,
                        current.RunStableId,
                        current.LifecycleGeneration));
            }
            ProcessPendingTerminals();
        }

        private void ProcessPendingTerminals()
        {
            if (pickupBootstrap == null
                || !pickupBootstrap.IsComposed
                || observedRun == null
                || generation == null
                || pending == null)
            {
                return;
            }

            var events = new List<StableId>(pendingTerminalByEvent.Keys);
            for (int index = 0; index < events.Count; index++)
            {
                StableId eventId = events[index];
                PendingPropTerminal record;
                if (!pendingTerminalByEvent.TryGetValue(eventId, out record)
                    || record == null
                    || record.Destruction == null)
                {
                    continue;
                }

                if (record.RunStableId != observedRun.RunStableId
                    || record.LifecycleGeneration
                        != observedRun.LifecycleGeneration)
                {
                    pendingTerminalByEvent.Remove(eventId);
                    diagnostic = "stage1-prop-terminal-retired-stale-lifecycle";
                    continue;
                }

                StableId roomStableId = controller == null
                    ? null
                    : controller.CurrentRoomStableId;
                if (roomStableId == null)
                {
                    diagnostic = "stage1-prop-pickup-room-unavailable";
                    continue;
                }

                Vector2 position;
                try
                {
                    position = record.Source == null
                        ? default(Vector2)
                        : record.Source.BlockingCollider == null
                            ? (Vector2)record.Source.transform.position
                            : record.Source.BlockingCollider.bounds.center;
                }
                catch (Exception exception)
                {
                    diagnostic = "stage1-prop-position-exception:"
                        + exception.GetType().Name
                        + ":"
                        + exception.Message;
                    continue;
                }
                if (record.Source == null)
                {
                    diagnostic = "stage1-prop-terminal-source-unavailable";
                    continue;
                }

                GeneratedTerminalDropResultV1 generated;
                try
                {
                    generated = generation.Generate(record.Destruction);
                }
                catch (Exception exception)
                {
                    diagnostic = "stage1-prop-generation-exception:"
                        + exception.GetType().Name
                        + ":"
                        + exception.Message;
                    continue;
                }

                try
                {
                    LastAdmission = pending.Admit(generated);
                }
                catch (Exception exception)
                {
                    diagnostic = "stage1-prop-admission-exception:"
                        + exception.GetType().Name
                        + ":"
                        + exception.Message;
                    continue;
                }
                if (LastAdmission == null
                    || !LastAdmission.IsAccepted
                    || LastAdmission.PendingResult == null
                    || LastAdmission.PendingResult.SourceFact == null)
                {
                    diagnostic = LastAdmission == null
                        ? "stage1-prop-admission-null"
                        : LastAdmission.Diagnostic;
                    continue;
                }

                TerminalDropSourceFactV1 sourceFact =
                    LastAdmission.PendingResult.SourceFact;
                string positionFingerprint = RunSessionFingerprintV1.Hash(
                    sourceFact.TerminalEventStableId
                    + "|"
                    + position.x.ToString("R", CultureInfo.InvariantCulture)
                    + "|"
                    + position.y.ToString("R", CultureInfo.InvariantCulture));
                try
                {
                    pickupBootstrap.RegisterFixedSource(
                        sourceFact.RunStableId,
                        sourceFact.RunLifecycleGeneration,
                        sourceFact.SourceEntityStableId,
                        sourceFact.SourcePlacementStableId,
                        roomStableId,
                        position,
                        positionFingerprint);
                    Stage1PickupDeliveryResultV1 queued =
                        pickupBootstrap.EnqueueAdmission(LastAdmission);
                    diagnostic = queued == null
                        ? "stage1-prop-pickup-enqueue-null"
                        : queued.Diagnostic;
                    if (queued != null && queued.IsAcknowledged)
                        pendingTerminalByEvent.Remove(eventId);
                }
                catch (Exception exception)
                {
                    diagnostic = "stage1-prop-pickup-enqueue-exception:"
                        + exception.GetType().Name
                        + ":"
                        + exception.Message;
                }
            }
        }

        private DestructibleProp2D FindSource(StableId propId)
        {
            for (int index = 0; index < subscribedProps.Count; index++)
            {
                DestructibleProp2D candidate = subscribedProps[index];
                if (candidate != null && candidate.PropId == propId)
                    return candidate;
            }
            return null;
        }

        private static RewardProfileCatalogResolverV1 BuildPropRewardProfiles()
        {
            RewardProfileV1 ordinary = RewardProfileV1.Create(
                StableId.Parse("drop.prop-stage1-ordinary"),
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse("grant.stage1-prop-ordinary-scrap"),
                        RewardGrantKindV1.Scrap,
                        StableId.Parse("currency.scrap"),
                        2L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            RewardProfileV1 explosive = RewardProfileV1.Create(
                StableId.Parse("drop.prop-stage1-explosive"),
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse("grant.stage1-prop-explosive-scrap"),
                        RewardGrantKindV1.Scrap,
                        StableId.Parse("currency.scrap"),
                        4L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            return new RewardProfileCatalogResolverV1(new[]
            {
                ordinary,
                explosive,
            });
        }

        private void ReleaseBindings()
        {
            for (int index = 0; index < subscribedProps.Count; index++)
            {
                DestructibleProp2D prop = subscribedProps[index];
                if (prop != null) prop.Destroyed -= HandleDestroyed;
            }
            subscribedProps.Clear();
            observedRun = null;
        }

        private void OnDestroy()
        {
            ReleaseBindings();
            pendingTerminalByEvent.Clear();
            generation = null;
            pending = null;
            LastAdmission = null;
        }
    }

    internal sealed class Stage1DestructiblePropTerminalDropFactAdapterV1 :
        ITerminalDropFactAdapterV1
    {
        private static readonly StableId FactKind =
            StableId.Parse("terminal-drop-fact.stage1-destructible-prop");
        private readonly Func<RunSessionAggregateV1> run;

        public Stage1DestructiblePropTerminalDropFactAdapterV1(
            Func<RunSessionAggregateV1> run)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
        }

        public StableId FactKindStableId { get { return FactKind; } }
        public Type FactType
        {
            get { return typeof(DestructiblePropDestructionResult); }
        }

        public TerminalDropAdaptationResultV1 Adapt(object terminalFact)
        {
            var destruction = terminalFact as DestructiblePropDestructionResult;
            RunSessionAggregateV1 current = run();
            if (destruction == null || current == null)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    "stage1-prop-terminal-context-unavailable");
            }

            RunPlayerRuntimeSnapshotV1 player;
            try
            {
                player = current.RuntimePorts.Player.ExportSnapshot();
            }
            catch (Exception exception)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    "stage1-prop-terminal-player-context-unavailable:"
                        + exception.GetType().Name);
            }
            if (player == null)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    "stage1-prop-terminal-player-context-null");
            }

            bool explosive = Math.Abs(
                destruction.DestroyedState.MaximumHealth
                    - Stage1DestructiblePropIntegration
                        .ExplosiveMaximumHealth) < 0.001d;
            StableId definitionStableId = StableId.Parse(
                explosive
                    ? "prop.stage1-explosive"
                    : "prop.stage1-crate");
            StableId profileStableId = StableId.Parse(
                explosive
                    ? "drop.prop-stage1-explosive"
                    : "drop.prop-stage1-ordinary");
            string canonical = destruction.EventId
                + "|"
                + destruction.PropId
                + "|"
                + destruction.SourceId
                + "|"
                + destruction.DestroyedState.MaximumHealth.ToString(
                    "R",
                    CultureInfo.InvariantCulture);
            return TerminalDropAdaptationResultV1.Accepted(
                new TerminalDropSourceFactV1(
                    FactKind,
                    destruction.EventId,
                    destruction.EventId,
                    current.RunStableId,
                    current.LifecycleGeneration,
                    destruction.PropId,
                    destruction.PropId,
                    current.LifecycleGeneration,
                    definitionStableId,
                    player.ParticipantStableId,
                    destruction.SourceId,
                    StableId.Create(
                        "damage",
                        "combat-channel-"
                            + ((int)destruction.Channel).ToString(
                                CultureInfo.InvariantCulture)),
                    profileStableId,
                    RunSessionFingerprintV1.Hash("source|" + canonical),
                    RunSessionFingerprintV1.Hash("definition|" + canonical),
                    RunSessionFingerprintV1.Hash("upstream|" + canonical)));
        }
    }
}
