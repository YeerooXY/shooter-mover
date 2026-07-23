using System;
using System.Collections;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.RunPickups;
using ShooterMover.TerminalDropBinding;
using ShooterMover.TestSupport.VisibleSlice;
using ShooterMover.UI.ProductionFlow;
using ShooterMover.UnityAdapters.Rewards.RunPickups;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Physical-pickup consumer for the one shared Stage 1 Run Session. Enemy terminal facts,
    /// live actor identities, attribution, room placement, and content provenance are exported
    /// by the production composition; this adapter never derives or reconstructs them.
    /// </summary>
    [DefaultExecutionOrder(21000)]
    [DisallowMultipleComponent]
    public sealed class Stage1RunPickupBootstrap2D : MonoBehaviour
    {
        private Stage1PlayableLoopCompositionV1 stage1;
        private GameObject runtimeRoot;
        private RunSessionAggregateV1 run;
        private long observedLifecycleGeneration = -1L;
        private RunPickupSourcePositionRegistry2D sourcePositions;
        private RunPickupLiveCompositionV1 pickups;
        private TerminalDropBindingCompositionV1 terminalDrops;
        private RetainedTerminalDropEquipmentPayloadAuthority equipmentPayloads;
        private RunPickupPresenter2D presenter;
        private readonly Stage1PickupTerminalDropRunContextResolverV1
            dropRunContext =
                new Stage1PickupTerminalDropRunContextResolverV1();
        private readonly PendingAdmissionPickupBridgeV1 admissionBridge =
            new PendingAdmissionPickupBridgeV1();
        private readonly Dictionary<StableId, string> deliveredEnemyEvents =
            new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, string> quarantinedEnemyEvents =
            new Dictionary<StableId, string>();
        private readonly List<UnityEngine.Object> runtimeAssets =
            new List<UnityEngine.Object>();
        private PendingTerminalDropAdmissionResultV1 lastEnemyAdmission;
        private string diagnostic = string.Empty;

        public bool IsComposed
        {
            get
            {
                return run != null
                    && pickups != null
                    && ReferenceEquals(run, ResolveSharedRunOrNull());
            }
        }

        public string Diagnostic { get { return diagnostic; } }
        public RunSessionAggregateV1 RunSession { get { return run; } }
        public RunLocalPickupAuthorityV1 PickupAuthority
        {
            get { return pickups == null ? null : pickups.Authority; }
        }
        public int PendingAdmissionCount { get { return admissionBridge.PendingCount; } }
        public int QuarantinedAdmissionCount
        {
            get { return admissionBridge.QuarantinedCount; }
        }
        public PendingTerminalDropAdmissionResultV1 LastEnemyAdmission
        {
            get { return lastEnemyAdmission; }
        }
        internal ITerminalDropRunContextResolverV1 DropRunContext
        {
            get { return dropRunContext; }
        }
        internal ICollectedRunEquipmentPayloadSource EquipmentPayloadSource
        {
            get { return equipmentPayloads; }
        }

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
                    Stage1VisibleSliceController controller = controllers[index];
                    if (controller != null
                        && controller.GetComponent<Stage1RunPickupBootstrap2D>() == null)
                    {
                        controller.gameObject.AddComponent<Stage1RunPickupBootstrap2D>();
                    }
                }
            }
        }

        private IEnumerator Start()
        {
            stage1 = GetComponent<Stage1PlayableLoopCompositionV1>();
            while (stage1 != null)
            {
                RunSessionAggregateV1 shared;
                if (stage1.IsRunPickupProductionReady
                    && stage1.TryResolveSharedRunSession(out shared)
                    && shared != null)
                {
                    TryCompose(shared);
                    yield break;
                }
                yield return null;
            }
        }

        private void LateUpdate()
        {
            if (stage1 == null)
                stage1 = GetComponent<Stage1PlayableLoopCompositionV1>();
            if (stage1 == null || !stage1.IsRunPickupProductionReady) return;

            RunSessionAggregateV1 shared;
            if (!stage1.TryResolveSharedRunSession(out shared) || shared == null)
            {
                diagnostic = "pickup-shared-run-unavailable";
                admissionBridge.ReleaseRuntime();
                return;
            }

            if (run == null || !ReferenceEquals(run, shared))
                TryCompose(shared);
            else if (observedLifecycleGeneration != shared.LifecycleGeneration)
                TryRefreshLifecycle(shared);

            ProcessCanonicalEnemyTerminalFacts();
            admissionBridge.ProcessPending();
            if (!string.IsNullOrWhiteSpace(admissionBridge.LastDiagnostic))
                diagnostic = admissionBridge.LastDiagnostic;
            else if (presenter != null && stage1.RunPickupRooms != null)
                presenter.Synchronize(stage1.RunPickupRooms.CurrentRoomStableId);
        }

        public PickupDeliveryResultV1 EnqueueAdmission(
            PendingTerminalDropAdmissionResultV1 admission)
        {
            PickupDeliveryResultV1 result =
                admissionBridge.TryEnqueue(admission);
            admissionBridge.ProcessPending();
            diagnostic = admissionBridge.LastDiagnostic;
            return result;
        }

        public void RegisterFixedSource(
            StableId runStableId,
            long lifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId,
            StableId roomStableId,
            Vector2 position,
            string fingerprint)
        {
            admissionBridge.RegisterFixedSource(
                runStableId,
                lifecycleGeneration,
                sourceEntityStableId,
                sourcePlacementStableId,
                roomStableId,
                position,
                fingerprint);
        }

        private RunSessionAggregateV1 ResolveSharedRunOrNull()
        {
            RunSessionAggregateV1 shared;
            return stage1 != null
                && stage1.TryResolveSharedRunSession(out shared)
                    ? shared
                    : null;
        }

        private void TryCompose(RunSessionAggregateV1 shared)
        {
            try
            {
                Compose(shared);
                diagnostic = string.Empty;
            }
            catch (Exception exception)
            {
                diagnostic = "Stage 1 pickup integration failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message;
                Debug.LogException(exception, this);
                TeardownProjection(false);
            }
        }

        private void Compose(RunSessionAggregateV1 shared)
        {
            if (stage1 == null || !stage1.IsRunPickupProductionReady)
                throw new InvalidOperationException(
                    "Stage 1 pickup prerequisites are unavailable.");
            if (shared == null
                || shared.LifecycleState != RunSessionLifecycleStateV1.Active
                || shared.RunStableId != stage1.RunPickupRunStableId)
            {
                throw new InvalidOperationException(
                    "The exact shared production Run Session is unavailable for pickups.");
            }

            bool changedRun = run != null && run.RunStableId != shared.RunStableId;
            TeardownProjection(changedRun);
            run = shared;
            dropRunContext.Bind(run);

            runtimeRoot = new GameObject("PICKUP-LIVE-001 Shared Run Consumer");
            runtimeRoot.transform.SetParent(transform, false);
            sourcePositions = runtimeRoot.AddComponent<
                RunPickupSourcePositionRegistry2D>();
            pickups = RunPickupLiveCompositionV1.Create(run, sourcePositions);

            RunPickupAuthorityHost2D authorityHost =
                runtimeRoot.AddComponent<RunPickupAuthorityHost2D>();
            authorityHost.Configure(pickups.Authority);
            RunPickupPresentationRegistry2D presentationRegistry =
                runtimeRoot.AddComponent<RunPickupPresentationRegistry2D>();
            presentationRegistry.Configure(BuildPresentationEntries());
            presenter = runtimeRoot.AddComponent<RunPickupPresenter2D>();
            presenter.Configure(
                authorityHost,
                presentationRegistry,
                runtimeRoot.transform);
            admissionBridge.ConfigureRuntime(
                new UnityPickupAdmissionRuntimeV1(
                    sourcePositions,
                    pickups.PendingConsumer,
                    presenter));

            EnemyCatalogV1 enemyCatalog;
            PropCatalogV1 propCatalog;
            IRewardProfileResolverV1 rewardProfiles;
            string contentDiagnostic;
            if (!stage1.TryResolveCanonicalTerminalDropContent(
                    out enemyCatalog,
                    out propCatalog,
                    out rewardProfiles,
                    out contentDiagnostic))
            {
                throw new InvalidOperationException(contentDiagnostic);
            }

            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 profile;
            CharacterCompositionCoordinatorV1 composition;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out graph,
                    out profile,
                    out composition)
                || graph == null
                || graph.IsDisposed)
            {
                throw new InvalidOperationException(
                    "The selected character graph is unavailable for exact equipment payload generation.");
            }

            var rewardGeneration = new RewardGenerationServiceV1();
            equipmentPayloads =
                new RetainedTerminalDropEquipmentPayloadAuthority(
                    rewardGeneration,
                    graph.LoadoutRuntime.EquipmentCatalog);
            terminalDrops = TerminalDropBindingCompositionV1.Create(
                enemyCatalog,
                new Stage1EnemyTerminalSourceContextResolverV1(() => run),
                propCatalog,
                new Stage1MissingPropTerminalSourceContextResolverV1(),
                dropRunContext,
                rewardProfiles,
                rewardGeneration,
                new PendingTerminalDropAdmissionAuthorityV1(),
                generationExecutor:
                    new RetainingTerminalDropRewardGenerationExecutor(
                        rewardGeneration,
                        equipmentPayloads));

            ConfigureCollector();
            observedLifecycleGeneration = run.LifecycleGeneration;
            admissionBridge.RetireOtherLifecycles(
                run.RunStableId,
                run.LifecycleGeneration);
            ProcessCanonicalEnemyTerminalFacts();
            admissionBridge.ProcessPending();
            presenter.Synchronize(stage1.RunPickupRooms.CurrentRoomStableId);
        }

        private void TryRefreshLifecycle(RunSessionAggregateV1 shared)
        {
            try
            {
                if (!ReferenceEquals(run, shared))
                    throw new InvalidOperationException(
                        "Pickup lifecycle refresh requires the same shared aggregate.");
                observedLifecycleGeneration = shared.LifecycleGeneration;
                dropRunContext.Bind(shared);
                deliveredEnemyEvents.Clear();
                quarantinedEnemyEvents.Clear();
                admissionBridge.RetireOtherLifecycles(
                    shared.RunStableId,
                    shared.LifecycleGeneration);
                ConfigureCollector();
                ProcessCanonicalEnemyTerminalFacts();
                admissionBridge.ProcessPending();
                diagnostic = admissionBridge.LastDiagnostic;
            }
            catch (Exception exception)
            {
                diagnostic = "Stage 1 pickup lifecycle refresh failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void ConfigureCollector()
        {
            RunPlayerRuntimeSnapshotV1 playerSnapshot =
                run.RuntimePorts.Player.ExportSnapshot();
            RunPickupCollector2D collector = stage1.RunPickupController
                .PlayerTransform.GetComponent<RunPickupCollector2D>();
            if (collector == null)
            {
                collector = stage1.RunPickupController.PlayerTransform
                    .gameObject.AddComponent<RunPickupCollector2D>();
            }
            collector.Configure(
                playerSnapshot.ActorInstanceStableId,
                playerSnapshot.ParticipantStableId);
        }

        private void ProcessCanonicalEnemyTerminalFacts()
        {
            if (stage1 == null || run == null || terminalDrops == null) return;

            IReadOnlyList<Stage1CanonicalEnemyTerminalFactV1> facts;
            string exportDiagnostic;
            if (!stage1.TryExportCanonicalEnemyTerminalFacts(
                    out facts,
                    out exportDiagnostic))
            {
                diagnostic = exportDiagnostic;
                return;
            }

            for (int index = 0; index < facts.Count; index++)
                ProcessCanonicalEnemyTerminalFact(facts[index]);
        }

        private void ProcessCanonicalEnemyTerminalFact(
            Stage1CanonicalEnemyTerminalFactV1 terminal)
        {
            if (terminal == null
                || terminal.Fact == null
                || terminal.Fact.DeathEventStableId == null)
            {
                return;
            }

            StableId eventId = terminal.Fact.DeathEventStableId;
            if (deliveredEnemyEvents.ContainsKey(eventId)
                || quarantinedEnemyEvents.ContainsKey(eventId))
            {
                return;
            }

            admissionBridge.RegisterFixedSource(
                run.RunStableId,
                run.LifecycleGeneration,
                terminal.Fact.Identity.EntityInstanceId,
                terminal.PlacementStableId,
                terminal.RoomStableId,
                terminal.TerminalPosition,
                terminal.PositionFingerprint);

            GeneratedTerminalDropResultV1 generated;
            try
            {
                generated = terminalDrops.Authority.Generate(terminal.Fact);
            }
            catch (Exception exception)
            {
                diagnostic = "stage1-enemy-terminal-generation-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
                return;
            }

            if (generated == null || !generated.IsAccepted)
            {
                string failure = generated == null
                    ? "stage1-enemy-terminal-generation-null"
                    : generated.Diagnostic;
                if (generated != null
                    && !IsRetryableTerminalGeneration(generated))
                {
                    quarantinedEnemyEvents[eventId] = failure;
                }
                diagnostic = failure;
                return;
            }

            try
            {
                lastEnemyAdmission = terminalDrops.PendingAdmission.Admit(generated);
            }
            catch (Exception exception)
            {
                diagnostic = "stage1-enemy-terminal-admission-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
                return;
            }

            if (lastEnemyAdmission == null || !lastEnemyAdmission.IsAccepted)
            {
                string failure = lastEnemyAdmission == null
                    ? "stage1-enemy-terminal-admission-null"
                    : lastEnemyAdmission.Diagnostic;
                if (lastEnemyAdmission != null
                    && lastEnemyAdmission.Status
                        == PendingTerminalDropAdmissionStatusV1.ConflictingDuplicate)
                {
                    quarantinedEnemyEvents[eventId] = failure;
                }
                diagnostic = failure;
                return;
            }

            PickupDeliveryResultV1 queued =
                admissionBridge.TryEnqueue(lastEnemyAdmission);
            if (queued != null && queued.IsAcknowledged)
            {
                deliveredEnemyEvents[eventId] = generated.Fingerprint;
            }
            else if (queued != null
                && (queued.Disposition == PickupDeliveryDispositionV1.Rejected
                    || queued.Disposition
                        == PickupDeliveryDispositionV1.ConflictingDuplicate))
            {
                quarantinedEnemyEvents[eventId] = queued.Diagnostic;
            }
            diagnostic = queued == null
                ? "stage1-enemy-terminal-queue-null"
                : queued.Diagnostic;
        }

        private static bool IsRetryableTerminalGeneration(
            GeneratedTerminalDropResultV1 result)
        {
            if (result == null) return true;
            return result.RejectionCode == TerminalDropRejectionCodeV1.MissingRun
                || result.RejectionCode
                    == TerminalDropRejectionCodeV1.MissingSourceContext;
        }

        private IEnumerable<RunPickupPresentationEntryV1>
            BuildPresentationEntries()
        {
            return new[]
            {
                Presentation(
                    RewardGrantKindV1.Money,
                    "Money",
                    new Color(1f, 0.85f, 0.15f, 1f)),
                Presentation(
                    RewardGrantKindV1.Scrap,
                    "Scrap",
                    new Color(0.6f, 0.85f, 1f, 1f)),
                Presentation(
                    RewardGrantKindV1.Strongbox,
                    "Strongbox",
                    new Color(0.2f, 1f, 0.45f, 1f)),
                Presentation(
                    RewardGrantKindV1.EquipmentReference,
                    "Equipment",
                    new Color(0.85f, 0.35f, 1f, 1f)),
            };
        }

        private RunPickupPresentationEntryV1 Presentation(
            RewardGrantKindV1 kind,
            string label,
            Color color)
        {
            var texture = new Texture2D(
                8,
                8,
                TextureFormat.RGBA32,
                false);
            Color[] pixels = new Color[64];
            for (int index = 0; index < pixels.Length; index++)
                pixels[index] = color;
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                new Vector2(0.5f, 0.5f),
                16f);
            texture.name = "Stage1" + label + "PickupTexture";
            sprite.name = "Stage1" + label + "PickupSprite";
            runtimeAssets.Add(texture);
            runtimeAssets.Add(sprite);

            var entry = new RunPickupPresentationEntryV1();
            entry.Configure(
                kind,
                null,
                null,
                sprite,
                Vector3.one,
                0.75f,
                label);
            return entry;
        }

        private void TeardownProjection(bool clearDeliveryState)
        {
            admissionBridge.ReleaseRuntime();
            if (runtimeRoot != null)
            {
                runtimeRoot.SetActive(false);
                Destroy(runtimeRoot);
            }
            for (int index = 0; index < runtimeAssets.Count; index++)
            {
                if (runtimeAssets[index] != null)
                    Destroy(runtimeAssets[index]);
            }
            runtimeAssets.Clear();
            runtimeRoot = null;
            sourcePositions = null;
            pickups = null;
            terminalDrops = null;
            equipmentPayloads = null;
            presenter = null;
            observedLifecycleGeneration = -1L;
            lastEnemyAdmission = null;
            if (clearDeliveryState)
            {
                admissionBridge.ClearAll();
                deliveredEnemyEvents.Clear();
                quarantinedEnemyEvents.Clear();
            }
            run = null;
        }

        private void OnDestroy()
        {
            TeardownProjection(true);
        }
    }
}
