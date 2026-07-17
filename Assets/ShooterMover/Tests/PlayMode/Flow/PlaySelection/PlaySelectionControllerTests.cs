using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.PlaySelection;
using ShooterMover.Content.Definitions.Flow.PlayModes;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.UI.PlaySelection;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Flow.PlaySelection
{
    public sealed class PlaySelectionControllerTests
    {
        [UnityTest]
        public IEnumerator SoloEmitsOneLevelSelectionRouteWithSamePayload()
        {
            PlayerRouteProfilePayloadV1 payload = CreatePayload();
            var adapter = new RecordingPlaySelectionRouteAdapterV1();
            PlaySelectionControllerV1 controller = CreateController(
                payload,
                adapter);

            PlaySelectionResultV1 first = controller.SelectSolo();
            PlaySelectionResultV1 repeated = controller.SelectSolo();
            yield return null;

            Assert.That(first.RouteEmitted, Is.True);
            Assert.That(
                first.Route,
                Is.EqualTo(PlaySelectionRouteV1.LevelSelection));
            Assert.That(first.Payload, Is.SameAs(payload));
            Assert.That(
                repeated.Status,
                Is.EqualTo(PlaySelectionStatusV1.InputLocked));
            Assert.That(adapter.PresentCount, Is.EqualTo(1));
            Assert.That(
                adapter.LastRoute,
                Is.EqualTo(PlaySelectionRouteV1.LevelSelection));
            Assert.That(adapter.LastPayload, Is.SameAs(payload));
            Assert.That(
                adapter.LastPayload.Fingerprint,
                Is.EqualTo(payload.Fingerprint));

            Object.Destroy(controller.gameObject);
        }

        [UnityTest]
        public IEnumerator MultiplayerShowsUnavailableWithoutAnyRoute()
        {
            PlayerRouteProfilePayloadV1 payload = CreatePayload();
            var adapter = new RecordingPlaySelectionRouteAdapterV1();
            PlaySelectionControllerV1 controller = CreateController(
                payload,
                adapter);

            PlaySelectionResultV1 result = controller.SelectMultiplayer();
            yield return null;

            Assert.That(
                result.Status,
                Is.EqualTo(PlaySelectionStatusV1.ModeUnavailable));
            Assert.That(result.Route, Is.EqualTo(PlaySelectionRouteV1.None));
            Assert.That(adapter.PresentCount, Is.Zero);
            Assert.That(controller.IsInputLocked, Is.False);
            Assert.That(controller.Payload, Is.SameAs(payload));

            Object.Destroy(controller.gameObject);
        }

        [UnityTest]
        public IEnumerator UnavailableMultiplayerCanBeFollowedBySolo()
        {
            PlayerRouteProfilePayloadV1 payload = CreatePayload();
            var adapter = new RecordingPlaySelectionRouteAdapterV1();
            PlaySelectionControllerV1 controller = CreateController(
                payload,
                adapter);

            PlaySelectionResultV1 unavailable =
                controller.SelectMultiplayer();
            PlaySelectionResultV1 solo = controller.SelectSolo();
            yield return null;

            Assert.That(
                unavailable.Status,
                Is.EqualTo(PlaySelectionStatusV1.ModeUnavailable));
            Assert.That(solo.RouteEmitted, Is.True);
            Assert.That(adapter.PresentCount, Is.EqualTo(1));
            Assert.That(
                adapter.LastRoute,
                Is.EqualTo(PlaySelectionRouteV1.LevelSelection));
            Assert.That(adapter.LastPayload, Is.SameAs(payload));

            Object.Destroy(controller.gameObject);
        }

        [UnityTest]
        public IEnumerator BackEmitsOneHubRouteWithSamePayload()
        {
            PlayerRouteProfilePayloadV1 payload = CreatePayload();
            var adapter = new RecordingPlaySelectionRouteAdapterV1();
            PlaySelectionControllerV1 controller = CreateController(
                payload,
                adapter);

            PlaySelectionResultV1 first = controller.NavigateBack();
            PlaySelectionResultV1 repeated = controller.NavigateBack();
            yield return null;

            Assert.That(first.RouteEmitted, Is.True);
            Assert.That(first.Route, Is.EqualTo(PlaySelectionRouteV1.Hub));
            Assert.That(first.Payload, Is.SameAs(payload));
            Assert.That(
                repeated.Status,
                Is.EqualTo(PlaySelectionStatusV1.InputLocked));
            Assert.That(adapter.PresentCount, Is.EqualTo(1));
            Assert.That(
                adapter.LastRoute,
                Is.EqualTo(PlaySelectionRouteV1.Hub));
            Assert.That(adapter.LastPayload, Is.SameAs(payload));

            Object.Destroy(controller.gameObject);
        }

        [UnityTest]
        public IEnumerator MissingPayloadCannotEmitAnyRoute()
        {
            var adapter = new RecordingPlaySelectionRouteAdapterV1();
            PlaySelectionControllerV1 controller = CreateController(
                null,
                adapter);

            PlaySelectionResultV1 solo = controller.SelectSolo();
            PlaySelectionResultV1 back = controller.NavigateBack();
            yield return null;

            Assert.That(
                solo.Status,
                Is.EqualTo(PlaySelectionStatusV1.InvalidPayload));
            Assert.That(
                back.Status,
                Is.EqualTo(PlaySelectionStatusV1.InvalidPayload));
            Assert.That(adapter.PresentCount, Is.Zero);
            Assert.That(controller.IsInputLocked, Is.False);

            Object.Destroy(controller.gameObject);
        }

        [UnityTest]
        public IEnumerator CatalogMetadataDrivesControllerModeList()
        {
            PlayerRouteProfilePayloadV1 payload = CreatePayload();
            var adapter = new RecordingPlaySelectionRouteAdapterV1();
            PlaySelectionControllerV1 controller = CreateController(
                payload,
                adapter);
            yield return null;

            Assert.That(controller.Catalog.Modes.Count, Is.EqualTo(2));
            Assert.That(
                controller.Catalog.Modes[0].ModeStableId.ToString(),
                Is.EqualTo(PlaySelectionServiceV1.SoloModeStableIdText));
            Assert.That(
                controller.Catalog.Modes[1].ModeStableId.ToString(),
                Is.EqualTo(
                    PlaySelectionServiceV1.MultiplayerModeStableIdText));
            Assert.That(adapter.PresentCount, Is.Zero);

            Object.Destroy(controller.gameObject);
        }

        private static PlaySelectionControllerV1 CreateController(
            PlayerRouteProfilePayloadV1 payload,
            IPlaySelectionRouteAdapterV1 adapter)
        {
            var gameObject = new GameObject("PlaySelectionControllerTests");
            PlaySelectionControllerV1 controller =
                gameObject.AddComponent<PlaySelectionControllerV1>();
            controller.Configure(
                payload,
                PlayModeCatalogDefinitionV1.CreateDefaultCatalog(),
                adapter);
            return controller;
        }

        private static PlayerRouteProfilePayloadV1 CreatePayload()
        {
            return PlayerRouteProfilePayloadV1.Create(
                StableId.Parse("character.play-selection-playmode"),
                StableId.Parse("loadout-profile.play-selection-playmode"),
                new List<StableId>
                {
                    StableId.Parse("equipment-instance.playmode-1"),
                    StableId.Parse("equipment-instance.playmode-2"),
                    StableId.Parse("equipment-instance.playmode-3"),
                    StableId.Parse("equipment-instance.playmode-4"),
                });
        }
    }
}
