using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Shops;
using ShooterMover.Application.Shops.Presentation;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Tests.EditMode.Shops.Presentation
{
    public sealed partial class ShopScreenPresentationV1Tests
    {
        [Test]
        public void SameRunProjectsSameDeterministicStockAndPrices()
        {
            Fixture first = new Fixture(10000L, 4);
            Fixture second = new Fixture(10000L, 4);

            ShopScreenProjectionV1 left = first.Session("run.shopui-deterministic").Open();
            ShopScreenProjectionV1 right = second.Session("run.shopui-deterministic").Open();

            Assert.That(right.InventoryFingerprint, Is.EqualTo(left.InventoryFingerprint));
            Assert.That(right.Stock.Count, Is.EqualTo(left.Stock.Count));
            for (int index = 0; index < left.Stock.Count; index++)
            {
                Assert.That(right.Stock[index].StockEntryStableId,
                    Is.EqualTo(left.Stock[index].StockEntryStableId));
                Assert.That(right.Stock[index].EquipmentInstanceStableId,
                    Is.EqualTo(left.Stock[index].EquipmentInstanceStableId));
                Assert.That(right.Stock[index].Price, Is.EqualTo(left.Stock[index].Price));
            }
        }

        [Test]
        public void DuplicateDefinitionsRemainSeparateEquipmentInstances()
        {
            Fixture fixture = new Fixture(10000L, 4);
            ShopScreenProjectionV1 projection =
                fixture.Session("run.shopui-duplicates").Open();

            Assert.That(projection.Stock.Count, Is.EqualTo(4));
            Assert.That(projection.Stock[1].DefinitionStableId,
                Is.EqualTo(projection.Stock[0].DefinitionStableId));
            Assert.That(projection.Stock[1].EquipmentInstanceStableId,
                Is.Not.EqualTo(projection.Stock[0].EquipmentInstanceStableId));
            Assert.That(projection.Stock[1].StockEntryStableId,
                Is.Not.EqualTo(projection.Stock[0].StockEntryStableId));
        }

        [Test]
        public void PurchaseUsesAuthoritiesAndProjectsBalanceAndSoldState()
        {
            Fixture fixture = new Fixture(10000L, 2);
            ShopScreenSessionV1 session = fixture.Session("run.shopui-applied");
            ShopScreenProjectionV1 before = session.Open();
            ShopScreenStockCardV1 card = before.Stock[0];

            ShopScreenActionResultV1 result = session.SubmitPurchase(
                Input("shop-screen-input.applied", card));

            Assert.That(result.Status, Is.EqualTo(ShopScreenActionStatusV1.PurchaseApplied));
            Assert.That(result.Projection.MoneyBalance,
                Is.EqualTo(before.MoneyBalance - card.Price));
            Assert.That(result.Projection.FindCard(card.StockEntryStableId).IsSold, Is.True);
            Assert.That(fixture.Holdings.TryGetUnique(
                card.EquipmentInstanceStableId,
                out _), Is.True);
        }

        [Test]
        public void InsufficientFundsLeavesBalanceAndStockAvailable()
        {
            Fixture fixture = new Fixture(0L, 2);
            ShopScreenSessionV1 session = fixture.Session("run.shopui-insufficient");
            ShopScreenProjectionV1 before = session.Open();
            ShopScreenStockCardV1 card = before.Stock[0];

            ShopScreenActionResultV1 result = session.SubmitPurchase(
                Input("shop-screen-input.insufficient", card));

            Assert.That(result.Status, Is.EqualTo(ShopScreenActionStatusV1.InsufficientFunds));
            Assert.That(result.Projection.MoneyBalance, Is.Zero);
            Assert.That(result.Projection.FindCard(card.StockEntryStableId).CanPurchase, Is.True);
            Assert.That(fixture.Money.Sequence, Is.Zero);
            Assert.That(fixture.Holdings.Sequence, Is.Zero);
        }

        [Test]
        public void ExactDuplicateInputReplaysWithoutAdditionalValue()
        {
            Fixture fixture = new Fixture(10000L, 2);
            ShopScreenSessionV1 session = fixture.Session("run.shopui-duplicate-input");
            ShopScreenStockCardV1 card = session.Open().Stock[0];
            ShopScreenPurchaseInputV1 input = Input(
                "shop-screen-input.exact-duplicate",
                card);

            ShopScreenActionResultV1 applied = session.SubmitPurchase(input);
            long balanceAfter = fixture.Money.Balance;
            long holdingsAfter = fixture.Holdings.Sequence;
            ShopScreenActionResultV1 replay = session.SubmitPurchase(input);

            Assert.That(applied.Status, Is.EqualTo(ShopScreenActionStatusV1.PurchaseApplied));
            Assert.That(replay.Status,
                Is.EqualTo(ShopScreenActionStatusV1.ExactDuplicateNoChange));
            Assert.That(fixture.Money.Balance, Is.EqualTo(balanceAfter));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsAfter));
        }

        [Test]
        public void ConflictingDuplicateInputIsRejected()
        {
            Fixture fixture = new Fixture(10000L, 2);
            ShopScreenSessionV1 session = fixture.Session("run.shopui-conflict");
            ShopScreenProjectionV1 projection = session.Open();
            StableId sharedInput = Id("shop-screen-input.conflict");

            session.SubmitPurchase(new ShopScreenPurchaseInputV1(
                sharedInput,
                projection.Stock[0].StockEntryStableId));
            ShopScreenActionResultV1 conflict = session.SubmitPurchase(
                new ShopScreenPurchaseInputV1(
                    sharedInput,
                    projection.Stock[1].StockEntryStableId));

            Assert.That(conflict.Status,
                Is.EqualTo(ShopScreenActionStatusV1.ConflictingDuplicate));
        }

        [Test]
        public void PendingPurchaseRetriesSameInputExactlyOnce()
        {
            var transient = new TransientHoldingsAuthority();
            Fixture fixture = new Fixture(10000L, 2, transient);
            ShopScreenSessionV1 session = fixture.Session("run.shopui-pending");
            ShopScreenStockCardV1 card = session.Open().Stock[0];
            ShopScreenPurchaseInputV1 input = Input(
                "shop-screen-input.pending",
                card);
            long before = fixture.Money.Balance;

            ShopScreenActionResultV1 pending = session.SubmitPurchase(input);
            ShopScreenStockCardV1 pendingCard =
                pending.Projection.FindCard(card.StockEntryStableId);
            ShopScreenActionResultV1 applied = session.SubmitPurchase(
                new ShopScreenPurchaseInputV1(
                    pendingCard.PurchaseTransactionStableId,
                    pendingCard.StockEntryStableId));

            Assert.That(pending.Status,
                Is.EqualTo(ShopScreenActionStatusV1.PurchasePending));
            Assert.That(pendingCard.CanRetry, Is.True);
            Assert.That(applied.Status,
                Is.EqualTo(ShopScreenActionStatusV1.PurchaseApplied));
            Assert.That(fixture.Money.Balance, Is.EqualTo(before - card.Price));
            Assert.That(transient.ApplyCalls, Is.EqualTo(2));
            Assert.That(transient.ConfirmedApplications, Is.EqualTo(1));
        }

        [Test]
        public void BackReturnsExactImmutableHubPayloadAndLocksSecondRoute()
        {
            Fixture fixture = new Fixture(10000L, 2);
            ShopScreenSessionV1 session = fixture.Session("run.shopui-back");
            session.Open();

            ShopScreenRouteResultV1 first = session.NavigateBack();
            ShopScreenRouteResultV1 second = session.NavigateBack();

            Assert.That(first.Emitted, Is.True);
            Assert.That(first.Route, Is.EqualTo(ShopScreenRouteV1.Hub));
            Assert.That(first.Payload, Is.SameAs(fixture.RoutePayload));
            Assert.That(first.Payload.Fingerprint,
                Is.EqualTo(fixture.RoutePayload.Fingerprint));
            Assert.That(second.Emitted, Is.False);
        }

    }
}
