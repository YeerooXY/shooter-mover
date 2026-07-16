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
    public sealed partial class PlayerHoldingsService
    {
        public PlayerHoldingsSnapshotV1 ExportSnapshot()
        {
            lock (sync)
            {
                var unique = new List<UniqueHoldingSnapshotV1>(
                    uniqueHoldings.Values);
                var stacks = new List<StackHoldingSnapshotV1>(
                    stackHoldings.Count);
                foreach (KeyValuePair<StableId, StackState> pair in stackHoldings)
                {
                    stacks.Add(StackHoldingSnapshotV1.Create(
                        pair.Value.RewardKind,
                        pair.Key,
                        pair.Value.Quantity));
                }

                var records = new List<PlayerHoldingsTransactionRecordV1>(
                    transactionRecords.Values);
                return PlayerHoldingsSnapshotV1.CreateCanonical(
                    PlayerHoldingsSnapshotV1.CurrentSchemaVersion,
                    AuthorityStableId,
                    MaximumStackQuantity,
                    ledger.ExportSnapshot(),
                    unique,
                    stacks,
                    records);
            }
        }

        public PlayerHoldingsImportResultV1 ImportSnapshot(
            PlayerHoldingsSnapshotV1 snapshot)
        {
            lock (sync)
            {
                if (snapshot == null)
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatusV1.InvalidSnapshot,
                        "snapshot-null");
                }

                if (snapshot.SchemaVersion
                    != PlayerHoldingsSnapshotV1.CurrentSchemaVersion)
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatusV1.UnsupportedSchemaVersion,
                        "unsupported-schema-version");
                }

                if (snapshot.AuthorityStableId != AuthorityStableId)
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatusV1.InvalidSnapshot,
                        "authority-mismatch");
                }

                if (snapshot.MaximumStackQuantity != MaximumStackQuantity)
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatusV1.InvalidSnapshot,
                        "maximum-stack-quantity-mismatch");
                }

                string computedFingerprint =
                    PlayerHoldingsSnapshotV1.ComputeFingerprint(snapshot);
                if (!HoldingsCanonicalV1.IsCanonicalFingerprint(
                        snapshot.Fingerprint)
                    || !string.Equals(
                        snapshot.Fingerprint,
                        computedFingerprint,
                        StringComparison.Ordinal))
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatusV1.FingerprintMismatch,
                        "snapshot-fingerprint-mismatch");
                }

                var importedLedger = CreateLedger();
                LedgerImportResult ledgerImport =
                    importedLedger.ImportSnapshot(snapshot.LedgerSnapshot);
                if (!ledgerImport.Succeeded)
                {
                    PlayerHoldingsImportStatusV1 status =
                        ledgerImport.Status == LedgerImportStatus.UnsupportedSchemaVersion
                            ? PlayerHoldingsImportStatusV1.UnsupportedSchemaVersion
                            : ledgerImport.Status == LedgerImportStatus.FingerprintMismatch
                                ? PlayerHoldingsImportStatusV1.FingerprintMismatch
                                : PlayerHoldingsImportStatusV1.InvalidSnapshot;
                    return ImportFailure(status, ledgerImport.RejectionCode);
                }

                Dictionary<StableId, PlayerHoldingsTransactionRecordV1> importedRecords;
                string rejectionCode;
                if (!TryValidateTransactionRecords(
                    snapshot,
                    out importedRecords,
                    out rejectionCode))
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatusV1.InvalidSnapshot,
                        rejectionCode);
                }

                Dictionary<StableId, UniqueHoldingSnapshotV1> rebuiltUnique;
                Dictionary<StableId, UniqueIdentityHistory> rebuiltUniqueHistory;
                Dictionary<StableId, StackState> rebuiltStacks;
                Dictionary<StableId, RewardGrantKindV1> rebuiltStackHistory;
                if (!TryRebuildAppliedState(
                    snapshot,
                    out rebuiltUnique,
                    out rebuiltUniqueHistory,
                    out rebuiltStacks,
                    out rebuiltStackHistory,
                    out rejectionCode))
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatusV1.InvalidSnapshot,
                        rejectionCode);
                }

                if (!CurrentProjectionMatches(
                    snapshot,
                    rebuiltUnique,
                    rebuiltStacks))
                {
                    return ImportFailure(
                        PlayerHoldingsImportStatusV1.InvalidSnapshot,
                        "current-projection-mismatch");
                }

                ledger = importedLedger;
                uniqueHoldings = rebuiltUnique;
                uniqueHistory = rebuiltUniqueHistory;
                stackHoldings = rebuiltStacks;
                stackKindHistory = rebuiltStackHistory;
                transactionRecords = importedRecords;

                return PlayerHoldingsImportResultV1.Create(
                    PlayerHoldingsImportStatusV1.Imported,
                    null,
                    ledger.Sequence);
            }
        }
    }
}
