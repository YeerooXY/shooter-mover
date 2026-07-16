using System;
using NUnit.Framework;
using ShooterMover.ContentPackages.Environment.VoidHazards;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Environment.VoidHazards
{
    public sealed class VoidHazardPolicyTests
    {
        [Test]
        public void CategoryPoliciesRemainIndependent()
        {
            VoidHazardPolicy policy = new VoidHazardPolicy(
                VoidPlayerResponseKind.Damage,
                17d,
                null,
                VoidEnemyResponseKind.RequestFall,
                VoidProjectileResponseKind.Ignore,
                VoidPropResponseKind.KeepSupported);

            Assert.That(policy.PlayerResponse, Is.EqualTo(VoidPlayerResponseKind.Damage));
            Assert.That(policy.PlayerDamageAmount, Is.EqualTo(17d));
            Assert.That(policy.EnemyResponse, Is.EqualTo(VoidEnemyResponseKind.RequestFall));
            Assert.That(policy.ProjectileResponse, Is.EqualTo(VoidProjectileResponseKind.Ignore));
            Assert.That(policy.PropResponse, Is.EqualTo(VoidPropResponseKind.KeepSupported));
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void DamageRequiresFinitePositiveAmount(double amount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoidHazardPolicy(
                VoidPlayerResponseKind.Damage,
                amount,
                null,
                VoidEnemyResponseKind.Ignore,
                VoidProjectileResponseKind.Ignore,
                VoidPropResponseKind.Ignore));
        }

        [Test]
        public void RespawnRequiresTypedCheckpointIdentity()
        {
            Assert.Throws<ArgumentNullException>(() => new VoidHazardPolicy(
                VoidPlayerResponseKind.Respawn,
                0d,
                null,
                VoidEnemyResponseKind.Ignore,
                VoidProjectileResponseKind.Ignore,
                VoidPropResponseKind.Ignore));

            VoidHazardPolicy valid = new VoidHazardPolicy(
                VoidPlayerResponseKind.Respawn,
                0d,
                StableId.Parse("checkpoint.alpha"),
                VoidEnemyResponseKind.Ignore,
                VoidProjectileResponseKind.Ignore,
                VoidPropResponseKind.Ignore);

            Assert.That(valid.PlayerCheckpointId, Is.EqualTo(StableId.Parse("checkpoint.alpha")));
        }

        [Test]
        public void DamageAndInstantDeathRequestsDeclareEnvironmentalChannel()
        {
            StableId eventId = StableId.Parse("event.void-a");
            StableId hazardId = StableId.Parse("placed.void-a");
            StableId targetId = StableId.Parse("player.primary");

            VoidHazardDamageRequest damage = new VoidHazardDamageRequest(
                eventId,
                hazardId,
                targetId,
                25d);
            VoidHazardInstantDeathRequest death = new VoidHazardInstantDeathRequest(
                eventId,
                hazardId,
                targetId);

            Assert.That(damage.Channel, Is.EqualTo(CombatChannel.Environmental));
            Assert.That(death.Channel, Is.EqualTo(CombatChannel.Environmental));
            Assert.That(damage.TargetId, Is.EqualTo(targetId));
            Assert.That(death.TargetId, Is.EqualTo(targetId));
        }

        [Test]
        public void DuplicateNoChangeIsAnAcceptedAuthorityOutcome()
        {
            VoidHazardContactResult result = new VoidHazardContactResult(
                VoidHazardContactStatus.Applied,
                VoidHazardTargetCategory.Projectile,
                StableId.Parse("event.void-b"),
                VoidHazardPortResult.DuplicateNoChange,
                "duplicate");

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(result.PortResult, Is.EqualTo(VoidHazardPortResult.DuplicateNoChange));
        }
    }
}
