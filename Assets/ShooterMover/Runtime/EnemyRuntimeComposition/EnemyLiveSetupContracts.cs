using System;
using System.Globalization;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyRuntimeComposition
{
    public sealed class EnemyLiveIdentity
    {
        public EnemyLiveIdentity(
            StableId entityInstanceId,
            StableId runParticipantId,
            StableId runStableId,
            StableId roomRuntimeInstanceStableId,
            StableId roomStableId,
            StableId placementStableId)
        {
            EntityInstanceId = entityInstanceId ?? throw new ArgumentNullException(nameof(entityInstanceId));
            RunParticipantId = runParticipantId ?? throw new ArgumentNullException(nameof(runParticipantId));
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            RoomRuntimeInstanceStableId = roomRuntimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(roomRuntimeInstanceStableId));
            RoomStableId = roomStableId ?? throw new ArgumentNullException(nameof(roomStableId));
            PlacementStableId = placementStableId ?? throw new ArgumentNullException(nameof(placementStableId));
        }

        public StableId EntityInstanceId { get; }
        public StableId RunParticipantId { get; }
        public StableId RunStableId { get; }
        public StableId RoomRuntimeInstanceStableId { get; }
        public StableId RoomStableId { get; }
        public StableId PlacementStableId { get; }
    }

    public interface IEnemyLiveIdentityDeriver
    {
        EnemyLiveIdentity Derive(
            StableId runStableId,
            StableId roomRuntimeInstanceStableId,
            StableId roomStableId,
            StableId placementStableId);
    }

    public sealed class DeterministicEnemyLiveIdentityDeriver : IEnemyLiveIdentityDeriver
    {
        public EnemyLiveIdentity Derive(
            StableId runStableId,
            StableId roomRuntimeInstanceStableId,
            StableId roomStableId,
            StableId placementStableId)
        {
            if (runStableId == null) throw new ArgumentNullException(nameof(runStableId));
            if (roomRuntimeInstanceStableId == null)
                throw new ArgumentNullException(nameof(roomRuntimeInstanceStableId));
            if (roomStableId == null) throw new ArgumentNullException(nameof(roomStableId));
            if (placementStableId == null) throw new ArgumentNullException(nameof(placementStableId));

            string basis = runStableId
                + "|" + roomRuntimeInstanceStableId
                + "|" + roomStableId
                + "|" + placementStableId;
            return new EnemyLiveIdentity(
                StableId.Create("enemy-entity", "runtime-" + Hash64(basis + "|entity")),
                StableId.Create("run-participant", "enemy-" + Hash64(basis + "|participant")),
                runStableId,
                roomRuntimeInstanceStableId,
                roomStableId,
                placementStableId);
        }

        internal static string Hash64(string value)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis;
            for (int index = 0; index < value.Length; index++)
            {
                hash ^= value[index];
                hash *= prime;
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }

    public sealed class EnemyDifficultyContext
    {
        public EnemyDifficultyContext(StableId difficultyId, double scalar)
        {
            DifficultyId = difficultyId ?? throw new ArgumentNullException(nameof(difficultyId));
            if (!IsFinitePositive(scalar)) throw new ArgumentOutOfRangeException(nameof(scalar));
            Scalar = scalar;
        }

        public StableId DifficultyId { get; }
        public double Scalar { get; }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        }
    }

    public sealed class EnemyDifficultyScalingConfiguration
    {
        public EnemyDifficultyScalingConfiguration(
            StableId policyId,
            double healthResponse,
            double damageResponse,
            double cooldownResponse,
            double movementResponse)
        {
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
            RequireFiniteNonNegative(healthResponse, nameof(healthResponse));
            RequireFiniteNonNegative(damageResponse, nameof(damageResponse));
            RequireFiniteNonNegative(cooldownResponse, nameof(cooldownResponse));
            RequireFiniteNonNegative(movementResponse, nameof(movementResponse));
            HealthResponse = healthResponse;
            DamageResponse = damageResponse;
            CooldownResponse = cooldownResponse;
            MovementResponse = movementResponse;
        }

        public StableId PolicyId { get; }
        public double HealthResponse { get; }
        public double DamageResponse { get; }
        public double CooldownResponse { get; }
        public double MovementResponse { get; }

        private static void RequireFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public sealed class EnemyDifficultyScaling
    {
        public EnemyDifficultyScaling(
            double healthMultiplier,
            double damageMultiplier,
            double cooldownMultiplier,
            double movementMultiplier)
        {
            RequireFinitePositive(healthMultiplier, nameof(healthMultiplier));
            RequireFinitePositive(damageMultiplier, nameof(damageMultiplier));
            RequireFinitePositive(cooldownMultiplier, nameof(cooldownMultiplier));
            RequireFinitePositive(movementMultiplier, nameof(movementMultiplier));
            HealthMultiplier = healthMultiplier;
            DamageMultiplier = damageMultiplier;
            CooldownMultiplier = cooldownMultiplier;
            MovementMultiplier = movementMultiplier;
        }

        public double HealthMultiplier { get; }
        public double DamageMultiplier { get; }
        public double CooldownMultiplier { get; }
        public double MovementMultiplier { get; }

        private static void RequireFinitePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public interface IEnemyDifficultyScalingPolicy
    {
        EnemyDifficultyScaling Resolve(
            int enemyLevel,
            EnemyDifficultyContext context,
            EnemyDifficultyScalingConfiguration configuration);
    }

    public sealed class ScalarEnemyDifficultyScalingPolicy : IEnemyDifficultyScalingPolicy
    {
        public EnemyDifficultyScaling Resolve(
            int enemyLevel,
            EnemyDifficultyContext context,
            EnemyDifficultyScalingConfiguration configuration)
        {
            if (enemyLevel <= 0) throw new ArgumentOutOfRangeException(nameof(enemyLevel));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            double delta = context.Scalar - 1d;
            return new EnemyDifficultyScaling(
                Math.Max(0.01d, 1d + (delta * configuration.HealthResponse)),
                Math.Max(0.01d, 1d + (delta * configuration.DamageResponse)),
                Math.Max(0.01d, 1d - (delta * configuration.CooldownResponse)),
                Math.Max(0.01d, 1d + (delta * configuration.MovementResponse)));
        }
    }

    public sealed class EnemyPerceptionPolicyConfiguration
    {
        public EnemyPerceptionPolicyConfiguration(StableId policyId)
        {
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
        }

        // Compatibility for callers compiled against the first draft. The removed option can no
        // longer promise an invariant that this engine-neutral adapter has no position authority to prove.
        public EnemyPerceptionPolicyConfiguration(
            StableId policyId,
            bool requireMatchingObserverPosition)
            : this(policyId)
        {
            if (requireMatchingObserverPosition)
            {
                throw new ArgumentException(
                    "Observer-position matching requires a real authoritative position port and is not configurable here.",
                    nameof(requireMatchingObserverPosition));
            }
        }

        public StableId PolicyId { get; }
    }

    public interface IEnemyPerceptionLiveBridge
    {
        EnemyPerceptionSnapshot Adapt(
            EnemyLiveView runtime,
            EnemyDefinition definition,
            EnemyPerceptionSnapshot source,
            EnemyPerceptionPolicyConfiguration configuration);
    }

    public sealed class ValidatedEnemyPerceptionLiveBridge : IEnemyPerceptionLiveBridge
    {
        public EnemyPerceptionSnapshot Adapt(
            EnemyLiveView runtime,
            EnemyDefinition definition,
            EnemyPerceptionSnapshot source,
            EnemyPerceptionPolicyConfiguration configuration)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (runtime.Definition.DefinitionId != definition.DefinitionId)
                throw new ArgumentException("Perception definition does not match the runtime.");
            return source;
        }
    }

    public sealed class EnemyPerceptionLiveRegistration
    {
        public EnemyPerceptionLiveRegistration(
            EnemyPerceptionPolicyConfiguration configuration,
            IEnemyPerceptionLiveBridge adapter)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        public EnemyPerceptionPolicyConfiguration Configuration { get; }
        public IEnemyPerceptionLiveBridge Adapter { get; }
    }

    public sealed class EnemyDifficultyLiveRegistration
    {
        public EnemyDifficultyLiveRegistration(
            EnemyDifficultyScalingConfiguration configuration,
            IEnemyDifficultyScalingPolicy policy)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public EnemyDifficultyScalingConfiguration Configuration { get; }
        public IEnemyDifficultyScalingPolicy Policy { get; }
    }

    public enum EnemyLiveOperationStatus
    {
        Applied = 1,
        ExactReplay = 2,
        NoEffect = 3,
        Rejected = 4,
    }

    public enum EnemyLiveRejectionCode
    {
        None = 0,
        MissingAttackIntent = 1,
        UnknownAttack = 2,
        CooldownActive = 3,
        StaleLifecycle = 4,
        EntityMismatch = 5,
        ConflictingDuplicate = 6,
        InvalidCommand = 7,
        ActorTerminal = 8,
        DecisionNotIssued = 9,
        ExecutionNotIssued = 10,
    }

    public sealed class EnemyPlacementDecision
    {
        public EnemyPlacementDecision(
            StableId entityInstanceId,
            long lifecycleGeneration,
            EnemyPerceptionSnapshot perception,
            EnemyDecisionEvaluation evaluation)
        {
            EntityInstanceId = entityInstanceId ?? throw new ArgumentNullException(nameof(entityInstanceId));
            if (lifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            LifecycleGeneration = lifecycleGeneration;
            Perception = perception ?? throw new ArgumentNullException(nameof(perception));
            Evaluation = evaluation ?? throw new ArgumentNullException(nameof(evaluation));
        }

        public StableId EntityInstanceId { get; }
        public long LifecycleGeneration { get; }
        public EnemyPerceptionSnapshot Perception { get; }
        public EnemyDecisionEvaluation Evaluation { get; }
    }

    public sealed class EnemyAttackExecutionContext
    {
        public EnemyAttackExecutionContext(
            StableId operationStableId,
            EnemyLiveIdentity identity,
            long lifecycleGeneration,
            double occurredAtSeconds,
            EnemyDifficultyScaling difficultyScaling)
        {
            OperationStableId = operationStableId ?? throw new ArgumentNullException(nameof(operationStableId));
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            if (lifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            if (double.IsNaN(occurredAtSeconds)
                || double.IsInfinity(occurredAtSeconds)
                || occurredAtSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(occurredAtSeconds));
            LifecycleGeneration = lifecycleGeneration;
            OccurredAtSeconds = occurredAtSeconds;
            DifficultyScaling = difficultyScaling ?? throw new ArgumentNullException(nameof(difficultyScaling));
        }

        public StableId OperationStableId { get; }
        public EnemyLiveIdentity Identity { get; }
        public long LifecycleGeneration { get; }
        public double OccurredAtSeconds { get; }
        public EnemyDifficultyScaling DifficultyScaling { get; }
    }

    public sealed class EnemyAttackExecutionResult
    {
        public EnemyAttackExecutionResult(
            EnemyLiveOperationStatus status,
            EnemyLiveRejectionCode rejection,
            EnemyAttackExecutionRequest request)
        {
            Status = status;
            Rejection = rejection;
            Request = request;
        }

        public EnemyLiveOperationStatus Status { get; }
        public EnemyLiveRejectionCode Rejection { get; }
        public EnemyAttackExecutionRequest Request { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == EnemyLiveOperationStatus.Applied
                    || Status == EnemyLiveOperationStatus.ExactReplay;
            }
        }
    }

    public sealed class EnemyPlayerDamageRequest
    {
        public EnemyPlayerDamageRequest(
            StableId hitEventStableId,
            StableId attackOperationStableId,
            StableId sourceEntityStableId,
            StableId sourceRunParticipantStableId,
            StableId targetEntityStableId,
            long observedTargetLifecycleGeneration,
            long sourceLifecycleGeneration,
            double damage,
            StableId damageChannelStableId,
            EnemyAttackIntent committedIntent)
        {
            HitEventStableId = hitEventStableId ?? throw new ArgumentNullException(nameof(hitEventStableId));
            AttackOperationStableId = attackOperationStableId
                ?? throw new ArgumentNullException(nameof(attackOperationStableId));
            SourceEntityStableId = sourceEntityStableId
                ?? throw new ArgumentNullException(nameof(sourceEntityStableId));
            SourceRunParticipantStableId = sourceRunParticipantStableId
                ?? throw new ArgumentNullException(nameof(sourceRunParticipantStableId));
            TargetEntityStableId = targetEntityStableId
                ?? throw new ArgumentNullException(nameof(targetEntityStableId));
            if (observedTargetLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(observedTargetLifecycleGeneration));
            if (sourceLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sourceLifecycleGeneration));
            if (double.IsNaN(damage) || double.IsInfinity(damage) || damage <= 0d)
                throw new ArgumentOutOfRangeException(nameof(damage));
            DamageChannelStableId = damageChannelStableId
                ?? throw new ArgumentNullException(nameof(damageChannelStableId));
            CommittedIntent = committedIntent ?? throw new ArgumentNullException(nameof(committedIntent));
            ObservedTargetLifecycleGeneration = observedTargetLifecycleGeneration;
            SourceLifecycleGeneration = sourceLifecycleGeneration;
            Damage = damage;
        }

        public StableId HitEventStableId { get; }
        public StableId AttackOperationStableId { get; }
        public StableId SourceEntityStableId { get; }
        public StableId SourceRunParticipantStableId { get; }
        public StableId TargetEntityStableId { get; }
        public long ObservedTargetLifecycleGeneration { get; }
        public long SourceLifecycleGeneration { get; }
        public double Damage { get; }
        public StableId DamageChannelStableId { get; }
        public EnemyAttackIntent CommittedIntent { get; }
    }

    public sealed class EnemyPlayerDamagePortResult
    {
        public EnemyPlayerDamagePortResult(
            EnemyLiveOperationStatus status,
            EnemyLiveRejectionCode rejection)
        {
            Status = status;
            Rejection = rejection;
        }

        public EnemyLiveOperationStatus Status { get; }
        public EnemyLiveRejectionCode Rejection { get; }
    }

    public interface IEnemyAttackEffectPort
    {
        void Emit(EnemyAttackExecutionRequest request);
    }

    public interface IEnemyPlayerDamagePort
    {
        EnemyPlayerDamagePortResult Route(EnemyPlayerDamageRequest request);
    }

    public sealed class EnemyTerminalCollisionFact
    {
        public EnemyTerminalCollisionFact(
            StableId entityInstanceStableId,
            StableId terminalEventStableId,
            long lifecycleGeneration)
        {
            EntityInstanceStableId = entityInstanceStableId
                ?? throw new ArgumentNullException(nameof(entityInstanceStableId));
            TerminalEventStableId = terminalEventStableId
                ?? throw new ArgumentNullException(nameof(terminalEventStableId));
            if (lifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            LifecycleGeneration = lifecycleGeneration;
        }

        public StableId EntityInstanceStableId { get; }
        public StableId TerminalEventStableId { get; }
        public long LifecycleGeneration { get; }
    }

    public sealed class EnemyDeathFact
    {
        public EnemyDeathFact(
            StableId deathEventStableId,
            StableId triggeringEventStableId,
            EnemyLiveIdentity identity,
            StableId definitionStableId,
            int level,
            long lifecycleGeneration,
            StableId killerEntityStableId,
            StableId killerRunParticipantStableId,
            StableId experienceProfileStableId,
            StableId dropProfileStableId,
            EnemyActorDeathCause deathCause)
        {
            DeathEventStableId = deathEventStableId
                ?? throw new ArgumentNullException(nameof(deathEventStableId));
            TriggeringEventStableId = triggeringEventStableId
                ?? throw new ArgumentNullException(nameof(triggeringEventStableId));
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            DefinitionStableId = definitionStableId
                ?? throw new ArgumentNullException(nameof(definitionStableId));
            if (level <= 0) throw new ArgumentOutOfRangeException(nameof(level));
            if (lifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            if (!Enum.IsDefined(typeof(EnemyActorDeathCause), deathCause))
                throw new ArgumentOutOfRangeException(nameof(deathCause));
            Level = level;
            LifecycleGeneration = lifecycleGeneration;
            KillerEntityStableId = killerEntityStableId;
            KillerRunParticipantStableId = killerRunParticipantStableId;
            ExperienceProfileStableId = experienceProfileStableId;
            DropProfileStableId = dropProfileStableId;
            DeathCause = deathCause;
        }

        public StableId DeathEventStableId { get; }
        public StableId TriggeringEventStableId { get; }
        public EnemyLiveIdentity Identity { get; }
        public StableId DefinitionStableId { get; }
        public int Level { get; }
        public long LifecycleGeneration { get; }
        public StableId KillerEntityStableId { get; }
        public StableId KillerRunParticipantStableId { get; }
        public StableId ExperienceProfileStableId { get; }
        public StableId DropProfileStableId { get; }
        public EnemyActorDeathCause DeathCause { get; }
    }

    public interface IEnemyRoomTerminalPort
    {
        void Report(ReportRoomOccupantTerminalCommand command, EnemyDeathFact deathFact);
    }

    public interface IEnemyExperienceFactConsumer
    {
        void Consume(EnemyDeathFact fact);
    }

    public interface IEnemyDropFactConsumer
    {
        void Consume(EnemyDeathFact fact);
    }

    public interface IEnemyKillStatFactConsumer
    {
        void Consume(EnemyDeathFact fact);
    }

    public interface IEnemyTerminalCollisionBridge
    {
        void SetTerminal(EnemyTerminalCollisionFact fact);
    }

    public sealed class EnemyLiveDownstreamPorts
    {
        public EnemyLiveDownstreamPorts(
            IEnemyAttackEffectPort attackEffects,
            IEnemyPlayerDamagePort playerDamage,
            IEnemyRoomTerminalPort roomTerminal,
            IEnemyExperienceFactConsumer experience,
            IEnemyDropFactConsumer drops,
            IEnemyKillStatFactConsumer killStats,
            IEnemyTerminalCollisionBridge terminalCollision)
        {
            AttackEffects = attackEffects ?? throw new ArgumentNullException(nameof(attackEffects));
            PlayerDamage = playerDamage ?? throw new ArgumentNullException(nameof(playerDamage));
            RoomTerminal = roomTerminal ?? throw new ArgumentNullException(nameof(roomTerminal));
            Experience = experience ?? throw new ArgumentNullException(nameof(experience));
            Drops = drops ?? throw new ArgumentNullException(nameof(drops));
            KillStats = killStats ?? throw new ArgumentNullException(nameof(killStats));
            TerminalCollision = terminalCollision ?? throw new ArgumentNullException(nameof(terminalCollision));
        }

        public IEnemyAttackEffectPort AttackEffects { get; }
        public IEnemyPlayerDamagePort PlayerDamage { get; }
        public IEnemyRoomTerminalPort RoomTerminal { get; }
        public IEnemyExperienceFactConsumer Experience { get; }
        public IEnemyDropFactConsumer Drops { get; }
        public IEnemyKillStatFactConsumer KillStats { get; }
        public IEnemyTerminalCollisionBridge TerminalCollision { get; }

        public static EnemyLiveDownstreamPorts None()
        {
            var sink = new NoOpEnemyLivePort();
            return new EnemyLiveDownstreamPorts(sink, sink, sink, sink, sink, sink, sink);
        }
    }

    internal sealed class NoOpEnemyLivePort :
        IEnemyAttackEffectPort,
        IEnemyPlayerDamagePort,
        IEnemyRoomTerminalPort,
        IEnemyExperienceFactConsumer,
        IEnemyDropFactConsumer,
        IEnemyKillStatFactConsumer,
        IEnemyTerminalCollisionBridge
    {
        public void Emit(EnemyAttackExecutionRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
        }

        public EnemyPlayerDamagePortResult Route(EnemyPlayerDamageRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return new EnemyPlayerDamagePortResult(
                EnemyLiveOperationStatus.NoEffect,
                EnemyLiveRejectionCode.None);
        }

        public void Report(ReportRoomOccupantTerminalCommand command, EnemyDeathFact deathFact)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (deathFact == null) throw new ArgumentNullException(nameof(deathFact));
        }

        public void Consume(EnemyDeathFact fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
        }

        public void SetTerminal(EnemyTerminalCollisionFact fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
        }
    }

    public sealed class EnemyLiveDamageCommand
    {
        public EnemyLiveDamageCommand(
            StableId operationStableId,
            StableId sourceEntityStableId,
            StableId sourceRunParticipantStableId,
            StableId targetEntityStableId,
            long targetLifecycleGeneration,
            long order,
            int channelValue,
            double amount)
        {
            OperationStableId = operationStableId ?? throw new ArgumentNullException(nameof(operationStableId));
            SourceEntityStableId = sourceEntityStableId ?? throw new ArgumentNullException(nameof(sourceEntityStableId));
            TargetEntityStableId = targetEntityStableId ?? throw new ArgumentNullException(nameof(targetEntityStableId));
            if (targetLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(targetLifecycleGeneration));
            if (order < 0L) throw new ArgumentOutOfRangeException(nameof(order));
            if (channelValue < 1 || channelValue > 6)
                throw new ArgumentOutOfRangeException(nameof(channelValue));
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount <= 0d)
                throw new ArgumentOutOfRangeException(nameof(amount));
            SourceRunParticipantStableId = sourceRunParticipantStableId;
            TargetLifecycleGeneration = targetLifecycleGeneration;
            Order = order;
            ChannelValue = channelValue;
            Amount = amount;
        }

        public StableId OperationStableId { get; }
        public StableId SourceEntityStableId { get; }
        public StableId SourceRunParticipantStableId { get; }
        public StableId TargetEntityStableId { get; }
        public long TargetLifecycleGeneration { get; }
        public long Order { get; }
        public int ChannelValue { get; }
        public double Amount { get; }
    }

    public sealed class EnemyLiveDamageResult
    {
        public EnemyLiveDamageResult(
            EnemyLiveOperationStatus status,
            EnemyLiveRejectionCode rejection,
            EnemyLiveView runtime,
            EnemyDeathFact deathFact)
        {
            Status = status;
            Rejection = rejection;
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            DeathFact = deathFact;
        }

        public EnemyLiveOperationStatus Status { get; }
        public EnemyLiveRejectionCode Rejection { get; }
        public EnemyLiveView Runtime { get; }
        public EnemyDeathFact DeathFact { get; }
    }
}
