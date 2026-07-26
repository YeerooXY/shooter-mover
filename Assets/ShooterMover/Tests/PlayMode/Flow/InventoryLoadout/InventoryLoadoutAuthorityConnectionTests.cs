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
    public sealed class InventoryLoadoutAuthorityConnectionTests
    {
        [UnityTest]
        public IEnumerator ConnectingAuthoritiesPreservesProductionReturnCallback()
        {
            PlayerRouteProfilePayloadV1 draft =
                PlayerRouteProfilePayloadV1.Create(
                    StableId.Parse("character.loadout-connect"),
                    StableId.Parse(
                        ProductionWeaponMountPolicyV1
                            .DefensiveLoadoutProfileId),
                    new StableId[
                        PlayerRouteProfilePayloadV1.WeaponSlotCount]);
            var runtime = new ProductionPlayerLoadoutRuntimeV1(draft);
            GameObject host = new GameObject("Loadout connection test");
            InventoryLoadoutScreenControllerV1 controller =
                host.AddComponent<InventoryLoadoutScreenControllerV1>();
            PlayerRouteProfilePayloadV1 returned = null;
            PlayerRouteProfilePayloadV1 confirmed = null;
            var order = new List<string>();

            controller.ConfigureDisconnected(
                delegate(PlayerRouteProfilePayloadV1 payload)
                {
                    order.Add("returned");
                    returned = payload;
                });
            controller.Confirmed +=
                delegate(PlayerRouteProfilePayloadV1 payload)
                {
                    order.Add("confirmed");
                    confirmed = payload;
                };
            controller.Present(
                HubRouteV1.Inventory,
                runtime.RoutePayload);
            controller.ConnectAuthorities(
                runtime.Holdings,
                runtime.CatalogAdapter,
                runtime.LoadoutAuthority);
            InventoryLoadoutScreenResultV1 result =
                controller.Confirm();

            Assert.That(
                result.Status,
                Is.EqualTo(
                    InventoryLoadoutScreenStatusV1.Confirmed));
            Assert.That(confirmed, Is.SameAs(result.RoutePayload));
            Assert.That(returned, Is.SameAs(result.RoutePayload));
            Assert.That(
                order,
                Is.EqualTo(new[] { "confirmed", "returned" }));
            Assert.That(controller.ReturnCount, Is.EqualTo(1));

            Object.Destroy(host);
            yield return null;
        }
    }
}
