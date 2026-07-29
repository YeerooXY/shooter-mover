using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.UnityAdapters.Guns.Live;

namespace ShooterMover.Tests.EditMode.Guns.Live
{
    public sealed partial class InventoryBackedGunExecutionBridgeTests
    {
        private static GunCatalog GunCatalogFor()
        {
            var rules = new GunCatalogRules(
                true,
                "20-25",
                new[] { 75, 105, 135 },
                new[] { "Kinetic", "Thermal" },
                10,
                true,
                true,
                true);
            var inputs = new GunCatalogInputs(
                12d,
                0.05d,
                0.055d,
                0.06d,
                new Dictionary<string, GunRarityInput>(StringComparer.Ordinal)
                {
                    { "Common", new GunRarityInput("Common", 1000d, 0, 4d, 13d) },
                });
            var archetype = new GunArchetypeDefinition(
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
            var family = new GunFamilyDefinition(
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
                GunCatalogAvailability.Live,
                new string[0]);
            return new GunCatalog(
                "0.1",
                "test",
                rules,
                inputs,
                new Dictionary<string, GunArchetypeDefinition>(StringComparer.Ordinal)
                {
                    { "Test", archetype },
                },
                new[] { family },
                new[]
                {
                    Definition(
                        "rattler.mk1",
                        "Kinetic",
                        10d,
                        1,
                        0d,
                        40d,
                        30d,
                        5d,
                        1),
                    Definition(
                        "ironwake.mk1",
                        "Kinetic",
                        2d,
                        7,
                        24d,
                        30d,
                        15d,
                        3d,
                        0),
                    Definition(
                        "crownfall.mk1",
                        "Thermal",
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
                        "nullstar.mk1",
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

        private static GunDefinitionData Definition(
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
            return new GunDefinitionData(
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
                GunCatalogAvailability.Live,
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

        private static PlayerHoldingsActions CreateHoldingsService()
        {
            return new PlayerHoldingsActions(
                HoldingsAuthorityId,
                1000L,
                new AcceptingEquipmentValidator());
        }

        private static void AddEquipment(
            PlayerHoldingsActions service,
            EquipmentInstance equipment,
            long expectedSequence)
        {
            string token = equipment.InstanceId.ToString().Replace('.', '-');
            PlayerHoldingsMutationResult result = service.Apply(
                PlayerHoldingsCommand.AddEquipment(
                    StableId.Parse("transaction." + token),
                    StableId.Parse("operation." + token),
                    HoldingsAuthorityId,
                    equipment,
                    HoldingProvenance.Create(
                        StableId.Parse("grant." + token),
                        StableId.Parse("source.test")),
                    expectedSequence));
            Assert.That(result.Status, Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
        }

    }
}
