using System;
using System.Globalization;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Mission
{
    public enum MissionCommandType
    {
        Unknown = 0,
        RoomClear = 1,
        CheckpointActivation = 2,
        RewardBanking = 3,
        MissionCompletion = 4,
    }

    public enum MissionEventType
    {
        RoomCleared = 1,
        CheckpointActivated = 2,
        RewardsBanked = 3,
        MissionCompleted = 4,
    }

    public enum MissionRejectionType
    {
        DuplicateCommand = 1,
        ConflictingDuplicateCommand = 2,
        UnsupportedPayloadVersion = 3,
        UnknownCommandType = 4,
        StaleSequence = 5,
        FutureSequence = 6,
    }

    /// <summary>
    /// Closed immutable command-payload hierarchy for Mission Messages v1.
    /// The internal constructor prevents external mutable implementations from
    /// being stored behind the public base type.
    /// </summary>
    public abstract class MissionCommandPayload
    {
        internal MissionCommandPayload()
        {
        }

        public abstract MissionCommandType CommandType { get; }

        public abstract string ToCanonicalString();
    }

    /// <summary>
    /// Closed immutable event-payload hierarchy for Mission Messages v1.
    /// </summary>
    public abstract class MissionEventPayload
    {
        internal MissionEventPayload()
        {
        }

        public abstract MissionEventType EventType { get; }

        public abstract string ToCanonicalString();
    }

    /// <summary>
    /// Represents a command type not understood by this contract version without
    /// treating its opaque type identifier as an accepted mission operation.
    /// </summary>
    public sealed class UnknownMissionCommandPayload :
        MissionCommandPayload,
        IEquatable<UnknownMissionCommandPayload>
    {
        public UnknownMissionCommandPayload(StableId typeId)
        {
            TypeId = MissionContractFormat.RequireNotNull(typeId, nameof(typeId));
        }

        public StableId TypeId { get; }

        public override MissionCommandType CommandType
        {
            get { return MissionCommandType.Unknown; }
        }

        public override string ToCanonicalString()
        {
            return "unknown_type_id=" + TypeId;
        }

        public bool Equals(UnknownMissionCommandPayload other)
        {
            return !ReferenceEquals(other, null) && TypeId.Equals(other.TypeId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as UnknownMissionCommandPayload);
        }

        public override int GetHashCode()
        {
            return MissionContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    public sealed class RoomClearRequest :
        MissionCommandPayload,
        IEquatable<RoomClearRequest>
    {
        public RoomClearRequest(StableId roomId, StableId encounterId)
        {
            RoomId = MissionContractFormat.RequireNotNull(roomId, nameof(roomId));
            EncounterId = MissionContractFormat.RequireNotNull(encounterId, nameof(encounterId));
        }

        public StableId RoomId { get; }

        public StableId EncounterId { get; }

        public override MissionCommandType CommandType
        {
            get { return MissionCommandType.RoomClear; }
        }

        public override string ToCanonicalString()
        {
            return "room_id=" + RoomId + "\nencounter_id=" + EncounterId;
        }

        public bool Equals(RoomClearRequest other)
        {
            return !ReferenceEquals(other, null)
                && RoomId.Equals(other.RoomId)
                && EncounterId.Equals(other.EncounterId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RoomClearRequest);
        }

        public override int GetHashCode()
        {
            return MissionContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    public sealed class CheckpointActivationRequest :
        MissionCommandPayload,
        IEquatable<CheckpointActivationRequest>
    {
        public CheckpointActivationRequest(StableId checkpointId, StableId roomId)
        {
            CheckpointId = MissionContractFormat.RequireNotNull(checkpointId, nameof(checkpointId));
            RoomId = MissionContractFormat.RequireNotNull(roomId, nameof(roomId));
        }

        public StableId CheckpointId { get; }

        public StableId RoomId { get; }

        public override MissionCommandType CommandType
        {
            get { return MissionCommandType.CheckpointActivation; }
        }

        public override string ToCanonicalString()
        {
            return "checkpoint_id=" + CheckpointId + "\nroom_id=" + RoomId;
        }

        public bool Equals(CheckpointActivationRequest other)
        {
            return !ReferenceEquals(other, null)
                && CheckpointId.Equals(other.CheckpointId)
                && RoomId.Equals(other.RoomId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CheckpointActivationRequest);
        }

        public override int GetHashCode()
        {
            return MissionContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    public sealed class RewardBankingRequest :
        MissionCommandPayload,
        IEquatable<RewardBankingRequest>
    {
        public RewardBankingRequest(StableId bankId)
        {
            BankId = MissionContractFormat.RequireNotNull(bankId, nameof(bankId));
        }

        public StableId BankId { get; }

        public override MissionCommandType CommandType
        {
            get { return MissionCommandType.RewardBanking; }
        }

        public override string ToCanonicalString()
        {
            return "bank_id=" + BankId;
        }

        public bool Equals(RewardBankingRequest other)
        {
            return !ReferenceEquals(other, null) && BankId.Equals(other.BankId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RewardBankingRequest);
        }

        public override int GetHashCode()
        {
            return MissionContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    public sealed class MissionCompletionRequest :
        MissionCommandPayload,
        IEquatable<MissionCompletionRequest>
    {
        public MissionCompletionRequest(StableId missionId)
        {
            MissionId = MissionContractFormat.RequireNotNull(missionId, nameof(missionId));
        }

        public StableId MissionId { get; }

        public override MissionCommandType CommandType
        {
            get { return MissionCommandType.MissionCompletion; }
        }

        public override string ToCanonicalString()
        {
            return "mission_id=" + MissionId;
        }

        public bool Equals(MissionCompletionRequest other)
        {
            return !ReferenceEquals(other, null) && MissionId.Equals(other.MissionId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MissionCompletionRequest);
        }

        public override int GetHashCode()
        {
            return MissionContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    public sealed class RoomClearedEvent :
        MissionEventPayload,
        IEquatable<RoomClearedEvent>
    {
        public RoomClearedEvent(StableId roomId, StableId encounterId)
        {
            RoomId = MissionContractFormat.RequireNotNull(roomId, nameof(roomId));
            EncounterId = MissionContractFormat.RequireNotNull(encounterId, nameof(encounterId));
        }

        public StableId RoomId { get; }

        public StableId EncounterId { get; }

        public override MissionEventType EventType
        {
            get { return MissionEventType.RoomCleared; }
        }

        public override string ToCanonicalString()
        {
            return "room_id=" + RoomId + "\nencounter_id=" + EncounterId;
        }

        public bool Equals(RoomClearedEvent other)
        {
            return !ReferenceEquals(other, null)
                && RoomId.Equals(other.RoomId)
                && EncounterId.Equals(other.EncounterId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RoomClearedEvent);
        }

        public override int GetHashCode()
        {
            return MissionContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    public sealed class CheckpointActivatedEvent :
        MissionEventPayload,
        IEquatable<CheckpointActivatedEvent>
    {
        public CheckpointActivatedEvent(StableId checkpointId, StableId roomId)
        {
            CheckpointId = MissionContractFormat.RequireNotNull(checkpointId, nameof(checkpointId));
            RoomId = MissionContractFormat.RequireNotNull(roomId, nameof(roomId));
        }

        public StableId CheckpointId { get; }

        public StableId RoomId { get; }

        public override MissionEventType EventType
        {
            get { return MissionEventType.CheckpointActivated; }
        }

        public override string ToCanonicalString()
        {
            return "checkpoint_id=" + CheckpointId + "\nroom_id=" + RoomId;
        }

        public bool Equals(CheckpointActivatedEvent other)
        {
            return !ReferenceEquals(other, null)
                && CheckpointId.Equals(other.CheckpointId)
                && RoomId.Equals(other.RoomId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CheckpointActivatedEvent);
        }

        public override int GetHashCode()
        {
            return MissionContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    public sealed class RewardsBankedEvent :
        MissionEventPayload,
        IEquatable<RewardsBankedEvent>
    {
        public RewardsBankedEvent(StableId bankId, StableId transactionId)
        {
            BankId = MissionContractFormat.RequireNotNull(bankId, nameof(bankId));
            TransactionId = MissionContractFormat.RequireNotNull(transactionId, nameof(transactionId));
        }

        public StableId BankId { get; }

        public StableId TransactionId { get; }

        public override MissionEventType EventType
        {
            get { return MissionEventType.RewardsBanked; }
        }

        public override string ToCanonicalString()
        {
            return "bank_id=" + BankId + "\ntransaction_id=" + TransactionId;
        }

        public bool Equals(RewardsBankedEvent other)
        {
            return !ReferenceEquals(other, null)
                && BankId.Equals(other.BankId)
                && TransactionId.Equals(other.TransactionId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RewardsBankedEvent);
        }

        public override int GetHashCode()
        {
            return MissionContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    public sealed class MissionCompletedEvent :
        MissionEventPayload,
        IEquatable<MissionCompletedEvent>
    {
        public MissionCompletedEvent(StableId missionId, StableId completionId)
        {
            MissionId = MissionContractFormat.RequireNotNull(missionId, nameof(missionId));
            CompletionId = MissionContractFormat.RequireNotNull(completionId, nameof(completionId));
        }

        public StableId MissionId { get; }

        public StableId CompletionId { get; }

        public override MissionEventType EventType
        {
            get { return MissionEventType.MissionCompleted; }
        }

        public override string ToCanonicalString()
        {
            return "mission_id=" + MissionId + "\ncompletion_id=" + CompletionId;
        }

        public bool Equals(MissionCompletedEvent other)
        {
            return !ReferenceEquals(other, null)
                && MissionId.Equals(other.MissionId)
                && CompletionId.Equals(other.CompletionId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MissionCompletedEvent);
        }

        public override int GetHashCode()
        {
            return MissionContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    /// <summary>
    /// Immutable request boundary. This envelope does not decide whether the
    /// requested mission transition is valid in domain state.
    /// </summary>
    public sealed class MissionCommandEnvelope : IEquatable<MissionCommandEnvelope>
    {
        public MissionCommandEnvelope(
            StableId commandId,
            StableId runId,
            MissionPayloadVersion payloadVersion,
            MissionSequence expectedSequence,
            MissionCommandPayload payload)
        {
            CommandId = MissionContractFormat.RequireNotNull(commandId, nameof(commandId));
            RunId = MissionContractFormat.RequireNotNull(runId, nameof(runId));
            PayloadVersion = MissionContractFormat.RequireNotNull(
                payloadVersion,
                nameof(payloadVersion));
            ExpectedSequence = MissionContractFormat.RequireNotNull(
                expectedSequence,
                nameof(expectedSequence));
            Payload = MissionContractFormat.RequireNotNull(payload, nameof(payload));
        }

        public StableId CommandId { get; }

        public StableId RunId { get; }

        public MissionPayloadVersion PayloadVersion { get; }

        public MissionSequence ExpectedSequence { get; }

        public MissionCommandPayload Payload { get; }

        public MissionCommandType CommandType
        {
            get { return Payload.CommandType; }
        }

        public string ToCanonicalString()
        {
            return "command_id="
                + CommandId
                + "\nrun_id="
                + RunId
                + "\n"
                + PayloadVersion.ToCanonicalString()
                + "\nexpected_sequence="
                + ExpectedSequence
                + "\ncommand_type="
                + MissionContractFormat.CommandTypeToken(CommandType)
                + "\npayload:\n"
                + Payload.ToCanonicalString();
        }

        public bool Equals(MissionCommandEnvelope other)
        {
            return !ReferenceEquals(other, null)
                && CommandId.Equals(other.CommandId)
                && RunId.Equals(other.RunId)
                && PayloadVersion.Equals(other.PayloadVersion)
                && ExpectedSequence.Equals(other.ExpectedSequence)
                && CommandType == other.CommandType
                && string.Equals(
                    Payload.ToCanonicalString(),
                    other.Payload.ToCanonicalString(),
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MissionCommandEnvelope);
        }

        public override int GetHashCode()
        {
            return MissionContractFormat.DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    /// <summary>
    /// Immutable accepted fact emitted by mission domain logic.
    /// </summary>
    public sealed class MissionEventEnvelope : IEquatable<MissionEventEnvelope>
    {
        public MissionEventEnvelope(
            StableId eventId,
            StableId commandId,
            StableId runId,
            MissionPayloadVersion payloadVersion,
            MissionSequence sequence,
            MissionEventPayload payload)
        {
            EventId = MissionContractFormat.RequireNotNull(eventId, nameof(eventId));
            CommandId = MissionContractFormat.RequireNotNull(commandId, nameof(commandId));
            RunId = MissionContractFormat.RequireNotNull(runId, nameof(runId));
            PayloadVersion = MissionContractFormat.RequireNotNull(
                payloadVersion,
                nameof(payloadVersion));
            Sequence = MissionContractFormat.RequireNotNull(sequence, nameof(sequence));
            Payload = MissionContractFormat.RequireNotNull(payload, nameof(payload));

            if (sequence.Value == MissionSequence.InitialValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sequence),
                    sequence.Value,
                    "Committed mission events must use a positive sequence.");
            }

            if (!MissionContractFormat.IsKnownEventType(payload.EventType))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(payload),
                    payload.EventType,
                    "Mission events must use an explicitly supported event type.");
            }
        }

        public StableId EventId { get; }

        public StableId CommandId { get; }

        public StableId RunId { get; }

        public MissionPayloadVersion PayloadVersion { get; }

        public MissionSequence Sequence { get; }

        public MissionEventPayload Payload { get; }

        public MissionEventType EventType
        {
            get { return Payload.EventType; }
        }

        public string ToCanonicalString()
        {
            return "event_id="
                + EventId
                + "\ncommand_id="
                + CommandId
                + "\nrun_id="
                + RunId
                + "\n"
                + PayloadVersion.ToCanonicalString()
                + "\nsequence="
                + Sequence
                + "\nevent_type="
                + MissionContractFormat.EventTypeToken(EventType)
                + "\npayload:\n"
                + Payload.ToCanonicalString();
        }

        public bool Equals(MissionEventEnvelope other)
        {
            return !ReferenceEquals(other, null)
                && EventId.Equals(other.EventId)
                && CommandId.Equals(other.CommandId)
                && RunId.Equals(other.RunId)
                && PayloadVersion.Equals(other.PayloadVersion)
                && Sequence.Equals(other.Sequence)
                && EventType == other.EventType
                && string.Equals(
                    Payload.ToCanonicalString(),
                    other.Payload.ToCanonicalString(),
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MissionEventEnvelope);
        }

        public override int GetHashCode()
        {
            return MissionContractFormat.DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    /// <summary>
    /// Immutable protocol rejection. It describes why an envelope could not
    /// enter domain validation; it does not describe mission-state failure.
    /// </summary>
    public sealed class MissionRejectionEnvelope : IEquatable<MissionRejectionEnvelope>
    {
        public MissionRejectionEnvelope(
            MissionCommandEnvelope command,
            MissionSequence currentSequence,
            MissionRejectionType rejectionType)
        {
            MissionCommandEnvelope validatedCommand = MissionContractFormat.RequireNotNull(
                command,
                nameof(command));
            MissionSequence validatedCurrent = MissionContractFormat.RequireNotNull(
                currentSequence,
                nameof(currentSequence));

            if (!MissionContractFormat.IsKnownRejectionType(rejectionType))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rejectionType),
                    rejectionType,
                    "Unknown mission rejection type.");
            }

            MissionSequenceRelation relation = command.ExpectedSequence.RelateTo(currentSequence);
            if (rejectionType == MissionRejectionType.StaleSequence
                && relation != MissionSequenceRelation.Stale)
            {
                throw new ArgumentException(
                    "StaleSequence requires an expected sequence below the current sequence.",
                    nameof(rejectionType));
            }

            if (rejectionType == MissionRejectionType.FutureSequence
                && relation != MissionSequenceRelation.Future)
            {
                throw new ArgumentException(
                    "FutureSequence requires an expected sequence above the current sequence.",
                    nameof(rejectionType));
            }

            CommandId = validatedCommand.CommandId;
            RunId = validatedCommand.RunId;
            PayloadVersion = validatedCommand.PayloadVersion;
            ExpectedSequence = validatedCommand.ExpectedSequence;
            CurrentSequence = validatedCurrent;
            CommandType = validatedCommand.CommandType;
            RejectionType = rejectionType;
        }

        public StableId CommandId { get; }

        public StableId RunId { get; }

        public MissionPayloadVersion PayloadVersion { get; }

        public MissionSequence ExpectedSequence { get; }

        public MissionSequence CurrentSequence { get; }

        public MissionCommandType CommandType { get; }

        public MissionRejectionType RejectionType { get; }

        public string ToCanonicalString()
        {
            return "command_id="
                + CommandId
                + "\nrun_id="
                + RunId
                + "\n"
                + PayloadVersion.ToCanonicalString()
                + "\nexpected_sequence="
                + ExpectedSequence
                + "\ncurrent_sequence="
                + CurrentSequence
                + "\ncommand_type="
                + MissionContractFormat.CommandTypeToken(CommandType)
                + "\nrejection_type="
                + MissionContractFormat.RejectionTypeToken(RejectionType);
        }

        public bool Equals(MissionRejectionEnvelope other)
        {
            return !ReferenceEquals(other, null)
                && CommandId.Equals(other.CommandId)
                && RunId.Equals(other.RunId)
                && PayloadVersion.Equals(other.PayloadVersion)
                && ExpectedSequence.Equals(other.ExpectedSequence)
                && CurrentSequence.Equals(other.CurrentSequence)
                && CommandType == other.CommandType
                && RejectionType == other.RejectionType;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MissionRejectionEnvelope);
        }

        public override int GetHashCode()
        {
            return MissionContractFormat.DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    internal static class MissionContractFormat
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static T RequireNotNull<T>(T value, string parameterName)
            where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return value;
        }

        public static bool IsKnownCommandType(MissionCommandType commandType)
        {
            return commandType == MissionCommandType.RoomClear
                || commandType == MissionCommandType.CheckpointActivation
                || commandType == MissionCommandType.RewardBanking
                || commandType == MissionCommandType.MissionCompletion;
        }

        public static bool IsKnownEventType(MissionEventType eventType)
        {
            return eventType == MissionEventType.RoomCleared
                || eventType == MissionEventType.CheckpointActivated
                || eventType == MissionEventType.RewardsBanked
                || eventType == MissionEventType.MissionCompleted;
        }

        public static bool IsKnownRejectionType(MissionRejectionType rejectionType)
        {
            return rejectionType == MissionRejectionType.DuplicateCommand
                || rejectionType == MissionRejectionType.ConflictingDuplicateCommand
                || rejectionType == MissionRejectionType.UnsupportedPayloadVersion
                || rejectionType == MissionRejectionType.UnknownCommandType
                || rejectionType == MissionRejectionType.StaleSequence
                || rejectionType == MissionRejectionType.FutureSequence;
        }

        public static string CommandTypeToken(MissionCommandType commandType)
        {
            switch (commandType)
            {
                case MissionCommandType.RoomClear:
                    return "room-clear";
                case MissionCommandType.CheckpointActivation:
                    return "checkpoint-activation";
                case MissionCommandType.RewardBanking:
                    return "reward-banking";
                case MissionCommandType.MissionCompletion:
                    return "mission-completion";
                case MissionCommandType.Unknown:
                    return "unknown";
                default:
                    return "unknown-" + ((int)commandType).ToString(CultureInfo.InvariantCulture);
            }
        }

        public static string EventTypeToken(MissionEventType eventType)
        {
            switch (eventType)
            {
                case MissionEventType.RoomCleared:
                    return "room-cleared";
                case MissionEventType.CheckpointActivated:
                    return "checkpoint-activated";
                case MissionEventType.RewardsBanked:
                    return "rewards-banked";
                case MissionEventType.MissionCompleted:
                    return "mission-completed";
                default:
                    return "unknown-" + ((int)eventType).ToString(CultureInfo.InvariantCulture);
            }
        }

        public static string RejectionTypeToken(MissionRejectionType rejectionType)
        {
            switch (rejectionType)
            {
                case MissionRejectionType.DuplicateCommand:
                    return "duplicate-command";
                case MissionRejectionType.ConflictingDuplicateCommand:
                    return "conflicting-duplicate-command";
                case MissionRejectionType.UnsupportedPayloadVersion:
                    return "unsupported-payload-version";
                case MissionRejectionType.UnknownCommandType:
                    return "unknown-command-type";
                case MissionRejectionType.StaleSequence:
                    return "stale-sequence";
                case MissionRejectionType.FutureSequence:
                    return "future-sequence";
                default:
                    return "unknown-" + ((int)rejectionType).ToString(CultureInfo.InvariantCulture);
            }
        }

        public static int DeterministicHash(string canonicalText)
        {
            if (canonicalText == null)
            {
                throw new ArgumentNullException(nameof(canonicalText));
            }

            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }
    }
}
