using System;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    public enum RunSessionDurableAcceptanceStatusV1
    {
        Accepted = 1,
        RetryableBeforeDurability = 2,
        TerminalPreparationFailure = 3,
        DurableStateUncertain = 4,
    }

    public enum RunSessionDurableEndStateV1
    {
        None = 1,
        PendingExactRetry = 2,
        TerminalPreparationFailure = 3,
        DurableStateUncertain = 4,
    }

    public sealed class RunSessionDurableAcceptanceResultV1
    {
        private RunSessionDurableAcceptanceResultV1(
            RunSessionDurableAcceptanceStatusV1 status,
            string rejectionCode)
        {
            if (!Enum.IsDefined(
                typeof(RunSessionDurableAcceptanceStatusV1),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public RunSessionDurableAcceptanceStatusV1 Status { get; }

        public bool Succeeded
        {
            get
            {
                return Status
                    == RunSessionDurableAcceptanceStatusV1.Accepted;
            }
        }

        public bool RetryableBeforeDurability
        {
            get
            {
                return Status
                    == RunSessionDurableAcceptanceStatusV1
                        .RetryableBeforeDurability;
            }
        }

        public bool TerminalPreparationFailure
        {
            get
            {
                return Status
                    == RunSessionDurableAcceptanceStatusV1
                        .TerminalPreparationFailure;
            }
        }

        public bool DurableStateUncertain
        {
            get
            {
                return Status
                    == RunSessionDurableAcceptanceStatusV1
                        .DurableStateUncertain;
            }
        }

        public string RejectionCode { get; }

        public static RunSessionDurableAcceptanceResultV1 Accepted()
        {
            return new RunSessionDurableAcceptanceResultV1(
                RunSessionDurableAcceptanceStatusV1.Accepted,
                string.Empty);
        }

        public static RunSessionDurableAcceptanceResultV1
            Retryable(string rejectionCode)
        {
            return new RunSessionDurableAcceptanceResultV1(
                RunSessionDurableAcceptanceStatusV1
                    .RetryableBeforeDurability,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "run-end-durable-acceptance-retryable"
                    : rejectionCode.Trim());
        }

        public static RunSessionDurableAcceptanceResultV1
            Terminal(string rejectionCode)
        {
            return new RunSessionDurableAcceptanceResultV1(
                RunSessionDurableAcceptanceStatusV1
                    .TerminalPreparationFailure,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "run-end-terminal-preparation-failure"
                    : rejectionCode.Trim());
        }

        public static RunSessionDurableAcceptanceResultV1
            Uncertain(string rejectionCode)
        {
            return new RunSessionDurableAcceptanceResultV1(
                RunSessionDurableAcceptanceStatusV1
                    .DurableStateUncertain,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "run-end-durable-state-uncertain"
                    : rejectionCode.Trim());
        }
    }

    public sealed partial class RunSessionAggregateV1
    {
        private RunSessionEndResultV1 pendingDurableEndCandidateV1;
        private StableId pendingDurableEndOperationStableIdV1;
        private string pendingDurableEndCommandFingerprintV1 = string.Empty;
        private RunSessionDurableEndStateV1 durableEndStateV1 =
            RunSessionDurableEndStateV1.None;
        private string durableEndDiagnosticV1 = string.Empty;

        public RunSessionDurableEndStateV1 DurableEndState
        {
            get
            {
                return pendingDurableEndCandidateV1 == null
                    ? RunSessionDurableEndStateV1.None
                    : durableEndStateV1;
            }
        }

        /// <summary>
        /// Retains the immutable terminal candidate whenever the mission-result authority has
        /// accepted it but durable transfer acceptance has not completed. This is diagnostic
        /// evidence only; callers cannot mutate or replace the candidate through this property.
        /// </summary>
        public RunSessionEndResultV1 PendingDurableEndCandidate
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

            RunSessionEndResultV1 candidate = pendingDurableEndCandidateV1;
            if (candidate != null)
            {
                if (pendingDurableEndOperationStableIdV1
                        != command.OperationStableId
                    || !string.Equals(
                        pendingDurableEndCommandFingerprintV1,
                        command.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return new RunSessionEndResultV1(
                        RunSessionEndStatusV1.ConflictingDuplicate,
                        command,
                        candidate.Receipt,
                        "run-end-pending-durable-operation-conflict");
                }
                if (durableEndStateV1
                    == RunSessionDurableEndStateV1
                        .TerminalPreparationFailure)
                {
                    return new RunSessionEndResultV1(
                        RunSessionEndStatusV1.Rejected,
                        command,
                        candidate.Receipt,
                        string.IsNullOrWhiteSpace(durableEndDiagnosticV1)
                            ? "run-end-terminal-preparation-failure"
                            : durableEndDiagnosticV1);
                }
                if (durableEndStateV1
                    == RunSessionDurableEndStateV1.DurableStateUncertain)
                {
                    return new RunSessionEndResultV1(
                        RunSessionEndStatusV1.Rejected,
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
                candidate = new RunSessionEndResultV1(
                    RunSessionEndStatusV1.Ended,
                    command,
                    candidateReceipt,
                    string.Empty);
                pendingDurableEndCandidateV1 = candidate;
                pendingDurableEndOperationStableIdV1 =
                    command.OperationStableId;
                pendingDurableEndCommandFingerprintV1 =
                    command.Fingerprint;
                durableEndStateV1 =
                    RunSessionDurableEndStateV1.PendingExactRetry;
                durableEndDiagnosticV1 = string.Empty;
            }

            RunSessionDurableAcceptanceResultV1 durable;
            try
            {
                durable = acceptDurably(candidate);
            }
            catch (Exception exception)
            {
                durableEndStateV1 =
                    RunSessionDurableEndStateV1.DurableStateUncertain;
                durableEndDiagnosticV1 =
                    "run-end-durable-acceptance-threw:"
                    + exception.GetType().Name;
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.Rejected,
                    command,
                    candidate.Receipt,
                    durableEndDiagnosticV1);
            }

            if (durable == null)
            {
                durableEndStateV1 =
                    RunSessionDurableEndStateV1.DurableStateUncertain;
                durableEndDiagnosticV1 =
                    "run-end-durable-acceptance-result-null";
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.Rejected,
                    command,
                    candidate.Receipt,
                    durableEndDiagnosticV1);
            }

            if (durable.Status
                == RunSessionDurableAcceptanceStatusV1
                    .DurableStateUncertain)
            {
                durableEndStateV1 =
                    RunSessionDurableEndStateV1.DurableStateUncertain;
                durableEndDiagnosticV1 = durable.RejectionCode;
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.Rejected,
                    command,
                    candidate.Receipt,
                    durableEndDiagnosticV1);
            }

            if (durable.Status
                == RunSessionDurableAcceptanceStatusV1
                    .TerminalPreparationFailure)
            {
                durableEndStateV1 =
                    RunSessionDurableEndStateV1
                        .TerminalPreparationFailure;
                durableEndDiagnosticV1 = durable.RejectionCode;
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.Rejected,
                    command,
                    candidate.Receipt,
                    durableEndDiagnosticV1);
            }

            if (durable.Status
                == RunSessionDurableAcceptanceStatusV1
                    .RetryableBeforeDurability)
            {
                durableEndStateV1 =
                    RunSessionDurableEndStateV1.PendingExactRetry;
                durableEndDiagnosticV1 = durable.RejectionCode;
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.Rejected,
                    command,
                    candidate.Receipt,
                    durableEndDiagnosticV1);
            }

            authoritativeTick = command.AuthoritativeTick;
            lifecycleState = RunSessionLifecycleStateV1.Ended;
            terminalReceipt = candidate.Receipt;
            pendingDurableEndCandidateV1 = null;
            pendingDurableEndOperationStableIdV1 = null;
            pendingDurableEndCommandFingerprintV1 = string.Empty;
            durableEndStateV1 = RunSessionDurableEndStateV1.None;
            durableEndDiagnosticV1 = string.Empty;
            endReplay.Add(
                command.OperationStableId,
                new EndReplayRecord(command.Fingerprint, candidate));
            return candidate;
        }
    }
}
