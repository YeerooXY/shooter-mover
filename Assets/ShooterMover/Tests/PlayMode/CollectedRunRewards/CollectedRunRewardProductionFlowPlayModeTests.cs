#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.RunPickups;
using ShooterMover.TerminalDropBinding;
using ShooterMover.UI.ProductionFlow;
using ShooterMover.UnityAdapters.Rewards.RunPickups;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.CollectedRunRewards
{
    public sealed class CollectedRunRewardProductionFlowPlayModeTests
    {
        private readonly List<UnityEngine.Object> objects =
            new List<UnityEngine.Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ProductionCollectedRunRewardResultsBridge.Clear();
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index] != null)
                    UnityEngine.Object.Destroy(objects[index]);
            }
            objects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PhysicalCollectedRewardsPersistExactlyOnceAcrossResultsHubAndReload()
        {
            const long expectedMoney = 25L;
            const long expectedScrap = 9L;
            const long uncollectedMoney = 99L;
            StableId character = Id("character-instance.production-flow");
            StableId otherCharacter = Id("character-instance.production-flow-other");
            StableId classId = Id("loadout-profile.striker");
            StableId run = Id("run-instance.production-flow-first");
            StableId participant = Id("participant.production-flow");
            StableId actor = Id("actor.production-flow");
            StableId room = Id("room.production-flow");
            StableId equipmentDefinition =
                ProductionStarterWeaponCatalogV1
                    .InitialEquipmentDefinitionStableIds[0];
            StableId equipmentInstance =
                Id("equipment-instance.production-flow-first");
            StableId secondEquipmentInstance =
                Id("equipment-instance.production-flow-second");
            StableId strongboxInstance =
                Id("strongbox-instance.production-flow");
            StableId strongboxTier =
                ProductionStrongboxCatalogV1.Tiers[0].TierStableId;
            var equipment = EquipmentInstance.Create(
                equipmentInstance,
                equipmentDefinition,
                5,
                Id("equipment-quality.common"),
                Array.Empty<AugmentInstance>());
            var secondEquipment = EquipmentInstance.Create(
                secondEquipmentInstance,
                equipmentDefinition,
                6,
                Id("equipment-quality.common"),
                Array.Empty<AugmentInstance>());

            ProductionCharacterRuntimeGraphFactoryV1 factory =
                ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults();
            CharacterInstanceSnapshotV1 firstCharacter = CreateCharacter(
                factory,
                0,
                character,
                classId,
                "Primary Pilot");
            CharacterInstanceSnapshotV1 secondCharacter = CreateCharacter(
                factory,
                1,
                otherCharacter,
                classId,
                "Other Pilot");
            string untouchedOtherFingerprint = secondCharacter.Fingerprint;
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            slots[0] = firstCharacter;
            slots[1] = secondCharacter;
            PlayerAccountSnapshotV1 durable = new PlayerAccountSnapshotV1(
                Id("account.production-flow"),
                0L,
                slots,
                null);
            int storeCalls = 0;
            Func<PlayerAccountSnapshotV1, PlayerAccountStoreResultV1> save =
                candidate =>
                {
                    storeCalls++;
                    durable = candidate;
                    return Saved(candidate);
                };
            var accountAuthority = new PlayerAccountSaveAuthorityV1(durable);
            var composition = new CharacterCompositionCoordinatorV1(
                accountAuthority,
                factory,
                save);
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var graph = (ProductionCharacterRuntimeGraphV1)
                composition.ActiveRuntime;
            RewardApplicationServiceV1 rewardApplication = BindRewardRuntime(
                character,
                graph,
                composition,
                out CollectedRunRewardPreparedTransferAuthorityV1 preparedAuthority,
                out CollectedRunRewardTransferReceiptAuthorityV1 receipts);

            var journal = new RecordingRunJournal(
                run,
                actor,
                participant);
            GameObject runtime = Track(new GameObject(
                "Production pickup and transfer runtime"));
            RunPickupSourcePositionRegistry2D positions =
                runtime.AddComponent<RunPickupSourcePositionRegistry2D>();
            var pickupPort = new ExistingRunSessionPickupPortV1(journal);
            var pickupAuthority = new RunLocalPickupAuthorityV1(
                pickupPort,
                positions);
            var pickupConsumer = new PendingTerminalDropPickupConsumerV1(
                pickupAuthority);
            RunPickupAuthorityHost2D host =
                runtime.AddComponent<RunPickupAuthorityHost2D>();
            host.Configure(pickupAuthority);
            RunPickupPresentationRegistry2D registry =
                runtime.AddComponent<RunPickupPresentationRegistry2D>();
            registry.Configure(new[]
            {
                Presentation(RewardGrantKindV1.Money, "Money"),
                Presentation(RewardGrantKindV1.Scrap, "Scrap"),
                Presentation(RewardGrantKindV1.EquipmentReference, "Equipment"),
                Presentation(RewardGrantKindV1.Strongbox, "Strongbox"),
            });
            RunPickupPresenter2D presenter =
                runtime.AddComponent<RunPickupPresenter2D>();
            presenter.Configure(host, registry, runtime.transform);

            StableId enemyEntity = Id("enemy-entity.production-flow");
            StableId enemyPlacement = Id("placement.enemy-production-flow");
            StableId propEntity = Id("prop-entity.production-flow");
            StableId propPlacement = Id("placement.prop-production-flow");
            string positionDiagnostic;
            Assert.That(positions.Register(
                run,
                1L,
                enemyEntity,
                enemyPlacement,
                room,
                Vector3.zero,
                Fingerprint("enemy-position"),
                out positionDiagnostic), Is.True, positionDiagnostic);
            Assert.That(positions.Register(
                run,
                1L,
                propEntity,
                propPlacement,
                room,
                new Vector3(20f, 0f, 0f),
                Fingerprint("prop-position"),
                out positionDiagnostic), Is.True, positionDiagnostic);

            GeneratedTerminalDropResultV1 enemyDrop = GeneratedDrop(
                "enemy",
                TerminalDropFactKindIdsV1.EnemyDeath,
                run,
                participant,
                actor,
                enemyEntity,
                enemyPlacement,
                new[]
                {
                    Child("enemy-money", 0, RewardGrantKindV1.Money,
                        MoneyWalletIdsV1.CurrencyStableId, expectedMoney),
                    Child("enemy-equipment", 1,
                        RewardGrantKindV1.EquipmentReference,
                        equipmentDefinition, 1L, equipmentInstance),
                    Child("enemy-box", 2, RewardGrantKindV1.Strongbox,
                        strongboxTier, 1L, strongboxInstance),
                });
            GeneratedTerminalDropResultV1 propDrop = GeneratedDrop(
                "prop",
                TerminalDropFactKindIdsV1.PropDestruction,
                run,
                participant,
                actor,
                propEntity,
                propPlacement,
                new[]
                {
                    Child("prop-scrap", 0, RewardGrantKindV1.Scrap,
                        graph.ScrapWallet.CurrencyStableId, expectedScrap),
                    Child("prop-left-money", 1, RewardGrantKindV1.Money,
                        MoneyWalletIdsV1.CurrencyStableId,
                        uncollectedMoney),
                });
            var admission = new PendingTerminalDropAdmissionAuthorityV1();
            Assert.That(pickupConsumer.Consume(admission.Admit(enemyDrop)).Succeeded,
                Is.True);
            Assert.That(pickupConsumer.Consume(admission.Admit(propDrop)).Succeeded,
                Is.True);
            presenter.Synchronize(room);

            RunPickupSnapshotV1 leftOnFloor = pickupAuthority
                .ExportAvailablePickups()
                .Single(item => item.Reward.RewardInstanceStableId
                    == Id("terminal-drop-child.prop-left-money"));
            RunRewardPickup2D leftView;
            Assert.That(presenter.TryGetView(
                leftOnFloor.PickupStableId,
                out leftView), Is.True);
            leftView.gameObject.SetActive(false);

            GameObject player = Track(new GameObject(
                "Production physical pickup collector"));
            RunPickupCollector2D collector =
                player.AddComponent<RunPickupCollector2D>();
            collector.Configure(actor, participant);
            CircleCollider2D playerCollider =
                player.AddComponent<CircleCollider2D>();
            playerCollider.radius = 4f;
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;
            player.transform.position = Vector3.zero;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            player.transform.position = new Vector3(20f, 0f, 0f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            IReadOnlyList<RunSessionCollectedRewardV1> collected =
                journal.ExportCollectedRunRewards();
            Assert.That(collected.Count, Is.EqualTo(4));
            Assert.That(pickupAuthority.CollectedPickupCount, Is.EqualTo(4));
            Assert.That(pickupAuthority.ExportAvailablePickups().Count,
                Is.EqualTo(1));
            Assert.That(collected.Any(item =>
                item.GeneratedRewardChildStableId
                    == leftOnFloor.Reward.RewardInstanceStableId), Is.False);

            var payloads = new ExactEquipmentPayloadSource(equipment);
            CollectedRunRewardGenerationContextV2 generation = Generation(
                "first");
            var endCommand = new EndRunSessionCommandV1(
                Id("operation.end-production-flow-first"),
                run,
                1L,
                MissionRunCompletionStateV1.Completed,
                100L);
            CollectedRunRewardPreparedTransferV1 awaiting;
            string diagnostic;
            Assert.That(CollectedRunRewardTransferPreparationFactoryV2
                .TryCreateAwaitingAcceptedEnd(
                    endCommand,
                    collected,
                    graph,
                    rewardApplication,
                    receipts,
                    preparedAuthority,
                    generation,
                    payloads,
                    out awaiting,
                    out diagnostic), Is.True, diagnostic);
            var persistence = new ProductionCollectedRunRewardPersistenceV2(
                composition,
                preparedAuthority,
                receipts,
                character);
            Assert.That(persistence.PersistPreparedCustody(awaiting).Succeeded,
                Is.True);
            RunSessionEndResultV1 acceptedEnd = AcceptedEnd(
                "first",
                awaiting,
                graph,
                endCommand);
            CollectedRunRewardPreparedTransferV1 prepared;
            CollectedRunRewardAtomicPlanV2 plan;
            Assert.That(CollectedRunRewardTransferPreparationFactoryV2
                .TryAcceptEndAndBuildPlan(
                    acceptedEnd,
                    awaiting,
                    graph,
                    rewardApplication,
                    out prepared,
                    out plan,
                    out diagnostic), Is.True, diagnostic);
            Assert.That(persistence.PersistPreparedCustody(prepared).Succeeded,
                Is.True);
            var transfer = new ProductionCollectedRunRewardTransferServiceV2(
                plan,
                new ProductionCollectedRunRewardAtomicAuthorityV2(
                    graph,
                    rewardApplication,
                    preparedAuthority,
                    receipts),
                persistence);
            CollectedRunRewardTransferResultV1 applied = transfer.Apply();
            Assert.That(applied.Status,
                Is.EqualTo(CollectedRunRewardTransferStatusV1.Applied));
            ProductionCollectedRunRewardResultsBridge.Publish(prepared, applied);
            string firstResultsFingerprint =
                ProductionCollectedRunRewardResultsBridge.Current.Fingerprint;
            long moneySequence = graph.MoneyWallet.Sequence;
            long scrapSequence = graph.ScrapWallet.Sequence;
            long holdingsSequence = graph.LoadoutRuntime.Holdings.Sequence;
            int callsAfterFirstTransfer = storeCalls;

            CollectedRunRewardTransferResultV1 replay = transfer.Apply();
            ProductionCollectedRunRewardResultsBridge.Publish(prepared, replay);
            Assert.That(replay.Status,
                Is.EqualTo(CollectedRunRewardTransferStatusV1.ExactReplay));
            Assert.That(graph.MoneyWallet.Sequence, Is.EqualTo(moneySequence));
            Assert.That(graph.ScrapWallet.Sequence, Is.EqualTo(scrapSequence));
            Assert.That(graph.LoadoutRuntime.Holdings.Sequence,
                Is.EqualTo(holdingsSequence));
            Assert.That(storeCalls, Is.EqualTo(callsAfterFirstTransfer));
            Assert.That(ProductionCollectedRunRewardResultsBridge.Current
                .ReceiptFingerprint, Is.EqualTo(applied.Receipt.Fingerprint));
            Assert.That(firstResultsFingerprint, Is.Not.Empty);

            composition.Dispose();
            ProductionCollectedRunRewardRuntimeRegistryV2.Release(character);
            var reloadAuthority = new PlayerAccountSaveAuthorityV1(durable);
            var reloadComposition = new CharacterCompositionCoordinatorV1(
                reloadAuthority,
                factory,
                save);
            Assert.That(reloadComposition.Select(0).Succeeded, Is.True);
            var reloadedGraph = (ProductionCharacterRuntimeGraphV1)
                reloadComposition.ActiveRuntime;
            RewardApplicationServiceV1 reloadedReward = BindRewardRuntime(
                character,
                reloadedGraph,
                reloadComposition,
                out CollectedRunRewardPreparedTransferAuthorityV1
                    reloadedPrepared,
                out CollectedRunRewardTransferReceiptAuthorityV1
                    reloadedReceipts);

            Assert.That(reloadedGraph.MoneyWallet.CurrentSnapshot.Balance,
                Is.EqualTo(expectedMoney));
            Assert.That(reloadedGraph.ScrapWallet.Balance,
                Is.EqualTo(expectedScrap));
            UniqueHoldingSnapshotV1 exactEquipment;
            Assert.That(reloadedGraph.LoadoutRuntime.Holdings.TryGetUnique(
                equipmentInstance,
                out exactEquipment), Is.True);
            Assert.That(exactEquipment.Equipment.InstanceId,
                Is.EqualTo(equipmentInstance));
            Assert.That(exactEquipment.Equipment.Fingerprint,
                Is.EqualTo(equipment.Fingerprint));
            UniqueHoldingSnapshotV1 ownedBox;
            Assert.That(reloadedGraph.LoadoutRuntime.Holdings.TryGetUnique(
                strongboxInstance,
                out ownedBox), Is.True);
            Assert.That(reloadedGraph.StrongboxAuthority.ExportSnapshot()
                .Contexts.Any(item => item.InstanceStableId
                    == strongboxInstance), Is.True);
            Assert.That(reloadAuthority.Current.CharacterAt(1).Fingerprint,
                Is.EqualTo(untouchedOtherFingerprint));
            Assert.That(reloadedGraph.MoneyWallet.CurrentSnapshot.Balance,
                Is.Not.EqualTo(expectedMoney + uncollectedMoney));

            CollectedRunRewardTransferResultV1 secondRun = ApplyEquipmentOnlyRun(
                "second",
                Id("run-instance.production-flow-second"),
                secondEquipment,
                reloadedGraph,
                reloadComposition,
                reloadedReward,
                reloadedPrepared,
                reloadedReceipts);
            Assert.That(secondRun.Succeeded, Is.True);
            UniqueHoldingSnapshotV1 firstHeld;
            UniqueHoldingSnapshotV1 secondHeld;
            Assert.That(reloadedGraph.LoadoutRuntime.Holdings.TryGetUnique(
                equipmentInstance,
                out firstHeld), Is.True);
            Assert.That(reloadedGraph.LoadoutRuntime.Holdings.TryGetUnique(
                secondEquipmentInstance,
                out secondHeld), Is.True);
            Assert.That(firstHeld.Equipment.DefinitionId,
                Is.EqualTo(secondHeld.Equipment.DefinitionId));
            Assert.That(firstHeld.Equipment.InstanceId,
                Is.Not.EqualTo(secondHeld.Equipment.InstanceId));

            StrongboxOpenCommandV1 openCommand = StrongboxOpenCommandV1.Create(
                Id("opening.production-flow"),
                Id("run.production-flow-opening"),
                strongboxInstance,
                character,
                MoneyWalletIdsV1.AuthorityStableId,
                reloadedGraph.ScrapWallet.AuthorityStableId,
                reloadedGraph.LoadoutRuntime.Holdings.AuthorityStableId);
            StrongboxOpeningResultRuntimeV1 opened =
                reloadedGraph.StrongboxAuthority.Open(openCommand);
            long sequenceAfterOpen = reloadedGraph.StrongboxAuthority.Sequence;
            StrongboxOpeningResultRuntimeV1 duplicateOpen =
                reloadedGraph.StrongboxAuthority.Open(openCommand);
            Assert.That(opened.Status,
                Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            Assert.That(duplicateOpen.Status,
                Is.EqualTo(
                    StrongboxOpeningRuntimeStatusV1.ExactDuplicateNoChange));
            Assert.That(reloadedGraph.StrongboxAuthority.Sequence,
                Is.EqualTo(sequenceAfterOpen));
            Assert.That(reloadedGraph.LoadoutRuntime.Holdings.TryGetUnique(
                strongboxInstance,
                out ownedBox), Is.False);
            Assert.That(reloadAuthority.Current.CharacterAt(1).Fingerprint,
                Is.EqualTo(untouchedOtherFingerprint));

            reloadComposition.Dispose();
            ProductionCollectedRunRewardRuntimeRegistryV2.Release(character);
        }

        private RunPickupPresentationEntryV1 Presentation(
            RewardGrantKindV1 kind,
            string label)
        {
            GameObject prefab = Track(new GameObject(label + " Pickup Prefab"));
            prefab.SetActive(false);
            var entry = new RunPickupPresentationEntryV1();
            entry.Configure(
                kind,
                null,
                prefab,
                null,
                Vector3.one,
                0.75f,
                label);
            return entry;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            objects.Add(value);
            return value;
        }

        private static RewardApplicationServiceV1 BindRewardRuntime(
            StableId character,
            ProductionCharacterRuntimeGraphV1 graph,
            CharacterCompositionCoordinatorV1 composition,
            out CollectedRunRewardPreparedTransferAuthorityV1 prepared,
            out CollectedRunRewardTransferReceiptAuthorityV1 receipts)
        {
            var rewardApplication = new RewardApplicationServiceV1(
                Id("authority.collected-run-flow-reward-application"),
                new MoneyRewardChildAuthorityV1(graph.MoneyWallet),
                new ScrapRewardChildAuthorityV1(graph.ScrapWallet),
                new PlayerHoldingsRewardChildAuthorityV1(
                    graph.LoadoutRuntime.Holdings,
                    graph.LoadoutRuntime.CatalogAdapter));
            ProductionCollectedRunRewardRuntimeRegistryV2
                .BindRewardApplication(character, rewardApplication);
            ProductionCollectedRunRewardRuntimeRegistryV2.BindRuntime(
                graph,
                composition);
            RewardApplicationServiceV1 resolved;
            Assert.That(ProductionCollectedRunRewardRuntimeRegistryV2.TryResolve(
                character,
                out resolved,
                out prepared,
                out receipts), Is.True);
            Assert.That(resolved, Is.SameAs(rewardApplication));
            return rewardApplication;
        }

        private static CharacterInstanceSnapshotV1 CreateCharacter(
            ProductionCharacterRuntimeGraphFactoryV1 factory,
            int slot,
            StableId character,
            StableId classId,
            string displayName)
        {
            PlayerRouteProfilePayloadV1 route =
                PlayerRouteProfilePayloadV1.Create(
                    character,
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
                slot,
                character,
                classId,
                displayName,
                route);
            IReadOnlyList<SaveComponentSnapshotV1> components =
                PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    starter.SaveAdapters);
            starter.Dispose();
            ProductionCollectedRunRewardRuntimeRegistryV2.Release(character);
            return new CharacterInstanceSnapshotV1(
                character,
                classId,
                slot,
                displayName,
                0L,
                components);
        }

        private static GeneratedTerminalDropRewardV1 Child(
            string suffix,
            int index,
            RewardGrantKindV1 kind,
            StableId content,
            long quantity,
            StableId exactInstance = null)
        {
            return new GeneratedTerminalDropRewardV1(
                exactInstance ?? Id("terminal-drop-child." + suffix),
                index,
                Id("grant." + suffix),
                kind,
                content,
                quantity);
        }

        private static GeneratedTerminalDropResultV1 GeneratedDrop(
            string suffix,
            StableId factKind,
            StableId run,
            StableId participant,
            StableId actor,
            StableId sourceEntity,
            StableId sourcePlacement,
            IReadOnlyList<GeneratedTerminalDropRewardV1> children)
        {
            StableId operation = Id("operation.drop-" + suffix);
            StableId profile = Id("drop-profile." + suffix);
            var source = new TerminalDropSourceFactV1(
                factKind,
                Id("terminal-event." + suffix),
                Id("triggering-event." + suffix),
                run,
                1L,
                sourceEntity,
                sourcePlacement,
                1L,
                Id("source-definition." + suffix),
                participant,
                actor,
                Id("damage-channel.kinetic"),
                profile,
                Fingerprint("source-context-" + suffix),
                Fingerprint("definition-" + suffix),
                Fingerprint("upstream-" + suffix));
            RewardOperationRequestV1 request = RewardOperationRequestV1.Create(
                run,
                sourceEntity,
                operation,
                Id("commitment." + suffix),
                profile,
                Fingerprint("request-context-" + suffix));
            return new GeneratedTerminalDropResultV1(
                TerminalDropBindingStatusV1.Accepted,
                TerminalDropRejectionCodeV1.None,
                source,
                profile,
                request,
                123UL,
                null,
                children,
                Fingerprint("drop-batch-" + suffix),
                string.Empty);
        }

        private static CollectedRunRewardGenerationContextV2 Generation(
            string suffix)
        {
            return new CollectedRunRewardGenerationContextV2(
                123UL,
                2,
                ProgressionContext.Create(
                    12,
                    8,
                    Id("difficulty.normal"),
                    0,
                    new[] { Id("progression-tag.campaign") }),
                Fingerprint("event-modifier-" + suffix));
        }

        private static RunSessionEndResultV1 AcceptedEnd(
            string suffix,
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
                Fingerprint("frozen-inputs-" + suffix),
                Fingerprint("combat-profile-" + suffix),
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

        private static CollectedRunRewardTransferResultV1 ApplyEquipmentOnlyRun(
            string suffix,
            StableId run,
            EquipmentInstance equipment,
            ProductionCharacterRuntimeGraphV1 graph,
            CharacterCompositionCoordinatorV1 composition,
            RewardApplicationServiceV1 rewardApplication,
            CollectedRunRewardPreparedTransferAuthorityV1 preparedAuthority,
            CollectedRunRewardTransferReceiptAuthorityV1 receipts)
        {
            RunSessionCollectedRewardV1 reward = DirectEquipmentReward(
                suffix,
                run,
                equipment);
            var endCommand = new EndRunSessionCommandV1(
                Id("operation.end-" + suffix),
                run,
                1L,
                MissionRunCompletionStateV1.Completed,
                100L);
            CollectedRunRewardPreparedTransferV1 awaiting;
            string diagnostic;
            Assert.That(CollectedRunRewardTransferPreparationFactoryV2
                .TryCreateAwaitingAcceptedEnd(
                    endCommand,
                    new[] { reward },
                    graph,
                    rewardApplication,
                    receipts,
                    preparedAuthority,
                    Generation(suffix),
                    new ExactEquipmentPayloadSource(equipment),
                    out awaiting,
                    out diagnostic), Is.True, diagnostic);
            var persistence = new ProductionCollectedRunRewardPersistenceV2(
                composition,
                preparedAuthority,
                receipts,
                graph.Character.CharacterInstanceStableId);
            Assert.That(persistence.PersistPreparedCustody(awaiting).Succeeded,
                Is.True);
            CollectedRunRewardPreparedTransferV1 prepared;
            CollectedRunRewardAtomicPlanV2 plan;
            Assert.That(CollectedRunRewardTransferPreparationFactoryV2
                .TryAcceptEndAndBuildPlan(
                    AcceptedEnd(suffix, awaiting, graph, endCommand),
                    awaiting,
                    graph,
                    rewardApplication,
                    out prepared,
                    out plan,
                    out diagnostic), Is.True, diagnostic);
            Assert.That(persistence.PersistPreparedCustody(prepared).Succeeded,
                Is.True);
            return new ProductionCollectedRunRewardTransferServiceV2(
                plan,
                new ProductionCollectedRunRewardAtomicAuthorityV2(
                    graph,
                    rewardApplication,
                    preparedAuthority,
                    receipts),
                persistence).Apply();
        }

        private static RunSessionCollectedRewardV1 DirectEquipmentReward(
            string suffix,
            StableId run,
            EquipmentInstance equipment)
        {
            return new RunSessionCollectedRewardV1(
                Id("pickup." + suffix),
                equipment.InstanceId,
                Id("grant." + suffix),
                Id("operation.drop-" + suffix),
                Id("terminal-event." + suffix),
                null,
                run,
                1L,
                Id("source-entity." + suffix),
                Id("source-placement." + suffix),
                1L,
                Id("source-definition." + suffix),
                Id("participant." + suffix),
                RewardGrantKindV1.EquipmentReference,
                equipment.DefinitionId,
                1L,
                Fingerprint("generated-batch-" + suffix),
                Fingerprint("generated-reward-" + suffix),
                Id("room." + suffix),
                0d,
                0d,
                Fingerprint("spawn-" + suffix),
                Fingerprint("available-" + suffix),
                Id("collector-entity." + suffix),
                Id("participant." + suffix),
                Id("operation.collect-" + suffix),
                1L,
                10L);
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
            private readonly Dictionary<StableId, EquipmentInstance> byId =
                new Dictionary<StableId, EquipmentInstance>();

            public ExactEquipmentPayloadSource(params EquipmentInstance[] values)
            {
                foreach (EquipmentInstance value in values)
                    byId.Add(value.InstanceId, value);
            }

            public bool TryResolveExact(
                StableId rewardInstanceStableId,
                StableId equipmentDefinitionStableId,
                out EquipmentInstance equipment,
                out string diagnostic)
            {
                if (!byId.TryGetValue(rewardInstanceStableId, out equipment))
                {
                    diagnostic = "equipment-payload-missing";
                    return false;
                }
                if (equipment.DefinitionId != equipmentDefinitionStableId)
                {
                    diagnostic = "equipment-definition-mismatch";
                    return false;
                }
                diagnostic = string.Empty;
                return true;
            }
        }

        private sealed class RecordingRunJournal :
            IRunSessionCollectedRewardAuthorityV1
        {
            private readonly List<RunSessionCollectedRewardV1> rewards =
                new List<RunSessionCollectedRewardV1>();

            public RecordingRunJournal(
                StableId run,
                StableId actor,
                StableId participant)
            {
                RunStableId = run;
                PlayerActorStableId = actor;
                PlayerParticipantStableId = participant;
            }

            public StableId RunStableId { get; }
            public long LifecycleGeneration { get { return 1L; } }
            public long AuthoritativeTick { get { return 40L + rewards.Count; } }
            public bool IsActive { get { return true; } }
            public StableId PlayerActorStableId { get; }
            public StableId PlayerParticipantStableId { get; }
            public long NextCollectedRewardOrder
            {
                get { return rewards.Count + 1L; }
            }

            public RunSessionRewardCollectionResultV1 RecordCollectedRunReward(
                RunSessionCollectedRewardV1 reward)
            {
                if (reward == null
                    || reward.RunStableId != RunStableId
                    || reward.RunLifecycleGeneration != LifecycleGeneration
                    || reward.CollectorEntityStableId != PlayerActorStableId
                    || reward.CollectorParticipantStableId
                        != PlayerParticipantStableId
                    || reward.AttributedParticipantStableId
                        != PlayerParticipantStableId
                    || reward.CollectionOrder != NextCollectedRewardOrder)
                {
                    return new RunSessionRewardCollectionResultV1(
                        RunSessionRewardCollectionStatusV1.Rejected,
                        reward,
                        "test-run-journal-rejected");
                }
                rewards.Add(reward);
                return new RunSessionRewardCollectionResultV1(
                    RunSessionRewardCollectionStatusV1.Collected,
                    reward,
                    string.Empty);
            }

            public IReadOnlyList<RunSessionCollectedRewardV1>
                ExportCollectedRunRewards()
            {
                return rewards.OrderBy(item => item.CollectionOrder).ToList()
                    .AsReadOnly();
            }
        }
    }
}
#endif
