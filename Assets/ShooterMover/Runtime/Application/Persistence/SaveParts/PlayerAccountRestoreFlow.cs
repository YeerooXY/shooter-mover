using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Persistence.SaveParts
{
    public sealed class CharacterSaveRestoreBinding
    {
        public CharacterSaveRestoreBinding(
            int slotIndex,
            StableId characterInstanceStableId,
            IEnumerable<ISavePart> adapters)
        {
            PlayerAccountSnapshot.ValidateSlotIndex(slotIndex);
            SlotIndex = slotIndex;
            CharacterInstanceStableId = characterInstanceStableId
                ?? throw new ArgumentNullException(
                    nameof(characterInstanceStableId));
            Adapters = FreezeAdapters(adapters);
        }

        public int SlotIndex { get; }

        public StableId CharacterInstanceStableId { get; }

        public IReadOnlyDictionary<StableId, ISavePart>
            Adapters { get; }

        private static IReadOnlyDictionary<StableId, ISavePart>
            FreezeAdapters(IEnumerable<ISavePart> adapters)
        {
            var output = new SortedDictionary<
                string,
                ISavePart>(StringComparer.Ordinal);
            foreach (ISavePart adapter in
                adapters ?? Array.Empty<ISavePart>())
            {
                if (adapter == null || adapter.Definition == null)
                {
                    throw new ArgumentException(
                        "Restore adapters must be non-null.",
                        nameof(adapters));
                }
                string key = adapter.Definition.ComponentStableId.ToString();
                if (output.ContainsKey(key))
                {
                    throw new ArgumentException(
                        "Restore adapter component identities must be unique.",
                        nameof(adapters));
                }
                output.Add(key, adapter);
            }

            return new ReadOnlyDictionary<StableId, ISavePart>(
                output.Values.ToDictionary(
                    item => item.Definition.ComponentStableId,
                    item => item));
        }
    }

    public enum PlayerAccountRestoreStatus
    {
        Restored = 1,
        ValidationRejected = 2,

        /// <summary>
        /// The failing component was compensated and every earlier successful
        /// component was confirmed restored to its exact pre-restore codec bytes.
        /// </summary>
        CommitFailedRolledBack = 3,

        /// <summary>
        /// Earlier components were restored, but the component whose apply failed
        /// could not be confirmed at its previous snapshot.
        /// </summary>
        CommitFailedCompensationIncomplete = 4,

        /// <summary>
        /// The failing component was compensated, but one or more earlier successful
        /// components could not be confirmed restored.
        /// </summary>
        CommitFailedEarlierRollbackIncomplete = 5,

        /// <summary>
        /// Both failing-component compensation and earlier-component rollback were
        /// incomplete.
        /// </summary>
        CommitFailedCompensationAndRollbackIncomplete = 6,
    }

    public sealed class RetainedUnknownSavePart
    {
        public RetainedUnknownSavePart(
            int? characterSlotIndex,
            SavePartSnapshot component)
        {
            CharacterSlotIndex = characterSlotIndex;
            Component = component
                ?? throw new ArgumentNullException(nameof(component));
        }

        public int? CharacterSlotIndex { get; }

        public SavePartSnapshot Component { get; }
    }

    public sealed class PlayerAccountRestoreResult
    {
        public PlayerAccountRestoreResult(
            PlayerAccountRestoreStatus status,
            string rejectionCode,
            IEnumerable<RetainedUnknownSavePart> retainedUnknownComponents,
            StableId failedComponentStableId = null)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            FailedComponentStableId = failedComponentStableId;
            RetainedUnknownComponents = new ReadOnlyCollection<
                RetainedUnknownSavePart>(
                new List<RetainedUnknownSavePart>(
                    retainedUnknownComponents
                    ?? Array.Empty<RetainedUnknownSavePart>()));
        }

        public PlayerAccountRestoreStatus Status { get; }

        public string RejectionCode { get; }

        public StableId FailedComponentStableId { get; }

        public IReadOnlyList<RetainedUnknownSavePart>
            RetainedUnknownComponents { get; }

        public bool Succeeded
        {
            get { return Status == PlayerAccountRestoreStatus.Restored; }
        }
    }

    /// <summary>
    /// Stages every account and character component without mutation, then commits
    /// only after all required components and aggregate semantic validators succeed.
    /// Unknown future component IDs remain opaque immutable facts. Commit failure is
    /// compensating: the failing component restores its captured previous snapshot,
    /// followed by reverse rollback of all earlier successful components. A rolled-
    /// back status is returned only when every previous fingerprint is confirmed.
    /// </summary>
    public sealed class PlayerAccountRestoreFlow
    {
        private readonly IReadOnlyDictionary<StableId, ISavePart>
            accountAdapters;
        private readonly Func<PlayerAccountSnapshot,
            SavePartValidationResult> validateAggregate;

        public PlayerAccountRestoreFlow(
            IEnumerable<ISavePart> accountAdapters = null,
            Func<PlayerAccountSnapshot, SavePartValidationResult>
                validateAggregate = null)
        {
            this.accountAdapters = FreezeAccountAdapters(accountAdapters);
            this.validateAggregate = validateAggregate
                ?? PlayerAccountAggregateCodec.Validate;
        }

        public PlayerAccountRestoreResult Restore(
            PlayerAccountSnapshot account,
            IEnumerable<CharacterSaveRestoreBinding> characterBindings)
        {
            SavePartValidationResult accountIntegrity =
                PlayerAccountAggregateCodec.Validate(account);
            if (!accountIntegrity.Succeeded)
            {
                return Rejected(accountIntegrity.RejectionCode);
            }
            SavePartValidationResult aggregate =
                validateAggregate(account);
            if (aggregate == null || !aggregate.Succeeded)
            {
                return Rejected(aggregate == null
                    ? "aggregate-save-validation-result-null"
                    : aggregate.RejectionCode);
            }

            Dictionary<int, CharacterSaveRestoreBinding> bindings;
            string bindingError;
            if (!TryFreezeBindings(
                characterBindings,
                out bindings,
                out bindingError))
            {
                return Rejected(bindingError);
            }

            var prepared = new List<PreparedEntry>();
            var unknown = new List<RetainedUnknownSavePart>();
            try
            {
                string rejectionCode;
                if (!TryPrepareComponentSet(
                    null,
                    account.AccountComponents,
                    accountAdapters,
                    prepared,
                    unknown,
                    out rejectionCode))
                {
                    DisposeAll(prepared);
                    return Rejected(rejectionCode, unknown);
                }

                for (int slotIndex = 0;
                    slotIndex < PlayerAccountSnapshot.CharacterSlotCount;
                    slotIndex++)
                {
                    CharacterInstanceSnapshot character =
                        account.CharacterAt(slotIndex);
                    CharacterSaveRestoreBinding binding;
                    bool hasBinding = bindings.TryGetValue(
                        slotIndex,
                        out binding);
                    if (character == null)
                    {
                        if (hasBinding)
                        {
                            DisposeAll(prepared);
                            return Rejected(
                                "restore-binding-for-empty-character-slot",
                                unknown);
                        }
                        continue;
                    }
                    if (!hasBinding)
                    {
                        DisposeAll(prepared);
                        return Rejected(
                            "restore-binding-missing-for-character-slot",
                            unknown);
                    }
                    if (binding.CharacterInstanceStableId
                        != character.CharacterInstanceStableId)
                    {
                        DisposeAll(prepared);
                        return Rejected(
                            "restore-binding-character-id-mismatch",
                            unknown);
                    }

                    if (!TryPrepareComponentSet(
                        slotIndex,
                        character.Components,
                        binding.Adapters,
                        prepared,
                        unknown,
                        out rejectionCode))
                    {
                        DisposeAll(prepared);
                        return Rejected(rejectionCode, unknown);
                    }
                }

                prepared.Sort(PreparedEntry.Compare);
                var committed = new List<PreparedEntry>();
                for (int index = 0; index < prepared.Count; index++)
                {
                    PreparedEntry entry = prepared[index];
                    SavePartCommitResult result;
                    try
                    {
                        result = entry.Restore.Commit();
                    }
                    catch (Exception exception)
                    {
                        // A compliant prepared restore does not throw, but treat an
                        // unexpected implementation as uncompensated rather than
                        // incorrectly claiming atomic restoration.
                        result = new SavePartCommitResult(
                            SavePartCommitStatus
                                .FailedCompensationIncomplete,
                            "prepared-restore-commit-threw:"
                                + exception.GetType().Name,
                            false);
                    }

                    if (result != null && result.Succeeded)
                    {
                        committed.Add(entry);
                        continue;
                    }

                    bool failingRestored = result != null
                        && result.PreviousSnapshotConfirmed;
                    RollbackSummary rollback = Rollback(committed);
                    PlayerAccountRestoreStatus status = ClassifyFailure(
                        failingRestored,
                        rollback.Complete);
                    string details = result == null
                        ? "save-part-commit-result-null"
                        : result.RejectionCode;
                    if (!rollback.Complete)
                    {
                        details += ";earlier_rollback="
                            + rollback.RejectionCode;
                    }
                    DisposeAll(prepared);
                    return new PlayerAccountRestoreResult(
                        status,
                        details,
                        unknown,
                        entry.Restore.ComponentStableId);
                }

                DisposeAll(prepared);
                return new PlayerAccountRestoreResult(
                    PlayerAccountRestoreStatus.Restored,
                    string.Empty,
                    unknown);
            }
            catch
            {
                DisposeAll(prepared);
                throw;
            }
        }

        public static IReadOnlyList<SavePartSnapshot> ExportComponents(
            IEnumerable<ISavePart> adapters)
        {
            var ordered = new SortedDictionary<
                string,
                SavePartSnapshot>(StringComparer.Ordinal);
            foreach (ISavePart adapter in
                adapters ?? Array.Empty<ISavePart>())
            {
                if (adapter == null || adapter.Definition == null)
                {
                    throw new ArgumentException(
                        "Export adapters must be non-null.",
                        nameof(adapters));
                }
                SavePartSnapshot component;
                try
                {
                    component = adapter.ExportComponent();
                }
                catch (Exception exception)
                {
                    string componentId = adapter.Definition == null
                        ? "unknown"
                        : adapter.Definition.ComponentStableId.ToString();
                    throw new InvalidOperationException(
                        "save-part-export-failed:"
                            + componentId
                            + ":"
                            + exception.Message);
                }
                string key = component.ComponentStableId.ToString();
                if (ordered.ContainsKey(key))
                {
                    throw new ArgumentException(
                        "Export adapter component identities must be unique.",
                        nameof(adapters));
                }
                ordered.Add(key, component);
            }
            return new ReadOnlyCollection<SavePartSnapshot>(
                ordered.Values.ToList());
        }

        private static PlayerAccountRestoreStatus ClassifyFailure(
            bool failingRestored,
            bool earlierRestored)
        {
            if (failingRestored && earlierRestored)
            {
                return PlayerAccountRestoreStatus.CommitFailedRolledBack;
            }
            if (!failingRestored && earlierRestored)
            {
                return PlayerAccountRestoreStatus
                    .CommitFailedCompensationIncomplete;
            }
            if (failingRestored)
            {
                return PlayerAccountRestoreStatus
                    .CommitFailedEarlierRollbackIncomplete;
            }
            return PlayerAccountRestoreStatus
                .CommitFailedCompensationAndRollbackIncomplete;
        }

        private static bool TryPrepareComponentSet(
            int? slotIndex,
            IReadOnlyDictionary<StableId, SavePartSnapshot> components,
            IReadOnlyDictionary<StableId, ISavePart> adapters,
            ICollection<PreparedEntry> prepared,
            ICollection<RetainedUnknownSavePart> unknown,
            out string rejectionCode)
        {
            foreach (ISavePart adapter in adapters.Values
                .OrderBy(
                    item => item.Definition.ComponentStableId.ToString(),
                    StringComparer.Ordinal))
            {
                SavePartSnapshot component;
                if (!components.TryGetValue(
                    adapter.Definition.ComponentStableId,
                    out component))
                {
                    if (adapter.Definition.IsRequired)
                    {
                        rejectionCode = "required-save-part-missing:"
                            + adapter.Definition.ComponentStableId;
                        return false;
                    }
                    continue;
                }

                SavePartPrepareResult result =
                    adapter.PrepareRestore(component);
                if (result == null || !result.Succeeded)
                {
                    rejectionCode = result == null
                        ? "save-part-prepare-result-null"
                        : result.RejectionCode;
                    return false;
                }
                prepared.Add(new PreparedEntry(
                    slotIndex,
                    adapter.Definition.RestoreOrder,
                    result.PreparedRestore));
            }

            foreach (SavePartSnapshot component in components.Values)
            {
                if (!adapters.ContainsKey(component.ComponentStableId))
                {
                    unknown.Add(new RetainedUnknownSavePart(
                        slotIndex,
                        component));
                }
            }

            rejectionCode = string.Empty;
            return true;
        }

        private static bool TryFreezeBindings(
            IEnumerable<CharacterSaveRestoreBinding> source,
            out Dictionary<int, CharacterSaveRestoreBinding> bindings,
            out string rejectionCode)
        {
            bindings = new Dictionary<int, CharacterSaveRestoreBinding>();
            foreach (CharacterSaveRestoreBinding binding in
                source ?? Array.Empty<CharacterSaveRestoreBinding>())
            {
                if (binding == null)
                {
                    rejectionCode = "restore-binding-null";
                    return false;
                }
                if (bindings.ContainsKey(binding.SlotIndex))
                {
                    rejectionCode = "restore-binding-slot-duplicate";
                    return false;
                }
                bindings.Add(binding.SlotIndex, binding);
            }
            rejectionCode = string.Empty;
            return true;
        }

        private static IReadOnlyDictionary<StableId, ISavePart>
            FreezeAccountAdapters(
                IEnumerable<ISavePart> source)
        {
            var output = new Dictionary<StableId, ISavePart>();
            foreach (ISavePart adapter in
                source ?? Array.Empty<ISavePart>())
            {
                if (adapter == null || adapter.Definition == null)
                {
                    throw new ArgumentException(
                        "Account adapters must be non-null.",
                        nameof(source));
                }
                if (output.ContainsKey(adapter.Definition.ComponentStableId))
                {
                    throw new ArgumentException(
                        "Account adapter component identities must be unique.",
                        nameof(source));
                }
                output.Add(adapter.Definition.ComponentStableId, adapter);
            }
            return new ReadOnlyDictionary<StableId, ISavePart>(
                output);
        }

        private static RollbackSummary Rollback(
            IReadOnlyList<PreparedEntry> committed)
        {
            bool complete = true;
            var errors = new List<string>();
            for (int index = committed.Count - 1; index >= 0; index--)
            {
                SavePartRollbackResult result;
                try
                {
                    result = committed[index].Restore.Rollback();
                }
                catch (Exception exception)
                {
                    result = new SavePartRollbackResult(
                        false,
                        "rollback-threw:"
                            + exception.GetType().Name);
                }
                if (result == null || !result.Restored)
                {
                    complete = false;
                    errors.Add(
                        committed[index].Restore.ComponentStableId
                        + "="
                        + (result == null
                            ? "rollback-result-null"
                            : result.RejectionCode));
                }
            }
            return new RollbackSummary(
                complete,
                string.Join("|", errors));
        }

        private static void DisposeAll(IEnumerable<PreparedEntry> prepared)
        {
            foreach (PreparedEntry entry in prepared)
            {
                entry.Restore.Dispose();
            }
        }

        private static PlayerAccountRestoreResult Rejected(
            string rejectionCode,
            IEnumerable<RetainedUnknownSavePart> unknown = null)
        {
            return new PlayerAccountRestoreResult(
                PlayerAccountRestoreStatus.ValidationRejected,
                rejectionCode,
                unknown);
        }

        private sealed class RollbackSummary
        {
            public RollbackSummary(bool complete, string rejectionCode)
            {
                Complete = complete;
                RejectionCode = rejectionCode ?? string.Empty;
            }

            public bool Complete { get; }

            public string RejectionCode { get; }
        }

        private sealed class PreparedEntry
        {
            public PreparedEntry(
                int? slotIndex,
                int restoreOrder,
                IPreparedSavePartRestore restore)
            {
                SlotIndex = slotIndex;
                RestoreOrder = restoreOrder;
                Restore = restore;
            }

            public int? SlotIndex { get; }

            public int RestoreOrder { get; }

            public IPreparedSavePartRestore Restore { get; }

            public static int Compare(PreparedEntry left, PreparedEntry right)
            {
                int leftSlot = left.SlotIndex.HasValue
                    ? left.SlotIndex.Value
                    : -1;
                int rightSlot = right.SlotIndex.HasValue
                    ? right.SlotIndex.Value
                    : -1;
                int slot = leftSlot.CompareTo(rightSlot);
                if (slot != 0) return slot;
                int order = left.RestoreOrder.CompareTo(right.RestoreOrder);
                return order != 0
                    ? order
                    : string.CompareOrdinal(
                        left.Restore.ComponentStableId.ToString(),
                        right.Restore.ComponentStableId.ToString());
            }
        }
    }
}
