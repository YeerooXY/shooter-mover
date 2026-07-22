using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Weapons.Live;

namespace ShooterMover.Tests.EditMode.Weapons.Live
{
    public sealed partial class InventoryBackedWeaponExecutionAdapterTests
    {
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
                    Definition(
                        "weapon.blaster-machine-gun",
                        "Kinetic",
                        10d,
                        1,
                        0d,
                        40d,
                        30d,
                        5d,
                        1),
                    Definition(
                        "weapon.shotgun",
                        "Kinetic",
                        2d,
                        7,
                        24d,
                        30d,
                        15d,
                        3d,
                        0),
                    Definition(
                        "weapon.rocket-launcher",
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
                    Definition(
                        "weapon.flamethrower",
                        "Thermal",
                        5d,
                        4,
                        12d,
                        10d,
                        8d,
                        1d,
                        0,
                        0d,
                        0d,
                        4d,
                        2d,
                        2d,
                        3d),
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
            int pierce,
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
                pierce,
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

        private static PlayerHoldingsService CreateHoldingsService()
        {
            return new PlayerHoldingsService(
                HoldingsAuthorityId,
                1000L,
                new AcceptingEquipmentValidator());
        }

        private static void AddEquipment(
            PlayerHoldingsService service,
            EquipmentInstance equipment,
            long expectedSequence)
        {
            string token = equipment.InstanceId.ToString().Replace('.', '-');
            PlayerHoldingsMutationResultV1 result = service.Apply(
                PlayerHoldingsCommandV1.AddEquipment(
                    StableId.Parse("transaction." + token),
                    StableId.Parse("operation." + token),
                    HoldingsAuthorityId,
                    equipment,
                    HoldingProvenanceV1.Create(
                        StableId.Parse("grant." + token),
                        StableId.Parse("source.test")),
                    expectedSequence));
            Assert.That(result.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
        }

    }
}
