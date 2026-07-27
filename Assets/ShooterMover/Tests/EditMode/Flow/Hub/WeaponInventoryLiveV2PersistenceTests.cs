using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class WeaponInventoryLiveV2PersistenceTests
    {
        [Test]
        public void SerializedSchemaV2RestartPreservesExactFirstMountWithoutGranting()
        {
            var runtime = new ProductionPlayerLoadoutRuntimeV1(Route(
                "restart-source",
                ProductionWeaponMountPolicyV1.HealerLoadoutProfileId));
            ProductionWeaponMountPositionV1 firstPosition =
                runtime.MountLayout.Positions[0];
            ProductionWeaponMountPositionV1 secondPosition =
                runtime.MountLayout.Positions[1];
            StableId firstSlot = firstPosition.LoadoutSlotStableId;
            StableId secondSlot = secondPosition.LoadoutSlotStableId;
            StableId replacement = runtime.MountLoadoutAuthority.ExportSnapshot()
                .Find(secondPosition.MountStableId).InstanceId;

            var inventory = Service(runtime);
            Assert.That(
                inventory.Unequip(secondSlot).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.SelectionChanged));
            inventory.SelectWeapon(replacement);
            Assert.That(
                inventory.EquipSelected(firstSlot).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.SelectionChanged));
            Assert.That(
                inventory.Confirm().Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.Confirmed));

            PlayerHoldingsSnapshotV1 genericBefore =
                runtime.Holdings.ExportSnapshot();
            WeaponHoldingsSnapshotV2 weaponsBefore =
                runtime.WeaponHoldings.ExportSnapshot();
            WeaponMountLoadoutSnapshotV2 mountsBefore =
                runtime.MountLoadoutAuthority.ExportSnapshot();
            InventoryLoadoutAuthoritySnapshotV1 armorOnlyBefore =
                ProductionWeaponMountLoadoutProjectionV2.ArmorOnly(
                    runtime.LoadoutAuthority.ExportSnapshot());

            string weaponPayload = WeaponHoldingsSaveComponentV2.Codec.Encode(
                weaponsBefore);
            string mountPayload = WeaponMountLoadoutSaveComponentV2.Codec.Encode(
                mountsBefore);
            string armorPayload = KnownSaveComponentCodecsV1.ExactInstanceLoadout
                .Encode(armorOnlyBefore);

            WeaponHoldingsSnapshotV2 decodedWeapons;
            WeaponMountLoadoutSnapshotV2 decodedMounts;
            InventoryLoadoutAuthoritySnapshotV1 decodedArmor;
            string rejectionCode;
            Assert.That(
                WeaponHoldingsSaveComponentV2.Codec.TryDecode(
                    weaponPayload,
                    out decodedWeapons,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(
                WeaponMountLoadoutSaveComponentV2.Codec.TryDecode(
                    mountPayload,
                    out decodedMounts,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(
                KnownSaveComponentCodecsV1.ExactInstanceLoadout.TryDecode(
                    armorPayload,
                    out decodedArmor,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(
                decodedArmor.Bindings
                    .Where((item, index) =>
                        InventoryLoadoutSlotsV1.All[index].Kind
                            == InventoryLoadoutSlotKindV1.Weapon)
                    .All(item => item.EquipmentInstanceStableId == null),
                Is.True,
                "V2 saves must not persist weapon truth in legacy generic slots.");

            ProductionPlayerLoadoutRuntimeV1 restored =
                ProductionPlayerLoadoutRuntimeV1.Restore(
                    runtime.RoutePayload.SelectedCharacterStableId,
                    runtime.RoutePayload.LoadoutProfileStableId,
                    genericBefore,
                    decodedWeapons,
                    decodedMounts,
                    decodedArmor);

            Assert.That(
                restored.Holdings.ExportSnapshot().Fingerprint,
                Is.EqualTo(genericBefore.Fingerprint));
            Assert.That(
                restored.WeaponHoldings.ExportSnapshot().Fingerprint,
                Is.EqualTo(weaponsBefore.Fingerprint));
            Assert.That(
                restored.MountLoadoutAuthority.ExportSnapshot().Fingerprint,
                Is.EqualTo(mountsBefore.Fingerprint));
            Assert.That(
                restored.WeaponHoldings.Count,
                Is.EqualTo(weaponsBefore.Instances.Count));
            Assert.That(
                restored.MountLoadoutAuthority.ExportSnapshot()
                    .Find(firstPosition.MountStableId).InstanceId,
                Is.EqualTo(replacement));

            WeaponEquipmentInstance exact;
            Assert.That(
                restored.TryResolveFirstActiveEquippedWeapon(
                    out exact,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(exact.InstanceId, Is.EqualTo(replacement));

            WeaponHoldingsSnapshotV2 beforeOpen =
                restored.WeaponHoldings.ExportSnapshot();
            WeaponMountLoadoutSnapshotV2 mountsBeforeOpen =
                restored.MountLoadoutAuthority.ExportSnapshot();
            Service(restored).Refresh();
            Service(restored).Refresh();
            Assert.That(
                restored.WeaponHoldings.ExportSnapshot().Fingerprint,
                Is.EqualTo(beforeOpen.Fingerprint),
                "Reopening Inventory after restore must never grant weapons.");
            Assert.That(
                restored.MountLoadoutAuthority.ExportSnapshot().Fingerprint,
                Is.EqualTo(mountsBeforeOpen.Fingerprint),
                "Reopening Inventory must not repair or reorder physical mounts.");
        }

        private static CanonicalWeaponInventoryScreenServiceV2 Service(
            ProductionPlayerLoadoutRuntimeV1 runtime)
        {
            ProductionWeaponMountLoadoutRegistryV2.Register(
                runtime.WeaponHoldings,
                runtime.MountLoadoutAuthority);
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
