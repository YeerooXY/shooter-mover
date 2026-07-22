using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    public sealed class CollectedRunRewardGenerationContextV2
    {
        public CollectedRunRewardGenerationContextV2(
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
                "schema=collected-run-reward-generation-context-v2");
            CollectedRunRewardTransferCanonicalV1.Append(builder, "root-seed", RootSeed);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "algorithm", AlgorithmVersion);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "progression", ProgressionContext.Fingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "event-modifiers", EventModifierFingerprint);
            Fingerprint = CollectedRunRewardTransferCanonicalV1.Hash(builder.ToString());
        }

        public ulong RootSeed { get; }
        public int AlgorithmVersion { get; }
        public ProgressionContext ProgressionContext { get; }
        public string EventModifierFingerprint { get; }
        public string Fingerprint { get; }
    }

    public interface ICollectedRunEquipmentPayloadSourceV2
    {
        bool TryResolveExact(
            StableId rewardInstanceStableId,
            StableId equipmentDefinitionStableId,
            out EquipmentInstance equipment,
            out string diagnostic);
    }

    public sealed class RejectingCollectedRunEquipmentPayloadSourceV2 :
        ICollectedRunEquipmentPayloadSourceV2
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

    public static class CollectedRunRewardTransferPreparationFactoryV2
    {
        private static readonly StableId TransferProfileStableId =
            StableId.Parse("reward-profile.collected-run-transfer");

        /// <summary>
        /// Freezes every fact that can be known before Run End. This is the pre-End
        /// construction proof and crash-recovery custody; it performs no permanent grant.
        /// </summary>
        public static bool TryCreateAwaitingAcceptedEnd(
            EndRunSessionCommandV1 endCommand,
            IReadOnlyList<RunSessionCollectedRewardV1> collectedRewards,
            ProductionCharacterRuntimeGraphV1 graph,
            RewardApplicationServiceV1 rewardApplication,
            CollectedRunRewardTransferReceiptAuthorityV1 receipts,
            CollectedRunRewardPreparedTransferAuthorityV1 preparedTransfers,
            CollectedRunRewardGenerationContextV2 generationContext,
            ICollectedRunEquipmentPayloadSourceV2 equipmentPayloadSource,
            out CollectedRunRewardPreparedTransferV1 awaiting,
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
                diagnostic = "collected-run-transfer-preparation-context-missing";
                return false;
            }
            CharacterInstanceSnapshotV1 character = graph.Character;
            if (character == null
                || character.CharacterInstanceStableId
                    != endCommand.RunStableId && false)
            {
                diagnostic = "collected-run-transfer-preparation-character-missing";
                return false;
            }

            var journal = new List<RunSessionCollectedRewardV1>(
                collectedRewards ?? Array.Empty<RunSessionCollectedRewardV1>());
            journal.Sort((left, right) =>
            {
                if (left == null || right == null)
                    return ReferenceEquals(left, right) ? 0 : (left == null ? -1 : 1);
                int identity = left.GeneratedRewardChildStableId.CompareTo(
                    right.GeneratedRewardChildStableId);
                return identity != 0
                    ? identity
                    : string.CompareOrdinal(left.Fingerprint, right.Fingerprint);
            });

            var items = new List<CollectedRunRewardTransferItemV1>(journal.Count);
            var equipment = new List<EquipmentInstance>();
            var boxes = new List<StrongboxInstanceContextV1>();
            ICollectedRunEquipmentPayloadSourceV2 equipmentSource =
                equipmentPayloadSource
                ?? new RejectingCollectedRunEquipmentPayloadSourceV2();

            for (int index = 0; index < journal.Count; index++)
            {
                RunSessionCollectedRewardV1 reward = journal[index];
                if (reward == null
                    || reward.RunStableId != endCommand.RunStableId
                    || reward.RunLifecycleGeneration
                        != endCommand.LifecycleGeneration)
                {
                    diagnostic =
                        "collected-run-transfer-preparation-journal-run-or-lifecycle-mismatch";
                    return false;
                }
                CollectedRunRewardTransferItemV1 item = ToTransferItem(reward);
                items.Add(item);
                switch (item.RewardKind)
                {
                    case RewardGrantKindV1.Money:
                    case RewardGrantKindV1.Scrap:
                        break;
                    case RewardGrantKindV1.EquipmentReference:
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
                            || exactEquipment.InstanceId != item.RewardInstanceStableId
                            || exactEquipment.DefinitionId != item.ContentStableId)
                        {
                            if (string.IsNullOrWhiteSpace(diagnostic))
                                diagnostic =
                                    "collected-run-transfer-exact-equipment-payload-invalid:"
                                    + item.RewardInstanceStableId;
                            return false;
                        }
                        equipment.Add(exactEquipment);
                        break;
                    case RewardGrantKindV1.Strongbox:
                        if (item.Quantity != 1L)
                        {
                            diagnostic =
                                "collected-run-transfer-strongbox-child-quantity-invalid:"
                                + item.RewardInstanceStableId;
                            return false;
                        }
                        StrongboxDefinitionV1 definition;
                        if (!graph.StrongboxCatalog.TryGet(
                            item.ContentStableId,
                            out definition))
                        {
                            diagnostic =
                                "collected-run-transfer-strongbox-tier-unknown:"
                                + item.ContentStableId;
                            return false;
                        }
                        boxes.Add(StrongboxInstanceContextV1.Create(
                            item.RewardInstanceStableId,
                            item.ContentStableId,
                            DeriveStrongboxSeed(generationContext, item),
                            generationContext.AlgorithmVersion,
                            generationContext.ProgressionContext,
                            item.DropOperationStableId,
                            item.SourceGrantStableId,
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
                CollectedRunRewardTransferCanonicalV1.DeriveStableId(
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
                CollectedRunRewardTransferCanonicalV1.DeriveStableId(
                    "operation",
                    "collected-run-prepare",
                    custodyStableId + "|" + endCommand.Fingerprint);

            var authorityFingerprints = new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                { "money", graph.MoneyWallet.CurrentSnapshot.Fingerprint },
                { "scrap", graph.ScrapWallet.ExportSnapshot().Fingerprint },
                { "holdings", graph.LoadoutRuntime.Holdings.ExportSnapshot().Fingerprint },
                { "reward-application", rewardApplication.ExportSnapshot().Fingerprint },
                { "strongboxes", graph.StrongboxAuthority.ExportSnapshot().Fingerprint },
                { "transfer-receipts", receipts.ExportSnapshot().Fingerprint },
            };
            awaiting =
                CollectedRunRewardPreparedTransferV1.AwaitingAcceptedEnd(
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
        /// Promotes pre-End custody using only the accepted immutable End result, then
        /// builds the exact whole-batch RAP/BOX plan. No mutable drop/profile lookup occurs.
        /// </summary>
        public static bool TryAcceptEndAndBuildPlan(
            RunSessionEndResultV1 acceptedEnd,
            CollectedRunRewardPreparedTransferV1 awaiting,
            ProductionCharacterRuntimeGraphV1 graph,
            RewardApplicationServiceV1 rewardApplication,
            out CollectedRunRewardPreparedTransferV1 prepared,
            out CollectedRunRewardAtomicPlanV2 plan,
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
                || awaiting.State
                    != CollectedRunRewardPreparedTransferStateV1.AwaitingAcceptedEnd)
            {
                diagnostic = "collected-run-transfer-end-receipt-not-accepted";
                return false;
            }
            if (acceptedEnd.Receipt.MissionResult == null
                || acceptedEnd.Receipt.MissionResult.CompletionState
                    != Contracts.Missions.Results.MissionRunCompletionStateV1.Completed
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
                diagnostic = "collected-run-transfer-accepted-end-conflict";
                return false;
            }
            StableId transferOperationStableId =
                CollectedRunRewardTransferCanonicalV1.DeriveStableId(
                    "operation",
                    "collected-run-transfer",
                    acceptedEnd.Receipt.Fingerprint);
            StableId missionResultStableId =
                CollectedRunRewardTransferCanonicalV1.DeriveStableId(
                    "mission-result",
                    "accepted",
                    acceptedEnd.Receipt.MissionResult.Fingerprint);
            string batchFingerprint =
                CollectedRunRewardAtomicPlanV2.ComputeBatchFingerprint(
                    transferOperationStableId,
                    awaiting.RunStableId,
                    awaiting.LifecycleGeneration,
                    missionResultStableId,
                    acceptedEnd.Receipt.MissionResult.Fingerprint,
                    awaiting.SelectedCharacterStableId,
                    awaiting.ExpectedCharacterRevision,
                    awaiting.ExpectedCharacterFingerprint,
                    awaiting.Rewards);

            RewardCommitCommandV1 commit;
            RewardClaimCommandV1 claim;
            List<RewardGrantApplicationPayloadV1> payloads;
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
                CollectedRunRewardAtomicPlanV2.ComputeFingerprint(
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
                plan = new CollectedRunRewardAtomicPlanV2(
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
            CollectedRunRewardPreparedTransferV1 prepared,
            ProductionCharacterRuntimeGraphV1 graph,
            RewardApplicationServiceV1 rewardApplication,
            out CollectedRunRewardAtomicPlanV2 plan,
            out string diagnostic)
        {
            plan = null;
            diagnostic = string.Empty;
            if (prepared == null
                || prepared.State
                    == CollectedRunRewardPreparedTransferStateV1.AwaitingAcceptedEnd
                || graph == null
                || graph.IsDisposed
                || rewardApplication == null
                || graph.Character.CharacterInstanceStableId
                    != prepared.SelectedCharacterStableId)
            {
                diagnostic = "collected-run-transfer-recovery-context-invalid";
                return false;
            }
            RewardCommitCommandV1 commit;
            RewardClaimCommandV1 claim;
            List<RewardGrantApplicationPayloadV1> payloads;
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
                plan = new CollectedRunRewardAtomicPlanV2(
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
            CollectedRunRewardPreparedTransferV1 prepared,
            StableId transferOperationStableId,
            string batchFingerprint,
            ProductionCharacterRuntimeGraphV1 graph,
            out RewardCommitCommandV1 commit,
            out RewardClaimCommandV1 claim,
            out List<RewardGrantApplicationPayloadV1> payloads,
            out string diagnostic)
        {
            commit = null;
            claim = null;
            payloads = new List<RewardGrantApplicationPayloadV1>();
            diagnostic = string.Empty;
            if (prepared == null
                || transferOperationStableId == null
                || string.IsNullOrWhiteSpace(batchFingerprint)
                || graph == null)
            {
                diagnostic = "collected-run-transfer-rap-plan-context-invalid";
                return false;
            }

            var equipmentById = prepared.Equipment.ToDictionary(
                item => item.InstanceId,
                item => item);
            var grants = new List<RewardGrantV1>(prepared.Rewards.Count);
            for (int index = 0; index < prepared.Rewards.Count; index++)
            {
                CollectedRunRewardTransferItemV1 item = prepared.Rewards[index];
                var grant = RewardGrantV1.Create(
                    item.RewardInstanceStableId,
                    item.RewardKind,
                    item.ContentStableId,
                    item.Quantity);
                grants.Add(grant);
                switch (item.RewardKind)
                {
                    case RewardGrantKindV1.Money:
                    case RewardGrantKindV1.Scrap:
                        payloads.Add(RewardGrantApplicationPayloadV1.ForValue(grant));
                        break;
                    case RewardGrantKindV1.Strongbox:
                        payloads.Add(RewardGrantApplicationPayloadV1.ForStrongboxes(
                            grant,
                            new[] { item.RewardInstanceStableId }));
                        break;
                    case RewardGrantKindV1.EquipmentReference:
                        EquipmentInstance equipment;
                        if (!equipmentById.TryGetValue(
                            item.RewardInstanceStableId,
                            out equipment)
                            || equipment.DefinitionId != item.ContentStableId)
                        {
                            diagnostic =
                                "collected-run-transfer-recovery-equipment-payload-missing:"
                                + item.RewardInstanceStableId;
                            return false;
                        }
                        payloads.Add(RewardGrantApplicationPayloadV1.ForEquipment(
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
                CollectedRunRewardTransferCanonicalV1.DeriveStableId(
                    "commitment",
                    "collected-run-transfer",
                    batchFingerprint);
            RewardResultV1 generatedReward = grants.Count == 0
                ? RewardResultV1.CreateExplicitNoDrop(
                    commitmentStableId,
                    transferOperationStableId)
                : RewardResultV1.CreateGrants(
                    commitmentStableId,
                    transferOperationStableId,
                    grants);
            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                prepared.RunStableId,
                prepared.RunStableId,
                transferOperationStableId,
                commitmentStableId,
                TransferProfileStableId,
                batchFingerprint);
            commit = RewardCommitCommandV1.Create(
                operation,
                generatedReward,
                GenerationFingerprint(prepared, batchFingerprint),
                payloads);
            claim = RewardClaimCommandV1.Create(
                CollectedRunRewardTransferCanonicalV1.DeriveStableId(
                    "claim",
                    "collected-run-transfer",
                    batchFingerprint),
                commitmentStableId,
                prepared.SelectedCharacterStableId,
                MoneyWalletIdsV1.AuthorityStableId,
                graph.ScrapWallet.AuthorityStableId,
                graph.LoadoutRuntime.Holdings.AuthorityStableId,
                prepared.ExpectedMoneySequence,
                prepared.ExpectedScrapSequence,
                prepared.ExpectedHoldingsSequence);
            return true;
        }

        private static CollectedRunRewardTransferItemV1 ToTransferItem(
            RunSessionCollectedRewardV1 reward)
        {
            return new CollectedRunRewardTransferItemV1(
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
            CollectedRunRewardGenerationContextV2 context,
            CollectedRunRewardTransferItemV1 item)
        {
            string fingerprint = CollectedRunRewardTransferCanonicalV1.Hash(
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
            IReadOnlyList<CollectedRunRewardTransferItemV1> items)
        {
            var builder = new StringBuilder("schema=collected-run-reward-custody-items-v2");
            for (int index = 0; index < items.Count; index++)
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "reward:" + index.ToString(CultureInfo.InvariantCulture),
                    items[index].Fingerprint);
            return CollectedRunRewardTransferCanonicalV1.Hash(builder.ToString());
        }

        private static string GenerationFingerprint(
            CollectedRunRewardPreparedTransferV1 prepared,
            string batchFingerprint)
        {
            var builder = new StringBuilder(
                "schema=collected-run-transfer-generation-proof-v2");
            CollectedRunRewardTransferCanonicalV1.Append(builder, "batch", batchFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "seed", prepared.GenerationRootSeed);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "algorithm", prepared.GenerationAlgorithmVersion);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "progression", prepared.ProgressionContext.Fingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "event-modifiers", prepared.EventModifierFingerprint);
            for (int index = 0; index < prepared.Rewards.Count; index++)
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "generated-reward:" + index.ToString(CultureInfo.InvariantCulture),
                    prepared.Rewards[index].GeneratedRewardFingerprint);
            return CollectedRunRewardTransferCanonicalV1.Hash(builder.ToString());
        }
    }
}
