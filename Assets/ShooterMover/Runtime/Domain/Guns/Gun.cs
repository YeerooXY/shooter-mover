using System;
using System.Collections.Generic;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Domain.Guns
{
    /// <summary>
    /// Canonical immutable authored gun definition.
    /// </summary>
    public sealed class Gun
    {
        private Gun(
            GunIdentity identity,
            FireSettings fireSettings,
            GunShotPattern shotPattern,
            GunBaseStats baseStats,
            ShotPattern delivery,
            GunPresentation presentation,
            GunDropMetadata dropMetadata,
            ProjectileSettings projectileProjection,
            GunDamageSpec damageProjection,
            bool isTransitionalCatalogProjection,
            string transitionalDropMetadataReference,
            string transitionalPresentationReference)
        {
            Identity = identity;
            FireSettings = fireSettings;
            ShotPattern = shotPattern;
            BaseStats = baseStats;
            Delivery = delivery;
            Presentation = presentation;
            DropMetadata = dropMetadata;
            Projectile = projectileProjection;
            Damage = damageProjection;
            IsTransitionalCatalogProjection = isTransitionalCatalogProjection;
            TransitionalDropMetadataReference = transitionalDropMetadataReference;
            TransitionalPresentationReference = transitionalPresentationReference;
        }

        public GunIdentity Identity { get; }
        public FireSettings FireSettings { get; }
        public GunShotPattern ShotPattern { get; }
        public GunBaseStats BaseStats { get; }
        public ShotPattern Delivery { get; }
        public GunPresentation Presentation { get; }
        public GunDropMetadata DropMetadata { get; }
        public bool IsTransitionalCatalogProjection { get; }

        public GunDefinitionId DefinitionId { get { return Identity.DefinitionId; } }
        public string DisplayName { get { return Identity.DisplayName; } }
        public string GunFamily { get { return Identity.FamilyId; } }

        public ProjectileSettings Projectile { get; }
        public GunGuidanceSpec Guidance
        {
            get { return Delivery == null ? TransitionalGuidance : Delivery.Guidance; }
        }
        public GunImpactSpec Impact
        {
            get { return Delivery == null ? TransitionalImpact : Delivery.Impact; }
        }
        public GunDamageSpec Damage { get; }
        public GunEffects Effects
        {
            get { return Delivery == null ? TransitionalEffects : Delivery.Effects; }
        }
        public string DropMetadataReference
        {
            get
            {
                return DropMetadata == null
                    ? TransitionalDropMetadataReference
                    : DropMetadata.EquipmentDefinitionId.ToString();
            }
        }
        public string PresentationReference
        {
            get
            {
                return Presentation == null
                    ? TransitionalPresentationReference
                    : Presentation.InventorySideProfileReference;
            }
        }

        private GunGuidanceSpec TransitionalGuidance { get; set; }
        private GunImpactSpec TransitionalImpact { get; set; }
        private GunEffects TransitionalEffects { get; set; }
        private string TransitionalDropMetadataReference { get; }
        private string TransitionalPresentationReference { get; }

        public static Gun CreateAuthored(
            GunIdentity identity,
            FireSettings fireSettings,
            GunShotPattern shotPattern,
            GunBaseStats baseStats,
            ShotPattern delivery,
            GunPresentation presentation,
            GunDropMetadata dropMetadata)
        {
            GunDefinitionConstructionResult result = TryCreateAuthored(
                identity,
                fireSettings,
                shotPattern,
                baseStats,
                delivery,
                presentation,
                dropMetadata);
            if (!result.Succeeded)
            {
                throw new GunDefinitionValidationException(result.Issues);
            }
            return result.Definition;
        }

        public static GunDefinitionConstructionResult TryCreateAuthored(
            GunIdentity identity,
            FireSettings fireSettings,
            GunShotPattern shotPattern,
            GunBaseStats baseStats,
            ShotPattern delivery,
            GunPresentation presentation,
            GunDropMetadata dropMetadata)
        {
            List<GunDefinitionIssue> issues = GunDefinitionValidator.Validate(
                identity,
                fireSettings,
                shotPattern,
                baseStats,
                delivery,
                presentation,
                dropMetadata);
            if (delivery != null
                && delivery.Type == GunDeliveryType.Rocket
                && (baseStats == null || baseStats.DirectDamage <= 0d))
            {
                issues.Add(new GunDefinitionIssue(
                    GunDefinitionIssueCode.RocketExplosionRequired,
                    "base_stats.direct_damage",
                    "Canonical Rocket universal damage must be positive because it is the executable explosion base damage."));
            }
            if (issues.Count != 0)
            {
                return new GunDefinitionConstructionResult(null, issues);
            }

            ProjectileSettings projectile = null;
            GunDamageSpec damage;
            try
            {
                projectile = delivery.CreateTravellingProjectileSpec(baseStats);
                damage = GunDamageSpec.Create(
                    baseStats.DamageCategory,
                    baseStats.DirectDamage,
                    baseStats.DamageOverTime,
                    baseStats.Knockback);
            }
            catch (InvalidOperationException exception)
            {
                issues.Add(new GunDefinitionIssue(
                    GunDefinitionIssueCode.TransitionalProjectionRejected,
                    "delivery",
                    exception.Message));
                return new GunDefinitionConstructionResult(null, issues);
            }
            catch (ArgumentException exception)
            {
                issues.Add(new GunDefinitionIssue(
                    GunDefinitionIssueCode.IncompatibleDeliveryData,
                    "delivery",
                    exception.Message));
                return new GunDefinitionConstructionResult(null, issues);
            }

            return new GunDefinitionConstructionResult(
                new Gun(
                    identity,
                    fireSettings,
                    shotPattern,
                    baseStats,
                    delivery,
                    presentation,
                    dropMetadata,
                    projectile,
                    damage,
                    false,
                    null,
                    null),
                issues);
        }

        public static Gun Create(
            GunDefinitionId definitionId,
            string displayName,
            string gunFamily,
            FireSettings fireSettings,
            GunShotPattern shotPattern,
            ProjectileSettings projectile,
            GunGuidanceSpec guidance,
            GunImpactSpec impact,
            GunDamageSpec damage,
            GunEffects effects,
            string dropMetadataReference,
            string presentationReference)
        {
            if (definitionId == null)
            {
                throw new ArgumentNullException(nameof(definitionId));
            }
            RequireText(displayName, nameof(displayName));
            RequireText(gunFamily, nameof(gunFamily));
            RequireText(dropMetadataReference, nameof(dropMetadataReference));
            RequireText(presentationReference, nameof(presentationReference));
            if (fireSettings == null)
            {
                throw new ArgumentNullException(nameof(fireSettings));
            }
            if (shotPattern == null)
            {
                throw new ArgumentNullException(nameof(shotPattern));
            }
            if (guidance == null)
            {
                throw new ArgumentNullException(nameof(guidance));
            }
            if (impact == null)
            {
                throw new ArgumentNullException(nameof(impact));
            }
            if (damage == null)
            {
                throw new ArgumentNullException(nameof(damage));
            }
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            ValidateTransitionalStructure(
                fireSettings,
                shotPattern,
                projectile,
                guidance,
                impact,
                damage,
                effects);

            var blueprint = new Gun(
                new GunIdentity(definitionId, displayName, gunFamily),
                fireSettings,
                shotPattern,
                null,
                null,
                null,
                null,
                projectile,
                damage,
                true,
                dropMetadataReference,
                presentationReference);
            blueprint.TransitionalGuidance = guidance;
            blueprint.TransitionalImpact = impact;
            blueprint.TransitionalEffects = effects;
            return blueprint;
        }

        private static void ValidateTransitionalStructure(
            FireSettings fireSettings,
            GunShotPattern shotPattern,
            ProjectileSettings projectile,
            GunGuidanceSpec guidance,
            GunImpactSpec impact,
            GunDamageSpec damage,
            GunEffects effects)
        {
            if (shotPattern.UsesProjectiles && projectile == null)
            {
                throw new ArgumentException(
                    "A projectile-emitting transitional shot pattern requires ProjectileSettings.",
                    nameof(projectile));
            }
            if (guidance.Mode == GunGuidanceMode.Homing && projectile == null)
            {
                throw new ArgumentException(
                    "Transitional homing guidance requires a projectile.",
                    nameof(guidance));
            }
            if (impact.Ricochet != null && projectile == null)
            {
                throw new ArgumentException(
                    "Transitional ricochet configuration requires a projectile.",
                    nameof(impact));
            }
            if (fireSettings.IsContinuous)
            {
                if (projectile != null || shotPattern.UsesProjectiles)
                {
                    throw new ArgumentException(
                        "Transitional continuous guns cannot reuse projectile emission fields.");
                }
                if (shotPattern.Kind != GunShotPatternKind.Beam
                    && shotPattern.Kind != GunShotPatternKind.Spray)
                {
                    throw new ArgumentException(
                        "Transitional continuous guns require a beam or spray shot pattern.",
                        nameof(shotPattern));
                }
            }
            else if (shotPattern.Kind == GunShotPatternKind.Beam)
            {
                throw new ArgumentException(
                    "Transitional beam patterns require continuous fire settings.",
                    nameof(shotPattern));
            }

            bool hasExplosionData = impact.ExplosionTrigger != null || damage.HasAreaDamage;
            if (hasExplosionData && effects.Explosion == null)
            {
                throw new ArgumentException(
                    "Explosion trigger or area-damage data requires an explosion effect.",
                    nameof(effects));
            }
            if (damage.HasDamageOverTime && effects.DamageOverTime == null)
            {
                throw new ArgumentException(
                    "Damage-over-time data requires a damage-over-time effect.",
                    nameof(effects));
            }
        }

        private static void RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }
        }
    }
}
