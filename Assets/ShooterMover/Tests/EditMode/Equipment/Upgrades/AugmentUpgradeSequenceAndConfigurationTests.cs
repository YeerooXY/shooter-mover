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
        public void WalletSequenceConflictIsRejected()
        {
            var fixture = new Fixture();
            AugmentUpgradeQuoteV1 quote = fixture.Quote(2);
            fixture.Money.Grant(
                Id("wallet-conflict.transaction"),
                Id("wallet-conflict.operation"),
                1L,
                fixture.Money.Sequence);
            long balance = fixture.Money.Balance;
            long sequence = fixture.Money.Sequence;

            AugmentUpgradeFactV1 fact = fixture.Confirm(
                quote,
                "confirmation.wallet-conflict");

            Assert.That(fact.Status,
                Is.EqualTo(
                    AugmentUpgradeConfirmationStatusV1.WalletSequenceConflict));
            Assert.That(fixture.Money.Balance, Is.EqualTo(balance));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(sequence));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(quote.HoldingsSequence));
        }

        [Test]
        public void HoldingsSequenceConflictIsRejected()
        {
            var fixture = new Fixture();
            AugmentUpgradeQuoteV1 quote = fixture.Quote(2);
            HoldingProvenanceV1 provenance = HoldingProvenanceV1.Create(
                Id("grant.unrelated"),
                Id("source.unrelated"));
            fixture.Holdings.Apply(PlayerHoldingsCommandV1.AddStack(
                Id("holdings-conflict.transaction"),
                Id("holdings-conflict.operation"),
                HoldingsAuthority,
                RewardGrantKindV1.Miscellaneous,
                Id("item.unrelated"),
                1L,
                provenance,
                fixture.Holdings.Sequence));
            long sequence = fixture.Holdings.Sequence;
            long balance = fixture.Money.Balance;

            AugmentUpgradeFactV1 fact = fixture.Confirm(
                quote,
                "confirmation.holdings-conflict");

            Assert.That(fact.Status,
                Is.EqualTo(
                    AugmentUpgradeConfirmationStatusV1.HoldingsSequenceConflict));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(sequence));
            Assert.That(fixture.Money.Balance, Is.EqualTo(balance));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(quote.WalletSequence));
        }

        [Test]
        public void DefaultTenLevelConfigurationWorks()
        {
            var fixture = new Fixture(maximumLevel: 10, currentLevel: 9);

            AugmentUpgradeFactV1 fact = fixture.Confirm(
                fixture.Quote(10),
                "confirmation.level-ten");

            Assert.That(fact.Status, Is.EqualTo(AugmentUpgradeConfirmationStatusV1.Applied));
            Assert.That(
                FindAugment(
                    fixture.GetUnique(fact.ReplacementEquipmentInstanceStableId)
                        .EquipmentInstance,
                    PrimaryAugmentInstanceId).Level,
                Is.EqualTo(10));
        }

        [Test]
        public void ConfiguredMaximumAboveTenWorks()
        {
            var fixture = new Fixture(maximumLevel: 25, currentLevel: 10);

            AugmentUpgradeFactV1 fact = fixture.Confirm(
                fixture.Quote(11),
                "confirmation.above-ten");

            Assert.That(fact.Status, Is.EqualTo(AugmentUpgradeConfirmationStatusV1.Applied));
            Assert.That(
                FindAugment(
                    fixture.GetUnique(fact.ReplacementEquipmentInstanceStableId)
                        .EquipmentInstance,
                    PrimaryAugmentInstanceId).Level,
                Is.EqualTo(11));
        }

        [Test]
        public void DifferentAugmentTiersUseDifferentCostCurves()
        {
            var tierOne = new Fixture(augmentTier: 1);
            var tierThree = new Fixture(augmentTier: 3);

            long tierOneCost = tierOne.Quote(2).MoneyCost;
            long tierThreeCost = tierThree.Quote(2).MoneyCost;

            Assert.That(tierOneCost, Is.Not.EqualTo(tierThreeCost));
            Assert.That(tierThreeCost, Is.GreaterThan(tierOneCost));
        }
    }
}
