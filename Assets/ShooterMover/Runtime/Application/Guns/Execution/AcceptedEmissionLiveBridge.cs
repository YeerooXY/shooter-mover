using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    /// <summary>
    /// Loss-aware projection from one scheduler-authorized emission. Canonical travelling
    /// deliveries are launched directly from the immutable EffectiveGun projection. Only
    /// transitional catalogue projections may continue through the retained behavior registry
    /// and GunEffectBatch compatibility route.
    /// </summary>
    public sealed class AcceptedEmissionLiveBridge
    {
        private const double Epsilon = 0.000000001d;

        private static readonly StableId ProjectileExecutionPurpose =
            StableId.Parse("gun.projectile-execution");

        private readonly GunBehaviorRegistry behaviorRegistry;

        public AcceptedEmissionLiveBridge(GunBehaviorRegistry registry)
        {
            behaviorRegistry = registry
                ?? throw new ArgumentNullException(nameof(registry));
        }

        public AcceptedEmissionLiveBridgeResult Adapt(
            EffectiveGun gun,
            GunFiringScheduler.AcceptedEmission acceptedEmission)
        {
            if (gun == null
                || acceptedEmission == null
                || acceptedEmission.Kind
                    != GunFiringEmissionKind.ProjectileShot
                || !acceptedEmission.HasValidFingerprint(gun)
                || !IsValidCommand(acceptedEmission.Command))
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.InvalidInput,
                    "gun-runtime-accepted-emission-invalid");
            }

            if (!acceptedEmission.EquipmentInstanceId.Equals(
                    acceptedEmission.Command.EquipmentInstanceId)
                || !gun.EquipmentInstanceId.Equals(
                    acceptedEmission.EquipmentInstanceId))
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.IdentityMismatch,
                    "gun-runtime-equipment-instance-mismatch");
            }
            if (!gun.DefinitionId.Equals(
                    acceptedEmission.GunDefinitionId))
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.IdentityMismatch,
                    "gun-runtime-definition-mismatch");
            }
            if (!acceptedEmission.EmissionFireOperationId.Equals(
                    acceptedEmission.Command.FireOperationId))
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.IdentityMismatch,
                    "gun-runtime-emission-operation-mismatch");
            }

            int cooldownTicks;
            if (acceptedEmission.TicksUntilNextEmission > int.MaxValue)
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.NumericalFailure,
                    "gun-runtime-cooldown-projection-overflow");
            }
            cooldownTicks = (int)acceptedEmission.TicksUntilNextEmission;

            if (gun.UsesCanonicalAuthoredDefinition)
            {
                return AdaptCanonicalProjectile(
                    gun,
                    acceptedEmission,
                    cooldownTicks);
            }

            if (gun.Blueprint == null
                || !gun.Blueprint.IsTransitionalCatalogProjection)
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.InvalidProjectileProfile,
                    "gun-runtime-transitional-blueprint-required");
            }

            GunLiveFiringProfile profile;
            AcceptedEmissionLiveBridgeStatus profileStatus;
            string profileCode;
            if (!TryBuildProfile(
                    gun,
                    cooldownTicks,
                    out profile,
                    out profileStatus,
                    out profileCode))
            {
                return Reject(profileStatus, profileCode);
            }

            IGunBehavior behavior;
            if (!behaviorRegistry.TryResolve(profile.BehaviorId, out behavior)
                || behavior == null)
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.UnknownBehavior,
                    "gun-runtime-behavior-unregistered:" + profile.BehaviorId);
            }

            GunBehaviorBuildResult built;
            try
            {
                built = behavior.Build(
                    new GunBehaviorContext(
                        acceptedEmission.Command,
                        acceptedEmission.ParticipantId,
                        profile,
                        acceptedEmission.ShotSequence));
            }
            catch
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.BehaviorRejected,
                    "gun-runtime-behavior-exception");
            }

            if (built == null || !built.Succeeded || built.Batch == null)
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.BehaviorRejected,
                    built == null
                        ? "gun-runtime-behavior-null-result"
                        : string.IsNullOrWhiteSpace(built.RejectionCode)
                            ? "gun-runtime-behavior-rejected"
                            : built.RejectionCode);
            }

            if (!HasExpectedBatch(
                    gun,
                    acceptedEmission,
                    profile,
                    built.Batch))
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.InvalidEffectBatch,
                    "gun-runtime-effect-batch-invalid");
            }

            return AcceptedEmissionLiveBridgeResult.Adapted(
                profile,
                built.Batch);
        }

        private static AcceptedEmissionLiveBridgeResult AdaptCanonicalProjectile(
            EffectiveGun gun,
            GunFiringScheduler.AcceptedEmission acceptedEmission,
            int cooldownTicks)
        {
            if (gun.FireSettings.IsContinuous)
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedFireMode,
                    "gun-runtime-canonical-travelling-continuous-rejected");
            }
            if (gun.ShotPattern.Kind != GunShotPatternKind.Single
                && gun.ShotPattern.Kind != GunShotPatternKind.Spread
                && gun.ShotPattern.Kind != GunShotPatternKind.PulseSpread)
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedShotPattern,
                    "gun-runtime-canonical-shot-pattern-unsupported:"
                        + gun.ShotPattern.Kind);
            }
            if (gun.ShotPattern.RandomnessDegrees > Epsilon)
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedShotPattern,
                    "gun-runtime-canonical-pattern-randomness-unsupported");
            }
            if (gun.ShotPattern.ProjectilesPerShot < 1
                || gun.ShotPattern.ProjectilesPerShot
                    > GunLiveFiringProfile.MaximumEffectsPerFire)
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedShotPattern,
                    "gun-runtime-canonical-projectile-count-unsupported");
            }

            ProjectileExecutionProfile profile;
            try
            {
                profile = ProjectileExecutionProfile.From(gun);
            }
            catch (OverflowException)
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.NumericalFailure,
                    "gun-runtime-canonical-projectile-profile-overflow");
            }
            catch (InvalidOperationException exception)
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.InvalidProjectileProfile,
                    string.IsNullOrWhiteSpace(exception.Message)
                        ? "gun-runtime-canonical-projectile-profile-invalid"
                        : exception.Message);
            }
            catch (ArgumentException)
            {
                return Reject(
                    AcceptedEmissionLiveBridgeStatus.InvalidProjectileProfile,
                    "gun-runtime-canonical-projectile-profile-invalid");
            }

            var launches = new List<AcceptedProjectileLaunch>(
                gun.ShotPattern.ProjectilesPerShot);
            for (int index = 0; index < gun.ShotPattern.ProjectilesPerShot; index++)
            {
                try
                {
                    ProjectileOrdinal ordinal = new ProjectileOrdinal(index);
                    GunEffectIdentity sourceIdentity = new GunEffectIdentity(
                        acceptedEmission.Command.ActorId,
                        acceptedEmission.ParticipantId,
                        gun.EquipmentInstanceId,
                        gun.DefinitionId,
                        acceptedEmission.EmissionFireOperationId,
                        acceptedEmission.Command.LifecycleGeneration,
                        acceptedEmission.ShotSequence,
                        ordinal);
                    ProjectileExecutionIdentity projectileIdentity =
                        new ProjectileExecutionIdentity(sourceIdentity);
                    DeterministicRandom random = DeterministicRandom.CreateSubstream(
                        acceptedEmission.Command.DeterministicSeed,
                        DeterministicRandom.CurrentAlgorithmVersion,
                        ProjectileExecutionPurpose,
                        checked((ulong)index));
                    ProjectileLifecycleContext lifecycle = new ProjectileLifecycleContext(
                        projectileIdentity,
                        acceptedEmission.ScheduledTick,
                        random);
                    GunVector2 direction = GunDeterministicSpread.DirectionFor(
                        acceptedEmission.Command.AimDirection,
                        gun.ShotPattern.SpreadDegrees,
                        acceptedEmission.Command.DeterministicSeed,
                        acceptedEmission.EmissionFireOperationId,
                        gun.EquipmentInstanceId,
                        acceptedEmission.ShotSequence,
                        ordinal);
                    ProjectileLaunchRequest request = new ProjectileLaunchRequest(
                        lifecycle,
                        profile,
                        acceptedEmission.Command.Origin,
                        direction,
                        null);
                    launches.Add(new AcceptedProjectileLaunch(request));
                }
                catch (OverflowException)
                {
                    return Reject(
                        AcceptedEmissionLiveBridgeStatus.NumericalFailure,
                        "gun-runtime-canonical-projectile-launch-overflow");
                }
                catch (Exception)
                {
                    return Reject(
                        AcceptedEmissionLiveBridgeStatus.InvalidProjectileLaunch,
                        "gun-runtime-canonical-projectile-launch-invalid");
                }
            }

            return AcceptedEmissionLiveBridgeResult.CanonicalProjectile(
                profile,
                launches,
                cooldownTicks,
                gun.ShotPattern.SpreadDegrees);
        }

        private static bool TryBuildProfile(
            EffectiveGun gun,
            int cooldownTicks,
            out GunLiveFiringProfile profile,
            out AcceptedEmissionLiveBridgeStatus status,
            out string code)
        {
            profile = null;

            if (gun.FireSettings.IsContinuous)
            {
                return Fail(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedFireMode,
                    "gun-runtime-continuous-fire-unsupported",
                    out status,
                    out code);
            }
            if (gun.FireSettings.Mode != GunFireMode.SemiAutomatic
                && gun.FireSettings.Mode != GunFireMode.Automatic
                && gun.FireSettings.Mode != GunFireMode.Burst)
            {
                return Fail(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedFireMode,
                    "gun-runtime-fire-mode-unsupported:"
                        + gun.FireSettings.Mode,
                    out status,
                    out code);
            }

            if (gun.ShotPattern.RandomnessDegrees > Epsilon)
            {
                return Fail(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedShotPattern,
                    "gun-runtime-pattern-randomness-unsupported",
                    out status,
                    out code);
            }
            if (gun.ShotPattern.Kind == GunShotPatternKind.TwinBarrel)
            {
                return Fail(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedShotPattern,
                    "gun-runtime-twin-barrel-unsupported",
                    out status,
                    out code);
            }

            if (gun.Guidance.Mode != GunGuidanceMode.Unguided)
            {
                return Fail(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedGuidance,
                    "gun-runtime-homing-unsupported",
                    out status,
                    out code);
            }
            if (gun.Impact.Ricochet != null)
            {
                return Fail(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedImpact,
                    "gun-runtime-ricochet-unsupported",
                    out status,
                    out code);
            }

            bool hasExplosion = gun.Effects.Explosion != null;
            bool hasDot = gun.Effects.DamageOverTime != null;
            bool hasChain = gun.Effects.ChainArc != null;
            int effectKinds = (hasExplosion ? 1 : 0)
                + (hasDot ? 1 : 0)
                + (hasChain ? 1 : 0);
            if (effectKinds > 1)
            {
                return Fail(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedEffects,
                    "gun-runtime-effect-combination-unsupported",
                    out status,
                    out code);
            }
            if (hasChain)
            {
                return Fail(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedEffects,
                    "gun-runtime-chain-unsupported",
                    out status,
                    out code);
            }
            if (hasDot || gun.Damage.HasDamageOverTime)
            {
                return Fail(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedEffects,
                    "gun-runtime-dot-unsupported",
                    out status,
                    out code);
            }

            return TryBuildProjectileProfile(
                gun,
                cooldownTicks,
                hasExplosion,
                out profile,
                out status,
                out code);
        }

        private static bool TryBuildProjectileProfile(
            EffectiveGun gun,
            int cooldownTicks,
            bool hasExplosion,
            out GunLiveFiringProfile profile,
            out AcceptedEmissionLiveBridgeStatus status,
            out string code)
        {
            profile = null;

            if (gun.ShotPattern.Kind != GunShotPatternKind.Single
                && gun.ShotPattern.Kind != GunShotPatternKind.Spread
                && gun.ShotPattern.Kind != GunShotPatternKind.PulseSpread)
            {
                return Fail(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedShotPattern,
                    "gun-runtime-shot-pattern-unsupported:"
                        + gun.ShotPattern.Kind,
                    out status,
                    out code);
            }
            if (gun.Projectile == null)
            {
                return Fail(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedProjectile,
                    "gun-runtime-projectile-required",
                    out status,
                    out code);
            }

            int pierce;
            if (!gun.Projectile.Pierce.TryToLegacyInteger(out pierce))
            {
                return Fail(
                    AcceptedEmissionLiveBridgeStatus.FractionalPierceUnsupported,
                    "gun-runtime-fractional-pierce-unsupported",
                    out status,
                    out code);
            }
            if (gun.Projectile.Kind == GunProjectileKind.Orb)
            {
                return Fail(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedProjectile,
                    "gun-runtime-orb-unsupported",
                    out status,
                    out code);
            }
            if (!HasLegacyProjectileImpactShape(gun, hasExplosion, pierce))
            {
                return Fail(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedImpact,
                    "gun-runtime-impact-policy-unsupported",
                    out status,
                    out code);
            }

            GunBehaviorId behaviorId;
            double areaDamage;
            double explosionRadius;
            if (hasExplosion)
            {
                if (gun.Projectile.Kind != GunProjectileKind.Rocket
                    || pierce != 0
                    || gun.Damage.AreaDamage <= 0d
                    || !ApproximatelyOne(
                        gun.Effects.Explosion.MinimumDamageMultiplier))
                {
                    return Fail(
                        AcceptedEmissionLiveBridgeStatus.UnsupportedEffects,
                        "gun-runtime-explosion-semantics-unsupported",
                        out status,
                        out code);
                }

                behaviorId = BuiltInGunBehaviorIds.Explosive;
                areaDamage = gun.Damage.AreaDamage;
                explosionRadius = gun.Effects.Explosion.Radius;
            }
            else
            {
                if (gun.Projectile.Kind != GunProjectileKind.RegularProjectile
                    || gun.Damage.AreaDamage > Epsilon)
                {
                    return Fail(
                        AcceptedEmissionLiveBridgeStatus.UnsupportedProjectile,
                        "gun-runtime-projectile-kind-unsupported",
                        out status,
                        out code);
                }

                behaviorId = BuiltInGunBehaviorIds.Projectile;
                areaDamage = 0d;
                explosionRadius = 0d;
            }

            if (gun.ShotPattern.ProjectilesPerShot < 1
                || gun.ShotPattern.ProjectilesPerShot
                    > GunLiveFiringProfile.MaximumEffectsPerFire)
            {
                return Fail(
                    AcceptedEmissionLiveBridgeStatus.UnsupportedShotPattern,
                    "gun-runtime-projectile-count-unsupported",
                    out status,
                    out code);
            }

            profile = new GunLiveFiringProfile(
                gun.DefinitionId,
                behaviorId,
                cooldownTicks,
                gun.ShotPattern.ProjectilesPerShot,
                gun.ShotPattern.SpreadDegrees,
                gun.Projectile.Speed,
                gun.Projectile.Range,
                gun.Damage.DirectDamage,
                pierce,
                areaDamage,
                explosionRadius,
                0d,
                0d,
                0d,
                0d,
                0,
                0d,
                gun.Damage.Knockback,
                GunDamageCategoryConversion.ToCatalogValue(
                    gun.Damage.Category));
            status = AcceptedEmissionLiveBridgeStatus.Adapted;
            code = string.Empty;
            return true;
        }

        private static bool HasLegacyProjectileImpactShape(
            EffectiveGun gun,
            bool hasExplosion,
            int pierce)
        {
            if (!gun.Impact.HandlesEnemyImpact
                || !gun.Impact.HandlesWallImpact
                || !gun.Impact.HandlesRangeExpiry
                || !gun.Impact.HandlesTermination)
            {
                return false;
            }

            if (hasExplosion)
            {
                GunExplosionTriggerSpec trigger =
                    gun.Impact.ExplosionTrigger;
                return trigger != null
                    && trigger.OnEnemyImpact
                    && trigger.OnWallImpact
                    && trigger.OnRangeExpiry
                    && trigger.OnTermination
                    && gun.Projectile.TerminationBehavior
                        == GunProjectileTerminationBehavior
                            .StopOnFirstBlockingImpact;
            }

            if (gun.Impact.ExplosionTrigger != null)
            {
                return false;
            }
            if (pierce == 0)
            {
                return gun.Projectile.TerminationBehavior
                    == GunProjectileTerminationBehavior
                        .StopOnFirstBlockingImpact;
            }
            return gun.Projectile.TerminationBehavior
                == GunProjectileTerminationBehavior.StopWhenPierceIsSpent;
        }

        private static bool HasExpectedBatch(
            EffectiveGun gun,
            GunFiringScheduler.AcceptedEmission acceptedEmission,
            GunLiveFiringProfile profile,
            GunEffectBatch batch)
        {
            if (batch == null
                || batch.Identity == null
                || batch.EffectCount < 1
                || batch.EffectCount > GunLiveFiringProfile.MaximumEffectsPerFire
                || batch.EffectCount != profile.ProjectileCount)
            {
                return false;
            }

            for (int index = 0; index < batch.Effects.Count; index++)
            {
                IGunEffectDescription effect = batch.Effects[index];
                GunEffectIdentity identity =
                    effect == null ? null : effect.Identity;
                if (identity == null
                    || !identity.ActorId.Equals(
                        acceptedEmission.Command.ActorId)
                    || !identity.ParticipantId.Equals(
                        acceptedEmission.ParticipantId)
                    || !identity.EquipmentInstanceId.Equals(
                        gun.EquipmentInstanceId)
                    || !identity.GunDefinitionId.Equals(
                        gun.DefinitionId)
                    || !identity.FireOperationId.Equals(
                        acceptedEmission.EmissionFireOperationId)
                    || !identity.LifecycleGeneration.Equals(
                        acceptedEmission.Command.LifecycleGeneration)
                    || identity.ShotSequence != acceptedEmission.ShotSequence
                    || identity.ProjectileOrdinal.Value != index
                    || !HasExpectedPayload(
                        profile,
                        acceptedEmission.Command,
                        acceptedEmission.ShotSequence,
                        index,
                        effect))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasExpectedPayload(
            GunLiveFiringProfile profile,
            GunFireCommand command,
            long shotSequence,
            int projectileOrdinal,
            IGunEffectDescription effect)
        {
            GunVector2 expectedDirection =
                GunDeterministicSpread.DirectionFor(
                    command.AimDirection,
                    profile.SpreadDegrees,
                    command.DeterministicSeed,
                    command.FireOperationId,
                    command.EquipmentInstanceId,
                    shotSequence,
                    new ProjectileOrdinal(projectileOrdinal));

            if (profile.BehaviorId.Equals(BuiltInGunBehaviorIds.Projectile))
            {
                DirectProjectileEffect direct =
                    effect as DirectProjectileEffect;
                return direct != null
                    && direct.Origin != null
                    && direct.Origin.Equals(command.Origin)
                    && direct.Direction != null
                    && direct.Direction.Equals(expectedDirection)
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

            if (profile.BehaviorId.Equals(BuiltInGunBehaviorIds.Explosive))
            {
                ExplosiveProjectileEffect explosive =
                    effect as ExplosiveProjectileEffect;
                return explosive != null
                    && explosive.Origin != null
                    && explosive.Origin.Equals(command.Origin)
                    && explosive.Direction != null
                    && explosive.Direction.Equals(expectedDirection)
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

        private static bool IsValidCommand(GunFireCommand command)
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
            AcceptedEmissionLiveBridgeStatus failureStatus,
            string failureCode,
            out AcceptedEmissionLiveBridgeStatus status,
            out string code)
        {
            status = failureStatus;
            code = failureCode;
            return false;
        }

        private static AcceptedEmissionLiveBridgeResult Reject(
            AcceptedEmissionLiveBridgeStatus status,
            string code)
        {
            return AcceptedEmissionLiveBridgeResult.Reject(status, code);
        }
    }
}
