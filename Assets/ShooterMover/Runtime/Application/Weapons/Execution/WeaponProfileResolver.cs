using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public static class BuiltInWeaponBehaviorIds
    {
        public static readonly WeaponBehaviorId Projectile = new WeaponBehaviorId(StableId.Parse("weapon-behavior.projectile"));
        public static readonly WeaponBehaviorId Explosive = new WeaponBehaviorId(StableId.Parse("weapon-behavior.explosive"));
        public static readonly WeaponBehaviorId Chain = new WeaponBehaviorId(StableId.Parse("weapon-behavior.chain"));
    }

    public enum WeaponProfileResolutionStatus { Resolved=1, InvalidEquipment=2, UnknownWeaponDefinition=3, PreviewOnlyWeaponDefinition=4, InvalidTuning=5, UnsupportedEffects=6, UnknownBehavior=7 }

    public sealed class WeaponProfileResolution
    {
        private WeaponProfileResolution(WeaponProfileResolutionStatus status, WeaponRuntimeFiringProfile profile, string rejectionCode) { Status=status; Profile=profile; RejectionCode=rejectionCode??string.Empty; }
        public WeaponProfileResolutionStatus Status { get; }
        public WeaponRuntimeFiringProfile Profile { get; }
        public string RejectionCode { get; }
        public bool Succeeded { get { return Status==WeaponProfileResolutionStatus.Resolved; } }
        public static WeaponProfileResolution Resolve(WeaponRuntimeFiringProfile profile) { return new WeaponProfileResolution(WeaponProfileResolutionStatus.Resolved, profile??throw new ArgumentNullException(nameof(profile)), string.Empty); }
        public static WeaponProfileResolution Reject(WeaponProfileResolutionStatus status,string code) { if(status==WeaponProfileResolutionStatus.Resolved) throw new ArgumentOutOfRangeException(nameof(status)); return new WeaponProfileResolution(status,null,code); }
    }

    public interface IWeaponBehaviorSelector { bool TrySelect(WeaponDefinitionData definition,out WeaponBehaviorId behaviorId); }
    public sealed class DefaultWeaponBehaviorSelector : IWeaponBehaviorSelector
    {
        public bool TrySelect(WeaponDefinitionData definition,out WeaponBehaviorId behaviorId)
        {
            if(definition==null){behaviorId=null;return false;}
            if(definition.ChainTargets>0){behaviorId=BuiltInWeaponBehaviorIds.Chain;return true;}
            if(definition.AreaDamagePerTrigger>0d||definition.ExplosionRadius>0d){behaviorId=BuiltInWeaponBehaviorIds.Explosive;return true;}
            behaviorId=BuiltInWeaponBehaviorIds.Projectile;return true;
        }
    }

    public interface IEquipmentWeaponDefinitionIdResolver { bool TryResolveWeaponDefinitionId(EquipmentDefinition equipmentDefinition,out WeaponDefinitionId weaponDefinitionId); }
    public sealed class RuntimeReferenceWeaponDefinitionIdResolver : IEquipmentWeaponDefinitionIdResolver
    {
        public bool TryResolveWeaponDefinitionId(EquipmentDefinition definition,out WeaponDefinitionId id)
        {
            if(definition==null||definition.RuntimeWeaponReferenceId==null){id=null;return false;}
            id=new WeaponDefinitionId(definition.RuntimeWeaponReferenceId.ToString());return true;
        }
    }

    public sealed class WeaponCatalogRuntimeProfileResolver
    {
        private const double E=0.000000001d;
        private readonly EquipmentCatalog equipmentCatalog; private readonly WeaponCatalog weaponCatalog; private readonly HashSet<string> liveIds; private readonly IWeaponBehaviorSelector selector; private readonly IEquipmentWeaponDefinitionIdResolver idResolver; private readonly int ticksPerSecond;
        public WeaponCatalogRuntimeProfileResolver(EquipmentCatalog equipment,WeaponCatalog weapons,IWeaponBehaviorSelector selector,int ticks):this(equipment,weapons,selector,new RuntimeReferenceWeaponDefinitionIdResolver(),ticks){}
        public WeaponCatalogRuntimeProfileResolver(EquipmentCatalog equipment,WeaponCatalog weapons,IWeaponBehaviorSelector behaviorSelector,IEquipmentWeaponDefinitionIdResolver definitionIdResolver,int simulationTicksPerSecond)
        {
            if(simulationTicksPerSecond<1)throw new ArgumentOutOfRangeException(nameof(simulationTicksPerSecond));
            equipmentCatalog=equipment??throw new ArgumentNullException(nameof(equipment)); weaponCatalog=weapons??throw new ArgumentNullException(nameof(weapons)); selector=behaviorSelector??throw new ArgumentNullException(nameof(behaviorSelector)); idResolver=definitionIdResolver??throw new ArgumentNullException(nameof(definitionIdResolver)); ticksPerSecond=simulationTicksPerSecond;
            liveIds=new HashSet<string>(StringComparer.Ordinal); IReadOnlyList<WeaponDefinitionData> live=weaponCatalog.GetDefinitions(WeaponCatalogContentFilter.LiveOnly); for(int i=0;i<live.Count;i++)liveIds.Add(live[i].DefinitionId);
        }
        public WeaponProfileResolution Resolve(EquipmentInstanceId requested,EquipmentInstance instance)
        {
            if(requested==null||instance==null||instance.InstanceId==null||requested.Value!=instance.InstanceId)return Reject(WeaponProfileResolutionStatus.InvalidEquipment,"weapon-equipment-instance-mismatch");
            EquipmentValidationResult validation=equipmentCatalog.ValidateInstance(instance); if(validation==null||!validation.IsValid)return Reject(WeaponProfileResolutionStatus.InvalidEquipment,"weapon-equipment-instance-invalid");
            EquipmentDefinition equipment=equipmentCatalog.FindEquipmentDefinition(instance.DefinitionId); if(equipment==null||equipment.CategoryId!=EquipmentCategoryIds.Weapon||equipment.RuntimeWeaponReferenceId==null)return Reject(WeaponProfileResolutionStatus.InvalidEquipment,"weapon-equipment-definition-invalid");
            WeaponDefinitionId id; if(!idResolver.TryResolveWeaponDefinitionId(equipment,out id)||id==null)return Reject(WeaponProfileResolutionStatus.InvalidEquipment,"weapon-equipment-definition-runtime-link-missing");
            WeaponDefinitionData definition; if(!weaponCatalog.TryGetDefinition(id.Value,out definition)||definition==null)return Reject(WeaponProfileResolutionStatus.UnknownWeaponDefinition,"weapon-definition-unknown:"+id.Value);
            if(!liveIds.Contains(id.Value))return Reject(WeaponProfileResolutionStatus.PreviewOnlyWeaponDefinition,"weapon-definition-preview-only:"+id.Value);
            string invalid; if(!Validate(definition,out invalid))return Reject(invalid.StartsWith("weapon-effect-unsupported",StringComparison.Ordinal)?WeaponProfileResolutionStatus.UnsupportedEffects:WeaponProfileResolutionStatus.InvalidTuning,invalid);
            WeaponBehaviorId behavior; if(!selector.TrySelect(definition,out behavior)||behavior==null)return Reject(WeaponProfileResolutionStatus.UnknownBehavior,"weapon-behavior-unresolved:"+id.Value);
            int cooldown=Math.Max(1,(int)Math.Ceiling(ticksPerSecond/definition.FireRate));
            return WeaponProfileResolution.Resolve(new WeaponRuntimeFiringProfile(new WeaponDefinitionId(definition.DefinitionId),behavior,cooldown,definition.ProjectilesPerTrigger,definition.SpreadDegrees,definition.ProjectileSpeed,definition.Range,definition.DamagePerProjectile,definition.Pierce,definition.AreaDamagePerTrigger,definition.ExplosionRadius,definition.ChainTargets,definition.ChainRange,definition.Knockback,definition.DamageType));
        }
        private static WeaponProfileResolution Reject(WeaponProfileResolutionStatus s,string c){return WeaponProfileResolution.Reject(s,c);}
        private static bool Validate(WeaponDefinitionData d,out string code)
        {
            if(d.BurstCount!=1||d.DotShare>E||d.DoTDPS>E||d.DoTDuration>E||d.PoolRadius>E||d.PoolDuration>E||d.HealingPerSecond>E){code="weapon-effect-unsupported:"+d.DefinitionId;return false;}
            if(!Pos(d.FireRate)||d.ProjectilesPerTrigger<1||d.ProjectilesPerTrigger>WeaponRuntimeFiringProfile.MaximumEffectsPerFire||!Range(d.SpreadDegrees,0d,360d)||!Pos(d.ProjectileSpeed)||!Pos(d.Range)||!NonNeg(d.DamagePerProjectile)||d.Pierce<0||!NonNeg(d.AreaDamagePerTrigger)||!NonNeg(d.ExplosionRadius)||d.ChainTargets<0||!NonNeg(d.ChainRange)||!NonNeg(d.Knockback)||string.IsNullOrWhiteSpace(d.DamageType)){code="weapon-tuning-invalid:"+d.DefinitionId;return false;}
            bool explosive=d.AreaDamagePerTrigger>E||d.ExplosionRadius>E; bool chain=d.ChainTargets>0||d.ChainRange>E;
            if(explosive&&chain){code="weapon-effect-unsupported-combination:"+d.DefinitionId;return false;}
            if(explosive&&(!Pos(d.AreaDamagePerTrigger)||!Pos(d.ExplosionRadius))){code="weapon-tuning-invalid-explosion:"+d.DefinitionId;return false;}
            if(chain&&(d.ChainTargets<1||!Pos(d.ChainRange)||d.ProjectilesPerTrigger!=1||!Pos(d.DamagePerProjectile))){code="weapon-tuning-invalid-chain:"+d.DefinitionId;return false;}
            if(!explosive&&!chain&&!Pos(d.DamagePerProjectile)){code="weapon-tuning-invalid-direct:"+d.DefinitionId;return false;}
            code=string.Empty;return true;
        }
        private static bool Pos(double v){return !double.IsNaN(v)&&!double.IsInfinity(v)&&v>0d;} private static bool NonNeg(double v){return !double.IsNaN(v)&&!double.IsInfinity(v)&&v>=0d;} private static bool Range(double v,double min,double max){return !double.IsNaN(v)&&!double.IsInfinity(v)&&v>=min&&v<=max;}
    }
}
