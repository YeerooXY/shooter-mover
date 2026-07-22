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
using ShooterMover.RunPickups;
using ShooterMover.TerminalDropBinding;
using ShooterMover.TestSupport.VisibleSlice;
using ShooterMover.UnityAdapters.Rewards.RunPickups;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Bounded production adapter for the existing Stage 1 destructible-prop package.
    /// The legacy prop authority emits one immutable Destroyed result; this adapter turns
    /// that result into the same #277 generation/pending-admission path used by enemies and
    /// realizes the exact admitted child through the existing #279 pickup authority.
    /// </summary>
    [DefaultExecutionOrder(21100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Stage1RunPickupBootstrap2D))]
    public sealed class Stage1RunPickupPropBootstrap2D : MonoBehaviour
    {
        private Stage1VisibleSliceController controller;
        private Stage1RunPickupBootstrap2D pickupBootstrap;
        private RunSessionAggregateV1 observedRun;
        private RunPickupSourcePositionRegistry2D sourcePositions;
        private RunPickupPresenter2D presenter;
        private TerminalDropGenerationAuthorityV1 generation;
        private PendingTerminalDropAdmissionAuthorityV1 pending;
        private PendingTerminalDropPickupConsumerV1 pickupConsumer;
        private readonly List<DestructibleProp2D> subscribedProps =
            new List<DestructibleProp2D>();
        private string diagnostic = string.Empty;

        public bool IsComposed
        {
            get { return generation != null && subscribedProps.Count > 0; }
        }

        public string Diagnostic { get { return diagnostic; } }
        public PendingTerminalDropAdmissionResultV1 LastAdmission { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
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
                    roots[rootIndex].GetComponentsInChildren<Stage1VisibleSliceController>(true);
                for (int index = 0; index < controllers.Length; index++)
                {
                    Stage1VisibleSliceController value = controllers[index];
                    if (value == null) continue;
                    if (value.GetComponent<Stage1RunPickupBootstrap2D>() == null)
                        value.gameObject.AddComponent<Stage1RunPickupBootstrap2D>();
                    if (value.GetComponent<Stage1RunPickupPropBootstrap2D>() == null)
                        value.gameObject.AddComponent<Stage1RunPickupPropBootstrap2D>();
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
                Teardown();
            }
        }

        private void Compose()
        {
            Teardown();
            if (controller == null)
                controller = GetComponent<Stage1VisibleSliceController>();
            if (pickupBootstrap == null
                || !pickupBootstrap.IsComposed
                || pickupBootstrap.RunSession == null
                || pickupBootstrap.PickupAuthority == null)
            {
                throw new InvalidOperationException(
                    "The Stage 1 enemy pickup composition is unavailable.");
            }

            sourcePositions = pickupBootstrap.GetComponentInChildren<
                RunPickupSourcePositionRegistry2D>(true);
            presenter = pickupBootstrap.GetComponentInChildren<RunPickupPresenter2D>(true);
            if (sourcePositions == null || presenter == null)
            {
                throw new InvalidOperationException(
                    "The shared pickup source-position registry or presenter is unavailable.");
            }

            observedRun = pickupBootstrap.RunSession;
            pickupConsumer = new PendingTerminalDropPickupConsumerV1(
                pickupBootstrap.PickupAuthority);
            pending = new PendingTerminalDropAdmissionAuthorityV1();
            generation = new TerminalDropGenerationAuthorityV1(
                new TerminalDropFactAdapterRegistryV1(new ITerminalDropFactAdapterV1[]
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

            DestructibleProp2D[] props =
                controller.GetComponentsInChildren<DestructibleProp2D>(true);
            for (int index = 0; index < props.Length; index++)
            {
                DestructibleProp2D prop = props[index];
                if (prop == null || !prop.IsConfigured || prop.PropId == null) continue;
                prop.Destroyed += HandleDestroyed;
                subscribedProps.Add(prop);
            }
            if (subscribedProps.Count == 0)
            {
                throw new InvalidOperationException(
                    "No configured Stage 1 destructible prop was available.");
            }
        }

        private void HandleDestroyed(DestructiblePropDestructionResult destruction)
        {
            if (destruction == null
                || observedRun == null
                || generation == null
                || pending == null
                || pickupConsumer == null
                || sourcePositions == null)
            {
                return;
            }

            DestructibleProp2D source = null;
            for (int index = 0; index < subscribedProps.Count; index++)
            {
                DestructibleProp2D candidate = subscribedProps[index];
                if (candidate != null && candidate.PropId == destruction.PropId)
                {
                    source = candidate;
                    break;
                }
            }
            if (source == null) return;

            StableId roomStableId = controller.CurrentRoomStableId;
            if (roomStableId == null)
            {
                diagnostic = "stage1-prop-pickup-room-unavailable";
                return;
            }
            Vector2 position = source.BlockingCollider == null
                ? (Vector2)source.transform.position
                : source.BlockingCollider.bounds.center;
            string positionDiagnostic;
            if (!sourcePositions.Register(
                observedRun.RunStableId,
                observedRun.LifecycleGeneration,
                source.PropId,
                source.PropId,
                roomStableId,
                position,
                RunSessionFingerprintV1.Hash(
                    destruction.EventId
                    + "|"
                    + position.x.ToString("R", CultureInfo.InvariantCulture)
                    + "|"
                    + position.y.ToString("R", CultureInfo.InvariantCulture)),
                out positionDiagnostic))
            {
                diagnostic = positionDiagnostic;
                return;
            }

            GeneratedTerminalDropResultV1 generated = generation.Generate(destruction);
            LastAdmission = pending.Admit(generated);
            RunPickupRealizationResultV1 realized = pickupConsumer.Consume(LastAdmission);
            diagnostic = realized == null
                ? "stage1-prop-pickup-realization-null"
                : realized.Diagnostic;
            presenter.Synchronize(roomStableId);
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
            return new RewardProfileCatalogResolverV1(new[] { ordinary, explosive });
        }

        private void Teardown()
        {
            for (int index = 0; index < subscribedProps.Count; index++)
            {
                DestructibleProp2D prop = subscribedProps[index];
                if (prop != null) prop.Destroyed -= HandleDestroyed;
            }
            subscribedProps.Clear();
            observedRun = null;
            sourcePositions = null;
            presenter = null;
            generation = null;
            pending = null;
            pickupConsumer = null;
            LastAdmission = null;
        }

        private void OnDestroy()
        {
            Teardown();
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
        public Type FactType { get { return typeof(DestructiblePropDestructionResult); } }

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
                    - Stage1DestructiblePropIntegration.ExplosiveMaximumHealth) < 0.001d;
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
