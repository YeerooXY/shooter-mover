using System;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Shops.Presentation
{
    public sealed partial class ShopScreenSession
    {
        private readonly PlayerRouteProfilePayload routePayload;
        private readonly StableId stockId;
        private readonly StableId claimantStableId;
        private readonly ShopLiveActions shopRuntime;
        private readonly MoneyWalletActions moneyWallet;
        private readonly ShopDefinition definition;
        private readonly EquipmentCatalog catalog;
        private readonly ProgressionContext progressionContext;
        private readonly GeneratedEquipmentAugmentSignatureState offerAugments;
        private readonly DateTime? refreshesAtUtc;
        private readonly IShopSave save;

        private ShopInventoryView inventory;
        private ShopScreenView currentProjection;
        private bool routeEmitted;

        public ShopScreenSession(
            PlayerRouteProfilePayload routePayload,
            StableId runStableId,
            StableId claimantStableId,
            ShopLiveActions shopRuntime,
            MoneyWalletActions moneyWallet,
            ShopDefinition definition,
            EquipmentCatalog catalog,
            ProgressionContext progressionContext)
            : this(
                routePayload,
                runStableId,
                claimantStableId,
                shopRuntime,
                moneyWallet,
                definition,
                catalog,
                progressionContext,
                null,
                null,
                null)
        {
        }

        public ShopScreenSession(
            PlayerRouteProfilePayload routePayload,
            StableId stockId,
            StableId claimantStableId,
            ShopLiveActions shopRuntime,
            MoneyWalletActions moneyWallet,
            ShopDefinition definition,
            EquipmentCatalog catalog,
            ProgressionContext progressionContext,
            GeneratedEquipmentAugmentSignatureState offerAugments,
            DateTime? refreshesAtUtc,
            IShopSave save)
        {
            this.routePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            if (!routePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid immutable Hub route payload is required.",
                    nameof(routePayload));
            }

            this.stockId = stockId
                ?? throw new ArgumentNullException(nameof(stockId));
            this.claimantStableId = claimantStableId
                ?? throw new ArgumentNullException(nameof(claimantStableId));
            this.shopRuntime = shopRuntime
                ?? throw new ArgumentNullException(nameof(shopRuntime));
            this.moneyWallet = moneyWallet
                ?? throw new ArgumentNullException(nameof(moneyWallet));
            this.definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
            this.catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
            this.progressionContext = progressionContext
                ?? throw new ArgumentNullException(nameof(progressionContext));
            if (refreshesAtUtc.HasValue
                && refreshesAtUtc.Value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Shop refresh time must be UTC.",
                    nameof(refreshesAtUtc));
            }
            this.offerAugments = offerAugments;
            this.refreshesAtUtc = refreshesAtUtc;
            this.save = save;
        }

        public PlayerRouteProfilePayload RoutePayload
        {
            get { return routePayload; }
        }

        public StableId RunStableId
        {
            get { return stockId; }
        }

        public StableId StockId
        {
            get { return stockId; }
        }

        public StableId ShopStableId
        {
            get { return definition.ShopStableId; }
        }

        public StableId ClaimantStableId
        {
            get { return claimantStableId; }
        }

        public ShopScreenView CurrentProjection
        {
            get { return currentProjection; }
        }

        public bool IsRouteEmitted
        {
            get { return routeEmitted; }
        }

        public ShopScreenView Open()
        {
            if (routeEmitted)
            {
                currentProjection = Project(
                    ShopScreenActionStatus.InputLocked,
                    ShopScreenFeedbackKind.Warning,
                    "INPUT LOCKED — A ROUTE HAS ALREADY BEEN EMITTED",
                    "shop-screen-input-locked");
                return currentProjection;
            }

            ShopInventoryOpenResult opened = shopRuntime.Open(
                stockId,
                definition,
                catalog,
                progressionContext);
            if (!opened.Succeeded || opened.Inventory == null)
            {
                currentProjection = Project(
                    ShopScreenActionStatus.InventoryUnavailable,
                    ShopScreenFeedbackKind.Error,
                    "SHOP STOCK UNAVAILABLE",
                    opened.RejectionCode);
                return currentProjection;
            }

            inventory = opened.Inventory;
            currentProjection = Project(
                ShopScreenActionStatus.Ready,
                ShopScreenFeedbackKind.Information,
                opened.Status == ShopInventoryOpenStatus.Generated
                    ? refreshesAtUtc.HasValue
                        ? "NEW 6-HOUR STOCK GENERATED"
                        : "DETERMINISTIC STOCK GENERATED FOR THIS RUN"
                    : refreshesAtUtc.HasValue
                        ? "CURRENT 6-HOUR STOCK RESTORED — NO REROLL"
                        : "DETERMINISTIC STOCK RESTORED — NO REROLL",
                string.Empty);
            return currentProjection;
        }

        public ShopScreenActionResult SubmitPurchase(
            ShopScreenPurchaseInput input)
        {
            if (routeEmitted)
            {
                ShopScreenView locked = Project(
                    ShopScreenActionStatus.InputLocked,
                    ShopScreenFeedbackKind.Warning,
                    "INPUT LOCKED — RETURN ROUTE ALREADY EMITTED",
                    "shop-screen-input-locked");
                currentProjection = locked;
                return new ShopScreenActionResult(
                    ShopScreenActionStatus.InputLocked,
                    null,
                    locked);
            }

            if (input == null)
            {
                ShopScreenView invalid = Project(
                    ShopScreenActionStatus.InvalidRequest,
                    ShopScreenFeedbackKind.Error,
                    "PURCHASE INPUT IS MISSING",
                    "shop-screen-purchase-input-null");
                currentProjection = invalid;
                return new ShopScreenActionResult(
                    ShopScreenActionStatus.InvalidRequest,
                    null,
                    invalid);
            }

            if (!EnsureInventory())
            {
                ShopScreenView unavailable = Project(
                    ShopScreenActionStatus.InventoryUnavailable,
                    ShopScreenFeedbackKind.Error,
                    "SHOP STOCK UNAVAILABLE",
                    "shop-screen-inventory-unavailable");
                currentProjection = unavailable;
                return new ShopScreenActionResult(
                    ShopScreenActionStatus.InventoryUnavailable,
                    null,
                    unavailable);
            }

            ShopStockEntry entry = inventory.FindEntry(
                input.StockEntryStableId);
            if (entry == null)
            {
                ShopScreenView invalid = Project(
                    ShopScreenActionStatus.InvalidRequest,
                    ShopScreenFeedbackKind.Error,
                    "THE SELECTED STOCK ENTRY DOES NOT EXIST",
                    "shop-screen-stock-entry-unknown");
                currentProjection = invalid;
                return new ShopScreenActionResult(
                    ShopScreenActionStatus.InvalidRequest,
                    null,
                    invalid);
            }

            ICompensatingShopSave compensatingSave =
                save as ICompensatingShopSave;
            if (compensatingSave != null)
            {
                string preparationRejection;
                if (!compensatingSave.Prepare(
                        out preparationRejection))
                {
                    ShopScreenView rejected = Project(
                        ShopScreenActionStatus.PurchaseRejected,
                        ShopScreenFeedbackKind.Error,
                        "PURCHASE CHECKPOINT FAILED — NOTHING CHANGED",
                        string.IsNullOrWhiteSpace(preparationRejection)
                            ? "shop-purchase-checkpoint-rejected"
                            : preparationRejection);
                    currentProjection = rejected;
                    return new ShopScreenActionResult(
                        ShopScreenActionStatus.PurchaseRejected,
                        null,
                        rejected);
                }
            }

            ShopPurchaseCommand command = ShopPurchaseCommand.Create(
                input.InputStableId,
                inventory.RunStableId,
                inventory.ShopStableId,
                entry.StockEntryStableId,
                claimantStableId,
                inventory.InventoryFingerprint,
                entry.Price);
            ShopPurchaseFact fact = shopRuntime.Purchase(command);
            RefreshProjectionInventory();

            ShopScreenActionStatus status = MapStatus(fact.Status);
            ShopScreenFeedbackKind kind;
            string feedback;
            BuildFeedback(fact, entry, out kind, out feedback);
            string feedbackCode = fact.RejectionCode;
            if (fact.Status == ShopPurchaseStatus.Applied
                && save != null)
            {
                string persistenceRejection;
                if (!save.Persist(
                        fact.CommandFingerprint,
                        out persistenceRejection))
                {
                    string persistenceCode = string.IsNullOrWhiteSpace(
                        persistenceRejection)
                            ? "shop-purchase-persist-rejected"
                            : persistenceRejection;
                    string restoreRejection = null;
                    bool restored = compensatingSave != null
                        && compensatingSave.Restore(
                            out restoreRejection);
                    RefreshProjectionInventory();
                    kind = restored
                        ? ShopScreenFeedbackKind.Error
                        : ShopScreenFeedbackKind.Pending;
                    status = restored
                        ? ShopScreenActionStatus.PurchaseRejected
                        : ShopScreenActionStatus.CompensationPending;
                    feedback = restored
                        ? "PURCHASE SAVE FAILED — MONEY AND ITEM RESTORED"
                        : "PURCHASE SAVE FAILED — RECOVERY REQUIRED";
                    feedbackCode = restored
                        ? persistenceCode + ";compensated"
                        : persistenceCode
                            + ";restore="
                            + (string.IsNullOrWhiteSpace(restoreRejection)
                                ? "unavailable"
                                : restoreRejection);
                }
            }
            currentProjection = Project(
                status,
                kind,
                feedback,
                feedbackCode);
            return new ShopScreenActionResult(
                status,
                fact,
                currentProjection);
        }
    }
}
