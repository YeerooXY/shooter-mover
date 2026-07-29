using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.UI.Hub;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Flow.Hub
{
    public sealed class HubFlowControllerTests
    {
        [UnityTest]
        public IEnumerator MainMenuCharacterSelectHubAndEveryDestinationRetainPayload()
        {
            GameObject host = new GameObject("HUB-001 PlayMode Host");
            HubFlowController controller = host.AddComponent<HubFlowController>();
            PlayerRouteProfilePayload payload = CreatePayload();
            var adapter = new RecordingBridge();
            controller.ConfigureForTests(payload, adapter);
            yield return null;

            Assert.That(controller.CurrentRoute, Is.EqualTo(HubRoute.MainMenu));
            Assert.That(controller.Payload, Is.SameAs(payload));
            AssertProjection(adapter, HubRoute.MainMenu, payload);

            Assert.That(controller.OpenCharacterSelect(), Is.True);
            Assert.That(controller.CurrentRoute, Is.EqualTo(HubRoute.CharacterSelect));
            AssertProjection(adapter, HubRoute.CharacterSelect, payload);

            Assert.That(controller.ContinueToHub(), Is.True);
            Assert.That(
                controller.CurrentRoute,
                Is.EqualTo(HubRoute.InventoryLoadoutHub));
            AssertProjection(adapter, HubRoute.InventoryLoadoutHub, payload);

            HubRoute[] destinations =
            {
                HubRoute.Inventory,
                HubRoute.Skills,
                HubRoute.Shop,
                HubRoute.Crafting,
                HubRoute.Play,
            };
            for (int index = 0; index < destinations.Length; index++)
            {
                Assert.That(controller.OpenDestination(destinations[index]), Is.True);
                Assert.That(controller.CurrentRoute, Is.EqualTo(destinations[index]));
                AssertProjection(adapter, destinations[index], payload);

                Assert.That(controller.ReturnToHub(), Is.True);
                Assert.That(
                    controller.CurrentRoute,
                    Is.EqualTo(HubRoute.InventoryLoadoutHub));
                AssertProjection(adapter, HubRoute.InventoryLoadoutHub, payload);
                Assert.That(controller.Payload, Is.SameAs(payload));
            }

            Assert.That(controller.NavigateBack(), Is.True);
            Assert.That(controller.CurrentRoute, Is.EqualTo(HubRoute.CharacterSelect));
            Assert.That(controller.NavigateBack(), Is.True);
            Assert.That(controller.CurrentRoute, Is.EqualTo(HubRoute.MainMenu));
            Assert.That(controller.Payload, Is.SameAs(payload));

            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainMenuButtonClearsBackHistoryWithoutReplacingProfile()
        {
            GameObject host = new GameObject("HUB-001 Main Menu Return Host");
            HubFlowController controller = host.AddComponent<HubFlowController>();
            PlayerRouteProfilePayload payload = CreatePayload();
            var adapter = new RecordingBridge();
            controller.ConfigureForTests(payload, adapter);

            controller.OpenCharacterSelect();
            controller.ContinueToHub();
            controller.OpenDestination(HubRoute.Shop);
            Assert.That(controller.GoToMainMenu(), Is.True);
            Assert.That(controller.CurrentRoute, Is.EqualTo(HubRoute.MainMenu));
            Assert.That(controller.Payload, Is.SameAs(payload));
            AssertProjection(adapter, HubRoute.MainMenu, payload);

            Assert.That(controller.NavigateBack(), Is.False);
            Assert.That(
                controller.LastNavigationResult.Status,
                Is.EqualTo(HubNavigationStatus.BackAtRoot));
            Assert.That(controller.CurrentRoute, Is.EqualTo(HubRoute.MainMenu));

            Object.Destroy(host);
            yield return null;
        }

        private static PlayerRouteProfilePayload CreatePayload()
        {
            return PlayerRouteProfilePayload.Create(
                StableId.Parse("character.playmode-pilot"),
                StableId.Parse("loadout-profile.playmode-loadout"),
                new List<StableId>
                {
                    StableId.Parse("equipment-instance.playmode-weapon-1"),
                    StableId.Parse("equipment-instance.playmode-weapon-2"),
                    StableId.Parse("equipment-instance.playmode-weapon-3"),
                    StableId.Parse("equipment-instance.playmode-weapon-4"),
                });
        }

        private static void AssertProjection(
            RecordingBridge adapter,
            HubRoute route,
            PlayerRouteProfilePayload payload)
        {
            Assert.That(adapter.LastRoute, Is.EqualTo(route));
            Assert.That(adapter.LastPayload, Is.SameAs(payload));
            Assert.That(adapter.LastPayload.Fingerprint, Is.EqualTo(payload.Fingerprint));
        }

        private sealed class RecordingBridge : IHubRouteDestinationBridge
        {
            public HubRoute LastRoute { get; private set; }

            public PlayerRouteProfilePayload LastPayload { get; private set; }

            public void Present(
                HubRoute route,
                PlayerRouteProfilePayload payload)
            {
                LastRoute = route;
                LastPayload = payload;
            }
        }
    }
}
