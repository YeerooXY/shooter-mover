using System;

namespace ShooterMover.Contracts.Mission
{
    /// <summary>
    /// Immutable result of protocol-level command admission. Accepted only means
    /// that identity, version and ordering checks passed; mission domain rules
    /// must still decide whether the requested transition is valid.
    /// </summary>
    public sealed class MissionCommandEvaluation
    {
        private MissionCommandEvaluation(
            MissionCommandEnvelope command,
            MissionSequence currentSequence,
            MissionRejectionEnvelope rejection)
        {
            Command = MissionContractFormat.RequireNotNull(command, nameof(command));
            CurrentSequence = MissionContractFormat.RequireNotNull(
                currentSequence,
                nameof(currentSequence));
            Rejection = rejection;
        }

        public MissionCommandEnvelope Command { get; }

        public MissionSequence CurrentSequence { get; }

        public MissionRejectionEnvelope Rejection { get; }

        public bool IsAccepted
        {
            get { return Rejection == null; }
        }

        internal static MissionCommandEvaluation Accepted(
            MissionCommandEnvelope command,
            MissionSequence currentSequence)
        {
            return new MissionCommandEvaluation(command, currentSequence, null);
        }

        internal static MissionCommandEvaluation Rejected(
            MissionCommandEnvelope command,
            MissionSequence currentSequence,
            MissionRejectionType rejectionType)
        {
            return new MissionCommandEvaluation(
                command,
                currentSequence,
                new MissionRejectionEnvelope(command, currentSequence, rejectionType));
        }
    }

    /// <summary>
    /// Deterministic protocol gate for Mission Messages v1. It intentionally has
    /// no access to MissionRunState, persistence, scenes, journals or transport.
    /// </summary>
    public static class MissionCommandGate
    {
        public static MissionCommandEvaluation Evaluate(
            MissionCommandEnvelope command,
            MissionSequence currentSequence,
            MissionPayloadVersion supportedPayloadVersion)
        {
            return Evaluate(
                command,
                currentSequence,
                supportedPayloadVersion,
                null);
        }

        public static MissionCommandEvaluation Evaluate(
            MissionCommandEnvelope command,
            MissionSequence currentSequence,
            MissionPayloadVersion supportedPayloadVersion,
            MissionCommandEnvelope existingCommandWithSameId)
        {
            MissionCommandEnvelope validatedCommand = MissionContractFormat.RequireNotNull(
                command,
                nameof(command));
            MissionSequence validatedCurrent = MissionContractFormat.RequireNotNull(
                currentSequence,
                nameof(currentSequence));
            MissionPayloadVersion validatedSupported = MissionContractFormat.RequireNotNull(
                supportedPayloadVersion,
                nameof(supportedPayloadVersion));

            // Duplicate identity is intentionally first. A retry after a timeout
            // must remain a duplicate even after the accepted command advanced
            // the run sequence and made the original expectation look stale.
            if (existingCommandWithSameId != null)
            {
                if (!existingCommandWithSameId.CommandId.Equals(validatedCommand.CommandId))
                {
                    throw new ArgumentException(
                        "The supplied existing command must have the same command ID.",
                        nameof(existingCommandWithSameId));
                }

                MissionRejectionType duplicateType = validatedCommand.Equals(
                    existingCommandWithSameId)
                    ? MissionRejectionType.DuplicateCommand
                    : MissionRejectionType.ConflictingDuplicateCommand;

                return MissionCommandEvaluation.Rejected(
                    validatedCommand,
                    validatedCurrent,
                    duplicateType);
            }

            // Version precedes type and sequence so unsupported payloads are not
            // interpreted under a schema this consumer does not understand.
            if (!validatedCommand.PayloadVersion.Equals(validatedSupported))
            {
                return MissionCommandEvaluation.Rejected(
                    validatedCommand,
                    validatedCurrent,
                    MissionRejectionType.UnsupportedPayloadVersion);
            }

            if (!MissionContractFormat.IsKnownCommandType(validatedCommand.CommandType))
            {
                return MissionCommandEvaluation.Rejected(
                    validatedCommand,
                    validatedCurrent,
                    MissionRejectionType.UnknownCommandType);
            }

            MissionSequenceRelation relation = validatedCommand.ExpectedSequence.RelateTo(
                validatedCurrent);
            if (relation == MissionSequenceRelation.Stale)
            {
                return MissionCommandEvaluation.Rejected(
                    validatedCommand,
                    validatedCurrent,
                    MissionRejectionType.StaleSequence);
            }

            if (relation == MissionSequenceRelation.Future)
            {
                return MissionCommandEvaluation.Rejected(
                    validatedCommand,
                    validatedCurrent,
                    MissionRejectionType.FutureSequence);
            }

            return MissionCommandEvaluation.Accepted(validatedCommand, validatedCurrent);
        }
    }
}
