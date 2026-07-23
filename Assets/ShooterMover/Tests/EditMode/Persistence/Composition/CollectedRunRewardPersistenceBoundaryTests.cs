using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class CollectedRunRewardPersistenceBoundaryTests
    {
        [Test]
        public void InvalidContextRejectsBeforeStoreCallbackAndChangesNothing()
        {
            using (Fixture fixture = Fixture.Create(
                "reject-before-callback",
                StoreMode.Success))
            {
                PlayerAccountSnapshotV1 beforeAccount = fixture.AccountAuthority.Current;
                PlayerAccountSnapshotV1 beforeFile = fixture.Store.DurableSnapshot;
                CollectedRunRewardPreparedTransferV1 wrongCharacter =
                    fixture.Awaiting(Id("character-instance.someone-else"));

                CollectedRunRewardTransferPersistenceResultV1 result =
                    fixture.Persistence.PersistPreparedCustody(wrongCharacter);

                Assert.That(result.RejectedBeforeReplacement, Is.True);
                Assert.That(fixture.Store.CallCount, Is.Zero);
                Assert.That(fixture.Prepared.ExportSnapshot().Records, Is.Empty);
                Assert.That(fixture.AccountAuthority.Current.Fingerprint,
                    Is.EqualTo(beforeAccount.Fingerprint));
                Assert.That(fixture.Composition.ActiveRuntime.Character.Fingerprint,
                    Is.EqualTo(beforeAccount.CharacterAt(0).Fingerprint));
                Assert.That(fixture.Store.DurableSnapshot.Fingerprint,
                    Is.EqualTo(beforeFile.Fingerprint));
            }
        }

        [Test]
        public void CallbackThrowBeforeReplacementIsReportedUncertainButFileStaysOld()
        {
            using (Fixture fixture = Fixture.Create(
                "throw-before-replacement",
                StoreMode.ThrowBeforeReplacement))
            {
                PlayerAccountSnapshotV1 oldFile = fixture.Store.DurableSnapshot;
                string oldCharacter = fixture.Composition.ActiveRuntime.Character.Fingerprint;
                CollectedRunRewardPreparedTransferV1 awaiting = fixture.Awaiting();

                CollectedRunRewardTransferPersistenceResultV1 result =
                    fixture.Persistence.PersistPreparedCustody(awaiting);

                AssertUncertain(result);
                AssertLiveCustody(fixture, awaiting);
                Assert.That(fixture.AccountAuthority.Current.CharacterAt(0)
                    .TryGetComponent(PreparedComponentId, out _), Is.False);
                Assert.That(fixture.Composition.ActiveRuntime.Character.Fingerprint,
                    Is.EqualTo(oldCharacter));
                Assert.That(fixture.Store.DurableSnapshot.Fingerprint,
                    Is.EqualTo(oldFile.Fingerprint));
            }
        }

        [Test]
        public void CallbackThrowAfterReplacementLeavesDurableCandidateAndReportsUncertain()
        {
            using (Fixture fixture = Fixture.Create(
                "throw-after-replacement",
                StoreMode.ThrowAfterReplacement))
            {
                PlayerAccountSnapshotV1 oldAccount = fixture.AccountAuthority.Current;
                string oldCharacter = fixture.Composition.ActiveRuntime.Character.Fingerprint;
                CollectedRunRewardPreparedTransferV1 awaiting = fixture.Awaiting();

                CollectedRunRewardTransferPersistenceResultV1 result =
                    fixture.Persistence.PersistPreparedCustody(awaiting);

                AssertUncertain(result);
                AssertLiveCustody(fixture, awaiting);
                Assert.That(fixture.AccountAuthority.Current.Fingerprint,
                    Is.EqualTo(oldAccount.Fingerprint));
                Assert.That(fixture.Composition.ActiveRuntime.Character.Fingerprint,
                    Is.EqualTo(oldCharacter));
                AssertDurableContains(fixture.Store.DurableSnapshot, awaiting);
            }
        }

        [Test]
        public void TemporaryValidationFailureKeepsAccountAndFileOldButLiveCustodyExists()
        {
            using (Fixture fixture = Fixture.Create(
                "temporary-validation",
                StoreMode.TemporaryValidationRejected))
            {
                PlayerAccountSnapshotV1 oldAccount = fixture.AccountAuthority.Current;
                PlayerAccountSnapshotV1 oldFile = fixture.Store.DurableSnapshot;
                CollectedRunRewardPreparedTransferV1 awaiting = fixture.Awaiting();

                CollectedRunRewardTransferPersistenceResultV1 result =
                    fixture.Persistence.PersistPreparedCustody(awaiting);

                AssertUncertain(result);
                AssertLiveCustody(fixture, awaiting);
                Assert.That(fixture.AccountAuthority.Current.Fingerprint,
                    Is.EqualTo(oldAccount.Fingerprint));
                Assert.That(fixture.Store.DurableSnapshot.Fingerprint,
                    Is.EqualTo(oldFile.Fingerprint));
                Assert.That(fixture.Composition.ActiveRuntime.Character.Fingerprint,
                    Is.EqualTo(oldAccount.CharacterAt(0).Fingerprint));
            }
        }

        [Test]
        public void ActiveReadBackFailureLeavesNewDurableFileButRollsBackAccountAuthority()
        {
            using (Fixture fixture = Fixture.Create(
                "active-readback",
                StoreMode.ActiveReadBackFailure))
            {
                PlayerAccountSnapshotV1 oldAccount = fixture.AccountAuthority.Current;
                string oldCharacter = fixture.Composition.ActiveRuntime.Character.Fingerprint;
                CollectedRunRewardPreparedTransferV1 awaiting = fixture.Awaiting();

                CollectedRunRewardTransferPersistenceResultV1 result =
                    fixture.Persistence.PersistPreparedCustody(awaiting);

                AssertUncertain(result);
                AssertLiveCustody(fixture, awaiting);
                Assert.That(fixture.AccountAuthority.Current.Fingerprint,
                    Is.EqualTo(oldAccount.Fingerprint));
                Assert.That(fixture.Composition.ActiveRuntime.Character.Fingerprint,
                    Is.EqualTo(oldCharacter));
                AssertDurableContains(fixture.Store.DurableSnapshot, awaiting);
            }
        }

        [Test]
        public void SuccessfulStoreReturningWrongComponentIsDetectedAsUncertain()
        {
            using (Fixture fixture = Fixture.Create(
                "component-mismatch",
                StoreMode.SuccessWithOldSnapshot))
            {
                PlayerAccountSnapshotV1 oldFile = fixture.Store.DurableSnapshot;
                CollectedRunRewardPreparedTransferV1 awaiting = fixture.Awaiting();

                CollectedRunRewardTransferPersistenceResultV1 result =
                    fixture.Persistence.PersistPreparedCustody(awaiting);

                AssertUncertain(result);
                Assert.That(result.Diagnostic,
                    Does.Contain("custody-active-component-mismatch"));
                AssertLiveCustody(fixture, awaiting);
                Assert.That(fixture.AccountAuthority.Current.CharacterAt(0)
                    .TryGetComponent(PreparedComponentId, out _), Is.True);
                Assert.That(fixture.Composition.ActiveRuntime.Character
                    .TryGetComponent(PreparedComponentId, out _), Is.False);
                Assert.That(fixture.Store.DurableSnapshot.Fingerprint,
                    Is.EqualTo(oldFile.Fingerprint));
            }
        }

        [Test]
        public void SuccessfulSaveAlignsLiveAccountActiveCharacterAndDurableFile()
        {
            using (Fixture fixture = Fixture.Create(
                "successful-save",
                StoreMode.Success))
            {
                CollectedRunRewardPreparedTransferV1 awaiting = fixture.Awaiting();

                CollectedRunRewardTransferPersistenceResultV1 result =
                    fixture.Persistence.PersistPreparedCustody(awaiting);

                Assert.That(result.Status,
                    Is.EqualTo(
                        CollectedRunRewardTransferPersistenceStatusV1
                            .PreparedAndVerified));
                Assert.That(result.Succeeded, Is.True);
                AssertLiveCustody(fixture, awaiting);
                AssertAccountContains(fixture.AccountAuthority.Current, awaiting);
                AssertAccountContains(
                    fixture.Composition.ActiveRuntime.Character,
                    awaiting);
                AssertDurableContains(fixture.Store.DurableSnapshot, awaiting);
                Assert.That(fixture.AccountAuthority.Current.Fingerprint,
                    Is.EqualTo(fixture.Store.DurableSnapshot.Fingerprint));
                Assert.That(fixture.Composition.ActiveRuntime.Character.Fingerprint,
                    Is.EqualTo(fixture.AccountAuthority.Current.CharacterAt(0)
                        .Fingerprint));
            }
        }

        [Test]
        public void ExactFinalNoChangeReplaySkipsStoreAndReturnsAlreadyPersisted()
        {
            using (Fixture fixture = Fixture.Create(
                "exact-no-change",
                StoreMode.Success))
            {
                CollectedRunRewardPreparedTransferV1 awaiting = fixture.Awaiting();
                Assert.That(fixture.Persistence.PersistPreparedCustody(awaiting)
                    .Succeeded, Is.True);
                CollectedRunRewardPreparedTransferV1 prepared = awaiting.AcceptEnd(
                    Id("operation.transfer-exact-no-change"),
                    Id("mission-result.exact-no-change"),
                    Fingerprint("mission-exact-no-change"),
                    Fingerprint("batch-exact-no-change"),
                    Fingerprint("plan-exact-no-change"));
                Assert.That(fixture.Persistence.PersistPreparedCustody(prepared)
                    .Succeeded, Is.True);
                CollectedRunRewardTransferReceiptV1 receipt =
                    fixture.Receipt(prepared);
                Assert.That(fixture.Receipts.Record(receipt).Succeeded, Is.True);
                CollectedRunRewardPreparedTransferV1 persisted =
                    prepared.MarkPersisted(receipt.Fingerprint);
                CollectedRunRewardTransferPersistenceResultV1 first =
                    fixture.Persistence.PersistAppliedAndVerify(
                        persisted,
                        receipt);
                int storeCalls = fixture.Store.CallCount;
                string accountFingerprint = fixture.AccountAuthority.Current.Fingerprint;
                string durableFingerprint = fixture.Store.DurableSnapshot.Fingerprint;

                CollectedRunRewardTransferPersistenceResultV1 replay =
                    fixture.Persistence.PersistAppliedAndVerify(
                        persisted,
                        receipt);

                Assert.That(first.Status,
                    Is.EqualTo(
                        CollectedRunRewardTransferPersistenceStatusV1
                            .PersistedAndVerified));
                Assert.That(replay.Status,
                    Is.EqualTo(
                        CollectedRunRewardTransferPersistenceStatusV1
                            .AlreadyPersisted));
                Assert.That(fixture.Store.CallCount, Is.EqualTo(storeCalls));
                Assert.That(fixture.AccountAuthority.Current.Fingerprint,
                    Is.EqualTo(accountFingerprint));
                Assert.That(fixture.Store.DurableSnapshot.Fingerprint,
                    Is.EqualTo(durableFingerprint));
                AssertAccountContains(fixture.Store.DurableSnapshot, persisted);
                AssertReceiptComponent(fixture.Store.DurableSnapshot, receipt);
            }
        }

        private static StableId PreparedComponentId
        {
            get
            {
                return CollectedRunRewardPreparedTransferSaveComponentV1
                    .ComponentStableId;
            }
        }

        private static void AssertUncertain(
            CollectedRunRewardTransferPersistenceResultV1 result)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.DurableStateUncertain, Is.True);
            Assert.That(result.Succeeded, Is.False);
        }

        private static void AssertLiveCustody(
            Fixture fixture,
            CollectedRunRewardPreparedTransferV1 expected)
        {
            CollectedRunRewardPreparedTransferV1 actual;
            Assert.That(fixture.Prepared.TryGetByCustody(
                expected.CustodyStableId,
                out actual), Is.True);
            Assert.That(actual.Fingerprint, Is.EqualTo(expected.Fingerprint));
        }

        private static void AssertDurableContains(
            PlayerAccountSnapshotV1 account,
            CollectedRunRewardPreparedTransferV1 expected)
        {
            Assert.That(account, Is.Not.Null);
            AssertAccountContains(account.CharacterAt(0), expected);
        }

        private static void AssertAccountContains(
            PlayerAccountSnapshotV1 account,
            CollectedRunRewardPreparedTransferV1 expected)
        {
            Assert.That(account, Is.Not.Null);
            AssertAccountContains(account.CharacterAt(0), expected);
        }

        private static void AssertAccountContains(
            CharacterInstanceSnapshotV1 character,
            CollectedRunRewardPreparedTransferV1 expected)
        {
            SaveComponentSnapshotV1 component;
            Assert.That(character, Is.Not.Null);
            Assert.That(character.TryGetComponent(
                PreparedComponentId,
                out component), Is.True);
            CollectedRunRewardPreparedTransferSnapshotV1 decoded;
            string rejection;
            Assert.That(CollectedRunRewardPreparedTransferSaveComponentV1
                .Codec.Instance.TryDecode(
                    component.CanonicalPayload,
                    out decoded,
                    out rejection), Is.True, rejection);
            CollectedRunRewardPreparedTransferV1 actual;
            Assert.That(decoded.TryGetByCustody(
                expected.CustodyStableId,
                out actual), Is.True);
            Assert.That(actual.Fingerprint, Is.EqualTo(expected.Fingerprint));
        }

        private static void AssertReceiptComponent(
            PlayerAccountSnapshotV1 account,
            CollectedRunRewardTransferReceiptV1 expected)
        {
            SaveComponentSnapshotV1 component;
            Assert.That(account.CharacterAt(0).TryGetComponent(
                CollectedRunRewardTransferReceiptSaveComponentV1
                    .ComponentStableId,
                out component), Is.True);
            CollectedRunRewardTransferReceiptSnapshotV1 decoded;
            string rejection;
            Assert.That(CollectedRunRewardTransferReceiptSaveComponentV1
                .Codec.Instance.TryDecode(
                    component.CanonicalPayload,
                    out decoded,
                    out rejection), Is.True, rejection);
            CollectedRunRewardTransferReceiptV1 actual;
            Assert.That(decoded.TryGetByOperation(
                expected.OperationStableId,
                out actual), Is.True);
            Assert.That(actual.Fingerprint, Is.EqualTo(expected.Fingerprint));
        }

        private static string Fingerprint(string material)
        {
            return StrongboxCanonicalV1.Fingerprint(material);
        }

        private static StableId Id(string canonical)
        {
            return StableId.Parse(canonical);
        }

        private enum StoreMode
        {
            Success,
            ThrowBeforeReplacement,
            ThrowAfterReplacement,
            TemporaryValidationRejected,
            ActiveReadBackFailure,
            SuccessWithOldSnapshot,
        }

        private sealed class ControlledStore
        {
            private readonly StoreMode mode;
            private readonly PlayerAccountSnapshotV1 original;

            public ControlledStore(
                StoreMode mode,
                PlayerAccountSnapshotV1 original)
            {
                this.mode = mode;
                this.original = original;
                DurableSnapshot = original;
            }

            public PlayerAccountSnapshotV1 DurableSnapshot { get; private set; }
            public int CallCount { get; private set; }

            public PlayerAccountStoreResultV1 Save(
                PlayerAccountSnapshotV1 candidate)
            {
                CallCount++;
                switch (mode)
                {
                    case StoreMode.ThrowBeforeReplacement:
                        throw new InvalidOperationException(
                            "fixture-before-replacement");
                    case StoreMode.ThrowAfterReplacement:
                        DurableSnapshot = candidate;
                        throw new InvalidOperationException(
                            "fixture-after-replacement");
                    case StoreMode.TemporaryValidationRejected:
                        return new PlayerAccountStoreResultV1(
                            PlayerAccountStoreStatusV1.ValidationRejected,
                            "temporary-readback-validation-failed",
                            null);
                    case StoreMode.ActiveReadBackFailure:
                        DurableSnapshot = candidate;
                        return new PlayerAccountStoreResultV1(
                            PlayerAccountStoreStatusV1.IoFailure,
                            "active-readback-invalid-after-atomic-replace",
                            null);
                    case StoreMode.SuccessWithOldSnapshot:
                        return new PlayerAccountStoreResultV1(
                            PlayerAccountStoreStatusV1.Saved,
                            string.Empty,
                            original);
                    default:
                        DurableSnapshot = candidate;
                        return new PlayerAccountStoreResultV1(
                            PlayerAccountStoreStatusV1.Saved,
                            string.Empty,
                            candidate);
                }
            }
        }

        private sealed class Fixture : IDisposable
        {
            private Fixture(
                string suffix,
                StableId characterId,
                PlayerAccountSaveAuthorityV1 accountAuthority,
                CharacterCompositionCoordinatorV1 composition,
                ControlledStore store,
                ProductionCharacterRuntimeGraphV1 graph,
                CollectedRunRewardPreparedTransferAuthorityV1 prepared,
                CollectedRunRewardTransferReceiptAuthorityV1 receipts)
            {
                Suffix = suffix;
                CharacterId = characterId;
                AccountAuthority = accountAuthority;
                Composition = composition;
                Store = store;
                Graph = graph;
                Prepared = prepared;
                Receipts = receipts;
                Persistence = new ProductionCollectedRunRewardPersistenceV2(
                    composition,
                    prepared,
                    receipts,
                    characterId);
            }

            public string Suffix { get; }
            public StableId CharacterId { get; }
            public PlayerAccountSaveAuthorityV1 AccountAuthority { get; }
            public CharacterCompositionCoordinatorV1 Composition { get; }
            public ControlledStore Store { get; }
            public ProductionCharacterRuntimeGraphV1 Graph { get; }
            public CollectedRunRewardPreparedTransferAuthorityV1 Prepared { get; }
            public CollectedRunRewardTransferReceiptAuthorityV1 Receipts { get; }
            public ProductionCollectedRunRewardPersistenceV2 Persistence { get; }

            public static Fixture Create(string suffix, StoreMode mode)
            {
                StableId characterId = Id("character-instance." + suffix);
                StableId classId = Id("loadout-profile.striker");
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
                ProductionCharacterRuntimeGraphFactoryV1 factory =
                    ProductionCharacterRuntimeGraphFactoryV1
                        .CreateVerticalSliceDefaults();
                ICharacterRuntimeGraphV1 starter = factory.CreateStarter(
                    0,
                    characterId,
                    classId,
                    "Persistence Pilot " + suffix,
                    route);
                IReadOnlyList<SaveComponentSnapshotV1> components =
                    PlayerAccountRestoreCoordinatorV1.ExportComponents(
                        starter.SaveAdapters);
                starter.Dispose();
                var character = new CharacterInstanceSnapshotV1(
                    characterId,
                    classId,
                    0,
                    "Persistence Pilot " + suffix,
                    0L,
                    components);
                var slots = new CharacterInstanceSnapshotV1[
                    PlayerAccountSnapshotV1.CharacterSlotCount];
                slots[0] = character;
                var account = new PlayerAccountSnapshotV1(
                    Id("account." + suffix),
                    0L,
                    slots,
                    null);
                var accountAuthority = new PlayerAccountSaveAuthorityV1(account);
                var store = new ControlledStore(mode, account);
                var composition = new CharacterCompositionCoordinatorV1(
                    accountAuthority,
                    factory,
                    store.Save);
                CharacterCompositionResultV1 selected = composition.Select(0);
                Assert.That(selected.Succeeded, Is.True, selected.Diagnostic);
                var graph = (ProductionCharacterRuntimeGraphV1)
                    composition.ActiveRuntime;
                var rewardApplication = new RewardApplicationServiceV1(
                    Id("authority.reward-application-" + suffix),
                    new MoneyRewardChildAuthorityV1(graph.MoneyWallet),
                    new ScrapRewardChildAuthorityV1(graph.ScrapWallet),
                    new PlayerHoldingsRewardChildAuthorityV1(
                        graph.LoadoutRuntime.Holdings,
                        graph.LoadoutRuntime.CatalogAdapter));
                ProductionCollectedRunRewardRuntimeRegistryV2
                    .BindRewardApplication(characterId, rewardApplication);
                ProductionCollectedRunRewardRuntimeRegistryV2
                    .BindRuntime(graph, composition);
                RewardApplicationServiceV1 resolvedReward;
                CollectedRunRewardPreparedTransferAuthorityV1 prepared;
                CollectedRunRewardTransferReceiptAuthorityV1 receipts;
                Assert.That(ProductionCollectedRunRewardRuntimeRegistryV2
                    .TryResolve(
                        characterId,
                        out resolvedReward,
                        out prepared,
                        out receipts), Is.True);
                Assert.That(resolvedReward, Is.SameAs(rewardApplication));
                return new Fixture(
                    suffix,
                    characterId,
                    accountAuthority,
                    composition,
                    store,
                    graph,
                    prepared,
                    receipts);
            }

            public CollectedRunRewardPreparedTransferV1 Awaiting(
                StableId selectedCharacter = null)
            {
                ProgressionContext progression = ProgressionContext.Create(
                    12,
                    8,
                    Id("difficulty.normal"),
                    0,
                    new[] { Id("progression-tag.campaign") });
                return CollectedRunRewardPreparedTransferV1
                    .AwaitingAcceptedEnd(
                        Id("custody." + Suffix),
                        Id("operation.prepare-" + Suffix),
                        Id("run-instance." + Suffix),
                        1L,
                        selectedCharacter ?? CharacterId,
                        Graph.Character.Revision,
                        Graph.Character.Fingerprint,
                        Id("operation.end-" + Suffix),
                        Fingerprint("end-command-" + Suffix),
                        1UL,
                        1,
                        progression,
                        Fingerprint("event-" + Suffix),
                        Graph.MoneyWallet.Sequence,
                        Graph.ScrapWallet.Sequence,
                        Graph.LoadoutRuntime.Holdings.Sequence,
                        new Dictionary<string, string>
                        {
                            {
                                "money",
                                Graph.MoneyWallet.CurrentSnapshot.Fingerprint
                            },
                            {
                                "scrap",
                                Graph.ScrapWallet.ExportSnapshot().Fingerprint
                            },
                            {
                                "holdings",
                                Graph.LoadoutRuntime.Holdings.ExportSnapshot()
                                    .Fingerprint
                            },
                        },
                        Array.Empty<CollectedRunRewardTransferItemV1>(),
                        Array.Empty<ShooterMover.Domain.Equipment.EquipmentInstance>(),
                        Array.Empty<StrongboxInstanceContextV1>());
            }

            public CollectedRunRewardTransferReceiptV1 Receipt(
                CollectedRunRewardPreparedTransferV1 prepared)
            {
                return new CollectedRunRewardTransferReceiptV1(
                    prepared.TransferOperationStableId,
                    prepared.BatchFingerprint,
                    prepared.RunStableId,
                    prepared.LifecycleGeneration,
                    prepared.AcceptedMissionResultStableId,
                    prepared.AcceptedMissionResultFingerprint,
                    prepared.SelectedCharacterStableId,
                    Array.Empty<StableId>(),
                    new Dictionary<string, string>
                    {
                        {
                            CollectedRunRewardTransferCoordinatorV2
                                .ApplicationPlanAuthorityKey,
                            prepared.ApplicationPlanFingerprint
                        },
                    });
            }

            public void Dispose()
            {
                Composition.Dispose();
                ProductionCollectedRunRewardRuntimeRegistryV2.Release(
                    CharacterId);
            }
        }
    }
}
