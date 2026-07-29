using System;
using System.Collections.Generic;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Domain.Guns.Guidance
{
    internal static class GunGuidanceTargetSelector
    {
        public static IReadOnlyList<GunGuidanceTargetSnapshot> Freeze(
            IGunGuidanceTargetSnapshotSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            IReadOnlyList<GunGuidanceTargetSnapshot> snapshots =
                source.GetTargetSnapshots();
            if (snapshots == null)
            {
                throw new InvalidOperationException(
                    "Gun guidance target sources cannot return a null snapshot list.");
            }

            List<GunGuidanceTargetSnapshot> copy =
                new List<GunGuidanceTargetSnapshot>(snapshots.Count);
            HashSet<GunTargetReference> identities =
                new HashSet<GunTargetReference>();

            for (int index = 0; index < snapshots.Count; index++)
            {
                GunGuidanceTargetSnapshot snapshot = snapshots[index];
                if (snapshot == null)
                {
                    throw new InvalidOperationException(
                        "Gun guidance target snapshots cannot contain null entries.");
                }
                if (!identities.Add(snapshot.Target))
                {
                    throw new InvalidOperationException(
                        "Gun guidance target snapshots contain duplicate identity "
                        + snapshot.Target
                        + ".");
                }
                copy.Add(snapshot);
            }

            return copy.AsReadOnly();
        }

        public static bool TryResolveExact(
            IReadOnlyList<GunGuidanceTargetSnapshot> snapshots,
            GunTargetReference target,
            GunVector2 projectilePosition,
            double acquisitionRangeSquared,
            out GunGuidanceTargetSnapshot resolved)
        {
            if (target == null)
            {
                resolved = null;
                return false;
            }

            for (int index = 0; index < snapshots.Count; index++)
            {
                GunGuidanceTargetSnapshot candidate = snapshots[index];
                double ignoredDistanceSquared;
                if (candidate.Target.Equals(target)
                    && IsEligible(
                        candidate,
                        projectilePosition,
                        acquisitionRangeSquared,
                        out ignoredDistanceSquared))
                {
                    resolved = candidate;
                    return true;
                }
            }

            resolved = null;
            return false;
        }

        public static bool TrySelect(
            IReadOnlyList<GunGuidanceTargetSnapshot> snapshots,
            GunTargetPolicy policy,
            GunTargetReference currentTarget,
            GunVector2 projectilePosition,
            GunVector2 acquisitionAimDirection,
            double acquisitionRangeSquared,
            out GunGuidanceTargetSnapshot selected)
        {
            if (policy == GunTargetPolicy.CurrentLockedTarget)
            {
                return TryResolveExact(
                    snapshots,
                    currentTarget,
                    projectilePosition,
                    acquisitionRangeSquared,
                    out selected);
            }

            GunGuidanceTargetSnapshot best = null;
            double bestDistanceSquared = 0d;
            double bestAlignment = 0d;

            for (int index = 0; index < snapshots.Count; index++)
            {
                GunGuidanceTargetSnapshot candidate = snapshots[index];
                double distanceSquared;
                if (!IsEligible(
                        candidate,
                        projectilePosition,
                        acquisitionRangeSquared,
                        out distanceSquared))
                {
                    continue;
                }

                double alignment = 0d;
                if (policy == GunTargetPolicy.ClosestToAim)
                {
                    GunVector2 offset = GunGuidanceGeometry.Difference(
                        candidate.Position,
                        projectilePosition);
                    alignment = GunGuidanceGeometry.Alignment(
                        acquisitionAimDirection,
                        offset);
                }
                else if (policy != GunTargetPolicy.NearestInRange)
                {
                    throw new ArgumentOutOfRangeException(nameof(policy));
                }

                if (best == null
                    || IsBetter(
                        policy,
                        candidate,
                        distanceSquared,
                        alignment,
                        best,
                        bestDistanceSquared,
                        bestAlignment))
                {
                    best = candidate;
                    bestDistanceSquared = distanceSquared;
                    bestAlignment = alignment;
                }
            }

            selected = best;
            return selected != null;
        }

        private static bool IsEligible(
            GunGuidanceTargetSnapshot candidate,
            GunVector2 projectilePosition,
            double acquisitionRangeSquared,
            out double distanceSquared)
        {
            distanceSquared = GunGuidanceGeometry.DistanceSquared(
                candidate.Position,
                projectilePosition);
            return candidate.IsTargetable
                && distanceSquared > 0d
                && distanceSquared <= acquisitionRangeSquared;
        }

        private static bool IsBetter(
            GunTargetPolicy policy,
            GunGuidanceTargetSnapshot candidate,
            double candidateDistanceSquared,
            double candidateAlignment,
            GunGuidanceTargetSnapshot current,
            double currentDistanceSquared,
            double currentAlignment)
        {
            if (policy == GunTargetPolicy.ClosestToAim)
            {
                int alignmentComparison = candidateAlignment.CompareTo(currentAlignment);
                if (alignmentComparison != 0)
                {
                    return alignmentComparison > 0;
                }
            }

            int distanceComparison = candidateDistanceSquared.CompareTo(currentDistanceSquared);
            if (distanceComparison != 0)
            {
                return distanceComparison < 0;
            }

            return candidate.Target.CompareTo(current.Target) < 0;
        }
    }
}
