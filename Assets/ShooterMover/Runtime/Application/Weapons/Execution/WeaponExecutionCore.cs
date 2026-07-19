using System;
using System.Collections.Generic;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public enum WeaponExecutionStatus { Accepted=1, InvalidCommand=2, UnknownActorOwnership=3, MissingEquippedEquipment=4, InvalidEquipment=5, UnknownWeaponDefinition=6, PreviewOnlyWeaponDefinition=7, InvalidTuning=8, UnsupportedEffects=9, UnknownBehavior=10, CooldownActive=11, ReplayAccepted=12, BehaviorRejected=13, InvalidEffectBatch=14, SinkRejected=15 }
    public sealed class WeaponExecutionResult
    {
        private WeaponExecutionResult(WeaponExecutionStatus status,string code,int effectCount,long shotSequence){Status=status;RejectionCode=code??string.Empty;EffectCount=effectCount;ShotSequence=shotSequence;}
        public WeaponExecutionStatus Status { get; } public string RejectionCode { get; } public int EffectCount { get; } public long ShotSequence { get; } public bool Succeeded { get{return Status==WeaponExecutionStatus.Accepted;} }
        public static WeaponExecutionResult Accept(int count,long sequence){return new WeaponExecutionResult(WeaponExecutionStatus.Accepted,string.Empty,count,sequence);} public static WeaponExecutionResult Reject(WeaponExecutionStatus status,string code,long sequence){if(status==WeaponExecutionStatus.Accepted)throw new ArgumentOutOfRangeException(nameof(status));return new WeaponExecutionResult(status,code,0,sequence);}
    }

    public sealed class WeaponExecutionCore
    {
        private readonly IWeaponActorOwnershipResolver ownership; private readonly IEquippedWeaponInstanceResolver equipped; private readonly WeaponCatalogRuntimeProfileResolver profiles; private readonly WeaponBehaviorRegistry registry; private readonly IWeaponEffectBatchSink sink; private readonly Dictionary<StateKey,FireState> states=new Dictionary<StateKey,FireState>();
        public WeaponExecutionCore(IWeaponActorOwnershipResolver ownershipResolver,IEquippedWeaponInstanceResolver equippedResolver,WeaponCatalogRuntimeProfileResolver profileResolver,WeaponBehaviorRegistry behaviorRegistry,IWeaponEffectBatchSink effectSink){ownership=ownershipResolver??throw new ArgumentNullException(nameof(ownershipResolver));equipped=equippedResolver??throw new ArgumentNullException(nameof(equippedResolver));profiles=profileResolver??throw new ArgumentNullException(nameof(profileResolver));registry=behaviorRegistry??throw new ArgumentNullException(nameof(behaviorRegistry));sink=effectSink??throw new ArgumentNullException(nameof(effectSink));}
        public WeaponExecutionResult TryExecute(WeaponFireCommand command)
        {
            if(!ValidCommand(command))return WeaponExecutionResult.Reject(WeaponExecutionStatus.InvalidCommand,"weapon-command-invalid",0L);
            RunParticipantId participant; if(!ownership.TryResolveParticipant(command.ActorId,command.LifecycleGeneration,out participant)||participant==null)return WeaponExecutionResult.Reject(WeaponExecutionStatus.UnknownActorOwnership,"weapon-actor-ownership-unresolved",0L);
            StateKey key=new StateKey(command.ActorId,command.EquipmentInstanceId,command.LifecycleGeneration);FireState state; if(!states.TryGetValue(key,out state))state=FireState.Initial;
            if(state.HasAccepted(command.FireOperationId))return WeaponExecutionResult.Reject(WeaponExecutionStatus.ReplayAccepted,"weapon-operation-already-accepted",state.ShotSequence);
            if(command.SimulationTick<state.NextAllowedTick)return WeaponExecutionResult.Reject(WeaponExecutionStatus.CooldownActive,"weapon-cooldown-active",state.ShotSequence);
            EquipmentInstance instance; if(!equipped.TryResolveEquippedWeapon(command.ActorId,command.EquipmentInstanceId,out instance)||instance==null)return WeaponExecutionResult.Reject(WeaponExecutionStatus.MissingEquippedEquipment,"weapon-equipment-not-equipped",state.ShotSequence);
            WeaponProfileResolution profile=profiles.Resolve(command.EquipmentInstanceId,instance); if(!profile.Succeeded)return WeaponExecutionResult.Reject(Map(profile.Status),profile.RejectionCode,state.ShotSequence);
            IWeaponBehavior behavior; if(!registry.TryResolve(profile.Profile.BehaviorId,out behavior)||behavior==null)return WeaponExecutionResult.Reject(WeaponExecutionStatus.UnknownBehavior,"weapon-behavior-unregistered:"+profile.Profile.BehaviorId,state.ShotSequence);
            WeaponBehaviorBuildResult built; try{built=behavior.Build(new WeaponBehaviorContext(command,participant,profile.Profile,state.ShotSequence));}catch{return WeaponExecutionResult.Reject(WeaponExecutionStatus.BehaviorRejected,"weapon-behavior-exception",state.ShotSequence);}
            if(built==null||!built.Succeeded)return WeaponExecutionResult.Reject(WeaponExecutionStatus.BehaviorRejected,built==null?"weapon-behavior-null-result":built.RejectionCode,state.ShotSequence);
            string batchCode; if(!ValidateBatch(command,participant,profile.Profile,state.ShotSequence,built.Batch,out batchCode))return WeaponExecutionResult.Reject(WeaponExecutionStatus.InvalidEffectBatch,batchCode,state.ShotSequence);
            WeaponEffectBatchSinkResult accepted; try{accepted=sink.TryAccept(built.Batch);}catch{return WeaponExecutionResult.Reject(WeaponExecutionStatus.SinkRejected,"weapon-effect-sink-exception",state.ShotSequence);}
            if(accepted==null||!accepted.IsAcceptance)return WeaponExecutionResult.Reject(WeaponExecutionStatus.SinkRejected,accepted==null?"weapon-effect-sink-null-result":accepted.RejectionCode,state.ShotSequence);
            states[key]=state.AfterAccepted(command.FireOperationId,command.SimulationTick+profile.Profile.CooldownTicks); return WeaponExecutionResult.Accept(built.Batch.EffectCount,state.ShotSequence);
        }
        private static bool ValidCommand(WeaponFireCommand c){return c!=null&&c.SimulationTick>=0L&&c.Origin!=null&&c.Origin.IsFinite&&c.AimDirection!=null&&c.AimDirection.IsFinite&&c.AimDirection.LengthSquared>0.000000000001d;}
        private static WeaponExecutionStatus Map(WeaponProfileResolutionStatus s){switch(s){case WeaponProfileResolutionStatus.InvalidEquipment:return WeaponExecutionStatus.InvalidEquipment;case WeaponProfileResolutionStatus.UnknownWeaponDefinition:return WeaponExecutionStatus.UnknownWeaponDefinition;case WeaponProfileResolutionStatus.PreviewOnlyWeaponDefinition:return WeaponExecutionStatus.PreviewOnlyWeaponDefinition;case WeaponProfileResolutionStatus.InvalidTuning:return WeaponExecutionStatus.InvalidTuning;case WeaponProfileResolutionStatus.UnsupportedEffects:return WeaponExecutionStatus.UnsupportedEffects;case WeaponProfileResolutionStatus.UnknownBehavior:return WeaponExecutionStatus.UnknownBehavior;default:return WeaponExecutionStatus.InvalidTuning;}}
        private static bool ValidateBatch(WeaponFireCommand command,RunParticipantId participant,WeaponRuntimeFiringProfile profile,long sequence,WeaponEffectBatch batch,out string code)
        {
            if(batch==null||batch.EffectCount<1){code="weapon-effect-batch-empty";return false;}
            for(int i=0;i<batch.Effects.Count;i++){IWeaponEffectDescription e=batch.Effects[i];if(e==null||e.Identity==null||!e.Identity.ActorId.Equals(command.ActorId)||!e.Identity.ParticipantId.Equals(participant)||!e.Identity.EquipmentInstanceId.Equals(command.EquipmentInstanceId)||!e.Identity.WeaponDefinitionId.Equals(profile.DefinitionId)||!e.Identity.FireOperationId.Equals(command.FireOperationId)||!e.Identity.LifecycleGeneration.Equals(command.LifecycleGeneration)||e.Identity.ShotSequence!=sequence||e.Identity.ProjectileOrdinal.Value!=i){code="weapon-effect-identity-invalid:"+i;return false;}if(!ValidateEffect(e)){code="weapon-effect-payload-invalid:"+i;return false;}}
            code=string.Empty;return true;
        }
        private static bool ValidateEffect(IWeaponEffectDescription effect)
        {
            DirectProjectileEffect d=effect as DirectProjectileEffect;if(d!=null)return Vector(d.Origin)&&Direction(d.Direction)&&Pos(d.Speed)&&Pos(d.Range)&&NonNeg(d.DirectDamage)&&d.Pierce>=0&&NonNeg(d.Knockback)&&!string.IsNullOrWhiteSpace(d.DamageType);
            ExplosiveProjectileEffect x=effect as ExplosiveProjectileEffect;if(x!=null)return Vector(x.Origin)&&Direction(x.Direction)&&Pos(x.Speed)&&Pos(x.Range)&&NonNeg(x.DirectDamage)&&Pos(x.AreaDamage)&&Pos(x.ExplosionRadius)&&NonNeg(x.Knockback)&&!string.IsNullOrWhiteSpace(x.DamageType);
            ChainArcEffect c=effect as ChainArcEffect;if(c!=null)return Vector(c.Origin)&&Direction(c.Direction)&&Pos(c.Damage)&&c.MaximumTargets>0&&Pos(c.MaximumRange)&&NonNeg(c.Knockback)&&!string.IsNullOrWhiteSpace(c.DamageType);return false;
        }
        private static bool Vector(WeaponVector2 v){return v!=null&&v.IsFinite;}private static bool Direction(WeaponVector2 v){return Vector(v)&&v.LengthSquared>0.000000000001d;}private static bool Pos(double v){return !double.IsNaN(v)&&!double.IsInfinity(v)&&v>0d;}private static bool NonNeg(double v){return !double.IsNaN(v)&&!double.IsInfinity(v)&&v>=0d;}
        private sealed class StateKey:IEquatable<StateKey>
        {
            public StateKey(WeaponActorInstanceId actor,EquipmentInstanceId equipment,LifecycleGeneration generation){Actor=actor;Equipment=equipment;Generation=generation;}public WeaponActorInstanceId Actor{get;}public EquipmentInstanceId Equipment{get;}public LifecycleGeneration Generation{get;}
            public bool Equals(StateKey other){return !ReferenceEquals(other,null)&&Actor.Equals(other.Actor)&&Equipment.Equals(other.Equipment)&&Generation.Equals(other.Generation);}public override bool Equals(object obj){return Equals(obj as StateKey);}public override int GetHashCode(){unchecked{int h=Actor.GetHashCode();h=h*397^Equipment.GetHashCode();return h*397^Generation.GetHashCode();}}
        }
        private sealed class FireState
        {
            private readonly HashSet<FireOperationId> accepted;private FireState(long next,long sequence,HashSet<FireOperationId> operations){NextAllowedTick=next;ShotSequence=sequence;accepted=operations;}public static FireState Initial{get{return new FireState(0L,0L,new HashSet<FireOperationId>());}}public long NextAllowedTick{get;}public long ShotSequence{get;}public bool HasAccepted(FireOperationId id){return accepted.Contains(id);}public FireState AfterAccepted(FireOperationId id,long next){HashSet<FireOperationId> copy=new HashSet<FireOperationId>(accepted);copy.Add(id);return new FireState(next,ShotSequence+1L,copy);}
        }
    }
}
