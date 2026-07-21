using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Domain.Modifiers;

namespace ShooterMover.Domain.Modifiers.Events
{
    public static class EventModifierTargetIdsV1
    {
        public const string RewardStrongboxWeight = "rewards.strongbox-drop-weight";
        public const string MoneyQuantity = "rewards.money-quantity";
        public const string ExperienceQuantity = "rewards.xp-quantity";
    }

    public enum SpecialEventOverlapModeV1
    {
        Combine = 1,
        Exclusive = 2,
    }

    public sealed class EventActivationWindowV1
    {
        public EventActivationWindowV1(
            long startUnixSecondsInclusive,
            long endUnixSecondsExclusive)
        {
            if (endUnixSecondsExclusive <= startUnixSecondsInclusive)
            {
                throw new ArgumentException(
                    "An event activation window must have a positive duration.");
            }

            StartUnixSecondsInclusive = startUnixSecondsInclusive;
            EndUnixSecondsExclusive = endUnixSecondsExclusive;
            Fingerprint = RuntimeModifierFingerprintV1.Hash(
                ToCanonicalString());
        }

        public long StartUnixSecondsInclusive { get; }

        public long EndUnixSecondsExclusive { get; }

        public string Fingerprint { get; }

        public bool Contains(long unixSeconds)
        {
            return unixSeconds >= StartUnixSecondsInclusive
                && unixSeconds < EndUnixSecondsExclusive;
        }

