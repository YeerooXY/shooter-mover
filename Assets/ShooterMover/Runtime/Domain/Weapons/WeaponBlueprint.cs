using System;
using System.Collections.Generic;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Domain.Weapons
{
    /// <summary>
    /// Canonical immutable authored weapon definition.
    ///
    /// New content uses the grouped identity, fire, shot, base-stat, delivery, presentation, and
    /// drop-metadata properties. The older flat construction path is retained only as an explicit
    /// transitional output of WeaponCatalogBlueprintMapper while the catalogue schema is migrated.
    /// Runtime state, inventory identity, item level, installed augments, and active modifiers
    /// deliberately live outside this contract.
    /// </summary>
    public sealed class WeaponBlueprint
    {
        private WeaponBlueprint(
            WeaponIdentity identity,
            WeaponFireSettings fireSettings,
            WeaponShotPattern shotPattern,
            WeaponBaseStats baseStats,
            WeaponDeliverySpec delivery,
            WeaponPresentation presentation,
            WeaponDropMetadata dropMetadata,
            WeaponProjectileSpec projectileProjection,
            WeaponDamageSpec damageProjection,
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

        public WeaponIdentity Identity { get; }
        public WeaponFireSettings FireSettings { get; }
        public WeaponShotPattern ShotPattern { get; }
        public WeaponBaseStats BaseStats { get; }
        public WeaponDeliverySpec Delivery { get; }
        public WeaponPresentation Presentation { get; }
        public WeaponDropMetadata DropMetadata { get; }
        public bool IsTransitionalCatalogProjection { get; }

        public WeaponDefinitionId DefinitionId { get { return Identity.DefinitionId; } }
        public string DisplayName { get { return Identity.DisplayName; } }
        public string WeaponFamily { get { return Identity.FamilyId; } }

        /// <summary>
        /// Compatibility projection consumed by the current effective-weapon and live-runtime
        /// route. Lasers and specials correctly have no travelling-projectile projection.
        /// </summary>
        public WeaponProjectileSpec Projectile { get; }
        public WeaponGuidanceSpec Guidance
        {
            get
            {
                return Delivery == null
                    ? TransitionalGuidance
                    : Delivery.Guidance;
            }
        }
        public WeaponImpactSpec Impact
        {
            get
            {
                return Delivery == null
                    ? TransitionalImpact
                    : Delivery.Impact;
            }
        }
        public WeaponDamageSpec Damage { get; }
        public WeaponEffects Effects
        {
            get
            {
                return Delivery == null
                    ? TransitionalEffects
                    : Delivery.Effects;
            }
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

        private WeaponGuidanceSpec TransitionalGuidance { get; set; }
        private WeaponImpactSpec TransitionalImpact { get; set; }
        private WeaponEffects TransitionalEffects { get; set; }
        private string TransitionalDropMetadataReference { get; }
        private string TransitionalPresentationReference { get; }

        public static WeaponBlueprint CreateAuthored(
            WeaponIdentity identity,
            WeaponFireSettings fireSettings,
            WeaponShotPattern shotPattern,
            WeaponBaseStats baseStats,
            WeaponDeliverySpec delivery,
            WeaponPresentation presentation,
            WeaponDropMetadata dropMetadata)
        {
            WeaponDefinitionConstructionResult result = TryCreateAuthored(
                identity,
                fireSettings,
                shotPattern,
                baseStats,
                delivery,
                presentation,
                dropMetadata);
            if (!result.Succeeded)
            {
                throw new WeaponDefinitionValidationException(result.Issues);
            }
            return result.Definition;
        }

        public static WeaponDefinitionConstructionResult TryCreateAuthored(
            WeaponIdentity identity,
            WeaponFireSettings fireSettings,
            WeaponShotPattern shotPattern,
            WeaponBaseStats baseStats,
            WeaponDeliverySpec delivery,
            WeaponPresentation presentation,
            WeaponDropMetadata dropMetadata)
        {
            List<WeaponDefinitionIssue> issues = WeaponDefinitionValidator.Validate(
                identity,
                fireSettings,
                shotPattern,
                baseStats,
                delivery,
                presentation,
                dropMetadata);
            if (issues.Count != 0)
            {
                return new WeaponDefinitionConstructionResult(null, issues);
            }

            WeaponProjectileSpec projectile = null;
            WeaponDamageSpec damage;
            try
            {
                projectile = delivery.CreateTravellingProjectileSpec(baseStats);
                damage = WeaponDamageSpec.Create(
                    baseStats.DamageCategory,
                    baseStats.DirectDamage,
                    baseStats.DamageOverTime,
                    0d);
            }
            catch (InvalidOperationException exception)
            {
                issues.Add(new WeaponDefinitionIssue(
                    WeaponDefinitionIssueCode.TransitionalProjectionRejected,
                    "delivery",
                    exception.Message));
                return new WeaponDefinitionConstructionResult(null, issues);
            }
            catch (ArgumentException exception)
            {
                issues.Add(new WeaponDefinitionIssue(
                    WeaponDefinitionIssueCode.IncompatibleDeliveryData,
                    "delivery",
                    exception.Message));
                return new WeaponDefinitionConstructionResult(null, issues);
            }

            return new WeaponDefinitionConstructionResult(
                new WeaponBlueprint(
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

        /// <summary>
        /// Transitional flat construction path used by the current catalogue mapper. It preserves
        /// the live route while making the migration state explicit on the resulting definition.
        /// New authored content must use CreateAuthored.
        /// </summary>
        public static WeaponBlueprint Create(
            WeaponDefinitionId definitionId,
            string displayName,
            string weaponFamily,
            WeaponFireSettings fireSettings,
            WeaponShotPattern shotPattern,
            WeaponProjectileSpec projectile,
            WeaponGuidanceSpec guidance,
            WeaponImpactSpec impact,
            WeaponDamageSpec damage,
            WeaponEffects effects,
            string dropMetadataReference,
            string presentationReference)
        {
            if (definitionId == null)
            {
                throw new ArgumentNullException(nameof(definitionId));
            }
            RequireText(displayName, nameof(displayName));
            RequireText(weaponFamily, nameof(weaponFamily));
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

            var blueprint = new WeaponBlueprint(
                new WeaponIdentity(definitionId, displayName, weaponFamily),
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
            WeaponFireSettings fireSettings,
            WeaponShotPattern shotPattern,
            WeaponProjectileSpec projectile,
            WeaponGuidanceSpec guidance,
            WeaponImpactSpec impact,
            WeaponDamageSpec damage,
            WeaponEffects effects)
        {
            if (shotPattern.UsesProjectiles && projectile == null)
            {
                throw new ArgumentException(
                    "A projectile-emitting transitional shot pattern requires WeaponProjectileSpec.",
                    nameof(projectile));
            }
            if (guidance.Mode == WeaponGuidanceMode.Homing && projectile == null)
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
                        "Transitional continuous weapons cannot reuse projectile emission fields.");
                }
                if (shotPattern.Kind != WeaponShotPatternKind.Beam
                    && shotPattern.Kind != WeaponShotPatternKind.Spray)
                {
                    throw new ArgumentException(
                        "Transitional continuous weapons require a beam or spray shot pattern.",
                        nameof(shotPattern));
                }
            }
            else if (shotPattern.Kind == WeaponShotPatternKind.Beam)
            {
                throw new ArgumentException(
                    "Transitional beam patterns require continuous fire settings.",
                    nameof(shotPattern));
            }

            bool hasExplosionData = impact.ExplosionTrigger != null
                || damage.HasAreaDamage;
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
                throw new ArgumentException(
                    "A non-empty value is required.",
                    parameterName);
            }
        }
    }
}
