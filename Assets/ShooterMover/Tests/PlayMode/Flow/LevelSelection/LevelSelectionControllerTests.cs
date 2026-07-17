using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.LevelSelection;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.UI.LevelSelection;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Flow.LevelSelection
{
    public sealed class LevelSelectionControllerTests
    {
        private sealed class RecordingSceneLoader : ILevelSelectionSceneLoaderV1
        {
            public int LoadCount { get; private set; }
            public string LastScenePath { get; private set; }

            public void Load(string scenePath)
            {
                LastScenePath = scenePath;
                LoadCount++;
            }
        }

        [SetUp]
        public void SetUp()
        {
            LevelSelectionRouteContextV1.ClearForTests();
        }

        [TearDown]
        public void TearDown()
        {
            LevelSelectionRouteContextV1.ClearForTests();
        }

        [Test]
        public void EntryContextInitializesControllerWithoutFallbackTruth()
        {
            PlayerRouteProfilePayloadV1 payload = Payload();
            StableId mode = StableId.Parse("play-mode.solo");
            LevelSelectionRouteContextV1.CaptureEntry(payload, mode);
            var host = new GameObject("Level selection entry host");

            try
            {
                LevelSelectionControllerV1 controller =
                    host.AddComponent<LevelSelectionControllerV1>();
                Assert.That(controller.Payload, Is.SameAs(payload));
                Assert.That(controller.SelectedModeStableId, Is.SameAs(mode));
                Assert.That(controller.Catalog.Levels.Count, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ControllerUsesMetadataScenePathAndExactContext()
        {
            PlayerRouteProfilePayloadV1 payload = Payload();
            StableId mode = StableId.Parse("play-mode.solo");
            StableId id = StableId.Parse("level.custom");
            const string scenePath = "Assets/Custom/Custom.unity";
            var definition = new LevelSelectionDefinitionV1(
                id, "CUSTOM", "Metadata route.", scenePath,
                LevelAvailabilityV1.Unlocked, LevelReleaseStateV1.Live,
                LevelRouteKindV1.Gameplay,
                new LevelRecommendationV1(1, 1, 1, "TEST"), 10);
            var adapter = new RecordingLevelSelectionRouteAdapterV1();
            var host = new GameObject("Level selection metadata host");

            try
            {
                LevelSelectionControllerV1 controller =
                    host.AddComponent<LevelSelectionControllerV1>();
                controller.Configure(payload, mode,
                    new LevelSelectionCatalogV1(new[] { definition }), adapter);

                LevelSelectionResultV1 result = controller.SelectLevel(id);

                Assert.That(result.RouteEmitted, Is.True);
                Assert.That(adapter.LastResult.DestinationScenePath,
                    Is.EqualTo(scenePath));
                Assert.That(adapter.LastResult.Payload, Is.SameAs(payload));
                Assert.That(adapter.LastResult.SelectedModeStableId, Is.SameAs(mode));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RepeatedControllerInputPresentsOnlyOneRoute()
        {
            var adapter = new RecordingLevelSelectionRouteAdapterV1();
            var host = new GameObject("Level selection one-shot host");

            try
            {
                LevelSelectionControllerV1 controller =
                    host.AddComponent<LevelSelectionControllerV1>();
                controller.Configure(Payload(), StableId.Parse("play-mode.solo"),
                    LevelSelectionCatalogDefinitionV1.CreateDefaultCatalog(), adapter);

                LevelSelectionResultV1 first = controller.SelectLevel1();
                LevelSelectionResultV1 second = controller.SelectLevel2();
                LevelSelectionResultV1 third = controller.NavigateBack();

                Assert.That(first.RouteEmitted, Is.True);
                Assert.That(second.Status, Is.EqualTo(LevelSelectionStatusV1.InputLocked));
                Assert.That(third.Status, Is.EqualTo(LevelSelectionStatusV1.InputLocked));
                Assert.That(adapter.PresentCount, Is.EqualTo(1));
                Assert.That(adapter.LastResult.DestinationScenePath,
                    Is.EqualTo(LevelSelectionCatalogDefinitionV1.Stage1ScenePath));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void BackPresentsPlaySelectionWithSamePayloadAndMode()
        {
            PlayerRouteProfilePayloadV1 payload = Payload();
            StableId mode = StableId.Parse("play-mode.solo");
            var adapter = new RecordingLevelSelectionRouteAdapterV1();
            var host = new GameObject("Level selection back host");

            try
            {
                LevelSelectionControllerV1 controller =
                    host.AddComponent<LevelSelectionControllerV1>();
                controller.Configure(payload, mode,
                    LevelSelectionCatalogDefinitionV1.CreateDefaultCatalog(), adapter);

                controller.NavigateBack();

                Assert.That(adapter.LastResult.Route,
                    Is.EqualTo(LevelSelectionRouteV1.PlaySelection));
                Assert.That(adapter.LastResult.Payload, Is.SameAs(payload));
                Assert.That(adapter.LastResult.SelectedModeStableId, Is.SameAs(mode));
                Assert.That(adapter.PresentCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void UnityAdapterCapturesContextBeforeLoading()
        {
            PlayerRouteProfilePayloadV1 payload = Payload();
            StableId mode = StableId.Parse("play-mode.solo");
            var service = new LevelSelectionServiceV1(payload, mode,
                LevelSelectionCatalogDefinitionV1.CreateDefaultCatalog());
            LevelSelectionResultV1 result = service.SelectLevel(Level2Id());
            var loader = new RecordingSceneLoader();

            new UnityLevelSelectionRouteAdapterV1(loader).Present(result);

            PlayerRouteProfilePayloadV1 capturedPayload;
            StableId capturedMode;
            StableId capturedLevel;
            Assert.That(LevelSelectionRouteContextV1.TryRead(
                out capturedPayload, out capturedMode, out capturedLevel), Is.True);
            Assert.That(capturedPayload, Is.SameAs(payload));
            Assert.That(capturedMode, Is.SameAs(mode));
            Assert.That(capturedLevel.ToString(),
                Is.EqualTo(LevelSelectionCatalogDefinitionV1.Level2StableIdText));
            Assert.That(loader.LoadCount, Is.EqualTo(1));
            Assert.That(loader.LastScenePath,
                Is.EqualTo(LevelSelectionCatalogDefinitionV1.Level2PrototypeScenePath));
        }

        [Test]
        public void Level2PrototypeBackLoadsOnceAndKeepsContext()
        {
            PlayerRouteProfilePayloadV1 payload = Payload();
            StableId mode = StableId.Parse("play-mode.solo");
            var service = new LevelSelectionServiceV1(payload, mode,
                LevelSelectionCatalogDefinitionV1.CreateDefaultCatalog());
            LevelSelectionRouteContextV1.Capture(service.SelectLevel(Level2Id()));
            var loader = new RecordingSceneLoader();
            var host = new GameObject("Level 2 prototype host");

            try
            {
                Level2PrototypeControllerV1 controller =
                    host.AddComponent<Level2PrototypeControllerV1>();
                controller.ConfigureForTests(loader);

                Assert.That(controller.BackToLevelSelection(), Is.True);
                Assert.That(controller.BackToLevelSelection(), Is.False);
                Assert.That(loader.LoadCount, Is.EqualTo(1));
                Assert.That(loader.LastScenePath,
                    Is.EqualTo(Level2PrototypeControllerV1.LevelSelectionScenePath));

                PlayerRouteProfilePayloadV1 capturedPayload;
                StableId capturedMode;
                StableId capturedLevel;
                Assert.That(LevelSelectionRouteContextV1.TryRead(
                    out capturedPayload, out capturedMode, out capturedLevel), Is.True);
                Assert.That(capturedPayload, Is.SameAs(payload));
                Assert.That(capturedMode, Is.SameAs(mode));
                Assert.That(capturedLevel.ToString(),
                    Is.EqualTo(LevelSelectionCatalogDefinitionV1.Level2StableIdText));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MissingContextDoesNotPresentOrLoad()
        {
            var adapter = new RecordingLevelSelectionRouteAdapterV1();
            var host = new GameObject("Level selection missing context host");

            try
            {
                LevelSelectionControllerV1 controller =
                    host.AddComponent<LevelSelectionControllerV1>();
                controller.Configure(null, null,
                    LevelSelectionCatalogDefinitionV1.CreateDefaultCatalog(), adapter);

                LevelSelectionResultV1 result = controller.SelectLevel1();

                Assert.That(result.Status,
                    Is.EqualTo(LevelSelectionStatusV1.InvalidContext));
                Assert.That(adapter.PresentCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static StableId Level2Id()
        {
            return StableId.Parse(LevelSelectionCatalogDefinitionV1.Level2StableIdText);
        }

        private static PlayerRouteProfilePayloadV1 Payload()
        {
            return PlayerRouteProfilePayloadV1.Create(
                StableId.Parse("character.level-selection-playmode"),
                StableId.Parse("loadout-profile.level-selection-playmode"),
                new List<StableId>
                {
                    StableId.Parse("equipment-instance.level-selection-pm-1"),
                    StableId.Parse("equipment-instance.level-selection-pm-2"),
                    StableId.Parse("equipment-instance.level-selection-pm-3"),
                    StableId.Parse("equipment-instance.level-selection-pm-4"),
                });
        }
    }
}
