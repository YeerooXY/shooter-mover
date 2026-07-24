using System;
using System.Collections.Generic;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Catalog
{
    /// <summary>
    /// Loss-conscious application boundary from the current catalog authority to immutable
    /// modular weapon contracts. Missing legacy semantics are supplied explicitly through
    /// WeaponCatalogBlueprintMappingIntent; the mapper never guesses them from prose.
    /// </summary>
    public static partial class WeaponCatalogBlueprintMapper
    {
        public static WeaponBlueprintMappingResult Map(
            WeaponCatalog catalog,
            string definitionId,
            WeaponCatalogBlueprintMappingIntent intent)
        {
            var issues = new List<WeaponBlueprintMappingIssue>();
            if (catalog == null)
            {
                Add(issues, WeaponBlueprintMappingIssueCode.NullCatalog, "$", "Weapon catalog is required.");
                return Failure(issues);
            }
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.MissingDefinitionId,
                    "definitionId",
                    "A stable weapon definition ID is required.");
                return Failure(issues);
            }

            WeaponDefinitionData definition;
            if (!catalog.TryGetDefinition(definitionId, out definition) || definition == null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.UnknownDefinition,
                    "definitionId",
                    "Catalog does not contain weapon definition '" + definitionId + "'.");
                return Failure(issues);
            }
            if (intent == null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.MissingMappingIntent,
                    Path(definition, string.Empty),
                    "Explicit mapping intent is required because the legacy schema does not encode every modular semantic.");
                return Failure(issues);
            }

            WeaponFamilyDefinition family;
            if (!catalog.TryGetFamily(definition.FamilyId, out family) || family == null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.UnknownFamily,
                    Path(definition, ".FamilyId"),
                    "Catalog family '" + definition.FamilyId + "' cannot be resolved.");
            }
            if (!catalog.Archetypes.ContainsKey(definition.Archetype))
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.UnknownArchetype,
                    Path(definition, ".Archetype"),
                    "Catalog archetype '" + definition.Archetype + "' cannot be resolved.");
            }

            WeaponDamageCategory damageCategory;
            ResolveDamageCategory(definition, intent, issues, out damageCategory);

            if (intent.FireMode == WeaponFireMode.Continuous)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.UnsupportedContinuousDefinition,
                    Path(definition, ".FireRate"),
                    "WeaponDefinitionData requires projectile count, speed, range, and per-projectile damage. Mapping it as continuous fire would discard authored values.");
            }
            if (definition.PoolRadius > 0d || definition.PoolDuration > 0d)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.UnsupportedPersistentPool,
                    Path(definition, ".PoolRadius"),
                    "WeaponBlueprint has no persistent-pool contract. Pool radius and duration remain in the catalog until that effect is modeled explicitly.");
            }
            if (definition.HealingPerSecond > 0d)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.UnsupportedHealing,
                    Path(definition, ".HealingPerSecond"),
                    "WeaponBlueprint has no healing-effect contract; the authored value cannot be dropped.");
            }

            WeaponFireSettings fireSettings = BuildFireSettings(definition, intent, issues);
            WeaponShotPattern shotPattern = BuildShotPattern(definition, intent, issues);
            WeaponProjectileSpec projectile = BuildProjectile(definition, intent, issues);

            if (intent.Guidance == null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.MissingGuidance,
                    Path(definition, ".Guidance"),
                    "The legacy catalog has no guidance fields. Supply explicit unguided or homing data.");
            }
            if (intent.Impact == null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.MissingImpactConfiguration,
                    Path(definition, ".Impact"),
                    "The legacy catalog has no impact-trigger or termination semantics. Supply them explicitly.");
            }

            WeaponDamageSpec damage = BuildDamage(definition, damageCategory, issues);
            WeaponEffects effects = BuildEffects(definition, intent, issues);
            string presentationReference = ResolvePresentationReference(
                definition,
                family,
                intent.PresentationReference,
                issues);

            if (issues.Count > 0)
            {
                return Failure(issues);
            }

            try
            {
                WeaponBlueprint blueprint = WeaponBlueprint.Create(
                    new WeaponDefinitionId(definition.DefinitionId),
                    definition.DisplayName,
                    definition.FamilyId,
                    fireSettings,
                    shotPattern,
                    projectile,
                    intent.Guidance,
                    intent.Impact,
                    damage,
                    effects,
                    definition.DefinitionId,
                    presentationReference);
                return new WeaponBlueprintMappingResult(blueprint, issues);
            }
            catch (Exception exception)
            {
                if (!(exception is ArgumentException)
                    && !(exception is InvalidOperationException)
                    && !(exception is OverflowException))
                {
                    throw;
                }

                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.DomainContractRejected,
                    Path(definition, string.Empty),
                    exception.Message);
                return Failure(issues);
            }
        }


        private static WeaponBlueprintMappingResult Failure(
            IEnumerable<WeaponBlueprintMappingIssue> issues)
        {
            return new WeaponBlueprintMappingResult(null, issues);
        }
    }
}
