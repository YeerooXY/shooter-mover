using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShooterMover.Domain.Weapons
{
    public enum WeaponDefinitionIssueCode
    {
        MissingIdentity = 1,
        MissingFireSettings = 2,
        InvalidRateOfFire = 3,
        MissingBurstSettings = 4,
        UnexpectedBurstSettings = 5,
        MissingShotSettings = 6,
        InvalidProjectileCount = 7,
        InvalidSpread = 8,
        MissingBaseStats = 9,
        InvalidDamage = 10,
        InvalidDamageOverTime = 11,
        InvalidPierce = 12,
        InvalidRicochet = 13,
        MissingDeliveryData = 14,
        IncompatibleDeliveryData = 15,
        LaserCarriesProjectileData = 16,
        RocketExplosionRequired = 17,
        InvalidGuidance = 18,
        MissingPresentation = 19,
        MissingDropMetadata = 20,
        InvalidStrongboxTierRestriction = 21,
        ConflictingSpecialBehaviors = 22,
        UnsupportedStructuralAugmentChange = 23,
        TransitionalProjectionRejected = 24,
    }

    public sealed class WeaponDefinitionIssue : IComparable<WeaponDefinitionIssue>
    {
        public WeaponDefinitionIssue(
            WeaponDefinitionIssueCode code,
            string path,
            string detail)
        {
            Code = code;
            Path = path ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public WeaponDefinitionIssueCode Code { get; }
        public string Path { get; }
        public string Detail { get; }

        public int CompareTo(WeaponDefinitionIssue other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }
            int path = string.CompareOrdinal(Path, other.Path);
            return path != 0 ? path : Code.CompareTo(other.Code);
        }

        public override string ToString()
        {
            return Code + " at " + Path + ": " + Detail;
        }
    }

    public sealed class WeaponDefinitionConstructionResult
    {
        private readonly ReadOnlyCollection<WeaponDefinitionIssue> issues;

        internal WeaponDefinitionConstructionResult(
            WeaponBlueprint definition,
            IEnumerable<WeaponDefinitionIssue> values)
        {
            Definition = definition;
            var copy = new List<WeaponDefinitionIssue>(
                values ?? Array.Empty<WeaponDefinitionIssue>());
            copy.Sort();
            issues = new ReadOnlyCollection<WeaponDefinitionIssue>(copy);
        }

        public WeaponBlueprint Definition { get; }
        public IReadOnlyList<WeaponDefinitionIssue> Issues { get { return issues; } }
        public bool Succeeded { get { return Definition != null && issues.Count == 0; } }
    }

    public sealed class WeaponDefinitionValidationException : ArgumentException
    {
        private readonly ReadOnlyCollection<WeaponDefinitionIssue> issues;

        public WeaponDefinitionValidationException(
            IEnumerable<WeaponDefinitionIssue> values)
            : base(BuildMessage(values))
        {
            var copy = new List<WeaponDefinitionIssue>(
                values ?? Array.Empty<WeaponDefinitionIssue>());
            copy.Sort();
            issues = new ReadOnlyCollection<WeaponDefinitionIssue>(copy);
        }

        public IReadOnlyList<WeaponDefinitionIssue> Issues { get { return issues; } }

        private static string BuildMessage(IEnumerable<WeaponDefinitionIssue> values)
        {
            var copy = new List<WeaponDefinitionIssue>(
                values ?? Array.Empty<WeaponDefinitionIssue>());
            copy.Sort();
            return copy.Count == 0
                ? "Weapon definition validation failed."
                : "Weapon definition validation failed: " + copy[0];
        }
    }

    internal static class WeaponDefinitionValidator
    {
        public static List<WeaponDefinitionIssue> Validate(
            WeaponIdentity identity,
            WeaponFireSettings fire,
            WeaponShotPattern shot,
            WeaponBaseStats baseStats,
            WeaponDeliverySpec delivery,
            WeaponPresentation presentation,
            WeaponDropMetadata dropMetadata)
        {
            var issues = new List<WeaponDefinitionIssue>();
            if (identity == null)
            {
                Add(issues, WeaponDefinitionIssueCode.MissingIdentity, "identity", "Identity is required.");
            }
            if (fire == null)
            {
                Add(issues, WeaponDefinitionIssueCode.MissingFireSettings, "fire", "Fire settings are required.");
            }
            else
            {
                ValidateFire(fire, issues);
            }
            if (shot == null)
            {
                Add(issues, WeaponDefinitionIssueCode.MissingShotSettings, "shot", "Shot settings are required.");
            }
            else
            {
                if (shot.ProjectilesPerShot < 1)
                {
                    Add(issues, WeaponDefinitionIssueCode.InvalidProjectileCount, "shot.projectiles_per_shot", "At least one simultaneous attack instance is required.");
                }
                if (double.IsNaN(shot.SpreadDegrees)
                    || double.IsInfinity(shot.SpreadDegrees)
                    || shot.SpreadDegrees < 0d
                    || shot.SpreadDegrees > 360d)
                {
                    Add(issues, WeaponDefinitionIssueCode.InvalidSpread, "shot.spread_degrees", "Spread must be finite and between zero and 360 degrees.");
                }
            }
            if (baseStats == null)
            {
                Add(issues, WeaponDefinitionIssueCode.MissingBaseStats, "base_stats", "Universal base stats are required.");
            }
            else
            {
                if (baseStats.DirectDamage < 0d)
                {
                    Add(issues, WeaponDefinitionIssueCode.InvalidDamage, "base_stats.direct_damage", "Direct damage cannot be negative.");
                }
                if (baseStats.DirectDamage <= 0d && baseStats.DamageOverTime == null)
                {
                    Add(issues, WeaponDefinitionIssueCode.InvalidDamage, "base_stats", "Positive direct damage or explicit DoT is required.");
                }
                if (baseStats.Pierce.Tenths < 0)
                {
                    Add(issues, WeaponDefinitionIssueCode.InvalidPierce, "base_stats.pierce", "Pierce fixed-point tenths cannot be negative.");
                }
                if (baseStats.Ricochet.Tenths < 0)
                {
                    Add(issues, WeaponDefinitionIssueCode.InvalidRicochet, "base_stats.ricochet", "Ricochet fixed-point tenths cannot be negative.");
                }
            }
            if (delivery == null)
            {
                Add(issues, WeaponDefinitionIssueCode.MissingDeliveryData, "delivery", "One typed delivery is required.");
            }
            else
            {
                ValidateDelivery(baseStats, delivery, issues);
            }
            if (presentation == null)
            {
                Add(issues, WeaponDefinitionIssueCode.MissingPresentation, "presentation", "Separate inventory, mounted, and delivery presentation references are required.");
            }
            if (dropMetadata == null)
            {
                Add(issues, WeaponDefinitionIssueCode.MissingDropMetadata, "drop_metadata", "Equipment-specific drop metadata is required.");
            }
            else if (dropMetadata.StrongboxEligibility == null)
            {
                Add(issues, WeaponDefinitionIssueCode.InvalidStrongboxTierRestriction, "drop_metadata.strongbox_eligibility", "An explicit minimum tier or allowed-tier list is required.");
            }
            return issues;
        }

        private static void ValidateFire(
            WeaponFireSettings fire,
            ICollection<WeaponDefinitionIssue> issues)
        {
            if (!fire.IsCanonicalAuthoredMode
                || double.IsNaN(fire.RateOfFire)
                || double.IsInfinity(fire.RateOfFire)
                || fire.RateOfFire <= 0d)
            {
                Add(issues, WeaponDefinitionIssueCode.InvalidRateOfFire, "fire.rate_of_fire", "Canonical authored weapons require a positive firing-cycle rate.");
            }
            if (fire.Mode == WeaponFireMode.Burst)
            {
                if (fire.BurstSettings == null
                    || fire.ShotsPerBurst < 2
                    || fire.IntervalBetweenBurstShotsSeconds <= 0d)
                {
                    Add(issues, WeaponDefinitionIssueCode.MissingBurstSettings, "fire.burst", "Burst mode requires valid sequential shot count and interval data.");
                }
            }
            else if (fire.BurstSettings != null
                || fire.ShotsPerBurst != 1
                || fire.IntervalBetweenBurstShotsSeconds != 0d)
            {
                Add(issues, WeaponDefinitionIssueCode.UnexpectedBurstSettings, "fire.burst", "Non-burst modes cannot carry burst-only data.");
            }
        }

        private static void ValidateDelivery(
            WeaponBaseStats baseStats,
            WeaponDeliverySpec delivery,
            ICollection<WeaponDefinitionIssue> issues)
        {
            if ((delivery.Type == WeaponDeliveryType.Laser
                    || delivery.Type == WeaponDeliveryType.Special)
                && delivery.Guidance.Mode != WeaponGuidanceMode.Unguided)
            {
                Add(issues, WeaponDefinitionIssueCode.InvalidGuidance, "delivery.guidance", "This delivery type cannot carry travelling-projectile guidance.");
            }
            if (delivery.Type == WeaponDeliveryType.Rocket
                && (delivery.Effects.Explosion == null
                    || delivery.Impact.ExplosionTrigger == null
                    || !delivery.Impact.ExplosionTrigger.OnEnemyImpact
                    || !delivery.Impact.ExplosionTrigger.OnWallImpact))
            {
                Add(issues, WeaponDefinitionIssueCode.RocketExplosionRequired, "delivery.rocket", "Rocket delivery requires immediate contact explosion behaviour and a positive explosion radius.");
            }
            if (delivery.Type == WeaponDeliveryType.Orb
                && delivery.Impact.ExplosionTrigger != null
                && delivery.Impact.ExplosionTrigger.OnWallImpact)
            {
                Add(issues, WeaponDefinitionIssueCode.IncompatibleDeliveryData, "delivery.orb", "Orb delivery cannot use rocket-style wall-contact detonation.");
            }
            if (delivery.Type == WeaponDeliveryType.Special
                && (delivery.Special == null || delivery.Special.BehaviorId == null))
            {
                Add(issues, WeaponDefinitionIssueCode.ConflictingSpecialBehaviors, "delivery.special.behavior_id", "Exactly one approved stable behaviour ID is required.");
            }
            if (baseStats != null
                && baseStats.Ricochet.Tenths > 0
                && delivery.Impact.Ricochet == null)
            {
                Add(issues, WeaponDefinitionIssueCode.InvalidRicochet, "delivery.impact.ricochet", "A positive ricochet budget requires reusable ricochet impact settings.");
            }
            if (baseStats != null
                && baseStats.Ricochet.Tenths == 0
                && delivery.Impact.Ricochet != null)
            {
                Add(issues, WeaponDefinitionIssueCode.InvalidRicochet, "base_stats.ricochet", "Ricochet impact settings require a positive authored ricochet budget.");
            }
        }

        private static void Add(
            ICollection<WeaponDefinitionIssue> issues,
            WeaponDefinitionIssueCode code,
            string path,
            string detail)
        {
            issues.Add(new WeaponDefinitionIssue(code, path, detail));
        }
    }
}
