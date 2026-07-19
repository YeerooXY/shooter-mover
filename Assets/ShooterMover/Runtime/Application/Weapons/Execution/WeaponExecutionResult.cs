using System;
using System.Collections.Generic;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public enum WeaponExecutionStatus
    {
        Accepted = 1,
        InvalidCommand = 2,
        UnknownActorOwnership = 3,
        MissingEquippedEquipment = 4,
        InvalidEquipment = 5,
        UnknownWeaponDefinition = 6,
        PreviewOnlyWeaponDefinition = 7,
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

    public sealed class WeaponExecutionResult
    {
        private WeaponExecutionResult(
            WeaponExecutionStatus status,
            string rejectionCode,
            int effectCount,
            long shotSequence)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            EffectCount = effectCount;
            ShotSequence = shotSequence;
        }

        public WeaponExecutionStatus Status { get; }
        public string RejectionCode { get; }
        public int EffectCount { get; }
        public long ShotSequence { get; }
        public bool Succeeded { get { return Status == WeaponExecutionStatus.Accepted; } }

        public static WeaponExecutionResult Accept(int count, long sequence)
        {
            return new WeaponExecutionResult(
                WeaponExecutionStatus.Accepted,
                string.Empty,
                count,
                sequence);
        }

        public static WeaponExecutionResult Replay(int count, long sequence)
        {
            return new WeaponExecutionResult(
                WeaponExecutionStatus.ReplayAccepted,
                "weapon-operation-already-accepted",
                count,
                sequence);
        }

        public static WeaponExecutionResult Reject(
            WeaponExecutionStatus status,
            string code,
            long sequence)
        {
            if (status == WeaponExecutionStatus.Accepted)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new WeaponExecutionResult(status, code, 0, sequence);
        }
    }
}
