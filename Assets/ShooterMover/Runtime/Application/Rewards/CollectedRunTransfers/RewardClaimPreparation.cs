using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    public sealed class RewardClaimGenerationContext
    {
        public RewardClaimGenerationContext(
            ulong rootSeed,
            int algorithmVersion,
            ProgressionContext progressionContext,
            string eventModifierFingerprint)
        {
            if (algorithmVersion < 1)
                throw new ArgumentOutOfRangeException(nameof(algorithmVersion));
            RootSeed = rootSeed;
            AlgorithmVersion = algorithmVersion;
            ProgressionContext = progressionContext
                ?? throw new ArgumentNullException(nameof(progressionContext));
            if (string.IsNullOrWhiteSpace(eventModifierFingerprint))
                throw new ArgumentException(
                    "An event/modifier fingerprint is required.",
                    nameof(eventModifierFingerprint));
            EventModifierFingerprint = eventModifierFingerprint.Trim();
            var builder = new StringBuilder(
                "schema=reward-claim-generation-context-v2");
            RewardClaimTransfer.Append(
                builder,
                "root-seed",
                RootSeed);
            RewardClaimTransfer.Append(
                builder,
                "algorithm",
                AlgorithmVersion);
            RewardClaimTransfer.Append(
                builder,
                "progression",
                ProgressionContext.Fingerprint);
            RewardClaimTransfer.Append(
                builder,
                "event-modifiers",
                EventModifierFingerprint);
            Fingerprint =
                RewardClaimTransfer.Hash(
                    builder.ToString());
        }

        public ulong RootSeed { get; }
        public int AlgorithmVersion { get; }
        public ProgressionContext ProgressionContext { get; }
        public string EventModifierFingerprint { get; }
        public string Fingerprint { get; }
    }

    public interface ICollectedRunGunPayloadSource
    {
        bool TryResolveExact(
            StableId rewardInstanceStableId,
            StableId equipmentDefinitionStableId,
            out EquipmentInstance equipment,
            out string diagnostic);
    }

    public sealed class RejectingCollectedRunEquipmentPayloadSource :
        ICollectedRunGunPayloadSource
    {
        public bool TryResolveExact(
            StableId rewardInstanceStableId,
            StableId equipmentDefinitionStableId,
            out EquipmentInstance equipment,
            out string diagnostic)
        {
            equipment = null;
            diagnostic =
                "collected-run-transfer-exact-equipment-payload-unavailable:"
                + rewardInstanceStableId;
            return false;
        }
    }

    public static class RewardClaimTransferPreparationFactory
    {
        private static readonly StableId TransferProfileStableId =
            StableId.Parse("reward-profile.collected-run-transfer");

        /// <summary>
        /// Freezes every fact available before Run End. This proves that all exact payloads
        /// and unopened BOX contexts are constructible before completion is accepted.
        /// </summary>
        public static bool TryCreateAwaitingAcceptedEnd(
            EndRunSessionCommand endCommand,
            IReadOnlyList<RunSessionCollectedReward> collectedRewards,
            CharacterLiveGraph graph,
            RewardApplicationActions rewardApplication,
            RewardClaimTransferReceiptState receipts,
            RewardClaimPreparedTransferStore preparedTransfers,
            RewardClaimGenerationContext generationContext,
            ICollectedRunGunPayloadSource equipmentPayloadSource,
            out RewardClaimPreparedTransfer awaiting,
            out string diagnostic)
        {
            awaiting = null;
            diagnostic = string.Empty;
            if (endCommand == null
                || graph == null
                || graph.IsDisposed
                || rewardApplication == null
                || receipts == null
                || preparedTransfers == null
                || generationContext == null)
            {
                diagnostic =
                    "collected-run-transfer-preparation-context-missing";
                return false;
            }
            if (endCommand.CompletionState
                != MissionRunCompletionState.Completed)
            {
                diagnostic =
                    "collected-run-transfer-requires-completed-end";
                return false;
            }
            CharacterInstanceSnapshot character = graph.Character;
            if (character == null)
            {
                diagnostic =
                    "collected-run-transfer-preparation-character-missing";
                return false;
            }

            var journal = new List<RunSessionCollectedReward>(
                collectedRewards
                ?? Array.Empty<RunSessionCollectedReward>());
            journal.Sort((left, right) =>
            {
                if (left == null || right == null)
                {
                    return ReferenceEquals(left, right)
                        ? 0
                        : (left == null ? -1 : 1);
                }
                int identity = left.GeneratedRewardChildStableId.CompareTo(
                    right.GeneratedRewardChildStableId);
                return identity != 0
                    ? identity
                    : string.CompareOrdinal(
                        left.Fingerprint,
                        right.Fingerprint);
            });

            var items = new List<RewardClaimTransferItem>(
                journal.Count);
            var equipment = new List<EquipmentInstance>();
            var boxes = new List<StrongboxInstanceContext>();
            ICollectedRunGunPayloadSource equipmentSource =
                equipmentPayloadSource
                ?? new RejectingCollectedRunEquipmentPayloadSource();

            for (int index = 0; index < journal.Count; index++)
            {
                RunSessionCollectedReward reward = journal[index];
                if (reward == null
                    || reward.RunStableId != endCommand.RunStableId
                    || reward.RunLifecycleGeneration
                        != endCommand.LifecycleGeneration)
                {
                    diagnostic =
                        "collected-run-transfer-preparation-journal-run-or-lifecycle-mismatch";
                    return false;
                }
                RewardClaimTransferItem item =
                    ToTransferItem(reward);
                items.Add(item);
                switch (item.RewardKind)
                {
                    case RewardGrantKind.Money:
                    case RewardGrantKind.Scrap:
                        break;
                    case RewardGrantKind.EquipmentReference:
                        if (item.Quantity != 1L)
                        {
                            diagnostic =
                                "collected-run-transfer-equipment-child-quantity-invalid:"
                                + item.RewardInstanceStableId;
                            return false;
                        }
                        EquipmentInstance exactEquipment;
                        if (!equipmentSource.TryResolveExact(
                                item.RewardInstanceStableId,
                                item.ContentStableId,
                                out exactEquipment,
                                out diagnostic)
                            || exactEquipment == null
                            || exactEquipment.InstanceId
                                != item.RewardInstanceStableId
                            || exactEquipment.DefinitionId
                                != item.ContentStableId)
                        {
                            if (string.IsNullOrWhiteSpace(diagnostic))
                            {
                                diagnostic =
                                    "collected-run-transfer-exact-equipment-payload-invalid:"
                                    + item.RewardInstanceStableId;
                            }
                            return false;
                        }
                        equipment.Add(exactEquipment);
                        break;
                    case RewardGrantKind.Strongbox:
                        if (item.Quantity != 1L)
                        {
                            diagnostic =
                                "collected-run-transfer-strongbox-child-quantity-invalid:"
                                + item.RewardInstanceStableId;
                            return false;
                        }
                        StrongboxDefinition definition;
                        if (!graph.StrongboxCatalog.TryGet(
                            item.ContentStableId,
                            out definition))
                        {
                            diagnostic =
                                "collected-run-transfer-strongbox-tier-unknown:"
                                + item.ContentStableId;
                            return false;
                        }
                        boxes.Add(StrongboxInstanceContext.Create(
                            item.RewardInstanceStableId,
                            item.ContentStableId,
                            DeriveStrongboxSeed(
                                generationContext,
                                item),
                            generationContext.AlgorithmVersion,
                            generationContext.ProgressionContext,
                            item.DropOperationStableId,
                            item.CollectionOperationStableId,
                            definition.Fingerprint));
                        break;
                    default:
                        diagnostic =
                            "collected-run-transfer-reward-kind-unsupported:"
                            + item.RewardKind;
                        return false;
                }
            }

            string journalFingerprint = FingerprintItems(items);
            StableId custodyStableId =
                RewardClaimTransfer.DeriveStableId(
                    "prepared-transfer",
                    "collected-run",
                    endCommand.RunStableId
                    + "|"
                    + endCommand.LifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture)
                    + "|"
                    + character.CharacterInstanceStableId
                    + "|"
                    + journalFingerprint);
            StableId preparationOperationStableId =
                RewardClaimTransfer.DeriveStableId(
                    "operation",
                    "collected-run-prepare",
                    custodyStableId + "|" + endCommand.Fingerprint);

            var authorityFingerprints =
                new Dictionary<string, string>(StringComparer.Ordinal)
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
            };
            awaiting =
                RewardClaimPreparedTransfer.AwaitingAcceptedEnd(
                    custodyStableId,
                    preparationOperationStableId,
                    endCommand.RunStableId,
                    endCommand.LifecycleGeneration,
                    character.CharacterInstanceStableId,
                    character.Revision,
                    character.Fingerprint,
                    endCommand.OperationStableId,
                    endCommand.Fingerprint,
                    generationContext.RootSeed,
                    generationContext.AlgorithmVersion,
                    generationContext.ProgressionContext,
                    generationContext.EventModifierFingerprint,
                    graph.MoneyWallet.Sequence,
                    graph.ScrapWallet.Sequence,
                    graph.LoadoutRuntime.Holdings.Sequence,
                    authorityFingerprints,
                    items,
                    equipment,
                    boxes);
            return true;
        }

        /// <summary>
        /// Promotes pre-End custody with the accepted End receipt, then builds the exact
        /// RAP/BOX batch without mutable content or run-local payload lookup.
        /// </summary>
        public static bool TryAcceptEndAndBuildPlan(
            RunSessionEndResult acceptedEnd,
            RewardClaimPreparedTransfer awaiting,
            CharacterLiveGraph graph,
            RewardApplicationActions rewardApplication,
            out RewardClaimPreparedTransfer prepared,
            out RewardClaimAtomicPlan plan,
            out string diagnostic)
        {
            prepared = null;
            plan = null;
            diagnostic = string.Empty;
            if (acceptedEnd == null
                || !acceptedEnd.Succeeded
                || acceptedEnd.Receipt == null
                || acceptedEnd.Command == null
                || awaiting == null
                || graph == null
                || rewardApplication == null
                || awaiting.State
                    != RewardClaimPreparedTransferState
                        .AwaitingAcceptedEnd)
            {
                diagnostic =
                    "collected-run-transfer-end-receipt-not-accepted";
                return false;
            }
            if (acceptedEnd.Receipt.MissionResult == null
                || acceptedEnd.Receipt.MissionResult.CompletionState
                    != MissionRunCompletionState.Completed
                || acceptedEnd.Receipt.RunStableId != awaiting.RunStableId
                || acceptedEnd.Command.LifecycleGeneration
                    != awaiting.LifecycleGeneration
                || acceptedEnd.Command.OperationStableId
                    != awaiting.EndOperationStableId
                || !string.Equals(
                    acceptedEnd.Command.Fingerprint,
                    awaiting.EndCommandFingerprint,
                    StringComparison.Ordinal))
            {
                diagnostic =
                    "collected-run-transfer-accepted-end-conflict";
                return false;
            }

            StableId transferOperationStableId =
                RewardClaimTransfer.DeriveStableId(
                    "operation",
                    "collected-run-transfer",
                    acceptedEnd.Receipt.Fingerprint);
            StableId missionResultStableId =
                RewardClaimTransfer.DeriveStableId(
                    "mission-result",
                    "accepted",
                    acceptedEnd.Receipt.MissionResult.Fingerprint);
            string batchFingerprint =
                RewardClaimAtomicPlan.ComputeBatchFingerprint(
                    transferOperationStableId,
                    awaiting.RunStableId,
                    awaiting.LifecycleGeneration,
                    missionResultStableId,
                    acceptedEnd.Receipt.MissionResult.Fingerprint,
                    awaiting.SelectedCharacterStableId,
                    awaiting.ExpectedCharacterRevision,
                    awaiting.ExpectedCharacterFingerprint,
                    awaiting.Rewards);

            RewardCommitCommand commit;
            RewardClaimCommand claim;
            List<RewardGrantApplicationPayload> payloads;
            if (!TryBuildRapCommands(
                awaiting,
                transferOperationStableId,
                batchFingerprint,
                graph,
                out commit,
                out claim,
                out payloads,
                out diagnostic))
            {
                return false;
            }
            string planFingerprint =
                RewardClaimAtomicPlan.ComputeFingerprint(
                    batchFingerprint,
                    commit,
                    claim,
                    payloads,
                    awaiting.Strongboxes);
            prepared = awaiting.AcceptEnd(
                transferOperationStableId,
                missionResultStableId,
                acceptedEnd.Receipt.MissionResult.Fingerprint,
                batchFingerprint,
                planFingerprint);
            try
            {
                plan = new RewardClaimAtomicPlan(
                    prepared,
                    commit,
                    claim,
                    payloads,
                    awaiting.Strongboxes);
                return true;
            }
            catch (Exception exception)
            {
                diagnostic =
                    "collected-run-transfer-atomic-plan-invalid:"
                    + exception.GetType().Name;
                prepared = null;
                plan = null;
                return false;
            }
        }

        public static bool TryBuildPlanFromPrepared(
            RewardClaimPreparedTransfer prepared,
            CharacterLiveGraph graph,
            RewardApplicationActions rewardApplication,
            out RewardClaimAtomicPlan plan,
            out string diagnostic)
        {
            plan = null;
            diagnostic = string.Empty;
            if (prepared == null
                || prepared.State
                    == RewardClaimPreparedTransferState
                        .AwaitingAcceptedEnd
                || graph == null
                || graph.IsDisposed
                || rewardApplication == null
                || graph.Character.CharacterInstanceStableId
                    != prepared.SelectedCharacterStableId)
            {
                diagnostic =
                    "collected-run-transfer-recovery-context-invalid";
                return false;
            }
            RewardCommitCommand commit;
            RewardClaimCommand claim;
            List<RewardGrantApplicationPayload> payloads;
            if (!TryBuildRapCommands(
                prepared,
                prepared.TransferOperationStableId,
                prepared.BatchFingerprint,
                graph,
                out commit,
                out claim,
                out payloads,
                out diagnostic))
            {
                return false;
            }
            try
            {
                plan = new RewardClaimAtomicPlan(
                    prepared,
                    commit,
                    claim,
                    payloads,
                    prepared.Strongboxes);
                return true;
            }
            catch (Exception exception)
            {
                diagnostic =
                    "collected-run-transfer-recovery-plan-invalid:"
                    + exception.GetType().Name;
                return false;
            }
        }

        private static bool TryBuildRapCommands(
            RewardClaimPreparedTransfer prepared,
            StableId transferOperationStableId,
            string batchFingerprint,
            CharacterLiveGraph graph,
            out RewardCommitCommand commit,
            out RewardClaimCommand claim,
            out List<RewardGrantApplicationPayload> payloads,
            out string diagnostic)
        {
            commit = null;
            claim = null;
            payloads = new List<RewardGrantApplicationPayload>();
            diagnostic = string.Empty;
            if (prepared == null
                || transferOperationStableId == null
                || string.IsNullOrWhiteSpace(batchFingerprint)
                || graph == null)
            {
                diagnostic =
                    "collected-run-transfer-rap-plan-context-invalid";
                return false;
            }

            var equipmentById = prepared.Equipment.ToDictionary(
                item => item.InstanceId,
                item => item);
            var grants = new List<RewardGrant>(
                prepared.Rewards.Count);
            for (int index = 0; index < prepared.Rewards.Count; index++)
            {
                RewardClaimTransferItem item =
                    prepared.Rewards[index];
                var grant = RewardGrant.Create(
                    item.RewardInstanceStableId,
                    item.RewardKind,
                    item.ContentStableId,
                    item.Quantity);
                grants.Add(grant);
                switch (item.RewardKind)
                {
                    case RewardGrantKind.Money:
                    case RewardGrantKind.Scrap:
                        payloads.Add(
                            RewardGrantApplicationPayload.ForValue(
                                grant));
                        break;
                    case RewardGrantKind.Strongbox:
                        payloads.Add(
                            RewardGrantApplicationPayload.ForStrongboxes(
                                grant,
                                new[] { item.RewardInstanceStableId }));
                        break;
                    case RewardGrantKind.EquipmentReference:
                        EquipmentInstance equipment;
                        if (!equipmentById.TryGetValue(
                            item.RewardInstanceStableId,
                            out equipment)
                            || equipment.DefinitionId
                                != item.ContentStableId)
                        {
                            diagnostic =
                                "collected-run-transfer-recovery-equipment-payload-missing:"
                                + item.RewardInstanceStableId;
                            return false;
                        }
                        payloads.Add(
                            RewardGrantApplicationPayload.ForEquipment(
                                grant,
                                new[] { equipment }));
                        break;
                    default:
                        diagnostic =
                            "collected-run-transfer-recovery-reward-kind-unsupported:"
                            + item.RewardKind;
                        return false;
                }
            }

            StableId commitmentStableId =
                RewardClaimTransfer.DeriveStableId(
                    "commitment",
                    "collected-run-transfer",
                    batchFingerprint);
            RewardResult generatedReward = grants.Count == 0
                ? RewardResult.CreateExplicitNoDrop(
                    commitmentStableId,
                    transferOperationStableId)
                : RewardResult.CreateGrants(
                    commitmentStableId,
                    transferOperationStableId,
                    grants);
            RewardOperationRequest operation =
                RewardOperationRequest.Create(
                    prepared.RunStableId,
                    prepared.RunStableId,
                    transferOperationStableId,
                    commitmentStableId,
                    TransferProfileStableId,
                    batchFingerprint);
            commit = RewardCommitCommand.Create(
                operation,
                generatedReward,
                GenerationFingerprint(prepared, batchFingerprint),
                payloads);
            claim = RewardClaimCommand.Create(
                RewardClaimTransfer.DeriveStableId(
                    "claim",
                    "collected-run-transfer",
                    batchFingerprint),
                commitmentStableId,
                prepared.SelectedCharacterStableId,
                MoneyWalletIds.AuthorityStableId,
                graph.ScrapWallet.AuthorityStableId,
                graph.LoadoutRuntime.Holdings.AuthorityStableId,
                prepared.ExpectedMoneySequence,
                prepared.ExpectedScrapSequence,
                prepared.ExpectedHoldingsSequence);
            return true;
        }

        private static RewardClaimTransferItem ToTransferItem(
            RunSessionCollectedReward reward)
        {
            return new RewardClaimTransferItem(
                reward.GeneratedRewardChildStableId,
                reward.RewardKind,
                reward.ContentStableId,
                reward.Quantity,
                reward.PickupStableId,
                reward.SourceGrantStableId,
                reward.DropOperationStableId,
                reward.TerminalEventStableId,
                reward.TriggeringEventStableId,
                reward.RunStableId,
                reward.RunLifecycleGeneration,
                reward.SourceEntityStableId,
                reward.SourcePlacementStableId,
                reward.SourceLifecycleGeneration,
                reward.SourceDefinitionStableId,
                reward.AttributedParticipantStableId,
                reward.GeneratedBatchFingerprint,
                reward.GeneratedRewardFingerprint,
                reward.RoomStableId,
                reward.WorldPositionX,
                reward.WorldPositionY,
                reward.WorldSpawnFingerprint,
                reward.AvailablePickupFingerprint,
                reward.CollectorEntityStableId,
                reward.CollectorParticipantStableId,
                reward.CollectionOperationStableId,
                reward.CollectionOrder,
                reward.CollectedAtAuthoritativeTick,
                reward.Fingerprint);
        }

        private static ulong DeriveStrongboxSeed(
            RewardClaimGenerationContext context,
            RewardClaimTransferItem item)
        {
            string fingerprint =
                RewardClaimTransfer.Hash(
                    context.Fingerprint
                    + "|"
                    + item.GeneratedRewardFingerprint
                    + "|"
                    + item.RewardInstanceStableId);
            return ulong.Parse(
                fingerprint.Substring("sha256:".Length, 16),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
        }

        private static string FingerprintItems(
            IReadOnlyList<RewardClaimTransferItem> items)
        {
            var builder = new StringBuilder(
                "schema=reward-claim-custody-items-v2");
            for (int index = 0; index < items.Count; index++)
                RewardClaimTransfer.Append(
                    builder,
                    "reward:"
                        + index.ToString(CultureInfo.InvariantCulture),
                    items[index].Fingerprint);
            return RewardClaimTransfer.Hash(
                builder.ToString());
        }

        private static string GenerationFingerprint(
            RewardClaimPreparedTransfer prepared,
            string batchFingerprint)
        {
            var builder = new StringBuilder(
                "schema=collected-run-transfer-generation-proof-v2");
            RewardClaimTransfer.Append(
                builder,
                "batch",
                batchFingerprint);
            RewardClaimTransfer.Append(
                builder,
                "seed",
                prepared.GenerationRootSeed);
            RewardClaimTransfer.Append(
                builder,
                "algorithm",
                prepared.GenerationAlgorithmVersion);
            RewardClaimTransfer.Append(
                builder,
                "progression",
                prepared.ProgressionContext.Fingerprint);
            RewardClaimTransfer.Append(
                builder,
                "event-modifiers",
                prepared.EventModifierFingerprint);
            for (int index = 0; index < prepared.Rewards.Count; index++)
                RewardClaimTransfer.Append(
                    builder,
                    "generated-reward:"
                        + index.ToString(CultureInfo.InvariantCulture),
                    prepared.Rewards[index].GeneratedRewardFingerprint);
            return RewardClaimTransfer.Hash(
                builder.ToString());
        }
    }
}
