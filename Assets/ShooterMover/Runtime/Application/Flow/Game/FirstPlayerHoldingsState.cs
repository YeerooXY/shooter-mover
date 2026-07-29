using System;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Application.Flow.Game
{
    /// <summary>
    /// Compatibility boundary for reward systems that still emit generic equipment commands.
    /// Gun ownership is committed to the canonical authority first. The generic authority is
    /// retained only as the immutable reward/strongbox receipt ledger. Any rejection or exception
    /// compensates both authorities from captured immutable snapshots, preventing ghost guns.
    /// </summary>
    public sealed class FirstPlayerHoldingsState :
        IPlayerHoldingsState
    {
        private readonly IPlayerHoldingsState receipts;
        private readonly GunInventoryState guns;

        public FirstPlayerHoldingsState(
            IPlayerHoldingsState receiptAuthority,
            GunInventoryState gunAuthority)
        {
            receipts = receiptAuthority
                ?? throw new ArgumentNullException(nameof(receiptAuthority));
            guns = gunAuthority
                ?? throw new ArgumentNullException(nameof(gunAuthority));
        }

        public StableId AuthorityStableId { get { return receipts.AuthorityStableId; } }
        public long Sequence { get { return receipts.Sequence; } }

        public PlayerHoldingsSnapshot ExportSnapshot()
        {
            return receipts.ExportSnapshot();
        }

        public PlayerHoldingsImportResult ImportSnapshot(
            PlayerHoldingsSnapshot snapshot)
        {
            // V1 dual-read migration occurs before this runtime boundary is created. Importing the
            // receipt ledger must never overwrite or recreate canonical V2 ownership.
            return receipts.ImportSnapshot(snapshot);
        }

        public PlayerHoldingsMutationResult Apply(
            PlayerHoldingsCommand command)
        {
            GunItem canonical;
            if (!TryResolveCanonicalMutation(command, out canonical))
            {
                return receipts.Apply(command);
            }

            GunInventorySnapshot gunsBefore = guns.ExportSnapshot();
            PlayerHoldingsSnapshot receiptsBefore = receipts.ExportSnapshot();
            if (receiptsBefore == null)
            {
                throw new InvalidOperationException(
                    "Canonical gun receipt authority exported a null snapshot.");
            }

            string rejectionCode;
            bool canonicalAccepted;
            if (command.Transaction.Operation
                == EconomyTransactionOperation.AddUnique)
            {
                canonicalAccepted = guns.TryAdd(
                    canonical,
                    out rejectionCode);
            }
            else
            {
                canonicalAccepted = guns.TryRemove(
                    command.Transaction.InstanceStableId,
                    out rejectionCode);
            }

            if (!canonicalAccepted)
            {
                throw new InvalidOperationException(
                    "Canonical gun ownership mutation failed before receipt write: "
                    + rejectionCode);
            }

            try
            {
                PlayerHoldingsMutationResult result = receipts.Apply(command);
                if (!IsReceiptAcceptance(result))
                {
                    RollBack(gunsBefore, receiptsBefore);
                }
                return result;
            }
            catch
            {
                RollBack(gunsBefore, receiptsBefore);
                throw;
            }
        }

        private bool TryResolveCanonicalMutation(
            PlayerHoldingsCommand command,
            out GunItem canonical)
        {
            canonical = null;
            if (command == null
                || command.RewardKind != RewardGrantKind.EquipmentReference)
            {
                return false;
            }

            if (command.Transaction.Operation
                == EconomyTransactionOperation.AddUnique)
            {
                if (command.EquipmentInstance == null)
                {
                    return false;
                }
                EquipmentDefinition definition =
                    GunCatalogProvider.EquipmentCatalog
                        .FindEquipmentDefinition(
                            command.EquipmentInstance.DefinitionId);
                if (definition == null
                    || definition.CategoryId != EquipmentCategoryIds.Gun)
                {
                    return false;
                }
                if (!GunInventoryMigration.TryConvertEquipment(
                        command.EquipmentInstance,
                        out canonical)
                    || canonical == null)
                {
                    throw new InvalidOperationException(
                        "A production gun reward cannot be represented by the canonical "
                        + "GunItem contract: "
                        + command.EquipmentInstance.InstanceId);
                }

                GunMark mark;
                bool definitionResolved = GunCatalogProvider.Current
                    .TryGetMark(canonical.GunDefinitionId.Value, out mark)
                    && mark != null;
                GunOperationAvailability availability =
                    GunSafetyPolicy.EvaluateRewardAcceptance(
                        canonical,
                        definitionResolved);
                if (!availability.IsAvailable)
                {
                    throw new InvalidOperationException(
                        availability.RejectionCode + ": " + availability.Message);
                }
                return true;
            }

            if (command.Transaction.Operation
                    != EconomyTransactionOperation.RemoveUnique
                || command.Transaction.InstanceStableId == null)
            {
                return false;
            }

            canonical = guns.Find(command.Transaction.InstanceStableId);
            if (canonical != null)
            {
                GunMark mark;
                if (!GunCatalogProvider.Current.TryGetMark(
                        canonical.GunDefinitionId.Value,
                        out mark)
                    || mark == null)
                {
                    throw new InvalidOperationException(
                        "canonical-gun-definition-unresolved: "
                        + canonical.GunDefinitionId.Value);
                }
                return true;
            }

            // An exact duplicate removal after both authorities accepted the original command may
            // legitimately find neither record. A retained equipment receipt without canonical
            // ownership must be classified before destruction. Unknown classification fails closed;
            // a recognized gun receipt proves that canonical ownership has already drifted.
            PlayerHoldingsSnapshot snapshot = receipts.ExportSnapshot();
            if (snapshot != null)
            {
                for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
                {
                    UniqueHoldingSnapshot holding = snapshot.UniqueHoldings[index];
                    if (holding == null
                        || holding.InstanceStableId
                            != command.Transaction.InstanceStableId
                        || holding.EquipmentInstance == null)
                    {
                        continue;
                    }
                    EquipmentDefinition definition =
                        GunCatalogProvider.EquipmentCatalog
                            .FindEquipmentDefinition(
                                holding.EquipmentInstance.DefinitionId);
                    if (definition == null)
                    {
                        throw new InvalidOperationException(
                            "canonical-gun-definition-unresolved: "
                            + holding.EquipmentInstance.DefinitionId);
                    }
                    if (definition.CategoryId == EquipmentCategoryIds.Gun)
                    {
                        throw new InvalidOperationException(
                            "A retained gun receipt is missing canonical ownership: "
                            + command.Transaction.InstanceStableId);
                    }
                    break;
                }
            }
            return false;
        }

        private static bool IsReceiptAcceptance(
            PlayerHoldingsMutationResult result)
        {
            return result != null
                && (result.Status == PlayerHoldingsMutationStatus.Applied
                    || result.Status
                        == PlayerHoldingsMutationStatus.ExactDuplicateNoChange);
        }

        private void RollBack(
            GunInventorySnapshot gunsBefore,
            PlayerHoldingsSnapshot receiptsBefore)
        {
            GunInventoryImportResult gunRollback =
                guns.ImportSnapshot(gunsBefore);
            PlayerHoldingsImportResult receiptRollback =
                receipts.ImportSnapshot(receiptsBefore);
            if (gunRollback == null
                || !gunRollback.Succeeded
                || receiptRollback == null
                || !receiptRollback.Succeeded)
            {
                throw new InvalidOperationException(
                    "Canonical gun ownership/receipt compensation failed: "
                    + (gunRollback == null
                            ? "gun-result-null"
                            : gunRollback.RejectionCode)
                    + ";"
                    + (receiptRollback == null
                            ? "receipt-result-null"
                            : receiptRollback.RejectionCode));
            }
        }
    }
}
