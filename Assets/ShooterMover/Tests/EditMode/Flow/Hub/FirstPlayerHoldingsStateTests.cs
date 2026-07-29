using System;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Holdings;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class FirstPlayerHoldingsStateTests
    {
        [Test]
        public void MutatingReceiptExceptionRestoresBothAuthorities()
        {
            var receiptService = new PlayerHoldingsActions(
                Id("authority.receipt-compensation"),
                99L,
                new EquipmentCatalogBridge(
                    GunCatalogProvider.EquipmentCatalog));
            var mutatingReceipt = new MutatingThrowingReceiptState(
                receiptService);
            var guns = new GunInventoryState();
            var authority = new FirstPlayerHoldingsState(
                mutatingReceipt,
                guns);

            EquipmentDefinition definition =
                GunCatalogProvider.EquipmentCatalog
                    .FindEquipmentDefinition(
                        Id("equipment.gun-rattler-mk1"));
            EquipmentInstance exact = EquipmentInstance.Create(
                Id("instance.receipt-compensation"),
                definition.DefinitionId,
                definition.ItemLevelRange.Minimum,
                definition.QualityTiers[0].QualityId,
                Array.Empty<AugmentInstance>());
            PlayerHoldingsCommand command =
                PlayerHoldingsCommand.AddEquipment(
                    Id("transaction.receipt-compensation"),
                    Id("operation.receipt-compensation"),
                    authority.AuthorityStableId,
                    exact,
                    HoldingProvenance.Create(
                        Id("grant.receipt-compensation"),
                        Id("source.receipt-compensation")),
                    authority.Sequence);

            GunInventorySnapshot gunsBefore = guns.ExportSnapshot();
            PlayerHoldingsSnapshot receiptsBefore =
                receiptService.ExportSnapshot();

            Assert.That(
                () => authority.Apply(command),
                Throws.InvalidOperationException.With.Message.Contains(
                    "mutating-receipt-test-failure"));
            Assert.That(
                guns.ExportSnapshot().Fingerprint,
                Is.EqualTo(gunsBefore.Fingerprint));
            Assert.That(
                receiptService.ExportSnapshot().Fingerprint,
                Is.EqualTo(receiptsBefore.Fingerprint));
            Assert.That(guns.Find(exact.InstanceId), Is.Null);
            Assert.That(receiptService.ExportSnapshot().UniqueHoldings, Is.Empty);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class MutatingThrowingReceiptState :
            IPlayerHoldingsState
        {
            private readonly IPlayerHoldingsState inner;

            public MutatingThrowingReceiptState(
                IPlayerHoldingsState authority)
            {
                inner = authority ?? throw new ArgumentNullException(nameof(authority));
            }

            public StableId AuthorityStableId { get { return inner.AuthorityStableId; } }
            public long Sequence { get { return inner.Sequence; } }

            public PlayerHoldingsSnapshot ExportSnapshot()
            {
                return inner.ExportSnapshot();
            }

            public PlayerHoldingsImportResult ImportSnapshot(
                PlayerHoldingsSnapshot snapshot)
            {
                return inner.ImportSnapshot(snapshot);
            }

            public PlayerHoldingsMutationResult Apply(
                PlayerHoldingsCommand command)
            {
                PlayerHoldingsMutationResult result = inner.Apply(command);
                if (result == null
                    || result.Status != PlayerHoldingsMutationStatus.Applied)
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
