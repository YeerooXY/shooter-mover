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
        private RunSessionEndResult pendingDurableEndCandidateV1;
        private StableId pendingDurableEndOperationStableIdV1;
        private string pendingDurableEndCommandFingerprintV1 = string.Empty;
        private RunSessionDurableEndState durableEndStateV1 =
            RunSessionDurableEndState.None;
        private string durableEndDiagnosticV1 = string.Empty;

        public RunSessionDurableEndState DurableEndState
        {
            get
            {
                return pendingDurableEndCandidateV1 == null
                    ? RunSessionDurableEndState.None
                    : durableEndStateV1;
            }
        }

        /// <summary>
        /// Retains the immutable terminal candidate whenever the mission-result authority has
        /// accepted it but durable transfer acceptance has not completed. This is diagnostic
        /// evidence only; callers cannot mutate or replace the candidate through this property.
        /// </summary>
        public RunSessionEndResult PendingDurableEndCandidate
        {
            get { return pendingDurableEndCandidateV1; }
        }

        public string DurableEndDiagnostic
        {
            get { return durableEndDiagnosticV1; }
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

            RunSessionEndResult candidate = pendingDurableEndCandidateV1;
            if (candidate != null)
            {
                if (pendingDurableEndOperationStableIdV1
                        != command.OperationStableId
                    || !string.Equals(
                        pendingDurableEndCommandFingerprintV1,
                        command.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return new RunSessionEndResult(
                        RunSessionEndStatus.ConflictingDuplicate,
                        command,
                        candidate.Receipt,
                        "run-end-pending-durable-operation-conflict");
                }
                if (durableEndStateV1
                    == RunSessionDurableEndState
                        .TerminalPreparationFailure)
                {
                    return new RunSessionEndResult(
                        RunSessionEndStatus.Rejected,
                        command,
                        candidate.Receipt,
                        string.IsNullOrWhiteSpace(durableEndDiagnosticV1)
                            ? "run-end-terminal-preparation-failure"
                            : durableEndDiagnosticV1);
                }
                if (durableEndStateV1
                    == RunSessionDurableEndState.DurableStateUncertain)
                {
                    return new RunSessionEndResult(
                        RunSessionEndStatus.Rejected,
                        command,
                        candidate.Receipt,
                        string.IsNullOrWhiteSpace(durableEndDiagnosticV1)
                            ? "run-end-durable-state-uncertain"
                            : durableEndDiagnosticV1);
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
                    var retryPolicy = RuntimePorts.MissionResults
                        as IRunMissionResultEndRetryPolicy;
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
                pendingDurableEndCandidateV1 = candidate;
                pendingDurableEndOperationStableIdV1 =
                    command.OperationStableId;
                pendingDurableEndCommandFingerprintV1 =
                    command.Fingerprint;
                durableEndStateV1 =
                    RunSessionDurableEndState.PendingExactRetry;
                durableEndDiagnosticV1 = string.Empty;
            }

            RunSessionDurableAcceptanceResult durable;
            try
            {
                durable = acceptDurably(candidate);
            }
            catch (Exception exception)
            {
                durableEndStateV1 =
                    RunSessionDurableEndState.DurableStateUncertain;
                durableEndDiagnosticV1 =
                    "run-end-durable-acceptance-threw:"
                    + exception.GetType().Name;
                return new RunSessionEndResult(
                    RunSessionEndStatus.Rejected,
                    command,
                    candidate.Receipt,
                    durableEndDiagnosticV1);
            }

            if (durable == null)
            {
                durableEndStateV1 =
                    RunSessionDurableEndState.DurableStateUncertain;
                durableEndDiagnosticV1 =
                    "run-end-durable-acceptance-result-null";
                return new RunSessionEndResult(
                    RunSessionEndStatus.Rejected,
                    command,
                    candidate.Receipt,
                    durableEndDiagnosticV1);
            }

            if (durable.Status
                == RunSessionDurableAcceptanceStatus
                    .DurableStateUncertain)
            {
                durableEndStateV1 =
                    RunSessionDurableEndState.DurableStateUncertain;
                durableEndDiagnosticV1 = durable.RejectionCode;
                return new RunSessionEndResult(
                    RunSessionEndStatus.Rejected,
                    command,
                    candidate.Receipt,
                    durableEndDiagnosticV1);
            }

            if (durable.Status
                == RunSessionDurableAcceptanceStatus
                    .TerminalPreparationFailure)
            {
                durableEndStateV1 =
                    RunSessionDurableEndState
                        .TerminalPreparationFailure;
                durableEndDiagnosticV1 = durable.RejectionCode;
                return new RunSessionEndResult(
                    RunSessionEndStatus.Rejected,
                    command,
                    candidate.Receipt,
                    durableEndDiagnosticV1);
            }

            if (durable.Status
                == RunSessionDurableAcceptanceStatus
                    .RetryableBeforeDurability)
            {
                durableEndStateV1 =
                    RunSessionDurableEndState.PendingExactRetry;
                durableEndDiagnosticV1 = durable.RejectionCode;
                return new RunSessionEndResult(
                    RunSessionEndStatus.Rejected,
                    command,
                    candidate.Receipt,
                    durableEndDiagnosticV1);
            }

            authoritativeTick = command.AuthoritativeTick;
            lifecycleState = RunSessionLifecycleState.Ended;
            terminalReceipt = candidate.Receipt;
            pendingDurableEndCandidateV1 = null;
            pendingDurableEndOperationStableIdV1 = null;
            pendingDurableEndCommandFingerprintV1 = string.Empty;
            durableEndStateV1 = RunSessionDurableEndState.None;
            durableEndDiagnosticV1 = string.Empty;
            endReplay.Add(
                command.OperationStableId,
                new EndReplayRecord(command.Fingerprint, candidate));
            return candidate;
        }
    }
}
