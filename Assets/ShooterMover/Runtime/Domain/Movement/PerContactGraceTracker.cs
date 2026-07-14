using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Movement
{
    /// <summary>
    /// Explicit result of observing one enemy StableId at a contact boundary.
    /// </summary>
    public enum ContactGraceDecision
    {
        Accepted = 1,
        DuplicateWithinSimultaneousWindow = 2,
        GraceActive = 3,
        CapacityRejected = 4,
    }

    /// <summary>
    /// Immutable per-contact decision emitted in canonical StableId order.
    /// </summary>
    public sealed class ContactGraceRegistration : IEquatable<ContactGraceRegistration>
    {
        internal ContactGraceRegistration(
            StableId enemyId,
            ContactGraceDecision decision,
            decimal observedAtSeconds,
            bool hasGraceExpiry,
            decimal graceExpiresAtSeconds)
        {
            if (enemyId == null)
            {
                throw new ArgumentNullException(nameof(enemyId));
            }

            if (!Enum.IsDefined(typeof(ContactGraceDecision), decision))
            {
                throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown contact-grace decision.");
            }

            if (observedAtSeconds < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(observedAtSeconds),
                    observedAtSeconds,
                    "Contact time cannot be negative.");
            }

            if (hasGraceExpiry && graceExpiresAtSeconds <= observedAtSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(graceExpiresAtSeconds),
                    graceExpiresAtSeconds,
                    "An active grace expiry must be later than the observed boundary.");
            }

            EnemyId = enemyId;
            Decision = decision;
            ObservedAtSeconds = (double)observedAtSeconds;
            HasGraceExpiry = hasGraceExpiry;
            GraceExpiresAtSeconds = hasGraceExpiry ? (double)graceExpiresAtSeconds : 0d;
        }

        public StableId EnemyId { get; }

        public ContactGraceDecision Decision { get; }

        public double ObservedAtSeconds { get; }

        public bool HasGraceExpiry { get; }

        public double GraceExpiresAtSeconds { get; }

        public bool ContactAccepted
        {
            get { return Decision == ContactGraceDecision.Accepted; }
        }

        public bool Equals(ContactGraceRegistration other)
        {
            return other != null
                && EnemyId == other.EnemyId
                && Decision == other.Decision
                && ObservedAtSeconds.Equals(other.ObservedAtSeconds)
                && HasGraceExpiry == other.HasGraceExpiry
                && GraceExpiresAtSeconds.Equals(other.GraceExpiresAtSeconds);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ContactGraceRegistration);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + EnemyId.GetHashCode();
                hash = (hash * 31) + Decision.GetHashCode();
                hash = (hash * 31) + ObservedAtSeconds.GetHashCode();
                hash = (hash * 31) + HasGraceExpiry.GetHashCode();
                hash = (hash * 31) + GraceExpiresAtSeconds.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// Immutable, StableId-keyed contact-grace state with deterministic ordering and a hard capacity.
    /// Exact expiry boundaries are eligible again. Batch inputs are sorted before resolution so many
    /// contacts observed in one step produce byte-for-byte equivalent state regardless of input order.
    /// </summary>
    public sealed class PerContactGraceTracker : IEquatable<PerContactGraceTracker>
    {
        private readonly Entry[] entries;
        private readonly decimal graceDurationSeconds;
        private readonly decimal simultaneousWindowSeconds;
        private readonly decimal lastObservedAtSeconds;

        private PerContactGraceTracker(
            StableId tuningIdentity,
            decimal graceDurationSeconds,
            decimal simultaneousWindowSeconds,
            int capacity,
            decimal lastObservedAtSeconds,
            Entry[] entries)
        {
            TuningIdentity = tuningIdentity;
            this.graceDurationSeconds = graceDurationSeconds;
            this.simultaneousWindowSeconds = simultaneousWindowSeconds;
            Capacity = capacity;
            this.lastObservedAtSeconds = lastObservedAtSeconds;
            this.entries = entries;
        }

        public StableId TuningIdentity { get; }

        public int Capacity { get; }

        public int Count
        {
            get { return entries.Length; }
        }

        public double GraceDurationSeconds
        {
            get { return (double)graceDurationSeconds; }
        }

        public double SimultaneousWindowSeconds
        {
            get { return (double)simultaneousWindowSeconds; }
        }

        public double LastObservedAtSeconds
        {
            get { return (double)lastObservedAtSeconds; }
        }

        public static PerContactGraceTracker Create(MovementThrusterTuningProfile tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            MovementThrusterTuningProfileValidator.Validate(tuning);
            return new PerContactGraceTracker(
                tuning.DeterministicIdentity,
                (decimal)tuning.PerEnemyContactGraceSeconds,
                (decimal)tuning.SimultaneousContactWindowSeconds,
                tuning.ContactGraceCapacity,
                0m,
                new Entry[0]);
        }

        /// <summary>
        /// Registers one enemy contact at an absolute monotonic simulation time.
        /// </summary>
        public PerContactGraceTracker Register(
            StableId enemyId,
            double observedAtSeconds,
            out ContactGraceDecision decision)
        {
            ContactGraceRegistration[] registrations;
            PerContactGraceTracker next = RegisterMany(
                new[] { enemyId },
                observedAtSeconds,
                out registrations);
            decision = registrations[0].Decision;
            return next;
        }

        /// <summary>
        /// Registers a same-step contact set in canonical StableId order. Duplicate IDs remain present
        /// in the result so the first occurrence may be accepted and later occurrences are explicitly
        /// identified as simultaneous duplicates.
        /// </summary>
        public PerContactGraceTracker RegisterMany(
            IEnumerable<StableId> enemyIds,
            double observedAtSeconds,
            out ContactGraceRegistration[] registrations)
        {
            if (enemyIds == null)
            {
                throw new ArgumentNullException(nameof(enemyIds));
            }

            decimal observedAt = ConvertObservedTime(observedAtSeconds);
            if (observedAt < lastObservedAtSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(observedAtSeconds),
                    observedAtSeconds,
                    "Contact-grace time must be monotonic.");
            }

            List<StableId> orderedEnemyIds = new List<StableId>();
            foreach (StableId enemyId in enemyIds)
            {
                if (enemyId == null)
                {
                    throw new ArgumentException("Contact enemy IDs cannot contain null.", nameof(enemyIds));
                }

                orderedEnemyIds.Add(enemyId);
            }

            orderedEnemyIds.Sort();
            Entry[] workingEntries = PurgeExpired(entries, observedAt);
            registrations = new ContactGraceRegistration[orderedEnemyIds.Count];

            for (int index = 0; index < orderedEnemyIds.Count; index++)
            {
                StableId enemyId = orderedEnemyIds[index];
                int entryIndex = FindEntryIndex(workingEntries, enemyId);
                if (entryIndex >= 0)
                {
                    Entry active = workingEntries[entryIndex];
                    decimal elapsed = observedAt - active.AcceptedAtSeconds;
                    ContactGraceDecision activeDecision = elapsed <= simultaneousWindowSeconds
                        ? ContactGraceDecision.DuplicateWithinSimultaneousWindow
                        : ContactGraceDecision.GraceActive;
                    registrations[index] = new ContactGraceRegistration(
                        enemyId,
                        activeDecision,
                        observedAt,
                        true,
                        active.ExpiresAtSeconds);
                    continue;
                }

                if (workingEntries.Length >= Capacity)
                {
                    registrations[index] = new ContactGraceRegistration(
                        enemyId,
                        ContactGraceDecision.CapacityRejected,
                        observedAt,
                        false,
                        0m);
                    continue;
                }

                int insertionIndex = ~entryIndex;
                Entry accepted = new Entry(
                    enemyId,
                    observedAt,
                    observedAt + graceDurationSeconds);
                workingEntries = InsertEntry(workingEntries, insertionIndex, accepted);
                registrations[index] = new ContactGraceRegistration(
                    enemyId,
                    ContactGraceDecision.Accepted,
                    observedAt,
                    true,
                    accepted.ExpiresAtSeconds);
            }

            return new PerContactGraceTracker(
                TuningIdentity,
                graceDurationSeconds,
                simultaneousWindowSeconds,
                Capacity,
                observedAt,
                workingEntries);
        }

        public bool IsGraceActive(StableId enemyId, double observedAtSeconds)
        {
            if (enemyId == null)
            {
                throw new ArgumentNullException(nameof(enemyId));
            }

            decimal observedAt = ConvertObservedTime(observedAtSeconds);
            if (observedAt < lastObservedAtSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(observedAtSeconds),
                    observedAtSeconds,
                    "Contact-grace queries cannot move backward in time.");
            }

            int index = FindEntryIndex(entries, enemyId);
            return index >= 0 && observedAt < entries[index].ExpiresAtSeconds;
        }

        public StableId GetTrackedEnemyId(int index)
        {
            ValidateIndex(index);
            return entries[index].EnemyId;
        }

        public double GetGraceExpiresAtSeconds(int index)
        {
            ValidateIndex(index);
            return (double)entries[index].ExpiresAtSeconds;
        }

        public bool Equals(PerContactGraceTracker other)
        {
            if (other == null
                || TuningIdentity != other.TuningIdentity
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
            return Equals(obj as PerContactGraceTracker);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + TuningIdentity.GetHashCode();
                hash = (hash * 31) + graceDurationSeconds.GetHashCode();
                hash = (hash * 31) + simultaneousWindowSeconds.GetHashCode();
                hash = (hash * 31) + Capacity.GetHashCode();
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
            int activeCount = 0;
            for (int index = 0; index < source.Length; index++)
            {
                if (observedAtSeconds < source[index].ExpiresAtSeconds)
                {
                    activeCount++;
                }
            }

            if (activeCount == source.Length)
            {
                return source;
            }

            Entry[] active = new Entry[activeCount];
            int targetIndex = 0;
            for (int sourceIndex = 0; sourceIndex < source.Length; sourceIndex++)
            {
                if (observedAtSeconds < source[sourceIndex].ExpiresAtSeconds)
                {
                    active[targetIndex] = source[sourceIndex];
                    targetIndex++;
                }
            }

            return active;
        }

        private static int FindEntryIndex(Entry[] source, StableId enemyId)
        {
            int low = 0;
            int high = source.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                int comparison = source[middle].EnemyId.CompareTo(enemyId);
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
            Entry[] expanded = new Entry[source.Length + 1];
            if (insertionIndex > 0)
            {
                Array.Copy(source, 0, expanded, 0, insertionIndex);
            }

            expanded[insertionIndex] = entry;
            if (insertionIndex < source.Length)
            {
                Array.Copy(
                    source,
                    insertionIndex,
                    expanded,
                    insertionIndex + 1,
                    source.Length - insertionIndex);
            }

            return expanded;
        }

        private static decimal ConvertObservedTime(double observedAtSeconds)
        {
            if (double.IsNaN(observedAtSeconds)
                || double.IsInfinity(observedAtSeconds)
                || observedAtSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(observedAtSeconds),
                    observedAtSeconds,
                    "Contact-grace time must be finite and non-negative.");
            }

            try
            {
                return (decimal)observedAtSeconds;
            }
            catch (OverflowException exception)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(observedAtSeconds),
                    observedAtSeconds,
                    "Contact-grace time is outside the supported deterministic range.",
                    exception);
            }
        }

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= entries.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Tracked contact index is out of range.");
            }
        }

        private struct Entry : IEquatable<Entry>
        {
            public Entry(StableId enemyId, decimal acceptedAtSeconds, decimal expiresAtSeconds)
            {
                EnemyId = enemyId;
                AcceptedAtSeconds = acceptedAtSeconds;
                ExpiresAtSeconds = expiresAtSeconds;
            }

            public StableId EnemyId { get; }

            public decimal AcceptedAtSeconds { get; }

            public decimal ExpiresAtSeconds { get; }

            public bool Equals(Entry other)
            {
                return EnemyId == other.EnemyId
                    && AcceptedAtSeconds == other.AcceptedAtSeconds
                    && ExpiresAtSeconds == other.ExpiresAtSeconds;
            }

            public override bool Equals(object obj)
            {
                return obj is Entry && Equals((Entry)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 31) + EnemyId.GetHashCode();
                    hash = (hash * 31) + AcceptedAtSeconds.GetHashCode();
                    hash = (hash * 31) + ExpiresAtSeconds.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
