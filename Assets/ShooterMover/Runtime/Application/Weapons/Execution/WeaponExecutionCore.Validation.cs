using System;
using System.Collections.Generic;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public sealed partial class WeaponExecutionCore
    {
        private static BatchBuildResult BuildBatch(
            WeaponFireCommand command,
            RunParticipantId participant,
            WeaponRuntimeFiringProfile profile,
            IWeaponBehavior behavior,
            long shotSequence)
        {
            WeaponBehaviorBuildResult built;
            try
            {
                built = behavior.Build(
                    new WeaponBehaviorContext(
                        command,
                        participant,
                        profile,
                        shotSequence));
            }
            catch
            {
                return BatchBuildResult.Reject(
                    WeaponExecutionStatus.BehaviorRejected,
                    "weapon-behavior-exception");
            }

            if (built == null || !built.Succeeded)
            {
                return BatchBuildResult.Reject(
                    WeaponExecutionStatus.BehaviorRejected,
                    built == null
                        ? "weapon-behavior-null-result"
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
                    WeaponExecutionStatus.InvalidEffectBatch,
                    batchCode);
            }

            return BatchBuildResult.Accept(built.Batch);
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

        private static WeaponExecutionStatus Map(WeaponProfileResolutionStatus status)
        {
            switch (status)
            {
                case WeaponProfileResolutionStatus.InvalidEquipment:
                    return WeaponExecutionStatus.InvalidEquipment;
                case WeaponProfileResolutionStatus.UnknownWeaponDefinition:
                    return WeaponExecutionStatus.UnknownWeaponDefinition;
                case WeaponProfileResolutionStatus.PreviewOnlyWeaponDefinition:
                    return WeaponExecutionStatus.PreviewOnlyWeaponDefinition;
                case WeaponProfileResolutionStatus.InvalidTuning:
                    return WeaponExecutionStatus.InvalidTuning;
                case WeaponProfileResolutionStatus.UnsupportedEffects:
                    return WeaponExecutionStatus.UnsupportedEffects;
                case WeaponProfileResolutionStatus.UnknownBehavior:
                    return WeaponExecutionStatus.UnknownBehavior;
                case WeaponProfileResolutionStatus.RuntimeBehaviorPending:
                    return WeaponExecutionStatus.RuntimeBehaviorPending;
                default:
                    return WeaponExecutionStatus.InvalidTuning;
            }
        }

        private static bool ValidateBatch(
            WeaponFireCommand command,
            RunParticipantId participant,
            WeaponRuntimeFiringProfile profile,
            long sequence,
            WeaponEffectBatch batch,
            out string code)
        {
            if (batch == null || batch.EffectCount < 1)
            {
                code = "weapon-effect-batch-empty";
                return false;
            }

            for (int index = 0; index < batch.Effects.Count; index++)
            {
                IWeaponEffectDescription effect = batch.Effects[index];
                if (effect == null
                    || effect.Identity == null
                    || !effect.Identity.ActorId.Equals(command.ActorId)
                    || !effect.Identity.ParticipantId.Equals(participant)
                    || !effect.Identity.EquipmentInstanceId.Equals(command.EquipmentInstanceId)
                    || !effect.Identity.WeaponDefinitionId.Equals(profile.DefinitionId)
                    || !effect.Identity.FireOperationId.Equals(command.FireOperationId)
                    || !effect.Identity.LifecycleGeneration.Equals(command.LifecycleGeneration)
                    || effect.Identity.ShotSequence != sequence
                    || effect.Identity.ProjectileOrdinal.Value != index)
                {
                    code = "weapon-effect-identity-invalid:" + index;
                    return false;
                }

                if (!ValidateEffect(effect))
                {
                    code = "weapon-effect-payload-invalid:" + index;
                    return false;
                }
            }

            code = string.Empty;
            return true;
        }

        private static bool ValidateEffect(IWeaponEffectDescription effect)
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

        private static bool IsVector(WeaponVector2 value)
        {
            return value != null && value.IsFinite;
        }

        private static bool IsDirection(WeaponVector2 value)
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
