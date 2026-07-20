using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Domain.Modifiers.Events;

namespace ShooterMover.Application.Modifiers.Events
{
    /// <summary>
    /// Authoritative time port for event selection. Production composition may bind
    /// this to an offline-trusted clock today and to a server-provided instant later.
    /// Domain and application event logic never read local system time directly.
    /// </summary>
    public interface IAuthoritativeEventClockV1
    {
        long GetCurrentUnixTimeSeconds();
    }

    public enum ActiveEventProjectionStatusV1
    {
        Projected = 1,
        ConflictingActiveEvents = 2,
    }

    public sealed class ActiveEventProjectionResultV1
    {
        private readonly ReadOnlyCollection<SpecialEventConflictV1> conflicts;

        private ActiveEventProjectionResultV1(
            ActiveEventProjectionStatusV1 status,
            long evaluatedAtUnixSeconds,
            string catalogFingerprint,
            ActiveEventModifierSnapshotV1 snapshot,
            IEnumerable<SpecialEventConflictV1> conflicts)
        {
            if (!Enum.IsDefined(typeof(ActiveEventProjectionStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (string.IsNullOrWhiteSpace(catalogFingerprint))
            {
                throw new ArgumentException(
                    "A catalog fingerprint is required.",
                    nameof(catalogFingerprint));
            }

            Status = status;
            EvaluatedAtUnixSeconds = evaluatedAtUnixSeconds;
            CatalogFingerprint = catalogFingerprint;
            Snapshot = snapshot;
            var conflictCopy = new List<SpecialEventConflictV1>(
                conflicts ?? Array.Empty<SpecialEventConflictV1>());
            if (conflictCopy.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Event conflicts must be non-null.",
                    nameof(conflicts));
            }
            conflictCopy.Sort();
            this.conflicts = new ReadOnlyCollection<SpecialEventConflictV1>(
                conflictCopy);
            Fingerprint = EventProjectionCanonicalV1.Fingerprint(
                ToCanonicalString());
        }

        public ActiveEventProjectionStatusV1 Status { get; }

        public long EvaluatedAtUnixSeconds { get; }

        public string CatalogFingerprint { get; }

        public ActiveEventModifierSnapshotV1 Snapshot { get; }

        public IReadOnlyList<SpecialEventConflictV1> Conflicts
        {
            get { return conflicts; }
        }

        public string Fingerprint { get; }

        public bool Succeeded
        {
            get { return Status == ActiveEventProjectionStatusV1.Projected; }
        }

        public static ActiveEventProjectionResultV1 Projected(
            ActiveEventModifierSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new ActiveEventProjectionResultV1(
                ActiveEventProjectionStatusV1.Projected,
                snapshot.EvaluatedAtUnixSeconds,
                snapshot.CatalogFingerprint,
                snapshot,
                Array.Empty<SpecialEventConflictV1>());
        }

        public static ActiveEventProjectionResultV1 Rejected(
            long evaluatedAtUnixSeconds,
            string catalogFingerprint,
            IEnumerable<SpecialEventConflictV1> conflicts)
        {
            var copy = new List<SpecialEventConflictV1>(
                conflicts ?? throw new ArgumentNullException(nameof(conflicts)));
            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "A rejected event projection requires at least one conflict.",
                    nameof(conflicts));
            }

            return new ActiveEventProjectionResultV1(
                ActiveEventProjectionStatusV1.ConflictingActiveEvents,
                evaluatedAtUnixSeconds,
                catalogFingerprint,
                null,
                copy);
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            EventProjectionCanonicalV1.AppendToken(
                builder,
                "status",
                ((int)Status).ToString(CultureInfo.InvariantCulture));
            EventProjectionCanonicalV1.AppendToken(
                builder,
                "evaluated_at_unix_seconds",
                EvaluatedAtUnixSeconds.ToString(CultureInfo.InvariantCulture));
            EventProjectionCanonicalV1.AppendToken(
                builder,
                "catalog_fingerprint",
                CatalogFingerprint);
            EventProjectionCanonicalV1.AppendToken(
                builder,
                "snapshot_fingerprint",
                Snapshot == null ? "none" : Snapshot.Fingerprint);
            EventProjectionCanonicalV1.AppendToken(
                builder,
                "conflict_count",
                conflicts.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < conflicts.Count; index++)
            {
                EventProjectionCanonicalV1.AppendToken(
                    builder,
                    "conflict_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    conflicts[index].ToCanonicalString());
            }
            return builder.ToString();
        }
    }

    public sealed class ActiveEventModifierProjectionServiceV1
    {
        private const string ExplicitExclusionReason = "explicit-exclusion";
        private const string ExclusiveOverlapReason = "exclusive-overlap";

        private readonly SpecialEventCatalogV1 catalog;
        private readonly IAuthoritativeEventClockV1 clock;

        public ActiveEventModifierProjectionServiceV1(
            SpecialEventCatalogV1 catalog,
            IAuthoritativeEventClockV1 clock)
        {
            this.catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
            this.clock = clock
                ?? throw new ArgumentNullException(nameof(clock));
        }

        public ActiveEventProjectionResultV1 ProjectActiveEvents()
        {
            long instant = clock.GetCurrentUnixTimeSeconds();
            List<SpecialEventDefinitionV1> active = catalog.Definitions
                .Where(item => item.ActivationWindow.Contains(instant))
                .OrderByDescending(item => item.Priority)
                .ThenBy(item => item.EventId, StringComparer.Ordinal)
                .ToList();
            List<SpecialEventConflictV1> conflicts = DetectConflicts(active);
            if (conflicts.Count > 0)
            {
                return ActiveEventProjectionResultV1.Rejected(
                    instant,
                    catalog.Fingerprint,
                    conflicts);
            }

            return ActiveEventProjectionResultV1.Projected(
                ActiveEventModifierSnapshotV1.Create(
                    catalog,
                    instant,
                    active));
        }

        private static List<SpecialEventConflictV1> DetectConflicts(
            IReadOnlyList<SpecialEventDefinitionV1> active)
        {
            var conflicts = new List<SpecialEventConflictV1>();
            for (int leftIndex = 0; leftIndex < active.Count; leftIndex++)
            {
                SpecialEventDefinitionV1 left = active[leftIndex];
                for (int rightIndex = leftIndex + 1;
                    rightIndex < active.Count;
                    rightIndex++)
                {
                    SpecialEventDefinitionV1 right = active[rightIndex];
                    if (left.Excludes(right.EventId)
                        || right.Excludes(left.EventId))
                    {
                        conflicts.Add(new SpecialEventConflictV1(
                            left.EventId,
                            right.EventId,
                            ExplicitExclusionReason));
                        continue;
                    }

                    if (left.OverlapMode == SpecialEventOverlapModeV1.Exclusive
                        || right.OverlapMode == SpecialEventOverlapModeV1.Exclusive)
                    {
                        conflicts.Add(new SpecialEventConflictV1(
                            left.EventId,
                            right.EventId,
                            ExclusiveOverlapReason));
                    }
                }
            }

            conflicts.Sort();
            return conflicts;
        }
    }

    internal static class EventProjectionCanonicalV1
    {
        internal static void AppendToken(
            StringBuilder builder,
            string key,
            string value)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            string normalizedKey = key ?? string.Empty;
            string normalizedValue = value ?? string.Empty;
            builder.Append(normalizedKey.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(normalizedKey);
            builder.Append('=');
            builder.Append(normalizedValue.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(normalizedValue);
            builder.Append(';');
        }

        internal static string Fingerprint(string canonicalText)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(canonicalText ?? string.Empty));
                return BitConverter.ToString(bytes)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}
