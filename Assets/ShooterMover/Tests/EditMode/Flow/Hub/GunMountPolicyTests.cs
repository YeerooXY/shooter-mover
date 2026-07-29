using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class GunMountPolicyTests
    {
        [Test]
        public void ProductionClassesExposeOnlyTheirPhysicalMounts()
        {
            GunSlots aggressive = Layout(
                GunMountPolicy.AggressiveLoadoutProfileId);
            GunSlots healer = Layout(
                GunMountPolicy.HealerLoadoutProfileId);
            GunSlots defensive = Layout(
                GunMountPolicy.DefensiveLoadoutProfileId);

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
                () => GunMountPolicy.ResolveLayout(
                    Id("loadout-profile.recon")),
                Throws.ArgumentException.With.Message.Contains("Unsupported"));
            Assert.That(
                () => GunMountPolicy.ResolveLayout(null),
                Throws.ArgumentException.With.Message.Contains("Unsupported"));
        }

        [Test]
        public void AggressiveLockedCenterIsProjectedVisibleAndUnbound()
        {
            PlayerRouteProfilePayload normalized =
                GunMountPolicy.NormalizeRoutePayload(
                    BoundRoute(
                        GunMountPolicy
                            .AggressiveLoadoutProfileId,
                        "aggressive"));
            GunMountSet mountSet =
                GunMountPolicy.BuildMountSet(normalized);

            Assert.That(normalized.GunSlots[0].IsBound, Is.True);
            Assert.That(normalized.GunSlots[1].IsBound, Is.False);
            Assert.That(normalized.GunSlots[2].IsBound, Is.False);
            Assert.That(normalized.GunSlots[3].IsBound, Is.True);
            Assert.That(mountSet.PhysicalBindings.Count, Is.EqualTo(3));
            Assert.That(mountSet.ConfiguredBindings.Count, Is.EqualTo(2));
            Assert.That(mountSet.EnabledBindings.Count, Is.EqualTo(2));
            Assert.That(
                mountSet.PhysicalBindings.Single(value =>
                    value.MountStableId
                        == GunMountPolicy.CenterMountStableId)
                    .EquipmentInstanceStableId,
                Is.Null);
        }

        [Test]
        public void EmptyActiveMountsAreValidAndNotEnabled()
        {
            PlayerRouteProfilePayload empty = PlayerRouteProfilePayload.Create(
                Id("character.empty-active"),
                Id(GunMountPolicy.HealerLoadoutProfileId),
                new StableId[PlayerRouteProfilePayload.GunSlotCount]);

            GunMountSet mountSet =
                GunMountPolicy.BuildMountSet(empty);

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
            PlayerRouteProfilePayload invalid = PlayerRouteProfilePayload.Create(
                Id("character.locked-bound"),
                Id(GunMountPolicy.AggressiveLoadoutProfileId),
                new[]
                {
                    Id("instance.outer-left"),
                    Id("instance.locked-center"),
                    null,
                    Id("instance.outer-right"),
                });

            Assert.That(
                () => GunMountPolicy.BuildMountSet(invalid),
                Throws.InvalidOperationException);
        }

        [Test]
        public void DefensivePositionOrderPreservesOuterAndInnerAssignments()
        {
            PlayerRouteProfilePayload route = BoundRoute(
                GunMountPolicy.DefensiveLoadoutProfileId,
                "defensive");
            GunMountSet mountSet =
                GunMountPolicy.BuildMountSet(route);

            Assert.That(mountSet.PhysicalBindings.Count, Is.EqualTo(4));
            Assert.That(mountSet.EnabledBindings.Count, Is.EqualTo(4));
            Assert.That(
                mountSet.ConfiguredBindings.Select(item =>
                    item.EquipmentInstanceStableId),
                Is.EqualTo(route.GunSlots.Select(item =>
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

        private static GunSlots Layout(string profileId)
        {
            return GunMountPolicy.ResolveLayout(Id(profileId));
        }

        private static PlayerRouteProfilePayload BoundRoute(
            string profileId,
            string suffix)
        {
            return PlayerRouteProfilePayload.Create(
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
