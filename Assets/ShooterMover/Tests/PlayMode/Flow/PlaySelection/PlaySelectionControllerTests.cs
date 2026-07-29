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
            PlayerRouteProfilePayload payload = CreatePayload();
            var adapter = new RecordingPlaySelectionRouteBridge();
            PlaySelectionController controller = CreateController(
                payload,
                adapter);

            PlaySelectionResult first = controller.SelectSolo();
            PlaySelectionResult repeated = controller.SelectSolo();
            yield return null;

            Assert.That(first.RouteEmitted, Is.True);
            Assert.That(
                first.Route,
                Is.EqualTo(PlaySelectionRoute.LevelSelection));
            Assert.That(first.Payload, Is.SameAs(payload));
            Assert.That(
                repeated.Status,
                Is.EqualTo(PlaySelectionStatus.InputLocked));
            Assert.That(adapter.PresentCount, Is.EqualTo(1));
            Assert.That(
                adapter.LastRoute,
                Is.EqualTo(PlaySelectionRoute.LevelSelection));
            Assert.That(adapter.LastPayload, Is.SameAs(payload));
            Assert.That(
                adapter.LastPayload.Fingerprint,
                Is.EqualTo(payload.Fingerprint));

            Object.Destroy(controller.gameObject);
        }

        [UnityTest]
        public IEnumerator MultiplayerShowsUnavailableWithoutAnyRoute()
        {
            PlayerRouteProfilePayload payload = CreatePayload();
            var adapter = new RecordingPlaySelectionRouteBridge();
            PlaySelectionController controller = CreateController(
                payload,
                adapter);

            PlaySelectionResult result = controller.SelectMultiplayer();
            yield return null;

            Assert.That(
                result.Status,
                Is.EqualTo(PlaySelectionStatus.ModeUnavailable));
            Assert.That(result.Route, Is.EqualTo(PlaySelectionRoute.None));
            Assert.That(adapter.PresentCount, Is.Zero);
            Assert.That(controller.IsInputLocked, Is.False);
            Assert.That(controller.Payload, Is.SameAs(payload));

            Object.Destroy(controller.gameObject);
        }

        [UnityTest]
        public IEnumerator UnavailableMultiplayerCanBeFollowedBySolo()
        {
            PlayerRouteProfilePayload payload = CreatePayload();
            var adapter = new RecordingPlaySelectionRouteBridge();
            PlaySelectionController controller = CreateController(
                payload,
                adapter);

            PlaySelectionResult unavailable =
                controller.SelectMultiplayer();
            PlaySelectionResult solo = controller.SelectSolo();
            yield return null;

            Assert.That(
                unavailable.Status,
                Is.EqualTo(PlaySelectionStatus.ModeUnavailable));
            Assert.That(solo.RouteEmitted, Is.True);
            Assert.That(adapter.PresentCount, Is.EqualTo(1));
            Assert.That(
                adapter.LastRoute,
                Is.EqualTo(PlaySelectionRoute.LevelSelection));
            Assert.That(adapter.LastPayload, Is.SameAs(payload));

            Object.Destroy(controller.gameObject);
        }

        [UnityTest]
        public IEnumerator BackEmitsOneHubRouteWithSamePayload()
        {
            PlayerRouteProfilePayload payload = CreatePayload();
            var adapter = new RecordingPlaySelectionRouteBridge();
            PlaySelectionController controller = CreateController(
                payload,
                adapter);

            PlaySelectionResult first = controller.NavigateBack();
            PlaySelectionResult repeated = controller.NavigateBack();
            yield return null;

            Assert.That(first.RouteEmitted, Is.True);
            Assert.That(first.Route, Is.EqualTo(PlaySelectionRoute.Hub));
            Assert.That(first.Payload, Is.SameAs(payload));
            Assert.That(
                repeated.Status,
                Is.EqualTo(PlaySelectionStatus.InputLocked));
            Assert.That(adapter.PresentCount, Is.EqualTo(1));
            Assert.That(
                adapter.LastRoute,
                Is.EqualTo(PlaySelectionRoute.Hub));
            Assert.That(adapter.LastPayload, Is.SameAs(payload));

            Object.Destroy(controller.gameObject);
        }

        [UnityTest]
        public IEnumerator MissingPayloadCannotEmitAnyRoute()
        {
            var adapter = new RecordingPlaySelectionRouteBridge();
            PlaySelectionController controller = CreateController(
                null,
                adapter);

            PlaySelectionResult solo = controller.SelectSolo();
            PlaySelectionResult back = controller.NavigateBack();
            yield return null;

            Assert.That(
                solo.Status,
                Is.EqualTo(PlaySelectionStatus.InvalidPayload));
            Assert.That(
                back.Status,
                Is.EqualTo(PlaySelectionStatus.InvalidPayload));
            Assert.That(adapter.PresentCount, Is.Zero);
            Assert.That(controller.IsInputLocked, Is.False);

            Object.Destroy(controller.gameObject);
        }

        [UnityTest]
        public IEnumerator CatalogMetadataDrivesControllerModeList()
        {
            PlayerRouteProfilePayload payload = CreatePayload();
            var adapter = new RecordingPlaySelectionRouteBridge();
            PlaySelectionController controller = CreateController(
                payload,
                adapter);
            yield return null;

            Assert.That(controller.Catalog.Modes.Count, Is.EqualTo(2));
            Assert.That(
                controller.Catalog.Modes[0].ModeStableId.ToString(),
                Is.EqualTo(PlaySelectionActions.SoloModeStableIdText));
            Assert.That(
                controller.Catalog.Modes[1].ModeStableId.ToString(),
                Is.EqualTo(
                    PlaySelectionActions.MultiplayerModeStableIdText));
            Assert.That(adapter.PresentCount, Is.Zero);

            Object.Destroy(controller.gameObject);
        }

        private static PlaySelectionController CreateController(
            PlayerRouteProfilePayload payload,
            IPlaySelectionRouteBridge adapter)
        {
            var gameObject = new GameObject("PlaySelectionControllerTests");
            PlaySelectionController controller =
                gameObject.AddComponent<PlaySelectionController>();
            controller.Configure(
                payload,
                PlayModeCatalogDefinition.CreateDefaultCatalog(),
                adapter);
            return controller;
        }

        private static PlayerRouteProfilePayload CreatePayload()
        {
            return PlayerRouteProfilePayload.Create(
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
