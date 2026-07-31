using System;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Enemies.Foundation;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class EnemyFoundationTests
    {
        [Test]
        public void GunAttackUsesStableBodyMounts()
        {
            BodyDef body = Body("mount.left");

            Assert.DoesNotThrow(() => Def(
                body,
                new GunAttack(
                    Id("attack.primary"),
                    Id("gun.rattler"),
                    new ShotPlan(
                        new[] { Id("mount.left") },
                        FireMode.Alternating,
                        MountOrder.Cycle,
                        2,
                        0.1d))));

            Assert.Throws<ArgumentException>(() => Def(
                body,
                new GunAttack(
                    Id("attack.primary"),
                    Id("gun.rattler"),
                    new ShotPlan(
                        new[] { Id("mount.missing") },
                        FireMode.Alternating,
                        MountOrder.Cycle,
                        2,
                        0.1d))));
        }

        [Test]
        public void SpawnOverridesMustBelongToDefinition()
        {
            EnemyDef def = Def(
                Body("mount.left"),
                BasicMelee(),
                new[] { new VariantDef(Id("variant.fast"), Array.Empty<StableId>()) },
                new PerkRules(
                    new[] { Id("perk.fixed") },
                    new[] { Id("perk.roll") },
                    1));

            Assert.That(def.Allows(new EnemySpawn(
                Id("spawn.one"),
                def.Id,
                EnemyTier.Two,
                Id("variant.fast"),
                new[] { Id("perk.roll") },
                new Vec2(2d, 3d),
                90d)), Is.True);

            Assert.That(def.Allows(new EnemySpawn(
                Id("spawn.two"),
                def.Id,
                EnemyTier.Two,
                Id("variant.unknown"),
                Array.Empty<StableId>(),
                new Vec2(2d, 3d),
                90d)), Is.False);
        }

        [Test]
        public void ModifierOrderIsFixed()
        {
            Assert.That(ModOrder.Stages, Is.EqualTo(new[]
            {
                ModStage.Base,
                ModStage.Tier,
                ModStage.Difficulty,
                ModStage.Variant,
                ModStage.FixedPerks,
                ModStage.RolledPerks,
                ModStage.Phase,
                ModStage.Temporary,
            }));
        }

        [Test]
        public void PhasesMustBeOneWayAndDescending()
        {
            Assert.DoesNotThrow(() => Def(
                Body("mount.left"),
                BasicMelee(),
                Array.Empty<VariantDef>(),
                EmptyPerks(),
                new[]
                {
                    new PhaseDef(Id("phase.two"), 0.7d, Array.Empty<StableId>()),
                    new PhaseDef(Id("phase.three"), 0.3d, Array.Empty<StableId>()),
                }));

            Assert.Throws<ArgumentException>(() => Def(
                Body("mount.left"),
                BasicMelee(),
                Array.Empty<VariantDef>(),
                EmptyPerks(),
                new[]
                {
                    new PhaseDef(Id("phase.two"), 0.3d, Array.Empty<StableId>()),
                    new PhaseDef(Id("phase.three"), 0.7d, Array.Empty<StableId>()),
                }));
        }

        [Test]
        public void RollStreamsDoNotShareKeys()
        {
            RollKey variant = new RollKey(
                42UL,
                Id("spawn.one"),
                Id("enemy.test"),
                RollStreams.Variant);
            RollKey perks = new RollKey(
                42UL,
                Id("spawn.one"),
                Id("enemy.test"),
                RollStreams.Perks);

            Assert.That(variant.Canonical, Is.Not.EqualTo(perks.Canonical));
        }

        private static EnemyDef Def(BodyDef body, AttackDef attack)
        {
            return Def(
                body,
                attack,
                Array.Empty<VariantDef>(),
                EmptyPerks());
        }

        private static EnemyDef Def(
            BodyDef body,
            AttackDef attack,
            VariantDef[] variants,
            PerkRules perks,
            PhaseDef[] phases = null)
        {
            return new EnemyDef(
                Id("enemy.test"),
                Id("presentation.enemy-test"),
                body,
                new StatsDef(10d),
                new SenseDef(12d, 180d),
                Id("enemy-movement.chase"),
                Id("enemy-ai.standard"),
                new[] { attack },
                new[] { EnemyTier.One, EnemyTier.Two },
                variants,
                perks,
                phases ?? Array.Empty<PhaseDef>(),
                Id("xp.enemy-standard"),
                Id("loot.enemy-standard"),
                EnemyCatalogRoomClearRole.RequiredEnemy);
        }

        private static BodyDef Body(string mount)
        {
            return new BodyDef(
                TravelMode.Ground,
                0.5d,
                2d,
                new[]
                {
                    new MountDef(
                        Id(mount),
                        new Vec2(0d, 0d),
                        new Vec2(1d, 0d)),
                });
        }

        private static MeleeAttack BasicMelee()
        {
            return new MeleeAttack(
                Id("attack.melee"),
                1d,
                0.1d,
                0.2d,
                0.4d,
                new EffectRef[] { new DamageRef(Id("damage.enemy-contact")) });
        }

        private static PerkRules EmptyPerks()
        {
            return new PerkRules(
                Array.Empty<StableId>(),
                Array.Empty<StableId>(),
                0);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
