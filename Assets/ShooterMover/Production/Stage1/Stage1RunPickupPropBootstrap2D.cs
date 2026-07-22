using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Runs.Session;
using ShooterMover.ContentPackages.Props.DestructibleProps;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Props;
using ShooterMover.TerminalDropBinding;
using ShooterMover.TestSupport.VisibleSlice;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    internal sealed class Stage1CanonicalPropDestructionFactV1
    {
        public Stage1CanonicalPropDestructionFactV1(
            DestructiblePropDestructionResult destruction,
            Stage1CanonicalPropTerminalSourceV1 provenance,
            StableId runStableId,
            long lifecycleGeneration,
            StableId roomStableId,
            StableId attributedParticipantStableId,
            Vector2 terminalPosition)
        {
            Destruction = destruction
                ?? throw new ArgumentNullException(nameof(destruction));
            Provenance = provenance
                ?? throw new ArgumentNullException(nameof(provenance));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (lifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            LifecycleGeneration = lifecycleGeneration;
            AttributedParticipantStableId = attributedParticipantStableId;
            TerminalPosition = terminalPosition;
        }

        public DestructiblePropDestructionResult Destruction { get; }
        public Stage1CanonicalPropTerminalSourceV1 Provenance { get; }
        public StableId RunStableId { get; }
        public long LifecycleGeneration { get; }
        public StableId RoomStableId { get; }
        public StableId AttributedParticipantStableId { get; }
        public Vector2 TerminalPosition { get; }
    }

    /// <summary>
    /// Retained transactional adapter for Stage 1 destructible props. The one-shot Destroyed
    /// callback captures an immutable canonical fact; generation/admission/realization retry from
    /// that fact. Definition and drop provenance come from the production prop catalog.
    /// </summary>
    [DefaultExecutionOrder(21100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Stage1RunPickupBootstrap2D))]
    public sealed class Stage1RunPickupPropBootstrap2D : MonoBehaviour
    {
        private sealed class PendingPropTerminal
        {
            public PendingPropTerminal(Stage1CanonicalPropDestructionFactV1 fact)
            {
                Fact = fact ?? throw new ArgumentNullException(nameof(fact));
            }

            public Stage1CanonicalPropDestructionFactV1 Fact { get; }
        }

        private Stage1VisibleSliceController controller;
        private Stage1PlayableLoopCompositionV1 stage1;
        private Stage1RunPickupBootstrap2D pickupBootstrap;
        private RunSessionAggregateV1 observedRun;
        private TerminalDropGenerationAuthorityV1 generation;
        private PendingTerminalDropAdmissionAuthorityV1 pending;
        private readonly List<DestructibleProp2D> subscribedProps =
            new List<DestructibleProp2D>();
        private readonly Dictionary<StableId, PendingPropTerminal>
            pendingTerminalByEvent =
                new Dictionary<StableId, PendingPropTerminal>();
        private readonly Dictionary<StableId, string> quarantinedTerminalByEvent =
            new Dictionary<StableId, string>();
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
        public int QuarantinedTerminalCount
        {
            get { return quarantinedTerminalByEvent.Count; }
        }
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
                        value.gameObject.AddComponent<Stage1RunPickupBootstrap2D>();
                    if (value.GetComponent<Stage1RunPickupPropBootstrap2D>() == null)
                        value.gameObject.AddComponent<Stage1RunPickupPropBootstrap2D>();
                }
            }
        }

        private IEnumerator Start()
        {
            controller = GetComponent<Stage1VisibleSliceController>();
            stage1 = GetComponent<Stage1PlayableLoopCompositionV1>();
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
            if (stage1 == null)
                stage1 = GetComponent<Stage1PlayableLoopCompositionV1>();
            if (pickupBootstrap == null)
                pickupBootstrap = GetComponent<Stage1RunPickupBootstrap2D>();
            if (stage1 == null
                || pickupBootstrap == null
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
                quarantinedTerminalByEvent.Clear();
                generation = null;
                pending = null;
                LastAdmission = null;
            }
            observedRun = nextRun;

            EnemyCatalogV1 ignoredEnemies;
            PropCatalogV1 ignoredProps;
            IRewardProfileResolverV1 rewardProfiles;
            string contentDiagnostic;
            if (!stage1.TryResolveCanonicalTerminalDropContent(
                    out ignoredEnemies,
                    out ignoredProps,
                    out rewardProfiles,
                    out contentDiagnostic))
            {
                throw new InvalidOperationException(contentDiagnostic);
            }

            if (pending == null)
                pending = new PendingTerminalDropAdmissionAuthorityV1();
            if (generation == null)
            {
                generation = new TerminalDropGenerationAuthorityV1(
                    new TerminalDropFactAdapterRegistryV1(
                        new ITerminalDropFactAdapterV1[]
                        {
                            new Stage1CanonicalPropTerminalDropFactAdapterV1(),
                        }),
                    new Stage1PickupTerminalDropRunContextResolverV1(
                        () => observedRun,
                        () => 1),
                    rewardProfiles,
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
                throw new InvalidOperationException(
                    "No configured Stage 1 destructible prop was available.");
        }

        private void HandleDestroyed(
            DestructiblePropDestructionResult destruction)
        {
            if (destruction == null || destruction.EventId == null) return;
            RunSessionAggregateV1 current = observedRun
                ?? (pickupBootstrap == null ? null : pickupBootstrap.RunSession);
            if (current == null || stage1 == null)
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
            Stage1CanonicalPropTerminalSourceV1 provenance;
            string provenanceDiagnostic;
            if (!stage1.TryResolveCanonicalPropTerminalSource(
                    source,
                    out provenance,
                    out provenanceDiagnostic))
            {
                Quarantine(destruction.EventId, provenanceDiagnostic);
                return;
            }

            StableId roomStableId = controller.CurrentRoomStableId;
            if (roomStableId == null)
            {
                diagnostic = "stage1-prop-pickup-room-unavailable";
                return;
            }

            Vector2 position;
            try
            {
                position = source.BlockingCollider == null
                    ? (Vector2)source.transform.position
                    : source.BlockingCollider.bounds.center;
            }
            catch (Exception exception)
            {
                diagnostic = "stage1-prop-position-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
                return;
            }

            StableId attributedParticipant;
            stage1.TryResolveTerminalParticipant(
                destruction.SourceId,
                out attributedParticipant);
            if (!pendingTerminalByEvent.ContainsKey(destruction.EventId)
                && !quarantinedTerminalByEvent.ContainsKey(destruction.EventId))
            {
                pendingTerminalByEvent.Add(
                    destruction.EventId,
                    new PendingPropTerminal(
                        new Stage1CanonicalPropDestructionFactV1(
                            destruction,
                            provenance,
                            current.RunStableId,
                            current.LifecycleGeneration,
                            roomStableId,
                            attributedParticipant,
                            position)));
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
                ProcessPendingTerminal(events[index]);
        }

        private void ProcessPendingTerminal(StableId eventId)
        {
            PendingPropTerminal record;
            if (!pendingTerminalByEvent.TryGetValue(eventId, out record)
                || record == null
                || record.Fact == null)
            {
                Quarantine(eventId, "stage1-prop-terminal-record-invalid");
                return;
            }

            Stage1CanonicalPropDestructionFactV1 fact = record.Fact;
            if (fact.RunStableId != observedRun.RunStableId
                || fact.LifecycleGeneration != observedRun.LifecycleGeneration)
            {
                Quarantine(eventId, "stage1-prop-terminal-stale-lifecycle");
                return;
            }

            GeneratedTerminalDropResultV1 generated;
            try
            {
                generated = generation.Generate(fact);
            }
            catch (Exception exception)
            {
                diagnostic = "stage1-prop-generation-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
                return;
            }
            if (generated == null || !generated.IsAccepted)
            {
                string failure = generated == null
                    ? "stage1-prop-generation-null"
                    : generated.Diagnostic;
                if (generated != null && !IsRetryableGeneration(generated))
                    Quarantine(eventId, failure);
                else
                    diagnostic = failure;
                return;
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
                return;
            }
            if (LastAdmission == null || !LastAdmission.IsAccepted)
            {
                string failure = LastAdmission == null
                    ? "stage1-prop-admission-null"
                    : LastAdmission.Diagnostic;
                if (LastAdmission != null
                    && LastAdmission.Status
                        == PendingTerminalDropAdmissionStatusV1.ConflictingDuplicate)
                {
                    Quarantine(eventId, failure);
                }
                else
                {
                    diagnostic = failure;
                }
                return;
            }

            TerminalDropSourceFactV1 sourceFact =
                LastAdmission.PendingResult.SourceFact;
            string positionFingerprint = RunSessionFingerprintV1.Hash(
                sourceFact.TerminalEventStableId
                + "|"
                + fact.TerminalPosition.x.ToString(
                    "R",
                    CultureInfo.InvariantCulture)
                + "|"
                + fact.TerminalPosition.y.ToString(
                    "R",
                    CultureInfo.InvariantCulture));
            try
            {
                pickupBootstrap.RegisterFixedSource(
                    sourceFact.RunStableId,
                    sourceFact.RunLifecycleGeneration,
                    sourceFact.SourceEntityStableId,
                    sourceFact.SourcePlacementStableId,
                    fact.RoomStableId,
                    fact.TerminalPosition,
                    positionFingerprint);
                Stage1PickupDeliveryResultV1 queued =
                    pickupBootstrap.EnqueueAdmission(LastAdmission);
                if (queued != null && queued.IsAcknowledged)
                {
                    pendingTerminalByEvent.Remove(eventId);
                    diagnostic = queued.Diagnostic;
                }
                else if (queued != null
                    && (queued.Disposition
                        == Stage1PickupDeliveryDispositionV1.Rejected
                        || queued.Disposition
                        == Stage1PickupDeliveryDispositionV1.ConflictingDuplicate))
                {
                    Quarantine(eventId, queued.Diagnostic);
                }
                else
                {
                    diagnostic = queued == null
                        ? "stage1-prop-pickup-enqueue-null"
                        : queued.Diagnostic;
                }
            }
            catch (Exception exception)
            {
                diagnostic = "stage1-prop-pickup-enqueue-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
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

        private void Quarantine(StableId eventId, string failure)
        {
            if (eventId == null) return;
            pendingTerminalByEvent.Remove(eventId);
            if (!quarantinedTerminalByEvent.ContainsKey(eventId))
            {
                quarantinedTerminalByEvent.Add(
                    eventId,
                    string.IsNullOrWhiteSpace(failure)
                        ? "stage1-prop-terminal-rejected"
                        : failure);
            }
            diagnostic = quarantinedTerminalByEvent[eventId];
        }

        private static bool IsRetryableGeneration(
            GeneratedTerminalDropResultV1 generated)
        {
            return generated != null
                && (generated.RejectionCode
                        == TerminalDropRejectionCodeV1.MissingRun
                    || generated.RejectionCode
                        == TerminalDropRejectionCodeV1.MissingSourceContext);
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
            quarantinedTerminalByEvent.Clear();
            generation = null;
            pending = null;
            LastAdmission = null;
        }
    }

    internal sealed class Stage1CanonicalPropTerminalDropFactAdapterV1 :
        ITerminalDropFactAdapterV1
    {
        public StableId FactKindStableId
        {
            get { return TerminalDropFactKindIdsV1.PropDestruction; }
        }
        public Type FactType
        {
            get { return typeof(Stage1CanonicalPropDestructionFactV1); }
        }

        public TerminalDropAdaptationResultV1 Adapt(object terminalFact)
        {
            var fact = terminalFact as Stage1CanonicalPropDestructionFactV1;
            if (fact == null
                || fact.Destruction == null
                || fact.Provenance == null)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    "stage1-prop-terminal-canonical-fact-invalid");
            }

            DestructiblePropDestructionResult destruction = fact.Destruction;
            string canonical = destruction.EventId
                + "|"
                + destruction.PropId
                + "|"
                + destruction.SourceId
                + "|"
                + fact.Provenance.Definition.DefinitionId
                + "|"
                + fact.Provenance.DropProfileStableId
                + "|"
                + fact.RoomStableId
                + "|"
                + fact.TerminalPosition.x.ToString(
                    "R",
                    CultureInfo.InvariantCulture)
                + "|"
                + fact.TerminalPosition.y.ToString(
                    "R",
                    CultureInfo.InvariantCulture);
            return TerminalDropAdaptationResultV1.Accepted(
                new TerminalDropSourceFactV1(
                    TerminalDropFactKindIdsV1.PropDestruction,
                    destruction.EventId,
                    destruction.EventId,
                    fact.RunStableId,
                    fact.LifecycleGeneration,
                    destruction.PropId,
                    destruction.PropId,
                    fact.LifecycleGeneration,
                    fact.Provenance.Definition.DefinitionId,
                    fact.AttributedParticipantStableId,
                    destruction.SourceId,
                    StableId.Create(
                        "damage",
                        "combat-channel-"
                            + ((int)destruction.Channel).ToString(
                                CultureInfo.InvariantCulture)),
                    fact.Provenance.DropProfileStableId,
                    RunSessionFingerprintV1.Hash("source|" + canonical),
                    fact.Provenance.DefinitionFingerprint,
                    RunSessionFingerprintV1.Hash("upstream|" + canonical)));
        }
    }
}
