using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.CollectedRunRewards
{
    public sealed class CollectedRunStrongboxPersistenceTests
    {
        [UnityTest]
        public IEnumerator PickupToReloadOpeningAndReceiptReplayIsExactlyOnce()
        {
            const string suffix = "strongbox-e2e";
            CharacterLiveGraph graph = CreateGraph(suffix);
            RewardApplicationActions rewardApplication;
            RewardClaimPreparedTransferStore preparedStore;
            RewardClaimTransferReceiptState receipts;
            Assert.That(
                RewardClaimLiveRegistry.TryResolve(
                    graph.Character.CharacterInstanceStableId,
                    out rewardApplication,
                    out preparedStore,
                    out receipts),
                Is.True);

            StableId runId = Id("run-instance." + suffix);
            var end = new EndRunSessionCommand(
                Id("operation.end-" + suffix),
                runId,
                1L,
                MissionRunCompletionState.Completed,
                100L);
            StrongboxDefinition definition =
                graph.StrongboxCatalog.Definitions[0];
            RunSessionCollectedReward pickup = Reward(
                suffix,
                runId,
                definition.TierStableId);
            var generation = new RewardClaimGenerationContext(
                0x5EEDUL,
                1,
                ProgressionContext.Create(
                    1,
                    1,
                    Id("difficulty.normal"),
                    0),
                Fingerprint("event-modifiers-" + suffix));

            RewardClaimPreparedTransfer awaiting;
            string diagnostic;
            Assert.That(
                RewardClaimTransferPreparationFactory
                    .TryCreateAwaitingAcceptedEnd(
                        end,
                        new[] { pickup },
                        graph,
                        rewardApplication,
                        receipts,
                        preparedStore,
                        generation,
                        new RejectingCollectedRunEquipmentPayloadSource(),
                        out awaiting,
                        out diagnostic),
                Is.True,
                diagnostic);

            RewardClaimPreparedTransfer prepared;
            RewardClaimAtomicPlan plan;
            Assert.That(
                RewardClaimTransferPreparationFactory
                    .TryAcceptEndAndBuildPlan(
                        AcceptedEnd(graph, end, pickup),
                        awaiting,
                        graph,
                        rewardApplication,
                        out prepared,
                        out plan,
                        out diagnostic),
                Is.True,
                diagnostic);

            var persistence = new StoreBackedPersistence(preparedStore);
            var authority = new RewardClaimAtomicState(
                graph,
                rewardApplication,
                preparedStore,
                receipts);
            var flow = new RewardClaimTransferFlow(authority, persistence);
            RewardClaimTransferResult applied = flow.Apply(plan);
            RewardClaimTransferResult replay = flow.Apply(plan);

            Assert.That(
                applied.Status,
                Is.EqualTo(RewardClaimTransferStatus.Applied),
                applied.Diagnostic);
            Assert.That(
                replay.Status,
                Is.EqualTo(RewardClaimTransferStatus.ExactReplay),
                replay.Diagnostic);
            Assert.That(receipts.ExportSnapshot().Receipts.Count, Is.EqualTo(1));
            AssertCanonicalBox(graph, pickup);

            PlayerAccountSnapshot durable = Account(graph);
            SavePartValidationResult validation = GameSaveRules.Validate(
                durable,
                tier => tier == definition.TierStableId
                    ? definition.Fingerprint
                    : null);
            Assert.That(validation.Succeeded, Is.True, validation.RejectionCode);

            string encoded = PlayerAccountFileCodec.Encode(durable);
            PlayerAccountSnapshot decoded;
            string rejection;
            Assert.That(
                PlayerAccountFileCodec.TryDecode(
                    encoded,
                    out decoded,
                    out rejection),
                Is.True,
                rejection);

            graph.Dispose();
            CharacterLiveGraph restored = CreateGraph(suffix);
            PlayerAccountRestoreResult restoredResult =
                new PlayerAccountRestoreFlow(
                    validateAggregate: account => GameSaveRules.Validate(
                        account,
                        tier => tier == definition.TierStableId
                            ? definition.Fingerprint
                            : null))
                .Restore(
                    decoded,
                    new[]
                    {
                        new CharacterSaveRestoreBinding(
                            0,
                            restored.Character.CharacterInstanceStableId,
                            restored.SaveAdapters),
                    });
            Assert.That(
                restoredResult.Succeeded,
                Is.True,
                restoredResult.RejectionCode);
            AssertCanonicalBox(restored, pickup);

            RewardApplicationActions restoredRewardApplication;
            RewardClaimPreparedTransferStore restoredPreparedStore;
            RewardClaimTransferReceiptState restoredReceipts;
            Assert.That(
                RewardClaimLiveRegistry.TryResolve(
                    restored.Character.CharacterInstanceStableId,
                    out restoredRewardApplication,
                    out restoredPreparedStore,
                    out restoredReceipts),
                Is.True);
            RewardClaimPreparedTransfer restoredPrepared;
            Assert.That(
                restoredPreparedStore.TryGetByCustody(
                    prepared.CustodyStableId,
                    out restoredPrepared),
                Is.True);
            RewardClaimAtomicPlan restoredPlan;
            Assert.That(
                RewardClaimTransferPreparationFactory.TryBuildPlanFromPrepared(
                    restoredPrepared,
                    restored,
                    restoredRewardApplication,
                    out restoredPlan,
                    out diagnostic),
                Is.True,
                diagnostic);
            int holdingsBeforeReceiptReplay = restored.LoadoutRuntime.Holdings
                .ExportSnapshot().UniqueHoldings.Count;
            RewardClaimTransferResult restoredReplay =
                new RewardClaimTransferFlow(
                    new RewardClaimAtomicState(
                        restored,
                        restoredRewardApplication,
                        restoredPreparedStore,
                        restoredReceipts),
                    new StoreBackedPersistence(restoredPreparedStore))
                .Apply(restoredPlan);
            Assert.That(
                restoredReplay.Status,
                Is.EqualTo(RewardClaimTransferStatus.ExactReplay),
                restoredReplay.Diagnostic);
            Assert.That(
                restored.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Count,
                Is.EqualTo(holdingsBeforeReceiptReplay));
            Assert.That(
                restoredReceipts.ExportSnapshot().Receipts.Count,
                Is.EqualTo(1));

            StrongboxOpenCommand open = StrongboxOpenCommand.Create(
                Id("opening." + suffix),
                runId,
                pickup.GeneratedRewardChildStableId,
                restored.Character.CharacterInstanceStableId,
                MoneyWalletIds.AuthorityStableId,
                restored.ScrapWallet.AuthorityStableId,
                restored.LoadoutRuntime.Holdings.AuthorityStableId);
            StrongboxOpeningResultLive opened =
                restored.StrongboxAuthority.Open(open);
            StrongboxOpeningResultLive openingReplay =
                restored.StrongboxAuthority.Open(open);
            Assert.That(
                opened.Status,
                Is.EqualTo(StrongboxOpeningLiveStatus.Opened),
                opened.RejectionCode);
            Assert.That(
                openingReplay.Status,
                Is.EqualTo(StrongboxOpeningLiveStatus.ExactDuplicateNoChange),
                openingReplay.RejectionCode);
            Assert.That(
                restored.LoadoutRuntime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Any(item => item.InstanceStableId
                        == pickup.GeneratedRewardChildStableId),
                Is.False);

            restored.Dispose();
            yield return null;
        }

        private static CharacterLiveGraph CreateGraph(string suffix)
        {
            StableId character = Id("character-instance." + suffix);
            StableId loadout = Id("loadout-profile.striker");
            PlayerRouteProfilePayload route = PlayerRouteProfilePayload.Create(
                character,
                loadout,
                new StableId[] { null, null, null, null });
            return (CharacterLiveGraph)CharacterLiveGraphFactory
                .CreateVerticalSliceDefaults()
                .CreateStarter(0, character, loadout, suffix, route);
        }

        private static RunSessionCollectedReward Reward(
            string suffix,
            StableId runId,
            StableId tier)
        {
            return new RunSessionCollectedReward(
                Id("pickup." + suffix),
                Id("reward-instance." + suffix),
                Id("grant." + suffix),
                Id("operation.drop-" + suffix),
                Id("terminal-event." + suffix),
                null,
                runId,
                1L,
                Id("source-entity." + suffix),
                Id("source-placement." + suffix),
                1L,
                Id("source-definition." + suffix),
                Id("participant." + suffix),
                RewardGrantKind.Strongbox,
                tier,
                1L,
                Fingerprint("generated-batch-" + suffix),
                Fingerprint("generated-reward-" + suffix),
                Id("room." + suffix),
                2d,
                3d,
                Fingerprint("spawn-" + suffix),
                Fingerprint("available-" + suffix),
                Id("collector-entity." + suffix),
                Id("participant." + suffix),
                Id("operation.collect-" + suffix),
                1L,
                70L);
        }

        private static RunSessionEndResult AcceptedEnd(
            CharacterLiveGraph graph,
            EndRunSessionCommand end,
            RunSessionCollectedReward pickup)
        {
            var collection = new MissionRunStrongboxCollection(
                pickup.ContentStableId,
                pickup.GeneratedRewardChildStableId,
                pickup.GeneratedRewardChildStableId,
                pickup.DropOperationStableId,
                pickup.CollectionOperationStableId,
                graph.LoadoutRuntime.Holdings.Sequence,
                graph.LoadoutRuntime.Holdings.ExportSnapshot().Fingerprint);
            MissionResultPayload mission = MissionResultPayload.Create(
                end.RunStableId,
                graph.RoutePayload,
                MissionRunCompletionState.Completed,
                new[]
                {
                    new MissionRunStrongboxResult(
                        collection,
                        MissionRunStrongboxState.Unopened,
                        null,
                        null),
                },
                1L,
                graph.LoadoutRuntime.Holdings.Sequence,
                graph.LoadoutRuntime.Holdings.ExportSnapshot().Fingerprint,
                graph.StrongboxAuthority.Sequence,
                graph.StrongboxAuthority.ExportSnapshot().Fingerprint);
            var local = new RunLocalStateSnapshot(
                0L,
                new Dictionary<string, long>(),
                new Dictionary<string, long>(),
                new Dictionary<string, long>());
            var receipt = new RunSessionEndReceipt(
                end.RunStableId,
                graph.Character.CharacterInstanceStableId,
                graph.Character.Revision,
                graph.Character.Fingerprint,
                Id("mission-layout.level-1"),
                Id("difficulty.normal"),
                42L,
                Fingerprint("frozen-inputs"),
                Fingerprint("combat-profile"),
                local,
                mission);
            return new RunSessionEndResult(
                RunSessionEndStatus.Ended,
                end,
                receipt,
                string.Empty);
        }

        private static void AssertCanonicalBox(
            CharacterLiveGraph graph,
            RunSessionCollectedReward pickup)
        {
            var holding = graph.LoadoutRuntime.Holdings.ExportSnapshot()
                .UniqueHoldings.Single(item => item.InstanceStableId
                    == pickup.GeneratedRewardChildStableId);
            StrongboxInstanceContext context = graph.StrongboxAuthority
                .ExportSnapshot().Contexts.Single(item => item.InstanceStableId
                    == pickup.GeneratedRewardChildStableId);
            Assert.That(
                holding.Provenance.GrantStableId,
                Is.EqualTo(pickup.GeneratedRewardChildStableId));
            Assert.That(
                context.CollectionProvenanceStableId,
                Is.EqualTo(pickup.CollectionOperationStableId));
            Assert.That(
                context.SourceContextStableId,
                Is.EqualTo(pickup.DropOperationStableId));
        }

        private static PlayerAccountSnapshot Account(CharacterLiveGraph graph)
        {
            var slots = new CharacterInstanceSnapshot[
                PlayerAccountSnapshot.CharacterSlotCount];
            slots[0] = new CharacterInstanceSnapshot(
                graph.Character.CharacterInstanceStableId,
                graph.Character.ClassDefinitionStableId,
                0,
                graph.Character.DisplayName,
                graph.Character.Revision,
                PlayerAccountRestoreFlow.ExportComponents(graph.SaveAdapters));
            return new PlayerAccountSnapshot(
                Id("account.collected-run-strongbox-e2e"),
                1L,
                slots,
                null);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private static string Fingerprint(string material)
        {
            return Strongbox.Fingerprint(material);
        }

        private sealed class StoreBackedPersistence :
            IRewardClaimTransferPersistencePort
        {
            private readonly RewardClaimPreparedTransferStore store;

            public StoreBackedPersistence(
                RewardClaimPreparedTransferStore store)
            {
                this.store = store;
            }

            public bool IsAvailable { get { return true; } }

            public RewardClaimTransferPersistenceResult
                PersistPreparedCustody(RewardClaimPreparedTransfer prepared)
            {
                string diagnostic;
                RewardClaimTransferStateStatus status =
                    store.Upsert(prepared, out diagnostic);
                return Success(
                    status == RewardClaimTransferStateStatus.Applied
                        || status == RewardClaimTransferStateStatus.ExactReplay,
                    RewardClaimTransferPersistenceStatus.PreparedAndVerified,
                    diagnostic);
            }

            public RewardClaimTransferPersistenceResult
                PersistAppliedAndVerify(
                    RewardClaimPreparedTransfer persisted,
                    RewardClaimTransferReceipt receipt)
            {
                string diagnostic;
                RewardClaimTransferStateStatus status =
                    store.Upsert(persisted, out diagnostic);
                return Success(
                    status == RewardClaimTransferStateStatus.Applied
                        || status == RewardClaimTransferStateStatus.ExactReplay,
                    RewardClaimTransferPersistenceStatus.PersistedAndVerified,
                    diagnostic);
            }

            private static RewardClaimTransferPersistenceResult Success(
                bool succeeded,
                RewardClaimTransferPersistenceStatus success,
                string diagnostic)
            {
                return new RewardClaimTransferPersistenceResult(
                    succeeded
                        ? success
                        : RewardClaimTransferPersistenceStatus
                            .RejectedBeforeReplacement,
                    succeeded ? 1L : 0L,
                    succeeded ? Fingerprint("account") : string.Empty,
                    succeeded ? 1L : 0L,
                    succeeded ? Fingerprint("character") : string.Empty,
                    diagnostic);
            }
        }
    }
}
