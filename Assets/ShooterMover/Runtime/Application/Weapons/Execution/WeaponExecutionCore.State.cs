using System;
using System.Collections.Generic;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public sealed partial class WeaponExecutionCore
    {
        private sealed class BatchBuildResult
        {
            private BatchBuildResult(
                WeaponExecutionStatus status,
                WeaponEffectBatch batch,
                string rejectionCode)
            {
                Status = status;
                Batch = batch;
                RejectionCode = rejectionCode ?? string.Empty;
            }

            public WeaponExecutionStatus Status { get; }
            public WeaponEffectBatch Batch { get; }
            public string RejectionCode { get; }
            public bool Succeeded { get { return Batch != null; } }

            public static BatchBuildResult Accept(WeaponEffectBatch batch)
            {
                return new BatchBuildResult(
                    WeaponExecutionStatus.Accepted,
                    batch ?? throw new ArgumentNullException(nameof(batch)),
                    string.Empty);
            }

            public static BatchBuildResult Reject(
                WeaponExecutionStatus status,
                string rejectionCode)
            {
                return new BatchBuildResult(status, null, rejectionCode);
            }
        }

        private sealed class StateKey : IEquatable<StateKey>
        {
            public StateKey(
                WeaponActorInstanceId actor,
                EquipmentInstanceId equipment,
                LifecycleGeneration generation)
            {
                Actor = actor;
                Equipment = equipment;
                Generation = generation;
            }

            public WeaponActorInstanceId Actor { get; }
            public EquipmentInstanceId Equipment { get; }
            public LifecycleGeneration Generation { get; }

            public bool Equals(StateKey other)
            {
                return !ReferenceEquals(other, null)
                    && Actor.Equals(other.Actor)
                    && Equipment.Equals(other.Equipment)
                    && Generation.Equals(other.Generation);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as StateKey);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Actor.GetHashCode();
                    hash = (hash * 397) ^ Equipment.GetHashCode();
                    return (hash * 397) ^ Generation.GetHashCode();
                }
            }
        }

        private sealed class OperationKey : IEquatable<OperationKey>
        {
            public OperationKey(
                WeaponActorInstanceId actor,
                LifecycleGeneration generation,
                FireOperationId operation)
            {
                Actor = actor;
                Generation = generation;
                Operation = operation;
            }

            public WeaponActorInstanceId Actor { get; }
            public LifecycleGeneration Generation { get; }
            public FireOperationId Operation { get; }

            public bool Equals(OperationKey other)
            {
                return !ReferenceEquals(other, null)
                    && Actor.Equals(other.Actor)
                    && Generation.Equals(other.Generation)
                    && Operation.Equals(other.Operation);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as OperationKey);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Actor.GetHashCode();
                    hash = (hash * 397) ^ Generation.GetHashCode();
                    return (hash * 397) ^ Operation.GetHashCode();
                }
            }
        }

        private sealed class AcceptedFireOperation
        {
            public AcceptedFireOperation(
                EquipmentInstanceId equipmentInstanceId,
                string commandFingerprint,
                string batchFingerprint,
                long shotSequence,
                int effectCount)
            {
                EquipmentInstanceId = equipmentInstanceId
                    ?? throw new ArgumentNullException(nameof(equipmentInstanceId));
                if (string.IsNullOrWhiteSpace(commandFingerprint))
                {
                    throw new ArgumentException(
                        "Command fingerprint is required.",
                        nameof(commandFingerprint));
                }

                if (string.IsNullOrWhiteSpace(batchFingerprint))
                {
                    throw new ArgumentException(
                        "Batch fingerprint is required.",
                        nameof(batchFingerprint));
                }

                if (shotSequence < 0L)
                {
                    throw new ArgumentOutOfRangeException(nameof(shotSequence));
                }

                if (effectCount < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(effectCount));
                }

                CommandFingerprint = commandFingerprint;
                BatchFingerprint = batchFingerprint;
                ShotSequence = shotSequence;
                EffectCount = effectCount;
            }

            public EquipmentInstanceId EquipmentInstanceId { get; }
            public string CommandFingerprint { get; }
            public string BatchFingerprint { get; }
            public long ShotSequence { get; }
            public int EffectCount { get; }

            public bool MatchesCommand(string commandFingerprint)
            {
                return string.Equals(
                    CommandFingerprint,
                    commandFingerprint,
                    StringComparison.Ordinal);
            }

            public bool MatchesBatch(string batchFingerprint)
            {
                return string.Equals(
                    BatchFingerprint,
                    batchFingerprint,
                    StringComparison.Ordinal);
            }
        }

        private sealed class FireState
        {
            private FireState(long nextAllowedTick, long shotSequence)
            {
                NextAllowedTick = nextAllowedTick;
                ShotSequence = shotSequence;
            }

            public static FireState Initial
            {
                get { return new FireState(0L, 0L); }
            }

            public long NextAllowedTick { get; }
            public long ShotSequence { get; }

            public FireState AfterAccepted(long nextAllowedTick)
            {
                return new FireState(nextAllowedTick, ShotSequence + 1L);
            }
        }
    }
}