        public string ToCanonicalString()
        {
            return StartUnixSecondsInclusive.ToString(CultureInfo.InvariantCulture)
                + "|"
                + EndUnixSecondsExclusive.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class EventModifierDescriptorV1
    {
        public EventModifierDescriptorV1(
            string targetId,
            RuntimeModifierOperationV1 operation,
            decimal value,
            string conditionId = "")
        {
            var validation = new RuntimeModifierDefinitionV1(
                "event.descriptor.validation",
                targetId,
                operation,
                value,
                conditionId);

            TargetId = validation.TargetId;
            Operation = validation.Operation;
            Value = validation.Value;
            ConditionId = validation.ConditionId;
            Fingerprint = RuntimeModifierFingerprintV1.Hash(
                ToCanonicalString());
        }

        public string TargetId { get; }

        public RuntimeModifierOperationV1 Operation { get; }

        public decimal Value { get; }

        public string ConditionId { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return TargetId
                + "|"
                + Operation
                + "|"
                + Value.ToString(CultureInfo.InvariantCulture)
                + "|"
                + ConditionId;
        }
    }

    public sealed class SpecialEventDefinitionV1
    {
        public const int CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<string> excludedEventIds;
        private readonly ReadOnlyCollection<EventModifierDescriptorV1> modifiers;

        public SpecialEventDefinitionV1(
            int schemaVersion,
            string contentVersion,
            string eventId,
            EventActivationWindowV1 activationWindow,
            int priority,
            SpecialEventOverlapModeV1 overlapMode,
            IEnumerable<EventModifierDescriptorV1> modifiers,
            IEnumerable<string> excludedEventIds = null)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion),
                    "Only the current special-event schema is supported.");
            }
            if (string.IsNullOrWhiteSpace(contentVersion))
            {
                throw new ArgumentException(
                    "An event content version is required.",
                    nameof(contentVersion));
            }
            if (string.IsNullOrWhiteSpace(eventId))
            {
                throw new ArgumentException(
                    "A stable event identity is required.",
                    nameof(eventId));
            }
            if (priority < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(priority));
            }
            if (!Enum.IsDefined(typeof(SpecialEventOverlapModeV1), overlapMode))
            {
                throw new ArgumentOutOfRangeException(nameof(overlapMode));
            }

            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion.Trim();
            EventId = eventId.Trim();
            ActivationWindow = activationWindow
                ?? throw new ArgumentNullException(nameof(activationWindow));
            Priority = priority;
            OverlapMode = overlapMode;

            var exclusionCopy = new List<string>();
            foreach (string excludedId in excludedEventIds
                ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(excludedId))
                {
                    throw new ArgumentException(
                        "Excluded event identities must be non-empty.",
                        nameof(excludedEventIds));
                }

                string normalized = excludedId.Trim();
                if (string.Equals(
                    normalized,
                    EventId,
                    StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "An event cannot exclude itself.",
                        nameof(excludedEventIds));
                }
                exclusionCopy.Add(normalized);
            }

            if (exclusionCopy.Count != exclusionCopy
                .Distinct(StringComparer.Ordinal)
                .Count())
            {
                throw new ArgumentException(
                    "Excluded event identities must be unique.",
                    nameof(excludedEventIds));
            }
            exclusionCopy.Sort(StringComparer.Ordinal);
            this.excludedEventIds = new ReadOnlyCollection<string>(
                exclusionCopy);

            var modifierCopy = new List<EventModifierDescriptorV1>(
                modifiers
                    ?? throw new ArgumentNullException(nameof(modifiers)));
            if (modifierCopy.Count == 0)
            {
                throw new ArgumentException(
                    "A special event must declare at least one modifier.",
                    nameof(modifiers));
            }
            if (modifierCopy.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Event modifier descriptors must be non-null.",
                    nameof(modifiers));
            }
            if (modifierCopy.Select(item => item.Fingerprint)
                .Distinct(StringComparer.Ordinal)
                .Count() != modifierCopy.Count)
            {
                throw new ArgumentException(
                    "Duplicate event modifier descriptors are not allowed.",
                    nameof(modifiers));
            }

            modifierCopy = modifierCopy
                .OrderBy(item => item.TargetId, StringComparer.Ordinal)
                .ThenBy(item => item.ConditionId, StringComparer.Ordinal)
                .ThenBy(item => item.Operation)
                .ThenBy(item => item.Value)
                .ToList();
            this.modifiers = new ReadOnlyCollection<EventModifierDescriptorV1>(
                modifierCopy);
            Fingerprint = RuntimeModifierFingerprintV1.Hash(
                ToCanonicalString());
        }

        public int SchemaVersion { get; }

        public string ContentVersion { get; }

        public string EventId { get; }

        public EventActivationWindowV1 ActivationWindow { get; }

        public int Priority { get; }

        public SpecialEventOverlapModeV1 OverlapMode { get; }

        public IReadOnlyList<string> ExcludedEventIds
        {
            get { return excludedEventIds; }
        }

        public IReadOnlyList<EventModifierDescriptorV1> Modifiers
        {
            get { return modifiers; }
        }

        public string Fingerprint { get; }

        public bool Excludes(string otherEventId)
        {
            return excludedEventIds.Contains(
                otherEventId,
                StringComparer.Ordinal);
        }

        public IEnumerable<RuntimeModifierDefinitionV1> ProjectModifiers()
        {
            return modifiers.Select(item => new RuntimeModifierDefinitionV1(
                EventId,
                item.TargetId,
                item.Operation,
                item.Value,
                item.ConditionId));
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            EventModifierCanonicalV1.AppendToken(
                builder,
                "schema_version",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            EventModifierCanonicalV1.AppendToken(
                builder,
                "content_version",
                ContentVersion);
            EventModifierCanonicalV1.AppendToken(builder, "event_id", EventId);
            EventModifierCanonicalV1.AppendToken(
                builder,
                "activation_window",
                ActivationWindow.ToCanonicalString());
            EventModifierCanonicalV1.AppendToken(
                builder,
                "priority",
                Priority.ToString(CultureInfo.InvariantCulture));
            EventModifierCanonicalV1.AppendToken(
                builder,
                "overlap_mode",
                ((int)OverlapMode).ToString(CultureInfo.InvariantCulture));
            EventModifierCanonicalV1.AppendToken(
                builder,
                "excluded_count",
                excludedEventIds.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < excludedEventIds.Count; index++)
            {
                EventModifierCanonicalV1.AppendToken(
                    builder,
                    "excluded_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    excludedEventIds[index]);
            }
            EventModifierCanonicalV1.AppendToken(
                builder,
                "modifier_count",
                modifiers.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < modifiers.Count; index++)
            {
                EventModifierCanonicalV1.AppendToken(
                    builder,
                    "modifier_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    modifiers[index].ToCanonicalString());
            }
            return builder.ToString();
        }
    }

    public sealed class SpecialEventCatalogV1
    {
        public const int CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<SpecialEventDefinitionV1> definitions;

        public SpecialEventCatalogV1(
            string contentVersion,
            IEnumerable<SpecialEventDefinitionV1> definitions)
        {
            if (string.IsNullOrWhiteSpace(contentVersion))
            {
                throw new ArgumentException(
                    "A catalog content version is required.",
                    nameof(contentVersion));
            }

            ContentVersion = contentVersion.Trim();
            var copy = new List<SpecialEventDefinitionV1>(
                definitions
                    ?? throw new ArgumentNullException(nameof(definitions)));
            if (copy.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Special-event definitions must be non-null.",
                    nameof(definitions));
            }
            if (copy.Select(item => item.EventId)
                .Distinct(StringComparer.Ordinal)
                .Count() != copy.Count)
            {
                throw new ArgumentException(
                    "Special-event identities must be unique.",
                    nameof(definitions));
            }

            var knownIds = new HashSet<string>(
                copy.Select(item => item.EventId),
                StringComparer.Ordinal);
            foreach (SpecialEventDefinitionV1 definition in copy)
            {
                foreach (string excludedEventId in definition.ExcludedEventIds)
                {
                    if (!knownIds.Contains(excludedEventId))
                    {
                        throw new ArgumentException(
                            "Event '"
                                + definition.EventId
                                + "' excludes unknown event '"
                                + excludedEventId
                                + "'.",
                            nameof(definitions));
                    }
                }
            }

            copy.Sort((left, right) => string.CompareOrdinal(
                left.EventId,
                right.EventId));
            this.definitions = new ReadOnlyCollection<SpecialEventDefinitionV1>(
                copy);
            Fingerprint = RuntimeModifierFingerprintV1.Hash(
                ToCanonicalString());
        }

        public int SchemaVersion
        {
            get { return CurrentSchemaVersion; }
        }

        public string ContentVersion { get; }

        public IReadOnlyList<SpecialEventDefinitionV1> Definitions
        {
            get { return definitions; }
        }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            EventModifierCanonicalV1.AppendToken(
                builder,
                "schema_version",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            EventModifierCanonicalV1.AppendToken(
                builder,
                "content_version",
                ContentVersion);
            EventModifierCanonicalV1.AppendToken(
                builder,
                "definition_count",
                definitions.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < definitions.Count; index++)
            {
                EventModifierCanonicalV1.AppendToken(
                    builder,
                    "definition_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    definitions[index].ToCanonicalString());
            }
            return builder.ToString();
        }
    }

    public sealed class ActiveEventDescriptorV1
        : IComparable<ActiveEventDescriptorV1>
    {
        public ActiveEventDescriptorV1(SpecialEventDefinitionV1 definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            EventId = definition.EventId;
            ContentVersion = definition.ContentVersion;
            Priority = definition.Priority;
            DefinitionFingerprint = definition.Fingerprint;
            Fingerprint = RuntimeModifierFingerprintV1.Hash(
                ToCanonicalString());
        }

        public string EventId { get; }

        public string ContentVersion { get; }

        public int Priority { get; }

        public string DefinitionFingerprint { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return EventId
                + "|"
                + ContentVersion
                + "|"
                + Priority.ToString(CultureInfo.InvariantCulture)
                + "|"
                + DefinitionFingerprint;
        }

        public int CompareTo(ActiveEventDescriptorV1 other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int priorityComparison = other.Priority.CompareTo(Priority);
            return priorityComparison != 0
                ? priorityComparison
                : string.CompareOrdinal(EventId, other.EventId);
        }
    }

    public sealed class ActiveEventModifierSnapshotV1
    {
        public const int CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<ActiveEventDescriptorV1> activeEvents;

        private ActiveEventModifierSnapshotV1(
            string catalogContentVersion,
            string catalogFingerprint,
            long evaluatedAtUnixSeconds,
            IEnumerable<ActiveEventDescriptorV1> activeEvents,
            RuntimeModifierSnapshotV1 modifierSnapshot,
            string fingerprint)
        {
            CatalogContentVersion = catalogContentVersion;
            CatalogFingerprint = catalogFingerprint;
            EvaluatedAtUnixSeconds = evaluatedAtUnixSeconds;
            this.activeEvents = new ReadOnlyCollection<ActiveEventDescriptorV1>(
                new List<ActiveEventDescriptorV1>(activeEvents));
            ModifierSnapshot = modifierSnapshot;
            Fingerprint = fingerprint;
        }

        public int SchemaVersion
        {
            get { return CurrentSchemaVersion; }
        }

        public string CatalogContentVersion { get; }

        public string CatalogFingerprint { get; }

        public long EvaluatedAtUnixSeconds { get; }

        public IReadOnlyList<ActiveEventDescriptorV1> ActiveEvents
        {
            get { return activeEvents; }
        }

        public RuntimeModifierSnapshotV1 ModifierSnapshot { get; }

        public string Fingerprint { get; }

        public static ActiveEventModifierSnapshotV1 Create(
            SpecialEventCatalogV1 catalog,
            long evaluatedAtUnixSeconds,
            IEnumerable<SpecialEventDefinitionV1> activeDefinitions)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var definitions = new List<SpecialEventDefinitionV1>(
                activeDefinitions
                    ?? throw new ArgumentNullException(nameof(activeDefinitions)));
            if (definitions.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Active event definitions must be non-null.",
                    nameof(activeDefinitions));
            }

            var active = definitions
                .Select(item => new ActiveEventDescriptorV1(item))
                .OrderBy(item => item)
                .ToList();
            var modifierSnapshot = new RuntimeModifierSnapshotV1(
                definitions.SelectMany(item => item.ProjectModifiers()));
            var provisional = new ActiveEventModifierSnapshotV1(
                catalog.ContentVersion,
                catalog.Fingerprint,
                evaluatedAtUnixSeconds,
                active,
                modifierSnapshot,
                string.Empty);
            string fingerprint = RuntimeModifierFingerprintV1.Hash(
                provisional.ToCanonicalString());
            return new ActiveEventModifierSnapshotV1(
                provisional.CatalogContentVersion,
                provisional.CatalogFingerprint,
                provisional.EvaluatedAtUnixSeconds,
                provisional.ActiveEvents,
                provisional.ModifierSnapshot,
                fingerprint);
        }

        public FrozenEventModifierContextV1 FreezeForCommand()
        {
            return new FrozenEventModifierContextV1(this);
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            EventModifierCanonicalV1.AppendToken(
                builder,
                "schema_version",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            EventModifierCanonicalV1.AppendToken(
                builder,
                "catalog_content_version",
                CatalogContentVersion);
            EventModifierCanonicalV1.AppendToken(
                builder,
                "catalog_fingerprint",
                CatalogFingerprint);
            EventModifierCanonicalV1.AppendToken(
                builder,
                "evaluated_at_unix_seconds",
                EvaluatedAtUnixSeconds.ToString(CultureInfo.InvariantCulture));
            EventModifierCanonicalV1.AppendToken(
                builder,
                "active_event_count",
                activeEvents.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < activeEvents.Count; index++)
            {
                EventModifierCanonicalV1.AppendToken(
                    builder,
                    "active_event_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    activeEvents[index].ToCanonicalString());
            }
            EventModifierCanonicalV1.AppendToken(
                builder,
                "modifier_snapshot_fingerprint",
                ModifierSnapshot.Fingerprint);
            return builder.ToString();
        }
    }

    /// <summary>
    /// Immutable command context for reward generation, drop generation, strongbox
    /// opening, or mission-result freezing. Commands include
    /// ActiveEventSnapshotFingerprint in their own canonical text and retain this
    /// object when they must evaluate the already-frozen modifier set later.
    /// </summary>
    public sealed class FrozenEventModifierContextV1
    {
        public const int CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<string> activeEventIds;

        public FrozenEventModifierContextV1(
            ActiveEventModifierSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ActiveEventSnapshotFingerprint = snapshot.Fingerprint;
            EventCatalogFingerprint = snapshot.CatalogFingerprint;
            EvaluatedAtUnixSeconds = snapshot.EvaluatedAtUnixSeconds;
            ModifierSnapshot = snapshot.ModifierSnapshot;
            activeEventIds = new ReadOnlyCollection<string>(
                snapshot.ActiveEvents
                    .Select(item => item.EventId)
                    .ToList());
            Fingerprint = RuntimeModifierFingerprintV1.Hash(
                ToCanonicalString());
        }

        public int SchemaVersion
        {
            get { return CurrentSchemaVersion; }
        }

        public string ActiveEventSnapshotFingerprint { get; }

        public string EventCatalogFingerprint { get; }

        public long EvaluatedAtUnixSeconds { get; }

        public IReadOnlyList<string> ActiveEventIds
        {
            get { return activeEventIds; }
        }

        public RuntimeModifierSnapshotV1 ModifierSnapshot { get; }

        public string Fingerprint { get; }

        public RuntimeModifierEvaluationV1 Evaluate(
            string targetId,
            decimal baseValue,
            IEnumerable<string> activeConditionIds = null,
            decimal? minimum = null,
            decimal? maximum = null)
        {
            return ModifierSnapshot.Evaluate(
                targetId,
                baseValue,
                activeConditionIds,
                minimum,
                maximum);
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            EventModifierCanonicalV1.AppendToken(
                builder,
                "schema_version",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            EventModifierCanonicalV1.AppendToken(
                builder,
                "active_event_snapshot_fingerprint",
                ActiveEventSnapshotFingerprint);
            EventModifierCanonicalV1.AppendToken(
                builder,
                "event_catalog_fingerprint",
                EventCatalogFingerprint);
            EventModifierCanonicalV1.AppendToken(
                builder,
                "evaluated_at_unix_seconds",
                EvaluatedAtUnixSeconds.ToString(CultureInfo.InvariantCulture));
            EventModifierCanonicalV1.AppendToken(
                builder,
                "active_event_count",
                activeEventIds.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < activeEventIds.Count; index++)
            {
                EventModifierCanonicalV1.AppendToken(
                    builder,
                    "active_event_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    activeEventIds[index]);
            }
            EventModifierCanonicalV1.AppendToken(
                builder,
                "modifier_snapshot_fingerprint",
                ModifierSnapshot.Fingerprint);
            return builder.ToString();
        }
    }

    public sealed class SpecialEventConflictV1
        : IComparable<SpecialEventConflictV1>
    {
        public SpecialEventConflictV1(
            string firstEventId,
            string secondEventId,
            string reasonCode)
        {
            if (string.IsNullOrWhiteSpace(firstEventId))
            {
                throw new ArgumentException(
                    "A first event identity is required.",
                    nameof(firstEventId));
            }
            if (string.IsNullOrWhiteSpace(secondEventId))
            {
                throw new ArgumentException(
                    "A second event identity is required.",
                    nameof(secondEventId));
            }
            if (string.IsNullOrWhiteSpace(reasonCode))
            {
                throw new ArgumentException(
                    "A conflict reason is required.",
                    nameof(reasonCode));
            }

            string left = firstEventId.Trim();
            string right = secondEventId.Trim();
            if (string.Equals(left, right, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A conflict requires two distinct event identities.");
            }
            if (string.CompareOrdinal(left, right) > 0)
            {
                string temporary = left;
                left = right;
                right = temporary;
            }

            FirstEventId = left;
            SecondEventId = right;
            ReasonCode = reasonCode.Trim();
            Fingerprint = RuntimeModifierFingerprintV1.Hash(
                ToCanonicalString());
        }

        public string FirstEventId { get; }

        public string SecondEventId { get; }

        public string ReasonCode { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return FirstEventId + "|" + SecondEventId + "|" + ReasonCode;
        }

        public int CompareTo(SpecialEventConflictV1 other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int first = string.CompareOrdinal(
                FirstEventId,
                other.FirstEventId);
            if (first != 0)
            {
                return first;
            }

            int second = string.CompareOrdinal(
                SecondEventId,
                other.SecondEventId);
            return second != 0
                ? second
                : string.CompareOrdinal(ReasonCode, other.ReasonCode);
        }
    }

    internal static class EventModifierCanonicalV1
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
    }
}
