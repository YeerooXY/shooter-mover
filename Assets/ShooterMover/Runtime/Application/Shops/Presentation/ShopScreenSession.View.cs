using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Shops.Presentation
{
    public sealed partial class ShopScreenSession
    {
        public ShopScreenRouteResult NavigateBack()
        {
            if (routeEmitted)
            {
                return new ShopScreenRouteResult(
                    ShopScreenRoute.None,
                    routePayload,
                    false,
                    "shop-screen-route-already-emitted");
            }

            routeEmitted = true;
            return new ShopScreenRouteResult(
                ShopScreenRoute.Hub,
                routePayload,
                true,
                string.Empty);
        }

        private bool EnsureInventory()
        {
            if (inventory != null)
            {
                return true;
            }

            ShopInventoryOpenResult opened = shopRuntime.Open(
                runStableId,
                definition,
                catalog,
                progressionContext);
            if (!opened.Succeeded || opened.Inventory == null)
            {
                return false;
            }

            inventory = opened.Inventory;
            return true;
        }

        private void RefreshProjectionInventory()
        {
            ShopInventoryOpenResult opened = shopRuntime.Open(
                runStableId,
                definition,
                catalog,
                progressionContext);
            if (opened.Succeeded && opened.Inventory != null)
            {
                inventory = opened.Inventory;
            }
        }

        private ShopScreenView Project(
            ShopScreenActionStatus status,
            ShopScreenFeedbackKind feedbackKind,
            string feedbackText,
            string feedbackCode)
        {
            var cards = new List<ShopScreenStockCard>();
            int refreshOrdinal = 0;
            string fingerprint = string.Empty;
            if (inventory != null)
            {
                refreshOrdinal = inventory.RefreshOrdinal;
                fingerprint = inventory.InventoryFingerprint;
                for (int index = 0; index < inventory.Entries.Count; index++)
                {
                    cards.Add(ProjectCard(inventory.Entries[index]));
                }
            }

            return new ShopScreenView(
                routePayload,
                runStableId,
                definition.ShopStableId,
                refreshOrdinal,
                fingerprint,
                moneyWallet.Balance,
                cards,
                status,
                feedbackKind,
                feedbackText,
                feedbackCode);
        }

        private ShopScreenStockCard ProjectCard(ShopStockEntry entry)
        {
            EquipmentDefinition equipmentDefinition =
                catalog.FindEquipmentDefinition(entry.Equipment.DefinitionId);
            string displayName = equipmentDefinition == null
                ? entry.Equipment.DefinitionId.ToString()
                : equipmentDefinition.DisplayName;
            string categoryLabel = CategoryLabel(
                equipmentDefinition == null ? null : equipmentDefinition.CategoryId);
            string qualityLabel = QualityLabel(
                equipmentDefinition,
                entry.Equipment.QualityId);
            return new ShopScreenStockCard(
                entry.StockEntryStableId,
                entry.Equipment.DefinitionId,
                entry.Equipment.InstanceId,
                displayName,
                categoryLabel,
                qualityLabel,
                entry.Equipment.ItemLevel,
                entry.Equipment.Augments.Count,
                entry.Price,
                entry.State,
                entry.PurchaseTransactionStableId);
        }

        private static string CategoryLabel(StableId categoryStableId)
        {
            if (categoryStableId == EquipmentCategoryIds.Gun)
            {
                return "GUN";
            }

            if (categoryStableId == EquipmentCategoryIds.Armor)
            {
                return "ARMOR";
            }

            return categoryStableId == null
                ? "EQUIPMENT"
                : categoryStableId.ToString();
        }

        private static string QualityLabel(
            EquipmentDefinition definition,
            StableId qualityStableId)
        {
            if (definition != null)
            {
                for (int index = 0; index < definition.QualityTiers.Count; index++)
                {
                    EquipmentQualityTier quality = definition.QualityTiers[index];
                    if (quality != null && quality.QualityId == qualityStableId)
                    {
                        return quality.Label;
                    }
                }
            }

            return qualityStableId == null
                ? string.Empty
                : qualityStableId.ToString();
        }

        private static ShopScreenActionStatus MapStatus(
            ShopPurchaseStatus status)
        {
            switch (status)
            {
                case ShopPurchaseStatus.Applied:
                    return ShopScreenActionStatus.PurchaseApplied;
                case ShopPurchaseStatus.ExactDuplicateNoChange:
                    return ShopScreenActionStatus.ExactDuplicateNoChange;
                case ShopPurchaseStatus.ConflictingDuplicate:
                    return ShopScreenActionStatus.ConflictingDuplicate;
                case ShopPurchaseStatus.PurchasePending:
                    return ShopScreenActionStatus.PurchasePending;
                case ShopPurchaseStatus.SoldOut:
                    return ShopScreenActionStatus.SoldOut;
                case ShopPurchaseStatus.InsufficientFunds:
                    return ShopScreenActionStatus.InsufficientFunds;
                case ShopPurchaseStatus.CompensationPending:
                    return ShopScreenActionStatus.CompensationPending;
                default:
                    return ShopScreenActionStatus.PurchaseRejected;
            }
        }

        private static void BuildFeedback(
            ShopPurchaseFact fact,
            ShopStockEntry entry,
            out ShopScreenFeedbackKind kind,
            out string feedback)
        {
            switch (fact.Status)
            {
                case ShopPurchaseStatus.Applied:
                    kind = ShopScreenFeedbackKind.Success;
                    feedback = "PURCHASE COMPLETE — INSTANCE "
                        + entry.Equipment.InstanceId;
                    return;
                case ShopPurchaseStatus.ExactDuplicateNoChange:
                    kind = ShopScreenFeedbackKind.Information;
                    feedback = "DUPLICATE INPUT REPLAYED — NO ADDITIONAL MONEY OR EQUIPMENT";
                    return;
                case ShopPurchaseStatus.ConflictingDuplicate:
                    kind = ShopScreenFeedbackKind.Error;
                    feedback = "CONFLICTING DUPLICATE INPUT REJECTED";
                    return;
                case ShopPurchaseStatus.PurchasePending:
                    kind = ShopScreenFeedbackKind.Pending;
                    feedback = "PURCHASE PENDING — RETRY THE SAME INPUT";
                    return;
                case ShopPurchaseStatus.CompensationPending:
                    kind = ShopScreenFeedbackKind.Pending;
                    feedback = "REFUND PENDING — RETRY THE SAME INPUT";
                    return;
                case ShopPurchaseStatus.SoldOut:
                    kind = ShopScreenFeedbackKind.Warning;
                    feedback = "THIS STOCK ENTRY IS SOLD";
                    return;
                case ShopPurchaseStatus.InsufficientFunds:
                    kind = ShopScreenFeedbackKind.Warning;
                    feedback = "INSUFFICIENT FUNDS — PRICE " + entry.Price;
                    return;
                case ShopPurchaseStatus.StaleInventoryFingerprint:
                    kind = ShopScreenFeedbackKind.Error;
                    feedback = "SHOP STOCK CHANGED — REOPEN THE CURRENT AUTHORITY VIEW";
                    return;
                case ShopPurchaseStatus.PriceMismatch:
                    kind = ShopScreenFeedbackKind.Error;
                    feedback = "PRICE MISMATCH — PURCHASE REJECTED";
                    return;
                default:
                    kind = ShopScreenFeedbackKind.Error;
                    feedback = "PURCHASE REJECTED — " + fact.Status;
                    return;
            }
        }
    }
}
