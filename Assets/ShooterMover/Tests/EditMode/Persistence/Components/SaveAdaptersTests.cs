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
    public sealed class RealStateSaveAdaptersTests
    {
        [Test]
        public void PlayerExperienceRealAuthorityRoundTripPreservesReplay()
        {
            PlayerExperienceCurve curve = ConstantCurve();
            PlayerExperience source = ExperienceAuthority(curve);
            var request = new PlayerExperienceGrantRequest(
                Id("xp-source.real-roundtrip"),
                250L);
            Assert.That(source.Grant(request).Status,
                Is.EqualTo(PlayerExperienceGrantStatus.Applied));

            PlayerExperience target = ExperienceAuthority(curve);
            ISaveComponentBridge sourceAdapter = ExperienceAdapter(source, curve);
            ISaveComponentBridge targetAdapter = ExperienceAdapter(target, curve);
            PlayerAccountSnapshot decoded = FileRoundTrip(sourceAdapter.ExportComponent());

            PlayerAccountRestoreResult restored = Restore(decoded, targetAdapter);

            Assert.That(restored.Succeeded, Is.True, restored.RejectionCode);
            Assert.That(target.ExportSnapshot().Fingerprint,
                Is.EqualTo(source.ExportSnapshot().Fingerprint));
            long sequence = target.CurrentSnapshot.Sequence;
            Assert.That(target.Grant(request).Status,
                Is.EqualTo(PlayerExperienceGrantStatus.DuplicateNoChange));
            Assert.That(target.CurrentSnapshot.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void MoneyWalletRealAuthorityRoundTripPreservesAcceptedAndRejectedReplay()
        {
            var source = new MoneyWalletActions();
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

            var target = new MoneyWalletActions();
            PlayerAccountSnapshot decoded = FileRoundTrip(
                MoneyAdapter(source).ExportComponent());
            PlayerAccountRestoreResult restored = Restore(
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
            var source = new ScrapWalletActions(authorityId, currencyId);
            ScrapTransactionCommand grant = ScrapGrant(
                authorityId,
                currencyId,
                "real-grant",
                30L,
                0L);
            ScrapTransactionCommand rejected = ScrapSpend(
                authorityId,
                currencyId,
                "real-rejected",
                99L,
                1L);
            Assert.That(source.Apply(grant).Status,
                Is.EqualTo(EconomyTransactionStatus.Applied));
            source.Apply(rejected);

            var target = new ScrapWalletActions(authorityId, currencyId);
            PlayerAccountSnapshot decoded = FileRoundTrip(
                ScrapAdapter(source, authorityId, currencyId).ExportComponent());
            PlayerAccountRestoreResult restored = Restore(
                decoded,
                ScrapAdapter(target, authorityId, currencyId));

            Assert.That(restored.Succeeded, Is.True, restored.RejectionCode);
            Assert.That(target.ExportSnapshot().Fingerprint,
                Is.EqualTo(source.ExportSnapshot().Fingerprint));
            long sequence = target.Sequence;
            Assert.That(target.Apply(grant).Status,
                Is.EqualTo(EconomyTransactionStatus.DuplicateNoChange));
            Assert.That(target.Apply(rejected).Status,
                Is.EqualTo(EconomyTransactionStatus.DuplicateNoChange));
            Assert.That(target.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void HoldingsRealAuthorityRoundTripPreservesDistinctInstancesAndUnopenedBox()
        {
            StableId authorityId = Id("authority.holdings.real-roundtrip");
            var source = new PlayerHoldingsActions(
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
            PlayerHoldingsCommand firstCommand = AddEquipmentCommand(
                source,
                first,
                "first",
                0L);
            PlayerHoldingsCommand secondCommand = AddEquipmentCommand(
                source,
                second,
                "second",
                1L);
            PlayerHoldingsCommand boxCommand =
                PlayerHoldingsCommand.AddStrongbox(
                    Id("transaction.holdings.box"),
                    Id("operation.holdings.box"),
                    authorityId,
                    Id("strongbox.tier.test"),
                    boxId,
                    HoldingProvenance.Create(
                        Id("grant.holdings.box"),
                        Id("source.holdings.box")),
                    2L);
            Assert.That(source.Apply(firstCommand).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(source.Apply(secondCommand).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(source.Apply(boxCommand).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));

            var target = new PlayerHoldingsActions(
                authorityId,
                1000L,
                new AcceptingEquipmentValidator());
            PlayerAccountSnapshot decoded = FileRoundTrip(
                HoldingsAdapter(source, authorityId).ExportComponent());
            PlayerAccountRestoreResult restored = Restore(
                decoded,
                HoldingsAdapter(target, authorityId));

            Assert.That(restored.Succeeded, Is.True, restored.RejectionCode);
            PlayerHoldingsSnapshot snapshot = target.ExportSnapshot();
            Assert.That(snapshot.Fingerprint,
                Is.EqualTo(source.ExportSnapshot().Fingerprint));
            Assert.That(snapshot.UniqueHoldings.Count, Is.EqualTo(3));
            Assert.That(snapshot.UniqueHoldings
                .Where(item => item.RewardKind
                    == RewardGrantKind.EquipmentReference)
                .Select(item => item.DefinitionStableId)
                .Distinct().Single(), Is.EqualTo(sharedDefinition));
            Assert.That(snapshot.UniqueHoldings
                .Where(item => item.RewardKind
                    == RewardGrantKind.EquipmentReference)
                .Select(item => item.InstanceStableId)
                .Distinct().Count(), Is.EqualTo(2));
            Assert.That(snapshot.UniqueHoldings.Any(item =>
                item.RewardKind == RewardGrantKind.Strongbox
                && item.InstanceStableId == boxId), Is.True);
            long sequence = target.Sequence;
            Assert.That(target.Apply(firstCommand).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.ExactDuplicateNoChange));
            Assert.That(target.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void RankedSkillRealAuthorityRoundTripPreservesAllocationAndBlocksReplayMutation()
        {
            RankedSkillCatalog catalog = RankedSkillSampleCatalog.Create();
            var source = new RankedSkillAllocationState(catalog);
            source.Seed(RankedSkillAllocationSnapshot.Empty(
                "profile.real-skills",
                "striker",
                catalog));
            var command = new AllocateSkillRankCommand(
                "operation.real-skills",
                "profile.real-skills",
                "generic.movement_speed",
                0L,
                2);
            Assert.That(source.Allocate(command).Accepted, Is.True);

            var target = new RankedSkillAllocationState(catalog);
            target.Seed(RankedSkillAllocationSnapshot.Empty(
                "profile.real-skills",
                "striker",
                catalog));
            PlayerAccountSnapshot decoded = FileRoundTrip(
                SkillAdapter(source, "profile.real-skills")
                    .ExportComponent());
            PlayerAccountRestoreResult restored = Restore(
                decoded,
                SkillAdapter(target, "profile.real-skills"));

            Assert.That(restored.Succeeded, Is.True, restored.RejectionCode);
            Assert.That(target.Get("profile.real-skills").Fingerprint,
                Is.EqualTo(source.Get("profile.real-skills").Fingerprint));
            string before = target.Get("profile.real-skills").Fingerprint;
            SkillAllocationResult replay = target.Allocate(command);
            Assert.That(replay.Accepted, Is.False);
            Assert.That(replay.Rejection,
                Is.EqualTo(SkillAllocationRejection.StaleVersion));
            Assert.That(target.Get("profile.real-skills").Fingerprint,
                Is.EqualTo(before));
        }

        [Test]
        public void LoadoutRealAuthorityRoundTripPreservesExactSlotBindingAndReplay()
        {
            PlayerRouteProfilePayload route = Route("real-loadout");
            var source = new PlayerLoadoutLive(route);
            InventoryLoadoutStateSnapshot before =
                source.LoadoutAuthority.ExportSnapshot();
            List<InventoryLoadoutSlotBinding> bindings = CopyBindings(before);
            bindings[3] = new InventoryLoadoutSlotBinding(
                InventoryLoadoutSlotIds.WeaponFour,
                StableId.Parse("equipment-instance.test-ricochet"));
            var originalCommand = new InventoryLoadoutStateCommand(
                before.Sequence,
                source.Holdings.Sequence,
                bindings);
            Assert.That(source.LoadoutAuthority.Apply(originalCommand).Status,
                Is.EqualTo(InventoryLoadoutStateMutationStatus.Applied));

            var target = new PlayerLoadoutLive(route);
            PlayerAccountSnapshot decoded = FileRoundTrip(
                LoadoutAdapter(source).ExportComponent());
            PlayerAccountRestoreResult restored = Restore(
                decoded,
                LoadoutAdapter(target));

            Assert.That(restored.Succeeded, Is.True, restored.RejectionCode);
            InventoryLoadoutStateSnapshot restoredSnapshot =
                target.LoadoutAuthority.ExportSnapshot();
            Assert.That(restoredSnapshot.Fingerprint,
                Is.EqualTo(source.LoadoutAuthority.ExportSnapshot().Fingerprint));
            Assert.That(restoredSnapshot.GetBinding(
                InventoryLoadoutSlotIds.WeaponFour)
                .EquipmentInstanceStableId,
                Is.EqualTo(StableId.Parse("equipment-instance.test-ricochet")));
            InventoryLoadoutStateResult replay =
                target.LoadoutAuthority.Apply(originalCommand);
            Assert.That(replay.Status,
                Is.EqualTo(InventoryLoadoutStateMutationStatus
                    .ExactRepeatNoChange));
            Assert.That(target.LoadoutAuthority.ExportSnapshot().Fingerprint,
                Is.EqualTo(restoredSnapshot.Fingerprint));
        }

        [Test]
        public void SemanticValidatorRejectsLoadoutInstanceAbsentFromHoldings()
        {
            StableId authorityId = Id("authority.holdings.semantic-test");
            var holdings = new PlayerHoldingsActions(
                authorityId,
                1000L,
                new AcceptingEquipmentValidator());
            PlayerHoldingsSnapshot holdingsSnapshot = holdings.ExportSnapshot();
            var bindings = new List<InventoryLoadoutSlotBinding>();
            for (int index = 0; index < InventoryLoadoutSlots.All.Count; index++)
            {
                InventoryLoadoutSlotDescriptor slot =
                    InventoryLoadoutSlots.All[index];
                bindings.Add(new InventoryLoadoutSlotBinding(
                    slot.SlotStableId,
                    index == 0
                        ? Id("equipment-instance.absent")
                        : null));
            }
            InventoryLoadoutStateSnapshot loadout =
                InventoryLoadoutStateSnapshot.CreateCanonical(0L, bindings);
            CharacterInstanceSnapshot character = Character(
                new SaveComponentSnapshot[]
                {
                    Component(
                        KnownSaveComponentDefinitions.PlayerHoldings(),
                        KnownSaveComponentCodecs.PlayerHoldings.Encode(
                            holdingsSnapshot)),
                    Component(
                        KnownSaveComponentDefinitions.ExactInstanceLoadout(),
                        KnownSaveComponentCodecs.ExactInstanceLoadout.Encode(
                            loadout)),
                });

            SaveComponentValidationResult result =
                PlayerAccountComponentSemantics.ValidateCharacter(character);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RejectionCode,
                Does.StartWith("loadout-equipment-instance-absent-from-holdings"));
        }

        [Test]
        public void ExplicitCodecGoldenPayloadsAreStableAndDoNotUseClrTypes()
        {
            PlayerExperienceCurve curve = ConstantCurve();
            PlayerExperience experience = ExperienceAuthority(curve);
            experience.Grant(new PlayerExperienceGrantRequest(
                Id("xp-source.golden"),
                100L));
            var money = new MoneyWalletActions();
            money.Grant(
                Id("transaction.money.golden"),
                Id("operation.money.golden"),
                5L);

            string xpPayload = KnownSaveComponentCodecs.PlayerExperience
                .Encode(experience.ExportSnapshot());
            string moneyPayload = KnownSaveComponentCodecs.MoneyWallet
                .Encode(money.CurrentSnapshot);

            Assert.That(xpPayload, Does.StartWith("O7:"));
            Assert.That(moneyPayload, Does.StartWith("O4:"));
            Assert.That(xpPayload, Does.Not.Contain("PlayerExperienceSnapshot"));
            Assert.That(moneyPayload, Does.Not.Contain("MoneyWalletSnapshot"));
            Assert.That(Sha256(xpPayload), Is.EqualTo(Sha256(
                KnownSaveComponentCodecs.PlayerExperience.Encode(
                    experience.ExportSnapshot()))));
            Assert.That(Sha256(moneyPayload), Is.EqualTo(Sha256(
                KnownSaveComponentCodecs.MoneyWallet.Encode(
                    money.CurrentSnapshot))));
        }

        private static PlayerAccountSnapshot FileRoundTrip(
            SaveComponentSnapshot component)
        {
            PlayerAccountSnapshot source = Account(component);
            string file = PlayerAccountFileCodec.Encode(source);
            PlayerAccountSnapshot decoded;
            string rejection;
            Assert.That(PlayerAccountFileCodec.TryDecode(
                file,
                out decoded,
                out rejection), Is.True, rejection);
            Assert.That(decoded.Fingerprint, Is.EqualTo(source.Fingerprint));
            return decoded;
        }

        private static PlayerAccountRestoreResult Restore(
            PlayerAccountSnapshot account,
            ISaveComponentBridge adapter)
        {
            return new PlayerAccountRestoreFlow(
                validateAggregate: snapshot =>
                    PlayerAccountComponentSemantics.Validate(snapshot))
                .Restore(
                    account,
                    new[]
                    {
                        new CharacterSaveRestoreBinding(
                            0,
                            Id("character.real-save-adapters"),
                            new[] { adapter }),
                    });
        }

        private static PlayerAccountSnapshot Account(
            SaveComponentSnapshot component)
        {
            var slots = new CharacterInstanceSnapshot[
                PlayerAccountSnapshot.CharacterSlotCount];
            slots[0] = Character(new[] { component });
            return new PlayerAccountSnapshot(
                Id("account.real-save-adapters"),
                3L,
                slots,
                null);
        }

        private static CharacterInstanceSnapshot Character(
            IEnumerable<SaveComponentSnapshot> components)
        {
            return new CharacterInstanceSnapshot(
                Id("character.real-save-adapters"),
                Id("class.striker"),
                0,
                "Real Save Adapter",
                2L,
                components);
        }

        private static SaveComponentSnapshot Component(
            SaveComponentDefinition definition,
            string payload)
        {
            return new SaveComponentSnapshot(
                definition.ComponentStableId,
                definition.SchemaVersion,
                definition.ContentVersion,
                payload);
        }

        private static ISaveComponentBridge ExperienceAdapter(
            PlayerExperience authority,
            PlayerExperienceCurve curve)
        {
            return KnownSaveComponentAdapters.PlayerExperience(
                authority.ExportSnapshot,
                snapshot =>
                {
                    PlayerExperienceImportResult result =
                        ExperienceAuthority(curve).TryImport(snapshot);
                    return result.Status == PlayerExperienceImportStatus.Imported
                        ? SaveComponentValidationResult.Accept()
                        : SaveComponentValidationResult.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    PlayerExperienceImportResult result =
                        authority.TryImport(snapshot);
                    return result.Status == PlayerExperienceImportStatus.Imported
                        ? SaveComponentApplyResult.Applied()
                        : SaveComponentApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentBridge MoneyAdapter(
            MoneyWalletActions authority)
        {
            return KnownSaveComponentAdapters.MoneyWallet(
                () => authority.CurrentSnapshot,
                snapshot =>
                {
                    MoneyWalletImportResult result =
                        new MoneyWalletActions().ImportSnapshot(snapshot);
                    return result.Status == MoneyWalletImportStatus.Imported
                        ? SaveComponentValidationResult.Accept()
                        : SaveComponentValidationResult.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    MoneyWalletImportResult result =
                        authority.ImportSnapshot(snapshot);
                    return result.Status == MoneyWalletImportStatus.Imported
                        ? SaveComponentApplyResult.Applied()
                        : SaveComponentApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentBridge ScrapAdapter(
            ScrapWalletActions authority,
            StableId authorityId,
            StableId currencyId)
        {
            return KnownSaveComponentAdapters.ScrapWallet(
                authority.ExportSnapshot,
                snapshot =>
                {
                    ScrapSnapshotImportResult result =
                        new ScrapWalletActions(authorityId, currencyId)
                            .ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentValidationResult.Accept()
                        : SaveComponentValidationResult.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    ScrapSnapshotImportResult result =
                        authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResult.Applied()
                        : SaveComponentApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentBridge HoldingsAdapter(
            PlayerHoldingsActions authority,
            StableId authorityId)
        {
            return KnownSaveComponentAdapters.PlayerHoldings(
                authority.ExportSnapshot,
                snapshot =>
                {
                    PlayerHoldingsImportResult result =
                        new PlayerHoldingsActions(
                            authorityId,
                            1000L,
                            new AcceptingEquipmentValidator())
                            .ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentValidationResult.Accept()
                        : SaveComponentValidationResult.Reject(
                            result.RejectionCode);
                },
                snapshot =>
                {
                    PlayerHoldingsImportResult result =
                        authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResult.Applied()
                        : SaveComponentApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        private static ISaveComponentBridge SkillAdapter(
            RankedSkillAllocationState authority,
            string targetProfile)
        {
            return KnownSaveComponentAdapters.RankedSkillAllocation(
                () => authority.Get(targetProfile),
                snapshot => KnownSaveComponentCodecs.RankedSkillAllocation
                    .Validate(snapshot),
                snapshot =>
                {
                    authority.Seed(snapshot);
                    return authority.Get(targetProfile).Fingerprint
                            == snapshot.Fingerprint
                        ? SaveComponentApplyResult.Applied()
                        : SaveComponentApplyResult.Rejected(
                            "ranked-skill-seed-mismatch");
                });
        }

        private static ISaveComponentBridge LoadoutAdapter(
            PlayerLoadoutLive runtime)
        {
            return KnownSaveComponentAdapters.ExactInstanceLoadout(
                runtime.LoadoutAuthority.ExportSnapshot,
                snapshot => KnownSaveComponentCodecs.ExactInstanceLoadout
                    .Validate(snapshot),
                snapshot =>
                {
                    InventoryLoadoutStateSnapshot current =
                        runtime.LoadoutAuthority.ExportSnapshot();
                    if (current.Fingerprint == snapshot.Fingerprint)
                    {
                        return SaveComponentApplyResult.Applied();
                    }
                    if (snapshot.Sequence != current.Sequence + 1L)
                    {
                        return SaveComponentApplyResult.Rejected(
                            "loadout-import-sequence-not-replayable");
                    }
                    var command = new InventoryLoadoutStateCommand(
                        current.Sequence,
                        runtime.Holdings.Sequence,
                        snapshot.Bindings);
                    InventoryLoadoutStateResult result =
                        runtime.LoadoutAuthority.Apply(command);
                    return result.Status
                                == InventoryLoadoutStateMutationStatus.Applied
                            && result.Snapshot.Fingerprint == snapshot.Fingerprint
                        ? SaveComponentApplyResult.Applied()
                        : SaveComponentApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        private static PlayerExperienceCurve ConstantCurve()
        {
            return new PlayerExperienceCurve(
                100L,
                100L,
                50,
                new SoftActivationCurveParameters(0.1, 10L, 10L));
        }

        private static PlayerExperience ExperienceAuthority(
            PlayerExperienceCurve curve)
        {
            return new PlayerExperience(
                curve,
                ProgressionContext.Create(
                    1,
                    1,
                    Id("difficulty.normal"),
                    0,
                    new[] { Id("progression-tag.campaign") }));
        }

        private static ScrapTransactionCommand ScrapGrant(
            StableId authorityId,
            StableId currencyId,
            string suffix,
            long amount,
            long? expectedSequence)
        {
            return new ScrapTransactionCommand(
                Id("transaction.scrap." + suffix),
                Id("operation.scrap." + suffix),
                authorityId,
                currencyId,
                ScrapMutationKind.Grant,
                amount,
                ScrapIdentity.RewardGrantReason,
                new ScrapProvenance(
                    ScrapIdentity.RewardSourceKind,
                    Id("source-operation.scrap." + suffix),
                    Id("subject.scrap." + suffix)),
                expectedSequence);
        }

        private static ScrapTransactionCommand ScrapSpend(
            StableId authorityId,
            StableId currencyId,
            string suffix,
            long amount,
            long? expectedSequence)
        {
            return new ScrapTransactionCommand(
                Id("transaction.scrap." + suffix),
                Id("operation.scrap." + suffix),
                authorityId,
                currencyId,
                ScrapMutationKind.Spend,
                amount,
                ScrapIdentity.CraftingSpendReason,
                new ScrapProvenance(
                    ScrapIdentity.CraftingSourceKind,
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

        private static PlayerHoldingsCommand AddEquipmentCommand(
            PlayerHoldingsActions authority,
            EquipmentInstance equipment,
            string suffix,
            long expectedSequence)
        {
            return PlayerHoldingsCommand.AddEquipment(
                Id("transaction.holdings." + suffix),
                Id("operation.holdings." + suffix),
                authority.AuthorityStableId,
                equipment,
                HoldingProvenance.Create(
                    Id("grant.holdings." + suffix),
                    Id("source.holdings." + suffix)),
                expectedSequence);
        }

        private static PlayerRouteProfilePayload Route(string suffix)
        {
            return PlayerRouteProfilePayload.Create(
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

        private static List<InventoryLoadoutSlotBinding> CopyBindings(
            InventoryLoadoutStateSnapshot snapshot)
        {
            return snapshot.Bindings.Select(binding =>
                new InventoryLoadoutSlotBinding(
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
