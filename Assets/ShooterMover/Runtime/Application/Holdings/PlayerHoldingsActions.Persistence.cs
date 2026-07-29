using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Holdings
{
    public sealed partial class PlayerHoldingsActions
    {
        public PlayerHoldingsSnapshot ExportSnapshot()
        {
            lock (sync)
            {
                var unique = new List<UniqueHoldingSnapshot>(
                    uniqueHoldings.Values);
                var stacks = new List<StackHoldingSnapshot>(
                    stackHoldings.Count);
                foreach (KeyValuePair<StableId, StackState> pair in stackHoldings)
                {
                    stacks.Add(StackHoldingSnapshot.Create(
                        pair.Value.RewardKind,
                        pair.Key,
                        pair.Value.Quantity));
                }

                var records = new List<PlayerHoldingsTransactionRecord>(
                    transactionRecords.Values);
                return PlayerHoldingsSnapshot.CreateCanonical(
                    PlayerHoldingsSnapshot.CurrentSchemaVersion,
                    AuthorityStableId,
                    MaximumStackQuantity,
                    ledger.ExportSnapshot(),
                    unique,
                    stacks,
                    records);
            }
        }

        public PlayerHoldingsImportResult ImportSnapshot(
            PlayerHoldingsSnapshot snapshot)
        {
            lock (sync)
            {
                if (snapshot == null)
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatus.InvalidSnapshot,
                        "snapshot-null");
                }

                if (snapshot.SchemaVersion
                    != PlayerHoldingsSnapshot.CurrentSchemaVersion)
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatus.UnsupportedSchemaVersion,
                        "unsupported-schema-version");
                }

                if (snapshot.AuthorityStableId != AuthorityStableId)
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatus.InvalidSnapshot,
                        "authority-mismatch");
                }

                if (snapshot.MaximumStackQuantity != MaximumStackQuantity)
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatus.InvalidSnapshot,
                        "maximum-stack-quantity-mismatch");
                }

                string computedFingerprint =
                    PlayerHoldingsSnapshot.ComputeFingerprint(snapshot);
                if (!HoldingsFormat.IsCanonicalFingerprint(
                        snapshot.Fingerprint)
                    || !string.Equals(
                        snapshot.Fingerprint,
                        computedFingerprint,
                        StringComparison.Ordinal))
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatus.FingerprintMismatch,
                        "snapshot-fingerprint-mismatch");
                }

                var importedLedger = CreateLedger();
                LedgerImportResult ledgerImport =
                    importedLedger.ImportSnapshot(snapshot.LedgerSnapshot);
                if (!ledgerImport.Succeeded)
                {
                    PlayerHoldingsImportStatus status =
                        ledgerImport.Status == LedgerImportStatus.UnsupportedSchemaVersion
                            ? PlayerHoldingsImportStatus.UnsupportedSchemaVersion
                            : ledgerImport.Status == LedgerImportStatus.FingerprintMismatch
                                ? PlayerHoldingsImportStatus.FingerprintMismatch
                                : PlayerHoldingsImportStatus.InvalidSnapshot;
                    return ImportFailure(status, ledgerImport.RejectionCode);
                }

                Dictionary<StableId, PlayerHoldingsTransactionRecord> importedRecords;
                string rejectionCode;
                if (!TryValidateTransactionRecords(
                    snapshot,
                    out importedRecords,
                    out rejectionCode))
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatus.InvalidSnapshot,
                        rejectionCode);
                }

                Dictionary<StableId, UniqueHoldingSnapshot> rebuiltUnique;
                Dictionary<StableId, UniqueIdentityHistory> rebuiltUniqueHistory;
                Dictionary<StableId, StackState> rebuiltStacks;
                Dictionary<StableId, RewardGrantKind> rebuiltStackHistory;
                if (!TryRebuildAppliedState(
                    snapshot,
                    out rebuiltUnique,
                    out rebuiltUniqueHistory,
                    out rebuiltStacks,
                    out rebuiltStackHistory,
                    out rejectionCode))
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatus.InvalidSnapshot,
                        rejectionCode);
                }

                if (!CurrentProjectionMatches(
                    snapshot,
                    rebuiltUnique,
                    rebuiltStacks))
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatus.InvalidSnapshot,
                        "current-projection-mismatch");
                }

                ledger = importedLedger;
                uniqueHoldings = rebuiltUnique;
                uniqueHistory = rebuiltUniqueHistory;
                stackHoldings = rebuiltStacks;
                stackKindHistory = rebuiltStackHistory;
                transactionRecords = importedRecords;

                return PlayerHoldingsImportResult.Create(
                    PlayerHoldingsImportStatus.Imported,
                    null,
                    ledger.Sequence);
            }
        }
    }
}
