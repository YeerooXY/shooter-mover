using System;
using System.Collections.Generic;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    public sealed partial class GunExecutionCore
    {
        private static BatchBuildResult BuildBatch(
            GunFireCommand command,
            RunParticipantId participant,
            GunLiveFiringProfile profile,
            IGunBehavior behavior,
            long shotSequence)
        {
            GunBehaviorBuildResult built;
            try
            {
                built = behavior.Build(
                    new GunBehaviorContext(
                        command,
                        participant,
                        profile,
                        shotSequence));
            }
            catch
            {
                return BatchBuildResult.Reject(
                    GunExecutionStatus.BehaviorRejected,
                    "gun-behavior-exception");
            }

            if (built == null || !built.Succeeded)
            {
                return BatchBuildResult.Reject(
                    GunExecutionStatus.BehaviorRejected,
                    built == null
                        ? "gun-behavior-null-result"
                        : built.RejectionCode);
            }

            string batchCode;
            if (!ValidateBatch(
                    command,
                    participant,
                    profile,
                    shotSequence,
                    built.Batch,
                    out batchCode))
            {
                return BatchBuildResult.Reject(
                    GunExecutionStatus.InvalidEffectBatch,
                    batchCode);
            }

            return BatchBuildResult.Accept(built.Batch);
        }

        private static bool IsValidCommand(GunFireCommand command)
        {
            return command != null
                && command.SimulationTick >= 0L
                && command.Origin != null
                && command.Origin.IsFinite
                && command.AimDirection != null
                && command.AimDirection.IsFinite
                && command.AimDirection.LengthSquared > 0.000000000001d;
        }

        private static GunExecutionStatus Map(GunProfileResolutionStatus status)
        {
            switch (status)
            {
                case GunProfileResolutionStatus.InvalidEquipment:
                    return GunExecutionStatus.InvalidEquipment;
                case GunProfileResolutionStatus.UnknownGunDefinition:
                    return GunExecutionStatus.UnknownGunDefinition;
                case GunProfileResolutionStatus.PreviewOnlyGunDefinition:
                    return GunExecutionStatus.PreviewOnlyGunDefinition;
                case GunProfileResolutionStatus.InvalidTuning:
                    return GunExecutionStatus.InvalidTuning;
                case GunProfileResolutionStatus.UnsupportedEffects:
                    return GunExecutionStatus.UnsupportedEffects;
                case GunProfileResolutionStatus.UnknownBehavior:
                    return GunExecutionStatus.UnknownBehavior;
                default:
                    return GunExecutionStatus.InvalidTuning;
            }
        }

        private static bool ValidateBatch(
            GunFireCommand command,
            RunParticipantId participant,
            GunLiveFiringProfile profile,
            long sequence,
            GunEffectBatch batch,
            out string code)
        {
            if (batch == null || batch.EffectCount < 1)
            {
                code = "gun-effect-batch-empty";
                return false;
            }

            for (int index = 0; index < batch.Effects.Count; index++)
            {
                IGunEffectDescription effect = batch.Effects[index];
                if (effect == null
                    || effect.Identity == null
                    || !effect.Identity.ActorId.Equals(command.ActorId)
                    || !effect.Identity.ParticipantId.Equals(participant)
                    || !effect.Identity.EquipmentInstanceId.Equals(command.EquipmentInstanceId)
                    || !effect.Identity.GunDefinitionId.Equals(profile.DefinitionId)
                    || !effect.Identity.FireOperationId.Equals(command.FireOperationId)
                    || !effect.Identity.LifecycleGeneration.Equals(command.LifecycleGeneration)
                    || effect.Identity.ShotSequence != sequence
                    || effect.Identity.ProjectileOrdinal.Value != index)
                {
                    code = "gun-effect-identity-invalid:" + index;
                    return false;
                }

                if (!ValidateEffect(effect))
                {
                    code = "gun-effect-payload-invalid:" + index;
                    return false;
                }
            }

            code = string.Empty;
            return true;
        }

        private static bool ValidateEffect(IGunEffectDescription effect)
        {
            DirectProjectileEffect direct = effect as DirectProjectileEffect;
            if (direct != null)
            {
                return IsVector(direct.Origin)
                    && IsDirection(direct.Direction)
                    && IsPositive(direct.Speed)
                    && IsPositive(direct.Range)
                    && IsNonNegative(direct.DirectDamage)
                    && direct.Pierce >= 0
                    && IsNonNegative(direct.Knockback)
                    && !string.IsNullOrWhiteSpace(direct.DamageType);
            }

            ExplosiveProjectileEffect explosive = effect as ExplosiveProjectileEffect;
            if (explosive != null)
            {
                return IsVector(explosive.Origin)
                    && IsDirection(explosive.Direction)
                    && IsPositive(explosive.Speed)
                    && IsPositive(explosive.Range)
                    && IsNonNegative(explosive.DirectDamage)
                    && IsPositive(explosive.AreaDamage)
                    && IsPositive(explosive.ExplosionRadius)
                    && IsNonNegative(explosive.Knockback)
                    && !string.IsNullOrWhiteSpace(explosive.DamageType);
            }

            DamageOverTimeProjectileEffect dot =
                effect as DamageOverTimeProjectileEffect;
            if (dot != null)
            {
                return IsVector(dot.Origin)
                    && IsDirection(dot.Direction)
                    && IsPositive(dot.Speed)
                    && IsPositive(dot.Range)
                    && IsNonNegative(dot.DirectDamage)
                    && dot.Pierce >= 0
                    && IsPositive(dot.DotDps)
                    && IsPositive(dot.DotDuration)
                    && IsPositive(dot.PoolRadius)
                    && IsPositive(dot.PoolDuration)
                    && IsNonNegative(dot.Knockback)
                    && !string.IsNullOrWhiteSpace(dot.DamageType);
            }

            ChainArcEffect chain = effect as ChainArcEffect;
            if (chain != null)
            {
                return IsVector(chain.Origin)
                    && IsDirection(chain.Direction)
                    && IsPositive(chain.Damage)
                    && chain.MaximumTargets > 0
                    && IsPositive(chain.MaximumRange)
                    && IsNonNegative(chain.Knockback)
                    && !string.IsNullOrWhiteSpace(chain.DamageType);
            }

            return false;
        }

        private static bool IsVector(GunVector2 value)
        {
            return value != null && value.IsFinite;
        }

        private static bool IsDirection(GunVector2 value)
        {
            return IsVector(value) && value.LengthSquared > 0.000000000001d;
        }

        private static bool IsPositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        }

        private static bool IsNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }
    }
}
