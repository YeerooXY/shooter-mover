using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Modifiers.Events;

namespace ShooterMover.Application.Modifiers.Events
{
    public enum EventStampedCommandKindV1
    {
        RewardGeneration = 1,
        DropGeneration = 2,
        StrongboxOpening = 3,
        MissionResult = 4,
    }

    /// <summary>
    /// Narrow immutable envelope that lets reward, drop, opening, and mission-result
    /// commands record the exact active-event snapshot used without adding an event
    /// dependency to their underlying generation algorithms or catalogs.
    /// </summary>
    public sealed class EventStampedCommandEnvelopeV1
    {
        public const int CurrentSchemaVersion = 1;

        private EventStampedCommandEnvelopeV1(
            EventStampedCommandKindV1 commandKind,
            string commandFingerprint,
            FrozenEventModifierContextV1 eventContext)
        {
            if (!Enum.IsDefined(typeof(EventStampedCommandKindV1), commandKind))
            {
                throw new ArgumentOutOfRangeException(nameof(commandKind));
            }
            if (string.IsNullOrWhiteSpace(commandFingerprint))
            {
                throw new ArgumentException(
                    "A canonical command fingerprint is required.",
                    nameof(commandFingerprint));
            }

            CommandKind = commandKind;
            CommandFingerprint = commandFingerprint.Trim();
            EventContext = eventContext
                ?? throw new ArgumentNullException(nameof(eventContext));
            ActiveEventSnapshotFingerprint =
                EventContext.ActiveEventSnapshotFingerprint;
            Fingerprint = EventProjectionCanonicalV1.Fingerprint(
                ToCanonicalString());
        }

        public int SchemaVersion
        {
            get { return CurrentSchemaVersion; }
        }

        public EventStampedCommandKindV1 CommandKind { get; }

        public string CommandFingerprint { get; }

        public FrozenEventModifierContextV1 EventContext { get; }

        public string ActiveEventSnapshotFingerprint { get; }

        public string Fingerprint { get; }

        public static EventStampedCommandEnvelopeV1 ForRewardGeneration(
            string commandFingerprint,
            FrozenEventModifierContextV1 eventContext)
        {
            return new EventStampedCommandEnvelopeV1(
                EventStampedCommandKindV1.RewardGeneration,
                commandFingerprint,
                eventContext);
        }

        public static EventStampedCommandEnvelopeV1 ForDropGeneration(
            string commandFingerprint,
            FrozenEventModifierContextV1 eventContext)
        {
            return new EventStampedCommandEnvelopeV1(
                EventStampedCommandKindV1.DropGeneration,
                commandFingerprint,
                eventContext);
        }

        public static EventStampedCommandEnvelopeV1 ForStrongboxOpening(
            string commandFingerprint,
            FrozenEventModifierContextV1 eventContext)
        {
            return new EventStampedCommandEnvelopeV1(
                EventStampedCommandKindV1.StrongboxOpening,
                commandFingerprint,
                eventContext);
        }

        public static EventStampedCommandEnvelopeV1 ForMissionResult(
            string commandFingerprint,
            FrozenEventModifierContextV1 eventContext)
        {
            return new EventStampedCommandEnvelopeV1(
                EventStampedCommandKindV1.MissionResult,
                commandFingerprint,
                eventContext);
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            EventProjectionCanonicalV1.AppendToken(
                builder,
                "schema_version",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            EventProjectionCanonicalV1.AppendToken(
                builder,
                "command_kind",
                ((int)CommandKind).ToString(CultureInfo.InvariantCulture));
            EventProjectionCanonicalV1.AppendToken(
                builder,
                "command_fingerprint",
                CommandFingerprint);
            EventProjectionCanonicalV1.AppendToken(
                builder,
                "active_event_snapshot_fingerprint",
                ActiveEventSnapshotFingerprint);
            EventProjectionCanonicalV1.AppendToken(
                builder,
                "frozen_event_context_fingerprint",
                EventContext.Fingerprint);
            return builder.ToString();
        }
    }
}
