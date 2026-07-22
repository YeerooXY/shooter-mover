using System;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    public enum RunSessionDurableAcceptanceStatusV1
    {
        Accepted = 1,
        SafelyRejectedBeforeDurability = 2,
        DurableStateUncertain = 3,
    }

    public enum RunSessionDurableEndStateV1
    {
        None = 1,
        PendingExactRetry = 2,
        DurableStateUncertain = 3,
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
            RejectedBeforeDurability(string rejectionCode)
        {
            return new RunSessionDurableAcceptanceResultV1(
                RunSessionDurableAcceptanceStatusV1
                    .SafelyRejectedBeforeDurability,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "run-end-durable-acceptance-safely-rejected"
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

        // Retained compatibility for callers that explicitly mean a safe rejection.
        public static RunSessionDurableAcceptanceResultV1 Rejected(
            string rejectionCode)
        {
            return RejectedBeforeDurability(rejectionCode);
        }
    }

    public sealed partial class RunSessionAggregateV1
    {
        private RunSessionEndResultV1 pendingDurableEndCandidateV1;
        private StableId pendingDurableEndOperationStableIdV1;
        private string pendingDurableEndCommandFingerprintV1 = string.Empty;
        private bool durableEndStateUncertainV1;

        public RunSessionDurableEndStateV1 DurableEndState
        {
            get
            {
                if (durableEndStateUncertainV1)
                    return RunSessionDurableEndStateV1
                        .DurableStateUncertain;
                return pendingDurableEndCandidateV1 == null
                    ? RunSessionDurableEndStateV1.None
                    : RunSessionDurableEndStateV1.PendingExactRetry;
            }
        }

        /// <summary>
        /// Ends a run only after the accepted mission result has crossed a caller-supplied
        /// durable acceptance boundary. Once the mission-result authority accepts, the exact
        /// candidate transaction is retained. A safe durability rejection can only retry that
        /// candidate; it never reopens normal mission-result construction. An uncertain
        /// durability outcome freezes the transaction permanently for external recovery.
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
                if (durableEndStateUncertainV1)
                {
                    return new RunSessionEndResultV1(
                        RunSessionEndStatusV1.Rejected,
                        command,
                        candidate.Receipt,
                        "run-end-durable-state-uncertain");
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
            }

            RunSessionDurableAcceptanceResultV1 durable;
            try
            {
                durable = acceptDurably(candidate);
            }
            catch (Exception exception)
            {
                durableEndStateUncertainV1 = true;
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.Rejected,
                    command,
                    candidate.Receipt,
                    "run-end-durable-acceptance-threw:"
                    + exception.GetType().Name);
            }

            if (durable == null)
            {
                durableEndStateUncertainV1 = true;
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.Rejected,
                    command,
                    candidate.Receipt,
                    "run-end-durable-acceptance-result-null");
            }

            if (durable.Status
                == RunSessionDurableAcceptanceStatusV1
                    .DurableStateUncertain)
            {
                durableEndStateUncertainV1 = true;
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.Rejected,
                    command,
                    candidate.Receipt,
                    durable.RejectionCode);
            }

            if (durable.Status
                == RunSessionDurableAcceptanceStatusV1
                    .SafelyRejectedBeforeDurability)
            {
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.Rejected,
                    command,
                    candidate.Receipt,
                    durable.RejectionCode);
            }

            authoritativeTick = command.AuthoritativeTick;
            lifecycleState = RunSessionLifecycleStateV1.Ended;
            terminalReceipt = candidate.Receipt;
            pendingDurableEndCandidateV1 = null;
            pendingDurableEndOperationStableIdV1 = null;
            pendingDurableEndCommandFingerprintV1 = string.Empty;
            durableEndStateUncertainV1 = false;
            endReplay.Add(
                command.OperationStableId,
                new EndReplayRecord(command.Fingerprint, candidate));
            return candidate;
        }
    }
}
