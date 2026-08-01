using System;
using System.Collections.Generic;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Shops;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Shops.Presentation
{
    /// <summary>
    /// Thin presentation boundary over SHOP/MON/INV/RAP. It retains only immutable
    /// projections and input identities; stock, sold state, prices, money, and grants
    /// remain owned by the existing authorities.
    /// </summary>
    public sealed partial class ShopScreenSession
    {
        private readonly PlayerRouteProfilePayload routePayload;
        private readonly StableId runStableId;
        private readonly StableId claimantStableId;
        private readonly ShopLiveActions shopRuntime;
        private readonly MoneyWalletActions moneyWallet;
        private readonly ShopDefinition definition;
        private readonly EquipmentCatalog catalog;
        private readonly ProgressionContext progressionContext;
        private readonly GeneratedEquipmentAugmentSignatureState augmentSignatures;
        private readonly DateTime? refreshesAtUtc;
        private readonly IShopScreenPersistencePort persistence;

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
            StableId runStableId,
            StableId claimantStableId,
            ShopLiveActions shopRuntime,
            MoneyWalletActions moneyWallet,
            ShopDefinition definition,
            EquipmentCatalog catalog,
            ProgressionContext progressionContext,
            GeneratedEquipmentAugmentSignatureState augmentSignatures,
            DateTime? refreshesAtUtc,
            IShopScreenPersistencePort persistence)
        {
            this.routePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            if (!routePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid immutable Hub route payload is required.",
                    nameof(routePayload));
            }

            this.runStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
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
            this.augmentSignatures = augmentSignatures;
            this.refreshesAtUtc = refreshesAtUtc;
            this.persistence = persistence;
        }

        public PlayerRouteProfilePayload RoutePayload
        {
            get { return routePayload; }
        }

        public StableId RunStableId
        {
            get { return runStableId; }
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
                runStableId,
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

            ShopStockEntry entry = inventory.FindEntry(input.StockEntryStableId);
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
                && persistence != null)
            {
                string persistenceRejection;
                if (!persistence.Persist(
                        fact.CommandFingerprint,
                        out persistenceRejection))
                {
                    kind = ShopScreenFeedbackKind.Error;
                    feedback =
                        "PURCHASE APPLIED — CHARACTER SAVE FAILED";
                    feedbackCode = string.IsNullOrWhiteSpace(
                            persistenceRejection)
                        ? "shop-purchase-persist-rejected"
                        : persistenceRejection;
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
