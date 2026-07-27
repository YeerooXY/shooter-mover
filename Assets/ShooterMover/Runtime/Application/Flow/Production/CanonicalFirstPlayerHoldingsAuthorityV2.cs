using System;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// Compatibility boundary for reward systems that still emit generic equipment commands.
    /// Weapon ownership is committed to the canonical authority first. The generic authority is
    /// retained only as the immutable reward/strongbox receipt ledger. Any rejection or exception
    /// compensates both authorities from captured immutable snapshots, preventing ghost weapons.
    /// </summary>
    public sealed class CanonicalFirstPlayerHoldingsAuthorityV2 :
        IPlayerHoldingsAuthorityV1
    {
        private readonly IPlayerHoldingsAuthorityV1 receipts;
        private readonly ProductionWeaponHoldingsAuthorityV2 weapons;

        public CanonicalFirstPlayerHoldingsAuthorityV2(
            IPlayerHoldingsAuthorityV1 receiptAuthority,
            ProductionWeaponHoldingsAuthorityV2 weaponAuthority)
        {
            receipts = receiptAuthority
                ?? throw new ArgumentNullException(nameof(receiptAuthority));
            weapons = weaponAuthority
                ?? throw new ArgumentNullException(nameof(weaponAuthority));
        }

        public StableId AuthorityStableId { get { return receipts.AuthorityStableId; } }
        public long Sequence { get { return receipts.Sequence; } }

        public PlayerHoldingsSnapshotV1 ExportSnapshot()
        {
            return receipts.ExportSnapshot();
        }

        public PlayerHoldingsImportResultV1 ImportSnapshot(
            PlayerHoldingsSnapshotV1 snapshot)
        {
            // V1 dual-read migration occurs before this runtime boundary is created. Importing the
            // receipt ledger must never overwrite or recreate canonical V2 ownership.
            return receipts.ImportSnapshot(snapshot);
        }

        public PlayerHoldingsMutationResultV1 Apply(
            PlayerHoldingsCommandV1 command)
        {
            WeaponEquipmentInstance canonical;
            if (!TryResolveCanonicalMutation(command, out canonical))
            {
                return receipts.Apply(command);
            }

            WeaponHoldingsSnapshotV2 weaponsBefore = weapons.ExportSnapshot();
            PlayerHoldingsSnapshotV1 receiptsBefore = receipts.ExportSnapshot();
            if (receiptsBefore == null)
            {
                throw new InvalidOperationException(
                    "Canonical weapon receipt authority exported a null snapshot.");
            }

            string rejectionCode;
            bool canonicalAccepted;
            if (command.Transaction.Operation
                == EconomyTransactionOperationV1.AddUnique)
            {
                canonicalAccepted = weapons.TryAdd(
                    canonical,
                    out rejectionCode);
            }
            else
            {
                canonicalAccepted = weapons.TryRemove(
                    command.Transaction.InstanceStableId,
                    out rejectionCode);
            }

            if (!canonicalAccepted)
            {
                throw new InvalidOperationException(
                    "Canonical weapon ownership mutation failed before receipt write: "
                    + rejectionCode);
            }

            try
            {
                PlayerHoldingsMutationResultV1 result = receipts.Apply(command);
                if (!IsReceiptAcceptance(result))
                {
                    RollBack(weaponsBefore, receiptsBefore);
                }
                return result;
            }
            catch
            {
                RollBack(weaponsBefore, receiptsBefore);
                throw;
            }
        }

        private bool TryResolveCanonicalMutation(
            PlayerHoldingsCommandV1 command,
            out WeaponEquipmentInstance canonical)
        {
            canonical = null;
            if (command == null
                || command.RewardKind != RewardGrantKindV1.EquipmentReference)
            {
                return false;
            }

            if (command.Transaction.Operation
                == EconomyTransactionOperationV1.AddUnique)
            {
                if (command.EquipmentInstance == null)
                {
                    return false;
                }
                EquipmentDefinition definition =
                    ProductionWeaponCatalogProvider.EquipmentCatalog
                        .FindEquipmentDefinition(
                            command.EquipmentInstance.DefinitionId);
                if (definition == null
                    || definition.CategoryId != EquipmentCategoryIds.Weapon)
                {
                    return false;
                }
                if (!ProductionWeaponHoldingsMigrationV2.TryConvertEquipment(
                        command.EquipmentInstance,
                        out canonical)
                    || canonical == null)
                {
                    throw new InvalidOperationException(
                        "A production weapon reward cannot be represented by the canonical "
                        + "WeaponEquipmentInstance contract: "
                        + command.EquipmentInstance.InstanceId);
                }

                ProductionWeaponMarkV1 mark;
                bool definitionResolved = ProductionWeaponCatalogProvider.Current
                    .TryGetMark(canonical.WeaponDefinitionId.Value, out mark)
                    && mark != null;
                CanonicalWeaponOperationAvailabilityV1 availability =
                    CanonicalWeaponSafetyPolicyV1.EvaluateRewardAcceptance(
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
                    != EconomyTransactionOperationV1.RemoveUnique
                || command.Transaction.InstanceStableId == null)
            {
                return false;
            }

            canonical = weapons.Find(command.Transaction.InstanceStableId);
            if (canonical != null)
            {
                ProductionWeaponMarkV1 mark;
                if (!ProductionWeaponCatalogProvider.Current.TryGetMark(
                        canonical.WeaponDefinitionId.Value,
                        out mark)
                    || mark == null)
                {
                    throw new InvalidOperationException(
                        "canonical-weapon-definition-unresolved: "
                        + canonical.WeaponDefinitionId.Value);
                }
                return true;
            }

            // An exact duplicate removal after both authorities accepted the original command may
            // legitimately find neither record. A retained equipment receipt without canonical
            // ownership must be classified before destruction. Unknown classification fails closed;
            // a recognized weapon receipt proves that canonical ownership has already drifted.
            PlayerHoldingsSnapshotV1 snapshot = receipts.ExportSnapshot();
            if (snapshot != null)
            {
                for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
                {
                    UniqueHoldingSnapshotV1 holding = snapshot.UniqueHoldings[index];
                    if (holding == null
                        || holding.InstanceStableId
                            != command.Transaction.InstanceStableId
                        || holding.EquipmentInstance == null)
                    {
                        continue;
                    }
                    EquipmentDefinition definition =
                        ProductionWeaponCatalogProvider.EquipmentCatalog
                            .FindEquipmentDefinition(
                                holding.EquipmentInstance.DefinitionId);
                    if (definition == null)
                    {
                        throw new InvalidOperationException(
                            "canonical-weapon-definition-unresolved: "
                            + holding.EquipmentInstance.DefinitionId);
                    }
                    if (definition.CategoryId == EquipmentCategoryIds.Weapon)
                    {
                        throw new InvalidOperationException(
                            "A retained weapon receipt is missing canonical ownership: "
                            + command.Transaction.InstanceStableId);
                    }
                    break;
                }
            }
            return false;
        }

        private static bool IsReceiptAcceptance(
            PlayerHoldingsMutationResultV1 result)
        {
            return result != null
                && (result.Status == PlayerHoldingsMutationStatusV1.Applied
                    || result.Status
                        == PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange);
        }

        private void RollBack(
            WeaponHoldingsSnapshotV2 weaponsBefore,
            PlayerHoldingsSnapshotV1 receiptsBefore)
        {
            WeaponHoldingsImportResultV2 weaponRollback =
                weapons.ImportSnapshot(weaponsBefore);
            PlayerHoldingsImportResultV1 receiptRollback =
                receipts.ImportSnapshot(receiptsBefore);
            if (weaponRollback == null
                || !weaponRollback.Succeeded
                || receiptRollback == null
                || !receiptRollback.Succeeded)
            {
                throw new InvalidOperationException(
                    "Canonical weapon ownership/receipt compensation failed: "
                    + (weaponRollback == null
                            ? "weapon-result-null"
                            : weaponRollback.RejectionCode)
                    + ";"
                    + (receiptRollback == null
                            ? "receipt-result-null"
                            : receiptRollback.RejectionCode));
            }
        }
    }
}
