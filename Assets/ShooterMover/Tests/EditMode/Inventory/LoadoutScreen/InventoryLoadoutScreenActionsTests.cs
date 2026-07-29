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
            InventoryLoadoutEquipmentView first = snapshot.FindEquipment(fixture.GunOne.InstanceId);
            InventoryLoadoutEquipmentView second = snapshot.FindEquipment(fixture.GunTwo.InstanceId);

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(first.InstanceStableId, Is.Not.EqualTo(second.InstanceStableId));
            Assert.That(first.DefinitionStableId, Is.EqualTo(second.DefinitionStableId));
        }

        [Test]
        public void ConfirmAppliesAllSlotsAndPreservesExactGunOrder()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenActions service = fixture.CreateService();
            string holdingsBefore = fixture.Holdings.ExportSnapshot().Fingerprint;

            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.GunTwo, fixture.GunTwo.InstanceId).ChangedSelection, Is.True);
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.ArmorHead, fixture.ArmorOne.InstanceId).ChangedSelection, Is.True);
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.ArmorBody, fixture.ArmorTwo.InstanceId).ChangedSelection, Is.True);
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.ArmorLegs, fixture.ArmorThree.InstanceId).ChangedSelection, Is.True);
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.ArmorFeet, fixture.ArmorFour.InstanceId).ChangedSelection, Is.True);

            InventoryLoadoutScreenResult result = service.Confirm();

            Assert.That(result.Status, Is.EqualTo(InventoryLoadoutScreenStatus.Confirmed));
            Assert.That(fixture.Loadout.ApplyCount, Is.EqualTo(1));
            Assert.That(result.RoutePayload, Is.Not.SameAs(fixture.RoutePayload));
            Assert.That(result.RoutePayload.GunSlots[0].EquipmentInstanceStableId, Is.EqualTo(fixture.GunOne.InstanceId));
            Assert.That(result.RoutePayload.GunSlots[1].EquipmentInstanceStableId, Is.EqualTo(fixture.GunTwo.InstanceId));
            Assert.That(result.RoutePayload.GunSlots[2].EquipmentInstanceStableId, Is.EqualTo(fixture.GunFour.InstanceId));
            Assert.That(result.RoutePayload.GunSlots[3].EquipmentInstanceStableId, Is.EqualTo(fixture.GunFive.InstanceId));
            Assert.That(fixture.Loadout.Snapshot.GetBinding(InventoryLoadoutSlotIds.ArmorHead).EquipmentInstanceStableId, Is.EqualTo(fixture.ArmorOne.InstanceId));
            Assert.That(fixture.Loadout.Snapshot.GetBinding(InventoryLoadoutSlotIds.ArmorFeet).EquipmentInstanceStableId, Is.EqualTo(fixture.ArmorFour.InstanceId));
            Assert.That(fixture.Holdings.ExportSnapshot().Fingerprint, Is.EqualTo(holdingsBefore));
        }

        [Test]
        public void EmptyGunSlotRejectsBeforeAuthorityMutation()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenActions service = fixture.CreateService();
            service.TryUnequip(InventoryLoadoutSlotIds.GunFour);

            InventoryLoadoutScreenResult result = service.Confirm();

            Assert.That(result.Status, Is.EqualTo(InventoryLoadoutScreenStatus.IncompleteGunLoadout));
            Assert.That(result.Snapshot.CanConfirm, Is.False);
            Assert.That(fixture.Loadout.ApplyCount, Is.Zero);
        }

        [Test]
        public void InvalidWrongTypeUnknownAndDuplicateSelectionsRejectWithoutMutation()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenActions service = fixture.CreateService();
            string before = fixture.Holdings.ExportSnapshot().Fingerprint;

            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.GunOne, fixture.ArmorOne.InstanceId).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.WrongEquipmentType));
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.ArmorHead, fixture.GunOne.InstanceId).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.WrongEquipmentType));
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.ArmorHead, fixture.Gadget.InstanceId).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.InvalidEquipment));
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.GunOne, Id("equipment-instance.unknown")).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.MissingEquipment));
            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.GunTwo, fixture.GunOne.InstanceId).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.DuplicateEquipmentInstance));
            Assert.That(fixture.Loadout.ApplyCount, Is.Zero);
            Assert.That(fixture.Holdings.ExportSnapshot().Fingerprint, Is.EqualTo(before));
        }

        [Test]
        public void RefreshRetainsIdentityAndMarksRemovedSelectionStale()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenActions service = fixture.CreateService();
            service.TrySelect(InventoryLoadoutSlotIds.GunTwo, fixture.GunTwo.InstanceId);
            fixture.Remove(fixture.GunTwo, "selected");

            InventoryLoadoutScreenResult refresh = service.Refresh();
            InventoryLoadoutSelectionView selection = refresh.Snapshot.GetSelection(InventoryLoadoutSlotIds.GunTwo);

            Assert.That(selection.EquipmentInstanceStableId, Is.EqualTo(fixture.GunTwo.InstanceId));
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

            Assert.That(service.TrySelect(InventoryLoadoutSlotIds.GunOne, fixture.GunOne.InstanceId).Status,
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
            service.TrySelect(InventoryLoadoutSlotIds.GunTwo, fixture.GunTwo.InstanceId);

            InventoryLoadoutScreenResult result = service.Back();

            Assert.That(result.Status, Is.EqualTo(InventoryLoadoutScreenStatus.Cancelled));
            Assert.That(result.RoutePayload, Is.SameAs(fixture.RoutePayload));
            Assert.That(fixture.Loadout.ApplyCount, Is.Zero);
            Assert.That(service.Back().Status, Is.EqualTo(InventoryLoadoutScreenStatus.AlreadyCompleted));
        }

        [Test]
        public void RevisitRestoresExactGunAndArmorInstanceIdentities()
        {
            Fixture fixture = new Fixture();
            InventoryLoadoutScreenActions first = fixture.CreateService();
            first.TrySelect(InventoryLoadoutSlotIds.GunTwo, fixture.GunTwo.InstanceId);
            first.TrySelect(InventoryLoadoutSlotIds.ArmorHead, fixture.ArmorOne.InstanceId);
            PlayerRouteProfilePayload confirmed = first.Confirm().RoutePayload;

            InventoryLoadoutScreenActions revisit = fixture.CreateService(confirmed);

            Assert.That(revisit.Snapshot.GetSelection(InventoryLoadoutSlotIds.GunTwo).EquipmentInstanceStableId,
                Is.EqualTo(fixture.GunTwo.InstanceId));
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
                EquipmentDefinition shared = Definition("equipment.shared-gun", EquipmentCategoryIds.Gun, "Shared Gun", common);
                EquipmentDefinition gunB = Definition("equipment.gun-b", EquipmentCategoryIds.Gun, "Gun B", common);
                EquipmentDefinition gunC = Definition("equipment.gun-c", EquipmentCategoryIds.Gun, "Gun C", common);
                EquipmentDefinition gunD = Definition("equipment.gun-d", EquipmentCategoryIds.Gun, "Gun D", common);
                EquipmentDefinition armor = Definition("equipment.shared-armor", EquipmentCategoryIds.Armor, "Armor", common);
                EquipmentDefinition gadget = Definition("equipment.future-gadget", Id("equipment-category.gadget"), "Gadget", common);
                EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                    new[] { shared, gunB, gunC, gunD, armor, gadget },
                    new AugmentDefinition[0]);
                Assert.That(build.IsValid, Is.True);
                Catalog = new CatalogBridge(build.Catalog);
                Holdings = new PlayerHoldingsActions(AuthorityId, 1000L, Catalog);

                GunOne = Instance("equipment-instance.gun-1", shared);
                GunTwo = Instance("equipment-instance.gun-2", shared);
                GunThree = Instance("equipment-instance.gun-3", gunB);
                GunFour = Instance("equipment-instance.gun-4", gunC);
                GunFive = Instance("equipment-instance.gun-5", gunD);
                ArmorOne = Instance("equipment-instance.armor-1", armor);
                ArmorTwo = Instance("equipment-instance.armor-2", armor);
                ArmorThree = Instance("equipment-instance.armor-3", armor);
                ArmorFour = Instance("equipment-instance.armor-4", armor);
                Gadget = Instance("equipment-instance.gadget", gadget);
                Add(GunOne); Add(GunTwo); Add(GunThree); Add(GunFour); Add(GunFive);
                Add(ArmorOne); Add(ArmorTwo); Add(ArmorThree); Add(ArmorFour); Add(Gadget);

                RoutePayload = PlayerRouteProfilePayload.Create(
                    Id("character.inventory-loadout-test"),
                    Id("loadout-profile.inventory-loadout-test"),
                    new[] { GunOne.InstanceId, GunThree.InstanceId, GunFour.InstanceId, GunFive.InstanceId });
                Loadout = new RecordingLoadoutState(RoutePayload);
            }

            public CatalogBridge Catalog { get; }
            public PlayerHoldingsActions Holdings { get; }
            public RecordingLoadoutState Loadout { get; }
            public PlayerRouteProfilePayload RoutePayload { get; }
            public EquipmentInstance GunOne { get; }
            public EquipmentInstance GunTwo { get; }
            public EquipmentInstance GunThree { get; }
            public EquipmentInstance GunFour { get; }
            public EquipmentInstance GunFive { get; }
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
                    category == EquipmentCategoryIds.Gun ? Id("gun.blaster-machine-gun") : null,
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
                    StableId instance = index < payload.GunSlots.Count ? payload.GunSlots[index].EquipmentInstanceStableId : null;
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
