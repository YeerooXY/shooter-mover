using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Catalog
{
    public enum GunMappingIssueCode
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
        MissingIntentDefinitionId = 27,
        MismatchedIntentDefinitionId = 28,
        MissingExplosionTrigger = 29,
        UnexpectedExplosionTrigger = 30,
        MissingAuthoredMappingDetails = 31,
        InvalidAuthoredDelivery = 32,
        LaserCarriesProjectileSpeed = 33,
        InvalidAuthoredRicochet = 34,
        InvalidAuthoredMovementPenalty = 35,
        MissingAuthoredPresentation = 36,
        MissingAuthoredDropIdentity = 37,
        InvalidStrongboxTierRestriction = 38,
        TopBoxOnlyRequiresExplicitRule = 39,
        AuthoredDefinitionRejected = 40,
        UnsupportedAreaDamage = 41,
    }

    public sealed class GunMappingIssue
    {
        public GunMappingIssue(GunMappingIssueCode code, string path, string detail)
        {
            Code = code;
            Path = path ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public GunMappingIssueCode Code { get; }
        public string Path { get; }
        public string Detail { get; }

        public override string ToString()
        {
            return Code + " at " + Path + ": " + Detail;
        }
    }

    public sealed class GunMappingResult
    {
        private readonly ReadOnlyCollection<GunMappingIssue> issues;

        internal GunMappingResult(Gun blueprint, IEnumerable<GunMappingIssue> mappingIssues)
        {
            Blueprint = blueprint;
            issues = new ReadOnlyCollection<GunMappingIssue>(
                new List<GunMappingIssue>(mappingIssues ?? Array.Empty<GunMappingIssue>()));
        }

        public Gun Blueprint { get; }
        public IReadOnlyList<GunMappingIssue> Issues { get { return issues; } }
        public bool Succeeded { get { return Blueprint != null && issues.Count == 0; } }
    }

    public enum GunCatalogSpreadInterpretation
    {
        None = 1,
        AuthoredSpread = 2,
        AuthoredRandomness = 3,
    }

    public sealed class GunCatalogExplosionMapping
    {
        public GunCatalogExplosionMapping(double minimumDamageMultiplier)
        {
            MinimumDamageMultiplier = minimumDamageMultiplier;
        }
        public double MinimumDamageMultiplier { get; }
    }

    public sealed class GunCatalogDamageOverTimeMapping
    {
        public GunCatalogDamageOverTimeMapping(double ticksPerSecond, int maximumStacks, bool refreshesDuration)
        {
            TicksPerSecond = ticksPerSecond;
            MaximumStacks = maximumStacks;
            RefreshesDuration = refreshesDuration;
        }
        public double TicksPerSecond { get; }
        public int MaximumStacks { get; }
        public bool RefreshesDuration { get; }
    }

    public sealed class GunCatalogChainMapping
    {
        public GunCatalogChainMapping(double retainedDamagePerJump)
        {
            RetainedDamagePerJump = retainedDamagePerJump;
        }
        public double RetainedDamagePerJump { get; }
    }

    public sealed class GunCatalogBlueprintMappingIntent
    {
        public GunCatalogBlueprintMappingIntent(
            GunDefinitionId expectedDefinitionId,
            GunFireMode fireMode,
            int shotsPerTrigger,
            GunShotPatternKind shotPatternKind,
            GunCatalogSpreadInterpretation spreadInterpretation,
            int pulsesPerShot,
            double intervalBetweenPulsesSeconds,
            double intervalBetweenBurstShotsSeconds,
            double intervalAfterBurstSeconds,
            GunProjectileKind projectileKind,
            GunProjectileTerminationBehavior projectileTermination,
            GunDamageCategory? explicitDamageCategory,
            GunGuidanceSpec guidance,
            GunImpactSpec impact,
            GunCatalogExplosionMapping explosion,
            GunCatalogDamageOverTimeMapping damageOverTime,
            GunCatalogChainMapping chain,
            string presentationReference)
        {
            ExpectedDefinitionId = expectedDefinitionId;
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

        public GunDefinitionId ExpectedDefinitionId { get; }
        public GunFireMode FireMode { get; }
        public int ShotsPerTrigger { get; }
        public GunShotPatternKind ShotPatternKind { get; }
        public GunCatalogSpreadInterpretation SpreadInterpretation { get; }
        public int PulsesPerShot { get; }
        public double IntervalBetweenPulsesSeconds { get; }
        public double IntervalBetweenBurstShotsSeconds { get; }
        public double IntervalAfterBurstSeconds { get; }
        public GunProjectileKind ProjectileKind { get; }
        public GunProjectileTerminationBehavior ProjectileTermination { get; }
        public GunDamageCategory? ExplicitDamageCategory { get; }
        public GunGuidanceSpec Guidance { get; }
        public GunImpactSpec Impact { get; }
        public GunCatalogExplosionMapping Explosion { get; }
        public GunCatalogDamageOverTimeMapping DamageOverTime { get; }
        public GunCatalogChainMapping Chain { get; }
        public string PresentationReference { get; }
    }

    public enum GunCatalogStrongboxEligibilityMappingMode
    {
        MinimumTier = 1,
        ExplicitAllowedTierIds = 2,
        ExplicitAllowedTiers = ExplicitAllowedTierIds,
    }

    public sealed class GunCatalogAuthoredMappingDetails
    {
        private readonly ReadOnlyCollection<StableId> allowedStrongboxTierIds;

        public GunCatalogAuthoredMappingDetails(
            GunDeliveryType deliveryType,
            double deliveryRadiusOrWidth,
            int ricochetTenths,
            double movementPenaltyPercent,
            GunSpecialDeliverySettings specialDelivery,
            GunPresentation presentation,
            StableId equipmentDefinitionId,
            StableId rarityId,
            GunDropAvailability availability,
            GunCatalogStrongboxEligibilityMappingMode strongboxEligibilityMode,
            int minimumStrongboxTier,
            IEnumerable<StableId> allowedStrongboxTierIds)
        {
            DeliveryType = deliveryType;
            DeliveryRadiusOrWidth = deliveryRadiusOrWidth;
            RicochetTenths = ricochetTenths;
            MovementPenaltyPercent = movementPenaltyPercent;
            SpecialDelivery = specialDelivery;
            Presentation = presentation;
            EquipmentDefinitionId = equipmentDefinitionId;
            RarityId = rarityId;
            Availability = availability;
            StrongboxEligibilityMode = strongboxEligibilityMode;
            MinimumStrongboxTier = minimumStrongboxTier;
            this.allowedStrongboxTierIds = new ReadOnlyCollection<StableId>(
                new List<StableId>(allowedStrongboxTierIds ?? Array.Empty<StableId>()));
        }

        public GunDeliveryType DeliveryType { get; }
        public double DeliveryRadiusOrWidth { get; }
        public int RicochetTenths { get; }
        public double MovementPenaltyPercent { get; }
        public GunSpecialDeliverySettings SpecialDelivery { get; }
        public GunPresentation Presentation { get; }
        public StableId EquipmentDefinitionId { get; }
        public StableId RarityId { get; }
        public GunDropAvailability Availability { get; }
        public GunCatalogStrongboxEligibilityMappingMode StrongboxEligibilityMode { get; }
        public int MinimumStrongboxTier { get; }
        public IReadOnlyList<StableId> AllowedStrongboxTierIds { get { return allowedStrongboxTierIds; } }
        internal IReadOnlyList<StableId> AllowedStrongboxTiers { get { return allowedStrongboxTierIds; } }
    }
}
