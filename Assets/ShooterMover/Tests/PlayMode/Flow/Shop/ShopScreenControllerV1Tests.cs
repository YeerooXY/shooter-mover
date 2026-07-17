using System;
using System.Collections;
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
using ShooterMover.UI.Shop;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Flow.Shop
{
    public sealed partial class ShopScreenControllerV1Tests
    {
        [UnityTest]
        public IEnumerator ControllerProjectsRealStockAndPurchaseResult()
        {
            Fixture fixture = new Fixture(10000L);
            var host = new GameObject("SHOPUI-001 Controller Test");
            ShopScreenControllerV1 controller =
                host.AddComponent<ShopScreenControllerV1>();
            var adapter = new RecordingShopScreenRouteAdapterV1();
            controller.Configure(fixture.Session("run.shopui-controller"), adapter);
            yield return null;

            ShopScreenStockCardV1 card = controller.Projection.Stock[0];
            long before = controller.Projection.MoneyBalance;
            ShopScreenActionResultV1 result = controller.Purchase(
                card.StockEntryStableId);

            Assert.That(result.Status,
                Is.EqualTo(ShopScreenActionStatusV1.PurchaseApplied));
            Assert.That(controller.Projection.MoneyBalance,
                Is.EqualTo(before - card.Price));
            Assert.That(controller.Projection.FindCard(
                card.StockEntryStableId).IsSold, Is.True);
            UnityEngine.Object.DestroyImmediate(host);
        }

        [UnityTest]
        public IEnumerator ControllerExposesExactDuplicateInputReplay()
        {
            Fixture fixture = new Fixture(10000L);
            var host = new GameObject("SHOPUI-001 Duplicate Test");
            ShopScreenControllerV1 controller =
                host.AddComponent<ShopScreenControllerV1>();
            controller.Configure(
                fixture.Session("run.shopui-controller-duplicate"),
                new RecordingShopScreenRouteAdapterV1());
            yield return null;

            ShopScreenStockCardV1 card = controller.Projection.Stock[0];
            StableId inputId = Id("shop-screen-input.controller-duplicate");
            controller.SubmitPurchase(inputId, card.StockEntryStableId);
            long balanceAfter = fixture.Money.Balance;
            ShopScreenActionResultV1 replay = controller.SubmitPurchase(
                inputId,
                card.StockEntryStableId);

            Assert.That(replay.Status,
                Is.EqualTo(ShopScreenActionStatusV1.ExactDuplicateNoChange));
            Assert.That(fixture.Money.Balance, Is.EqualTo(balanceAfter));
            UnityEngine.Object.DestroyImmediate(host);
        }

        [UnityTest]
        public IEnumerator RetryButtonPathReusesPendingAuthorityIdentity()
        {
            var transient = new TransientHoldingsAuthority();
            Fixture fixture = new Fixture(10000L, transient);
            var host = new GameObject("SHOPUI-001 Retry Test");
            ShopScreenControllerV1 controller =
                host.AddComponent<ShopScreenControllerV1>();
            controller.Configure(
                fixture.Session("run.shopui-controller-retry"),
                new RecordingShopScreenRouteAdapterV1());
            yield return null;

            ShopScreenStockCardV1 card = controller.Projection.Stock[0];
            ShopScreenActionResultV1 pending = controller.SubmitPurchase(
                Id("shop-screen-input.controller-retry"),
                card.StockEntryStableId);
            ShopScreenStockCardV1 pendingCard =
                controller.Projection.FindCard(card.StockEntryStableId);
            ShopScreenActionResultV1 applied = controller.Retry(
                card.StockEntryStableId);

            Assert.That(pending.Status,
                Is.EqualTo(ShopScreenActionStatusV1.PurchasePending));
            Assert.That(pendingCard.CanRetry, Is.True);
            Assert.That(applied.Status,
                Is.EqualTo(ShopScreenActionStatusV1.PurchaseApplied));
            Assert.That(transient.ApplyCalls, Is.EqualTo(2));
            Assert.That(transient.ConfirmedApplications, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(host);
        }

        [UnityTest]
        public IEnumerator BackEmitsSamePayloadOnlyOnce()
        {
            Fixture fixture = new Fixture(10000L);
            var host = new GameObject("SHOPUI-001 Back Test");
            ShopScreenControllerV1 controller =
                host.AddComponent<ShopScreenControllerV1>();
            var adapter = new RecordingShopScreenRouteAdapterV1();
            controller.Configure(fixture.Session("run.shopui-controller-back"), adapter);
            yield return null;

            ShopScreenRouteResultV1 first = controller.NavigateBack();
            ShopScreenRouteResultV1 second = controller.NavigateBack();

            Assert.That(first.Emitted, Is.True);
            Assert.That(first.Payload, Is.SameAs(fixture.RoutePayload));
            Assert.That(second.Emitted, Is.False);
            Assert.That(adapter.PresentCount, Is.EqualTo(1));
            Assert.That(adapter.LastPayload, Is.SameAs(fixture.RoutePayload));
            UnityEngine.Object.DestroyImmediate(host);
        }

    }
}
