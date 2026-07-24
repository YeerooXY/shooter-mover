using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Application.Weapons.Catalog
{
    public enum WeaponBlueprintMappingIssueCode
    {
        NullCatalog = 1,
        MissingDefinitionId = 2,
        UnknownDefinition = 3,
        MissingMappingIntent = 4,
        UnknownFamily = 5,
        UnknownArchetype = 6,
        UnsupportedDamageType = 7,
        ConflictingDamageCategory = 8,
        UnsupportedContinuousDefinition = 9,
        InvalidFireConfiguration = 10,
        InvalidShotPattern = 11,
        InvalidProjectileConfiguration = 12,
        MissingGuidance = 13,
        MissingImpactConfiguration = 14,
        MissingExplosionMapping = 15,
        UnexpectedExplosionMapping = 16,
        MissingDamageOverTimeMapping = 17,
        UnexpectedDamageOverTimeMapping = 18,
        MissingChainMapping = 19,
        UnexpectedChainMapping = 20,
        UnsupportedPersistentPool = 21,
        UnsupportedHealing = 22,
        MissingPresentationReference = 23,
        AmbiguousPresentationReference = 24,
        UnauthoredPresentationReference = 25,
        DomainContractRejected = 26,
    }

    public sealed class WeaponBlueprintMappingIssue
    {
        public WeaponBlueprintMappingIssue(
            WeaponBlueprintMappingIssueCode code,
            string path,
            string detail)
        {
            Code = code;
            Path = path ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public WeaponBlueprintMappingIssueCode Code { get; }
        public string Path { get; }
        public string Detail { get; }

        public override string ToString()
        {
            return Code + " at " + Path + ": " + Detail;
        }
    }

    public sealed class WeaponBlueprintMappingResult
    {
        private readonly ReadOnlyCollection<WeaponBlueprintMappingIssue> issues;

        internal WeaponBlueprintMappingResult(
            WeaponBlueprint blueprint,
            IEnumerable<WeaponBlueprintMappingIssue> mappingIssues)
        {
            Blueprint = blueprint;
            issues = new ReadOnlyCollection<WeaponBlueprintMappingIssue>(
                new List<WeaponBlueprintMappingIssue>(
                    mappingIssues ?? Array.Empty<WeaponBlueprintMappingIssue>()));
        }

        public WeaponBlueprint Blueprint { get; }
        public IReadOnlyList<WeaponBlueprintMappingIssue> Issues
        {
            get { return issues; }
        }

        public bool Succeeded
        {
            get { return Blueprint != null && issues.Count == 0; }
        }
    }

    /// <summary>
    /// States whether the legacy SpreadDegrees value is an authored multi-projectile arc
    /// or a random angular deviation. The catalog does not distinguish those meanings.
    /// </summary>
    public enum WeaponCatalogSpreadInterpretation
    {
        None = 1,
        AuthoredSpread = 2,
        AuthoredRandomness = 3,
    }

    public sealed class WeaponCatalogExplosionMapping
    {
        public WeaponCatalogExplosionMapping(double minimumDamageMultiplier)
        {
            MinimumDamageMultiplier = minimumDamageMultiplier;
        }

        public double MinimumDamageMultiplier { get; }
    }

    public sealed class WeaponCatalogDamageOverTimeMapping
    {
        public WeaponCatalogDamageOverTimeMapping(
            double ticksPerSecond,
            int maximumStacks,
            bool refreshesDuration)
        {
            TicksPerSecond = ticksPerSecond;
            MaximumStacks = maximumStacks;
            RefreshesDuration = refreshesDuration;
        }

        public double TicksPerSecond { get; }
        public int MaximumStacks { get; }
        public bool RefreshesDuration { get; }
    }

    public sealed class WeaponCatalogChainMapping
    {
        public WeaponCatalogChainMapping(double retainedDamagePerJump)
        {
            RetainedDamagePerJump = retainedDamagePerJump;
        }

        public double RetainedDamagePerJump { get; }
    }

    /// <summary>
    /// Explicit semantic decisions that are absent from WeaponDefinitionData. Numeric combat
    /// values already present in the catalog are never overridden by this contract.
    /// </summary>
    public sealed class WeaponCatalogBlueprintMappingIntent
    {
        public WeaponCatalogBlueprintMappingIntent(
            WeaponFireMode fireMode,
            int shotsPerTrigger,
            WeaponShotPatternKind shotPatternKind,
            WeaponCatalogSpreadInterpretation spreadInterpretation,
            int pulsesPerShot,
            double intervalBetweenPulsesSeconds,
            double intervalBetweenBurstShotsSeconds,
            double intervalAfterBurstSeconds,
            WeaponProjectileKind projectileKind,
            WeaponProjectileTerminationBehavior projectileTermination,
            WeaponDamageCategory? explicitDamageCategory,
            WeaponGuidanceSpec guidance,
            WeaponImpactSpec impact,
            WeaponCatalogExplosionMapping explosion,
            WeaponCatalogDamageOverTimeMapping damageOverTime,
            WeaponCatalogChainMapping chain,
            string presentationReference)
        {
            FireMode = fireMode;
            ShotsPerTrigger = shotsPerTrigger;
            ShotPatternKind = shotPatternKind;
            SpreadInterpretation = spreadInterpretation;
            PulsesPerShot = pulsesPerShot;
            IntervalBetweenPulsesSeconds = intervalBetweenPulsesSeconds;
            IntervalBetweenBurstShotsSeconds = intervalBetweenBurstShotsSeconds;
            IntervalAfterBurstSeconds = intervalAfterBurstSeconds;
            ProjectileKind = projectileKind;
            ProjectileTermination = projectileTermination;
            ExplicitDamageCategory = explicitDamageCategory;
            Guidance = guidance;
            Impact = impact;
            Explosion = explosion;
            DamageOverTime = damageOverTime;
            Chain = chain;
            PresentationReference = presentationReference;
        }

        public WeaponFireMode FireMode { get; }
        public int ShotsPerTrigger { get; }
        public WeaponShotPatternKind ShotPatternKind { get; }
        public WeaponCatalogSpreadInterpretation SpreadInterpretation { get; }
        public int PulsesPerShot { get; }
        public double IntervalBetweenPulsesSeconds { get; }
        public double IntervalBetweenBurstShotsSeconds { get; }
        public double IntervalAfterBurstSeconds { get; }
        public WeaponProjectileKind ProjectileKind { get; }
        public WeaponProjectileTerminationBehavior ProjectileTermination { get; }
        public WeaponDamageCategory? ExplicitDamageCategory { get; }
        public WeaponGuidanceSpec Guidance { get; }
        public WeaponImpactSpec Impact { get; }
        public WeaponCatalogExplosionMapping Explosion { get; }
        public WeaponCatalogDamageOverTimeMapping DamageOverTime { get; }
        public WeaponCatalogChainMapping Chain { get; }
        public string PresentationReference { get; }
    }
}
