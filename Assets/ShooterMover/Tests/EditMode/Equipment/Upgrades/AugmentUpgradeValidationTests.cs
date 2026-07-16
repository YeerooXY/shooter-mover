using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Equipment.Upgrades;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Equipment.Upgrades;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Equipment.Upgrades
{
    public sealed partial class AugmentUpgradeServiceV1Tests
    {
        [Test]
        public void ConflictingDuplicateIsRejected()
        {
            var fixture = new Fixture();
            AugmentUpgradeQuoteV1 quote = fixture.Quote(2);
            StableId confirmationId = Id("confirmation.conflict");
            AugmentUpgradeFactV1 first = fixture.Service.Confirm(
                AugmentUpgradeConfirmationV1.Create(confirmationId, quote));
            long balance = fixture.Money.Balance;
            long holdingsSequence = fixture.Holdings.Sequence;

            AugmentUpgradeFactV1 conflict = fixture.Service.Confirm(
                AugmentUpgradeConfirmationV1.Create(
                    confirmationId,
                    quote,
                    Hash('x')));

            Assert.That(first.Status, Is.EqualTo(AugmentUpgradeConfirmationStatusV1.Applied));
            Assert.That(conflict.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatusV1.ConflictingDuplicate));
            Assert.That(fixture.Money.Balance, Is.EqualTo(balance));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsSequence));
        }

        [Test]
        public void InsufficientFundsChangesNothing()
        {
            var fixture = new Fixture(initialMoney: 50L);
            AugmentUpgradeQuoteV1 quote = fixture.Quote(2);
            long walletSequence = fixture.Money.Sequence;
            long holdingsSequence = fixture.Holdings.Sequence;

            AugmentUpgradeFactV1 fact = fixture.Confirm(
                quote,
                "confirmation.insufficient");

            Assert.That(fact.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatusV1.InsufficientFunds));
            Assert.That(fixture.Money.Balance, Is.EqualTo(50L));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(walletSequence));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsSequence));
            Assert.That(fixture.GetUnique(EquipmentInstanceId), Is.Not.Null);
        }

        [Test]
        public void MissingEquipmentIsRejected()
        {
            var fixture = new Fixture();
            AugmentUpgradeQuoteV1 quote = fixture.Quote(2);
            fixture.RemoveOriginal("setup-remove.missing-equipment");
            long balance = fixture.Money.Balance;
            long holdingsSequence = fixture.Holdings.Sequence;

            AugmentUpgradeFactV1 fact = fixture.Confirm(
                quote,
                "confirmation.missing-equipment");

            Assert.That(fact.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatusV1.MissingEquipment));
            Assert.That(fixture.Money.Balance, Is.EqualTo(balance));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsSequence));
        }

        [Test]
        public void MissingAugmentSlotIsRejected()
        {
            var fixture = new Fixture();
            AugmentUpgradeQuoteV1 quote = fixture.Quote(2);
            AugmentUpgradeQuoteV1 missing = CopyQuote(
                quote,
                augmentSlotIndex: 7,
                augmentInstanceStableId: Id("augment-instance.missing"));

            AugmentUpgradeFactV1 fact = fixture.Confirm(
                missing,
                "confirmation.missing-augment");

            Assert.That(fact.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatusV1.MissingAugment));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(quote.WalletSequence));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(quote.HoldingsSequence));
        }

        [Test]
        public void InvalidLevelJumpIsRejected()
        {
            var fixture = new Fixture();
            AugmentUpgradeQuoteV1 quote = fixture.Quote(2);
            AugmentUpgradeQuoteV1 jump = CopyQuote(
                quote,
                targetLevel: 3,
                moneyCost: quote.MoneyCost + 1L);

            AugmentUpgradeFactV1 fact = fixture.Confirm(
                jump,
                "confirmation.invalid-jump");

            Assert.That(fact.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatusV1.InvalidLevelJump));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(quote.WalletSequence));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(quote.HoldingsSequence));
        }

        [Test]
        public void MaximumLevelUpgradeIsRejected()
        {
            var fixture = new Fixture(maximumLevel: 3, currentLevel: 3);
            AugmentUpgradeQuoteV1 manual = fixture.CreateManualQuote(
                targetLevel: 4,
                moneyCost: 1L);

            AugmentUpgradeFactV1 fact = fixture.Confirm(
                manual,
                "confirmation.maximum");

            Assert.That(fact.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatusV1.MaximumLevel));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(manual.WalletSequence));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(manual.HoldingsSequence));
        }

        [Test]
        public void StaleEquipmentFingerprintIsRejected()
        {
            var fixture = new Fixture();
            AugmentUpgradeQuoteV1 quote = fixture.Quote(2);
            AugmentUpgradeQuoteV1 stale = CopyQuote(
                quote,
                equipmentFingerprint: Hash('e'));

            AugmentUpgradeFactV1 fact = fixture.Confirm(
                stale,
                "confirmation.stale-equipment");

            Assert.That(fact.Status,
                Is.EqualTo(
                    AugmentUpgradeConfirmationStatusV1.StaleEquipmentFingerprint));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(quote.WalletSequence));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(quote.HoldingsSequence));
        }

        [Test]
        public void StaleQuoteAndCostPolicyAreRejected()
        {
            var fixture = new Fixture();
            AugmentUpgradeQuoteV1 quote = fixture.Quote(2);
            AugmentUpgradeFactV1 staleQuote = fixture.Service.Confirm(
                AugmentUpgradeConfirmationV1.Create(
                    Id("confirmation.stale-quote"),
                    quote,
                    Hash('q')));

            AugmentUpgradeCostPolicyV1 replacementPolicy = Policy(
                version: 2,
                tierOneBase: 777L);
            AugmentUpgradeServiceV1 replacementService = fixture.CreateService(
                replacementPolicy);
            AugmentUpgradeFactV1 stalePolicy = replacementService.Confirm(
                AugmentUpgradeConfirmationV1.Create(
                    Id("confirmation.stale-policy"),
                    quote));

            Assert.That(staleQuote.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatusV1.StaleQuote));
            Assert.That(stalePolicy.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatusV1.StaleCostPolicy));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(quote.WalletSequence));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(quote.HoldingsSequence));
        }
    }
}
