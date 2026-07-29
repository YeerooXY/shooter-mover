using System.Collections;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Players;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Weapons.Live
{
    public sealed class WeaponGameplayResolutionPlayModeTests
    {
        [UnityTest]
        public IEnumerator ReplacedFirstMountBindsExactInstanceToSpawnedPlayer()
        {
            var runtime = new PlayerLoadoutLive(
                PlayerRouteProfilePayload.Create(
                    StableId.Parse("character.playmode-exact-weapon"),
                    StableId.Parse(
                        WeaponMountPolicy
                            .DefensiveLoadoutProfileId),
                    new StableId[
                        PlayerRouteProfilePayload.WeaponSlotCount]));
            WeaponMountLoadoutRegistry.Register(
                runtime.WeaponHoldings,
                runtime.MountLoadoutAuthority);
            WeaponMountPosition firstPosition =
                runtime.MountLayout.Positions[0];
            WeaponMountPosition secondPosition =
                runtime.MountLayout.Positions[1];
            StableId firstMount = firstPosition.LoadoutSlotStableId;
            StableId secondMount = secondPosition.LoadoutSlotStableId;
            StableId replacement = runtime.MountLoadoutAuthority.ExportSnapshot()
                .Find(secondPosition.MountStableId).InstanceId;
            var inventory = new WeaponInventoryScreenActions(
                runtime.CurrentRoutePayload,
                runtime.Holdings,
                runtime.WeaponHoldings,
                runtime.LoadoutAuthority,
                runtime.MountLayout,
                runtime.WeaponCatalog);

            Assert.That(
                inventory.Unequip(secondMount).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.SelectionChanged));
            inventory.SelectWeapon(replacement);
            Assert.That(
                inventory.EquipSelected(firstMount).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.SelectionChanged));
            Assert.That(
                inventory.Confirm().Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.Confirmed));
            Assert.That(
                runtime.MountLoadoutAuthority.ExportSnapshot()
                    .Find(firstPosition.MountStableId).InstanceId,
                Is.EqualTo(replacement));

            WeaponEquipmentInstance exact;
            string rejectionCode;
            Assert.That(
                runtime.TryResolveFirstActiveEquippedWeapon(
                    out exact,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(exact.InstanceId, Is.EqualTo(replacement));

            WeaponMark mark;
            Assert.That(
                WeaponCatalogProvider.Current.TryGetMark(
                    exact.WeaponDefinitionId.Value,
                    out mark),
                Is.True);
            var player = new GameObject("Canonical Player Test");
            try
            {
                PlayerWeaponSource source =
                    player.AddComponent<PlayerWeaponSource>();
                source.Bind(
                    runtime.RoutePayload.SelectedCharacterStableId,
                    runtime,
                    exact,
                    mark);

                EquipmentInstance projected;
                Assert.That(
                    source.TryResolveLiveEquipment(
                        out projected,
                        out rejectionCode),
                    Is.True,
                    rejectionCode);
                Assert.That(source.ExactWeaponInstanceId, Is.EqualTo(replacement));
                Assert.That(projected.InstanceId, Is.EqualTo(replacement));
                Assert.That(
                    runtime.EquipmentCatalog.FindEquipmentDefinition(
                        projected.DefinitionId).RuntimeWeaponReferenceId.ToString(),
                    Is.EqualTo(exact.WeaponDefinitionId.Value));
            }
            finally
            {
                Object.Destroy(player);
            }

            var lookup = new WeaponEquipmentViewLookup(
                runtime.WeaponHoldings,
                runtime.EquipmentCatalog,
                runtime.Holdings);
            EquipmentInstance missing;
            Assert.That(
                lookup.TryResolve(
                    new EquipmentInstanceId(
                        StableId.Parse("instance.not-owned-by-character")),
                    out missing),
                Is.False,
                "Gameplay must not fabricate a fallback weapon.");

            yield return null;
        }
    }
}
