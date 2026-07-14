using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Enemies
{
    public enum EnemyContactMode
    {
        None = 0,
        OrdinaryDamage = 1,
        DisposableImpact = 2,
    }

    public enum EnemyContactDecision
    {
        Accepted = 1,
        DuplicateWithinSimultaneousWindow = 2,
        GraceActive = 3,
        CapacityRejected = 4,
        ActorAlreadyDestroyed = 5,
    }

    public sealed class EnemyContactResolution
    {
        internal EnemyContactResolution(
            EnemyContactDecision decision,
            int contractResultValue,
            int weightResultValue,
            bool requestsMoverDamage,
            double moverDamageAmount)
        {
            Decision = decision;
            ContractResultValue = contractResultValue;
            WeightResultValue = weightResultValue;
            RequestsMoverDamage = requestsMoverDamage;
            MoverDamageAmount = moverDamageAmount;
        }

        public EnemyContactDecision Decision { get; }

        /// <summary>
        /// Frozen CS-004 ContactResult v1 numeric value.
        /// </summary>
        public int ContractResultValue { get; }

        /// <summary>
        /// Frozen CS-004 WeightResult v1 numeric value, oriented mover versus enemy.
        /// It is reported for the movement bridge but never mutates velocity here.
        /// </summary>
        public int WeightResultValue { get; }

        public bool RequestsMoverDamage { get; }

        public double MoverDamageAmount { get; }
    }

    /// <summary>
    /// Immutable, bounded per-mover contact grace. Weight is classified for the
    /// accepted movement boundary, while contact damage remains an enemy rule.
    /// </summary>
    public sealed class EnemyContactPolicy : IEquatable<EnemyContactPolicy>
    {
        public const int KineticChannelValue = 1;
        public const int ContactChannelValue = 5;
        public const int SystemChannelValue = 7;

        public const int ContactAcceptedResultValue = 1;
        public const int ContactGracePeriodIgnoredResultValue = 2;
        public const int ContactBlockedByWeightResultValue = 3;
        public const int ContactDuplicateEventIgnoredResultValue = 4;
        public const int ContactTargetAlreadyDestroyedResultValue = 5;

        public const int WeightSourceLighterResultValue = 1;
        public const int WeightEqualResultValue = 2;
        public const int WeightSourceHeavierResultValue = 3;
        public const int WeightTargetImmovableResultValue = 4;

        private readonly Entry[] entries;
        private readonly decimal graceDurationSeconds;
        private readonly decimal simultaneousWindowSeconds;
        private readonly decimal lastObservedAtSeconds;
        private readonly ReadOnlyCollection<StableId> trackedMoverIds;

        private EnemyContactPolicy(
            EnemyContactMode mode,
            double moverDamageAmount,
            decimal graceDurationSeconds,
            decimal simultaneousWindowSeconds,
            int capacity,
            decimal lastObservedAtSeconds,
            Entry[] entries)
        {
            if (!Enum.IsDefined(typeof(EnemyContactMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            if (mode == EnemyContactMode.None)
            {
                if (moverDamageAmount != 0d)
                {
                    throw new ArgumentException(
                        "A contact-disabled actor cannot request target damage.",
                        nameof(moverDamageAmount));
                }
            }
            else
            {
                EnemyActorState.RequireFinitePositive(moverDamageAmount, nameof(moverDamageAmount));
            }

            if (graceDurationSeconds <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(graceDurationSeconds));
            }

            if (simultaneousWindowSeconds < 0m
                || simultaneousWindowSeconds > graceDurationSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(simultaneousWindowSeconds));
            }

            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Mode = mode;
            MoverDamageAmount = moverDamageAmount;
            this.graceDurationSeconds = graceDurationSeconds;
            this.simultaneousWindowSeconds = simultaneousWindowSeconds;
            Capacity = capacity;
            this.lastObservedAtSeconds = lastObservedAtSeconds;
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            this.entries = entries;
            StableId[] ids = new StableId[entries.Length];
            for (int index = 0; index < entries.Length; index++)
            {
                ids[index] = entries[index].MoverId;
            }

            trackedMoverIds = Array.AsReadOnly(ids);
        }

        public EnemyContactMode Mode { get; }

        public double MoverDamageAmount { get; }

        public double GraceDurationSeconds
        {
            get { return (double)graceDurationSeconds; }
        }

        public double SimultaneousWindowSeconds
        {
            get { return (double)simultaneousWindowSeconds; }
        }

        public int Capacity { get; }

        public double LastObservedAtSeconds
        {
            get { return (double)lastObservedAtSeconds; }
        }

        public IReadOnlyList<StableId> TrackedMoverIds
        {
            get { return trackedMoverIds; }
        }

        public static EnemyContactPolicy Create(
            EnemyContactMode mode,
            double moverDamageAmount,
            double graceDurationSeconds,
            double simultaneousWindowSeconds,
            int capacity)
        {
            EnemyActorState.RequireFinitePositive(graceDurationSeconds, nameof(graceDurationSeconds));
            EnemyActorState.RequireFiniteNonNegative(
                simultaneousWindowSeconds,
                nameof(simultaneousWindowSeconds));

            return new EnemyContactPolicy(
                mode,
                moverDamageAmount,
                (decimal)graceDurationSeconds,
                (decimal)simultaneousWindowSeconds,
                capacity,
                0m,
                new Entry[0]);
        }

        public EnemyContactPolicy Register(
            StableId moverId,
            double observedAtSeconds,
            int moverWeightClassValue,
            int enemyWeightClassValue,
            out EnemyContactResolution resolution)
        {
            EnemyActorState.RequireId(moverId, nameof(moverId));
            EnemyActorState.RequireWeightClass(
                moverWeightClassValue,
                nameof(moverWeightClassValue));
            EnemyActorState.RequireWeightClass(
                enemyWeightClassValue,
                nameof(enemyWeightClassValue));
            EnemyActorState.RequireFiniteNonNegative(
                observedAtSeconds,
                nameof(observedAtSeconds));

            decimal observedAt = (decimal)observedAtSeconds;
            if (observedAt < lastObservedAtSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(observedAtSeconds),
                    observedAtSeconds,
                    "Enemy contact time must be monotonic.");
            }

            int weightResult = DetermineWeightResult(
                moverWeightClassValue,
                enemyWeightClassValue);
            Entry[] activeEntries = PurgeExpired(entries, observedAt);
            int existingIndex = FindEntryIndex(activeEntries, moverId);

            EnemyContactDecision decision;
            int contractResult;
            bool requestsDamage = false;

            if (existingIndex >= 0)
            {
                decimal elapsed = observedAt - activeEntries[existingIndex].AcceptedAtSeconds;
                if (elapsed <= simultaneousWindowSeconds)
                {
                    decision = EnemyContactDecision.DuplicateWithinSimultaneousWindow;
                    contractResult = ContactDuplicateEventIgnoredResultValue;
                }
                else
                {
                    decision = EnemyContactDecision.GraceActive;
                    contractResult = ContactGracePeriodIgnoredResultValue;
                }
            }
            else if (activeEntries.Length >= Capacity)
            {
                decision = EnemyContactDecision.CapacityRejected;
                contractResult = ContactGracePeriodIgnoredResultValue;
            }
            else
            {
                decision = EnemyContactDecision.Accepted;
                contractResult = ContactAcceptedResultValue;
                requestsDamage = Mode != EnemyContactMode.None;

                Entry accepted = new Entry(
                    moverId,
                    observedAt,
                    observedAt + graceDurationSeconds);
                activeEntries = InsertEntry(activeEntries, ~existingIndex, accepted);
            }

            resolution = new EnemyContactResolution(
                decision,
                contractResult,
                weightResult,
                requestsDamage,
                requestsDamage ? MoverDamageAmount : 0d);

            return new EnemyContactPolicy(
                Mode,
                MoverDamageAmount,
                graceDurationSeconds,
                simultaneousWindowSeconds,
                Capacity,
                observedAt,
                activeEntries);
        }

        public static int DetermineWeightResult(
            int moverWeightClassValue,
            int enemyWeightClassValue)
        {
            EnemyActorState.RequireWeightClass(
                moverWeightClassValue,
                nameof(moverWeightClassValue));
            EnemyActorState.RequireWeightClass(
                enemyWeightClassValue,
                nameof(enemyWeightClassValue));

            if (enemyWeightClassValue == 4 && moverWeightClassValue != 4)
            {
                return WeightTargetImmovableResultValue;
            }

            if (moverWeightClassValue < enemyWeightClassValue)
            {
                return WeightSourceLighterResultValue;
            }

            if (moverWeightClassValue > enemyWeightClassValue)
            {
                return WeightSourceHeavierResultValue;
            }

            return WeightEqualResultValue;
        }

        public bool Equals(EnemyContactPolicy other)
        {
            if (other == null
                || Mode != other.Mode
                || MoverDamageAmount != other.MoverDamageAmount
                || graceDurationSeconds != other.graceDurationSeconds
                || simultaneousWindowSeconds != other.simultaneousWindowSeconds
                || Capacity != other.Capacity
                || lastObservedAtSeconds != other.lastObservedAtSeconds
                || entries.Length != other.entries.Length)
            {
                return false;
            }

            for (int index = 0; index < entries.Length; index++)
            {
                if (!entries[index].Equals(other.entries[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EnemyContactPolicy);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + Mode.GetHashCode();
                hash = (hash * 31) + MoverDamageAmount.GetHashCode();
                hash = (hash * 31) + graceDurationSeconds.GetHashCode();
                hash = (hash * 31) + simultaneousWindowSeconds.GetHashCode();
                hash = (hash * 31) + Capacity;
                hash = (hash * 31) + lastObservedAtSeconds.GetHashCode();
                for (int index = 0; index < entries.Length; index++)
                {
                    hash = (hash * 31) + entries[index].GetHashCode();
                }

                return hash;
            }
        }

        private static Entry[] PurgeExpired(Entry[] source, decimal observedAtSeconds)
        {
            List<Entry> active = new List<Entry>(source.Length);
            foreach (Entry entry in source)
            {
                if (observedAtSeconds < entry.ExpiresAtSeconds)
                {
                    active.Add(entry);
                }
            }

            return active.ToArray();
        }

        private static int FindEntryIndex(Entry[] source, StableId moverId)
        {
            int low = 0;
            int high = source.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                int comparison = source[middle].MoverId.CompareTo(moverId);
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

        private static Entry[] InsertEntry(Entry[] source, int insertionIndex, Entry entry)
        {
            Entry[] next = new Entry[source.Length + 1];
            if (insertionIndex > 0)
            {
                Array.Copy(source, 0, next, 0, insertionIndex);
            }

            next[insertionIndex] = entry;
            if (insertionIndex < source.Length)
            {
                Array.Copy(
                    source,
                    insertionIndex,
                    next,
                    insertionIndex + 1,
                    source.Length - insertionIndex);
            }

            return next;
        }

        private sealed class Entry : IEquatable<Entry>
        {
            public Entry(
                StableId moverId,
                decimal acceptedAtSeconds,
                decimal expiresAtSeconds)
            {
                MoverId = EnemyActorState.RequireId(moverId, nameof(moverId));
                AcceptedAtSeconds = acceptedAtSeconds;
                ExpiresAtSeconds = expiresAtSeconds;
            }

            public StableId MoverId { get; }

            public decimal AcceptedAtSeconds { get; }

            public decimal ExpiresAtSeconds { get; }

            public bool Equals(Entry other)
            {
                return other != null
                    && MoverId == other.MoverId
                    && AcceptedAtSeconds == other.AcceptedAtSeconds
                    && ExpiresAtSeconds == other.ExpiresAtSeconds;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as Entry);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 31) + MoverId.GetHashCode();
                    hash = (hash * 31) + AcceptedAtSeconds.GetHashCode();
                    hash = (hash * 31) + ExpiresAtSeconds.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
