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
            Assert.That(copy.GunSlots, Is.Not.SameAs(first.GunSlots));
            Assert.That(copy.GunSlots[0], Is.Not.SameAs(first.GunSlots[0]));
            Assert.That(first.HasValidFingerprint(), Is.True);

            sourceInstances[0] = StableId.Parse("equipment-instance.replaced-source");
            Assert.That(
                first.GunSlots[0].EquipmentInstanceStableId.ToString(),
                Is.EqualTo("equipment-instance.route-gun-1"));

            var readOnlyView = (IList<PlayerRouteGunSlot>)first.GunSlots;
            Assert.Throws<NotSupportedException>(delegate { readOnlyView.Clear(); });
        }

        [Test]
        public void ImportRejectsUnsupportedMalformedDuplicateAndTamperedDataWithoutMutation()
        {
            PlayerRouteProfilePayload payload = CreatePayload(CreateInstanceIds());
            PlayerRouteProfileEnvelope valid = payload.ToEnvelope();
            var originalSlots = new List<PlayerRouteGunSlotEnvelope>(valid.GunSlots);

            AssertStatus(
                new PlayerRouteProfileEnvelope(
                    2,
                    valid.ContractStableId,
                    valid.SelectedCharacterStableId,
                    valid.LoadoutProfileStableId,
                    valid.GunSlots,
                    valid.Fingerprint),
                PlayerRouteProfileValidationStatus.UnsupportedSchemaVersion);

            AssertStatus(
                new PlayerRouteProfileEnvelope(
                    valid.SchemaVersion,
                    valid.ContractStableId,
                    "NOT-CANONICAL",
                    valid.LoadoutProfileStableId,
                    valid.GunSlots,
                    valid.Fingerprint),
                PlayerRouteProfileValidationStatus.MalformedCharacterIdentity);

            var duplicateSlotIds = new List<PlayerRouteGunSlotEnvelope>(
                valid.GunSlots);
            duplicateSlotIds[1] = new PlayerRouteGunSlotEnvelope(
                valid.GunSlots[0].GunSlotStableId,
                valid.GunSlots[1].EquipmentInstanceStableId);
            AssertStatus(
                Rebuild(valid, duplicateSlotIds, valid.Fingerprint),
                PlayerRouteProfileValidationStatus.DuplicateGunSlotIdentity);

            var duplicateEquipmentIds = new List<PlayerRouteGunSlotEnvelope>(
                valid.GunSlots);
            duplicateEquipmentIds[3] = new PlayerRouteGunSlotEnvelope(
                valid.GunSlots[3].GunSlotStableId,
                valid.GunSlots[0].EquipmentInstanceStableId);
            AssertStatus(
                Rebuild(valid, duplicateEquipmentIds, valid.Fingerprint),
                PlayerRouteProfileValidationStatus.DuplicateEquipmentInstanceIdentity);

            var missingSlot = new List<PlayerRouteGunSlotEnvelope>(valid.GunSlots);
            missingSlot.RemoveAt(3);
            AssertStatus(
                Rebuild(valid, missingSlot, valid.Fingerprint),
                PlayerRouteProfileValidationStatus.GunSlotCountMismatch);

            AssertStatus(
                Rebuild(valid, valid.GunSlots, new string('0', 64)),
                PlayerRouteProfileValidationStatus.FingerprintMismatch);

            Assert.That(valid.GunSlots.Count, Is.EqualTo(4));
            for (int index = 0; index < originalSlots.Count; index++)
            {
                Assert.That(valid.GunSlots[index], Is.SameAs(originalSlots[index]));
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
                StableId.Parse("equipment-instance.route-gun-1"),
                StableId.Parse("equipment-instance.route-gun-2"),
                StableId.Parse("equipment-instance.route-gun-3"),
                StableId.Parse("equipment-instance.route-gun-4"),
            };
        }

        private static PlayerRouteProfileEnvelope Rebuild(
            PlayerRouteProfileEnvelope source,
            IEnumerable<PlayerRouteGunSlotEnvelope> slots,
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
