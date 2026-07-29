using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.PlaySelection;
using ShooterMover.Content.Definitions.Flow.PlayModes;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Flow.PlaySelection
{
    public sealed class PlaySelectionActionsTests
    {
        [Test]
        public void DefaultCatalogHasStableSoloAndMultiplayerIdentities()
        {
            PlayModeCatalog catalog =
                PlayModeCatalogDefinition.CreateDefaultCatalog();

            Assert.That(catalog.Modes.Count, Is.EqualTo(2));
            Assert.That(
                catalog.Modes[0].ModeStableId.ToString(),
                Is.EqualTo(PlaySelectionActions.SoloModeStableIdText));
            Assert.That(
                catalog.Modes[0].Availability,
                Is.EqualTo(PlayModeAvailability.Available));
            Assert.That(
                catalog.Modes[0].Destination,
                Is.EqualTo(PlayModeDestination.LevelSelection));
            Assert.That(
                catalog.Modes[1].ModeStableId.ToString(),
                Is.EqualTo(
                    PlaySelectionActions.MultiplayerModeStableIdText));
            Assert.That(
                catalog.Modes[1].Availability,
                Is.EqualTo(PlayModeAvailability.PrototypeUnavailable));
            Assert.That(
                catalog.Modes[1].Destination,
                Is.EqualTo(PlayModeDestination.None));
        }

        [Test]
        public void CatalogOrderingIsDeterministic()
        {
            var multiplayer = new PlayModeDefinition(
                StableId.Parse(
                    PlaySelectionActions.MultiplayerModeStableIdText),
                "MULTIPLAYER",
                "Unavailable.",
                PlayModeAvailability.PrototypeUnavailable,
                PlayModeDestination.None,
                20);
            var solo = new PlayModeDefinition(
                StableId.Parse(PlaySelectionActions.SoloModeStableIdText),
                "SOLO",
                "Available.",
                PlayModeAvailability.Available,
                PlayModeDestination.LevelSelection,
                10);

            var catalog = new PlayModeCatalog(new[] { multiplayer, solo });

            Assert.That(catalog.Modes[0], Is.SameAs(solo));
            Assert.That(catalog.Modes[1], Is.SameAs(multiplayer));
        }

        [Test]
        public void SoloEmitsLevelSelectionWithExactIncomingPayload()
        {
            PlayerRouteProfilePayload payload = CreatePayload();
            var service = CreateService(payload);

            PlaySelectionResult result = service.SelectMode(
                StableId.Parse(PlaySelectionActions.SoloModeStableIdText));

            Assert.That(
                result.Status,
                Is.EqualTo(PlaySelectionStatus.RouteEmitted));
            Assert.That(
                result.Route,
                Is.EqualTo(PlaySelectionRoute.LevelSelection));
            Assert.That(result.Payload, Is.SameAs(payload));
            Assert.That(
                result.Payload.Fingerprint,
                Is.EqualTo(payload.Fingerprint));
            Assert.That(service.IsInputLocked, Is.True);
        }

        [Test]
        public void MultiplayerIsUnavailableAndDoesNotLockOrEmitRoute()
        {
            PlayerRouteProfilePayload payload = CreatePayload();
            var service = CreateService(payload);

            PlaySelectionResult unavailable = service.SelectMode(
                StableId.Parse(
                    PlaySelectionActions.MultiplayerModeStableIdText));

            Assert.That(
                unavailable.Status,
                Is.EqualTo(PlaySelectionStatus.ModeUnavailable));
            Assert.That(unavailable.Route, Is.EqualTo(PlaySelectionRoute.None));
            Assert.That(unavailable.Payload, Is.SameAs(payload));
            Assert.That(service.IsInputLocked, Is.False);

            PlaySelectionResult solo = service.SelectMode(
                StableId.Parse(PlaySelectionActions.SoloModeStableIdText));
            Assert.That(solo.RouteEmitted, Is.True);
            Assert.That(
                solo.Route,
                Is.EqualTo(PlaySelectionRoute.LevelSelection));
        }

        [Test]
        public void BackEmitsHubWithExactIncomingPayload()
        {
            PlayerRouteProfilePayload payload = CreatePayload();
            var service = CreateService(payload);

            PlaySelectionResult result = service.NavigateBack();

            Assert.That(result.RouteEmitted, Is.True);
            Assert.That(result.Route, Is.EqualTo(PlaySelectionRoute.Hub));
            Assert.That(result.Payload, Is.SameAs(payload));
            Assert.That(
                result.Payload.Fingerprint,
                Is.EqualTo(payload.Fingerprint));
        }

        [Test]
        public void RepeatedInputAfterTerminalRouteIsLocked()
        {
            var service = CreateService(CreatePayload());

            PlaySelectionResult first = service.SelectMode(
                StableId.Parse(PlaySelectionActions.SoloModeStableIdText));
            PlaySelectionResult second = service.SelectMode(
                StableId.Parse(PlaySelectionActions.SoloModeStableIdText));
            PlaySelectionResult third = service.NavigateBack();

            Assert.That(first.RouteEmitted, Is.True);
            Assert.That(
                second.Status,
                Is.EqualTo(PlaySelectionStatus.InputLocked));
            Assert.That(second.Route, Is.EqualTo(PlaySelectionRoute.None));
            Assert.That(
                third.Status,
                Is.EqualTo(PlaySelectionStatus.InputLocked));
            Assert.That(third.Route, Is.EqualTo(PlaySelectionRoute.None));
            Assert.That(service.TerminalResult, Is.SameAs(first));
        }

        [Test]
        public void MissingPayloadRejectsEveryActionWithoutLocking()
        {
            var service = CreateService(null);

            PlaySelectionResult solo = service.SelectMode(
                StableId.Parse(PlaySelectionActions.SoloModeStableIdText));
            PlaySelectionResult back = service.NavigateBack();

            Assert.That(
                solo.Status,
                Is.EqualTo(PlaySelectionStatus.InvalidPayload));
            Assert.That(solo.Route, Is.EqualTo(PlaySelectionRoute.None));
            Assert.That(
                back.Status,
                Is.EqualTo(PlaySelectionStatus.InvalidPayload));
            Assert.That(service.IsInputLocked, Is.False);
        }

        [Test]
        public void RejectedImportedPayloadCannotStartSelection()
        {
            PlayerRouteProfilePayload valid = CreatePayload();
            PlayerRouteProfileEnvelope envelope = valid.ToEnvelope();
            var tampered = new PlayerRouteProfileEnvelope(
                envelope.SchemaVersion,
                envelope.ContractStableId,
                envelope.SelectedCharacterStableId,
                envelope.LoadoutProfileStableId,
                envelope.GunSlots,
                "tampered");
            PlayerRouteProfileValidationResult importResult =
                PlayerRouteProfilePayload.TryImport(tampered);
            Assert.That(importResult.IsValid, Is.False);

            var service = CreateService(importResult.Payload);
            PlaySelectionResult result = service.SelectMode(
                StableId.Parse(PlaySelectionActions.SoloModeStableIdText));

            Assert.That(
                result.Status,
                Is.EqualTo(PlaySelectionStatus.InvalidPayload));
            Assert.That(result.Route, Is.EqualTo(PlaySelectionRoute.None));
        }

        [Test]
        public void UnknownModeDoesNotEmitOrLock()
        {
            var service = CreateService(CreatePayload());

            PlaySelectionResult result = service.SelectMode(
                StableId.Parse("play-mode.unknown"));

            Assert.That(
                result.Status,
                Is.EqualTo(PlaySelectionStatus.UnknownMode));
            Assert.That(result.Route, Is.EqualTo(PlaySelectionRoute.None));
            Assert.That(service.IsInputLocked, Is.False);
        }

        [Test]
        public void InvalidCatalogShapesFailClosed()
        {
            Assert.Throws<ArgumentException>(
                delegate
                {
                    new PlayModeCatalog(new PlayModeDefinition[0]);
                });

            PlayModeDefinition solo = new PlayModeDefinition(
                StableId.Parse(PlaySelectionActions.SoloModeStableIdText),
                "SOLO",
                "Available.",
                PlayModeAvailability.Available,
                PlayModeDestination.LevelSelection,
                10);
            Assert.Throws<ArgumentException>(
                delegate
                {
                    new PlayModeCatalog(new[] { solo, solo });
                });
        }

        private static PlaySelectionActions CreateService(
            PlayerRouteProfilePayload payload)
        {
            return new PlaySelectionActions(
                payload,
                PlayModeCatalogDefinition.CreateDefaultCatalog());
        }

        private static PlayerRouteProfilePayload CreatePayload()
        {
            return PlayerRouteProfilePayload.Create(
                StableId.Parse("character.play-selection-test"),
                StableId.Parse("loadout-profile.play-selection-test"),
                new List<StableId>
                {
                    StableId.Parse("equipment-instance.play-selection-1"),
                    StableId.Parse("equipment-instance.play-selection-2"),
                    StableId.Parse("equipment-instance.play-selection-3"),
                    StableId.Parse("equipment-instance.play-selection-4"),
                });
        }
    }
}
