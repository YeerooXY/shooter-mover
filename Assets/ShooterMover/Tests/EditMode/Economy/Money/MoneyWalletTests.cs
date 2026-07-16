using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;

namespace ShooterMover.Tests.EditMode.Economy.Money
{
    public sealed class MoneyWalletTests
    {
        [Test]
        public void GrantAndBoundedSpendApplyWithImmutableChangeFacts()
        {
            var wallet = new MoneyWalletService();

            MoneyWalletChangeFact granted = wallet.Grant(
                Id("transaction.grant-001"),
                Id("operation.reward-001"),
                75L,
                0L);
            MoneyWalletChangeFact spent = wallet.Spend(
                Id("transaction.spend-001"),
                Id("operation.shop-001"),
                20L,
                1L);

            Assert.That(granted.Status, Is.EqualTo(MoneyWalletTransactionStatus.Applied));
            Assert.That(granted.Changed, Is.True);
            Assert.That(granted.PreviousSnapshot.Balance, Is.Zero);
            Assert.That(granted.CurrentSnapshot.Balance, Is.EqualTo(75L));
            Assert.That(spent.Status, Is.EqualTo(MoneyWalletTransactionStatus.Applied));
            Assert.That(spent.PreviousSnapshot.Balance, Is.EqualTo(75L));
            Assert.That(spent.CurrentSnapshot.Balance, Is.EqualTo(55L));
            Assert.That(wallet.Balance, Is.EqualTo(55L));
            Assert.That(wallet.Sequence, Is.EqualTo(2L));
            Assert.That(granted.CurrentSnapshot.Balance, Is.EqualTo(75L));
        }

        [Test]
        public void InsufficientFundsRejectWithoutMutationAndReplayDeterministically()
        {
            var wallet = new MoneyWalletService();
            wallet.Grant(Id("transaction.seed-001"), Id("operation.seed-001"), 10L);
            MoneyTransactionCommand rejectedCommand = MoneyTransactionCommand.CreateSpend(
                Id("transaction.spend-rejected"),
                Id("operation.shop-rejected"),
                11L,
                1L);

            MoneyWalletChangeFact first = wallet.Apply(rejectedCommand);
            wallet.Grant(Id("transaction.seed-002"), Id("operation.seed-002"), 100L, 1L);
            MoneyWalletChangeFact duplicate = wallet.Apply(rejectedCommand);

            Assert.That(first.Status, Is.EqualTo(MoneyWalletTransactionStatus.InsufficientFunds));
            Assert.That(first.Changed, Is.False);
            Assert.That(first.PreviousSnapshot, Is.SameAs(first.CurrentSnapshot));
            Assert.That(first.CurrentSnapshot.Balance, Is.EqualTo(10L));
            Assert.That(first.CurrentSnapshot.Sequence, Is.EqualTo(1L));
            Assert.That(duplicate.Status, Is.EqualTo(MoneyWalletTransactionStatus.DuplicateNoChange));
            Assert.That(
                duplicate.OriginalStatus,
                Is.EqualTo(MoneyWalletTransactionStatus.InsufficientFunds));
            Assert.That(duplicate.Changed, Is.False);
            Assert.That(duplicate.PreviousSnapshot.Balance, Is.EqualTo(110L));
            Assert.That(duplicate.CurrentSnapshot.Balance, Is.EqualTo(110L));
            Assert.That(wallet.Sequence, Is.EqualTo(2L));
        }

        [TestCase("currency.scrap")]
        [TestCase("currency.unknown")]
        public void NonMoneyCurrencyRejectsAndIsIdempotent(string currencyText)
        {
            var wallet = new MoneyWalletService();
            MoneyTransactionCommand command = MoneyTransactionCommand.CreateGrant(
                Id("transaction.wrong-currency"),
                Id("operation.reward-wrong-currency"),
                Id(currencyText),
                5L);

            MoneyWalletChangeFact first = wallet.Apply(command);
            MoneyWalletChangeFact duplicate = wallet.Apply(command);

            Assert.That(first.Status, Is.EqualTo(MoneyWalletTransactionStatus.WrongCurrency));
            Assert.That(first.Changed, Is.False);
            Assert.That(duplicate.Status, Is.EqualTo(MoneyWalletTransactionStatus.DuplicateNoChange));
            Assert.That(
                duplicate.OriginalStatus,
                Is.EqualTo(MoneyWalletTransactionStatus.WrongCurrency));
            Assert.That(wallet.Balance, Is.Zero);
            Assert.That(wallet.Sequence, Is.Zero);
        }

