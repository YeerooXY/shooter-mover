using System;
using System.Collections.Generic;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Catalog
{
    /// <summary>
    /// Loss-conscious application boundary from the current catalog authority to immutable
    /// modular gun contracts. Missing legacy semantics are supplied explicitly through
    /// GunCatalogBlueprintMappingIntent; the mapper never guesses them from prose.
    /// </summary>
    public static partial class GunCatalogBlueprintMapper
    {
        public static GunMappingResult Map(
            GunCatalog catalog,
            string definitionId,
            GunCatalogBlueprintMappingIntent intent)
        {
            var issues = new List<GunMappingIssue>();
            if (catalog == null)
            {
                Add(issues, GunMappingIssueCode.NullCatalog, "$", "Gun catalog is required.");
                return Failure(issues);
            }
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                Add(
                    issues,
                    GunMappingIssueCode.MissingDefinitionId,
                    "definitionId",
                    "A stable gun definition ID is required.");
                return Failure(issues);
            }

            GunDefinitionData definition;
            if (!catalog.TryGetDefinition(definitionId, out definition) || definition == null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.UnknownDefinition,
                    "definitionId",
                    "Catalog does not contain gun definition '" + definitionId + "'.");
                return Failure(issues);
            }
            if (intent == null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.MissingMappingIntent,
                    Path(definition, string.Empty),
                    "Explicit mapping intent is required because the legacy schema does not encode every modular semantic.");
                return Failure(issues);
            }
            if (intent.ExpectedDefinitionId == null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.MissingIntentDefinitionId,
                    "intent.ExpectedDefinitionId",
                    "Mapping intent must be bound to the stable definition ID it authoritatively describes.");
                return Failure(issues);
            }
            if (!string.Equals(
                    intent.ExpectedDefinitionId.Value,
                    definition.DefinitionId,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    GunMappingIssueCode.MismatchedIntentDefinitionId,
                    "intent.ExpectedDefinitionId",
                    "Mapping intent for '"
                    + intent.ExpectedDefinitionId.Value
                    + "' cannot map catalog definition '"
                    + definition.DefinitionId
                    + "'.");
                return Failure(issues);
            }

            GunFamilyDefinition family;
            if (!catalog.TryGetFamily(definition.FamilyId, out family) || family == null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.UnknownFamily,
                    Path(definition, ".FamilyId"),
                    "Catalog family '" + definition.FamilyId + "' cannot be resolved.");
            }
            if (!catalog.Archetypes.ContainsKey(definition.Archetype))
            {
                Add(
                    issues,
                    GunMappingIssueCode.UnknownArchetype,
                    Path(definition, ".Archetype"),
                    "Catalog archetype '" + definition.Archetype + "' cannot be resolved.");
            }

            GunDamageCategory damageCategory;
            ResolveDamageCategory(definition, intent, issues, out damageCategory);

            if (intent.FireMode == GunFireMode.Continuous)
            {
                Add(
                    issues,
                    GunMappingIssueCode.UnsupportedContinuousDefinition,
                    Path(definition, ".FireRate"),
                    "GunDefinitionData requires projectile count, speed, range, and per-projectile damage. Mapping it as continuous fire would discard authored values.");
            }
            if (definition.PoolRadius > 0d || definition.PoolDuration > 0d)
            {
                Add(
                    issues,
                    GunMappingIssueCode.UnsupportedPersistentPool,
                    Path(definition, ".PoolRadius"),
                    "Gun has no persistent-pool contract. Pool radius and duration remain in the catalog until that effect is modeled explicitly.");
            }
            if (definition.HealingPerSecond > 0d)
            {
                Add(
                    issues,
                    GunMappingIssueCode.UnsupportedHealing,
                    Path(definition, ".HealingPerSecond"),
                    "Gun has no healing-effect contract; the authored value cannot be dropped.");
            }

            FireSettings fireSettings = BuildFireSettings(definition, intent, issues);
            GunShotPattern shotPattern = BuildShotPattern(definition, intent, issues);
            ProjectileSettings projectile = BuildProjectile(definition, intent, issues);

            if (intent.Guidance == null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.MissingGuidance,
                    Path(definition, ".Guidance"),
                    "The legacy catalog has no guidance fields. Supply explicit unguided or homing data.");
            }
            if (intent.Impact == null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.MissingImpactConfiguration,
                    Path(definition, ".Impact"),
                    "The legacy catalog has no impact-trigger or termination semantics. Supply them explicitly.");
            }

            GunDamageSpec damage = BuildDamage(definition, damageCategory, issues);
            GunEffects effects = BuildEffects(definition, intent, issues);
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
                Gun blueprint = Gun.Create(
                    new GunDefinitionId(definition.DefinitionId),
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
                return new GunMappingResult(blueprint, issues);
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
                    GunMappingIssueCode.DomainContractRejected,
                    Path(definition, string.Empty),
                    exception.Message);
                return Failure(issues);
            }
        }


        private static GunMappingResult Failure(
            IEnumerable<GunMappingIssue> issues)
        {
            return new GunMappingResult(null, issues);
        }
    }
}
