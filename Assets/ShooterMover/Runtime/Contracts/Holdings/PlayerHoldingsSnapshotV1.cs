using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Economy;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Contracts.Holdings
{
    /// <summary>
    /// Immutable canonical authority snapshot. The nested ledger snapshot carries
    /// the exact-once transaction facts; holdings records carry typed immutable
    /// payload/provenance needed to validate and rebuild ownership atomically.
    /// </summary>
    public sealed class PlayerHoldingsSnapshotV1
    {
        public const int CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<UniqueHoldingSnapshotV1> uniqueHoldings;
        private readonly ReadOnlyCollection<StackHoldingSnapshotV1> stackHoldings;
        private readonly ReadOnlyCollection<PlayerHoldingsTransactionRecordV1> transactions;

        public PlayerHoldingsSnapshotV1(
            int schemaVersion,
            StableId authorityStableId,
            long maximumStackQuantity,
            LedgerSnapshot<HoldingsLedgerVocabularyV1> ledgerSnapshot,
            IEnumerable<UniqueHoldingSnapshotV1> uniqueHoldings,
            IEnumerable<StackHoldingSnapshotV1> stackHoldings,
            IEnumerable<PlayerHoldingsTransactionRecordV1> transactions,
            string fingerprint)
        {
            SchemaVersion = schemaVersion;
            AuthorityStableId = authorityStableId
                ?? throw new ArgumentNullException(nameof(authorityStableId));
            MaximumStackQuantity = maximumStackQuantity;
            LedgerSnapshot = ledgerSnapshot
                ?? throw new ArgumentNullException(nameof(ledgerSnapshot));
            this.uniqueHoldings = HoldingsCanonicalV1.CopyAndSort(
                uniqueHoldings,
                delegate(UniqueHoldingSnapshotV1 left, UniqueHoldingSnapshotV1 right)
                {
                    return left.CompareTo(right);
                },
                nameof(uniqueHoldings));
            this.stackHoldings = HoldingsCanonicalV1.CopyAndSort(
                stackHoldings,
                delegate(StackHoldingSnapshotV1 left, StackHoldingSnapshotV1 right)
                {
                    return left.CompareTo(right);
                },
                nameof(stackHoldings));
            this.transactions = HoldingsCanonicalV1.CopyAndSort(
                transactions,
                delegate(PlayerHoldingsTransactionRecordV1 left, PlayerHoldingsTransactionRecordV1 right)
                {
                    return left.CompareTo(right);
                },
                nameof(transactions));
            Fingerprint = fingerprint;
        }

        public int SchemaVersion { get; }

        public StableId AuthorityStableId { get; }

        public long MaximumStackQuantity { get; }

        public LedgerSnapshot<HoldingsLedgerVocabularyV1> LedgerSnapshot { get; }

        public IReadOnlyList<UniqueHoldingSnapshotV1> UniqueHoldings
        {
            get { return uniqueHoldings; }
        }

        public IReadOnlyList<StackHoldingSnapshotV1> StackHoldings
        {
            get { return stackHoldings; }
        }

        public IReadOnlyList<PlayerHoldingsTransactionRecordV1> Transactions
        {
            get { return transactions; }
        }

        public string Fingerprint { get; }

        public static PlayerHoldingsSnapshotV1 CreateCanonical(
            int schemaVersion,
            StableId authorityStableId,
            long maximumStackQuantity,
            LedgerSnapshot<HoldingsLedgerVocabularyV1> ledgerSnapshot,
            IEnumerable<UniqueHoldingSnapshotV1> uniqueHoldings,
            IEnumerable<StackHoldingSnapshotV1> stackHoldings,
            IEnumerable<PlayerHoldingsTransactionRecordV1> transactions)
        {
            var withoutFingerprint = new PlayerHoldingsSnapshotV1(
                schemaVersion,
                authorityStableId,
                maximumStackQuantity,
                ledgerSnapshot,
                uniqueHoldings,
                stackHoldings,
                transactions,
                string.Empty);
            string fingerprint = ComputeFingerprint(withoutFingerprint);
            return new PlayerHoldingsSnapshotV1(
                schemaVersion,
                authorityStableId,
                maximumStackQuantity,
                ledgerSnapshot,
                withoutFingerprint.UniqueHoldings,
                withoutFingerprint.StackHoldings,
                withoutFingerprint.Transactions,
                fingerprint);
        }

        public static string ComputeFingerprint(PlayerHoldingsSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var builder = new StringBuilder();
            HoldingsCanonicalV1.AppendToken(
                builder,
                "schema_version",
                snapshot.SchemaVersion.ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "authority_stable_id",
                snapshot.AuthorityStableId.ToString());
            HoldingsCanonicalV1.AppendToken(
                builder,
                "maximum_stack_quantity",
                snapshot.MaximumStackQuantity.ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "ledger_fingerprint",
                snapshot.LedgerSnapshot.Fingerprint ?? "null");
            HoldingsCanonicalV1.AppendToken(
                builder,
                "unique_count",
                snapshot.UniqueHoldings.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
            {
                HoldingsCanonicalV1.AppendToken(
                    builder,
                    "unique_" + index.ToString(CultureInfo.InvariantCulture),
                    snapshot.UniqueHoldings[index].ToCanonicalString());
            }

            HoldingsCanonicalV1.AppendToken(
                builder,
                "stack_count",
                snapshot.StackHoldings.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.StackHoldings.Count; index++)
            {
                HoldingsCanonicalV1.AppendToken(
                    builder,
                    "stack_" + index.ToString(CultureInfo.InvariantCulture),
                    snapshot.StackHoldings[index].ToCanonicalString());
            }

            HoldingsCanonicalV1.AppendToken(
                builder,
                "transaction_count",
                snapshot.Transactions.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.Transactions.Count; index++)
            {
                HoldingsCanonicalV1.AppendToken(
                    builder,
                    "transaction_" + index.ToString(CultureInfo.InvariantCulture),
                    snapshot.Transactions[index].ToCanonicalString());
            }

            return HoldingsCanonicalV1.ComputeSha256(builder.ToString());
        }
    }

}
