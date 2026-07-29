using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    public static class BuiltInGunBehaviorIds
    {
        public static readonly GunBehaviorId Projectile = new GunBehaviorId(
            StableId.Parse("gun-behavior.projectile"));
        public static readonly GunBehaviorId Explosive = new GunBehaviorId(
            StableId.Parse("gun-behavior.explosive"));
        public static readonly GunBehaviorId DamageOverTime = new GunBehaviorId(
            StableId.Parse("gun-behavior.damage-over-time"));
        public static readonly GunBehaviorId Chain = new GunBehaviorId(
            StableId.Parse("gun-behavior.chain"));
    }

    public enum GunProfileResolutionStatus
    {
        Resolved = 1,
        InvalidEquipment = 2,
        UnknownGunDefinition = 3,
        PreviewOnlyGunDefinition = 4,
        InvalidTuning = 5,
        UnsupportedEffects = 6,
        UnknownBehavior = 7,
    }

    public sealed class GunProfileResolution
    {
        private GunProfileResolution(
            GunProfileResolutionStatus status,
            GunLiveFiringProfile profile,
            string rejectionCode)
        {
            Status = status;
            Profile = profile;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public GunProfileResolutionStatus Status { get; }
        public GunLiveFiringProfile Profile { get; }
        public string RejectionCode { get; }
        public bool Succeeded { get { return Status == GunProfileResolutionStatus.Resolved; } }

        public static GunProfileResolution Resolve(GunLiveFiringProfile profile)
        {
            return new GunProfileResolution(
                GunProfileResolutionStatus.Resolved,
                profile ?? throw new ArgumentNullException(nameof(profile)),
                string.Empty);
        }

        public static GunProfileResolution Reject(
            GunProfileResolutionStatus status,
            string code)
        {
            if (status == GunProfileResolutionStatus.Resolved)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new GunProfileResolution(status, null, code);
        }
    }

    public interface IGunBehaviorSelector
    {
        bool TrySelect(
            GunDefinitionData definition,
            out GunBehaviorId behaviorId);
    }

    /// <summary>
    /// Legacy flat-catalog inference retained for GunExecutionCore tooling/tests only.
    /// Production behavior selection is structural and comes from EffectiveGun projection.
    /// </summary>
    [Obsolete(
        "Legacy tooling/test selector only. Live behavior comes from EffectiveGun structure.",
        false)]
    public sealed class DefaultGunBehaviorSelector : IGunBehaviorSelector
    {
        private const double Epsilon = 0.000000001d;

        public bool TrySelect(
            GunDefinitionData definition,
            out GunBehaviorId behaviorId)
        {
            if (definition == null)
            {
                behaviorId = null;
                return false;
            }

            if (definition.ChainTargets > 0)
            {
                behaviorId = BuiltInGunBehaviorIds.Chain;
                return true;
            }

            if (definition.AreaDamagePerTrigger > Epsilon
                || definition.ExplosionRadius > Epsilon)
            {
                behaviorId = BuiltInGunBehaviorIds.Explosive;
                return true;
            }

            if (definition.DotShare > Epsilon
                || definition.DotDps > Epsilon
                || definition.DotDuration > Epsilon
                || definition.PoolRadius > Epsilon
                || definition.PoolDuration > Epsilon)
            {
                behaviorId = BuiltInGunBehaviorIds.DamageOverTime;
                return true;
            }

            behaviorId = BuiltInGunBehaviorIds.Projectile;
            return true;
        }
    }

    public interface IEquipmentGunDefinitionIdResolver
    {
        bool TryResolveGunDefinitionId(
            EquipmentDefinition equipmentDefinition,
            out GunDefinitionId gunDefinitionId);
    }

    public sealed class LiveReferenceGunDefinitionIdResolver
        : IEquipmentGunDefinitionIdResolver
    {
        public bool TryResolveGunDefinitionId(
            EquipmentDefinition definition,
            out GunDefinitionId id)
        {
            if (definition == null || definition.RuntimeGunReferenceId == null)
            {
                id = null;
                return false;
            }

            id = GunDefinitionId.FromRuntimeReference(
                definition.RuntimeGunReferenceId);
            return true;
        }
    }
}
