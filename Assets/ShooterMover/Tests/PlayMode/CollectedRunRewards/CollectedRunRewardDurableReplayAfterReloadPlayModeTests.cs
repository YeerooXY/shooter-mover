using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Strongboxes;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.CollectedRunRewards
{
    public sealed class CollectedRunRewardDurableReplayAfterReloadPlayModeTests
    {
        [UnityTest]
        public IEnumerator PersistedReceiptSurvivesReloadAndPreventsDuplicateApplication()
        {
            StableId characterId = Id("character-instance.durable-replay");
            StableId classId = Id("loadout-profile.striker");
            StableId runId = Id("run-instance.durable-replay");
            StableId equipmentDefinitionId =
                ProductionStarterWeaponCatalogV1
                    .InitialEquipmentDefinitionStableIds[0];
            EquipmentInstance equipment = EquipmentInstance.Create(
                Id("equipment-instance.durable-replay"),
                equipmentDefinitionId,
                11,
                Id("equipment-quality.common"),
                new[]
                {
                    AugmentInstance.Create(
                        Id("augment-instance.durable-replay"),
                        Id("augment-definition.damage"),
                        2,
                        8),
                });
            StableId strongboxInstanceId =
                Id("strongbox-instance.durable-replay");
            StableId strongboxTierId =
                ProductionStrongboxCatalogV1.Tiers[0].TierStableId;

            ProductionCharacterRuntimeGraphFactoryV1 factory =
                ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults();
            PlayerAccountSnapshotV1 durableAccount = CreateAccount(
                factory,
                characterId,
                classId);
            int saveCallbackCount = 0;
            Func<PlayerAccountSnapshotV1, PlayerAccountStoreResultV1> save =
                candidate =>
                {
                    saveCallbackCount++;
                    durableAccount = candidate;
                    return Saved(candidate);
                };

            var initialAccountAuthority =
                new PlayerAccountSaveAuthorityV1(durableAccount);
            var initialComposition = new CharacterCompositionCoordinatorV1(
                initialAccountAuthority,
                factory,
                save);
            Assert.That(initialComposition.Select(0).Succeeded, Is.True);
            var initialGraph = (ProductionCharacterRuntimeGraphV1)
                initialComposition.ActiveRuntime;
            CollectedRunRewardPreparedTransferAuthorityV1 initialPrepared;
            CollectedRunRewardTransferReceiptAuthorityV1 initialReceipts;
            RewardApplicationServiceV1 initialRewardApplication = BindRuntime(
                characterId,
                initialGraph,
                initialComposition,
                out initialPrepared,
                out initialReceipts);

            EndRunSessionCommandV1 endCommand = new EndRunSessionCommandV1(
                Id("operation.end-durable-replay"),
                runId,
                1L,
                MissionRunCompletionStateV1.Completed,
                100L);
            IReadOnlyList<RunSessionCollectedRewardV1> journal = new[]
            {
                Reward(
                    "money",
                    runId,
                    RewardGrantKindV1.Money,
                    MoneyWalletIdsV1.CurrencyStableId,
                    31L,
                    1L),
                Reward(
                    "scrap",
                    runId,
                    RewardGrantKindV1.Scrap,
                    initialGraph.ScrapWallet.CurrencyStableId,
                    12L,
                    2L),
                Reward(
                    "equipment",
                    runId,
                    RewardGrantKindV1.EquipmentReference,
                    equipmentDefinitionId,
                    1L,
                    3L,
                    equipment.InstanceId),
                Reward(
                    "strongbox",
                    runId,
                    RewardGrantKindV1.Strongbox,
                    strongboxTierId,
                    1L,
                    4L,
                    strongboxInstanceId),
            };
            CollectedRunRewardPreparedTransferV1 awaiting;
            string diagnostic;
            Assert.That(CollectedRunRewardTransferPreparationFactoryV2
                .TryCreateAwaitingAcceptedEnd(
                    endCommand,
                    journal,
                    initialGraph,
                    initialRewardApplication,
                    initialReceipts,
                    initialPrepared,
                    GenerationContext(),
                    new ExactEquipmentPayloadSource(equipment),
                    out awaiting,
                    out diagnostic), Is.True, diagnostic);

            var initialPersistence =
                new ProductionCollectedRunRewardPersistenceV2(
                    initialComposition,
                    initialPrepared,
                    initialReceipts,
                    characterId);
            Assert.That(initialPersistence.PersistPreparedCustody(awaiting)
                .Succeeded, Is.True);
            CollectedRunRewardPreparedTransferV1 prepared;
            CollectedRunRewardAtomicPlanV2 originalPlan;
            Assert.That(CollectedRunRewardTransferPreparationFactoryV2
                .TryAcceptEndAndBuildPlan(
                    AcceptedEnd(
                        awaiting,
                        initialGraph,
                        endCommand),
                    awaiting,
                    initialGraph,
                    initialRewardApplication,
                    out prepared,
                    out originalPlan,
                    out diagnostic), Is.True, diagnostic);
            Assert.That(initialPersistence.PersistPreparedCustody(prepared)
                .Succeeded, Is.True);

            var initialService =
                new ProductionCollectedRunRewardTransferServiceV2(
                    originalPlan,
                    new ProductionCollectedRunRewardAtomicAuthorityV2(
                        initialGraph,
                        initialRewardApplication,
                        initialPrepared,
                        initialReceipts),
                    initialPersistence);
            CollectedRunRewardTransferResultV1 first =
                initialService.Apply();
            Assert.That(first.Status,
                Is.EqualTo(CollectedRunRewardTransferStatusV1.Applied));
            Assert.That(first.Receipt, Is.Not.Null);
            string durableReceiptFingerprint = first.Receipt.Fingerprint;

            initialComposition.Dispose();
            ProductionCollectedRunRewardRuntimeRegistryV2.Release(characterId);

            var reloadedAccountAuthority =
                new PlayerAccountSaveAuthorityV1(durableAccount);
            var reloadedComposition = new CharacterCompositionCoordinatorV1(
                reloadedAccountAuthority,
                factory,
                save);
            Assert.That(reloadedComposition.Select(0).Succeeded, Is.True);
            var reloadedGraph = (ProductionCharacterRuntimeGraphV1)
                reloadedComposition.ActiveRuntime;
            CollectedRunRewardPreparedTransferAuthorityV1 reloadedPrepared;
            CollectedRunRewardTransferReceiptAuthorityV1 reloadedReceipts;
            RewardApplicationServiceV1 reloadedRewardApplication = BindRuntime(
                characterId,
                reloadedGraph,
                reloadedComposition,
                out reloadedPrepared,
                out reloadedReceipts);

            CollectedRunRewardTransferReceiptV1 restoredReceipt;
            Assert.That(reloadedReceipts.TryGetByOperation(
                originalPlan.TransferOperationStableId,
                out restoredReceipt), Is.True);
            Assert.That(restoredReceipt.Fingerprint,
                Is.EqualTo(durableReceiptFingerprint));

            long moneySequence = reloadedGraph.MoneyWallet.Sequence;
            long scrapSequence = reloadedGraph.ScrapWallet.Sequence;
            long holdingsSequence =
                reloadedGraph.LoadoutRuntime.Holdings.Sequence;
            long strongboxSequence =
                reloadedGraph.StrongboxAuthority.Sequence;
            int savesBeforeReplay = saveCallbackCount;

            var replayService =
                new ProductionCollectedRunRewardTransferServiceV2(
                    originalPlan,
                    new ProductionCollectedRunRewardAtomicAuthorityV2(
                        reloadedGraph,
                        reloadedRewardApplication,
                        reloadedPrepared,
                        reloadedReceipts),
                    new ProductionCollectedRunRewardPersistenceV2(
                        reloadedComposition,
                        reloadedPrepared,
                        reloadedReceipts,
                        characterId));
            CollectedRunRewardTransferResultV1 replay =
                replayService.Apply();

            Assert.That(replay.Status,
                Is.EqualTo(CollectedRunRewardTransferStatusV1.ExactReplay));
            Assert.That(replay.Receipt, Is.Not.Null);
            Assert.That(replay.Receipt.Fingerprint,
                Is.EqualTo(durableReceiptFingerprint));
            Assert.That(reloadedGraph.MoneyWallet.Sequence,
                Is.EqualTo(moneySequence));
            Assert.That(reloadedGraph.ScrapWallet.Sequence,
                Is.EqualTo(scrapSequence));
            Assert.That(reloadedGraph.LoadoutRuntime.Holdings.Sequence,
                Is.EqualTo(holdingsSequence));
            Assert.That(reloadedGraph.StrongboxAuthority.Sequence,
                Is.EqualTo(strongboxSequence));
            Assert.That(saveCallbackCount, Is.EqualTo(savesBeforeReplay));

            reloadedComposition.Dispose();
            ProductionCollectedRunRewardRuntimeRegistryV2.Release(characterId);
            yield return null;
        }

        private static PlayerAccountSnapshotV1 CreateAccount(
            ProductionCharacterRuntimeGraphFactoryV1 factory,
            StableId characterId,
            StableId classId)
        {
            PlayerRouteProfilePayloadV1 route =
                PlayerRouteProfilePayloadV1.Create(
                    characterId,
                    classId,
                    new[]
                    {
                        ProductionStarterWeaponCatalogV1
                            .BlasterEquipmentInstanceStableId,
                        ProductionStarterWeaponCatalogV1
                            .ShotgunEquipmentInstanceStableId,
                        ProductionStarterWeaponCatalogV1
                            .RocketEquipmentInstanceStableId,
                        ProductionStarterWeaponCatalogV1
                            .ArcEquipmentInstanceStableId,
                    });
            ICharacterRuntimeGraphV1 starter = factory.CreateStarter(
                0,
                characterId,
                classId,
                "Durable Replay Pilot",
                route);
            IReadOnlyList<SaveComponentSnapshotV1> components =
                PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    starter.SaveAdapters);
            starter.Dispose();
            ProductionCollectedRunRewardRuntimeRegistryV2.Release(characterId);
            var character = new CharacterInstanceSnapshotV1(
                characterId,
                classId,
                0,
                "Durable Replay Pilot",
                0L,
                components);
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            slots[0] = character;
            return new PlayerAccountSnapshotV1(
                Id("account.durable-replay"),
                0L,
                slots,
                null);
        }

        private static RewardApplicationServiceV1 BindRuntime(
            StableId characterId,
            ProductionCharacterRuntimeGraphV1 graph,
            CharacterCompositionCoordinatorV1 composition,
            out CollectedRunRewardPreparedTransferAuthorityV1 prepared,
            out CollectedRunRewardTransferReceiptAuthorityV1 receipts)
        {
            var rewardApplication = new RewardApplicationServiceV1(
                Id("authority.reward-application-durable-replay"),
                new MoneyRewardChildAuthorityV1(graph.MoneyWallet),
                new ScrapRewardChildAuthorityV1(graph.ScrapWallet),
                new PlayerHoldingsRewardChildAuthorityV1(
                    graph.LoadoutRuntime.Holdings,
                    graph.LoadoutRuntime.CatalogAdapter));
            ProductionCollectedRunRewardRuntimeRegistryV2
                .BindRewardApplication(characterId, rewardApplication);
            ProductionCollectedRunRewardRuntimeRegistryV2.BindRuntime(
                graph,
                composition);
            RewardApplicationServiceV1 resolved;
            Assert.That(ProductionCollectedRunRewardRuntimeRegistryV2
                .TryResolve(
                    characterId,
                    out resolved,
                    out prepared,
                    out receipts), Is.True);
            Assert.That(resolved, Is.SameAs(rewardApplication));
            return rewardApplication;
        }

        private static CollectedRunRewardGenerationContextV2
            GenerationContext()
        {
            return new CollectedRunRewardGenerationContextV2(
                0xD00DUL,
                2,
                ProgressionContext.Create(
                    11,
                    9,
                    Id("difficulty.normal"),
                    0,
                    new[] { Id("progression-tag.campaign") }),
                Fingerprint("event-modifiers-durable-replay"));
        }

        private static RunSessionEndResultV1 AcceptedEnd(
            CollectedRunRewardPreparedTransferV1 awaiting,
            ProductionCharacterRuntimeGraphV1 graph,
            EndRunSessionCommandV1 command)
        {
            MissionResultPayloadV1 mission = MissionResultPayloadV1.Create(
                awaiting.RunStableId,
                graph.RoutePayload,
                MissionRunCompletionStateV1.Completed,
                Array.Empty<MissionRunStrongboxResultV1>(),
                1L,
                graph.LoadoutRuntime.Holdings.Sequence,
                graph.LoadoutRuntime.Holdings.ExportSnapshot().Fingerprint,
                graph.StrongboxAuthority.Sequence,
                graph.StrongboxAuthority.ExportSnapshot().Fingerprint);
            var receipt = new RunSessionEndReceiptV1(
                awaiting.RunStableId,
                awaiting.SelectedCharacterStableId,
                awaiting.ExpectedCharacterRevision,
                awaiting.ExpectedCharacterFingerprint,
                Id("mission-layout.level-1"),
                Id("difficulty.normal"),
                42L,
                Fingerprint("frozen-inputs-durable-replay"),
                Fingerprint("combat-profile-durable-replay"),
                new RunLocalStateSnapshotV1(
                    0L,
                    new Dictionary<string, long>(),
                    new Dictionary<string, long>(),
                    new Dictionary<string, long>()),
                mission);
            return new RunSessionEndResultV1(
                RunSessionEndStatusV1.Ended,
                command,
                receipt,
                string.Empty);
        }

        private static RunSessionCollectedRewardV1 Reward(
            string suffix,
            StableId runId,
            RewardGrantKindV1 kind,
            StableId contentId,
            long quantity,
            long collectionOrder,
            StableId exactRewardInstanceId = null)
        {
            StableId rewardInstanceId = exactRewardInstanceId
                ?? Id("reward-instance.durable-replay-" + suffix);
            return new RunSessionCollectedRewardV1(
                Id("pickup.durable-replay-" + suffix),
                rewardInstanceId,
                Id("grant.durable-replay-" + suffix),
                Id("operation.drop-durable-replay-" + suffix),
                Id("terminal-event.durable-replay-" + suffix),
                null,
                runId,
                1L,
                Id("source-entity.durable-replay-" + suffix),
                Id("source-placement.durable-replay-" + suffix),
                1L,
                Id("source-definition.durable-replay-" + suffix),
                Id("participant.durable-replay"),
                kind,
                contentId,
                quantity,
                Fingerprint("generated-batch-durable-replay"),
                Fingerprint("generated-reward-durable-replay-" + suffix),
                Id("room.durable-replay"),
                0d,
                0d,
                Fingerprint("spawn-durable-replay-" + suffix),
                Fingerprint("available-durable-replay-" + suffix),
                Id("collector-entity.durable-replay"),
                Id("participant.durable-replay"),
                Id("operation.collect-durable-replay-" + suffix),
                collectionOrder,
                50L + collectionOrder);
        }

        private static PlayerAccountStoreResultV1 Saved(
            PlayerAccountSnapshotV1 snapshot)
        {
            return new PlayerAccountStoreResultV1(
                PlayerAccountStoreStatusV1.Saved,
                string.Empty,
                snapshot);
        }

        private static string Fingerprint(string material)
        {
            return StrongboxCanonicalV1.Fingerprint(material);
        }

        private static StableId Id(string canonical)
        {
            return StableId.Parse(canonical);
        }

        private sealed class ExactEquipmentPayloadSource :
            ICollectedRunEquipmentPayloadSourceV2
        {
            private readonly EquipmentInstance equipment;

            public ExactEquipmentPayloadSource(EquipmentInstance equipment)
            {
                this.equipment = equipment;
            }

            public bool TryResolveExact(
                StableId rewardInstanceStableId,
                StableId equipmentDefinitionStableId,
                out EquipmentInstance resolved,
                out string diagnostic)
            {
                resolved = null;
                if (equipment.InstanceId != rewardInstanceStableId)
                {
                    diagnostic = "durable-replay-equipment-instance-mismatch";
                    return false;
                }
                if (equipment.DefinitionId != equipmentDefinitionStableId)
                {
                    diagnostic = "durable-replay-equipment-definition-mismatch";
                    return false;
                }
                resolved = equipment;
                diagnostic = string.Empty;
                return true;
            }
        }
    }
}
