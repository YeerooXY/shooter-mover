using System.Collections;
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
            PlayerRouteProfilePayloadV1 route =
                PlayerRouteProfilePayloadV1.Create(
                    StableId.Parse("character.loadout-connect"),
                    StableId.Parse(
                        "loadout-profile.loadout-connect"),
                    new[]
                    {
                        StableId.Parse(
                            "equipment-instance.flow-draft-slot-1"),
                        StableId.Parse(
                            "equipment-instance.flow-draft-slot-2"),
                        StableId.Parse(
                            "equipment-instance.flow-draft-slot-3"),
                        StableId.Parse(
                            "equipment-instance.flow-draft-slot-4"),
                    });
            var runtime = new ProductionPlayerLoadoutRuntimeV1(route);
            GameObject host = new GameObject("Loadout connection test");
            InventoryLoadoutScreenControllerV1 controller =
                host.AddComponent<InventoryLoadoutScreenControllerV1>();
            PlayerRouteProfilePayloadV1 returned = null;

            controller.ConfigureDisconnected(
                delegate(PlayerRouteProfilePayloadV1 payload)
                {
                    returned = payload;
                });
            controller.Present(HubRouteV1.Inventory, route);
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
            Assert.That(returned, Is.SameAs(result.RoutePayload));
            Assert.That(controller.ReturnCount, Is.EqualTo(1));

            Object.Destroy(host);
            yield return null;
        }
    }
}
