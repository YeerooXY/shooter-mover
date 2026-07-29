using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    public sealed class GunChainArcResolutionRequest
    {
        private readonly ReadOnlyCollection<GunTargetReference> alreadyUsedTargets;

        private GunChainArcResolutionRequest(
            GunEffectSourceContext source,
            GunVector2 sourcePosition,
            GunTargetReference originTarget,
            GunDamageSpec damage,
            GunChainArcEffect chainArc,
            IGunEffectTargetSource targetSource,
            IEnumerable<GunTargetReference> alreadyUsedTargets,
            GunEffectLineOfSightPolicy lineOfSightPolicy,
            IGunEffectLineOfSightResolver lineOfSightResolver)
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
            GunEffectResolutionMath.ValidateLineOfSight(
                lineOfSightPolicy,
                lineOfSightResolver);
            LineOfSightPolicy = lineOfSightPolicy;
            LineOfSightResolver = lineOfSightResolver;

            List<GunTargetReference> copy = new List<GunTargetReference>();
            HashSet<GunTargetReference> seen = new HashSet<GunTargetReference>();
            if (originTarget != null && seen.Add(originTarget))
            {
                copy.Add(originTarget);
            }
            if (alreadyUsedTargets != null)
            {
                foreach (GunTargetReference target in alreadyUsedTargets)
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
                new ReadOnlyCollection<GunTargetReference>(copy);
        }

        public GunEffectSourceContext Source { get; }
        public GunVector2 SourcePosition { get; }
        public GunTargetReference OriginTarget { get; }
        public GunDamageSpec Damage { get; }
        public GunChainArcEffect ChainArc { get; }
        public IGunEffectTargetSource TargetSource { get; }
        public IReadOnlyList<GunTargetReference> AlreadyUsedTargets
        {
            get { return alreadyUsedTargets; }
        }
        public GunEffectLineOfSightPolicy LineOfSightPolicy { get; }
        public IGunEffectLineOfSightResolver LineOfSightResolver { get; }

        public static GunChainArcResolutionRequest FromPoint(
            GunEffectSourceContext source,
            GunVector2 sourcePosition,
            GunDamageSpec damage,
            GunChainArcEffect chainArc,
            IGunEffectTargetSource targetSource,
            IEnumerable<GunTargetReference> alreadyUsedTargets,
            GunEffectLineOfSightPolicy lineOfSightPolicy,
            IGunEffectLineOfSightResolver lineOfSightResolver)
        {
            return new GunChainArcResolutionRequest(
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

        public static GunChainArcResolutionRequest FromEnemyImpact(
            GunEffectSourceContext source,
            GunVector2 sourcePosition,
            GunTargetReference impactTarget,
            GunDamageSpec damage,
            GunChainArcEffect chainArc,
            IGunEffectTargetSource targetSource,
            IEnumerable<GunTargetReference> alreadyUsedTargets,
            GunEffectLineOfSightPolicy lineOfSightPolicy,
            IGunEffectLineOfSightResolver lineOfSightResolver)
        {
            if (impactTarget == null)
            {
                throw new ArgumentNullException(nameof(impactTarget));
            }

            return new GunChainArcResolutionRequest(
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

    public sealed class GunChainArcResolution
    {
        private readonly ReadOnlyCollection<GunChainArcDamageDecision> decisions;

        internal GunChainArcResolution(IList<GunChainArcDamageDecision> decisions)
        {
            this.decisions = new ReadOnlyCollection<GunChainArcDamageDecision>(
                new List<GunChainArcDamageDecision>(decisions));
        }

        public IReadOnlyList<GunChainArcDamageDecision> Decisions
        {
            get { return decisions; }
        }
    }

    public sealed class ChainLightning
    {
        public GunChainArcResolution Resolve(GunChainArcResolutionRequest request)
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

            List<GunEffectTargetSnapshot> uniqueTargets = BuildUniqueEligibleTargets(snapshot);
            HashSet<GunTargetReference> used = new HashSet<GunTargetReference>();
            for (int index = 0; index < request.AlreadyUsedTargets.Count; index++)
            {
                used.Add(request.AlreadyUsedTargets[index]);
            }

            List<GunChainArcDamageDecision> decisions =
                new List<GunChainArcDamageDecision>();
            GunVector2 currentPosition = request.SourcePosition;
            double currentDamage = request.Damage.DirectDamage;
            double rangeSquared = request.ChainArc.AcquisitionRange
                * request.ChainArc.AcquisitionRange;

            for (int jumpIndex = 0;
                jumpIndex < request.ChainArc.MaximumTargets && currentDamage > 0d;
                jumpIndex++)
            {
                GunEffectTargetSnapshot selected = SelectNextTarget(
                    request,
                    uniqueTargets,
                    used,
                    currentPosition,
                    rangeSquared);
                if (selected == null)
                {
                    break;
                }

                decisions.Add(new GunChainArcDamageDecision(
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

            return new GunChainArcResolution(decisions);
        }

        private static List<GunEffectTargetSnapshot> BuildUniqueEligibleTargets(
            IReadOnlyList<GunEffectTargetSnapshot> snapshot)
        {
            HashSet<GunTargetReference> seen = new HashSet<GunTargetReference>();
            List<GunEffectTargetSnapshot> result =
                new List<GunEffectTargetSnapshot>();
            for (int index = 0; index < snapshot.Count; index++)
            {
                GunEffectTargetSnapshot target = snapshot[index];
                if (target != null && target.IsEligible && seen.Add(target.Target))
                {
                    result.Add(target);
                }
            }
            return result;
        }

        private static GunEffectTargetSnapshot SelectNextTarget(
            GunChainArcResolutionRequest request,
            IList<GunEffectTargetSnapshot> targets,
            ISet<GunTargetReference> used,
            GunVector2 origin,
            double rangeSquared)
        {
            List<GunEffectTargetSnapshot> candidates =
                new List<GunEffectTargetSnapshot>();
            for (int index = 0; index < targets.Count; index++)
            {
                GunEffectTargetSnapshot target = targets[index];
                if (used.Contains(target.Target)
                    || GunEffectResolutionMath.DistanceSquared(
                        target.Position,
                        origin) > rangeSquared)
                {
                    continue;
                }
                if (request.LineOfSightPolicy == GunEffectLineOfSightPolicy.Require
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
                GunEffectTargetSnapshot left,
                GunEffectTargetSnapshot right)
            {
                return GunEffectResolutionMath.CompareTargets(left, right, origin);
            });
            return candidates[0];
        }
    }
}
