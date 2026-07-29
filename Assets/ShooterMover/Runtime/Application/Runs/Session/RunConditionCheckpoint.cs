using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    /// <summary>
    /// Extends the normal transient RunCheckpoint with the immutable condition,
    /// effect, and modifier projection owned by the same run generation.
    /// This remains diagnostic/recovery data and never becomes permanent character truth.
    /// </summary>
    public sealed class RunConditionCheckpoint
    {
        public RunConditionCheckpoint(
            RunCheckpoint runCheckpoint,
            RunConditionLiveSnapshot conditionRuntime)
        {
            RunCheckpoint = runCheckpoint
                ?? throw new ArgumentNullException(nameof(runCheckpoint));
            ConditionRuntime = conditionRuntime
                ?? throw new ArgumentNullException(nameof(conditionRuntime));
            RunDebugSnapshot debug = RunCheckpoint.Recovery.Debug;
            if (debug.RunStableId != ConditionRuntime.RunStableId)
            {
                throw new ArgumentException(
                    "The condition checkpoint must belong to the exact run.",
                    nameof(conditionRuntime));
            }
            if (debug.LifecycleGeneration
                != ConditionRuntime.LifecycleGeneration)
            {
                throw new ArgumentException(
                    "The condition checkpoint must belong to the exact run lifecycle.",
                    nameof(conditionRuntime));
            }
            if (RunCheckpoint.Recovery.IsPermanentCharacterTruth)
            {
                throw new ArgumentException(
                    "Condition checkpoints cannot become permanent character truth.",
                    nameof(runCheckpoint));
            }
            Fingerprint = RunConditionHash.Hash(
                RunCheckpoint.Fingerprint + "|" + ConditionRuntime.Fingerprint);
        }

        public RunCheckpoint RunCheckpoint { get; }
        public RunConditionLiveSnapshot ConditionRuntime { get; }
        public string Fingerprint { get; }
    }
}
