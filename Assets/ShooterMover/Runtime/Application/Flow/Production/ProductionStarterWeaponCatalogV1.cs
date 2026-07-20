using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Flow.Production
{
    public static class ProductionStarterWeaponCatalogV1
    {
        public const string ArcWeaponDefinitionId = "weapon.arc-gun";
        public const string RicochetWeaponDefinitionId =
            "weapon.ricochet-gun";

        public static readonly StableId BlasterEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-blaster");
        public static readonly StableId ShotgunEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-shotgun");
        public static readonly StableId RocketEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-rocket-launcher");
        public static readonly StableId ArcEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-arc-gun");
        public static readonly StableId RicochetEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-ricochet-gun");

        public static readonly StableId BlasterEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.flow-draft-slot-1");
        public static readonly StableId ShotgunEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.flow-draft-slot-2");
        public static readonly StableId RocketEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.flow-draft-slot-3");
        public static readonly StableId ArcEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.flow-draft-slot-4");
        public static readonly StableId RicochetEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.production-starter-ricochet");

        private static readonly StableId[] initialEquipmentDefinitionStableIds =
        {
            BlasterEquipmentDefinitionStableId,
            ShotgunEquipmentDefinitionStableId,
            RocketEquipmentDefinitionStableId,
            ArcEquipmentDefinitionStableId,
        };

        private static readonly StableId[] allEquipmentDefinitionStableIds =
        {
            BlasterEquipmentDefinitionStableId,
            ShotgunEquipmentDefinitionStableId,
            RocketEquipmentDefinitionStableId,
            ArcEquipmentDefinitionStableId,
            RicochetEquipmentDefinitionStableId,
        };

        private static readonly Dictionary<StableId, StableId>
            knownDefinitionByInstance = BuildKnownDefinitionMap();

        public static IReadOnlyList<StableId>
            InitialEquipmentDefinitionStableIds
        {
            get { return initialEquipmentDefinitionStableIds; }
        }

        public static IReadOnlyList<StableId>
            AllEquipmentDefinitionStableIds
        {
            get { return allEquipmentDefinitionStableIds; }
        }

        public static bool TryResolveDefinitionForInstance(
            StableId equipmentInstanceStableId,
            out StableId equipmentDefinitionStableId)
        {
            equipmentDefinitionStableId = null;
            return equipmentInstanceStableId != null
                && knownDefinitionByInstance.TryGetValue(
                    equipmentInstanceStableId,
                    out equipmentDefinitionStableId);
        }

        public static StableId ReserveInstanceForDefinition(
            StableId equipmentDefinitionStableId)
        {
            if (equipmentDefinitionStableId
                == BlasterEquipmentDefinitionStableId)
            {
                return BlasterEquipmentInstanceStableId;
            }
            if (equipmentDefinitionStableId
                == ShotgunEquipmentDefinitionStableId)
            {
                return ShotgunEquipmentInstanceStableId;
            }
            if (equipmentDefinitionStableId
                == RocketEquipmentDefinitionStableId)
            {
                return RocketEquipmentInstanceStableId;
            }
            if (equipmentDefinitionStableId
                == ArcEquipmentDefinitionStableId)
            {
                return ArcEquipmentInstanceStableId;
            }
            if (equipmentDefinitionStableId
                == RicochetEquipmentDefinitionStableId)
            {
                return RicochetEquipmentInstanceStableId;
            }

            throw new ArgumentException(
                "Unknown production starter equipment definition.",
                nameof(equipmentDefinitionStableId));
        }

        public static EquipmentCatalog BuildEquipmentCatalog()
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(
                StableId.Parse("equipment-quality.common"),
                "Common",
                1);
            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[]
                {
                    WeaponEquipment(
                        BlasterEquipmentDefinitionStableId,
                        "family.blaster",
                        "Blaster",
                        "weapon.blaster-machine-gun",
                        common),
                    WeaponEquipment(
                        ShotgunEquipmentDefinitionStableId,
                        "family.shotgun",
                        "Shotgun",
                        "weapon.shotgun",
                        common),
                    WeaponEquipment(
                        RocketEquipmentDefinitionStableId,
                        "family.rocket-launcher",
                        "Rocket Launcher",
                        "weapon.rocket-launcher",
                        common),
                    WeaponEquipment(
                        ArcEquipmentDefinitionStableId,
                        "family.arc-gun",
                        "Arc Gun",
                        ArcWeaponDefinitionId,
                        common),
                    WeaponEquipment(
                        RicochetEquipmentDefinitionStableId,
                        "family.ricochet-gun",
                        "Ricochet Gun",
                        RicochetWeaponDefinitionId,
                        common),
                },
                Array.Empty<AugmentDefinition>());
            if (!result.IsValid || result.Catalog == null)
            {
                throw new InvalidOperationException(
                    "The production starter equipment catalog is invalid.");
            }

            return result.Catalog;
        }

        public static WeaponCatalog BuildWeaponCatalog()
        {
            var rules = new WeaponCatalogRules(
                true,
                false,
                "20-25",
                new[] { 75, 105, 135 },
                new[] { "Kinetic", "Energized" },
                10,
                true,
                true,
                true);
            var inputs = new WeaponCatalogInputs(
                12d,
                0.05d,
                0.055d,
                0.06d,
                new Dictionary<string, WeaponRarityInput>(
                    StringComparer.Ordinal)
                {
                    {
                        "Common",
                        new WeaponRarityInput(
                            "Common",
                            1000d,
                            0,
                            4d,
                            13d)
                    },
                });
            var archetype = new WeaponArchetypeDefinition(
                "DemoCutover",
                "Demo Cutover",
                1d,
                1d,
                1,
                1,
                0d,
                10d,
                10d,
                1d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0,
                0,
                0d,
                0d,
                1d);

            WeaponFamilyDefinition[] families =
            {
                Family("production-starter-blaster", "Blaster", "Kinetic"),
                Family("production-starter-shotgun", "Shotgun", "Kinetic"),
                Family(
                    "production-starter-rocket",
                    "Rocket Launcher",
                    "Kinetic"),
                Family("production-starter-arc", "Arc Gun", "Energized"),
                Family(
                    "production-starter-ricochet",
                    "Ricochet Gun",
                    "Kinetic"),
            };
            return new WeaponCatalog(
                "1.0",
                "production-hub-loadout",
                rules,
                inputs,
                new Dictionary<string, WeaponArchetypeDefinition>(
                    StringComparer.Ordinal)
                {
                    { "DemoCutover", archetype },
                },
                families,
                new[]
                {
                    WeaponDefinition(
                        "weapon.blaster-machine-gun",
                        "Blaster",
                        "production-starter-blaster",
                        "Kinetic",
                        10d,
                        1,
                        0d,
                        40d,
                        30d,
                        5d,
                        1),
                    WeaponDefinition(
                        "weapon.shotgun",
                        "Shotgun",
                        "production-starter-shotgun",
                        "Kinetic",
                        2d,
                        7,
                        24d,
                        30d,
                        15d,
                        3d,
                        0),
                    WeaponDefinition(
                        "weapon.rocket-launcher",
                        "Rocket Launcher",
                        "production-starter-rocket",
                        "Kinetic",
                        1d,
                        1,
                        0d,
                        12d,
                        35d,
                        4d,
                        0,
                        20d,
                        3d),
                    WeaponDefinition(
                        ArcWeaponDefinitionId,
                        "Arc Gun",
                        "production-starter-arc",
                        "Energized",
                        1.5d,
                        1,
                        0d,
                        12d,
                        12d,
                        12d,
                        0,
                        0d,
                        0d,
                        3,
                        6d),
                    WeaponDefinition(
                        RicochetWeaponDefinitionId,
                        "Ricochet Gun",
                        "production-starter-ricochet",
                        "Kinetic",
                        2.5d,
                        1,
                        0d,
                        24d,
                        30d,
                        8d,
                        0),
                });
        }

        private static Dictionary<StableId, StableId>
            BuildKnownDefinitionMap()
        {
            var result = new Dictionary<StableId, StableId>();
            AddKnown(
                result,
                BlasterEquipmentInstanceStableId,
                BlasterEquipmentDefinitionStableId);
            AddKnown(
                result,
                ShotgunEquipmentInstanceStableId,
                ShotgunEquipmentDefinitionStableId);
            AddKnown(
                result,
                RocketEquipmentInstanceStableId,
                RocketEquipmentDefinitionStableId);
            AddKnown(
                result,
                ArcEquipmentInstanceStableId,
                ArcEquipmentDefinitionStableId);
            AddKnown(
                result,
                RicochetEquipmentInstanceStableId,
                RicochetEquipmentDefinitionStableId);
            return result;
        }

        private static void AddKnown(
            IDictionary<StableId, StableId> map,
            StableId instanceStableId,
            StableId definitionStableId)
        {
            map.Add(instanceStableId, definitionStableId);
        }

        private static EquipmentDefinition WeaponEquipment(
            StableId definitionStableId,
            string family,
            string displayName,
            string runtime,
            EquipmentQualityTier quality)
        {
            return EquipmentDefinition.Create(
                definitionStableId,
                EquipmentCategoryIds.Weapon,
                StableId.Parse(family),
                displayName,
                StableId.Parse(runtime),
                InclusiveIntRange.Create(1, 100),
                0,
                new[] { quality },
                Array.Empty<StableId>());
        }

        private static WeaponFamilyDefinition Family(
            string id,
            string displayName,
            string damageType)
        {
            return new WeaponFamilyDefinition(
                id,
                displayName,
                "DemoCutover",
                damageType,
                "Universal",
                1,
                20,
                20,
                3,
                "Common",
                "Common",
                "Common",
                1d,
                "Standard",
                "Production vertical slice",
                "Production vertical slice",
                WeaponCatalogAvailability.Live,
                Array.Empty<string>());
        }

        private static WeaponDefinitionData WeaponDefinition(
            string id,
            string displayName,
            string family,
            string damageType,
            double fireRate,
            int projectiles,
            double spread,
            double speed,
            double range,
            double damage,
            int pierce,
            double areaDamage = 0d,
            double explosionRadius = 0d,
            int chainTargets = 0,
            double chainRange = 0d)
        {
            bool explosive = areaDamage > 0d;
            return new WeaponDefinitionData(
                id,
                displayName,
                family,
                1,
                damageType,
                "DemoCutover",
                "Universal",
                1,
                1,
                1,
                "Common",
                1000d,
                1d,
                1000d,
                4d,
                13d,
                "Standard",
                false,
                "Standard",
                1d,
                100d,
                10d,
                explosive ? 0.2d : 1d,
                explosive ? 0.8d : 0d,
                0d,
                fireRate,
                projectiles,
                1,
                damage,
                spread,
                speed,
                range,
                pierce,
                explosionRadius,
                areaDamage,
                0d,
                0d,
                0d,
                0d,
                chainTargets,
                chainRange,
                0.5d,
                1d,
                0d,
                "Production vertical slice",
                "Production vertical slice",
                WeaponCatalogAvailability.Live,
                Array.Empty<string>());
        }
    }
}
