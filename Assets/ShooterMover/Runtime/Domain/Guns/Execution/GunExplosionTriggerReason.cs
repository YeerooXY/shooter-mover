using System;

namespace ShooterMover.Domain.Guns.Execution
{
    /// <summary>
    /// Shared engine-independent reasons that request one explosion emission.
    /// Continuation and termination remain separate decisions.
    /// </summary>
    [Flags]
    public enum GunExplosionTriggerReason
    {
        None = 0,
        EnemyImpact = 1 << 0,
        WallImpact = 1 << 1,
        RangeExpiry = 1 << 2,
        Termination = 1 << 3,
    }

    public static class GunExplosionTriggerReasonRules
    {
        public const GunExplosionTriggerReason All =
            GunExplosionTriggerReason.EnemyImpact
            | GunExplosionTriggerReason.WallImpact
            | GunExplosionTriggerReason.RangeExpiry
            | GunExplosionTriggerReason.Termination;

        public static bool IsValid(GunExplosionTriggerReason reasons)
        {
            return (reasons & ~All) == 0;
        }
    }
}
