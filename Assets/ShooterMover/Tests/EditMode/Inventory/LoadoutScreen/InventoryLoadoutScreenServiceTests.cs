using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;

namespace ShooterMover.Tests.EditMode.Inventory.LoadoutScreen
{
    public sealed class InventoryLoadoutScreenServiceTests
    {
        [Test]
        public void DuplicateDefinitionsRemainSeparateConcreteInstances()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenSnapshotV1 snapshot = fixture.CreateService().Snapshot;
            InventoryLoadoutEquipmentProjectionV1 first = snapshot.FindEquipment(fixture.WeaponOne.InstanceId);
            InventoryLoadoutEquipmentProjectionV1 second = snapshot.FindEquipment(fixture.WeaponTwo.InstanceId);

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(first.InstanceStableId, Is.Not.EqualTo(second.InstanceStableId));
            Assert.That(first.DefinitionStableId, Is.EqualTo(second.DefinitionStableId));
        }

        [Test]
        public void ConfirmAppliesAllSlotsAndPreservesExactWeaponOrder()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenServiceV1 service = fixture.CreateService();
            string holdingsBefore = fixture.Holdings.ExportSnapshot().Fingerprint;

            Assert.That(service.TrySelect(InventoryLoadoutSlotIdsV1.WeaponTwo, fixture.WeaponTwo.InstanceId).ChangedSelection, Is.True);
            Assert.That(service.TrySelect(InventoryLoadoutSlotIdsV1.ArmorHead, fixture.ArmorOne.InstanceId).ChangedSelection, Is.True);
            Assert.That(service.TrySelect(InventoryLoadoutSlotIdsV1.ArmorBody, fixture.ArmorTwo.InstanceId).ChangedSelection, Is.True);
            Assert.That(service.TrySelect(InventoryLoadoutSlotIdsV1.ArmorLegs, fixture.ArmorThree.InstanceId).ChangedSelection, Is.True);
            Assert.That(service.TrySelect(InventoryLoadoutSlotIdsV1.ArmorFeet, fixture.ArmorFour.InstanceId).ChangedSelection, Is.True);

            InventoryLoadoutScreenResultV1 result = service.Confirm();

