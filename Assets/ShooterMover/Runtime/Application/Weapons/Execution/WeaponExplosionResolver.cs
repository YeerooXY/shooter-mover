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
        /// Explicit compatibility projection for one canonical effective Rocket. Universal final
        /// damage becomes explosion base damage, and final Pierce becomes explosion-victim capacity.
        /// The Rocket body therefore does not need an independently authored area-damage value.
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
            if (!effectiveWeapon.UsesCanonicalAuthoredDefinition
                || effectiveWeapon.AuthoredDelivery == null
                || effectiveWeapon.AuthoredDelivery.Type != WeaponDeliveryType.Rocket)
            {
                throw new ArgumentException(
                    "Canonical Rocket explosion projection requires a canonical effective Rocket.",
                    nameof(effectiveWeapon));
            }
            if (effectiveWeapon.Effects.Explosion == null)
            {
                throw new ArgumentException(
                    "Canonical Rocket explosion projection requires an explosion effect.",
                    nameof(effectiveWeapon));
            }
            WeaponExplosionTriggerSpec trigger = effectiveWeapon.Impact.ExplosionTrigger;
            if (trigger == null || !trigger.OnEnemyImpact || !trigger.OnWallImpact)
            {
                throw new ArgumentException(
                    "Canonical Rocket explosion projection requires enemy- and wall-contact triggers.",
                    nameof(effectiveWeapon));
            }
            if (effectiveWeapon.Damage.DirectDamage <= 0d)
            {
                throw new ArgumentException(
                    "Canonical Rocket explosion projection requires positive final universal damage.",
                    nameof(effectiveWeapon));
            }
            if (effectiveWeapon.Damage.HasAreaDamage)
            {
                throw new ArgumentException(
                    "Canonical Rockets cannot carry an independently authored area-damage payload.",
                    nameof(effectiveWeapon));
            }

            return new WeaponExplosionResolutionRequest(
                source,
                impactPosition,
                effectiveWeapon.Damage,
                effectiveWeapon.Effects.Explosion,
                effectiveWeapon.Damage.DirectDamage,
                WeaponExplosionTargetBudgetPolicy.RocketPierce,
                effectiveWeapon.Pierce,
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
