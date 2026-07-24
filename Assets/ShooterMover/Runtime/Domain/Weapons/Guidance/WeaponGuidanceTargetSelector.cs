using System;
using System.Collections.Generic;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Domain.Weapons.Guidance
{
    internal static class WeaponGuidanceTargetSelector
    {
        public static IReadOnlyList<WeaponGuidanceTargetSnapshot> Freeze(
            IWeaponGuidanceTargetSnapshotSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            IReadOnlyList<WeaponGuidanceTargetSnapshot> snapshots =
                source.GetTargetSnapshots();
            if (snapshots == null)
            {
                throw new InvalidOperationException(
                    "Weapon guidance target sources cannot return a null snapshot list.");
            }

            List<WeaponGuidanceTargetSnapshot> copy =
                new List<WeaponGuidanceTargetSnapshot>(snapshots.Count);
            HashSet<WeaponGuidanceTargetReference> identities =
                new HashSet<WeaponGuidanceTargetReference>();

            for (int index = 0; index < snapshots.Count; index++)
            {
                WeaponGuidanceTargetSnapshot snapshot = snapshots[index];
                if (snapshot == null)
                {
                    throw new InvalidOperationException(
                        "Weapon guidance target snapshots cannot contain null entries.");
                }
                if (!identities.Add(snapshot.Target))
                {
                    throw new InvalidOperationException(
                        "Weapon guidance target snapshots contain duplicate identity "
                        + snapshot.Target
                        + ".");
                }
                copy.Add(snapshot);
            }

            return copy.AsReadOnly();
        }

        public static bool TryResolveExact(
            IReadOnlyList<WeaponGuidanceTargetSnapshot> snapshots,
            WeaponGuidanceTargetReference target,
            WeaponVector2 projectilePosition,
            double acquisitionRangeSquared,
            out WeaponGuidanceTargetSnapshot resolved)
        {
            if (target == null)
            {
                resolved = null;
                return false;
            }

            for (int index = 0; index < snapshots.Count; index++)
            {
                WeaponGuidanceTargetSnapshot candidate = snapshots[index];
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
            IReadOnlyList<WeaponGuidanceTargetSnapshot> snapshots,
            WeaponTargetPolicy policy,
            WeaponGuidanceTargetReference currentTarget,
            WeaponVector2 projectilePosition,
            WeaponVector2 acquisitionAimDirection,
            double acquisitionRangeSquared,
            out WeaponGuidanceTargetSnapshot selected)
        {
            if (policy == WeaponTargetPolicy.CurrentLockedTarget)
            {
                return TryResolveExact(
                    snapshots,
                    currentTarget,
                    projectilePosition,
                    acquisitionRangeSquared,
                    out selected);
            }

            WeaponGuidanceTargetSnapshot best = null;
            double bestDistanceSquared = 0d;
            double bestAlignment = 0d;

            for (int index = 0; index < snapshots.Count; index++)
            {
                WeaponGuidanceTargetSnapshot candidate = snapshots[index];
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
                if (policy == WeaponTargetPolicy.ClosestToAim)
                {
                    WeaponVector2 offset = WeaponGuidanceGeometry.Difference(
                        candidate.Position,
                        projectilePosition);
                    alignment = WeaponGuidanceGeometry.Alignment(
                        acquisitionAimDirection,
                        offset);
                }
                else if (policy != WeaponTargetPolicy.NearestInRange)
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
            WeaponGuidanceTargetSnapshot candidate,
            WeaponVector2 projectilePosition,
            double acquisitionRangeSquared,
            out double distanceSquared)
        {
            distanceSquared = WeaponGuidanceGeometry.DistanceSquared(
                candidate.Position,
                projectilePosition);
            return candidate.IsTargetable
                && distanceSquared > 0d
                && distanceSquared <= acquisitionRangeSquared;
        }

        private static bool IsBetter(
            WeaponTargetPolicy policy,
            WeaponGuidanceTargetSnapshot candidate,
            double candidateDistanceSquared,
            double candidateAlignment,
            WeaponGuidanceTargetSnapshot current,
            double currentDistanceSquared,
            double currentAlignment)
        {
            if (policy == WeaponTargetPolicy.ClosestToAim)
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
