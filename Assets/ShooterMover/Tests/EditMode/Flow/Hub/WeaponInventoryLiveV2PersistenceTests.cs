using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class WeaponInventoryLiveV2PersistenceTests
    {
        [Test]
        public void SchemaV2RestorePreservesExactFirstMountWithoutGranting()
        {
            var runtime = new ProductionPlayerLoadoutRuntimeV1(Route(
                "restart-source",
                ProductionWeaponMountPolicyV1.HealerLoadoutProfileId));
            StableId firstMount = runtime.MountLayout.Positions[0]
                .LoadoutSlotStableId;
            StableId secondMount = runtime.MountLayout.Positions[1]
                .LoadoutSlotStableId;
            StableId replacement = runtime.LoadoutAuthority.ExportSnapshot()
                .GetBinding(secondMount).EquipmentInstanceStableId;

            var inventory = Service(runtime);
            Assert.That(
                inventory.Unequip(secondMount).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.SelectionChanged));
            inventory.SelectWeapon(replacement);
            Assert.That(
                inventory.EquipSelected(firstMount).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.SelectionChanged));
            Assert.That(
                inventory.Confirm().Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.Confirmed));

            PlayerHoldingsSnapshotV1 genericBefore =
                runtime.Holdings.ExportSnapshot();
            WeaponHoldingsSnapshotV2 weaponsBefore =
                runtime.WeaponHoldings.ExportSnapshot();
            InventoryLoadoutAuthoritySnapshotV1 loadoutBefore =
                runtime.LoadoutAuthority.ExportSnapshot();

            ProductionPlayerLoadoutRuntimeV1 restored =
                ProductionPlayerLoadoutRuntimeV1.Restore(
                    runtime.RoutePayload.SelectedCharacterStableId,
                    runtime.RoutePayload.LoadoutProfileStableId,
                    genericBefore,
                    weaponsBefore,
                    loadoutBefore);

            Assert.That(
                restored.Holdings.ExportSnapshot().Fingerprint,
                Is.EqualTo(genericBefore.Fingerprint));
            Assert.That(
                restored.WeaponHoldings.ExportSnapshot().Fingerprint,
                Is.EqualTo(weaponsBefore.Fingerprint));
            Assert.That(
                restored.LoadoutAuthority.ExportSnapshot().Fingerprint,
                Is.EqualTo(loadoutBefore.Fingerprint));
            Assert.That(
                restored.WeaponHoldings.Count,
                Is.EqualTo(weaponsBefore.Instances.Count));

            WeaponEquipmentInstance exact;
            string rejectionCode;
            Assert.That(
                restored.TryResolveFirstActiveEquippedWeapon(
                    out exact,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(exact.InstanceId, Is.EqualTo(replacement));

            WeaponHoldingsSnapshotV2 beforeOpen =
                restored.WeaponHoldings.ExportSnapshot();
            Service(restored).Refresh();
            Service(restored).Refresh();
            Assert.That(
                restored.WeaponHoldings.ExportSnapshot().Fingerprint,
                Is.EqualTo(beforeOpen.Fingerprint),
                "Reopening Inventory after restore must never grant weapons.");
        }

        private static CanonicalWeaponInventoryScreenServiceV2 Service(
            ProductionPlayerLoadoutRuntimeV1 runtime)
        {
            return new CanonicalWeaponInventoryScreenServiceV2(
                runtime.CurrentRoutePayload,
                runtime.Holdings,
                runtime.WeaponHoldings,
                runtime.LoadoutAuthority,
                runtime.MountLayout,
                runtime.WeaponCatalog);
        }

        private static PlayerRouteProfilePayloadV1 Route(
            string suffix,
            string profileId)
        {
            return PlayerRouteProfilePayloadV1.Create(
                StableId.Parse("character." + suffix),
                StableId.Parse(profileId),
                new StableId[PlayerRouteProfilePayloadV1.WeaponSlotCount]);
        }
    }
}
