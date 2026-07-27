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
        public void ProductionClassesExposeOnlyTheirPhysicalMounts()
        {
            ProductionWeaponMountLayoutV1 aggressive = Layout(
                ProductionWeaponMountPolicyV1.AggressiveLoadoutProfileId);
            ProductionWeaponMountLayoutV1 healer = Layout(
                ProductionWeaponMountPolicyV1.HealerLoadoutProfileId);
            ProductionWeaponMountLayoutV1 defensive = Layout(
                ProductionWeaponMountPolicyV1.DefensiveLoadoutProfileId);

            Assert.That(aggressive.PhysicalMountCount, Is.EqualTo(3));
            Assert.That(aggressive.ActiveMountCount, Is.EqualTo(2));
            Assert.That(aggressive.LockedBySkillMountCount, Is.EqualTo(1));
            Assert.That(
                aggressive.Positions.Select(item => item.DisplayName),
                Is.EqualTo(new[] { "Outer Left", "Center", "Outer Right" }));
            Assert.That(
                aggressive.Positions.Single(item => item.DisplayName == "Center")
                    .IsLockedBySkill,
                Is.True);

            Assert.That(healer.PhysicalMountCount, Is.EqualTo(3));
            Assert.That(healer.ActiveMountCount, Is.EqualTo(3));
            Assert.That(
                healer.Positions.Select(item => item.DisplayName),
                Is.EqualTo(new[] { "Outer Left", "Center", "Outer Right" }));

            Assert.That(defensive.PhysicalMountCount, Is.EqualTo(4));
            Assert.That(defensive.ActiveMountCount, Is.EqualTo(4));
            Assert.That(
                defensive.Positions.Select(item => item.DisplayName),
                Is.EqualTo(new[]
                {
                    "Outer Left",
                    "Inner Left",
                    "Inner Right",
                    "Outer Right",
                }));
        }

        [Test]
        public void CharacterSpecificProfilesResolveByClassSuffix()
        {
            Assert.That(
                Layout("loadout-profile.frontier-vanguard-aggressive")
                    .ActiveMountCount,
                Is.EqualTo(2));
            Assert.That(
                Layout("loadout-profile.custom-pilot-healer")
                    .ActiveMountCount,
                Is.EqualTo(3));
            Assert.That(
                Layout("loadout-profile.frontier-vanguard-defensive")
                    .ActiveMountCount,
                Is.EqualTo(4));
        }

        [Test]
        public void UnknownProfilesFailClosedInsteadOfReceivingDefensiveMounts()
        {
            Assert.That(
                () => ProductionWeaponMountPolicyV1.ResolveLayout(
                    Id("loadout-profile.recon")),
                Throws.ArgumentException.With.Message.Contains("Unsupported"));
            Assert.That(
                () => ProductionWeaponMountPolicyV1.ResolveLayout(null),
                Throws.ArgumentException.With.Message.Contains("Unsupported"));
        }

        [Test]
        public void AggressiveLockedCenterIsProjectedVisibleAndUnbound()
        {
            PlayerRouteProfilePayloadV1 normalized =
                ProductionWeaponMountPolicyV1.NormalizeRoutePayload(
                    BoundRoute(
                        ProductionWeaponMountPolicyV1
                            .AggressiveLoadoutProfileId,
                        "aggressive"));
            ProductionWeaponMountSetV1 mountSet =
                ProductionWeaponMountPolicyV1.BuildMountSet(normalized);

            Assert.That(normalized.WeaponSlots[0].IsBound, Is.True);
            Assert.That(normalized.WeaponSlots[1].IsBound, Is.False);
            Assert.That(normalized.WeaponSlots[2].IsBound, Is.False);
            Assert.That(normalized.WeaponSlots[3].IsBound, Is.True);
            Assert.That(mountSet.PhysicalBindings.Count, Is.EqualTo(3));
            Assert.That(mountSet.ConfiguredBindings.Count, Is.EqualTo(2));
            Assert.That(mountSet.EnabledBindings.Count, Is.EqualTo(2));
            Assert.That(
                mountSet.PhysicalBindings.Single(value =>
                    value.MountStableId
                        == ProductionWeaponMountPolicyV1.CenterMountStableId)
                    .EquipmentInstanceStableId,
                Is.Null);
        }

        [Test]
        public void EmptyActiveMountsAreValidAndNotEnabled()
        {
            PlayerRouteProfilePayloadV1 empty = PlayerRouteProfilePayloadV1.Create(
                Id("character.empty-active"),
                Id(ProductionWeaponMountPolicyV1.HealerLoadoutProfileId),
                new StableId[PlayerRouteProfilePayloadV1.WeaponSlotCount]);

            ProductionWeaponMountSetV1 mountSet =
                ProductionWeaponMountPolicyV1.BuildMountSet(empty);

            Assert.That(mountSet.PhysicalBindings.Count, Is.EqualTo(3));
            Assert.That(mountSet.ConfiguredBindings.Count, Is.EqualTo(3));
            Assert.That(mountSet.EnabledBindings, Is.Empty);
            Assert.That(
                mountSet.ConfiguredBindings.All(item => !item.IsBound),
                Is.True);
        }

        [Test]
        public void LockedAggressiveCenterRejectsBoundPayload()
        {
            PlayerRouteProfilePayloadV1 invalid = PlayerRouteProfilePayloadV1.Create(
                Id("character.locked-bound"),
                Id(ProductionWeaponMountPolicyV1.AggressiveLoadoutProfileId),
                new[]
                {
                    Id("instance.outer-left"),
                    Id("instance.locked-center"),
                    null,
                    Id("instance.outer-right"),
                });

            Assert.That(
                () => ProductionWeaponMountPolicyV1.BuildMountSet(invalid),
                Throws.InvalidOperationException);
        }

        [Test]
        public void DefensivePositionOrderPreservesOuterAndInnerAssignments()
        {
            PlayerRouteProfilePayloadV1 route = BoundRoute(
                ProductionWeaponMountPolicyV1.DefensiveLoadoutProfileId,
                "defensive");
            ProductionWeaponMountSetV1 mountSet =
                ProductionWeaponMountPolicyV1.BuildMountSet(route);

            Assert.That(mountSet.PhysicalBindings.Count, Is.EqualTo(4));
            Assert.That(mountSet.EnabledBindings.Count, Is.EqualTo(4));
            Assert.That(
                mountSet.ConfiguredBindings.Select(item =>
                    item.EquipmentInstanceStableId),
                Is.EqualTo(route.WeaponSlots.Select(item =>
                    item.EquipmentInstanceStableId)));
            Assert.That(
                mountSet.Layout.ConfigurablePositions[0].LateralOffset,
                Is.LessThan(
                    mountSet.Layout.ConfigurablePositions[1].LateralOffset));
            Assert.That(
                mountSet.Layout.ConfigurablePositions[2].LateralOffset,
                Is.LessThan(
                    mountSet.Layout.ConfigurablePositions[3].LateralOffset));
        }

        private static ProductionWeaponMountLayoutV1 Layout(string profileId)
        {
            return ProductionWeaponMountPolicyV1.ResolveLayout(Id(profileId));
        }

        private static PlayerRouteProfilePayloadV1 BoundRoute(
            string profileId,
            string suffix)
        {
            return PlayerRouteProfilePayloadV1.Create(
                Id("character." + suffix),
                Id(profileId),
                new[]
                {
                    Id("instance." + suffix + "-outer-left"),
                    Id("instance." + suffix + "-inner-left"),
                    Id("instance." + suffix + "-inner-right"),
                    Id("instance." + suffix + "-outer-right"),
                });
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
