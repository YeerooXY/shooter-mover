using System;
using System.Globalization;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyRuntimeComposition
{
    public sealed class EnemyRuntimeIdentityV1
    {
        public EnemyRuntimeIdentityV1(
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

    public interface IEnemyRuntimeIdentityDeriverV1
    {
        EnemyRuntimeIdentityV1 Derive(
            StableId runStableId,
            StableId roomRuntimeInstanceStableId,
            StableId roomStableId,
            StableId placementStableId);
    }

    public sealed class DeterministicEnemyRuntimeIdentityDeriverV1 : IEnemyRuntimeIdentityDeriverV1
    {
        public EnemyRuntimeIdentityV1 Derive(
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
            return new EnemyRuntimeIdentityV1(
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

    public sealed class EnemyDifficultyContextV1
    {
        public EnemyDifficultyContextV1(StableId difficultyId, double scalar)
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

    public sealed class EnemyDifficultyScalingConfigurationV1
    {
        public EnemyDifficultyScalingConfigurationV1(
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

    public sealed class EnemyDifficultyScalingV1
    {
        public EnemyDifficultyScalingV1(
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

    public interface IEnemyDifficultyScalingPolicyV1
    {
        EnemyDifficultyScalingV1 Resolve(
            int enemyLevel,
            EnemyDifficultyContextV1 context,
            EnemyDifficultyScalingConfigurationV1 configuration);
    }

    public sealed class ScalarEnemyDifficultyScalingPolicyV1 : IEnemyDifficultyScalingPolicyV1
    {
        public EnemyDifficultyScalingV1 Resolve(
            int enemyLevel,
            EnemyDifficultyContextV1 context,
            EnemyDifficultyScalingConfigurationV1 configuration)
        {
            if (enemyLevel <= 0) throw new ArgumentOutOfRangeException(nameof(enemyLevel));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            double delta = context.Scalar - 1d;
            return new EnemyDifficultyScalingV1(
                Math.Max(0.01d, 1d + (delta * configuration.HealthResponse)),
                Math.Max(0.01d, 1d + (delta * configuration.DamageResponse)),
                Math.Max(0.01d, 1d - (delta * configuration.CooldownResponse)),
                Math.Max(0.01d, 1d + (delta * configuration.MovementResponse)));
        }
    }

    public sealed class EnemyPerceptionPolicyConfigurationV1
    {
        public EnemyPerceptionPolicyConfigurationV1(StableId policyId)
        {
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
        }

        // Compatibility for callers compiled against the first draft. The removed option can no
        // longer promise an invariant that this engine-neutral adapter has no position authority to prove.
        public EnemyPerceptionPolicyConfigurationV1(
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

    public interface IEnemyPerceptionRuntimeAdapterV1
    {
        EnemyPerceptionSnapshot Adapt(
            EnemyRuntimeProjection runtime,
            EnemyDefinitionV1 definition,
            EnemyPerceptionSnapshot source,
            EnemyPerceptionPolicyConfigurationV1 configuration);
    }

    public sealed class ValidatedEnemyPerceptionRuntimeAdapterV1 : IEnemyPerceptionRuntimeAdapterV1
    {
        public EnemyPerceptionSnapshot Adapt(
            EnemyRuntimeProjection runtime,
            EnemyDefinitionV1 definition,
            EnemyPerceptionSnapshot source,
            EnemyPerceptionPolicyConfigurationV1 configuration)
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

    public sealed class EnemyPerceptionRuntimeRegistrationV1
    {
        public EnemyPerceptionRuntimeRegistrationV1(
            EnemyPerceptionPolicyConfigurationV1 configuration,
            IEnemyPerceptionRuntimeAdapterV1 adapter)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        public EnemyPerceptionPolicyConfigurationV1 Configuration { get; }
        public IEnemyPerceptionRuntimeAdapterV1 Adapter { get; }
    }

    public sealed class EnemyDifficultyRuntimeRegistrationV1
    {
        public EnemyDifficultyRuntimeRegistrationV1(
            EnemyDifficultyScalingConfigurationV1 configuration,
            IEnemyDifficultyScalingPolicyV1 policy)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public EnemyDifficultyScalingConfigurationV1 Configuration { get; }
        public IEnemyDifficultyScalingPolicyV1 Policy { get; }
    }

    public enum EnemyRuntimeOperationStatusV1
    {
        Applied = 1,
        ExactReplay = 2,
        NoEffect = 3,
        Rejected = 4,
    }

    public enum EnemyRuntimeRejectionCodeV1
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

    public sealed class EnemyPlacementDecisionV1
    {
        public EnemyPlacementDecisionV1(
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

    public sealed class EnemyAttackExecutionContextV1
    {
        public EnemyAttackExecutionContextV1(
            StableId operationStableId,
            EnemyRuntimeIdentityV1 identity,
            long lifecycleGeneration,
            double occurredAtSeconds,
            EnemyDifficultyScalingV1 difficultyScaling)
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
        public EnemyRuntimeIdentityV1 Identity { get; }
        public long LifecycleGeneration { get; }
        public double OccurredAtSeconds { get; }
        public EnemyDifficultyScalingV1 DifficultyScaling { get; }
    }

    public sealed class EnemyAttackExecutionResultV1
    {
        public EnemyAttackExecutionResultV1(
            EnemyRuntimeOperationStatusV1 status,
            EnemyRuntimeRejectionCodeV1 rejection,
            EnemyAttackExecutionRequestV1 request)
        {
            Status = status;
            Rejection = rejection;
            Request = request;
        }

        public EnemyRuntimeOperationStatusV1 Status { get; }
        public EnemyRuntimeRejectionCodeV1 Rejection { get; }
        public EnemyAttackExecutionRequestV1 Request { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == EnemyRuntimeOperationStatusV1.Applied
                    || Status == EnemyRuntimeOperationStatusV1.ExactReplay;
            }
        }
    }

    public sealed class EnemyPlayerDamageRequestV1
    {
        public EnemyPlayerDamageRequestV1(
            StableId hitEventStableId,
            StableId attackOperationStableId,
            StableId sourceEntityStableId,
            StableId sourceRunParticipantStableId,
            StableId targetEntityStableId,
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
            if (sourceLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sourceLifecycleGeneration));
            if (double.IsNaN(damage) || double.IsInfinity(damage) || damage <= 0d)
                throw new ArgumentOutOfRangeException(nameof(damage));
            DamageChannelStableId = damageChannelStableId
                ?? throw new ArgumentNullException(nameof(damageChannelStableId));
            CommittedIntent = committedIntent ?? throw new ArgumentNullException(nameof(committedIntent));
            SourceLifecycleGeneration = sourceLifecycleGeneration;
            Damage = damage;
        }

        public StableId HitEventStableId { get; }
        public StableId AttackOperationStableId { get; }
        public StableId SourceEntityStableId { get; }
        public StableId SourceRunParticipantStableId { get; }
        public StableId TargetEntityStableId { get; }
        public long SourceLifecycleGeneration { get; }
        public double Damage { get; }
        public StableId DamageChannelStableId { get; }
        public EnemyAttackIntent CommittedIntent { get; }
    }

    public sealed class EnemyPlayerDamagePortResultV1
    {
        public EnemyPlayerDamagePortResultV1(
            EnemyRuntimeOperationStatusV1 status,
            EnemyRuntimeRejectionCodeV1 rejection)
        {
            Status = status;
            Rejection = rejection;
        }

        public EnemyRuntimeOperationStatusV1 Status { get; }
        public EnemyRuntimeRejectionCodeV1 Rejection { get; }
    }

    public interface IEnemyAttackEffectPortV1
    {
        void Emit(EnemyAttackExecutionRequestV1 request);
    }

    public interface IEnemyPlayerDamagePortV1
    {
        EnemyPlayerDamagePortResultV1 Route(EnemyPlayerDamageRequestV1 request);
    }

    public sealed class EnemyTerminalCollisionFactV1
    {
        public EnemyTerminalCollisionFactV1(
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

    public sealed class EnemyDeathFactV1
    {
        public EnemyDeathFactV1(
            StableId deathEventStableId,
            StableId triggeringEventStableId,
            EnemyRuntimeIdentityV1 identity,
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
        public EnemyRuntimeIdentityV1 Identity { get; }
        public StableId DefinitionStableId { get; }
        public int Level { get; }
        public long LifecycleGeneration { get; }
        public StableId KillerEntityStableId { get; }
        public StableId KillerRunParticipantStableId { get; }
        public StableId ExperienceProfileStableId { get; }
        public StableId DropProfileStableId { get; }
        public EnemyActorDeathCause DeathCause { get; }
    }

    public interface IEnemyRoomTerminalPortV1
    {
        void Report(ReportRoomOccupantTerminalCommandV1 command, EnemyDeathFactV1 deathFact);
    }

    public interface IEnemyExperienceFactConsumerV1
    {
        void Consume(EnemyDeathFactV1 fact);
    }

    public interface IEnemyDropFactConsumerV1
    {
        void Consume(EnemyDeathFactV1 fact);
    }

    public interface IEnemyKillStatFactConsumerV1
    {
        void Consume(EnemyDeathFactV1 fact);
    }

    public interface IEnemyTerminalCollisionAdapterV1
    {
        void SetTerminal(EnemyTerminalCollisionFactV1 fact);
    }

    public sealed class EnemyRuntimeDownstreamPortsV1
    {
        public EnemyRuntimeDownstreamPortsV1(
            IEnemyAttackEffectPortV1 attackEffects,
            IEnemyPlayerDamagePortV1 playerDamage,
            IEnemyRoomTerminalPortV1 roomTerminal,
            IEnemyExperienceFactConsumerV1 experience,
            IEnemyDropFactConsumerV1 drops,
            IEnemyKillStatFactConsumerV1 killStats,
            IEnemyTerminalCollisionAdapterV1 terminalCollision)
        {
            AttackEffects = attackEffects ?? throw new ArgumentNullException(nameof(attackEffects));
            PlayerDamage = playerDamage ?? throw new ArgumentNullException(nameof(playerDamage));
            RoomTerminal = roomTerminal ?? throw new ArgumentNullException(nameof(roomTerminal));
            Experience = experience ?? throw new ArgumentNullException(nameof(experience));
            Drops = drops ?? throw new ArgumentNullException(nameof(drops));
            KillStats = killStats ?? throw new ArgumentNullException(nameof(killStats));
            TerminalCollision = terminalCollision ?? throw new ArgumentNullException(nameof(terminalCollision));
        }

        public IEnemyAttackEffectPortV1 AttackEffects { get; }
        public IEnemyPlayerDamagePortV1 PlayerDamage { get; }
        public IEnemyRoomTerminalPortV1 RoomTerminal { get; }
        public IEnemyExperienceFactConsumerV1 Experience { get; }
        public IEnemyDropFactConsumerV1 Drops { get; }
        public IEnemyKillStatFactConsumerV1 KillStats { get; }
        public IEnemyTerminalCollisionAdapterV1 TerminalCollision { get; }

        public static EnemyRuntimeDownstreamPortsV1 None()
        {
            var sink = new NoOpEnemyRuntimePortV1();
            return new EnemyRuntimeDownstreamPortsV1(sink, sink, sink, sink, sink, sink, sink);
        }
    }

    internal sealed class NoOpEnemyRuntimePortV1 :
        IEnemyAttackEffectPortV1,
        IEnemyPlayerDamagePortV1,
        IEnemyRoomTerminalPortV1,
        IEnemyExperienceFactConsumerV1,
        IEnemyDropFactConsumerV1,
        IEnemyKillStatFactConsumerV1,
        IEnemyTerminalCollisionAdapterV1
    {
        public void Emit(EnemyAttackExecutionRequestV1 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
        }

        public EnemyPlayerDamagePortResultV1 Route(EnemyPlayerDamageRequestV1 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return new EnemyPlayerDamagePortResultV1(
                EnemyRuntimeOperationStatusV1.NoEffect,
                EnemyRuntimeRejectionCodeV1.None);
        }

        public void Report(ReportRoomOccupantTerminalCommandV1 command, EnemyDeathFactV1 deathFact)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (deathFact == null) throw new ArgumentNullException(nameof(deathFact));
        }

        public void Consume(EnemyDeathFactV1 fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
        }

        public void SetTerminal(EnemyTerminalCollisionFactV1 fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
        }
    }

    public sealed class EnemyRuntimeDamageCommandV1
    {
        public EnemyRuntimeDamageCommandV1(
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

    public sealed class EnemyRuntimeDamageResultV1
    {
        public EnemyRuntimeDamageResultV1(
            EnemyRuntimeOperationStatusV1 status,
            EnemyRuntimeRejectionCodeV1 rejection,
            EnemyRuntimeProjection runtime,
            EnemyDeathFactV1 deathFact)
        {
            Status = status;
            Rejection = rejection;
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            DeathFact = deathFact;
        }

        public EnemyRuntimeOperationStatusV1 Status { get; }
        public EnemyRuntimeRejectionCodeV1 Rejection { get; }
        public EnemyRuntimeProjection Runtime { get; }
        public EnemyDeathFactV1 DeathFact { get; }
    }
}
