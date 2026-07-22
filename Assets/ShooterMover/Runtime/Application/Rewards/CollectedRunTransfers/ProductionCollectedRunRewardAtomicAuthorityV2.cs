using System;
using System.Collections.Generic;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    internal sealed class ProductionCollectedRunRewardCompensationV2 :
        ICollectedRunRewardTransferCompensationV1
    {
        public ProductionCollectedRunRewardCompensationV2(
            MoneyWalletSnapshot money,
            ScrapSnapshotV1 scrap,
            PlayerHoldingsSnapshotV1 holdings,
            RewardApplicationSnapshotV1 rewardApplication,
            StrongboxOpeningSnapshotV1 strongboxes,
            CollectedRunRewardTransferReceiptSnapshotV1 receipts,
            CollectedRunRewardPreparedTransferSnapshotV1 prepared)
        {
            Money = money ?? throw new ArgumentNullException(nameof(money));
            Scrap = scrap ?? throw new ArgumentNullException(nameof(scrap));
            Holdings = holdings ?? throw new ArgumentNullException(nameof(holdings));
            RewardApplication = rewardApplication
                ?? throw new ArgumentNullException(nameof(rewardApplication));
            Strongboxes = strongboxes
                ?? throw new ArgumentNullException(nameof(strongboxes));
            Receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
            Prepared = prepared ?? throw new ArgumentNullException(nameof(prepared));
            Fingerprint = CollectedRunRewardTransferCanonicalV1.Hash(
                Money.Fingerprint
                + "|" + Scrap.Fingerprint
                + "|" + Holdings.Fingerprint
                + "|" + RewardApplication.Fingerprint
                + "|" + Strongboxes.Fingerprint
                + "|" + Receipts.Fingerprint
                + "|" + Prepared.Fingerprint);
        }

        public MoneyWalletSnapshot Money { get; }
        public ScrapSnapshotV1 Scrap { get; }
        public PlayerHoldingsSnapshotV1 Holdings { get; }
        public RewardApplicationSnapshotV1 RewardApplication { get; }
        public StrongboxOpeningSnapshotV1 Strongboxes { get; }
        public CollectedRunRewardTransferReceiptSnapshotV1 Receipts { get; }
        public CollectedRunRewardPreparedTransferSnapshotV1 Prepared { get; }
        public string Fingerprint { get; }
    }

    /// <summary>
    /// Concrete one-call authority over the selected character's existing RAP, wallet,
    /// scrap, holdings, BOX, receipt and prepared-custody authorities.
    /// </summary>
    public sealed class ProductionCollectedRunRewardAtomicAuthorityV2 :
        ICollectedRunRewardAtomicBatchAuthorityPortV1
    {
        private readonly ProductionCharacterRuntimeGraphV1 graph;
        private readonly RewardApplicationServiceV1 rewardApplication;
        private readonly CollectedRunRewardPreparedTransferAuthorityV1 prepared;
        private readonly CollectedRunRewardTransferReceiptAuthorityV1 receipts;

        public ProductionCollectedRunRewardAtomicAuthorityV2(
            ProductionCharacterRuntimeGraphV1 graph,
            RewardApplicationServiceV1 rewardApplication,
            CollectedRunRewardPreparedTransferAuthorityV1 prepared,
            CollectedRunRewardTransferReceiptAuthorityV1 receipts)
        {
            this.graph = graph ?? throw new ArgumentNullException(nameof(graph));
            this.rewardApplication = rewardApplication
                ?? throw new ArgumentNullException(nameof(rewardApplication));
            this.prepared = prepared ?? throw new ArgumentNullException(nameof(prepared));
            this.receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
        }

        public PermanentRewardTransferStateV1 ExportState()
        {
            CharacterInstanceSnapshotV1 character = graph.Character;
            var fingerprints = ExportAuthorityFingerprints();
            return new PermanentRewardTransferStateV1(
                character.CharacterInstanceStableId,
                character.Revision,
                character.Fingerprint,
                0L,
                CollectedRunRewardTransferCanonicalV1.Hash(
                    "runtime-account-unavailable|" + character.Fingerprint),
                fingerprints);
        }

        public bool TryGetDurableReceipt(
            StableId transferOperationStableId,
            out CollectedRunRewardTransferReceiptV1 receipt)
        {
            return receipts.TryGetByOperation(transferOperationStableId, out receipt);
        }

        public bool TryGetDurableReceiptForReward(
            StableId rewardInstanceStableId,
            out CollectedRunRewardTransferReceiptV1 receipt)
        {
            return receipts.TryGetByReward(rewardInstanceStableId, out receipt);
        }

        public CollectedRunRewardTransferPreflightResultV1 Preflight(
            CollectedRunRewardAtomicPlanV2 plan)
        {
            if (plan == null)
                return Reject("collected-run-transfer-plan-null");
            CollectedRunRewardPreparedTransferV1 custody = plan.PreparedTransfer;
            if (graph.IsDisposed
                || graph.Character.CharacterInstanceStableId
                    != custody.SelectedCharacterStableId)
            {
                return Reject("collected-run-transfer-selected-character-mismatch");
            }
            if (custody.State
                != CollectedRunRewardPreparedTransferStateV1.Prepared)
            {
                return Reject("collected-run-transfer-custody-not-prepared");
            }

            IDictionary<string, string> current = ExportAuthorityFingerprints();
            foreach (KeyValuePair<string, string> pair in
                custody.FrozenAuthorityFingerprints)
            {
                string value;
                if (!current.TryGetValue(pair.Key, out value)
                    || !string.Equals(value, pair.Value, StringComparison.Ordinal))
                {
                    return Reject(
                        "collected-run-transfer-frozen-authority-mismatch:"
                        + pair.Key);
                }
            }
            if (graph.MoneyWallet.Sequence != custody.ExpectedMoneySequence
                || graph.ScrapWallet.Sequence != custody.ExpectedScrapSequence
                || graph.LoadoutRuntime.Holdings.Sequence
                    != custody.ExpectedHoldingsSequence)
            {
                return Reject("collected-run-transfer-frozen-sequence-mismatch");
            }

            CollectedRunRewardTransferPreflightResultV1 boxPreflight =
                PreflightStrongboxes(plan);
            if (!boxPreflight.Succeeded) return boxPreflight;
            return DryRunRap(plan);
        }

        public ICollectedRunRewardTransferCompensationV1 CaptureCompensation()
        {
            return new ProductionCollectedRunRewardCompensationV2(
                graph.MoneyWallet.CurrentSnapshot,
                graph.ScrapWallet.ExportSnapshot(),
                graph.LoadoutRuntime.Holdings.ExportSnapshot(),
                rewardApplication.ExportSnapshot(),
                graph.StrongboxAuthority.ExportSnapshot(),
                receipts.ExportSnapshot(),
                prepared.ExportSnapshot());
        }

        public CollectedRunRewardAtomicApplyResultV1 ApplyAtomicBatch(
            CollectedRunRewardAtomicPlanV2 plan)
        {
            if (plan == null)
            {
                return RejectedApply(
                    "collected-run-transfer-atomic-plan-null");
            }
            RewardApplicationResultV1 committed =
                rewardApplication.Commit(plan.CommitCommand);
            if (!CommitAccepted(committed))
            {
                return RejectedApply(
                    "collected-run-transfer-rap-commit-rejected:"
                    + ResultCode(committed));
            }
            RewardApplicationResultV1 claimed =
                rewardApplication.Claim(plan.ClaimCommand);
            if (!ClaimAccepted(claimed))
            {
                return RejectedApply(
                    "collected-run-transfer-rap-claim-rejected:"
                    + ResultCode(claimed));
            }
            for (int index = 0; index < plan.StrongboxContexts.Count; index++)
            {
                StrongboxRegistrationResultV1 registered =
                    graph.StrongboxAuthority.RegisterInstance(
                        plan.StrongboxContexts[index]);
                if (registered == null
                    || (registered.Status
                            != StrongboxRegistrationStatusV1.Registered
                        && registered.Status
                            != StrongboxRegistrationStatusV1
                                .ExactDuplicateNoChange))
                {
                    return RejectedApply(
                        "collected-run-transfer-box-context-rejected:"
                        + (registered == null
                            ? "null"
                            : registered.RejectionCode));
                }
            }
            var rewardIds = new List<StableId>(plan.Rewards.Count);
            for (int index = 0; index < plan.Rewards.Count; index++)
                rewardIds.Add(plan.Rewards[index].RewardInstanceStableId);
            return new CollectedRunRewardAtomicApplyResultV1(
                CollectedRunRewardTransferAuthorityStatusV1.Applied,
                rewardIds,
                ExportAuthorityFingerprints(),
                string.Empty);
        }

        public CollectedRunRewardTransferReceiptRecordResultV1 RecordReceipt(
            CollectedRunRewardTransferReceiptV1 receipt)
        {
            return receipts.Record(receipt);
        }

        public CollectedRunRewardTransferRestoreResultV1 Restore(
            ICollectedRunRewardTransferCompensationV1 compensation)
        {
            var typed = compensation as ProductionCollectedRunRewardCompensationV2;
            if (typed == null)
            {
                return new CollectedRunRewardTransferRestoreResultV1(
                    false,
                    "collected-run-transfer-compensation-type-invalid");
            }
            var diagnostics = new List<string>();
            MoneyWalletImportResult money = graph.MoneyWallet.ImportSnapshot(typed.Money);
            if (money.Status != MoneyWalletImportStatus.Imported)
                diagnostics.Add("money:" + money.RejectionCode);
            ScrapSnapshotImportResultV1 scrap =
                graph.ScrapWallet.ImportSnapshot(typed.Scrap);
            if (!scrap.Succeeded) diagnostics.Add("scrap:" + scrap.RejectionCode);
            PlayerHoldingsImportResultV1 holdings =
                graph.LoadoutRuntime.Holdings.ImportSnapshot(typed.Holdings);
            if (!holdings.Succeeded)
                diagnostics.Add("holdings:" + holdings.RejectionCode);
            RewardApplicationImportResultV1 rap =
                rewardApplication.ImportSnapshot(typed.RewardApplication);
            if (rap.Status != RewardApplicationImportStatusV1.Imported)
                diagnostics.Add("reward-application:" + rap.RejectionCode);
            StrongboxOpeningImportResultV1 boxes =
                graph.StrongboxAuthority.ImportSnapshot(typed.Strongboxes);
            if (!boxes.Succeeded)
                diagnostics.Add("strongboxes:" + boxes.RejectionCode);
            SaveComponentApplyResultV1 receiptRestore =
                receipts.ImportSnapshot(typed.Receipts);
            if (!receiptRestore.Succeeded)
                diagnostics.Add("receipts:" + receiptRestore.RejectionCode);
            SaveComponentApplyResultV1 preparedRestore =
                prepared.ImportSnapshot(typed.Prepared);
            if (!preparedRestore.Succeeded)
                diagnostics.Add("prepared:" + preparedRestore.RejectionCode);
            return new CollectedRunRewardTransferRestoreResultV1(
                diagnostics.Count == 0,
                string.Join("|", diagnostics));
        }

        private CollectedRunRewardTransferPreflightResultV1 DryRunRap(
            CollectedRunRewardAtomicPlanV2 plan)
        {
            try
            {
                var dryMoney = new MoneyWalletService();
                MoneyWalletImportResult moneyImport =
                    dryMoney.ImportSnapshot(graph.MoneyWallet.CurrentSnapshot);
                if (moneyImport.Status != MoneyWalletImportStatus.Imported)
                    return Reject("dry-money:" + moneyImport.RejectionCode);

                var dryScrap = new ScrapWalletServiceV1(
                    graph.ScrapWallet.AuthorityStableId,
                    graph.ScrapWallet.CurrencyStableId);
                ScrapSnapshotImportResultV1 scrapImport =
                    dryScrap.ImportSnapshot(graph.ScrapWallet.ExportSnapshot());
                if (!scrapImport.Succeeded)
                    return Reject("dry-scrap:" + scrapImport.RejectionCode);

                var dryHoldings = new PlayerHoldingsService(
                    graph.LoadoutRuntime.Holdings.AuthorityStableId,
                    999L,
                    graph.LoadoutRuntime.CatalogAdapter);
                PlayerHoldingsImportResultV1 holdingsImport =
                    dryHoldings.ImportSnapshot(
                        graph.LoadoutRuntime.Holdings.ExportSnapshot());
                if (!holdingsImport.Succeeded)
                    return Reject("dry-holdings:" + holdingsImport.RejectionCode);

                var dryRap = new RewardApplicationServiceV1(
                    rewardApplication.AuthorityStableId,
                    new MoneyRewardChildAuthorityV1(dryMoney),
                    new ScrapRewardChildAuthorityV1(dryScrap),
                    new PlayerHoldingsRewardChildAuthorityV1(
                        dryHoldings,
                        graph.LoadoutRuntime.CatalogAdapter));
                RewardApplicationImportResultV1 rapImport =
                    dryRap.ImportSnapshot(rewardApplication.ExportSnapshot());
                if (rapImport.Status != RewardApplicationImportStatusV1.Imported)
                    return Reject("dry-rap-import:" + rapImport.RejectionCode);
                RewardApplicationResultV1 commit =
                    dryRap.Commit(plan.CommitCommand);
                if (!CommitAccepted(commit))
                    return Reject("dry-rap-commit:" + ResultCode(commit));
                RewardApplicationResultV1 claim =
                    dryRap.Claim(plan.ClaimCommand);
                if (!ClaimAccepted(claim))
                    return Reject("dry-rap-claim:" + ResultCode(claim));
                return CollectedRunRewardTransferPreflightResultV1.Accepted();
            }
            catch (Exception exception)
            {
                return Reject(
                    "collected-run-transfer-dry-run-threw:"
                    + exception.GetType().Name);
            }
        }

        private CollectedRunRewardTransferPreflightResultV1 PreflightStrongboxes(
            CollectedRunRewardAtomicPlanV2 plan)
        {
            StrongboxOpeningSnapshotV1 snapshot =
                graph.StrongboxAuthority.ExportSnapshot();
            var existing = new Dictionary<StableId, StrongboxInstanceContextV1>();
            for (int index = 0; index < snapshot.Contexts.Count; index++)
                existing.Add(snapshot.Contexts[index].InstanceStableId, snapshot.Contexts[index]);
            for (int index = 0; index < plan.StrongboxContexts.Count; index++)
            {
                StrongboxInstanceContextV1 context = plan.StrongboxContexts[index];
                StrongboxDefinitionV1 definition;
                if (!graph.StrongboxCatalog.TryGet(context.TierStableId, out definition))
                    return Reject("strongbox-tier-unknown:" + context.TierStableId);
                if (!string.Equals(
                    definition.Fingerprint,
                    context.AlgorithmContentFingerprint,
                    StringComparison.Ordinal))
                {
                    return Reject(
                        "strongbox-definition-fingerprint-conflict:"
                        + context.InstanceStableId);
                }
                StrongboxInstanceContextV1 prior;
                if (existing.TryGetValue(context.InstanceStableId, out prior)
                    && !string.Equals(
                        prior.Fingerprint,
                        context.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return Reject(
                        "strongbox-context-conflict:"
                        + context.InstanceStableId);
                }
            }
            return CollectedRunRewardTransferPreflightResultV1.Accepted();
        }

        private IDictionary<string, string> ExportAuthorityFingerprints()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "money", graph.MoneyWallet.CurrentSnapshot.Fingerprint },
                { "scrap", graph.ScrapWallet.ExportSnapshot().Fingerprint },
                { "holdings", graph.LoadoutRuntime.Holdings.ExportSnapshot().Fingerprint },
                { "reward-application", rewardApplication.ExportSnapshot().Fingerprint },
                { "strongboxes", graph.StrongboxAuthority.ExportSnapshot().Fingerprint },
                { "transfer-receipts", receipts.ExportSnapshot().Fingerprint },
                { "prepared-transfers", prepared.ExportSnapshot().Fingerprint },
            };
        }

        private static CollectedRunRewardTransferPreflightResultV1 Reject(
            string diagnostic)
        {
            return CollectedRunRewardTransferPreflightResultV1.Rejected(diagnostic);
        }

        private static CollectedRunRewardAtomicApplyResultV1 RejectedApply(
            string diagnostic)
        {
            return new CollectedRunRewardAtomicApplyResultV1(
                CollectedRunRewardTransferAuthorityStatusV1.Rejected,
                Array.Empty<StableId>(),
                new Dictionary<string, string>(),
                diagnostic);
        }

        private static bool CommitAccepted(RewardApplicationResultV1 result)
        {
            return result != null
                && (result.Status == RewardApplicationResultStatusV1.Generated
                    || result.Status
                        == RewardApplicationResultStatusV1.ExactDuplicateNoChange);
        }

        private static bool ClaimAccepted(RewardApplicationResultV1 result)
        {
            return result != null
                && (result.Status == RewardApplicationResultStatusV1.Applied
                    || result.Status
                        == RewardApplicationResultStatusV1.AlreadyAppliedNoChange
                    || result.Status
                        == RewardApplicationResultStatusV1.ExactDuplicateNoChange);
        }

        private static string ResultCode(RewardApplicationResultV1 result)
        {
            return result == null
                ? "null"
                : (string.IsNullOrWhiteSpace(result.RejectionCode)
                    ? result.Status.ToString()
                    : result.RejectionCode);
        }
    }
}
