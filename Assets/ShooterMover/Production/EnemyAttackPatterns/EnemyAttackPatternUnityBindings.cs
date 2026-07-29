using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Combat.HitPolicy;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    public interface IEnemyAttackPatternProjectilePrefabResolver
    {
        bool TryResolve(
            StableId projectileProfileStableId,
            out BoundedProjectile2D projectilePrefab);
    }

    public sealed class EnemyAttackPatternProjectilePrefabRegistry :
        IEnemyAttackPatternProjectilePrefabResolver
    {
        private readonly Dictionary<StableId, BoundedProjectile2D> prefabs =
            new Dictionary<StableId, BoundedProjectile2D>();

        public void Register(
            StableId projectileProfileStableId,
            BoundedProjectile2D projectilePrefab)
        {
            if (projectileProfileStableId == null)
            {
                throw new ArgumentNullException(nameof(projectileProfileStableId));
            }
            if (projectilePrefab == null)
            {
                throw new ArgumentNullException(nameof(projectilePrefab));
            }
            BoundedProjectile2D existing;
            if (prefabs.TryGetValue(projectileProfileStableId, out existing))
            {
                if (existing != projectilePrefab)
                {
                    throw new InvalidOperationException(
                        "A projectile profile cannot resolve to two prefabs: "
                        + projectileProfileStableId);
                }
                return;
            }
            prefabs.Add(projectileProfileStableId, projectilePrefab);
        }

        public bool TryResolve(
            StableId projectileProfileStableId,
            out BoundedProjectile2D projectilePrefab)
        {
            projectilePrefab = null;
            return projectileProfileStableId != null
                && prefabs.TryGetValue(
                    projectileProfileStableId,
                    out projectilePrefab)
                && projectilePrefab != null;
        }
    }

    public sealed class EnemyAttackPatternTargetBinding
    {
        private readonly ReadOnlyCollection<Collider2D> colliders;
        private readonly Func<long> lifecycleGenerationExporter;
        private readonly Func<bool> activeExporter;

        public EnemyAttackPatternTargetBinding(
            StableId targetEntityStableId,
            IEnumerable<Collider2D> targetColliders,
            Func<long> lifecycleGenerationExporter,
            Func<bool> activeExporter)
        {
            TargetEntityStableId = targetEntityStableId
                ?? throw new ArgumentNullException(nameof(targetEntityStableId));
            this.lifecycleGenerationExporter = lifecycleGenerationExporter
                ?? throw new ArgumentNullException(
                    nameof(lifecycleGenerationExporter));
            this.activeExporter = activeExporter
                ?? throw new ArgumentNullException(nameof(activeExporter));
            var copy = new List<Collider2D>();
            var instanceIds = new HashSet<int>();
            if (targetColliders == null)
            {
                throw new ArgumentNullException(nameof(targetColliders));
            }
            foreach (Collider2D collider in targetColliders)
            {
                if (collider == null
                    || !instanceIds.Add(collider.GetInstanceID()))
                {
                    continue;
                }
                copy.Add(collider);
            }
            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "At least one explicit target collider is required.",
                    nameof(targetColliders));
            }
            copy.Sort((left, right) =>
                left.GetInstanceID().CompareTo(right.GetInstanceID()));
            colliders = new ReadOnlyCollection<Collider2D>(copy);
        }

        public StableId TargetEntityStableId { get; }
        public IReadOnlyList<Collider2D> Colliders
        {
            get { return colliders; }
        }
        public long LifecycleGeneration
        {
            get { return lifecycleGenerationExporter(); }
        }
        public bool IsActive
        {
            get { return activeExporter(); }
        }

        public bool Contains(Collider2D candidate)
        {
            if (candidate == null)
            {
                return false;
            }
            for (int index = 0; index < colliders.Count; index++)
            {
                if (colliders[index] == candidate)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public interface IEnemyAttackPatternPounceMotion
    {
        void Open(
            EnemyAttackEffectEmission emission,
            Vector2 committedOrigin,
            Vector2 committedDirection,
            float lungeDistance);

        void Tick(
            EnemyAttackEffectEmission emission,
            double authoritativeTimeSeconds);

        void Close(
            EnemyAttackEffectEmission emission,
            bool cancelled);
    }

    public sealed class EnemyAttackPatternUnitySourceBinding
    {
        private readonly ReadOnlyCollection<Collider2D> ownerColliders;
        private readonly ReadOnlyCollection<EnemyAttackPatternTargetBinding> targets;
        private readonly Func<long> lifecycleGenerationExporter;
        private readonly Func<bool> activeExporter;

        public EnemyAttackPatternUnitySourceBinding(
            StableId sourceEntityStableId,
            StableId sourceRunParticipantStableId,
            StableId factionStableId,
            GameObject sourceRoot,
            IEnumerable<Collider2D> sourceOwnerColliders,
            IEnumerable<EnemyAttackPatternTargetBinding> targetBindings,
            IEnemyAttackPatternProjectilePrefabResolver projectilePrefabs,
            Func<long> lifecycleGenerationExporter,
            Func<bool> activeExporter,
            IEnemyAttackPatternPounceMotion pounceMotion = null)
            : this(
                sourceEntityStableId,
                sourceRunParticipantStableId,
                factionStableId,
                null,
                null,
                null,
                1,
                sourceRoot,
                sourceOwnerColliders,
                targetBindings,
                projectilePrefabs,
                lifecycleGenerationExporter,
                activeExporter,
                pounceMotion)
        {
        }

        public EnemyAttackPatternUnitySourceBinding(
            StableId sourceEntityStableId,
            StableId sourceRunParticipantStableId,
            StableId factionStableId,
            StableId roomRuntimeInstanceStableId,
            StableId roomStableId,
            StableId placementStableId,
            int sourceLevel,
            GameObject sourceRoot,
            IEnumerable<Collider2D> sourceOwnerColliders,
            IEnumerable<EnemyAttackPatternTargetBinding> targetBindings,
            IEnemyAttackPatternProjectilePrefabResolver projectilePrefabs,
            Func<long> lifecycleGenerationExporter,
            Func<bool> activeExporter,
            IEnemyAttackPatternPounceMotion pounceMotion = null)
        {
            SourceEntityStableId = sourceEntityStableId
                ?? throw new ArgumentNullException(nameof(sourceEntityStableId));
            SourceRunParticipantStableId = sourceRunParticipantStableId
                ?? throw new ArgumentNullException(
                    nameof(sourceRunParticipantStableId));
            FactionStableId = factionStableId
                ?? throw new ArgumentNullException(nameof(factionStableId));
            RoomRuntimeInstanceStableId = roomRuntimeInstanceStableId;
            RoomStableId = roomStableId;
            PlacementStableId = placementStableId;
            if (sourceLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(sourceLevel));
            SourceLevel = sourceLevel;
            SourceRoot = sourceRoot
                ?? throw new ArgumentNullException(nameof(sourceRoot));
            ProjectilePrefabs = projectilePrefabs
                ?? throw new ArgumentNullException(nameof(projectilePrefabs));
            this.lifecycleGenerationExporter = lifecycleGenerationExporter
                ?? throw new ArgumentNullException(
                    nameof(lifecycleGenerationExporter));
            this.activeExporter = activeExporter
                ?? throw new ArgumentNullException(nameof(activeExporter));
            PounceMotion = pounceMotion;

            ownerColliders = CopyColliders(
                sourceOwnerColliders,
                nameof(sourceOwnerColliders),
                false);
            targets = CopyTargets(targetBindings);
        }

        public StableId SourceEntityStableId { get; }
        public StableId SourceRunParticipantStableId { get; }
        public StableId FactionStableId { get; }
        public StableId RoomRuntimeInstanceStableId { get; }
        public StableId RoomStableId { get; }
        public StableId PlacementStableId { get; }
        public int SourceLevel { get; }
        public bool HasCanonicalPlacementIdentity
        {
            get
            {
                return RoomRuntimeInstanceStableId != null
                    && RoomStableId != null
                    && PlacementStableId != null;
            }
        }
        public GameObject SourceRoot { get; }
        public IReadOnlyList<Collider2D> OwnerColliders
        {
            get { return ownerColliders; }
        }
        public IReadOnlyList<EnemyAttackPatternTargetBinding> Targets
        {
            get { return targets; }
        }
        public IEnemyAttackPatternProjectilePrefabResolver ProjectilePrefabs { get; }
        public IEnemyAttackPatternPounceMotion PounceMotion { get; }
        public long LifecycleGeneration
        {
            get { return lifecycleGenerationExporter(); }
        }
        public bool IsActive
        {
            get { return activeExporter(); }
        }

        public EnemyAttackPatternTargetBinding FindTarget(
            StableId targetEntityStableId)
        {
            for (int index = 0; index < targets.Count; index++)
            {
                if (targets[index].TargetEntityStableId == targetEntityStableId)
                {
                    return targets[index];
                }
            }
            return null;
        }

        public EnemyAttackPatternTargetBinding FindTarget(Collider2D collider)
        {
            for (int index = 0; index < targets.Count; index++)
            {
                if (targets[index].Contains(collider))
                {
                    return targets[index];
                }
            }
            return null;
        }

        private static ReadOnlyCollection<Collider2D> CopyColliders(
            IEnumerable<Collider2D> source,
            string parameterName,
            bool requireOne)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            var copy = new List<Collider2D>();
            var ids = new HashSet<int>();
            foreach (Collider2D collider in source)
            {
                if (collider != null && ids.Add(collider.GetInstanceID()))
                {
                    copy.Add(collider);
                }
            }
            if (requireOne && copy.Count == 0)
            {
                throw new ArgumentException(
                    "At least one collider is required.",
                    parameterName);
            }
            copy.Sort((left, right) =>
                left.GetInstanceID().CompareTo(right.GetInstanceID()));
            return new ReadOnlyCollection<Collider2D>(copy);
        }

        private static ReadOnlyCollection<EnemyAttackPatternTargetBinding>
            CopyTargets(
                IEnumerable<EnemyAttackPatternTargetBinding> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            var copy = new List<EnemyAttackPatternTargetBinding>();
            var ids = new HashSet<StableId>();
            foreach (EnemyAttackPatternTargetBinding target in source)
            {
                if (target == null
                    || !ids.Add(target.TargetEntityStableId))
                {
                    throw new ArgumentException(
                        "Target bindings must be non-null and unique.",
                        nameof(source));
                }
                copy.Add(target);
            }
            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "At least one explicit target binding is required.",
                    nameof(source));
            }
            copy.Sort((left, right) =>
                left.TargetEntityStableId.CompareTo(
                    right.TargetEntityStableId));
            return new ReadOnlyCollection<EnemyAttackPatternTargetBinding>(copy);
        }
    }

    /// <summary>
    /// Typed source registry shared by Run Session lifecycle validation, policy snapshots, and
    /// physical realization. It contains no scene search, static authority, or enemy-type branch.
    /// </summary>
    public sealed class EnemyAttackPatternUnitySourceRegistry :
        IEnemyAttackPatternSourceLifecycle
    {
        private readonly Dictionary<StableId, EnemyAttackPatternUnitySourceBinding>
            bindings =
                new Dictionary<StableId, EnemyAttackPatternUnitySourceBinding>();

        public void Register(EnemyAttackPatternUnitySourceBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }
            EnemyAttackPatternUnitySourceBinding existing;
            if (bindings.TryGetValue(binding.SourceEntityStableId, out existing))
            {
                if (!ReferenceEquals(existing, binding))
                {
                    throw new InvalidOperationException(
                        "Enemy attack-pattern source identity was registered twice: "
                        + binding.SourceEntityStableId);
                }
                return;
            }
            bindings.Add(binding.SourceEntityStableId, binding);
        }

        public bool TryGet(
            StableId sourceEntityStableId,
            out EnemyAttackPatternUnitySourceBinding binding)
        {
            binding = null;
            return sourceEntityStableId != null
                && bindings.TryGetValue(sourceEntityStableId, out binding)
                && binding != null;
        }

        public bool IsCurrent(
            StableId sourceEntityStableId,
            long lifecycleGeneration)
        {
            EnemyAttackPatternUnitySourceBinding binding;
            return lifecycleGeneration > 0L
                && TryGet(sourceEntityStableId, out binding)
                && binding.IsActive
                && binding.LifecycleGeneration == lifecycleGeneration;
        }

        public bool TryReadSource(
            EnemyAttackEffectEmission emission,
            out CombatActorSnapshot source)
        {
            source = null;
            EnemyAttackPatternUnitySourceBinding binding;
            if (emission == null
                || !TryGet(emission.SourceEntityStableId, out binding))
            {
                return false;
            }
            long lifecycle = binding.LifecycleGeneration;
            source = new CombatActorSnapshot(
                binding.SourceEntityStableId,
                new GameplayEntityIdentity(
                    binding.SourceEntityStableId,
                    GameplayEntityOwnership.Create(
                        binding.SourceRunParticipantStableId,
                        null),
                    binding.FactionStableId),
                lifecycle,
                true,
                binding.IsActive,
                new StableId[0]);
            return lifecycle == emission.SourceLifecycleGeneration;
        }

        public void Clear()
        {
            bindings.Clear();
        }
    }
}
