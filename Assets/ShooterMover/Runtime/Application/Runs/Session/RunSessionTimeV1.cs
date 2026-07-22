using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    public enum RunSessionTimeAdvanceStatusV1
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
    public sealed class AdvanceRunSessionTimeCommandV1
    {
        public AdvanceRunSessionTimeCommandV1(
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
            Fingerprint = RunSessionFingerprintV1.Hash(
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

    public sealed class RunSessionTimeAdvanceResultV1
    {
        public RunSessionTimeAdvanceResultV1(
            RunSessionTimeAdvanceStatusV1 status,
            AdvanceRunSessionTimeCommandV1 command,
            long previousTick,
            long currentTick,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(RunSessionTimeAdvanceStatusV1), status))
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

        public RunSessionTimeAdvanceStatusV1 Status { get; }
        public AdvanceRunSessionTimeCommandV1 Command { get; }
        public long PreviousTick { get; }
        public long CurrentTick { get; }
        public string RejectionCode { get; }
        public bool Succeeded
        {
            get
            {
                return Status == RunSessionTimeAdvanceStatusV1.Applied
                    || Status == RunSessionTimeAdvanceStatusV1.ExactReplay;
            }
        }
    }

    public sealed partial class RunSessionAggregateV1
    {
        private sealed class TimeAdvanceReplayRecord
        {
            public TimeAdvanceReplayRecord(
                string commandFingerprint,
                RunSessionTimeAdvanceResultV1 result)
            {
                CommandFingerprint = commandFingerprint;
                Result = result;
            }

            public string CommandFingerprint { get; }
            public RunSessionTimeAdvanceResultV1 Result { get; }
        }

        private readonly Dictionary<StableId, TimeAdvanceReplayRecord>
            timeAdvanceReplay =
                new Dictionary<StableId, TimeAdvanceReplayRecord>();

        /// <summary>
        /// Advances the canonical run clock monotonically. Exact operation replay returns the
        /// original result; conflicting reuse and tick regression reject without mutation.
        /// </summary>
        public RunSessionTimeAdvanceResultV1 AdvanceTime(
            AdvanceRunSessionTimeCommandV1 command)
        {
            long before = authoritativeTick;
            if (command == null)
            {
                return TimeResult(
                    RunSessionTimeAdvanceStatusV1.Rejected,
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
                    return new RunSessionTimeAdvanceResultV1(
                        RunSessionTimeAdvanceStatusV1.ExactReplay,
                        command,
                        replay.Result.PreviousTick,
                        replay.Result.CurrentTick,
                        string.Empty);
                }
                return TimeResult(
                    RunSessionTimeAdvanceStatusV1.ConflictingDuplicate,
                    command,
                    before,
                    "run-time-operation-conflict");
            }

            RunSessionTimeAdvanceStatusV1 rejectionStatus;
            string rejection = ValidateTimeAdvance(command, out rejectionStatus);
            if (!string.IsNullOrEmpty(rejection))
            {
                RunSessionTimeAdvanceResultV1 rejected = TimeResult(
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
            RunSessionTimeAdvanceResultV1 applied = TimeResult(
                RunSessionTimeAdvanceStatusV1.Applied,
                command,
                before,
                string.Empty);
            timeAdvanceReplay.Add(
                command.OperationStableId,
                new TimeAdvanceReplayRecord(command.Fingerprint, applied));
            return applied;
        }

        private string ValidateTimeAdvance(
            AdvanceRunSessionTimeCommandV1 command,
            out RunSessionTimeAdvanceStatusV1 status)
        {
            status = RunSessionTimeAdvanceStatusV1.Rejected;
            if (command.RunStableId != RunStableId)
            {
                status = RunSessionTimeAdvanceStatusV1.WrongRun;
                return "run-time-wrong-run";
            }
            if (command.LifecycleGeneration != lifecycleGeneration)
            {
                status = RunSessionTimeAdvanceStatusV1.StaleLifecycle;
                return command.LifecycleGeneration < lifecycleGeneration
                    ? "run-time-stale-generation"
                    : "run-time-future-generation";
            }
            if (lifecycleState == RunSessionLifecycleStateV1.Ended)
            {
                status = RunSessionTimeAdvanceStatusV1.RunEnded;
                return "run-time-after-end";
            }
            if (command.AuthoritativeTick < authoritativeTick)
            {
                return "run-time-tick-regression";
            }
            return string.Empty;
        }

        private RunSessionTimeAdvanceResultV1 TimeResult(
            RunSessionTimeAdvanceStatusV1 status,
            AdvanceRunSessionTimeCommandV1 command,
            long previousTick,
            string rejectionCode)
        {
            return new RunSessionTimeAdvanceResultV1(
                status,
                command,
                previousTick,
                authoritativeTick,
                rejectionCode);
        }
    }
}
