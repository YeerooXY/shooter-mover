using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;

namespace ShooterMover.UnityAdapters.Players
{
    public enum EnemyDamageSourceRegistrationStatus
    {
        Registered = 1,
        AlreadyRegistered = 2,
        InvalidInput = 3,
        ConflictingSource = 4,
        Disposed = 5,
    }

    public enum EnemyProjectileEmissionObservationStatus
    {
        Accepted = 1,
        Duplicate = 2,
        InvalidInput = 3,
        ConflictingDuplicate = 4,
        Disposed = 5,
    }

    /// <summary>
    /// Immutable lifecycle fact emitted when one enemy projectile becomes live. The hit
    /// event identifies the later physical collision; lifecycle generation is carried as
    /// typed data rather than reconstructed by the damage router.
    /// </summary>
    public sealed class EnemyProjectileEmissionFactV1
    {
        public EnemyProjectileEmissionFactV1(
            StableId hitEventId,
            long lifecycleGeneration)
        {
            HitEventId = hitEventId
                ?? throw new ArgumentNullException(nameof(hitEventId));
            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }

            LifecycleGeneration = lifecycleGeneration;
        }

        public StableId HitEventId { get; }

        public long LifecycleGeneration { get; }
    }

    /// <summary>
    /// Reusable enemy-projectile-to-player admission boundary. Every registered enemy
    /// source uses the same immutable emission ledger and the same PlayerDamageRequest
    /// path. It contains no enemy type, level, scene, package, or weapon-name branch.
    /// </summary>
    public sealed class EnemyToPlayerDamageRouterV1 : IDisposable
    {
        private sealed class SourceBinding
        {
            public SourceBinding(CombatHit2DAdapter hitAdapter, double damage)
            {
                HitAdapter = hitAdapter;
                Damage = damage;
            }

            public CombatHit2DAdapter HitAdapter { get; }

            public double Damage { get; }
        }

        private readonly Func<bool> isActive;
        private readonly Func<PlayerDamageRequest, DamageReceiverResult> applyDamage;
        private readonly Dictionary<StableId, SourceBinding> sources =
            new Dictionary<StableId, SourceBinding>();
        private readonly Dictionary<StableId, long> emissionGenerations =
            new Dictionary<StableId, long>();
        private bool disposed;

        public EnemyToPlayerDamageRouterV1(
            Func<PlayerDamageRequest, DamageReceiverResult> applyDamage,
            Func<bool> isActive = null)
        {
            this.applyDamage = applyDamage
                ?? throw new ArgumentNullException(nameof(applyDamage));
            this.isActive = isActive ?? (() => true);
        }

        public int RegisteredSourceCount
        {
            get { return sources.Count; }
        }

        public int PendingEmissionCount
        {
            get { return emissionGenerations.Count; }
        }

        public int ForwardedRequestCount { get; private set; }

        public int RejectedHitCount { get; private set; }

        public DamageReceiverResult LastDamageResult { get; private set; }

        public EnemyDamageSourceRegistrationStatus RegisterDamageSource(
            CombatHit2DAdapter hitAdapter,
            double damage)
        {
            if (disposed)
            {
                return EnemyDamageSourceRegistrationStatus.Disposed;
            }
            if (hitAdapter == null
                || hitAdapter.SourceId == null
                || !IsFinitePositive(damage))
            {
                return EnemyDamageSourceRegistrationStatus.InvalidInput;
            }

            SourceBinding existing;
            if (sources.TryGetValue(hitAdapter.SourceId, out existing))
            {
                return existing.HitAdapter == hitAdapter
                    && Math.Abs(existing.Damage - damage) <= 0.0000001d
                        ? EnemyDamageSourceRegistrationStatus.AlreadyRegistered
                        : EnemyDamageSourceRegistrationStatus.ConflictingSource;
            }

            sources.Add(
                hitAdapter.SourceId,
                new SourceBinding(hitAdapter, damage));
            hitAdapter.HitTranslated += HandleHitTranslated;
            return EnemyDamageSourceRegistrationStatus.Registered;
        }

        public EnemyProjectileEmissionObservationStatus ObserveEmission(
            EnemyProjectileEmissionFactV1 emission)
        {
            if (disposed)
            {
                return EnemyProjectileEmissionObservationStatus.Disposed;
            }
            if (emission == null || emission.HitEventId == null)
            {
                return EnemyProjectileEmissionObservationStatus.InvalidInput;
            }

            long existingGeneration;
            if (emissionGenerations.TryGetValue(
                    emission.HitEventId,
                    out existingGeneration))
            {
                return existingGeneration == emission.LifecycleGeneration
                    ? EnemyProjectileEmissionObservationStatus.Duplicate
                    : EnemyProjectileEmissionObservationStatus.ConflictingDuplicate;
            }

            emissionGenerations.Add(
                emission.HitEventId,
                emission.LifecycleGeneration);
            return EnemyProjectileEmissionObservationStatus.Accepted;
        }

        public bool RetireEmission(StableId hitEventId)
        {
            return !disposed
                && hitEventId != null
                && emissionGenerations.Remove(hitEventId);
        }

        public void ClearLifecycle()
        {
            emissionGenerations.Clear();
            LastDamageResult = null;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (SourceBinding binding in sources.Values)
            {
                if (binding != null && binding.HitAdapter != null)
                {
                    binding.HitAdapter.HitTranslated -= HandleHitTranslated;
                }
            }
            sources.Clear();
            emissionGenerations.Clear();
            LastDamageResult = null;
        }

        private void HandleHitTranslated(CombatHit2DTranslationResult translation)
        {
            if (disposed
                || !isActive()
                || translation == null
                || translation.Status != CombatHit2DTranslationStatus.Confirmed
                || translation.Message == null)
            {
                RejectedHitCount++;
                return;
            }

            HitMessage hit = translation.Message;
            SourceBinding source;
            long lifecycleGeneration;
            if (!sources.TryGetValue(hit.SourceId, out source)
                || source == null
                || !emissionGenerations.TryGetValue(
                    hit.EventId,
                    out lifecycleGeneration))
            {
                RejectedHitCount++;
                return;
            }

            emissionGenerations.Remove(hit.EventId);
            try
            {
                LastDamageResult = applyDamage(
                    new PlayerDamageRequest(
                        hit.EventId,
                        hit.SourceId,
                        null,
                        hit.TargetId,
                        source.Damage,
                        hit.Channel,
                        lifecycleGeneration));
                ForwardedRequestCount++;
            }
            catch (Exception)
            {
                LastDamageResult = null;
                RejectedHitCount++;
            }
        }

        private static bool IsFinitePositive(double value)
        {
            return value > 0d
                && !double.IsNaN(value)
                && !double.IsInfinity(value);
        }
    }
}
