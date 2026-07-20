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
            Validate(
                baseDirection,
                spreadDegrees,
                operationId,
                equipmentId,
                ordinal);
            double unit = UnitFor(
                seed,
                operationId,
                equipmentId,
                shotSequence,
                null,
                ordinal);
            double offsetDegrees = spreadDegrees <= 0d
                ? 0d
                : (unit - 0.5d) * spreadDegrees;
            return baseDirection.Normalized
                .RotateDegrees(offsetDegrees)
                .Normalized;
        }

        public static WeaponVector2 DirectionFor(
            WeaponVector2 baseDirection,
            double spreadDegrees,
            ulong seed,
            FireOperationId operationId,
            EquipmentInstanceId equipmentId,
            long shotSequence,
            int projectileCount,
            ProjectileOrdinal ordinal)
        {
            Validate(
                baseDirection,
                spreadDegrees,
                operationId,
                equipmentId,
                ordinal);
            if (projectileCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileCount));
            }
            if (ordinal.Value >= projectileCount)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            }
            if (projectileCount == 1)
            {
                return DirectionFor(
                    baseDirection,
                    spreadDegrees,
                    seed,
                    operationId,
                    equipmentId,
                    shotSequence,
                    ordinal);
            }

            double unit = UnitFor(
                seed,
                operationId,
                equipmentId,
                shotSequence,
                projectileCount,
                ordinal);
            double offsetDegrees = MultiProjectileOffsetDegrees(
                spreadDegrees,
                projectileCount,
                ordinal.Value,
                unit);
            return baseDirection.Normalized
                .RotateDegrees(offsetDegrees)
                .Normalized;
        }

        private static void Validate(
            WeaponVector2 baseDirection,
            double spreadDegrees,
            FireOperationId operationId,
            EquipmentInstanceId equipmentId,
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
            if (double.IsNaN(spreadDegrees)
                || double.IsInfinity(spreadDegrees)
                || spreadDegrees < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(spreadDegrees));
            }
        }

        private static double UnitFor(
            ulong seed,
            FireOperationId operationId,
            EquipmentInstanceId equipmentId,
            long shotSequence,
            int? projectileCount,
            ProjectileOrdinal ordinal)
        {
            string facts = seed.ToString(CultureInfo.InvariantCulture)
                + "|" + operationId
                + "|" + equipmentId
                + "|" + shotSequence.ToString(CultureInfo.InvariantCulture)
                + (projectileCount.HasValue
                    ? "|" + projectileCount.Value.ToString(
                        CultureInfo.InvariantCulture)
                    : string.Empty)
                + "|" + ordinal;

            ulong hash = Offset;
            for (int index = 0; index < facts.Length; index++)
            {
                hash ^= facts[index];
                hash *= Prime;
            }

            // Adjacent decimal ordinals primarily changed FNV's low bits. Sampling the
            // upper 53 bits directly therefore collapsed pellets 0..6 onto one angle.
            // Avalanche first so the complete immutable identity reaches retained bits.
            hash = Avalanche(hash);
            return (hash >> 11) * Unit53;
        }

        private static double MultiProjectileOffsetDegrees(
            double spreadDegrees,
            int projectileCount,
            int ordinal,
            double unit)
        {
            if (spreadDegrees <= 0d)
            {
                return 0d;
            }

            // Give every projectile one ordered lane across the configured cone and
            // apply only small bounded jitter inside the lane. This is deterministic,
            // readable, and cannot collapse a shotgun batch onto one trajectory.
            double halfSpread = spreadDegrees * 0.5d;
            double step = spreadDegrees / (projectileCount - 1);
            double laneCenter = -halfSpread + (ordinal * step);
            double jitter = (unit - 0.5d) * step * 0.2d;
            double result = laneCenter + jitter;
            if (result < -halfSpread)
            {
                return -halfSpread;
            }
            if (result > halfSpread)
            {
                return halfSpread;
            }
            return result;
        }

        private static ulong Avalanche(ulong value)
        {
            value ^= value >> 30;
            value *= 0xbf58476d1ce4e5b9UL;
            value ^= value >> 27;
            value *= 0x94d049bb133111ebUL;
            value ^= value >> 31;
            return value;
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

        private static WeaponVector2 Direction(
            WeaponBehaviorContext context,
            int index)
        {
            return WeaponDeterministicSpread.DirectionFor(
                context.Command.AimDirection,
                context.Profile.SpreadDegrees,
                context.Command.DeterministicSeed,
                context.Command.FireOperationId,
                context.Command.EquipmentInstanceId,
                context.ShotSequence,
                context.Profile.ProjectileCount,
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
                    context.Profile.ProjectileCount,
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
                    context.Profile.ProjectileCount,
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
