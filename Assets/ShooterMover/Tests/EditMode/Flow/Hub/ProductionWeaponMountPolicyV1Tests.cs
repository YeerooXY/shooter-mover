using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class ProductionWeaponMountPolicyV1Tests
    {
        [Test]
        public void ProductionClassesExposeClassCorrectPhysicalAndActiveMounts()
        {
            ProductionWeaponMountLayoutV1 aggressive =
                ProductionWeaponMountPolicyV1.ResolveLayout(
                    StableId.Parse(
                        ProductionWeaponMountPolicyV1
                            .AggressiveLoadoutProfileId));
            ProductionWeaponMountLayoutV1 healer =
                ProductionWeaponMountPolicyV1.ResolveLayout(
                    StableId.Parse(
                        ProductionWeaponMountPolicyV1
                            .HealerLoadoutProfileId));
            ProductionWeaponMountLayoutV1 defensive =
                ProductionWeaponMountPolicyV1.ResolveLayout(
                    StableId.Parse(
                        ProductionWeaponMountPolicyV1
                            .DefensiveLoadoutProfileId));

            Assert.That(aggressive.PhysicalMountCount, Is.EqualTo(3));
            Assert.That(aggressive.ActiveMountCount, Is.EqualTo(2));
            Assert.That(aggressive.LockedBySkillMountCount, Is.EqualTo(1));
            Assert.That(aggressive.BaselineEnabledMountCount, Is.EqualTo(2));

            Assert.That(healer.PhysicalMountCount, Is.EqualTo(3));
            Assert.That(healer.ActiveMountCount, Is.EqualTo(3));
            Assert.That(healer.LockedBySkillMountCount, Is.EqualTo(0));

            Assert.That(defensive.PhysicalMountCount, Is.EqualTo(4));
            Assert.That(defensive.ActiveMountCount, Is.EqualTo(4));
            Assert.That(defensive.LockedBySkillMountCount, Is.EqualTo(0));
        }

        [Test]
        public void BuiltInCharacterSpecificProfilesResolveByClassSuffix()
        {
            ProductionWeaponMountLayoutV1 aggressive =
                ProductionWeaponMountPolicyV1.ResolveLayout(
                    StableId.Parse(
                        "loadout-profile.frontier-vanguard-aggressive"));
            ProductionWeaponMountLayoutV1 healer =
                ProductionWeaponMountPolicyV1.ResolveLayout(
                    StableId.Parse(
                        "loadout-profile.custom-pilot-healer"));
            ProductionWeaponMountLayoutV1 defensive =
                ProductionWeaponMountPolicyV1.ResolveLayout(
                    StableId.Parse(
                        "loadout-profile.frontier-vanguard-defensive"));

            Assert.That(aggressive.PhysicalMountCount, Is.EqualTo(3));
            Assert.That(aggressive.ActiveMountCount, Is.EqualTo(2));
            Assert.That(aggressive.LockedBySkillMountCount, Is.EqualTo(1));
            Assert.That(healer.ActiveMountCount, Is.EqualTo(3));
            Assert.That(defensive.ActiveMountCount, Is.EqualTo(4));
        }

        [Test]
        public void AggressiveProjectsTwoActiveAndOneSkillLockedPhysicalMount()
        {
            PlayerRouteProfilePayloadV1 normalized =
                ProductionWeaponMountPolicyV1.NormalizeRoutePayload(
                    Route(
                        ProductionWeaponMountPolicyV1
                            .AggressiveLoadoutProfileId,
                        "aggressive"));

            Assert.That(normalized.WeaponSlots[0].IsBound, Is.True);
            Assert.That(normalized.WeaponSlots[1].IsBound, Is.False);
            Assert.That(normalized.WeaponSlots[2].IsBound, Is.False);
            Assert.That(normalized.WeaponSlots[3].IsBound, Is.True);

            PlayerRouteProfileValidationResultV1 imported =
                PlayerRouteProfilePayloadV1.TryImport(
                    normalized.ToEnvelope());
            Assert.That(imported.IsValid, Is.True);
            Assert.That(imported.Payload, Is.EqualTo(normalized));

            ProductionWeaponMountSetV1 mountSet =
                ProductionWeaponMountPolicyV1.BuildMountSet(normalized);
            Assert.That(mountSet.Layout.PhysicalMountCount, Is.EqualTo(3));
            Assert.That(mountSet.Layout.ActiveMountCount, Is.EqualTo(2));
            Assert.That(
                mountSet.Layout.LockedBySkillMountCount,
                Is.EqualTo(1));
            Assert.That(mountSet.PhysicalBindings.Count, Is.EqualTo(3));
            Assert.That(mountSet.ConfiguredBindings.Count, Is.EqualTo(2));
            Assert.That(mountSet.EnabledBindings.Count, Is.EqualTo(2));
            Assert.That(
                mountSet.PhysicalBindings[0].MountStableId,
                Is.EqualTo(
                    ProductionWeaponMountPolicyV1
                        .OuterLeftMountStableId));
            Assert.That(
                mountSet.PhysicalBindings[1].MountStableId,
                Is.EqualTo(
                    ProductionWeaponMountPolicyV1.CenterMountStableId));
            Assert.That(
                mountSet.PhysicalBindings[1].EquipmentInstanceStableId,
                Is.Null);
            Assert.That(
                mountSet.PhysicalBindings[2].MountStableId,
                Is.EqualTo(
                    ProductionWeaponMountPolicyV1
                        .OuterRightMountStableId));
        }

        [Test]
        public void AggressiveRuntimeOwnsOnlyItsTwoActiveStarterWeapons()
        {
            var runtime = new ProductionPlayerLoadoutRuntimeV1(
                Route(
                    ProductionWeaponMountPolicyV1
                        .AggressiveLoadoutProfileId,
                    "aggressive-runtime"));
            var service = new InventoryLoadoutScreenServiceV1(
                runtime.RoutePayload,
                runtime.Holdings,
                runtime.CatalogAdapter,
                runtime.LoadoutAuthority);
            StableId owned = runtime.Holdings.ExportSnapshot()
                .UniqueHoldings.First().InstanceStableId;
            ProductionWeaponMountSetV1 mountSet =
                ProductionWeaponMountPolicyV1.BuildMountSet(
                    runtime.RoutePayload);

            Assert.That(
                runtime.Holdings.ExportSnapshot().UniqueHoldings.Count,
                Is.EqualTo(2));
            Assert.That(mountSet.Layout.PhysicalMountCount, Is.EqualTo(3));
            Assert.That(mountSet.PhysicalBindings.Count, Is.EqualTo(3));
            Assert.That(mountSet.ConfiguredBindings.Count, Is.EqualTo(2));
            Assert.That(mountSet.EnabledBindings.Count, Is.EqualTo(2));
            Assert.That(
                mountSet.PhysicalBindings.Single(value =>
                    value.MountStableId
                        == ProductionWeaponMountPolicyV1
                            .CenterMountStableId)
                    .EquipmentInstanceStableId,
                Is.Null);
            Assert.That(service.Snapshot.CanConfirm, Is.True);
            Assert.That(
                service.TrySelect(
                    InventoryLoadoutSlotIdsV1.WeaponTwo,
                    owned).Status,
                Is.EqualTo(
                    InventoryLoadoutScreenStatusV1.InvalidSlot));
        }

        [Test]
        public void HealerUsesOuterCenterOuterPositions()
        {
            PlayerRouteProfilePayloadV1 normalized =
                ProductionWeaponMountPolicyV1.NormalizeRoutePayload(
                    Route(
                        ProductionWeaponMountPolicyV1
                            .HealerLoadoutProfileId,
                        "healer"));
            ProductionWeaponMountSetV1 mountSet =
                ProductionWeaponMountPolicyV1.BuildMountSet(normalized);

            Assert.That(mountSet.PhysicalBindings.Count, Is.EqualTo(3));
            Assert.That(mountSet.ConfiguredBindings.Count, Is.EqualTo(3));
            Assert.That(mountSet.EnabledBindings.Count, Is.EqualTo(3));
            Assert.That(
                mountSet.ConfiguredBindings[0].MountStableId,
                Is.EqualTo(
                    ProductionWeaponMountPolicyV1
                        .OuterLeftMountStableId));
            Assert.That(
                mountSet.ConfiguredBindings[1].MountStableId,
                Is.EqualTo(
                    ProductionWeaponMountPolicyV1.CenterMountStableId));
            Assert.That(
                mountSet.ConfiguredBindings[2].MountStableId,
                Is.EqualTo(
                    ProductionWeaponMountPolicyV1
                        .OuterRightMountStableId));
        }

        [Test]
        public void JuggernautPositionOrderPreservesOuterAndInnerAssignments()
        {
            PlayerRouteProfilePayloadV1 route = Route(
                ProductionWeaponMountPolicyV1
                    .DefensiveLoadoutProfileId,
                "juggernaut");
            ProductionWeaponMountSetV1 mountSet =
                ProductionWeaponMountPolicyV1.BuildMountSet(route);

            Assert.That(mountSet.PhysicalBindings.Count, Is.EqualTo(4));
            Assert.That(mountSet.ConfiguredBindings.Count, Is.EqualTo(4));
            Assert.That(
                mountSet.ConfiguredBindings[0]
                    .EquipmentInstanceStableId,
                Is.EqualTo(route.WeaponSlots[0]
                    .EquipmentInstanceStableId));
            Assert.That(
                mountSet.ConfiguredBindings[1]
                    .EquipmentInstanceStableId,
                Is.EqualTo(route.WeaponSlots[1]
                    .EquipmentInstanceStableId));
            Assert.That(
                mountSet.ConfiguredBindings[2]
                    .EquipmentInstanceStableId,
                Is.EqualTo(route.WeaponSlots[2]
                    .EquipmentInstanceStableId));
            Assert.That(
                mountSet.ConfiguredBindings[3]
                    .EquipmentInstanceStableId,
                Is.EqualTo(route.WeaponSlots[3]
                    .EquipmentInstanceStableId));
            Assert.That(
                mountSet.Layout.ConfigurablePositions[0].LateralOffset,
                Is.LessThan(
                    mountSet.Layout.ConfigurablePositions[1]
                        .LateralOffset));
            Assert.That(
                mountSet.Layout.ConfigurablePositions[2].LateralOffset,
                Is.LessThan(
                    mountSet.Layout.ConfigurablePositions[3]
                        .LateralOffset));
        }

        private static PlayerRouteProfilePayloadV1 Route(
            string profileId,
            string suffix)
        {
            return PlayerRouteProfilePayloadV1.Create(
                StableId.Parse("character." + suffix),
                StableId.Parse(profileId),
                new[]
                {
                    StableId.Parse(
                        "equipment-instance." + suffix + "-outer-left"),
                    StableId.Parse(
                        "equipment-instance." + suffix + "-inner-left"),
                    StableId.Parse(
                        "equipment-instance." + suffix + "-inner-right"),
                    StableId.Parse(
                        "equipment-instance." + suffix + "-outer-right"),
                });
        }
    }
}
