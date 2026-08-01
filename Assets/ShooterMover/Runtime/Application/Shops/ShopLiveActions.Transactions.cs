using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Shops;

namespace ShooterMover.Application.Shops
{
    public sealed partial class ShopLiveActions
    {
        private ShopPurchaseFact ResumePendingPurchase(PurchaseRecord record)
        {
            if (record.Fact.OriginalStatus
                == ShopPurchaseStatus.CompensationPending)
            {
                MoneyWalletChangeFact refund = money.Grant(
                    RefundTransaction(record.Command.TransactionStableId),
                    RefundOperation(record.Command.TransactionStableId),
                    record.Entry.Price);
                if (IsMoneyApplied(refund))
                {
                    record.State.SetEntry(record.Entry);
                    record.Fact = new ShopPurchaseFact(
                        record.Command.TransactionStableId,
                        record.Command.Fingerprint,
                        ShopPurchaseStatus.RewardApplicationRejected,
                        ShopPurchaseStatus.RewardApplicationRejected,
                        record.Entry.StockEntryStableId,
                        record.Entry.Price,
                        record.Fact.MoneyBalanceBefore,
                        money.Balance,
                        false,
                        "shop-rap-rejected-refunded");
                    return record.Fact;
                }

                return record.Fact;
            }

            RewardApplicationResult retried = rewardApplication.Retry(
                RewardRetryClaimCommand.Create(
                    record.Commit.CommitmentStableId,
                    ClaimIdentity(record.Command.TransactionStableId)));
            if (IsRewardApplied(retried.Status))
            {
                record.State.SetEntry(record.Entry.WithPurchaseState(
                    ShopStockEntryState.SoldOut,
                    record.Command.TransactionStableId));
                string receiptDiagnostic;
                RecordReceipt(
                    record.Entry.StockEntryStableId,
                    record.Command.TransactionStableId,
                    out receiptDiagnostic);
                record.Fact = new ShopPurchaseFact(
                    record.Command.TransactionStableId,
                    record.Command.Fingerprint,
                    ShopPurchaseStatus.Applied,
                    ShopPurchaseStatus.Applied,
                    record.Entry.StockEntryStableId,
                    record.Entry.Price,
                    record.Fact.MoneyBalanceBefore,
                    money.Balance,
                    true,
                    receiptDiagnostic);
                return record.Fact;
            }

            return record.Fact;
        }

        private ShopPurchaseFact RecordTerminal(
            ShopPurchaseCommand command,
            ShopStockEntry entry,
            ShopPurchaseStatus status,
            long price,
            long balanceBefore,
            long balanceAfter,
            bool equipmentConfirmed,
            string rejectionCode)
        {
            ShopPurchaseFact fact = new ShopPurchaseFact(
                command.TransactionStableId,
                command.Fingerprint,
                status,
                status,
                entry == null
                    ? command.StockEntryStableId
                    : entry.StockEntryStableId,
                price,
                balanceBefore,
                balanceAfter,
                equipmentConfirmed,
                rejectionCode);
            purchases.Add(
                command.TransactionStableId,
                new PurchaseRecord(command, fact, null, entry, null));
            return fact;
        }

        private ShopRefreshFact RecordRefresh(
            ShopRefreshCommand command,
            ShopState state,
            ShopRefreshStatus status,
            string rejectionCode)
        {
            int ordinal = state == null ? -1 : state.RefreshOrdinal;
            string fingerprint = state == null
                ? null
                : state.InventoryFingerprint;
            ShopRefreshFact fact = new ShopRefreshFact(
                command.TransactionStableId,
                command.Fingerprint,
                status,
                status,
                ordinal,
                ordinal,
                fingerprint,
                fingerprint,
                rejectionCode);
            refreshes.Add(
                command.TransactionStableId,
                new RefreshRecord(command, fact));
            return fact;
        }

