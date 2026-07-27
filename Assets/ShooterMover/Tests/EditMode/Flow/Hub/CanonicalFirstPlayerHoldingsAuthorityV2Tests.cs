using System;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class CanonicalFirstPlayerHoldingsAuthorityV2Tests
    {
        [Test]
        public void MutatingReceiptExceptionRestoresBothAuthorities()
        {
            var receiptService = new PlayerHoldingsService(
                Id("authority.receipt-compensation"),
                99L,
                new ProductionEquipmentCatalogAdapterV1(
                    ProductionWeaponCatalogProvider.EquipmentCatalog));
            var mutatingReceipt = new MutatingThrowingReceiptAuthority(
                receiptService);
            var weapons = new ProductionWeaponHoldingsAuthorityV2();
            var authority = new CanonicalFirstPlayerHoldingsAuthorityV2(
                mutatingReceipt,
                weapons);

            EquipmentDefinition definition =
                ProductionWeaponCatalogProvider.EquipmentCatalog
                    .FindEquipmentDefinition(
                        Id("equipment.weapon-rattler-mk1"));
            EquipmentInstance exact = EquipmentInstance.Create(
                Id("instance.receipt-compensation"),
                definition.DefinitionId,
                definition.ItemLevelRange.Minimum,
                definition.QualityTiers[0].QualityId,
                Array.Empty<AugmentInstance>());
            PlayerHoldingsCommandV1 command =
                PlayerHoldingsCommandV1.AddEquipment(
                    Id("transaction.receipt-compensation"),
                    Id("operation.receipt-compensation"),
                    authority.AuthorityStableId,
                    exact,
                    HoldingProvenanceV1.Create(
                        Id("grant.receipt-compensation"),
                        Id("source.receipt-compensation")),
                    authority.Sequence);

            WeaponHoldingsSnapshotV2 weaponsBefore = weapons.ExportSnapshot();
            PlayerHoldingsSnapshotV1 receiptsBefore =
                receiptService.ExportSnapshot();

            Assert.That(
                () => authority.Apply(command),
                Throws.InvalidOperationException.With.Message.Contains(
                    "mutating-receipt-test-failure"));
            Assert.That(
                weapons.ExportSnapshot().Fingerprint,
                Is.EqualTo(weaponsBefore.Fingerprint));
            Assert.That(
                receiptService.ExportSnapshot().Fingerprint,
                Is.EqualTo(receiptsBefore.Fingerprint));
            Assert.That(weapons.Find(exact.InstanceId), Is.Null);
            Assert.That(receiptService.ExportSnapshot().UniqueHoldings, Is.Empty);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class MutatingThrowingReceiptAuthority :
            IPlayerHoldingsAuthorityV1
        {
            private readonly IPlayerHoldingsAuthorityV1 inner;

            public MutatingThrowingReceiptAuthority(
                IPlayerHoldingsAuthorityV1 authority)
            {
                inner = authority ?? throw new ArgumentNullException(nameof(authority));
            }

            public StableId AuthorityStableId { get { return inner.AuthorityStableId; } }
            public long Sequence { get { return inner.Sequence; } }

            public PlayerHoldingsSnapshotV1 ExportSnapshot()
            {
                return inner.ExportSnapshot();
            }

            public PlayerHoldingsImportResultV1 ImportSnapshot(
                PlayerHoldingsSnapshotV1 snapshot)
            {
                return inner.ImportSnapshot(snapshot);
            }

            public PlayerHoldingsMutationResultV1 Apply(
                PlayerHoldingsCommandV1 command)
            {
                PlayerHoldingsMutationResultV1 result = inner.Apply(command);
                if (result == null
                    || result.Status != PlayerHoldingsMutationStatusV1.Applied)
                {
                    throw new InvalidOperationException(
                        "mutating-receipt-test-setup-rejected");
                }
                throw new InvalidOperationException(
                    "mutating-receipt-test-failure");
            }
        }
    }
}
