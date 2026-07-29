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
    public interface IAuthoritativeEventClock
    {
        long GetCurrentUnixTimeSeconds();
    }

    public enum ActiveEventViewStatus
    {
        Projected = 1,
        ConflictingActiveEvents = 2,
    }

    public sealed class ActiveEventViewResult
    {
        private readonly ReadOnlyCollection<SpecialEventConflict> conflicts;

        private ActiveEventViewResult(
            ActiveEventViewStatus status,
            long evaluatedAtUnixSeconds,
            string catalogFingerprint,
            ActiveEventModifierSnapshot snapshot,
            IEnumerable<SpecialEventConflict> conflicts)
        {
            if (!Enum.IsDefined(typeof(ActiveEventViewStatus), status))
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
            var conflictCopy = new List<SpecialEventConflict>(
                conflicts ?? Array.Empty<SpecialEventConflict>());
            if (conflictCopy.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Event conflicts must be non-null.",
                    nameof(conflicts));
            }
            conflictCopy.Sort();
            this.conflicts = new ReadOnlyCollection<SpecialEventConflict>(
                conflictCopy);
            Fingerprint = EventView.Fingerprint(
                ToCanonicalString());
        }

        public ActiveEventViewStatus Status { get; }

        public long EvaluatedAtUnixSeconds { get; }

        public string CatalogFingerprint { get; }

        public ActiveEventModifierSnapshot Snapshot { get; }

        public IReadOnlyList<SpecialEventConflict> Conflicts
        {
            get { return conflicts; }
        }

        public string Fingerprint { get; }

        public bool Succeeded
        {
            get { return Status == ActiveEventViewStatus.Projected; }
        }

        public static ActiveEventViewResult Projected(
            ActiveEventModifierSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new ActiveEventViewResult(
                ActiveEventViewStatus.Projected,
                snapshot.EvaluatedAtUnixSeconds,
                snapshot.CatalogFingerprint,
                snapshot,
                Array.Empty<SpecialEventConflict>());
        }

        public static ActiveEventViewResult Rejected(
            long evaluatedAtUnixSeconds,
            string catalogFingerprint,
            IEnumerable<SpecialEventConflict> conflicts)
        {
            var copy = new List<SpecialEventConflict>(
                conflicts ?? throw new ArgumentNullException(nameof(conflicts)));
            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "A rejected event projection requires at least one conflict.",
                    nameof(conflicts));
            }

            return new ActiveEventViewResult(
                ActiveEventViewStatus.ConflictingActiveEvents,
                evaluatedAtUnixSeconds,
                catalogFingerprint,
                null,
                copy);
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            EventView.AppendToken(
                builder,
                "status",
                ((int)Status).ToString(CultureInfo.InvariantCulture));
            EventView.AppendToken(
                builder,
                "evaluated_at_unix_seconds",
                EvaluatedAtUnixSeconds.ToString(CultureInfo.InvariantCulture));
            EventView.AppendToken(
                builder,
                "catalog_fingerprint",
                CatalogFingerprint);
            EventView.AppendToken(
                builder,
                "snapshot_fingerprint",
                Snapshot == null ? "none" : Snapshot.Fingerprint);
            EventView.AppendToken(
                builder,
                "conflict_count",
                conflicts.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < conflicts.Count; index++)
            {
                EventView.AppendToken(
                    builder,
                    "conflict_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    conflicts[index].ToCanonicalString());
            }
            return builder.ToString();
        }
    }

    public sealed class ActiveEventModifierViewActions
    {
        private const string ExplicitExclusionReason = "explicit-exclusion";
        private const string ExclusiveOverlapReason = "exclusive-overlap";

        private readonly SpecialEventCatalog catalog;
        private readonly IAuthoritativeEventClock clock;

        public ActiveEventModifierViewActions(
            SpecialEventCatalog catalog,
            IAuthoritativeEventClock clock)
        {
            this.catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
            this.clock = clock
                ?? throw new ArgumentNullException(nameof(clock));
        }

        public ActiveEventViewResult ProjectActiveEvents()
        {
            long instant = clock.GetCurrentUnixTimeSeconds();
            List<SpecialEventDefinition> active = catalog.Definitions
                .Where(item => item.ActivationWindow.Contains(instant))
                .OrderByDescending(item => item.Priority)
                .ThenBy(item => item.EventId, StringComparer.Ordinal)
                .ToList();
            List<SpecialEventConflict> conflicts = DetectConflicts(active);
            if (conflicts.Count > 0)
            {
                return ActiveEventViewResult.Rejected(
                    instant,
                    catalog.Fingerprint,
                    conflicts);
            }

            return ActiveEventViewResult.Projected(
                ActiveEventModifierSnapshot.Create(
                    catalog,
                    instant,
                    active));
        }

        private static List<SpecialEventConflict> DetectConflicts(
            IReadOnlyList<SpecialEventDefinition> active)
        {
            var conflicts = new List<SpecialEventConflict>();
            for (int leftIndex = 0; leftIndex < active.Count; leftIndex++)
            {
                SpecialEventDefinition left = active[leftIndex];
                for (int rightIndex = leftIndex + 1;
                    rightIndex < active.Count;
                    rightIndex++)
                {
                    SpecialEventDefinition right = active[rightIndex];
                    if (left.Excludes(right.EventId)
                        || right.Excludes(left.EventId))
                    {
                        conflicts.Add(new SpecialEventConflict(
                            left.EventId,
                            right.EventId,
                            ExplicitExclusionReason));
                        continue;
                    }

                    if (left.OverlapMode == SpecialEventOverlapMode.Exclusive
                        || right.OverlapMode == SpecialEventOverlapMode.Exclusive)
                    {
                        conflicts.Add(new SpecialEventConflict(
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

    internal static class EventView
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
