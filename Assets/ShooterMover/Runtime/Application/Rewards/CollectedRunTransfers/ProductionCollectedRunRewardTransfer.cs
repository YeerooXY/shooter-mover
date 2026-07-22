using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    /// <summary>
    /// Frozen generation facts needed to preserve unopened strongbox context without
    /// rerolling or consulting mutable player progression during Results.
    /// </summary>
    public sealed class CollectedRunRewardGenerationContextV1
    {
        public CollectedRunRewardGenerationContextV1(
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
                    "An event-modifier fingerprint is required.",
                    nameof(eventModifierFingerprint));
            EventModifierFingerprint = eventModifierFingerprint.Trim();

            var builder = new StringBuilder(
                "schema=collected-run-reward-generation-context-v1");
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "root-seed", RootSeed);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "algorithm-version", AlgorithmVersion);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "progression", ProgressionContext.Fingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "event-modifiers", EventModifierFingerprint);
            Fingerprint =
                CollectedRunRewardTransferCanonicalV1.Hash(builder.ToString());
        }

        public ulong RootSeed { get; }
        public int AlgorithmVersion { get; }
        public ProgressionContext ProgressionContext { get; }
        public string EventModifierFingerprint { get; }
        public string Fingerprint { get; }
    }

    /// <summary>
    /// Exact generated equipment lookup. Implementations may only return the retained
    /// EquipmentInstance produced for the journal reward; they must never regenerate it.
    /// </summary>
    public interface ICollectedRunEquipmentPayloadSource
    {
        bool TryResolveExact(
            StableId rewardInstanceStableId,
            StableId equipmentDefinitionStableId,
            out EquipmentInstance equipment,
            out string diagnostic);
    }

    public sealed class RejectingCollectedRunEquipmentPayloadSource :
        ICollectedRunEquipmentPayloadSource
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

    /// <summary>
    /// Immutable application plan pairing the canonical run journal with exact RAP
    /// payloads and unopened BOX contexts. Its fingerprint closes the gap between a
    /// journal identity and the concrete permanent payload applied for that identity.
    /// </summary>
    public sealed class CollectedRunRewardApplicationPlanV1
    {
        private readonly ReadOnlyCollection<
            RewardGrantApplicationPayloadV1> payloads;
        private readonly ReadOnlyCollection<
            StrongboxInstanceContextV1> strongboxContexts;
        private readonly ReadOnlyDictionary<
            StableId, StrongboxInstanceContextV1> strongboxByReward;

        public CollectedRunRewardApplicationPlanV1(
            CollectedRunRewardTransferBatchV1 batch,
            RewardCommitCommandV1 commitCommand,
            RewardClaimCommandV1 claimCommand,
            IEnumerable<RewardGrantApplicationPayloadV1> payloads,
            IEnumerable<StrongboxInstanceContextV1> strongboxContexts)
        {
            Batch = batch ?? throw new ArgumentNullException(nameof(batch));
            CommitCommand = commitCommand
                ?? throw new ArgumentNullException(nameof(commitCommand));
            ClaimCommand = claimCommand
                ?? throw new ArgumentNullException(nameof(claimCommand));
            if (CommitCommand.CommitmentStableId
                    != ClaimCommand.CommitmentStableId
                || CommitCommand.SourceOperationStableId
                    != Batch.TransferOperationStableId)
            {
                throw new ArgumentException(
                    "The RAP commands must belong to the exact transfer batch.");
            }

            var payloadCopy =
                new List<RewardGrantApplicationPayloadV1>(
                    payloads
                    ?? throw new ArgumentNullException(nameof(payloads)));
            if (payloadCopy.Any(item => item == null))
                throw new ArgumentException(
                    "Application payloads cannot contain null.",
                    nameof(payloads));
            payloadCopy.Sort();
            this.payloads =
                new ReadOnlyCollection<RewardGrantApplicationPayloadV1>(
                    payloadCopy);

            var contextCopy =
                new List<StrongboxInstanceContextV1>(
                    strongboxContexts
                    ?? throw new ArgumentNullException(
                        nameof(strongboxContexts)));
            if (contextCopy.Any(item => item == null))
                throw new ArgumentException(
                    "Strongbox contexts cannot contain null.",
                    nameof(strongboxContexts));
            contextCopy.Sort((left, right) =>
                left.InstanceStableId.CompareTo(right.InstanceStableId));
            var byReward =
                new Dictionary<StableId, StrongboxInstanceContextV1>();
            for (int index = 0; index < contextCopy.Count; index++)
            {
                StrongboxInstanceContextV1 context = contextCopy[index];
                if (byReward.ContainsKey(context.InstanceStableId))
                {
                    throw new ArgumentException(
                        "Strongbox contexts contain duplicate instance identity.",
                        nameof(strongboxContexts));
                }
                byReward.Add(context.InstanceStableId, context);
            }
            this.strongboxContexts =
                new ReadOnlyCollection<StrongboxInstanceContextV1>(
                    contextCopy);
            strongboxByReward =
                new ReadOnlyDictionary<
                    StableId, StrongboxInstanceContextV1>(byReward);

            var builder = new StringBuilder(
                "schema=collected-run-reward-application-plan-v1");
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "batch", Batch.Fingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "commit", CommitCommand.Fingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "claim", ClaimCommand.Fingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "payload-count", this.payloads.Count);
            for (int index = 0; index < this.payloads.Count; index++)
            {
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "payload:" + index.ToString(
                        CultureInfo.InvariantCulture),
                    this.payloads[index].Fingerprint);
            }
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "strongbox-context-count",
                this.strongboxContexts.Count);
            for (int index = 0;
                index < this.strongboxContexts.Count;
                index++)
            {
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "strongbox-context:" + index.ToString(
                        CultureInfo.InvariantCulture),
                    this.strongboxContexts[index].Fingerprint);
            }
            Fingerprint =
                CollectedRunRewardTransferCanonicalV1.Hash(
                    builder.ToString());
        }

        public CollectedRunRewardTransferBatchV1 Batch { get; }
        public RewardCommitCommandV1 CommitCommand { get; }
        public RewardClaimCommandV1 ClaimCommand { get; }
        public IReadOnlyList<RewardGrantApplicationPayloadV1>
            Payloads { get { return payloads; } }
        public IReadOnlyList<StrongboxInstanceContextV1>
            StrongboxContexts { get { return strongboxContexts; } }
        public string Fingerprint { get; }

        public bool TryGetStrongboxContext(
            StableId rewardInstanceStableId,
            out StrongboxInstanceContextV1 context)
        {
            context = null;
            return rewardInstanceStableId != null
                && strongboxByReward.TryGetValue(
                    rewardInstanceStableId,
                    out context);
        }
    }

    /// <summary>
    /// Canonical adapter from the shared Run Session journal and accepted end receipt.
    /// No values are read from collision callbacks, UI, reward profiles, or mutable XP.
    /// </summary>
    public static class RunSessionCollectedRewardTransferPlanFactory
    {
        private static readonly StableId TransferProfileStableId =
            StableId.Parse("reward-profile.collected-run-transfer");

        public static bool TryCreate(
            RunSessionEndResultV1 acceptedEnd,
            IReadOnlyList<RunSessionCollectedRewardV1> collectedRewards,
            CharacterInstanceSnapshotV1 selectedCharacter,
            CollectedRunRewardGenerationContextV1 generationContext,
            ProductionCharacterRuntimeGraphV1 graph,
            RewardApplicationServiceV1 rewardApplication,
            ICollectedRunEquipmentPayloadSource equipmentPayloadSource,
            out CollectedRunRewardApplicationPlanV1 plan,
            out string diagnostic)
        {
            plan = null;
            diagnostic = string.Empty;
            if (acceptedEnd == null
                || !acceptedEnd.Succeeded
                || acceptedEnd.Receipt == null
                || acceptedEnd.Command == null)
            {
                diagnostic =
                    "collected-run-transfer-end-receipt-not-accepted";
                return false;
            }
            if (acceptedEnd.Receipt.MissionResult.CompletionState
                != MissionRunCompletionStateV1.Completed)
            {
                diagnostic =
                    "collected-run-transfer-mission-not-completed";
                return false;
            }
            if (selectedCharacter == null
                || graph == null
                || graph.IsDisposed
                || rewardApplication == null
                || generationContext == null)
            {
                diagnostic =
                    "collected-run-transfer-production-context-missing";
                return false;
            }
            if (selectedCharacter.CharacterInstanceStableId
                    != acceptedEnd.Receipt.SelectedCharacterStableId
                || graph.Character.CharacterInstanceStableId
                    != selectedCharacter.CharacterInstanceStableId
                || selectedCharacter.Revision
                    != acceptedEnd.Receipt.ExpectedCharacterRevision
                || !string.Equals(
                    selectedCharacter.Fingerprint,
                    acceptedEnd.Receipt.ExpectedCharacterFingerprint,
                    StringComparison.Ordinal))
            {
                diagnostic =
                    "collected-run-transfer-character-expectation-mismatch";
                return false;
            }

            var journal =
                new List<RunSessionCollectedRewardV1>(
                    collectedRewards
                    ?? Array.Empty<RunSessionCollectedRewardV1>());
            journal.Sort((left, right) =>
            {
                int identity = left.GeneratedRewardChildStableId.CompareTo(
                    right.GeneratedRewardChildStableId);
                return identity != 0
                    ? identity
                    : string.CompareOrdinal(
                        left.Fingerprint,
                        right.Fingerprint);
            });

            var items =
                new List<CollectedRunRewardTransferItemV1>(
                    journal.Count);
            for (int index = 0; index < journal.Count; index++)
            {
                RunSessionCollectedRewardV1 reward = journal[index];
                if (reward == null
                    || reward.RunStableId
                        != acceptedEnd.Receipt.RunStableId
                    || reward.RunLifecycleGeneration
                        != acceptedEnd.Command.LifecycleGeneration)
                {
                    diagnostic =
                        "collected-run-transfer-journal-run-or-lifecycle-mismatch";
                    return false;
                }
                items.Add(ToTransferItem(reward));
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
            var batch = new CollectedRunRewardTransferBatchV1(
                transferOperationStableId,
                acceptedEnd.Receipt.RunStableId,
                acceptedEnd.Command.LifecycleGeneration,
                missionResultStableId,
                acceptedEnd.Receipt.MissionResult,
                selectedCharacter.CharacterInstanceStableId,
                selectedCharacter.Revision,
                selectedCharacter.Fingerprint,
                items);

            StableId commitmentStableId =
                CollectedRunRewardTransferCanonicalV1.DeriveStableId(
                    "commitment",
                    "collected-run-transfer",
                    batch.Fingerprint);
            var grants = new List<RewardGrantV1>(items.Count);
            var payloads =
                new List<RewardGrantApplicationPayloadV1>(
                    items.Count);
            var strongboxContexts =
                new List<StrongboxInstanceContextV1>();

            ICollectedRunEquipmentPayloadSource equipmentSource =
                equipmentPayloadSource
                ?? new RejectingCollectedRunEquipmentPayloadSource();
            for (int index = 0; index < items.Count; index++)
            {
                CollectedRunRewardTransferItemV1 item = items[index];
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
                        payloads.Add(
                            RewardGrantApplicationPayloadV1.ForValue(
                                grant));
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
                        var context =
                            StrongboxInstanceContextV1.Create(
                                item.RewardInstanceStableId,
                                item.ContentStableId,
                                DeriveStrongboxSeed(
                                    generationContext,
                                    item),
                                generationContext.AlgorithmVersion,
                                generationContext.ProgressionContext,
                                item.DropOperationStableId,
                                item.CollectionOperationStableId,
                                definition.Fingerprint);
                        strongboxContexts.Add(context);
                        payloads.Add(
                            RewardGrantApplicationPayloadV1.ForStrongboxes(
                                grant,
                                new[]
                                {
                                    item.RewardInstanceStableId,
                                }));
                        break;
                    case RewardGrantKindV1.EquipmentReference:
                        if (item.Quantity != 1L)
                        {
                            diagnostic =
                                "collected-run-transfer-equipment-child-quantity-invalid:"
                                + item.RewardInstanceStableId;
                            return false;
                        }
                        EquipmentInstance equipment;
                        if (!equipmentSource.TryResolveExact(
                            item.RewardInstanceStableId,
                            item.ContentStableId,
                            out equipment,
                            out diagnostic)
                            || equipment == null
                            || equipment.InstanceId
                                != item.RewardInstanceStableId
                            || equipment.DefinitionId
                                != item.ContentStableId)
                        {
                            if (string.IsNullOrWhiteSpace(diagnostic))
                                diagnostic =
                                    "collected-run-transfer-exact-equipment-payload-invalid:"
                                    + item.RewardInstanceStableId;
                            return false;
                        }
                        payloads.Add(
                            RewardGrantApplicationPayloadV1.ForEquipment(
                                grant,
                                new[] { equipment }));
                        break;
                    default:
                        diagnostic =
                            "collected-run-transfer-reward-kind-unsupported:"
                            + item.RewardKind;
                        return false;
                }
            }

            RewardResultV1 generatedReward =
                grants.Count == 0
                    ? RewardResultV1.CreateExplicitNoDrop(
                        commitmentStableId,
                        batch.TransferOperationStableId)
                    : RewardResultV1.CreateGrants(
                        commitmentStableId,
                        batch.TransferOperationStableId,
                        grants);
            RewardOperationRequestV1 operation =
                RewardOperationRequestV1.Create(
                    batch.RunStableId,
                    batch.RunStableId,
                    batch.TransferOperationStableId,
                    commitmentStableId,
                    TransferProfileStableId,
                    batch.Fingerprint);
            RewardCommitCommandV1 commit =
                RewardCommitCommandV1.Create(
                    operation,
                    generatedReward,
                    GenerationFingerprint(
                        batch,
                        generationContext,
                        items),
                    payloads);
            RewardClaimCommandV1 claim =
                RewardClaimCommandV1.Create(
                    CollectedRunRewardTransferCanonicalV1.DeriveStableId(
                        "claim",
                        "collected-run-transfer",
                        batch.Fingerprint),
                    commitmentStableId,
                    selectedCharacter.CharacterInstanceStableId,
                    MoneyWalletIdsV1.AuthorityStableId,
                    graph.ScrapWallet.AuthorityStableId,
                    graph.LoadoutRuntime.Holdings.AuthorityStableId,
                    graph.MoneyWallet.Sequence,
                    graph.ScrapWallet.Sequence,
                    graph.LoadoutRuntime.Holdings.Sequence);
            plan = new CollectedRunRewardApplicationPlanV1(
                batch,
                commit,
                claim,
                payloads,
                strongboxContexts);
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
            CollectedRunRewardGenerationContextV1 context,
            CollectedRunRewardTransferItemV1 item)
        {
            string fingerprint =
                CollectedRunRewardTransferCanonicalV1.Hash(
                    context.Fingerprint
                    + "|"
                    + item.GeneratedRewardFingerprint
                    + "|"
                    + item.RewardInstanceStableId);
            string hex = fingerprint.Substring(
                "sha256:".Length,
                16);
            return ulong.Parse(
                hex,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
        }

        private static string GenerationFingerprint(
            CollectedRunRewardTransferBatchV1 batch,
            CollectedRunRewardGenerationContextV1 context,
            IReadOnlyList<CollectedRunRewardTransferItemV1> items)
        {
            var builder = new StringBuilder(
                "schema=collected-run-transfer-generation-proof-v1");
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "batch", batch.Fingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder, "context", context.Fingerprint);
            for (int index = 0; index < items.Count; index++)
            {
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "generated-reward:" + index.ToString(
                        CultureInfo.InvariantCulture),
                    items[index].GeneratedRewardFingerprint);
            }
            return CollectedRunRewardTransferCanonicalV1.Hash(
                builder.ToString());
        }
    }

    /// <summary>
    /// Character-scoped wiring registry. It stores references only; RAP, BOX and receipt
    /// authorities remain the owners of their existing state.
    /// </summary>
    public static class ProductionCollectedRunRewardTransferRuntimeRegistry
    {
        private sealed class Entry
        {
            public RewardApplicationServiceV1 RewardApplication;
            public CollectedRunRewardTransferReceiptAuthorityV1 Receipts;
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<StableId, Entry> Entries =
            new Dictionary<StableId, Entry>();

        public static void BindRewardApplication(
            StableId characterStableId,
            RewardApplicationServiceV1 rewardApplication)
        {
            if (characterStableId == null)
                throw new ArgumentNullException(nameof(characterStableId));
            if (rewardApplication == null)
                throw new ArgumentNullException(nameof(rewardApplication));
            lock (Gate)
            {
                Entry entry;
                if (!Entries.TryGetValue(characterStableId, out entry))
                {
                    entry = new Entry();
                    Entries.Add(characterStableId, entry);
                }
                entry.RewardApplication = rewardApplication;
            }
        }

        public static ISaveComponentAdapterV1 CreateReceiptSaveAdapter(
            StableId characterStableId)
        {
            if (characterStableId == null)
                throw new ArgumentNullException(nameof(characterStableId));
            lock (Gate)
            {
                Entry entry;
                if (!Entries.TryGetValue(characterStableId, out entry))
                {
                    entry = new Entry();
                    Entries.Add(characterStableId, entry);
                }
                entry.Receipts =
                    new CollectedRunRewardTransferReceiptAuthorityV1();
                return CollectedRunRewardTransferReceiptSaveComponentV1
                    .CreateAdapter(entry.Receipts);
            }
        }

        public static bool TryResolve(
            StableId characterStableId,
            out RewardApplicationServiceV1 rewardApplication,
            out CollectedRunRewardTransferReceiptAuthorityV1 receipts)
        {
            rewardApplication = null;
            receipts = null;
            if (characterStableId == null)
                return false;
            lock (Gate)
            {
                Entry entry;
                if (!Entries.TryGetValue(characterStableId, out entry)
                    || entry.RewardApplication == null
                    || entry.Receipts == null)
                {
                    return false;
                }
                rewardApplication = entry.RewardApplication;
                receipts = entry.Receipts;
                return true;
            }
        }
    }

    internal sealed class ProductionCollectedRunRewardTransferCompensation :
        ICollectedRunRewardTransferCompensationV1
    {
        public ProductionCollectedRunRewardTransferCompensation(
            MoneyWalletSnapshot money,
            ScrapSnapshotV1 scrap,
            PlayerHoldingsSnapshotV1 holdings,
            RewardApplicationSnapshotV1 rewardApplication,
            StrongboxOpeningSnapshotV1 strongboxes,
            CollectedRunRewardTransferReceiptSnapshotV1 receipts)
        {
            Money = money ?? throw new ArgumentNullException(nameof(money));
            Scrap = scrap ?? throw new ArgumentNullException(nameof(scrap));
            Holdings = holdings
                ?? throw new ArgumentNullException(nameof(holdings));
            RewardApplication = rewardApplication
                ?? throw new ArgumentNullException(
                    nameof(rewardApplication));
            Strongboxes = strongboxes
                ?? throw new ArgumentNullException(nameof(strongboxes));
            Receipts = receipts
                ?? throw new ArgumentNullException(nameof(receipts));
            Fingerprint =
                CollectedRunRewardTransferCanonicalV1.Hash(
                    Money.Fingerprint
                    + "|"
                    + Scrap.Fingerprint
                    + "|"
                    + Holdings.Fingerprint
                    + "|"
                    + RewardApplication.Fingerprint
                    + "|"
                    + Strongboxes.Fingerprint
                    + "|"
                    + Receipts.Fingerprint);
        }

        public MoneyWalletSnapshot Money { get; }
        public ScrapSnapshotV1 Scrap { get; }
        public PlayerHoldingsSnapshotV1 Holdings { get; }
        public RewardApplicationSnapshotV1 RewardApplication { get; }
        public StrongboxOpeningSnapshotV1 Strongboxes { get; }
        public CollectedRunRewardTransferReceiptSnapshotV1 Receipts
        {
            get;
        }
        public string Fingerprint { get; }
    }

    /// <summary>
    /// Concrete port over the selected production character's existing RAP, wallet,
    /// scrap, holdings, BOX and durable receipt authorities.
    /// </summary>
    public sealed class ProductionCollectedRunRewardTransferAuthorityAdapter :
        ICollectedRunRewardTransferAuthorityPortV1
    {
        public const string ApplicationPlanAuthorityKey =
            "collected-run-application-plan";
        private readonly ProductionCharacterRuntimeGraphV1 graph;
        private readonly CharacterCompositionCoordinatorV1 composition;
        private readonly RewardApplicationServiceV1 rewardApplication;
        private readonly CollectedRunRewardTransferReceiptAuthorityV1 receipts;
        private readonly CollectedRunRewardApplicationPlanV1 plan;
        private bool batchApplied;

        public ProductionCollectedRunRewardTransferAuthorityAdapter(
            ProductionCharacterRuntimeGraphV1 graph,
            CharacterCompositionCoordinatorV1 composition,
            RewardApplicationServiceV1 rewardApplication,
            CollectedRunRewardTransferReceiptAuthorityV1 receipts,
            CollectedRunRewardApplicationPlanV1 plan)
        {
            this.graph = graph
                ?? throw new ArgumentNullException(nameof(graph));
            this.composition = composition
                ?? throw new ArgumentNullException(nameof(composition));
            this.rewardApplication = rewardApplication
                ?? throw new ArgumentNullException(
                    nameof(rewardApplication));
            this.receipts = receipts
                ?? throw new ArgumentNullException(nameof(receipts));
            this.plan = plan
                ?? throw new ArgumentNullException(nameof(plan));
        }

        public PermanentRewardTransferStateV1 ExportState()
        {
            PlayerAccountSnapshotV1 account = composition.Account;
            CharacterInstanceSnapshotV1 character = graph.Character;
            var fingerprints = new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                { "money", graph.MoneyWallet.CurrentSnapshot.Fingerprint },
                { "scrap", graph.ScrapWallet.ExportSnapshot().Fingerprint },
                {
                    "holdings",
                    graph.LoadoutRuntime.Holdings.ExportSnapshot()
                        .Fingerprint
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
                { ApplicationPlanAuthorityKey, plan.Fingerprint },
            };
            return new PermanentRewardTransferStateV1(
                character.CharacterInstanceStableId,
                character.Revision,
                character.Fingerprint,
                account.Revision,
                account.Fingerprint,
                fingerprints);
        }

        public bool TryGetDurableReceipt(
            StableId transferOperationStableId,
            out CollectedRunRewardTransferReceiptV1 receipt)
        {
            return receipts.TryGetByOperation(
                transferOperationStableId,
                out receipt);
        }

        public bool TryGetDurableReceiptForReward(
            StableId rewardInstanceStableId,
            out CollectedRunRewardTransferReceiptV1 receipt)
        {
            return receipts.TryGetByReward(
                rewardInstanceStableId,
                out receipt);
        }

        public CollectedRunRewardTransferPreflightResultV1 Preflight(
            CollectedRunRewardTransferBatchV1 batch)
        {
            if (batch == null
                || !string.Equals(
                    batch.Fingerprint,
                    plan.Batch.Fingerprint,
                    StringComparison.Ordinal))
            {
                return CollectedRunRewardTransferPreflightResultV1.Rejected(
                    "collected-run-transfer-plan-batch-mismatch");
            }
            if (graph.IsDisposed
                || graph.Character.CharacterInstanceStableId
                    != batch.SelectedCharacterStableId)
            {
                return CollectedRunRewardTransferPreflightResultV1.Rejected(
                    "collected-run-transfer-active-character-mismatch");
            }

            StrongboxOpeningSnapshotV1 current =
                graph.StrongboxAuthority.ExportSnapshot();
            var existing =
                new Dictionary<StableId, StrongboxInstanceContextV1>();
            for (int index = 0; index < current.Contexts.Count; index++)
                existing[current.Contexts[index].InstanceStableId] =
                    current.Contexts[index];
            for (int index = 0;
                index < plan.StrongboxContexts.Count;
                index++)
            {
                StrongboxInstanceContextV1 context =
                    plan.StrongboxContexts[index];
                StrongboxInstanceContextV1 prior;
                if (existing.TryGetValue(
                        context.InstanceStableId,
                        out prior)
                    && !string.Equals(
                        prior.Fingerprint,
                        context.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return CollectedRunRewardTransferPreflightResultV1
                        .Rejected(
                            "collected-run-transfer-strongbox-context-conflict:"
                            + context.InstanceStableId);
                }
            }
            return CollectedRunRewardTransferPreflightResultV1.Accepted();
        }

        public string ResolveAuthorityTarget(
            CollectedRunRewardTransferItemV1 reward)
        {
            if (reward == null)
                return string.Empty;
            switch (reward.RewardKind)
            {
                case RewardGrantKindV1.Money:
                    return "money";
                case RewardGrantKindV1.Scrap:
                    return "scrap";
                case RewardGrantKindV1.Strongbox:
                    return "holdings+box";
                case RewardGrantKindV1.EquipmentReference:
                    return "holdings";
                default:
                    return string.Empty;
            }
        }

        public ICollectedRunRewardTransferCompensationV1
            CaptureCompensation()
        {
            return new ProductionCollectedRunRewardTransferCompensation(
                graph.MoneyWallet.CurrentSnapshot,
                graph.ScrapWallet.ExportSnapshot(),
                graph.LoadoutRuntime.Holdings.ExportSnapshot(),
                rewardApplication.ExportSnapshot(),
                graph.StrongboxAuthority.ExportSnapshot(),
                receipts.ExportSnapshot());
        }

        public CollectedRunRewardTransferChildResultV1 Apply(
            CollectedRunRewardTransferChildCommandV1 command)
        {
            if (command == null
                || !string.Equals(
                    command.Batch.Fingerprint,
                    plan.Batch.Fingerprint,
                    StringComparison.Ordinal))
            {
                return new CollectedRunRewardTransferChildResultV1(
                    CollectedRunRewardTransferAuthorityStatusV1.Rejected,
                    command,
                    string.Empty,
                    "collected-run-transfer-child-plan-mismatch");
            }
            if (batchApplied)
            {
                return new CollectedRunRewardTransferChildResultV1(
                    CollectedRunRewardTransferAuthorityStatusV1.ExactReplay,
                    command,
                    ExportState().Fingerprint,
                    string.Empty);
            }

            RewardApplicationResultV1 committed =
                rewardApplication.Commit(plan.CommitCommand);
            if (!CommitAccepted(committed))
                return Rejected(
                    command,
                    "collected-run-transfer-rap-commit-rejected:"
                    + ResultCode(committed));

            RewardApplicationResultV1 claimed =
                rewardApplication.Claim(plan.ClaimCommand);
            if (!ClaimAccepted(claimed))
                return Rejected(
                    command,
                    "collected-run-transfer-rap-claim-rejected:"
                    + ResultCode(claimed));

            for (int index = 0;
                index < plan.StrongboxContexts.Count;
                index++)
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
                    return Rejected(
                        command,
                        "collected-run-transfer-box-context-rejected:"
                        + (registered == null
                            ? "null"
                            : registered.RejectionCode));
                }
            }

            batchApplied = true;
            return new CollectedRunRewardTransferChildResultV1(
                CollectedRunRewardTransferAuthorityStatusV1.Applied,
                command,
                ExportState().Fingerprint,
                string.Empty);
        }

        public CollectedRunRewardTransferReceiptRecordResultV1
            RecordReceipt(
                CollectedRunRewardTransferReceiptV1 receipt)
        {
            return receipts.Record(receipt);
        }

        public CollectedRunRewardTransferRestoreResultV1 Restore(
            ICollectedRunRewardTransferCompensationV1 compensation)
        {
            var typed =
                compensation
                as ProductionCollectedRunRewardTransferCompensation;
            if (typed == null)
            {
                return new CollectedRunRewardTransferRestoreResultV1(
                    false,
                    "collected-run-transfer-compensation-type-invalid");
            }

            var diagnostics = new List<string>();
            MoneyWalletImportResult money =
                graph.MoneyWallet.ImportSnapshot(typed.Money);
            if (money.Status != MoneyWalletImportStatus.Imported)
                diagnostics.Add("money:" + money.RejectionCode);
            ScrapSnapshotImportResultV1 scrap =
                graph.ScrapWallet.ImportSnapshot(typed.Scrap);
            if (!scrap.Succeeded)
                diagnostics.Add("scrap:" + scrap.RejectionCode);
            PlayerHoldingsImportResultV1 holdings =
                graph.LoadoutRuntime.Holdings.ImportSnapshot(
                    typed.Holdings);
            if (!holdings.Succeeded)
                diagnostics.Add(
                    "holdings:" + holdings.RejectionCode);
            RewardApplicationImportResultV1 rap =
                rewardApplication.ImportSnapshot(
                    typed.RewardApplication);
            if (rap.Status != RewardApplicationImportStatusV1.Imported)
                diagnostics.Add(
                    "reward-application:" + rap.RejectionCode);
            StrongboxOpeningImportResultV1 boxes =
                graph.StrongboxAuthority.ImportSnapshot(
                    typed.Strongboxes);
            if (!boxes.Succeeded)
                diagnostics.Add(
                    "strongboxes:" + boxes.RejectionCode);
            SaveComponentApplyResultV1 receiptRestore =
                receipts.ImportSnapshot(typed.Receipts);
            if (!receiptRestore.Succeeded)
                diagnostics.Add(
                    "receipts:" + receiptRestore.RejectionCode);

            batchApplied = false;
            return new CollectedRunRewardTransferRestoreResultV1(
                diagnostics.Count == 0,
                string.Join("|", diagnostics));
        }

        private static bool CommitAccepted(
            RewardApplicationResultV1 result)
        {
            return result != null
                && (result.Status
                        == RewardApplicationResultStatusV1.Generated
                    || result.Status
                        == RewardApplicationResultStatusV1
                            .ExactDuplicateNoChange);
        }

        private static bool ClaimAccepted(
            RewardApplicationResultV1 result)
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

        private static string ResultCode(
            RewardApplicationResultV1 result)
        {
            return result == null
                ? "null"
                : (string.IsNullOrWhiteSpace(result.RejectionCode)
                    ? result.Status.ToString()
                    : result.RejectionCode);
        }

        private static CollectedRunRewardTransferChildResultV1 Rejected(
            CollectedRunRewardTransferChildCommandV1 command,
            string diagnostic)
        {
            return new CollectedRunRewardTransferChildResultV1(
                CollectedRunRewardTransferAuthorityStatusV1.Rejected,
                command,
                string.Empty,
                diagnostic);
        }
    }

    /// <summary>
    /// Existing CharacterCompositionCoordinator save seam with exact receipt read-back
    /// verification. No second account store or save protocol is introduced.
    /// </summary>
    public sealed class ProductionCollectedRunRewardTransferPersistenceAdapter :
        ICollectedRunRewardTransferPersistencePortV1
    {
        private readonly CharacterCompositionCoordinatorV1 composition;
        private readonly CollectedRunRewardTransferReceiptAuthorityV1 receipts;
        private readonly StableId selectedCharacterStableId;

        public ProductionCollectedRunRewardTransferPersistenceAdapter(
            CharacterCompositionCoordinatorV1 composition,
            CollectedRunRewardTransferReceiptAuthorityV1 receipts,
            StableId selectedCharacterStableId)
        {
            this.composition = composition
                ?? throw new ArgumentNullException(nameof(composition));
            this.receipts = receipts
                ?? throw new ArgumentNullException(nameof(receipts));
            this.selectedCharacterStableId = selectedCharacterStableId
                ?? throw new ArgumentNullException(
                    nameof(selectedCharacterStableId));
        }

        public bool IsAvailable
        {
            get
            {
                return composition.ActiveRuntime != null
                    && !composition.ActiveRuntime.IsDisposed
                    && composition.ActiveRuntime.Character
                        .CharacterInstanceStableId
                        == selectedCharacterStableId;
            }
        }

        public CollectedRunRewardTransferPersistenceResultV1
            PersistAndVerify(
                StableId saveOperationStableId,
                CollectedRunRewardTransferReceiptV1 receipt)
        {
            if (!IsAvailable || receipt == null)
            {
                return Rejected(
                    "collected-run-transfer-persistence-context-invalid");
            }

            CharacterCompositionResultV1 persisted =
                composition.PersistActive(saveOperationStableId);
            if (persisted == null || !persisted.Succeeded
                || persisted.Account == null
                || persisted.Character == null)
            {
                return Rejected(
                    persisted == null
                        ? "collected-run-transfer-persistence-result-null"
                        : "collected-run-transfer-persistence-rejected:"
                            + persisted.Diagnostic);
            }

            CollectedRunRewardTransferReceiptV1 verified;
            if (!receipts.TryGetByOperation(
                    receipt.OperationStableId,
                    out verified)
                || verified == null
                || !string.Equals(
                    verified.Fingerprint,
                    receipt.Fingerprint,
                    StringComparison.Ordinal))
            {
                return new CollectedRunRewardTransferPersistenceResultV1(
                    CollectedRunRewardTransferPersistenceStatusV1
                        .VerificationMismatch,
                    persisted.Account.Revision,
                    persisted.Account.Fingerprint,
                    persisted.Character.Revision,
                    persisted.Character.Fingerprint,
                    "collected-run-transfer-receipt-readback-mismatch");
            }

            SaveComponentSnapshotV1 receiptComponent;
            if (!persisted.Character.TryGetComponent(
                    CollectedRunRewardTransferReceiptSaveComponentV1
                        .ComponentStableId,
                    out receiptComponent))
            {
                return new CollectedRunRewardTransferPersistenceResultV1(
                    CollectedRunRewardTransferPersistenceStatusV1
                        .VerificationMismatch,
                    persisted.Account.Revision,
                    persisted.Account.Fingerprint,
                    persisted.Character.Revision,
                    persisted.Character.Fingerprint,
                    "collected-run-transfer-receipt-component-missing");
            }

            return new CollectedRunRewardTransferPersistenceResultV1(
                persisted.Status
                        == CharacterCompositionStatusV1.ExactNoChange
                    ? CollectedRunRewardTransferPersistenceStatusV1
                        .AlreadyPersisted
                    : CollectedRunRewardTransferPersistenceStatusV1
                        .PersistedAndVerified,
                persisted.Account.Revision,
                persisted.Account.Fingerprint,
                persisted.Character.Revision,
                persisted.Character.Fingerprint,
                string.Empty);
        }

        private static CollectedRunRewardTransferPersistenceResultV1
            Rejected(string diagnostic)
        {
            return new CollectedRunRewardTransferPersistenceResultV1(
                CollectedRunRewardTransferPersistenceStatusV1.Rejected,
                0L,
                string.Empty,
                0L,
                string.Empty,
                diagnostic);
        }
    }

    /// <summary>
    /// Production entry point adding plan-fingerprint conflict detection ahead of the
    /// generic exactly-once coordinator.
    /// </summary>
    public sealed class ProductionCollectedRunRewardTransferService
    {
        private readonly CollectedRunRewardApplicationPlanV1 plan;
        private readonly ProductionCollectedRunRewardTransferAuthorityAdapter
            authority;
        private readonly CollectedRunRewardTransferCoordinatorV1
            coordinator;

        public ProductionCollectedRunRewardTransferService(
            CollectedRunRewardApplicationPlanV1 plan,
            ProductionCollectedRunRewardTransferAuthorityAdapter authority,
            ICollectedRunRewardTransferPersistencePortV1 persistence)
        {
            this.plan = plan
                ?? throw new ArgumentNullException(nameof(plan));
            this.authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
            coordinator = new CollectedRunRewardTransferCoordinatorV1(
                authority,
                persistence
                ?? throw new ArgumentNullException(nameof(persistence)));
        }

        public CollectedRunRewardTransferResultV1 Apply()
        {
            CollectedRunRewardTransferReceiptV1 existing;
            if (authority.TryGetDurableReceipt(
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
                        CollectedRunRewardTransferStatusV1
                            .ConflictingDuplicate,
                        plan.Batch.TransferOperationStableId,
                        plan.Batch.Fingerprint,
                        plan.Batch.RunStableId,
                        plan.Batch.SelectedCharacterStableId,
                        existing,
                        authority.ExportState(),
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
