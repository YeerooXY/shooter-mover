using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Shops;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Shops.Presentation
{
    public enum ShopScreenActionStatusV1
    {
        Ready = 1,
        PurchaseApplied = 2,
        ExactDuplicateNoChange = 3,
        ConflictingDuplicate = 4,
        PurchasePending = 5,
        SoldOut = 6,
        InsufficientFunds = 7,
        CompensationPending = 8,
        PurchaseRejected = 9,
        InventoryUnavailable = 10,
        InputLocked = 11,
        InvalidRequest = 12,
    }

    public enum ShopScreenFeedbackKindV1
    {
        None = 0,
        Information = 1,
        Success = 2,
        Warning = 3,
        Error = 4,
        Pending = 5,
    }

    public enum ShopScreenRouteV1
    {
        None = 0,
        Hub = 1,
    }

    public interface IShopScreenRouteAdapterV1
    {
        void Present(
            ShopScreenRouteV1 route,
            PlayerRouteProfilePayloadV1 payload);
    }

    public sealed class ShopScreenPurchaseInputV1
    {
        public ShopScreenPurchaseInputV1(
            StableId inputStableId,
            StableId stockEntryStableId)
        {
            InputStableId = inputStableId
                ?? throw new ArgumentNullException(nameof(inputStableId));
            StockEntryStableId = stockEntryStableId
                ?? throw new ArgumentNullException(nameof(stockEntryStableId));
        }

        public StableId InputStableId { get; }

        public StableId StockEntryStableId { get; }
    }

    public sealed class ShopScreenStockCardV1
    {
        public ShopScreenStockCardV1(
            StableId stockEntryStableId,
            StableId definitionStableId,
            StableId equipmentInstanceStableId,
            string displayName,
            string categoryLabel,
            string qualityLabel,
            int itemLevel,
            int augmentCount,
            long price,
            ShopStockEntryStateV1 state,
            StableId purchaseTransactionStableId)
        {
            StockEntryStableId = stockEntryStableId
                ?? throw new ArgumentNullException(nameof(stockEntryStableId));
            DefinitionStableId = definitionStableId
                ?? throw new ArgumentNullException(nameof(definitionStableId));
            EquipmentInstanceStableId = equipmentInstanceStableId
                ?? throw new ArgumentNullException(nameof(equipmentInstanceStableId));
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? definitionStableId.ToString()
                : displayName.Trim();
            CategoryLabel = categoryLabel ?? string.Empty;
            QualityLabel = qualityLabel ?? string.Empty;
            if (itemLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(itemLevel));
            }

            if (augmentCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(augmentCount));
            }

            if (price < 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(price));
            }

            if (!Enum.IsDefined(typeof(ShopStockEntryStateV1), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            ItemLevel = itemLevel;
            AugmentCount = augmentCount;
            Price = price;
            State = state;
            PurchaseTransactionStableId = purchaseTransactionStableId;
        }

        public StableId StockEntryStableId { get; }

        public StableId DefinitionStableId { get; }

        public StableId EquipmentInstanceStableId { get; }

        public string DisplayName { get; }

        public string CategoryLabel { get; }

        public string QualityLabel { get; }

        public int ItemLevel { get; }

        public int AugmentCount { get; }

        public long Price { get; }

        public ShopStockEntryStateV1 State { get; }

        public StableId PurchaseTransactionStableId { get; }

        public bool CanPurchase
        {
            get { return State == ShopStockEntryStateV1.Available; }
        }

        public bool CanRetry
        {
            get
            {
                return State == ShopStockEntryStateV1.PurchasePending
                    && PurchaseTransactionStableId != null;
            }
        }

        public bool IsSold
        {
            get { return State == ShopStockEntryStateV1.SoldOut; }
        }
    }

    public sealed class ShopScreenProjectionV1
    {
        private readonly ReadOnlyCollection<ShopScreenStockCardV1> stock;

        public ShopScreenProjectionV1(
            PlayerRouteProfilePayloadV1 routePayload,
            StableId runStableId,
            StableId shopStableId,
            int refreshOrdinal,
            string inventoryFingerprint,
            long moneyBalance,
            IEnumerable<ShopScreenStockCardV1> stock,
            ShopScreenActionStatusV1 status,
            ShopScreenFeedbackKindV1 feedbackKind,
            string feedbackText,
            string feedbackCode)
        {
            RoutePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            ShopStableId = shopStableId
                ?? throw new ArgumentNullException(nameof(shopStableId));
            if (refreshOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(refreshOrdinal));
            }

            if (moneyBalance < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(moneyBalance));
            }

            if (!Enum.IsDefined(typeof(ShopScreenActionStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (!Enum.IsDefined(typeof(ShopScreenFeedbackKindV1), feedbackKind))
            {
                throw new ArgumentOutOfRangeException(nameof(feedbackKind));
            }

            RefreshOrdinal = refreshOrdinal;
            InventoryFingerprint = inventoryFingerprint ?? string.Empty;
            MoneyBalance = moneyBalance;
            this.stock = new ReadOnlyCollection<ShopScreenStockCardV1>(
                new List<ShopScreenStockCardV1>(
                    stock ?? Array.Empty<ShopScreenStockCardV1>()));
            Status = status;
            FeedbackKind = feedbackKind;
            FeedbackText = feedbackText ?? string.Empty;
            FeedbackCode = feedbackCode ?? string.Empty;
        }

        public PlayerRouteProfilePayloadV1 RoutePayload { get; }

        public StableId RunStableId { get; }

        public StableId ShopStableId { get; }

        public int RefreshOrdinal { get; }

        public string InventoryFingerprint { get; }

        public long MoneyBalance { get; }

        public IReadOnlyList<ShopScreenStockCardV1> Stock
        {
            get { return stock; }
        }

        public ShopScreenActionStatusV1 Status { get; }

        public ShopScreenFeedbackKindV1 FeedbackKind { get; }

        public string FeedbackText { get; }

        public string FeedbackCode { get; }

        public ShopScreenStockCardV1 FindCard(StableId stockEntryStableId)
        {
            if (stockEntryStableId == null)
            {
                return null;
            }

            for (int index = 0; index < stock.Count; index++)
            {
                if (stock[index].StockEntryStableId == stockEntryStableId)
                {
                    return stock[index];
                }
            }

            return null;
        }
    }

    public sealed class ShopScreenActionResultV1
    {
        public ShopScreenActionResultV1(
            ShopScreenActionStatusV1 status,
            ShopPurchaseFactV1 authorityFact,
            ShopScreenProjectionV1 projection)
        {
            if (!Enum.IsDefined(typeof(ShopScreenActionStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            AuthorityFact = authorityFact;
            Projection = projection
                ?? throw new ArgumentNullException(nameof(projection));
        }

        public ShopScreenActionStatusV1 Status { get; }

        public ShopPurchaseFactV1 AuthorityFact { get; }

        public ShopScreenProjectionV1 Projection { get; }

        public bool CanRetry
        {
            get
            {
                return Status == ShopScreenActionStatusV1.PurchasePending
                    || Status == ShopScreenActionStatusV1.CompensationPending;
            }
        }
    }

    public sealed class ShopScreenRouteResultV1
    {
        public ShopScreenRouteResultV1(
            ShopScreenRouteV1 route,
            PlayerRouteProfilePayloadV1 payload,
            bool emitted,
            string feedbackCode)
        {
            if (!Enum.IsDefined(typeof(ShopScreenRouteV1), route))
            {
                throw new ArgumentOutOfRangeException(nameof(route));
            }

            Route = route;
            Payload = payload;
            Emitted = emitted;
            FeedbackCode = feedbackCode ?? string.Empty;
        }

        public ShopScreenRouteV1 Route { get; }

        public PlayerRouteProfilePayloadV1 Payload { get; }

        public bool Emitted { get; }

        public string FeedbackCode { get; }
    }

}
