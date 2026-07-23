using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
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
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.UI.ProductionFlow;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.CollectedRunRewards
{
    public sealed class ProductionCollectedRunRewardRecoveryPlayModeTests
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [TearDown]
        public void TearDown()
        {
            ProductionCollectedRunRewardResultsBridge.Clear();
        }

        [Test]
        public void EligibilityIgnoresAwaitingAndPersistedAndFiltersCharacter()
        {
            StableId selected = Id("character-instance.recovery-selected");
            StableId other = Id("character-instance.recovery-other");
            CollectedRunRewardPreparedTransferV1 awaiting =
                SimpleAwaiting("eligibility-awaiting", selected);
            CollectedRunRewardPreparedTransferV1 prepared =
                SimpleAwaiting("eligibility-prepared", selected).AcceptEnd(
                    Id("operation.transfer-eligibility-prepared"),
                    Id("mission-result.eligibility-prepared"),
                    Fingerprint("mission-eligibility-prepared"),
                    Fingerprint("batch-eligibility-prepared"),
                    Fingerprint("plan-eligibility-prepared"));
            CollectedRunRewardPreparedTransferV1 persisted =
                SimpleAwaiting("eligibility-persisted", selected)
                    .AcceptEnd(
                        Id("operation.transfer-eligibility-persisted"),
                        Id("mission-result.eligibility-persisted"),
                        Fingerprint("mission-eligibility-persisted"),
                        Fingerprint("batch-eligibility-persisted"),
                        Fingerprint("plan-eligibility-persisted"))
                    .MarkPersisted(
                        Fingerprint("receipt-eligibility-persisted"));
            var authority =
                new CollectedRunRewardPreparedTransferAuthorityV1(
                    new CollectedRunRewardPreparedTransferSnapshotV1(
                        4L,
                        new[] { awaiting, prepared, persisted }));

            IReadOnlyList<CollectedRunRewardPreparedTransferV1> selectedRows =
                authority.ExportRecoverable(selected);
            IReadOnlyList<CollectedRunRewardPreparedTransferV1> otherRows =
                authority.ExportRecoverable(other);

            Assert.That(selectedRows.Count, Is.EqualTo(1));
            Assert.That(selectedRows[0].Fingerprint,
                Is.EqualTo(prepared.Fingerprint));
            Assert.That(otherRows, Is.Empty);
        }

        [Test]
        public void ProcessRestartReloadsAndReconstructsExactPlan()
        {
            using (RecoveryFixture fixture = RecoveryFixture.Create(
                "restart-rebuild",
                RestartStoreMode.Success))
            {
                CollectedRunRewardAtomicPlanV2 rebuilt;
                string diagnostic;
                bool accepted = CollectedRunRewardTransferPreparationFactoryV2
                    .TryBuildPlanFromPrepared(
                        fixture.Prepared,
                        fixture.Graph,
                        fixture.RewardApplication,
                        out rebuilt,
                        out diagnostic);

                Assert.That(accepted, Is.True, diagnostic);
                Assert.That(rebuilt, Is.Not.Null);
                Assert.That(rebuilt.Fingerprint,
                    Is.EqualTo(fixture.OriginalPlanFingerprint));
                Assert.That(rebuilt.PreparedTransfer.Fingerprint,
                    Is.EqualTo(fixture.Prepared.Fingerprint));
                Assert.That(fixture.PreparedAuthority.ExportRecoverable(
                    fixture.CharacterId).Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void PreparedRecoveryAppliesOnceAndUsesOneStoreTransaction()
        {
            using (RecoveryFixture fixture = RecoveryFixture.Create(
                "success",
                RestartStoreMode.Success))
            using (RecoveryHost host = new RecoveryHost())
            {
                long moneyBefore = fixture.Graph.MoneyWallet
                    .CurrentSnapshot.Balance;
                object state = NewAttemptState();
                AddAttempt(host.Component, fixture.Prepared.CustodyStableId, state);

                InvokeAttempt(host.Component, fixture, fixture.Prepared, state);

                Assert.That(fixture.Graph.MoneyWallet.CurrentSnapshot.Balance,
                    Is.EqualTo(moneyBefore + fixture.MoneyQuantity));
                Assert.That(fixture.Store.CallCount, Is.EqualTo(1));
                Assert.That(GetInt(state, "AttemptCount"), Is.EqualTo(1));
                Assert.That(GetBool(state, "Fatal"), Is.False);
                Assert.That(GetBool(state, "Exhausted"), Is.False);
                Assert.That(Attempts(host.Component).Contains(
                    fixture.Prepared.CustodyStableId), Is.False);
                Assert.That(GetNotice(host.Component), Is.Null);
                Assert.That(ProductionCollectedRunRewardResultsBridge.Current,
                    Is.Not.Null);
                Assert.That(ProductionCollectedRunRewardResultsBridge.Current
                    .IsComplete, Is.True);

                CollectedRunRewardTransferReceiptV1 receipt;
                Assert.That(fixture.Receipts.TryGetByOperation(
                    fixture.Prepared.TransferOperationStableId,
                    out receipt), Is.True);
                CollectedRunRewardPreparedTransferV1 persisted;
                Assert.That(fixture.PreparedAuthority.TryGetByCustody(
                    fixture.Prepared.CustodyStableId,
                    out persisted), Is.True);
                Assert.That(persisted.State,
                    Is.EqualTo(
                        CollectedRunRewardPreparedTransferStateV1.Persisted));
            }
        }

        [Test]
        public void RetryPolicyBacksOffAndStopsAfterFiveAttempts()
        {
            using (RecoveryFixture fixture = RecoveryFixture.Create(
                "retry-limit",
                RestartStoreMode.Success))
            using (RecoveryHost host = new RecoveryHost())
            {
                CollectedRunRewardPreparedTransferV1 invalid =
                    fixture.Awaiting.AcceptEnd(
                        fixture.Prepared.TransferOperationStableId,
                        fixture.Prepared.AcceptedMissionResultStableId,
                        fixture.Prepared.AcceptedMissionResultFingerprint,
                        fixture.Prepared.BatchFingerprint,
                        Fingerprint("intentionally-invalid-plan"));
                object state = NewAttemptState();
                AddAttempt(host.Component, invalid.CustodyStableId, state);

                for (int attempt = 1; attempt <= 5; attempt++)
                {
                    LogAssert.Expect(
                        LogType.Warning,
                        new Regex(
                            "Durable collected-run transfer remains prepared for recovery:.*"));
                    float before = Time.unscaledTime;
                    InvokeAttempt(host.Component, fixture, invalid, state);
                    Assert.That(GetInt(state, "AttemptCount"),
                        Is.EqualTo(attempt));
                    if (attempt < 5)
                    {
                        float delay = GetFloat(state, "NextAttemptAt") - before;
                        Assert.That(delay, Is.GreaterThanOrEqualTo(0.9f));
                        Assert.That(delay, Is.LessThanOrEqualTo(30.1f));
                        Assert.That(GetBool(state, "Exhausted"), Is.False);
                    }
                }

                Assert.That(GetBool(state, "Exhausted"), Is.True);
                Assert.That(float.IsPositiveInfinity(
                    GetFloat(state, "NextAttemptAt")), Is.True);
                Assert.That(fixture.Store.CallCount, Is.Zero);
            }
        }

        [Test]
        public void ManualExactRecoveryRemainsAvailableAfterExhaustion()
        {
            using (RecoveryFixture fixture = RecoveryFixture.Create(
                "manual-after-exhaustion",
                RestartStoreMode.Success))
            using (RecoveryHost host = new RecoveryHost())
            {
                CollectedRunRewardPreparedTransferV1 invalid =
                    fixture.Awaiting.AcceptEnd(
                        fixture.Prepared.TransferOperationStableId,
                        fixture.Prepared.AcceptedMissionResultStableId,
                        fixture.Prepared.AcceptedMissionResultFingerprint,
                        fixture.Prepared.BatchFingerprint,
                        Fingerprint("manual-invalid-plan"));
                object state = NewAttemptState();
                AddAttempt(host.Component, invalid.CustodyStableId, state);
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    LogAssert.Expect(
                        LogType.Warning,
                        new Regex(
                            "Durable collected-run transfer remains prepared for recovery:.*"));
                    InvokeAttempt(host.Component, fixture, invalid, state);
                }
                Assert.That(GetBool(state, "Exhausted"), Is.True);
                Assert.That(GetNotice(host.Component), Is.Not.Null);

                InvokePrivate(host.Component, "RetryNoticeNow");

                Assert.That(GetBool(state, "Exhausted"), Is.False);
                Assert.That(GetFloat(state, "NextAttemptAt"),
                    Is.LessThanOrEqualTo(Time.unscaledTime + 0.1f));
                Assert.That(GetBool(GetNotice(host.Component), "Fatal"),
                    Is.False);
            }
        }

        [Test]
        public void SuccessfulRecoveryClearsAnExistingNotice()
        {
            using (RecoveryFixture fixture = RecoveryFixture.Create(
                "clear-notice",
                RestartStoreMode.Success))
            using (RecoveryHost host = new RecoveryHost())
            {
                object state = NewAttemptState();
                SetField(state, "AttemptCount", 1);
                AddAttempt(host.Component, fixture.Prepared.CustodyStableId, state);
                LogAssert.Expect(
                    LogType.Warning,
                    new Regex(
                        "Durable collected-run transfer remains prepared for recovery:.*"));
                InvokePrivate(
                    host.Component,
                    "RegisterRecoverableFailure",
                    fixture.Prepared,
                    state,
                    "fixture-pending",
                    null);
                Assert.That(GetNotice(host.Component), Is.Not.Null);

                InvokeAttempt(host.Component, fixture, fixture.Prepared, state);

                Assert.That(GetNotice(host.Component), Is.Null);
                Assert.That(Attempts(host.Component).Contains(
                    fixture.Prepared.CustodyStableId), Is.False);
            }
        }

        [Test]
        public void FatalDurableUncertaintyDisablesAutomaticAndManualRetry()
        {
            using (RecoveryFixture fixture = RecoveryFixture.Create(
                "fatal-uncertain",
                RestartStoreMode.ActiveReadBackFailure))
            using (RecoveryHost host = new RecoveryHost())
            {
                object state = NewAttemptState();
                AddAttempt(host.Component, fixture.Prepared.CustodyStableId, state);
                LogAssert.Expect(
                    LogType.Error,
                    new Regex(
                        "Durable collected-run transfer recovery is fatal for.*"));

                InvokeAttempt(host.Component, fixture, fixture.Prepared, state);

                object notice = GetNotice(host.Component);
                Assert.That(GetBool(state, "Fatal"), Is.True);
                Assert.That(GetBool(state, "Exhausted"), Is.False);
                Assert.That(GetBool(notice, "Fatal"), Is.True);
                Assert.That(float.IsPositiveInfinity(
                    GetFloat(state, "NextAttemptAt")), Is.False);
                int attemptsBefore = GetInt(state, "AttemptCount");

                InvokePrivate(host.Component, "RetryNoticeNow");

                Assert.That(GetBool(state, "Fatal"), Is.True);
                Assert.That(GetInt(state, "AttemptCount"),
                    Is.EqualTo(attemptsBefore));
                Assert.That(GetBool(GetNotice(host.Component), "Fatal"),
                    Is.True);
            }
        }

        [Test]
        public void WrongSelectedCharacterCannotConsumeAnotherCharactersCustody()
        {
            using (RecoveryFixture fixture = RecoveryFixture.Create(
                "character-isolation",
                RestartStoreMode.Success))
            {
                StableId other = Id("character-instance.not-selected");
                long moneyBefore = fixture.Graph.MoneyWallet
                    .CurrentSnapshot.Balance;

                IReadOnlyList<CollectedRunRewardPreparedTransferV1> rows =
                    fixture.PreparedAuthority.ExportRecoverable(other);

                Assert.That(rows, Is.Empty);
                Assert.That(fixture.Graph.MoneyWallet.CurrentSnapshot.Balance,
                    Is.EqualTo(moneyBefore));
                Assert.That(fixture.Store.CallCount, Is.Zero);
                CollectedRunRewardTransferReceiptV1 receipt;
                Assert.That(fixture.Receipts.TryGetByOperation(
                    fixture.Prepared.TransferOperationStableId,
                    out receipt), Is.False);
            }
        }

        private static void InvokeAttempt(
            ProductionCollectedRunRewardRecoveryV2 component,
            RecoveryFixture fixture,
            CollectedRunRewardPreparedTransferV1 prepared,
            object state)
        {
            InvokePrivate(
                component,
                "AttemptRecovery",
                prepared,
                fixture.Graph,
                fixture.Composition,
                fixture.RewardApplication,
                fixture.PreparedAuthority,
                fixture.Receipts,
                state);
        }

        private static object InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                PrivateInstance);
            Assert.That(method, Is.Not.Null, methodName);
            try
            {
                return method.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static object NewAttemptState()
        {
            Type stateType = typeof(ProductionCollectedRunRewardRecoveryV2)
                .GetNestedType("RecoveryAttemptState", BindingFlags.NonPublic);
            Assert.That(stateType, Is.Not.Null);
            return Activator.CreateInstance(stateType, true);
        }

        private static IDictionary Attempts(
            ProductionCollectedRunRewardRecoveryV2 component)
        {
            return (IDictionary)GetField(component, "attempts");
        }

        private static void AddAttempt(
            ProductionCollectedRunRewardRecoveryV2 component,
            StableId custodyStableId,
            object state)
        {
            Attempts(component)[custodyStableId] = state;
        }

        private static object GetNotice(
            ProductionCollectedRunRewardRecoveryV2 component)
        {
            return GetField(component, "notice");
        }

        private static object GetField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                PrivateInstance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(target);
        }

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                PrivateInstance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static int GetInt(object target, string fieldName)
        {
            return (int)GetField(target, fieldName);
        }

        private static float GetFloat(object target, string fieldName)
        {
            return (float)GetField(target, fieldName);
        }

        private static bool GetBool(object target, string fieldName)
        {
            return (bool)GetField(target, fieldName);
        }

        private static CollectedRunRewardPreparedTransferV1 SimpleAwaiting(
            string suffix,
            StableId character)
        {
            return CollectedRunRewardPreparedTransferV1.AwaitingAcceptedEnd(
                Id("custody." + suffix),
                Id("operation.prepare-" + suffix),
                Id("run-instance." + suffix),
                1L,
                character,
                0L,
                Fingerprint("character-" + suffix),
                Id("operation.end-" + suffix),
                Fingerprint("end-command-" + suffix),
                1UL,
                1,
                ProgressionContext.Create(
                    1,
                    1,
                    Id("difficulty.normal"),
                    0,
                    Array.Empty<StableId>()),
                Fingerprint("event-" + suffix),
                0L,
                0L,
                0L,
                new Dictionary<string, string>
                {
                    { "money", Fingerprint("money-" + suffix) },
                },
                Array.Empty<CollectedRunRewardTransferItemV1>(),
                Array.Empty<ShooterMover.Domain.Equipment.EquipmentInstance>(),
                Array.Empty<StrongboxInstanceContextV1>());
        }

        private static string Fingerprint(string material)
        {
            return StrongboxCanonicalV1.Fingerprint(material);
        }

        private static StableId Id(string canonical)
        {
            return StableId.Parse(canonical);
        }

        private sealed class RecoveryHost : IDisposable
        {
            private readonly GameObject gameObject;

            public RecoveryHost()
            {
                gameObject = new GameObject("CollectedRunRewardRecoveryTest");
                Component = gameObject.AddComponent<
                    ProductionCollectedRunRewardRecoveryV2>();
            }

            public ProductionCollectedRunRewardRecoveryV2 Component { get; }

            public void Dispose()
            {
                if (gameObject != null)
                    UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private enum RestartStoreMode
        {
            Success,
            ActiveReadBackFailure,
        }

        private sealed class RestartStore
        {
            private readonly RestartStoreMode mode;

            public RestartStore(
                RestartStoreMode mode,
                PlayerAccountSnapshotV1 durableSnapshot)
            {
                this.mode = mode;
                DurableSnapshot = durableSnapshot;
            }

            public int CallCount { get; private set; }
            public PlayerAccountSnapshotV1 DurableSnapshot { get; private set; }

            public PlayerAccountStoreResultV1 Save(
                PlayerAccountSnapshotV1 candidate)
            {
                CallCount++;
                DurableSnapshot = candidate;
                if (mode == RestartStoreMode.ActiveReadBackFailure)
                {
                    return new PlayerAccountStoreResultV1(
                        PlayerAccountStoreStatusV1.IoFailure,
                        "active-readback-invalid-after-atomic-replace",
                        null);
                }
                return Saved(candidate);
            }

            public void ResetCalls()
            {
                CallCount = 0;
            }
        }

        private sealed class RecoveryFixture : IDisposable
        {
            private RecoveryFixture(
                string suffix,
                StableId characterId,
                ProductionCharacterRuntimeGraphFactoryV1 factory,
                PlayerAccountSaveAuthorityV1 accountAuthority,
                CharacterCompositionCoordinatorV1 composition,
                ProductionCharacterRuntimeGraphV1 graph,
                RewardApplicationServiceV1 rewardApplication,
                CollectedRunRewardPreparedTransferAuthorityV1 preparedAuthority,
                CollectedRunRewardTransferReceiptAuthorityV1 receipts,
                CollectedRunRewardPreparedTransferV1 awaiting,
                CollectedRunRewardPreparedTransferV1 prepared,
                string originalPlanFingerprint,
                RestartStore store,
                long moneyQuantity)
            {
                Suffix = suffix;
                CharacterId = characterId;
                Factory = factory;
                AccountAuthority = accountAuthority;
                Composition = composition;
                Graph = graph;
                RewardApplication = rewardApplication;
                PreparedAuthority = preparedAuthority;
                Receipts = receipts;
                Awaiting = awaiting;
                Prepared = prepared;
                OriginalPlanFingerprint = originalPlanFingerprint;
                Store = store;
                MoneyQuantity = moneyQuantity;
            }

            public string Suffix { get; }
            public StableId CharacterId { get; }
            public ProductionCharacterRuntimeGraphFactoryV1 Factory { get; }
            public PlayerAccountSaveAuthorityV1 AccountAuthority { get; }
            public CharacterCompositionCoordinatorV1 Composition { get; }
            public ProductionCharacterRuntimeGraphV1 Graph { get; }
            public RewardApplicationServiceV1 RewardApplication { get; }
            public CollectedRunRewardPreparedTransferAuthorityV1
                PreparedAuthority { get; }
            public CollectedRunRewardTransferReceiptAuthorityV1 Receipts { get; }
            public CollectedRunRewardPreparedTransferV1 Awaiting { get; }
            public CollectedRunRewardPreparedTransferV1 Prepared { get; }
            public string OriginalPlanFingerprint { get; }
            public RestartStore Store { get; }
            public long MoneyQuantity { get; }

            public static RecoveryFixture Create(
                string suffix,
                RestartStoreMode restartMode)
            {
                StableId characterId = Id("character-instance." + suffix);
                StableId classId = Id("loadout-profile.striker");
                ProductionCharacterRuntimeGraphFactoryV1 factory =
                    ProductionCharacterRuntimeGraphFactoryV1
                        .CreateVerticalSliceDefaults();
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
                    "Recovery Pilot " + suffix,
                    route);
                IReadOnlyList<SaveComponentSnapshotV1> components =
                    PlayerAccountRestoreCoordinatorV1.ExportComponents(
                        starter.SaveAdapters);
                starter.Dispose();
                var character = new CharacterInstanceSnapshotV1(
                    characterId,
                    classId,
                    0,
                    "Recovery Pilot " + suffix,
                    0L,
                    components);
                var slots = new CharacterInstanceSnapshotV1[
                    PlayerAccountSnapshotV1.CharacterSlotCount];
                slots[0] = character;
                var initialAccount = new PlayerAccountSnapshotV1(
                    Id("account." + suffix),
                    0L,
                    slots,
                    null);
                var initialAuthority =
                    new PlayerAccountSaveAuthorityV1(initialAccount);
                var initialComposition = new CharacterCompositionCoordinatorV1(
                    initialAuthority,
                    factory,
                    Saved);
                Assert.That(initialComposition.Select(0).Succeeded, Is.True);
                var initialGraph = (ProductionCharacterRuntimeGraphV1)
                    initialComposition.ActiveRuntime;
                RewardApplicationServiceV1 initialReward = RewardApplication(
                    suffix,
                    initialGraph);
                ProductionCollectedRunRewardRuntimeRegistryV2
                    .BindRewardApplication(characterId, initialReward);
                ProductionCollectedRunRewardRuntimeRegistryV2.BindRuntime(
                    initialGraph,
                    initialComposition);
                CollectedRunRewardPreparedTransferAuthorityV1 initialPrepared;
                CollectedRunRewardTransferReceiptAuthorityV1 initialReceipts;
                RewardApplicationServiceV1 ignored;
                Assert.That(ProductionCollectedRunRewardRuntimeRegistryV2
                    .TryResolve(
                        characterId,
                        out ignored,
                        out initialPrepared,
                        out initialReceipts), Is.True);

                long moneyQuantity = 17L;
                StableId run = Id("run-instance." + suffix);
                var endCommand = new EndRunSessionCommandV1(
                    Id("operation.end-" + suffix),
                    run,
                    1L,
                    MissionRunCompletionStateV1.Completed,
                    100L);
                RunSessionCollectedRewardV1 collected = MoneyReward(
                    suffix,
                    run,
                    moneyQuantity);
                var generation = new CollectedRunRewardGenerationContextV2(
                    987UL,
                    2,
                    ProgressionContext.Create(
                        12,
                        10,
                        Id("difficulty.normal"),
                        0,
                        new[] { Id("progression-tag.campaign") }),
                    Fingerprint("event-" + suffix));
                CollectedRunRewardPreparedTransferV1 awaiting;
                string diagnostic;
                Assert.That(CollectedRunRewardTransferPreparationFactoryV2
                    .TryCreateAwaitingAcceptedEnd(
                        endCommand,
                        new[] { collected },
                        initialGraph,
                        initialReward,
                        initialReceipts,
                        initialPrepared,
                        generation,
                        new NoEquipmentPayloadSource(),
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

                RunSessionEndResultV1 acceptedEnd = AcceptedEnd(
                    suffix,
                    awaiting,
                    initialGraph,
                    endCommand);
                CollectedRunRewardPreparedTransferV1 prepared;
                CollectedRunRewardAtomicPlanV2 plan;
                Assert.That(CollectedRunRewardTransferPreparationFactoryV2
                    .TryAcceptEndAndBuildPlan(
                        acceptedEnd,
                        awaiting,
                        initialGraph,
                        initialReward,
                        out prepared,
                        out plan,
                        out diagnostic), Is.True, diagnostic);
                Assert.That(initialPersistence.PersistPreparedCustody(prepared)
                    .Succeeded, Is.True);
                PlayerAccountSnapshotV1 durablePrepared =
                    initialAuthority.Current;
                initialComposition.Dispose();
                ProductionCollectedRunRewardRuntimeRegistryV2.Release(
                    characterId);

                var restartAuthority =
                    new PlayerAccountSaveAuthorityV1(durablePrepared);
                var store = new RestartStore(
                    restartMode,
                    durablePrepared);
                var restartComposition =
                    new CharacterCompositionCoordinatorV1(
                        restartAuthority,
                        factory,
                        store.Save);
                CharacterCompositionResultV1 selected =
                    restartComposition.Select(0);
                Assert.That(selected.Succeeded, Is.True, selected.Diagnostic);
                var restartGraph = (ProductionCharacterRuntimeGraphV1)
                    restartComposition.ActiveRuntime;
                RewardApplicationServiceV1 restartReward = RewardApplication(
                    suffix,
                    restartGraph);
                ProductionCollectedRunRewardRuntimeRegistryV2
                    .BindRewardApplication(characterId, restartReward);
                ProductionCollectedRunRewardRuntimeRegistryV2.BindRuntime(
                    restartGraph,
                    restartComposition);
                CollectedRunRewardPreparedTransferAuthorityV1 restoredPrepared;
                CollectedRunRewardTransferReceiptAuthorityV1 restoredReceipts;
                Assert.That(ProductionCollectedRunRewardRuntimeRegistryV2
                    .TryResolve(
                        characterId,
                        out ignored,
                        out restoredPrepared,
                        out restoredReceipts), Is.True);
                CollectedRunRewardPreparedTransferV1 restored;
                Assert.That(restoredPrepared.TryGetByCustody(
                    prepared.CustodyStableId,
                    out restored), Is.True);
                Assert.That(restored.Fingerprint,
                    Is.EqualTo(prepared.Fingerprint));
                store.ResetCalls();

                return new RecoveryFixture(
                    suffix,
                    characterId,
                    factory,
                    restartAuthority,
                    restartComposition,
                    restartGraph,
                    restartReward,
                    restoredPrepared,
                    restoredReceipts,
                    awaiting,
                    restored,
                    plan.Fingerprint,
                    store,
                    moneyQuantity);
            }

            public void Dispose()
            {
                Composition.Dispose();
                ProductionCollectedRunRewardRuntimeRegistryV2.Release(
                    CharacterId);
            }

            private static RewardApplicationServiceV1 RewardApplication(
                string suffix,
                ProductionCharacterRuntimeGraphV1 graph)
            {
                return new RewardApplicationServiceV1(
                    Id("authority.recovery-reward-application-" + suffix),
                    new MoneyRewardChildAuthorityV1(graph.MoneyWallet),
                    new ScrapRewardChildAuthorityV1(graph.ScrapWallet),
                    new PlayerHoldingsRewardChildAuthorityV1(
                        graph.LoadoutRuntime.Holdings,
                        graph.LoadoutRuntime.CatalogAdapter));
            }

            private static RunSessionCollectedRewardV1 MoneyReward(
                string suffix,
                StableId run,
                long quantity)
            {
                return new RunSessionCollectedRewardV1(
                    Id("pickup." + suffix),
                    Id("reward-instance." + suffix),
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
                    RewardGrantKindV1.Money,
                    MoneyWalletIdsV1.CurrencyStableId,
                    quantity,
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
                    50L);
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
        }

        private sealed class NoEquipmentPayloadSource :
            ICollectedRunEquipmentPayloadSourceV2
        {
            public bool TryResolveExact(
                StableId rewardInstanceStableId,
                StableId equipmentDefinitionStableId,
                out ShooterMover.Domain.Equipment.EquipmentInstance equipment,
                out string diagnostic)
            {
                equipment = null;
                diagnostic = "unexpected-equipment-reward";
                return false;
            }
        }

        private static PlayerAccountStoreResultV1 Saved(
            PlayerAccountSnapshotV1 snapshot)
        {
            return new PlayerAccountStoreResultV1(
                PlayerAccountStoreStatusV1.Saved,
                string.Empty,
                snapshot);
        }
    }
}
