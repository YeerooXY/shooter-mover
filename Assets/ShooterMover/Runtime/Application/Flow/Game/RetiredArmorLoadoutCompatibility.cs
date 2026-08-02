using System;
using System.Collections.Generic;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Application.Flow.Game
{
    /// <summary>
    /// Save-schema bridge for characters created before the typed gear system.
    /// The old fixed armor slots are no longer runtime state. Existing valid snapshots are
    /// accepted during restore and replaced by an empty projection on the next save.
    /// </summary>
    internal static class RetiredArmorLoadoutCompatibility
    {
        private static readonly InventoryLoadoutStateSnapshot EmptyValue =
            BuildEmpty();

        public static InventoryLoadoutStateSnapshot Empty()
        {
            return EmptyValue;
        }

        public static InventoryLoadoutStateSnapshot GunOnly(
            long sequence,
            IReadOnlyList<InventoryLoadoutSlotBinding> bindings)
        {
            if (bindings == null
                || bindings.Count != InventoryLoadoutSlots.All.Count)
            {
                throw new ArgumentException(
                    "The retired loadout projection has an invalid binding count.",
                    nameof(bindings));
            }

            var output = new List<InventoryLoadoutSlotBinding>(
                InventoryLoadoutSlots.All.Count);
            for (int index = 0;
                 index < InventoryLoadoutSlots.All.Count;
                 index++)
            {
                InventoryLoadoutSlotDescriptor slot =
                    InventoryLoadoutSlots.All[index];
                InventoryLoadoutSlotBinding binding = bindings[index];
                if (binding == null
                    || binding.SlotStableId != slot.SlotStableId)
                {
                    throw new ArgumentException(
                        "The retired loadout projection has invalid slot ordering.",
                        nameof(bindings));
                }
                output.Add(new InventoryLoadoutSlotBinding(
                    slot.SlotStableId,
                    slot.Kind == InventoryLoadoutSlotKind.Gun
                        ? binding.EquipmentInstanceStableId
                        : null));
            }
            return InventoryLoadoutStateSnapshot.CreateCanonical(
                sequence,
                output);
        }

        public static bool ContainsRetiredArmorBinding(
            IReadOnlyList<InventoryLoadoutSlotBinding> bindings)
        {
            if (bindings == null
                || bindings.Count != InventoryLoadoutSlots.All.Count)
            {
                return false;
            }
            for (int index = 0;
                 index < InventoryLoadoutSlots.All.Count;
                 index++)
            {
                if (InventoryLoadoutSlots.All[index].Kind
                        != InventoryLoadoutSlotKind.Gun
                    && bindings[index] != null
                    && bindings[index].EquipmentInstanceStableId != null)
                {
                    return true;
                }
            }
            return false;
        }

        private static InventoryLoadoutStateSnapshot BuildEmpty()
        {
            var bindings = new List<InventoryLoadoutSlotBinding>(
                InventoryLoadoutSlots.All.Count);
            for (int index = 0;
                 index < InventoryLoadoutSlots.All.Count;
                 index++)
            {
                bindings.Add(new InventoryLoadoutSlotBinding(
                    InventoryLoadoutSlots.All[index].SlotStableId,
                    null));
            }
            return InventoryLoadoutStateSnapshot.CreateCanonical(
                0L,
                bindings);
        }
    }

    public sealed class InventoryLoadoutImportResult
    {
        public InventoryLoadoutImportResult(
            bool succeeded,
            string rejectionCode,
            InventoryLoadoutStateSnapshot snapshot)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public string RejectionCode { get; }
        public InventoryLoadoutStateSnapshot Snapshot { get; }
    }

    /// <summary>
    /// Retained API shell for the current gun Inventory compatibility projection.
    /// It preserves gun route slots only. Every former armor slot is forced empty, and any
    /// new command attempting to bind one is rejected until the typed gear authority replaces it.
    /// </summary>
    public sealed class InventoryLoadoutState :
        IInventoryLoadoutStatePort
    {
        private InventoryLoadoutStateSnapshot snapshot;
        private string lastAcceptedCommandFingerprint = string.Empty;

        public InventoryLoadoutState(
            PlayerRouteProfilePayload routePayload,
            IPlayerHoldingsState holdings,
            IEquipmentCatalogProvider equipmentCatalogProvider)
            : this(
                routePayload,
                holdings,
                equipmentCatalogProvider,
                null,
                null)
        {
        }

        public InventoryLoadoutState(
            PlayerRouteProfilePayload routePayload,
            IPlayerHoldingsState holdings,
            IEquipmentCatalogProvider equipmentCatalogProvider,
            GunInventoryState canonicalGunInventory,
            GunCatalog canonicalGunCatalog)
        {
            if (routePayload == null)
            {
                throw new ArgumentNullException(nameof(routePayload));
            }
            if (!routePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The initial route payload is invalid.",
                    nameof(routePayload));
            }
            if (holdings == null)
            {
                throw new ArgumentNullException(nameof(holdings));
            }
            if (equipmentCatalogProvider == null)
            {
                throw new ArgumentNullException(
                    nameof(equipmentCatalogProvider));
            }

            var bindings = new List<InventoryLoadoutSlotBinding>(
                InventoryLoadoutSlots.All.Count);
            for (int index = 0;
                 index < InventoryLoadoutSlots.All.Count;
                 index++)
            {
                InventoryLoadoutSlotDescriptor slot =
                    InventoryLoadoutSlots.All[index];
                StableId instanceId = slot.Kind == InventoryLoadoutSlotKind.Gun
                    && index < PlayerRouteProfilePayload.GunSlotCount
                        ? routePayload.GunSlots[index]
                            .EquipmentInstanceStableId
                        : null;
                bindings.Add(new InventoryLoadoutSlotBinding(
                    slot.SlotStableId,
                    instanceId));
            }
            snapshot = RetiredArmorLoadoutCompatibility.GunOnly(
                0L,
                bindings);
        }

        public InventoryLoadoutStateSnapshot ExportSnapshot()
        {
            return snapshot;
        }

        public InventoryLoadoutImportResult ImportSnapshot(
            InventoryLoadoutStateSnapshot imported)
        {
            if (imported == null)
            {
                return ImportRejected("production-loadout-import-null");
            }
            if (!imported.HasValidFingerprint())
            {
                return ImportRejected(
                    "production-loadout-import-fingerprint-invalid");
            }

            try
            {
                snapshot = RetiredArmorLoadoutCompatibility.GunOnly(
                    imported.Sequence,
                    imported.Bindings);
                lastAcceptedCommandFingerprint = string.Empty;
                return new InventoryLoadoutImportResult(
                    true,
                    string.Empty,
                    snapshot);
            }
            catch (ArgumentException exception)
            {
                return ImportRejected(
                    "production-loadout-import-invalid:"
                    + exception.Message);
            }
        }

        public InventoryLoadoutStateResult Apply(
            InventoryLoadoutStateCommand command)
        {
            if (command == null)
            {
                return Reject("production-loadout-command-null");
            }
            if (string.Equals(
                    command.Fingerprint,
                    lastAcceptedCommandFingerprint,
                    StringComparison.Ordinal))
            {
                return new InventoryLoadoutStateResult(
                    InventoryLoadoutStateMutationStatus.ExactRepeatNoChange,
                    string.Empty,
                    snapshot);
            }
            if (command.ExpectedSequence != snapshot.Sequence)
            {
                return new InventoryLoadoutStateResult(
                    InventoryLoadoutStateMutationStatus.StaleSnapshot,
                    "production-loadout-sequence-stale",
                    snapshot);
            }
            if (RetiredArmorLoadoutCompatibility.ContainsRetiredArmorBinding(
                    command.Bindings))
            {
                return Reject("retired-armor-loadout-slot-unsupported");
            }

            InventoryLoadoutStateSnapshot candidate;
            try
            {
                candidate = RetiredArmorLoadoutCompatibility.GunOnly(
                    checked(snapshot.Sequence + 1L),
                    command.Bindings);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is OverflowException)
            {
                return Reject(
                    "production-loadout-command-invalid:"
                    + exception.Message);
            }

            if (BindingsEqual(snapshot.Bindings, candidate.Bindings))
            {
                return new InventoryLoadoutStateResult(
                    InventoryLoadoutStateMutationStatus.ExactRepeatNoChange,
                    string.Empty,
                    snapshot);
            }

            snapshot = candidate;
            lastAcceptedCommandFingerprint = command.Fingerprint;
            return new InventoryLoadoutStateResult(
                InventoryLoadoutStateMutationStatus.Applied,
                string.Empty,
                snapshot);
        }

        private InventoryLoadoutImportResult ImportRejected(
            string rejectionCode)
        {
            return new InventoryLoadoutImportResult(
                false,
                rejectionCode,
                snapshot);
        }

        private InventoryLoadoutStateResult Reject(
            string rejectionCode)
        {
            return new InventoryLoadoutStateResult(
                InventoryLoadoutStateMutationStatus.Rejected,
                rejectionCode,
                snapshot);
        }

        private static bool BindingsEqual(
            IReadOnlyList<InventoryLoadoutSlotBinding> left,
            IReadOnlyList<InventoryLoadoutSlotBinding> right)
        {
            if (left == null
                || right == null
                || left.Count != right.Count)
            {
                return false;
            }
            for (int index = 0; index < left.Count; index++)
            {
                if (!left[index].Equals(right[index]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
