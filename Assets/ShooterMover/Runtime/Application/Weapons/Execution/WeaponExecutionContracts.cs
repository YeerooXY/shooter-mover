using System;
using System.Collections.Generic;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public sealed class WeaponFireCommand
    {
        public WeaponFireCommand(WeaponActorInstanceId actorId,EquipmentInstanceId equipmentInstanceId,FireOperationId fireOperationId,LifecycleGeneration lifecycleGeneration,long simulationTick,ulong deterministicSeed,WeaponVector2 origin,WeaponVector2 aimDirection)
        { ActorId=actorId??throw new ArgumentNullException(nameof(actorId)); EquipmentInstanceId=equipmentInstanceId??throw new ArgumentNullException(nameof(equipmentInstanceId)); FireOperationId=fireOperationId??throw new ArgumentNullException(nameof(fireOperationId)); LifecycleGeneration=lifecycleGeneration??throw new ArgumentNullException(nameof(lifecycleGeneration)); SimulationTick=simulationTick; DeterministicSeed=deterministicSeed; Origin=origin; AimDirection=aimDirection; }
        public WeaponActorInstanceId ActorId { get; } public EquipmentInstanceId EquipmentInstanceId { get; } public FireOperationId FireOperationId { get; } public LifecycleGeneration LifecycleGeneration { get; } public long SimulationTick { get; } public ulong DeterministicSeed { get; } public WeaponVector2 Origin { get; } public WeaponVector2 AimDirection { get; }
    }
    public interface IWeaponActorOwnershipResolver { bool TryResolveParticipant(WeaponActorInstanceId actorId,LifecycleGeneration lifecycleGeneration,out RunParticipantId participantId); }
    public interface IEquippedWeaponInstanceResolver { bool TryResolveEquippedWeapon(WeaponActorInstanceId actorId,EquipmentInstanceId requestedEquipmentInstanceId,out EquipmentInstance equipmentInstance); }
    public enum WeaponEffectBatchSinkStatus { Accepted=1, AlreadyAccepted=2, Rejected=3 }
    public sealed class WeaponEffectBatchSinkResult
    {
        private WeaponEffectBatchSinkResult(WeaponEffectBatchSinkStatus status,string code){Status=status;RejectionCode=code??string.Empty;}
        public WeaponEffectBatchSinkStatus Status { get; } public string RejectionCode { get; } public bool IsAcceptance { get{return Status==WeaponEffectBatchSinkStatus.Accepted||Status==WeaponEffectBatchSinkStatus.AlreadyAccepted;} }
        public static WeaponEffectBatchSinkResult Accept(){return new WeaponEffectBatchSinkResult(WeaponEffectBatchSinkStatus.Accepted,string.Empty);} public static WeaponEffectBatchSinkResult AlreadyAccepted(){return new WeaponEffectBatchSinkResult(WeaponEffectBatchSinkStatus.AlreadyAccepted,string.Empty);} public static WeaponEffectBatchSinkResult Reject(string code){return new WeaponEffectBatchSinkResult(WeaponEffectBatchSinkStatus.Rejected,code);}
    }
    public interface IWeaponEffectBatchSink { WeaponEffectBatchSinkResult TryAccept(WeaponEffectBatch batch); }
    public sealed class WeaponBehaviorContext
    {
        public WeaponBehaviorContext(WeaponFireCommand command,RunParticipantId participantId,WeaponRuntimeFiringProfile profile,long shotSequence){if(shotSequence<0L)throw new ArgumentOutOfRangeException(nameof(shotSequence));Command=command??throw new ArgumentNullException(nameof(command));ParticipantId=participantId??throw new ArgumentNullException(nameof(participantId));Profile=profile??throw new ArgumentNullException(nameof(profile));ShotSequence=shotSequence;}
        public WeaponFireCommand Command { get; } public RunParticipantId ParticipantId { get; } public WeaponRuntimeFiringProfile Profile { get; } public long ShotSequence { get; }
        public WeaponEffectIdentity IdentityFor(int ordinal){return new WeaponEffectIdentity(Command.ActorId,ParticipantId,Command.EquipmentInstanceId,Profile.DefinitionId,Command.FireOperationId,Command.LifecycleGeneration,ShotSequence,new ProjectileOrdinal(ordinal));}
    }
    public sealed class WeaponBehaviorBuildResult
    {
        private WeaponBehaviorBuildResult(WeaponEffectBatch batch,string code){Batch=batch;RejectionCode=code??string.Empty;} public WeaponEffectBatch Batch { get; } public string RejectionCode { get; } public bool Succeeded { get{return Batch!=null;} }
        public static WeaponBehaviorBuildResult Accept(WeaponEffectBatch batch){return new WeaponBehaviorBuildResult(batch??throw new ArgumentNullException(nameof(batch)),string.Empty);} public static WeaponBehaviorBuildResult Reject(string code){return new WeaponBehaviorBuildResult(null,code);}
    }
    public interface IWeaponBehavior { WeaponBehaviorId BehaviorId { get; } WeaponBehaviorBuildResult Build(WeaponBehaviorContext context); }
    public sealed class WeaponBehaviorRegistry
    {
        private readonly Dictionary<WeaponBehaviorId,IWeaponBehavior> behaviors=new Dictionary<WeaponBehaviorId,IWeaponBehavior>(); public int Count { get{return behaviors.Count;} }
        public void Register(IWeaponBehavior behavior){if(behavior==null)throw new ArgumentNullException(nameof(behavior));if(behavior.BehaviorId==null)throw new ArgumentException("Weapon behavior ID is required.",nameof(behavior));if(behaviors.ContainsKey(behavior.BehaviorId))throw new InvalidOperationException("Duplicate weapon behavior: "+behavior.BehaviorId);behaviors.Add(behavior.BehaviorId,behavior);}
        public bool TryResolve(WeaponBehaviorId id,out IWeaponBehavior behavior){return id!=null&&behaviors.TryGetValue(id,out behavior);}
        public static WeaponBehaviorRegistry CreateWithBuiltIns(){WeaponBehaviorRegistry r=new WeaponBehaviorRegistry();r.Register(new ProjectileWeaponBehavior());r.Register(new ExplosiveWeaponBehavior());r.Register(new ChainWeaponBehavior());return r;}
    }
}
