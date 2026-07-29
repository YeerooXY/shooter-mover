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
    public sealed class InventoryLoadoutScreenActionsTests
    {
        [Test]
        public void DuplicateDefinitionsRemainSeparateConcreteInstances()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenSnapshot snapshot = fixture.CreateService().Snapshot;
            InventoryLoadoutEquipmentView first = snapshot.FindEquipment(fixture.WeaponOne.InstanceId);
            InventoryLoadoutEquipmentView second = snapshot.FindEquipment(fixture.WeaponTwo.InstanceId);

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(first.InstanceStableId, Is.Not.EqualTo(second.InstanceStableId));
            Assert.That(first.DefinitionStableId, Is.EqualTo(second.DefinitionStableId));
        }

        [Test]
        public void ConfirmAppliesAllSlotsAndPreservesExactWeaponOrder()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenActions service = fixture.CreateService();
            string holdingsBefore = fixture.Holdings.ExportSnapshot().Fingerprint;

            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.WeaponTwo, fixture.WeaponTwo.InstanceId).ChangedSelection, Is.True);
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.ArmorHead, fixture.ArmorOne.InstanceId).ChangedSelection, Is.True);
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.ArmorBody, fixture.ArmorTwo.InstanceId).ChangedSelection, Is.True);
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.ArmorLegs, fixture.ArmorThree.InstanceId).ChangedSelection, Is.True);
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.ArmorFeet, fixture.ArmorFour.InstanceId).ChangedSelection, Is.True);

            InventoryLoadoutScreenResult result = service.Confirm();

            Assert.That(result.Status, Is.EqualTo(InventoryLoadoutScreenStatus.Confirmed));
            Assert.That(fixture.Loadout.ApplyCount, Is.EqualTo(1));
            Assert.That(result.RoutePayload, Is.Not.SameAs(fixture.RoutePayload));
            Assert.That(result.RoutePayload.WeaponSlots[0].EquipmentInstanceStableId, Is.EqualTo(fixture.WeaponOne.InstanceId));
            Assert.That(result.RoutePayload.WeaponSlots[1].EquipmentInstanceStableId, Is.EqualTo(fixture.WeaponTwo.InstanceId));
            Assert.That(result.RoutePayload.WeaponSlots[2].EquipmentInstanceStableId, Is.EqualTo(fixture.WeaponFour.InstanceId));
            Assert.That(result.RoutePayload.WeaponSlots[3].EquipmentInstanceStableId, Is.EqualTo(fixture.WeaponFive.InstanceId));
            Assert.That(fixture.Loadout.Snapshot.GetBinding(InventoryLoadoutSlotIds.ArmorHead).EquipmentInstanceStableId, Is.EqualTo(fixture.ArmorOne.InstanceId));
            Assert.That(fixture.Loadout.Snapshot.GetBinding(InventoryLoadoutSlotIds.ArmorFeet).EquipmentInstanceStableId, Is.EqualTo(fixture.ArmorFour.InstanceId));
            Assert.That(fixture.Holdings.ExportSnapshot().Fingerprint, Is.EqualTo(holdingsBefore));
        }

        [Test]
        public void EmptyWeaponSlotRejectsBeforeAuthorityMutation()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenActions service = fixture.CreateService();
            service.TryUnequip(InventoryLoadoutSlotIds.WeaponFour);

            InventoryLoadoutScreenResult result = service.Confirm();

            Assert.That(result.Status, Is.EqualTo(InventoryLoadoutScreenStatus.IncompleteWeaponLoadout));
            Assert.That(result.Snapshot.CanConfirm, Is.False);
            Assert.That(fixture.Loadout.ApplyCount, Is.Zero);
        }

        [Test]
        public void InvalidWrongTypeUnknownAndDuplicateSelectionsRejectWithoutMutation()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenActions service = fixture.CreateService();
            string before = fixture.Holdings.ExportSnapshot().Fingerprint;

            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.WeaponOne, fixture.ArmorOne.InstanceId).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.WrongEquipmentType));
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.ArmorHead, fixture.WeaponOne.InstanceId).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.WrongEquipmentType));
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.ArmorHead, fixture.Gadget.InstanceId).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.InvalidEquipment));
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.WeaponOne, Id("equipment-instance.unknown")).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.MissingEquipment));
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.WeaponTwo, fixture.WeaponOne.InstanceId).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.DuplicateEquipmentInstance));
            Assert.That(fixture.Loadout.ApplyCount, Is.Zero);
            Assert.That(fixture.Holdings.ExportSnapshot().Fingerprint, Is.EqualTo(before));
        }

        [Test]
        public void RefreshRetainsIdentityAndMarksRemovedSelectionStale()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenActions service = fixture.CreateService();
            service.TrySelect(InventoryLoadoutSlotIds.WeaponTwo, fixture.WeaponTwo.InstanceId);
            fixture.Remove(fixture.WeaponTwo, "selected");

            InventoryLoadoutScreenResult refresh = service.Refresh();
            InventoryLoadoutSelectionView selection = refresh.Snapshot.GetSelection(InventoryLoadoutSlotIds.WeaponTwo);

            Assert.That(selection.EquipmentInstanceStableId, Is.EqualTo(fixture.WeaponTwo.InstanceId));
            Assert.That(selection.IsValid, Is.False);
            Assert.That(selection.RejectionCode, Is.EqualTo("inventory-loadout-selection-stale"));
            Assert.That(service.Confirm().Status, Is.EqualTo(InventoryLoadoutScreenStatus.StaleSelection));
            Assert.That(fixture.Loadout.ApplyCount, Is.Zero);
        }

        [Test]
        public void RepeatInputDoesNotApplyTwice()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenActions service = fixture.CreateService();

            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.WeaponOne, fixture.WeaponOne.InstanceId).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.NoChange));
            Assert.That(service.Confirm().Status, Is.EqualTo(InventoryLoadoutScreenStatus.Confirmed));
            Assert.That(service.Confirm().Status, Is.EqualTo(InventoryLoadoutScreenStatus.AlreadyCompleted));
            Assert.That(fixture.Loadout.ApplyCount, Is.EqualTo(1));
        }

        [Test]
        public void BackReturnsExactIncomingPayloadWithoutAuthorityCall()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenActions service = fixture.CreateService();
            service.TrySelect(InventoryLoadoutSlotIds.WeaponTwo, fixture.WeaponTwo.InstanceId);

            InventoryLoadoutScreenResult result = service.Back();

            Assert.That(result.Status, Is.EqualTo(InventoryLoadoutScreenStatus.Cancelled));
            Assert.That(result.RoutePayload, Is.SameAs(fixture.RoutePayload));
            Assert.That(fixture.Loadout.ApplyCount, Is.Zero);
            Assert.That(service.Back().Status, Is.EqualTo(InventoryLoadoutScreenStatus.AlreadyCompleted));
        }

        [Test]
        public void RevisitRestoresExactWeaponAndArmorInstanceIdentities()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenActions first = fixture.CreateService();
            first.TrySelect(InventoryLoadoutSlotIds.WeaponTwo, fixture.WeaponTwo.InstanceId);
            first.TrySelect(InventoryLoadoutSlotIds.ArmorHead, fixture.ArmorOne.InstanceId);
            PlayerRouteProfilePayload confirmed = first.Confirm().RoutePayload;

            InventoryLoadoutScreenActions revisit = fixture.CreateService(confirmed);

            Assert.That(revisit.Snapshot.GetSelection(InventoryLoadoutSlotIds.WeaponTwo).EquipmentInstanceStableId,
                Is.EqualTo(fixture.WeaponTwo.InstanceId));
            Assert.That(revisit.Snapshot.GetSelection(InventoryLoadoutSlotIds.ArmorHead).EquipmentInstanceStableId,
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
                Catalog = new CatalogBridge(build.Catalog);
                Holdings = new PlayerHoldingsActions(AuthorityId, 1000L, Catalog);

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

                RoutePayload = PlayerRouteProfilePayload.Create(
                    Id("character.inventory-loadout-test"),
                    Id("loadout-profile.inventory-loadout-test"),
                    new[] { WeaponOne.InstanceId, WeaponThree.InstanceId, WeaponFour.InstanceId, WeaponFive.InstanceId });
                Loadout = new RecordingLoadoutState(RoutePayload);
            }

            public CatalogBridge Catalog { get; }
            public PlayerHoldingsActions Holdings { get; }
            public RecordingLoadoutState Loadout { get; }
            public PlayerRouteProfilePayload RoutePayload { get; }
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

            public InventoryLoadoutScreenActions CreateService(PlayerRouteProfilePayload payload = null)
            {
                return new InventoryLoadoutScreenActions(payload ?? RoutePayload, Holdings, Catalog, Loadout);
            }

            public void Remove(EquipmentInstance instance, string suffix)
            {
                PlayerHoldingsMutationResult result = Holdings.Apply(PlayerHoldingsCommand.RemoveEquipment(
                    Id("transaction.remove-" + suffix),
                    Id("operation.remove-" + suffix),
                    AuthorityId,
                    instance.DefinitionId,
                    instance.InstanceId,
                    HoldingProvenance.Create(Id("grant.remove-" + suffix), Id("source.inventory-loadout-test")),
                    Holdings.Sequence));
                Assert.That(result.Status, Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            }

            private void Add(EquipmentInstance instance)
            {
                ordinal++;
                string suffix = ordinal.ToString();
                PlayerHoldingsMutationResult result = Holdings.Apply(PlayerHoldingsCommand.AddEquipment(
                    Id("transaction.add-" + suffix),
                    Id("operation.add-" + suffix),
                    AuthorityId,
                    instance,
                    HoldingProvenance.Create(Id("grant.add-" + suffix), Id("source.inventory-loadout-fixture")),
                    Holdings.Sequence));
                Assert.That(result.Status, Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
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

        private sealed class CatalogBridge : IEquipmentCatalogProvider, IEquipmentInstanceValidator
        {
            public CatalogBridge(EquipmentCatalog catalog)
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

        private sealed class RecordingLoadoutState : IInventoryLoadoutStatePort
        {
            public RecordingLoadoutState(PlayerRouteProfilePayload payload)
            {
                var bindings = new List<InventoryLoadoutSlotBinding>();
                for (int index = 0; index < InventoryLoadoutSlots.All.Count; index++)
                {
                    StableId instance = index < payload.WeaponSlots.Count ? payload.WeaponSlots[index].EquipmentInstanceStableId : null;
                    bindings.Add(new InventoryLoadoutSlotBinding(InventoryLoadoutSlots.All[index].SlotStableId, instance));
                }
                Snapshot = InventoryLoadoutStateSnapshot.CreateCanonical(0L, bindings);
            }

            public int ApplyCount { get; private set; }
            public InventoryLoadoutStateSnapshot Snapshot { get; private set; }
            public InventoryLoadoutStateSnapshot ExportSnapshot() { return Snapshot; }
            public InventoryLoadoutStateResult Apply(InventoryLoadoutStateCommand command)
            {
                ApplyCount++;
                if (command == null || command.ExpectedSequence != Snapshot.Sequence)
                {
                    return new InventoryLoadoutStateResult(InventoryLoadoutStateMutationStatus.StaleSnapshot, "sequence-stale", Snapshot);
                }
                Snapshot = InventoryLoadoutStateSnapshot.CreateCanonical(Snapshot.Sequence + 1L, command.Bindings);
                return new InventoryLoadoutStateResult(InventoryLoadoutStateMutationStatus.Applied, string.Empty, Snapshot);
            }
        }
    }
}
