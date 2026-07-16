using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    public enum DestructiblePropLifecycleState
    {
        Active = 1,
        Destroyed = 2,
    }

    public enum DestructiblePropDamageStatus
    {
        Applied = 1,
        Destroyed = 2,
        DuplicateEventIgnored = 3,
        ConflictingDuplicate = 4,
        HitNotConfirmed = 5,
        TargetMismatch = 6,
        TargetAlreadyDestroyed = 7,
        InvalidInput = 8,
    }

    /// <summary>
    /// Immutable deterministic snapshot for one destructible arena prop.
    /// </summary>
    public sealed class DestructiblePropState : IEquatable<DestructiblePropState>
    {
        public DestructiblePropState(
            StableId propId,
            double currentHealth,
            double maximumHealth,
            DestructiblePropLifecycleState lifecycleState)
        {
            if (propId == null)
            {
                throw new ArgumentNullException(nameof(propId));
            }

            RequireFinitePositive(maximumHealth, nameof(maximumHealth));
            RequireFiniteNonNegative(currentHealth, nameof(currentHealth));
            if (currentHealth > maximumHealth)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentHealth),
                    currentHealth,
                    "Current health cannot exceed maximum health.");
            }

            if (!Enum.IsDefined(typeof(DestructiblePropLifecycleState), lifecycleState))
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleState));
            }

            if (lifecycleState == DestructiblePropLifecycleState.Active && currentHealth <= 0d)
            {
                throw new ArgumentException(
                    "An active destructible prop requires positive health.",
                    nameof(lifecycleState));
            }

            if (lifecycleState == DestructiblePropLifecycleState.Destroyed && currentHealth != 0d)
            {
                throw new ArgumentException(
                    "A destroyed destructible prop requires zero health.",
                    nameof(lifecycleState));
            }

            PropId = propId;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            LifecycleState = lifecycleState;
        }

        public StableId PropId { get; }

        public double CurrentHealth { get; }

        public double MaximumHealth { get; }

        public DestructiblePropLifecycleState LifecycleState { get; }

        public bool IsActive
        {
            get { return LifecycleState == DestructiblePropLifecycleState.Active; }
        }

        public bool IsDestroyed
        {
            get { return LifecycleState == DestructiblePropLifecycleState.Destroyed; }
        }

        public bool Equals(DestructiblePropState other)
        {
            return !ReferenceEquals(other, null)
                && PropId == other.PropId
                && CurrentHealth == other.CurrentHealth
                && MaximumHealth == other.MaximumHealth
                && LifecycleState == other.LifecycleState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DestructiblePropState);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + PropId.GetHashCode();
                hash = (hash * 31) + CurrentHealth.GetHashCode();
                hash = (hash * 31) + MaximumHealth.GetHashCode();
                hash = (hash * 31) + LifecycleState.GetHashCode();
                return hash;
            }
        }

        private static void RequireFinitePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void RequireFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    /// <summary>
    /// Immutable one-shot destruction result suitable for presentation and integration observers.
    /// </summary>
    public sealed class DestructiblePropDestructionResult
    {
        internal DestructiblePropDestructionResult(
            HitMessage confirmedHit,
            DamageMessage damage,
            DestructiblePropState previousState,
            DestructiblePropState destroyedState)
        {
            ConfirmedHit = confirmedHit ?? throw new ArgumentNullException(nameof(confirmedHit));
            Damage = damage ?? throw new ArgumentNullException(nameof(damage));
            PreviousState = previousState ?? throw new ArgumentNullException(nameof(previousState));
            DestroyedState = destroyedState ?? throw new ArgumentNullException(nameof(destroyedState));

            if (!destroyedState.IsDestroyed)
            {
                throw new ArgumentException(
                    "A destruction result requires a destroyed state.",
                    nameof(destroyedState));
            }
        }

        public StableId EventId
        {
            get { return ConfirmedHit.EventId; }
        }

        public StableId PropId
        {
            get { return DestroyedState.PropId; }
        }

        public StableId SourceId
        {
            get { return ConfirmedHit.SourceId; }
        }

        public CombatChannel Channel
        {
            get { return ConfirmedHit.Channel; }
        }

        public HitMessage ConfirmedHit { get; }

        public DamageMessage Damage { get; }

        public DestructiblePropState PreviousState { get; }

        public DestructiblePropState DestroyedState { get; }
    }

    public sealed class DestructiblePropDamageResult
    {
        internal DestructiblePropDamageResult(
            DestructiblePropDamageStatus status,
            HitMessage hit,
            DamageMessage damage,
            DestructiblePropState state,
            DestructiblePropDestructionResult destruction)
        {
            Status = status;
            Hit = hit;
            Damage = damage;
            State = state;
            Destruction = destruction;
        }

        public DestructiblePropDamageStatus Status { get; }

        public HitMessage Hit { get; }

        public DamageMessage Damage { get; }

        public DestructiblePropState State { get; }

        public DestructiblePropDestructionResult Destruction { get; }

        public bool StateChanged
        {
            get
            {
                return Status == DestructiblePropDamageStatus.Applied
                    || Status == DestructiblePropDamageStatus.Destroyed;
            }
        }
    }

    /// <summary>
    /// Plain-C# health and lifecycle authority. It accepts only confirmed Combat Messages v1
    /// hits, rejects event replays, and creates one terminal destruction result per session.
    /// </summary>
    public sealed class DestructiblePropAuthority
    {
        private readonly StableId propId;
        private readonly double maximumHealth;
        private readonly Dictionary<StableId, HitMessage> firstHitsByEventId =
            new Dictionary<StableId, HitMessage>();
        private DestructiblePropState currentState;

        public DestructiblePropAuthority(StableId configuredPropId, double configuredMaximumHealth)
        {
            if (configuredPropId == null)
            {
                throw new ArgumentNullException(nameof(configuredPropId));
            }

            if (double.IsNaN(configuredMaximumHealth)
                || double.IsInfinity(configuredMaximumHealth)
                || configuredMaximumHealth <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(configuredMaximumHealth));
            }

            propId = configuredPropId;
            maximumHealth = configuredMaximumHealth;
            currentState = CreateActiveState();
        }

        public StableId PropId
        {
            get { return propId; }
        }

        public double MaximumHealth
        {
            get { return maximumHealth; }
        }

        public DestructiblePropState CurrentState
        {
            get { return currentState; }
        }

        public int ProcessedEventCount
        {
            get { return firstHitsByEventId.Count; }
        }

        public DestructiblePropDamageResult ApplyConfirmedHit(
            HitMessage hit,
            double requestedDamage)
        {
            if (hit == null
                || double.IsNaN(requestedDamage)
                || double.IsInfinity(requestedDamage)
                || requestedDamage <= 0d)
            {
                return Result(
                    DestructiblePropDamageStatus.InvalidInput,
                    hit,
                    null,
                    null);
            }

            HitMessage firstHit;
            if (firstHitsByEventId.TryGetValue(hit.EventId, out firstHit))
            {
                CombatEventIdentityResult identity = CombatEventIdentity.Classify(firstHit, hit);
                return Result(
                    identity == CombatEventIdentityResult.ConflictingDuplicate
                        ? DestructiblePropDamageStatus.ConflictingDuplicate
                        : DestructiblePropDamageStatus.DuplicateEventIgnored,
                    hit,
                    null,
                    null);
            }

            if (hit.TargetId != propId)
            {
                return Result(
                    DestructiblePropDamageStatus.TargetMismatch,
                    hit,
                    null,
                    null);
            }

            if (hit.Result != HitResult.Confirmed)
            {
                return Result(
                    DestructiblePropDamageStatus.HitNotConfirmed,
                    hit,
                    null,
                    null);
            }

            firstHitsByEventId.Add(hit.EventId, hit);
            DestructiblePropState before = currentState;
            if (before.IsDestroyed)
            {
                VitalState destroyedVital = ToVitalState(before);
                DamageMessage lateDamage = new DamageMessage(
                    hit.EventId,
                    hit.SourceId,
                    hit.TargetId,
                    hit.Channel,
                    requestedDamage,
                    DamageResult.TargetAlreadyDestroyed,
                    destroyedVital,
                    destroyedVital,
                    0d,
                    0d,
                    0d,
                    requestedDamage);
                return Result(
                    DestructiblePropDamageStatus.TargetAlreadyDestroyed,
                    hit,
                    lateDamage,
                    null);
            }

            double healthDamage = Math.Min(before.CurrentHealth, requestedDamage);
            double remainingHealth = before.CurrentHealth - healthDamage;
            DestructiblePropState after = remainingHealth <= 0d
                ? new DestructiblePropState(
                    propId,
                    0d,
                    maximumHealth,
                    DestructiblePropLifecycleState.Destroyed)
                : new DestructiblePropState(
                    propId,
                    remainingHealth,
                    maximumHealth,
                    DestructiblePropLifecycleState.Active);

            DamageMessage damage = new DamageMessage(
                hit.EventId,
                hit.SourceId,
                hit.TargetId,
                hit.Channel,
                requestedDamage,
                DamageResult.Applied,
                ToVitalState(before),
                ToVitalState(after),
                0d,
                requestedDamage,
                healthDamage,
                requestedDamage - healthDamage);

            currentState = after;
            if (!after.IsDestroyed)
            {
                return Result(
                    DestructiblePropDamageStatus.Applied,
                    hit,
                    damage,
                    null);
            }

            DestructiblePropDestructionResult destruction =
                new DestructiblePropDestructionResult(hit, damage, before, after);
            return Result(
                DestructiblePropDamageStatus.Destroyed,
                hit,
                damage,
                destruction);
        }

        public void Restart()
        {
            firstHitsByEventId.Clear();
            currentState = CreateActiveState();
        }

        private DestructiblePropState CreateActiveState()
        {
            return new DestructiblePropState(
                propId,
                maximumHealth,
                maximumHealth,
                DestructiblePropLifecycleState.Active);
        }

        private DestructiblePropDamageResult Result(
            DestructiblePropDamageStatus status,
            HitMessage hit,
            DamageMessage damage,
            DestructiblePropDestructionResult destruction)
        {
            return new DestructiblePropDamageResult(
                status,
                hit,
                damage,
                currentState,
                destruction);
        }

        private static VitalState ToVitalState(DestructiblePropState state)
        {
            return new VitalState(
                state.CurrentHealth,
                state.MaximumHealth,
                0d,
                0d);
        }
    }
}