        [TestCase(0L)]
        [TestCase(-1L)]
        public void InvalidAmountsRejectWithoutAdmission(long amount)
        {
            var wallet = new MoneyWalletService();

            MoneyWalletChangeFact grant = wallet.Grant(
                Id("transaction.invalid-grant"),
                Id("operation.invalid-grant"),
                amount);
            MoneyWalletChangeFact spend = wallet.Spend(
                Id("transaction.invalid-spend"),
                Id("operation.invalid-spend"),
                amount);

            Assert.That(grant.Status, Is.EqualTo(MoneyWalletTransactionStatus.InvalidAmount));
            Assert.That(spend.Status, Is.EqualTo(MoneyWalletTransactionStatus.InvalidAmount));
            Assert.That(grant.Changed, Is.False);
            Assert.That(spend.Changed, Is.False);
            Assert.That(wallet.CurrentSnapshot.Transactions, Is.Empty);
            Assert.That(wallet.Sequence, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MoneyTransactionCommand.CreateGrant(
                    Id("transaction.invalid-command"),
                    Id("operation.invalid-command"),
                    amount));
        }

        [Test]
        public void CheckedOverflowRejectsAndExactRetryReturnsNoChange()
        {
            var wallet = new MoneyWalletService();
            wallet.Grant(
                Id("transaction.max"),
                Id("operation.max"),
                long.MaxValue);
            MoneyTransactionCommand overflow = MoneyTransactionCommand.CreateGrant(
                Id("transaction.overflow"),
                Id("operation.overflow"),
                1L);

            MoneyWalletChangeFact first = wallet.Apply(overflow);
            MoneyWalletChangeFact duplicate = wallet.Apply(overflow);

            Assert.That(first.Status, Is.EqualTo(MoneyWalletTransactionStatus.InvalidAmount));
            Assert.That(first.Changed, Is.False);
            Assert.That(duplicate.Status, Is.EqualTo(MoneyWalletTransactionStatus.DuplicateNoChange));
            Assert.That(
                duplicate.OriginalStatus,
                Is.EqualTo(MoneyWalletTransactionStatus.InvalidAmount));
            Assert.That(wallet.Balance, Is.EqualTo(long.MaxValue));
            Assert.That(wallet.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void ExactDuplicateAndChangedReuseAreDistinctOutcomes()
        {
            var wallet = new MoneyWalletService();
            MoneyTransactionCommand original = MoneyTransactionCommand.CreateGrant(
                Id("transaction.duplicate"),
                Id("operation.duplicate"),
                25L);

            MoneyWalletChangeFact applied = wallet.Apply(original);
            MoneyWalletChangeFact duplicate = wallet.Apply(original);
            MoneyWalletChangeFact conflict = wallet.Apply(
                MoneyTransactionCommand.CreateGrant(
                    Id("transaction.duplicate"),
                    Id("operation.changed"),
                    25L));

            Assert.That(applied.Status, Is.EqualTo(MoneyWalletTransactionStatus.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(MoneyWalletTransactionStatus.DuplicateNoChange));
            Assert.That(duplicate.OriginalStatus, Is.EqualTo(MoneyWalletTransactionStatus.Applied));
            Assert.That(conflict.Status, Is.EqualTo(MoneyWalletTransactionStatus.ConflictingDuplicate));
            Assert.That(conflict.OriginalStatus, Is.EqualTo(MoneyWalletTransactionStatus.Applied));
            Assert.That(wallet.Balance, Is.EqualTo(25L));
            Assert.That(wallet.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void ExpectedSequenceAdmitsExactMatchAndRejectsStaleCommand()
        {
            var wallet = new MoneyWalletService();

            MoneyWalletChangeFact applied = wallet.Grant(
                Id("transaction.sequence-001"),
                Id("operation.sequence-001"),
                3L,
                0L);
            MoneyTransactionCommand stale = MoneyTransactionCommand.CreateGrant(
                Id("transaction.sequence-002"),
                Id("operation.sequence-002"),
                4L,
                0L);
            MoneyWalletChangeFact conflict = wallet.Apply(stale);
            MoneyWalletChangeFact duplicate = wallet.Apply(stale);

            Assert.That(applied.Status, Is.EqualTo(MoneyWalletTransactionStatus.Applied));
            Assert.That(conflict.Status, Is.EqualTo(MoneyWalletTransactionStatus.SequenceConflict));
            Assert.That(conflict.Changed, Is.False);
            Assert.That(duplicate.Status, Is.EqualTo(MoneyWalletTransactionStatus.DuplicateNoChange));
            Assert.That(
                duplicate.OriginalStatus,
                Is.EqualTo(MoneyWalletTransactionStatus.SequenceConflict));
            Assert.That(wallet.Balance, Is.EqualTo(3L));
            Assert.That(wallet.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void ExportedSnapshotIsImmutableDetachedAndCanonicallyOrdered()
        {
            var wallet = new MoneyWalletService();
            wallet.Grant(Id("transaction.zulu"), Id("operation.zulu"), 2L);
            wallet.Grant(Id("transaction.alpha"), Id("operation.alpha"), 1L);
            MoneyWalletSnapshot snapshot = wallet.CurrentSnapshot;

            Assert.Throws<NotSupportedException>(() =>
                ((IList<MoneyWalletContributionSnapshot>)snapshot.Contributions).Clear());
            Assert.Throws<NotSupportedException>(() =>
                ((IList<MoneyWalletTransactionSnapshot>)snapshot.Transactions).Clear());
            Assert.That(
                snapshot.Transactions.Select(item => item.TransactionStableId),
                Is.EqualTo(new[] { "transaction.alpha", "transaction.zulu" }));

            wallet.Grant(Id("transaction.later"), Id("operation.later"), 9L);

            Assert.That(snapshot.Balance, Is.EqualTo(3L));
            Assert.That(snapshot.Sequence, Is.EqualTo(2L));
            Assert.That(snapshot.Transactions.Count, Is.EqualTo(2));
        }

        [Test]
        public void SnapshotRoundTripPreservesBalanceSequenceFingerprintAndReplayFacts()
        {
            var source = new MoneyWalletService();
            MoneyTransactionCommand grant = MoneyTransactionCommand.CreateGrant(
                Id("transaction.roundtrip-grant"),
                Id("operation.roundtrip-grant"),
                40L);
            MoneyTransactionCommand rejected = MoneyTransactionCommand.CreateSpend(
                Id("transaction.roundtrip-rejected"),
                Id("operation.roundtrip-rejected"),
                50L,
                1L);
            source.Apply(grant);
            source.Apply(rejected);
            source.Spend(
                Id("transaction.roundtrip-spend"),
                Id("operation.roundtrip-spend"),
                5L,
                1L);
            MoneyWalletSnapshot exported = source.CurrentSnapshot;
            var restored = new MoneyWalletService();

            MoneyWalletImportResult imported = restored.ImportSnapshot(exported);
            MoneyWalletChangeFact duplicateGrant = restored.Apply(grant);
            MoneyWalletChangeFact duplicateRejected = restored.Apply(rejected);

            Assert.That(imported.Status, Is.EqualTo(MoneyWalletImportStatus.Imported));
            Assert.That(imported.Succeeded, Is.True);
            Assert.That(restored.Balance, Is.EqualTo(35L));
            Assert.That(restored.Sequence, Is.EqualTo(2L));
            Assert.That(restored.CurrentSnapshot.Fingerprint, Is.EqualTo(exported.Fingerprint));
            Assert.That(duplicateGrant.Status, Is.EqualTo(MoneyWalletTransactionStatus.DuplicateNoChange));
            Assert.That(duplicateGrant.OriginalStatus, Is.EqualTo(MoneyWalletTransactionStatus.Applied));
            Assert.That(duplicateRejected.Status, Is.EqualTo(MoneyWalletTransactionStatus.DuplicateNoChange));
            Assert.That(
                duplicateRejected.OriginalStatus,
                Is.EqualTo(MoneyWalletTransactionStatus.InsufficientFunds));
        }

        [Test]
        public void CorruptImportRejectsAtomicallyAndLeavesExistingStateUnchanged()
        {
            var source = new MoneyWalletService();
            source.Grant(Id("transaction.source"), Id("operation.source"), 20L);
            MoneyWalletSnapshot valid = source.CurrentSnapshot;
            var corrupt = new MoneyWalletSnapshot(
                valid.SchemaVersion,
                valid.Sequence,
                valid.Balance,
                valid.Contributions,
                valid.Transactions,
                "sha256:" + new string('0', 64));
            var target = new MoneyWalletService();
            target.Grant(Id("transaction.existing"), Id("operation.existing"), 7L);
            MoneyWalletSnapshot before = target.CurrentSnapshot;

            MoneyWalletImportResult result = target.ImportSnapshot(corrupt);

            Assert.That(result.Status, Is.EqualTo(MoneyWalletImportStatus.FingerprintMismatch));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.PreviousSnapshot, Is.SameAs(result.CurrentSnapshot));
            Assert.That(target.Balance, Is.EqualTo(7L));
            Assert.That(target.Sequence, Is.EqualTo(1L));
            Assert.That(target.CurrentSnapshot.Fingerprint, Is.EqualTo(before.Fingerprint));
        }

        [Test]
        public void CanonicalSnapshotFingerprintIgnoresCallerCollectionOrder()
        {
            var wallet = new MoneyWalletService();
            wallet.Grant(Id("transaction.order-b"), Id("operation.order-b"), 2L);
            wallet.Grant(Id("transaction.order-a"), Id("operation.order-a"), 1L);
            MoneyWalletSnapshot exported = wallet.CurrentSnapshot;

            MoneyWalletSnapshot rebuilt = MoneyWalletSnapshot.CreateCanonical(
                exported.SchemaVersion,
                exported.Sequence,
                exported.Contributions.Reverse(),
                exported.Transactions.Reverse());

            Assert.That(rebuilt.Balance, Is.EqualTo(exported.Balance));
            Assert.That(rebuilt.Fingerprint, Is.EqualTo(exported.Fingerprint));
            Assert.That(
                rebuilt.Transactions.Select(item => item.TransactionStableId),
                Is.EqualTo(exported.Transactions.Select(item => item.TransactionStableId)));
        }

        [Test]
        public void PublicAuthorityHasNoUnitySceneUiProductScrapOrRawLedgerSurface()
        {
            AssertAssemblyHasNoForbiddenReference(typeof(MoneyWalletSnapshot).Assembly);
            AssertAssemblyHasNoForbiddenReference(typeof(MoneyWalletService).Assembly);
            AssertPublicSurfaceHasNoForbiddenType(typeof(MoneyWalletSnapshot));
            AssertPublicSurfaceHasNoForbiddenType(typeof(MoneyWalletChangeFact));
            AssertPublicSurfaceHasNoForbiddenType(typeof(MoneyTransactionCommand));
            AssertPublicSurfaceHasNoForbiddenType(typeof(MoneyWalletService));

            PropertyInfo[] publicProperties = typeof(MoneyWalletService).GetProperties();
            Assert.That(
                publicProperties.Any(property =>
                    (property.PropertyType.FullName ?? string.Empty)
                        .Contains("IdempotentLedger")),
                Is.False);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private static void AssertAssemblyHasNoForbiddenReference(Assembly assembly)
        {
            string[] forbidden =
            {
                "UnityEngine",
                "UnityEditor",
                "ShooterMover.Presentation",
                "ShooterMover.UnityAdapters",
            };
            AssemblyName[] references = assembly.GetReferencedAssemblies();
            for (int index = 0; index < references.Length; index++)
            {
                for (int forbiddenIndex = 0; forbiddenIndex < forbidden.Length; forbiddenIndex++)
                {
                    Assert.That(
                        references[index].Name,
                        Does.Not.StartWith(forbidden[forbiddenIndex]));
                }
            }
        }

        private static void AssertPublicSurfaceHasNoForbiddenType(Type type)
        {
            const BindingFlags Flags =
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            foreach (PropertyInfo property in type.GetProperties(Flags))
            {
                AssertTypeIsAllowed(property.PropertyType);
            }

            foreach (FieldInfo field in type.GetFields(Flags))
            {
                AssertTypeIsAllowed(field.FieldType);
            }

            foreach (MethodInfo method in type.GetMethods(Flags))
            {
                AssertTypeIsAllowed(method.ReturnType);
                ParameterInfo[] parameters = method.GetParameters();
                for (int index = 0; index < parameters.Length; index++)
                {
                    AssertTypeIsAllowed(parameters[index].ParameterType);
                }
            }
        }

        private static void AssertTypeIsAllowed(Type type)
        {
            string fullName = type.FullName ?? string.Empty;
            Assert.That(fullName, Does.Not.Contain("UnityEngine"));
            Assert.That(fullName, Does.Not.Contain("UnityEditor"));
            Assert.That(fullName, Does.Not.Contain(".Economy.Ledger"));
            Assert.That(fullName, Does.Not.Contain(".Economy.Scrap"));
            Assert.That(fullName, Does.Not.Contain(".Shops"));
            Assert.That(fullName, Does.Not.Contain(".Pickups"));
            Assert.That(fullName, Does.Not.Contain(".Presentation"));

            if (!type.IsGenericType)
            {
                return;
            }

            Type[] arguments = type.GetGenericArguments();
            for (int index = 0; index < arguments.Length; index++)
            {
                AssertTypeIsAllowed(arguments[index]);
            }
        }
    }
}
