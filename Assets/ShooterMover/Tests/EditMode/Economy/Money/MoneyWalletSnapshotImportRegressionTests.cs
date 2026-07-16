using System;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;

namespace ShooterMover.Tests.EditMode.Economy.Money
{
    public sealed class MoneyWalletSnapshotImportRegressionTests
    {
        [Test]
        public void ExportedSnapshotUsesDistinctCommandAndLedgerFingerprintFormats()
        {
            var wallet = new MoneyWalletService();
            wallet.Grant(
                Id("transaction.fingerprint-grant"),
                Id("operation.fingerprint-grant"),
                12L);

            MoneyWalletSnapshot snapshot = wallet.CurrentSnapshot;
            MoneyWalletContributionSnapshot contribution = snapshot.Contributions[0];
            MoneyWalletTransactionSnapshot transaction = snapshot.Transactions[0];

            Assert.That(snapshot.Fingerprint.Length, Is.EqualTo(64));
            Assert.That(IsLowerHex(snapshot.Fingerprint), Is.True);
            Assert.That(
                contribution.CommandFingerprint,
                Does.StartWith("sha256:"));
            Assert.That(contribution.CommandFingerprint.Length, Is.EqualTo(71));
            Assert.That(
                transaction.CommandFingerprint,
                Is.EqualTo(contribution.CommandFingerprint));
            Assert.That(transaction.MutationFingerprint.Length, Is.EqualTo(64));
            Assert.That(IsLowerHex(transaction.MutationFingerprint), Is.True);
        }

        [Test]
        public void ExportedSnapshotRoundTripsAndRetainsAppliedAndRejectedReplayFacts()
        {
            var source = new MoneyWalletService();
            MoneyTransactionCommand grant = MoneyTransactionCommand.CreateGrant(
                Id("transaction.regression-grant"),
                Id("operation.regression-grant"),
                40L);
            MoneyTransactionCommand rejected = MoneyTransactionCommand.CreateSpend(
                Id("transaction.regression-rejected"),
                Id("operation.regression-rejected"),
                50L,
                1L);
            source.Apply(grant);
            source.Apply(rejected);
            source.Spend(
                Id("transaction.regression-spend"),
                Id("operation.regression-spend"),
                5L,
                1L);
            MoneyWalletSnapshot exported = source.CurrentSnapshot;
            var restored = new MoneyWalletService();

            MoneyWalletImportResult imported = restored.ImportSnapshot(exported);

            Assert.That(
                imported.Status,
                Is.EqualTo(MoneyWalletImportStatus.Imported),
                imported.RejectionCode);
            Assert.That(restored.Balance, Is.EqualTo(35L));
            Assert.That(restored.Sequence, Is.EqualTo(2L));
            Assert.That(
                restored.CurrentSnapshot.Fingerprint,
                Is.EqualTo(exported.Fingerprint));

            MoneyWalletChangeFact duplicateGrant = restored.Apply(grant);
            MoneyWalletChangeFact duplicateRejected = restored.Apply(rejected);

            Assert.That(
                duplicateGrant.Status,
                Is.EqualTo(MoneyWalletTransactionStatus.DuplicateNoChange));
            Assert.That(
                duplicateGrant.OriginalStatus,
                Is.EqualTo(MoneyWalletTransactionStatus.Applied));
            Assert.That(
                duplicateRejected.Status,
                Is.EqualTo(MoneyWalletTransactionStatus.DuplicateNoChange));
            Assert.That(
                duplicateRejected.OriginalStatus,
                Is.EqualTo(MoneyWalletTransactionStatus.InsufficientFunds));
        }

        [Test]
        public void CanonicalButCorruptSnapshotFingerprintMapsToMismatchAtomically()
        {
            MoneyWalletSnapshot valid = CreateSourceSnapshot();
            char replacement = valid.Fingerprint[0] == '0' ? '1' : '0';
            string corruptFingerprint =
                replacement + valid.Fingerprint.Substring(1);
            var corrupt = new MoneyWalletSnapshot(
                valid.SchemaVersion,
                valid.Sequence,
                valid.Balance,
                valid.Contributions,
                valid.Transactions,
                corruptFingerprint);
            var target = CreateTargetWallet();
            MoneyWalletSnapshot before = target.CurrentSnapshot;

            MoneyWalletImportResult result = target.ImportSnapshot(corrupt);

            Assert.That(
                result.Status,
                Is.EqualTo(MoneyWalletImportStatus.FingerprintMismatch),
                result.RejectionCode);
            AssertUnchanged(target, before);
        }

        [Test]
        public void MalformedSnapshotFingerprintMapsToMismatchAtomically()
        {
            MoneyWalletSnapshot valid = CreateSourceSnapshot();
            var malformed = new MoneyWalletSnapshot(
                valid.SchemaVersion,
                valid.Sequence,
                valid.Balance,
                valid.Contributions,
                valid.Transactions,
                "sha256:" + new string('0', 64));
            var target = CreateTargetWallet();
            MoneyWalletSnapshot before = target.CurrentSnapshot;

            MoneyWalletImportResult result = target.ImportSnapshot(malformed);

            Assert.That(
                result.Status,
                Is.EqualTo(MoneyWalletImportStatus.FingerprintMismatch),
                result.RejectionCode);
            Assert.That(
                result.RejectionCode,
                Is.EqualTo("money-snapshot-fingerprint-invalid"));
            AssertUnchanged(target, before);
        }

        private static MoneyWalletSnapshot CreateSourceSnapshot()
        {
            var source = new MoneyWalletService();
            source.Grant(
                Id("transaction.regression-source"),
                Id("operation.regression-source"),
                20L);
            return source.CurrentSnapshot;
        }

        private static MoneyWalletService CreateTargetWallet()
        {
            var target = new MoneyWalletService();
            target.Grant(
                Id("transaction.regression-existing"),
                Id("operation.regression-existing"),
                7L);
            return target;
        }

        private static void AssertUnchanged(
            MoneyWalletService target,
            MoneyWalletSnapshot before)
        {
            Assert.That(target.Balance, Is.EqualTo(7L));
            Assert.That(target.Sequence, Is.EqualTo(1L));
            Assert.That(
                target.CurrentSnapshot.Fingerprint,
                Is.EqualTo(before.Fingerprint));
        }

        private static bool IsLowerHex(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                bool digit = current >= '0' && current <= '9';
                bool lowerHex = current >= 'a' && current <= 'f';
                if (!digit && !lowerHex)
                {
                    return false;
                }
            }

            return true;
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
