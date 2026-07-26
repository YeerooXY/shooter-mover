using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public enum WeaponExplosionTargetBudgetPolicy
    {
        Unlimited = 1,
        RocketPierce = 2,
    }

    public sealed class WeaponExplosionResolutionRequest
    {
        private WeaponExplosionResolutionRequest(
            WeaponEffectSourceContext source,
            WeaponVector2 impactPosition,
            WeaponDamageSpec damage,
            WeaponExplosionEffect explosion,
            double resolvedExplosionDamage,
            WeaponExplosionTargetBudgetPolicy targetBudgetPolicy,
            PierceValue targetPierce,
            DeterministicRandom? random,
            IWeaponEffectTargetSource targetSource,
            WeaponEffectLineOfSightPolicy lineOfSightPolicy,
            IWeaponEffectLineOfSightResolver lineOfSightResolver)
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
            if (!Enum.IsDefined(typeof(WeaponExplosionTargetBudgetPolicy), targetBudgetPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(targetBudgetPolicy));
            }
            if (targetBudgetPolicy == WeaponExplosionTargetBudgetPolicy.RocketPierce)
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
            WeaponEffectResolutionMath.ValidateLineOfSight(
                lineOfSightPolicy,
                lineOfSightResolver);
            LineOfSightPolicy = lineOfSightPolicy;
            LineOfSightResolver = lineOfSightResolver;
        }

        /// <summary>
        /// Transitional constructor. Existing content continues to use independently authored
        /// AreaDamage and affects every eligible target in the explosion radius.
        /// </summary>
        public WeaponExplosionResolutionRequest(
            WeaponEffectSourceContext source,
            WeaponVector2 impactPosition,
            WeaponDamageSpec damage,
            WeaponExplosionEffect explosion,
            IWeaponEffectTargetSource targetSource,
            WeaponEffectLineOfSightPolicy lineOfSightPolicy,
            IWeaponEffectLineOfSightResolver lineOfSightResolver)
            : this(
                source,
                impactPosition,
                RequireLegacyAreaDamage(damage),
                explosion,
                damage.AreaDamage,
                WeaponExplosionTargetBudgetPolicy.Unlimited,
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
        public static WeaponExplosionResolutionRequest ForCanonicalRocket(
            ProjectileExecutionProfile profile,
            WeaponEffectSourceContext source,
            WeaponVector2 impactPosition,
            DeterministicRandom random,
            IWeaponEffectTargetSource targetSource,
            WeaponEffectLineOfSightPolicy lineOfSightPolicy,
            IWeaponEffectLineOfSightResolver lineOfSightResolver)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }
            if (!profile.IsCanonicalRocket
                || profile.CanonicalDeliveryType != WeaponDeliveryType.Rocket
                || profile.Projectile == null
                || profile.Projectile.Kind != WeaponProjectileKind.Rocket)
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
            WeaponExplosionTriggerSpec trigger = profile.Impact == null
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
                != WeaponProjectileTerminationBehavior.StopOnFirstBlockingImpact)
            {
                throw new ArgumentException(
                    "canonical-rocket-request-first-contact-termination-required",
                    nameof(profile));
            }

            return new WeaponExplosionResolutionRequest(
                source,
                impactPosition,
                profile.Damage,
                profile.Effects.Explosion,
                profile.Damage.DirectDamage,
                WeaponExplosionTargetBudgetPolicy.RocketPierce,
                profile.Pierce,
                random,
                targetSource,
                lineOfSightPolicy,
                lineOfSightResolver);
        }

        /// <summary>
        /// Additive compatibility overload. It delegates through the same EffectiveWeapon to
        /// ProjectileExecutionProfile projection so there is only one final-value Rocket path.
        /// </summary>
        public static WeaponExplosionResolutionRequest ForCanonicalRocket(
            EffectiveWeapon effectiveWeapon,
            WeaponEffectSourceContext source,
            WeaponVector2 impactPosition,
            DeterministicRandom random,
            IWeaponEffectTargetSource targetSource,
            WeaponEffectLineOfSightPolicy lineOfSightPolicy,
            IWeaponEffectLineOfSightResolver lineOfSightResolver)
        {
            if (effectiveWeapon == null)
            {
                throw new ArgumentNullException(nameof(effectiveWeapon));
            }
            return ForCanonicalRocket(
                ProjectileExecutionProfile.From(effectiveWeapon),
                source,
                impactPosition,
                random,
                targetSource,
                lineOfSightPolicy,
                lineOfSightResolver);
        }

        public WeaponEffectSourceContext Source { get; }
        public WeaponVector2 ImpactPosition { get; }
        public WeaponDamageSpec Damage { get; }
        public WeaponExplosionEffect Explosion { get; }
        public double ResolvedExplosionDamage { get; }
        public WeaponExplosionTargetBudgetPolicy TargetBudgetPolicy { get; }
        public PierceValue TargetPierce { get; }
        public DeterministicRandom? Random { get; }
        public IWeaponEffectTargetSource TargetSource { get; }
        public WeaponEffectLineOfSightPolicy LineOfSightPolicy { get; }
        public IWeaponEffectLineOfSightResolver LineOfSightResolver { get; }

        private static WeaponDamageSpec RequireLegacyAreaDamage(WeaponDamageSpec damage)
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
    public sealed class ProjectileExplosionResolutionAdapter
    {
        private readonly WeaponExplosionResolver resolver;

        public ProjectileExplosionResolutionAdapter(WeaponExplosionResolver resolver)
        {
            this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public WeaponExplosionResolution Resolve(
            ProjectileEffectEmission emission,
            IWeaponEffectTargetSource targetSource,
            WeaponEffectLineOfSightPolicy lineOfSightPolicy,
            IWeaponEffectLineOfSightResolver lineOfSightResolver)
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

            WeaponEffectSourceContext source = new WeaponEffectSourceContext(
                emission.Lifecycle.Identity.SourceIdentity,
                emission.EventOrdinal);
            WeaponExplosionResolutionRequest request;
            if (emission.IsCanonicalRocket)
            {
                ValidateCanonicalRocketReason(emission);
                request = WeaponExplosionResolutionRequest.ForCanonicalRocket(
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
                request = new WeaponExplosionResolutionRequest(
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
            WeaponExplosionTriggerReason required;
            switch (emission.SourceContactKind)
            {
                case ProjectileContactKind.Enemy:
                    required = WeaponExplosionTriggerReason.EnemyImpact;
                    break;
                case ProjectileContactKind.Wall:
                    required = WeaponExplosionTriggerReason.WallImpact;
                    break;
                case ProjectileContactKind.RangeExpiry:
                    required = WeaponExplosionTriggerReason.RangeExpiry;
                    break;
                case ProjectileContactKind.ExplicitTermination:
                    required = WeaponExplosionTriggerReason.Termination;
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

    public sealed class WeaponExplosionResolution
    {
        private readonly ReadOnlyCollection<WeaponExplosionDamageDecision> decisions;

        internal WeaponExplosionResolution(
            IList<WeaponExplosionDamageDecision> decisions,
            int resolvedTargetCapacity)
        {
            this.decisions = new ReadOnlyCollection<WeaponExplosionDamageDecision>(
                new List<WeaponExplosionDamageDecision>(decisions));
            ResolvedTargetCapacity = resolvedTargetCapacity;
        }

        public IReadOnlyList<WeaponExplosionDamageDecision> Decisions
        {
            get { return decisions; }
        }

        public int ResolvedTargetCapacity { get; }
    }

    public sealed class WeaponExplosionResolver
    {
        private static readonly StableId RocketPierceDecisionPurpose =
            StableId.Parse("weapon.rocket-explosion-pierce");
        private static readonly StableId RocketProjectileOrdinalPurpose =
            StableId.Parse("weapon.rocket-explosion-projectile-ordinal");

        public WeaponExplosionResolution Resolve(WeaponExplosionResolutionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            IReadOnlyList<WeaponEffectTargetSnapshot> snapshot =
                request.TargetSource.SnapshotTargets();
            if (snapshot == null)
            {
                throw new InvalidOperationException("Target source returned a null snapshot.");
            }

            List<WeaponEffectTargetSnapshot> candidates =
                CollectCandidates(request, snapshot);
            candidates.Sort(delegate(
                WeaponEffectTargetSnapshot left,
                WeaponEffectTargetSnapshot right)
            {
                return WeaponEffectResolutionMath.CompareTargets(
                    left,
                    right,
                    request.ImpactPosition);
            });

            int targetCapacity = ResolveTargetCapacity(request, candidates.Count);
            int decisionCount = Math.Min(candidates.Count, targetCapacity);
            List<WeaponExplosionDamageDecision> decisions =
                new List<WeaponExplosionDamageDecision>(decisionCount);
            for (int index = 0; index < decisionCount; index++)
            {
                WeaponEffectTargetSnapshot target = candidates[index];
                double distance = Math.Sqrt(WeaponEffectResolutionMath.DistanceSquared(
                    target.Position,
                    request.ImpactPosition));
                double normalizedDistance = Math.Min(1d, distance / request.Explosion.Radius);
                double multiplier = 1d
                    - ((1d - request.Explosion.MinimumDamageMultiplier) * normalizedDistance);
                decisions.Add(new WeaponExplosionDamageDecision(
                    request.Source,
                    target.Target,
                    target.Position,
                    request.Damage.Category,
                    request.ResolvedExplosionDamage * multiplier,
                    multiplier,
                    distance,
                    request.Damage.Knockback));
            }

            return new WeaponExplosionResolution(decisions, targetCapacity);
        }

        private static int ResolveTargetCapacity(
            WeaponExplosionResolutionRequest request,
            int candidateCount)
        {
            if (request.TargetBudgetPolicy == WeaponExplosionTargetBudgetPolicy.Unlimited)
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
            return WeaponFixedPointBudgetRules.ResolvePierceTargetCapacity(
                pierce,
                fractionalGranted);
        }

        private static DeterministicRandom CreateRocketPierceDecisionStream(
            WeaponExplosionResolutionRequest request)
        {
            DeterministicRandom random = request.Random.Value;
            WeaponEffectIdentity identity = request.Source.Identity;
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

        private static List<WeaponEffectTargetSnapshot> CollectCandidates(
            WeaponExplosionResolutionRequest request,
            IReadOnlyList<WeaponEffectTargetSnapshot> snapshot)
        {
            double radiusSquared = request.Explosion.Radius * request.Explosion.Radius;
            HashSet<WeaponTargetReference> seen = new HashSet<WeaponTargetReference>();
            List<WeaponEffectTargetSnapshot> candidates =
                new List<WeaponEffectTargetSnapshot>();
            for (int index = 0; index < snapshot.Count; index++)
            {
                WeaponEffectTargetSnapshot target = snapshot[index];
                if (target == null
                    || !target.IsEligible
                    || !seen.Add(target.Target)
                    || WeaponEffectResolutionMath.DistanceSquared(
                        target.Position,
                        request.ImpactPosition) > radiusSquared)
                {
                    continue;
                }

                if (request.LineOfSightPolicy == WeaponEffectLineOfSightPolicy.Require
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
