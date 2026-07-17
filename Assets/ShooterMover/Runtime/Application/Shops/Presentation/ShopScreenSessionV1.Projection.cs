using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Shops.Presentation
{
    public sealed partial class ShopScreenSessionV1
    {
        public ShopScreenRouteResultV1 NavigateBack()
        {
            if (routeEmitted)
            {
                return new ShopScreenRouteResultV1(
                    ShopScreenRouteV1.None,
                    routePayload,
                    false,
                    "shop-screen-route-already-emitted");
            }

            routeEmitted = true;
            return new ShopScreenRouteResultV1(
                ShopScreenRouteV1.Hub,
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

            ShopInventoryOpenResultV1 opened = shopRuntime.Open(
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
            ShopInventoryOpenResultV1 opened = shopRuntime.Open(
                runStableId,
                definition,
                catalog,
                progressionContext);
            if (opened.Succeeded && opened.Inventory != null)
            {
                inventory = opened.Inventory;
            }
        }

        private ShopScreenProjectionV1 Project(
            ShopScreenActionStatusV1 status,
            ShopScreenFeedbackKindV1 feedbackKind,
            string feedbackText,
            string feedbackCode)
        {
            var cards = new List<ShopScreenStockCardV1>();
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

            return new ShopScreenProjectionV1(
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

        private ShopScreenStockCardV1 ProjectCard(ShopStockEntryV1 entry)
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
            return new ShopScreenStockCardV1(
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
            if (categoryStableId == EquipmentCategoryIds.Weapon)
            {
                return "WEAPON";
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

        private static ShopScreenActionStatusV1 MapStatus(
            ShopPurchaseStatusV1 status)
        {
            switch (status)
            {
                case ShopPurchaseStatusV1.Applied:
                    return ShopScreenActionStatusV1.PurchaseApplied;
                case ShopPurchaseStatusV1.ExactDuplicateNoChange:
                    return ShopScreenActionStatusV1.ExactDuplicateNoChange;
                case ShopPurchaseStatusV1.ConflictingDuplicate:
                    return ShopScreenActionStatusV1.ConflictingDuplicate;
                case ShopPurchaseStatusV1.PurchasePending:
                    return ShopScreenActionStatusV1.PurchasePending;
                case ShopPurchaseStatusV1.SoldOut:
                    return ShopScreenActionStatusV1.SoldOut;
                case ShopPurchaseStatusV1.InsufficientFunds:
                    return ShopScreenActionStatusV1.InsufficientFunds;
                case ShopPurchaseStatusV1.CompensationPending:
                    return ShopScreenActionStatusV1.CompensationPending;
                default:
                    return ShopScreenActionStatusV1.PurchaseRejected;
            }
        }

        private static void BuildFeedback(
            ShopPurchaseFactV1 fact,
            ShopStockEntryV1 entry,
            out ShopScreenFeedbackKindV1 kind,
            out string feedback)
        {
            switch (fact.Status)
            {
                case ShopPurchaseStatusV1.Applied:
                    kind = ShopScreenFeedbackKindV1.Success;
                    feedback = "PURCHASE COMPLETE — INSTANCE "
                        + entry.Equipment.InstanceId;
                    return;
                case ShopPurchaseStatusV1.ExactDuplicateNoChange:
                    kind = ShopScreenFeedbackKindV1.Information;
                    feedback = "DUPLICATE INPUT REPLAYED — NO ADDITIONAL MONEY OR EQUIPMENT";
                    return;
                case ShopPurchaseStatusV1.ConflictingDuplicate:
                    kind = ShopScreenFeedbackKindV1.Error;
                    feedback = "CONFLICTING DUPLICATE INPUT REJECTED";
                    return;
                case ShopPurchaseStatusV1.PurchasePending:
                    kind = ShopScreenFeedbackKindV1.Pending;
                    feedback = "PURCHASE PENDING — RETRY THE SAME INPUT";
                    return;
                case ShopPurchaseStatusV1.CompensationPending:
                    kind = ShopScreenFeedbackKindV1.Pending;
                    feedback = "REFUND PENDING — RETRY THE SAME INPUT";
                    return;
                case ShopPurchaseStatusV1.SoldOut:
                    kind = ShopScreenFeedbackKindV1.Warning;
                    feedback = "THIS STOCK ENTRY IS SOLD";
                    return;
                case ShopPurchaseStatusV1.InsufficientFunds:
                    kind = ShopScreenFeedbackKindV1.Warning;
                    feedback = "INSUFFICIENT FUNDS — PRICE " + entry.Price;
                    return;
                case ShopPurchaseStatusV1.StaleInventoryFingerprint:
                    kind = ShopScreenFeedbackKindV1.Error;
                    feedback = "SHOP STOCK CHANGED — REOPEN THE CURRENT AUTHORITY VIEW";
                    return;
                case ShopPurchaseStatusV1.PriceMismatch:
                    kind = ShopScreenFeedbackKindV1.Error;
                    feedback = "PRICE MISMATCH — PURCHASE REJECTED";
                    return;
                default:
                    kind = ShopScreenFeedbackKindV1.Error;
                    feedback = "PURCHASE REJECTED — " + fact.Status;
                    return;
            }
        }
    }
}
