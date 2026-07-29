using System;
using System.Collections;
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
using ShooterMover.UI.InventoryLoadout;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Flow.InventoryLoadout
{
    public sealed class InventoryLoadoutScreenControllerTests
    {
        [UnityTest]
        public IEnumerator ConfirmReturnsNewPayloadWithExactConcreteIdentity()
        {
            Fixture fixture = new Fixture();
            GameObject host = new GameObject("INV-002 Confirm Test");
            InventoryLoadoutScreenController controller = host.AddComponent<InventoryLoadoutScreenController>();
            PlayerRouteProfilePayload returned = null;
            controller.ConfigureForTests(fixture.Holdings, fixture.Catalog, fixture.Loadout, delegate(PlayerRouteProfilePayload payload) { returned = payload; });
            controller.Present(HubRoute.Inventory, fixture.RoutePayload);
            yield return null;

            controller.SelectSlot(InventoryLoadoutSlotIds.WeaponTwo);
            Assert.That(controller.SelectInstance(fixture.AlternateWeapon.InstanceId).ChangedSelection, Is.True);
            controller.SelectSlot(InventoryLoadoutSlotIds.ArmorHead);
            Assert.That(controller.SelectInstance(fixture.Armor.InstanceId).ChangedSelection, Is.True);
            InventoryLoadoutScreenResult result = controller.Confirm();

            Assert.That(result.Status, Is.EqualTo(InventoryLoadoutScreenStatus.Confirmed));
            Assert.That(returned, Is.SameAs(result.RoutePayload));
            Assert.That(returned, Is.Not.SameAs(fixture.RoutePayload));
            Assert.That(returned.WeaponSlots[1].EquipmentInstanceStableId, Is.EqualTo(fixture.AlternateWeapon.InstanceId));
            Assert.That(controller.ReturnCount, Is.EqualTo(1));
            Assert.That(fixture.Loadout.ApplyCount, Is.EqualTo(1));
            UnityEngine.Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator BackReturnsSamePayloadOnlyOnce()
        {
            Fixture fixture = new Fixture();
            GameObject host = new GameObject("INV-002 Back Test");
            InventoryLoadoutScreenController controller = host.AddComponent<InventoryLoadoutScreenController>();
            PlayerRouteProfilePayload returned = null;
            controller.ConfigureForTests(fixture.Holdings, fixture.Catalog, fixture.Loadout, delegate(PlayerRouteProfilePayload payload) { returned = payload; });
            controller.Present(HubRoute.Inventory, fixture.RoutePayload);
            yield return null;

            InventoryLoadoutScreenResult first = controller.Back();
            InventoryLoadoutScreenResult second = controller.Back();

            Assert.That(first.Status, Is.EqualTo(InventoryLoadoutScreenStatus.Cancelled));
            Assert.That(second.Status, Is.EqualTo(InventoryLoadoutScreenStatus.AlreadyCompleted));
            Assert.That(returned, Is.SameAs(fixture.RoutePayload));
            Assert.That(controller.ReturnCount, Is.EqualTo(1));
            Assert.That(fixture.Loadout.ApplyCount, Is.Zero);
            UnityEngine.Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator RevisitProjectsConfirmedWeaponAndArmorInstances()
        {
            Fixture fixture = new Fixture();
            GameObject host = new GameObject("INV-002 Revisit Test");
            InventoryLoadoutScreenController controller = host.AddComponent<InventoryLoadoutScreenController>();
            controller.ConfigureForTests(fixture.Holdings, fixture.Catalog, fixture.Loadout, null);
            controller.Present(HubRoute.Inventory, fixture.RoutePayload);
            controller.SelectSlot(InventoryLoadoutSlotIds.WeaponTwo);
            controller.SelectInstance(fixture.AlternateWeapon.InstanceId);
            controller.SelectSlot(InventoryLoadoutSlotIds.ArmorHead);
            controller.SelectInstance(fixture.Armor.InstanceId);
            PlayerRouteProfilePayload confirmed = controller.Confirm().RoutePayload;
            yield return null;

            controller.Present(HubRoute.Inventory, confirmed);
            yield return null;

            Assert.That(controller.Snapshot.GetSelection(InventoryLoadoutSlotIds.WeaponTwo).EquipmentInstanceStableId,
                Is.EqualTo(fixture.AlternateWeapon.InstanceId));
            Assert.That(controller.Snapshot.GetSelection(InventoryLoadoutSlotIds.ArmorHead).EquipmentInstanceStableId,
                Is.EqualTo(fixture.Armor.InstanceId));
            Assert.That(controller.Snapshot.CanConfirm, Is.True);
            UnityEngine.Object.Destroy(host);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class Fixture
        {
            private static readonly StableId AuthorityId = Id("holdings.inventory-loadout-playmode");
            private int ordinal;

            public Fixture()
            {
                EquipmentQualityTier common = EquipmentQualityTier.Create(Id("quality.common"), "Common", 1);
                EquipmentDefinition shared = Definition("equipment.playmode-shared", EquipmentCategoryIds.Weapon, "Shared", common);
                EquipmentDefinition weaponB = Definition("equipment.playmode-b", EquipmentCategoryIds.Weapon, "B", common);
                EquipmentDefinition weaponC = Definition("equipment.playmode-c", EquipmentCategoryIds.Weapon, "C", common);
                EquipmentDefinition weaponD = Definition("equipment.playmode-d", EquipmentCategoryIds.Weapon, "D", common);
                EquipmentDefinition armorDefinition = Definition("equipment.playmode-armor", EquipmentCategoryIds.Armor, "Armor", common);
                EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                    new[] { shared, weaponB, weaponC, weaponD, armorDefinition },
                    new AugmentDefinition[0]);
                Assert.That(build.IsValid, Is.True);
                Catalog = new CatalogBridge(build.Catalog);
                Holdings = new PlayerHoldingsActions(AuthorityId, 1000L, Catalog);
                WeaponOne = Instance("equipment-instance.playmode-1", shared);
                AlternateWeapon = Instance("equipment-instance.playmode-2", shared);
                EquipmentInstance weaponThree = Instance("equipment-instance.playmode-3", weaponB);
                EquipmentInstance weaponFour = Instance("equipment-instance.playmode-4", weaponC);
                EquipmentInstance weaponFive = Instance("equipment-instance.playmode-5", weaponD);
                Armor = Instance("equipment-instance.playmode-armor", armorDefinition);
                Add(WeaponOne); Add(AlternateWeapon); Add(weaponThree); Add(weaponFour); Add(weaponFive); Add(Armor);
                RoutePayload = PlayerRouteProfilePayload.Create(
                    Id("character.playmode"),
                    Id("loadout-profile.playmode"),
                    new[] { WeaponOne.InstanceId, weaponThree.InstanceId, weaponFour.InstanceId, weaponFive.InstanceId });
                Loadout = new RecordingLoadoutState(RoutePayload);
            }

            public CatalogBridge Catalog { get; }
            public PlayerHoldingsActions Holdings { get; }
            public RecordingLoadoutState Loadout { get; }
            public PlayerRouteProfilePayload RoutePayload { get; }
            public EquipmentInstance WeaponOne { get; }
            public EquipmentInstance AlternateWeapon { get; }
            public EquipmentInstance Armor { get; }

            private void Add(EquipmentInstance instance)
            {
                ordinal++;
                PlayerHoldingsMutationResult result = Holdings.Apply(PlayerHoldingsCommand.AddEquipment(
                    Id("transaction.playmode-add-" + ordinal),
                    Id("operation.playmode-add-" + ordinal),
                    AuthorityId,
                    instance,
                    HoldingProvenance.Create(Id("grant.playmode-add-" + ordinal), Id("source.playmode-fixture")),
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
            public CatalogBridge(EquipmentCatalog catalog) { Catalog = catalog; }
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
                if (command.ExpectedSequence != Snapshot.Sequence)
                {
                    return new InventoryLoadoutStateResult(InventoryLoadoutStateMutationStatus.StaleSnapshot, "sequence-stale", Snapshot);
                }
                Snapshot = InventoryLoadoutStateSnapshot.CreateCanonical(Snapshot.Sequence + 1L, command.Bindings);
                return new InventoryLoadoutStateResult(InventoryLoadoutStateMutationStatus.Applied, string.Empty, Snapshot);
            }
        }
    }
}
