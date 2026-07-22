using System;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Rewards.Application;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    /// <summary>
    /// Adds a non-mutating whole-batch RAP dry run ahead of the production adapter.
    /// The dry graph is restored from the exact live snapshots and discarded afterwards.
    /// </summary>
    public sealed class FullyPreflightedCollectedRunRewardTransferAuthorityAdapter :
        ICollectedRunRewardTransferAuthorityPortV1
    {
        private readonly ProductionCharacterRuntimeGraphV1 graph;
        private readonly RewardApplicationServiceV1 rewardApplication;
        private readonly CollectedRunRewardApplicationPlanV1 plan;
        private readonly ProductionCollectedRunRewardTransferAuthorityAdapter inner;

        public FullyPreflightedCollectedRunRewardTransferAuthorityAdapter(
            ProductionCharacterRuntimeGraphV1 graph,
            RewardApplicationServiceV1 rewardApplication,
            CollectedRunRewardApplicationPlanV1 plan,
            ProductionCollectedRunRewardTransferAuthorityAdapter inner)
        {
            this.graph = graph ?? throw new ArgumentNullException(nameof(graph));
            this.rewardApplication = rewardApplication
                ?? throw new ArgumentNullException(nameof(rewardApplication));
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public PermanentRewardTransferStateV1 ExportState()
        {
            return inner.ExportState();
        }

        public bool TryGetDurableReceipt(
            StableId transferOperationStableId,
            out CollectedRunRewardTransferReceiptV1 receipt)
        {
            return inner.TryGetDurableReceipt(
                transferOperationStableId,
                out receipt);
        }

        public bool TryGetDurableReceiptForReward(
            StableId rewardInstanceStableId,
            out CollectedRunRewardTransferReceiptV1 receipt)
        {
            return inner.TryGetDurableReceiptForReward(
                rewardInstanceStableId,
                out receipt);
        }

        public CollectedRunRewardTransferPreflightResultV1 Preflight(
            CollectedRunRewardTransferBatchV1 batch)
        {
            CollectedRunRewardTransferPreflightResultV1 structural =
                inner.Preflight(batch);
            if (structural == null || !structural.Succeeded)
                return structural
                    ?? CollectedRunRewardTransferPreflightResultV1.Rejected(
                        "collected-run-transfer-structural-preflight-null");

            try
            {
                var dryMoney = new MoneyWalletService();
                MoneyWalletImportResult moneyImport = dryMoney.ImportSnapshot(
                    graph.MoneyWallet.CurrentSnapshot);
                if (moneyImport.Status != MoneyWalletImportStatus.Imported)
                    return Reject("money", moneyImport.RejectionCode);

                var dryScrap = new ScrapWalletServiceV1(
                    graph.ScrapWallet.AuthorityStableId,
                    graph.ScrapWallet.CurrencyStableId);
                var scrapImport = dryScrap.ImportSnapshot(
                    graph.ScrapWallet.ExportSnapshot());
                if (!scrapImport.Succeeded)
                    return Reject("scrap", scrapImport.RejectionCode);

                var dryHoldings = new PlayerHoldingsService(
                    graph.LoadoutRuntime.Holdings.AuthorityStableId,
                    999L,
                    graph.LoadoutRuntime.CatalogAdapter);
                PlayerHoldingsImportResultV1 holdingsImport =
                    dryHoldings.ImportSnapshot(
                        graph.LoadoutRuntime.Holdings.ExportSnapshot());
                if (!holdingsImport.Succeeded)
                    return Reject("holdings", holdingsImport.RejectionCode);

                var dryRewardApplication = new RewardApplicationServiceV1(
                    rewardApplication.AuthorityStableId,
                    new MoneyRewardChildAuthorityV1(dryMoney),
                    new ScrapRewardChildAuthorityV1(dryScrap),
                    new PlayerHoldingsRewardChildAuthorityV1(
                        dryHoldings,
                        graph.LoadoutRuntime.CatalogAdapter));
                RewardApplicationImportResultV1 rapImport =
                    dryRewardApplication.ImportSnapshot(
                        rewardApplication.ExportSnapshot());
                if (rapImport.Status
                    != RewardApplicationImportStatusV1.Imported)
                {
                    return Reject(
                        "reward-application",
                        rapImport.RejectionCode);
                }

                RewardApplicationResultV1 commit =
                    dryRewardApplication.Commit(plan.CommitCommand);
                if (!CommitAccepted(commit))
                    return Reject("rap-commit", ResultCode(commit));

                RewardApplicationResultV1 claim =
                    dryRewardApplication.Claim(plan.ClaimCommand);
                if (!ClaimAccepted(claim))
                    return Reject("rap-claim", ResultCode(claim));

                return CollectedRunRewardTransferPreflightResultV1.Accepted();
            }
            catch (Exception exception)
            {
                return CollectedRunRewardTransferPreflightResultV1.Rejected(
                    "collected-run-transfer-dry-run-threw:"
                    + exception.GetType().Name);
            }
        }

        public string ResolveAuthorityTarget(
            CollectedRunRewardTransferItemV1 reward)
        {
            return inner.ResolveAuthorityTarget(reward);
        }

        public ICollectedRunRewardTransferCompensationV1 CaptureCompensation()
        {
            return inner.CaptureCompensation();
        }

        public CollectedRunRewardTransferChildResultV1 Apply(
            CollectedRunRewardTransferChildCommandV1 command)
        {
            return inner.Apply(command);
        }

        public CollectedRunRewardTransferReceiptRecordResultV1 RecordReceipt(
            CollectedRunRewardTransferReceiptV1 receipt)
        {
            return inner.RecordReceipt(receipt);
        }

        public CollectedRunRewardTransferRestoreResultV1 Restore(
            ICollectedRunRewardTransferCompensationV1 compensation)
        {
            return inner.Restore(compensation);
        }

        private static CollectedRunRewardTransferPreflightResultV1 Reject(
            string boundary,
            string code)
        {
            return CollectedRunRewardTransferPreflightResultV1.Rejected(
                "collected-run-transfer-dry-run-"
                + boundary
                + "-rejected:"
                + (string.IsNullOrWhiteSpace(code) ? "unspecified" : code));
        }

        private static bool CommitAccepted(RewardApplicationResultV1 result)
        {
            return result != null
                && (result.Status
                        == RewardApplicationResultStatusV1.Generated
                    || result.Status
                        == RewardApplicationResultStatusV1
                            .ExactDuplicateNoChange);
        }

        private static bool ClaimAccepted(RewardApplicationResultV1 result)
        {
            return result != null
                && (result.Status
                        == RewardApplicationResultStatusV1.Applied
                    || result.Status
                        == RewardApplicationResultStatusV1
                            .AlreadyAppliedNoChange
                    || result.Status
                        == RewardApplicationResultStatusV1
                            .ExactDuplicateNoChange);
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

    /// <summary>
    /// Exactly-once production service using the fully preflighted authority wrapper.
    /// </summary>
    public sealed class FullyPreflightedCollectedRunRewardTransferService
    {
        private readonly CollectedRunRewardApplicationPlanV1 plan;
        private readonly ProductionCollectedRunRewardTransferAuthorityAdapter inner;
        private readonly FullyPreflightedCollectedRunRewardTransferAuthorityAdapter
            preflighted;
        private readonly CollectedRunRewardTransferCoordinatorV1 coordinator;

        public FullyPreflightedCollectedRunRewardTransferService(
            CollectedRunRewardApplicationPlanV1 plan,
            ProductionCollectedRunRewardTransferAuthorityAdapter inner,
            FullyPreflightedCollectedRunRewardTransferAuthorityAdapter preflighted,
            ICollectedRunRewardTransferPersistencePortV1 persistence)
        {
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.preflighted = preflighted
                ?? throw new ArgumentNullException(nameof(preflighted));
            coordinator = new CollectedRunRewardTransferCoordinatorV1(
                preflighted,
                persistence ?? throw new ArgumentNullException(nameof(persistence)));
        }

        public CollectedRunRewardTransferResultV1 Apply()
        {
            CollectedRunRewardTransferReceiptV1 existing;
            if (inner.TryGetDurableReceipt(
                plan.Batch.TransferOperationStableId,
                out existing))
            {
                string recordedPlan;
                if (existing == null
                    || !existing.AuthorityFingerprints.TryGetValue(
                        ProductionCollectedRunRewardTransferAuthorityAdapter
                            .ApplicationPlanAuthorityKey,
                        out recordedPlan)
                    || !string.Equals(
                        recordedPlan,
                        plan.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return new CollectedRunRewardTransferResultV1(
                        CollectedRunRewardTransferStatusV1.ConflictingDuplicate,
                        plan.Batch.TransferOperationStableId,
                        plan.Batch.Fingerprint,
                        plan.Batch.RunStableId,
                        plan.Batch.SelectedCharacterStableId,
                        existing,
                        preflighted.ExportState(),
                        CollectedRunRewardTransferPersistenceResultV1
                            .NotAttempted(string.Empty),
                        "collected-run-transfer-application-plan-conflict",
                        string.Empty,
                        false);
                }
            }
            return coordinator.Apply(plan.Batch);
        }
    }
}
