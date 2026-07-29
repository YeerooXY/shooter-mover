using System;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    public enum GunResolvedEffectKind
    {
        ExplosionDamage = 1,
        DamageOverTimeApplication = 2,
        ChainArcDamage = 3,
    }

    public interface IGunResolvedEffectDecision
    {
        GunResolvedEffectKind Kind { get; }
        GunEffectSourceContext Source { get; }
        GunTargetReference Target { get; }
        GunDamageCategory DamageCategory { get; }
    }

    public sealed class GunExplosionDamageDecision : IGunResolvedEffectDecision
    {
        public GunExplosionDamageDecision(
            GunEffectSourceContext source,
            GunTargetReference target,
            GunVector2 targetPosition,
            GunDamageCategory damageCategory,
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

        public GunResolvedEffectKind Kind { get { return GunResolvedEffectKind.ExplosionDamage; } }
        public GunEffectSourceContext Source { get; }
        public GunTargetReference Target { get; }
        public GunVector2 TargetPosition { get; }
        public GunDamageCategory DamageCategory { get; }
        public double Damage { get; }
        public double DamageMultiplier { get; }
        public double Distance { get; }
        public double Knockback { get; }
    }

    public sealed class GunDamageOverTimeApplicationDecision : IGunResolvedEffectDecision
    {
        public GunDamageOverTimeApplicationDecision(
            GunEffectSourceContext source,
            GunTargetReference target,
            GunEffectApplicationKey applicationKey,
            GunDamageCategory damageCategory,
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

        public GunResolvedEffectKind Kind
        {
            get { return GunResolvedEffectKind.DamageOverTimeApplication; }
        }

        public GunEffectSourceContext Source { get; }
        public GunTargetReference Target { get; }
        public GunEffectApplicationKey ApplicationKey { get; }
        public GunDamageCategory DamageCategory { get; }
        public double DamagePerSecondPerStack { get; }
        public double DurationSeconds { get; }
        public double TicksPerSecond { get; }
        public int ResultingStackCount { get; }
        public double ResultingRemainingDurationSeconds { get; }
        public bool RefreshedDuration { get; }
    }

    public sealed class GunChainArcDamageDecision : IGunResolvedEffectDecision
    {
        public GunChainArcDamageDecision(
            GunEffectSourceContext source,
            GunTargetReference target,
            GunVector2 fromPosition,
            GunVector2 targetPosition,
            int jumpIndex,
            GunDamageCategory damageCategory,
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

        public GunResolvedEffectKind Kind { get { return GunResolvedEffectKind.ChainArcDamage; } }
        public GunEffectSourceContext Source { get; }
        public GunTargetReference Target { get; }
        public GunVector2 FromPosition { get; }
        public GunVector2 TargetPosition { get; }
        public int JumpIndex { get; }
        public GunDamageCategory DamageCategory { get; }
        public double Damage { get; }
        public double Knockback { get; }
    }
}
