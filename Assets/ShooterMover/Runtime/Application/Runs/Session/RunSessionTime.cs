using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    public enum RunSessionTimeAdvanceStatus
    {
        Applied = 1,
        ExactReplay = 2,
        WrongRun = 3,
        StaleLifecycle = 4,
        RunEnded = 5,
        ConflictingDuplicate = 6,
        Rejected = 7,
    }

    /// <summary>
    /// Immutable command that advances the one canonical Run Session clock to an explicit
    /// simulation tick. The caller supplies a tick; the run never reads wall-clock or Unity time.
    /// </summary>
    public sealed class AdvanceRunSessionTimeCommand
    {
        public AdvanceRunSessionTimeCommand(
            StableId operationStableId,
            StableId runStableId,
            long lifecycleGeneration,
            long authoritativeTick)
        {
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            if (authoritativeTick < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(authoritativeTick));
            }
            LifecycleGeneration = lifecycleGeneration;
            AuthoritativeTick = authoritativeTick;
            Fingerprint = RunSessionFingerprint.Hash(
                OperationStableId
                + "|"
                + RunStableId
                + "|"
                + LifecycleGeneration
                + "|"
                + AuthoritativeTick);
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public long LifecycleGeneration { get; }
        public long AuthoritativeTick { get; }
        public string Fingerprint { get; }
    }

    public sealed class RunSessionTimeAdvanceResult
    {
        public RunSessionTimeAdvanceResult(
            RunSessionTimeAdvanceStatus status,
            AdvanceRunSessionTimeCommand command,
            long previousTick,
            long currentTick,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(RunSessionTimeAdvanceStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (previousTick < 0L || currentTick < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    previousTick < 0L ? nameof(previousTick) : nameof(currentTick));
            }
            Status = status;
            Command = command;
            PreviousTick = previousTick;
            CurrentTick = currentTick;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public RunSessionTimeAdvanceStatus Status { get; }
        public AdvanceRunSessionTimeCommand Command { get; }
        public long PreviousTick { get; }
        public long CurrentTick { get; }
        public string RejectionCode { get; }
        public bool Succeeded
        {
            get
            {
                return Status == RunSessionTimeAdvanceStatus.Applied
                    || Status == RunSessionTimeAdvanceStatus.ExactReplay;
            }
        }
    }

    public sealed partial class RunSessionAggregate
    {
        private sealed class TimeAdvanceReplayRecord
        {
            public TimeAdvanceReplayRecord(
                string commandFingerprint,
                RunSessionTimeAdvanceResult result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }
            public RunSessionTimeAdvanceResult Result { get; }
        }

        private readonly Dictionary<StableId, TimeAdvanceReplayRecord>
            timeAdvanceReplay =
                new Dictionary<StableId, TimeAdvanceReplayRecord>();

        /// <summary>
        /// Advances the canonical run clock monotonically. Exact operation replay returns the
        /// original result; conflicting reuse and tick regression reject without mutation.
        /// </summary>
        public RunSessionTimeAdvanceResult AdvanceTime(
            AdvanceRunSessionTimeCommand command)
        {
            long before = authoritativeTick;
            if (command == null)
            {
                return TimeResult(
                    RunSessionTimeAdvanceStatus.Rejected,
                    null,
                    before,
                    "run-time-command-null");
            }

            TimeAdvanceReplayRecord replay;
            if (timeAdvanceReplay.TryGetValue(command.OperationStableId, out replay))
            {
                if (string.Equals(
                        replay.CommandFingerprint,
                        command.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return new RunSessionTimeAdvanceResult(
                        RunSessionTimeAdvanceStatus.ExactReplay,
                        command,
                        replay.Result.PreviousTick,
                        replay.Result.CurrentTick,
                        string.Empty);
                }
                return TimeResult(
                    RunSessionTimeAdvanceStatus.ConflictingDuplicate,
                    command,
                    before,
                    "run-time-operation-conflict");
            }

            RunSessionTimeAdvanceStatus rejectionStatus;
            string rejection = ValidateTimeAdvance(command, out rejectionStatus);
            if (!string.IsNullOrEmpty(rejection))
            {
                RunSessionTimeAdvanceResult rejected = TimeResult(
                    rejectionStatus,
                    command,
                    before,
                    rejection);
                timeAdvanceReplay.Add(
                    command.OperationStableId,
                    new TimeAdvanceReplayRecord(command.Fingerprint, rejected));
                return rejected;
            }

            authoritativeTick = command.AuthoritativeTick;
            RunSessionTimeAdvanceResult applied = TimeResult(
                RunSessionTimeAdvanceStatus.Applied,
                command,
                before,
                string.Empty);
            timeAdvanceReplay.Add(
                command.OperationStableId,
                new TimeAdvanceReplayRecord(command.Fingerprint, applied));
            return applied;
        }

        private string ValidateTimeAdvance(
            AdvanceRunSessionTimeCommand command,
            out RunSessionTimeAdvanceStatus status)
        {
            status = RunSessionTimeAdvanceStatus.Rejected;
            if (command.RunStableId != RunStableId)
            {
                status = RunSessionTimeAdvanceStatus.WrongRun;
                return "run-time-wrong-run";
            }
            if (command.LifecycleGeneration != lifecycleGeneration)
            {
                status = RunSessionTimeAdvanceStatus.StaleLifecycle;
                return command.LifecycleGeneration < lifecycleGeneration
                    ? "run-time-stale-generation"
                    : "run-time-future-generation";
            }
            if (lifecycleState == RunSessionLifecycleState.Ended)
            {
                status = RunSessionTimeAdvanceStatus.RunEnded;
                return "run-time-after-end";
            }
            if (command.AuthoritativeTick < authoritativeTick)
            {
                return "run-time-tick-regression";
            }
            return string.Empty;
        }

        private RunSessionTimeAdvanceResult TimeResult(
            RunSessionTimeAdvanceStatus status,
            AdvanceRunSessionTimeCommand command,
            long previousTick,
            string rejectionCode)
        {
            return new RunSessionTimeAdvanceResult(
                status,
                command,
                previousTick,
                authoritativeTick,
                rejectionCode);
        }
    }
}
