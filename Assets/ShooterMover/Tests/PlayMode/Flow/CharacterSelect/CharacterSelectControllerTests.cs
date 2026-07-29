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
    public sealed class CharacterSelectControllerTests
    {
        [Test]
        public void HighlightingTwoProfilesDoesNotRouteUntilConfirm()
        {
            PlayerRouteProfilePayload incoming = CreateIncomingPayload();
            var sink = new CharacterSelectionRecordingRouteSink();
            CharacterSelectController controller =
                CreateController(incoming, sink);

            try
            {
                // The catalog presents characters in stable ID order; custom-pilot
                // therefore occupies index zero ahead of frontier-vanguard.
                Assert.That(controller.SelectCharacterByIndex(0), Is.True);
                Assert.That(controller.ContinueToClassChoice(), Is.True);
                Assert.That(
                    controller.SelectClass(CharacterClassKind.Defensive),
                    Is.True);
                Assert.That(
                    controller.SelectClass(CharacterClassKind.Healer),
                    Is.True);

                Assert.That(sink.AcceptCount, Is.Zero);
                Assert.That(controller.LastRouteResult, Is.Null);
                Assert.That(
                    incoming.SelectedCharacterStableId.ToString(),
                    Is.EqualTo("character.flow-incoming"));
                Assert.That(
                    incoming.LoadoutProfileStableId.ToString(),
                    Is.EqualTo("loadout-profile.flow-incoming"));

                CharacterSelectionRouteResult result =
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
            var sink = new CharacterSelectionRecordingRouteSink();
            CharacterSelectController controller =
                CreateController(CreateIncomingPayload(), sink);

            try
            {
                controller.ContinueToClassChoice();
                CharacterSelectionRouteResult first =
                    controller.ConfirmSelection();
                CharacterSelectionRouteResult second =
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
            PlayerRouteProfilePayload incoming = CreateIncomingPayload();
            var sink = new CharacterSelectionRecordingRouteSink();
            CharacterSelectController controller =
                CreateController(incoming, sink);

            try
            {
                controller.SelectCharacterByIndex(1);
                controller.ContinueToClassChoice();
                controller.SelectClass(CharacterClassKind.Healer);

                Assert.That(controller.NavigateBack(), Is.False);
                Assert.That(
                    controller.CurrentStage,
                    Is.EqualTo(CharacterSelectStage.CharacterChoice));
                Assert.That(sink.AcceptCount, Is.Zero);

                Assert.That(controller.NavigateBack(), Is.True);
                Assert.That(sink.AcceptCount, Is.EqualTo(1));
                Assert.That(
                    sink.LastResult.Status,
                    Is.EqualTo(CharacterSelectionRouteStatus.Back));
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
            var firstSink = new CharacterSelectionRecordingRouteSink();
            CharacterSelectController first =
                CreateController(CreateIncomingPayload(), firstSink);
            CharacterSelectionRouteResult confirmed;

            try
            {
                first.SelectCharacterByIndex(1);
                first.ContinueToClassChoice();
                first.SelectClass(CharacterClassKind.Defensive);
                confirmed = first.ConfirmSelection();
            }
            finally
            {
                Object.DestroyImmediate(first.gameObject);
            }

            var secondSink = new CharacterSelectionRecordingRouteSink();
            CharacterSelectController second =
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

        private static CharacterSelectController CreateController(
            PlayerRouteProfilePayload incoming,
            ICharacterSelectionRouteSink sink)
        {
            var gameObject = new GameObject("CharacterSelectControllerTests");
            CharacterSelectController controller =
                gameObject.AddComponent<CharacterSelectController>();
            controller.ConfigureForTests(
                incoming,
                BuiltInCharacterSelectionCatalog.Create(),
                sink);
            return controller;
        }

        private static PlayerRouteProfilePayload CreateIncomingPayload()
        {
            return PlayerRouteProfilePayload.Create(
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
            PlayerRouteProfilePayload expected,
            PlayerRouteProfilePayload actual)
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
