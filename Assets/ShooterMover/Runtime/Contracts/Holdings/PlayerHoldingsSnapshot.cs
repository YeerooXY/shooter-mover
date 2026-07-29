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
    public sealed class PlayerHoldingsSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<UniqueHoldingSnapshot> uniqueHoldings;
        private readonly ReadOnlyCollection<StackHoldingSnapshot> stackHoldings;
        private readonly ReadOnlyCollection<PlayerHoldingsTransactionRecord> transactions;

        public PlayerHoldingsSnapshot(
            int schemaVersion,
            StableId authorityStableId,
            long maximumStackQuantity,
            LedgerSnapshot<HoldingsLedgerVocabulary> ledgerSnapshot,
            IEnumerable<UniqueHoldingSnapshot> uniqueHoldings,
            IEnumerable<StackHoldingSnapshot> stackHoldings,
            IEnumerable<PlayerHoldingsTransactionRecord> transactions,
            string fingerprint)
        {
            SchemaVersion = schemaVersion;
            AuthorityStableId = authorityStableId
                ?? throw new ArgumentNullException(nameof(authorityStableId));
            MaximumStackQuantity = maximumStackQuantity;
            LedgerSnapshot = ledgerSnapshot
                ?? throw new ArgumentNullException(nameof(ledgerSnapshot));
            this.uniqueHoldings = HoldingsFormat.CopyAndSort(
                uniqueHoldings,
                delegate(UniqueHoldingSnapshot left, UniqueHoldingSnapshot right)
                {
                    return left.CompareTo(right);
                },
                nameof(uniqueHoldings));
            this.stackHoldings = HoldingsFormat.CopyAndSort(
                stackHoldings,
                delegate(StackHoldingSnapshot left, StackHoldingSnapshot right)
                {
                    return left.CompareTo(right);
                },
                nameof(stackHoldings));
            this.transactions = HoldingsFormat.CopyAndSort(
                transactions,
                delegate(PlayerHoldingsTransactionRecord left, PlayerHoldingsTransactionRecord right)
                {
                    return left.CompareTo(right);
                },
                nameof(transactions));
            Fingerprint = fingerprint;
        }

        public int SchemaVersion { get; }

        public StableId AuthorityStableId { get; }

        public long MaximumStackQuantity { get; }

        public LedgerSnapshot<HoldingsLedgerVocabulary> LedgerSnapshot { get; }

        public IReadOnlyList<UniqueHoldingSnapshot> UniqueHoldings
        {
            get { return uniqueHoldings; }
        }

        public IReadOnlyList<StackHoldingSnapshot> StackHoldings
        {
            get { return stackHoldings; }
        }

        public IReadOnlyList<PlayerHoldingsTransactionRecord> Transactions
        {
            get { return transactions; }
        }

        public string Fingerprint { get; }

        public static PlayerHoldingsSnapshot CreateCanonical(
            int schemaVersion,
            StableId authorityStableId,
            long maximumStackQuantity,
            LedgerSnapshot<HoldingsLedgerVocabulary> ledgerSnapshot,
            IEnumerable<UniqueHoldingSnapshot> uniqueHoldings,
            IEnumerable<StackHoldingSnapshot> stackHoldings,
            IEnumerable<PlayerHoldingsTransactionRecord> transactions)
        {
            var withoutFingerprint = new PlayerHoldingsSnapshot(
                schemaVersion,
                authorityStableId,
                maximumStackQuantity,
                ledgerSnapshot,
                uniqueHoldings,
                stackHoldings,
                transactions,
                string.Empty);
            string fingerprint = ComputeFingerprint(withoutFingerprint);
            return new PlayerHoldingsSnapshot(
                schemaVersion,
                authorityStableId,
                maximumStackQuantity,
                ledgerSnapshot,
                withoutFingerprint.UniqueHoldings,
                withoutFingerprint.StackHoldings,
                withoutFingerprint.Transactions,
                fingerprint);
        }

        public static string ComputeFingerprint(PlayerHoldingsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var builder = new StringBuilder();
            HoldingsFormat.AppendToken(
                builder,
                "schema_version",
                snapshot.SchemaVersion.ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "authority_stable_id",
                snapshot.AuthorityStableId.ToString());
            HoldingsFormat.AppendToken(
                builder,
                "maximum_stack_quantity",
                snapshot.MaximumStackQuantity.ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "ledger_fingerprint",
                snapshot.LedgerSnapshot.Fingerprint ?? "null");
            HoldingsFormat.AppendToken(
                builder,
                "unique_count",
                snapshot.UniqueHoldings.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
            {
                HoldingsFormat.AppendToken(
                    builder,
                    "unique_" + index.ToString(CultureInfo.InvariantCulture),
                    snapshot.UniqueHoldings[index].ToCanonicalString());
            }

            HoldingsFormat.AppendToken(
                builder,
                "stack_count",
                snapshot.StackHoldings.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.StackHoldings.Count; index++)
            {
                HoldingsFormat.AppendToken(
                    builder,
                    "stack_" + index.ToString(CultureInfo.InvariantCulture),
                    snapshot.StackHoldings[index].ToCanonicalString());
            }

            HoldingsFormat.AppendToken(
                builder,
                "transaction_count",
                snapshot.Transactions.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.Transactions.Count; index++)
            {
                HoldingsFormat.AppendToken(
                    builder,
                    "transaction_" + index.ToString(CultureInfo.InvariantCulture),
                    snapshot.Transactions[index].ToCanonicalString());
            }

            return HoldingsFormat.ComputeSha256(builder.ToString());
        }
    }

}
