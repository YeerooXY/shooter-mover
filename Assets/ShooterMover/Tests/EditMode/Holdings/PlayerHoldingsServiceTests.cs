using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Holdings;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Holdings
{
    public sealed class PlayerHoldingsServiceTests
    {
        private static readonly StableId AuthorityId =
            StableId.Parse("holdings.player-profile");

        [Test]
        public void UniqueEquipmentArmorAndStrongboxesAddAndRemove()
        {
            var validator = new RecordingEquipmentValidator();
            PlayerHoldingsService service = CreateService(validator);
            EquipmentInstance weapon = Equipment(
                "equipment-instance.weapon-001",
                "equipment-definition.blaster",
                "quality.common");
            EquipmentInstance armor = Equipment(
                "equipment-instance.armor-001",
                "equipment-definition.armor-shell",
                "quality.rare");

            PlayerHoldingsMutationResultV1 weaponAdd = service.Apply(
                PlayerHoldingsCommandV1.AddEquipment(
                    Id("transaction.weapon-add"),
                    Id("operation.reward-001"),
                    AuthorityId,
                    weapon,
                    Provenance("grant.weapon", "source.enemy"),
                    0L));
            PlayerHoldingsMutationResultV1 armorAdd = service.Apply(
                PlayerHoldingsCommandV1.AddEquipment(
                    Id("transaction.armor-add"),
                    Id("operation.reward-002"),
                    AuthorityId,
                    armor,
                    Provenance("grant.armor", "source.strongbox"),
                    1L));
            PlayerHoldingsMutationResultV1 boxAdd = service.Apply(
                PlayerHoldingsCommandV1.AddStrongbox(
                    Id("transaction.box-add"),
                    Id("operation.reward-003"),
                    AuthorityId,
                    Id("strongbox-definition.tier-01"),
                    Id("strongbox-instance.box-001"),
                    Provenance("grant.box", "source.boss"),
                    2L));

            Assert.That(weaponAdd.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(armorAdd.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(boxAdd.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(validator.CallCount, Is.EqualTo(2));
            Assert.That(service.Sequence, Is.EqualTo(3L));

            UniqueHoldingSnapshotV1 holding;
            Assert.That(service.TryGetUnique(weapon.InstanceId, out holding), Is.True);
            Assert.That(holding.EquipmentInstance, Is.EqualTo(weapon));
            Assert.That(holding.Provenance.GrantStableId, Is.EqualTo(Id("grant.weapon")));
            Assert.That(service.TryGetUnique(armor.InstanceId, out holding), Is.True);
            Assert.That(holding.EquipmentInstance, Is.EqualTo(armor));
            Assert.That(service.TryGetUnique(Id("strongbox-instance.box-001"), out holding), Is.True);
            Assert.That(holding.RewardKind, Is.EqualTo(RewardGrantKindV1.Strongbox));

            Assert.That(service.Apply(
                PlayerHoldingsCommandV1.RemoveEquipment(
                    Id("transaction.weapon-remove"),
                    Id("operation.shop-sale"),
                    AuthorityId,
                    weapon.DefinitionId,
                    weapon.InstanceId,
                    Provenance("grant.weapon", "source.shop"),
                    3L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(service.Apply(
                PlayerHoldingsCommandV1.RemoveStrongbox(
                    Id("transaction.box-remove"),
                    Id("operation.box-open"),
                    AuthorityId,
                    Id("strongbox-definition.tier-01"),
                    Id("strongbox-instance.box-001"),
                    Provenance("grant.box", "source.box-open"),
                    4L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(service.TryGetUnique(weapon.InstanceId, out holding), Is.False);
            Assert.That(service.TryGetUnique(Id("strongbox-instance.box-001"), out holding), Is.False);
            Assert.That(service.TryGetUnique(armor.InstanceId, out holding), Is.True);
        }

        [Test]
        public void PremiumAmmoAndArbitraryMiscStacksAddAndRemove()
        {
            PlayerHoldingsService service = CreateService();
            StableId ammoId = Id("premium-ammo.incendiary");
            StableId miscId = Id("misc.future-widget-2049");

            Assert.That(service.Apply(Stack(
                "transaction.ammo-add",
                EconomyTransactionOperationV1.AddStack,
                RewardGrantKindV1.PremiumAmmo,
                ammoId,
                25L,
                0L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(service.Apply(Stack(
                "transaction.misc-add",
                EconomyTransactionOperationV1.AddStack,
                RewardGrantKindV1.Miscellaneous,
                miscId,
                7L,
                1L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(service.Apply(Stack(
                "transaction.ammo-remove",
                EconomyTransactionOperationV1.RemoveStack,
                RewardGrantKindV1.PremiumAmmo,
                ammoId,
                4L,
                2L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(service.Apply(Stack(
                "transaction.misc-remove",
                EconomyTransactionOperationV1.RemoveStack,
                RewardGrantKindV1.Miscellaneous,
                miscId,
                7L,
                3L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));

            Assert.That(service.GetStackQuantity(
                RewardGrantKindV1.PremiumAmmo,
                ammoId), Is.EqualTo(21L));
            Assert.That(service.GetStackQuantity(
                RewardGrantKindV1.Miscellaneous,
                miscId), Is.Zero);
        }

        [Test]
        public void UniqueCollisionMissingAndEquipmentValidationRejectWithoutMutation()
        {
            var validator = new RecordingEquipmentValidator();
            PlayerHoldingsService service = CreateService(validator);
            EquipmentInstance equipment = Equipment(
                "equipment-instance.collision",
                "equipment-definition.blaster",
                "quality.common");

            Assert.That(service.Apply(PlayerHoldingsCommandV1.AddEquipment(
                Id("transaction.unique-add"),
                Id("operation.reward"),
                AuthorityId,
                equipment,
                Provenance("grant.unique", "source.enemy"))).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(service.Apply(PlayerHoldingsCommandV1.RemoveEquipment(
                Id("transaction.unique-remove"),
                Id("operation.remove"),
                AuthorityId,
                equipment.DefinitionId,
                equipment.InstanceId,
                Provenance("grant.unique", "source.remove"))).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));

            PlayerHoldingsMutationResultV1 collision = service.Apply(
                PlayerHoldingsCommandV1.AddStrongbox(
                    Id("transaction.collision"),
                    Id("operation.reward-2"),
                    AuthorityId,
                    Id("strongbox-definition.tier-02"),
                    equipment.InstanceId,
                    Provenance("grant.box", "source.enemy")));
            PlayerHoldingsMutationResultV1 missing = service.Apply(
                PlayerHoldingsCommandV1.RemoveStrongbox(
                    Id("transaction.missing"),
                    Id("operation.open"),
                    AuthorityId,
                    Id("strongbox-definition.tier-02"),
                    Id("strongbox-instance.missing"),
                    Provenance("grant.missing", "source.open")));

            validator.Accept = false;
            PlayerHoldingsMutationResultV1 invalidEquipment = service.Apply(
                PlayerHoldingsCommandV1.AddEquipment(
                    Id("transaction.invalid-equipment"),
                    Id("operation.reward-3"),
                    AuthorityId,
                    Equipment(
                        "equipment-instance.invalid",
                        "equipment-definition.unknown",
                        "quality.invalid"),
                    Provenance("grant.invalid", "source.enemy")));

            Assert.That(collision.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.UniqueInstanceCollision));
            Assert.That(missing.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.MissingItem));
            Assert.That(invalidEquipment.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.EquipmentValidationRejected));
            Assert.That(service.Sequence, Is.EqualTo(2L));
            Assert.That(service.ExportSnapshot().UniqueHoldings, Is.Empty);
        }

        [Test]
        public void StackUnderflowCapacityOverflowAndTypeMismatchReject()
        {
            PlayerHoldingsService bounded = CreateService(maximumStackQuantity: 10L);
            StableId item = Id("misc.bound-item");
            Assert.That(bounded.Apply(Stack(
                "transaction.bound-add",
                EconomyTransactionOperationV1.AddStack,
                RewardGrantKindV1.Miscellaneous,
                item,
                8L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));

            Assert.That(bounded.Apply(Stack(
                "transaction.underflow",
                EconomyTransactionOperationV1.RemoveStack,
                RewardGrantKindV1.Miscellaneous,
                item,
                9L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.InsufficientValue));
            Assert.That(bounded.Apply(Stack(
                "transaction.capacity",
                EconomyTransactionOperationV1.AddStack,
                RewardGrantKindV1.Miscellaneous,
                item,
                3L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.InsufficientCapacity));
            Assert.That(bounded.Apply(Stack(
                "transaction.type-mismatch",
                EconomyTransactionOperationV1.AddStack,
                RewardGrantKindV1.PremiumAmmo,
                item,
                1L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.TypeMismatch));
            Assert.That(bounded.GetStackQuantity(
                RewardGrantKindV1.Miscellaneous,
                item), Is.EqualTo(8L));
            Assert.That(bounded.Sequence, Is.EqualTo(1L));

            PlayerHoldingsService huge = CreateService(
                maximumStackQuantity: long.MaxValue);
            Assert.That(huge.Apply(Stack(
                "transaction.max",
                EconomyTransactionOperationV1.AddStack,
                RewardGrantKindV1.Miscellaneous,
                Id("misc.max"),
                long.MaxValue)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(huge.Apply(Stack(
                "transaction.overflow",
                EconomyTransactionOperationV1.AddStack,
                RewardGrantKindV1.Miscellaneous,
                Id("misc.max"),
                1L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.ArithmeticOverflow));
        }

        [Test]
        public void WrongRewardTypeAndWrongAuthorityRejectWithoutPartialMutation()
        {
            PlayerHoldingsService service = CreateService();
            EconomyTransactionCommandV1 raw = EconomyTransactionCommandV1.Create(
                Id("transaction.wrong-reward"),
                Id("operation.raw"),
                AuthorityId,
                EconomyTransactionOperationV1.AddStack,
                EconomyResourceKindV1.Item,
                Id("misc.raw"),
                null,
                5L,
                null);
            PlayerHoldingsCommandV1 wrongReward = PlayerHoldingsCommandV1.Create(
                raw,
                RewardGrantKindV1.Money,
                Provenance("grant.raw", "source.raw"));
            PlayerHoldingsCommandV1 wrongAuthority = PlayerHoldingsCommandV1.AddStack(
                Id("transaction.wrong-authority"),
                Id("operation.raw-2"),
                Id("holdings.someone-else"),
                RewardGrantKindV1.Miscellaneous,
                Id("misc.raw"),
                5L,
                Provenance("grant.raw-2", "source.raw"));

            Assert.That(service.Apply(wrongReward).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.WrongRewardType));
            Assert.That(service.Apply(wrongAuthority).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.WrongAuthority));
            Assert.That(service.Sequence, Is.Zero);
            Assert.That(service.ExportSnapshot().StackHoldings, Is.Empty);
        }

        [Test]
        public void DuplicateConflictAndExpectedSequenceAreExactOnce()
        {
            PlayerHoldingsService service = CreateService();
            PlayerHoldingsCommandV1 original = Stack(
                "transaction.exact-once",
                EconomyTransactionOperationV1.AddStack,
                RewardGrantKindV1.Miscellaneous,
                Id("misc.exact"),
                5L,
                0L);

            PlayerHoldingsMutationResultV1 first = service.Apply(original);
            PlayerHoldingsMutationResultV1 duplicate = service.Apply(original);
            PlayerHoldingsMutationResultV1 conflict = service.Apply(Stack(
                "transaction.exact-once",
                EconomyTransactionOperationV1.AddStack,
                RewardGrantKindV1.Miscellaneous,
                Id("misc.exact"),
                6L,
                0L));
            PlayerHoldingsCommandV1 stale = Stack(
                "transaction.sequence-stale",
                EconomyTransactionOperationV1.AddStack,
                RewardGrantKindV1.Miscellaneous,
                Id("misc.exact"),
                1L,
                0L);
            PlayerHoldingsMutationResultV1 sequenceConflict = service.Apply(stale);
            PlayerHoldingsMutationResultV1 sequenceDuplicate = service.Apply(stale);

            Assert.That(first.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange));
            Assert.That(duplicate.OriginalStatus, Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            Assert.That(conflict.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.ConflictingDuplicate));
            Assert.That(sequenceConflict.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.ExpectedSequenceConflict));
            Assert.That(sequenceDuplicate.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange));
            Assert.That(sequenceDuplicate.OriginalStatus, Is.EqualTo(PlayerHoldingsMutationStatusV1.ExpectedSequenceConflict));
            Assert.That(service.Sequence, Is.EqualTo(1L));
            Assert.That(service.GetStackQuantity(
                RewardGrantKindV1.Miscellaneous,
                Id("misc.exact")), Is.EqualTo(5L));
            Assert.That(service.ExportSnapshot().Transactions.Count, Is.EqualTo(2));
        }

        [Test]
        public void SnapshotRoundTripIsDeterministicAndPreservesReplayHistory()
        {
            PlayerHoldingsService source = CreateService();
            EquipmentInstance equipment = Equipment(
                "equipment-instance.snapshot",
                "equipment-definition.snapshot",
                "quality.snapshot");
            source.Apply(PlayerHoldingsCommandV1.AddEquipment(
                Id("transaction.snapshot-equipment"),
                Id("operation.snapshot"),
                AuthorityId,
                equipment,
                Provenance("grant.snapshot-equipment", "source.snapshot"),
                0L));
            source.Apply(Stack(
                "transaction.snapshot-stack",
                EconomyTransactionOperationV1.AddStack,
                RewardGrantKindV1.Miscellaneous,
                Id("misc.snapshot"),
                12L,
                1L));
            PlayerHoldingsCommandV1 rejected = Stack(
                "transaction.snapshot-rejected",
                EconomyTransactionOperationV1.RemoveStack,
                RewardGrantKindV1.Miscellaneous,
                Id("misc.snapshot"),
                13L,
                2L);
            source.Apply(rejected);

            PlayerHoldingsSnapshotV1 first = source.ExportSnapshot();
            PlayerHoldingsService restored = CreateService();
            PlayerHoldingsImportResultV1 import = restored.ImportSnapshot(first);
            PlayerHoldingsSnapshotV1 second = restored.ExportSnapshot();

            Assert.That(import.Status, Is.EqualTo(PlayerHoldingsImportStatusV1.Imported));
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(second.LedgerSnapshot.Fingerprint,
                Is.EqualTo(first.LedgerSnapshot.Fingerprint));
            Assert.That(restored.Sequence, Is.EqualTo(2L));
            Assert.That(restored.GetStackQuantity(
                RewardGrantKindV1.Miscellaneous,
                Id("misc.snapshot")), Is.EqualTo(12L));
            Assert.That(restored.Apply(rejected).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange));
            Assert.That(restored.Apply(PlayerHoldingsCommandV1.AddStrongbox(
                Id("transaction.snapshot-collision"),
                Id("operation.snapshot-2"),
                AuthorityId,
                Id("strongbox-definition.snapshot"),
                equipment.InstanceId,
                Provenance("grant.snapshot-box", "source.snapshot"))).Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.UniqueInstanceCollision));
        }

        [Test]
        public void CorruptSnapshotLeavesPreviousStateUnchanged()
        {
            PlayerHoldingsService source = CreateService();
            source.Apply(Stack(
                "transaction.source",
                EconomyTransactionOperationV1.AddStack,
                RewardGrantKindV1.Miscellaneous,
                Id("misc.source"),
                20L));
            PlayerHoldingsSnapshotV1 valid = source.ExportSnapshot();
            var corrupt = new PlayerHoldingsSnapshotV1(
                valid.SchemaVersion,
                valid.AuthorityStableId,
                valid.MaximumStackQuantity,
                valid.LedgerSnapshot,
                valid.UniqueHoldings,
                valid.StackHoldings,
                valid.Transactions,
                "sha256:0000000000000000000000000000000000000000000000000000000000000000");

            PlayerHoldingsService target = CreateService();
            target.Apply(Stack(
                "transaction.target",
                EconomyTransactionOperationV1.AddStack,
                RewardGrantKindV1.PremiumAmmo,
                Id("premium-ammo.target"),
                3L));
            PlayerHoldingsSnapshotV1 before = target.ExportSnapshot();
            PlayerHoldingsImportResultV1 result = target.ImportSnapshot(corrupt);
            PlayerHoldingsSnapshotV1 after = target.ExportSnapshot();

            Assert.That(result.Status, Is.EqualTo(PlayerHoldingsImportStatusV1.FingerprintMismatch));
            Assert.That(after.Fingerprint, Is.EqualTo(before.Fingerprint));
            Assert.That(target.Sequence, Is.EqualTo(1L));
            Assert.That(target.GetStackQuantity(
                RewardGrantKindV1.PremiumAmmo,
                Id("premium-ammo.target")), Is.EqualTo(3L));
        }

        private static PlayerHoldingsService CreateService(
            RecordingEquipmentValidator validator = null,
            long maximumStackQuantity = 1000L)
        {
            return new PlayerHoldingsService(
                AuthorityId,
                maximumStackQuantity,
                validator ?? new RecordingEquipmentValidator());
        }

        private static PlayerHoldingsCommandV1 Stack(
            string transactionId,
            EconomyTransactionOperationV1 operation,
            RewardGrantKindV1 rewardKind,
            StableId itemStableId,
            long quantity,
            long? expectedSequence = null)
        {
            return operation == EconomyTransactionOperationV1.AddStack
                ? PlayerHoldingsCommandV1.AddStack(
                    Id(transactionId),
                    Id("operation." + transactionId.Replace("transaction.", string.Empty)),
                    AuthorityId,
                    rewardKind,
                    itemStableId,
                    quantity,
                    Provenance(
                        "grant." + transactionId.Replace("transaction.", string.Empty),
                        "source.test"),
                    expectedSequence)
                : PlayerHoldingsCommandV1.RemoveStack(
                    Id(transactionId),
                    Id("operation." + transactionId.Replace("transaction.", string.Empty)),
                    AuthorityId,
                    rewardKind,
                    itemStableId,
                    quantity,
                    Provenance(
                        "grant." + transactionId.Replace("transaction.", string.Empty),
                        "source.test"),
                    expectedSequence);
        }

        private static EquipmentInstance Equipment(
            string instanceId,
            string definitionId,
            string qualityId)
        {
            return EquipmentInstance.Create(
                Id(instanceId),
                Id(definitionId),
                1,
                Id(qualityId),
                new AugmentInstance[0]);
        }

        private static HoldingProvenanceV1 Provenance(
            string grantId,
            string sourceId)
        {
            return HoldingProvenanceV1.Create(
                Id(grantId),
                Id(sourceId));
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class RecordingEquipmentValidator :
            IEquipmentInstanceValidator
        {
            public bool Accept { get; set; } = true;

            public int CallCount { get; private set; }

            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                CallCount++;
                return new EquipmentInstanceValidationResponse(
                    Accept,
                    "catalog-test",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    new List<EquipmentModelIssue>());
            }
        }
    }
}
