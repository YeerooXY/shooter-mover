using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.LevelSelection;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Flow.LevelSelection
{
    public sealed class LevelSelectionServiceTests
    {
        [Test]
        public void DefaultCatalogHasStableExactRoutesAndPrototypeMetadata()
        {
            LevelSelectionCatalogV1 catalog = DefaultCatalog();

            Assert.That(catalog.Levels.Count, Is.EqualTo(2));
            Assert.That(catalog.Levels[0].LevelStableId.ToString(),
                Is.EqualTo(LevelSelectionCatalogDefinitionV1.Level1StableIdText));
            Assert.That(catalog.Levels[0].ScenePath,
                Is.EqualTo(LevelSelectionCatalogDefinitionV1.Stage1ScenePath));
            Assert.That(catalog.Levels[0].Availability,
                Is.EqualTo(LevelAvailabilityV1.Unlocked));
            Assert.That(catalog.Levels[0].ReleaseState,
                Is.EqualTo(LevelReleaseStateV1.Live));
            Assert.That(catalog.Levels[1].LevelStableId.ToString(),
                Is.EqualTo(LevelSelectionCatalogDefinitionV1.Level2StableIdText));
            Assert.That(catalog.Levels[1].ScenePath,
                Is.EqualTo(LevelSelectionCatalogDefinitionV1.Level2PrototypeScenePath));
            Assert.That(catalog.Levels[1].ReleaseState,
                Is.EqualTo(LevelReleaseStateV1.Prototype));
            Assert.That(catalog.Levels[1].DisplayName, Does.Contain("PROTOTYPE"));
        }

        [Test]
        public void CatalogFingerprintIsDeterministicAndMetadataSensitive()
        {
            LevelSelectionDefinitionV1 one = Level(
                "level.one", "Assets/Test/One.unity",
                LevelReleaseStateV1.Live, LevelRouteKindV1.Gameplay, 10);
            LevelSelectionDefinitionV1 two = Level(
                "level.two", "Assets/Test/Two.unity",
                LevelReleaseStateV1.Prototype, LevelRouteKindV1.Prototype, 20);
            LevelSelectionDefinitionV1 changed = Level(
                "level.two", "Assets/Test/Changed.unity",
                LevelReleaseStateV1.Prototype, LevelRouteKindV1.Prototype, 20);

            var forward = new LevelSelectionCatalogV1(new[] { one, two });
            var reverse = new LevelSelectionCatalogV1(new[] { two, one });
            var different = new LevelSelectionCatalogV1(new[] { one, changed });

            Assert.That(forward.Fingerprint, Is.EqualTo(reverse.Fingerprint));
            Assert.That(forward.Fingerprint, Does.StartWith("sha256:"));
            Assert.That(forward.Fingerprint, Is.Not.EqualTo(different.Fingerprint));
            Assert.That(reverse.Levels[0], Is.SameAs(one));
        }

        [Test]
        public void InvalidCatalogShapesAndRoutesAreRejected()
        {
            Assert.Throws<ArgumentException>(delegate
            {
                new LevelSelectionCatalogV1(new LevelSelectionDefinitionV1[0]);
            });

            LevelSelectionDefinitionV1 duplicate = Level(
                "level.duplicate", "Assets/Test/Duplicate.unity",
                LevelReleaseStateV1.Live, LevelRouteKindV1.Gameplay, 10);
            Assert.Throws<ArgumentException>(delegate
            {
                new LevelSelectionCatalogV1(new[] { duplicate, duplicate });
            });
            Assert.Throws<ArgumentException>(delegate
            {
                new LevelSelectionDefinitionV1(
                    StableId.Parse("level.missing-route"), "MISSING", "Missing.",
                    string.Empty, LevelAvailabilityV1.Unlocked,
                    LevelReleaseStateV1.Live, LevelRouteKindV1.Gameplay,
                    new LevelRecommendationV1(1, 1, 1, "TEST"), 10);
            });
            Assert.Throws<ArgumentException>(delegate
            {
                Level("level.live-prototype", "Assets/Test/Invalid.unity",
                    LevelReleaseStateV1.Live, LevelRouteKindV1.Prototype, 10);
            });
            Assert.Throws<ArgumentException>(delegate
            {
                Level("level.prototype-gameplay", "Assets/Test/Invalid.unity",
                    LevelReleaseStateV1.Prototype, LevelRouteKindV1.Gameplay, 10);
            });
        }

        [Test]
        public void Level1EmitsExactGameplayRouteWithSameModeAndPayload()
        {
            PlayerRouteProfilePayloadV1 payload = Payload();
            StableId mode = StableId.Parse("play-mode.solo");
            var service = new LevelSelectionServiceV1(payload, mode, DefaultCatalog());

            LevelSelectionResultV1 result = service.SelectLevel(Level1Id());

            Assert.That(result.Route, Is.EqualTo(LevelSelectionRouteV1.GameplayScene));
            Assert.That(result.DestinationScenePath,
                Is.EqualTo(LevelSelectionCatalogDefinitionV1.Stage1ScenePath));
            Assert.That(result.Payload, Is.SameAs(payload));
            Assert.That(result.SelectedModeStableId, Is.SameAs(mode));
            Assert.That(result.Payload.Fingerprint, Is.EqualTo(payload.Fingerprint));
        }

        [Test]
        public void Level2EmitsExactPrototypeRoute()
        {
            LevelSelectionResultV1 result = Service(Payload()).SelectLevel(Level2Id());

            Assert.That(result.Route, Is.EqualTo(LevelSelectionRouteV1.PrototypeScene));
            Assert.That(result.DestinationScenePath,
                Is.EqualTo(LevelSelectionCatalogDefinitionV1.Level2PrototypeScenePath));
        }

        [Test]
        public void LockedLevelRejectsWithoutEmittingOrLocking()
        {
            LevelSelectionDefinitionV1 locked = new LevelSelectionDefinitionV1(
                StableId.Parse("level.locked"), "LOCKED", "Locked test level.",
                "Assets/Test/Locked.unity", LevelAvailabilityV1.Locked,
                LevelReleaseStateV1.Live, LevelRouteKindV1.Gameplay,
                new LevelRecommendationV1(3, 3, 1, "LOCKED"), 10);
            var service = new LevelSelectionServiceV1(
                Payload(), StableId.Parse("play-mode.solo"),
                new LevelSelectionCatalogV1(new[] { locked }));

            LevelSelectionResultV1 result = service.SelectLevel(locked.LevelStableId);

            Assert.That(result.Status, Is.EqualTo(LevelSelectionStatusV1.LevelLocked));
            Assert.That(result.RouteEmitted, Is.False);
            Assert.That(service.IsInputLocked, Is.False);
        }

        [Test]
        public void BackReturnsToPlaySelectionWithExactContext()
        {
            PlayerRouteProfilePayloadV1 payload = Payload();
            StableId mode = StableId.Parse("play-mode.solo");
            var service = new LevelSelectionServiceV1(payload, mode, DefaultCatalog());

            LevelSelectionResultV1 result = service.NavigateBack();

            Assert.That(result.Route, Is.EqualTo(LevelSelectionRouteV1.PlaySelection));
            Assert.That(result.DestinationScenePath,
                Is.EqualTo(LevelSelectionServiceV1.PlaySelectionScenePath));
            Assert.That(result.Payload, Is.SameAs(payload));
            Assert.That(result.SelectedModeStableId, Is.SameAs(mode));
        }

        [Test]
        public void RepeatedInputAfterAcceptedRouteIsLocked()
        {
            LevelSelectionServiceV1 service = Service(Payload());

            LevelSelectionResultV1 first = service.SelectLevel(Level1Id());
            LevelSelectionResultV1 second = service.SelectLevel(Level2Id());
            LevelSelectionResultV1 third = service.NavigateBack();

            Assert.That(first.RouteEmitted, Is.True);
            Assert.That(second.Status, Is.EqualTo(LevelSelectionStatusV1.InputLocked));
            Assert.That(third.Status, Is.EqualTo(LevelSelectionStatusV1.InputLocked));
            Assert.That(service.TerminalResult, Is.SameAs(first));
        }

        [Test]
        public void MissingPayloadOrModeFailsClosedWithoutLocking()
        {
            var noPayload = new LevelSelectionServiceV1(
                null, StableId.Parse("play-mode.solo"), DefaultCatalog());
            var noMode = new LevelSelectionServiceV1(Payload(), null, DefaultCatalog());

            Assert.That(noPayload.SelectLevel(Level1Id()).Status,
                Is.EqualTo(LevelSelectionStatusV1.InvalidContext));
            Assert.That(noMode.NavigateBack().Status,
                Is.EqualTo(LevelSelectionStatusV1.InvalidContext));
            Assert.That(noPayload.IsInputLocked, Is.False);
            Assert.That(noMode.IsInputLocked, Is.False);
        }

        [Test]
        public void UnknownLevelDoesNotEmitOrLock()
        {
            LevelSelectionServiceV1 service = Service(Payload());
            LevelSelectionResultV1 result =
                service.SelectLevel(StableId.Parse("level.unknown"));

            Assert.That(result.Status, Is.EqualTo(LevelSelectionStatusV1.UnknownLevel));
            Assert.That(result.RouteEmitted, Is.False);
            Assert.That(service.IsInputLocked, Is.False);
        }

        private static LevelSelectionCatalogV1 DefaultCatalog()
        {
            return LevelSelectionCatalogDefinitionV1.CreateDefaultCatalog();
        }

        private static LevelSelectionServiceV1 Service(
            PlayerRouteProfilePayloadV1 payload)
        {
            return new LevelSelectionServiceV1(
                payload, StableId.Parse("play-mode.solo"), DefaultCatalog());
        }

        private static StableId Level1Id()
        {
            return StableId.Parse(LevelSelectionCatalogDefinitionV1.Level1StableIdText);
        }

        private static StableId Level2Id()
        {
            return StableId.Parse(LevelSelectionCatalogDefinitionV1.Level2StableIdText);
        }

        private static LevelSelectionDefinitionV1 Level(
            string id,
            string scenePath,
            LevelReleaseStateV1 releaseState,
            LevelRouteKindV1 routeKind,
            int sortOrder)
        {
            return new LevelSelectionDefinitionV1(
                StableId.Parse(id), id.ToUpperInvariant(), "Test level.", scenePath,
                LevelAvailabilityV1.Unlocked, releaseState, routeKind,
                new LevelRecommendationV1(1, 1, 1, "TEST"), sortOrder);
        }

        private static PlayerRouteProfilePayloadV1 Payload()
        {
            return PlayerRouteProfilePayloadV1.Create(
                StableId.Parse("character.level-selection-test"),
                StableId.Parse("loadout-profile.level-selection-test"),
                new List<StableId>
                {
                    StableId.Parse("equipment-instance.level-selection-1"),
                    StableId.Parse("equipment-instance.level-selection-2"),
                    StableId.Parse("equipment-instance.level-selection-3"),
                    StableId.Parse("equipment-instance.level-selection-4"),
                });
        }
    }
}
