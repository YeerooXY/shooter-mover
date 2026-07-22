using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Weapons.Live
{
    public sealed partial class InventoryWeaponRuntimePlayModeTests
    {
        private static EquipmentCatalog EquipmentCatalogFor(
            IEnumerable<EquipmentInstance> equipment)
        {
            var definitions = new List<EquipmentDefinition>();
            foreach (EquipmentInstance instance in equipment)
            {
                definitions.Add(EquipmentDefinition.Create(
                    instance.DefinitionId,
                    EquipmentCategoryIds.Weapon,
                    StableId.Parse("equipment-family.playmode-weapons"),
                    instance.DefinitionId.ToString(),
                    RuntimeWeaponId(instance.DefinitionId),
                    InclusiveIntRange.Create(1, 100),
                    0,
                    new[] { EquipmentQualityTier.Create(QualityId, "Common", 1) },
                    new StableId[0]));
            }

            EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                definitions,
                new AugmentDefinition[0]);
            Assert.That(build.IsValid, Is.True);
            return build.Catalog;
        }

        private static StableId RuntimeWeaponId(StableId equipmentDefinitionId)
        {
            string value = equipmentDefinitionId.ToString();
            if (value.EndsWith("shotgun", StringComparison.Ordinal))
            {
                return StableId.Parse("weapon.shotgun");
            }

            if (value.EndsWith("rocket", StringComparison.Ordinal))
            {
                return StableId.Parse("weapon.rocket-launcher");
            }

            if (value.EndsWith("flamethrower", StringComparison.Ordinal))
            {
                return StableId.Parse("weapon.flamethrower");
            }

            return StableId.Parse("weapon.blaster-machine-gun");
        }

        private static WeaponCatalog WeaponCatalogFor()
        {
            var rules = new WeaponCatalogRules(
                true,
                "20-25",
                new[] { 75, 105, 135 },
                new[] { "Kinetic", "Thermal" },
                10,
                true,
                true,
                true);
            var inputs = new WeaponCatalogInputs(
                12d,
                0.05d,
                0.055d,
                0.06d,
                new Dictionary<string, WeaponRarityInput>(StringComparer.Ordinal)
                {
                    { "Common", new WeaponRarityInput("Common", 1000d, 0, 4d, 13d) },
                });
            var archetype = new WeaponArchetypeDefinition(
                "Test",
                "Test",
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
            var family = new WeaponFamilyDefinition(
                "test-family",
                "Test Family",
                "Test",
                "Kinetic",
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
                "Test",
                "Test",
                WeaponCatalogAvailability.Live,
                new string[0]);
            return new WeaponCatalog(
                "0.1",
                "test",
                rules,
                inputs,
                new Dictionary<string, WeaponArchetypeDefinition>(StringComparer.Ordinal)
                {
                    { "Test", archetype },
                },
                new[] { family },
                new[]
                {
                    Definition("weapon.blaster-machine-gun", "Kinetic", 10d, 1, 0d, 40d, 30d, 5d),
                    Definition("weapon.shotgun", "Kinetic", 2d, 7, 24d, 30d, 15d, 3d),
                    Definition("weapon.rocket-launcher", "Kinetic", 1d, 1, 0d, 12d, 35d, 4d, 20d, 3d),
                    Definition("weapon.flamethrower", "Thermal", 5d, 4, 12d, 10d, 8d, 1d, 0d, 0d, 4d, 2d, 2d, 3d),
                });
        }

        private static WeaponDefinitionData Definition(
            string id,
            string damageType,
            double fireRate,
            int projectiles,
            double spread,
            double speed,
            double range,
            double damage,
            double areaDamage = 0d,
            double explosionRadius = 0d,
            double dotDps = 0d,
            double dotDuration = 0d,
            double poolRadius = 0d,
            double poolDuration = 0d)
        {
            bool explosive = areaDamage > 0d;
            bool dot = dotDps > 0d;
            return new WeaponDefinitionData(
                id,
                id,
                "test-family",
                1,
                damageType,
                "Test",
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
                explosive ? 0.2d : dot ? 0.2d : 1d,
                explosive ? 0.8d : 0d,
                dot ? 0.8d : 0d,
                fireRate,
                projectiles,
                1,
                damage,
                spread,
                speed,
                range,
                0,
                explosionRadius,
                areaDamage,
                dotDps,
                dotDuration,
                poolRadius,
                poolDuration,
                0,
                0d,
                0.5d,
                1d,
                0d,
                "Test",
                "Test",
                WeaponCatalogAvailability.Live,
                new string[0]);
        }

        private static EquipmentInstance Equipment(
            string instanceId,
            string definitionId)
        {
            return EquipmentInstance.Create(
                StableId.Parse(instanceId),
                StableId.Parse(definitionId),
                1,
                QualityId,
                new AugmentInstance[0]);
        }

    }
}
