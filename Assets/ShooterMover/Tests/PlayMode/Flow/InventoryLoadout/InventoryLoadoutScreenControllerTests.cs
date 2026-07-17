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
            InventoryLoadoutScreenControllerV1 controller = host.AddComponent<InventoryLoadoutScreenControllerV1>();
            PlayerRouteProfilePayloadV1 returned = null;
            controller.ConfigureForTests(fixture.Holdings, fixture.Catalog, fixture.Loadout, delegate(PlayerRouteProfilePayloadV1 payload) { returned = payload; });
            controller.Present(HubRouteV1.Inventory, fixture.RoutePayload);
            yield return null;

            controller.SelectSlot(InventoryLoadoutSlotIdsV1.WeaponTwo);
            Assert.That(controller.SelectInstance(fixture.AlternateWeapon.InstanceId).ChangedSelection, Is.True);
            controller.SelectSlot(InventoryLoadoutSlotIdsV1.ArmorHead);
            Assert.That(controller.SelectInstance(fixture.Armor.InstanceId).ChangedSelection, Is.True);
            InventoryLoadoutScreenResultV1 result = controller.Confirm();

            Assert.That(result.Status, Is.EqualTo(InventoryLoadoutScreenStatusV1.Confirmed));
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
            InventoryLoadoutScreenControllerV1 controller = host.AddComponent<InventoryLoadoutScreenControllerV1>();
            PlayerRouteProfilePayloadV1 returned = null;
            controller.ConfigureForTests(fixture.Holdings, fixture.Catalog, fixture.Loadout, delegate(PlayerRouteProfilePayloadV1 payload) { returned = payload; });
            controller.Present(HubRouteV1.Inventory, fixture.RoutePayload);
            yield return null;

            InventoryLoadoutScreenResultV1 first = controller.Back();
            InventoryLoadoutScreenResultV1 second = controller.Back();

            Assert.That(first.Status, Is.EqualTo(InventoryLoadoutScreenStatusV1.Cancelled));
            Assert.That(second.Status, Is.EqualTo(InventoryLoadoutScreenStatusV1.AlreadyCompleted));
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
            InventoryLoadoutScreenControllerV1 controller = host.AddComponent<InventoryLoadoutScreenControllerV1>();
            controller.ConfigureForTests(fixture.Holdings, fixture.Catalog, fixture.Loadout, null);
            controller.Present(HubRouteV1.Inventory, fixture.RoutePayload);
            controller.SelectSlot(InventoryLoadoutSlotIdsV1.WeaponTwo);
            controller.SelectInstance(fixture.AlternateWeapon.InstanceId);
            controller.SelectSlot(InventoryLoadoutSlotIdsV1.ArmorHead);
            controller.SelectInstance(fixture.Armor.InstanceId);
            PlayerRouteProfilePayloadV1 confirmed = controller.Confirm().RoutePayload;
            yield return null;

            controller.Present(HubRouteV1.Inventory, confirmed);
            yield return null;

            Assert.That(controller.Snapshot.GetSelection(InventoryLoadoutSlotIdsV1.WeaponTwo).EquipmentInstanceStableId,
                Is.EqualTo(fixture.AlternateWeapon.InstanceId));
            Assert.That(controller.Snapshot.GetSelection(InventoryLoadoutSlotIdsV1.ArmorHead).EquipmentInstanceStableId,
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
                Catalog = new CatalogAdapter(build.Catalog);
                Holdings = new PlayerHoldingsService(AuthorityId, 1000L, Catalog);
                WeaponOne = Instance("equipment-instance.playmode-1", shared);
                AlternateWeapon = Instance("equipment-instance.playmode-2", shared);
                EquipmentInstance weaponThree = Instance("equipment-instance.playmode-3", weaponB);
                EquipmentInstance weaponFour = Instance("equipment-instance.playmode-4", weaponC);
                EquipmentInstance weaponFive = Instance("equipment-instance.playmode-5", weaponD);
                Armor = Instance("equipment-instance.playmode-armor", armorDefinition);
                Add(WeaponOne); Add(AlternateWeapon); Add(weaponThree); Add(weaponFour); Add(weaponFive); Add(Armor);
                RoutePayload = PlayerRouteProfilePayloadV1.Create(
                    Id("character.playmode"),
                    Id("loadout-profile.playmode"),
                    new[] { WeaponOne.InstanceId, weaponThree.InstanceId, weaponFour.InstanceId, weaponFive.InstanceId });
                Loadout = new RecordingLoadoutAuthority(RoutePayload);
            }

            public CatalogAdapter Catalog { get; }
            public PlayerHoldingsService Holdings { get; }
            public RecordingLoadoutAuthority Loadout { get; }
            public PlayerRouteProfilePayloadV1 RoutePayload { get; }
            public EquipmentInstance WeaponOne { get; }
            public EquipmentInstance AlternateWeapon { get; }
            public EquipmentInstance Armor { get; }

            private void Add(EquipmentInstance instance)
            {
                ordinal++;
                PlayerHoldingsMutationResultV1 result = Holdings.Apply(PlayerHoldingsCommandV1.AddEquipment(
                    Id("transaction.playmode-add-" + ordinal),
                    Id("operation.playmode-add-" + ordinal),
                    AuthorityId,
                    instance,
                    HoldingProvenanceV1.Create(Id("grant.playmode-add-" + ordinal), Id("source.playmode-fixture")),
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
            public CatalogAdapter(EquipmentCatalog catalog) { Catalog = catalog; }
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
                if (command.ExpectedSequence != Snapshot.Sequence)
                {
                    return new InventoryLoadoutAuthorityResultV1(InventoryLoadoutAuthorityMutationStatusV1.StaleSnapshot, "sequence-stale", Snapshot);
                }
                Snapshot = InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(Snapshot.Sequence + 1L, command.Bindings);
                return new InventoryLoadoutAuthorityResultV1(InventoryLoadoutAuthorityMutationStatusV1.Applied, string.Empty, Snapshot);
            }
        }
    }
}
