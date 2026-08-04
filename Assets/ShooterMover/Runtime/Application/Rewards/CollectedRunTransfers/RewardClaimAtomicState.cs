using System;
using System.Collections.Generic;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Strongboxes;
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
    internal sealed class RewardClaimCompensation :
        IRewardClaimTransferCompensation
    {
        public RewardClaimCompensation(
            MoneyWalletSnapshot money,
            ScrapSnapshot scrap,
            PlayerHoldingsSnapshot holdings,
            RewardApplicationSnapshot rewardApplication,
            StrongboxOpeningSnapshot strongboxes,
            RewardClaimTransferReceiptSnapshot receipts,
            RewardClaimPreparedTransferSnapshot prepared)
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
            Fingerprint = RewardClaimTransfer.Hash(
                Money.Fingerprint
                + "|" + Scrap.Fingerprint
                + "|" + Holdings.Fingerprint
                + "|" + RewardApplication.Fingerprint
                + "|" + Strongboxes.Fingerprint
                + "|" + Receipts.Fingerprint
                + "|" + Prepared.Fingerprint);
        }

        public MoneyWalletSnapshot Money { get; }
        public ScrapSnapshot Scrap { get; }
        public PlayerHoldingsSnapshot Holdings { get; }
        public RewardApplicationSnapshot RewardApplication { get; }
        public StrongboxOpeningSnapshot Strongboxes { get; }
        public RewardClaimTransferReceiptSnapshot Receipts { get; }
        public RewardClaimPreparedTransferSnapshot Prepared { get; }
        public string Fingerprint { get; }
    }

    /// <summary>
    /// Concrete one-call authority over the selected character's existing RAP, wallet,
    /// scrap, holdings, BOX, receipt and prepared-custody authorities.
    /// </summary>
    public sealed class RewardClaimAtomicState :
        IRewardClaimAtomicBatchStatePort
    {
        private const string PreparedTransfersAuthorityKey =
            "prepared-transfers";

        private readonly CharacterLiveGraph graph;
        private readonly RewardApplicationActions rewardApplication;
        private readonly RewardClaimPreparedTransferStore prepared;
        private readonly RewardClaimTransferReceiptState receipts;

        public RewardClaimAtomicState(
            CharacterLiveGraph graph,
            RewardApplicationActions rewardApplication,
            RewardClaimPreparedTransferStore prepared,
            RewardClaimTransferReceiptState receipts)
        {
            this.graph = graph ?? throw new ArgumentNullException(nameof(graph));
            this.rewardApplication = rewardApplication
                ?? throw new ArgumentNullException(nameof(rewardApplication));
            this.prepared = prepared ?? throw new ArgumentNullException(nameof(prepared));
            this.receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
        }

        public PermanentRewardTransferState ExportState()
        {
            CharacterInstanceSnapshot character = graph.Character;
            return new PermanentRewardTransferState(
                character.CharacterInstanceStableId,
                character.Revision,
                character.Fingerprint,
                0L,
                RewardClaimTransfer.Hash(
                    "runtime-account-unavailable|" + character.Fingerprint),
                ExportAuthorityFingerprints());
        }

        public bool TryGetDurableReceipt(
            StableId transferOperationStableId,
            out RewardClaimTransferReceipt receipt)
        {
            return receipts.TryGetByOperation(
                transferOperationStableId,
                out receipt);
        }

        public bool TryGetDurableReceiptForReward(
            StableId rewardInstanceStableId,
            out RewardClaimTransferReceipt receipt)
        {
            return receipts.TryGetByReward(rewardInstanceStableId, out receipt);
        }

        public RewardClaimTransferPreflightResult Preflight(
            RewardClaimAtomicPlan plan)
        {
            if (plan == null)
                return Reject("collected-run-transfer-plan-null");

            RewardClaimPreparedTransfer custody = plan.PreparedTransfer;
            if (graph.IsDisposed
                || graph.Character.CharacterInstanceStableId
                    != custody.SelectedCharacterStableId)
            {
                return Reject(
                    "collected-run-transfer-selected-character-mismatch");
            }
            if (custody.State
                != RewardClaimPreparedTransferState.Prepared)
            {
                return Reject("collected-run-transfer-custody-not-prepared");
            }

            // The prepared-transfer authority necessarily changes from the pre-End frozen
            // snapshot when Awaiting/Prepared custody is recorded. Comparing that old aggregate
            // fingerprint here would make every valid first application and restart recovery
            // reject itself. The exact current custody row is verified separately below.
            IDictionary<string, string> current = ExportAuthorityFingerprints();
            foreach (KeyValuePair<string, string> pair in
                custody.FrozenAuthorityFingerprints)
            {
                if (string.Equals(
                    pair.Key,
                    PreparedTransfersAuthorityKey,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                string value;
                if (!current.TryGetValue(pair.Key, out value)
                    || !string.Equals(
                        value,
                        pair.Value,
                        StringComparison.Ordinal))
                {
                    return Reject(
                        "collected-run-transfer-frozen-authority-mismatch:"
                        + pair.Key);
                }
            }

            RewardClaimPreparedTransfer exactCustody;
            if (!prepared.TryGetByCustody(
                    custody.CustodyStableId,
                    out exactCustody)
                || exactCustody == null
                || !string.Equals(
                    exactCustody.Fingerprint,
                    custody.Fingerprint,
                    StringComparison.Ordinal))
            {
                return Reject(
                    "collected-run-transfer-prepared-custody-mismatch:"
                    + custody.CustodyStableId);
            }

            if (graph.MoneyWallet.Sequence != custody.ExpectedMoneySequence
                || graph.ScrapWallet.Sequence != custody.ExpectedScrapSequence
                || graph.LoadoutRuntime.Holdings.Sequence
                    != custody.ExpectedHoldingsSequence)
            {
                return Reject(
                    "collected-run-transfer-frozen-sequence-mismatch");
            }

            RewardClaimTransferPreflightResult boxPreflight =
                PreflightStrongboxes(plan);
            if (!boxPreflight.Succeeded) return boxPreflight;
            return DryRunRap(plan);
        }

        public IRewardClaimTransferCompensation CaptureCompensation()
        {
            return new RewardClaimCompensation(
                graph.MoneyWallet.CurrentSnapshot,
                graph.ScrapWallet.ExportSnapshot(),
                graph.LoadoutRuntime.Holdings.ExportSnapshot(),
                rewardApplication.ExportSnapshot(),
                graph.StrongboxAuthority.ExportSnapshot(),
                receipts.ExportSnapshot(),
                prepared.ExportSnapshot());
        }

        public RewardClaimAtomicApplyResult ApplyAtomicBatch(
            RewardClaimAtomicPlan plan)
        {
            if (plan == null)
                return RejectedApply(
                    "collected-run-transfer-atomic-plan-null");

            RewardApplicationResult committed =
                rewardApplication.Commit(plan.CommitCommand);
            if (!CommitAccepted(committed))
            {
                return RejectedApply(
                    "collected-run-transfer-rap-commit-rejected:"
                    + ResultCode(committed));
            }

            RewardApplicationResult claimed =
                rewardApplication.Claim(plan.ClaimCommand);
            if (!ClaimAccepted(claimed))
            {
                return RejectedApply(
                    "collected-run-transfer-rap-claim-rejected:"
                    + ResultCode(claimed));
            }

            for (int index = 0;
                index < plan.StrongboxContexts.Count;
                index++)
            {
                StrongboxRegistrationResult registered =
                    graph.StrongboxAuthority.RegisterInstance(
                        plan.StrongboxContexts[index]);
                if (registered == null
                    || (registered.Status
                            != StrongboxRegistrationStatus.Registered
                        && registered.Status
                            != StrongboxRegistrationStatus
                                .ExactDuplicateNoChange))
                {
                    return RejectedApply(
                        "collected-run-transfer-box-context-rejected:"
                        + (registered == null
                            ? "null"
                            : registered.RejectionCode));
                }
            }

            for (int index = 0;
                index < plan.StrongboxContexts.Count;
                index++)
            {
                StrongboxInstanceContext context =
                    plan.StrongboxContexts[index];
                StrongboxGeneratedOutcome preparedOutcome;
                string preparationRejection;
                if (!graph.StrongboxAuthority.TryPrepare(
                        StrongboxOpenCommand.CreateForCollectedRun(
                            plan.RunStableId,
                            context.InstanceStableId,
                            plan.SelectedCharacterStableId,
                            MoneyWalletIds.AuthorityStableId,
                            graph.ScrapWallet.AuthorityStableId,
                            graph.LoadoutRuntime.Holdings
                                .AuthorityStableId),
                        out preparedOutcome,
                        out preparationRejection)
                    || preparedOutcome == null)
                {
                    return RejectedApply(
                        "collected-run-transfer-box-preparation-rejected:"
                        + context.InstanceStableId
                        + ":"
                        + (string.IsNullOrWhiteSpace(
                                preparationRejection)
                            ? "outcome-missing"
                            : preparationRejection));
                }
            }

            var rewardIds = new List<StableId>(plan.Rewards.Count);
            for (int index = 0; index < plan.Rewards.Count; index++)
            {
                rewardIds.Add(
                    plan.Rewards[index].RewardInstanceStableId);
            }
            return new RewardClaimAtomicApplyResult(
                RewardClaimTransferStateStatus.Applied,
                rewardIds,
                ExportAuthorityFingerprints(),
                string.Empty);
        }

        public RewardClaimTransferReceiptRecordResult RecordReceipt(
            RewardClaimTransferReceipt receipt)
        {
            return receipts.Record(receipt);
        }

        public RewardClaimTransferRestoreResult Restore(
            IRewardClaimTransferCompensation compensation)
        {
            var typed =
                compensation as RewardClaimCompensation;
            if (typed == null)
            {
                return new RewardClaimTransferRestoreResult(
                    false,
                    "collected-run-transfer-compensation-type-invalid");
            }

            var diagnostics = new List<string>();
            MoneyWalletImportResult money =
                graph.MoneyWallet.ImportSnapshot(typed.Money);
            if (money.Status != MoneyWalletImportStatus.Imported)
                diagnostics.Add("money:" + money.RejectionCode);

            ScrapSnapshotImportResult scrap =
                graph.ScrapWallet.ImportSnapshot(typed.Scrap);
            if (!scrap.Succeeded)
                diagnostics.Add("scrap:" + scrap.RejectionCode);

            PlayerHoldingsImportResult holdings =
                graph.LoadoutRuntime.Holdings.ImportSnapshot(typed.Holdings);
            if (!holdings.Succeeded)
                diagnostics.Add("holdings:" + holdings.RejectionCode);

            RewardApplicationImportResult rap =
                rewardApplication.ImportSnapshot(typed.RewardApplication);
            if (rap.Status != RewardApplicationImportStatus.Imported)
            {
                diagnostics.Add(
                    "reward-application:" + rap.RejectionCode);
            }

            StrongboxOpeningImportResult boxes =
                graph.StrongboxAuthority.ImportSnapshot(typed.Strongboxes);
            if (!boxes.Succeeded)
                diagnostics.Add("strongboxes:" + boxes.RejectionCode);

            SavePartApplyResult receiptRestore =
                receipts.ImportSnapshot(typed.Receipts);
            if (!receiptRestore.Succeeded)
            {
                diagnostics.Add(
                    "receipts:" + receiptRestore.RejectionCode);
            }

            SavePartApplyResult preparedRestore =
                prepared.ImportSnapshot(typed.Prepared);
            if (!preparedRestore.Succeeded)
            {
                diagnostics.Add(
                    "prepared:" + preparedRestore.RejectionCode);
            }

            return new RewardClaimTransferRestoreResult(
                diagnostics.Count == 0,
                string.Join("|", diagnostics));
        }

        private RewardClaimTransferPreflightResult DryRunRap(
            RewardClaimAtomicPlan plan)
        {
            try
            {
                var dryMoney = new MoneyWalletActions();
                MoneyWalletImportResult moneyImport =
                    dryMoney.ImportSnapshot(
                        graph.MoneyWallet.CurrentSnapshot);
                if (moneyImport.Status != MoneyWalletImportStatus.Imported)
                {
                    return Reject(
                        "dry-money:" + moneyImport.RejectionCode);
                }

                var dryScrap = new ScrapWalletActions(
                    graph.ScrapWallet.AuthorityStableId,
                    graph.ScrapWallet.CurrencyStableId);
                ScrapSnapshotImportResult scrapImport =
                    dryScrap.ImportSnapshot(
                        graph.ScrapWallet.ExportSnapshot());
                if (!scrapImport.Succeeded)
                {
                    return Reject(
                        "dry-scrap:" + scrapImport.RejectionCode);
                }

                var dryHoldings = new PlayerHoldingsActions(
                    graph.LoadoutRuntime.Holdings.AuthorityStableId,
                    999L,
                    graph.LoadoutRuntime.CatalogBridge);
                PlayerHoldingsImportResult holdingsImport =
                    dryHoldings.ImportSnapshot(
                        graph.LoadoutRuntime.Holdings.ExportSnapshot());
                if (!holdingsImport.Succeeded)
                {
                    return Reject(
                        "dry-holdings:" + holdingsImport.RejectionCode);
                }

                var dryRap = new RewardApplicationActions(
                    rewardApplication.AuthorityStableId,
                    new MoneyRewardChildState(dryMoney),
                    new ScrapRewardChildState(dryScrap),
                    new PlayerHoldingsRewardChildState(
                        dryHoldings,
                        graph.LoadoutRuntime.CatalogBridge));
                RewardApplicationImportResult rapImport =
                    dryRap.ImportSnapshot(
                        rewardApplication.ExportSnapshot());
                if (rapImport.Status
                    != RewardApplicationImportStatus.Imported)
                {
                    return Reject(
                        "dry-rap-import:" + rapImport.RejectionCode);
                }

                RewardApplicationResult commit =
                    dryRap.Commit(plan.CommitCommand);
                if (!CommitAccepted(commit))
                {
                    return Reject(
                        "dry-rap-commit:" + ResultCode(commit));
                }

                RewardApplicationResult claim =
                    dryRap.Claim(plan.ClaimCommand);
                if (!ClaimAccepted(claim))
                {
                    return Reject(
                        "dry-rap-claim:" + ResultCode(claim));
                }

                return RewardClaimTransferPreflightResult.Accepted();
            }
            catch (Exception exception)
            {
                return Reject(
                    "collected-run-transfer-dry-run-threw:"
                    + exception.GetType().Name);
            }
        }

        private RewardClaimTransferPreflightResult
            PreflightStrongboxes(RewardClaimAtomicPlan plan)
        {
            StrongboxOpeningSnapshot snapshot =
                graph.StrongboxAuthority.ExportSnapshot();
            var existing =
                new Dictionary<StableId, StrongboxInstanceContext>();
            for (int index = 0; index < snapshot.Contexts.Count; index++)
            {
                existing.Add(
                    snapshot.Contexts[index].InstanceStableId,
                    snapshot.Contexts[index]);
            }

            for (int index = 0;
                index < plan.StrongboxContexts.Count;
                index++)
            {
                StrongboxInstanceContext context =
                    plan.StrongboxContexts[index];
                StrongboxDefinition definition;
                if (!graph.StrongboxCatalog.TryGet(
                    context.TierStableId,
                    out definition))
                {
                    return Reject(
                        "strongbox-tier-unknown:"
                        + context.TierStableId);
                }
                if (!string.Equals(
                    definition.Fingerprint,
                    context.AlgorithmContentFingerprint,
                    StringComparison.Ordinal))
                {
                    return Reject(
                        "strongbox-definition-fingerprint-conflict:"
                        + context.InstanceStableId);
                }

                StrongboxInstanceContext prior;
                if (existing.TryGetValue(
                        context.InstanceStableId,
                        out prior)
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
            return RewardClaimTransferPreflightResult.Accepted();
        }

        private IDictionary<string, string> ExportAuthorityFingerprints()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "money", graph.MoneyWallet.CurrentSnapshot.Fingerprint },
                { "scrap", graph.ScrapWallet.ExportSnapshot().Fingerprint },
                {
                    "holdings",
                    graph.LoadoutRuntime.Holdings.ExportSnapshot().Fingerprint
                },
                {
                    "reward-application",
                    rewardApplication.ExportSnapshot().Fingerprint
                },
                {
                    "strongboxes",
                    graph.StrongboxAuthority.ExportSnapshot().Fingerprint
                },
                {
                    "transfer-receipts",
                    receipts.ExportSnapshot().Fingerprint
                },
                {
                    PreparedTransfersAuthorityKey,
                    prepared.ExportSnapshot().Fingerprint
                },
            };
        }

        private static RewardClaimTransferPreflightResult Reject(
            string diagnostic)
        {
            return RewardClaimTransferPreflightResult.Rejected(
                diagnostic);
        }

        private static RewardClaimAtomicApplyResult RejectedApply(
            string diagnostic)
        {
            return new RewardClaimAtomicApplyResult(
                RewardClaimTransferStateStatus.Rejected,
                Array.Empty<StableId>(),
                new Dictionary<string, string>(),
                diagnostic);
        }

        private static bool CommitAccepted(
            RewardApplicationResult result)
        {
            return result != null
                && (result.Status
                        == RewardApplicationResultStatus.Generated
                    || result.Status
                        == RewardApplicationResultStatus
                            .ExactDuplicateNoChange);
        }

        private static bool ClaimAccepted(
            RewardApplicationResult result)
        {
            return result != null
                && (result.Status
                        == RewardApplicationResultStatus.Applied
                    || result.Status
                        == RewardApplicationResultStatus
                            .AlreadyAppliedNoChange
                    || result.Status
                        == RewardApplicationResultStatus
                            .ExactDuplicateNoChange);
        }

        private static string ResultCode(
            RewardApplicationResult result)
        {
            return result == null
                ? "null"
                : (string.IsNullOrWhiteSpace(result.RejectionCode)
                    ? result.Status.ToString()
                    : result.RejectionCode);
        }
    }
}
