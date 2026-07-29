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
    public sealed class PlayerHoldingsActionsTests
    {
        private static readonly StableId AuthorityId =
            StableId.Parse("holdings.player-profile");

        [Test]
        public void UniqueEquipmentArmorAndStrongboxesAddAndRemove()
        {
            var validator = new RecordingEquipmentValidator();
            PlayerHoldingsActions service = CreateService(validator);
            EquipmentInstance gun = Equipment(
                "equipment-instance.gun-001",
                "equipment-definition.blaster",
                "quality.common");
            EquipmentInstance armor = Equipment(
                "equipment-instance.armor-001",
                "equipment-definition.armor-shell",
                "quality.rare");

            PlayerHoldingsMutationResult gunAdd = service.Apply(
                PlayerHoldingsCommand.AddEquipment(
                    Id("transaction.gun-add"),
                    Id("operation.reward-001"),
                    AuthorityId,
                    gun,
                    Provenance("grant.gun", "source.enemy"),
                    0L));
            PlayerHoldingsMutationResult armorAdd = service.Apply(
                PlayerHoldingsCommand.AddEquipment(
                    Id("transaction.armor-add"),
                    Id("operation.reward-002"),
                    AuthorityId,
                    armor,
                    Provenance("grant.armor", "source.strongbox"),
                    1L));
            PlayerHoldingsMutationResult boxAdd = service.Apply(
                PlayerHoldingsCommand.AddStrongbox(
                    Id("transaction.box-add"),
                    Id("operation.reward-003"),
                    AuthorityId,
                    Id("strongbox-definition.tier-01"),
                    Id("strongbox-instance.box-001"),
                    Provenance("grant.box", "source.boss"),
                    2L));

            Assert.That(gunAdd.Status, Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(armorAdd.Status, Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(boxAdd.Status, Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(validator.CallCount, Is.EqualTo(2));
            Assert.That(service.Sequence, Is.EqualTo(3L));

            UniqueHoldingSnapshot holding;
            Assert.That(service.TryGetUnique(gun.InstanceId, out holding), Is.True);
            Assert.That(holding.EquipmentInstance, Is.EqualTo(gun));
            Assert.That(holding.Provenance.GrantStableId, Is.EqualTo(Id("grant.gun")));
            Assert.That(service.TryGetUnique(armor.InstanceId, out holding), Is.True);
            Assert.That(holding.EquipmentInstance, Is.EqualTo(armor));
            Assert.That(service.TryGetUnique(Id("strongbox-instance.box-001"), out holding), Is.True);
            Assert.That(holding.RewardKind, Is.EqualTo(RewardGrantKind.Strongbox));

            Assert.That(service.Apply(
                PlayerHoldingsCommand.RemoveEquipment(
                    Id("transaction.gun-remove"),
                    Id("operation.shop-sale"),
                    AuthorityId,
                    gun.DefinitionId,
                    gun.InstanceId,
                    Provenance("grant.gun", "source.shop"),
                    3L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(service.Apply(
                PlayerHoldingsCommand.RemoveStrongbox(
                    Id("transaction.box-remove"),
                    Id("operation.box-open"),
                    AuthorityId,
                    Id("strongbox-definition.tier-01"),
                    Id("strongbox-instance.box-001"),
                    Provenance("grant.box", "source.box-open"),
                    4L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(service.TryGetUnique(gun.InstanceId, out holding), Is.False);
            Assert.That(service.TryGetUnique(Id("strongbox-instance.box-001"), out holding), Is.False);
            Assert.That(service.TryGetUnique(armor.InstanceId, out holding), Is.True);
        }

        [Test]
        public void PremiumAmmoAndArbitraryMiscStacksAddAndRemove()
        {
            PlayerHoldingsActions service = CreateService();
            StableId ammoId = Id("premium-ammo.incendiary");
            StableId miscId = Id("misc.future-widget-2049");

            Assert.That(service.Apply(Stack(
                "transaction.ammo-add",
                EconomyTransactionOperation.AddStack,
                RewardGrantKind.PremiumAmmo,
                ammoId,
                25L,
                0L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(service.Apply(Stack(
                "transaction.misc-add",
                EconomyTransactionOperation.AddStack,
                RewardGrantKind.Miscellaneous,
                miscId,
                7L,
                1L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(service.Apply(Stack(
                "transaction.ammo-remove",
                EconomyTransactionOperation.RemoveStack,
                RewardGrantKind.PremiumAmmo,
                ammoId,
                4L,
                2L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(service.Apply(Stack(
                "transaction.misc-remove",
                EconomyTransactionOperation.RemoveStack,
                RewardGrantKind.Miscellaneous,
                miscId,
                7L,
                3L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));

            Assert.That(service.GetStackQuantity(
                RewardGrantKind.PremiumAmmo,
                ammoId), Is.EqualTo(21L));
            Assert.That(service.GetStackQuantity(
                RewardGrantKind.Miscellaneous,
                miscId), Is.Zero);
        }

        [Test]
        public void UniqueCollisionMissingAndEquipmentValidationRejectWithoutMutation()
        {
            var validator = new RecordingEquipmentValidator();
            PlayerHoldingsActions service = CreateService(validator);
            EquipmentInstance equipment = Equipment(
                "equipment-instance.collision",
                "equipment-definition.blaster",
                "quality.common");

            Assert.That(service.Apply(PlayerHoldingsCommand.AddEquipment(
                Id("transaction.unique-add"),
                Id("operation.reward"),
                AuthorityId,
                equipment,
                Provenance("grant.unique", "source.enemy"))).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(service.Apply(PlayerHoldingsCommand.RemoveEquipment(
                Id("transaction.unique-remove"),
                Id("operation.remove"),
                AuthorityId,
                equipment.DefinitionId,
                equipment.InstanceId,
                Provenance("grant.unique", "source.remove"))).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));

            PlayerHoldingsMutationResult collision = service.Apply(
                PlayerHoldingsCommand.AddStrongbox(
                    Id("transaction.collision"),
                    Id("operation.reward-2"),
                    AuthorityId,
                    Id("strongbox-definition.tier-02"),
                    equipment.InstanceId,
                    Provenance("grant.box", "source.enemy")));
            PlayerHoldingsMutationResult missing = service.Apply(
                PlayerHoldingsCommand.RemoveStrongbox(
                    Id("transaction.missing"),
                    Id("operation.open"),
                    AuthorityId,
                    Id("strongbox-definition.tier-02"),
                    Id("strongbox-instance.missing"),
                    Provenance("grant.missing", "source.open")));

            validator.Accept = false;
            PlayerHoldingsMutationResult invalidEquipment = service.Apply(
                PlayerHoldingsCommand.AddEquipment(
                    Id("transaction.invalid-equipment"),
                    Id("operation.reward-3"),
                    AuthorityId,
                    Equipment(
                        "equipment-instance.invalid",
                        "equipment-definition.unknown",
                        "quality.invalid"),
                    Provenance("grant.invalid", "source.enemy")));

            Assert.That(collision.Status, Is.EqualTo(PlayerHoldingsMutationStatus.UniqueInstanceCollision));
            Assert.That(missing.Status, Is.EqualTo(PlayerHoldingsMutationStatus.MissingItem));
            Assert.That(invalidEquipment.Status, Is.EqualTo(PlayerHoldingsMutationStatus.EquipmentValidationRejected));
            Assert.That(service.Sequence, Is.EqualTo(2L));
            Assert.That(service.ExportSnapshot().UniqueHoldings, Is.Empty);
        }

        [Test]
        public void StackUnderflowCapacityOverflowAndTypeMismatchReject()
        {
            PlayerHoldingsActions bounded = CreateService(maximumStackQuantity: 10L);
            StableId item = Id("misc.bound-item");
            Assert.That(bounded.Apply(Stack(
                "transaction.bound-add",
                EconomyTransactionOperation.AddStack,
                RewardGrantKind.Miscellaneous,
                item,
                8L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));

            Assert.That(bounded.Apply(Stack(
                "transaction.underflow",
                EconomyTransactionOperation.RemoveStack,
                RewardGrantKind.Miscellaneous,
                item,
                9L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.InsufficientValue));
            Assert.That(bounded.Apply(Stack(
                "transaction.capacity",
                EconomyTransactionOperation.AddStack,
                RewardGrantKind.Miscellaneous,
                item,
                3L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.InsufficientCapacity));
            Assert.That(bounded.Apply(Stack(
                "transaction.type-mismatch",
                EconomyTransactionOperation.AddStack,
                RewardGrantKind.PremiumAmmo,
                item,
                1L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.TypeMismatch));
            Assert.That(bounded.GetStackQuantity(
                RewardGrantKind.Miscellaneous,
                item), Is.EqualTo(8L));
            Assert.That(bounded.Sequence, Is.EqualTo(1L));

            PlayerHoldingsActions huge = CreateService(
                maximumStackQuantity: long.MaxValue);
            Assert.That(huge.Apply(Stack(
                "transaction.max",
                EconomyTransactionOperation.AddStack,
                RewardGrantKind.Miscellaneous,
                Id("misc.max"),
                long.MaxValue)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(huge.Apply(Stack(
                "transaction.overflow",
                EconomyTransactionOperation.AddStack,
                RewardGrantKind.Miscellaneous,
                Id("misc.max"),
                1L)).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.ArithmeticOverflow));
        }

        [Test]
        public void WrongRewardTypeAndWrongAuthorityRejectWithoutPartialMutation()
        {
            PlayerHoldingsActions service = CreateService();
            EconomyTransactionCommand raw = EconomyTransactionCommand.Create(
                Id("transaction.wrong-reward"),
                Id("operation.raw"),
                AuthorityId,
                EconomyTransactionOperation.AddStack,
                EconomyResourceKind.Item,
                Id("misc.raw"),
                null,
                5L,
                null);
            PlayerHoldingsCommand wrongReward = PlayerHoldingsCommand.Create(
                raw,
                RewardGrantKind.Money,
                Provenance("grant.raw", "source.raw"));
            PlayerHoldingsCommand wrongAuthority = PlayerHoldingsCommand.AddStack(
                Id("transaction.wrong-authority"),
                Id("operation.raw-2"),
                Id("holdings.someone-else"),
                RewardGrantKind.Miscellaneous,
                Id("misc.raw"),
                5L,
                Provenance("grant.raw-2", "source.raw"));

            Assert.That(service.Apply(wrongReward).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.WrongRewardType));
            Assert.That(service.Apply(wrongAuthority).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.WrongAuthority));
            Assert.That(service.Sequence, Is.Zero);
            Assert.That(service.ExportSnapshot().StackHoldings, Is.Empty);
        }

        [Test]
        public void DuplicateConflictAndExpectedSequenceAreExactOnce()
        {
            PlayerHoldingsActions service = CreateService();
            PlayerHoldingsCommand original = Stack(
                "transaction.exact-once",
                EconomyTransactionOperation.AddStack,
                RewardGrantKind.Miscellaneous,
                Id("misc.exact"),
                5L,
                0L);

            PlayerHoldingsMutationResult first = service.Apply(original);
            PlayerHoldingsMutationResult duplicate = service.Apply(original);
            PlayerHoldingsMutationResult conflict = service.Apply(Stack(
                "transaction.exact-once",
                EconomyTransactionOperation.AddStack,
                RewardGrantKind.Miscellaneous,
                Id("misc.exact"),
                6L,
                0L));
            PlayerHoldingsCommand stale = Stack(
                "transaction.sequence-stale",
                EconomyTransactionOperation.AddStack,
                RewardGrantKind.Miscellaneous,
                Id("misc.exact"),
                1L,
                0L);
            PlayerHoldingsMutationResult sequenceConflict = service.Apply(stale);
            PlayerHoldingsMutationResult sequenceDuplicate = service.Apply(stale);

            Assert.That(first.Status, Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(PlayerHoldingsMutationStatus.ExactDuplicateNoChange));
            Assert.That(duplicate.OriginalStatus, Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            Assert.That(conflict.Status, Is.EqualTo(PlayerHoldingsMutationStatus.ConflictingDuplicate));
            Assert.That(sequenceConflict.Status, Is.EqualTo(PlayerHoldingsMutationStatus.ExpectedSequenceConflict));
            Assert.That(sequenceDuplicate.Status, Is.EqualTo(PlayerHoldingsMutationStatus.ExactDuplicateNoChange));
            Assert.That(sequenceDuplicate.OriginalStatus, Is.EqualTo(PlayerHoldingsMutationStatus.ExpectedSequenceConflict));
            Assert.That(service.Sequence, Is.EqualTo(1L));
            Assert.That(service.GetStackQuantity(
                RewardGrantKind.Miscellaneous,
                Id("misc.exact")), Is.EqualTo(5L));
            Assert.That(service.ExportSnapshot().Transactions.Count, Is.EqualTo(2));
        }

        [Test]
        public void SnapshotRoundTripIsDeterministicAndPreservesReplayHistory()
        {
            PlayerHoldingsActions source = CreateService();
            EquipmentInstance equipment = Equipment(
                "equipment-instance.snapshot",
                "equipment-definition.snapshot",
                "quality.snapshot");
            source.Apply(PlayerHoldingsCommand.AddEquipment(
                Id("transaction.snapshot-equipment"),
                Id("operation.snapshot"),
                AuthorityId,
                equipment,
                Provenance("grant.snapshot-equipment", "source.snapshot"),
                0L));
            source.Apply(Stack(
                "transaction.snapshot-stack",
                EconomyTransactionOperation.AddStack,
                RewardGrantKind.Miscellaneous,
                Id("misc.snapshot"),
                12L,
                1L));
            PlayerHoldingsCommand rejected = Stack(
                "transaction.snapshot-rejected",
                EconomyTransactionOperation.RemoveStack,
                RewardGrantKind.Miscellaneous,
                Id("misc.snapshot"),
                13L,
                2L);
            source.Apply(rejected);

            PlayerHoldingsSnapshot first = source.ExportSnapshot();
            PlayerHoldingsActions restored = CreateService();
            PlayerHoldingsImportResult import = restored.ImportSnapshot(first);
            PlayerHoldingsSnapshot second = restored.ExportSnapshot();

            Assert.That(import.Status, Is.EqualTo(PlayerHoldingsImportStatus.Imported));
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(second.LedgerSnapshot.Fingerprint,
                Is.EqualTo(first.LedgerSnapshot.Fingerprint));
            Assert.That(restored.Sequence, Is.EqualTo(2L));
            Assert.That(restored.GetStackQuantity(
                RewardGrantKind.Miscellaneous,
                Id("misc.snapshot")), Is.EqualTo(12L));
            Assert.That(restored.Apply(rejected).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.ExactDuplicateNoChange));
            Assert.That(restored.Apply(PlayerHoldingsCommand.AddStrongbox(
                Id("transaction.snapshot-collision"),
                Id("operation.snapshot-2"),
                AuthorityId,
                Id("strongbox-definition.snapshot"),
                equipment.InstanceId,
                Provenance("grant.snapshot-box", "source.snapshot"))).Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.UniqueInstanceCollision));
        }

        [Test]
        public void CorruptSnapshotLeavesPreviousStateUnchanged()
        {
            PlayerHoldingsActions source = CreateService();
            source.Apply(Stack(
                "transaction.source",
                EconomyTransactionOperation.AddStack,
                RewardGrantKind.Miscellaneous,
                Id("misc.source"),
                20L));
            PlayerHoldingsSnapshot valid = source.ExportSnapshot();
            var corrupt = new PlayerHoldingsSnapshot(
                valid.SchemaVersion,
                valid.AuthorityStableId,
                valid.MaximumStackQuantity,
                valid.LedgerSnapshot,
                valid.UniqueHoldings,
                valid.StackHoldings,
                valid.Transactions,
                "sha256:0000000000000000000000000000000000000000000000000000000000000000");

            PlayerHoldingsActions target = CreateService();
            target.Apply(Stack(
                "transaction.target",
                EconomyTransactionOperation.AddStack,
                RewardGrantKind.PremiumAmmo,
                Id("premium-ammo.target"),
                3L));
            PlayerHoldingsSnapshot before = target.ExportSnapshot();
            PlayerHoldingsImportResult result = target.ImportSnapshot(corrupt);
            PlayerHoldingsSnapshot after = target.ExportSnapshot();

            Assert.That(result.Status, Is.EqualTo(PlayerHoldingsImportStatus.FingerprintMismatch));
            Assert.That(after.Fingerprint, Is.EqualTo(before.Fingerprint));
            Assert.That(target.Sequence, Is.EqualTo(1L));
            Assert.That(target.GetStackQuantity(
                RewardGrantKind.PremiumAmmo,
                Id("premium-ammo.target")), Is.EqualTo(3L));
        }

        private static PlayerHoldingsActions CreateService(
            RecordingEquipmentValidator validator = null,
            long maximumStackQuantity = 1000L)
        {
            return new PlayerHoldingsActions(
                AuthorityId,
                maximumStackQuantity,
                validator ?? new RecordingEquipmentValidator());
        }

        private static PlayerHoldingsCommand Stack(
            string transactionId,
            EconomyTransactionOperation operation,
            RewardGrantKind rewardKind,
            StableId itemStableId,
            long quantity,
            long? expectedSequence = null)
        {
            return operation == EconomyTransactionOperation.AddStack
                ? PlayerHoldingsCommand.AddStack(
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
                : PlayerHoldingsCommand.RemoveStack(
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

        private static HoldingProvenance Provenance(
            string grantId,
            string sourceId)
        {
            return HoldingProvenance.Create(
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
