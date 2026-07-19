using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public static class WeaponDeterministicSpread
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;
        private const double Unit53 = 1d / 9007199254740992d;

        public static WeaponVector2 DirectionFor(
            WeaponVector2 baseDirection,
            double spreadDegrees,
            ulong seed,
            FireOperationId operationId,
            EquipmentInstanceId equipmentId,
            long shotSequence,
            ProjectileOrdinal ordinal)
        {
            if (baseDirection == null)
            {
                throw new ArgumentNullException(nameof(baseDirection));
            }

            if (operationId == null)
            {
                throw new ArgumentNullException(nameof(operationId));
            }

            if (equipmentId == null)
            {
                throw new ArgumentNullException(nameof(equipmentId));
            }

            if (ordinal == null)
            {
                throw new ArgumentNullException(nameof(ordinal));
            }

            string facts = seed.ToString(CultureInfo.InvariantCulture)
                + "|" + operationId
                + "|" + equipmentId
                + "|" + shotSequence.ToString(CultureInfo.InvariantCulture)
                + "|" + ordinal;

            ulong hash = Offset;
            for (int index = 0; index < facts.Length; index++)
            {
                hash ^= facts[index];
                hash *= Prime;
            }

            double unit = (hash >> 11) * Unit53;
            double offsetDegrees = (unit - 0.5d) * spreadDegrees;
            return baseDirection.Normalized.RotateDegrees(offsetDegrees).Normalized;
        }
    }

    public sealed class ProjectileWeaponBehavior : IWeaponBehavior
    {
        public WeaponBehaviorId BehaviorId
        {
            get { return BuiltInWeaponBehaviorIds.Projectile; }
        }

        public WeaponBehaviorBuildResult Build(WeaponBehaviorContext context)
        {
            if (context == null)
            {
                return WeaponBehaviorBuildResult.Reject("weapon-context-missing");
            }

            List<IWeaponEffectDescription> effects =
                new List<IWeaponEffectDescription>(context.Profile.ProjectileCount);
            for (int index = 0; index < context.Profile.ProjectileCount; index++)
            {
                WeaponVector2 direction = Direction(context, index);
                effects.Add(
                    new DirectProjectileEffect(
                        context.IdentityFor(index),
                        context.Command.Origin,
                        direction,
                        context.Profile.ProjectileSpeed,
                        context.Profile.ProjectileRange,
                        context.Profile.DirectDamage,
                        context.Profile.Pierce,
                        context.Profile.Knockback,
                        context.Profile.DamageType));
            }

            return WeaponBehaviorBuildResult.Accept(new WeaponEffectBatch(effects));
        }

        private static WeaponVector2 Direction(WeaponBehaviorContext context, int index)
        {
            return WeaponDeterministicSpread.DirectionFor(
                context.Command.AimDirection,
                context.Profile.SpreadDegrees,
                context.Command.DeterministicSeed,
                context.Command.FireOperationId,
                context.Command.EquipmentInstanceId,
                context.ShotSequence,
                new ProjectileOrdinal(index));
        }
    }

    public sealed class ExplosiveWeaponBehavior : IWeaponBehavior
    {
        public WeaponBehaviorId BehaviorId
        {
            get { return BuiltInWeaponBehaviorIds.Explosive; }
        }

        public WeaponBehaviorBuildResult Build(WeaponBehaviorContext context)
        {
            if (context == null)
            {
                return WeaponBehaviorBuildResult.Reject("weapon-context-missing");
            }

            List<IWeaponEffectDescription> effects =
                new List<IWeaponEffectDescription>(context.Profile.ProjectileCount);
            for (int index = 0; index < context.Profile.ProjectileCount; index++)
            {
                WeaponVector2 direction = WeaponDeterministicSpread.DirectionFor(
                    context.Command.AimDirection,
                    context.Profile.SpreadDegrees,
                    context.Command.DeterministicSeed,
                    context.Command.FireOperationId,
                    context.Command.EquipmentInstanceId,
                    context.ShotSequence,
                    new ProjectileOrdinal(index));
                effects.Add(
                    new ExplosiveProjectileEffect(
                        context.IdentityFor(index),
                        context.Command.Origin,
                        direction,
                        context.Profile.ProjectileSpeed,
                        context.Profile.ProjectileRange,
                        context.Profile.DirectDamage,
                        context.Profile.AreaDamage,
                        context.Profile.ExplosionRadius,
                        context.Profile.Knockback,
                        context.Profile.DamageType));
            }

            return WeaponBehaviorBuildResult.Accept(new WeaponEffectBatch(effects));
        }
    }

    public sealed class DamageOverTimeWeaponBehavior : IWeaponBehavior
    {
        public WeaponBehaviorId BehaviorId
        {
            get { return BuiltInWeaponBehaviorIds.DamageOverTime; }
        }

        public WeaponBehaviorBuildResult Build(WeaponBehaviorContext context)
        {
            if (context == null)
            {
                return WeaponBehaviorBuildResult.Reject("weapon-context-missing");
            }

            List<IWeaponEffectDescription> effects =
                new List<IWeaponEffectDescription>(context.Profile.ProjectileCount);
            for (int index = 0; index < context.Profile.ProjectileCount; index++)
            {
                WeaponVector2 direction = WeaponDeterministicSpread.DirectionFor(
                    context.Command.AimDirection,
                    context.Profile.SpreadDegrees,
                    context.Command.DeterministicSeed,
                    context.Command.FireOperationId,
                    context.Command.EquipmentInstanceId,
                    context.ShotSequence,
                    new ProjectileOrdinal(index));
                effects.Add(
                    new DamageOverTimeProjectileEffect(
                        context.IdentityFor(index),
                        context.Command.Origin,
                        direction,
                        context.Profile.ProjectileSpeed,
                        context.Profile.ProjectileRange,
                        context.Profile.DirectDamage,
                        context.Profile.Pierce,
                        context.Profile.DotDps,
                        context.Profile.DotDuration,
                        context.Profile.PoolRadius,
                        context.Profile.PoolDuration,
                        context.Profile.Knockback,
                        context.Profile.DamageType));
            }

            return WeaponBehaviorBuildResult.Accept(new WeaponEffectBatch(effects));
        }
    }

    public sealed class ChainWeaponBehavior : IWeaponBehavior
    {
        public WeaponBehaviorId BehaviorId
        {
            get { return BuiltInWeaponBehaviorIds.Chain; }
        }

        public WeaponBehaviorBuildResult Build(WeaponBehaviorContext context)
        {
            if (context == null)
            {
                return WeaponBehaviorBuildResult.Reject("weapon-context-missing");
            }

            ChainArcEffect effect = new ChainArcEffect(
                context.IdentityFor(0),
                context.Command.Origin,
                context.Command.AimDirection.Normalized,
                context.Profile.DirectDamage,
                context.Profile.ChainTargets,
                context.Profile.ChainRange,
                context.Profile.Knockback,
                context.Profile.DamageType);
            return WeaponBehaviorBuildResult.Accept(
                new WeaponEffectBatch(new List<IWeaponEffectDescription> { effect }));
        }
    }
}
