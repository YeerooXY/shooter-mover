using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Persistence.Components
{
    public sealed class CharacterSaveRestoreBindingV1
    {
        public CharacterSaveRestoreBindingV1(
            int slotIndex,
            StableId characterInstanceStableId,
            IEnumerable<ISaveComponentAdapterV1> adapters)
        {
            PlayerAccountSnapshotV1.ValidateSlotIndex(slotIndex);
            SlotIndex = slotIndex;
            CharacterInstanceStableId = characterInstanceStableId
                ?? throw new ArgumentNullException(
                    nameof(characterInstanceStableId));
            Adapters = FreezeAdapters(adapters);
        }

        public int SlotIndex { get; }

        public StableId CharacterInstanceStableId { get; }

        public IReadOnlyDictionary<StableId, ISaveComponentAdapterV1>
            Adapters { get; }

        private static IReadOnlyDictionary<StableId, ISaveComponentAdapterV1>
            FreezeAdapters(IEnumerable<ISaveComponentAdapterV1> adapters)
        {
            var output = new SortedDictionary<
                string,
                ISaveComponentAdapterV1>(StringComparer.Ordinal);
            foreach (ISaveComponentAdapterV1 adapter in
                adapters ?? Array.Empty<ISaveComponentAdapterV1>())
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

            return new ReadOnlyDictionary<StableId, ISaveComponentAdapterV1>(
                output.Values.ToDictionary(
                    item => item.Definition.ComponentStableId,
                    item => item));
        }
    }

    public enum PlayerAccountRestoreStatusV1
    {
        Restored = 1,
        ValidationRejected = 2,
        CommitFailedRolledBack = 3,
        CommitFailedRollbackIncomplete = 4,
    }

    public sealed class RetainedUnknownSaveComponentV1
    {
        public RetainedUnknownSaveComponentV1(
            int? characterSlotIndex,
            SaveComponentSnapshotV1 component)
        {
            CharacterSlotIndex = characterSlotIndex;
            Component = component
                ?? throw new ArgumentNullException(nameof(component));
        }

        public int? CharacterSlotIndex { get; }

        public SaveComponentSnapshotV1 Component { get; }
    }

    public sealed class PlayerAccountRestoreResultV1
    {
        public PlayerAccountRestoreResultV1(
            PlayerAccountRestoreStatusV1 status,
            string rejectionCode,
            IEnumerable<RetainedUnknownSaveComponentV1> retainedUnknownComponents)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            RetainedUnknownComponents = new ReadOnlyCollection<
                RetainedUnknownSaveComponentV1>(
                new List<RetainedUnknownSaveComponentV1>(
                    retainedUnknownComponents
                    ?? Array.Empty<RetainedUnknownSaveComponentV1>()));
        }

        public PlayerAccountRestoreStatusV1 Status { get; }

        public string RejectionCode { get; }

        public IReadOnlyList<RetainedUnknownSaveComponentV1>
            RetainedUnknownComponents { get; }

        public bool Succeeded
        {
            get { return Status == PlayerAccountRestoreStatusV1.Restored; }
        }
    }

    /// <summary>
    /// Stages account and character component restores without mutation, then commits
    /// them only after every required component and every semantic validator succeeds.
    /// Unknown future components remain attached to the immutable account snapshot and
    /// are returned as opaque retained facts rather than interpreted.
    /// </summary>
    public sealed class PlayerAccountRestoreCoordinatorV1
    {
        private readonly IReadOnlyDictionary<StableId, ISaveComponentAdapterV1>
            accountAdapters;
        private readonly Func<PlayerAccountSnapshotV1,
            SaveComponentValidationResultV1> validateAggregate;

        public PlayerAccountRestoreCoordinatorV1(
            IEnumerable<ISaveComponentAdapterV1> accountAdapters = null,
            Func<PlayerAccountSnapshotV1, SaveComponentValidationResultV1>
                validateAggregate = null)
        {
            this.accountAdapters = FreezeAccountAdapters(accountAdapters);
            this.validateAggregate = validateAggregate
                ?? (snapshot => CanonicalSnapshotIntegrityV1.Validate(snapshot));
        }

        public PlayerAccountRestoreResultV1 Restore(
            PlayerAccountSnapshotV1 account,
            IEnumerable<CharacterSaveRestoreBindingV1> characterBindings)
        {
            SaveComponentValidationResultV1 accountIntegrity =
                CanonicalSnapshotIntegrityV1.Validate(account);
            if (!accountIntegrity.Succeeded)
            {
                return Rejected(accountIntegrity.RejectionCode);
            }
            SaveComponentValidationResultV1 aggregate =
                validateAggregate(account);
            if (aggregate == null || !aggregate.Succeeded)
            {
                return Rejected(aggregate == null
                    ? "aggregate-save-validation-result-null"
                    : aggregate.RejectionCode);
            }

            Dictionary<int, CharacterSaveRestoreBindingV1> bindings;
            string bindingError;
            if (!TryFreezeBindings(
                characterBindings,
                out bindings,
                out bindingError))
            {
                return Rejected(bindingError);
            }

            var prepared = new List<PreparedEntry>();
            var unknown = new List<RetainedUnknownSaveComponentV1>();
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
                    slotIndex < PlayerAccountSnapshotV1.CharacterSlotCount;
                    slotIndex++)
                {
                    CharacterInstanceSnapshotV1 character =
                        account.CharacterAt(slotIndex);
                    CharacterSaveRestoreBindingV1 binding;
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
                try
                {
                    for (int index = 0; index < prepared.Count; index++)
                    {
                        prepared[index].Restore.Commit();
                        committed.Add(prepared[index]);
                    }
                }
                catch (Exception exception)
                {
                    bool rollbackComplete = Rollback(committed);
                    DisposeAll(prepared);
                    return new PlayerAccountRestoreResultV1(
                        rollbackComplete
                            ? PlayerAccountRestoreStatusV1
                                .CommitFailedRolledBack
                            : PlayerAccountRestoreStatusV1
                                .CommitFailedRollbackIncomplete,
                        "restore-commit-failed:"
                            + exception.GetType().Name,
                        unknown);
                }

                DisposeAll(prepared);
                return new PlayerAccountRestoreResultV1(
                    PlayerAccountRestoreStatusV1.Restored,
                    string.Empty,
                    unknown);
            }
            catch
            {
                DisposeAll(prepared);
                throw;
            }
        }

        public static IReadOnlyList<SaveComponentSnapshotV1> ExportComponents(
            IEnumerable<ISaveComponentAdapterV1> adapters)
        {
            var ordered = new SortedDictionary<
                string,
                SaveComponentSnapshotV1>(StringComparer.Ordinal);
            foreach (ISaveComponentAdapterV1 adapter in
                adapters ?? Array.Empty<ISaveComponentAdapterV1>())
            {
                if (adapter == null || adapter.Definition == null)
                {
                    throw new ArgumentException(
                        "Export adapters must be non-null.",
                        nameof(adapters));
                }
                SaveComponentSnapshotV1 component = adapter.ExportComponent();
                string key = component.ComponentStableId.ToString();
                if (ordered.ContainsKey(key))
                {
                    throw new ArgumentException(
                        "Export adapter component identities must be unique.",
                        nameof(adapters));
                }
                ordered.Add(key, component);
            }
            return new ReadOnlyCollection<SaveComponentSnapshotV1>(
                ordered.Values.ToList());
        }

        private static bool TryPrepareComponentSet(
            int? slotIndex,
            IReadOnlyDictionary<StableId, SaveComponentSnapshotV1> components,
            IReadOnlyDictionary<StableId, ISaveComponentAdapterV1> adapters,
            ICollection<PreparedEntry> prepared,
            ICollection<RetainedUnknownSaveComponentV1> unknown,
            out string rejectionCode)
        {
            foreach (ISaveComponentAdapterV1 adapter in adapters.Values
                .OrderBy(
                    item => item.Definition.ComponentStableId.ToString(),
                    StringComparer.Ordinal))
            {
                SaveComponentSnapshotV1 component;
                if (!components.TryGetValue(
                    adapter.Definition.ComponentStableId,
                    out component))
                {
                    if (adapter.Definition.IsRequired)
                    {
                        rejectionCode = "required-save-component-missing:"
                            + adapter.Definition.ComponentStableId;
                        return false;
                    }
                    continue;
                }

                SaveComponentPrepareResultV1 result =
                    adapter.PrepareRestore(component);
                if (result == null || !result.Succeeded)
                {
                    rejectionCode = result == null
                        ? "save-component-prepare-result-null"
                        : result.RejectionCode;
                    return false;
                }
                prepared.Add(new PreparedEntry(
                    slotIndex,
                    result.PreparedRestore));
            }

            foreach (SaveComponentSnapshotV1 component in components.Values)
            {
                if (!adapters.ContainsKey(component.ComponentStableId))
                {
                    unknown.Add(new RetainedUnknownSaveComponentV1(
                        slotIndex,
                        component));
                }
            }

            rejectionCode = string.Empty;
            return true;
        }

        private static bool TryFreezeBindings(
            IEnumerable<CharacterSaveRestoreBindingV1> source,
            out Dictionary<int, CharacterSaveRestoreBindingV1> bindings,
            out string rejectionCode)
        {
            bindings = new Dictionary<int, CharacterSaveRestoreBindingV1>();
            foreach (CharacterSaveRestoreBindingV1 binding in
                source ?? Array.Empty<CharacterSaveRestoreBindingV1>())
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

        private static IReadOnlyDictionary<StableId, ISaveComponentAdapterV1>
            FreezeAccountAdapters(
                IEnumerable<ISaveComponentAdapterV1> source)
        {
            var output = new Dictionary<StableId, ISaveComponentAdapterV1>();
            foreach (ISaveComponentAdapterV1 adapter in
                source ?? Array.Empty<ISaveComponentAdapterV1>())
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
            return new ReadOnlyDictionary<StableId, ISaveComponentAdapterV1>(
                output);
        }

        private static bool Rollback(IReadOnlyList<PreparedEntry> committed)
        {
            bool complete = true;
            for (int index = committed.Count - 1; index >= 0; index--)
            {
                try
                {
                    committed[index].Restore.Rollback();
                }
                catch
                {
                    complete = false;
                }
            }
            return complete;
        }

        private static void DisposeAll(IEnumerable<PreparedEntry> prepared)
        {
            foreach (PreparedEntry entry in prepared)
            {
                entry.Restore.Dispose();
            }
        }

        private static PlayerAccountRestoreResultV1 Rejected(
            string rejectionCode,
            IEnumerable<RetainedUnknownSaveComponentV1> unknown = null)
        {
            return new PlayerAccountRestoreResultV1(
                PlayerAccountRestoreStatusV1.ValidationRejected,
                rejectionCode,
                unknown);
        }

        private sealed class PreparedEntry
        {
            public PreparedEntry(
                int? slotIndex,
                IPreparedSaveComponentRestoreV1 restore)
            {
                SlotIndex = slotIndex;
                Restore = restore;
            }

            public int? SlotIndex { get; }

            public IPreparedSaveComponentRestoreV1 Restore { get; }

            public static int Compare(PreparedEntry left, PreparedEntry right)
            {
                int leftSlot = left.SlotIndex.HasValue
                    ? left.SlotIndex.Value
                    : -1;
                int rightSlot = right.SlotIndex.HasValue
                    ? right.SlotIndex.Value
                    : -1;
                int slot = leftSlot.CompareTo(rightSlot);
                return slot != 0
                    ? slot
                    : string.CompareOrdinal(
                        left.Restore.ComponentStableId.ToString(),
                        right.Restore.ComponentStableId.ToString());
            }
        }
    }
}
