using System;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Shops;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Tests.EditMode.Shops
{
    public sealed class ShopRefreshAndReceiptTests
    {
        [Test]
        public void SixHourWindowsUseFixedUtcBoundaries()
        {
            ShopRefreshWindow before = ShopRefreshSchedule.Resolve(
                new DateTime(2026, 8, 1, 5, 59, 59, DateTimeKind.Utc));
            Assert.That(
                before.StartsAtUtc,
                Is.EqualTo(new DateTime(
                    2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(
                before.RefreshesAtUtc,
                Is.EqualTo(new DateTime(
                    2026, 8, 1, 6, 0, 0, DateTimeKind.Utc)));

            ShopRefreshWindow boundary = ShopRefreshSchedule.Resolve(
                new DateTime(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc));
            Assert.That(
                boundary.StartsAtUtc,
                Is.EqualTo(new DateTime(
                    2026, 8, 1, 6, 0, 0, DateTimeKind.Utc)));
            Assert.That(
                boundary.RefreshesAtUtc,
                Is.EqualTo(new DateTime(
                    2026, 8, 1, 12, 0, 0, DateTimeKind.Utc)));
            Assert.That(boundary.Ordinal, Is.EqualTo(before.Ordinal + 1L));
        }

        [Test]
        public void StockIdentityIsStableInsideOneWindow()
        {
            StableId character = StableId.Parse("character.shop-window-test");
            ShopRefreshWindow first = ShopRefreshSchedule.Resolve(
                new DateTime(2026, 8, 1, 7, 0, 0, DateTimeKind.Utc));
            ShopRefreshWindow same = ShopRefreshSchedule.Resolve(
                new DateTime(2026, 8, 1, 11, 59, 59, DateTimeKind.Utc));
            ShopRefreshWindow next = ShopRefreshSchedule.Resolve(
                new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc));

            Assert.That(
                first.StockIdentity(character),
                Is.EqualTo(same.StockIdentity(character)));
            Assert.That(
                next.StockIdentity(character),
                Is.Not.EqualTo(first.StockIdentity(character)));
        }

        [Test]
        public void ShopTierProfileAuthorsFiftyFivePercentSteel()
        {
            StrongboxTierSelectionProfile profile =
                StrongboxTierSelectionCatalog.Get(
                    StrongboxTierSelectionCatalog.ShopSourceProfileId);
            StableId steel = StrongboxCatalog.GetByNumber(1).TierStableId;
            ulong total = 0UL;
            ulong steelWeight = 0UL;
            for (int index = 0; index < profile.BaseWeights.Count; index++)
            {
                StrongboxTierWeight weight = profile.BaseWeights[index];
                total += weight.Weight;
                if (weight.TierStableId == steel)
                {
                    steelWeight = weight.Weight;
                }
            }

            Assert.That(total, Is.EqualTo(1000000UL));
            Assert.That(steelWeight, Is.EqualTo(550000UL));
        }

        [Test]
        public void PurchaseLedgerAcceptsExactReplayAndRejectsConflict()
        {
            var ledger = new ShopPurchaseLedger();
            StableId stock = StableId.Parse("shopstock.receipt-test");
            StableId purchase = StableId.Parse("shoppurchase.receipt-test");
            string rejection;

            Assert.That(
                ledger.TryRecord(stock, purchase, out rejection),
                Is.True,
                rejection);
            Assert.That(
                ledger.TryRecord(stock, purchase, out rejection),
                Is.True,
                rejection);
            Assert.That(
                ledger.TryRecord(
                    stock,
                    StableId.Parse("shoppurchase.conflict"),
                    out rejection),
                Is.False);
            Assert.That(
                rejection,
                Is.EqualTo("shop-purchase-receipt-conflict"));
        }

        [Test]
        public void PurchaseLedgerSnapshotRoundTripsExactReceipts()
        {
            var source = new ShopPurchaseLedger();
            StableId stock = StableId.Parse("shopstock.snapshot-test");
            StableId purchase = StableId.Parse("shoppurchase.snapshot-test");
            string rejection;
            Assert.That(
                source.TryRecord(stock, purchase, out rejection),
                Is.True,
                rejection);

            var restored = new ShopPurchaseLedger();
            Assert.That(
                restored.TryImportSnapshot(
                    source.ExportSnapshot(),
                    out rejection),
                Is.True,
                rejection);

            StableId restoredPurchase;
            Assert.That(
                restored.TryGet(stock, out restoredPurchase),
                Is.True);
            Assert.That(restoredPurchase, Is.EqualTo(purchase));
        }
    }
}
