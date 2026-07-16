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
        private static readonly StableId HoldingsAuthority = Id("holdings.player-profile");
        private static readonly StableId RapAuthority = Id("authority.reward-application");
        private static readonly StableId ScrapAuthority = Id("authority.scrap");
        private static readonly StableId ScrapCurrency = Id("currency.scrap");
        private static readonly StableId EquipmentDefinitionId = Id("equipment.blaster");
        private static readonly StableId PrimaryAugmentDefinitionId = Id("augment.power");
        private static readonly StableId SecondaryAugmentDefinitionId = Id("augment.cooldown");
        private static readonly StableId EquipmentInstanceId = Id("equipment-instance.original");
        private static readonly StableId PrimaryAugmentInstanceId = Id("augment-instance.primary");
        private static readonly StableId SecondaryAugmentInstanceId = Id("augment-instance.secondary");

        [Test]
        public void ValidNextLevelUpgradeSucceeds()
        {
            var fixture = new Fixture();
            AugmentUpgradeQuoteV1 quote = fixture.Quote(2);

            AugmentUpgradeFactV1 fact = fixture.Confirm(quote, "confirmation.valid");

            Assert.That(fact.Status, Is.EqualTo(AugmentUpgradeConfirmationStatusV1.Applied));
            UniqueHoldingSnapshotV1 replacement = fixture.GetUnique(
                fact.ReplacementEquipmentInstanceStableId);
            Assert.That(FindAugment(replacement.EquipmentInstance, PrimaryAugmentInstanceId).Level,
                Is.EqualTo(2));
        }

        [Test]
        public void ExactMoneyCostIsSpentOnce()
        {
            var fixture = new Fixture();
            AugmentUpgradeQuoteV1 quote = fixture.Quote(2);
            long before = fixture.Money.Balance;

            AugmentUpgradeFactV1 first = fixture.Confirm(quote, "confirmation.money-once");
            AugmentUpgradeFactV1 duplicate = fixture.Confirm(quote, "confirmation.money-once");

            Assert.That(first.Status, Is.EqualTo(AugmentUpgradeConfirmationStatusV1.Applied));
            Assert.That(duplicate.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatusV1.ExactDuplicateNoChange));
            Assert.That(fixture.Money.Balance, Is.EqualTo(before - quote.MoneyCost));
            Assert.That(fixture.Money.Sequence, Is.EqualTo(quote.WalletSequence + 1L));
        }

        [Test]
        public void EquipmentInstanceIsReplacedOnce()
        {
            var fixture = new Fixture();
            AugmentUpgradeQuoteV1 quote = fixture.Quote(2);

            AugmentUpgradeFactV1 fact = fixture.Confirm(quote, "confirmation.replace-once");
            AugmentUpgradeFactV1 duplicate = fixture.Confirm(quote, "confirmation.replace-once");

            UniqueHoldingSnapshotV1 ignored;
            Assert.That(fixture.Holdings.TryGetUnique(EquipmentInstanceId, out ignored), Is.False);
            Assert.That(
                fixture.Holdings.TryGetUnique(
                    fact.ReplacementEquipmentInstanceStableId,
                    out ignored),
                Is.True);
            Assert.That(duplicate.ReplacementEquipmentInstanceStableId,
                Is.EqualTo(fact.ReplacementEquipmentInstanceStableId));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(quote.HoldingsSequence + 2L));
        }

        [Test]
        public void UnrelatedEquipmentFieldsRemainUnchanged()
        {
            var fixture = new Fixture();
            EquipmentInstance original = fixture.Equipment;
            AugmentUpgradeFactV1 fact = fixture.Confirm(
                fixture.Quote(2),
                "confirmation.preserve-fields");
            EquipmentInstance replacement = fixture.GetUnique(
                fact.ReplacementEquipmentInstanceStableId).EquipmentInstance;

            Assert.That(replacement.DefinitionId, Is.EqualTo(original.DefinitionId));
            Assert.That(replacement.ItemLevel, Is.EqualTo(original.ItemLevel));
            Assert.That(replacement.QualityId, Is.EqualTo(original.QualityId));
            Assert.That(
                FindAugment(replacement, SecondaryAugmentInstanceId).ToCanonicalString(),
                Is.EqualTo(
                    FindAugment(original, SecondaryAugmentInstanceId).ToCanonicalString()));
            Assert.That(
                FindAugment(replacement, PrimaryAugmentInstanceId).InstanceId,
                Is.EqualTo(PrimaryAugmentInstanceId));
        }

        [Test]
        public void DuplicateConfirmationCannotSpendTwice()
        {
            var fixture = new Fixture();
            AugmentUpgradeQuoteV1 quote = fixture.Quote(2);
            AugmentUpgradeConfirmationV1 confirmation = AugmentUpgradeConfirmationV1.Create(
                Id("confirmation.duplicate"),
                quote);

            AugmentUpgradeFactV1 first = fixture.Service.Confirm(confirmation);
            long balanceAfterFirst = fixture.Money.Balance;
            long holdingsSequenceAfterFirst = fixture.Holdings.Sequence;
            AugmentUpgradeFactV1 second = fixture.Service.Confirm(confirmation);

            Assert.That(second.Status,
                Is.EqualTo(AugmentUpgradeConfirmationStatusV1.ExactDuplicateNoChange));
            Assert.That(second.OriginalStatus,
                Is.EqualTo(AugmentUpgradeConfirmationStatusV1.Applied));
            Assert.That(second.MoneyTransactionStableId,
                Is.EqualTo(first.MoneyTransactionStableId));
            Assert.That(fixture.Money.Balance, Is.EqualTo(balanceAfterFirst));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsSequenceAfterFirst));
        }
    }
}
