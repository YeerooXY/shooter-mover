using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Characters.Selection;
using ShooterMover.Content.Definitions.Characters.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Characters.Selection;
using ShooterMover.Domain.Common;
using ShooterMover.UI.CharacterSelect;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Flow.CharacterSelect
{
    public sealed class CharacterSelectControllerV1Tests
    {
        [Test]
        public void HighlightingTwoProfilesDoesNotRouteUntilConfirm()
        {
            PlayerRouteProfilePayloadV1 incoming = CreateIncomingPayload();
            var sink = new CharacterSelectionRecordingRouteSinkV1();
            CharacterSelectControllerV1 controller =
                CreateController(incoming, sink);

            try
            {
                Assert.That(controller.SelectCharacterByIndex(1), Is.True);
                Assert.That(controller.ContinueToClassChoice(), Is.True);
                Assert.That(
                    controller.SelectClass(CharacterClassKindV1.Defensive),
                    Is.True);
                Assert.That(
                    controller.SelectClass(CharacterClassKindV1.Healer),
                    Is.True);

                Assert.That(sink.AcceptCount, Is.Zero);
                Assert.That(controller.LastRouteResult, Is.Null);
                Assert.That(
                    incoming.SelectedCharacterStableId.ToString(),
                    Is.EqualTo("character.flow-incoming"));
                Assert.That(
                    incoming.LoadoutProfileStableId.ToString(),
                    Is.EqualTo("loadout-profile.flow-incoming"));

                CharacterSelectionRouteResultV1 result =
                    controller.ConfirmSelection();
                Assert.That(sink.AcceptCount, Is.EqualTo(1));
                Assert.That(sink.LastResult, Is.SameAs(result));
                Assert.That(
                    result.Payload.SelectedCharacterStableId.ToString(),
                    Is.EqualTo("character.custom-pilot"));
                Assert.That(
                    result.Payload.LoadoutProfileStableId.ToString(),
                    Is.EqualTo("loadout-profile.custom-pilot-healer"));
                AssertEquipmentRetained(incoming, result.Payload);
            }
            finally
            {
                Object.DestroyImmediate(controller.gameObject);
            }
        }

        [Test]
        public void RepeatedConfirmDispatchesOneImmutableResult()
        {
            var sink = new CharacterSelectionRecordingRouteSinkV1();
            CharacterSelectControllerV1 controller =
                CreateController(CreateIncomingPayload(), sink);

            try
            {
                controller.ContinueToClassChoice();
                CharacterSelectionRouteResultV1 first =
                    controller.ConfirmSelection();
                CharacterSelectionRouteResultV1 second =
                    controller.ConfirmSelection();

                Assert.That(second, Is.SameAs(first));
                Assert.That(second.Payload, Is.SameAs(first.Payload));
                Assert.That(sink.AcceptCount, Is.EqualTo(1));
                Assert.That(controller.TerminalResultDispatched, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(controller.gameObject);
            }
        }

        [Test]
        public void BackFromClassReturnsLocallyThenBackReturnsIncomingPayload()
        {
            PlayerRouteProfilePayloadV1 incoming = CreateIncomingPayload();
            var sink = new CharacterSelectionRecordingRouteSinkV1();
            CharacterSelectControllerV1 controller =
                CreateController(incoming, sink);

            try
            {
                controller.SelectCharacterByIndex(1);
                controller.ContinueToClassChoice();
                controller.SelectClass(CharacterClassKindV1.Healer);

                Assert.That(controller.NavigateBack(), Is.False);
                Assert.That(
                    controller.CurrentStage,
                    Is.EqualTo(CharacterSelectStageV1.CharacterChoice));
                Assert.That(sink.AcceptCount, Is.Zero);

                Assert.That(controller.NavigateBack(), Is.True);
                Assert.That(sink.AcceptCount, Is.EqualTo(1));
                Assert.That(
                    sink.LastResult.Status,
                    Is.EqualTo(CharacterSelectionRouteStatusV1.Back));
                Assert.That(sink.LastResult.Payload, Is.SameAs(incoming));
                Assert.That(
                    sink.LastResult.Payload.Fingerprint,
                    Is.EqualTo(incoming.Fingerprint));
            }
            finally
            {
                Object.DestroyImmediate(controller.gameObject);
            }
        }

        [Test]
        public void ConfirmedPayloadSurvivesControllerRecreation()
        {
            var firstSink = new CharacterSelectionRecordingRouteSinkV1();
            CharacterSelectControllerV1 first =
                CreateController(CreateIncomingPayload(), firstSink);
            CharacterSelectionRouteResultV1 confirmed;

            try
            {
                first.SelectCharacterByIndex(1);
                first.ContinueToClassChoice();
                first.SelectClass(CharacterClassKindV1.Defensive);
                confirmed = first.ConfirmSelection();
            }
            finally
            {
                Object.DestroyImmediate(first.gameObject);
            }

            var secondSink = new CharacterSelectionRecordingRouteSinkV1();
            CharacterSelectControllerV1 second =
                CreateController(confirmed.Payload, secondSink);
            try
            {
                Assert.That(
                    second.Service.HighlightedCharacterStableId,
                    Is.EqualTo(confirmed.Payload.SelectedCharacterStableId));
                Assert.That(
                    second.Service.HighlightedLoadoutProfileStableId,
                    Is.EqualTo(confirmed.Payload.LoadoutProfileStableId));
                AssertEquipmentRetained(confirmed.Payload, second.Service.IncomingPayload);
            }
            finally
            {
                Object.DestroyImmediate(second.gameObject);
            }
        }

        [Test]
        public void SuppliedArtworkResourcesAreImportableTextAssets()
        {
            AssertResource("CharacterSelect/character_choice_screen");
            AssertResource("CharacterSelect/character_creation_choice_screen");
            AssertResource("CharacterSelect/aggressive_class");
            AssertResource("CharacterSelect/defensive_class");
            AssertResource("CharacterSelect/healer_class");
        }

        private static CharacterSelectControllerV1 CreateController(
            PlayerRouteProfilePayloadV1 incoming,
            ICharacterSelectionRouteSinkV1 sink)
        {
            var gameObject = new GameObject("CharacterSelectControllerV1Tests");
            CharacterSelectControllerV1 controller =
                gameObject.AddComponent<CharacterSelectControllerV1>();
            controller.ConfigureForTests(
                incoming,
                BuiltInCharacterSelectionCatalogV1.Create(),
                sink);
            return controller;
        }

        private static PlayerRouteProfilePayloadV1 CreateIncomingPayload()
        {
            return PlayerRouteProfilePayloadV1.Create(
                StableId.Parse("character.flow-incoming"),
                StableId.Parse("loadout-profile.flow-incoming"),
                new List<StableId>
                {
                    StableId.Parse("equipment-instance.flow-character-1"),
                    StableId.Parse("equipment-instance.flow-character-2"),
                    StableId.Parse("equipment-instance.flow-character-3"),
                    StableId.Parse("equipment-instance.flow-character-4"),
                });
        }

        private static void AssertEquipmentRetained(
            PlayerRouteProfilePayloadV1 expected,
            PlayerRouteProfilePayloadV1 actual)
        {
            Assert.That(
                actual.WeaponSlots.Count,
                Is.EqualTo(expected.WeaponSlots.Count));
            for (int index = 0; index < expected.WeaponSlots.Count; index++)
            {
                Assert.That(
                    actual.WeaponSlots[index].EquipmentInstanceStableId,
                    Is.EqualTo(
                        expected.WeaponSlots[index].EquipmentInstanceStableId));
            }
        }

        private static void AssertResource(string path)
        {
            TextAsset asset = Resources.Load<TextAsset>(path);
            Assert.That(asset, Is.Not.Null, path);
            Assert.That(asset.bytes.Length, Is.GreaterThan(1000), path);
        }
    }
}
