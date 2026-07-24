using System;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    /// <summary>
    /// Loss-aware adapter into the retained firing profile and effect batch boundaries.
    /// It never selects behavior by weapon definition ID and never substitutes a fallback
    /// behavior when modular semantics cannot be represented exactly.
    /// </summary>
    public sealed class EffectiveWeaponRuntimeAdapter : IEffectiveWeaponRuntimeAdapter
    {
        private const double Epsilon = 0.000000001d;

        private readonly WeaponBehaviorRegistry behaviorRegistry;

        public EffectiveWeaponRuntimeAdapter(WeaponBehaviorRegistry behaviorRegistry)
        {
            this.behaviorRegistry = behaviorRegistry
                ?? throw new ArgumentNullException(nameof(behaviorRegistry));
        }

        public EffectiveWeaponRuntimeAdapterResult Adapt(
            EffectiveWeapon weapon,
            WeaponFiringScheduleEntry scheduleEntry)
        {
            if (weapon == null || scheduleEntry == null || !IsValidCommand(scheduleEntry.Command))
            {
                return Reject(
                    EffectiveWeaponRuntimeAdapterStatus.InvalidInput,
                    "weapon-runtime-adapter-input-invalid");
            }

            if (!weapon.EquipmentInstanceId.Equals(
                    scheduleEntry.Command.EquipmentInstanceId))
            {
                return Reject(
                    EffectiveWeaponRuntimeAdapterStatus.IdentityMismatch,
                    "weapon-runtime-adapter-equipment-instance-mismatch");
            }

            WeaponRuntimeFiringProfile profile;
            EffectiveWeaponRuntimeAdapterStatus profileStatus;
            string profileCode;
            if (!TryBuildProfile(
                    weapon,
                    scheduleEntry.CooldownTicks,
                    out profile,
                    out profileStatus,
                    out profileCode))
            {
                return Reject(profileStatus, profileCode);
            }

            IWeaponBehavior behavior;
            if (!behaviorRegistry.TryResolve(profile.BehaviorId, out behavior)
                || behavior == null)
            {
                return Reject(
                    EffectiveWeaponRuntimeAdapterStatus.UnknownBehavior,
                    "weapon-runtime-adapter-behavior-unregistered:" + profile.BehaviorId);
            }

            WeaponBehaviorBuildResult built;
            try
            {
                built = behavior.Build(
                    new WeaponBehaviorContext(
                        scheduleEntry.Command,
                        scheduleEntry.ParticipantId,
                        profile,
                        scheduleEntry.ShotSequence));
            }
            catch
            {
                return Reject(
                    EffectiveWeaponRuntimeAdapterStatus.BehaviorRejected,
                    "weapon-runtime-adapter-behavior-exception");
            }

            if (built == null || !built.Succeeded || built.Batch == null)
            {
                return Reject(
                    EffectiveWeaponRuntimeAdapterStatus.BehaviorRejected,
                    built == null
                        ? "weapon-runtime-adapter-behavior-null-result"
                        : string.IsNullOrWhiteSpace(built.RejectionCode)
                            ? "weapon-runtime-adapter-behavior-rejected"
                            : built.RejectionCode);
            }

            if (!HasExpectedIdentity(
                    weapon,
                    scheduleEntry,
                    built.Batch))
            {
                return Reject(
                    EffectiveWeaponRuntimeAdapterStatus.InvalidEffectBatch,
                    "weapon-runtime-adapter-effect-identity-invalid");
            }

            return EffectiveWeaponRuntimeAdapterResult.Adapted(profile, built.Batch);
        }

        private static bool TryBuildProfile(
            EffectiveWeapon weapon,
            int cooldownTicks,
            out WeaponRuntimeFiringProfile profile,
            out EffectiveWeaponRuntimeAdapterStatus status,
            out string code)
        {
            profile = null;

            if (weapon.FireSettings.IsContinuous)
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedFireMode,
                    "weapon-runtime-adapter-continuous-fire-unsupported",
                    out status,
                    out code);
            }

            if (weapon.FireSettings.Mode != WeaponFireMode.SemiAutomatic
                && weapon.FireSettings.Mode != WeaponFireMode.Automatic
                && weapon.FireSettings.Mode != WeaponFireMode.Burst)
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedFireMode,
                    "weapon-runtime-adapter-fire-mode-unsupported:" + weapon.FireSettings.Mode,
                    out status,
                    out code);
            }

            if (weapon.ShotPattern.PulsesPerShot != 1
                || weapon.ShotPattern.IntervalBetweenPulsesSeconds > Epsilon)
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedShotPattern,
                    "weapon-runtime-adapter-pulses-unsupported",
                    out status,
                    out code);
            }

            if (weapon.ShotPattern.RandomnessDegrees > Epsilon)
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedShotPattern,
                    "weapon-runtime-adapter-pattern-randomness-unsupported",
                    out status,
                    out code);
            }

            if (weapon.Guidance.Mode != WeaponGuidanceMode.Unguided)
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedGuidance,
                    "weapon-runtime-adapter-homing-unsupported",
                    out status,
                    out code);
            }

            if (weapon.Impact.Ricochet != null)
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedImpact,
                    "weapon-runtime-adapter-ricochet-unsupported",
                    out status,
                    out code);
            }

            bool hasExplosion = weapon.Effects.Explosion != null;
            bool hasDot = weapon.Effects.DamageOverTime != null;
            bool hasChain = weapon.Effects.ChainArc != null;
            int effectKinds = (hasExplosion ? 1 : 0) + (hasDot ? 1 : 0) + (hasChain ? 1 : 0);
            if (effectKinds > 1)
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedEffects,
                    "weapon-runtime-adapter-effect-combination-unsupported",
                    out status,
                    out code);
            }

            if (hasDot || weapon.Damage.HasDamageOverTime)
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedEffects,
                    "weapon-runtime-adapter-dot-unsupported",
                    out status,
                    out code);
            }

            if (hasChain)
            {
                return TryBuildChainProfile(
                    weapon,
                    cooldownTicks,
                    out profile,
                    out status,
                    out code);
            }

            return TryBuildProjectileProfile(
                weapon,
                cooldownTicks,
                hasExplosion,
                out profile,
                out status,
                out code);
        }

        private static bool TryBuildProjectileProfile(
            EffectiveWeapon weapon,
            int cooldownTicks,
            bool hasExplosion,
            out WeaponRuntimeFiringProfile profile,
            out EffectiveWeaponRuntimeAdapterStatus status,
            out string code)
        {
            profile = null;

            if (weapon.ShotPattern.Kind != WeaponShotPatternKind.Single
                && weapon.ShotPattern.Kind != WeaponShotPatternKind.Spread)
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedShotPattern,
                    "weapon-runtime-adapter-shot-pattern-unsupported:" + weapon.ShotPattern.Kind,
                    out status,
                    out code);
            }

            if (weapon.Projectile == null)
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedProjectile,
                    "weapon-runtime-adapter-projectile-required",
                    out status,
                    out code);
            }

            int pierce;
            if (!weapon.Projectile.Pierce.TryToLegacyInteger(out pierce))
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.FractionalPierceUnsupported,
                    "weapon-runtime-adapter-fractional-pierce-unsupported",
                    out status,
                    out code);
            }

            if (weapon.Projectile.Kind == WeaponProjectileKind.Orb)
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedProjectile,
                    "weapon-runtime-adapter-orb-unsupported",
                    out status,
                    out code);
            }

            if (!HasLegacyProjectileImpactShape(weapon, hasExplosion, pierce))
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedImpact,
                    "weapon-runtime-adapter-impact-policy-unsupported",
                    out status,
                    out code);
            }

            WeaponBehaviorId behaviorId;
            double areaDamage;
            double explosionRadius;
            if (hasExplosion)
            {
                if (weapon.Projectile.Kind != WeaponProjectileKind.Rocket
                    || weapon.Damage.AreaDamage <= 0d
                    || !ApproximatelyOne(
                        weapon.Effects.Explosion.MinimumDamageMultiplier))
                {
                    return Fail(
                        EffectiveWeaponRuntimeAdapterStatus.UnsupportedEffects,
                        "weapon-runtime-adapter-explosion-semantics-unsupported",
                        out status,
                        out code);
                }

                behaviorId = BuiltInWeaponBehaviorIds.Explosive;
                areaDamage = weapon.Damage.AreaDamage;
                explosionRadius = weapon.Effects.Explosion.Radius;
            }
            else
            {
                if (weapon.Projectile.Kind != WeaponProjectileKind.RegularProjectile
                    || weapon.Damage.AreaDamage > Epsilon)
                {
                    return Fail(
                        EffectiveWeaponRuntimeAdapterStatus.UnsupportedProjectile,
                        "weapon-runtime-adapter-projectile-kind-unsupported",
                        out status,
                        out code);
                }

                behaviorId = BuiltInWeaponBehaviorIds.Projectile;
                areaDamage = 0d;
                explosionRadius = 0d;
            }

            if (weapon.ShotPattern.ProjectilesPerShot < 1
                || weapon.ShotPattern.ProjectilesPerShot
                    > WeaponRuntimeFiringProfile.MaximumEffectsPerFire)
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedShotPattern,
                    "weapon-runtime-adapter-projectile-count-unsupported",
                    out status,
                    out code);
            }

            profile = new WeaponRuntimeFiringProfile(
                weapon.DefinitionId,
                behaviorId,
                cooldownTicks,
                weapon.ShotPattern.ProjectilesPerShot,
                weapon.ShotPattern.SpreadDegrees,
                weapon.Projectile.Speed,
                weapon.Projectile.Range,
                weapon.Damage.DirectDamage,
                pierce,
                areaDamage,
                explosionRadius,
                0d,
                0d,
                0d,
                0d,
                0,
                0d,
                weapon.Damage.Knockback,
                WeaponDamageCategoryConversion.ToCatalogValue(weapon.Damage.Category));
            status = EffectiveWeaponRuntimeAdapterStatus.Adapted;
            code = string.Empty;
            return true;
        }

        private static bool TryBuildChainProfile(
            EffectiveWeapon weapon,
            int cooldownTicks,
            out WeaponRuntimeFiringProfile profile,
            out EffectiveWeaponRuntimeAdapterStatus status,
            out string code)
        {
            profile = null;

            if (weapon.ShotPattern.Kind != WeaponShotPatternKind.Beam
                || weapon.Projectile != null
                || weapon.ShotPattern.ProjectilesPerShot != 0)
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedShotPattern,
                    "weapon-runtime-adapter-chain-delivery-unsupported",
                    out status,
                    out code);
            }

            if (weapon.Damage.AreaDamage > Epsilon
                || !ApproximatelyOne(weapon.Effects.ChainArc.RetainedDamagePerJump))
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedEffects,
                    "weapon-runtime-adapter-chain-retention-unsupported",
                    out status,
                    out code);
            }

            if (weapon.Impact.HandlesEnemyImpact
                || weapon.Impact.HandlesWallImpact
                || weapon.Impact.HandlesRangeExpiry
                || weapon.Impact.HandlesTermination
                || weapon.Impact.ExplosionTrigger != null)
            {
                return Fail(
                    EffectiveWeaponRuntimeAdapterStatus.UnsupportedImpact,
                    "weapon-runtime-adapter-chain-impact-policy-unsupported",
                    out status,
                    out code);
            }

            profile = new WeaponRuntimeFiringProfile(
                weapon.DefinitionId,
                BuiltInWeaponBehaviorIds.Chain,
                cooldownTicks,
                1,
                0d,
                0d,
                0d,
                weapon.Damage.DirectDamage,
                0,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                weapon.Effects.ChainArc.MaximumTargets,
                weapon.Effects.ChainArc.AcquisitionRange,
                weapon.Damage.Knockback,
                WeaponDamageCategoryConversion.ToCatalogValue(weapon.Damage.Category));
            status = EffectiveWeaponRuntimeAdapterStatus.Adapted;
            code = string.Empty;
            return true;
        }

        private static bool HasLegacyProjectileImpactShape(
            EffectiveWeapon weapon,
            bool hasExplosion,
            int pierce)
        {
            if (!weapon.Impact.HandlesEnemyImpact
                || !weapon.Impact.HandlesWallImpact
                || !weapon.Impact.HandlesRangeExpiry
                || !weapon.Impact.HandlesTermination)
            {
                return false;
            }

            if (hasExplosion)
            {
                WeaponExplosionTriggerSpec trigger = weapon.Impact.ExplosionTrigger;
                return trigger != null
                    && trigger.OnEnemyImpact
                    && trigger.OnWallImpact
                    && trigger.OnRangeExpiry
                    && trigger.OnTermination
                    && weapon.Projectile.TerminationBehavior
                        == WeaponProjectileTerminationBehavior.StopOnFirstBlockingImpact;
            }

            if (weapon.Impact.ExplosionTrigger != null)
            {
                return false;
            }

            if (pierce == 0)
            {
                return weapon.Projectile.TerminationBehavior
                    == WeaponProjectileTerminationBehavior.StopOnFirstBlockingImpact;
            }

            return weapon.Projectile.TerminationBehavior
                == WeaponProjectileTerminationBehavior.StopWhenPierceIsSpent;
        }

        private static bool HasExpectedIdentity(
            EffectiveWeapon weapon,
            WeaponFiringScheduleEntry scheduleEntry,
            WeaponEffectBatch batch)
        {
            if (batch == null
                || batch.EffectCount < 1
                || batch.EffectCount > WeaponRuntimeFiringProfile.MaximumEffectsPerFire)
            {
                return false;
            }

            for (int index = 0; index < batch.Effects.Count; index++)
            {
                IWeaponEffectDescription effect = batch.Effects[index];
                WeaponEffectIdentity identity = effect == null ? null : effect.Identity;
                if (identity == null
                    || !identity.ActorId.Equals(scheduleEntry.Command.ActorId)
                    || !identity.ParticipantId.Equals(scheduleEntry.ParticipantId)
                    || !identity.EquipmentInstanceId.Equals(weapon.EquipmentInstanceId)
                    || !identity.WeaponDefinitionId.Equals(weapon.DefinitionId)
                    || !identity.FireOperationId.Equals(scheduleEntry.Command.FireOperationId)
                    || !identity.LifecycleGeneration.Equals(
                        scheduleEntry.Command.LifecycleGeneration)
                    || identity.ShotSequence != scheduleEntry.ShotSequence
                    || identity.ProjectileOrdinal.Value != index)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidCommand(WeaponFireCommand command)
        {
            return command != null
                && command.SimulationTick >= 0L
                && command.Origin != null
                && command.Origin.IsFinite
                && command.AimDirection != null
                && command.AimDirection.IsFinite
                && command.AimDirection.LengthSquared > 0.000000000001d;
        }

        private static bool ApproximatelyOne(double value)
        {
            return Math.Abs(value - 1d) <= Epsilon;
        }

        private static bool Fail(
            EffectiveWeaponRuntimeAdapterStatus failureStatus,
            string failureCode,
            out EffectiveWeaponRuntimeAdapterStatus status,
            out string code)
        {
            status = failureStatus;
            code = failureCode;
            return false;
        }

        private static EffectiveWeaponRuntimeAdapterResult Reject(
            EffectiveWeaponRuntimeAdapterStatus status,
            string code)
        {
            return EffectiveWeaponRuntimeAdapterResult.Reject(status, code);
        }
    }
}