        private bool TryGenerateInventory(
            StableId stockId,
            ShopDefinition definition,
            EquipmentCatalog catalog,
            ProgressionContext context,
            int revision,
            IReadOnlyList<ShopStockEntry> lockedEntries,
            out ulong inventorySeed,
            out List<ShopStockEntry> entries,
            out string rejectionCode)
        {
            inventorySeed = Shop.DeriveInventorySeed(
                stockId,
                definition.ShopStableId,
                revision,
                definition.AlgorithmVersion);
            entries = new List<ShopStockEntry>();
            rejectionCode = null;
            for (int index = 0; index < lockedEntries.Count; index++)
            {
                entries.Add(lockedEntries[index]);
            }

            EquipmentGenerationPolicy policy = null;
            if (offerRoller == null
                && !TryBuildRestrictedPolicy(
                    definition,
                    catalog,
                    out policy,
                    out rejectionCode))
            {
                return false;
            }

            for (int slotIndex = lockedEntries.Count;
                slotIndex < definition.InventorySize;
                slotIndex++)
            {
                string ordinal = revision.ToString(
                    CultureInfo.InvariantCulture);
                string slot = slotIndex.ToString(
                    CultureInfo.InvariantCulture);
                EquipmentInstance equipment;
                string generationFingerprint;

                if (offerRoller != null)
                {
                    ShopOfferRoll rolled;
                    if (!offerRoller.TryRoll(
                            new ShopOfferRequest(
                                stockId,
                                definition,
                                catalog,
                                context,
                                inventorySeed,
                                revision,
                                slotIndex),
                            out rolled,
                            out rejectionCode)
                        || rolled == null
                        || rolled.Equipment == null)
                    {
                        if (string.IsNullOrWhiteSpace(rejectionCode))
                        {
                            rejectionCode = "shop-offer-roller-rejected";
                        }
                        return false;
                    }
                    equipment = rolled.Equipment;
                    generationFingerprint = rolled.Fingerprint;
                }
                else
                {
                    StableId operationId = Shop.DeriveStableId(
                        "shopgenop",
                        stockId.ToString(),
                        definition.ShopStableId.ToString(),
                        ordinal,
                        slot,
                        definition.AlgorithmVersion.ToString(
                            CultureInfo.InvariantCulture));
                    StableId equipmentInstanceId = Shop.DeriveStableId(
                        "shopequipment",
                        stockId.ToString(),
                        definition.ShopStableId.ToString(),
                        ordinal,
                        slot,
                        definition.AlgorithmVersion.ToString(
                            CultureInfo.InvariantCulture));
                    EquipmentGenerationResult generated =
                        generator.GenerateEquipment(
                            EquipmentGenerationRequest.Create(
                                operationId,
                                equipmentInstanceId,
                                policy,
                                catalog,
                                context,
                                inventorySeed,
                                definition.AlgorithmVersion));
                    if (!generated.IsSuccess || generated.Equipment == null)
                    {
                        rejectionCode = string.IsNullOrEmpty(
                                generated.FailureReason)
                            ? "shop-generator-rejected"
                            : "shop-generator-rejected:"
                                + generated.FailureReason;
                        return false;
                    }
                    equipment = generated.Equipment;
                    generationFingerprint = generated.ResultFingerprint;
                }

                long price;
                string priceFailure;
                if (!definition.PricingPolicy.TryCalculatePrice(
                    equipment,
                    catalog,
                    out price,
                    out priceFailure))
                {
                    rejectionCode = priceFailure;
                    return false;
                }

                StableId entryId = Shop.DeriveStableId(
                    "shopstock",
                    stockId.ToString(),
                    definition.ShopStableId.ToString(),
                    ordinal,
                    slot,
                    definition.AlgorithmVersion.ToString(
                        CultureInfo.InvariantCulture));
                entries.Add(new ShopStockEntry(
                    entryId,
                    equipment,
                    price,
                    generationFingerprint,
                    ShopStockEntryState.Available,
                    null));
            }

            entries.Sort();
            return true;
        }

        private static bool TryBuildRestrictedPolicy(
            ShopDefinition definition,
            EquipmentCatalog catalog,
            out EquipmentGenerationPolicy result,
            out string rejectionCode)
        {
            var candidates = new List<EquipmentGenerationCandidate>();
            EquipmentGenerationPolicy source = definition.GenerationPolicy;
            for (int index = 0;
                 index < source.EquipmentCandidates.Count;
                 index++)
            {
                EquipmentGenerationCandidate candidate =
                    source.EquipmentCandidates[index];
                EquipmentDefinition equipment =
                    catalog.FindEquipmentDefinition(
                        candidate.EquipmentDefinitionId);
                if (equipment != null && definition.Allows(equipment))
                {
                    candidates.Add(candidate);
                }
            }

            if (candidates.Count == 0)
            {
                result = null;
                rejectionCode =
                    "shop-no-candidate-after-category-tag-restrictions";
                return false;
            }

            result = EquipmentGenerationPolicy.Create(
                Shop.DeriveStableId(
                    "shopgenpolicy",
                    definition.ShopStableId.ToString(),
                    source.Fingerprint,
                    definition.Fingerprint),
                candidates,
                source.QualityCandidates,
                source.AugmentCandidates,
                source.MinimumAugmentSlots,
                source.MaximumAugmentSlots,
                source.RequireExactSlotCount,
                source.Activation,
                source.Obsolescence);
            rejectionCode = null;
            return true;
        }

