using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    public sealed class GunFireCommand
    {
        public GunFireCommand(
            GunActorInstanceId actorId,
            EquipmentInstanceId equipmentInstanceId,
            FireOperationId fireOperationId,
            LifecycleGeneration lifecycleGeneration,
            long simulationTick,
            ulong deterministicSeed,
            GunVector2 origin,
            GunVector2 aimDirection)
        {
            ActorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
            EquipmentInstanceId = equipmentInstanceId
                ?? throw new ArgumentNullException(nameof(equipmentInstanceId));
            FireOperationId = fireOperationId
                ?? throw new ArgumentNullException(nameof(fireOperationId));
            LifecycleGeneration = lifecycleGeneration
                ?? throw new ArgumentNullException(nameof(lifecycleGeneration));
            SimulationTick = simulationTick;
            DeterministicSeed = deterministicSeed;
            Origin = origin;
            AimDirection = aimDirection;
            CanonicalText = BuildCanonicalText();
            Fingerprint = GunExecutionFingerprint.Compute(CanonicalText);
        }

        public GunActorInstanceId ActorId { get; }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public FireOperationId FireOperationId { get; }
        public LifecycleGeneration LifecycleGeneration { get; }
        public long SimulationTick { get; }
        public ulong DeterministicSeed { get; }
        public GunVector2 Origin { get; }
        public GunVector2 AimDirection { get; }
        public string CanonicalText { get; }
        public string Fingerprint { get; }

        private string BuildCanonicalText()
        {
            return string.Join(
                "\n",
                new[]
                {
                    "actor_id=" + ActorId,
                    "equipment_instance_id=" + EquipmentInstanceId,
                    "fire_operation_id=" + FireOperationId,
                    "lifecycle_generation=" + LifecycleGeneration,
                    "simulation_tick=" + SimulationTick.ToString(CultureInfo.InvariantCulture),
                    "deterministic_seed=" + DeterministicSeed.ToString(CultureInfo.InvariantCulture),
                    "origin=" + FormatVector(Origin),
                    "aim_direction=" + FormatVector(AimDirection),
                });
        }

        private static string FormatVector(GunVector2 value)
        {
            return value == null ? "null" : value.ToString();
        }
    }

    public interface IGunActorOwnershipResolver
    {
        bool TryResolveParticipant(
            GunActorInstanceId actorId,
            LifecycleGeneration lifecycleGeneration,
            out RunParticipantId participantId);
    }

    public interface IEquippedGunInstanceResolver
    {
        bool TryResolveEquippedGun(
            GunActorInstanceId actorId,
            EquipmentInstanceId requestedEquipmentInstanceId,
            out EquipmentInstance equipmentInstance);
    }

    public enum GunEffectBatchSinkStatus
    {
        Accepted = 1,
        AlreadyAccepted = 2,
        Rejected = 3,
    }

    public sealed class GunEffectBatchSinkResult
    {
        private GunEffectBatchSinkResult(
            GunEffectBatchSinkStatus status,
            string rejectionCode)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public GunEffectBatchSinkStatus Status { get; }
        public string RejectionCode { get; }

        public bool IsAcceptance
        {
            get
            {
                return Status == GunEffectBatchSinkStatus.Accepted
                    || Status == GunEffectBatchSinkStatus.AlreadyAccepted;
            }
        }

        public static GunEffectBatchSinkResult Accept()
        {
            return new GunEffectBatchSinkResult(
                GunEffectBatchSinkStatus.Accepted,
                string.Empty);
        }

        public static GunEffectBatchSinkResult AlreadyAccepted()
        {
            return new GunEffectBatchSinkResult(
                GunEffectBatchSinkStatus.AlreadyAccepted,
                string.Empty);
        }

        public static GunEffectBatchSinkResult Reject(string code)
        {
            return new GunEffectBatchSinkResult(
                GunEffectBatchSinkStatus.Rejected,
                code);
        }
    }

    public interface IGunEffectBatchSink
    {
        GunEffectBatchSinkResult TryAccept(GunEffectBatch batch);
    }

    public sealed class GunBehaviorContext
    {
        public GunBehaviorContext(
            GunFireCommand command,
            RunParticipantId participantId,
            GunLiveFiringProfile profile,
            long shotSequence)
        {
            if (shotSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(shotSequence));
            }

            Command = command ?? throw new ArgumentNullException(nameof(command));
            ParticipantId = participantId ?? throw new ArgumentNullException(nameof(participantId));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            ShotSequence = shotSequence;
        }

        public GunFireCommand Command { get; }
        public RunParticipantId ParticipantId { get; }
        public GunLiveFiringProfile Profile { get; }
        public long ShotSequence { get; }

        public GunEffectIdentity IdentityFor(int ordinal)
        {
            return new GunEffectIdentity(
                Command.ActorId,
                ParticipantId,
                Command.EquipmentInstanceId,
                Profile.DefinitionId,
                Command.FireOperationId,
                Command.LifecycleGeneration,
                ShotSequence,
                new ProjectileOrdinal(ordinal));
        }
    }

    public sealed class GunBehaviorBuildResult
    {
        private GunBehaviorBuildResult(GunEffectBatch batch, string rejectionCode)
        {
            Batch = batch;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public GunEffectBatch Batch { get; }
        public string RejectionCode { get; }
        public bool Succeeded { get { return Batch != null; } }

        public static GunBehaviorBuildResult Accept(GunEffectBatch batch)
        {
            return new GunBehaviorBuildResult(
                batch ?? throw new ArgumentNullException(nameof(batch)),
                string.Empty);
        }

        public static GunBehaviorBuildResult Reject(string code)
        {
            return new GunBehaviorBuildResult(null, code);
        }
    }

    public interface IGunBehavior
    {
        GunBehaviorId BehaviorId { get; }
        GunBehaviorBuildResult Build(GunBehaviorContext context);
    }

    public sealed class GunBehaviorRegistry
    {
        private readonly Dictionary<GunBehaviorId, IGunBehavior> behaviors =
            new Dictionary<GunBehaviorId, IGunBehavior>();

        public int Count { get { return behaviors.Count; } }

        public void Register(IGunBehavior behavior)
        {
            if (behavior == null)
            {
                throw new ArgumentNullException(nameof(behavior));
            }

            if (behavior.BehaviorId == null)
            {
                throw new ArgumentException("Gun behavior ID is required.", nameof(behavior));
            }

            if (behaviors.ContainsKey(behavior.BehaviorId))
            {
                throw new InvalidOperationException(
                    "Duplicate gun behavior: " + behavior.BehaviorId);
            }

            behaviors.Add(behavior.BehaviorId, behavior);
        }

        public bool TryResolve(GunBehaviorId id, out IGunBehavior behavior)
        {
            if (id == null)
            {
                behavior = null;
                return false;
            }

            return behaviors.TryGetValue(id, out behavior);
        }

        public static GunBehaviorRegistry CreateWithBuiltIns()
        {
            GunBehaviorRegistry registry = new GunBehaviorRegistry();
            registry.Register(new ProjectileGunBehavior());
            registry.Register(new ExplosiveGunBehavior());
            registry.Register(new DamageOverTimeGunBehavior());
            registry.Register(new ChainGunBehavior());
            return registry;
        }
    }
}
