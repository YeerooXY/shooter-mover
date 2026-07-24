using System;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public enum AcceptedEmissionRuntimeAdapterStatus
    {
        Adapted = 1,
        InvalidInput = 2,
        IdentityMismatch = 3,
        UnsupportedFireMode = 4,
        UnsupportedShotPattern = 5,
        UnsupportedProjectile = 6,
        UnsupportedGuidance = 7,
        UnsupportedImpact = 8,
        UnsupportedEffects = 9,
        FractionalPierceUnsupported = 10,
        UnknownBehavior = 11,
        BehaviorRejected = 12,
        InvalidEffectBatch = 13,
        NumericalFailure = 14,
    }

    public sealed class AcceptedEmissionRuntimeAdapterResult
    {
        private AcceptedEmissionRuntimeAdapterResult(
            AcceptedEmissionRuntimeAdapterStatus status,
            string rejectionCode,
            WeaponRuntimeFiringProfile profile,
            WeaponEffectBatch batch)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Profile = profile;
            Batch = batch;
        }

        public AcceptedEmissionRuntimeAdapterStatus Status { get; }
        public string RejectionCode { get; }
        public WeaponRuntimeFiringProfile Profile { get; }
        public WeaponEffectBatch Batch { get; }
        public bool Succeeded
        {
            get
            {
                return Status == AcceptedEmissionRuntimeAdapterStatus.Adapted
                    && Profile != null
                    && Batch != null;
            }
        }

        public static AcceptedEmissionRuntimeAdapterResult Adapted(
            WeaponRuntimeFiringProfile profile,
            WeaponEffectBatch batch)
        {
            return new AcceptedEmissionRuntimeAdapterResult(
                AcceptedEmissionRuntimeAdapterStatus.Adapted,
                string.Empty,
                profile ?? throw new ArgumentNullException(nameof(profile)),
                batch ?? throw new ArgumentNullException(nameof(batch)));
        }

        public static AcceptedEmissionRuntimeAdapterResult Reject(
            AcceptedEmissionRuntimeAdapterStatus status,
            string rejectionCode)
        {
            if (status == AcceptedEmissionRuntimeAdapterStatus.Adapted)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (string.IsNullOrWhiteSpace(rejectionCode))
            {
                throw new ArgumentException(
                    "A stable adapter rejection code is required.",
                    nameof(rejectionCode));
            }

            return new AcceptedEmissionRuntimeAdapterResult(
                status,
                rejectionCode,
                null,
                null);
        }
    }
}