        private RewardCommitCommand BuildCommit(
            ShopPurchaseCommand command,
            ShopState state,
            ShopStockEntry entry)
        {
            StableId commitment = CommitmentIdentity(
                command.TransactionStableId);
            StableId sourceOperation = SourceOperationIdentity(
                command.TransactionStableId);
            StableId profile = Shop.DeriveStableId(
                "shopprofile",
                state.ShopStableId.ToString(),
                state.DefinitionFingerprint);
            string contentFingerprint = Shop.Fingerprint(
                "schema=shop-purchase-content-v1"
                + "\ndefinition_fingerprint="
                + state.DefinitionFingerprint
                + "\ninventory_fingerprint="
                + state.InventoryFingerprint
                + "\nentry_id=" + entry.StockEntryStableId
                + "\nequipment_fingerprint="
                + entry.Equipment.Fingerprint
                + "\nprice="
                + entry.Price.ToString(CultureInfo.InvariantCulture));
            RewardOperationRequest operation = RewardOperationRequest.Create(
                command.RunStableId,
                command.ShopStableId,
                sourceOperation,
                commitment,
                profile,
                contentFingerprint);
            RewardGrant grant = RewardGrant.Create(
                Shop.DeriveStableId(
                    "shopgrant",
                    command.TransactionStableId.ToString()),
                RewardGrantKind.EquipmentReference,
                entry.Equipment.DefinitionId,
                1L);
            RewardResult reward = RewardResult.CreateGrants(
                commitment,
                sourceOperation,
                new[] { grant });
            RewardGrantApplicationPayload payload =
                RewardGrantApplicationPayload.ForEquipment(
                    grant,
                    new[] { entry.Equipment });
            return RewardCommitCommand.Create(
                operation,
                reward,
                entry.GenerationFingerprint,
                new[] { payload });
        }

        private RewardClaimCommand BuildClaim(
            ShopPurchaseCommand command,
            RewardCommitCommand commit)
        {
            return RewardClaimCommand.Create(
                ClaimIdentity(command.TransactionStableId),
                commit.CommitmentStableId,
                command.ClaimantStableId,
                MoneyWalletIds.AuthorityStableId,
                scrapAuthorityStableId,
                holdingsAuthorityStableId);
        }

        private static bool IsCommitAccepted(
            RewardApplicationResultStatus status)
        {
            return status == RewardApplicationResultStatus.Generated
                || status
                    == RewardApplicationResultStatus.ExactDuplicateNoChange;
        }

        private static bool IsRewardApplied(
            RewardApplicationResultStatus status)
        {
            return status == RewardApplicationResultStatus.Applied
                || status
                    == RewardApplicationResultStatus.AlreadyAppliedNoChange;
        }

        private static bool IsMoneyApplied(MoneyWalletChangeFact fact)
        {
            return fact != null
                && (fact.Status == MoneyWalletTransactionStatus.Applied
                    || (fact.Status
                            == MoneyWalletTransactionStatus.DuplicateNoChange
                        && fact.OriginalStatus
                            == MoneyWalletTransactionStatus.Applied));
        }

        private static string Key(
            StableId stockId,
            StableId shopId)
        {
            return stockId + "|" + shopId;
        }

        private static StableId SpendTransaction(StableId purchaseId)
        {
            return Shop.DeriveStableId(
                "shopspend",
                purchaseId.ToString());
        }

        private static StableId SpendOperation(StableId purchaseId)
        {
            return Shop.DeriveStableId(
                "shopspendop",
                purchaseId.ToString());
        }

        private static StableId RefundTransaction(StableId purchaseId)
        {
            return Shop.DeriveStableId(
                "shoprefund",
                purchaseId.ToString());
        }

        private static StableId RefundOperation(StableId purchaseId)
        {
            return Shop.DeriveStableId(
                "shoprefundop",
                purchaseId.ToString());
        }

        private static StableId SourceOperationIdentity(StableId purchaseId)
        {
            return Shop.DeriveStableId(
                "shopsourceop",
                purchaseId.ToString());
        }

        private static StableId CommitmentIdentity(StableId purchaseId)
        {
            return Shop.DeriveStableId(
                "shopcommit",
                purchaseId.ToString());
        }

        private static StableId ClaimIdentity(StableId purchaseId)
        {
            return Shop.DeriveStableId(
                "shopclaim",
                purchaseId.ToString());
        }
    }
}
