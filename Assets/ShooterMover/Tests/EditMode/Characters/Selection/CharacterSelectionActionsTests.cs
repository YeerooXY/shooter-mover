using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Characters.Selection;
using ShooterMover.Content.Definitions.Characters.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Characters.Selection;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Characters.Selection
{
    public sealed class CharacterSelectionActionsTests
    {
        [Test]
        public void BuiltInCatalogHasStableCharactersAndThreeProfilesPerCharacter()
        {
            CharacterSelectionCatalog catalog =
                BuiltInCharacterSelectionCatalog.Create();

            Assert.That(catalog.Characters.Count, Is.EqualTo(2));
            Assert.That(catalog.Profiles.Count, Is.EqualTo(6));
            for (int index = 0; index < catalog.Characters.Count; index++)
            {
                CharacterSelectionDefinition character =
                    catalog.Characters[index];
                Assert.That(
                    catalog.GetProfiles(character.CharacterStableId).Count,
                    Is.EqualTo(3));
                Assert.That(character.VisualMetadata.PortraitResourceKey, Is.Not.Empty);
                Assert.That(character.DefaultLoadoutProfileStableId, Is.Not.Null);
            }
        }

        [Test]
        public void CatalogFingerprintIsIndependentOfInputOrder()
        {
            CharacterSelectionCatalog source =
                BuiltInCharacterSelectionCatalog.Create();
            var reversedCharacters = new List<CharacterSelectionDefinition>(
                source.Characters);
            var reversedProfiles = new List<CharacterClassProfileDefinition>(
                source.Profiles);
            reversedCharacters.Reverse();
            reversedProfiles.Reverse();

            CharacterSelectionCatalogResult result =
                CharacterSelectionCatalog.TryCreate(
                    source.DefaultCharacterStableId,
                    reversedCharacters,
                    reversedProfiles);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Catalog.Fingerprint, Is.EqualTo(source.Fingerprint));
        }

        [Test]
        public void CatalogRejectsDuplicateCharacterAndProfileIdentities()
        {
            CharacterSelectionCatalog source =
                BuiltInCharacterSelectionCatalog.Create();

            var duplicateCharacters = new List<CharacterSelectionDefinition>(
                source.Characters)
            {
                source.Characters[0],
            };
            CharacterSelectionCatalogResult characterResult =
                CharacterSelectionCatalog.TryCreate(
                    source.DefaultCharacterStableId,
                    duplicateCharacters,
                    source.Profiles);
            Assert.That(
                characterResult.Status,
                Is.EqualTo(
                    CharacterSelectionCatalogStatus.DuplicateCharacterIdentity));

            var duplicateProfiles = new List<CharacterClassProfileDefinition>(
                source.Profiles)
            {
                source.Profiles[0],
            };
            CharacterSelectionCatalogResult profileResult =
                CharacterSelectionCatalog.TryCreate(
                    source.DefaultCharacterStableId,
                    source.Characters,
                    duplicateProfiles);
            Assert.That(
                profileResult.Status,
                Is.EqualTo(
                    CharacterSelectionCatalogStatus.DuplicateProfileIdentity));
        }

        [Test]
        public void CatalogRejectsMissingAndMismatchedDefaultProfileReferences()
        {
            CharacterSelectionCatalog source =
                BuiltInCharacterSelectionCatalog.Create();
            CharacterSelectionDefinition original = source.Characters[0];

            var missingDefaultCharacter = new CharacterSelectionDefinition(
                original.CharacterStableId,
                original.DisplayName,
                original.Description,
                StableId.Parse("loadout-profile.missing-default"),
                original.VisualMetadata);
            var missingCharacters = new List<CharacterSelectionDefinition>
            {
                missingDefaultCharacter,
                source.Characters[1],
            };
            CharacterSelectionCatalogResult missingResult =
                CharacterSelectionCatalog.TryCreate(
                    source.DefaultCharacterStableId,
                    missingCharacters,
                    source.Profiles);
            Assert.That(
                missingResult.Status,
                Is.EqualTo(
                    CharacterSelectionCatalogStatus.CharacterDefaultProfileMissing));

            CharacterSelectionDefinition wrongOwnerCharacter =
                new CharacterSelectionDefinition(
                    original.CharacterStableId,
                    original.DisplayName,
                    original.Description,
                    source.Characters[1].DefaultLoadoutProfileStableId,
                    original.VisualMetadata);
            var wrongOwnerCharacters = new List<CharacterSelectionDefinition>
            {
                wrongOwnerCharacter,
                source.Characters[1],
            };
            CharacterSelectionCatalogResult wrongOwnerResult =
                CharacterSelectionCatalog.TryCreate(
                    source.DefaultCharacterStableId,
                    wrongOwnerCharacters,
                    source.Profiles);
            Assert.That(
                wrongOwnerResult.Status,
                Is.EqualTo(
                    CharacterSelectionCatalogStatus
                        .CharacterDefaultProfileOwnerMismatch));
        }

        [Test]
        public void HighlightDoesNotMutateIncomingPayload()
        {
            PlayerRouteProfilePayload incoming = CreateIncomingPayload();
            string originalFingerprint = incoming.Fingerprint;
            var service = new CharacterSelectionActions(
                BuiltInCharacterSelectionCatalog.Create(),
                incoming);

            CharacterSelectionOperationResult characterResult =
                service.TryHighlightCharacter(
                    StableId.Parse("character.custom-pilot"));
            CharacterSelectionOperationResult profileResult =
                service.TryHighlightProfile(
                    StableId.Parse("loadout-profile.custom-pilot-healer"));

            Assert.That(characterResult.Changed, Is.True);
            Assert.That(profileResult.Changed, Is.True);
            Assert.That(incoming.Fingerprint, Is.EqualTo(originalFingerprint));
            Assert.That(
                incoming.SelectedCharacterStableId.ToString(),
                Is.EqualTo("character.incoming-pilot"));
            AssertEquipmentInstances(incoming, CreateEquipmentIds());
        }

        [Test]
        public void ConfirmCreatesNewHubPayloadAndPreservesLoadoutInstances()
        {
            PlayerRouteProfilePayload incoming = CreateIncomingPayload();
            var service = new CharacterSelectionActions(
                BuiltInCharacterSelectionCatalog.Create(),
                incoming);
            service.TryHighlightCharacter(
                StableId.Parse("character.custom-pilot"));
            service.TryHighlightProfile(
                StableId.Parse("loadout-profile.custom-pilot-defensive"));

            CharacterSelectionRouteResult result = service.Confirm();

            Assert.That(
                result.Status,
                Is.EqualTo(CharacterSelectionRouteStatus.Confirmed));
            Assert.That(result.TargetRoute, Is.EqualTo(HubRoute.InventoryLoadoutHub));
            Assert.That(result.Payload, Is.Not.SameAs(incoming));
            Assert.That(
                result.Payload.SelectedCharacterStableId.ToString(),
                Is.EqualTo("character.custom-pilot"));
            Assert.That(
                result.Payload.LoadoutProfileStableId.ToString(),
                Is.EqualTo("loadout-profile.custom-pilot-defensive"));
            Assert.That(result.Payload.HasValidFingerprint(), Is.True);
            AssertEquipmentInstances(result.Payload, CreateEquipmentIds());
        }

        [Test]
        public void RepeatedConfirmReturnsSameCachedResultAndPayload()
        {
            var service = new CharacterSelectionActions(
                BuiltInCharacterSelectionCatalog.Create(),
                CreateIncomingPayload());

            CharacterSelectionRouteResult first = service.Confirm();
            CharacterSelectionRouteResult second = service.Confirm();

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.Payload, Is.SameAs(first.Payload));
            Assert.That(
                service.TryHighlightCharacter(
                    StableId.Parse("character.custom-pilot")).Status,
                Is.EqualTo(CharacterSelectionOperationStatus.Rejected));
        }

        [Test]
        public void BackReturnsExactIncomingPayloadAfterArbitraryHighlights()
        {
            PlayerRouteProfilePayload incoming = CreateIncomingPayload();
            var service = new CharacterSelectionActions(
                BuiltInCharacterSelectionCatalog.Create(),
                incoming);
            service.TryHighlightCharacter(
                StableId.Parse("character.custom-pilot"));
            service.TryHighlightProfile(
                StableId.Parse("loadout-profile.custom-pilot-healer"));

            CharacterSelectionRouteResult first = service.Back();
            CharacterSelectionRouteResult second = service.Back();

            Assert.That(first.Status, Is.EqualTo(CharacterSelectionRouteStatus.Back));
            Assert.That(first.TargetRoute, Is.EqualTo(HubRoute.MainMenu));
            Assert.That(first.Payload, Is.SameAs(incoming));
            Assert.That(second, Is.SameAs(first));
            Assert.That(first.Payload.Fingerprint, Is.EqualTo(incoming.Fingerprint));
        }

        [Test]
        public void InvalidProfileSelectionRejectsWithoutChangingSnapshot()
        {
            var service = new CharacterSelectionActions(
                BuiltInCharacterSelectionCatalog.Create(),
                CreateIncomingPayload());
            CharacterSelectionSnapshot before = service.ExportSnapshot();

            CharacterSelectionOperationResult result =
                service.TryHighlightProfile(
                    StableId.Parse("loadout-profile.custom-pilot-healer"));

            Assert.That(
                result.Status,
                Is.EqualTo(CharacterSelectionOperationStatus.Rejected));
            Assert.That(
                result.Snapshot.SelectionFingerprint,
                Is.EqualTo(before.SelectionFingerprint));
        }

        [Test]
        public void ConfirmedPayloadRestoresTheSameSelectionOnReload()
        {
            var first = new CharacterSelectionActions(
                BuiltInCharacterSelectionCatalog.Create(),
                CreateIncomingPayload());
            first.TryHighlightCharacter(
                StableId.Parse("character.custom-pilot"));
            first.TryHighlightProfile(
                StableId.Parse("loadout-profile.custom-pilot-healer"));
            CharacterSelectionRouteResult confirmed = first.Confirm();

            var reloaded = new CharacterSelectionActions(
                BuiltInCharacterSelectionCatalog.Create(),
                confirmed.Payload);

            Assert.That(
                reloaded.HighlightedCharacterStableId,
                Is.EqualTo(confirmed.Payload.SelectedCharacterStableId));
            Assert.That(
                reloaded.HighlightedLoadoutProfileStableId,
                Is.EqualTo(confirmed.Payload.LoadoutProfileStableId));
            AssertEquipmentInstances(reloaded.IncomingPayload, CreateEquipmentIds());
        }

        private static PlayerRouteProfilePayload CreateIncomingPayload()
        {
            return PlayerRouteProfilePayload.Create(
                StableId.Parse("character.incoming-pilot"),
                StableId.Parse("loadout-profile.incoming"),
                CreateEquipmentIds());
        }

        private static List<StableId> CreateEquipmentIds()
        {
            return new List<StableId>
            {
                StableId.Parse("equipment-instance.character-test-1"),
                StableId.Parse("equipment-instance.character-test-2"),
                StableId.Parse("equipment-instance.character-test-3"),
                StableId.Parse("equipment-instance.character-test-4"),
            };
        }

        private static void AssertEquipmentInstances(
            PlayerRouteProfilePayload payload,
            IList<StableId> expected)
        {
            Assert.That(payload.WeaponSlots.Count, Is.EqualTo(expected.Count));
            for (int index = 0; index < expected.Count; index++)
            {
                Assert.That(
                    payload.WeaponSlots[index].EquipmentInstanceStableId,
                    Is.EqualTo(expected[index]));
            }
        }
    }
}
