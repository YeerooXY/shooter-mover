using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    /// <summary>
    /// Extends the normal transient RunCheckpointV1 with the immutable condition,
    /// effect, and modifier projection owned by the same run generation.
    /// This remains diagnostic/recovery data and never becomes permanent character truth.
    /// </summary>
    public sealed class RunConditionCheckpointV1
    {
        public RunConditionCheckpointV1(
            RunCheckpointV1 runCheckpoint,
            RunConditionRuntimeSnapshotV1 conditionRuntime)
        {
            RunCheckpoint = runCheckpoint
                ?? throw new ArgumentNullException(nameof(runCheckpoint));
            ConditionRuntime = conditionRuntime
                ?? throw new ArgumentNullException(nameof(conditionRuntime));
            RunDebugSnapshotV1 debug = RunCheckpoint.Recovery.Debug;
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
            Fingerprint = RunConditionHashV1.Hash(
                RunCheckpoint.Fingerprint + "|" + ConditionRuntime.Fingerprint);
        }

        public RunCheckpointV1 RunCheckpoint { get; }
        public RunConditionRuntimeSnapshotV1 ConditionRuntime { get; }
        public string Fingerprint { get; }
    }
}
