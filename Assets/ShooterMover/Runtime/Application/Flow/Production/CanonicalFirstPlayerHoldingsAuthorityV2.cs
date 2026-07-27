using System;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// Compatibility boundary for reward systems that still emit generic equipment commands.
    /// Weapon ownership is committed to the canonical authority first. The generic authority is
    /// retained only as the immutable reward/strongbox receipt ledger. A rejected or exceptional
    /// receipt write restores the exact canonical snapshot, preventing ghost weapons.
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

            WeaponHoldingsSnapshotV2 before = weapons.ExportSnapshot();
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
                    RollBack(before);
                }
                return result;
            }
            catch
            {
                RollBack(before);
                throw;
            }
        }

        private bool TryResolveCanonicalMutation(
            PlayerHoldingsCommandV1 command,
            out WeaponEquipmentInstance canonical)
        {
            canonical = null;
            if (command == null
                || command.Transaction == null
                || command.RewardKind != RewardGrantKindV1.EquipmentReference)
            {
                return false;
            }

            if (command.Transaction.Operation
                == EconomyTransactionOperationV1.AddUnique)
            {
                return command.EquipmentInstance != null
                    && ProductionWeaponHoldingsMigrationV2.TryConvertEquipment(
                        command.EquipmentInstance,
                        out canonical);
            }

            if (command.Transaction.Operation
                    == EconomyTransactionOperationV1.RemoveUnique
                && command.Transaction.InstanceStableId != null)
            {
                canonical = weapons.Find(command.Transaction.InstanceStableId);
                return canonical != null;
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

        private void RollBack(WeaponHoldingsSnapshotV2 before)
        {
            WeaponHoldingsImportResultV2 rollback = weapons.ImportSnapshot(before);
            if (rollback == null || !rollback.Succeeded)
            {
                throw new InvalidOperationException(
                    "Canonical weapon ownership rollback failed: "
                    + (rollback == null
                        ? "result-null"
                        : rollback.RejectionCode));
            }
        }
    }
}
