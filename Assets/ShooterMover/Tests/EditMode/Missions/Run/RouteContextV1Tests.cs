using System;
using NUnit.Framework;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Missions.Run
{
    public sealed class RouteContextV1Tests
    {
        [SetUp]
        public void SetUp()
        {
            ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            ClearAll();
        }

        [Test]
        public void CharacterSelectionContext_PreservesExactPayload_AndConsumesOnce()
        {
            PlayerRouteProfilePayloadV1 expected = CreatePayload("character");

            CharacterSelectionEntryRouteContextV1.Capture(expected);

            Assert.That(
                CharacterSelectionEntryRouteContextV1.TryConsume(out PlayerRouteProfilePayloadV1 actual),
                Is.True);
            Assert.That(actual, Is.SameAs(expected));
            Assert.That(actual.Fingerprint, Is.EqualTo(expected.Fingerprint));
            Assert.That(
                CharacterSelectionEntryRouteContextV1.TryConsume(out PlayerRouteProfilePayloadV1 replay),
                Is.False);
            Assert.That(replay, Is.Null);
        }

        [Test]
        public void PlaySelectionContext_LatestValidCaptureWins_AndConsumesOnce()
        {
            PlayerRouteProfilePayloadV1 first = CreatePayload("play-first");
            PlayerRouteProfilePayloadV1 expected = CreatePayload("play-latest");

            PlaySelectionEntryRouteContextV1.Capture(first);
            PlaySelectionEntryRouteContextV1.Capture(expected);

            Assert.That(
                PlaySelectionEntryRouteContextV1.TryConsume(out PlayerRouteProfilePayloadV1 actual),
                Is.True);
            Assert.That(actual, Is.SameAs(expected));
            Assert.That(
                PlaySelectionEntryRouteContextV1.TryConsume(out _),
                Is.False);
        }

        [Test]
        public void HubReturnContext_PeekDoesNotConsume_ThenConsumeClearsValue()
        {
            PlayerRouteProfilePayloadV1 expected = CreatePayload("hub-return");

            Assert.That(HubReturnRouteContextV1.HasValue, Is.False);
            HubReturnRouteContextV1.Capture(expected);

            Assert.That(HubReturnRouteContextV1.HasValue, Is.True);
            Assert.That(
                HubReturnRouteContextV1.TryPeek(out PlayerRouteProfilePayloadV1 peeked),
                Is.True);
            Assert.That(peeked, Is.SameAs(expected));
            Assert.That(HubReturnRouteContextV1.HasValue, Is.True);

            Assert.That(
                HubReturnRouteContextV1.TryConsume(out PlayerRouteProfilePayloadV1 consumed),
                Is.True);
            Assert.That(consumed, Is.SameAs(expected));
            Assert.That(HubReturnRouteContextV1.HasValue, Is.False);
            Assert.That(HubReturnRouteContextV1.TryPeek(out _), Is.False);
            Assert.That(HubReturnRouteContextV1.TryConsume(out _), Is.False);
        }

        [Test]
        public void Contexts_RejectNullPayloadsWithoutMutation()
        {
            Assert.That(
                () => CharacterSelectionEntryRouteContextV1.Capture(null),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => PlaySelectionEntryRouteContextV1.Capture(null),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => HubReturnRouteContextV1.Capture(null),
                Throws.TypeOf<ArgumentException>());

            Assert.That(CharacterSelectionEntryRouteContextV1.TryConsume(out _), Is.False);
            Assert.That(PlaySelectionEntryRouteContextV1.TryConsume(out _), Is.False);
            Assert.That(HubReturnRouteContextV1.TryConsume(out _), Is.False);
        }

        private static PlayerRouteProfilePayloadV1 CreatePayload(string suffix)
        {
            return PlayerRouteProfilePayloadV1.Create(
                StableId.Create("character", suffix),
                StableId.Create("loadout", suffix),
                new[]
                {
                    StableId.Create("equipment-instance", suffix + "-slot-1"),
                    StableId.Create("equipment-instance", suffix + "-slot-2"),
                    StableId.Create("equipment-instance", suffix + "-slot-3"),
                    StableId.Create("equipment-instance", suffix + "-slot-4"),
                });
        }

        private static void ClearAll()
        {
            CharacterSelectionEntryRouteContextV1.Clear();
            PlaySelectionEntryRouteContextV1.Clear();
            HubReturnRouteContextV1.Clear();
        }
    }
}
