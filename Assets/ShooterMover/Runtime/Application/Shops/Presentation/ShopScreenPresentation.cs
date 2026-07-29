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
    public enum ShopScreenActionStatus
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

    public enum ShopScreenFeedbackKind
    {
        None = 0,
        Information = 1,
        Success = 2,
        Warning = 3,
        Error = 4,
        Pending = 5,
    }

    public enum ShopScreenRoute
    {
        None = 0,
        Hub = 1,
    }

    public interface IShopScreenRouteBridge
    {
        void Present(
            ShopScreenRoute route,
            PlayerRouteProfilePayload payload);
    }

    public sealed class ShopScreenPurchaseInput
    {
        public ShopScreenPurchaseInput(
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

    public sealed class ShopScreenStockCard
    {
        public ShopScreenStockCard(
            StableId stockEntryStableId,
            StableId definitionStableId,
            StableId equipmentInstanceStableId,
            string displayName,
            string categoryLabel,
            string qualityLabel,
            int itemLevel,
            int augmentCount,
            long price,
            ShopStockEntryState state,
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

            if (!Enum.IsDefined(typeof(ShopStockEntryState), state))
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

        public ShopStockEntryState State { get; }

        public StableId PurchaseTransactionStableId { get; }

        public bool CanPurchase
        {
            get { return State == ShopStockEntryState.Available; }
        }

        public bool CanRetry
        {
            get
            {
                return State == ShopStockEntryState.PurchasePending
                    && PurchaseTransactionStableId != null;
            }
        }

        public bool IsSold
        {
            get { return State == ShopStockEntryState.SoldOut; }
        }
    }

    public sealed class ShopScreenView
    {
        private readonly ReadOnlyCollection<ShopScreenStockCard> stock;

        public ShopScreenView(
            PlayerRouteProfilePayload routePayload,
            StableId runStableId,
            StableId shopStableId,
            int refreshOrdinal,
            string inventoryFingerprint,
            long moneyBalance,
            IEnumerable<ShopScreenStockCard> stock,
            ShopScreenActionStatus status,
            ShopScreenFeedbackKind feedbackKind,
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

            if (!Enum.IsDefined(typeof(ShopScreenActionStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (!Enum.IsDefined(typeof(ShopScreenFeedbackKind), feedbackKind))
            {
                throw new ArgumentOutOfRangeException(nameof(feedbackKind));
            }

            RefreshOrdinal = refreshOrdinal;
            InventoryFingerprint = inventoryFingerprint ?? string.Empty;
            MoneyBalance = moneyBalance;
            this.stock = new ReadOnlyCollection<ShopScreenStockCard>(
                new List<ShopScreenStockCard>(
                    stock ?? Array.Empty<ShopScreenStockCard>()));
            Status = status;
            FeedbackKind = feedbackKind;
            FeedbackText = feedbackText ?? string.Empty;
            FeedbackCode = feedbackCode ?? string.Empty;
        }

        public PlayerRouteProfilePayload RoutePayload { get; }

        public StableId RunStableId { get; }

        public StableId ShopStableId { get; }

        public int RefreshOrdinal { get; }

        public string InventoryFingerprint { get; }

        public long MoneyBalance { get; }

        public IReadOnlyList<ShopScreenStockCard> Stock
        {
            get { return stock; }
        }

        public ShopScreenActionStatus Status { get; }

        public ShopScreenFeedbackKind FeedbackKind { get; }

        public string FeedbackText { get; }

        public string FeedbackCode { get; }

        public ShopScreenStockCard FindCard(StableId stockEntryStableId)
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

    public sealed class ShopScreenActionResult
    {
        public ShopScreenActionResult(
            ShopScreenActionStatus status,
            ShopPurchaseFact authorityFact,
            ShopScreenView projection)
        {
            if (!Enum.IsDefined(typeof(ShopScreenActionStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            AuthorityFact = authorityFact;
            Projection = projection
                ?? throw new ArgumentNullException(nameof(projection));
        }

        public ShopScreenActionStatus Status { get; }

        public ShopPurchaseFact AuthorityFact { get; }

        public ShopScreenView Projection { get; }

        public bool CanRetry
        {
            get
            {
                return Status == ShopScreenActionStatus.PurchasePending
                    || Status == ShopScreenActionStatus.CompensationPending;
            }
        }
    }

    public sealed class ShopScreenRouteResult
    {
        public ShopScreenRouteResult(
            ShopScreenRoute route,
            PlayerRouteProfilePayload payload,
            bool emitted,
            string feedbackCode)
        {
            if (!Enum.IsDefined(typeof(ShopScreenRoute), route))
            {
                throw new ArgumentOutOfRangeException(nameof(route));
            }

            Route = route;
            Payload = payload;
            Emitted = emitted;
            FeedbackCode = feedbackCode ?? string.Empty;
        }

        public ShopScreenRoute Route { get; }

        public PlayerRouteProfilePayload Payload { get; }

        public bool Emitted { get; }

        public string FeedbackCode { get; }
    }

}
