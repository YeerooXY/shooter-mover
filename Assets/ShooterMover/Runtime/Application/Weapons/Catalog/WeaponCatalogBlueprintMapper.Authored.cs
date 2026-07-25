using System;
using System.Collections.Generic;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Catalog
{
    public static partial class WeaponCatalogBlueprintMapper
    {
        /// <summary>
        /// Explicit validated migration path from one current flat catalogue definition into the
        /// canonical grouped WeaponBlueprint authority. Missing semantics must be supplied through
        /// intent/details; the mapper never infers delivery from content names or TopBoxOnly from
        /// the catalogue's current highest tier.
        /// </summary>
        public static WeaponBlueprintMappingResult MapAuthored(
            WeaponCatalog catalog,
            string definitionId,
            WeaponCatalogBlueprintMappingIntent intent,
            WeaponCatalogAuthoredMappingDetails details)
        {
            var issues = new List<WeaponBlueprintMappingIssue>();
            WeaponDefinitionData definition;
            if (!TryResolveAuthoredInputs(
                    catalog,
                    definitionId,
                    intent,
                    details,
                    issues,
                    out definition))
            {
                return Failure(issues);
            }

            WeaponFamilyDefinition family;
            if (!catalog.TryGetFamily(definition.FamilyId, out family)
                || family == null)
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
            ValidateUnsupportedLegacyEffects(definition, issues);

            WeaponFireSettings fireSettings = BuildFireSettings(
                definition,
                intent,
                issues);
            WeaponShotPattern shotPattern = BuildShotPattern(
                definition,
                intent,
                issues);
            WeaponDamageSpec damage = BuildDamage(
                definition,
                damageCategory,
                issues);
            WeaponEffects effects = BuildEffects(
                definition,
                intent,
                issues);

            if (intent.Guidance == null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.MissingGuidance,
                    Path(definition, ".Guidance"),
                    "Canonical mapping requires explicit unguided or homing data.");
            }
            if (intent.Impact == null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.MissingImpactConfiguration,
                    Path(definition, ".Impact"),
                    "Canonical mapping requires explicit impact and termination semantics.");
            }

            RicochetValue ricochet = default(RicochetValue);
            if (details.RicochetTenths < 0)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.InvalidAuthoredRicochet,
                    "details.RicochetTenths",
                    "Ricochet fixed-point tenths cannot be negative.");
            }
            else
            {
                ricochet = new RicochetValue(details.RicochetTenths);
            }
            if (double.IsNaN(details.MovementPenaltyPercent)
                || double.IsInfinity(details.MovementPenaltyPercent)
                || details.MovementPenaltyPercent < 0d
                || details.MovementPenaltyPercent > 100d)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.InvalidAuthoredMovementPenalty,
                    "details.MovementPenaltyPercent",
                    "Movement penalty must be finite and between zero and 100 percent.");
            }

            WeaponStrongboxEligibility eligibility = BuildStrongboxEligibility(
                definition,
                details,
                issues);
            if (details.Presentation == null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.MissingAuthoredPresentation,
                    "details.Presentation",
                    "Canonical mapping requires separate inventory, mounted, and delivery presentation references.");
            }
            if (details.EquipmentDefinitionId == null || details.RarityId == null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.MissingAuthoredDropIdentity,
                    "details.DropMetadata",
                    "Canonical drop metadata requires exact equipment and rarity identities.");
            }

            WeaponDeliverySpec delivery = null;
            if (intent.Guidance != null
                && intent.Impact != null
                && effects != null)
            {
                delivery = BuildAuthoredDelivery(
                    definition,
                    intent,
                    details,
                    effects,
                    issues);
            }

            WeaponBaseStats baseStats = null;
            WeaponDropMetadata dropMetadata = null;
            if (issues.Count == 0)
            {
                try
                {
                    baseStats = new WeaponBaseStats(
                        damage.DirectDamage,
                        damage.Category,
                        damage.DamageOverTime,
                        PierceValue.FromLegacyInteger(definition.Pierce),
                        ricochet,
                        details.MovementPenaltyPercent,
                        WeaponAttackDistance.Limited(definition.Range));
                    dropMetadata = new WeaponDropMetadata(
                        details.EquipmentDefinitionId,
                        details.RarityId,
                        details.Availability,
                        definition.PeakDropLevel,
                        definition.FinalBaseWeight,
                        eligibility);
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
                }
            }

            if (issues.Count != 0)
            {
                return Failure(issues);
            }

            WeaponDefinitionConstructionResult construction =
                WeaponBlueprint.TryCreateAuthored(
                    new WeaponIdentity(
                        new WeaponDefinitionId(definition.DefinitionId),
                        definition.DisplayName,
                        definition.FamilyId),
                    fireSettings,
                    shotPattern,
                    baseStats,
                    delivery,
                    details.Presentation,
                    dropMetadata);
            if (!construction.Succeeded)
            {
                for (int index = 0; index < construction.Issues.Count; index++)
                {
                    WeaponDefinitionIssue issue = construction.Issues[index];
                    Add(
                        issues,
                        WeaponBlueprintMappingIssueCode.AuthoredDefinitionRejected,
                        issue.Path,
                        issue.Code + ": " + issue.Detail);
                }
                return Failure(issues);
            }

            return new WeaponBlueprintMappingResult(
                construction.Definition,
                issues);
        }

        private static bool TryResolveAuthoredInputs(
            WeaponCatalog catalog,
            string definitionId,
            WeaponCatalogBlueprintMappingIntent intent,
            WeaponCatalogAuthoredMappingDetails details,
            ICollection<WeaponBlueprintMappingIssue> issues,
            out WeaponDefinitionData definition)
        {
            definition = null;
            if (catalog == null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.NullCatalog,
                    "$",
                    "Weapon catalog is required.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.MissingDefinitionId,
                    "definitionId",
                    "A stable weapon definition ID is required.");
                return false;
            }
            if (!catalog.TryGetDefinition(definitionId, out definition)
                || definition == null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.UnknownDefinition,
                    "definitionId",
                    "Catalog does not contain weapon definition '" + definitionId + "'.");
                return false;
            }
            if (intent == null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.MissingMappingIntent,
                    Path(definition, string.Empty),
                    "Explicit semantic mapping intent is required.");
                return false;
            }
            if (details == null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.MissingAuthoredMappingDetails,
                    Path(definition, string.Empty),
                    "Canonical grouped mapping details are required.");
                return false;
            }
            if (intent.ExpectedDefinitionId == null)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.MissingIntentDefinitionId,
                    "intent.ExpectedDefinitionId",
                    "Mapping intent must be bound to one stable definition ID.");
                return false;
            }
            if (!string.Equals(
                    intent.ExpectedDefinitionId.Value,
                    definition.DefinitionId,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.MismatchedIntentDefinitionId,
                    "intent.ExpectedDefinitionId",
                    "Mapping intent cannot be reused for another catalogue definition.");
                return false;
            }
            return true;
        }

        private static void ValidateUnsupportedLegacyEffects(
            WeaponDefinitionData definition,
            ICollection<WeaponBlueprintMappingIssue> issues)
        {
            if (definition.PoolRadius > 0d || definition.PoolDuration > 0d)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.UnsupportedPersistentPool,
                    Path(definition, ".PoolRadius"),
                    "Persistent-pool behaviour requires a reusable typed effect contract before canonical migration.");
            }
            if (definition.HealingPerSecond > 0d)
            {
                Add(
                    issues,
                    WeaponBlueprintMappingIssueCode.UnsupportedHealing,
                    Path(definition, ".HealingPerSecond"),
                    "Healing cannot be discarded or hidden inside Special delivery.");
            }
        }

        private static WeaponDeliverySpec BuildAuthoredDelivery(
            WeaponDefinitionData definition,
            WeaponCatalogBlueprintMappingIntent intent,
            WeaponCatalogAuthoredMappingDetails details,
            WeaponEffects effects,
            ICollection<WeaponBlueprintMappingIssue> issues)
        {
            WeaponNormalDeliverySettings normal = null;
            WeaponOrbDeliverySettings orb = null;
            WeaponRocketDeliverySettings rocket = null;
            WeaponLaserDeliverySettings laser = null;
            WeaponSpecialDeliverySettings special = null;

            try
            {
                switch (details.DeliveryType)
                {
                    case WeaponDeliveryType.Normal:
                        normal = new WeaponNormalDeliverySettings(
                            definition.ProjectileSpeed,
                            details.DeliveryRadiusOrWidth);
                        break;
                    case WeaponDeliveryType.Orb:
                        orb = new WeaponOrbDeliverySettings(
                            definition.ProjectileSpeed,
                            details.DeliveryRadiusOrWidth);
                        break;
                    case WeaponDeliveryType.Rocket:
                        rocket = new WeaponRocketDeliverySettings(
                            definition.ProjectileSpeed,
                            details.DeliveryRadiusOrWidth);
                        break;
                    case WeaponDeliveryType.Laser:
                        if (definition.ProjectileSpeed != 0d)
                        {
                            Add(
                                issues,
                                WeaponBlueprintMappingIssueCode.LaserCarriesProjectileSpeed,
                                Path(definition, ".ProjectileSpeed"),
                                "A canonical laser cannot preserve a fake projectile speed. Correct the source content before migration.");
                            return null;
                        }
                        laser = new WeaponLaserDeliverySettings(
                            details.DeliveryRadiusOrWidth);
                        break;
                    case WeaponDeliveryType.Special:
                        if (definition.ProjectileSpeed != 0d
                            || details.DeliveryRadiusOrWidth != 0d)
                        {
                            Add(
                                issues,
                                WeaponBlueprintMappingIssueCode.InvalidAuthoredDelivery,
                                Path(definition, ".ProjectileSpeed"),
                                "Special delivery cannot silently absorb travelling-projectile fields.");
                            return null;
                        }
                        special = details.SpecialDelivery;
                        if (special == null)
                        {
                            Add(
                                issues,
                                WeaponBlueprintMappingIssueCode.InvalidAuthoredDelivery,
                                "details.SpecialDelivery",
                                "Special delivery requires exactly one validated behaviour reference.");
                            return null;
                        }
                        break;
                    default:
                        Add(
                            issues,
                            WeaponBlueprintMappingIssueCode.InvalidAuthoredDelivery,
                            "details.DeliveryType",
                            "Unknown canonical delivery type.");
                        return null;
                }

                return WeaponDeliverySpec.Create(
                    details.DeliveryType,
                    normal,
                    orb,
                    rocket,
                    laser,
                    special,
                    intent.Guidance,
                    intent.Impact,
                    effects);
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
                    WeaponBlueprintMappingIssueCode.InvalidAuthoredDelivery,
                    "details.Delivery",
                    exception.Message);
                return null;
            }
        }

        private static WeaponStrongboxEligibility BuildStrongboxEligibility(
            WeaponDefinitionData definition,
            WeaponCatalogAuthoredMappingDetails details,
            ICollection<WeaponBlueprintMappingIssue> issues)
        {
            try
            {
                WeaponStrongboxEligibility eligibility;
                switch (details.StrongboxEligibilityMode)
                {
                    case WeaponCatalogStrongboxEligibilityMappingMode.MinimumTier:
                        if (details.AllowedStrongboxTiers.Count != 0)
                        {
                            throw new ArgumentException(
                                "Minimum-tier mapping cannot also carry an allowed-tier list.");
                        }
                        eligibility = WeaponStrongboxEligibility.FromMinimumTier(
                            details.MinimumStrongboxTier);
                        break;
                    case WeaponCatalogStrongboxEligibilityMappingMode.ExplicitAllowedTiers:
                        if (details.MinimumStrongboxTier != 0)
                        {
                            throw new ArgumentException(
                                "Explicit allowed tiers cannot also carry a minimum tier.");
                        }
                        eligibility = WeaponStrongboxEligibility.FromAllowedTiers(
                            details.AllowedStrongboxTiers);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(details.StrongboxEligibilityMode));
                }

                // TopBoxOnly is deliberately not converted from the current maximum tier. The
                // explicit rule above is the authored replacement required for migration.
                if (definition.TopBoxOnly && eligibility == null)
                {
                    Add(
                        issues,
                        WeaponBlueprintMappingIssueCode.TopBoxOnlyRequiresExplicitRule,
                        Path(definition, ".TopBoxOnly"),
                        "TopBoxOnly requires an explicit stable tier rule before canonical migration.");
                }
                return eligibility;
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
                    definition.TopBoxOnly
                        ? WeaponBlueprintMappingIssueCode.TopBoxOnlyRequiresExplicitRule
                        : WeaponBlueprintMappingIssueCode.InvalidStrongboxTierRestriction,
                    Path(definition, ".TopBoxOnly"),
                    exception.Message);
                return null;
            }
        }
    }
}
