using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Tests.EditMode.Weapons.Execution
{
    public sealed partial class WeaponExecutionCoreTests
    {
        private static Harness HarnessFor(WeaponDefinitionData definition,IEnumerable<EquipmentInstance> equippedInstances,StableId runtimeReferenceId=null,IWeaponBehaviorSelector selector=null,WeaponBehaviorRegistry registry=null,RecordingSink sink=null)
        {
            StableId runtimeReference=runtimeReferenceId??StableId.Parse(definition.DefinitionId);EquipmentQualityTier quality=EquipmentQualityTier.Create(QualityStableId,"Common",1);
            EquipmentDefinition equipmentDefinition=EquipmentDefinition.Create(EquipmentDefinitionStableId,EquipmentCategoryIds.Weapon,StableId.Parse("equipment-family.test"),"Test Weapon",runtimeReference,InclusiveIntRange.Create(1,100),0,new[]{quality},new StableId[0]);
            EquipmentCatalogBuildResult build=EquipmentCatalog.Build(new[]{equipmentDefinition},new AugmentDefinition[0]);Assert.That(build.IsValid,Is.True);
            WeaponCatalogRuntimeProfileResolver profiles=new WeaponCatalogRuntimeProfileResolver(build.Catalog,Catalog(definition),selector??new DefaultWeaponBehaviorSelector(),60);RecordingSink actual=sink??new RecordingSink();TestEquippedResolver equipped=new TestEquippedResolver(equippedInstances);
            return new Harness(new WeaponExecutionCore(new TestOwnershipResolver(),equipped,profiles,registry??WeaponBehaviorRegistry.CreateWithBuiltIns(),actual),actual);
        }
        private static WeaponCatalog Catalog(WeaponDefinitionData d)
        {
            WeaponCatalogRules rules=new WeaponCatalogRules(true,false,"20-25",new[]{75,105,135},new[]{"Kinetic","Thermal","Energized"},10,true,true,true);
            WeaponCatalogInputs inputs=new WeaponCatalogInputs(12d,.05d,.055d,.06d,new Dictionary<string,WeaponRarityInput>(StringComparer.Ordinal){{"Common",new WeaponRarityInput("Common",1000d,0,4d,13d)}});
            WeaponArchetypeDefinition archetype=new WeaponArchetypeDefinition("Test","Test",1d,Math.Max(1d,d.FireRate),Math.Max(1,d.ProjectilesPerTrigger),1,Math.Max(0d,d.SpreadDegrees),30d,30d,1d,0d,0d,0d,0d,0d,0d,0,0,0d,0d,1d);
            WeaponFamilyDefinition family=new WeaponFamilyDefinition("test-family","Test Family","Test",d.DamageType,"Universal",1,20,20,3,"Common","Common","Common",1d,"Standard","Test","Test",WeaponCatalogAvailability.Live,new string[0]);
            return new WeaponCatalog("0.1","test",rules,inputs,new Dictionary<string,WeaponArchetypeDefinition>(StringComparer.Ordinal){{"Test",archetype}},new[]{family},new[]{d});
        }
        private static WeaponDefinitionData Definition(string id,int projectileCount,double spread,double fireRate,double areaDamage=0d,double explosionRadius=0d,int chainTargets=0,double chainRange=0d,WeaponCatalogAvailability availability=WeaponCatalogAvailability.Live,double dotDps=0d,int burstCount=1)
        {
            return new WeaponDefinitionData(id,id,"test-family",1,"Kinetic","Test","Universal",1,1,1,"Common",1000d,1d,1000d,4d,13d,"Standard",false,"Standard",1d,100d,10d,areaDamage>0d?.5d:1d,areaDamage>0d?.5d:0d,dotDps>0d?1d:0d,fireRate,projectileCount,burstCount,5d,spread,30d,30d,0,explosionRadius,areaDamage,dotDps,dotDps>0d?2d:0d,0d,0d,chainTargets,chainRange,.5d,1d,0d,"Test","Test",availability,new string[0]);
        }
        private static EquipmentInstance Equipment(string id){return EquipmentInstance.Create(StableId.Parse(id),EquipmentDefinitionStableId,1,QualityStableId,new AugmentInstance[0]);}
        private static WeaponFireCommand Command(EquipmentInstance equipment,string operation,long tick,long generation=0L,ulong seed=123UL,WeaponVector2 aim=null){return new WeaponFireCommand(new WeaponActorInstanceId(ActorStableId),new EquipmentInstanceId(equipment.InstanceId),new FireOperationId(StableId.Parse(operation)),new LifecycleGeneration(generation),tick,seed,new WeaponVector2(2d,3d),aim??new WeaponVector2(1d,0d));}
        private sealed class Harness{public Harness(WeaponExecutionCore core,RecordingSink sink){Core=core;Sink=sink;}public WeaponExecutionCore Core{get;}public RecordingSink Sink{get;}}
        private sealed class TestOwnershipResolver:IWeaponActorOwnershipResolver{public bool TryResolveParticipant(WeaponActorInstanceId actor,LifecycleGeneration generation,out RunParticipantId participant){participant=actor!=null&&generation!=null?new RunParticipantId(ParticipantStableId):null;return participant!=null;}}
        private sealed class TestEquippedResolver:IEquippedWeaponInstanceResolver
        {
            private readonly Dictionary<StableId,EquipmentInstance> instances=new Dictionary<StableId,EquipmentInstance>();public TestEquippedResolver(IEnumerable<EquipmentInstance> values){foreach(EquipmentInstance v in values??new EquipmentInstance[0])instances[v.InstanceId]=v;}
            public bool TryResolveEquippedWeapon(WeaponActorInstanceId actor,EquipmentInstanceId requested,out EquipmentInstance instance){return actor!=null&&requested!=null&&instances.TryGetValue(requested.Value,out instance);}
        }
        private sealed class RecordingSink:IWeaponEffectBatchSink
        {
            public bool Reject{get;set;}public List<WeaponEffectBatch>Batches{get;}=new List<WeaponEffectBatch>();public List<int>ValidatedCounts{get;}=new List<int>();
            public WeaponEffectBatchSinkResult TryAccept(WeaponEffectBatch batch){int validated=0;foreach(IWeaponEffectDescription effect in batch.Effects){Assert.That(effect,Is.Not.Null);Assert.That(effect.Identity,Is.Not.Null);validated++;}Batches.Add(batch);ValidatedCounts.Add(validated);return Reject?WeaponEffectBatchSinkResult.Reject("test-sink-rejected"):WeaponEffectBatchSinkResult.Accept();}
        }
        private sealed class ExactDefinitionSelector:IWeaponBehaviorSelector
        {
            private readonly string definitionId;private readonly WeaponBehaviorId behaviorId;private readonly DefaultWeaponBehaviorSelector fallback=new DefaultWeaponBehaviorSelector();public ExactDefinitionSelector(string id,WeaponBehaviorId behavior){definitionId=id;behaviorId=behavior;}
            public bool TrySelect(WeaponDefinitionData definition,out WeaponBehaviorId selected){if(definition!=null&&string.Equals(definition.DefinitionId,definitionId,StringComparison.Ordinal)){selected=behaviorId;return true;}return fallback.TrySelect(definition,out selected);}
        }
        private sealed class ThreeProjectileTestBehavior:IWeaponBehavior
        {
            public ThreeProjectileTestBehavior(WeaponBehaviorId id){BehaviorId=id;}public WeaponBehaviorId BehaviorId{get;}
            public WeaponBehaviorBuildResult Build(WeaponBehaviorContext c){List<IWeaponEffectDescription> effects=new List<IWeaponEffectDescription>();for(int i=0;i<3;i++)effects.Add(new DirectProjectileEffect(c.IdentityFor(i),c.Command.Origin,c.Command.AimDirection.Normalized,c.Profile.ProjectileSpeed,c.Profile.ProjectileRange,c.Profile.DirectDamage,c.Profile.Pierce,c.Profile.Knockback,c.Profile.DamageType));return WeaponBehaviorBuildResult.Accept(new WeaponEffectBatch(effects));}
        }
    }
}
