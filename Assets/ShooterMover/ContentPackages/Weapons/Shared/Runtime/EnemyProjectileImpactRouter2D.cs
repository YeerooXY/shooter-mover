using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;

namespace ShooterMover.ContentPackages.Weapons.Shared.Runtime
{
    /// <summary>
    /// Immutable registration data for one projectile-producing enemy source. The router
    /// depends only on the package-neutral projectile and hit adapters; it contains no
    /// enemy-type, level, scene, weapon-name, or player-authority branching.
    /// </summary>
    public sealed class EnemyProjectileDamageBinding2D
    {
        public EnemyProjectileDamageBinding2D(
            StableId sourceActorId,
            CombatHit2DAdapter hitAdapter,
            ProjectileExecutionPlanAdapter projectileAdapter,
            double damage)
        {
            if (sourceActorId == null)
            {
                throw new ArgumentNullException(nameof(sourceActorId));
            }
            if (hitAdapter == null)
            {
                throw new ArgumentNullException(nameof(hitAdapter));
            }
            if (projectileAdapter == null)
            {
                throw new ArgumentNullException(nameof(projectileAdapter));
            }
            if (!IsFinitePositive(damage))
            {
                throw new ArgumentOutOfRangeException(nameof(damage));
            }
            if (hitAdapter.SourceId != sourceActorId)
            {
                throw new ArgumentException(
                    "The binding source actor must match the hit adapter source actor.",
                    nameof(sourceActorId));
            }

            SourceActorId = sourceActorId;
            HitAdapter = hitAdapter;
            ProjectileAdapter = projectileAdapter;
            Damage = damage;
        }

        public StableId SourceActorId { get; }

        public CombatHit2DAdapter HitAdapter { get; }

        public ProjectileExecutionPlanAdapter ProjectileAdapter { get; }

        public double Damage { get; }

        internal bool IsEquivalentTo(EnemyProjectileDamageBinding2D other)
        {
            return other != null
                && SourceActorId == other.SourceActorId
                && ReferenceEquals(HitAdapter, other.HitAdapter)
                && ReferenceEquals(ProjectileAdapter, other.ProjectileAdapter)
                && Damage.Equals(other.Damage);
        }

        private static bool IsFinitePositive(double value)
        {
            return value > 0d
                && !double.IsNaN(value)
                && !double.IsInfinity(value);
        }
    }

    /// <summary>
    /// Immutable package-neutral enemy impact fact. TargetLifecycleGeneration is captured
    /// directly when the physical projectile is emitted; runtime state is never decoded
    /// from a StableId string.
    /// </summary>
    public sealed class EnemyProjectileImpactFact2D
    {
        public EnemyProjectileImpactFact2D(
            StableId combatEventId,
            StableId hitEventId,
            StableId sourceActorId,
            StableId targetActorId,
            double damage,
            CombatChannel channel,
            long targetLifecycleGeneration)
        {
            CombatEventId = combatEventId
                ?? throw new ArgumentNullException(nameof(combatEventId));
            HitEventId = hitEventId
                ?? throw new ArgumentNullException(nameof(hitEventId));
            SourceActorId = sourceActorId
                ?? throw new ArgumentNullException(nameof(sourceActorId));
            TargetActorId = targetActorId
                ?? throw new ArgumentNullException(nameof(targetActorId));
            if (damage <= 0d
                || double.IsNaN(damage)
                || double.IsInfinity(damage))
            {
                throw new ArgumentOutOfRangeException(nameof(damage));
            }
            if (!Enum.IsDefined(typeof(CombatChannel), channel)
                || channel == CombatChannel.System)
            {
                throw new ArgumentOutOfRangeException(nameof(channel));
            }
            if (targetLifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetLifecycleGeneration));
            }

