using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShooterMover.Domain.Guns
{
    public enum GunDefinitionIssueCode
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

    public sealed class GunDefinitionIssue : IComparable<GunDefinitionIssue>
    {
        public GunDefinitionIssue(
            GunDefinitionIssueCode code,
            string path,
            string detail)
        {
            Code = code;
            Path = path ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public GunDefinitionIssueCode Code { get; }
        public string Path { get; }
        public string Detail { get; }

        public int CompareTo(GunDefinitionIssue other)
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

    public sealed class GunDefinitionConstructionResult
    {
        private readonly ReadOnlyCollection<GunDefinitionIssue> issues;

        internal GunDefinitionConstructionResult(
            Gun definition,
            IEnumerable<GunDefinitionIssue> values)
        {
            Definition = definition;
            var copy = new List<GunDefinitionIssue>(
                values ?? Array.Empty<GunDefinitionIssue>());
            copy.Sort();
            issues = new ReadOnlyCollection<GunDefinitionIssue>(copy);
        }

        public Gun Definition { get; }
        public IReadOnlyList<GunDefinitionIssue> Issues { get { return issues; } }
        public bool Succeeded { get { return Definition != null && issues.Count == 0; } }
    }

    public sealed class GunDefinitionValidationException : ArgumentException
    {
        private readonly ReadOnlyCollection<GunDefinitionIssue> issues;

        public GunDefinitionValidationException(
            IEnumerable<GunDefinitionIssue> values)
            : base(BuildMessage(values))
        {
            var copy = new List<GunDefinitionIssue>(
                values ?? Array.Empty<GunDefinitionIssue>());
            copy.Sort();
            issues = new ReadOnlyCollection<GunDefinitionIssue>(copy);
        }

        public IReadOnlyList<GunDefinitionIssue> Issues { get { return issues; } }

        private static string BuildMessage(IEnumerable<GunDefinitionIssue> values)
        {
            var copy = new List<GunDefinitionIssue>(
                values ?? Array.Empty<GunDefinitionIssue>());
            copy.Sort();
            return copy.Count == 0
                ? "Gun definition validation failed."
                : "Gun definition validation failed: " + copy[0];
        }
    }

    internal static class GunDefinitionValidator
    {
        public static List<GunDefinitionIssue> Validate(
            GunIdentity identity,
            FireSettings fire,
            GunShotPattern shot,
            GunBaseStats baseStats,
            ShotPattern delivery,
            GunPresentation presentation,
            GunDropMetadata dropMetadata)
        {
            var issues = new List<GunDefinitionIssue>();
            if (identity == null)
            {
                Add(issues, GunDefinitionIssueCode.MissingIdentity, "identity", "Identity is required.");
            }
            if (fire == null)
            {
                Add(issues, GunDefinitionIssueCode.MissingFireSettings, "fire", "Fire settings are required.");
            }
            else
            {
                ValidateFire(fire, issues);
            }
            if (shot == null)
            {
                Add(issues, GunDefinitionIssueCode.MissingShotSettings, "shot", "Shot settings are required.");
            }
            else
            {
                ValidateShot(shot, issues);
            }
            if (baseStats == null)
            {
                Add(issues, GunDefinitionIssueCode.MissingBaseStats, "base_stats", "Universal base stats are required.");
            }
            else
            {
                ValidateBaseStats(baseStats, issues);
            }
            if (delivery == null)
            {
                Add(issues, GunDefinitionIssueCode.MissingDeliveryData, "delivery", "One typed delivery is required.");
            }
            else
            {
                ValidateDelivery(baseStats, delivery, issues);
            }
            if (presentation == null)
            {
                Add(issues, GunDefinitionIssueCode.MissingPresentation, "presentation", "Separate inventory, mounted, and delivery presentation references are required.");
            }
            if (dropMetadata == null)
            {
                Add(issues, GunDefinitionIssueCode.MissingDropMetadata, "drop_metadata", "Equipment-specific drop metadata is required.");
            }
            else if (dropMetadata.StrongboxEligibility == null)
            {
                Add(issues, GunDefinitionIssueCode.InvalidStrongboxTierRestriction, "drop_metadata.strongbox_eligibility", "An explicit minimum tier or allowed-tier list is required.");
            }
            return issues;
        }

        private static void ValidateFire(
            FireSettings fire,
            ICollection<GunDefinitionIssue> issues)
        {
            if (!fire.IsCanonicalAuthoredMode
                || double.IsNaN(fire.RateOfFire)
                || double.IsInfinity(fire.RateOfFire)
                || fire.RateOfFire <= 0d)
            {
                Add(issues, GunDefinitionIssueCode.InvalidRateOfFire, "fire.rate_of_fire", "Canonical authored guns require a positive firing-cycle rate.");
            }
            if (fire.ShotsPerTrigger != 1)
            {
                Add(issues, GunDefinitionIssueCode.IncompatibleDeliveryData, "fire.shots_per_trigger", "Canonical firing cycles contain one shot group; simultaneous emissions belong to shot.projectiles_per_shot.");
            }

            if (fire.Mode == GunFireMode.Burst)
            {
                if (fire.BurstSettings == null
                    || fire.ShotsPerBurst < 2
                    || fire.IntervalBetweenBurstShotsSeconds <= 0d)
                {
                    Add(issues, GunDefinitionIssueCode.MissingBurstSettings, "fire.burst", "Burst mode requires valid sequential shot count and interval data.");
                    return;
                }
                if (fire.RateOfFire <= 0d)
                {
                    return;
                }

                double cycleIntervalSeconds = 1d / fire.RateOfFire;
                double burstEmissionSpanSeconds =
                    (fire.ShotsPerBurst - 1d)
                    * fire.IntervalBetweenBurstShotsSeconds;
                double expectedRecoverySeconds =
                    cycleIntervalSeconds - burstEmissionSpanSeconds;
                if (double.IsNaN(expectedRecoverySeconds)
                    || double.IsInfinity(expectedRecoverySeconds)
                    || expectedRecoverySeconds <= 0d)
                {
                    Add(issues, GunDefinitionIssueCode.InvalidRateOfFire, "fire.rate_of_fire", "Rate of fire must leave positive recovery time after the sequential burst emission span.");
                    return;
                }

                double expectedSchedulerRate = 1d / expectedRecoverySeconds;
                if (!ApproximatelyEqual(
                        fire.IntervalAfterBurstSeconds,
                        expectedRecoverySeconds)
                    || !ApproximatelyEqual(
                        fire.ShotsPerSecond,
                        expectedSchedulerRate))
                {
                    Add(issues, GunDefinitionIssueCode.UnexpectedBurstSettings, "fire", "Canonical burst scheduler fields must be the exact derived recovery projection of rate of fire and burst timing.");
                }
            }
            else
            {
                if (fire.BurstSettings != null
                    || fire.ShotsPerBurst != 1
                    || fire.IntervalBetweenBurstShotsSeconds != 0d
                    || fire.IntervalAfterBurstSeconds != 0d)
                {
                    Add(issues, GunDefinitionIssueCode.UnexpectedBurstSettings, "fire.burst", "Non-burst modes cannot carry burst-only data.");
                }
                if (!ApproximatelyEqual(fire.ShotsPerSecond, fire.RateOfFire))
                {
                    Add(issues, GunDefinitionIssueCode.InvalidRateOfFire, "fire", "Semi-automatic and automatic scheduler cadence must equal the authored firing-cycle rate.");
                }
            }
        }

        private static void ValidateShot(
            GunShotPattern shot,
            ICollection<GunDefinitionIssue> issues)
        {
            if (shot.ProjectilesPerShot < 1)
            {
                Add(issues, GunDefinitionIssueCode.InvalidProjectileCount, "shot.projectiles_per_shot", "At least one simultaneous attack instance is required.");
            }

            double canonicalSpread = shot.CanonicalSpreadDegrees;
            if (double.IsNaN(canonicalSpread)
                || double.IsInfinity(canonicalSpread)
                || canonicalSpread < 0d
                || canonicalSpread > 360d)
            {
                Add(issues, GunDefinitionIssueCode.InvalidSpread, "shot.spread_degrees", "Spread must be finite and between zero and 360 degrees.");
            }

            if (shot.PulsesPerShot != 1
                || shot.IntervalBetweenPulsesSeconds != 0d)
            {
                Add(issues, GunDefinitionIssueCode.IncompatibleDeliveryData, "shot", "Canonical authored shot data contains simultaneous count and spread only. Sequential timing belongs to burst fire.");
            }

            switch (shot.Kind)
            {
                case GunShotPatternKind.Single:
                    if (shot.ProjectilesPerShot != 1
                        || shot.SpreadDegrees != 0d
                        || shot.RandomnessDegrees != 0d)
                    {
                        Add(issues, GunDefinitionIssueCode.IncompatibleDeliveryData, "shot", "A zero-spread single emission must use the canonical Single representation.");
                    }
                    break;

                case GunShotPatternKind.Spray:
                    if (shot.ProjectilesPerShot != 1
                        || shot.SpreadDegrees != 0d
                        || shot.RandomnessDegrees <= 0d)
                    {
                        Add(issues, GunDefinitionIssueCode.IncompatibleDeliveryData, "shot", "A single-emission accuracy cone must use positive random angular deviation and no deterministic spread arc.");
                    }
                    break;

                case GunShotPatternKind.Spread:
                    if (shot.ProjectilesPerShot < 2
                        || shot.SpreadDegrees <= 0d
                        || shot.RandomnessDegrees != 0d)
                    {
                        Add(issues, GunDefinitionIssueCode.IncompatibleDeliveryData, "shot", "A simultaneous multi-emission spread requires at least two attacks, a positive deterministic arc, and no separate randomness field.");
                    }
                    break;

                case GunShotPatternKind.Volley:
                    if (shot.ProjectilesPerShot < 2
                        || shot.SpreadDegrees != 0d
                        || shot.RandomnessDegrees != 0d)
                    {
                        Add(issues, GunDefinitionIssueCode.IncompatibleDeliveryData, "shot", "A zero-spread simultaneous multi-emission shot must use the canonical Volley representation.");
                    }
                    break;

                default:
                    Add(issues, GunDefinitionIssueCode.IncompatibleDeliveryData, "shot.kind", "Canonical authored definitions use Single, Spray, Spread, or Volley. Legacy pulse, twin-barrel, and beam patterns remain transitional.");
                    break;
            }
        }

        private static void ValidateBaseStats(
            GunBaseStats baseStats,
            ICollection<GunDefinitionIssue> issues)
        {
            if (baseStats.DirectDamage < 0d)
            {
                Add(issues, GunDefinitionIssueCode.InvalidDamage, "base_stats.direct_damage", "Direct damage cannot be negative.");
            }
            if (baseStats.DirectDamage <= 0d && baseStats.DamageOverTime == null)
            {
                Add(issues, GunDefinitionIssueCode.InvalidDamage, "base_stats", "Positive direct damage or explicit DoT is required.");
            }
            if (baseStats.DamageOverTime != null
                && (baseStats.DamageOverTime.DamagePerSecond <= 0d
                    || baseStats.DamageOverTime.DurationSeconds <= 0d))
            {
                Add(issues, GunDefinitionIssueCode.InvalidDamageOverTime, "base_stats.damage_over_time", "DoT damage per second and duration must both be positive.");
            }
            if (baseStats.Pierce.Tenths < 0)
            {
                Add(issues, GunDefinitionIssueCode.InvalidPierce, "base_stats.pierce", "Pierce fixed-point tenths cannot be negative.");
            }
            if (baseStats.Ricochet.Tenths < 0)
            {
                Add(issues, GunDefinitionIssueCode.InvalidRicochet, "base_stats.ricochet", "Ricochet fixed-point tenths cannot be negative.");
            }
            if (double.IsNaN(baseStats.Knockback)
                || double.IsInfinity(baseStats.Knockback)
                || baseStats.Knockback < 0d)
            {
                Add(issues, GunDefinitionIssueCode.InvalidDamage, "base_stats.knockback", "Knockback must be finite and non-negative.");
            }
        }

        private static void ValidateDelivery(
            GunBaseStats baseStats,
            ShotPattern delivery,
            ICollection<GunDefinitionIssue> issues)
        {
            if ((delivery.Type == GunDeliveryType.Laser
                    || delivery.Type == GunDeliveryType.Special)
                && delivery.Guidance.Mode != GunGuidanceMode.Unguided)
            {
                Add(issues, GunDefinitionIssueCode.InvalidGuidance, "delivery.guidance", "This delivery type cannot carry travelling-projectile guidance.");
            }
            if (delivery.Type == GunDeliveryType.Rocket
                && (delivery.Effects.Explosion == null
                    || delivery.Impact.ExplosionTrigger == null
                    || !delivery.Impact.ExplosionTrigger.OnEnemyImpact
                    || !delivery.Impact.ExplosionTrigger.OnWallImpact))
            {
                Add(issues, GunDefinitionIssueCode.RocketExplosionRequired, "delivery.rocket", "Rocket delivery requires immediate contact explosion behaviour and a positive explosion radius.");
            }
            if (delivery.Type == GunDeliveryType.Orb
                && delivery.Impact.ExplosionTrigger != null
                && delivery.Impact.ExplosionTrigger.OnWallImpact)
            {
                Add(issues, GunDefinitionIssueCode.IncompatibleDeliveryData, "delivery.orb", "Orb delivery cannot use rocket-style wall-contact detonation.");
            }
            if (delivery.Type == GunDeliveryType.Special
                && (delivery.Special == null || delivery.Special.BehaviorId == null))
            {
                Add(issues, GunDefinitionIssueCode.ConflictingSpecialBehaviors, "delivery.special.behavior_id", "Exactly one approved stable behaviour ID is required.");
            }
            if (baseStats != null)
            {
                ValidateEffectPresence(baseStats, delivery, issues);
                ValidateRicochet(baseStats, delivery, issues);
            }
        }

        private static void ValidateEffectPresence(
            GunBaseStats baseStats,
            ShotPattern delivery,
            ICollection<GunDefinitionIssue> issues)
        {
            if (baseStats.DamageOverTime != null
                && delivery.Effects.DamageOverTime == null)
            {
                Add(issues, GunDefinitionIssueCode.InvalidDamageOverTime, "delivery.effects.damage_over_time", "Authored DoT magnitude/duration requires reusable tick, stack, and refresh policy.");
            }
            if (baseStats.DamageOverTime == null
                && delivery.Effects.DamageOverTime != null)
            {
                Add(issues, GunDefinitionIssueCode.InvalidDamageOverTime, "base_stats.damage_over_time", "DoT effect policy cannot exist without explicit authored DoT magnitude and duration.");
            }
        }

        private static void ValidateRicochet(
            GunBaseStats baseStats,
            ShotPattern delivery,
            ICollection<GunDefinitionIssue> issues)
        {
            GunRicochetSpec impactRicochet = delivery.Impact.Ricochet;
            if (baseStats.Ricochet.Tenths > 0 && impactRicochet == null)
            {
                Add(issues, GunDefinitionIssueCode.InvalidRicochet, "delivery.impact.ricochet", "A positive ricochet budget requires reusable ricochet impact settings.");
                return;
            }
            if (baseStats.Ricochet.Tenths == 0 && impactRicochet != null)
            {
                Add(issues, GunDefinitionIssueCode.InvalidRicochet, "base_stats.ricochet", "Ricochet impact settings require a positive authored ricochet budget.");
                return;
            }
            if (impactRicochet != null
                && (!impactRicochet.FixedPointBudget.HasValue
                    || impactRicochet.FixedPointBudget.Value != baseStats.Ricochet))
            {
                Add(issues, GunDefinitionIssueCode.InvalidRicochet, "delivery.impact.ricochet.fixed_point_budget", "Canonical impact settings must carry the exact authored fixed-point ricochet budget.");
            }
        }

        private static bool ApproximatelyEqual(double left, double right)
        {
            double scale = Math.Max(1d, Math.Max(Math.Abs(left), Math.Abs(right)));
            return Math.Abs(left - right) <= scale * 1e-9d;
        }

        private static void Add(
            ICollection<GunDefinitionIssue> issues,
            GunDefinitionIssueCode code,
            string path,
            string detail)
        {
            issues.Add(new GunDefinitionIssue(code, path, detail));
        }
    }
}
