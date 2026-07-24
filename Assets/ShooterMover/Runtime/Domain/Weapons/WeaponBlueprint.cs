using System;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Domain.Weapons
{
    /// <summary>
    /// Canonical immutable weapon definition. Runtime state, inventory identity, item level,
    /// installed augments, and resources deliberately live outside this contract.
    /// </summary>
    public sealed class WeaponBlueprint
    {
        private WeaponBlueprint(
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
            DefinitionId = definitionId;
            DisplayName = displayName;
            WeaponFamily = weaponFamily;
            FireSettings = fireSettings;
            ShotPattern = shotPattern;
            Projectile = projectile;
            Guidance = guidance;
            Impact = impact;
            Damage = damage;
            Effects = effects;
            DropMetadataReference = dropMetadataReference;
            PresentationReference = presentationReference;
        }

        public WeaponDefinitionId DefinitionId { get; }
        public string DisplayName { get; }
        public string WeaponFamily { get; }
        public WeaponFireSettings FireSettings { get; }
        public WeaponShotPattern ShotPattern { get; }
        public WeaponProjectileSpec Projectile { get; }
        public WeaponGuidanceSpec Guidance { get; }
        public WeaponImpactSpec Impact { get; }
        public WeaponDamageSpec Damage { get; }
        public WeaponEffects Effects { get; }
        public string DropMetadataReference { get; }
        public string PresentationReference { get; }

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

            if (shotPattern.UsesProjectiles && projectile == null)
            {
                throw new ArgumentException(
                    "A projectile-emitting shot pattern requires WeaponProjectileSpec.",
                    nameof(projectile));
            }
            if (guidance.Mode == WeaponGuidanceMode.Homing && projectile == null)
            {
                throw new ArgumentException(
                    "Homing guidance requires a projectile.",
                    nameof(guidance));
            }
            if (impact.Ricochet != null && projectile == null)
            {
                throw new ArgumentException(
                    "Ricochet configuration requires a projectile.",
                    nameof(impact));
            }
            if (fireSettings.IsContinuous)
            {
                if (projectile != null || shotPattern.UsesProjectiles)
                {
                    throw new ArgumentException(
                        "Continuous weapons cannot reuse projectile emission fields.");
                }
                if (shotPattern.Kind != WeaponShotPatternKind.Beam
                    && shotPattern.Kind != WeaponShotPatternKind.Spray)
                {
                    throw new ArgumentException(
                        "Continuous weapons require a beam or spray shot pattern.",
                        nameof(shotPattern));
                }
            }
            else if (shotPattern.Kind == WeaponShotPatternKind.Beam)
            {
                throw new ArgumentException(
                    "Beam patterns require continuous fire settings.",
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

            return new WeaponBlueprint(
                definitionId,
                displayName,
                weaponFamily,
                fireSettings,
                shotPattern,
                projectile,
                guidance,
                impact,
                damage,
                effects,
                dropMetadataReference,
                presentationReference);
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
