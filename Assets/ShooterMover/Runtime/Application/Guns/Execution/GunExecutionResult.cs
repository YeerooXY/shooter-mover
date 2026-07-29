using System;
using System.Collections.Generic;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    public enum GunExecutionStatus
    {
        Accepted = 1,
        InvalidCommand = 2,
        UnknownActorOwnership = 3,
        MissingEquippedEquipment = 4,
        InvalidEquipment = 5,
        UnknownGunDefinition = 6,
        PreviewOnlyGunDefinition = 7,
        InvalidTuning = 8,
        UnsupportedEffects = 9,
        UnknownBehavior = 10,
        CooldownActive = 11,
        ReplayAccepted = 12,
        BehaviorRejected = 13,
        InvalidEffectBatch = 14,
        SinkRejected = 15,
        ConflictingDuplicate = 16,
    }

    public sealed class GunExecutionResult
    {
        private GunExecutionResult(
            GunExecutionStatus status,
            string rejectionCode,
            int effectCount,
            long shotSequence)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            EffectCount = effectCount;
            ShotSequence = shotSequence;
        }

        public GunExecutionStatus Status { get; }
        public string RejectionCode { get; }
        public int EffectCount { get; }
        public long ShotSequence { get; }
        public bool Succeeded { get { return Status == GunExecutionStatus.Accepted; } }

        public static GunExecutionResult Accept(int count, long sequence)
        {
            return new GunExecutionResult(
                GunExecutionStatus.Accepted,
                string.Empty,
                count,
                sequence);
        }

        public static GunExecutionResult Replay(int count, long sequence)
        {
            return new GunExecutionResult(
                GunExecutionStatus.ReplayAccepted,
                "gun-operation-already-accepted",
                count,
                sequence);
        }

        public static GunExecutionResult Reject(
            GunExecutionStatus status,
            string code,
            long sequence)
        {
            if (status == GunExecutionStatus.Accepted)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new GunExecutionResult(status, code, 0, sequence);
        }
    }
}
