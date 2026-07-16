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
    public sealed partial class ShopRuntimeServiceV1
    {
        private ShopPurchaseFactV1 ResumePendingPurchase(PurchaseRecord record)
        {
            if (record.Fact.OriginalStatus == ShopPurchaseStatusV1.CompensationPending)
            {
                MoneyWalletChangeFact refund = money.Grant(
                    RefundTransaction(record.Command.TransactionStableId),
                    RefundOperation(record.Command.TransactionStableId),
                    record.Entry.Price);
                if (IsMoneyApplied(refund))
                {
                    record.State.SetEntry(record.Entry);
                    record.Fact = new ShopPurchaseFactV1(
                        record.Command.TransactionStableId,
                        record.Command.Fingerprint,
                        ShopPurchaseStatusV1.RewardApplicationRejected,
                        ShopPurchaseStatusV1.RewardApplicationRejected,
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

            RewardApplicationResultV1 retried = rewardApplication.Retry(
                RewardRetryClaimCommandV1.Create(
                    record.Commit.CommitmentStableId,
                    ClaimIdentity(record.Command.TransactionStableId)));
            if (IsRewardApplied(retried.Status))
            {
                record.State.SetEntry(record.Entry.WithPurchaseState(
                    ShopStockEntryStateV1.SoldOut,
                    record.Command.TransactionStableId));
                record.Fact = new ShopPurchaseFactV1(
                    record.Command.TransactionStableId,
                    record.Command.Fingerprint,
                    ShopPurchaseStatusV1.Applied,
                    ShopPurchaseStatusV1.Applied,
                    record.Entry.StockEntryStableId,
                    record.Entry.Price,
                    record.Fact.MoneyBalanceBefore,
                    money.Balance,
                    true,
                    null);
                return record.Fact;
            }

            return record.Fact;
        }

        private ShopPurchaseFactV1 RecordTerminal(
            ShopPurchaseCommandV1 command,
            ShopStockEntryV1 entry,
            ShopPurchaseStatusV1 status,
            long price,
            long balanceBefore,
            long balanceAfter,
            bool equipmentConfirmed,
            string rejectionCode)
        {
            ShopPurchaseFactV1 fact = new ShopPurchaseFactV1(
                command.TransactionStableId,
                command.Fingerprint,
                status,
                status,
                entry == null ? command.StockEntryStableId : entry.StockEntryStableId,
                price,
                balanceBefore,
                balanceAfter,
                equipmentConfirmed,
                rejectionCode);
            purchases.Add(command.TransactionStableId,
                new PurchaseRecord(command, fact, null, entry, null));
            return fact;
        }

        private ShopRefreshFactV1 RecordRefresh(
            ShopRefreshCommandV1 command,
            ShopState state,
            ShopRefreshStatusV1 status,
            string rejectionCode)
        {
            int ordinal = state == null ? -1 : state.RefreshOrdinal;
            string fingerprint = state == null ? null : state.InventoryFingerprint;
            ShopRefreshFactV1 fact = new ShopRefreshFactV1(
                command.TransactionStableId,
                command.Fingerprint,
                status,
                status,
                ordinal,
                ordinal,
                fingerprint,
                fingerprint,
                rejectionCode);
            refreshes.Add(command.TransactionStableId, new RefreshRecord(command, fact));
            return fact;
        }

        private bool TryGenerateInventory(
            StableId runStableId,
            ShopDefinitionV1 definition,
            EquipmentCatalog catalog,
            ProgressionContext context,
            int refreshOrdinal,
            IReadOnlyList<ShopStockEntryV1> lockedEntries,
            out ulong inventorySeed,
            out List<ShopStockEntryV1> entries,
            out string rejectionCode)
        {
            inventorySeed = ShopCanonicalV1.DeriveInventorySeed(
                runStableId,
                definition.ShopStableId,
                refreshOrdinal,
                definition.AlgorithmVersion);
            entries = new List<ShopStockEntryV1>();
            rejectionCode = null;
            for (int index = 0; index < lockedEntries.Count; index++)
            {
                entries.Add(lockedEntries[index]);
            }

            EquipmentGenerationPolicyV1 policy;
            if (!TryBuildRestrictedPolicy(definition, catalog, out policy, out rejectionCode))
            {
                return false;
            }

            for (int slotIndex = lockedEntries.Count;
                slotIndex < definition.InventorySize;
                slotIndex++)
            {
                string ordinal = refreshOrdinal.ToString(CultureInfo.InvariantCulture);
                string slot = slotIndex.ToString(CultureInfo.InvariantCulture);
                StableId operationId = ShopCanonicalV1.DeriveStableId(
                    "shopgenop",
                    runStableId.ToString(),
                    definition.ShopStableId.ToString(),
                    ordinal,
                    slot,
                    definition.AlgorithmVersion.ToString(CultureInfo.InvariantCulture));
                StableId equipmentInstanceId = ShopCanonicalV1.DeriveStableId(
                    "shopequipment",
                    runStableId.ToString(),
                    definition.ShopStableId.ToString(),
                    ordinal,
                    slot,
                    definition.AlgorithmVersion.ToString(CultureInfo.InvariantCulture));
                EquipmentGenerationResultV1 generated = generator.GenerateEquipment(
                    EquipmentGenerationRequestV1.Create(
                        operationId,
                        equipmentInstanceId,
                        policy,
                        catalog,
                        context,
                        inventorySeed,
                        definition.AlgorithmVersion));
                if (!generated.IsSuccess || generated.Equipment == null)
                {
                    rejectionCode = string.IsNullOrEmpty(generated.FailureReason)
                        ? "shop-generator-rejected"
                        : "shop-generator-rejected:" + generated.FailureReason;
                    return false;
                }

                long price;
                string priceFailure;
                if (!definition.PricingPolicy.TryCalculatePrice(
                    generated.Equipment,
                    catalog,
                    out price,
                    out priceFailure))
                {
                    rejectionCode = priceFailure;
                    return false;
                }

                StableId entryId = ShopCanonicalV1.DeriveStableId(
                    "shopstock",
                    runStableId.ToString(),
                    definition.ShopStableId.ToString(),
                    ordinal,
                    slot,
                    definition.AlgorithmVersion.ToString(CultureInfo.InvariantCulture));
                entries.Add(new ShopStockEntryV1(
                    entryId,
                    generated.Equipment,
                    price,
                    generated.ResultFingerprint,
                    ShopStockEntryStateV1.Available,
                    null));
            }

            entries.Sort();
            return true;
        }

        private static bool TryBuildRestrictedPolicy(
            ShopDefinitionV1 definition,
            EquipmentCatalog catalog,
            out EquipmentGenerationPolicyV1 result,
            out string rejectionCode)
        {
            List<EquipmentGenerationCandidateV1> candidates = new List<EquipmentGenerationCandidateV1>();
            EquipmentGenerationPolicyV1 source = definition.GenerationPolicy;
            for (int index = 0; index < source.EquipmentCandidates.Count; index++)
            {
                EquipmentGenerationCandidateV1 candidate = source.EquipmentCandidates[index];
                EquipmentDefinition equipment = catalog.FindEquipmentDefinition(
                    candidate.EquipmentDefinitionId);
                if (equipment != null && definition.Allows(equipment))
                {
                    candidates.Add(candidate);
                }
            }

            if (candidates.Count == 0)
            {
                result = null;
                rejectionCode = "shop-no-candidate-after-category-tag-restrictions";
                return false;
            }

            result = EquipmentGenerationPolicyV1.Create(
                ShopCanonicalV1.DeriveStableId(
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

        private RewardCommitCommandV1 BuildCommit(
            ShopPurchaseCommandV1 command,
            ShopState state,
            ShopStockEntryV1 entry)
        {
            StableId commitment = CommitmentIdentity(command.TransactionStableId);
            StableId sourceOperation = SourceOperationIdentity(command.TransactionStableId);
            StableId profile = ShopCanonicalV1.DeriveStableId(
                "shopprofile",
                state.ShopStableId.ToString(),
                state.DefinitionFingerprint);
            string contentFingerprint = ShopCanonicalV1.Fingerprint(
                "schema=shop-purchase-content-v1"
                + "\ndefinition_fingerprint=" + state.DefinitionFingerprint
                + "\ninventory_fingerprint=" + state.InventoryFingerprint
                + "\nentry_id=" + entry.StockEntryStableId
                + "\nequipment_fingerprint=" + entry.Equipment.Fingerprint
                + "\nprice=" + entry.Price.ToString(CultureInfo.InvariantCulture));
            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                command.RunStableId,
                command.ShopStableId,
                sourceOperation,
                commitment,
                profile,
                contentFingerprint);
            RewardGrantV1 grant = RewardGrantV1.Create(
                ShopCanonicalV1.DeriveStableId(
                    "shopgrant",
                    command.TransactionStableId.ToString()),
                RewardGrantKindV1.EquipmentReference,
                entry.Equipment.DefinitionId,
                1L);
            RewardResultV1 reward = RewardResultV1.CreateGrants(
                commitment,
                sourceOperation,
                new[] { grant });
            RewardGrantApplicationPayloadV1 payload =
                RewardGrantApplicationPayloadV1.ForEquipment(
                    grant,
                    new[] { entry.Equipment });
            return RewardCommitCommandV1.Create(
                operation,
                reward,
                entry.GenerationFingerprint,
                new[] { payload });
        }

        private RewardClaimCommandV1 BuildClaim(
            ShopPurchaseCommandV1 command,
            RewardCommitCommandV1 commit)
        {
            return RewardClaimCommandV1.Create(
                ClaimIdentity(command.TransactionStableId),
                commit.CommitmentStableId,
                command.ClaimantStableId,
                MoneyWalletIdsV1.AuthorityStableId,
                scrapAuthorityStableId,
                holdingsAuthorityStableId);
        }

        private static bool IsCommitAccepted(RewardApplicationResultStatusV1 status)
        {
            return status == RewardApplicationResultStatusV1.Generated
                || status == RewardApplicationResultStatusV1.ExactDuplicateNoChange;
        }

        private static bool IsRewardApplied(RewardApplicationResultStatusV1 status)
        {
            return status == RewardApplicationResultStatusV1.Applied
                || status == RewardApplicationResultStatusV1.AlreadyAppliedNoChange;
        }

        private static bool IsMoneyApplied(MoneyWalletChangeFact fact)
        {
            return fact != null
                && (fact.Status == MoneyWalletTransactionStatus.Applied
                    || (fact.Status == MoneyWalletTransactionStatus.DuplicateNoChange
                        && fact.OriginalStatus == MoneyWalletTransactionStatus.Applied));
        }

        private static string Key(StableId runStableId, StableId shopStableId)
        {
            return runStableId + "|" + shopStableId;
        }

        private static StableId SpendTransaction(StableId purchaseId)
        {
            return ShopCanonicalV1.DeriveStableId("shopspend", purchaseId.ToString());
        }

        private static StableId SpendOperation(StableId purchaseId)
        {
            return ShopCanonicalV1.DeriveStableId("shopspendop", purchaseId.ToString());
        }

        private static StableId RefundTransaction(StableId purchaseId)
        {
            return ShopCanonicalV1.DeriveStableId("shoprefund", purchaseId.ToString());
        }

        private static StableId RefundOperation(StableId purchaseId)
        {
            return ShopCanonicalV1.DeriveStableId("shoprefundop", purchaseId.ToString());
        }

        private static StableId SourceOperationIdentity(StableId purchaseId)
        {
            return ShopCanonicalV1.DeriveStableId("shopsourceop", purchaseId.ToString());
        }

        private static StableId CommitmentIdentity(StableId purchaseId)
        {
            return ShopCanonicalV1.DeriveStableId("shopcommit", purchaseId.ToString());
        }

        private static StableId ClaimIdentity(StableId purchaseId)
        {
            return ShopCanonicalV1.DeriveStableId("shopclaim", purchaseId.ToString());
        }

    }
}