            Damage = damage;
            Channel = channel;
            TargetLifecycleGeneration = targetLifecycleGeneration;
        }

        public StableId CombatEventId { get; }

        public StableId HitEventId { get; }

        public StableId SourceActorId { get; }

        public StableId TargetActorId { get; }

        public double Damage { get; }

        public CombatChannel Channel { get; }

        public long TargetLifecycleGeneration { get; }
    }

    /// <summary>
    /// Reusable many-source enemy projectile router. Each source uses the same registration,
    /// emission ledger, collision admission, lifecycle capture, and immutable impact output.
    /// </summary>
    public sealed class EnemyProjectileImpactRouter2D : IDisposable
    {
        private readonly Func<long> readTargetLifecycleGeneration;
        private readonly List<Registration> registrations = new List<Registration>();
        private readonly Dictionary<StableId, PendingImpact> pendingImpacts =
            new Dictionary<StableId, PendingImpact>();
        private readonly Dictionary<BoundedProjectile2D, StableId> trackedProjectiles =
            new Dictionary<BoundedProjectile2D, StableId>();

        private bool disposed;

        public EnemyProjectileImpactRouter2D(
            Func<long> readTargetLifecycleGeneration)
        {
            this.readTargetLifecycleGeneration = readTargetLifecycleGeneration
                ?? throw new ArgumentNullException(
                    nameof(readTargetLifecycleGeneration));
        }

        public event Action<EnemyProjectileImpactFact2D> ImpactConfirmed;

        public int RegisteredSourceCount
        {
            get { return registrations.Count; }
        }

        public int PendingImpactCount
        {
            get { return pendingImpacts.Count; }
        }

        public int ConfirmedImpactCount { get; private set; }

        public EnemyProjectileImpactFact2D LastConfirmedImpact { get; private set; }

        public bool TryRegister(EnemyProjectileDamageBinding2D binding)
        {
            if (disposed || binding == null)
            {
                return false;
            }

            for (int index = 0; index < registrations.Count; index++)
            {
                Registration existing = registrations[index];
                bool sharesHitAdapter = ReferenceEquals(
                    existing.Binding.HitAdapter,
                    binding.HitAdapter);
                bool sharesProjectileAdapter = ReferenceEquals(
                    existing.Binding.ProjectileAdapter,
                    binding.ProjectileAdapter);
                if (!sharesHitAdapter && !sharesProjectileAdapter)
                {
                    continue;
                }

                return sharesHitAdapter
                    && sharesProjectileAdapter
                    && existing.Binding.IsEquivalentTo(binding);
            }

            var registration = new Registration(binding);
            registration.HitHandler = translation =>
                HandleHitTranslated(registration, translation);
            registration.ProjectileHandler = emission =>
                HandleProjectileSpawned(registration, emission);

            binding.HitAdapter.HitTranslated += registration.HitHandler;
            binding.ProjectileAdapter.ProjectileSpawned +=
                registration.ProjectileHandler;
            registrations.Add(registration);
            return true;
        }

        public void ClearPendingImpacts()
        {
            foreach (KeyValuePair<BoundedProjectile2D, StableId> pair
                in trackedProjectiles)
            {
                if (pair.Key != null)
                {
                    pair.Key.Completed -= HandleTrackedProjectileCompleted;
                }
            }
            trackedProjectiles.Clear();
            pendingImpacts.Clear();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;

            for (int index = 0; index < registrations.Count; index++)
            {
                Registration registration = registrations[index];
                registration.Binding.HitAdapter.HitTranslated -=
                    registration.HitHandler;
                registration.Binding.ProjectileAdapter.ProjectileSpawned -=
                    registration.ProjectileHandler;
            }
            registrations.Clear();
            ClearPendingImpacts();
            ImpactConfirmed = null;
            LastConfirmedImpact = null;
        }

        private void HandleProjectileSpawned(
            Registration registration,
            ProjectileExecutionEmission2D emission)
        {
            if (disposed
                || registration == null
                || emission == null
                || emission.Projectile == null
                || emission.CombatEventId == null
                || emission.HitEventId == null)
            {
                return;
            }

            long generation;
            try
            {
                generation = readTargetLifecycleGeneration();
            }
            catch (Exception)
            {
                return;
            }
            if (generation < 0L)
            {
                return;
            }

            var pending = new PendingImpact(
                registration,
                emission.CombatEventId,
                emission.HitEventId,
                generation);
            PendingImpact existing;
            if (pendingImpacts.TryGetValue(emission.HitEventId, out existing))
            {
                if (!existing.IsEquivalentTo(pending))
                {
                    return;
                }
            }
            else
            {
                pendingImpacts.Add(emission.HitEventId, pending);
            }

            StableId previousHitEventId;
            if (trackedProjectiles.TryGetValue(
                    emission.Projectile,
                    out previousHitEventId)
                && previousHitEventId != emission.HitEventId)
            {
                pendingImpacts.Remove(previousHitEventId);
            }
            trackedProjectiles[emission.Projectile] = emission.HitEventId;
            emission.Projectile.Completed -= HandleTrackedProjectileCompleted;
            emission.Projectile.Completed += HandleTrackedProjectileCompleted;
        }

        private void HandleHitTranslated(
            Registration registration,
            CombatHit2DTranslationResult translation)
        {
            if (disposed
                || registration == null
                || translation == null
                || translation.Status != CombatHit2DTranslationStatus.Confirmed
                || translation.Message == null)
            {
                return;
            }

            HitMessage message = translation.Message;
            PendingImpact pending;
            if (!pendingImpacts.TryGetValue(message.EventId, out pending)
                || !ReferenceEquals(pending.Registration, registration)
                || message.SourceId != registration.Binding.SourceActorId
                || message.TargetId == null)
            {
                return;
            }

            var fact = new EnemyProjectileImpactFact2D(
                pending.CombatEventId,
                message.EventId,
                message.SourceId,
                message.TargetId,
                registration.Binding.Damage,
                message.Channel,
                pending.TargetLifecycleGeneration);
            LastConfirmedImpact = fact;
            ConfirmedImpactCount++;

            Action<EnemyProjectileImpactFact2D> handler = ImpactConfirmed;
            if (handler == null)
            {
                return;
            }
            try
            {
                handler(fact);
            }
            catch (Exception)
            {
                // A downstream observer cannot invalidate the physical collision fact.
            }
        }

        private void HandleTrackedProjectileCompleted(BoundedProjectile2D projectile)
        {
            if (projectile == null)
            {
                return;
            }
            projectile.Completed -= HandleTrackedProjectileCompleted;

            StableId hitEventId;
            if (!trackedProjectiles.TryGetValue(projectile, out hitEventId))
            {
                return;
            }
            trackedProjectiles.Remove(projectile);
            pendingImpacts.Remove(hitEventId);
        }

        private sealed class Registration
        {
            public Registration(EnemyProjectileDamageBinding2D binding)
            {
                Binding = binding;
            }

            public EnemyProjectileDamageBinding2D Binding { get; }

            public Action<CombatHit2DTranslationResult> HitHandler { get; set; }

            public Action<ProjectileExecutionEmission2D> ProjectileHandler { get; set; }
        }

        private sealed class PendingImpact
        {
            public PendingImpact(
                Registration registration,
                StableId combatEventId,
                StableId hitEventId,
                long targetLifecycleGeneration)
            {
                Registration = registration;
                CombatEventId = combatEventId;
                HitEventId = hitEventId;
                TargetLifecycleGeneration = targetLifecycleGeneration;
            }

            public Registration Registration { get; }

            public StableId CombatEventId { get; }

            public StableId HitEventId { get; }

            public long TargetLifecycleGeneration { get; }

            public bool IsEquivalentTo(PendingImpact other)
            {
                return other != null
                    && ReferenceEquals(Registration, other.Registration)
                    && CombatEventId == other.CombatEventId
                    && HitEventId == other.HitEventId
                    && TargetLifecycleGeneration == other.TargetLifecycleGeneration;
            }
        }
    }
}
