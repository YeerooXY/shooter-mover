using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.RunPickups;
using ShooterMover.TerminalDropBinding;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    public enum Stage1PickupDeliveryDispositionV1
    {
        Applied = 1,
        ExactReplay = 2,
        Retryable = 3,
        Rejected = 4,
        ConflictingDuplicate = 5,
    }

    public sealed class Stage1PickupDeliveryResultV1
    {
        public Stage1PickupDeliveryResultV1(
            Stage1PickupDeliveryDispositionV1 disposition,
            PendingTerminalDropAdmissionResultV1 admission,
            string diagnostic)
        {
            Disposition = disposition;
            Admission = admission;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public Stage1PickupDeliveryDispositionV1 Disposition { get; }
        public PendingTerminalDropAdmissionResultV1 Admission { get; }
        public string Diagnostic { get; }
        public bool IsAcknowledged
        {
            get
            {
                return Disposition == Stage1PickupDeliveryDispositionV1.Applied
                    || Disposition == Stage1PickupDeliveryDispositionV1.ExactReplay;
            }
        }
    }

    public sealed class Stage1PickupSourcePositionV1
    {
        public Stage1PickupSourcePositionV1(
            StableId roomStableId,
            Vector2 position,
            string fingerprint)
        {
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                throw new ArgumentException(
                    "A source-position fingerprint is required.",
                    nameof(fingerprint));
            }
            Position = position;
            Fingerprint = fingerprint.Trim();
        }

        public StableId RoomStableId { get; }
        public Vector2 Position { get; }
        public string Fingerprint { get; }
    }

    public interface IStage1PickupSourcePositionResolverV1
    {
        bool TryResolve(
            out Stage1PickupSourcePositionV1 position,
            out string diagnostic);
    }

    public sealed class TransformStage1PickupSourcePositionResolverV1 :
        IStage1PickupSourcePositionResolverV1
    {
        private readonly StableId roomStableId;
        private readonly Transform sourceTransform;
        private readonly StableId terminalEventStableId;

        public TransformStage1PickupSourcePositionResolverV1(
            StableId roomStableId,
            Transform sourceTransform,
            StableId terminalEventStableId)
        {
            this.roomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            this.sourceTransform = sourceTransform
                ?? throw new ArgumentNullException(nameof(sourceTransform));
            this.terminalEventStableId = terminalEventStableId;
        }

        public bool TryResolve(
            out Stage1PickupSourcePositionV1 position,
            out string diagnostic)
        {
            position = null;
            diagnostic = string.Empty;
            if (sourceTransform == null)
            {
                diagnostic = "stage1-pickup-terminal-source-transform-missing";
                return false;
            }

            try
            {
                Vector2 value = sourceTransform.position;
                string fingerprint = RunSessionFingerprintV1.Hash(
                    (terminalEventStableId == null
                        ? "terminal-event-unbound"
                        : terminalEventStableId.ToString())
                    + "|"
                    + value.x.ToString("R", CultureInfo.InvariantCulture)
                    + "|"
                    + value.y.ToString("R", CultureInfo.InvariantCulture));
                position = new Stage1PickupSourcePositionV1(
                    roomStableId,
                    value,
                    fingerprint);
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = "stage1-pickup-source-transform-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
                return false;
            }
        }
    }

    public sealed class FixedStage1PickupSourcePositionResolverV1 :
        IStage1PickupSourcePositionResolverV1
    {
        private readonly Stage1PickupSourcePositionV1 value;

        public FixedStage1PickupSourcePositionResolverV1(
            Stage1PickupSourcePositionV1 value)
        {
            this.value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool TryResolve(
            out Stage1PickupSourcePositionV1 position,
            out string diagnostic)
        {
            position = value;
            diagnostic = string.Empty;
            return true;
        }
    }

    public interface IStage1PickupAdmissionRuntimeV1
    {
        bool TryRegisterPosition(
            TerminalDropSourceFactV1 source,
            Stage1PickupSourcePositionV1 position,
            out string diagnostic);

        RunPickupRealizationResultV1 Realize(
            PendingTerminalDropAdmissionResultV1 admission);

        RunPickupPresentationSyncResultV1 Synchronize(
            StableId roomStableId);
    }

    internal sealed class Stage1UnityPickupAdmissionRuntimeV1 :
        IStage1PickupAdmissionRuntimeV1
    {
        private readonly RunPickupSourcePositionRegistry2D sourcePositions;
        private readonly PendingTerminalDropPickupConsumerV1 pickupConsumer;
        private readonly RunPickupPresenter2D presenter;

        public Stage1UnityPickupAdmissionRuntimeV1(
            RunPickupSourcePositionRegistry2D sourcePositions,
            PendingTerminalDropPickupConsumerV1 pickupConsumer,
            RunPickupPresenter2D presenter)
        {
            this.sourcePositions = sourcePositions
                ?? throw new ArgumentNullException(nameof(sourcePositions));
            this.pickupConsumer = pickupConsumer
                ?? throw new ArgumentNullException(nameof(pickupConsumer));
            this.presenter = presenter
                ?? throw new ArgumentNullException(nameof(presenter));
        }

        public bool TryRegisterPosition(
            TerminalDropSourceFactV1 source,
            Stage1PickupSourcePositionV1 position,
            out string diagnostic)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (position == null)
                throw new ArgumentNullException(nameof(position));
            return sourcePositions.Register(
                source.RunStableId,
                source.RunLifecycleGeneration,
                source.SourceEntityStableId,
                source.SourcePlacementStableId,
                position.RoomStableId,
                position.Position,
                position.Fingerprint,
                out diagnostic);
        }

        public RunPickupRealizationResultV1 Realize(
            PendingTerminalDropAdmissionResultV1 admission)
        {
            return pickupConsumer.Consume(admission);
        }

        public RunPickupPresentationSyncResultV1 Synchronize(
            StableId roomStableId)
        {
            return presenter.Synchronize(roomStableId);
        }
    }

    /// <summary>
    /// Retained transactional delivery queue. Accepted #277 admissions are keyed by exact
    /// DROP operation and batch fingerprint, then retried until source registration,
    /// authoritative realization, and presentation synchronization all acknowledge them.
    /// Releasing and reconfiguring Unity dependencies does not discard queued admissions.
    /// </summary>
    public sealed class Stage1PendingAdmissionPickupBridgeV1 :
        IPendingTerminalDropAdmissionConsumerV1
    {
        private sealed class SourceBinding
        {
            public SourceBinding(IStage1PickupSourcePositionResolverV1 resolver)
            {
                Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            }

            public IStage1PickupSourcePositionResolverV1 Resolver { get; }
        }

        private sealed class DeliveryRecord
        {
            public DeliveryRecord(PendingTerminalDropAdmissionResultV1 admission)
            {
                Admission = admission;
            }

            public PendingTerminalDropAdmissionResultV1 Admission { get; }
        }

        private readonly object gate = new object();
        private readonly Dictionary<string, SourceBinding> sources =
            new Dictionary<string, SourceBinding>(StringComparer.Ordinal);
        private readonly Dictionary<StableId, DeliveryRecord> pendingByOperation =
            new Dictionary<StableId, DeliveryRecord>();
        private readonly Dictionary<StableId, string> completedByOperation =
            new Dictionary<StableId, string>();
        private IStage1PickupAdmissionRuntimeV1 runtime;

        public int PendingCount
        {
            get
            {
                lock (gate)
                {
                    return pendingByOperation.Count;
                }
            }
        }

        public int CompletedCount
        {
            get
            {
                lock (gate)
                {
                    return completedByOperation.Count;
                }
            }
        }

        public string LastDiagnostic { get; private set; } = string.Empty;

        public void ConfigureRuntime(IStage1PickupAdmissionRuntimeV1 runtime)
        {
            lock (gate)
            {
                this.runtime = runtime
                    ?? throw new ArgumentNullException(nameof(runtime));
            }
        }

        public void ReleaseRuntime()
        {
            lock (gate)
            {
                runtime = null;
            }
        }

        public void RegisterSource(
            StableId runStableId,
            long lifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId,
            IStage1PickupSourcePositionResolverV1 resolver)
        {
            if (runStableId == null
                || lifecycleGeneration <= 0L
                || sourceEntityStableId == null
                || resolver == null)
            {
                throw new ArgumentException(
                    "A complete terminal source binding is required.");
            }

            lock (gate)
            {
                sources[Key(
                    runStableId,
                    lifecycleGeneration,
                    sourceEntityStableId,
                    sourcePlacementStableId)] = new SourceBinding(resolver);
            }
        }

        public void RegisterTransformSource(
            StableId runStableId,
            long lifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId,
            StableId roomStableId,
            Transform sourceTransform,
            StableId terminalEventStableId = null)
        {
            RegisterSource(
                runStableId,
                lifecycleGeneration,
                sourceEntityStableId,
                sourcePlacementStableId,
                new TransformStage1PickupSourcePositionResolverV1(
                    roomStableId,
                    sourceTransform,
                    terminalEventStableId));
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
            RegisterSource(
                runStableId,
                lifecycleGeneration,
                sourceEntityStableId,
                sourcePlacementStableId,
                new FixedStage1PickupSourcePositionResolverV1(
                    new Stage1PickupSourcePositionV1(
                        roomStableId,
                        position,
                        fingerprint)));
        }

        public void Consume(PendingTerminalDropAdmissionResultV1 admission)
        {
            TryEnqueue(admission);
        }

        public Stage1PickupDeliveryResultV1 TryEnqueue(
            PendingTerminalDropAdmissionResultV1 admission)
        {
            lock (gate)
            {
                LastDiagnostic = string.Empty;
                if (admission == null
                    || !admission.IsAccepted
                    || admission.OperationStableId == null
                    || string.IsNullOrWhiteSpace(admission.BatchFingerprint)
                    || admission.PendingResult == null
                    || admission.PendingResult.SourceFact == null)
                {
                    LastDiagnostic = admission == null
                        ? "stage1-pickup-admission-null"
                        : string.IsNullOrWhiteSpace(admission.Diagnostic)
                            ? "stage1-pickup-admission-not-accepted"
                            : admission.Diagnostic;
                    return Result(
                        Stage1PickupDeliveryDispositionV1.Rejected,
                        admission,
                        LastDiagnostic);
                }

                string completedFingerprint;
                if (completedByOperation.TryGetValue(
                    admission.OperationStableId,
                    out completedFingerprint))
                {
                    bool exact = string.Equals(
                        completedFingerprint,
                        admission.BatchFingerprint,
                        StringComparison.Ordinal);
                    LastDiagnostic = exact
                        ? string.Empty
                        : "stage1-pickup-admission-completed-conflict";
                    return Result(
                        exact
                            ? Stage1PickupDeliveryDispositionV1.ExactReplay
                            : Stage1PickupDeliveryDispositionV1.ConflictingDuplicate,
                        admission,
                        LastDiagnostic);
                }

                DeliveryRecord existing;
                if (pendingByOperation.TryGetValue(
                    admission.OperationStableId,
                    out existing))
                {
                    bool exact = existing != null
                        && existing.Admission != null
                        && string.Equals(
                            existing.Admission.BatchFingerprint,
                            admission.BatchFingerprint,
                            StringComparison.Ordinal);
                    LastDiagnostic = exact
                        ? string.Empty
                        : "stage1-pickup-admission-pending-conflict";
                    return Result(
                        exact
                            ? Stage1PickupDeliveryDispositionV1.ExactReplay
                            : Stage1PickupDeliveryDispositionV1.ConflictingDuplicate,
                        admission,
                        LastDiagnostic);
                }

                pendingByOperation.Add(
                    admission.OperationStableId,
                    new DeliveryRecord(admission));
                return Result(
                    Stage1PickupDeliveryDispositionV1.Applied,
                    admission,
                    string.Empty);
            }
        }

        public int ProcessPending()
        {
            lock (gate)
            {
                if (runtime == null)
                {
                    LastDiagnostic = "stage1-pickup-delivery-runtime-unavailable";
                    return 0;
                }

                int completed = 0;
                var operations = new List<StableId>(pendingByOperation.Keys);
                for (int index = 0; index < operations.Count; index++)
                {
                    StableId operation = operations[index];
                    DeliveryRecord record;
                    if (!pendingByOperation.TryGetValue(operation, out record)
                        || record == null
                        || record.Admission == null
                        || record.Admission.PendingResult == null
                        || record.Admission.PendingResult.SourceFact == null)
                    {
                        LastDiagnostic = "stage1-pickup-delivery-record-invalid";
                        continue;
                    }

                    TerminalDropSourceFactV1 source =
                        record.Admission.PendingResult.SourceFact;
                    SourceBinding binding;
                    if (!sources.TryGetValue(
                            Key(
                                source.RunStableId,
                                source.RunLifecycleGeneration,
                                source.SourceEntityStableId,
                                source.SourcePlacementStableId),
                            out binding)
                        || binding == null
                        || binding.Resolver == null)
                    {
                        LastDiagnostic =
                            "stage1-pickup-terminal-source-binding-missing";
                        continue;
                    }

                    Stage1PickupSourcePositionV1 position;
                    string diagnostic;
                    bool resolved;
                    try
                    {
                        resolved = binding.Resolver.TryResolve(
                            out position,
                            out diagnostic);
                    }
                    catch (Exception exception)
                    {
                        resolved = false;
                        position = null;
                        diagnostic = "stage1-pickup-source-resolution-exception:"
                            + exception.GetType().Name
                            + ":"
                            + exception.Message;
                    }
                    if (!resolved || position == null)
                    {
                        LastDiagnostic = string.IsNullOrWhiteSpace(diagnostic)
                            ? "stage1-pickup-source-position-unavailable"
                            : diagnostic;
                        continue;
                    }

                    bool registered;
                    try
                    {
                        registered = runtime.TryRegisterPosition(
                            source,
                            position,
                            out diagnostic);
                    }
                    catch (Exception exception)
                    {
                        registered = false;
                        diagnostic = "stage1-pickup-position-registration-exception:"
                            + exception.GetType().Name
                            + ":"
                            + exception.Message;
                    }
                    if (!registered)
                    {
                        LastDiagnostic = string.IsNullOrWhiteSpace(diagnostic)
                            ? "stage1-pickup-position-registration-rejected"
                            : diagnostic;
                        continue;
                    }

                    RunPickupRealizationResultV1 realization;
                    try
                    {
                        realization = runtime.Realize(record.Admission);
                    }
                    catch (Exception exception)
                    {
                        LastDiagnostic = "stage1-pickup-realization-exception:"
                            + exception.GetType().Name
                            + ":"
                            + exception.Message;
                        continue;
                    }
                    if (realization == null
                        || (realization.Status
                                != RunPickupRealizationStatusV1.Realized
                            && realization.Status
                                != RunPickupRealizationStatusV1.ExactReplay))
                    {
                        LastDiagnostic = realization == null
                            ? "stage1-pickup-realization-null"
                            : string.IsNullOrWhiteSpace(realization.Diagnostic)
                                ? "stage1-pickup-realization-retryable:"
                                    + realization.Status
                                : realization.Diagnostic;
                        continue;
                    }

                    RunPickupPresentationSyncResultV1 presentation;
                    try
                    {
                        presentation = runtime.Synchronize(position.RoomStableId);
                    }
                    catch (Exception exception)
                    {
                        LastDiagnostic = "stage1-pickup-presentation-exception:"
                            + exception.GetType().Name
                            + ":"
                            + exception.Message;
                        continue;
                    }
                    if (presentation == null || !presentation.Succeeded)
                    {
                        LastDiagnostic = presentation == null
                            ? "stage1-pickup-presentation-unavailable"
                            : string.IsNullOrWhiteSpace(presentation.Diagnostic)
                                ? "stage1-pickup-presentation-retryable"
                                : presentation.Diagnostic;
                        continue;
                    }

                    pendingByOperation.Remove(operation);
                    completedByOperation[operation] =
                        record.Admission.BatchFingerprint;
                    completed++;
                    LastDiagnostic = string.Empty;
                }
                return completed;
            }
        }

        public void RetireOtherLifecycles(
            StableId runStableId,
            long lifecycleGeneration)
        {
            if (runStableId == null || lifecycleGeneration <= 0L) return;
            lock (gate)
            {
                var removePending = new List<StableId>();
                foreach (KeyValuePair<StableId, DeliveryRecord> pair in pendingByOperation)
                {
                    TerminalDropSourceFactV1 source = pair.Value == null
                        || pair.Value.Admission == null
                        || pair.Value.Admission.PendingResult == null
                            ? null
                            : pair.Value.Admission.PendingResult.SourceFact;
                    if (source == null
                        || source.RunStableId != runStableId
                        || source.RunLifecycleGeneration != lifecycleGeneration)
                    {
                        removePending.Add(pair.Key);
                    }
                }
                for (int index = 0; index < removePending.Count; index++)
                    pendingByOperation.Remove(removePending[index]);

                string currentPrefix = runStableId
                    + "|"
                    + lifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                    + "|";
                var removeSources = new List<string>();
                foreach (string key in sources.Keys)
                {
                    if (!key.StartsWith(currentPrefix, StringComparison.Ordinal))
                        removeSources.Add(key);
                }
                for (int index = 0; index < removeSources.Count; index++)
                    sources.Remove(removeSources[index]);
            }
        }

        public void ClearAll()
        {
            lock (gate)
            {
                pendingByOperation.Clear();
                completedByOperation.Clear();
                sources.Clear();
                runtime = null;
                LastDiagnostic = string.Empty;
            }
        }

        private static Stage1PickupDeliveryResultV1 Result(
            Stage1PickupDeliveryDispositionV1 disposition,
            PendingTerminalDropAdmissionResultV1 admission,
            string diagnostic)
        {
            return new Stage1PickupDeliveryResultV1(
                disposition,
                admission,
                diagnostic);
        }

        private static string Key(
            StableId runStableId,
            long lifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId)
        {
            return runStableId
                + "|"
                + lifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                + "|"
                + sourceEntityStableId
                + "|"
                + (sourcePlacementStableId == null
                    ? "none"
                    : sourcePlacementStableId.ToString());
        }
    }
}
