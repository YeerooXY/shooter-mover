using NUnit.Framework;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Tests.EditMode.Guns.Catalog
{
    public sealed class BallerGunCatalogueTests
    {
        [Test]
        public void BallerFamily_UsesConfirmedOrbValues()
        {
            GunFamily family = FindFamily("baller");

            Assert.That(family.DisplayName, Is.EqualTo("Baller"));
            Assert.That(family.CatalogRarity, Is.EqualTo("epic"));
            Assert.That(family.Marks.Count, Is.EqualTo(3));

            int[] anchors = { 15, 40, 65 };
            double[] damage = { 10d, 15d, 20d };
            for (int index = 0; index < family.Marks.Count; index++)
            {
                GunMark mark = family.Marks[index];
                Gun gun = mark.Blueprint;

                Assert.That(mark.Mark, Is.EqualTo(index + 1));
                Assert.That(mark.DropAnchorLevel, Is.EqualTo(anchors[index]));
                Assert.That(mark.CraftUnlockLevel, Is.EqualTo(anchors[index]));
                Assert.That(mark.IsCombatTuningProvisional, Is.True);

                Assert.That(gun.DisplayName, Is.EqualTo("Baller MK" + (index + 1)));
                Assert.That(gun.FireSettings.Mode, Is.EqualTo(GunFireMode.Automatic));
                Assert.That(gun.FireSettings.RateOfFire, Is.EqualTo(3d));
                Assert.That(gun.ShotPattern.Kind, Is.EqualTo(GunShotPatternKind.Single));
                Assert.That(gun.ShotPattern.ProjectilesPerShot, Is.EqualTo(1));
                Assert.That(gun.ShotPattern.SpreadDegrees, Is.EqualTo(0d));
                Assert.That(gun.ShotPattern.RandomnessDegrees, Is.EqualTo(0d));

                Assert.That(gun.BaseStats.DirectDamage, Is.EqualTo(damage[index]));
                Assert.That(gun.BaseStats.DamageCategory, Is.EqualTo(GunDamageCategory.Energy));
                Assert.That(gun.BaseStats.DamageOverTime, Is.Null);
                Assert.That(gun.BaseStats.Pierce.GuaranteedHits, Is.EqualTo(3));
                Assert.That(gun.BaseStats.MaximumAttackDistance.Distance, Is.EqualTo(30d));

                Assert.That(gun.Delivery.Type, Is.EqualTo(GunDeliveryType.Orb));
                Assert.That(gun.Delivery.Orb.ProjectileSpeed, Is.EqualTo(10d));
                Assert.That(gun.Delivery.Orb.ProjectileRadius, Is.EqualTo(0.5d));
                Assert.That(gun.Guidance.Mode, Is.EqualTo(GunGuidanceMode.Unguided));
                Assert.That(gun.Effects.Explosion, Is.Null);
                Assert.That(gun.Effects.DamageOverTime, Is.Null);
                Assert.That(gun.Effects.ChainArc, Is.Null);

                Assert.That(gun.Projectile.Kind, Is.EqualTo(GunProjectileKind.Orb));
                Assert.That(gun.Projectile.Speed, Is.EqualTo(10d));
                Assert.That(gun.Projectile.Range, Is.EqualTo(30d));
                Assert.That(
                    gun.Projectile.TerminationBehavior,
                    Is.EqualTo(GunProjectileTerminationBehavior.StopWhenPierceIsSpent));
            }
        }

        private static GunFamily FindFamily(string familyId)
        {
            for (int index = 0; index < GunCatalogue.Current.Families.Count; index++)
            {
                GunFamily family = GunCatalogue.Current.Families[index];
                if (family.FamilyId == familyId)
                {
                    return family;
                }
            }

            Assert.Fail("Missing gun family: " + familyId);
            return null;
        }
    }
}
