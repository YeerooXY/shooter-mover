using System;
using System.Collections.Generic;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Catalog
{
    public static partial class GunCatalogBlueprintMapper
    {
        /// <summary>
        /// Explicit validated migration path from one current flat catalogue definition into the
        /// canonical grouped Gun authority. Missing semantics must be supplied through
        /// intent/details; the mapper never infers delivery from content names or TopBoxOnly from
        /// the catalogue's current highest tier.
        /// </summary>
        public static GunMappingResult MapAuthored(
            GunCatalog catalog,
            string definitionId,
            GunCatalogBlueprintMappingIntent intent,
            GunCatalogAuthoredMappingDetails details)
        {
            var issues = new List<GunMappingIssue>();
            GunDefinitionData definition;
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

            GunFamilyDefinition family;
            if (!catalog.TryGetFamily(definition.FamilyId, out family)
                || family == null)
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
            ValidateUnsupportedLegacyEffects(definition, issues);

            FireSettings fireSettings = BuildFireSettings(
                definition,
                intent,
                issues);
            GunShotPattern shotPattern = BuildShotPattern(
                definition,
                intent,
                issues);
            GunDamageSpec damage = BuildDamage(
                definition,
                damageCategory,
                issues);
            GunEffects effects = BuildEffects(
                definition,
                intent,
                issues);

            if (intent.Guidance == null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.MissingGuidance,
                    Path(definition, ".Guidance"),
                    "Canonical mapping requires explicit unguided or homing data.");
            }
            if (intent.Impact == null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.MissingImpactConfiguration,
                    Path(definition, ".Impact"),
                    "Canonical mapping requires explicit impact and termination semantics.");
            }

            RicochetValue ricochet = default(RicochetValue);
            if (details.RicochetTenths < 0)
            {
                Add(
                    issues,
                    GunMappingIssueCode.InvalidAuthoredRicochet,
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
                    GunMappingIssueCode.InvalidAuthoredMovementPenalty,
                    "details.MovementPenaltyPercent",
                    "Movement penalty must be finite and between zero and 100 percent.");
            }

            GunStrongboxEligibility eligibility = BuildStrongboxEligibility(
                definition,
                details,
                issues);
            if (details.Presentation == null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.MissingAuthoredPresentation,
                    "details.Presentation",
                    "Canonical mapping requires separate inventory, mounted, and delivery presentation references.");
            }
            if (details.EquipmentDefinitionId == null || details.RarityId == null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.MissingAuthoredDropIdentity,
                    "details.DropMetadata",
                    "Canonical drop metadata requires exact equipment and rarity identities.");
            }

            ShotPattern delivery = null;
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

            GunBaseStats baseStats = null;
            GunDropMetadata dropMetadata = null;
            if (issues.Count == 0)
            {
                try
                {
                    baseStats = new GunBaseStats(
                        damage.DirectDamage,
                        damage.Category,
                        damage.DamageOverTime,
                        PierceValue.FromLegacyInteger(definition.Pierce),
                        ricochet,
                        details.MovementPenaltyPercent,
                        GunAttackDistance.Limited(definition.Range),
                        definition.Knockback);
                    dropMetadata = new GunDropMetadata(
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
                        GunMappingIssueCode.DomainContractRejected,
                        Path(definition, string.Empty),
                        exception.Message);
                }
            }

            if (issues.Count != 0)
            {
                return Failure(issues);
            }

            GunDefinitionConstructionResult construction =
                Gun.TryCreateAuthored(
                    new GunIdentity(
                        new GunDefinitionId(definition.DefinitionId),
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
                    GunDefinitionIssue issue = construction.Issues[index];
                    Add(
                        issues,
                        GunMappingIssueCode.AuthoredDefinitionRejected,
                        issue.Path,
                        issue.Code + ": " + issue.Detail);
                }
                return Failure(issues);
            }

            return new GunMappingResult(
                construction.Definition,
                issues);
        }

        private static bool TryResolveAuthoredInputs(
            GunCatalog catalog,
            string definitionId,
            GunCatalogBlueprintMappingIntent intent,
            GunCatalogAuthoredMappingDetails details,
            ICollection<GunMappingIssue> issues,
            out GunDefinitionData definition)
        {
            definition = null;
            if (catalog == null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.NullCatalog,
                    "$",
                    "Gun catalog is required.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                Add(
                    issues,
                    GunMappingIssueCode.MissingDefinitionId,
                    "definitionId",
                    "A stable gun definition ID is required.");
                return false;
            }
            if (!catalog.TryGetDefinition(definitionId, out definition)
                || definition == null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.UnknownDefinition,
                    "definitionId",
                    "Catalog does not contain gun definition '" + definitionId + "'.");
                return false;
            }
            if (intent == null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.MissingMappingIntent,
                    Path(definition, string.Empty),
                    "Explicit semantic mapping intent is required.");
                return false;
            }
            if (details == null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.MissingAuthoredMappingDetails,
                    Path(definition, string.Empty),
                    "Canonical grouped mapping details are required.");
                return false;
            }
            if (intent.ExpectedDefinitionId == null)
            {
                Add(
                    issues,
                    GunMappingIssueCode.MissingIntentDefinitionId,
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
                    GunMappingIssueCode.MismatchedIntentDefinitionId,
                    "intent.ExpectedDefinitionId",
                    "Mapping intent cannot be reused for another catalogue definition.");
                return false;
            }
            return true;
        }

        private static void ValidateUnsupportedLegacyEffects(
            GunDefinitionData definition,
            ICollection<GunMappingIssue> issues)
        {
            if (definition.AreaDamagePerTrigger > 0d)
            {
                Add(
                    issues,
                    GunMappingIssueCode.UnsupportedAreaDamage,
                    Path(definition, ".AreaDamagePerTrigger"),
                    "Legacy independent area-damage magnitude cannot be discarded. Migrate it to the canonical direct-damage-plus-explosion interpretation explicitly before using MapAuthored.");
            }
            if (definition.PoolRadius > 0d || definition.PoolDuration > 0d)
            {
                Add(
                    issues,
                    GunMappingIssueCode.UnsupportedPersistentPool,
                    Path(definition, ".PoolRadius"),
                    "Persistent-pool behaviour requires a reusable typed effect contract before canonical migration.");
            }
            if (definition.HealingPerSecond > 0d)
            {
                Add(
                    issues,
                    GunMappingIssueCode.UnsupportedHealing,
                    Path(definition, ".HealingPerSecond"),
                    "Healing cannot be discarded or hidden inside Special delivery.");
            }
        }

        private static ShotPattern BuildAuthoredDelivery(
            GunDefinitionData definition,
            GunCatalogBlueprintMappingIntent intent,
            GunCatalogAuthoredMappingDetails details,
            GunEffects effects,
            ICollection<GunMappingIssue> issues)
        {
            GunNormalDeliverySettings normal = null;
            GunOrbDeliverySettings orb = null;
            GunRocketDeliverySettings rocket = null;
            GunLaserDeliverySettings laser = null;
            GunSpecialDeliverySettings special = null;

            try
            {
                switch (details.DeliveryType)
                {
                    case GunDeliveryType.Normal:
                        if (!ValidateTravellingProjection(
                                intent,
                                GunProjectileKind.RegularProjectile,
                                GunProjectileTerminationBehavior.StopWhenPierceIsSpent,
                                issues))
                        {
                            return null;
                        }
                        normal = new GunNormalDeliverySettings(
                            definition.ProjectileSpeed,
                            details.DeliveryRadiusOrWidth);
                        break;
                    case GunDeliveryType.Orb:
                        if (!ValidateTravellingProjection(
                                intent,
                                GunProjectileKind.Orb,
                                GunProjectileTerminationBehavior.StopWhenPierceIsSpent,
                                issues))
                        {
                            return null;
                        }
                        orb = new GunOrbDeliverySettings(
                            definition.ProjectileSpeed,
                            details.DeliveryRadiusOrWidth);
                        break;
                    case GunDeliveryType.Rocket:
                        if (!ValidateTravellingProjection(
                                intent,
                                GunProjectileKind.Rocket,
                                GunProjectileTerminationBehavior.StopOnFirstBlockingImpact,
                                issues))
                        {
                            return null;
                        }
                        rocket = new GunRocketDeliverySettings(
                            definition.ProjectileSpeed,
                            details.DeliveryRadiusOrWidth);
                        break;
                    case GunDeliveryType.Laser:
                        if (definition.ProjectileSpeed != 0d)
                        {
                            Add(
                                issues,
                                GunMappingIssueCode.LaserCarriesProjectileSpeed,
                                Path(definition, ".ProjectileSpeed"),
                                "A canonical laser cannot preserve a fake projectile speed. Correct the source content before migration.");
                            return null;
                        }
                        laser = new GunLaserDeliverySettings(
                            details.DeliveryRadiusOrWidth);
                        break;
                    case GunDeliveryType.Special:
                        if (definition.ProjectileSpeed != 0d
                            || details.DeliveryRadiusOrWidth != 0d)
                        {
                            Add(
                                issues,
                                GunMappingIssueCode.InvalidAuthoredDelivery,
                                Path(definition, ".ProjectileSpeed"),
                                "Special delivery cannot silently absorb travelling-projectile fields.");
                            return null;
                        }
                        special = details.SpecialDelivery;
                        if (special == null)
                        {
                            Add(
                                issues,
                                GunMappingIssueCode.InvalidAuthoredDelivery,
                                "details.SpecialDelivery",
                                "Special delivery requires exactly one validated behaviour reference.");
                            return null;
                        }
                        break;
                    default:
                        Add(
                            issues,
                            GunMappingIssueCode.InvalidAuthoredDelivery,
                            "details.DeliveryType",
                            "Unknown canonical delivery type.");
                        return null;
                }

                return ShotPattern.Create(
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
                    GunMappingIssueCode.InvalidAuthoredDelivery,
                    "details.Delivery",
                    exception.Message);
                return null;
            }
        }

        private static bool ValidateTravellingProjection(
            GunCatalogBlueprintMappingIntent intent,
            GunProjectileKind expectedKind,
            GunProjectileTerminationBehavior expectedTermination,
            ICollection<GunMappingIssue> issues)
        {
            if (intent.ProjectileKind != expectedKind)
            {
                Add(
                    issues,
                    GunMappingIssueCode.InvalidAuthoredDelivery,
                    "intent.ProjectileKind",
                    "The explicit projectile kind does not match the selected canonical delivery type.");
                return false;
            }
            if (intent.ProjectileTermination != expectedTermination)
            {
                Add(
                    issues,
                    GunMappingIssueCode.InvalidAuthoredDelivery,
                    "intent.ProjectileTermination",
                    "The explicit termination policy cannot be discarded by the canonical delivery projection.");
                return false;
            }
            return true;
        }

        private static GunStrongboxEligibility BuildStrongboxEligibility(
            GunDefinitionData definition,
            GunCatalogAuthoredMappingDetails details,
            ICollection<GunMappingIssue> issues)
        {
            try
            {
                GunStrongboxEligibility eligibility;
                switch (details.StrongboxEligibilityMode)
                {
                    case GunCatalogStrongboxEligibilityMappingMode.MinimumTier:
                        if (details.AllowedStrongboxTiers.Count != 0)
                        {
                            throw new ArgumentException(
                                "Minimum-tier mapping cannot also carry an allowed-tier list.");
                        }
                        eligibility = GunStrongboxEligibility.FromMinimumTier(
                            details.MinimumStrongboxTier);
                        break;
                    case GunCatalogStrongboxEligibilityMappingMode.ExplicitAllowedTiers:
                        if (details.MinimumStrongboxTier != 0)
                        {
                            throw new ArgumentException(
                                "Explicit allowed tiers cannot also carry a minimum tier.");
                        }
                        eligibility = GunStrongboxEligibility.FromAllowedTierIds(
                            details.AllowedStrongboxTiers);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(details.StrongboxEligibilityMode));
                }

                // TopBoxOnly is deliberately not converted from the current maximum tier. The
                // explicit rule above is the authored replacement required for migration.
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
                        ? GunMappingIssueCode.TopBoxOnlyRequiresExplicitRule
                        : GunMappingIssueCode.InvalidStrongboxTierRestriction,
                    Path(definition, ".TopBoxOnly"),
                    exception.Message);
                return null;
            }
        }
    }
}
