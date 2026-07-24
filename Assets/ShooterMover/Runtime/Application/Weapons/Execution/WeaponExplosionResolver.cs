using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public sealed class WeaponExplosionResolutionRequest
    {
        public WeaponExplosionResolutionRequest(
            WeaponEffectSourceContext source,
            WeaponVector2 impactPosition,
            WeaponDamageSpec damage,
            WeaponExplosionEffect explosion,
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
            if (!damage.HasAreaDamage)
            {
                throw new ArgumentException(
                    "Explosion resolution requires positive authored area damage.",
                    nameof(damage));
            }
            Explosion = explosion ?? throw new ArgumentNullException(nameof(explosion));
            TargetSource = targetSource ?? throw new ArgumentNullException(nameof(targetSource));
            WeaponEffectResolutionMath.ValidateLineOfSight(
                lineOfSightPolicy,
                lineOfSightResolver);
            LineOfSightPolicy = lineOfSightPolicy;
            LineOfSightResolver = lineOfSightResolver;
        }

        public WeaponEffectSourceContext Source { get; }
        public WeaponVector2 ImpactPosition { get; }
        public WeaponDamageSpec Damage { get; }
        public WeaponExplosionEffect Explosion { get; }
        public IWeaponEffectTargetSource TargetSource { get; }
        public WeaponEffectLineOfSightPolicy LineOfSightPolicy { get; }
        public IWeaponEffectLineOfSightResolver LineOfSightResolver { get; }
    }

    public sealed class WeaponExplosionResolution
    {
        private readonly ReadOnlyCollection<WeaponExplosionDamageDecision> decisions;

        internal WeaponExplosionResolution(IList<WeaponExplosionDamageDecision> decisions)
        {
            this.decisions = new ReadOnlyCollection<WeaponExplosionDamageDecision>(
                new List<WeaponExplosionDamageDecision>(decisions));
        }

        public IReadOnlyList<WeaponExplosionDamageDecision> Decisions
        {
            get { return decisions; }
        }
    }

    public sealed class WeaponExplosionResolver
    {
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

            List<WeaponExplosionDamageDecision> decisions =
                new List<WeaponExplosionDamageDecision>(candidates.Count);
            for (int index = 0; index < candidates.Count; index++)
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
                    request.Damage.AreaDamage * multiplier,
                    multiplier,
                    distance,
                    request.Damage.Knockback));
            }

            return new WeaponExplosionResolution(decisions);
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
