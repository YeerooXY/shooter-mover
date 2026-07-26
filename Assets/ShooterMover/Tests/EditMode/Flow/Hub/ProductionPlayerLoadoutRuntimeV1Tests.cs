using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class ProductionPlayerLoadoutRuntimeV1Tests
    {
        [TestCase(ProductionWeaponMountPolicyV1.AggressiveLoadoutProfileId, 2)]
        [TestCase(ProductionWeaponMountPolicyV1.HealerLoadoutProfileId, 3)]
        [TestCase(ProductionWeaponMountPolicyV1.DefensiveLoadoutProfileId, 4)]
        public void StarterRuntimeOwnsExactlyRequiredFreshInstances(
            string loadoutProfileId,
            int expectedMounts)
        {
            PlayerRouteProfilePayloadV1 draft = Route(
                "starter-" + expectedMounts,
                loadoutProfileId);

            var runtime = new ProductionPlayerLoadoutRuntimeV1(draft);
            PlayerHoldingsSnapshotV1 holdings = runtime.Holdings.ExportSnapshot();
            StableId[] owned = holdings.UniqueHoldings
                .Where(item => item.RewardKind
                    == RewardGrantKindV1.EquipmentReference)
                .Select(item => item.InstanceStableId)
                .ToArray();
            StableId[] equipped = runtime.LoadoutAuthority.ExportSnapshot()
                .Bindings
                .Where(item => item.EquipmentInstanceStableId != null)
                .Select(item => item.EquipmentInstanceStableId)
                .ToArray();

            Assert.That(owned.Length, Is.EqualTo(expectedMounts));
            Assert.That(owned.Distinct().Count(), Is.EqualTo(expectedMounts));
            Assert.That(equipped.Length, Is.EqualTo(expectedMounts));
            Assert.That(equipped.Distinct().Count(), Is.EqualTo(expectedMounts));
            Assert.That(equipped.All(owned.Contains), Is.True);
            Assert.That(
                holdings.UniqueHoldings.All(item =>
                    item.DefinitionStableId
                    == Id("equipment.weapon-rattler-mk1")),
                Is.True);
            Assert.That(
                holdings.UniqueHoldings.Count,
                Is.LessThan(runtime.EquipmentCatalog.EquipmentDefinitions.Count));
            Assert.That(
                draft.WeaponSlots.All(item => !item.IsBound),
                Is.True,
                "The navigation payload must not be mutated into ownership.");
        }

        [Test]
        public void CreatingAnotherCharacterDoesNotReuseInstanceIds()
        {
            var first = new ProductionPlayerLoadoutRuntimeV1(Route(
                "first-character",
                ProductionWeaponMountPolicyV1.DefensiveLoadoutProfileId));
            var second = new ProductionPlayerLoadoutRuntimeV1(Route(
                "second-character",
                ProductionWeaponMountPolicyV1.DefensiveLoadoutProfileId));

            StableId[] firstIds = first.Holdings.ExportSnapshot()
                .UniqueHoldings.Select(item => item.InstanceStableId).ToArray();
            StableId[] secondIds = second.Holdings.ExportSnapshot()
                .UniqueHoldings.Select(item => item.InstanceStableId).ToArray();

            Assert.That(firstIds.Intersect(secondIds), Is.Empty);
        }

        [Test]
        public void InventoryScreenOpensAfterFreshCharacterCreation()
        {
            var runtime = new ProductionPlayerLoadoutRuntimeV1(Route(
                "inventory-open",
                ProductionWeaponMountPolicyV1.HealerLoadoutProfileId));

            var service = new InventoryLoadoutScreenServiceV1(
                runtime.RoutePayload,
                runtime.Holdings,
                runtime.CatalogAdapter,
                runtime.LoadoutAuthority);

            Assert.That(service.Snapshot, Is.Not.Null);
            Assert.That(
                service.Snapshot.Selections.Count(item =>
                    item.EquipmentInstanceStableId != null),
                Is.EqualTo(3));
        }

        [Test]
        public void LoadoutSurvivesExactHoldingsRestore()
        {
            var first = new ProductionPlayerLoadoutRuntimeV1(Route(
                "restore",
                ProductionWeaponMountPolicyV1.DefensiveLoadoutProfileId));
            PlayerHoldingsSnapshotV1 holdings = first.Holdings.ExportSnapshot();
            InventoryLoadoutAuthoritySnapshotV1 loadout =
                first.LoadoutAuthority.ExportSnapshot();

            ProductionPlayerLoadoutRuntimeV1 restored =
                ProductionPlayerLoadoutRuntimeV1.Restore(
                    first.RoutePayload.SelectedCharacterStableId,
                    first.RoutePayload.LoadoutProfileStableId,
                    holdings,
                    loadout);

            Assert.That(
                restored.Holdings.ExportSnapshot().Fingerprint,
                Is.EqualTo(holdings.Fingerprint));
            Assert.That(
                restored.LoadoutAuthority.ExportSnapshot().Fingerprint,
                Is.EqualTo(loadout.Fingerprint));
        }

        [Test]
        public void EquippedOwnedInstancesResolveIntoLiveWeaponDefinitions()
        {
            var runtime = new ProductionPlayerLoadoutRuntimeV1(Route(
                "live-resolution",
                ProductionWeaponMountPolicyV1.DefensiveLoadoutProfileId));
            PlayerHoldingsSnapshotV1 holdings = runtime.Holdings.ExportSnapshot();
            Dictionary<StableId, UniqueHoldingSnapshotV1> owned = holdings
                .UniqueHoldings.ToDictionary(
                    item => item.InstanceStableId,
                    item => item);

            foreach (InventoryLoadoutSlotBindingV1 binding in
                runtime.LoadoutAuthority.ExportSnapshot().Bindings)
            {
                if (binding.EquipmentInstanceStableId == null)
                {
                    continue;
                }

                UniqueHoldingSnapshotV1 holding;
                Assert.That(
                    owned.TryGetValue(
                        binding.EquipmentInstanceStableId,
                        out holding),
                    Is.True);
                EquipmentDefinition equipmentDefinition =
                    runtime.EquipmentCatalog.FindEquipmentDefinition(
                        holding.DefinitionStableId);
                Assert.That(equipmentDefinition, Is.Not.Null);
                WeaponDefinitionData weaponDefinition;
                Assert.That(
                    runtime.WeaponCatalog.TryGetDefinition(
                        equipmentDefinition.RuntimeWeaponReferenceId.ToString(),
                        out weaponDefinition),
                    Is.True);
                Assert.That(weaponDefinition, Is.Not.Null);
            }
        }

        [Test]
        public void DirectDuplicateInstanceCommandRejectsWithoutMutation()
        {
            var runtime = new ProductionPlayerLoadoutRuntimeV1(Route(
                "duplicate",
                ProductionWeaponMountPolicyV1.DefensiveLoadoutProfileId));
            InventoryLoadoutAuthoritySnapshotV1 before =
                runtime.LoadoutAuthority.ExportSnapshot();
            var bindings = CopyBindings(before);
            bindings[1] = new InventoryLoadoutSlotBindingV1(
                InventoryLoadoutSlotIdsV1.WeaponTwo,
                bindings[0].EquipmentInstanceStableId);

            InventoryLoadoutAuthorityResultV1 result =
                runtime.LoadoutAuthority.Apply(
                    new InventoryLoadoutAuthorityCommandV1(
                        before.Sequence,
                        runtime.Holdings.Sequence,
                        bindings));

            Assert.That(
                result.Status,
                Is.EqualTo(
                    InventoryLoadoutAuthorityMutationStatusV1.Rejected));
            Assert.That(
                result.RejectionCode,
                Is.EqualTo("production-loadout-instance-duplicate"));
            Assert.That(
                runtime.LoadoutAuthority.ExportSnapshot().Sequence,
                Is.EqualTo(before.Sequence));
        }

        [Test]
        public void HoldingsChangeMakesPreparedLoadoutCommandStale()
        {
            var runtime = new ProductionPlayerLoadoutRuntimeV1(Route(
                "stale",
                ProductionWeaponMountPolicyV1.DefensiveLoadoutProfileId));
            InventoryLoadoutAuthoritySnapshotV1 before =
                runtime.LoadoutAuthority.ExportSnapshot();
            var command = new InventoryLoadoutAuthorityCommandV1(
                before.Sequence,
                runtime.Holdings.Sequence,
                CopyBindings(before));

            AddExtraCurrentWeapon(runtime);
            InventoryLoadoutAuthorityResultV1 result =
                runtime.LoadoutAuthority.Apply(command);

            Assert.That(
                result.Status,
                Is.EqualTo(
                    InventoryLoadoutAuthorityMutationStatusV1.StaleSnapshot));
            Assert.That(
                result.RejectionCode,
                Is.EqualTo("production-loadout-holdings-stale"));
            Assert.That(result.Snapshot.Sequence, Is.EqualTo(before.Sequence));
        }

        private static List<InventoryLoadoutSlotBindingV1> CopyBindings(
            InventoryLoadoutAuthoritySnapshotV1 snapshot)
        {
            return snapshot.Bindings.Select(item =>
                new InventoryLoadoutSlotBindingV1(
                    item.SlotStableId,
                    item.EquipmentInstanceStableId)).ToList();
        }

        private static void AddExtraCurrentWeapon(
            ProductionPlayerLoadoutRuntimeV1 runtime)
        {
            EquipmentDefinition definition = runtime.EquipmentCatalog
                .FindEquipmentDefinition(Id("equipment.weapon-rattler-mk1"));
            Assert.That(definition, Is.Not.Null);
            EquipmentInstance instance = EquipmentInstance.Create(
                Id("equipment-instance.test-extra-rattler"),
                definition.DefinitionId,
                definition.ItemLevelRange.Minimum,
                definition.QualityTiers[0].QualityId,
                Array.Empty<AugmentInstance>());
            PlayerHoldingsMutationResultV1 result = runtime.Holdings.Apply(
                PlayerHoldingsCommandV1.AddEquipment(
                    Id("transaction.test-extra-rattler"),
                    Id("operation.test-extra-rattler"),
                    runtime.Holdings.AuthorityStableId,
                    instance,
                    HoldingProvenanceV1.Create(
                        Id("grant.test-extra-rattler"),
                        Id("source.production-loadout-test")),
                    runtime.Holdings.Sequence));
            Assert.That(
                result.Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
        }

        private static PlayerRouteProfilePayloadV1 Route(
            string suffix,
            string loadoutProfileId)
        {
            return PlayerRouteProfilePayloadV1.Create(
                Id("character." + suffix),
                Id(loadoutProfileId),
                new StableId[PlayerRouteProfilePayloadV1.WeaponSlotCount]);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
