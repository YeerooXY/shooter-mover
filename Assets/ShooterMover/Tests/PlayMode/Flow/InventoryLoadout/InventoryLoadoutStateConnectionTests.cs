using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.UI.InventoryLoadout;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Flow.InventoryLoadout
{
    public sealed class InventoryLoadoutStateConnectionTests
    {
        [UnityTest]
        public IEnumerator ConnectingCanonicalAuthoritiesPreservesProductionReturnCallback()
        {
            PlayerRouteProfilePayload draft =
                PlayerRouteProfilePayload.Create(
                    StableId.Parse("character.loadout-connect"),
                    StableId.Parse(
                        WeaponMountPolicy
                            .DefensiveLoadoutProfileId),
                    new StableId[
                        PlayerRouteProfilePayload.WeaponSlotCount]);
            var runtime = new PlayerLoadoutLive(draft);
            WeaponMountLoadoutRegistry.Register(
                runtime.WeaponHoldings,
                runtime.MountLoadoutAuthority);
            GameObject host = new GameObject("Loadout connection test");
            InventoryLoadoutScreenController controller =
                host.AddComponent<InventoryLoadoutScreenController>();
            PlayerRouteProfilePayload returned = null;
            PlayerRouteProfilePayload confirmed = null;
            var order = new List<string>();

            controller.ConfigureDisconnected(
                delegate(PlayerRouteProfilePayload payload)
                {
                    order.Add("returned");
                    returned = payload;
                });
            controller.Confirmed +=
                delegate(PlayerRouteProfilePayload payload)
                {
                    order.Add("confirmed");
                    confirmed = payload;
                };
            controller.Present(
                HubRoute.Inventory,
                runtime.CurrentRoutePayload);
            controller.ConnectCanonicalAuthorities(
                runtime.Holdings,
                runtime.CatalogBridge,
                runtime.WeaponHoldings,
                runtime.LoadoutAuthority,
                runtime.MountLayout,
                runtime.WeaponCatalog);
            InventoryLoadoutScreenResult result =
                controller.Confirm();

            Assert.That(
                result.Status,
                Is.EqualTo(
                    InventoryLoadoutScreenStatus.Confirmed));
            Assert.That(confirmed, Is.SameAs(result.RoutePayload));
            Assert.That(returned, Is.SameAs(result.RoutePayload));
            Assert.That(
                order,
                Is.EqualTo(new[] { "confirmed", "returned" }));
            Assert.That(controller.ReturnCount, Is.EqualTo(1));
            Assert.That(controller.CanonicalSnapshot, Is.Not.Null);
            Assert.That(controller.CanonicalSnapshot.OwnedWeapons.Count, Is.EqualTo(4));
            Assert.That(
                runtime.MountLoadoutAuthority.ExportSnapshot().Bindings.Count,
                Is.EqualTo(4));

            Object.Destroy(host);
            yield return null;
        }
    }
}
