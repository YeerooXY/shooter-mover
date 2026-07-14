using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Enemies
{
    public enum EnemyActorLifecyclePhase
    {
        Active = 1,
        Destroyed = 2,
        Despawned = 3,
    }

    public enum EnemyActorDeathCause
    {
        None = 0,
        IncomingDamage = 1,
        DisposableImpact = 2,
    }

    public enum EnemyActorCommandKind
    {
        Damage = 1,
        Contact = 2,
        Despawn = 3,
    }

    /// <summary>
    /// Immutable, session-only enemy truth. It deliberately owns no Unity object,
    /// player velocity, encounter lifecycle, mission state, reward, or persistence data.
    /// </summary>
    public sealed class EnemyActorState : IEquatable<EnemyActorState>
    {
        private readonly ReadOnlyCollection<StableId> processedEventIds;

        private EnemyActorState(
            StableId actorId,
            StableId roleId,
            double maximumHealth,
            double health,
            int weightClassValue,
            EnemyContactPolicy contactPolicy,
            EnemyActorLifecyclePhase lifecyclePhase,
            EnemyActorDeathCause deathCause,
            bool destroyedVitalEmitted,
            bool encounterResolutionEmitted,
            IList<StableId> processedEventIds)
        {
            ActorId = RequireId(actorId, nameof(actorId));
            RoleId = RequireId(roleId, nameof(roleId));
            RequireFinitePositive(maximumHealth, nameof(maximumHealth));
            RequireFiniteNonNegative(health, nameof(health));
            RequireWeightClass(weightClassValue, nameof(weightClassValue));

            if (health > maximumHealth)
            {
                throw new ArgumentException("Enemy health cannot exceed maximum health.", nameof(health));
            }

            if (!Enum.IsDefined(typeof(EnemyActorLifecyclePhase), lifecyclePhase))
            {
                throw new ArgumentOutOfRangeException(nameof(lifecyclePhase));
            }

            if (!Enum.IsDefined(typeof(EnemyActorDeathCause), deathCause))
            {
                throw new ArgumentOutOfRangeException(nameof(deathCause));
            }

            if (contactPolicy == null)
            {
                throw new ArgumentNullException(nameof(contactPolicy));
            }

            bool terminal = lifecyclePhase != EnemyActorLifecyclePhase.Active;
            if (terminal != (health == 0d))
            {
                throw new ArgumentException("Only active enemies may retain health.");
            }

            if (lifecyclePhase == EnemyActorLifecyclePhase.Active
                && (deathCause != EnemyActorDeathCause.None
                    || destroyedVitalEmitted
                    || encounterResolutionEmitted))
            {
                throw new ArgumentException("An active enemy cannot retain terminal lifecycle facts.");
            }

            if (terminal
                && (deathCause == EnemyActorDeathCause.None
                    || !destroyedVitalEmitted
                    || !encounterResolutionEmitted))
            {
                throw new ArgumentException(
                    "A terminal enemy requires one death cause and both emitted terminal facts.");
            }

            MaximumHealth = maximumHealth;
            Health = health;
            WeightClassValue = weightClassValue;
            ContactPolicy = contactPolicy;
            LifecyclePhase = lifecyclePhase;
            DeathCause = deathCause;
            DestroyedVitalEmitted = destroyedVitalEmitted;
            EncounterResolutionEmitted = encounterResolutionEmitted;
            this.processedEventIds = CopyEventIds(processedEventIds);
        }

        public StableId ActorId { get; }

        public StableId RoleId { get; }

        public double MaximumHealth { get; }

        public double Health { get; }

        /// <summary>
        /// Frozen CS-004 CombatWeightClass v1 numeric value.
        /// Domain remains reference-free; EditMode tests prove the mapping.
        /// </summary>
        public int WeightClassValue { get; }

        public EnemyContactPolicy ContactPolicy { get; }

        public EnemyActorLifecyclePhase LifecyclePhase { get; }

        public EnemyActorDeathCause DeathCause { get; }

        public bool DestroyedVitalEmitted { get; }

        public bool EncounterResolutionEmitted { get; }

        public IReadOnlyList<StableId> ProcessedEventIds
        {
            get { return processedEventIds; }
        }

        public bool IsActive
        {
            get { return LifecyclePhase == EnemyActorLifecyclePhase.Active; }
        }

        public bool IsDestroyed
        {
            get { return LifecyclePhase != EnemyActorLifecyclePhase.Active; }
        }

        public bool IsDespawned
        {
            get { return LifecyclePhase == EnemyActorLifecyclePhase.Despawned; }
        }

        public static EnemyActorState Create(
            StableId actorId,
            StableId roleId,
            double maximumHealth,
            int weightClassValue,
            EnemyContactPolicy contactPolicy)
        {
            return new EnemyActorState(
                actorId,
                roleId,
                maximumHealth,
                maximumHealth,
                weightClassValue,
                contactPolicy,
                EnemyActorLifecyclePhase.Active,
                EnemyActorDeathCause.None,
                false,
                false,
                new StableId[0]);
        }

        public bool HasProcessed(StableId eventId)
        {
            RequireId(eventId, nameof(eventId));
            return FindEventIndex(processedEventIds, eventId) >= 0;
        }

        internal EnemyActorState Next(
            double health,
            EnemyContactPolicy contactPolicy,
            EnemyActorLifecyclePhase lifecyclePhase,
            EnemyActorDeathCause deathCause,
            bool destroyedVitalEmitted,
            bool encounterResolutionEmitted,
            StableId processedEventId)
        {
            if (processedEventId == null)
            {
                throw new ArgumentNullException(nameof(processedEventId));
            }

            List<StableId> nextIds = new List<StableId>(processedEventIds);
            int index = FindEventIndex(processedEventIds, processedEventId);
            if (index < 0)
            {
                nextIds.Insert(~index, processedEventId);
            }

            return new EnemyActorState(
                ActorId,
                RoleId,
                MaximumHealth,
                health,
                WeightClassValue,
                contactPolicy,
                lifecyclePhase,
                deathCause,
                destroyedVitalEmitted,
                encounterResolutionEmitted,
                nextIds);
        }

        public bool Equals(EnemyActorState other)
        {
            if (other == null
                || ActorId != other.ActorId
                || RoleId != other.RoleId
                || MaximumHealth != other.MaximumHealth
                || Health != other.Health
                || WeightClassValue != other.WeightClassValue
                || !ContactPolicy.Equals(other.ContactPolicy)
                || LifecyclePhase != other.LifecyclePhase
                || DeathCause != other.DeathCause
                || DestroyedVitalEmitted != other.DestroyedVitalEmitted
                || EncounterResolutionEmitted != other.EncounterResolutionEmitted
                || processedEventIds.Count != other.processedEventIds.Count)
            {
                return false;
            }

            for (int index = 0; index < processedEventIds.Count; index++)
            {
                if (processedEventIds[index] != other.processedEventIds[index])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EnemyActorState);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + ActorId.GetHashCode();
                hash = (hash * 31) + RoleId.GetHashCode();
                hash = (hash * 31) + MaximumHealth.GetHashCode();
                hash = (hash * 31) + Health.GetHashCode();
                hash = (hash * 31) + WeightClassValue;
                hash = (hash * 31) + ContactPolicy.GetHashCode();
                hash = (hash * 31) + LifecyclePhase.GetHashCode();
                hash = (hash * 31) + DeathCause.GetHashCode();
                hash = (hash * 31) + DestroyedVitalEmitted.GetHashCode();
                hash = (hash * 31) + EncounterResolutionEmitted.GetHashCode();
                foreach (StableId eventId in processedEventIds)
                {
                    hash = (hash * 31) + eventId.GetHashCode();
                }

                return hash;
            }
        }

        internal static void RequireFinitePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be finite and positive.");
            }
        }

        internal static void RequireFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be finite and non-negative.");
            }
        }

        internal static StableId RequireId(StableId value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return value;
        }

        internal static void RequireWeightClass(int value, string parameterName)
        {
            if (value < 1 || value > 4)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Unknown CS-004 CombatWeightClass v1 numeric value.");
            }
        }

        private static ReadOnlyCollection<StableId> CopyEventIds(IList<StableId> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            List<StableId> copy = new List<StableId>(source.Count);
            StableId previous = null;
            foreach (StableId item in source)
            {
                RequireId(item, nameof(source));
                if (previous != null && previous.CompareTo(item) >= 0)
                {
                    throw new ArgumentException(
                        "Processed event IDs must be unique and in canonical order.",
                        nameof(source));
                }

                copy.Add(item);
                previous = item;
            }

            return new ReadOnlyCollection<StableId>(copy);
        }

        private static int FindEventIndex(IList<StableId> source, StableId eventId)
        {
            int low = 0;
            int high = source.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                int comparison = source[middle].CompareTo(eventId);
                if (comparison == 0)
                {
                    return middle;
                }

                if (comparison < 0)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return ~low;
        }
    }

    /// <summary>
    /// One validated input fact. Order is authoritative within one step; the stepper
    /// applies a stable EventId/Kind tie-break so caller enumeration cannot change results.
    /// </summary>
    public sealed class EnemyActorCommand
    {
        private EnemyActorCommand(
            EnemyActorCommandKind kind,
            long order,
            StableId eventId,
            StableId otherActorId,
            int channelValue,
            double amount,
            double observedAtSeconds,
            int contactClassificationValue,
            int moverWeightClassValue)
        {
            if (!Enum.IsDefined(typeof(EnemyActorCommandKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (order < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(order));
            }

            Kind = kind;
            Order = order;
            EventId = EnemyActorState.RequireId(eventId, nameof(eventId));
            OtherActorId = EnemyActorState.RequireId(otherActorId, nameof(otherActorId));
            ChannelValue = channelValue;
            Amount = amount;
            ObservedAtSeconds = observedAtSeconds;
            ContactClassificationValue = contactClassificationValue;
            MoverWeightClassValue = moverWeightClassValue;
        }

        public EnemyActorCommandKind Kind { get; }

        public long Order { get; }

        public StableId EventId { get; }

        public StableId OtherActorId { get; }

        public int ChannelValue { get; }

        public double Amount { get; }

        public double ObservedAtSeconds { get; }

        public int ContactClassificationValue { get; }

        public int MoverWeightClassValue { get; }

        public static EnemyActorCommand Damage(
            long order,
            StableId eventId,
            StableId sourceId,
            int channelValue,
            double amount)
        {
            if (channelValue < 1 || channelValue > 6)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(channelValue),
                    channelValue,
                    "Damage must use a CS-004 damage-bearing channel.");
            }

            EnemyActorState.RequireFinitePositive(amount, nameof(amount));
            return new EnemyActorCommand(
                EnemyActorCommandKind.Damage,
                order,
                eventId,
                sourceId,
                channelValue,
                amount,
                0d,
                0,
                0);
        }

        public static EnemyActorCommand Contact(
            long order,
            StableId eventId,
            StableId moverId,
            double observedAtSeconds,
            int contactClassificationValue,
            int moverWeightClassValue)
        {
            EnemyActorState.RequireFiniteNonNegative(observedAtSeconds, nameof(observedAtSeconds));
            if (contactClassificationValue < 1 || contactClassificationValue > 5)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(contactClassificationValue),
                    contactClassificationValue,
                    "Unknown CS-004 ContactClassification v1 numeric value.");
            }

            EnemyActorState.RequireWeightClass(moverWeightClassValue, nameof(moverWeightClassValue));
            return new EnemyActorCommand(
                EnemyActorCommandKind.Contact,
                order,
                eventId,
                moverId,
                EnemyContactPolicy.ContactChannelValue,
                0d,
                observedAtSeconds,
                contactClassificationValue,
                moverWeightClassValue);
        }

        public static EnemyActorCommand Despawn(
            long order,
            StableId eventId,
            StableId sourceId)
        {
            return new EnemyActorCommand(
                EnemyActorCommandKind.Despawn,
                order,
                eventId,
                sourceId,
                EnemyContactPolicy.SystemChannelValue,
                0d,
                0d,
                0,
                0);
        }
    }

    public abstract class EnemyActorNotification
    {
        protected EnemyActorNotification(
            StableId eventId,
            StableId sourceId,
            StableId targetId,
            int channelValue)
        {
            EventId = EnemyActorState.RequireId(eventId, nameof(eventId));
            SourceId = EnemyActorState.RequireId(sourceId, nameof(sourceId));
            TargetId = EnemyActorState.RequireId(targetId, nameof(targetId));
            ChannelValue = channelValue;
        }

        public StableId EventId { get; }

        public StableId SourceId { get; }

        public StableId TargetId { get; }

        public int ChannelValue { get; }
    }

    public sealed class EnemyDamageNotification : EnemyActorNotification
    {
        internal EnemyDamageNotification(
            StableId eventId,
            StableId sourceId,
            StableId targetId,
            int channelValue,
            double requestedAmount,
            int resultValue,
            double beforeHealth,
            double afterHealth,
            double maximumHealth,
            double healthDamageApplied,
            double unappliedAmount)
            : base(eventId, sourceId, targetId, channelValue)
        {
            RequestedAmount = requestedAmount;
            ResultValue = resultValue;
            BeforeHealth = beforeHealth;
            AfterHealth = afterHealth;
            MaximumHealth = maximumHealth;
            HealthDamageApplied = healthDamageApplied;
            UnappliedAmount = unappliedAmount;
        }

        public double RequestedAmount { get; }

        public int ResultValue { get; }

        public double BeforeHealth { get; }

        public double AfterHealth { get; }

        public double MaximumHealth { get; }

        public double HealthDamageApplied { get; }

        public double UnappliedAmount { get; }
    }

    public sealed class EnemyContactNotification : EnemyActorNotification
    {
        internal EnemyContactNotification(
            StableId eventId,
            StableId sourceId,
            StableId targetId,
            int contactClassificationValue,
            int resultValue,
            int weightResultValue,
            EnemyContactDecision decision,
            EnemyContactMode mode,
            bool requestsMoverDamage,
            double moverDamageAmount)
            : base(eventId, sourceId, targetId, EnemyContactPolicy.ContactChannelValue)
        {
            ContactClassificationValue = contactClassificationValue;
            ResultValue = resultValue;
            WeightResultValue = weightResultValue;
            Decision = decision;
            Mode = mode;
            RequestsMoverDamage = requestsMoverDamage;
            MoverDamageAmount = moverDamageAmount;
        }

        public int ContactClassificationValue { get; }

        public int ResultValue { get; }

        public int WeightResultValue { get; }

        public EnemyContactDecision Decision { get; }

        public EnemyContactMode Mode { get; }

        public bool RequestsMoverDamage { get; }

        public double MoverDamageAmount { get; }
    }

    public sealed class EnemyDestroyedNotification : EnemyActorNotification
    {
        internal EnemyDestroyedNotification(
            StableId eventId,
            StableId sourceId,
            StableId targetId,
            int channelValue,
            double maximumHealth,
            EnemyActorDeathCause deathCause)
            : base(eventId, sourceId, targetId, channelValue)
        {
            MaximumHealth = maximumHealth;
            DeathCause = deathCause;
        }

        public double MaximumHealth { get; }

        public EnemyActorDeathCause DeathCause { get; }
    }

    /// <summary>
    /// Encounter-resolution-ready terminal fact. The later adapter supplies the
    /// EncounterRuntimeIdentity; this actor does not own encounter or mission state.
    /// </summary>
    public sealed class EnemyEncounterResolutionNotification : EnemyActorNotification
    {
        internal EnemyEncounterResolutionNotification(EnemyDestroyedNotification vital)
            : base(
                RequireVital(vital).EventId,
                vital.SourceId,
                vital.TargetId,
                vital.ChannelValue)
        {
            Vital = vital;
        }

        public EnemyDestroyedNotification Vital { get; }

        public StableId ActorId
        {
            get { return Vital.TargetId; }
        }

        private static EnemyDestroyedNotification RequireVital(
            EnemyDestroyedNotification vital)
        {
            if (vital == null)
            {
                throw new ArgumentNullException(nameof(vital));
            }

            return vital;
        }
    }

    public sealed class EnemyDespawnedNotification : EnemyActorNotification
    {
        internal EnemyDespawnedNotification(
            StableId eventId,
            StableId sourceId,
            StableId targetId)
            : base(eventId, sourceId, targetId, EnemyContactPolicy.SystemChannelValue)
        {
        }
    }
}
