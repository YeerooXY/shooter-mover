using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Modifiers.Events;

namespace ShooterMover.Application.Modifiers.Events
{
    public enum EventStampedCommandKind
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
    public sealed class EventStampedCommandEnvelope
    {
        public const int CurrentSchemaVersion = 1;

        private EventStampedCommandEnvelope(
            EventStampedCommandKind commandKind,
            string commandFingerprint,
            FrozenEventModifierContext eventContext)
        {
            if (!Enum.IsDefined(typeof(EventStampedCommandKind), commandKind))
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
            Fingerprint = EventView.Fingerprint(
                ToCanonicalString());
        }

        public int SchemaVersion
        {
            get { return CurrentSchemaVersion; }
        }

        public EventStampedCommandKind CommandKind { get; }

        public string CommandFingerprint { get; }

        public FrozenEventModifierContext EventContext { get; }

        public string ActiveEventSnapshotFingerprint { get; }

        public string Fingerprint { get; }

        public static EventStampedCommandEnvelope ForRewardGeneration(
            string commandFingerprint,
            FrozenEventModifierContext eventContext)
        {
            return new EventStampedCommandEnvelope(
                EventStampedCommandKind.RewardGeneration,
                commandFingerprint,
                eventContext);
        }

        public static EventStampedCommandEnvelope ForDropGeneration(
            string commandFingerprint,
            FrozenEventModifierContext eventContext)
        {
            return new EventStampedCommandEnvelope(
                EventStampedCommandKind.DropGeneration,
                commandFingerprint,
                eventContext);
        }

        public static EventStampedCommandEnvelope ForStrongboxOpening(
            string commandFingerprint,
            FrozenEventModifierContext eventContext)
        {
            return new EventStampedCommandEnvelope(
                EventStampedCommandKind.StrongboxOpening,
                commandFingerprint,
                eventContext);
        }

        public static EventStampedCommandEnvelope ForMissionResult(
            string commandFingerprint,
            FrozenEventModifierContext eventContext)
        {
            return new EventStampedCommandEnvelope(
                EventStampedCommandKind.MissionResult,
                commandFingerprint,
                eventContext);
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            EventView.AppendToken(
                builder,
                "schema_version",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            EventView.AppendToken(
                builder,
                "command_kind",
                ((int)CommandKind).ToString(CultureInfo.InvariantCulture));
            EventView.AppendToken(
                builder,
                "command_fingerprint",
                CommandFingerprint);
            EventView.AppendToken(
                builder,
                "active_event_snapshot_fingerprint",
                ActiveEventSnapshotFingerprint);
            EventView.AppendToken(
                builder,
                "frozen_event_context_fingerprint",
                EventContext.Fingerprint);
            return builder.ToString();
        }
    }
}
