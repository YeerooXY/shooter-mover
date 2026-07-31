using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.RunLoot;
using ShooterMover.LootDropBinding;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.RunLoots
{
    public enum LootSendState
    {
        Applied = 1,
        ExactReplay = 2,
        Retryable = 3,
        Rejected = 4,
        ConflictingDuplicate = 5,
    }

    public sealed class LootSendResult
    {
        public LootSendResult(
            LootSendState disposition,
            PendingLootDropAdmissionResult admission,
            string diagnostic)
        {
            Disposition = disposition;
            Admission = admission;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public LootSendState Disposition { get; }
        public PendingLootDropAdmissionResult Admission { get; }
        public string Diagnostic { get; }
        public bool IsAcknowledged
        {
            get
            {
                return Disposition == LootSendState.Applied
                    || Disposition == LootSendState.ExactReplay;
            }
        }
    }

    public sealed class LootOrigin
    {
        public LootOrigin(
            StableId roomStableId,
            Vector2 position,
            string fingerprint)
        {
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (string.IsNullOrWhiteSpace(fingerprint))
                throw new ArgumentException(
                    "A source-position fingerprint is required.",
                    nameof(fingerprint));
            Position = position;
            Fingerprint = fingerprint.Trim();
        }

        public StableId RoomStableId { get; }
        public Vector2 Position { get; }
        public string Fingerprint { get; }
    }

    public interface ILootOrigin
    {
        bool TryResolve(
            out LootOrigin position,
            out string diagnostic);
    }

    public sealed class TransformOrigin : ILootOrigin
    {
        private readonly StableId roomStableId;
        private readonly Transform sourceTransform;
        private readonly StableId terminalEventStableId;

        public TransformOrigin(
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
            out LootOrigin position,
            out string diagnostic)
        {
            position = null;
            diagnostic = string.Empty;
            if (sourceTransform == null)
            {
                diagnostic = "pickup-terminal-source-transform-missing";
                return false;
            }
            try
            {
                Vector2 value = sourceTransform.position;
                string fingerprint = LootHash.Hash(
                    (terminalEventStableId == null
                        ? "terminal-event-unbound"
                        : terminalEventStableId.ToString())
                    + "|"
                    + value.x.ToString("R", CultureInfo.InvariantCulture)
                    + "|"
                    + value.y.ToString("R", CultureInfo.InvariantCulture));
                position = new LootOrigin(
                    roomStableId,
                    value,
                    fingerprint);
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = "pickup-source-transform-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
                return false;
            }
        }
    }

    public sealed class FixedOrigin : ILootOrigin
    {
        private readonly LootOrigin value;

        public FixedOrigin(LootOrigin value)
        {
            this.value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool TryResolve(
            out LootOrigin position,
            out string diagnostic)
        {
            position = value;
            diagnostic = string.Empty;
            return true;
        }
    }

    public interface ILootLive
    {
        bool TryRegisterPosition(
            LootDropSourceFact source,
            LootOrigin position,
            out string diagnostic);

        RunLootRealizationResult Realize(
            PendingLootDropAdmissionResult admission);

        RunLootPresentationSyncResult Synchronize(
            StableId roomStableId);
    }

    internal sealed class LootLive : ILootLive
    {
        private readonly RunLootPositions sourcePositions;
        private readonly PendingLootDropPickupConsumer pickupConsumer;
        private readonly RunLootView presenter;

        public LootLive(
            RunLootPositions sourcePositions,
            PendingLootDropPickupConsumer pickupConsumer,
            RunLootView presenter)
        {
            this.sourcePositions = sourcePositions
                ?? throw new ArgumentNullException(nameof(sourcePositions));
            this.pickupConsumer = pickupConsumer
                ?? throw new ArgumentNullException(nameof(pickupConsumer));
            this.presenter = presenter
                ?? throw new ArgumentNullException(nameof(presenter));
        }

        public bool TryRegisterPosition(
            LootDropSourceFact source,
            LootOrigin position,
            out string diagnostic)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (position == null) throw new ArgumentNullException(nameof(position));
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

        public RunLootRealizationResult Realize(
            PendingLootDropAdmissionResult admission)
        {
            return pickupConsumer.Consume(admission);
        }

        public RunLootPresentationSyncResult Synchronize(
            StableId roomStableId)
        {
            return presenter.Synchronize(roomStableId);
        }
    }

    /// <summary>
    /// Retained transactional delivery queue. Temporary context and presentation failures retry;
    /// malformed, conflicting, stale or otherwise impossible facts are quarantined exactly once.
    /// </summary>
    public sealed class LootBridge : IPendingLootDropAdmissionConsumer
    {
        private sealed class SourceBinding
        {
            public SourceBinding(ILootOrigin resolver)
            {
                Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            }

            public ILootOrigin Resolver { get; }
        }

        private sealed class DeliveryRecord
        {
            public DeliveryRecord(PendingLootDropAdmissionResult admission)
            {
                Admission = admission;
            }

            public PendingLootDropAdmissionResult Admission { get; }
        }

        private sealed class BadRecord
        {
            public BadRecord(
                PendingLootDropAdmissionResult admission,
                string diagnostic)
            {
                Admission = admission;
                Diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "pickup-delivery-terminal-rejection"
                    : diagnostic.Trim();
            }

            public PendingLootDropAdmissionResult Admission { get; }
            public string Diagnostic { get; }
            public string Fingerprint
            {
                get
                {
                    return Admission == null
                        ? string.Empty
                        : Admission.BatchFingerprint;
                }
            }
        }

        private readonly object gate = new object();
        private readonly Dictionary<string, SourceBinding> sources =
            new Dictionary<string, SourceBinding>(StringComparer.Ordinal);
        private readonly Dictionary<StableId, DeliveryRecord> pendingByOperation =
            new Dictionary<StableId, DeliveryRecord>();
        private readonly Dictionary<StableId, string> completedByOperation =
            new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, BadRecord> quarantinedByOperation =
            new Dictionary<StableId, BadRecord>();
        private ILootLive runtime;

        public int PendingCount
        {
            get
            {
                lock (gate) return pendingByOperation.Count;
            }
        }

        public int CompletedCount
        {
            get
            {
                lock (gate) return completedByOperation.Count;
            }
        }

        public int QuarantinedCount
        {
            get
            {
                lock (gate) return quarantinedByOperation.Count;
            }
        }

        public string LastDiagnostic { get; private set; } = string.Empty;

        public void ConfigureRuntime(ILootLive runtime)
        {
            lock (gate)
            {
                this.runtime = runtime
                    ?? throw new ArgumentNullException(nameof(runtime));
            }
        }

        public void ConfigureRuntime(
            RunLootPositions sourcePositions,
            PendingLootDropPickupConsumer pickupConsumer,
            RunLootView presenter)
        {
            ConfigureRuntime(new LootLive(
                sourcePositions,
                pickupConsumer,
                presenter));
        }

        public void ReleaseRuntime()
        {
            lock (gate) runtime = null;
        }

        public void RegisterSource(
            StableId runStableId,
            long lifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId,
            ILootOrigin resolver)
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
                new TransformOrigin(
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
                new FixedOrigin(
                    new LootOrigin(
                        roomStableId,
                        position,
                        fingerprint)));
        }

        public void Consume(PendingLootDropAdmissionResult admission)
        {
            TryEnqueue(admission);
        }

        public LootSendResult TryEnqueue(
            PendingLootDropAdmissionResult admission)
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
                        ? "pickup-admission-null"
                        : string.IsNullOrWhiteSpace(admission.Diagnostic)
                            ? "pickup-admission-not-accepted"
                            : admission.Diagnostic;
                    return Result(
                        LootSendState.Rejected,
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
                        : "pickup-admission-completed-conflict";
                    return Result(
                        exact
                            ? LootSendState.ExactReplay
                            : LootSendState.ConflictingDuplicate,
                        admission,
                        LastDiagnostic);
                }

                BadRecord quarantined;
                if (quarantinedByOperation.TryGetValue(
                    admission.OperationStableId,
                    out quarantined))
                {
                    bool exact = quarantined != null
                        && string.Equals(
                            quarantined.Fingerprint,
                            admission.BatchFingerprint,
                            StringComparison.Ordinal);
                    LastDiagnostic = exact
                        ? quarantined.Diagnostic
                        : "pickup-admission-quarantine-conflict";
                    return Result(
                        exact
                            ? LootSendState.Rejected
                            : LootSendState.ConflictingDuplicate,
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
                        : "pickup-admission-pending-conflict";
                    return Result(
                        exact
                            ? LootSendState.ExactReplay
                            : LootSendState.ConflictingDuplicate,
                        admission,
                        LastDiagnostic);
                }

                pendingByOperation.Add(
                    admission.OperationStableId,
                    new DeliveryRecord(admission));
                return Result(
                    LootSendState.Applied,
                    admission,
                    string.Empty);
            }
        }

        /// <summary>
        /// Compensates a newly accepted admission only while it is still retained in this
        /// bridge's pending queue. A realized/completed or quarantined delivery is never
        /// silently rewound.
        /// </summary>
        public bool TryRollbackAccepted(
            PendingLootDropAdmissionResult admission,
            out string diagnostic)
        {
            lock (gate)
            {
                diagnostic = string.Empty;
                if (admission == null
                    || admission.Status
                        != PendingLootDropAdmissionStatus.Accepted
                    || admission.OperationStableId == null
                    || string.IsNullOrWhiteSpace(admission.BatchFingerprint))
                {
                    diagnostic = "pickup-admission-rollback-input-invalid";
                    return false;
                }

                string completed;
                if (completedByOperation.TryGetValue(
                        admission.OperationStableId,
                        out completed))
                {
                    diagnostic = string.Equals(
                            completed,
                            admission.BatchFingerprint,
                            StringComparison.Ordinal)
                        ? "pickup-admission-rollback-already-completed"
                        : "pickup-admission-rollback-completed-conflict";
                    return false;
                }

                BadRecord quarantined;
                if (quarantinedByOperation.TryGetValue(
                        admission.OperationStableId,
                        out quarantined))
                {
                    diagnostic = quarantined != null
                        && string.Equals(
                            quarantined.Fingerprint,
                            admission.BatchFingerprint,
                            StringComparison.Ordinal)
                        ? "pickup-admission-rollback-quarantined"
                        : "pickup-admission-rollback-quarantine-conflict";
                    return false;
                }

                DeliveryRecord pending;
                if (!pendingByOperation.TryGetValue(
                        admission.OperationStableId,
                        out pending))
                {
                    diagnostic = "pickup-admission-rollback-pending-missing";
                    return false;
                }
                if (pending == null
                    || pending.Admission == null
                    || !string.Equals(
                        pending.Admission.BatchFingerprint,
                        admission.BatchFingerprint,
                        StringComparison.Ordinal))
                {
                    diagnostic = "pickup-admission-rollback-pending-conflict";
                    return false;
                }

                pendingByOperation.Remove(admission.OperationStableId);
                LastDiagnostic = string.Empty;
                return true;
            }
        }

        public int ProcessPending()
        {
            lock (gate)
            {
                if (runtime == null)
                {
                    LastDiagnostic = "pickup-delivery-runtime-unavailable";
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
                        Quarantine(
                            operation,
                            record == null ? null : record.Admission,
                            "pickup-delivery-record-invalid");
                        continue;
                    }

                    LootDropSourceFact source =
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
                            "pickup-terminal-source-binding-missing";
                        continue;
                    }

                    LootOrigin position;
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
                        diagnostic = "pickup-source-resolution-exception:"
                            + exception.GetType().Name
                            + ":"
                            + exception.Message;
                    }
                    if (!resolved || position == null)
                    {
                        diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                            ? "pickup-source-position-unavailable"
                            : diagnostic;
                        if (IsTerminalDiagnostic(diagnostic))
                            Quarantine(operation, record.Admission, diagnostic);
                        else
                            LastDiagnostic = diagnostic;
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
                        diagnostic = "pickup-position-registration-exception:"
                            + exception.GetType().Name
                            + ":"
                            + exception.Message;
                    }
                    if (!registered)
                    {
                        diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                            ? "pickup-position-registration-rejected"
                            : diagnostic;
                        if (IsTerminalDiagnostic(diagnostic))
                            Quarantine(operation, record.Admission, diagnostic);
                        else
                            LastDiagnostic = diagnostic;
                        continue;
                    }

                    RunLootRealizationResult realization;
                    try
                    {
                        realization = runtime.Realize(record.Admission);
                    }
                    catch (Exception exception)
                    {
                        LastDiagnostic = "pickup-realization-exception:"
                            + exception.GetType().Name
                            + ":"
                            + exception.Message;
                        continue;
                    }

                    if (realization == null)
                    {
                        LastDiagnostic = "pickup-realization-null";
                        continue;
                    }
                    if (realization.Status
                            == RunLootRealizationStatus.ConflictingDuplicate
                        || (realization.Status
                                == RunLootRealizationStatus.Rejected
                            && !IsRetryableRealization(realization.Diagnostic)))
                    {
                        Quarantine(
                            operation,
                            record.Admission,
                            string.IsNullOrWhiteSpace(realization.Diagnostic)
                                ? "pickup-realization-terminal:"
                                    + realization.Status
                                : realization.Diagnostic);
                        continue;
                    }
                    if (realization.Status
                            != RunLootRealizationStatus.Realized
                        && realization.Status
                            != RunLootRealizationStatus.ExactReplay)
                    {
                        LastDiagnostic = string.IsNullOrWhiteSpace(realization.Diagnostic)
                            ? "pickup-realization-retryable:"
                                + realization.Status
                            : realization.Diagnostic;
                        continue;
                    }

                    RunLootPresentationSyncResult presentation;
                    try
                    {
                        presentation = runtime.Synchronize(position.RoomStableId);
                    }
                    catch (Exception exception)
                    {
                        LastDiagnostic = "pickup-presentation-exception:"
                            + exception.GetType().Name
                            + ":"
                            + exception.Message;
                        continue;
                    }
                    if (presentation == null || !presentation.Succeeded)
                    {
                        LastDiagnostic = presentation == null
                            ? "pickup-presentation-unavailable"
                            : string.IsNullOrWhiteSpace(presentation.Diagnostic)
                                ? "pickup-presentation-retryable"
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
                    LootDropSourceFact source = SourceOf(
                        pair.Value == null ? null : pair.Value.Admission);
                    if (!IsCurrent(source, runStableId, lifecycleGeneration))
                        removePending.Add(pair.Key);
                }
                for (int index = 0; index < removePending.Count; index++)
                    pendingByOperation.Remove(removePending[index]);

                var removeQuarantined = new List<StableId>();
                foreach (KeyValuePair<StableId, BadRecord> pair in
                    quarantinedByOperation)
                {
                    LootDropSourceFact source = SourceOf(
                        pair.Value == null ? null : pair.Value.Admission);
                    if (!IsCurrent(source, runStableId, lifecycleGeneration))
                        removeQuarantined.Add(pair.Key);
                }
                for (int index = 0; index < removeQuarantined.Count; index++)
                    quarantinedByOperation.Remove(removeQuarantined[index]);

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
                quarantinedByOperation.Clear();
                sources.Clear();
                runtime = null;
                LastDiagnostic = string.Empty;
            }
        }

        private void Quarantine(
            StableId operation,
            PendingLootDropAdmissionResult admission,
            string diagnostic)
        {
            if (operation == null) return;
            pendingByOperation.Remove(operation);
            if (!quarantinedByOperation.ContainsKey(operation))
            {
                quarantinedByOperation.Add(
                    operation,
                    new BadRecord(admission, diagnostic));
            }
            LastDiagnostic = quarantinedByOperation[operation].Diagnostic;
        }

        private static bool IsRetryableRealization(string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(diagnostic)) return false;
            return diagnostic == "run-pickup-session-context-unavailable"
                || diagnostic.StartsWith(
                    "run-pickup-session-context-exception:",
                    StringComparison.Ordinal)
                || diagnostic == "run-pickup-source-position-unresolved"
                || diagnostic.StartsWith(
                    "run-pickup-source-position-exception:",
                    StringComparison.Ordinal)
                || diagnostic == "run-pickup-awaiting-source-position";
        }

        private static bool IsTerminalDiagnostic(string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(diagnostic)) return false;
            string value = diagnostic.ToLowerInvariant();
            return value.Contains("conflict")
                || value.Contains("invalid")
                || value.Contains("impossible")
                || value.Contains("mismatch")
                || value.Contains("stale")
                || value.Contains("future-generation")
                || value.Contains("run-ended")
                || value.Contains("wrong-run")
                || value.Contains("participant-mismatch");
        }

        private static LootDropSourceFact SourceOf(
            PendingLootDropAdmissionResult admission)
        {
            return admission == null || admission.PendingResult == null
                ? null
                : admission.PendingResult.SourceFact;
        }

        private static bool IsCurrent(
            LootDropSourceFact source,
            StableId runStableId,
            long lifecycleGeneration)
        {
            return source != null
                && source.RunStableId == runStableId
                && source.RunLifecycleGeneration == lifecycleGeneration;
        }

        private static LootSendResult Result(
            LootSendState disposition,
            PendingLootDropAdmissionResult admission,
            string diagnostic)
        {
            return new LootSendResult(
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

    internal static class LootHash
    {
        public static string Hash(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] digest = algorithm.ComputeHash(bytes);
                var text = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                    text.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
                return "sha256:" + text;
            }
        }
    }
}
