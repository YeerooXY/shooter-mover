using System;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    public enum RunSessionDurableAcceptanceStatus
    {
        Accepted = 1,
        RetryableBeforeDurability = 2,
        TerminalPreparationFailure = 3,
        DurableStateUncertain = 4,
    }

    public enum RunSessionDurableEndState
    {
        None = 1,
        PendingExactRetry = 2,
        TerminalPreparationFailure = 3,
        DurableStateUncertain = 4,
    }

    public sealed class RunSessionDurableAcceptanceResult
    {
        private RunSessionDurableAcceptanceResult(
            RunSessionDurableAcceptanceStatus status,
            string rejectionCode)
        {
            if (!Enum.IsDefined(
                typeof(RunSessionDurableAcceptanceStatus),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public RunSessionDurableAcceptanceStatus Status { get; }

        public bool Succeeded
        {
            get
            {
                return Status
                    == RunSessionDurableAcceptanceStatus.Accepted;
            }
        }

        public bool RetryableBeforeDurability
        {
            get
            {
                return Status
                    == RunSessionDurableAcceptanceStatus
                        .RetryableBeforeDurability;
            }
        }

        public bool TerminalPreparationFailure
        {
            get
            {
                return Status
                    == RunSessionDurableAcceptanceStatus
                        .TerminalPreparationFailure;
            }
        }

        public bool DurableStateUncertain
        {
            get
            {
                return Status
                    == RunSessionDurableAcceptanceStatus
                        .DurableStateUncertain;
            }
        }

        public string RejectionCode { get; }

        public static RunSessionDurableAcceptanceResult Accepted()
        {
            return new RunSessionDurableAcceptanceResult(
                RunSessionDurableAcceptanceStatus.Accepted,
                string.Empty);
        }

        public static RunSessionDurableAcceptanceResult
            Retryable(string rejectionCode)
        {
            return new RunSessionDurableAcceptanceResult(
                RunSessionDurableAcceptanceStatus
                    .RetryableBeforeDurability,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "run-end-durable-acceptance-retryable"
                    : rejectionCode.Trim());
        }

        public static RunSessionDurableAcceptanceResult
            Terminal(string rejectionCode)
        {
            return new RunSessionDurableAcceptanceResult(
                RunSessionDurableAcceptanceStatus
                    .TerminalPreparationFailure,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "run-end-terminal-preparation-failure"
                    : rejectionCode.Trim());
        }

        public static RunSessionDurableAcceptanceResult
            Uncertain(string rejectionCode)
        {
            return new RunSessionDurableAcceptanceResult(
                RunSessionDurableAcceptanceStatus
                    .DurableStateUncertain,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "run-end-durable-state-uncertain"
                    : rejectionCode.Trim());
        }
    }

    public sealed partial class RunSessionAggregate
    {
        private RunSessionEndResult pendingDurableEndCandidate;
        private StableId pendingDurableEndOperationStableId;
        private string pendingDurableEndCommandFingerprint = string.Empty;
        private RunSessionDurableEndState durableEndState =
            RunSessionDurableEndState.None;
        private string durableEndDiagnostic = string.Empty;

        public RunSessionDurableEndState DurableEndState
        {
            get
            {
                return pendingDurableEndCandidate == null
                    ? RunSessionDurableEndState.None
                    : durableEndState;
            }
        }

        /// <summary>
        /// Retains the immutable terminal candidate whenever the mission-result authority has
        /// accepted it but durable transfer acceptance has not completed. This is diagnostic
        /// evidence only; callers cannot mutate or replace the candidate through this property.
        /// </summary>
        public RunSessionEndResult PendingDurableEndCandidate
        {
            get { return pendingDurableEndCandidate; }
        }

        public string DurableEndDiagnostic
        {
            get { return durableEndDiagnostic; }
        }

        /// <summary>
        /// Ends a run only after the accepted mission result crosses a caller-supplied durable
        /// acceptance boundary. Retryable failures may invoke the callback again for this exact
        /// candidate. Deterministic preparation failures and uncertain durability are sticky:
        /// they preserve the candidate and reject every ordinary End retry without re-entering
        /// mission-result or durable-acceptance logic.
        /// </summary>
        public RunSessionEndResult EndWithDurableAcceptance(
            EndRunSessionCommand command,
            Func<RunSessionEndResult,
                RunSessionDurableAcceptanceResult> acceptDurably)
        {
            if (command == null)
            {
                return new RunSessionEndResult(
                    RunSessionEndStatus.Rejected,
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
                return new RunSessionEndResult(
                    RunSessionEndStatus.ConflictingDuplicate,
                    command,
                    terminalReceipt,
                    "run-end-operation-conflict");
            }

            RunSessionEndResult candidate = pendingDurableEndCandidate;
            if (candidate != null)
            {
                if (pendingDurableEndOperationStableId
                        != command.OperationStableId
                    || !string.Equals(
                        pendingDurableEndCommandFingerprint,
                        command.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return new RunSessionEndResult(
                        RunSessionEndStatus.ConflictingDuplicate,
                        command,
                        candidate.Receipt,
                        "run-end-pending-durable-operation-conflict");
                }
                if (durableEndState
                    == RunSessionDurableEndState
                        .TerminalPreparationFailure)
                {
                    return new RunSessionEndResult(
                        RunSessionEndStatus.Rejected,
                        command,
                        candidate.Receipt,
                        string.IsNullOrWhiteSpace(durableEndDiagnostic)
                            ? "run-end-terminal-preparation-failure"
                            : durableEndDiagnostic);
                }
                if (durableEndState
                    == RunSessionDurableEndState.DurableStateUncertain)
                {
                    return new RunSessionEndResult(
                        RunSessionEndStatus.Rejected,
                        command,
                        candidate.Receipt,
                        string.IsNullOrWhiteSpace(durableEndDiagnostic)
                            ? "run-end-durable-state-uncertain"
                            : durableEndDiagnostic);
                }
            }
            else
            {
                string rejection = ValidateEnd(command);
                if (!string.IsNullOrEmpty(rejection))
                {
                    RunSessionEndResult rejected =
                        new RunSessionEndResult(
                            RunSessionEndStatus.Rejected,
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

                MissionRunStateResult existingResult =
                    RuntimePorts.MissionResults.EndRun(
                        command,
                        FrozenInputs.RoutePayload);
                if (existingResult == null
                    || !existingResult.Succeeded
                    || existingResult.ResultPayload == null)
                {
                    RunSessionEndResult rejected =
                        new RunSessionEndResult(
                            RunSessionEndStatus.Rejected,
                            command,
                            null,
                            existingResult == null
                                ? "mission-result-port-null"
                                : existingResult.RejectionCode);
                    endReplay.Add(
                        command.OperationStableId,
                        new EndReplayRecord(
                            command.Fingerprint,
                            rejected));
                    return rejected;
                }

                RunLocalStateSnapshot localState = ExportLocalState();
                var candidateReceipt = new RunSessionEndReceipt(
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
                candidate = new RunSessionEndResult(
                    RunSessionEndStatus.Ended,
                    command,
                    candidateReceipt,
                    string.Empty);
                pendingDurableEndCandidate = candidate;
                pendingDurableEndOperationStableId =
                    command.OperationStableId;
                pendingDurableEndCommandFingerprint =
                    command.Fingerprint;
                durableEndState =
                    RunSessionDurableEndState.PendingExactRetry;
                durableEndDiagnostic = string.Empty;
            }

            RunSessionDurableAcceptanceResult durable;
            try
            {
                durable = acceptDurably(candidate);
            }
            catch (Exception exception)
            {
                durableEndState =
                    RunSessionDurableEndState.DurableStateUncertain;
                durableEndDiagnostic =
                    "run-end-durable-acceptance-threw:"
                    + exception.GetType().Name;
                return new RunSessionEndResult(
                    RunSessionEndStatus.Rejected,
                    command,
                    candidate.Receipt,
                    durableEndDiagnostic);
            }

            if (durable == null)
            {
                durableEndState =
                    RunSessionDurableEndState.DurableStateUncertain;
                durableEndDiagnostic =
                    "run-end-durable-acceptance-result-null";
                return new RunSessionEndResult(
                    RunSessionEndStatus.Rejected,
                    command,
                    candidate.Receipt,
                    durableEndDiagnostic);
            }

            if (durable.Status
                == RunSessionDurableAcceptanceStatus
                    .DurableStateUncertain)
            {
                durableEndState =
                    RunSessionDurableEndState.DurableStateUncertain;
                durableEndDiagnostic = durable.RejectionCode;
                return new RunSessionEndResult(
                    RunSessionEndStatus.Rejected,
                    command,
                    candidate.Receipt,
                    durableEndDiagnostic);
            }

            if (durable.Status
                == RunSessionDurableAcceptanceStatus
                    .TerminalPreparationFailure)
            {
                durableEndState =
                    RunSessionDurableEndState
                        .TerminalPreparationFailure;
                durableEndDiagnostic = durable.RejectionCode;
                return new RunSessionEndResult(
                    RunSessionEndStatus.Rejected,
                    command,
                    candidate.Receipt,
                    durableEndDiagnostic);
            }

            if (durable.Status
                == RunSessionDurableAcceptanceStatus
                    .RetryableBeforeDurability)
            {
                durableEndState =
                    RunSessionDurableEndState.PendingExactRetry;
                durableEndDiagnostic = durable.RejectionCode;
                return new RunSessionEndResult(
                    RunSessionEndStatus.Rejected,
                    command,
                    candidate.Receipt,
                    durableEndDiagnostic);
            }

            authoritativeTick = command.AuthoritativeTick;
            lifecycleState = RunSessionLifecycleState.Ended;
            terminalReceipt = candidate.Receipt;
            pendingDurableEndCandidate = null;
            pendingDurableEndOperationStableId = null;
            pendingDurableEndCommandFingerprint = string.Empty;
            durableEndState = RunSessionDurableEndState.None;
            durableEndDiagnostic = string.Empty;
            endReplay.Add(
                command.OperationStableId,
                new EndReplayRecord(command.Fingerprint, candidate));
            return candidate;
        }
    }
}
