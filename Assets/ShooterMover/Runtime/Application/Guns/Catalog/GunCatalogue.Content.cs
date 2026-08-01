using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Catalog
{
    /// <summary>
    /// Current production gun-content authority. Combat numbers remain provisional except for
    /// the confirmed Rattler MK1 starter values, but the catalogue deliberately spans representative
    /// fire modes, delivery types, effects, guidance, and damage channels for integration testing.
    /// </summary>
    public static partial class GunCatalogue
    {
        public const string CatalogueVersion = "gun-catalogue-001";
        public const string CatalogueStatus = "production-provisional-system-matrix";

        private const string ProvisionalArchetypeId =
            "gun-archetype.provisional-system-matrix";
        private const double PlaceholderBaseWeight = 1d;

        private static readonly GunCatalogueView current =
            Build();

        private enum ProvisionalGunTestProfile
        {
            Rattler = 1,
            Sweeper = 2,
            Voltspike = 3,
            Prismata = 4,
            Crownfall = 5,
            Nullstar = 6,
            Baller = 7,
        }

        private sealed class ProvisionalCombatProfile
        {
            public ProvisionalCombatProfile(
                FireSettings fireSettings,
                GunShotPattern shotPattern,
                GunBaseStats baseStats,
                ShotPattern delivery,
                string presentationKey)
            {
                FireSettings = fireSettings
                    ?? throw new ArgumentNullException(nameof(fireSettings));
                ShotPattern = shotPattern
                    ?? throw new ArgumentNullException(nameof(shotPattern));
                BaseStats = baseStats
                    ?? throw new ArgumentNullException(nameof(baseStats));
                Delivery = delivery
                    ?? throw new ArgumentNullException(nameof(delivery));
                if (string.IsNullOrWhiteSpace(presentationKey))
                {
                    throw new ArgumentException(
                        "A provisional delivery presentation key is required.",
                        nameof(presentationKey));
                }

                PresentationKey = presentationKey.Trim();
            }

            public FireSettings FireSettings { get; }
            public GunShotPattern ShotPattern { get; }
            public GunBaseStats BaseStats { get; }
            public ShotPattern Delivery { get; }
            public string PresentationKey { get; }
        }

        public static GunCatalogueView Current
        {
            get { return current; }
        }

        private static GunCatalogueView Build()
        {
            var families = new[]
            {
                BuildFamily(
                    "rattler",
                    "Rattler",
                    "common",
                    new[] { 1, 25, 50 },
                    ProvisionalGunTestProfile.Rattler,
                    false),
                BuildFamily(
                    "sweeper",
                    "Sweeper",
                    "common",
                    new[] { 60, 80, 100 },
                    ProvisionalGunTestProfile.Sweeper,
                    true),
                BuildFamily(
                    "voltspike",
                    "Voltspike",
                    "rare",
                    new[] { 58, 79, 100 },
                    ProvisionalGunTestProfile.Voltspike,
                    true),
                BuildFamily(
                    "prismata",
                    "Prismata",
                    "epic",
                    new[] { 64, 84, 99 },
                    ProvisionalGunTestProfile.Prismata,
                    true),
                BuildFamily(
                    "baller",
                    "Baller",
                    "epic",
                    new[] { 15, 40, 65 },
                    ProvisionalGunTestProfile.Baller,
                    true),
                BuildFamily(
                    "crownfall",
                    "Crownfall",
                    "legendary",
                    new[] { 68, 88, 103 },
                    ProvisionalGunTestProfile.Crownfall,
                    true),
                BuildFamily(
                    "nullstar",
                    "Nullstar",
                    "artifact",
                    new[] { 70, 90, 110 },
                    ProvisionalGunTestProfile.Nullstar,
                    true),
            };

            return new GunCatalogueView(
                families,
                BuildGunCatalog(families),
                BuildEquipmentCatalog(families));
        }

        private static GunFamily BuildFamily(
            string slug,
            string displayName,
            string rarity,
            IReadOnlyList<int> anchors,
            ProvisionalGunTestProfile profile,
            bool provisionalFamily)
        {
            if (anchors == null || anchors.Count != 3)
            {
                throw new ArgumentException(
                    "Every gun family requires exactly three drop anchors.",
                    nameof(anchors));
            }

            string familyId = slug;
            StableId rarityId = StableId.Create("gun-rarity", rarity);
            var marks = new GunMark[3];
            for (int index = 0; index < marks.Length; index++)
            {
                int mark = index + 1;
                int anchor = anchors[index];
                bool provisionalCombat = provisionalFamily
                    || profile != ProvisionalGunTestProfile.Rattler
                    || mark != 1;
                marks[index] = BuildMark(
                    familyId,
                    slug,
                    displayName,
                    rarityId,
                    profile,
                    mark,
                    anchor,
                    Math.Min(anchor, 100),
                    provisionalCombat);
            }

            return new GunFamily(
                familyId,
                displayName,
                GunCategoryId(profile),
                rarityId,
                rarity,
                marks);
        }

        private static GunMark BuildMark(
            string familyId,
            string slug,
            string familyDisplayName,
            StableId rarityId,
            ProvisionalGunTestProfile profile,
            int mark,
            int dropAnchorLevel,
            int craftUnlockLevel,
            bool provisionalCombat)
        {
            string definitionId = familyId + ".mk" + mark;
            string equipmentValue = "gun-" + slug + "-mk" + mark;
            string presentationKey = slug + ".mk" + mark;
            ProvisionalCombatProfile combat = BuildCombatProfile(profile, mark);
            Gun blueprint = Gun.CreateAuthored(
                new GunIdentity(
                    new GunDefinitionId(definitionId),
                    familyDisplayName + " MK" + mark,
                    familyId),
                combat.FireSettings,
                combat.ShotPattern,
                combat.BaseStats,
                combat.Delivery,
                new GunPresentation(
                    "gun-art." + presentationKey + ".side-v1",
                    "gun-art." + presentationKey + ".mounted-top-v1",
                    "gun-delivery-art." + combat.PresentationKey + ".v1",
                    "gun-trail-art." + combat.PresentationKey + ".v1",
                    "gun-impact-art." + combat.PresentationKey + ".v1",
                    null),
                new GunDropMetadata(
                    StableId.Create("equipment", equipmentValue),
                    rarityId,
                    GunDropAvailability.Live,
                    dropAnchorLevel,
                    PlaceholderBaseWeight,
                    GunStrongboxEligibility.FromMinimumTier(1)));

            return new GunMark(
                mark,
                dropAnchorLevel,
                craftUnlockLevel,
                provisionalCombat,
                blueprint);
        }

        private static ProvisionalCombatProfile BuildCombatProfile(
            ProvisionalGunTestProfile profile,
            int mark)
        {
            switch (profile)
            {
                case ProvisionalGunTestProfile.Rattler:
                    return RattlerProfile(mark);
                case ProvisionalGunTestProfile.Sweeper:
                    return SweeperProfile(mark);
                case ProvisionalGunTestProfile.Voltspike:
                    return VoltspikeProfile(mark);
                case ProvisionalGunTestProfile.Prismata:
                    return PrismataProfile(mark);
                case ProvisionalGunTestProfile.Crownfall:
                    return CrownfallProfile(mark);
                case ProvisionalGunTestProfile.Nullstar:
                    return NullstarProfile(mark);
                case ProvisionalGunTestProfile.Baller:
                    return BallerProfile(mark);
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }

        private static ProvisionalCombatProfile RattlerProfile(int mark)
        {
            FireSettings fire;
            switch (mark)
            {
                case 1:
                    // Just-for-fun MK1 boost: five times the normal 4 shots/second.
                    fire = FireSettings.Automatic(20d);
                    break;
                case 2:
                    fire = FireSettings.SemiAutomatic(4d);
                    break;
                case 3:
                    fire = FireSettings.Burst(
                        4d / 3d,
                        new GunBurstSettings(3, 0.08d));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mark));
            }

            return TravellingProfile(
                fire,
                GunShotPattern.Canonical(1, 0d),
                1d,
                GunDamageCategory.Physical,
                null,
                1,
                0d,
                25d,
                GunDeliveryType.Normal,
                20d,
                0.1d,
                GunGuidanceSpec.Unguided(),
                StandardTravellingImpact(),
                GunEffects.None(),
                "normal-physical");
        }

        private static ProvisionalCombatProfile SweeperProfile(int mark)
        {
            if (mark < 1 || mark > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(mark));
            }

            return TravellingProfile(
                FireSettings.Automatic(2d),
                GunShotPattern.Canonical(3, 24d),
                1d,
                GunDamageCategory.Physical,
                null,
                1,
                6d,
                16d,
                GunDeliveryType.Normal,
                28d,
                0.12d,
                GunGuidanceSpec.Unguided(),
                StandardTravellingImpact(),
                GunEffects.None(),
                "shotgun-physical");
        }

        private static ProvisionalCombatProfile VoltspikeProfile(int mark)
        {
            FireSettings fire;
            double damage;
            switch (mark)
            {
                case 1:
                    fire = FireSettings.SemiAutomatic(2d);
                    damage = 2d;
                    break;
                case 2:
                    fire = FireSettings.Automatic(4d);
                    damage = 1d;
                    break;
                case 3:
                    fire = FireSettings.Burst(
                        4d / 3d,
                        new GunBurstSettings(3, 0.08d));
                    damage = 1d;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mark));
            }

            return TravellingProfile(
                fire,
                GunShotPattern.Canonical(1, 0d),
                damage,
                GunDamageCategory.Energy,
                null,
                1,
                2d,
                32d,
                GunDeliveryType.Normal,
                18d,
                0.11d,
                GunGuidanceSpec.Homing(
                    18d + (mark * 2d),
                    120d + (mark * 30d),
                    Math.Max(0.05d, 0.2d - (mark * 0.05d)),
                    GunTargetPolicy.ClosestToAim,
                    GunReacquisitionMode.ReuseTargetPolicy),
                StandardTravellingImpact(),
                GunEffects.None(),
                "seeking-energy");
        }

        private static ProvisionalCombatProfile PrismataProfile(int mark)
        {
            FireSettings fire;
            double damage;
            switch (mark)
            {
                case 1:
                    fire = FireSettings.SemiAutomatic(1d);
                    damage = 4d;
                    break;
                case 2:
                    fire = FireSettings.Automatic(2d);
                    damage = 2d;
                    break;
                case 3:
                    fire = FireSettings.Burst(
                        2d / 3d,
                        new GunBurstSettings(3, 0.12d));
                    damage = 2d;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mark));
            }

            return TravellingProfile(
                fire,
                GunShotPattern.Canonical(1, 0d),
                damage,
                GunDamageCategory.Chemical,
                null,
                1,
                3d,
                30d,
                GunDeliveryType.Orb,
                10d,
                0.42d,
                GunGuidanceSpec.Unguided(),
                StandardTravellingImpact(),
                GunEffects.None(),
                "orb-chemical");
        }

        private static ProvisionalCombatProfile BallerProfile(int mark)
        {
            double damage;
            switch (mark)
            {
                case 1:
                    damage = 10d;
                    break;
                case 2:
                    damage = 15d;
                    break;
                case 3:
                    damage = 20d;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mark));
            }

            return TravellingProfile(
                FireSettings.Automatic(3d),
                GunShotPattern.Canonical(1, 0d),
                damage,
                GunDamageCategory.Energy,
                null,
                3,
                0d,
                30d,
                GunDeliveryType.Orb,
                10d,
                0.5d,
                GunGuidanceSpec.Unguided(),
                StandardTravellingImpact(),
                GunEffects.None(),
                "orb-energy");
        }

        private static ProvisionalCombatProfile CrownfallProfile(int mark)
        {
            double radius;
            double minimumDamageMultiplier;
            switch (mark)
            {
                case 1:
                    radius = 2d;
                    minimumDamageMultiplier = 0.5d;
                    break;
                case 2:
                    radius = 2.5d;
                    minimumDamageMultiplier = 0.4d;
                    break;
                case 3:
                    radius = 3d;
                    minimumDamageMultiplier = 0.3d;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mark));
            }

            var explosion = new GunExplosionEffect(
                radius,
                minimumDamageMultiplier);
            var impact = GunImpactSpec.Create(
                true,
                true,
                true,
                true,
                null,
                new GunExplosionTriggerSpec(true, true, true, true));
            return TravellingProfile(
                FireSettings.SemiAutomatic(0.5d),
                GunShotPattern.Canonical(1, 0d),
                8d,
                GunDamageCategory.Thermal,
                null,
                1,
                16d,
                38d,
                GunDeliveryType.Rocket,
                18d,
                0.28d,
                GunGuidanceSpec.Unguided(),
                impact,
                new GunEffects(explosion, null, null),
                "rocket-thermal");
        }

        private static ProvisionalCombatProfile NullstarProfile(int mark)
        {
            FireSettings fire;
            double directDamage;
            switch (mark)
            {
                case 1:
                    fire = FireSettings.SemiAutomatic(1d);
                    directDamage = 4d;
                    break;
                case 2:
                    fire = FireSettings.Automatic(2d);
                    directDamage = 2d;
                    break;
                case 3:
                    fire = FireSettings.Burst(
                        2d / 3d,
                        new GunBurstSettings(3, 0.12d));
                    directDamage = 2d;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mark));
            }

            var damageOverTime = new GunDamageOverTimeStats(
                0.75d + (mark * 0.75d),
                2d + mark);
            var effects = new GunEffects(
                null,
                new GunDamageOverTimeEffect(
                    2d + mark,
                    mark,
                    true),
                null);
            return TravellingProfile(
                fire,
                GunShotPattern.Canonical(1, 0d),
                directDamage,
                GunDamageCategory.Chemical,
                damageOverTime,
                1,
                1d,
                28d,
                GunDeliveryType.Normal,
                16d,
                0.16d,
                GunGuidanceSpec.Unguided(),
                StandardTravellingImpact(),
                effects,
                "dot-chemical");
        }

        private static ProvisionalCombatProfile TravellingProfile(
            FireSettings fire,
            GunShotPattern shot,
            double directDamage,
            GunDamageCategory damageCategory,
            GunDamageOverTimeStats damageOverTime,
            int legacyPierce,
            double knockback,
            double range,
            GunDeliveryType deliveryType,
            double projectileSpeed,
            double projectileRadius,
            GunGuidanceSpec guidance,
            GunImpactSpec impact,
            GunEffects effects,
            string presentationKey)
        {
            GunNormalDeliverySettings normal = null;
            GunOrbDeliverySettings orb = null;
            GunRocketDeliverySettings rocket = null;
            switch (deliveryType)
            {
                case GunDeliveryType.Normal:
                    normal = new GunNormalDeliverySettings(
                        projectileSpeed,
                        projectileRadius);
                    break;
                case GunDeliveryType.Orb:
                    orb = new GunOrbDeliverySettings(
                        projectileSpeed,
                        projectileRadius);
                    break;
                case GunDeliveryType.Rocket:
                    rocket = new GunRocketDeliverySettings(
                        projectileSpeed,
                        projectileRadius);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(deliveryType),
                        "The provisional matrix currently uses travelling deliveries only.");
            }

            return new ProvisionalCombatProfile(
                fire,
                shot,
                new GunBaseStats(
                    directDamage,
                    damageCategory,
                    damageOverTime,
                    PierceValue.FromLegacyInteger(legacyPierce),
                    new RicochetValue(0),
                    knockback,
                    GunAttackDistance.Limited(range)),
                ShotPattern.Create(
                    deliveryType,
                    normal,
                    orb,
                    rocket,
                    null,
                    null,
                    guidance,
                    impact,
                    effects),
                presentationKey);
        }

        private static StableId GunCategoryId(
            ProvisionalGunTestProfile profile)
        {
            switch (profile)
            {
                case ProvisionalGunTestProfile.Rattler:
                    return StableId.Create("gun-category", "normal-firearm");
                case ProvisionalGunTestProfile.Sweeper:
                    return StableId.Create("gun-category", "shotgun");
                case ProvisionalGunTestProfile.Voltspike:
                    return StableId.Create(
                        "gun-category",
                        "seeking-projectile");
                case ProvisionalGunTestProfile.Prismata:
                case ProvisionalGunTestProfile.Baller:
                    return StableId.Create("gun-category", "orb");
                case ProvisionalGunTestProfile.Crownfall:
                    return StableId.Create("gun-category", "rocket");
                case ProvisionalGunTestProfile.Nullstar:
                    return StableId.Create(
                        "gun-category",
                        "damage-over-time");
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }

        private static GunImpactSpec StandardTravellingImpact()
        {
            return GunImpactSpec.Create(
                true,
                true,
                true,
                true,
                null,
                null);
        }
    }
}
