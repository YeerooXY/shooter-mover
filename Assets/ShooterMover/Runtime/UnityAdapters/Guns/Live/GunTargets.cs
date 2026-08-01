using System;
using System.Collections.Generic;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.Domain.Guns.Guidance;
using ShooterMover.UnityAdapters.Combat;
using RoomEnemy = ShooterMover.UnityAdapters.Missions.Rooms.Enemy;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    /// <summary>
    /// Scene-owned projection of exact live targets. Homing sees enemies only;
    /// area effects may also affect other Damageable gameplay objects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GunTargets : MonoBehaviour,
        IGunGuidanceTargetSnapshotSource,
        IGunEffectTargetSource
    {
        private Transform owner;

        public void Configure(Transform sourceOwner)
        {
            if (sourceOwner == null)
            {
                throw new ArgumentNullException(nameof(sourceOwner));
            }
            if (owner != null && !ReferenceEquals(owner, sourceOwner))
            {
                throw new InvalidOperationException(
                    "Live gun targets are already bound to another owner.");
            }
            owner = sourceOwner;
        }

        public IReadOnlyList<GunGuidanceTargetSnapshot> GetTargetSnapshots()
        {
            List<Damageable> values = FindTargets(true);
            var snapshots = new List<GunGuidanceTargetSnapshot>(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                Damageable target = values[index];
                snapshots.Add(new GunGuidanceTargetSnapshot(
                    ToReference(target),
                    ToPosition(target.transform.position),
                    target.CanTakeDamage));
            }
            snapshots.Sort(delegate(
                GunGuidanceTargetSnapshot left,
                GunGuidanceTargetSnapshot right)
            {
                return left.Target.CompareTo(right.Target);
            });
            return snapshots;
        }

        public IReadOnlyList<GunEffectTargetSnapshot> SnapshotTargets()
        {
            List<Damageable> values = FindTargets(false);
            var snapshots = new List<GunEffectTargetSnapshot>(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                Damageable target = values[index];
                snapshots.Add(new GunEffectTargetSnapshot(
                    ToReference(target),
                    ToPosition(target.transform.position),
                    target.CanTakeDamage));
            }
            snapshots.Sort(delegate(
                GunEffectTargetSnapshot left,
                GunEffectTargetSnapshot right)
            {
                return left.Target.CompareTo(right.Target);
            });
            return snapshots;
        }

        public bool TryResolve(
            GunTargetReference reference,
            out Damageable target)
        {
            target = null;
            if (reference == null) return false;

            List<Damageable> values = FindTargets(false);
            for (int index = 0; index < values.Count; index++)
            {
                Damageable candidate = values[index];
                if (candidate.DamageableStableId
                        == reference.ActorId.Value
                    && candidate.DamageableLifecycleGeneration
                        == reference.LifecycleGeneration.Value)
                {
                    target = candidate;
                    return true;
                }
            }
            return false;
        }

        private List<Damageable> FindTargets(bool enemiesOnly)
        {
            var result = new List<Damageable>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            {
                return result;
            }

            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Damageable[] values = roots[rootIndex]
                    .GetComponentsInChildren<Damageable>(true);
                for (int index = 0; index < values.Length; index++)
                {
                    Damageable candidate = values[index];
                    if (!IsUsable(candidate, enemiesOnly)) continue;
                    string key = candidate.DamageableStableId
                        + "|" + candidate.DamageableLifecycleGeneration;
                    if (seen.Add(key)) result.Add(candidate);
                }
            }
            return result;
        }

        private bool IsUsable(Damageable candidate, bool enemiesOnly)
        {
            if (candidate == null
                || !candidate.isActiveAndEnabled
                || !candidate.gameObject.activeInHierarchy
                || !candidate.CanTakeDamage
                || candidate.DamageableStableId == null
                || candidate.DamageableLifecycleGeneration <= 0L
                || IsOwner(candidate.transform))
            {
                return false;
            }
            return !enemiesOnly || candidate is RoomEnemy;
        }

        private bool IsOwner(Transform value)
        {
            return owner != null
                && (value == owner || value.IsChildOf(owner));
        }

        private static GunTargetReference ToReference(Damageable target)
        {
            return new GunTargetReference(
                new GunActorInstanceId(target.DamageableStableId),
                new LifecycleGeneration(
                    target.DamageableLifecycleGeneration));
        }

        private static GunVector2 ToPosition(Vector3 value)
        {
            return new GunVector2(value.x, value.y);
        }
    }
}
