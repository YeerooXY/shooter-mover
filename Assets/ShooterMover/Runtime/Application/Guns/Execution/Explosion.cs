using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    public enum GunExplosionTargetBudgetPolicy
    {
        Unlimited = 1,
        RocketPierce = 2,
    }

    public sealed class GunExplosionResolutionRequest
    {
        private GunExplosionResolutionRequest(
            GunEffectSourceContext source,
            GunVector2 impactPosition,
            GunDamageSpec damage,
            GunExplosionEffect explosion,
            double resolvedExplosionDamage,
            GunExplosionTargetBudgetPolicy targetBudgetPolicy,
            PierceValue targetPierce,
            DeterministicRandom? random,
            IGunEffectTargetSource targetSource,
            GunEffectLineOfSightPolicy lineOfSightPolicy,
            IGunEffectLineOfSightResolver lineOfSightResolver)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ImpactPosition = impactPosition ?? throw new ArgumentNullException(nameof(impactPosition));
            if (!impactPosition.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(impactPosition));
            }
            Damage = damage ?? throw new ArgumentNullException(nameof(damage));
            if (double.IsNaN(resolvedExplosionDamage)
                || double.IsInfinity(resolvedExplosionDamage)
                || resolvedExplosionDamage <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(resolvedExplosionDamage),
                    "Explosion resolution requires positive resolved base damage.");
            }
            if (!Enum.IsDefined(typeof(GunExplosionTargetBudgetPolicy), targetBudgetPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(targetBudgetPolicy));
            }
            if (targetBudgetPolicy == GunExplosionTargetBudgetPolicy.RocketPierce)
            {
                if (targetPierce.Tenths <= 0)
                {
                    throw new ArgumentException(
                        "Canonical Rocket target budgeting requires positive final Pierce.",
                        nameof(targetPierce));
                }
                if (!random.HasValue
                    || random.Value.AlgorithmVersion
                        != DeterministicRandom.CurrentAlgorithmVersion)
                {
                    throw new ArgumentException(
                        "Canonical Rocket target budgeting requires the existing deterministic-random authority.",
                        nameof(random));
                }
            }

            Explosion = explosion ?? throw new ArgumentNullException(nameof(explosion));
            ResolvedExplosionDamage = resolvedExplosionDamage;
            TargetBudgetPolicy = targetBudgetPolicy;
            TargetPierce = targetPierce;
            Random = random;
            TargetSource = targetSource ?? throw new ArgumentNullException(nameof(targetSource));
            GunEffectResolutionMath.ValidateLineOfSight(
                lineOfSightPolicy,
                lineOfSightResolver);
            LineOfSightPolicy = lineOfSightPolicy;
            LineOfSightResolver = lineOfSightResolver;
        }

        /// <summary>
        /// Transitional constructor. Existing content continues to use independently authored
        /// AreaDamage and affects every eligible target in the explosion radius.
        /// </summary>
        public GunExplosionResolutionRequest(
            GunEffectSourceContext source,
            GunVector2 impactPosition,
            GunDamageSpec damage,
            GunExplosionEffect explosion,
            IGunEffectTargetSource targetSource,
            GunEffectLineOfSightPolicy lineOfSightPolicy,
            IGunEffectLineOfSightResolver lineOfSightResolver)
            : this(
                source,
                impactPosition,
                RequireLegacyAreaDamage(damage),
                explosion,
                damage.AreaDamage,
                GunExplosionTargetBudgetPolicy.Unlimited,
                new PierceValue(0),
                null,
                targetSource,
                lineOfSightPolicy,
                lineOfSightResolver)
        {
        }

        /// <summary>
        /// Canonical Rocket projection from the exact final values retained by projectile
        /// execution. No inventory, augment, skill, catalogue, registry or character lookup occurs.
        /// </summary>
        public static GunExplosionResolutionRequest ForCanonicalRocket(
            ProjectileExecutionProfile profile,
            GunEffectSourceContext source,
            GunVector2 impactPosition,
            DeterministicRandom random,
            IGunEffectTargetSource targetSource,
            GunEffectLineOfSightPolicy lineOfSightPolicy,
            IGunEffectLineOfSightResolver lineOfSightResolver)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }
            if (!profile.IsCanonicalRocket
                || profile.CanonicalDeliveryType != GunDeliveryType.Rocket
                || profile.Projectile == null
                || profile.Projectile.Kind != GunProjectileKind.Rocket)
            {
                throw new ArgumentException(
                    "canonical-rocket-request-profile-required",
                    nameof(profile));
            }
            if (profile.Effects == null || profile.Effects.Explosion == null)
            {
                throw new ArgumentException(
                    "canonical-rocket-request-explosion-required",
                    nameof(profile));
            }
            GunExplosionTriggerSpec trigger = profile.Impact == null
                ? null
                : profile.Impact.ExplosionTrigger;
            if (trigger == null || !trigger.OnEnemyImpact || !trigger.OnWallImpact)
            {
                throw new ArgumentException(
                    "canonical-rocket-request-contact-triggers-required",
                    nameof(profile));
            }
            if (profile.Damage == null || profile.Damage.DirectDamage <= 0d)
            {
                throw new ArgumentException(
                    "canonical-rocket-request-positive-damage-required",
                    nameof(profile));
            }
            if (profile.Pierce.Tenths <= 0)
            {
                throw new ArgumentException(
                    "canonical-rocket-request-positive-pierce-required",
                    nameof(profile));
            }
            if (profile.Damage.HasAreaDamage)
            {
                throw new ArgumentException(
                    "canonical-rocket-request-independent-area-damage-rejected",
                    nameof(profile));
            }
            if (profile.Projectile.TerminationBehavior
                != GunProjectileTerminationBehavior.StopOnFirstBlockingImpact)
            {
                throw new ArgumentException(
                    "canonical-rocket-request-first-contact-termination-required",
                    nameof(profile));
            }

            return new GunExplosionResolutionRequest(
                source,
                impactPosition,
                profile.Damage,
                profile.Effects.Explosion,
                profile.Damage.DirectDamage,
                GunExplosionTargetBudgetPolicy.RocketPierce,
                profile.Pierce,
                random,
                targetSource,
                lineOfSightPolicy,
                lineOfSightResolver);
        }

        /// <summary>
        /// Additive compatibility overload. It delegates through the same EffectiveGun to
        /// ProjectileExecutionProfile projection so there is only one final-value Rocket path.
        /// </summary>
        public static GunExplosionResolutionRequest ForCanonicalRocket(
            EffectiveGun effectiveGun,
            GunEffectSourceContext source,
            GunVector2 impactPosition,
            DeterministicRandom random,
            IGunEffectTargetSource targetSource,
            GunEffectLineOfSightPolicy lineOfSightPolicy,
            IGunEffectLineOfSightResolver lineOfSightResolver)
        {
            if (effectiveGun == null)
            {
                throw new ArgumentNullException(nameof(effectiveGun));
            }
            return ForCanonicalRocket(
                ProjectileExecutionProfile.From(effectiveGun),
                source,
                impactPosition,
                random,
                targetSource,
                lineOfSightPolicy,
                lineOfSightResolver);
        }

        public GunEffectSourceContext Source { get; }
        public GunVector2 ImpactPosition { get; }
        public GunDamageSpec Damage { get; }
        public GunExplosionEffect Explosion { get; }
        public double ResolvedExplosionDamage { get; }
        public GunExplosionTargetBudgetPolicy TargetBudgetPolicy { get; }
        public PierceValue TargetPierce { get; }
        public DeterministicRandom? Random { get; }
        public IGunEffectTargetSource TargetSource { get; }
        public GunEffectLineOfSightPolicy LineOfSightPolicy { get; }
        public IGunEffectLineOfSightResolver LineOfSightResolver { get; }

        private static GunDamageSpec RequireLegacyAreaDamage(GunDamageSpec damage)
        {
            if (damage == null)
            {
                throw new ArgumentNullException(nameof(damage));
            }
            if (!damage.HasAreaDamage)
            {
                throw new ArgumentException(
                    "Transitional explosion resolution requires positive authored area damage.",
                    nameof(damage));
            }
            return damage;
        }
    }

    /// <summary>
    /// Downstream projectile-emission adapter. Canonical Rockets use only the profile retained by
    /// the emission; transitional area-damage content keeps the legacy unlimited-target request.
    /// </summary>
    public sealed class ProjectileExplosionResolutionBridge
    {
        private readonly Explosion resolver;

        public ProjectileExplosionResolutionBridge(Explosion resolver)
        {
            this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public GunExplosionResolution Resolve(
            ProjectileEffectEmission emission,
            IGunEffectTargetSource targetSource,
            GunEffectLineOfSightPolicy lineOfSightPolicy,
            IGunEffectLineOfSightResolver lineOfSightResolver)
        {
            if (emission == null)
            {
                throw new ArgumentNullException(nameof(emission));
            }
            if (emission.Kind != ProjectileEffectEmissionKind.Explosion)
            {
                throw new ArgumentException(
                    "projectile-explosion-emission-required",
                    nameof(emission));
            }
            if (emission.Effects == null || emission.Effects.Explosion == null)
            {
                throw new InvalidOperationException(
                    "projectile-explosion-emission-effect-required");
            }

            GunEffectSourceContext source = new GunEffectSourceContext(
                emission.Lifecycle.Identity.SourceIdentity,
                emission.EventOrdinal);
            GunExplosionResolutionRequest request;
            if (emission.IsCanonicalRocket)
            {
                ValidateCanonicalRocketReason(emission);
                request = GunExplosionResolutionRequest.ForCanonicalRocket(
                    emission.Profile,
                    source,
                    emission.Position,
                    emission.Lifecycle.Random,
                    targetSource,
                    lineOfSightPolicy,
                    lineOfSightResolver);
            }
            else
            {
                if (emission.Profile != null && emission.Profile.IsCanonical)
                {
                    throw new InvalidOperationException(
                        "projectile-explosion-canonical-non-rocket-request-rejected");
                }
                request = new GunExplosionResolutionRequest(
                    source,
                    emission.Position,
                    emission.Damage,
                    emission.Effects.Explosion,
                    targetSource,
                    lineOfSightPolicy,
                    lineOfSightResolver);
            }

            return resolver.Resolve(request);
        }

        private static void ValidateCanonicalRocketReason(
            ProjectileEffectEmission emission)
        {
            GunExplosionTriggerReason required;
            switch (emission.SourceContactKind)
            {
                case ProjectileContactKind.Enemy:
                    required = GunExplosionTriggerReason.EnemyImpact;
                    break;
                case ProjectileContactKind.Wall:
                    required = GunExplosionTriggerReason.WallImpact;
                    break;
                case ProjectileContactKind.RangeExpiry:
                    required = GunExplosionTriggerReason.RangeExpiry;
                    break;
                case ProjectileContactKind.ExplicitTermination:
                    required = GunExplosionTriggerReason.Termination;
                    break;
                default:
                    throw new InvalidOperationException(
                        "projectile-explosion-canonical-rocket-contact-invalid");
            }
            if ((emission.ExplosionTriggerReasons & required) == 0)
            {
                throw new InvalidOperationException(
                    "projectile-explosion-canonical-rocket-trigger-mismatch");
            }
        }
    }

    public sealed class GunExplosionResolution
    {
        private readonly ReadOnlyCollection<GunExplosionDamageDecision> decisions;

        internal GunExplosionResolution(
            IList<GunExplosionDamageDecision> decisions,
            int resolvedTargetCapacity)
        {
            this.decisions = new ReadOnlyCollection<GunExplosionDamageDecision>(
                new List<GunExplosionDamageDecision>(decisions));
            ResolvedTargetCapacity = resolvedTargetCapacity;
        }

        public IReadOnlyList<GunExplosionDamageDecision> Decisions
        {
            get { return decisions; }
        }

        public int ResolvedTargetCapacity { get; }
    }

    public sealed class Explosion
    {
        private static readonly StableId RocketPierceDecisionPurpose =
            StableId.Parse("gun.rocket-explosion-pierce");
        private static readonly StableId RocketProjectileOrdinalPurpose =
            StableId.Parse("gun.rocket-explosion-projectile-ordinal");

        public GunExplosionResolution Resolve(GunExplosionResolutionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            IReadOnlyList<GunEffectTargetSnapshot> snapshot =
                request.TargetSource.SnapshotTargets();
            if (snapshot == null)
            {
                throw new InvalidOperationException("Target source returned a null snapshot.");
            }

            List<GunEffectTargetSnapshot> candidates =
                CollectCandidates(request, snapshot);
            candidates.Sort(delegate(
                GunEffectTargetSnapshot left,
                GunEffectTargetSnapshot right)
            {
                return GunEffectResolutionMath.CompareTargets(
                    left,
                    right,
                    request.ImpactPosition);
            });

            int targetCapacity = ResolveTargetCapacity(request, candidates.Count);
            int decisionCount = Math.Min(candidates.Count, targetCapacity);
            List<GunExplosionDamageDecision> decisions =
                new List<GunExplosionDamageDecision>(decisionCount);
            for (int index = 0; index < decisionCount; index++)
            {
                GunEffectTargetSnapshot target = candidates[index];
                double distance = Math.Sqrt(GunEffectResolutionMath.DistanceSquared(
                    target.Position,
                    request.ImpactPosition));
                double normalizedDistance = Math.Min(1d, distance / request.Explosion.Radius);
                double multiplier = 1d
                    - ((1d - request.Explosion.MinimumDamageMultiplier) * normalizedDistance);
                decisions.Add(new GunExplosionDamageDecision(
                    request.Source,
                    target.Target,
                    target.Position,
                    request.Damage.Category,
                    request.ResolvedExplosionDamage * multiplier,
                    multiplier,
                    distance,
                    request.Damage.Knockback));
            }

            return new GunExplosionResolution(decisions, targetCapacity);
        }

        private static int ResolveTargetCapacity(
            GunExplosionResolutionRequest request,
            int candidateCount)
        {
            if (request.TargetBudgetPolicy == GunExplosionTargetBudgetPolicy.Unlimited)
            {
                return candidateCount;
            }

            PierceValue pierce = request.TargetPierce;
            bool fractionalGranted = false;
            if (pierce.HasFractionalAdditionalHitChance
                && candidateCount > pierce.GuaranteedHits)
            {
                DeterministicRandom stream = CreateRocketPierceDecisionStream(request);
                stream.NextChance(
                    checked((ulong)(pierce.Tenths % 10)),
                    10UL,
                    out fractionalGranted);
            }
            return GunFixedPointBudgetRules.ResolvePierceTargetCapacity(
                pierce,
                fractionalGranted);
        }

        private static DeterministicRandom CreateRocketPierceDecisionStream(
            GunExplosionResolutionRequest request)
        {
            DeterministicRandom random = request.Random.Value;
            GunEffectIdentity identity = request.Source.Identity;
            DeterministicRandom stream = DeterministicRandom.CreateSubstream(
                random.StreamSeed,
                random.AlgorithmVersion,
                RocketPierceDecisionPurpose,
                checked((ulong)identity.ShotSequence));
            stream = DeterministicRandom.CreateSubstream(
                stream.StreamSeed,
                stream.AlgorithmVersion,
                RocketProjectileOrdinalPurpose,
                checked((ulong)identity.ProjectileOrdinal.Value));
            stream = DeterministicRandom.CreateSubstream(
                stream.StreamSeed,
                stream.AlgorithmVersion,
                identity.ActorId.Value,
                checked((ulong)identity.LifecycleGeneration.Value));
            stream = DeterministicRandom.CreateSubstream(
                stream.StreamSeed,
                stream.AlgorithmVersion,
                identity.ParticipantId.Value,
                0UL);
            stream = DeterministicRandom.CreateSubstream(
                stream.StreamSeed,
                stream.AlgorithmVersion,
                identity.EquipmentInstanceId.Value,
                0UL);
            return DeterministicRandom.CreateSubstream(
                stream.StreamSeed,
                stream.AlgorithmVersion,
                identity.FireOperationId.Value,
                checked((ulong)request.Source.ImpactOrdinal));
        }

        private static List<GunEffectTargetSnapshot> CollectCandidates(
            GunExplosionResolutionRequest request,
            IReadOnlyList<GunEffectTargetSnapshot> snapshot)
        {
            double radiusSquared = request.Explosion.Radius * request.Explosion.Radius;
            HashSet<GunTargetReference> seen = new HashSet<GunTargetReference>();
            List<GunEffectTargetSnapshot> candidates =
                new List<GunEffectTargetSnapshot>();
            for (int index = 0; index < snapshot.Count; index++)
            {
                GunEffectTargetSnapshot target = snapshot[index];
                if (target == null
                    || !target.IsEligible
                    || !seen.Add(target.Target)
                    || GunEffectResolutionMath.DistanceSquared(
                        target.Position,
                        request.ImpactPosition) > radiusSquared)
                {
                    continue;
                }

                if (request.LineOfSightPolicy == GunEffectLineOfSightPolicy.Require
                    && !request.LineOfSightResolver.HasLineOfSight(
                        request.ImpactPosition,
                        target))
                {
                    continue;
                }

                candidates.Add(target);
            }
            return candidates;
        }
    }
}
