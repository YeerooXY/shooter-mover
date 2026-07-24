using System;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    /// <summary>
    /// Immutable output of the firing scheduler for one concrete shot emission.
    /// This type carries no scheduler state or cadence policy; it only freezes the
    /// already accepted shot sequence and cooldown used by the legacy runtime boundary.
    /// </summary>
    public sealed class WeaponFiringScheduleEntry
    {
        public WeaponFiringScheduleEntry(
            WeaponFireCommand command,
            RunParticipantId participantId,
            long shotSequence,
            int cooldownTicks)
        {
            if (shotSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(shotSequence));
            }
            if (cooldownTicks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownTicks));
            }

            Command = command ?? throw new ArgumentNullException(nameof(command));
            ParticipantId = participantId ?? throw new ArgumentNullException(nameof(participantId));
            ShotSequence = shotSequence;
            CooldownTicks = cooldownTicks;
        }

        public WeaponFireCommand Command { get; }
        public RunParticipantId ParticipantId { get; }
        public long ShotSequence { get; }
        public int CooldownTicks { get; }
    }

    public enum EffectiveWeaponRuntimeAdapterStatus
    {
        Adapted = 1,
        InvalidInput = 2,
        IdentityMismatch = 3,
        UnsupportedFireMode = 4,
        UnsupportedShotPattern = 5,
        UnsupportedProjectile = 6,
        FractionalPierceUnsupported = 7,
        UnsupportedGuidance = 8,
        UnsupportedImpact = 9,
        UnsupportedEffects = 10,
        UnknownBehavior = 11,
        BehaviorRejected = 12,
        InvalidEffectBatch = 13,
    }

    public sealed class EffectiveWeaponRuntimeAdapterResult
    {
        private EffectiveWeaponRuntimeAdapterResult(
            EffectiveWeaponRuntimeAdapterStatus status,
            WeaponRuntimeFiringProfile profile,
            WeaponEffectBatch batch,
            string rejectionCode)
        {
            Status = status;
            Profile = profile;
            Batch = batch;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public EffectiveWeaponRuntimeAdapterStatus Status { get; }
        public WeaponRuntimeFiringProfile Profile { get; }
        public WeaponEffectBatch Batch { get; }
        public string RejectionCode { get; }
        public bool Succeeded { get { return Status == EffectiveWeaponRuntimeAdapterStatus.Adapted; } }

        public static EffectiveWeaponRuntimeAdapterResult Adapted(
            WeaponRuntimeFiringProfile profile,
            WeaponEffectBatch batch)
        {
            return new EffectiveWeaponRuntimeAdapterResult(
                EffectiveWeaponRuntimeAdapterStatus.Adapted,
                profile ?? throw new ArgumentNullException(nameof(profile)),
                batch ?? throw new ArgumentNullException(nameof(batch)),
                string.Empty);
        }

        public static EffectiveWeaponRuntimeAdapterResult Reject(
            EffectiveWeaponRuntimeAdapterStatus status,
            string rejectionCode)
        {
            if (status == EffectiveWeaponRuntimeAdapterStatus.Adapted)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new EffectiveWeaponRuntimeAdapterResult(
                status,
                null,
                null,
                rejectionCode);
        }
    }

    /// <summary>
    /// The single typed migration seam from an immutable EffectiveWeapon and one
    /// accepted firing-schedule entry into the retained runtime profile and effect batch.
    /// </summary>
    public interface IEffectiveWeaponRuntimeAdapter
    {
        EffectiveWeaponRuntimeAdapterResult Adapt(
            EffectiveWeapon weapon,
            WeaponFiringScheduleEntry scheduleEntry);
    }
}
