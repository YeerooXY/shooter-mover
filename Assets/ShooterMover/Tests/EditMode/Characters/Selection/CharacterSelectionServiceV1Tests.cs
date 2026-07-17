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
    public sealed class CharacterSelectionServiceV1Tests
    {
        [Test]
        public void BuiltInCatalogHasStableCharactersAndThreeProfilesPerCharacter()
        {
            CharacterSelectionCatalogV1 catalog =
                BuiltInCharacterSelectionCatalogV1.Create();

            Assert.That(catalog.Characters.Count, Is.EqualTo(2));
            Assert.That(catalog.Profiles.Count, Is.EqualTo(6));
            for (int index = 0; index < catalog.Characters.Count; index++)
            {
                CharacterSelectionDefinitionV1 character =
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
            CharacterSelectionCatalogV1 source =
                BuiltInCharacterSelectionCatalogV1.Create();
            var reversedCharacters = new List<CharacterSelectionDefinitionV1>(
                source.Characters);
            var reversedProfiles = new List<CharacterClassProfileDefinitionV1>(
                source.Profiles);
            reversedCharacters.Reverse();
            reversedProfiles.Reverse();

            CharacterSelectionCatalogResultV1 result =
                CharacterSelectionCatalogV1.TryCreate(
                    source.DefaultCharacterStableId,
                    reversedCharacters,
                    reversedProfiles);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Catalog.Fingerprint, Is.EqualTo(source.Fingerprint));
        }

        [Test]
        public void CatalogRejectsDuplicateCharacterAndProfileIdentities()
        {
            CharacterSelectionCatalogV1 source =
                BuiltInCharacterSelectionCatalogV1.Create();

            var duplicateCharacters = new List<CharacterSelectionDefinitionV1>(
                source.Characters)
            {
                source.Characters[0],
            };
            CharacterSelectionCatalogResultV1 characterResult =
                CharacterSelectionCatalogV1.TryCreate(
                    source.DefaultCharacterStableId,
                    duplicateCharacters,
                    source.Profiles);
            Assert.That(
                characterResult.Status,
                Is.EqualTo(
                    CharacterSelectionCatalogStatusV1.DuplicateCharacterIdentity));

            var duplicateProfiles = new List<CharacterClassProfileDefinitionV1>(
                source.Profiles)
            {
                source.Profiles[0],
            };
            CharacterSelectionCatalogResultV1 profileResult =
                CharacterSelectionCatalogV1.TryCreate(
                    source.DefaultCharacterStableId,
                    source.Characters,
                    duplicateProfiles);
            Assert.That(
                profileResult.Status,
                Is.EqualTo(
                    CharacterSelectionCatalogStatusV1.DuplicateProfileIdentity));
        }

        [Test]
        public void CatalogRejectsMissingAndMismatchedDefaultProfileReferences()
        {
            CharacterSelectionCatalogV1 source =
                BuiltInCharacterSelectionCatalogV1.Create();
            CharacterSelectionDefinitionV1 original = source.Characters[0];

            var missingDefaultCharacter = new CharacterSelectionDefinitionV1(
                original.CharacterStableId,
                original.DisplayName,
                original.Description,
                StableId.Parse("loadout-profile.missing-default"),
                original.VisualMetadata);
            var missingCharacters = new List<CharacterSelectionDefinitionV1>
            {
                missingDefaultCharacter,
                source.Characters[1],
            };
            CharacterSelectionCatalogResultV1 missingResult =
                CharacterSelectionCatalogV1.TryCreate(
                    source.DefaultCharacterStableId,
                    missingCharacters,
                    source.Profiles);
            Assert.That(
                missingResult.Status,
                Is.EqualTo(
                    CharacterSelectionCatalogStatusV1.CharacterDefaultProfileMissing));

            CharacterSelectionDefinitionV1 wrongOwnerCharacter =
                new CharacterSelectionDefinitionV1(
                    original.CharacterStableId,
                    original.DisplayName,
                    original.Description,
                    source.Characters[1].DefaultLoadoutProfileStableId,
                    original.VisualMetadata);
            var wrongOwnerCharacters = new List<CharacterSelectionDefinitionV1>
            {
                wrongOwnerCharacter,
                source.Characters[1],
            };
            CharacterSelectionCatalogResultV1 wrongOwnerResult =
                CharacterSelectionCatalogV1.TryCreate(
                    source.DefaultCharacterStableId,
                    wrongOwnerCharacters,
                    source.Profiles);
            Assert.That(
                wrongOwnerResult.Status,
                Is.EqualTo(
                    CharacterSelectionCatalogStatusV1
                        .CharacterDefaultProfileOwnerMismatch));
        }

        [Test]
        public void HighlightDoesNotMutateIncomingPayload()
        {
            PlayerRouteProfilePayloadV1 incoming = CreateIncomingPayload();
            string originalFingerprint = incoming.Fingerprint;
            var service = new CharacterSelectionServiceV1(
                BuiltInCharacterSelectionCatalogV1.Create(),
                incoming);

            CharacterSelectionOperationResultV1 characterResult =
                service.TryHighlightCharacter(
                    StableId.Parse("character.custom-pilot"));
            CharacterSelectionOperationResultV1 profileResult =
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
            PlayerRouteProfilePayloadV1 incoming = CreateIncomingPayload();
            var service = new CharacterSelectionServiceV1(
                BuiltInCharacterSelectionCatalogV1.Create(),
                incoming);
            service.TryHighlightCharacter(
                StableId.Parse("character.custom-pilot"));
            service.TryHighlightProfile(
                StableId.Parse("loadout-profile.custom-pilot-defensive"));

            CharacterSelectionRouteResultV1 result = service.Confirm();

            Assert.That(
                result.Status,
                Is.EqualTo(CharacterSelectionRouteStatusV1.Confirmed));
            Assert.That(result.TargetRoute, Is.EqualTo(HubRouteV1.InventoryLoadoutHub));
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
            var service = new CharacterSelectionServiceV1(
                BuiltInCharacterSelectionCatalogV1.Create(),
                CreateIncomingPayload());

            CharacterSelectionRouteResultV1 first = service.Confirm();
            CharacterSelectionRouteResultV1 second = service.Confirm();

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.Payload, Is.SameAs(first.Payload));
            Assert.That(
                service.TryHighlightCharacter(
                    StableId.Parse("character.custom-pilot")).Status,
                Is.EqualTo(CharacterSelectionOperationStatusV1.Rejected));
        }

        [Test]
        public void BackReturnsExactIncomingPayloadAfterArbitraryHighlights()
        {
            PlayerRouteProfilePayloadV1 incoming = CreateIncomingPayload();
            var service = new CharacterSelectionServiceV1(
                BuiltInCharacterSelectionCatalogV1.Create(),
                incoming);
            service.TryHighlightCharacter(
                StableId.Parse("character.custom-pilot"));
            service.TryHighlightProfile(
                StableId.Parse("loadout-profile.custom-pilot-healer"));

            CharacterSelectionRouteResultV1 first = service.Back();
            CharacterSelectionRouteResultV1 second = service.Back();

            Assert.That(first.Status, Is.EqualTo(CharacterSelectionRouteStatusV1.Back));
            Assert.That(first.TargetRoute, Is.EqualTo(HubRouteV1.MainMenu));
            Assert.That(first.Payload, Is.SameAs(incoming));
            Assert.That(second, Is.SameAs(first));
            Assert.That(first.Payload.Fingerprint, Is.EqualTo(incoming.Fingerprint));
        }

        [Test]
        public void InvalidProfileSelectionRejectsWithoutChangingSnapshot()
        {
            var service = new CharacterSelectionServiceV1(
                BuiltInCharacterSelectionCatalogV1.Create(),
                CreateIncomingPayload());
            CharacterSelectionSnapshotV1 before = service.ExportSnapshot();

            CharacterSelectionOperationResultV1 result =
                service.TryHighlightProfile(
                    StableId.Parse("loadout-profile.custom-pilot-healer"));

            Assert.That(
                result.Status,
                Is.EqualTo(CharacterSelectionOperationStatusV1.Rejected));
            Assert.That(
                result.Snapshot.SelectionFingerprint,
                Is.EqualTo(before.SelectionFingerprint));
        }

        [Test]
        public void ConfirmedPayloadRestoresTheSameSelectionOnReload()
        {
            var first = new CharacterSelectionServiceV1(
                BuiltInCharacterSelectionCatalogV1.Create(),
                CreateIncomingPayload());
            first.TryHighlightCharacter(
                StableId.Parse("character.custom-pilot"));
            first.TryHighlightProfile(
                StableId.Parse("loadout-profile.custom-pilot-healer"));
            CharacterSelectionRouteResultV1 confirmed = first.Confirm();

            var reloaded = new CharacterSelectionServiceV1(
                BuiltInCharacterSelectionCatalogV1.Create(),
                confirmed.Payload);

            Assert.That(
                reloaded.HighlightedCharacterStableId,
                Is.EqualTo(confirmed.Payload.SelectedCharacterStableId));
            Assert.That(
                reloaded.HighlightedLoadoutProfileStableId,
                Is.EqualTo(confirmed.Payload.LoadoutProfileStableId));
            AssertEquipmentInstances(reloaded.IncomingPayload, CreateEquipmentIds());
        }

        private static PlayerRouteProfilePayloadV1 CreateIncomingPayload()
        {
            return PlayerRouteProfilePayloadV1.Create(
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
            PlayerRouteProfilePayloadV1 payload,
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
