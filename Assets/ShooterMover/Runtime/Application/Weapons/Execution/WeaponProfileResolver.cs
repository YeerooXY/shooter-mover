using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public static class BuiltInWeaponBehaviorIds
    {
        public static readonly WeaponBehaviorId Projectile = new WeaponBehaviorId(
            StableId.Parse("weapon-behavior.projectile"));
        public static readonly WeaponBehaviorId Explosive = new WeaponBehaviorId(
            StableId.Parse("weapon-behavior.explosive"));
        public static readonly WeaponBehaviorId DamageOverTime = new WeaponBehaviorId(
            StableId.Parse("weapon-behavior.damage-over-time"));
        public static readonly WeaponBehaviorId Chain = new WeaponBehaviorId(
            StableId.Parse("weapon-behavior.chain"));
    }

    public enum WeaponProfileResolutionStatus
    {
        Resolved = 1,
        InvalidEquipment = 2,
        UnknownWeaponDefinition = 3,
        PreviewOnlyWeaponDefinition = 4,
        InvalidTuning = 5,
        UnsupportedEffects = 6,
        UnknownBehavior = 7,
        RuntimeBehaviorPending = 8,
    }

    public sealed class WeaponProfileResolution
    {
        private WeaponProfileResolution(
            WeaponProfileResolutionStatus status,
            WeaponRuntimeFiringProfile profile,
            string rejectionCode)
        {
            Status = status;
            Profile = profile;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public WeaponProfileResolutionStatus Status { get; }
        public WeaponRuntimeFiringProfile Profile { get; }
        public string RejectionCode { get; }
        public bool Succeeded { get { return Status == WeaponProfileResolutionStatus.Resolved; } }

        public static WeaponProfileResolution Resolve(WeaponRuntimeFiringProfile profile)
        {
            return new WeaponProfileResolution(
                WeaponProfileResolutionStatus.Resolved,
                profile ?? throw new ArgumentNullException(nameof(profile)),
                string.Empty);
        }

        public static WeaponProfileResolution Reject(
            WeaponProfileResolutionStatus status,
            string code)
        {
            if (status == WeaponProfileResolutionStatus.Resolved)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new WeaponProfileResolution(status, null, code);
        }
    }

    public interface IWeaponBehaviorSelector
    {
        bool TrySelect(
            WeaponDefinitionData definition,
            out WeaponBehaviorId behaviorId);
    }

    public sealed class DefaultWeaponBehaviorSelector : IWeaponBehaviorSelector
    {
        private const double Epsilon = 0.000000001d;

        public bool TrySelect(
            WeaponDefinitionData definition,
            out WeaponBehaviorId behaviorId)
        {
            if (definition == null)
            {
                behaviorId = null;
                return false;
            }

            if (definition.ChainTargets > 0)
            {
                behaviorId = BuiltInWeaponBehaviorIds.Chain;
                return true;
            }

            if (definition.AreaDamagePerTrigger > Epsilon
                || definition.ExplosionRadius > Epsilon)
            {
                behaviorId = BuiltInWeaponBehaviorIds.Explosive;
                return true;
            }

            if (definition.DotShare > Epsilon
                || definition.DotDps > Epsilon
                || definition.DotDuration > Epsilon
                || definition.PoolRadius > Epsilon
                || definition.PoolDuration > Epsilon)
            {
                behaviorId = BuiltInWeaponBehaviorIds.DamageOverTime;
                return true;
            }

            behaviorId = BuiltInWeaponBehaviorIds.Projectile;
            return true;
        }
    }

    public interface IEquipmentWeaponDefinitionIdResolver
    {
        bool TryResolveWeaponDefinitionId(
            EquipmentDefinition equipmentDefinition,
            out WeaponDefinitionId weaponDefinitionId);
    }

    public sealed class RuntimeReferenceWeaponDefinitionIdResolver
        : IEquipmentWeaponDefinitionIdResolver
    {
        public bool TryResolveWeaponDefinitionId(
            EquipmentDefinition definition,
            out WeaponDefinitionId id)
        {
            if (definition == null || definition.RuntimeWeaponReferenceId == null)
            {
                id = null;
                return false;
            }

            id = new WeaponDefinitionId(definition.RuntimeWeaponReferenceId.ToString());
            return true;
        }
    }
}
