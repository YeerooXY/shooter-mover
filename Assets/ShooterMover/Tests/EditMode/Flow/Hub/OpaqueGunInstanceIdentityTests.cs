using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class OpaqueGunInstanceIdentityTests
    {
        [Test]
        public void AggressiveStarterCreatesOpaqueCanonicalInstancesForActiveMounts()
        {
            StarterInventory starter = StarterLoadout.CreateStarter(
                StableId.Parse("character-instance.opaque-starter-test"),
                StableId.Parse(
                    GunMountPolicy.AggressiveLoadoutProfileId));
            GunSlots layout = GunMountPolicy.ResolveLayout(
                starter.RoutePayload.LoadoutProfileStableId);

            StableId[] owned = starter.GunInventory.Instances
                .Select(value => value.InstanceId)
                .ToArray();
            StableId[] equipped = starter.EquippedGuns.Bindings
                .Where(value => value.InstanceId != null)
                .Select(value => value.InstanceId)
                .ToArray();
            EquippedGun locked = starter.EquippedGuns.Bindings.Single(value =>
                GunMountPolicy.FindPosition(
                    layout,
                    value.MountId).IsLockedBySkill);

            Assert.That(layout.PhysicalMountCount, Is.EqualTo(3));
            Assert.That(layout.ActiveMountCount, Is.EqualTo(2));
            Assert.That(layout.LockedBySkillMountCount, Is.EqualTo(1));
            Assert.That(starter.EquippedGuns.Bindings.Count, Is.EqualTo(3));
            Assert.That(equipped.Length, Is.EqualTo(2));
            Assert.That(equipped.Distinct().Count(), Is.EqualTo(2));
            Assert.That(equipped.All(owned.Contains), Is.True);
            Assert.That(locked.InstanceId, Is.Null);

            foreach (StableId instanceId in owned)
            {
                Assert.That(instanceId.Namespace, Is.EqualTo("instance"));
                Assert.That(instanceId.Value.Length, Is.EqualTo(32));
                Assert.That(instanceId.ToString(), Does.Not.Contain("rattler"));
                Assert.That(instanceId.ToString(), Does.Not.Contain("gun"));
                Assert.That(instanceId.ToString(), Does.Not.Contain("starter"));
                Assert.That(instanceId.ToString(), Does.Not.Contain("slot"));
                Assert.That(instanceId.ToString(), Does.Not.Contain("onboarding"));
            }
        }
    }
}
