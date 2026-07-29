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
    public sealed class InventoryMenuTests
    {
        [UnityTest]
        public IEnumerator ConfirmReturnsNewPayloadWithExactConcreteIdentity()
        {
            Fixture fixture = new Fixture();
            GameObject host = new GameObject("INV-002 Confirm Test");
            InventoryMenu controller = host.AddComponent<InventoryMenu>();
            PlayerRouteProfilePayload returned = null;
            controller.ConfigureForTests(fixture.Holdings, fixture.Catalog, fixture.Loadout, delegate(PlayerRouteProfilePayload payload) { returned = payload; });
            controller.Present(HubRoute.Inventory, fixture.RoutePayload);
            yield return null;

            controller.SelectSlot(InventoryLoadoutSlotIds.GunTwo);
            Assert.That(controller.SelectInstance(fixture.AlternateGun.InstanceId).ChangedSelection, Is.True);
            controller.SelectSlot(InventoryLoadoutSlotIds.ArmorHead);
            Assert.That(controller.SelectInstance(fixture.Armor.InstanceId).ChangedSelection, Is.True);
            InventoryLoadoutScreenResult result = controller.Confirm();

            Assert.That(result.Status, Is.EqualTo(InventoryLoadoutScreenStatus.Confirmed));
            Assert.That(returned, Is.SameAs(result.RoutePayload));
            Assert.That(returned, Is.Not.SameAs(fixture.RoutePayload));
            Assert.That(returned.GunSlots[1].EquipmentInstanceStableId, Is.EqualTo(fixture.AlternateGun.InstanceId));
            Assert.That(controller.ReturnCount, Is.EqualTo(1));
            Assert.That(fixture.Loadout.ApplyCount, Is.EqualTo(1));
            UnityEngine.Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator BackReturnsSamePayloadOnlyOnce()
        {
            Fixture fixture = new Fixture();
            GameObject host = new GameObject("INV-002 Back Test");
            InventoryMenu controller = host.AddComponent<InventoryMenu>();
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
        public IEnumerator RevisitProjectsConfirmedGunAndArmorInstances()
        {
            Fixture fixture = new Fixture();
            GameObject host = new GameObject("INV-002 Revisit Test");
            InventoryMenu controller = host.AddComponent<InventoryMenu>();
            controller.ConfigureForTests(fixture.Holdings, fixture.Catalog, fixture.Loadout, null);
            controller.Present(HubRoute.Inventory, fixture.RoutePayload);
            controller.SelectSlot(InventoryLoadoutSlotIds.GunTwo);
            controller.SelectInstance(fixture.AlternateGun.InstanceId);
            controller.SelectSlot(InventoryLoadoutSlotIds.ArmorHead);
            controller.SelectInstance(fixture.Armor.InstanceId);
            PlayerRouteProfilePayload confirmed = controller.Confirm().RoutePayload;
            yield return null;

            controller.Present(HubRoute.Inventory, confirmed);
            yield return null;

            Assert.That(controller.Snapshot.GetSelection(InventoryLoadoutSlotIds.GunTwo).EquipmentInstanceStableId,
                Is.EqualTo(fixture.AlternateGun.InstanceId));
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
                EquipmentDefinition shared = Definition("equipment.playmode-shared", EquipmentCategoryIds.Gun, "Shared", common);
                EquipmentDefinition gunB = Definition("equipment.playmode-b", EquipmentCategoryIds.Gun, "B", common);
                EquipmentDefinition gunC = Definition("equipment.playmode-c", EquipmentCategoryIds.Gun, "C", common);
                EquipmentDefinition gunD = Definition("equipment.playmode-d", EquipmentCategoryIds.Gun, "D", common);
                EquipmentDefinition armorDefinition = Definition("equipment.playmode-armor", EquipmentCategoryIds.Armor, "Armor", common);
                EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                    new[] { shared, gunB, gunC, gunD, armorDefinition },
                    new AugmentDefinition[0]);
                Assert.That(build.IsValid, Is.True);
                Catalog = new CatalogBridge(build.Catalog);
                Holdings = new PlayerHoldingsActions(AuthorityId, 1000L, Catalog);
                GunOne = Instance("equipment-instance.playmode-1", shared);
                AlternateGun = Instance("equipment-instance.playmode-2", shared);
                EquipmentInstance gunThree = Instance("equipment-instance.playmode-3", gunB);
                EquipmentInstance gunFour = Instance("equipment-instance.playmode-4", gunC);
                EquipmentInstance gunFive = Instance("equipment-instance.playmode-5", gunD);
                Armor = Instance("equipment-instance.playmode-armor", armorDefinition);
                Add(GunOne); Add(AlternateGun); Add(gunThree); Add(gunFour); Add(gunFive); Add(Armor);
                RoutePayload = PlayerRouteProfilePayload.Create(
                    Id("character.playmode"),
                    Id("loadout-profile.playmode"),
                    new[] { GunOne.InstanceId, gunThree.InstanceId, gunFour.InstanceId, gunFive.InstanceId });
                Loadout = new RecordingLoadoutState(RoutePayload);
            }

            public CatalogBridge Catalog { get; }
            public PlayerHoldingsActions Holdings { get; }
            public RecordingLoadoutState Loadout { get; }
            public PlayerRouteProfilePayload RoutePayload { get; }
            public EquipmentInstance GunOne { get; }
            public EquipmentInstance AlternateGun { get; }
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
