using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public sealed class WeaponChainArcResolutionRequest
    {
        private readonly ReadOnlyCollection<WeaponTargetReference> alreadyUsedTargets;

        private WeaponChainArcResolutionRequest(
            WeaponEffectSourceContext source,
            WeaponVector2 sourcePosition,
            WeaponTargetReference originTarget,
            WeaponDamageSpec damage,
            WeaponChainArcEffect chainArc,
            IWeaponEffectTargetSource targetSource,
            IEnumerable<WeaponTargetReference> alreadyUsedTargets,
            WeaponEffectLineOfSightPolicy lineOfSightPolicy,
            IWeaponEffectLineOfSightResolver lineOfSightResolver)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            SourcePosition = sourcePosition ?? throw new ArgumentNullException(nameof(sourcePosition));
            if (!sourcePosition.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(sourcePosition));
            }
            OriginTarget = originTarget;
            Damage = damage ?? throw new ArgumentNullException(nameof(damage));
            if (damage.DirectDamage <= 0d)
            {
                throw new ArgumentException(
                    "Chain resolution requires positive authored direct damage.",
                    nameof(damage));
            }
            ChainArc = chainArc ?? throw new ArgumentNullException(nameof(chainArc));
            TargetSource = targetSource ?? throw new ArgumentNullException(nameof(targetSource));
            WeaponEffectResolutionMath.ValidateLineOfSight(
                lineOfSightPolicy,
                lineOfSightResolver);
            LineOfSightPolicy = lineOfSightPolicy;
            LineOfSightResolver = lineOfSightResolver;

            List<WeaponTargetReference> copy = new List<WeaponTargetReference>();
            HashSet<WeaponTargetReference> seen = new HashSet<WeaponTargetReference>();
            if (originTarget != null && seen.Add(originTarget))
            {
                copy.Add(originTarget);
            }
            if (alreadyUsedTargets != null)
            {
                foreach (WeaponTargetReference target in alreadyUsedTargets)
                {
                    if (target == null)
                    {
                        throw new ArgumentException(
                            "Already-used targets cannot contain null values.",
                            nameof(alreadyUsedTargets));
                    }
                    if (seen.Add(target))
                    {
                        copy.Add(target);
                    }
                }
            }
            this.alreadyUsedTargets =
                new ReadOnlyCollection<WeaponTargetReference>(copy);
        }

        public WeaponEffectSourceContext Source { get; }
        public WeaponVector2 SourcePosition { get; }
        public WeaponTargetReference OriginTarget { get; }
        public WeaponDamageSpec Damage { get; }
        public WeaponChainArcEffect ChainArc { get; }
        public IWeaponEffectTargetSource TargetSource { get; }
        public IReadOnlyList<WeaponTargetReference> AlreadyUsedTargets
        {
            get { return alreadyUsedTargets; }
        }
        public WeaponEffectLineOfSightPolicy LineOfSightPolicy { get; }
        public IWeaponEffectLineOfSightResolver LineOfSightResolver { get; }

        public static WeaponChainArcResolutionRequest FromPoint(
            WeaponEffectSourceContext source,
            WeaponVector2 sourcePosition,
            WeaponDamageSpec damage,
            WeaponChainArcEffect chainArc,
            IWeaponEffectTargetSource targetSource,
            IEnumerable<WeaponTargetReference> alreadyUsedTargets,
            WeaponEffectLineOfSightPolicy lineOfSightPolicy,
            IWeaponEffectLineOfSightResolver lineOfSightResolver)
        {
            return new WeaponChainArcResolutionRequest(
                source,
                sourcePosition,
                null,
                damage,
                chainArc,
                targetSource,
                alreadyUsedTargets,
                lineOfSightPolicy,
                lineOfSightResolver);
        }

        public static WeaponChainArcResolutionRequest FromEnemyImpact(
            WeaponEffectSourceContext source,
            WeaponVector2 sourcePosition,
            WeaponTargetReference impactTarget,
            WeaponDamageSpec damage,
            WeaponChainArcEffect chainArc,
            IWeaponEffectTargetSource targetSource,
            IEnumerable<WeaponTargetReference> alreadyUsedTargets,
            WeaponEffectLineOfSightPolicy lineOfSightPolicy,
            IWeaponEffectLineOfSightResolver lineOfSightResolver)
        {
            if (impactTarget == null)
            {
                throw new ArgumentNullException(nameof(impactTarget));
            }

            return new WeaponChainArcResolutionRequest(
                source,
                sourcePosition,
                impactTarget,
                damage,
                chainArc,
                targetSource,
                alreadyUsedTargets,
                lineOfSightPolicy,
                lineOfSightResolver);
        }
    }

    public sealed class WeaponChainArcResolution
    {
        private readonly ReadOnlyCollection<WeaponChainArcDamageDecision> decisions;

        internal WeaponChainArcResolution(IList<WeaponChainArcDamageDecision> decisions)
        {
            this.decisions = new ReadOnlyCollection<WeaponChainArcDamageDecision>(
                new List<WeaponChainArcDamageDecision>(decisions));
        }

        public IReadOnlyList<WeaponChainArcDamageDecision> Decisions
        {
            get { return decisions; }
        }
    }

    public sealed class WeaponChainArcResolver
    {
        public WeaponChainArcResolution Resolve(WeaponChainArcResolutionRequest request)
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

            List<WeaponEffectTargetSnapshot> uniqueTargets = BuildUniqueEligibleTargets(snapshot);
            HashSet<WeaponTargetReference> used = new HashSet<WeaponTargetReference>();
            for (int index = 0; index < request.AlreadyUsedTargets.Count; index++)
            {
                used.Add(request.AlreadyUsedTargets[index]);
            }

            List<WeaponChainArcDamageDecision> decisions =
                new List<WeaponChainArcDamageDecision>();
            WeaponVector2 currentPosition = request.SourcePosition;
            double currentDamage = request.Damage.DirectDamage;
            double rangeSquared = request.ChainArc.AcquisitionRange
                * request.ChainArc.AcquisitionRange;

            for (int jumpIndex = 0;
                jumpIndex < request.ChainArc.MaximumTargets && currentDamage > 0d;
                jumpIndex++)
            {
                WeaponEffectTargetSnapshot selected = SelectNextTarget(
                    request,
                    uniqueTargets,
                    used,
                    currentPosition,
                    rangeSquared);
                if (selected == null)
                {
                    break;
                }

                decisions.Add(new WeaponChainArcDamageDecision(
                    request.Source,
                    selected.Target,
                    currentPosition,
                    selected.Position,
                    jumpIndex,
                    request.Damage.Category,
                    currentDamage,
                    request.Damage.Knockback));
                used.Add(selected.Target);
                currentPosition = selected.Position;
                currentDamage *= request.ChainArc.RetainedDamagePerJump;
            }

            return new WeaponChainArcResolution(decisions);
        }

        private static List<WeaponEffectTargetSnapshot> BuildUniqueEligibleTargets(
            IReadOnlyList<WeaponEffectTargetSnapshot> snapshot)
        {
            HashSet<WeaponTargetReference> seen = new HashSet<WeaponTargetReference>();
            List<WeaponEffectTargetSnapshot> result =
                new List<WeaponEffectTargetSnapshot>();
            for (int index = 0; index < snapshot.Count; index++)
            {
                WeaponEffectTargetSnapshot target = snapshot[index];
                if (target != null && target.IsEligible && seen.Add(target.Target))
                {
                    result.Add(target);
                }
            }
            return result;
        }

        private static WeaponEffectTargetSnapshot SelectNextTarget(
            WeaponChainArcResolutionRequest request,
            IList<WeaponEffectTargetSnapshot> targets,
            ISet<WeaponTargetReference> used,
            WeaponVector2 origin,
            double rangeSquared)
        {
            List<WeaponEffectTargetSnapshot> candidates =
                new List<WeaponEffectTargetSnapshot>();
            for (int index = 0; index < targets.Count; index++)
            {
                WeaponEffectTargetSnapshot target = targets[index];
                if (used.Contains(target.Target)
                    || WeaponEffectResolutionMath.DistanceSquared(
                        target.Position,
                        origin) > rangeSquared)
                {
                    continue;
                }
                if (request.LineOfSightPolicy == WeaponEffectLineOfSightPolicy.Require
                    && !request.LineOfSightResolver.HasLineOfSight(origin, target))
                {
                    continue;
                }
                candidates.Add(target);
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            candidates.Sort(delegate(
                WeaponEffectTargetSnapshot left,
                WeaponEffectTargetSnapshot right)
            {
                return WeaponEffectResolutionMath.CompareTargets(left, right, origin);
            });
            return candidates[0];
        }
    }
}
