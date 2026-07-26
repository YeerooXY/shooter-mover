using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Catalog
{
    /// <summary>
    /// Current production weapon-content authority. Combat numbers remain provisional except for
    /// the confirmed Rattler MK1 starter values, but the catalogue deliberately spans representative
    /// fire modes, delivery types, effects, guidance, and damage channels for integration testing.
    /// </summary>
    public static partial class ProductionWeaponCatalogueV1
    {
        public const string CatalogueVersion = "weapon-catalogue-001";
        public const string CatalogueStatus = "production-provisional-system-matrix";

        private const string ProvisionalArchetypeId =
            "weapon-archetype.provisional-system-matrix";
        private const double PlaceholderBaseWeight = 1d;

        private static readonly ProductionWeaponCatalogueProjectionV1 current =
            Build();

        private enum ProvisionalWeaponTestProfile
        {
            Rattler = 1,
            Ironwake = 2,
            Voltspike = 3,
            Prismata = 4,
            Crownfall = 5,
            Nullstar = 6,
        }

        private sealed class ProvisionalCombatProfile
        {
            public ProvisionalCombatProfile(
                WeaponFireSettings fireSettings,
                WeaponShotPattern shotPattern,
                WeaponBaseStats baseStats,
                WeaponDeliverySpec delivery,
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

            public WeaponFireSettings FireSettings { get; }
            public WeaponShotPattern ShotPattern { get; }
            public WeaponBaseStats BaseStats { get; }
            public WeaponDeliverySpec Delivery { get; }
            public string PresentationKey { get; }
        }

        public static ProductionWeaponCatalogueProjectionV1 Current
        {
            get { return current; }
        }

        private static ProductionWeaponCatalogueProjectionV1 Build()
        {
            var families = new[]
            {
                BuildFamily(
                    "rattler",
                    "Rattler",
                    "common",
                    new[] { 1, 25, 50 },
                    ProvisionalWeaponTestProfile.Rattler,
                    false),
                BuildFamily(
                    "ironwake",
                    "Ironwake",
                    "common",
                    new[] { 60, 80, 100 },
                    ProvisionalWeaponTestProfile.Ironwake,
                    true),
                BuildFamily(
                    "voltspike",
                    "Voltspike",
                    "rare",
                    new[] { 58, 79, 100 },
                    ProvisionalWeaponTestProfile.Voltspike,
                    true),
                BuildFamily(
                    "prismata",
                    "Prismata",
                    "epic",
                    new[] { 64, 84, 99 },
                    ProvisionalWeaponTestProfile.Prismata,
                    true),
                BuildFamily(
                    "crownfall",
                    "Crownfall",
                    "legendary",
                    new[] { 68, 88, 103 },
                    ProvisionalWeaponTestProfile.Crownfall,
                    true),
                BuildFamily(
                    "nullstar",
                    "Nullstar",
                    "artifact",
                    new[] { 70, 90, 110 },
                    ProvisionalWeaponTestProfile.Nullstar,
                    true),
            };

            return new ProductionWeaponCatalogueProjectionV1(
                families,
                BuildWeaponCatalog(families),
                BuildEquipmentCatalog(families));
        }

        private static ProductionWeaponFamilyV1 BuildFamily(
            string slug,
            string displayName,
            string rarity,
            IReadOnlyList<int> anchors,
            ProvisionalWeaponTestProfile profile,
            bool provisionalFamily)
        {
            if (anchors == null || anchors.Count != 3)
            {
                throw new ArgumentException(
                    "Every weapon family requires exactly three drop anchors.",
                    nameof(anchors));
            }

            string familyId = slug;
            StableId rarityId = StableId.Create("weapon-rarity", rarity);
            var marks = new ProductionWeaponMarkV1[3];
            for (int index = 0; index < marks.Length; index++)
            {
                int mark = index + 1;
                int anchor = anchors[index];
                bool provisionalCombat = provisionalFamily
                    || profile != ProvisionalWeaponTestProfile.Rattler
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

            return new ProductionWeaponFamilyV1(
                familyId,
                displayName,
                WeaponCategoryId(profile),
                rarityId,
                rarity,
                marks);
        }

        private static ProductionWeaponMarkV1 BuildMark(
            string familyId,
            string slug,
            string familyDisplayName,
            StableId rarityId,
            ProvisionalWeaponTestProfile profile,
            int mark,
            int dropAnchorLevel,
            int craftUnlockLevel,
            bool provisionalCombat)
        {
            string definitionId = familyId + ".mk" + mark;
            string equipmentValue = "weapon-" + slug + "-mk" + mark;
            string presentationKey = slug + ".mk" + mark;
            ProvisionalCombatProfile combat = BuildCombatProfile(profile, mark);
            WeaponBlueprint blueprint = WeaponBlueprint.CreateAuthored(
                new WeaponIdentity(
                    new WeaponDefinitionId(definitionId),
                    familyDisplayName + " MK" + mark,
                    familyId),
                combat.FireSettings,
                combat.ShotPattern,
                combat.BaseStats,
                combat.Delivery,
                new WeaponPresentation(
                    "weapon-art." + presentationKey + ".side-v1",
                    "weapon-art." + presentationKey + ".mounted-top-v1",
                    "weapon-delivery-art." + combat.PresentationKey + ".v1",
                    "weapon-trail-art." + combat.PresentationKey + ".v1",
                    "weapon-impact-art." + combat.PresentationKey + ".v1",
                    null),
                new WeaponDropMetadata(
                    StableId.Create("equipment", equipmentValue),
                    rarityId,
                    WeaponDropAvailability.Live,
                    dropAnchorLevel,
                    PlaceholderBaseWeight,
                    WeaponStrongboxEligibility.FromMinimumTier(1)));

            return new ProductionWeaponMarkV1(
                mark,
                dropAnchorLevel,
                craftUnlockLevel,
                provisionalCombat,
                blueprint);
        }

        private static ProvisionalCombatProfile BuildCombatProfile(
            ProvisionalWeaponTestProfile profile,
            int mark)
        {
            switch (profile)
            {
                case ProvisionalWeaponTestProfile.Rattler:
                    return RattlerProfile(mark);
                case ProvisionalWeaponTestProfile.Ironwake:
                    return IronwakeProfile(mark);
                case ProvisionalWeaponTestProfile.Voltspike:
                    return VoltspikeProfile(mark);
                case ProvisionalWeaponTestProfile.Prismata:
                    return PrismataProfile(mark);
                case ProvisionalWeaponTestProfile.Crownfall:
                    return CrownfallProfile(mark);
                case ProvisionalWeaponTestProfile.Nullstar:
                    return NullstarProfile(mark);
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }

        private static ProvisionalCombatProfile RattlerProfile(int mark)
        {
            WeaponFireSettings fire;
            switch (mark)
            {
                case 1:
                    fire = WeaponFireSettings.Automatic(4d);
                    break;
                case 2:
                    fire = WeaponFireSettings.SemiAutomatic(4d);
                    break;
                case 3:
                    fire = WeaponFireSettings.Burst(
                        4d / 3d,
                        new WeaponBurstSettings(3, 0.08d));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mark));
            }

            return TravellingProfile(
                fire,
                WeaponShotPattern.Canonical(1, 0d),
                1d,
                WeaponDamageCategory.Physical,
                null,
                1,
                0d,
                25d,
                WeaponDeliveryType.Normal,
                20d,
                0.1d,
                WeaponGuidanceSpec.Unguided(),
                StandardTravellingImpact(),
                WeaponEffects.None(),
                "normal-physical");
        }

        private static ProvisionalCombatProfile IronwakeProfile(int mark)
        {
            int pellets;
            double spread;
            double range;
            switch (mark)
            {
                case 1:
                    pellets = 6;
                    spread = 24d;
                    range = 16d;
                    break;
                case 2:
                    pellets = 8;
                    spread = 28d;
                    range = 18d;
                    break;
                case 3:
                    pellets = 10;
                    spread = 32d;
                    range = 20d;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mark));
            }

            return TravellingProfile(
                WeaponFireSettings.SemiAutomatic(1d),
                WeaponShotPattern.Canonical(pellets, spread),
                4d / pellets,
                WeaponDamageCategory.Physical,
                null,
                1,
                6d,
                range,
                WeaponDeliveryType.Normal,
                28d,
                0.12d,
                WeaponGuidanceSpec.Unguided(),
                StandardTravellingImpact(),
                WeaponEffects.None(),
                "shotgun-physical");
        }

        private static ProvisionalCombatProfile VoltspikeProfile(int mark)
        {
            WeaponFireSettings fire;
            double damage;
            switch (mark)
            {
                case 1:
                    fire = WeaponFireSettings.SemiAutomatic(2d);
                    damage = 2d;
                    break;
                case 2:
                    fire = WeaponFireSettings.Automatic(4d);
                    damage = 1d;
                    break;
                case 3:
                    fire = WeaponFireSettings.Burst(
                        4d / 3d,
                        new WeaponBurstSettings(3, 0.08d));
                    damage = 1d;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mark));
            }

            return TravellingProfile(
                fire,
                WeaponShotPattern.Canonical(1, 0d),
                damage,
                WeaponDamageCategory.Energy,
                null,
                1,
                2d,
                32d,
                WeaponDeliveryType.Normal,
                18d,
                0.11d,
                WeaponGuidanceSpec.Homing(
                    18d + (mark * 2d),
                    120d + (mark * 30d),
                    Math.Max(0.05d, 0.2d - (mark * 0.05d)),
                    WeaponTargetPolicy.ClosestToAim,
                    WeaponReacquisitionMode.ReuseTargetPolicy),
                StandardTravellingImpact(),
                WeaponEffects.None(),
                "seeking-energy");
        }

        private static ProvisionalCombatProfile PrismataProfile(int mark)
        {
            WeaponFireSettings fire;
            double damage;
            switch (mark)
            {
                case 1:
                    fire = WeaponFireSettings.SemiAutomatic(1d);
                    damage = 4d;
                    break;
                case 2:
                    fire = WeaponFireSettings.Automatic(2d);
                    damage = 2d;
                    break;
                case 3:
                    fire = WeaponFireSettings.Burst(
                        2d / 3d,
                        new WeaponBurstSettings(3, 0.12d));
                    damage = 2d;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mark));
            }

            return TravellingProfile(
                fire,
                WeaponShotPattern.Canonical(1, 0d),
                damage,
                WeaponDamageCategory.Chemical,
                null,
                1,
                3d,
                30d,
                WeaponDeliveryType.Orb,
                10d,
                0.42d,
                WeaponGuidanceSpec.Unguided(),
                StandardTravellingImpact(),
                WeaponEffects.None(),
                "orb-chemical");
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

            var explosion = new WeaponExplosionEffect(
                radius,
                minimumDamageMultiplier);
            var impact = WeaponImpactSpec.Create(
                true,
                true,
                true,
                true,
                null,
                new WeaponExplosionTriggerSpec(true, true, true, true));
            return TravellingProfile(
                WeaponFireSettings.SemiAutomatic(0.5d),
                WeaponShotPattern.Canonical(1, 0d),
                8d,
                WeaponDamageCategory.Thermal,
                null,
                1,
                16d,
                38d,
                WeaponDeliveryType.Rocket,
                18d,
                0.28d,
                WeaponGuidanceSpec.Unguided(),
                impact,
                new WeaponEffects(explosion, null, null),
                "rocket-thermal");
        }

        private static ProvisionalCombatProfile NullstarProfile(int mark)
        {
            WeaponFireSettings fire;
            double directDamage;
            switch (mark)
            {
                case 1:
                    fire = WeaponFireSettings.SemiAutomatic(1d);
                    directDamage = 4d;
                    break;
                case 2:
                    fire = WeaponFireSettings.Automatic(2d);
                    directDamage = 2d;
                    break;
                case 3:
                    fire = WeaponFireSettings.Burst(
                        2d / 3d,
                        new WeaponBurstSettings(3, 0.12d));
                    directDamage = 2d;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mark));
            }

            var damageOverTime = new WeaponDamageOverTimeStats(
                0.75d + (mark * 0.75d),
                2d + mark);
            var effects = new WeaponEffects(
                null,
                new WeaponDamageOverTimeEffect(
                    2d + mark,
                    mark,
                    true),
                null);
            return TravellingProfile(
                fire,
                WeaponShotPattern.Canonical(1, 0d),
                directDamage,
                WeaponDamageCategory.Chemical,
                damageOverTime,
                1,
                1d,
                28d,
                WeaponDeliveryType.Normal,
                16d,
                0.16d,
                WeaponGuidanceSpec.Unguided(),
                StandardTravellingImpact(),
                effects,
                "dot-chemical");
        }

        private static ProvisionalCombatProfile TravellingProfile(
            WeaponFireSettings fire,
            WeaponShotPattern shot,
            double directDamage,
            WeaponDamageCategory damageCategory,
            WeaponDamageOverTimeStats damageOverTime,
            int legacyPierce,
            double knockback,
            double range,
            WeaponDeliveryType deliveryType,
            double projectileSpeed,
            double projectileRadius,
            WeaponGuidanceSpec guidance,
            WeaponImpactSpec impact,
            WeaponEffects effects,
            string presentationKey)
        {
            WeaponNormalDeliverySettings normal = null;
            WeaponOrbDeliverySettings orb = null;
            WeaponRocketDeliverySettings rocket = null;
            switch (deliveryType)
            {
                case WeaponDeliveryType.Normal:
                    normal = new WeaponNormalDeliverySettings(
                        projectileSpeed,
                        projectileRadius);
                    break;
                case WeaponDeliveryType.Orb:
                    orb = new WeaponOrbDeliverySettings(
                        projectileSpeed,
                        projectileRadius);
                    break;
                case WeaponDeliveryType.Rocket:
                    rocket = new WeaponRocketDeliverySettings(
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
                new WeaponBaseStats(
                    directDamage,
                    damageCategory,
                    damageOverTime,
                    PierceValue.FromLegacyInteger(legacyPierce),
                    new RicochetValue(0),
                    knockback,
                    WeaponAttackDistance.Limited(range)),
                WeaponDeliverySpec.Create(
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

        private static StableId WeaponCategoryId(
            ProvisionalWeaponTestProfile profile)
        {
            switch (profile)
            {
                case ProvisionalWeaponTestProfile.Rattler:
                    return StableId.Create("weapon-category", "normal-firearm");
                case ProvisionalWeaponTestProfile.Ironwake:
                    return StableId.Create("weapon-category", "shotgun");
                case ProvisionalWeaponTestProfile.Voltspike:
                    return StableId.Create(
                        "weapon-category",
                        "seeking-projectile");
                case ProvisionalWeaponTestProfile.Prismata:
                    return StableId.Create("weapon-category", "orb");
                case ProvisionalWeaponTestProfile.Crownfall:
                    return StableId.Create("weapon-category", "rocket");
                case ProvisionalWeaponTestProfile.Nullstar:
                    return StableId.Create(
                        "weapon-category",
                        "damage-over-time");
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }

        private static WeaponImpactSpec StandardTravellingImpact()
        {
            return WeaponImpactSpec.Create(
                true,
                true,
                true,
                true,
                null,
                null);
        }
    }
}
