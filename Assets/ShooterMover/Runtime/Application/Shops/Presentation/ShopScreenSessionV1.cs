using System;
using System.Collections.Generic;
using ShooterMover.Application.Economy.Money;
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
    public sealed partial class ShopScreenSessionV1
    {
        private readonly PlayerRouteProfilePayloadV1 routePayload;
        private readonly StableId runStableId;
        private readonly StableId claimantStableId;
        private readonly ShopRuntimeServiceV1 shopRuntime;
        private readonly MoneyWalletService moneyWallet;
        private readonly ShopDefinitionV1 definition;
        private readonly EquipmentCatalog catalog;
        private readonly ProgressionContext progressionContext;

        private ShopInventoryViewV1 inventory;
        private ShopScreenProjectionV1 currentProjection;
        private bool routeEmitted;

        public ShopScreenSessionV1(
            PlayerRouteProfilePayloadV1 routePayload,
            StableId runStableId,
            StableId claimantStableId,
            ShopRuntimeServiceV1 shopRuntime,
            MoneyWalletService moneyWallet,
            ShopDefinitionV1 definition,
            EquipmentCatalog catalog,
            ProgressionContext progressionContext)
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
        }

        public PlayerRouteProfilePayloadV1 RoutePayload
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

        public ShopScreenProjectionV1 CurrentProjection
        {
            get { return currentProjection; }
        }

        public bool IsRouteEmitted
        {
            get { return routeEmitted; }
        }

        public ShopScreenProjectionV1 Open()
        {
            if (routeEmitted)
            {
                currentProjection = Project(
                    ShopScreenActionStatusV1.InputLocked,
                    ShopScreenFeedbackKindV1.Warning,
                    "INPUT LOCKED — A ROUTE HAS ALREADY BEEN EMITTED",
                    "shop-screen-input-locked");
                return currentProjection;
            }

            ShopInventoryOpenResultV1 opened = shopRuntime.Open(
                runStableId,
                definition,
                catalog,
                progressionContext);
            if (!opened.Succeeded || opened.Inventory == null)
            {
                currentProjection = Project(
                    ShopScreenActionStatusV1.InventoryUnavailable,
                    ShopScreenFeedbackKindV1.Error,
                    "SHOP STOCK UNAVAILABLE",
                    opened.RejectionCode);
                return currentProjection;
            }

            inventory = opened.Inventory;
            currentProjection = Project(
                ShopScreenActionStatusV1.Ready,
                ShopScreenFeedbackKindV1.Information,
                opened.Status == ShopInventoryOpenStatusV1.Generated
                    ? "DETERMINISTIC STOCK GENERATED FOR THIS RUN"
                    : "DETERMINISTIC STOCK RESTORED — NO REROLL",
                string.Empty);
            return currentProjection;
        }

        public ShopScreenActionResultV1 SubmitPurchase(
            ShopScreenPurchaseInputV1 input)
        {
            if (routeEmitted)
            {
                ShopScreenProjectionV1 locked = Project(
                    ShopScreenActionStatusV1.InputLocked,
                    ShopScreenFeedbackKindV1.Warning,
                    "INPUT LOCKED — RETURN ROUTE ALREADY EMITTED",
                    "shop-screen-input-locked");
                currentProjection = locked;
                return new ShopScreenActionResultV1(
                    ShopScreenActionStatusV1.InputLocked,
                    null,
                    locked);
            }

            if (input == null)
            {
                ShopScreenProjectionV1 invalid = Project(
                    ShopScreenActionStatusV1.InvalidRequest,
                    ShopScreenFeedbackKindV1.Error,
                    "PURCHASE INPUT IS MISSING",
                    "shop-screen-purchase-input-null");
                currentProjection = invalid;
                return new ShopScreenActionResultV1(
                    ShopScreenActionStatusV1.InvalidRequest,
                    null,
                    invalid);
            }

            if (!EnsureInventory())
            {
                ShopScreenProjectionV1 unavailable = Project(
                    ShopScreenActionStatusV1.InventoryUnavailable,
                    ShopScreenFeedbackKindV1.Error,
                    "SHOP STOCK UNAVAILABLE",
                    "shop-screen-inventory-unavailable");
                currentProjection = unavailable;
                return new ShopScreenActionResultV1(
                    ShopScreenActionStatusV1.InventoryUnavailable,
                    null,
                    unavailable);
            }

            ShopStockEntryV1 entry = inventory.FindEntry(input.StockEntryStableId);
            if (entry == null)
            {
                ShopScreenProjectionV1 invalid = Project(
                    ShopScreenActionStatusV1.InvalidRequest,
                    ShopScreenFeedbackKindV1.Error,
                    "THE SELECTED STOCK ENTRY DOES NOT EXIST",
                    "shop-screen-stock-entry-unknown");
                currentProjection = invalid;
                return new ShopScreenActionResultV1(
                    ShopScreenActionStatusV1.InvalidRequest,
                    null,
                    invalid);
            }

            ShopPurchaseCommandV1 command = ShopPurchaseCommandV1.Create(
                input.InputStableId,
                inventory.RunStableId,
                inventory.ShopStableId,
                entry.StockEntryStableId,
                claimantStableId,
                inventory.InventoryFingerprint,
                entry.Price);
            ShopPurchaseFactV1 fact = shopRuntime.Purchase(command);
            RefreshProjectionInventory();

            ShopScreenActionStatusV1 status = MapStatus(fact.Status);
            ShopScreenFeedbackKindV1 kind;
            string feedback;
            BuildFeedback(fact, entry, out kind, out feedback);
            currentProjection = Project(
                status,
                kind,
                feedback,
                fact.RejectionCode);
            return new ShopScreenActionResultV1(
                status,
                fact,
                currentProjection);
        }

    }
}
