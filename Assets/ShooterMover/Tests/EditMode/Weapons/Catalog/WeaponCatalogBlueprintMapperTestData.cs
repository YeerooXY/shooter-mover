using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Tests.EditMode.Weapons.Catalog
{
    public sealed partial class WeaponCatalogBlueprintMapperTests
    {
        private static WeaponCatalogBlueprintMappingIntent Intent(
            WeaponFireMode fireMode = WeaponFireMode.Automatic,
            WeaponShotPatternKind pattern = WeaponShotPatternKind.Spray,
            WeaponCatalogSpreadInterpretation spread = WeaponCatalogSpreadInterpretation.AuthoredRandomness,
            WeaponGuidanceSpec guidance = null,
            WeaponImpactSpec impact = null,
            WeaponCatalogExplosionMapping explosion = null,
            WeaponCatalogDamageOverTimeMapping dot = null,
            WeaponCatalogChainMapping chain = null,
            WeaponDamageCategory? explicitDamageCategory = null,
            int shotsPerTrigger = 1,
            double burstInterval = 0d,
            double burstRecovery = 0d,
            string presentationReference = null)
        {
            return new WeaponCatalogBlueprintMappingIntent(
                fireMode,
                shotsPerTrigger,
                pattern,
                spread,
                1,
                0d,
                burstInterval,
                burstRecovery,
                WeaponProjectileKind.Rocket,
                WeaponProjectileTerminationBehavior.StopWhenPierceIsSpent,
                explicitDamageCategory,
                guidance ?? WeaponGuidanceSpec.Unguided(),
                impact ?? WeaponImpactSpec.Create(true, true, true, true, null, null),
                explosion,
                dot,
                chain,
                presentationReference);
        }

        private static WeaponCatalog BuildCatalog(
            string damageType = "Physical",
            int burstCount = 1,
            int projectiles = 1,
            double spread = 1d,
            int pierce = 0,
            double areaDamage = 0d,
            double explosionRadius = 0d,
            double dotDps = 0d,
            double dotDuration = 0d,
            double poolRadius = 0d,
            double poolDuration = 0d,
            int chainTargets = 0,
            double chainRange = 0d,
            double healingPerSecond = 0d,
            IEnumerable<string> definitionArt = null)
        {
            var archetype = new WeaponArchetypeDefinition(
                "TestArchetype",
                "test",
                1d,
                2.5d,
                projectiles,
                burstCount,
                spread,
                31d,
                42d,
                1d,
                0d,
                0d,
                explosionRadius,
                dotDuration,
                poolRadius,
                poolDuration,
                pierce,
                chainTargets,
                chainRange,
                0.5d,
                1d);
            var family = new WeaponFamilyDefinition(
                "test_weapon",
                "Test Weapon",
                "TestArchetype",
                damageType,
                "Universal",
                1,
                1,
                1,
                1,
                "Common",
                string.Empty,
                string.Empty,
                1d,
                "Standard",
                "Test",
                string.Empty,
                WeaponCatalogAvailability.Live,
                Array.Empty<string>());
            var definition = new WeaponDefinitionData(
                "test_weapon.mk1",
                "Test Weapon MK1",
                "test_weapon",
                1,
                damageType,
                "TestArchetype",
                "Universal",
                1,
                1,
                1,
                "Common",
                1d,
                1d,
                1d,
                1d,
                1d,
                "Standard",
                false,
                "Standard",
                1d,
                100d,
                30d,
                1d,
                0d,
                0d,
                2.5d,
                projectiles,
                burstCount,
                12d,
                spread,
                31d,
                42d,
                pierce,
                explosionRadius,
                areaDamage,
                dotDps,
                dotDuration,
                poolRadius,
                poolDuration,
                chainTargets,
                chainRange,
                0.5d,
                1d,
                healingPerSecond,
                "Test",
                string.Empty,
                WeaponCatalogAvailability.Live,
                definitionArt ?? new[] { "art/default.png" });
            return new WeaponCatalog(
                "test",
                "test",
                new WeaponCatalogRules(
                    true,
                    string.Empty,
                    new[] { 1 },
                    new[] { damageType },
                    10,
                    true,
                    true,
                    true),
                new WeaponCatalogInputs(
                    1d,
                    0d,
                    0d,
                    0d,
                    new Dictionary<string, WeaponRarityInput>()),
                new Dictionary<string, WeaponArchetypeDefinition>
                {
                    { "TestArchetype", archetype },
                },
                new[] { family },
                new[] { definition });
        }

        private static void AssertIssue(
            WeaponBlueprintMappingResult result,
            WeaponBlueprintMappingIssueCode code)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                HasIssue(result, code),
                Is.True,
                "Expected " + code + ". Actual: " + JoinIssues(result));
        }

        private static bool HasIssue(
            WeaponBlueprintMappingResult result,
            WeaponBlueprintMappingIssueCode code)
        {
            for (int index = 0; index < result.Issues.Count; index++)
            {
                if (result.Issues[index].Code == code)
                {
                    return true;
                }
            }
            return false;
        }

        private static string JoinIssues(WeaponBlueprintMappingResult result)
        {
            var values = new List<string>();
            for (int index = 0; index < result.Issues.Count; index++)
            {
                values.Add(result.Issues[index].ToString());
            }
            return string.Join(" | ", values);
        }
    }
}
