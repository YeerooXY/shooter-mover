using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Catalog
{
    /// <summary>
    /// Pure production importer for the generated Weapon Maker payload. Content/Weapons is the
    /// authoring authority; this importer projects its validated merged definitions into the
    /// existing canonical gun, Strongbox, equipment, Inventory, Shop, and live-fire models.
    /// </summary>
    internal static class AuthoredGunCatalogJsonImporter
    {
        private const double NumericTolerance = 0.000000001d;

        public static List<GunFamily> Import(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw Error("document-missing");
            }

            CatalogDocument document;
            try
            {
                var serializer = new DataContractJsonSerializer(
                    typeof(CatalogDocument));
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                using (var stream = new MemoryStream(bytes, false))
                {
                    document = serializer.ReadObject(stream) as CatalogDocument;
                }
            }
            catch (Exception exception)
            {
                throw Error("document-invalid", exception);
            }

            if (document == null || document.Schema != 1)
            {
                throw Error("schema-unsupported");
            }
            if (document.Families == null || document.Families.Count == 0)
            {
                throw Error("families-missing");
            }

            document.Families.Sort((left, right) => string.CompareOrdinal(
                left == null ? string.Empty : left.Id,
                right == null ? string.Empty : right.Id));
            var familyIds = new HashSet<string>(StringComparer.Ordinal);
            var definitionIds = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<GunFamily>(document.Families.Count);
            for (int index = 0; index < document.Families.Count; index++)
            {
                FamilyDocument family = document.Families[index];
                string path = "families[" + index + "]";
                if (family == null)
                {
                    throw Error(path + ":missing");
                }
                family.Id = RequireText(family.Id, path + ".id");
                family.Name = RequireText(family.Name, path + ".name");
                family.Category = RequireText(
                    family.Category,
                    path + ".category");
                family.Rarity = RequireRarity(
                    family.Rarity,
                    path + ".rarity");
                if (!familyIds.Add(family.Id))
                {
                    throw Error(path + ":duplicate-family:" + family.Id);
                }
                if (family.Marks == null || family.Marks.Count != 3)
                {
                    throw Error(path + ":marks-must-equal-three");
                }

                family.Marks.Sort((left, right) =>
                    (left == null ? 0 : left.Mark)
                    .CompareTo(right == null ? 0 : right.Mark));
                var marks = new GunMark[3];
                for (int markIndex = 0; markIndex < family.Marks.Count; markIndex++)
                {
                    MarkDocument mark = family.Marks[markIndex];
                    string markPath = path + ".marks[" + markIndex + "]";
                    if (mark == null || mark.Mark != markIndex + 1)
                    {
                        throw Error(markPath + ":mark-order-invalid");
                    }
                    string expectedDefinitionId = family.Id + ".mk" + mark.Mark;
                    if (!string.Equals(
                        mark.DefinitionId,
                        expectedDefinitionId,
                        StringComparison.Ordinal))
                    {
                        throw Error(
                            markPath
                            + ":definition-id-mismatch:"
                            + (mark.DefinitionId ?? "<null>"));
                    }
                    if (!definitionIds.Add(expectedDefinitionId))
                    {
                        throw Error(
                            markPath
                            + ":duplicate-definition:"
                            + expectedDefinitionId);
                    }
                    marks[markIndex] = BuildMark(
                        family,
                        mark,
                        markPath);
                }

                result.Add(new GunFamily(
                    family.Id,
                    family.Name,
                    StableId.Create(
                        "gun-category",
                        StableToken(family.Category)),
                    StableId.Create("gun-rarity", family.Rarity),
                    family.Rarity,
                    marks));
            }
            return result;
        }

        private static GunMark BuildMark(
            FamilyDocument family,
            MarkDocument mark,
            string path)
        {
            if (mark.PeakLevel < 1)
            {
                throw Error(path + ":peak-level-invalid");
            }
            RequireFinitePositive(mark.Damage, path + ".damage");
            FireSettings fire = BuildFire(mark.Fire, path + ".fire");
            GunShotPattern shot = BuildShot(mark, path);
            GunDamageCategory damageCategory = DamageCategory(
                mark.DamageType,
                path + ".damageType");
            GunDamageOverTimeStats dotStats = BuildDotStats(
                mark.Dot,
                path + ".dot");
            GunEffects effects = BuildEffects(mark, path);
            GunGuidanceSpec guidance = BuildGuidance(
                mark.Homing,
                path + ".homing");
            GunImpactSpec impact = BuildImpact(mark, path);
            double range = ResolveRange(mark, path);
            int ricochetTenths = FixedTenths(
                RequireImpact(mark.Impact, path + ".impact").Ricochet,
                path + ".impact.ricochet");
            var baseStats = new GunBaseStats(
                mark.Damage,
                damageCategory,
                dotStats,
                PierceValue.FromLegacyInteger(mark.Impact.Pierce),
                new RicochetValue(ricochetTenths),
                mark.Impact.Knockback,
                GunAttackDistance.Limited(range));
            ShotPattern delivery = BuildDelivery(
                mark,
                guidance,
                impact,
                effects,
                path);
            ArtDocument art = RequireArt(mark.Art, path + ".art");
            string definitionId = family.Id + ".mk" + mark.Mark;
            string equipmentToken = "gun-"
                + StableToken(family.Id)
                + "-mk"
                + mark.Mark;
            Gun blueprint = Gun.CreateAuthored(
                new GunIdentity(
                    new GunDefinitionId(definitionId),
                    family.Name + " MK" + mark.Mark,
                    family.Id),
                fire,
                shot,
                baseStats,
                delivery,
                new GunPresentation(
                    RequireText(art.Side, path + ".art.side"),
                    RequireText(art.Mounted, path + ".art.mounted"),
                    RequireText(art.Delivery, path + ".art.delivery"),
                    RequireText(art.Trail, path + ".art.trail"),
                    RequireText(art.Impact, path + ".art.impact"),
                    null),
                new GunDropMetadata(
                    StableId.Create("equipment", equipmentToken),
                    StableId.Create("gun-rarity", family.Rarity),
                    GunDropAvailability.Live,
                    mark.PeakLevel,
                    1d,
                    GunStrongboxEligibility.FromMinimumTier(1)));

            return new GunMark(
                mark.Mark,
                mark.PeakLevel,
                Math.Min(mark.PeakLevel, 100),
                false,
                blueprint);
        }

        private static FireSettings BuildFire(FireDocument fire, string path)
        {
            if (fire == null)
            {
                throw Error(path + ":missing");
            }
            RequireFinitePositive(fire.Rate, path + ".rate");
            switch (RequireText(fire.Mode, path + ".mode"))
            {
                case "semi-automatic":
                    return FireSettings.SemiAutomatic(fire.Rate);
                case "automatic":
                    return FireSettings.Automatic(fire.Rate);
                case "burst":
                    if (fire.ShotsPerBurst < 2)
                    {
                        throw Error(path + ".shotsPerBurst:invalid");
                    }
                    RequireFinitePositive(
                        fire.SecondsBetweenShots,
                        path + ".secondsBetweenShots");
                    return FireSettings.Burst(
                        fire.Rate,
                        new GunBurstSettings(
                            fire.ShotsPerBurst,
                            fire.SecondsBetweenShots));
                default:
                    throw Error(path + ":mode-unsupported:" + fire.Mode);
            }
        }

        private static GunShotPattern BuildShot(
            MarkDocument mark,
            string path)
        {
            string type = RequireText(
                mark.ProjectileType,
                path + ".projectileType");
            if (string.Equals(type, "beam", StringComparison.Ordinal))
            {
                if (mark.Shot != null
                    && (mark.Shot.Projectiles != 0
                        || Math.Abs(mark.Shot.Spread) > NumericTolerance))
                {
                    throw Error(path + ".shot:beam-shot-invalid");
                }
                return GunShotPattern.Create(
                    GunShotPatternKind.Beam,
                    0,
                    0d,
                    0d,
                    1,
                    0d);
            }

            if (mark.Shot == null || mark.Shot.Projectiles < 1)
            {
                throw Error(path + ".shot:missing-or-invalid");
            }
            RequireFiniteNonNegative(
                mark.Shot.Spread,
                path + ".shot.spread");
            return GunShotPattern.Canonical(
                mark.Shot.Projectiles,
                mark.Shot.Spread);
        }

        private static ShotPattern BuildDelivery(
            MarkDocument mark,
            GunGuidanceSpec guidance,
            GunImpactSpec impact,
            GunEffects effects,
            string path)
        {
            GunNormalDeliverySettings normal = null;
            GunOrbDeliverySettings orb = null;
            GunRocketDeliverySettings rocket = null;
            GunLaserDeliverySettings laser = null;
            GunDeliveryType type;
            switch (RequireText(
                mark.ProjectileType,
                path + ".projectileType"))
            {
                case "bullet":
                    RequireProjectile(mark.Projectile, path + ".projectile");
                    normal = new GunNormalDeliverySettings(
                        mark.Projectile.Speed,
                        mark.Projectile.Radius);
                    type = GunDeliveryType.Normal;
                    break;
                case "orb":
                    RequireProjectile(mark.Projectile, path + ".projectile");
                    orb = new GunOrbDeliverySettings(
                        mark.Projectile.Speed,
                        mark.Projectile.Radius);
                    type = GunDeliveryType.Orb;
                    break;
                case "rocket":
                    RequireProjectile(mark.Projectile, path + ".projectile");
                    if (mark.Explosion == null)
                    {
                        throw Error(path + ".explosion:rocket-requires-explosion");
                    }
                    rocket = new GunRocketDeliverySettings(
                        mark.Projectile.Speed,
                        mark.Projectile.Radius);
                    type = GunDeliveryType.Rocket;
                    break;
                case "beam":
                    if (mark.Beam == null)
                    {
                        throw Error(path + ".beam:missing");
                    }
                    RequireFinitePositive(mark.Beam.Width, path + ".beam.width");
                    laser = new GunLaserDeliverySettings(mark.Beam.Width);
                    type = GunDeliveryType.Laser;
                    break;
                default:
                    throw Error(
                        path
                        + ".projectileType:unsupported:"
                        + mark.ProjectileType);
            }

            return ShotPattern.Create(
                type,
                normal,
                orb,
                rocket,
                laser,
                null,
                guidance,
                impact,
                effects);
        }

        private static GunGuidanceSpec BuildGuidance(
            HomingDocument homing,
            string path)
        {
            if (homing == null)
            {
                return GunGuidanceSpec.Unguided();
            }
            if (!string.Equals(
                homing.TargetPolicy,
                "closest-to-aim",
                StringComparison.Ordinal))
            {
                throw Error(path + ".targetPolicy:unsupported");
            }
            return GunGuidanceSpec.Homing(
                homing.AcquisitionRange,
                homing.TurnRate,
                homing.ActivationDelay,
                GunTargetPolicy.ClosestToAim,
                homing.Reacquire
                    ? GunReacquisitionMode.ReuseTargetPolicy
                    : GunReacquisitionMode.None);
        }

        private static GunImpactSpec BuildImpact(
            MarkDocument mark,
            string path)
        {
            ImpactDocument source = RequireImpact(
                mark.Impact,
                path + ".impact");
            if (source.Pierce < 0)
            {
                throw Error(path + ".impact.pierce:invalid");
            }
            RequireFiniteNonNegative(
                source.Ricochet,
                path + ".impact.ricochet");
            RequireFiniteNonNegative(
                source.Knockback,
                path + ".impact.knockback");
            int ricochetTenths = FixedTenths(
                source.Ricochet,
                path + ".impact.ricochet");
            GunRicochetSpec ricochet = ricochetTenths == 0
                ? null
                : new GunRicochetSpec(
                    new RicochetValue(ricochetTenths),
                    1d,
                    0d,
                    0d);
            GunExplosionTriggerSpec explosion = mark.Explosion == null
                ? null
                : new GunExplosionTriggerSpec(true, true, true, true);
            return GunImpactSpec.Create(
                true,
                true,
                true,
                true,
                ricochet,
                explosion);
        }

        private static GunDamageOverTimeStats BuildDotStats(
            DotDocument dot,
            string path)
        {
            if (dot == null) return null;
            RequireFinitePositive(dot.DamagePerSecond, path + ".damagePerSecond");
            RequireFinitePositive(dot.Duration, path + ".duration");
            return new GunDamageOverTimeStats(
                dot.DamagePerSecond,
                dot.Duration);
        }

        private static GunEffects BuildEffects(
            MarkDocument mark,
            string path)
        {
            GunExplosionEffect explosion = null;
            if (mark.Explosion != null)
            {
                RequireFinitePositive(
                    mark.Explosion.Radius,
                    path + ".explosion.radius");
                if (mark.Explosion.EdgeDamageMultiplier < 0d
                    || mark.Explosion.EdgeDamageMultiplier > 1d)
                {
                    throw Error(
                        path
                        + ".explosion.edgeDamageMultiplier:invalid");
                }
                explosion = new GunExplosionEffect(
                    mark.Explosion.Radius,
                    mark.Explosion.EdgeDamageMultiplier);
            }

            GunDamageOverTimeEffect dot = null;
            if (mark.Dot != null)
            {
                RequireFinitePositive(
                    mark.Dot.TicksPerSecond,
                    path + ".dot.ticksPerSecond");
                if (mark.Dot.MaxStacks < 1)
                {
                    throw Error(path + ".dot.maxStacks:invalid");
                }
                dot = new GunDamageOverTimeEffect(
                    mark.Dot.TicksPerSecond,
                    mark.Dot.MaxStacks,
                    mark.Dot.RefreshDuration);
            }
            return new GunEffects(explosion, dot, null);
        }

        private static double ResolveRange(MarkDocument mark, string path)
        {
            if (string.Equals(
                mark.ProjectileType,
                "beam",
                StringComparison.Ordinal))
            {
                if (mark.Beam == null)
                {
                    throw Error(path + ".beam:missing");
                }
                RequireFinitePositive(mark.Beam.Range, path + ".beam.range");
                return mark.Beam.Range;
            }
            RequireProjectile(mark.Projectile, path + ".projectile");
            return mark.Projectile.Range;
        }

        private static ProjectileDocument RequireProjectile(
            ProjectileDocument projectile,
            string path)
        {
            if (projectile == null)
            {
                throw Error(path + ":missing");
            }
            RequireFinitePositive(projectile.Speed, path + ".speed");
            RequireFinitePositive(projectile.Radius, path + ".radius");
            RequireFinitePositive(projectile.Range, path + ".range");
            return projectile;
        }

        private static ImpactDocument RequireImpact(
            ImpactDocument impact,
            string path)
        {
            if (impact == null)
            {
                throw Error(path + ":missing");
            }
            return impact;
        }

        private static ArtDocument RequireArt(ArtDocument art, string path)
        {
            if (art == null)
            {
                throw Error(path + ":missing");
            }
            return art;
        }

        private static GunDamageCategory DamageCategory(
            string value,
            string path)
        {
            switch (RequireText(value, path))
            {
                case "physical":
                    return GunDamageCategory.Physical;
                case "thermal":
                    return GunDamageCategory.Thermal;
                case "chemical":
                    return GunDamageCategory.Chemical;
                case "energy":
                    return GunDamageCategory.Energy;
                default:
                    throw Error(path + ":unsupported:" + value);
            }
        }

        private static string RequireRarity(string value, string path)
        {
            value = RequireText(value, path);
            switch (value)
            {
                case "common":
                case "rare":
                case "epic":
                case "legendary":
                case "artifact":
                    return value;
                default:
                    throw Error(path + ":unsupported:" + value);
            }
        }

        private static int FixedTenths(double value, string path)
        {
            RequireFiniteNonNegative(value, path);
            double scaled = value * 10d;
            int rounded;
            try
            {
                rounded = checked((int)Math.Round(
                    scaled,
                    MidpointRounding.AwayFromZero));
            }
            catch (OverflowException exception)
            {
                throw Error(path + ":overflow", exception);
            }
            if (Math.Abs(scaled - rounded) > NumericTolerance)
            {
                throw Error(path + ":whole-tenths-required");
            }
            return rounded;
        }

        private static string StableToken(string value)
        {
            return RequireText(value, "stable-token").Replace('_', '-');
        }

        private static string RequireText(string value, string path)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw Error(path + ":text-required");
            }
            return value.Trim();
        }

        private static void RequireFinitePositive(double value, string path)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw Error(path + ":positive-number-required");
            }
        }

        private static void RequireFiniteNonNegative(double value, string path)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw Error(path + ":non-negative-number-required");
            }
        }

        private static InvalidOperationException Error(string code)
        {
            return new InvalidOperationException(
                "authored-gun-catalog-import-failed:" + code);
        }

        private static InvalidOperationException Error(
            string code,
            Exception inner)
        {
            return new InvalidOperationException(
                "authored-gun-catalog-import-failed:" + code,
                inner);
        }

        [DataContract]
        private sealed class CatalogDocument
        {
            [DataMember(Name = "schema", IsRequired = true)]
            public int Schema { get; set; }

            [DataMember(Name = "families", IsRequired = true)]
            public List<FamilyDocument> Families { get; set; }
        }

        [DataContract]
        private sealed class FamilyDocument
        {
            [DataMember(Name = "id", IsRequired = true)]
            public string Id { get; set; }

            [DataMember(Name = "category", IsRequired = true)]
            public string Category { get; set; }

            [DataMember(Name = "name", IsRequired = true)]
            public string Name { get; set; }

            [DataMember(Name = "rarity", IsRequired = true)]
            public string Rarity { get; set; }

            [DataMember(Name = "marks", IsRequired = true)]
            public List<MarkDocument> Marks { get; set; }
        }

        [DataContract]
        private sealed class MarkDocument
        {
            [DataMember(Name = "definitionId", IsRequired = true)]
            public string DefinitionId { get; set; }

            [DataMember(Name = "mark", IsRequired = true)]
            public int Mark { get; set; }

            [DataMember(Name = "peakLevel", IsRequired = true)]
            public int PeakLevel { get; set; }

            [DataMember(Name = "damage", IsRequired = true)]
            public double Damage { get; set; }

            [DataMember(Name = "projectileType", IsRequired = true)]
            public string ProjectileType { get; set; }

            [DataMember(Name = "damageType", IsRequired = true)]
            public string DamageType { get; set; }

            [DataMember(Name = "fire", IsRequired = true)]
            public FireDocument Fire { get; set; }

            [DataMember(Name = "shot", EmitDefaultValue = false)]
            public ShotDocument Shot { get; set; }

            [DataMember(Name = "projectile", EmitDefaultValue = false)]
            public ProjectileDocument Projectile { get; set; }

            [DataMember(Name = "beam", EmitDefaultValue = false)]
            public BeamDocument Beam { get; set; }

            [DataMember(Name = "impact", IsRequired = true)]
            public ImpactDocument Impact { get; set; }

            [DataMember(Name = "homing", EmitDefaultValue = false)]
            public HomingDocument Homing { get; set; }

            [DataMember(Name = "dot", EmitDefaultValue = false)]
            public DotDocument Dot { get; set; }

            [DataMember(Name = "explosion", EmitDefaultValue = false)]
            public ExplosionDocument Explosion { get; set; }

            [DataMember(Name = "art", IsRequired = true)]
            public ArtDocument Art { get; set; }
        }

        [DataContract]
        private sealed class FireDocument
        {
            [DataMember(Name = "mode", IsRequired = true)]
            public string Mode { get; set; }

            [DataMember(Name = "rate", IsRequired = true)]
            public double Rate { get; set; }

            [DataMember(Name = "shotsPerBurst", EmitDefaultValue = false)]
            public int ShotsPerBurst { get; set; }

            [DataMember(Name = "secondsBetweenShots", EmitDefaultValue = false)]
            public double SecondsBetweenShots { get; set; }
        }

        [DataContract]
        private sealed class ShotDocument
        {
            [DataMember(Name = "projectiles", IsRequired = true)]
            public int Projectiles { get; set; }

            [DataMember(Name = "spread", IsRequired = true)]
            public double Spread { get; set; }
        }

        [DataContract]
        private sealed class ProjectileDocument
        {
            [DataMember(Name = "speed", IsRequired = true)]
            public double Speed { get; set; }

            [DataMember(Name = "radius", IsRequired = true)]
            public double Radius { get; set; }

            [DataMember(Name = "range", IsRequired = true)]
            public double Range { get; set; }
        }

        [DataContract]
        private sealed class BeamDocument
        {
            [DataMember(Name = "range", IsRequired = true)]
            public double Range { get; set; }

            [DataMember(Name = "width", IsRequired = true)]
            public double Width { get; set; }
        }

        [DataContract]
        private sealed class ImpactDocument
        {
            [DataMember(Name = "pierce", IsRequired = true)]
            public int Pierce { get; set; }

            [DataMember(Name = "ricochet", IsRequired = true)]
            public double Ricochet { get; set; }

            [DataMember(Name = "knockback", IsRequired = true)]
            public double Knockback { get; set; }
        }

        [DataContract]
        private sealed class HomingDocument
        {
            [DataMember(Name = "acquisitionRange", IsRequired = true)]
            public double AcquisitionRange { get; set; }

            [DataMember(Name = "turnRate", IsRequired = true)]
            public double TurnRate { get; set; }

            [DataMember(Name = "activationDelay", IsRequired = true)]
            public double ActivationDelay { get; set; }

            [DataMember(Name = "targetPolicy", IsRequired = true)]
            public string TargetPolicy { get; set; }

            [DataMember(Name = "reacquire", IsRequired = true)]
            public bool Reacquire { get; set; }
        }

        [DataContract]
        private sealed class DotDocument
        {
            [DataMember(Name = "damagePerSecond", IsRequired = true)]
            public double DamagePerSecond { get; set; }

            [DataMember(Name = "duration", IsRequired = true)]
            public double Duration { get; set; }

            [DataMember(Name = "ticksPerSecond", IsRequired = true)]
            public double TicksPerSecond { get; set; }

            [DataMember(Name = "maxStacks", IsRequired = true)]
            public int MaxStacks { get; set; }

            [DataMember(Name = "refreshDuration", IsRequired = true)]
            public bool RefreshDuration { get; set; }
        }

        [DataContract]
        private sealed class ExplosionDocument
        {
            [DataMember(Name = "radius", IsRequired = true)]
            public double Radius { get; set; }

            [DataMember(Name = "edgeDamageMultiplier", IsRequired = true)]
            public double EdgeDamageMultiplier { get; set; }
        }

        [DataContract]
        private sealed class ArtDocument
        {
            [DataMember(Name = "side", IsRequired = true)]
            public string Side { get; set; }

            [DataMember(Name = "mounted", IsRequired = true)]
            public string Mounted { get; set; }

            [DataMember(Name = "delivery", IsRequired = true)]
            public string Delivery { get; set; }

            [DataMember(Name = "trail", IsRequired = true)]
            public string Trail { get; set; }

            [DataMember(Name = "impact", IsRequired = true)]
            public string Impact { get; set; }
        }
    }
}
