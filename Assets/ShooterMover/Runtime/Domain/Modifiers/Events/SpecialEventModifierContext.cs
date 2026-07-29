using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Domain.Modifiers;

namespace ShooterMover.Domain.Modifiers.Events
{
    public static class EventModifierTargetIds
    {
        public const string RewardStrongboxWeight = "rewards.strongbox-drop-weight";
        public const string MoneyQuantity = "rewards.money-quantity";
        public const string ExperienceQuantity = "rewards.xp-quantity";
    }

    public enum SpecialEventOverlapMode
    {
        Combine = 1,
        Exclusive = 2,
    }

    public sealed class EventActivationWindow
    {
        public EventActivationWindow(
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
            Fingerprint = LiveModifierFingerprint.Hash(
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

    public sealed class EventModifierDescriptor
    {
        public EventModifierDescriptor(
            string targetId,
            LiveModifierOperation operation,
            decimal value,
            string conditionId = "")
        {
            var validation = new LiveModifierDefinition(
                "event.descriptor.validation",
                targetId,
                operation,
                value,
                conditionId);

            TargetId = validation.TargetId;
            Operation = validation.Operation;
            Value = validation.Value;
            ConditionId = validation.ConditionId;
            Fingerprint = LiveModifierFingerprint.Hash(
                ToCanonicalString());
        }

        public string TargetId { get; }

        public LiveModifierOperation Operation { get; }

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

    public sealed class SpecialEventDefinition
    {
        public const int CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<string> excludedEventIds;
        private readonly ReadOnlyCollection<EventModifierDescriptor> modifiers;

        public SpecialEventDefinition(
            int schemaVersion,
            string contentVersion,
            string eventId,
            EventActivationWindow activationWindow,
            int priority,
            SpecialEventOverlapMode overlapMode,
            IEnumerable<EventModifierDescriptor> modifiers,
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
            if (!Enum.IsDefined(typeof(SpecialEventOverlapMode), overlapMode))
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

            var modifierCopy = new List<EventModifierDescriptor>(
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
            this.modifiers = new ReadOnlyCollection<EventModifierDescriptor>(
                modifierCopy);
            Fingerprint = LiveModifierFingerprint.Hash(
                ToCanonicalString());
        }

        public int SchemaVersion { get; }

        public string ContentVersion { get; }

        public string EventId { get; }

        public EventActivationWindow ActivationWindow { get; }

        public int Priority { get; }

        public SpecialEventOverlapMode OverlapMode { get; }

        public IReadOnlyList<string> ExcludedEventIds
        {
            get { return excludedEventIds; }
        }

        public IReadOnlyList<EventModifierDescriptor> Modifiers
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

        public IEnumerable<LiveModifierDefinition> ProjectModifiers()
        {
            return modifiers.Select(item => new LiveModifierDefinition(
                EventId,
                item.TargetId,
                item.Operation,
                item.Value,
                item.ConditionId));
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            EventModifier.AppendToken(
                builder,
                "schema_version",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            EventModifier.AppendToken(
                builder,
                "content_version",
                ContentVersion);
            EventModifier.AppendToken(builder, "event_id", EventId);
            EventModifier.AppendToken(
                builder,
                "activation_window",
                ActivationWindow.ToCanonicalString());
            EventModifier.AppendToken(
                builder,
                "priority",
                Priority.ToString(CultureInfo.InvariantCulture));
            EventModifier.AppendToken(
                builder,
                "overlap_mode",
                ((int)OverlapMode).ToString(CultureInfo.InvariantCulture));
            EventModifier.AppendToken(
                builder,
                "excluded_count",
                excludedEventIds.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < excludedEventIds.Count; index++)
            {
                EventModifier.AppendToken(
                    builder,
                    "excluded_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    excludedEventIds[index]);
            }
            EventModifier.AppendToken(
                builder,
                "modifier_count",
                modifiers.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < modifiers.Count; index++)
            {
                EventModifier.AppendToken(
                    builder,
                    "modifier_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    modifiers[index].ToCanonicalString());
            }
            return builder.ToString();
        }
    }

    public sealed class SpecialEventCatalog
    {
        public const int CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<SpecialEventDefinition> definitions;

        public SpecialEventCatalog(
            string contentVersion,
            IEnumerable<SpecialEventDefinition> definitions)
        {
            if (string.IsNullOrWhiteSpace(contentVersion))
            {
                throw new ArgumentException(
                    "A catalog content version is required.",
                    nameof(contentVersion));
            }

            ContentVersion = contentVersion.Trim();
            var copy = new List<SpecialEventDefinition>(
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
            foreach (SpecialEventDefinition definition in copy)
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
            this.definitions = new ReadOnlyCollection<SpecialEventDefinition>(
                copy);
            Fingerprint = LiveModifierFingerprint.Hash(
                ToCanonicalString());
        }

        public int SchemaVersion
        {
            get { return CurrentSchemaVersion; }
        }

        public string ContentVersion { get; }

        public IReadOnlyList<SpecialEventDefinition> Definitions
        {
            get { return definitions; }
        }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            EventModifier.AppendToken(
                builder,
                "schema_version",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            EventModifier.AppendToken(
                builder,
                "content_version",
                ContentVersion);
            EventModifier.AppendToken(
                builder,
                "definition_count",
                definitions.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < definitions.Count; index++)
            {
                EventModifier.AppendToken(
                    builder,
                    "definition_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    definitions[index].ToCanonicalString());
            }
            return builder.ToString();
        }
    }

    public sealed class ActiveEventDescriptor
        : IComparable<ActiveEventDescriptor>
    {
        public ActiveEventDescriptor(SpecialEventDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            EventId = definition.EventId;
            ContentVersion = definition.ContentVersion;
            Priority = definition.Priority;
            DefinitionFingerprint = definition.Fingerprint;
            Fingerprint = LiveModifierFingerprint.Hash(
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

        public int CompareTo(ActiveEventDescriptor other)
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

    public sealed class ActiveEventModifierSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<ActiveEventDescriptor> activeEvents;

        private ActiveEventModifierSnapshot(
            string catalogContentVersion,
            string catalogFingerprint,
            long evaluatedAtUnixSeconds,
            IEnumerable<ActiveEventDescriptor> activeEvents,
            LiveModifierSnapshot modifierSnapshot,
            string fingerprint)
        {
            CatalogContentVersion = catalogContentVersion;
            CatalogFingerprint = catalogFingerprint;
            EvaluatedAtUnixSeconds = evaluatedAtUnixSeconds;
            this.activeEvents = new ReadOnlyCollection<ActiveEventDescriptor>(
                new List<ActiveEventDescriptor>(activeEvents));
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

        public IReadOnlyList<ActiveEventDescriptor> ActiveEvents
        {
            get { return activeEvents; }
        }

        public LiveModifierSnapshot ModifierSnapshot { get; }

        public string Fingerprint { get; }

        public static ActiveEventModifierSnapshot Create(
            SpecialEventCatalog catalog,
            long evaluatedAtUnixSeconds,
            IEnumerable<SpecialEventDefinition> activeDefinitions)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var definitions = new List<SpecialEventDefinition>(
                activeDefinitions
                    ?? throw new ArgumentNullException(nameof(activeDefinitions)));
            if (definitions.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Active event definitions must be non-null.",
                    nameof(activeDefinitions));
            }

            var active = definitions
                .Select(item => new ActiveEventDescriptor(item))
                .OrderBy(item => item)
                .ToList();
            var modifierSnapshot = new LiveModifierSnapshot(
                definitions.SelectMany(item => item.ProjectModifiers()));
            var provisional = new ActiveEventModifierSnapshot(
                catalog.ContentVersion,
                catalog.Fingerprint,
                evaluatedAtUnixSeconds,
                active,
                modifierSnapshot,
                string.Empty);
            string fingerprint = LiveModifierFingerprint.Hash(
                provisional.ToCanonicalString());
            return new ActiveEventModifierSnapshot(
                provisional.CatalogContentVersion,
                provisional.CatalogFingerprint,
                provisional.EvaluatedAtUnixSeconds,
                provisional.ActiveEvents,
                provisional.ModifierSnapshot,
                fingerprint);
        }

        public FrozenEventModifierContext FreezeForCommand()
        {
            return new FrozenEventModifierContext(this);
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            EventModifier.AppendToken(
                builder,
                "schema_version",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            EventModifier.AppendToken(
                builder,
                "catalog_content_version",
                CatalogContentVersion);
            EventModifier.AppendToken(
                builder,
                "catalog_fingerprint",
                CatalogFingerprint);
            EventModifier.AppendToken(
                builder,
                "evaluated_at_unix_seconds",
                EvaluatedAtUnixSeconds.ToString(CultureInfo.InvariantCulture));
            EventModifier.AppendToken(
                builder,
                "active_event_count",
                activeEvents.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < activeEvents.Count; index++)
            {
                EventModifier.AppendToken(
                    builder,
                    "active_event_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    activeEvents[index].ToCanonicalString());
            }
            EventModifier.AppendToken(
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
    public sealed class FrozenEventModifierContext
    {
        public const int CurrentSchemaVersion = 1;

        private readonly ReadOnlyCollection<string> activeEventIds;

        public FrozenEventModifierContext(
            ActiveEventModifierSnapshot snapshot)
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
            Fingerprint = LiveModifierFingerprint.Hash(
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

        public LiveModifierSnapshot ModifierSnapshot { get; }

        public string Fingerprint { get; }

        public LiveModifierEvaluation Evaluate(
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
            EventModifier.AppendToken(
                builder,
                "schema_version",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            EventModifier.AppendToken(
                builder,
                "active_event_snapshot_fingerprint",
                ActiveEventSnapshotFingerprint);
            EventModifier.AppendToken(
                builder,
                "event_catalog_fingerprint",
                EventCatalogFingerprint);
            EventModifier.AppendToken(
                builder,
                "evaluated_at_unix_seconds",
                EvaluatedAtUnixSeconds.ToString(CultureInfo.InvariantCulture));
            EventModifier.AppendToken(
                builder,
                "active_event_count",
                activeEventIds.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < activeEventIds.Count; index++)
            {
                EventModifier.AppendToken(
                    builder,
                    "active_event_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    activeEventIds[index]);
            }
            EventModifier.AppendToken(
                builder,
                "modifier_snapshot_fingerprint",
                ModifierSnapshot.Fingerprint);
            return builder.ToString();
        }
    }

    public sealed class SpecialEventConflict
        : IComparable<SpecialEventConflict>
    {
        public SpecialEventConflict(
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
            Fingerprint = LiveModifierFingerprint.Hash(
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

        public int CompareTo(SpecialEventConflict other)
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

    internal static class EventModifier
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
