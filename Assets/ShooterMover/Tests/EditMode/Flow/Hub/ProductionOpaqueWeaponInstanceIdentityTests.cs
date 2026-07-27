using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class ProductionOpaqueWeaponInstanceIdentityTests
    {
        [Test]
        public void AggressiveStarterCreatesTwoOpaqueInstancesAndLeavesOtherMountsEmpty()
        {
            ProductionWeaponInventoryStateV1 state =
                ProductionWeaponOnboardingV1.CreateStarter(
                    StableId.Parse("character-instance.opaque-starter-test"),
                    StableId.Parse(
                        ProductionWeaponMountPolicyV1
                            .AggressiveLoadoutProfileId));

            StableId[] owned = state.Holdings.UniqueHoldings
                .Select(value => value.InstanceStableId)
                .ToArray();
            StableId[] equipped = state.Loadout.Bindings
                .Where(value => value.EquipmentInstanceStableId != null)
                .Select(value => value.EquipmentInstanceStableId)
                .ToArray();

            Assert.That(owned.Length, Is.EqualTo(2));
            Assert.That(owned.Distinct().Count(), Is.EqualTo(2));
            Assert.That(equipped, Is.EquivalentTo(owned));
            Assert.That(
                state.Loadout.Bindings.Count(value =>
                    value.EquipmentInstanceStableId == null),
                Is.EqualTo(2));

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
