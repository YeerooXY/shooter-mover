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

    public enum EnemyDamageAdmissionObservationStatus
    {
        Accepted = 1,
        Duplicate = 2,
        InvalidInput = 3,
        ConflictingDuplicate = 4,
        Disposed = 5,
    }

    public enum EnemyAttackDeliveryKind
    {
        Projectile = 1,
        Contact = 2,
        Pounce = 3,
    }

    /// <summary>
    /// Immutable admission fact for one enemy attack that may later translate into a
    /// physical hit. Lifecycle generation is typed data and delivery kind is diagnostic;
    /// the damage router applies identical admission rules for every enemy attack kind.
    /// </summary>
    public class EnemyDamageAdmissionFactV1
    {
        public EnemyDamageAdmissionFactV1(
            StableId eventId,
            long lifecycleGeneration,
            EnemyAttackDeliveryKind deliveryKind)
        {
            EventId = eventId
                ?? throw new ArgumentNullException(nameof(eventId));
            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            if (!Enum.IsDefined(typeof(EnemyAttackDeliveryKind), deliveryKind))
            {
                throw new ArgumentOutOfRangeException(nameof(deliveryKind));
            }

            LifecycleGeneration = lifecycleGeneration;
            DeliveryKind = deliveryKind;
        }

        public StableId EventId { get; }

        public long LifecycleGeneration { get; }

        public EnemyAttackDeliveryKind DeliveryKind { get; }
    }

    public sealed class EnemyProjectileEmissionFactV1 :
        EnemyDamageAdmissionFactV1
    {
        public EnemyProjectileEmissionFactV1(
            StableId hitEventId,
            long lifecycleGeneration)
            : base(
                hitEventId,
                lifecycleGeneration,
                EnemyAttackDeliveryKind.Projectile)
        {
        }

        public StableId HitEventId
        {
            get { return EventId; }
        }
    }

    /// <summary>
    /// Reusable enemy-to-player admission boundary. Every registered enemy source uses
    /// the same immutable attack ledger and the same PlayerDamageRequest path. It contains
    /// no enemy type, level, scene, package, hierarchy-name, or weapon-name branch.
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

        public EnemyDamageAdmissionObservationStatus ObserveAttack(
            EnemyDamageAdmissionFactV1 admission)
        {
            if (disposed)
            {
                return EnemyDamageAdmissionObservationStatus.Disposed;
            }
            if (admission == null || admission.EventId == null)
            {
                return EnemyDamageAdmissionObservationStatus.InvalidInput;
            }

            long existingGeneration;
            if (emissionGenerations.TryGetValue(
                    admission.EventId,
                    out existingGeneration))
            {
                return existingGeneration == admission.LifecycleGeneration
                    ? EnemyDamageAdmissionObservationStatus.Duplicate
                    : EnemyDamageAdmissionObservationStatus.ConflictingDuplicate;
            }

            emissionGenerations.Add(
                admission.EventId,
                admission.LifecycleGeneration);
            return EnemyDamageAdmissionObservationStatus.Accepted;
        }

        public EnemyDamageAdmissionObservationStatus ObserveEmission(
            EnemyProjectileEmissionFactV1 emission)
        {
            return ObserveAttack(emission);
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
