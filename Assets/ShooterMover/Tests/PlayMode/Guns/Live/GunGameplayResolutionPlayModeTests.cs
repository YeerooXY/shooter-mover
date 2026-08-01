using System.Collections;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.UnityAdapters.Players;
using ShooterMover.UnityAdapters.Guns.Live;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Guns.Live
{
    public sealed class GunGameplayResolutionPlayModeTests
    {
        [UnityTest]
        public IEnumerator ReplacedFirstMountBindsExactInstanceToSpawnedPlayer()
        {
            var runtime = new PlayerLoadoutLive(
                PlayerRouteProfilePayload.Create(
                    StableId.Parse("character.playmode-exact-gun"),
                    StableId.Parse(
                        GunMountPolicy
                            .DefensiveLoadoutProfileId),
                    new StableId[
                        PlayerRouteProfilePayload.GunSlotCount]));
            LoadoutRegistry.Register(
                runtime.GunInventory,
                runtime.MountLoadoutAuthority);
            GunSlot firstPosition =
                runtime.MountLayout.Positions[0];
            GunSlot secondPosition =
                runtime.MountLayout.Positions[1];
            StableId firstMount = firstPosition.LoadoutSlotStableId;
            StableId secondMount = secondPosition.LoadoutSlotStableId;
            StableId replacement = runtime.MountLoadoutAuthority.ExportSnapshot()
                .Find(secondPosition.MountStableId).InstanceId;
            var inventory = new InventoryMenuActions(
                runtime.CurrentRoutePayload,
                runtime.Holdings,
                runtime.GunInventory,
                runtime.LoadoutAuthority,
                runtime.MountLayout,
                runtime.GunCatalog);

            Assert.That(
                inventory.Unequip(secondMount).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.SelectionChanged));
            inventory.SelectGun(replacement);
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

            GunItem exact;
            string rejectionCode;
            Assert.That(
                runtime.TryResolveFirstActiveEquippedGun(
                    out exact,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(exact.InstanceId, Is.EqualTo(replacement));

            GunMark mark;
            Assert.That(
                GunCatalogProvider.Current.TryGetMark(
                    exact.GunDefinitionId.Value,
                    out mark),
                Is.True);
            var player = new GameObject("Canonical Player Test");
            try
            {
                PlayerGunSource source =
                    player.AddComponent<PlayerGunSource>();
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
                Assert.That(source.ExactGunInstanceId, Is.EqualTo(replacement));
                Assert.That(projected.InstanceId, Is.EqualTo(replacement));
                Assert.That(
                    runtime.EquipmentCatalog.FindEquipmentDefinition(
                        projected.DefinitionId).RuntimeGunReferenceId,
                    Is.EqualTo(exact.GunDefinitionId.ToRuntimeReference()));
            }
            finally
            {
                Object.Destroy(player);
            }

            var lookup = new GunEquipmentViewLookup(
                runtime.GunInventory,
                runtime.EquipmentCatalog,
                runtime.Holdings);
            EquipmentInstance missing;
            Assert.That(
                lookup.TryResolve(
                    new EquipmentInstanceId(
                        StableId.Parse("instance.not-owned-by-character")),
                    out missing),
                Is.False,
                "Gameplay must not fabricate a fallback gun.");

            yield return null;
        }
    }
}
