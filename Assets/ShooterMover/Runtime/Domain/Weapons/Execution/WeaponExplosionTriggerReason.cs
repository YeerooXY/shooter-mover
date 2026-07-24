using System;

namespace ShooterMover.Domain.Weapons.Execution
{
    /// <summary>
    /// Shared engine-independent reasons that request one explosion emission.
    /// Continuation and termination remain separate decisions.
    /// </summary>
    [Flags]
    public enum WeaponExplosionTriggerReason
    {
        None = 0,
        EnemyImpact = 1 << 0,
        WallImpact = 1 << 1,
        RangeExpiry = 1 << 2,
        Termination = 1 << 3,
    }

    public static class WeaponExplosionTriggerReasonRules
    {
        public const WeaponExplosionTriggerReason All =
            WeaponExplosionTriggerReason.EnemyImpact
            | WeaponExplosionTriggerReason.WallImpact
            | WeaponExplosionTriggerReason.RangeExpiry
            | WeaponExplosionTriggerReason.Termination;

        public static bool IsValid(WeaponExplosionTriggerReason reasons)
        {
            return (reasons & ~All) == 0;
        }
    }
}
