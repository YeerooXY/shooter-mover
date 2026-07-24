using System;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    /// <summary>
    /// Loss-aware projection from one scheduler-authorized emission into the retained
    /// behavior-registry and immutable effect-batch boundaries. It never admits cadence,
    /// reconstructs bursts, or selects behavior by weapon definition ID.
    /// </summary>
    public sealed class AcceptedEmissionRuntimeAdapter
    {
        private const double Epsilon = 0.000000001d;

        private readonly WeaponBehaviorRegistry behaviorRegistry;

        public AcceptedEmissionRuntimeAdapter(WeaponBehaviorRegistry registry)
        {
            behaviorRegistry = registry
                ?? throw new ArgumentNullException(nameof(registry));
        }

        public AcceptedEmissionRuntimeAdapterResult Adapt(
            EffectiveWeapon weapon,
            WeaponFiringScheduler.AcceptedEmission acceptedEmission)
        {
            if (weapon == null
                || acceptedEmission == null
                || !acceptedEmission.HasValidFingerprint(weapon)
                || !IsValidCommand(acceptedEmission.Command))
            {
                return Reject(
                    AcceptedEmissionRuntimeAdapterStatus.InvalidInput,
                    "weapon-runtime-accepted-emission-invalid");
            }

            if (!acceptedEmission.EquipmentInstanceId.Equals(
                    acceptedEmission.Command.EquipmentInstanceId)
                || !weapon.EquipmentInstanceId.Equals(
                    acceptedEmission.EquipmentInstanceId))
            {
                return Reject(
                    AcceptedEmissionRuntimeAdapterStatus.IdentityMismatch,
                    "weapon-runtime-equipment-instance-mismatch");
            }
            if (!weapon.DefinitionId.Equals(
                    acceptedEmission.WeaponDefinitionId))
            {
                return Reject(
                    AcceptedEmissionRuntimeAdapterStatus.IdentityMismatch,
                    "weapon-runtime-definition-mismatch");
            }
            if (!acceptedEmission.EmissionFireOperationId.Equals(
                    acceptedEmission.Command.FireOperationId))
            {
                return Reject(
                    AcceptedEmissionRuntimeAdapterStatus.IdentityMismatch,
                    "weapon-runtime-emission-operation-mismatch");
            }

            int cooldownTicks;
            if (acceptedEmission.TicksUntilNextEmission > int.MaxValue)
            {
                return Reject(
                    AcceptedEmissionRuntimeAdapterStatus.NumericalFailure,
                    "weapon-runtime-cooldown-projection-overflow");
            }
            cooldownTicks = (int)acceptedEmission.TicksUntilNextEmission;

            WeaponRuntimeFiringProfile profile;
            AcceptedEmissionRuntimeAdapterStatus profileStatus;
            string profileCode;
            if (!TryBuildProfile(
                    weapon,
                    cooldownTicks,
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
                    AcceptedEmissionRuntimeAdapterStatus.UnknownBehavior,
                    "weapon-runtime-behavior-unregistered:" + profile.BehaviorId);
            }

            WeaponBehaviorBuildResult built;
            try
            {
                built = behavior.Build(
                    new WeaponBehaviorContext(
                        acceptedEmission.Command,
                        acceptedEmission.ParticipantId,
                        profile,
                        acceptedEmission.ShotSequence));
            }
            catch
            {
                return Reject(
                    AcceptedEmissionRuntimeAdapterStatus.BehaviorRejected,
                    "weapon-runtime-behavior-exception");
            }

            if (built == null || !built.Succeeded || built.Batch == null)
            {
                return Reject(
                    AcceptedEmissionRuntimeAdapterStatus.BehaviorRejected,
                    built == null
                        ? "weapon-runtime-behavior-null-result"
                        : string.IsNullOrWhiteSpace(built.RejectionCode)
                            ? "weapon-runtime-behavior-rejected"
                            : built.RejectionCode);
            }

            if (!HasExpectedBatch(
                    weapon,
                    acceptedEmission,
                    profile,
                    built.Batch))
            {
                return Reject(
                    AcceptedEmissionRuntimeAdapterStatus.InvalidEffectBatch,
                    "weapon-runtime-effect-batch-invalid");
            }

            return AcceptedEmissionRuntimeAdapterResult.Adapted(
                profile,
                built.Batch);
        }

        private static bool TryBuildProfile(
            EffectiveWeapon weapon,
            int cooldownTicks,
            out WeaponRuntimeFiringProfile profile,
            out AcceptedEmissionRuntimeAdapterStatus status,
            out string code)
        {
            profile = null;

            if (weapon.FireSettings.IsContinuous)
            {
                return Fail(
                    AcceptedEmissionRuntimeAdapterStatus.UnsupportedFireMode,
                    "weapon-runtime-continuous-fire-unsupported",
                    out status,
                    out code);
            }
            if (weapon.FireSettings.Mode != WeaponFireMode.SemiAutomatic
                && weapon.FireSettings.Mode != WeaponFireMode.Automatic
                && weapon.FireSettings.Mode != WeaponFireMode.Burst)
            {
                return Fail(
                    AcceptedEmissionRuntimeAdapterStatus.UnsupportedFireMode,
                    "weapon-runtime-fire-mode-unsupported:"
                        + weapon.FireSettings.Mode,
                    out status,
                    out code);
            }

            if (weapon.ShotPattern.RandomnessDegrees > Epsilon)
            {
                return Fail(
                    AcceptedEmissionRuntimeAdapterStatus.UnsupportedShotPattern,
                    "weapon-runtime-pattern-randomness-unsupported",
                    out status,
                    out code);
            }
            if (weapon.ShotPattern.Kind == WeaponShotPatternKind.TwinBarrel)
            {
                return Fail(
                    AcceptedEmissionRuntimeAdapterStatus.UnsupportedShotPattern,
                    "weapon-runtime-twin-barrel-unsupported",
                    out status,
                    out code);
            }

            if (weapon.Guidance.Mode != WeaponGuidanceMode.Unguided)
            {
                return Fail(
                    AcceptedEmissionRuntimeAdapterStatus.UnsupportedGuidance,
                    "weapon-runtime-homing-unsupported",
                    out status,
                    out code);
            }
            if (weapon.Impact.Ricochet != null)
            {
                return Fail(
                    AcceptedEmissionRuntimeAdapterStatus.UnsupportedImpact,
                    "weapon-runtime-ricochet-unsupported",
                    out status,
                    out code);
            }

            bool hasExplosion = weapon.Effects.Explosion != null;
            bool hasDot = weapon.Effects.DamageOverTime != null;
            bool hasChain = weapon.Effects.ChainArc != null;
            int effectKinds = (hasExplosion ? 1 : 0)
                + (hasDot ? 1 : 0)
                + (hasChain ? 1 : 0);
            if (effectKinds > 1)
            {
                return Fail(
                    AcceptedEmissionRuntimeAdapterStatus.UnsupportedEffects,
                    "weapon-runtime-effect-combination-unsupported",
                    out status,
                    out code);
            }
            if (hasChain)
            {
                return Fail(
                    AcceptedEmissionRuntimeAdapterStatus.UnsupportedEffects,
                    "weapon-runtime-chain-unsupported",
                    out status,
                    out code);
            }
            if (hasDot || weapon.Damage.HasDamageOverTime)
            {
                return Fail(
                    AcceptedEmissionRuntimeAdapterStatus.UnsupportedEffects,
                    "weapon-runtime-dot-unsupported",
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
            out AcceptedEmissionRuntimeAdapterStatus status,
            out string code)
        {
            profile = null;

            if (weapon.ShotPattern.Kind != WeaponShotPatternKind.Single
                && weapon.ShotPattern.Kind != WeaponShotPatternKind.Spread
                && weapon.ShotPattern.Kind != WeaponShotPatternKind.PulseSpread)
            {
                return Fail(
                    AcceptedEmissionRuntimeAdapterStatus.UnsupportedShotPattern,
                    "weapon-runtime-shot-pattern-unsupported:"
                        + weapon.ShotPattern.Kind,
                    out status,
                    out code);
            }
            if (weapon.Projectile == null)
            {
                return Fail(
                    AcceptedEmissionRuntimeAdapterStatus.UnsupportedProjectile,
                    "weapon-runtime-projectile-required",
                    out status,
                    out code);
            }

            int pierce;
            if (!weapon.Projectile.Pierce.TryToLegacyInteger(out pierce))
            {
                return Fail(
                    AcceptedEmissionRuntimeAdapterStatus.FractionalPierceUnsupported,
                    "weapon-runtime-fractional-pierce-unsupported",
                    out status,
                    out code);
            }
            if (weapon.Projectile.Kind == WeaponProjectileKind.Orb)
            {
                return Fail(
                    AcceptedEmissionRuntimeAdapterStatus.UnsupportedProjectile,
                    "weapon-runtime-orb-unsupported",
                    out status,
                    out code);
            }
            if (!HasLegacyProjectileImpactShape(weapon, hasExplosion, pierce))
            {
                return Fail(
                    AcceptedEmissionRuntimeAdapterStatus.UnsupportedImpact,
                    "weapon-runtime-impact-policy-unsupported",
                    out status,
                    out code);
            }

            WeaponBehaviorId behaviorId;
            double areaDamage;
            double explosionRadius;
            if (hasExplosion)
            {
                if (weapon.Projectile.Kind != WeaponProjectileKind.Rocket
                    || pierce != 0
                    || weapon.Damage.AreaDamage <= 0d
                    || !ApproximatelyOne(
                        weapon.Effects.Explosion.MinimumDamageMultiplier))
                {
                    return Fail(
                        AcceptedEmissionRuntimeAdapterStatus.UnsupportedEffects,
                        "weapon-runtime-explosion-semantics-unsupported",
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
                        AcceptedEmissionRuntimeAdapterStatus.UnsupportedProjectile,
                        "weapon-runtime-projectile-kind-unsupported",
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
                    AcceptedEmissionRuntimeAdapterStatus.UnsupportedShotPattern,
                    "weapon-runtime-projectile-count-unsupported",
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
                WeaponDamageCategoryConversion.ToCatalogValue(
                    weapon.Damage.Category));
            status = AcceptedEmissionRuntimeAdapterStatus.Adapted;
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
                WeaponExplosionTriggerSpec trigger =
                    weapon.Impact.ExplosionTrigger;
                return trigger != null
                    && trigger.OnEnemyImpact
                    && trigger.OnWallImpact
                    && trigger.OnRangeExpiry
                    && trigger.OnTermination
                    && weapon.Projectile.TerminationBehavior
                        == WeaponProjectileTerminationBehavior
                            .StopOnFirstBlockingImpact;
            }

            if (weapon.Impact.ExplosionTrigger != null)
            {
                return false;
            }
            if (pierce == 0)
            {
                return weapon.Projectile.TerminationBehavior
                    == WeaponProjectileTerminationBehavior
                        .StopOnFirstBlockingImpact;
            }
            return weapon.Projectile.TerminationBehavior
                == WeaponProjectileTerminationBehavior.StopWhenPierceIsSpent;
        }

        private static bool HasExpectedBatch(
            EffectiveWeapon weapon,
            WeaponFiringScheduler.AcceptedEmission acceptedEmission,
            WeaponRuntimeFiringProfile profile,
            WeaponEffectBatch batch)
        {
            if (batch == null
                || batch.Identity == null
                || batch.EffectCount < 1
                || batch.EffectCount > WeaponRuntimeFiringProfile.MaximumEffectsPerFire
                || batch.EffectCount != profile.ProjectileCount)
            {
                return false;
            }

            for (int index = 0; index < batch.Effects.Count; index++)
            {
                IWeaponEffectDescription effect = batch.Effects[index];
                WeaponEffectIdentity identity =
                    effect == null ? null : effect.Identity;
                if (identity == null
                    || !identity.ActorId.Equals(
                        acceptedEmission.Command.ActorId)
                    || !identity.ParticipantId.Equals(
                        acceptedEmission.ParticipantId)
                    || !identity.EquipmentInstanceId.Equals(
                        weapon.EquipmentInstanceId)
                    || !identity.WeaponDefinitionId.Equals(
                        weapon.DefinitionId)
                    || !identity.FireOperationId.Equals(
                        acceptedEmission.EmissionFireOperationId)
                    || !identity.LifecycleGeneration.Equals(
                        acceptedEmission.Command.LifecycleGeneration)
                    || identity.ShotSequence != acceptedEmission.ShotSequence
                    || identity.ProjectileOrdinal.Value != index
                    || !HasExpectedPayload(profile, effect))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasExpectedPayload(
            WeaponRuntimeFiringProfile profile,
            IWeaponEffectDescription effect)
        {
            if (profile.BehaviorId.Equals(BuiltInWeaponBehaviorIds.Projectile))
            {
                DirectProjectileEffect direct =
                    effect as DirectProjectileEffect;
                return direct != null
                    && Same(direct.Speed, profile.ProjectileSpeed)
                    && Same(direct.Range, profile.ProjectileRange)
                    && Same(direct.DirectDamage, profile.DirectDamage)
                    && direct.Pierce == profile.Pierce
                    && Same(direct.Knockback, profile.Knockback)
                    && string.Equals(
                        direct.DamageType,
                        profile.DamageType,
                        StringComparison.Ordinal);
            }

            if (profile.BehaviorId.Equals(BuiltInWeaponBehaviorIds.Explosive))
            {
                ExplosiveProjectileEffect explosive =
                    effect as ExplosiveProjectileEffect;
                return explosive != null
                    && Same(explosive.Speed, profile.ProjectileSpeed)
                    && Same(explosive.Range, profile.ProjectileRange)
                    && Same(explosive.DirectDamage, profile.DirectDamage)
                    && Same(explosive.AreaDamage, profile.AreaDamage)
                    && Same(explosive.ExplosionRadius, profile.ExplosionRadius)
                    && Same(explosive.Knockback, profile.Knockback)
                    && string.Equals(
                        explosive.DamageType,
                        profile.DamageType,
                        StringComparison.Ordinal);
            }

            return false;
        }

        private static bool IsValidCommand(WeaponFireCommand command)
        {
            return command != null
                && command.ActorId != null
                && command.EquipmentInstanceId != null
                && command.FireOperationId != null
                && command.LifecycleGeneration != null
                && command.SimulationTick >= 0L
                && command.Origin != null
                && command.Origin.IsFinite
                && command.AimDirection != null
                && command.AimDirection.IsFinite
                && command.AimDirection.LengthSquared > Epsilon;
        }

        private static bool Same(double left, double right)
        {
            return left.Equals(right);
        }

        private static bool ApproximatelyOne(double value)
        {
            return Math.Abs(value - 1d) <= Epsilon;
        }

        private static bool Fail(
            AcceptedEmissionRuntimeAdapterStatus failureStatus,
            string failureCode,
            out AcceptedEmissionRuntimeAdapterStatus status,
            out string code)
        {
            status = failureStatus;
            code = failureCode;
            return false;
        }

        private static AcceptedEmissionRuntimeAdapterResult Reject(
            AcceptedEmissionRuntimeAdapterStatus status,
            string code)
        {
            return AcceptedEmissionRuntimeAdapterResult.Reject(status, code);
        }
    }
}
