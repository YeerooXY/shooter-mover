using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class ProductionOpaqueWeaponInstanceIdentityTests
    {
        [Test]
        public void AggressiveStarterCreatesTwoOpaqueInstancesForTwoActiveMounts()
        {
            ProductionWeaponInventoryStateV1 state =
                ProductionWeaponOnboardingV1.CreateStarter(
                    StableId.Parse("character-instance.opaque-starter-test"),
                    StableId.Parse(
                        ProductionWeaponMountPolicyV1
                            .AggressiveLoadoutProfileId));
            ProductionWeaponMountSetV1 mountSet =
                ProductionWeaponMountPolicyV1.BuildMountSet(
                    state.RoutePayload);

            StableId[] owned = state.Holdings.UniqueHoldings
                .Select(value => value.InstanceStableId)
                .ToArray();
            StableId[] equipped = mountSet.EnabledBindings
                .Select(value => value.EquipmentInstanceStableId)
                .ToArray();
            ProductionWeaponMountBindingV1 locked = mountSet
                .ConfiguredBindings.Single(value =>
                    ProductionWeaponMountPolicyV1.FindPosition(
                        mountSet.Layout,
                        value.MountStableId).IsLockedBySkill);

            Assert.That(mountSet.Layout.PhysicalMountCount, Is.EqualTo(3));
            Assert.That(mountSet.Layout.ActiveMountCount, Is.EqualTo(2));
            Assert.That(
                mountSet.Layout.LockedBySkillMountCount,
                Is.EqualTo(1));
            Assert.That(mountSet.ConfiguredBindings.Count, Is.EqualTo(3));
            Assert.That(mountSet.EnabledBindings.Count, Is.EqualTo(2));
            Assert.That(owned.Length, Is.EqualTo(2));
            Assert.That(owned.Distinct().Count(), Is.EqualTo(2));
            Assert.That(equipped, Is.EquivalentTo(owned));
            Assert.That(locked.EquipmentInstanceStableId, Is.Null);

            foreach (StableId instanceId in owned)
            {
                Assert.That(instanceId.Namespace, Is.EqualTo("instance"));
                Assert.That(instanceId.Value.Length, Is.EqualTo(32));
                Assert.That(instanceId.ToString(), Does.Not.Contain("rattler"));
                Assert.That(instanceId.ToString(), Does.Not.Contain("weapon"));
                Assert.That(instanceId.ToString(), Does.Not.Contain("starter"));
                Assert.That(instanceId.ToString(), Does.Not.Contain("slot"));
                Assert.That(instanceId.ToString(), Does.Not.Contain("onboarding"));
            }
        }
    }
}
