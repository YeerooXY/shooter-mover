using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.PlaySelection;
using ShooterMover.Content.Definitions.Flow.PlayModes;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Flow.PlaySelection
{
    public sealed class PlaySelectionServiceTests
    {
        [Test]
        public void DefaultCatalogHasStableSoloAndMultiplayerIdentities()
        {
            PlayModeCatalogV1 catalog =
                PlayModeCatalogDefinitionV1.CreateDefaultCatalog();

            Assert.That(catalog.Modes.Count, Is.EqualTo(2));
            Assert.That(
                catalog.Modes[0].ModeStableId.ToString(),
                Is.EqualTo(PlaySelectionServiceV1.SoloModeStableIdText));
            Assert.That(
                catalog.Modes[0].Availability,
                Is.EqualTo(PlayModeAvailabilityV1.Available));
            Assert.That(
                catalog.Modes[0].Destination,
                Is.EqualTo(PlayModeDestinationV1.LevelSelection));
            Assert.That(
                catalog.Modes[1].ModeStableId.ToString(),
                Is.EqualTo(
                    PlaySelectionServiceV1.MultiplayerModeStableIdText));
            Assert.That(
                catalog.Modes[1].Availability,
                Is.EqualTo(PlayModeAvailabilityV1.PrototypeUnavailable));
            Assert.That(
                catalog.Modes[1].Destination,
                Is.EqualTo(PlayModeDestinationV1.None));
        }

        [Test]
        public void CatalogOrderingIsDeterministic()
        {
            var multiplayer = new PlayModeDefinitionV1(
                StableId.Parse(
                    PlaySelectionServiceV1.MultiplayerModeStableIdText),
                "MULTIPLAYER",
                "Unavailable.",
                PlayModeAvailabilityV1.PrototypeUnavailable,
                PlayModeDestinationV1.None,
                20);
            var solo = new PlayModeDefinitionV1(
                StableId.Parse(PlaySelectionServiceV1.SoloModeStableIdText),
                "SOLO",
                "Available.",
                PlayModeAvailabilityV1.Available,
                PlayModeDestinationV1.LevelSelection,
                10);

            var catalog = new PlayModeCatalogV1(new[] { multiplayer, solo });

            Assert.That(catalog.Modes[0], Is.SameAs(solo));
            Assert.That(catalog.Modes[1], Is.SameAs(multiplayer));
        }

        [Test]
        public void SoloEmitsLevelSelectionWithExactIncomingPayload()
        {
            PlayerRouteProfilePayloadV1 payload = CreatePayload();
            var service = CreateService(payload);

            PlaySelectionResultV1 result = service.SelectMode(
                StableId.Parse(PlaySelectionServiceV1.SoloModeStableIdText));

            Assert.That(
                result.Status,
                Is.EqualTo(PlaySelectionStatusV1.RouteEmitted));
            Assert.That(
                result.Route,
                Is.EqualTo(PlaySelectionRouteV1.LevelSelection));
            Assert.That(result.Payload, Is.SameAs(payload));
            Assert.That(
                result.Payload.Fingerprint,
                Is.EqualTo(payload.Fingerprint));
            Assert.That(service.IsInputLocked, Is.True);
        }

        [Test]
        public void MultiplayerIsUnavailableAndDoesNotLockOrEmitRoute()
        {
            PlayerRouteProfilePayloadV1 payload = CreatePayload();
            var service = CreateService(payload);

            PlaySelectionResultV1 unavailable = service.SelectMode(
                StableId.Parse(
                    PlaySelectionServiceV1.MultiplayerModeStableIdText));

            Assert.That(
                unavailable.Status,
                Is.EqualTo(PlaySelectionStatusV1.ModeUnavailable));
            Assert.That(unavailable.Route, Is.EqualTo(PlaySelectionRouteV1.None));
            Assert.That(unavailable.Payload, Is.SameAs(payload));
            Assert.That(service.IsInputLocked, Is.False);

            PlaySelectionResultV1 solo = service.SelectMode(
                StableId.Parse(PlaySelectionServiceV1.SoloModeStableIdText));
            Assert.That(solo.RouteEmitted, Is.True);
            Assert.That(
                solo.Route,
                Is.EqualTo(PlaySelectionRouteV1.LevelSelection));
        }

        [Test]
        public void BackEmitsHubWithExactIncomingPayload()
        {
            PlayerRouteProfilePayloadV1 payload = CreatePayload();
            var service = CreateService(payload);

            PlaySelectionResultV1 result = service.NavigateBack();

            Assert.That(result.RouteEmitted, Is.True);
            Assert.That(result.Route, Is.EqualTo(PlaySelectionRouteV1.Hub));
            Assert.That(result.Payload, Is.SameAs(payload));
            Assert.That(
                result.Payload.Fingerprint,
                Is.EqualTo(payload.Fingerprint));
        }

        [Test]
        public void RepeatedInputAfterTerminalRouteIsLocked()
        {
            var service = CreateService(CreatePayload());

            PlaySelectionResultV1 first = service.SelectMode(
                StableId.Parse(PlaySelectionServiceV1.SoloModeStableIdText));
            PlaySelectionResultV1 second = service.SelectMode(
                StableId.Parse(PlaySelectionServiceV1.SoloModeStableIdText));
            PlaySelectionResultV1 third = service.NavigateBack();

            Assert.That(first.RouteEmitted, Is.True);
            Assert.That(
                second.Status,
                Is.EqualTo(PlaySelectionStatusV1.InputLocked));
            Assert.That(second.Route, Is.EqualTo(PlaySelectionRouteV1.None));
            Assert.That(
                third.Status,
                Is.EqualTo(PlaySelectionStatusV1.InputLocked));
            Assert.That(third.Route, Is.EqualTo(PlaySelectionRouteV1.None));
            Assert.That(service.TerminalResult, Is.SameAs(first));
        }

        [Test]
        public void MissingPayloadRejectsEveryActionWithoutLocking()
        {
            var service = CreateService(null);

            PlaySelectionResultV1 solo = service.SelectMode(
                StableId.Parse(PlaySelectionServiceV1.SoloModeStableIdText));
            PlaySelectionResultV1 back = service.NavigateBack();

            Assert.That(
                solo.Status,
                Is.EqualTo(PlaySelectionStatusV1.InvalidPayload));
            Assert.That(solo.Route, Is.EqualTo(PlaySelectionRouteV1.None));
            Assert.That(
                back.Status,
                Is.EqualTo(PlaySelectionStatusV1.InvalidPayload));
            Assert.That(service.IsInputLocked, Is.False);
        }

        [Test]
        public void RejectedImportedPayloadCannotStartSelection()
        {
            PlayerRouteProfilePayloadV1 valid = CreatePayload();
            PlayerRouteProfileEnvelopeV1 envelope = valid.ToEnvelope();
            var tampered = new PlayerRouteProfileEnvelopeV1(
                envelope.SchemaVersion,
                envelope.ContractStableId,
                envelope.SelectedCharacterStableId,
                envelope.LoadoutProfileStableId,
                envelope.WeaponSlots,
                "tampered");
            PlayerRouteProfileValidationResultV1 importResult =
                PlayerRouteProfilePayloadV1.TryImport(tampered);
            Assert.That(importResult.IsValid, Is.False);

            var service = CreateService(importResult.Payload);
            PlaySelectionResultV1 result = service.SelectMode(
                StableId.Parse(PlaySelectionServiceV1.SoloModeStableIdText));

            Assert.That(
                result.Status,
                Is.EqualTo(PlaySelectionStatusV1.InvalidPayload));
            Assert.That(result.Route, Is.EqualTo(PlaySelectionRouteV1.None));
        }

        [Test]
        public void UnknownModeDoesNotEmitOrLock()
        {
            var service = CreateService(CreatePayload());

            PlaySelectionResultV1 result = service.SelectMode(
                StableId.Parse("play-mode.unknown"));

            Assert.That(
                result.Status,
                Is.EqualTo(PlaySelectionStatusV1.UnknownMode));
            Assert.That(result.Route, Is.EqualTo(PlaySelectionRouteV1.None));
            Assert.That(service.IsInputLocked, Is.False);
        }

        [Test]
        public void InvalidCatalogShapesFailClosed()
        {
            Assert.Throws<ArgumentException>(
                delegate
                {
                    new PlayModeCatalogV1(new PlayModeDefinitionV1[0]);
                });

            PlayModeDefinitionV1 solo = new PlayModeDefinitionV1(
                StableId.Parse(PlaySelectionServiceV1.SoloModeStableIdText),
                "SOLO",
                "Available.",
                PlayModeAvailabilityV1.Available,
                PlayModeDestinationV1.LevelSelection,
                10);
            Assert.Throws<ArgumentException>(
                delegate
                {
                    new PlayModeCatalogV1(new[] { solo, solo });
                });
        }

        private static PlaySelectionServiceV1 CreateService(
            PlayerRouteProfilePayloadV1 payload)
        {
            return new PlaySelectionServiceV1(
                payload,
                PlayModeCatalogDefinitionV1.CreateDefaultCatalog());
        }

        private static PlayerRouteProfilePayloadV1 CreatePayload()
        {
            return PlayerRouteProfilePayloadV1.Create(
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
