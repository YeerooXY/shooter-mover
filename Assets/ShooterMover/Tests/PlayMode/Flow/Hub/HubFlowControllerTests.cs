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
            HubFlowControllerV1 controller = host.AddComponent<HubFlowControllerV1>();
            PlayerRouteProfilePayloadV1 payload = CreatePayload();
            var adapter = new RecordingAdapter();
            controller.ConfigureForTests(payload, adapter);
            yield return null;

            Assert.That(controller.CurrentRoute, Is.EqualTo(HubRouteV1.MainMenu));
            Assert.That(controller.Payload, Is.SameAs(payload));
            AssertProjection(adapter, HubRouteV1.MainMenu, payload);

            Assert.That(controller.OpenCharacterSelect(), Is.True);
            Assert.That(controller.CurrentRoute, Is.EqualTo(HubRouteV1.CharacterSelect));
            AssertProjection(adapter, HubRouteV1.CharacterSelect, payload);

            Assert.That(controller.ContinueToHub(), Is.True);
            Assert.That(
                controller.CurrentRoute,
                Is.EqualTo(HubRouteV1.InventoryLoadoutHub));
            AssertProjection(adapter, HubRouteV1.InventoryLoadoutHub, payload);

            HubRouteV1[] destinations =
            {
                HubRouteV1.Inventory,
                HubRouteV1.Skills,
                HubRouteV1.Shop,
                HubRouteV1.Crafting,
                HubRouteV1.Play,
            };
            for (int index = 0; index < destinations.Length; index++)
            {
                Assert.That(controller.OpenDestination(destinations[index]), Is.True);
                Assert.That(controller.CurrentRoute, Is.EqualTo(destinations[index]));
                AssertProjection(adapter, destinations[index], payload);

                Assert.That(controller.ReturnToHub(), Is.True);
                Assert.That(
                    controller.CurrentRoute,
                    Is.EqualTo(HubRouteV1.InventoryLoadoutHub));
                AssertProjection(adapter, HubRouteV1.InventoryLoadoutHub, payload);
                Assert.That(controller.Payload, Is.SameAs(payload));
            }

            Assert.That(controller.NavigateBack(), Is.True);
            Assert.That(controller.CurrentRoute, Is.EqualTo(HubRouteV1.CharacterSelect));
            Assert.That(controller.NavigateBack(), Is.True);
            Assert.That(controller.CurrentRoute, Is.EqualTo(HubRouteV1.MainMenu));
            Assert.That(controller.Payload, Is.SameAs(payload));

            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainMenuButtonClearsBackHistoryWithoutReplacingProfile()
        {
            GameObject host = new GameObject("HUB-001 Main Menu Return Host");
            HubFlowControllerV1 controller = host.AddComponent<HubFlowControllerV1>();
            PlayerRouteProfilePayloadV1 payload = CreatePayload();
            var adapter = new RecordingAdapter();
            controller.ConfigureForTests(payload, adapter);

            controller.OpenCharacterSelect();
            controller.ContinueToHub();
            controller.OpenDestination(HubRouteV1.Shop);
            Assert.That(controller.GoToMainMenu(), Is.True);
            Assert.That(controller.CurrentRoute, Is.EqualTo(HubRouteV1.MainMenu));
            Assert.That(controller.Payload, Is.SameAs(payload));
            AssertProjection(adapter, HubRouteV1.MainMenu, payload);

            Assert.That(controller.NavigateBack(), Is.False);
            Assert.That(
                controller.LastNavigationResult.Status,
                Is.EqualTo(HubNavigationStatusV1.BackAtRoot));
            Assert.That(controller.CurrentRoute, Is.EqualTo(HubRouteV1.MainMenu));

            Object.Destroy(host);
            yield return null;
        }

        private static PlayerRouteProfilePayloadV1 CreatePayload()
        {
            return PlayerRouteProfilePayloadV1.Create(
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
            RecordingAdapter adapter,
            HubRouteV1 route,
            PlayerRouteProfilePayloadV1 payload)
        {
            Assert.That(adapter.LastRoute, Is.EqualTo(route));
            Assert.That(adapter.LastPayload, Is.SameAs(payload));
            Assert.That(adapter.LastPayload.Fingerprint, Is.EqualTo(payload.Fingerprint));
        }

        private sealed class RecordingAdapter : IHubRouteDestinationAdapterV1
        {
            public HubRouteV1 LastRoute { get; private set; }

            public PlayerRouteProfilePayloadV1 LastPayload { get; private set; }

            public void Present(
                HubRouteV1 route,
                PlayerRouteProfilePayloadV1 payload)
            {
                LastRoute = route;
                LastPayload = payload;
            }
        }
    }
}