            Assert.That(result.Status, Is.EqualTo(InventoryLoadoutScreenStatusV1.Confirmed));
            Assert.That(fixture.Loadout.ApplyCount, Is.EqualTo(1));
            Assert.That(result.RoutePayload, Is.Not.SameAs(fixture.RoutePayload));
            Assert.That(result.RoutePayload.WeaponSlots[0].EquipmentInstanceStableId, Is.EqualTo(fixture.WeaponOne.InstanceId));
            Assert.That(result.RoutePayload.WeaponSlots[1].EquipmentInstanceStableId, Is.EqualTo(fixture.WeaponTwo.InstanceId));
            Assert.That(result.RoutePayload.WeaponSlots[2].EquipmentInstanceStableId, Is.EqualTo(fixture.WeaponFour.InstanceId));
            Assert.That(result.RoutePayload.WeaponSlots[3].EquipmentInstanceStableId, Is.EqualTo(fixture.WeaponFive.InstanceId));
            Assert.That(fixture.Loadout.Snapshot.GetBinding(InventoryLoadoutSlotIdsV1.ArmorHead).EquipmentInstanceStableId, Is.EqualTo(fixture.ArmorOne.InstanceId));
            Assert.That(fixture.Loadout.Snapshot.GetBinding(InventoryLoadoutSlotIdsV1.ArmorFeet).EquipmentInstanceStableId, Is.EqualTo(fixture.ArmorFour.InstanceId));
            Assert.That(fixture.Holdings.ExportSnapshot().Fingerprint, Is.EqualTo(holdingsBefore));
        }

        [Test]
        public void EmptyWeaponSlotRejectsBeforeAuthorityMutation()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenServiceV1 service = fixture.CreateService();
            service.TryUnequip(InventoryLoadoutSlotIdsV1.WeaponFour);

            InventoryLoadoutScreenResultV1 result = service.Confirm();

            Assert.That(result.Status, Is.EqualTo(InventoryLoadoutScreenStatusV1.IncompleteWeaponLoadout));
            Assert.That(result.Snapshot.CanConfirm, Is.False);
            Assert.That(fixture.Loadout.ApplyCount, Is.Zero);
        }

        [Test]
        public void InvalidWrongTypeUnknownAndDuplicateSelectionsRejectWithoutMutation()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenServiceV1 service = fixture.CreateService();
            string before = fixture.Holdings.ExportSnapshot().Fingerprint;

            Assert.That(service.TrySelect(InventoryLoadoutSlotIdsV1.WeaponOne, fixture.ArmorOne.InstanceId).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.WrongEquipmentType));
            Assert.That(service.TrySelect(InventoryLoadoutSlotIdsV1.ArmorHead, fixture.WeaponOne.InstanceId).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.WrongEquipmentType));
            Assert.That(service.TrySelect(InventoryLoadoutSlotIdsV1.ArmorHead, fixture.Gadget.InstanceId).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.InvalidEquipment));
            Assert.That(service.TrySelect(InventoryLoadoutSlotIdsV1.WeaponOne, Id("equipment-instance.unknown")).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.MissingEquipment));
            Assert.That(service.TrySelect(InventoryLoadoutSlotIdsV1.WeaponTwo, fixture.WeaponOne.InstanceId).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.DuplicateEquipmentInstance));
            Assert.That(fixture.Loadout.ApplyCount, Is.Zero);
            Assert.That(fixture.Holdings.ExportSnapshot().Fingerprint, Is.EqualTo(before));
        }

        [Test]
        public void RefreshRetainsIdentityAndMarksRemovedSelectionStale()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenServiceV1 service = fixture.CreateService();
            service.TrySelect(InventoryLoadoutSlotIdsV1.WeaponTwo, fixture.WeaponTwo.InstanceId);
            fixture.Remove(fixture.WeaponTwo, "selected");

            InventoryLoadoutScreenResultV1 refresh = service.Refresh();
            InventoryLoadoutSelectionProjectionV1 selection = refresh.Snapshot.GetSelection(InventoryLoadoutSlotIdsV1.WeaponTwo);

            Assert.That(selection.EquipmentInstanceStableId, Is.EqualTo(fixture.WeaponTwo.InstanceId));
            Assert.That(selection.IsValid, Is.False);
            Assert.That(selection.RejectionCode, Is.EqualTo("inventory-loadout-selection-stale"));
            Assert.That(service.Confirm().Status, Is.EqualTo(InventoryLoadoutScreenStatusV1.StaleSelection));
            Assert.That(fixture.Loadout.ApplyCount, Is.Zero);
        }

        [Test]
        public void RepeatInputDoesNotApplyTwice()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenServiceV1 service = fixture.CreateService();

            Assert.That(service.TrySelect(InventoryLoadoutSlotIdsV1.WeaponOne, fixture.WeaponOne.InstanceId).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.NoChange));
            Assert.That(service.Confirm().Status, Is.EqualTo(InventoryLoadoutScreenStatusV1.Confirmed));
            Assert.That(service.Confirm().Status, Is.EqualTo(InventoryLoadoutScreenStatusV1.AlreadyCompleted));
            Assert.That(fixture.Loadout.ApplyCount, Is.EqualTo(1));
        }

        [Test]
        public void BackReturnsExactIncomingPayloadWithoutAuthorityCall()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenServiceV1 service = fixture.CreateService();
            service.TrySelect(InventoryLoadoutSlotIdsV1.WeaponTwo, fixture.WeaponTwo.InstanceId);

            InventoryLoadoutScreenResultV1 result = service.Back();

            Assert.That(result.Status, Is.EqualTo(InventoryLoadoutScreenStatusV1.Cancelled));
            Assert.That(result.RoutePayload, Is.SameAs(fixture.RoutePayload));
            Assert.That(fixture.Loadout.ApplyCount, Is.Zero);
            Assert.That(service.Back().Status, Is.EqualTo(InventoryLoadoutScreenStatusV1.AlreadyCompleted));
        }

        [Test]
        public void RevisitRestoresExactWeaponAndArmorInstanceIdentities()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenServiceV1 first = fixture.CreateService();
            first.TrySelect(InventoryLoadoutSlotIdsV1.WeaponTwo, fixture.WeaponTwo.InstanceId);
            first.TrySelect(InventoryLoadoutSlotIdsV1.ArmorHead, fixture.ArmorOne.InstanceId);
            PlayerRouteProfilePayloadV1 confirmed = first.Confirm().RoutePayload;

            InventoryLoadoutScreenServiceV1 revisit = fixture.CreateService(confirmed);

            Assert.That(revisit.Snapshot.GetSelection(InventoryLoadoutSlotIdsV1.WeaponTwo).EquipmentInstanceStableId,
                Is.EqualTo(fixture.WeaponTwo.InstanceId));
            Assert.That(revisit.Snapshot.GetSelection(InventoryLoadoutSlotIdsV1.ArmorHead).EquipmentInstanceStableId,
                Is.EqualTo(fixture.ArmorOne.InstanceId));
            Assert.That(revisit.Snapshot.CanConfirm, Is.True);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class Fixture
        {
            private static readonly StableId AuthorityId = Id("holdings.inventory-loadout-tests");
            private int ordinal;

            public Fixture()
            {
                EquipmentQualityTier common = EquipmentQualityTier.Create(Id("quality.common"), "Common", 1);
                EquipmentDefinition shared = Definition("equipment.shared-weapon", EquipmentCategoryIds.Weapon, "Shared Weapon", common);
                EquipmentDefinition weaponB = Definition("equipment.weapon-b", EquipmentCategoryIds.Weapon, "Weapon B", common);
                EquipmentDefinition weaponC = Definition("equipment.weapon-c", EquipmentCategoryIds.Weapon, "Weapon C", common);
                EquipmentDefinition weaponD = Definition("equipment.weapon-d", EquipmentCategoryIds.Weapon, "Weapon D", common);
                EquipmentDefinition armor = Definition("equipment.shared-armor", EquipmentCategoryIds.Armor, "Armor", common);
                EquipmentDefinition gadget = Definition("equipment.future-gadget", Id("equipment-category.gadget"), "Gadget", common);
                EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                    new[] { shared, weaponB, weaponC, weaponD, armor, gadget },
                    new AugmentDefinition[0]);
                Assert.That(build.IsValid, Is.True);
                Catalog = new CatalogAdapter(build.Catalog);
                Holdings = new PlayerHoldingsService(AuthorityId, 1000L, Catalog);

                WeaponOne = Instance("equipment-instance.weapon-1", shared);
                WeaponTwo = Instance("equipment-instance.weapon-2", shared);
                WeaponThree = Instance("equipment-instance.weapon-3", weaponB);
                WeaponFour = Instance("equipment-instance.weapon-4", weaponC);
                WeaponFive = Instance("equipment-instance.weapon-5", weaponD);
                ArmorOne = Instance("equipment-instance.armor-1", armor);
                ArmorTwo = Instance("equipment-instance.armor-2", armor);
                ArmorThree = Instance("equipment-instance.armor-3", armor);
                ArmorFour = Instance("equipment-instance.armor-4", armor);
                Gadget = Instance("equipment-instance.gadget", gadget);
                Add(WeaponOne); Add(WeaponTwo); Add(WeaponThree); Add(WeaponFour); Add(WeaponFive);
                Add(ArmorOne); Add(ArmorTwo); Add(ArmorThree); Add(ArmorFour); Add(Gadget);

                RoutePayload = PlayerRouteProfilePayloadV1.Create(
                    Id("character.inventory-loadout-test"),
                    Id("loadout-profile.inventory-loadout-test"),
                    new[] { WeaponOne.InstanceId, WeaponThree.InstanceId, WeaponFour.InstanceId, WeaponFive.InstanceId });
                Loadout = new RecordingLoadoutAuthority(RoutePayload);
            }

            public CatalogAdapter Catalog { get; }
            public PlayerHoldingsService Holdings { get; }
            public RecordingLoadoutAuthority Loadout { get; }
            public PlayerRouteProfilePayloadV1 RoutePayload { get; }
            public EquipmentInstance WeaponOne { get; }
            public EquipmentInstance WeaponTwo { get; }
            public EquipmentInstance WeaponThree { get; }
            public EquipmentInstance WeaponFour { get; }
            public EquipmentInstance WeaponFive { get; }
            public EquipmentInstance ArmorOne { get; }
            public EquipmentInstance ArmorTwo { get; }
            public EquipmentInstance ArmorThree { get; }
            public EquipmentInstance ArmorFour { get; }
            public EquipmentInstance Gadget { get; }

            public InventoryLoadoutScreenServiceV1 CreateService(PlayerRouteProfilePayloadV1 payload = null)
            {
                return new InventoryLoadoutScreenServiceV1(payload ?? RoutePayload, Holdings, Catalog, Loadout);
            }

            public void Remove(EquipmentInstance instance, string suffix)
            {
                PlayerHoldingsMutationResultV1 result = Holdings.Apply(PlayerHoldingsCommandV1.RemoveEquipment(
                    Id("transaction.remove-" + suffix),
                    Id("operation.remove-" + suffix),
                    AuthorityId,
                    instance.DefinitionId,
                    instance.InstanceId,
                    HoldingProvenanceV1.Create(Id("grant.remove-" + suffix), Id("source.inventory-loadout-test")),
                    Holdings.Sequence));
                Assert.That(result.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            }

            private void Add(EquipmentInstance instance)
            {
                ordinal++;
                string suffix = ordinal.ToString();
                PlayerHoldingsMutationResultV1 result = Holdings.Apply(PlayerHoldingsCommandV1.AddEquipment(
                    Id("transaction.add-" + suffix),
                    Id("operation.add-" + suffix),
                    AuthorityId,
                    instance,
                    HoldingProvenanceV1.Create(Id("grant.add-" + suffix), Id("source.inventory-loadout-fixture")),
                    Holdings.Sequence));
                Assert.That(result.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            }

            private static EquipmentDefinition Definition(string id, StableId category, string name, EquipmentQualityTier quality)
            {
                return EquipmentDefinition.Create(
                    Id(id),
                    category,
                    Id(id.Replace("equipment.", "equipment-family.")),
                    name,
                    category == EquipmentCategoryIds.Weapon ? Id("weapon.blaster-machine-gun") : null,
                    InclusiveIntRange.Create(1, 100),
                    0,
                    new[] { quality },
                    new StableId[0]);
            }

            private static EquipmentInstance Instance(string id, EquipmentDefinition definition)
            {
                return EquipmentInstance.Create(Id(id), definition.DefinitionId, 10, Id("quality.common"), new AugmentInstance[0]);
            }
        }

        private sealed class CatalogAdapter : IEquipmentCatalogProvider, IEquipmentInstanceValidator
        {
            public CatalogAdapter(EquipmentCatalog catalog)
            {
                Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            }
            public EquipmentCatalog Catalog { get; }
            public EquipmentInstanceValidationResponse Validate(EquipmentInstanceValidationRequest request)
            {
                EquipmentInstance instance = request == null ? null : request.Instance;
                return EquipmentInstanceValidationResponse.From(Catalog, instance, Catalog.ValidateInstance(instance));
            }
        }

        private sealed class RecordingLoadoutAuthority : IInventoryLoadoutAuthorityPortV1
        {
            public RecordingLoadoutAuthority(PlayerRouteProfilePayloadV1 payload)
            {
                var bindings = new List<InventoryLoadoutSlotBindingV1>();
                for (int index = 0; index < InventoryLoadoutSlotsV1.All.Count; index++)
                {
                    StableId instance = index < payload.WeaponSlots.Count ? payload.WeaponSlots[index].EquipmentInstanceStableId : null;
                    bindings.Add(new InventoryLoadoutSlotBindingV1(InventoryLoadoutSlotsV1.All[index].SlotStableId, instance));
                }
                Snapshot = InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(0L, bindings);
            }

            public int ApplyCount { get; private set; }
            public InventoryLoadoutAuthoritySnapshotV1 Snapshot { get; private set; }
            public InventoryLoadoutAuthoritySnapshotV1 ExportSnapshot() { return Snapshot; }
            public InventoryLoadoutAuthorityResultV1 Apply(InventoryLoadoutAuthorityCommandV1 command)
            {
                ApplyCount++;
                if (command == null || command.ExpectedSequence != Snapshot.Sequence)
                {
                    return new InventoryLoadoutAuthorityResultV1(InventoryLoadoutAuthorityMutationStatusV1.StaleSnapshot, "sequence-stale", Snapshot);
                }
                Snapshot = InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(Snapshot.Sequence + 1L, command.Bindings);
                return new InventoryLoadoutAuthorityResultV1(InventoryLoadoutAuthorityMutationStatusV1.Applied, string.Empty, Snapshot);
            }
        }
    }
}
