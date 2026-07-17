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
            PlayerRouteProfilePayloadV1 first = CreatePayload(sourceInstances);
            PlayerRouteProfilePayloadV1 second = CreatePayload(CreateInstanceIds());
            PlayerRouteProfilePayloadV1 copy = first.Copy();

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

            var readOnlyView = (IList<PlayerRouteWeaponSlotV1>)first.WeaponSlots;
            Assert.Throws<NotSupportedException>(delegate { readOnlyView.Clear(); });
        }

        [Test]
        public void ImportRejectsUnsupportedMalformedDuplicateAndTamperedDataWithoutMutation()
        {
            PlayerRouteProfilePayloadV1 payload = CreatePayload(CreateInstanceIds());
            PlayerRouteProfileEnvelopeV1 valid = payload.ToEnvelope();
            var originalSlots = new List<PlayerRouteWeaponSlotEnvelopeV1>(valid.WeaponSlots);

            AssertStatus(
                new PlayerRouteProfileEnvelopeV1(
                    2,
                    valid.ContractStableId,
                    valid.SelectedCharacterStableId,
                    valid.LoadoutProfileStableId,
                    valid.WeaponSlots,
                    valid.Fingerprint),
                PlayerRouteProfileValidationStatusV1.UnsupportedSchemaVersion);

            AssertStatus(
                new PlayerRouteProfileEnvelopeV1(
                    valid.SchemaVersion,
                    valid.ContractStableId,
                    "NOT-CANONICAL",
                    valid.LoadoutProfileStableId,
                    valid.WeaponSlots,
                    valid.Fingerprint),
                PlayerRouteProfileValidationStatusV1.MalformedCharacterIdentity);

            var duplicateSlotIds = new List<PlayerRouteWeaponSlotEnvelopeV1>(
                valid.WeaponSlots);
            duplicateSlotIds[1] = new PlayerRouteWeaponSlotEnvelopeV1(
                valid.WeaponSlots[0].WeaponSlotStableId,
                valid.WeaponSlots[1].EquipmentInstanceStableId);
            AssertStatus(
                Rebuild(valid, duplicateSlotIds, valid.Fingerprint),
                PlayerRouteProfileValidationStatusV1.DuplicateWeaponSlotIdentity);

            var duplicateEquipmentIds = new List<PlayerRouteWeaponSlotEnvelopeV1>(
                valid.WeaponSlots);
            duplicateEquipmentIds[3] = new PlayerRouteWeaponSlotEnvelopeV1(
                valid.WeaponSlots[3].WeaponSlotStableId,
                valid.WeaponSlots[0].EquipmentInstanceStableId);
            AssertStatus(
                Rebuild(valid, duplicateEquipmentIds, valid.Fingerprint),
                PlayerRouteProfileValidationStatusV1.DuplicateEquipmentInstanceIdentity);

            var missingSlot = new List<PlayerRouteWeaponSlotEnvelopeV1>(valid.WeaponSlots);
            missingSlot.RemoveAt(3);
            AssertStatus(
                Rebuild(valid, missingSlot, valid.Fingerprint),
                PlayerRouteProfileValidationStatusV1.WeaponSlotCountMismatch);

            AssertStatus(
                Rebuild(valid, valid.WeaponSlots, new string('0', 64)),
                PlayerRouteProfileValidationStatusV1.FingerprintMismatch);

            Assert.That(valid.WeaponSlots.Count, Is.EqualTo(4));
            for (int index = 0; index < originalSlots.Count; index++)
            {
                Assert.That(valid.WeaponSlots[index], Is.SameAs(originalSlots[index]));
            }
        }

        [Test]
        public void ValidEnvelopeRoundTripsToEquivalentPayload()
        {
            PlayerRouteProfilePayloadV1 payload = CreatePayload(CreateInstanceIds());
            PlayerRouteProfileValidationResultV1 result =
                PlayerRouteProfilePayloadV1.TryImport(payload.ToEnvelope());

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Status, Is.EqualTo(PlayerRouteProfileValidationStatusV1.Valid));
            Assert.That(result.Payload, Is.EqualTo(payload));
            Assert.That(result.Payload, Is.Not.SameAs(payload));
        }

        [Test]
        public void RouteHistoryRetainsOnePayloadAndRejectsInvalidTransitions()
        {
            PlayerRouteProfilePayloadV1 payload = CreatePayload(CreateInstanceIds());
            var navigation = new HubNavigationServiceV1(payload);

            HubNavigationResultV1 invalid =
                navigation.TryNavigateTo(HubRouteV1.Shop);
            Assert.That(invalid.Status, Is.EqualTo(HubNavigationStatusV1.InvalidTransition));
            Assert.That(invalid.Snapshot.RouteHistory, Is.Empty);
            Assert.That(navigation.CurrentRoute, Is.EqualTo(HubRouteV1.MainMenu));

            Assert.That(
                navigation.TryNavigateTo(HubRouteV1.CharacterSelect).Changed,
                Is.True);
            Assert.That(
                navigation.TryNavigateTo(HubRouteV1.InventoryLoadoutHub).Changed,
                Is.True);
            Assert.That(
                navigation.TryNavigateTo(HubRouteV1.Skills).Changed,
                Is.True);
            Assert.That(navigation.NavigateBack().Changed, Is.True);
            Assert.That(
                navigation.CurrentRoute,
                Is.EqualTo(HubRouteV1.InventoryLoadoutHub));
            Assert.That(navigation.NavigateBack().Changed, Is.True);
            Assert.That(navigation.CurrentRoute, Is.EqualTo(HubRouteV1.CharacterSelect));
            Assert.That(navigation.NavigateBack().Changed, Is.True);
            Assert.That(navigation.CurrentRoute, Is.EqualTo(HubRouteV1.MainMenu));

            HubNavigationResultV1 rootBack = navigation.NavigateBack();
            Assert.That(rootBack.Status, Is.EqualTo(HubNavigationStatusV1.BackAtRoot));

            HubNavigationSnapshotV1 snapshot = navigation.ExportSnapshot();
            Assert.That(snapshot.Payload, Is.SameAs(payload));
            Assert.That(snapshot.RouteHistory.Count, Is.EqualTo(6));
            for (int index = 0; index < snapshot.RouteHistory.Count; index++)
            {
                Assert.That(
                    snapshot.RouteHistory[index].PayloadFingerprint,
                    Is.EqualTo(payload.Fingerprint));
            }
        }

        private static PlayerRouteProfilePayloadV1 CreatePayload(
            IEnumerable<StableId> instances)
        {
            return PlayerRouteProfilePayloadV1.Create(
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

        private static PlayerRouteProfileEnvelopeV1 Rebuild(
            PlayerRouteProfileEnvelopeV1 source,
            IEnumerable<PlayerRouteWeaponSlotEnvelopeV1> slots,
            string fingerprint)
        {
            return new PlayerRouteProfileEnvelopeV1(
                source.SchemaVersion,
                source.ContractStableId,
                source.SelectedCharacterStableId,
                source.LoadoutProfileStableId,
                slots,
                fingerprint);
        }

        private static void AssertStatus(
            PlayerRouteProfileEnvelopeV1 envelope,
            PlayerRouteProfileValidationStatusV1 expected)
        {
            PlayerRouteProfileValidationResultV1 result =
                PlayerRouteProfilePayloadV1.TryImport(envelope);
            Assert.That(result.Status, Is.EqualTo(expected));
            Assert.That(result.Payload, Is.Null);
        }
    }
}
