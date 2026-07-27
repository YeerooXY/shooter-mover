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
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class ProductionPlayerLoadoutRuntimeV1Tests
    {
        [TestCase(ProductionWeaponMountPolicyV1.AggressiveLoadoutProfileId, 3, 2, 1)]
        [TestCase(ProductionWeaponMountPolicyV1.HealerLoadoutProfileId, 3, 3, 0)]
        [TestCase(ProductionWeaponMountPolicyV1.DefensiveLoadoutProfileId, 4, 4, 0)]
        public void FreshCharacterCreatesOneDistinctCanonicalStarterPerActiveMount(
            string profileId,
            int expectedPhysical,
            int expectedActive,
            int expectedLocked)
        {
            PlayerRouteProfilePayloadV1 draft = Route(
                "starter-" + expectedActive,
                profileId);

            var runtime = new ProductionPlayerLoadoutRuntimeV1(draft);
            WeaponHoldingsSnapshotV2 weapons =
                runtime.WeaponHoldings.ExportSnapshot();
            InventoryLoadoutAuthoritySnapshotV1 loadout =
                runtime.LoadoutAuthority.ExportSnapshot();

            Assert.That(runtime.MountLayout.PhysicalMountCount, Is.EqualTo(expectedPhysical));
            Assert.That(runtime.MountLayout.ActiveMountCount, Is.EqualTo(expectedActive));
            Assert.That(runtime.MountLayout.LockedBySkillMountCount, Is.EqualTo(expectedLocked));
            Assert.That(weapons.Instances.Count, Is.EqualTo(expectedActive));
            Assert.That(
                weapons.Instances.Select(item => item.InstanceId).Distinct().Count(),
                Is.EqualTo(expectedActive));
            Assert.That(
                weapons.Instances.All(item =>
                    item.WeaponDefinitionId.Value
                    == ProductionWeaponOnboardingV1.StarterWeaponDefinitionId),
                Is.True);
            Assert.That(
                loadout.Bindings.Count(item =>
                    item.EquipmentInstanceStableId != null),
                Is.EqualTo(expectedActive));
            Assert.That(
                loadout.Bindings
                    .Where(item => item.EquipmentInstanceStableId != null)
                    .All(item => weapons.Find(item.EquipmentInstanceStableId) != null),
                Is.True);
            Assert.That(
                runtime.LegacyHoldings.ExportSnapshot().UniqueHoldings,
                Is.Empty,
                "Fresh starters are canonical holdings, not generic reward grants.");
            Assert.That(
                draft.WeaponSlots.All(item => !item.IsBound),
                Is.True,
                "Character creation must not mutate the incoming route payload.");
        }

        [Test]
        public void AggressiveLockedCenterIsVisibleEmptyAndRejectsEquip()
        {
            var runtime = new ProductionPlayerLoadoutRuntimeV1(Route(
                "aggressive-locked",
                ProductionWeaponMountPolicyV1.AggressiveLoadoutProfileId));
            var service = CanonicalService(runtime);
            CanonicalWeaponInventoryMountV2 center = service.Snapshot.Mounts.Single(
                item => item.Position.MountStableId
                    == ProductionWeaponMountPolicyV1.CenterMountStableId);
            StableId selected = service.Snapshot.OwnedWeapons[0].Instance.InstanceId;

            Assert.That(center.Position.IsLockedBySkill, Is.True);
            Assert.That(center.EquippedInstanceId, Is.Null);
            Assert.That(
                service.SelectWeapon(selected).Status,
                Is.AnyOf(
                    InventoryLoadoutScreenStatusV1.SelectionChanged,
                    InventoryLoadoutScreenStatusV1.NoChange));
            InventoryLoadoutScreenResultV1 result = service.EquipSelected(
                center.Position.LoadoutSlotStableId);

            Assert.That(result.Status, Is.EqualTo(InventoryLoadoutScreenStatusV1.InvalidSlot));
            Assert.That(result.RejectionCode, Does.Contain("skill"));
            Assert.That(service.Snapshot.FindMount(center.Position.LoadoutSlotStableId)
                .EquippedInstanceId, Is.Null);
        }

        [Test]
        public void OpeningAndRefreshingInventoryNeverGrantsWeapons()
        {
            var runtime = new ProductionPlayerLoadoutRuntimeV1(Route(
                "inventory-no-grant",
                ProductionWeaponMountPolicyV1.HealerLoadoutProfileId));
            WeaponHoldingsSnapshotV2 before = runtime.WeaponHoldings.ExportSnapshot();

            var first = CanonicalService(runtime);
            first.Refresh();
            var second = CanonicalService(runtime);
            second.Refresh();
            WeaponHoldingsSnapshotV2 after = runtime.WeaponHoldings.ExportSnapshot();

            Assert.That(after.Sequence, Is.EqualTo(before.Sequence));
            Assert.That(after.Fingerprint, Is.EqualTo(before.Fingerprint));
            Assert.That(after.Instances.Count, Is.EqualTo(3));
        }

        [Test]
        public void UnequipReequipAndReplacementPreserveExactOwnership()
        {
            var runtime = new ProductionPlayerLoadoutRuntimeV1(Route(
                "exact-equip",
                ProductionWeaponMountPolicyV1.DefensiveLoadoutProfileId));
            WeaponHoldingsSnapshotV2 heldBefore =
                runtime.WeaponHoldings.ExportSnapshot();
            StableId firstMount = runtime.MountLayout.Positions[0].LoadoutSlotStableId;
            StableId secondMount = runtime.MountLayout.Positions[1].LoadoutSlotStableId;
            StableId instanceA = runtime.LoadoutAuthority.ExportSnapshot()
                .GetBinding(firstMount).EquipmentInstanceStableId;
            StableId instanceB = runtime.LoadoutAuthority.ExportSnapshot()
                .GetBinding(secondMount).EquipmentInstanceStableId;

            var unequip = CanonicalService(runtime);
            Assert.That(
                unequip.Unequip(firstMount).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.SelectionChanged));
            Assert.That(
                unequip.Confirm().Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.Confirmed));
            Assert.That(runtime.LoadoutAuthority.ExportSnapshot()
                .GetBinding(firstMount).EquipmentInstanceStableId, Is.Null);
            Assert.That(runtime.WeaponHoldings.Find(instanceA), Is.Not.Null);

            var reequip = CanonicalService(runtime);
            reequip.SelectWeapon(instanceA);
            Assert.That(
                reequip.EquipSelected(firstMount).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.SelectionChanged));
            Assert.That(
                reequip.Confirm().Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.Confirmed));
            Assert.That(runtime.LoadoutAuthority.ExportSnapshot()
                .GetBinding(firstMount).EquipmentInstanceStableId,
                Is.EqualTo(instanceA));

            var replace = CanonicalService(runtime);
            Assert.That(
                replace.Unequip(secondMount).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.SelectionChanged));
            replace.SelectWeapon(instanceB);
            Assert.That(
                replace.EquipSelected(firstMount).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.SelectionChanged));
            Assert.That(
                replace.Confirm().Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.Confirmed));

            Assert.That(runtime.LoadoutAuthority.ExportSnapshot()
                .GetBinding(firstMount).EquipmentInstanceStableId,
                Is.EqualTo(instanceB));
            Assert.That(runtime.LoadoutAuthority.ExportSnapshot()
                .GetBinding(secondMount).EquipmentInstanceStableId,
                Is.Null);
            Assert.That(runtime.WeaponHoldings.Find(instanceA), Is.Not.Null);
            Assert.That(runtime.WeaponHoldings.Find(instanceB), Is.Not.Null);
            Assert.That(
                runtime.WeaponHoldings.ExportSnapshot().Fingerprint,
                Is.EqualTo(heldBefore.Fingerprint));

            WeaponEquipmentInstance gameplay;
            string rejectionCode;
            Assert.That(
                runtime.TryResolveFirstActiveEquippedWeapon(
                    out gameplay,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(gameplay.InstanceId, Is.EqualTo(instanceB));
        }

        [Test]
        public void DuplicateDefinitionsRemainDistinctSelectableCards()
        {
            var runtime = new ProductionPlayerLoadoutRuntimeV1(Route(
                "duplicates",
                ProductionWeaponMountPolicyV1.HealerLoadoutProfileId));
            var service = CanonicalService(runtime);

            Assert.That(service.Snapshot.OwnedWeapons.Count, Is.EqualTo(3));
            Assert.That(
                service.Snapshot.OwnedWeapons.Select(item =>
                    item.Instance.WeaponDefinitionId.Value).Distinct().Count(),
                Is.EqualTo(1));
            Assert.That(
                service.Snapshot.OwnedWeapons.Select(item =>
                    item.Instance.InstanceId).Distinct().Count(),
                Is.EqualTo(3));
        }

        [Test]
        public void CanonicalSnapshotCodecRoundTripsExactInstancesAndAssignments()
        {
            WeaponEquipmentInstance instance = WeaponEquipmentInstance.Create(
                Id("instance.codec-exact"),
                new WeaponDefinitionId(
                    ProductionWeaponOnboardingV1.StarterWeaponDefinitionId),
                new[] { Id("augment-assignment.alpha") },
                new[] { Id("overclock-assignment.beta") });
            WeaponHoldingsSnapshotV2 source =
                WeaponHoldingsSnapshotV2.CreateCanonical(7L, new[] { instance });
            var codec = new WeaponHoldingsComponentCodecV2();

            string payload = codec.Encode(source);
            WeaponHoldingsSnapshotV2 decoded;
            string rejectionCode;
            Assert.That(
                codec.TryDecode(payload, out decoded, out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(decoded.Sequence, Is.EqualTo(7L));
            Assert.That(decoded.Fingerprint, Is.EqualTo(source.Fingerprint));
            Assert.That(decoded.Instances.Single(), Is.EqualTo(instance));
        }

        [Test]
        public void LegacyMigrationIsDeterministicAndPreservesOpaqueInstanceId()
        {
            var legacy = new PlayerHoldingsService(
                Id("authority.legacy-migration"),
                99L,
                new ProductionEquipmentCatalogAdapterV1(
                    ProductionWeaponCatalogProvider.EquipmentCatalog));
            EquipmentDefinition definition =
                ProductionWeaponCatalogProvider.EquipmentCatalog
                    .FindEquipmentDefinition(
                        Id("equipment.weapon-rattler-mk1"));
            StableId exactId = Id("instance.legacy-opaque-7f4a");
            EquipmentInstance equipment = EquipmentInstance.Create(
                exactId,
                definition.DefinitionId,
                definition.ItemLevelRange.Minimum,
                definition.QualityTiers[0].QualityId,
                Array.Empty<AugmentInstance>());
            PlayerHoldingsMutationResultV1 grant = legacy.Apply(
                PlayerHoldingsCommandV1.AddEquipment(
                    Id("transaction.legacy-migration"),
                    Id("operation.legacy-migration"),
                    legacy.AuthorityStableId,
                    equipment,
                    HoldingProvenanceV1.Create(
                        Id("grant.legacy-migration"),
                        Id("source.legacy-save")),
                    legacy.Sequence));
            Assert.That(grant.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));

            PlayerHoldingsSnapshotV1 receiptLedger = legacy.ExportSnapshot();
            WeaponHoldingsSnapshotV2 first =
                ProductionWeaponHoldingsMigrationV2.ConvertLegacy(receiptLedger);
            WeaponHoldingsSnapshotV2 second =
                ProductionWeaponHoldingsMigrationV2.ConvertLegacy(receiptLedger);

            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.That(first.Instances.Single().InstanceId, Is.EqualTo(exactId));
            Assert.That(
                legacy.ExportSnapshot().Fingerprint,
                Is.EqualTo(receiptLedger.Fingerprint),
                "Migration must not rewrite immutable generic reward receipts.");
        }

        [Test]
        public void CharacterWeaponOwnershipIsIsolated()
        {
            var first = new ProductionPlayerLoadoutRuntimeV1(Route(
                "first-character",
                ProductionWeaponMountPolicyV1.DefensiveLoadoutProfileId));
            var second = new ProductionPlayerLoadoutRuntimeV1(Route(
                "second-character",
                ProductionWeaponMountPolicyV1.DefensiveLoadoutProfileId));

            StableId[] firstIds = first.WeaponHoldings.ExportSnapshot()
                .Instances.Select(item => item.InstanceId).ToArray();
            StableId[] secondIds = second.WeaponHoldings.ExportSnapshot()
                .Instances.Select(item => item.InstanceId).ToArray();

            Assert.That(firstIds.Intersect(secondIds), Is.Empty);
        }

        private static CanonicalWeaponInventoryScreenServiceV2 CanonicalService(
            ProductionPlayerLoadoutRuntimeV1 runtime)
        {
            return new CanonicalWeaponInventoryScreenServiceV2(
                runtime.CurrentRoutePayload,
                runtime.Holdings,
                runtime.WeaponHoldings,
                runtime.LoadoutAuthority,
                runtime.MountLayout,
                runtime.WeaponCatalog);
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
