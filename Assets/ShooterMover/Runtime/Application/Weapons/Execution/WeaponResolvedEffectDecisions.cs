using System;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public enum WeaponResolvedEffectKind
    {
        ExplosionDamage = 1,
        DamageOverTimeApplication = 2,
        ChainArcDamage = 3,
    }

    public interface IWeaponResolvedEffectDecision
    {
        WeaponResolvedEffectKind Kind { get; }
        WeaponEffectSourceContext Source { get; }
        WeaponTargetReference Target { get; }
        WeaponDamageCategory DamageCategory { get; }
    }

    public sealed class WeaponExplosionDamageDecision : IWeaponResolvedEffectDecision
    {
        public WeaponExplosionDamageDecision(
            WeaponEffectSourceContext source,
            WeaponTargetReference target,
            WeaponVector2 targetPosition,
            WeaponDamageCategory damageCategory,
            double damage,
            double damageMultiplier,
            double distance,
            double knockback)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            TargetPosition = targetPosition ?? throw new ArgumentNullException(nameof(targetPosition));
            DamageCategory = damageCategory;
            Damage = damage;
            DamageMultiplier = damageMultiplier;
            Distance = distance;
            Knockback = knockback;
        }

        public WeaponResolvedEffectKind Kind { get { return WeaponResolvedEffectKind.ExplosionDamage; } }
        public WeaponEffectSourceContext Source { get; }
        public WeaponTargetReference Target { get; }
        public WeaponVector2 TargetPosition { get; }
        public WeaponDamageCategory DamageCategory { get; }
        public double Damage { get; }
        public double DamageMultiplier { get; }
        public double Distance { get; }
        public double Knockback { get; }
    }

    public sealed class WeaponDamageOverTimeApplicationDecision : IWeaponResolvedEffectDecision
    {
        public WeaponDamageOverTimeApplicationDecision(
            WeaponEffectSourceContext source,
            WeaponTargetReference target,
            WeaponEffectApplicationKey applicationKey,
            WeaponDamageCategory damageCategory,
            double damagePerSecondPerStack,
            double durationSeconds,
            double ticksPerSecond,
            int resultingStackCount,
            double resultingRemainingDurationSeconds,
            bool refreshedDuration)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            ApplicationKey = applicationKey
                ?? throw new ArgumentNullException(nameof(applicationKey));
            DamageCategory = damageCategory;
            DamagePerSecondPerStack = damagePerSecondPerStack;
            DurationSeconds = durationSeconds;
            TicksPerSecond = ticksPerSecond;
            ResultingStackCount = resultingStackCount;
            ResultingRemainingDurationSeconds = resultingRemainingDurationSeconds;
            RefreshedDuration = refreshedDuration;
        }

        public WeaponResolvedEffectKind Kind
        {
            get { return WeaponResolvedEffectKind.DamageOverTimeApplication; }
        }

        public WeaponEffectSourceContext Source { get; }
        public WeaponTargetReference Target { get; }
        public WeaponEffectApplicationKey ApplicationKey { get; }
        public WeaponDamageCategory DamageCategory { get; }
        public double DamagePerSecondPerStack { get; }
        public double DurationSeconds { get; }
        public double TicksPerSecond { get; }
        public int ResultingStackCount { get; }
        public double ResultingRemainingDurationSeconds { get; }
        public bool RefreshedDuration { get; }
    }

    public sealed class WeaponChainArcDamageDecision : IWeaponResolvedEffectDecision
    {
        public WeaponChainArcDamageDecision(
            WeaponEffectSourceContext source,
            WeaponTargetReference target,
            WeaponVector2 fromPosition,
            WeaponVector2 targetPosition,
            int jumpIndex,
            WeaponDamageCategory damageCategory,
            double damage,
            double knockback)
        {
            if (jumpIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(jumpIndex));
            }

            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            FromPosition = fromPosition ?? throw new ArgumentNullException(nameof(fromPosition));
            TargetPosition = targetPosition ?? throw new ArgumentNullException(nameof(targetPosition));
            JumpIndex = jumpIndex;
            DamageCategory = damageCategory;
            Damage = damage;
            Knockback = knockback;
        }

        public WeaponResolvedEffectKind Kind { get { return WeaponResolvedEffectKind.ChainArcDamage; } }
        public WeaponEffectSourceContext Source { get; }
        public WeaponTargetReference Target { get; }
        public WeaponVector2 FromPosition { get; }
        public WeaponVector2 TargetPosition { get; }
        public int JumpIndex { get; }
        public WeaponDamageCategory DamageCategory { get; }
        public double Damage { get; }
        public double Knockback { get; }
    }
}
