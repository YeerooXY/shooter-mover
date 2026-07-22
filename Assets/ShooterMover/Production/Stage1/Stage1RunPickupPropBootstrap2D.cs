using System;
using System.Collections;
using System.Collections.Generic;
using ShooterMover.Application.Runs.Session;
using ShooterMover.ContentPackages.Props.DestructibleProps;
using ShooterMover.Domain.Common;
using ShooterMover.TerminalDropBinding;
using ShooterMover.TestSupport.VisibleSlice;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Transactional Stage 1 prop adapter. The immutable destruction fact is resolved
    /// once, then every eligible participant result is delivered through the same
    /// shared reward/pacing/pickup composition used by enemy terminal facts.
    /// </summary>
    [DefaultExecutionOrder(21100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Stage1RunPickupBootstrap2D))]
    public sealed class Stage1RunPickupPropBootstrap2D : MonoBehaviour
    {
        private sealed class PendingPropTerminal
        {
            public PendingPropTerminal(DestructiblePropTerminalEvent2D terminal)
            {
                Terminal = terminal
                    ?? throw new ArgumentNullException(nameof(terminal));
            }

            public DestructiblePropTerminalEvent2D Terminal { get; }
            public Stage1CanonicalPropDestructionFactV1 Fact { get; private set; }

            public void BindFact(Stage1CanonicalPropDestructionFactV1 fact)
            {
                if (Fact != null)
                {
                    throw new InvalidOperationException(
                        "A pending prop terminal already has a canonical fact.");
                }
                Fact = fact ?? throw new ArgumentNullException(nameof(fact));
            }
        }

        private Stage1VisibleSliceController controller;
        private Stage1PlayableLoopCompositionV1 stage1;
        private Stage1RunPickupBootstrap2D pickupBootstrap;
        private RunSessionAggregateV1 observedRun;
        private TerminalDropGenerationAuthorityV1 generation;
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

        private IEnumerator Start()
        {
            controller = GetComponent<Stage1VisibleSliceController>();
            stage1 = GetComponent<Stage1PlayableLoopCompositionV1>();
            pickupBootstrap = GetComponent<Stage1RunPickupBootstrap2D>();
            while (pickupBootstrap == null || !pickupBootstrap.IsComposed)
            {
                if (pickupBootstrap == null)
                {
                    pickupBootstrap = GetComponent<Stage1RunPickupBootstrap2D>();
                }
                yield return null;
            }
            TryCompose();
        }

        private void LateUpdate()
        {
            if (pickupBootstrap == null)
            {
                pickupBootstrap = GetComponent<Stage1RunPickupBootstrap2D>();
            }
            if (pickupBootstrap == null || !pickupBootstrap.IsComposed) return;
            if (!ReferenceEquals(observedRun, pickupBootstrap.RunSession))
            {
                TryCompose();
            }
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
            {
                controller = GetComponent<Stage1VisibleSliceController>();
            }
            if (stage1 == null)
            {
                stage1 = GetComponent<Stage1PlayableLoopCompositionV1>();
            }
            if (pickupBootstrap == null)
            {
                pickupBootstrap = GetComponent<Stage1RunPickupBootstrap2D>();
            }
            if (stage1 == null
                || pickupBootstrap == null
                || !pickupBootstrap.IsComposed
                || pickupBootstrap.RunSession == null
                || pickupBootstrap.TerminalDrops == null)
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
                LastAdmission = null;
            }
            observedRun = nextRun;
            generation = pickupBootstrap.TerminalDrops.Authority;

            DestructibleProp2D[] props =
                controller.GetComponentsInChildren<DestructibleProp2D>(true);
            for (int index = 0; index < props.Length; index++)
            {
                DestructibleProp2D prop = props[index];
                if (prop == null || !prop.IsConfigured || prop.PropId == null)
                {
                    continue;
                }
                prop.TerminalDestroyed += HandleTerminalDestroyed;
                subscribedProps.Add(prop);
            }
            if (subscribedProps.Count == 0)
            {
                throw new InvalidOperationException(
                    "No configured Stage 1 destructible prop was available.");
            }
        }

        private void HandleTerminalDestroyed(
            DestructiblePropTerminalEvent2D terminal)
        {
            DestructiblePropDestructionResult destruction =
                terminal == null ? null : terminal.Destruction;
            if (destruction == null || destruction.EventId == null) return;
            if (!pendingTerminalByEvent.ContainsKey(destruction.EventId)
                && !quarantinedTerminalByEvent.ContainsKey(destruction.EventId))
            {
                pendingTerminalByEvent.Add(
                    destruction.EventId,
                    new PendingPropTerminal(terminal));
            }
            ProcessPendingTerminals();
        }

        private void ProcessPendingTerminals()
        {
            if (pickupBootstrap == null
                || !pickupBootstrap.IsComposed
                || observedRun == null
                || generation == null)
            {
                return;
            }

            var events = new List<StableId>(pendingTerminalByEvent.Keys);
            for (int index = 0; index < events.Count; index++)
            {
                ProcessPendingTerminal(events[index]);
            }
        }

        private void ProcessPendingTerminal(StableId eventId)
        {
            PendingPropTerminal record;
            if (!pendingTerminalByEvent.TryGetValue(eventId, out record)
                || record == null
                || record.Terminal == null
                || record.Terminal.Destruction == null)
            {
                Quarantine(eventId, "stage1-prop-terminal-record-invalid");
                return;
            }

            if (record.Fact == null && !TryBindCanonicalFact(record))
            {
                return;
            }

            Stage1CanonicalPropDestructionFactV1 fact = record.Fact;
            if (fact.RunStableId != observedRun.RunStableId
                || fact.LifecycleGeneration != observedRun.LifecycleGeneration)
            {
                Quarantine(eventId, "stage1-prop-terminal-stale-lifecycle");
                return;
            }

            TerminalPersonalRewardBatchV1 batch;
            try
            {
                batch = generation.GenerateBatch(fact);
            }
            catch (Exception exception)
            {
                diagnostic = "stage1-prop-generation-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
                return;
            }
            if (batch == null || !batch.IsAccepted)
            {
                string failure = batch == null
                    ? "stage1-prop-generation-null"
                    : batch.Diagnostic;
                if (!IsRetryableGeneration(batch))
                {
                    Quarantine(eventId, failure);
                }
                else
                {
                    diagnostic = failure;
                }
                return;
            }

            Stage1PersonalRewardBatchDeliveryResultV1 delivery =
                pickupBootstrap.DeliverPersonalRewardBatch(
                    batch,
                    fact.RoomStableId,
                    fact.TerminalPosition,
                    fact.PositionFingerprint);
            LastAdmission = delivery.LastAdmission;
            if (delivery.Succeeded)
            {
                pendingTerminalByEvent.Remove(eventId);
                diagnostic = delivery.Diagnostic;
            }
            else if (delivery.TerminalConflict)
            {
                Quarantine(eventId, delivery.Diagnostic);
            }
            else
            {
                diagnostic = delivery.Diagnostic;
            }
        }

        private bool TryBindCanonicalFact(PendingPropTerminal record)
        {
            if (record == null
                || record.Terminal == null
                || record.Terminal.Destruction == null)
            {
                return false;
            }
            if (observedRun == null || stage1 == null)
            {
                diagnostic = "stage1-prop-terminal-shared-run-unavailable";
                return false;
            }

            DestructiblePropTerminalEvent2D terminal = record.Terminal;
            Stage1CanonicalPropTerminalSourceV1 provenance;
            string provenanceDiagnostic;
            if (!stage1.TryResolveCanonicalPropTerminalSource(
                    terminal.Provenance,
                    terminal.Destruction.PropId,
                    out provenance,
                    out provenanceDiagnostic))
            {
                if (provenanceDiagnostic.StartsWith(
                        "stage1-terminal-content-unavailable",
                        StringComparison.Ordinal))
                {
                    diagnostic = provenanceDiagnostic;
                }
                else
                {
                    Quarantine(
                        terminal.Destruction.EventId,
                        provenanceDiagnostic);
                }
                return false;
            }

            StableId attributedParticipant;
            stage1.TryResolveTerminalParticipant(
                terminal.Destruction.SourceId,
                out attributedParticipant);
            record.BindFact(
                new Stage1CanonicalPropDestructionFactV1(
                    terminal.Destruction,
                    provenance,
                    observedRun.RunStableId,
                    observedRun.LifecycleGeneration,
                    attributedParticipant,
                    terminal.TerminalPosition,
                    terminal.PositionFingerprint));
            return true;
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
            TerminalPersonalRewardBatchV1 batch)
        {
            if (batch == null) return true;
            string value = batch.Diagnostic ?? string.Empty;
            return value.IndexOf("run-context", StringComparison.Ordinal) >= 0
                || value.IndexOf("run-roster", StringComparison.Ordinal) >= 0
                || value.IndexOf("environment-not-configured", StringComparison.Ordinal) >= 0
                || value.IndexOf("source-context", StringComparison.Ordinal) >= 0;
        }

        private void ReleaseBindings()
        {
            for (int index = 0; index < subscribedProps.Count; index++)
            {
                DestructibleProp2D prop = subscribedProps[index];
                if (prop != null)
                {
                    prop.TerminalDestroyed -= HandleTerminalDestroyed;
                }
            }
            subscribedProps.Clear();
            observedRun = null;
            generation = null;
        }

        private void OnDestroy()
        {
            ReleaseBindings();
            pendingTerminalByEvent.Clear();
            quarantinedTerminalByEvent.Clear();
            LastAdmission = null;
        }
    }
}
