using System;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    public sealed class RunSessionDurableAcceptanceResultV1
    {
        private RunSessionDurableAcceptanceResultV1(
            bool succeeded,
            string rejectionCode)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string RejectionCode { get; }

        public static RunSessionDurableAcceptanceResultV1 Accepted()
        {
            return new RunSessionDurableAcceptanceResultV1(
                true,
                string.Empty);
        }

        public static RunSessionDurableAcceptanceResultV1 Rejected(
            string rejectionCode)
        {
            return new RunSessionDurableAcceptanceResultV1(
                false,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "run-end-durable-acceptance-rejected"
                    : rejectionCode.Trim());
        }
    }

    public sealed partial class RunSessionAggregateV1
    {
        /// <summary>
        /// Ends a run only after the accepted mission result has crossed a caller-supplied
        /// durable acceptance boundary. The mission-result authority may have produced its
        /// immutable terminal result, but this aggregate is not marked Ended and no End
        /// replay is recorded until the callback confirms durability.
        ///
        /// If the callback rejects, retrying the same command asks the mission-result port
        /// for its exact replay and invokes the callback again. A process interruption after
        /// callback durability but before this method returns is recoverable from the durable
        /// marker, while an interruption before durability is not considered an accepted End.
        /// </summary>
        public RunSessionEndResultV1 EndWithDurableAcceptance(
            EndRunSessionCommandV1 command,
            Func<RunSessionEndResultV1,
                RunSessionDurableAcceptanceResultV1> acceptDurably)
        {
            if (command == null)
            {
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.Rejected,
                    null,
                    null,
                    "run-end-command-null");
            }
            if (acceptDurably == null)
                throw new ArgumentNullException(nameof(acceptDurably));

            EndReplayRecord existing;
            if (endReplay.TryGetValue(command.OperationStableId, out existing))
            {
                if (string.Equals(
                    existing.CommandFingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return existing.Result;
                }
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.ConflictingDuplicate,
                    command,
                    terminalReceipt,
                    "run-end-operation-conflict");
            }

            string rejection = ValidateEnd(command);
            if (!string.IsNullOrEmpty(rejection))
            {
                RunSessionEndResultV1 rejected =
                    new RunSessionEndResultV1(
                        RunSessionEndStatusV1.Rejected,
                        command,
                        terminalReceipt,
                        rejection);
                endReplay.Add(
                    command.OperationStableId,
                    new EndReplayRecord(
                        command.Fingerprint,
                        rejected));
                return rejected;
            }

            MissionRunAuthorityResultV1 existingResult =
                RuntimePorts.MissionResults.EndRun(
                    command,
                    FrozenInputs.RoutePayload);
            if (existingResult == null
                || !existingResult.Succeeded
                || existingResult.ResultPayload == null)
            {
                RunSessionEndResultV1 rejected =
                    new RunSessionEndResultV1(
                        RunSessionEndStatusV1.Rejected,
                        command,
                        null,
                        existingResult == null
                            ? "mission-result-port-null"
                            : existingResult.RejectionCode);
                var retryPolicy = RuntimePorts.MissionResults
                    as IRunMissionResultEndRetryPolicyV1;
                bool retryable = existingResult != null
                    && retryPolicy != null
                    && retryPolicy.IsRetryableEndFailure(
                        command,
                        existingResult);
                if (!retryable)
                {
                    endReplay.Add(
                        command.OperationStableId,
                        new EndReplayRecord(
                            command.Fingerprint,
                            rejected));
                }
                return rejected;
            }

            RunLocalStateSnapshotV1 localState = ExportLocalState();
            var candidateReceipt = new RunSessionEndReceiptV1(
                RunStableId,
                FrozenInputs.Character.CharacterInstanceStableId,
                FrozenInputs.Character.Revision,
                FrozenInputs.Character.Fingerprint,
                StartCommand.MissionLayoutStableId,
                StartCommand.DifficultyStableId,
                StartCommand.DeterministicSeed,
                FrozenInputs.Fingerprint,
                FrozenInputs.CombatProfile.Fingerprint,
                localState,
                existingResult.ResultPayload);
            var candidate = new RunSessionEndResultV1(
                RunSessionEndStatusV1.Ended,
                command,
                candidateReceipt,
                string.Empty);

            RunSessionDurableAcceptanceResultV1 durable;
            try
            {
                durable = acceptDurably(candidate);
            }
            catch (Exception exception)
            {
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.Rejected,
                    command,
                    null,
                    "run-end-durable-acceptance-threw:"
                    + exception.GetType().Name);
            }
            if (durable == null || !durable.Succeeded)
            {
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.Rejected,
                    command,
                    null,
                    durable == null
                        ? "run-end-durable-acceptance-result-null"
                        : durable.RejectionCode);
            }

            authoritativeTick = command.AuthoritativeTick;
            lifecycleState = RunSessionLifecycleStateV1.Ended;
            terminalReceipt = candidateReceipt;
            endReplay.Add(
                command.OperationStableId,
                new EndReplayRecord(command.Fingerprint, candidate));
            return candidate;
        }
    }
}
