using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Tests.EditMode.Persistence.Components
{
    public sealed class SaveAdaptersV1Tests
    {
        [Test]
        public void RoundTripCharacterContainingEverySupportedComponent()
        {
            SaveComponentDefinitionV1[] definitions = AllDefinitions();
            var sourceAuthorities = new List<FixtureAuthority>();
            var sourceAdapters = new List<ISaveComponentAdapterV1>();
            for (int index = 0; index < definitions.Length; index++)
            {
                var authority = new FixtureAuthority(FixtureSnapshotV1.CreateCanonical(
                    "source-" + index,
                    index + 1,
                    new[]
                    {
                        new FixtureReceiptV1(
                            "operation-" + index,
                            index == 1 ? "weapon.shared" : "definition-" + index,
                            "instance-" + index,
                            index == 6 ? "opened" : "applied"),
                    }));
                sourceAuthorities.Add(authority);
                sourceAdapters.Add(CreateAdapter(
                    definitions[index],
                    authority,
                    "source-" + index));
            }

            IReadOnlyList<SaveComponentSnapshotV1> components =
                PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    sourceAdapters);
            PlayerAccountSnapshotV1 account = AccountWithCharacter(
                0,
                "character.alpha",
                components);

            string encoded = PlayerAccountFileCodecV1.Encode(account);
            PlayerAccountSnapshotV1 decoded;
            string decodeError;
            Assert.That(PlayerAccountFileCodecV1.TryDecode(
                encoded,
                out decoded,
                out decodeError), Is.True, decodeError);
            Assert.That(decoded.Fingerprint, Is.EqualTo(account.Fingerprint));
            Assert.That(
                decoded.CharacterAt(0).Components.Count,
                Is.EqualTo(definitions.Length));

            var targetAuthorities = new List<FixtureAuthority>();
            var targetAdapters = new List<ISaveComponentAdapterV1>();
            for (int index = 0; index < definitions.Length; index++)
            {
                var target = new FixtureAuthority(FixtureSnapshotV1.CreateCanonical(
                    "source-" + index,
                    0,
                    Array.Empty<FixtureReceiptV1>()));
                targetAuthorities.Add(target);
                targetAdapters.Add(CreateAdapter(
                    definitions[index],
                    target,
                    "source-" + index));
            }

            var coordinator = new PlayerAccountRestoreCoordinatorV1();
            PlayerAccountRestoreResultV1 restored = coordinator.Restore(
                decoded,
                new[]
                {
                    new CharacterSaveRestoreBindingV1(
                        0,
                        StableId.Parse("character.alpha"),
                        targetAdapters),
                });

            Assert.That(restored.Status, Is.EqualTo(
                PlayerAccountRestoreStatusV1.Restored));
            for (int index = 0; index < definitions.Length; index++)
            {
                Assert.That(
                    targetAuthorities[index].Current.Fingerprint,
                    Is.EqualTo(sourceAuthorities[index].Current.Fingerprint));
                Assert.That(targetAuthorities[index].ApplyCount, Is.EqualTo(1));
            }
            Assert.That(
                targetAuthorities[6].Current.Receipts[0].Outcome,
                Is.EqualTo("opened"));
        }

        [Test]
        public void ExistingXpAndMoneyReplayHistoriesSurviveTypedAdapters()
        {
            PlayerExperienceCurveV1 curve = new PlayerExperienceCurveV1(
                100L,
                100L,
                50,
                new SoftActivationCurveParameters(0.1, 10L, 10L));
            PlayerExperienceAuthorityV1 sourceXp =
                CreateExperienceAuthority(curve);
            var xpRequest = new PlayerExperienceGrantRequestV1(
                StableId.Parse("xp-source.save-adapter"),
                125L);
            sourceXp.Grant(xpRequest);

            var sourceMoney = new MoneyWalletService();
            MoneyTransactionCommand moneyGrant =
                MoneyTransactionCommand.CreateGrant(
                    StableId.Parse("transaction.save-adapter-grant"),
                    StableId.Parse("operation.save-adapter-grant"),
                    40L);
            MoneyTransactionCommand moneyRejected =
                MoneyTransactionCommand.CreateSpend(
                    StableId.Parse("transaction.save-adapter-rejected"),
                    StableId.Parse("operation.save-adapter-rejected"),
                    50L,
                    1L);
            sourceMoney.Apply(moneyGrant);
            sourceMoney.Apply(moneyRejected);

            ISaveComponentAdapterV1 sourceXpAdapter =
                KnownSaveComponentAdaptersV1.PlayerExperience(
                    sourceXp.ExportSnapshot,
                    snapshot => ValidateExperienceSnapshot(curve, snapshot),
                    snapshot => ApplyExperienceSnapshot(sourceXp, snapshot));
            ISaveComponentAdapterV1 sourceMoneyAdapter =
                KnownSaveComponentAdaptersV1.MoneyWallet(
                    () => sourceMoney.CurrentSnapshot,
                    ValidateMoneySnapshot,
                    snapshot => ApplyMoneySnapshot(sourceMoney, snapshot));

            PlayerAccountSnapshotV1 account = AccountWithCharacter(
                0,
                "character.real-authorities",
                PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    new[] { sourceXpAdapter, sourceMoneyAdapter }));

            PlayerExperienceAuthorityV1 targetXp =
                CreateExperienceAuthority(curve);
            var targetMoney = new MoneyWalletService();
            ISaveComponentAdapterV1 targetXpAdapter =
                KnownSaveComponentAdaptersV1.PlayerExperience(
                    targetXp.ExportSnapshot,
                    snapshot => ValidateExperienceSnapshot(curve, snapshot),
                    snapshot => ApplyExperienceSnapshot(targetXp, snapshot));
            ISaveComponentAdapterV1 targetMoneyAdapter =
                KnownSaveComponentAdaptersV1.MoneyWallet(
                    () => targetMoney.CurrentSnapshot,
                    ValidateMoneySnapshot,
                    snapshot => ApplyMoneySnapshot(targetMoney, snapshot));

            PlayerAccountRestoreResultV1 restore =
                new PlayerAccountRestoreCoordinatorV1().Restore(
                    account,
                    new[]
                    {
                        new CharacterSaveRestoreBindingV1(
                            0,
                            StableId.Parse("character.real-authorities"),
                            new[] { targetXpAdapter, targetMoneyAdapter }),
                    });

            Assert.That(restore.Succeeded, Is.True, restore.RejectionCode);
            Assert.That(targetXp.Grant(xpRequest).Status, Is.EqualTo(
                PlayerExperienceGrantStatusV1.DuplicateNoChange));
            Assert.That(targetMoney.Apply(moneyGrant).Status, Is.EqualTo(
                MoneyWalletTransactionStatus.DuplicateNoChange));
            MoneyWalletChangeFact rejectedReplay =
                targetMoney.Apply(moneyRejected);
            Assert.That(rejectedReplay.Status, Is.EqualTo(
                MoneyWalletTransactionStatus.DuplicateNoChange));
            Assert.That(rejectedReplay.OriginalStatus, Is.EqualTo(
                MoneyWalletTransactionStatus.InsufficientFunds));
            Assert.That(targetXp.CurrentSnapshot.Sequence, Is.EqualTo(1L));
            Assert.That(targetMoney.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void SixSlotsRemainIsolated()
        {
            SaveComponentDefinitionV1 definition =
                KnownSaveComponentDefinitionsV1.PlayerExperience();
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            var bindings = new List<CharacterSaveRestoreBindingV1>();
            var targets = new List<FixtureAuthority>();

            for (int index = 0;
                index < PlayerAccountSnapshotV1.CharacterSlotCount;
                index++)
            {
                string characterId = "character.slot-" + index;
                string ownerId = "owner-slot-" + index;
                var source = new FixtureAuthority(
                    FixtureSnapshotV1.CreateCanonical(
                        ownerId,
                        index + 10,
                        new[]
                        {
                            new FixtureReceiptV1(
                                "operation-slot-" + index,
                                "xp.enemy",
                                "enemy-instance-" + index,
                                "applied"),
                        }));
                ISaveComponentAdapterV1 sourceAdapter = CreateAdapter(
                    definition,
                    source,
                    ownerId);
                slots[index] = Character(
                    index,
                    characterId,
                    new[] { sourceAdapter.ExportComponent() });

                var target = new FixtureAuthority(
                    FixtureSnapshotV1.CreateCanonical(
                        ownerId,
                        0,
                        Array.Empty<FixtureReceiptV1>()));
                targets.Add(target);
                bindings.Add(new CharacterSaveRestoreBindingV1(
                    index,
                    StableId.Parse(characterId),
                    new[] { CreateAdapter(definition, target, ownerId) }));
            }

            var account = new PlayerAccountSnapshotV1(
                StableId.Parse("account.six-slot-test"),
                0L,
                slots,
                null);
            PlayerAccountRestoreResultV1 result =
                new PlayerAccountRestoreCoordinatorV1().Restore(
                    account,
                    bindings);

            Assert.That(result.Succeeded, Is.True, result.RejectionCode);
            for (int index = 0; index < targets.Count; index++)
            {
                Assert.That(targets[index].Current.OwnerId,
                    Is.EqualTo("owner-slot-" + index));
                Assert.That(targets[index].Current.Sequence,
                    Is.EqualTo(index + 10));
                Assert.That(targets[index].Current.Receipts.Single().InstanceId,
                    Is.EqualTo("enemy-instance-" + index));
            }
        }

        [Test]
        public void DuplicateDefinitionsRemainDistinctInstancesAndReplaySurvives()
        {
            var snapshot = FixtureSnapshotV1.CreateCanonical(
                "inventory-owner",
                2,
                new[]
                {
                    new FixtureReceiptV1(
                        "operation.add-first",
                        "weapon.shared-shotgun",
                        "equipment.instance-one",
                        "applied"),
                    new FixtureReceiptV1(
                        "operation.add-second",
                        "weapon.shared-shotgun",
                        "equipment.instance-two",
                        "applied"),
                });
            string payload = CanonicalSnapshotCodecV1.Serialize(snapshot);
            FixtureSnapshotV1 restored;
            string error;

            Assert.That(CanonicalSnapshotCodecV1.TryDeserialize(
                payload,
                out restored,
                out error), Is.True, error);
            Assert.That(restored.Receipts.Count, Is.EqualTo(2));
            Assert.That(restored.Receipts.Select(item => item.DefinitionId)
                .Distinct().Single(), Is.EqualTo("weapon.shared-shotgun"));
            Assert.That(restored.Receipts.Select(item => item.InstanceId)
                .Distinct().Count(), Is.EqualTo(2));
            Assert.That(restored.Receipts.Select(item => item.OperationId),
                Is.EquivalentTo(new[]
                {
                    "operation.add-first",
                    "operation.add-second",
                }));
        }

        [Test]
        public void AggregateSemanticMismatchRejectsBeforeAnyAuthorityChanges()
        {
            SaveComponentDefinitionV1 holdings =
                KnownSaveComponentDefinitionsV1.PlayerHoldings();
            SaveComponentDefinitionV1 loadout =
                KnownSaveComponentDefinitionsV1.ExactInstanceLoadout();
            var holdingsSource = new FixtureAuthority(
                FixtureSnapshotV1.CreateCanonical(
                    "holdings-owner", 2, Receipts("holdings")));
            var loadoutSource = new FixtureAuthority(
                FixtureSnapshotV1.CreateCanonical(
                    "loadout-owner", 5, Receipts("loadout")));
            PlayerAccountSnapshotV1 account = AccountWithCharacter(
                0,
                "character.semantic-mismatch",
                new[]
                {
                    CreateAdapter(
                        holdings, holdingsSource, "holdings-owner")
                        .ExportComponent(),
                    CreateAdapter(
                        loadout, loadoutSource, "loadout-owner")
                        .ExportComponent(),
                });
            var holdingsTarget = new FixtureAuthority(
                FixtureSnapshotV1.CreateCanonical(
                    "holdings-owner", 0, Array.Empty<FixtureReceiptV1>()));
            var loadoutTarget = new FixtureAuthority(
                FixtureSnapshotV1.CreateCanonical(
                    "loadout-owner", 0, Array.Empty<FixtureReceiptV1>()));
            var coordinator = new PlayerAccountRestoreCoordinatorV1(
                null,
                ignored => SaveComponentValidationResultV1.Reject(
                    "loadout-references-missing-holding-instance"));

            PlayerAccountRestoreResultV1 result = coordinator.Restore(
                account,
                new[]
                {
                    new CharacterSaveRestoreBindingV1(
                        0,
                        StableId.Parse("character.semantic-mismatch"),
                        new[]
                        {
                            CreateAdapter(
                                holdings, holdingsTarget, "holdings-owner"),
                            CreateAdapter(
                                loadout, loadoutTarget, "loadout-owner"),
                        }),
                });

            Assert.That(result.Status, Is.EqualTo(
                PlayerAccountRestoreStatusV1.ValidationRejected));
            Assert.That(result.RejectionCode, Is.EqualTo(
                "loadout-references-missing-holding-instance"));
            Assert.That(holdingsTarget.ApplyCount, Is.Zero);
            Assert.That(loadoutTarget.ApplyCount, Is.Zero);
        }

        [Test]
        public void CorruptComponentRejectsBeforeAnyAuthorityChanges()
        {
            SaveComponentDefinitionV1 xp =
                KnownSaveComponentDefinitionsV1.PlayerExperience();
            SaveComponentDefinitionV1 money =
                KnownSaveComponentDefinitionsV1.StrongboxState(true);
            var xpSource = new FixtureAuthority(FixtureSnapshotV1.CreateCanonical(
                "xp-owner",
                3,
                Receipts("xp")));
            var moneySource = new FixtureAuthority(FixtureSnapshotV1.CreateCanonical(
                "money-owner",
                4,
                Receipts("money")));
            SaveComponentSnapshotV1 validXp = CreateAdapter(
                xp,
                xpSource,
                "xp-owner").ExportComponent();
            SaveComponentSnapshotV1 validMoney = CreateAdapter(
                money,
                moneySource,
                "money-owner").ExportComponent();
            string corruptPayload = validMoney.CanonicalPayload.Substring(
                0,
                validMoney.CanonicalPayload.Length - 1) + "x";
            var corruptMoney = new SaveComponentSnapshotV1(
                validMoney.ComponentStableId,
                validMoney.SchemaVersion,
                validMoney.ContentVersion,
                corruptPayload);
            PlayerAccountSnapshotV1 account = AccountWithCharacter(
                0,
                "character.corrupt-test",
                new[] { validXp, corruptMoney });

            var xpTarget = new FixtureAuthority(FixtureSnapshotV1.CreateCanonical(
                "xp-owner", 0, Array.Empty<FixtureReceiptV1>()));
            var moneyTarget = new FixtureAuthority(FixtureSnapshotV1.CreateCanonical(
                "money-owner", 0, Array.Empty<FixtureReceiptV1>()));
            PlayerAccountRestoreResultV1 result =
                new PlayerAccountRestoreCoordinatorV1().Restore(
                    account,
                    new[]
                    {
                        new CharacterSaveRestoreBindingV1(
                            0,
                            StableId.Parse("character.corrupt-test"),
                            new[]
                            {
                                CreateAdapter(xp, xpTarget, "xp-owner"),
                                CreateAdapter(money, moneyTarget, "money-owner"),
                            }),
                    });

            Assert.That(result.Status, Is.EqualTo(
                PlayerAccountRestoreStatusV1.ValidationRejected));
            Assert.That(xpTarget.ApplyCount, Is.Zero);
            Assert.That(moneyTarget.ApplyCount, Is.Zero);
            Assert.That(xpTarget.Current.Sequence, Is.Zero);
            Assert.That(moneyTarget.Current.Sequence, Is.Zero);
        }

        [Test]
        public void MissingOptionalAcceptedButMissingRequiredRejected()
        {
            var xpDefinition =
                KnownSaveComponentDefinitionsV1.PlayerExperience(true);
            var optionalBox =
                KnownSaveComponentDefinitionsV1.StrongboxState(false);
            var source = new FixtureAuthority(FixtureSnapshotV1.CreateCanonical(
                "xp-owner", 1, Receipts("xp")));
            PlayerAccountSnapshotV1 account = AccountWithCharacter(
                0,
                "character.optional-test",
                new[] { CreateAdapter(xpDefinition, source, "xp-owner")
                    .ExportComponent() });

            var target = new FixtureAuthority(FixtureSnapshotV1.CreateCanonical(
                "xp-owner", 0, Array.Empty<FixtureReceiptV1>()));
            var optionalTarget = new FixtureAuthority(
                FixtureSnapshotV1.CreateCanonical(
                    "box-owner", 0, Array.Empty<FixtureReceiptV1>()));
            var coordinator = new PlayerAccountRestoreCoordinatorV1();
            PlayerAccountRestoreResultV1 accepted = coordinator.Restore(
                account,
                new[]
                {
                    new CharacterSaveRestoreBindingV1(
                        0,
                        StableId.Parse("character.optional-test"),
                        new[]
                        {
                            CreateAdapter(xpDefinition, target, "xp-owner"),
                            CreateAdapter(optionalBox, optionalTarget, "box-owner"),
                        }),
                });
            Assert.That(accepted.Succeeded, Is.True, accepted.RejectionCode);
            Assert.That(optionalTarget.ApplyCount, Is.Zero);

            var missingRequiredTarget = new FixtureAuthority(
                FixtureSnapshotV1.CreateCanonical(
                    "money-owner", 0, Array.Empty<FixtureReceiptV1>()));
            PlayerAccountRestoreResultV1 rejected = coordinator.Restore(
                account,
                new[]
                {
                    new CharacterSaveRestoreBindingV1(
                        0,
                        StableId.Parse("character.optional-test"),
                        new[]
                        {
                            CreateAdapter(
                                KnownSaveComponentDefinitionsV1.MoneyWallet(true),
                                missingRequiredTarget,
                                "money-owner"),
                        }),
                });
            Assert.That(rejected.Status, Is.EqualTo(
                PlayerAccountRestoreStatusV1.ValidationRejected));
            Assert.That(missingRequiredTarget.ApplyCount, Is.Zero);
        }

        [Test]
        public void UnsupportedSchemaDoesNotOverwriteLastValidSave()
        {
            var files = new MemoryAtomicFilePort();
            var store = new AtomicPlayerAccountStoreV1(
                files,
                "account.active",
                "account.temp",
                "account.backup",
                ValidateKnownComponentSchemas);
            PlayerAccountSnapshotV1 valid = AccountWithCharacter(
                0,
                "character.store-test",
                new[]
                {
                    new SaveComponentSnapshotV1(
                        KnownSaveComponentDefinitionsV1.PlayerExperience()
                            .ComponentStableId,
                        1,
                        "player-experience-snapshot-v1",
                        CanonicalSnapshotCodecV1.Serialize(
                            FixtureSnapshotV1.CreateCanonical(
                                "xp-owner", 1, Receipts("xp")))),
                });
            Assert.That(store.Save(valid).Status,
                Is.EqualTo(PlayerAccountStoreStatusV1.Saved));
            string previousActive = files.ReadAllText("account.active");

            PlayerAccountSnapshotV1 unsupported = AccountWithCharacter(
                0,
                "character.store-test",
                new[]
                {
                    new SaveComponentSnapshotV1(
                        KnownSaveComponentDefinitionsV1.PlayerExperience()
                            .ComponentStableId,
                        99,
                        "player-experience-snapshot-v1",
                        CanonicalSnapshotCodecV1.Serialize(
                            FixtureSnapshotV1.CreateCanonical(
                                "xp-owner", 2, Receipts("xp-new")))),
                });
            PlayerAccountStoreResultV1 rejected = store.Save(unsupported);

            Assert.That(rejected.Status, Is.EqualTo(
                PlayerAccountStoreStatusV1.ValidationRejected));
            Assert.That(files.ReadAllText("account.active"),
                Is.EqualTo(previousActive));
            Assert.That(store.Load().Snapshot.Fingerprint,
                Is.EqualTo(valid.Fingerprint));
        }

        [Test]
        public void TemporaryWriteInterruptionPreservesPreviousValidSave()
        {
            var files = new MemoryAtomicFilePort();
            var store = new AtomicPlayerAccountStoreV1(
                files,
                "account.active",
                "account.temp",
                "account.backup");
            PlayerAccountSnapshotV1 first = AccountWithCharacter(
                0,
                "character.interruption",
                Array.Empty<SaveComponentSnapshotV1>());
            Assert.That(store.Save(first).Succeeded, Is.True);
            string previous = files.ReadAllText("account.active");

            files.FailNextReadPath = "account.temp";
            PlayerAccountSnapshotV1 second = first.WithCharacter(
                0,
                Character(
                    0,
                    "character.interruption",
                    new[]
                    {
                        new SaveComponentSnapshotV1(
                            StableId.Parse("future.optional-state"),
                            1,
                            "future-v1",
                            "opaque"),
                    }));
            PlayerAccountStoreResultV1 interrupted = store.Save(second);

            Assert.That(interrupted.Status, Is.EqualTo(
                PlayerAccountStoreStatusV1.IoFailure));
            Assert.That(files.ReadAllText("account.active"), Is.EqualTo(previous));
            Assert.That(store.Load().Snapshot.Fingerprint,
                Is.EqualTo(first.Fingerprint));
        }

        [Test]
        public void UnknownOptionalComponentIsRetainedWithoutInterpretation()
        {
            var unknown = new SaveComponentSnapshotV1(
                StableId.Parse("future.seasonal-state"),
                7,
                "season-2030-v7",
                "opaque-future-payload");
            PlayerAccountSnapshotV1 account = AccountWithCharacter(
                0,
                "character.future-state",
                new[] { unknown });
            PlayerAccountRestoreResultV1 result =
                new PlayerAccountRestoreCoordinatorV1().Restore(
                    account,
                    new[]
                    {
                        new CharacterSaveRestoreBindingV1(
                            0,
                            StableId.Parse("character.future-state"),
                            Array.Empty<ISaveComponentAdapterV1>()),
                    });

            Assert.That(result.Succeeded, Is.True, result.RejectionCode);
            Assert.That(result.RetainedUnknownComponents.Count, Is.EqualTo(1));
            Assert.That(result.RetainedUnknownComponents[0].Component,
                Is.SameAs(unknown));
            Assert.That(result.RetainedUnknownComponents[0].CharacterSlotIndex,
                Is.EqualTo(0));
        }

        [Test]
        public void CommitFailureRollsBackEarlierAuthority()
        {
            SaveComponentDefinitionV1 xp =
                KnownSaveComponentDefinitionsV1.PlayerExperience();
            SaveComponentDefinitionV1 money =
                KnownSaveComponentDefinitionsV1.StrongboxState(true);
            var xpSource = new FixtureAuthority(FixtureSnapshotV1.CreateCanonical(
                "xp-owner", 5, Receipts("xp")));
            var moneySource = new FixtureAuthority(FixtureSnapshotV1.CreateCanonical(
                "money-owner", 6, Receipts("money")));
            PlayerAccountSnapshotV1 account = AccountWithCharacter(
                0,
                "character.rollback",
                new[]
                {
                    CreateAdapter(xp, xpSource, "xp-owner").ExportComponent(),
                    CreateAdapter(money, moneySource, "money-owner")
                        .ExportComponent(),
                });

            FixtureSnapshotV1 xpInitial = FixtureSnapshotV1.CreateCanonical(
                "xp-owner", 1, Receipts("initial"));
            var xpTarget = new FixtureAuthority(xpInitial);
            var moneyTarget = new FixtureAuthority(FixtureSnapshotV1.CreateCanonical(
                "money-owner", 1, Receipts("initial")));
            moneyTarget.FailNextApply = true;

            PlayerAccountRestoreResultV1 result =
                new PlayerAccountRestoreCoordinatorV1().Restore(
                    account,
                    new[]
                    {
                        new CharacterSaveRestoreBindingV1(
                            0,
                            StableId.Parse("character.rollback"),
                            new[]
                            {
                                CreateAdapter(xp, xpTarget, "xp-owner"),
                                CreateAdapter(money, moneyTarget, "money-owner"),
                            }),
                    });

            Assert.That(result.Status, Is.EqualTo(
                PlayerAccountRestoreStatusV1.CommitFailedRolledBack));
            Assert.That(xpTarget.Current.Fingerprint,
                Is.EqualTo(xpInitial.Fingerprint));
            Assert.That(moneyTarget.Current.Sequence, Is.EqualTo(1));
        }

        private static SaveComponentValidationResultV1
            ValidateExperienceSnapshot(
                PlayerExperienceCurveV1 curve,
                PlayerExperienceSnapshotV1 snapshot)
        {
            PlayerExperienceImportResultV1 result =
                CreateExperienceAuthority(curve).TryImport(snapshot);
            return result.Status == PlayerExperienceImportStatusV1.Imported
                ? SaveComponentValidationResultV1.Accept()
                : SaveComponentValidationResultV1.Reject(
                    result.RejectionCode);
        }

        private static SaveComponentApplyResultV1 ApplyExperienceSnapshot(
            PlayerExperienceAuthorityV1 authority,
            PlayerExperienceSnapshotV1 snapshot)
        {
            PlayerExperienceImportResultV1 result =
                authority.TryImport(snapshot);
            return result.Status == PlayerExperienceImportStatusV1.Imported
                ? SaveComponentApplyResultV1.Applied()
                : SaveComponentApplyResultV1.Rejected(
                    result.RejectionCode);
        }

        private static SaveComponentValidationResultV1 ValidateMoneySnapshot(
            MoneyWalletSnapshot snapshot)
        {
            MoneyWalletImportResult result =
                new MoneyWalletService().ImportSnapshot(snapshot);
            return result.Status == MoneyWalletImportStatus.Imported
                ? SaveComponentValidationResultV1.Accept()
                : SaveComponentValidationResultV1.Reject(
                    result.RejectionCode);
        }

        private static SaveComponentApplyResultV1 ApplyMoneySnapshot(
            MoneyWalletService authority,
            MoneyWalletSnapshot snapshot)
        {
            MoneyWalletImportResult result = authority.ImportSnapshot(snapshot);
            return result.Status == MoneyWalletImportStatus.Imported
                ? SaveComponentApplyResultV1.Applied()
                : SaveComponentApplyResultV1.Rejected(
                    result.RejectionCode);
        }

        private static PlayerExperienceAuthorityV1
            CreateExperienceAuthority(PlayerExperienceCurveV1 curve)
        {
            return new PlayerExperienceAuthorityV1(
                curve,
                ProgressionContext.Create(
                    42,
                    1,
                    StableId.Parse("difficulty.normal"),
                    0,
                    new[]
                    {
                        StableId.Parse("progression-tag.campaign"),
                    }));
        }

        private static SaveComponentValidationResultV1
            ValidateKnownComponentSchemas(PlayerAccountSnapshotV1 account)
        {
            SaveComponentValidationResultV1 integrity =
                CanonicalSnapshotIntegrityV1.Validate(account);
            if (!integrity.Succeeded)
            {
                return integrity;
            }
            foreach (CharacterInstanceSnapshotV1 character in
                account.CharacterSlots)
            {
                if (character == null) continue;
                foreach (SaveComponentSnapshotV1 component in
                    character.Components.Values)
                {
                    if (component.ComponentStableId
                        == KnownSaveComponentDefinitionsV1.PlayerExperience()
                            .ComponentStableId
                        && component.SchemaVersion != 1)
                    {
                        return SaveComponentValidationResultV1.Reject(
                            "save-component-schema-unsupported");
                    }
                }
            }
            return SaveComponentValidationResultV1.Accept();
        }

        private static SaveComponentDefinitionV1[] AllDefinitions()
        {
            return new[]
            {
                KnownSaveComponentDefinitionsV1.PlayerExperience(),
                KnownSaveComponentDefinitionsV1.PlayerHoldings(),
                KnownSaveComponentDefinitionsV1.MoneyWallet(),
                KnownSaveComponentDefinitionsV1.ScrapWallet(),
                KnownSaveComponentDefinitionsV1.RankedSkillAllocation(),
                KnownSaveComponentDefinitionsV1.ExactInstanceLoadout(),
                KnownSaveComponentDefinitionsV1.StrongboxState(true),
                KnownSaveComponentDefinitionsV1.CharacterStatistics(true),
            };
        }

        private static ISaveComponentAdapterV1 CreateAdapter(
            SaveComponentDefinitionV1 definition,
            FixtureAuthority authority,
            string expectedOwner)
        {
            return new AuthoritySnapshotSaveComponentAdapterV1<FixtureSnapshotV1>(
                definition,
                () => authority.Current,
                snapshot => string.Equals(
                    snapshot.OwnerId,
                    expectedOwner,
                    StringComparison.Ordinal)
                    ? SaveComponentValidationResultV1.Accept()
                    : SaveComponentValidationResultV1.Reject(
                        "fixture-owner-mismatch"),
                authority.Apply);
        }

        private static PlayerAccountSnapshotV1 AccountWithCharacter(
            int slotIndex,
            string characterId,
            IEnumerable<SaveComponentSnapshotV1> components)
        {
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            slots[slotIndex] = Character(slotIndex, characterId, components);
            return new PlayerAccountSnapshotV1(
                StableId.Parse("account.save-adapters-tests"),
                0L,
                slots,
                null);
        }

        private static CharacterInstanceSnapshotV1 Character(
            int slotIndex,
            string characterId,
            IEnumerable<SaveComponentSnapshotV1> components)
        {
            return new CharacterInstanceSnapshotV1(
                StableId.Parse(characterId),
                StableId.Parse("class.test-mech"),
                slotIndex,
                "Test Mech " + slotIndex,
                0L,
                components);
        }

        private static FixtureReceiptV1[] Receipts(string token)
        {
            return new[]
            {
                new FixtureReceiptV1(
                    "operation." + token,
                    "definition." + token,
                    "instance." + token,
                    "applied"),
            };
        }

        private sealed class FixtureAuthority
        {
            public FixtureAuthority(FixtureSnapshotV1 initial)
            {
                Current = initial;
            }

            public FixtureSnapshotV1 Current { get; private set; }

            public int ApplyCount { get; private set; }

            public bool FailNextApply { get; set; }

            public SaveComponentApplyResultV1 Apply(FixtureSnapshotV1 snapshot)
            {
                if (FailNextApply)
                {
                    FailNextApply = false;
                    return SaveComponentApplyResultV1.Rejected(
                        "fixture-apply-failed");
                }
                Current = snapshot;
                ApplyCount++;
                return SaveComponentApplyResultV1.Applied();
            }
        }

        public sealed class FixtureReceiptV1
        {
            public FixtureReceiptV1(
                string operationId,
                string definitionId,
                string instanceId,
                string outcome)
            {
                OperationId = operationId;
                DefinitionId = definitionId;
                InstanceId = instanceId;
                Outcome = outcome;
            }

            public string OperationId { get; }

            public string DefinitionId { get; }

            public string InstanceId { get; }

            public string Outcome { get; }

            public string ToCanonicalString()
            {
                return OperationId + "|" + DefinitionId + "|" + InstanceId
                    + "|" + Outcome;
            }
        }

        public sealed class FixtureSnapshotV1
        {
            public const int CurrentSchemaVersion = 1;
            private readonly ReadOnlyCollection<FixtureReceiptV1> receipts;

            public FixtureSnapshotV1(
                int schemaVersion,
                string ownerId,
                long sequence,
                IEnumerable<FixtureReceiptV1> receipts,
                string fingerprint)
            {
                SchemaVersion = schemaVersion;
                OwnerId = ownerId;
                Sequence = sequence;
                var copy = new List<FixtureReceiptV1>(
                    receipts ?? Array.Empty<FixtureReceiptV1>());
                copy.Sort((left, right) => string.CompareOrdinal(
                    left.OperationId,
                    right.OperationId));
                this.receipts = new ReadOnlyCollection<FixtureReceiptV1>(copy);
                Fingerprint = fingerprint;
            }

            public int SchemaVersion { get; }

            public string OwnerId { get; }

            public long Sequence { get; }

            public IReadOnlyList<FixtureReceiptV1> Receipts
            {
                get { return receipts; }
            }

            public string Fingerprint { get; }

            public static FixtureSnapshotV1 CreateCanonical(
                string ownerId,
                long sequence,
                IEnumerable<FixtureReceiptV1> receipts)
            {
                var provisional = new FixtureSnapshotV1(
                    CurrentSchemaVersion,
                    ownerId,
                    sequence,
                    receipts,
                    string.Empty);
                return new FixtureSnapshotV1(
                    provisional.SchemaVersion,
                    provisional.OwnerId,
                    provisional.Sequence,
                    provisional.Receipts,
                    ComputeFingerprint(provisional));
            }

            public static string ComputeFingerprint(
                FixtureSnapshotV1 snapshot)
            {
                string canonical = snapshot.SchemaVersion
                    .ToString(CultureInfo.InvariantCulture)
                    + "|" + snapshot.OwnerId
                    + "|" + snapshot.Sequence.ToString(
                        CultureInfo.InvariantCulture)
                    + "|" + string.Join(
                        ";",
                        snapshot.Receipts.Select(
                            item => item.ToCanonicalString()));
                using (SHA256 sha = SHA256.Create())
                {
                    return BitConverter.ToString(sha.ComputeHash(
                        Encoding.UTF8.GetBytes(canonical)))
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();
                }
            }
        }

        private sealed class MemoryAtomicFilePort : IAtomicSaveFilePortV1
        {
            private readonly Dictionary<string, string> files =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public string FailNextReadPath { get; set; }

            public bool Exists(string path)
            {
                return files.ContainsKey(path);
            }

            public string ReadAllText(string path)
            {
                if (string.Equals(
                    FailNextReadPath,
                    path,
                    StringComparison.Ordinal))
                {
                    FailNextReadPath = null;
                    throw new InvalidOperationException("simulated-read-failure");
                }
                return files[path];
            }

            public void WriteAllText(string path, string contents)
            {
                files[path] = contents;
            }

            public void Move(string sourcePath, string destinationPath)
            {
                files[destinationPath] = files[sourcePath];
                files.Remove(sourcePath);
            }

            public void Replace(
                string sourcePath,
                string destinationPath,
                string backupPath)
            {
                string source = files[sourcePath];
                string previous = files[destinationPath];
                files[backupPath] = previous;
                files[destinationPath] = source;
                files.Remove(sourcePath);
            }

            public void Delete(string path)
            {
                files.Remove(path);
            }
        }
    }
}
