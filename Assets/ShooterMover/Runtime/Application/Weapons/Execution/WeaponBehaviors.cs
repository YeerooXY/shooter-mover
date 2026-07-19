using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public static class WeaponDeterministicSpread
    {
        private const ulong Offset=14695981039346656037UL; private const ulong Prime=1099511628211UL; private const double Unit53=1d/9007199254740992d;
        public static WeaponVector2 DirectionFor(WeaponVector2 baseDirection,double spreadDegrees,ulong seed,FireOperationId operationId,EquipmentInstanceId equipmentId,long shotSequence,ProjectileOrdinal ordinal)
        {
            if(baseDirection==null)throw new ArgumentNullException(nameof(baseDirection));if(operationId==null)throw new ArgumentNullException(nameof(operationId));if(equipmentId==null)throw new ArgumentNullException(nameof(equipmentId));if(ordinal==null)throw new ArgumentNullException(nameof(ordinal));
            string facts=seed.ToString(CultureInfo.InvariantCulture)+"|"+operationId+"|"+equipmentId+"|"+shotSequence.ToString(CultureInfo.InvariantCulture)+"|"+ordinal; ulong hash=Offset; for(int i=0;i<facts.Length;i++){hash^=facts[i];hash*=Prime;} double unit=(hash>>11)*Unit53; return baseDirection.Normalized.RotateDegrees((unit-0.5d)*spreadDegrees).Normalized;
        }
    }
    public sealed class ProjectileWeaponBehavior:IWeaponBehavior
    {
        public WeaponBehaviorId BehaviorId { get{return BuiltInWeaponBehaviorIds.Projectile;} }
        public WeaponBehaviorBuildResult Build(WeaponBehaviorContext c){if(c==null)return WeaponBehaviorBuildResult.Reject("weapon-context-missing");List<IWeaponEffectDescription> effects=new List<IWeaponEffectDescription>();for(int i=0;i<c.Profile.ProjectileCount;i++){WeaponVector2 d=WeaponDeterministicSpread.DirectionFor(c.Command.AimDirection,c.Profile.SpreadDegrees,c.Command.DeterministicSeed,c.Command.FireOperationId,c.Command.EquipmentInstanceId,c.ShotSequence,new ProjectileOrdinal(i));effects.Add(new DirectProjectileEffect(c.IdentityFor(i),c.Command.Origin,d,c.Profile.ProjectileSpeed,c.Profile.ProjectileRange,c.Profile.DirectDamage,c.Profile.Pierce,c.Profile.Knockback,c.Profile.DamageType));}return WeaponBehaviorBuildResult.Accept(new WeaponEffectBatch(effects));}
    }
    public sealed class ExplosiveWeaponBehavior:IWeaponBehavior
    {
        public WeaponBehaviorId BehaviorId { get{return BuiltInWeaponBehaviorIds.Explosive;} }
        public WeaponBehaviorBuildResult Build(WeaponBehaviorContext c){if(c==null)return WeaponBehaviorBuildResult.Reject("weapon-context-missing");List<IWeaponEffectDescription> effects=new List<IWeaponEffectDescription>();for(int i=0;i<c.Profile.ProjectileCount;i++){WeaponVector2 d=WeaponDeterministicSpread.DirectionFor(c.Command.AimDirection,c.Profile.SpreadDegrees,c.Command.DeterministicSeed,c.Command.FireOperationId,c.Command.EquipmentInstanceId,c.ShotSequence,new ProjectileOrdinal(i));effects.Add(new ExplosiveProjectileEffect(c.IdentityFor(i),c.Command.Origin,d,c.Profile.ProjectileSpeed,c.Profile.ProjectileRange,c.Profile.DirectDamage,c.Profile.AreaDamage,c.Profile.ExplosionRadius,c.Profile.Knockback,c.Profile.DamageType));}return WeaponBehaviorBuildResult.Accept(new WeaponEffectBatch(effects));}
    }
    public sealed class ChainWeaponBehavior:IWeaponBehavior
    {
        public WeaponBehaviorId BehaviorId { get{return BuiltInWeaponBehaviorIds.Chain;} }
        public WeaponBehaviorBuildResult Build(WeaponBehaviorContext c){if(c==null)return WeaponBehaviorBuildResult.Reject("weapon-context-missing");return WeaponBehaviorBuildResult.Accept(new WeaponEffectBatch(new List<IWeaponEffectDescription>{new ChainArcEffect(c.IdentityFor(0),c.Command.Origin,c.Command.AimDirection.Normalized,c.Profile.DirectDamage,c.Profile.ChainTargets,c.Profile.ChainRange,c.Profile.Knockback,c.Profile.DamageType)}));}
    }
}
