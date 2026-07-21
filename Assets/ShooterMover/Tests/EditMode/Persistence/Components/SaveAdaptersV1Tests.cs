using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Persistence.Components
{
    public sealed class RealAuthoritySaveAdaptersV1Tests
    {
        [Test]
        public void PlayerExperienceRealAuthorityRoundTripPreservesReplay()
        {
            PlayerExperienceCurveV1 curve = ConstantCurve();
            PlayerExperienceAuthorityV1 source = ExperienceAuthority(curve);
            var request = new PlayerExperienceGrantRequestV1(
                Id("xp-source.real-roundtrip"),
                250L);
            Assert.That(source.Grant(request).Status,
                Is.EqualTo(PlayerExperienceGrantStatusV1.Applied));

            PlayerExperienceAuthorityV1 target = ExperienceAuthority(curve);
            ISaveComponentAdapterV1 sourceAdapter = ExperienceAdapter(source, curve);
            ISaveComponentAdapterV1 targetAdapter = ExperienceAdapter(target, curve);
            PlayerAccountSnapshotV1 decoded = FileRoundTrip(sourceAdapter.ExportComponent());

            PlayerAccountRestoreResultV1 restored = Restore(decoded, targetAdapter);

            Assert.That(restored.Succeeded, Is.True, restored.RejectionCode);
            Assert.That(target.ExportSnapshot().Fingerprint,
                Is.EqualTo(source.ExportSnapshot().Fingerprint));
            long sequence = target.CurrentSnapshot.Sequence;
            Assert.That(target.Grant(request).Status,
                Is.EqualTo(PlayerExperienceGrantStatusV1.DuplicateNoChange));
            Assert.That(target.CurrentSnapshot.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void MoneyWalletRealAuthorityRoundTripPreservesAcceptedAndRejectedReplay()
        {
            var source = new MoneyWalletService();
            MoneyTransactionCommand grant = MoneyTransactionCommand.CreateGrant(
                Id("transaction.money.real-grant"),
                Id("operation.money.real-grant"),
                40L);
            MoneyTransactionCommand rejected = MoneyTransactionCommand.CreateSpend(
                Id("transaction.money.real-rejected"),
                Id("operation.money.real-rejected"),
                50L,
                1L);
            source.Apply(grant);
            source.Apply(rejected);

            var target = new MoneyWalletService();
            PlayerAccountSnapshotV1 decoded = FileRoundTrip(
                MoneyAdapter(source).ExportComponent());
            PlayerAccountRestoreResultV1 restored = Restore(
                decoded,
                MoneyAdapter(target));

            Assert.That(restored.Succeeded, Is.True, restored.RejectionCode);
            Assert.That(target.CurrentSnapshot.Fingerprint,
                Is.EqualTo(source.CurrentSnapshot.Fingerprint));
            long sequence = target.Sequence;
            Assert.That(target.Apply(grant).Status,
                Is.EqualTo(MoneyWalletTransactionStatus.DuplicateNoChange));
            MoneyWalletChangeFact rejectedReplay = target.Apply(rejected);
            Assert.That(rejectedReplay.Status,
                Is.EqualTo(MoneyWalletTransactionStatus.DuplicateNoChange));
            Assert.That(rejectedReplay.OriginalStatus,
                Is.EqualTo(MoneyWalletTransactionStatus.InsufficientFunds));
            Assert.That(target.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void ScrapWalletRealAuthorityRoundTripPreservesReplay()
        {
            StableId authorityId = Id("authority.scrap.real-roundtrip");
            StableId currencyId = Id("currency.scrap");
            var source = new ScrapWalletServiceV1(authorityId, currencyId);
            ScrapTransactionCommandV1 grant = ScrapGrant(
                authorityId,
                currencyId,
                "real-grant",
                30L,
                0L);
            ScrapTransactionCommandV1 rejected = ScrapSpend(
                authorityId,
                currencyId,
                "real-rejected",
                99L,
                1L);
            Assert.That(source.Apply(grant).Status,
                Is.EqualTo(EconomyTransactionStatusV1.Applied));
            source.Apply(rejected);

            var target = new ScrapWalletServiceV1(authorityId, currencyId);
            PlayerAccountSnapshotV1 decoded = FileRoundTrip(
                ScrapAdapter(source, authorityId, currencyId).ExportComponent());
            PlayerAccountRestoreResultV1 restored = Restore(
                decoded,
                ScrapAdapter(target, authorityId, currencyId));

            Assert.That(restored.Succeeded, Is.True, restored.RejectionCode);
            Assert.That(target.ExportSnapshot().Fingerprint,
                Is.EqualTo(source.ExportSnapshot().Fingerprint));
            long sequence = target.Sequence;
            Assert.That(target.Apply(grant).Status,
                Is.EqualTo(EconomyTransactionStatusV1.DuplicateNoChange));
            Assert.That(target.Apply(rejected).Status,
                Is.EqualTo(EconomyTransactionStatusV1.DuplicateNoChange));
            Assert.That(target.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void HoldingsRealAuthorityRoundTripPreservesDistinctInstancesAndUnopenedBox()
        {
            StableId authorityId = Id("authority.holdings.real-roundtrip");
            var source = new PlayerHoldingsService(
                authorityId,
                1000L,
                new AcceptingEquipmentValidator());
            StableId sharedDefinition = Id("equipment-definition.shared-shotgun");
            EquipmentInstance first = Equipment(
                "equipment-instance.shared-shotgun-a",
                sharedDefinition);
            EquipmentInstance second = Equipment(
                "equipment-instance.shared-shotgun-b",
                sharedDefinition);
            StableId boxId = Id("strongbox.instance.unopened-real");
            PlayerHoldingsCommandV1 firstCommand = AddEquipmentCommand(
                source,
                first,
                "first",
                0L);
            PlayerHoldingsCommandV1 secondCommand = AddEquipmentCommand(
                source,
                second,
                "second",
                1L);
            PlayerHoldingsCommandV1 boxCommand =
                PlayerHoldingsCommandV1.AddStrongbox(
                    Id("transaction.holdings.box"),
                    Id("operation.holdings.box"),
                    authorityId,
                    Id("strongbox.tier.test"),
                    boxId,
                    HoldingProvenanceV1.Create(
                        Id("grant.holdings.box"),
                        Id("source.holdings.box")),
                    2L);
            Assert.That(source.Apply(firstCommand).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(source.Apply(secondCommand).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(source.Apply(boxCommand).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));

            var target = new PlayerHoldingsService(
                authorityId,
                1000L,
                new AcceptingEquipmentValidator());
            PlayerAccountSnapshotV1 decoded = FileRoundTrip(
                HoldingsAdapter(source, authorityId).ExportComponent());
            PlayerAccountRestoreResultV1 restored = Restore(
                decoded,
                HoldingsAdapter(target, authorityId));

            Assert.That(restored.Succeeded, Is.True, restored.RejectionCode);
            PlayerHoldingsSnapshotV1 snapshot = target.ExportSnapshot();
            Assert.That(snapshot.Fingerprint,
                Is.EqualTo(source.ExportSnapshot().Fingerprint));
            Assert.That(snapshot.UniqueHoldings.Count, Is.EqualTo(3));
            Assert.That(snapshot.UniqueHoldings
                .Where(item => item.RewardKind
                    == RewardGrantKindV1.EquipmentReference)
                .Select(item => item.DefinitionStableId)
                .Distinct().Single(), Is.EqualTo(sharedDefinition));
            Assert.That(snapshot.UniqueHoldings
                .Where(item => item.RewardKind
                    == RewardGrantKindV1.EquipmentReference)
                .Select(item => item.InstanceStableId)
                .Distinct().Count(), Is.EqualTo(2));
            Assert.That(snapshot.UniqueHoldings.Any(item =>
                item.RewardKind == RewardGrantKindV1.Strongbox
                && item.InstanceStableId == boxId), Is.True);
            long sequence = target.Sequence;
            Assert.That(target.Apply(firstCommand).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange));
            Assert.That(target.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void RankedSkillRealAuthorityRoundTripPreservesAllocationAndBlocksReplayMutation()
        {
            RankedSkillCatalogV2 catalog = RankedSkillSampleCatalogV2.Create();
            var source = new RankedSkillAllocationAuthorityV2(catalog);
            source.Seed(RankedSkillAllocationSnapshotV2.Empty(
                "profile.real-skills",
                "striker",
                catalog));
            var command = new AllocateSkillRankCommandV2(
                "operation.real-skills",
                "profile.real-skills",
                "generic.movement_speed",
                0L,
                2);
            Assert.That(source.Allocate(command).Accepted, Is.True);

            var target = new RankedSkillAllocationAuthorityV2(catalog);
            target.Seed(RankedSkillAllocationSnapshotV2.Empty(
                "profile.real-skills",
                "striker",
                catalog));
            PlayerAccountSnapshotV1 decoded = FileRoundTrip(
                SkillAdapter(source, "profile.real-skills")
                    .ExportComponent());
            PlayerAccountRestoreResultV1 restored = Restore(
                decoded,
                SkillAdapter(target, "profile.real-skills"));

            Assert.That(restored.Succeeded, Is.True, restored.RejectionCode);
            Assert.That(target.Get("profile.real-skills").Fingerprint,
                Is.EqualTo(source.Get("profile.real-skills").Fingerprint));
            string before = target.Get("profile.real-skills").Fingerprint;
            SkillAllocationResultV2 replay = target.Allocate(command);
            Assert.That(replay.Accepted, Is.False);
            Assert.That(replay.Rejection,
                Is.EqualTo(SkillAllocationRejectionV2.StaleVersion));
            Assert.That(target.Get("profile.real-skills").Fingerprint,
                Is.EqualTo(before));
        }

        [Test]
        public void LoadoutRealAuthorityRoundTripPreservesExactSlotBindingAndReplay()
        {
            PlayerRouteProfilePayloadV1 route = Route("real-loadout");
            var source = new ProductionPlayerLoadoutRuntimeV1(route);
            InventoryLoadoutAuthoritySnapshotV1 before =
                source.LoadoutAuthority.ExportSnapshot();
            List<InventoryLoadoutSlotBindingV1> bindings = CopyBindings(before);
            bindings[3] = new InventoryLoadoutSlotBindingV1(
                InventoryLoadoutSlotIdsV1.WeaponFour,
                source.RicochetEquipmentInstanceStableId);
            var originalCommand = new InventoryLoadoutAuthorityCommandV1(
                before.Sequence,
                source.Holdings.Sequence,
                bindings);
            Assert.That(source.LoadoutAuthority.Apply(originalCommand).Status,
                Is.EqualTo(InventoryLoadoutAuthorityMutationStatusV1.Applied));

            var target = new ProductionPlayerLoadoutRuntimeV1(route);
            PlayerAccountSnapshotV1 decoded = FileRoundTrip(
                LoadoutAdapter(source).ExportComponent());
            PlayerAccountRestoreResultV1 restored = Restore(
                decoded,
                LoadoutAdapter(target));

            Assert.That(restored.Succeeded, Is.True, restored.RejectionCode);
            InventoryLoadoutAuthoritySnapshotV1 restoredSnapshot =
                target.LoadoutAuthority.ExportSnapshot();
            Assert.That(restoredSnapshot.Fingerprint,
                Is.EqualTo(source.LoadoutAuthority.ExportSnapshot().Fingerprint));
            Assert.That(restoredSnapshot.GetBinding(
                InventoryLoadoutSlotIdsV1.WeaponFour)
                .EquipmentInstanceStableId,
                Is.EqualTo(target.RicochetEquipmentInstanceStableId));
            InventoryLoadoutAuthorityResultV1 replay =
                target.LoadoutAuthority.Apply(originalCommand);
            Assert.That(replay.Status,
                Is.EqualTo(InventoryLoadoutAuthorityMutationStatusV1
                    .ExactRepeatNoChange));
            Assert.That(target.LoadoutAuthority.ExportSnapshot().Fingerprint,
                Is.EqualTo(restoredSnapshot.Fingerprint));
        }

        [Test]
        public void SemanticValidatorRejectsLoadoutInstanceAbsentFromHoldings()
        {
            StableId authorityId = Id("authority.holdings.semantic-test");
            var holdings = new PlayerHoldingsService(
                authorityId,
                1000L,
                new AcceptingEquipmentValidator());
            PlayerHoldingsSnapshotV1 holdingsSnapshot = holdings.ExportSnapshot();
            var bindings = new List<InventoryLoadoutSlotBindingV1>();
            for (int index = 0; index < InventoryLoadoutSlotsV1.All.Count; index++)
            {
                InventoryLoadoutSlotDescriptorV1 slot =
                    InventoryLoadoutSlotsV1.All[index];
                bindings.Add(new InventoryLoadoutSlotBindingV1(
                    slot.SlotStableId,
                    index == 0
                        ? Id("equipment-instance.absent")
                        : null));
            }
            InventoryLoadoutAuthoritySnapshotV1 loadout =
                InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(0L, bindings);
            CharacterInstanceSnapshotV1 character = Character(
                new SaveComponentSnapshotV1[]
                {
                    Component(
                        KnownSaveComponentDefinitionsV1.PlayerHoldings(),
                        KnownSaveComponentCodecsV1.PlayerHoldings.Encode(
                            holdingsSnapshot)),
                    Component(
                        KnownSaveComponentDefinitionsV1.ExactInstanceLoadout(),
                        KnownSaveComponentCodecsV1.ExactInstanceLoadout.Encode(
                            loadout)),
                });

            SaveComponentValidationResultV1 result =
                PlayerAccountComponentSemanticsV1.ValidateCharacter(character);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RejectionCode,
                Does.StartWith("loadout-equipment-instance-absent-from-holdings"));
        }

        [Test]
        public void ExplicitCodecGoldenPayloadsAreStableAndDoNotUseClrTypes()
        {
            PlayerExperienceCurveV1 curve = ConstantCurve();
            PlayerExperienceAuthorityV1 experience = ExperienceAuthority(curve);
            experience.Grant(new PlayerExperienceGrantRequestV1(
                Id("xp-source.golden"),
                100L));
            var money = new MoneyWalletService();
            money.Grant(
                Id("transaction.money.golden"),
                Id("operation.money.golden"),
                5L);

            string xpPayload = KnownSaveComponentCodecsV1.PlayerExperience
                .Encode(experience.ExportSnapshot());
            string moneyPayload = KnownSaveComponentCodecsV1.MoneyWallet
                .Encode(money.CurrentSnapshot);

            Assert.That(xpPayload, Does.StartWith("O7:"));
            Assert.That(moneyPayload, Does.StartWith("O4:"));
            Assert.That(xpPayload, Does.Not.Contain("PlayerExperienceSnapshotV1"));
            Assert.That(moneyPayload, Does.Not.Contain("MoneyWalletSnapshot"));
            Assert.That(Sha256(xpPayload), Is.EqualTo(Sha256(
                KnownSaveComponentCodecsV1.PlayerExperience.Encode(
                    experience.ExportSnapshot()))));
            Assert.That(Sha256(moneyPayload), Is.EqualTo(Sha256(
                KnownSaveComponentCodecsV1.MoneyWallet.Encode(
                    money.CurrentSnapshot))));
        }

        private static PlayerAccountSnapshotV1 FileRoundTrip(
            SaveComponentSnapshotV1 component)
        {
            PlayerAccountSnapshotV1 source = Account(component);
            string file = PlayerAccountFileCodecV1.Encode(source);
            PlayerAccountSnapshotV1 decoded;
            string rejection;
            Assert.That(PlayerAccountFileCodecV1.TryDecode(
                file,
                out decoded,
                out rejection), Is.True, rejection);
            Assert.That(decoded.Fingerprint, Is.EqualTo(source.Fingerprint));
            return decoded;
        }

        private static PlayerAccountRestoreResultV1 Restore(
            PlayerAccountSnapshotV1 account,
            ISaveComponentAdapterV1 adapter)
        {
            return new PlayerAccountRestoreCoordinatorV1(
                validateAggregate: snapshot =>
                    PlayerAccountComponentSemanticsV1.Validate(snapshot))
                .Restore(
                    account,
                    new[]
                    {
                        new CharacterSaveRestoreBindingV1(
                            0,
                            Id("character.real-save-adapters"),
                            new[] { adapter }),
                    });
        }

        private static PlayerAccountSnapshotV1 Account(
            SaveComponentSnapshotV1 component)
        {
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            slots[0] = Character(new[] { component });
            return new PlayerAccountSnapshotV1(
                Id("account.real-save-adapters"),
                3L,
                slots,
                null);
        }

        private static CharacterInstanceSnapshotV1 Character(
            IEnumerable<SaveComponentSnapshotV1> components)
        {
            return new CharacterInstanceSnapshotV1(
                Id("character.real-save-adapters"),
                Id("class.striker"),
                0,
                "Real Save Adapter",
                2L,
                components);
        }

        private static SaveComponentSnapshotV1 Component(
            SaveComponentDefinitionV1 definition,
            string payload)
        {
            return new SaveComponentSnapshotV1(
                definition.ComponentStableId,
                definition.SchemaVersion,
                definition.ContentVersion,
                payload);
        }

        private static ISaveComponentAdapterV1 ExperienceAdapter(
            PlayerExperienceAuthorityV1 authority,
            PlayerExperienceCurveV1 curve)
        {
            return KnownSaveComponentAdaptersV1.PlayerExperience(
                authority.ExportSnapshot,
                snapshot =>
                {
                    PlayerExperienceImportResultV1 result =
                        ExperienceAuthority(curve).TryImport(snapshot);
                    return result.Status == PlayerExperienceImportStatusV1.Imported
                        ? SaveComponentValidationResultV1.Accept()
                        : SaveComponentValidationResultV1.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    PlayerExperienceImportResultV1 result =
                        authority.TryImport(snapshot);
                    return result.Status == PlayerExperienceImportStatusV1.Imported
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentAdapterV1 MoneyAdapter(
            MoneyWalletService authority)
        {
            return KnownSaveComponentAdaptersV1.MoneyWallet(
                () => authority.CurrentSnapshot,
                snapshot =>
                {
                    MoneyWalletImportResult result =
                        new MoneyWalletService().ImportSnapshot(snapshot);
                    return result.Status == MoneyWalletImportStatus.Imported
                        ? SaveComponentValidationResultV1.Accept()
                        : SaveComponentValidationResultV1.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    MoneyWalletImportResult result =
                        authority.ImportSnapshot(snapshot);
                    return result.Status == MoneyWalletImportStatus.Imported
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentAdapterV1 ScrapAdapter(
            ScrapWalletServiceV1 authority,
            StableId authorityId,
            StableId currencyId)
        {
            return KnownSaveComponentAdaptersV1.ScrapWallet(
                authority.ExportSnapshot,
                snapshot =>
                {
                    ScrapSnapshotImportResultV1 result =
                        new ScrapWalletServiceV1(authorityId, currencyId)
                            .ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentValidationResultV1.Accept()
                        : SaveComponentValidationResultV1.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    ScrapSnapshotImportResultV1 result =
                        authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentAdapterV1 HoldingsAdapter(
            PlayerHoldingsService authority,
            StableId authorityId)
        {
            return KnownSaveComponentAdaptersV1.PlayerHoldings(
                authority.ExportSnapshot,
                snapshot =>
                {
                    PlayerHoldingsImportResultV1 result =
                        new PlayerHoldingsService(
                            authorityId,
                            1000L,
                            new AcceptingEquipmentValidator())
                            .ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentValidationResultV1.Accept()
                        : SaveComponentValidationResultV1.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    PlayerHoldingsImportResultV1 result =
                        authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentAdapterV1 SkillAdapter(
            RankedSkillAllocationAuthorityV2 authority,
            string targetProfile)
        {
            return KnownSaveComponentAdaptersV1.RankedSkillAllocation(
                () => authority.Get(targetProfile),
                snapshot => KnownSaveComponentCodecsV1.RankedSkillAllocation
                    .Validate(snapshot),
                snapshot =>
                {
                    authority.Seed(snapshot);
                    return authority.Get(targetProfile).Fingerprint
                            == snapshot.Fingerprint
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            "ranked-skill-seed-mismatch");
                });
        }

        private static ISaveComponentAdapterV1 LoadoutAdapter(
            ProductionPlayerLoadoutRuntimeV1 runtime)
        {
            return KnownSaveComponentAdaptersV1.ExactInstanceLoadout(
                runtime.LoadoutAuthority.ExportSnapshot,
                snapshot => KnownSaveComponentCodecsV1.ExactInstanceLoadout
                    .Validate(snapshot),
                snapshot =>
                {
                    InventoryLoadoutAuthoritySnapshotV1 current =
                        runtime.LoadoutAuthority.ExportSnapshot();
                    if (current.Fingerprint == snapshot.Fingerprint)
                    {
                        return SaveComponentApplyResultV1.Applied();
                    }
                    if (snapshot.Sequence != current.Sequence + 1L)
                    {
                        return SaveComponentApplyResultV1.Rejected(
                            "loadout-import-sequence-not-replayable");
                    }
                    var command = new InventoryLoadoutAuthorityCommandV1(
                        current.Sequence,
                        runtime.Holdings.Sequence,
                        snapshot.Bindings);
                    InventoryLoadoutAuthorityResultV1 result =
                        runtime.LoadoutAuthority.Apply(command);
                    return result.Status
                                == InventoryLoadoutAuthorityMutationStatusV1.Applied
                            && result.Snapshot.Fingerprint == snapshot.Fingerprint
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }

        private static PlayerExperienceCurveV1 ConstantCurve()
        {
            return new PlayerExperienceCurveV1(
                100L,
                100L,
                50,
                new SoftActivationCurveParameters(0.1, 10L, 10L));
        }

        private static PlayerExperienceAuthorityV1 ExperienceAuthority(
            PlayerExperienceCurveV1 curve)
        {
            return new PlayerExperienceAuthorityV1(
                curve,
                ProgressionContext.Create(
                    1,
                    1,
                    Id("difficulty.normal"),
                    0,
                    new[] { Id("progression-tag.campaign") }));
        }

        private static ScrapTransactionCommandV1 ScrapGrant(
            StableId authorityId,
            StableId currencyId,
            string suffix,
            long amount,
            long? expectedSequence)
        {
            return new ScrapTransactionCommandV1(
                Id("transaction.scrap." + suffix),
                Id("operation.scrap." + suffix),
                authorityId,
                currencyId,
                ScrapMutationKindV1.Grant,
                amount,
                ScrapIdentityV1.RewardGrantReason,
                new ScrapProvenanceV1(
                    ScrapIdentityV1.RewardSourceKind,
                    Id("source-operation.scrap." + suffix),
                    Id("subject.scrap." + suffix)),
                expectedSequence);
        }

        private static ScrapTransactionCommandV1 ScrapSpend(
            StableId authorityId,
            StableId currencyId,
            string suffix,
            long amount,
            long? expectedSequence)
        {
            return new ScrapTransactionCommandV1(
                Id("transaction.scrap." + suffix),
                Id("operation.scrap." + suffix),
                authorityId,
                currencyId,
                ScrapMutationKindV1.Spend,
                amount,
                ScrapIdentityV1.CraftingSpendReason,
                new ScrapProvenanceV1(
                    ScrapIdentityV1.CraftingSourceKind,
                    Id("source-operation.scrap." + suffix),
                    Id("subject.scrap." + suffix)),
                expectedSequence);
        }

        private static EquipmentInstance Equipment(
            string instanceId,
            StableId definitionId)
        {
            return EquipmentInstance.Create(
                Id(instanceId),
                definitionId,
                7,
                Id("equipment-quality.common"),
                Array.Empty<AugmentInstance>());
        }

        private static PlayerHoldingsCommandV1 AddEquipmentCommand(
            PlayerHoldingsService authority,
            EquipmentInstance equipment,
            string suffix,
            long expectedSequence)
        {
            return PlayerHoldingsCommandV1.AddEquipment(
                Id("transaction.holdings." + suffix),
                Id("operation.holdings." + suffix),
                authority.AuthorityStableId,
                equipment,
                HoldingProvenanceV1.Create(
                    Id("grant.holdings." + suffix),
                    Id("source.holdings." + suffix)),
                expectedSequence);
        }

        private static PlayerRouteProfilePayloadV1 Route(string suffix)
        {
            return PlayerRouteProfilePayloadV1.Create(
                Id("character." + suffix),
                Id("loadout-profile." + suffix),
                new[]
                {
                    Id("equipment-instance." + suffix + "-1"),
                    Id("equipment-instance." + suffix + "-2"),
                    Id("equipment-instance." + suffix + "-3"),
                    Id("equipment-instance." + suffix + "-4"),
                });
        }

        private static List<InventoryLoadoutSlotBindingV1> CopyBindings(
            InventoryLoadoutAuthoritySnapshotV1 snapshot)
        {
            return snapshot.Bindings.Select(binding =>
                new InventoryLoadoutSlotBindingV1(
                    binding.SlotStableId,
                    binding.EquipmentInstanceStableId)).ToList();
        }

        private static string Sha256(string value)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(
                    Encoding.UTF8.GetBytes(value)))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class AcceptingEquipmentValidator :
            IEquipmentInstanceValidator
        {
            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                return new EquipmentInstanceValidationResponse(
                    request != null && request.Instance != null,
                    "save-adapter-real-test-catalog",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    Array.Empty<EquipmentModelIssue>());
            }
        }
    }
}
