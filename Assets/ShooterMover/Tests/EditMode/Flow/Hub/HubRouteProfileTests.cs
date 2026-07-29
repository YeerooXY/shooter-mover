using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class HubRouteProfileTests
    {
        [Test]
        public void PayloadFingerprintIsDeterministicAndCopyIsDeeplyImmutable()
        {
            var sourceInstances = CreateInstanceIds();
            PlayerRouteProfilePayload first = CreatePayload(sourceInstances);
            PlayerRouteProfilePayload second = CreatePayload(CreateInstanceIds());
            PlayerRouteProfilePayload copy = first.Copy();

            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.That(first, Is.EqualTo(second));
            Assert.That(copy, Is.EqualTo(first));
            Assert.That(copy, Is.Not.SameAs(first));
            Assert.That(copy.WeaponSlots, Is.Not.SameAs(first.WeaponSlots));
            Assert.That(copy.WeaponSlots[0], Is.Not.SameAs(first.WeaponSlots[0]));
            Assert.That(first.HasValidFingerprint(), Is.True);

            sourceInstances[0] = StableId.Parse("equipment-instance.replaced-source");
            Assert.That(
                first.WeaponSlots[0].EquipmentInstanceStableId.ToString(),
                Is.EqualTo("equipment-instance.route-weapon-1"));

            var readOnlyView = (IList<PlayerRouteWeaponSlot>)first.WeaponSlots;
            Assert.Throws<NotSupportedException>(delegate { readOnlyView.Clear(); });
        }

        [Test]
        public void ImportRejectsUnsupportedMalformedDuplicateAndTamperedDataWithoutMutation()
        {
            PlayerRouteProfilePayload payload = CreatePayload(CreateInstanceIds());
            PlayerRouteProfileEnvelope valid = payload.ToEnvelope();
            var originalSlots = new List<PlayerRouteWeaponSlotEnvelope>(valid.WeaponSlots);

            AssertStatus(
                new PlayerRouteProfileEnvelope(
                    2,
                    valid.ContractStableId,
                    valid.SelectedCharacterStableId,
                    valid.LoadoutProfileStableId,
                    valid.WeaponSlots,
                    valid.Fingerprint),
                PlayerRouteProfileValidationStatus.UnsupportedSchemaVersion);

            AssertStatus(
                new PlayerRouteProfileEnvelope(
                    valid.SchemaVersion,
                    valid.ContractStableId,
                    "NOT-CANONICAL",
                    valid.LoadoutProfileStableId,
                    valid.WeaponSlots,
                    valid.Fingerprint),
                PlayerRouteProfileValidationStatus.MalformedCharacterIdentity);

            var duplicateSlotIds = new List<PlayerRouteWeaponSlotEnvelope>(
                valid.WeaponSlots);
            duplicateSlotIds[1] = new PlayerRouteWeaponSlotEnvelope(
                valid.WeaponSlots[0].WeaponSlotStableId,
                valid.WeaponSlots[1].EquipmentInstanceStableId);
            AssertStatus(
                Rebuild(valid, duplicateSlotIds, valid.Fingerprint),
                PlayerRouteProfileValidationStatus.DuplicateWeaponSlotIdentity);

            var duplicateEquipmentIds = new List<PlayerRouteWeaponSlotEnvelope>(
                valid.WeaponSlots);
            duplicateEquipmentIds[3] = new PlayerRouteWeaponSlotEnvelope(
                valid.WeaponSlots[3].WeaponSlotStableId,
                valid.WeaponSlots[0].EquipmentInstanceStableId);
            AssertStatus(
                Rebuild(valid, duplicateEquipmentIds, valid.Fingerprint),
                PlayerRouteProfileValidationStatus.DuplicateEquipmentInstanceIdentity);

            var missingSlot = new List<PlayerRouteWeaponSlotEnvelope>(valid.WeaponSlots);
            missingSlot.RemoveAt(3);
            AssertStatus(
                Rebuild(valid, missingSlot, valid.Fingerprint),
                PlayerRouteProfileValidationStatus.WeaponSlotCountMismatch);

            AssertStatus(
                Rebuild(valid, valid.WeaponSlots, new string('0', 64)),
                PlayerRouteProfileValidationStatus.FingerprintMismatch);

            Assert.That(valid.WeaponSlots.Count, Is.EqualTo(4));
            for (int index = 0; index < originalSlots.Count; index++)
            {
                Assert.That(valid.WeaponSlots[index], Is.SameAs(originalSlots[index]));
            }
        }

        [Test]
        public void ValidEnvelopeRoundTripsToEquivalentPayload()
        {
            PlayerRouteProfilePayload payload = CreatePayload(CreateInstanceIds());
            PlayerRouteProfileValidationResult result =
                PlayerRouteProfilePayload.TryImport(payload.ToEnvelope());

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Status, Is.EqualTo(PlayerRouteProfileValidationStatus.Valid));
            Assert.That(result.Payload, Is.EqualTo(payload));
            Assert.That(result.Payload, Is.Not.SameAs(payload));
        }

        [Test]
        public void RouteHistoryRetainsOnePayloadAndRejectsInvalidTransitions()
        {
            PlayerRouteProfilePayload payload = CreatePayload(CreateInstanceIds());
            var navigation = new HubNavigationActions(payload);

            HubNavigationResult invalid =
                navigation.TryNavigateTo(HubRoute.Shop);
            Assert.That(invalid.Status, Is.EqualTo(HubNavigationStatus.InvalidTransition));
            Assert.That(invalid.Snapshot.RouteHistory, Is.Empty);
            Assert.That(navigation.CurrentRoute, Is.EqualTo(HubRoute.MainMenu));

            Assert.That(
                navigation.TryNavigateTo(HubRoute.CharacterSelect).Changed,
                Is.True);
            Assert.That(
                navigation.TryNavigateTo(HubRoute.InventoryLoadoutHub).Changed,
                Is.True);
            Assert.That(
                navigation.TryNavigateTo(HubRoute.Skills).Changed,
                Is.True);
            Assert.That(navigation.NavigateBack().Changed, Is.True);
            Assert.That(
                navigation.CurrentRoute,
                Is.EqualTo(HubRoute.InventoryLoadoutHub));
            Assert.That(navigation.NavigateBack().Changed, Is.True);
            Assert.That(navigation.CurrentRoute, Is.EqualTo(HubRoute.CharacterSelect));
            Assert.That(navigation.NavigateBack().Changed, Is.True);
            Assert.That(navigation.CurrentRoute, Is.EqualTo(HubRoute.MainMenu));

            HubNavigationResult rootBack = navigation.NavigateBack();
            Assert.That(rootBack.Status, Is.EqualTo(HubNavigationStatus.BackAtRoot));

            HubNavigationSnapshot snapshot = navigation.ExportSnapshot();
            Assert.That(snapshot.Payload, Is.SameAs(payload));
            Assert.That(snapshot.RouteHistory.Count, Is.EqualTo(6));
            for (int index = 0; index < snapshot.RouteHistory.Count; index++)
            {
                Assert.That(
                    snapshot.RouteHistory[index].PayloadFingerprint,
                    Is.EqualTo(payload.Fingerprint));
            }
        }

        private static PlayerRouteProfilePayload CreatePayload(
            IEnumerable<StableId> instances)
        {
            return PlayerRouteProfilePayload.Create(
                StableId.Parse("character.test-pilot"),
                StableId.Parse("loadout-profile.test-assault"),
                instances);
        }

        private static List<StableId> CreateInstanceIds()
        {
            return new List<StableId>
            {
                StableId.Parse("equipment-instance.route-weapon-1"),
                StableId.Parse("equipment-instance.route-weapon-2"),
                StableId.Parse("equipment-instance.route-weapon-3"),
                StableId.Parse("equipment-instance.route-weapon-4"),
            };
        }

        private static PlayerRouteProfileEnvelope Rebuild(
            PlayerRouteProfileEnvelope source,
            IEnumerable<PlayerRouteWeaponSlotEnvelope> slots,
            string fingerprint)
        {
            return new PlayerRouteProfileEnvelope(
                source.SchemaVersion,
                source.ContractStableId,
                source.SelectedCharacterStableId,
                source.LoadoutProfileStableId,
                slots,
                fingerprint);
        }

        private static void AssertStatus(
            PlayerRouteProfileEnvelope envelope,
            PlayerRouteProfileValidationStatus expected)
        {
            PlayerRouteProfileValidationResult result =
                PlayerRouteProfilePayload.TryImport(envelope);
            Assert.That(result.Status, Is.EqualTo(expected));
            Assert.That(result.Payload, Is.Null);
        }
    }
}
