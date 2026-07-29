using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyRuntimeComposition
{
    public enum EnemyAimCommitmentMode
    {
        LockedDirectionAndPoint = 1,
        LockedDirection = 2,
    }

    public enum EnemyAttackExecutionKind
    {
        Projectile = 1,
        Area = 2,
        Contact = 3,
        Pounce = 4,
    }

    public sealed class EnemyMovementPolicyConfiguration
    {
        public EnemyMovementPolicyConfiguration(
            StableId policyId,
            double maximumSpeed,
            double acceleration,
            double turnRateDegreesPerSecond,
            bool usesPlanarCollision)
        {
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
            RequireFiniteNonNegative(maximumSpeed, nameof(maximumSpeed));
            RequireFiniteNonNegative(acceleration, nameof(acceleration));
            RequireFiniteNonNegative(turnRateDegreesPerSecond, nameof(turnRateDegreesPerSecond));
            MaximumSpeed = maximumSpeed;
            Acceleration = acceleration;
            TurnRateDegreesPerSecond = turnRateDegreesPerSecond;
            UsesPlanarCollision = usesPlanarCollision;
        }

        public StableId PolicyId { get; }
        public double MaximumSpeed { get; }
        public double Acceleration { get; }
        public double TurnRateDegreesPerSecond { get; }
        public bool UsesPlanarCollision { get; }

        private static void RequireFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class EnemyDecisionPolicyConfiguration
    {
        public EnemyDecisionPolicyConfiguration(
            StableId policyId,
            StableId readyPhaseId,
            bool usesIndependentMovementBand,
            double preferredMovementDistance,
            double movementTolerance)
        {
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
            ReadyPhaseId = readyPhaseId ?? throw new ArgumentNullException(nameof(readyPhaseId));
            if (double.IsNaN(preferredMovementDistance)
                || double.IsInfinity(preferredMovementDistance)
                || preferredMovementDistance < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(preferredMovementDistance));
            }
            if (double.IsNaN(movementTolerance)
                || double.IsInfinity(movementTolerance)
                || movementTolerance < 0d
                || movementTolerance > preferredMovementDistance)
            {
                throw new ArgumentOutOfRangeException(nameof(movementTolerance));
            }

            UsesIndependentMovementBand = usesIndependentMovementBand;
            PreferredMovementDistance = preferredMovementDistance;
            MovementTolerance = movementTolerance;
        }

        public StableId PolicyId { get; }
        public StableId ReadyPhaseId { get; }
        public bool UsesIndependentMovementBand { get; }
        public double PreferredMovementDistance { get; }
        public double MovementTolerance { get; }
    }

    public sealed class EnemyTargetingAimPolicyConfiguration
    {
        public EnemyTargetingAimPolicyConfiguration(
            StableId policyId,
            EnemyAimCommitmentMode commitmentMode,
            double predictionHorizonSeconds,
            double maximumPredictionDistance)
        {
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
            if (!Enum.IsDefined(typeof(EnemyAimCommitmentMode), commitmentMode))
            {
                throw new ArgumentOutOfRangeException(nameof(commitmentMode));
            }
            if (double.IsNaN(predictionHorizonSeconds)
                || double.IsInfinity(predictionHorizonSeconds)
                || predictionHorizonSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(predictionHorizonSeconds));
            }
            if (double.IsNaN(maximumPredictionDistance)
                || double.IsInfinity(maximumPredictionDistance)
                || maximumPredictionDistance < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumPredictionDistance));
            }

            CommitmentMode = commitmentMode;
            PredictionHorizonSeconds = predictionHorizonSeconds;
            MaximumPredictionDistance = maximumPredictionDistance;
        }

        public StableId PolicyId { get; }
        public EnemyAimCommitmentMode CommitmentMode { get; }
        public double PredictionHorizonSeconds { get; }
        public double MaximumPredictionDistance { get; }
    }

    public sealed class EnemyAttackCapabilityConfiguration
    {
        public EnemyAttackCapabilityConfiguration(
            StableId capabilityId,
            StableId targetingAimPolicyId,
            EnemyAttackExecutionKind executionKind)
        {
            CapabilityId = capabilityId ?? throw new ArgumentNullException(nameof(capabilityId));
            TargetingAimPolicyId = targetingAimPolicyId
                ?? throw new ArgumentNullException(nameof(targetingAimPolicyId));
            if (!Enum.IsDefined(typeof(EnemyAttackExecutionKind), executionKind))
            {
                throw new ArgumentOutOfRangeException(nameof(executionKind));
            }
            ExecutionKind = executionKind;
        }

        public StableId CapabilityId { get; }
        public StableId TargetingAimPolicyId { get; }
        public EnemyAttackExecutionKind ExecutionKind { get; }
    }

    public sealed class EnemyMovementPolicyIntent
    {
        public EnemyMovementPolicyIntent(
            EnemyVector2 desiredDirection,
            EnemyVector2 desiredFacing,
            EnemyMovementIntentKind kind,
            StableId reasonCode)
        {
            if (!Enum.IsDefined(typeof(EnemyMovementIntentKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            DesiredDirection = desiredDirection;
            DesiredFacing = desiredFacing;
            Kind = kind;
            ReasonCode = reasonCode;
        }

        public EnemyVector2 DesiredDirection { get; }
        public EnemyVector2 DesiredFacing { get; }
        public EnemyMovementIntentKind Kind { get; }
        public StableId ReasonCode { get; }
    }

    public interface IEnemyMovementEnvironmentQuery
    {
        bool TryResolveDirection(
            StableId entityInstanceId,
            StableId roomStableId,
            EnemyVector2 origin,
            EnemyVector2 desiredDirection,
            double lookAheadDistance,
            out EnemyVector2 resolvedDirection);
    }

    public sealed class EnemyMovementRealizationContext
    {
        public EnemyMovementRealizationContext(
            StableId entityInstanceId,
            StableId roomStableId,
            EnemyVector2 currentPosition,
            EnemyVector2 currentFacing,
            long simulationTick,
            double speedScalar,
            IEnemyMovementEnvironmentQuery environmentQuery)
        {
            EntityInstanceId = entityInstanceId
                ?? throw new ArgumentNullException(nameof(entityInstanceId));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (simulationTick < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationTick));
            }
            if (double.IsNaN(speedScalar)
                || double.IsInfinity(speedScalar)
                || speedScalar <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(speedScalar));
            }

            CurrentPosition = currentPosition;
            CurrentFacing = currentFacing.Normalized;
            SimulationTick = simulationTick;
            SpeedScalar = speedScalar;
            EnvironmentQuery = environmentQuery;
        }

        public StableId EntityInstanceId { get; }
        public StableId RoomStableId { get; }
        public EnemyVector2 CurrentPosition { get; }
        public EnemyVector2 CurrentFacing { get; }
        public long SimulationTick { get; }
        public double SpeedScalar { get; }
        public IEnemyMovementEnvironmentQuery EnvironmentQuery { get; }
    }

    public sealed class EnemyTargetingAimContext
    {
        public EnemyTargetingAimContext(
            EnemyPerceptionSnapshot perception,
            double difficultyScalar)
        {
            Perception = perception ?? throw new ArgumentNullException(nameof(perception));
            if (double.IsNaN(difficultyScalar)
                || double.IsInfinity(difficultyScalar)
                || difficultyScalar <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(difficultyScalar));
            }

            DifficultyScalar = difficultyScalar;
        }

        public EnemyPerceptionSnapshot Perception { get; }
        public double DifficultyScalar { get; }
    }

    public sealed class EnemyMovementRealization
    {
        public EnemyMovementRealization(
            EnemyVector2 desiredVelocity,
            EnemyVector2 desiredFacing,
            EnemyMovementIntentKind kind,
            StableId policyId)
        {
            DesiredVelocity = desiredVelocity;
            DesiredFacing = desiredFacing;
            Kind = kind;
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
        }

        public EnemyVector2 DesiredVelocity { get; }
        public EnemyVector2 DesiredFacing { get; }
        public EnemyMovementIntentKind Kind { get; }
        public StableId PolicyId { get; }
    }

    public sealed class EnemyAttackExecutionRequest
    {
        public EnemyAttackExecutionRequest(
            StableId operationStableId,
            EnemyLiveIdentity identity,
            long lifecycleGeneration,
            double occurredAtSeconds,
            EnemyAttackCapabilityDescriptor descriptor,
            EnemyAttackIntent committedIntent,
            StableId itemInstanceStableId,
            EnemyAttackExecutionKind executionKind,
            double resolvedDamage,
            double resolvedCooldownSeconds)
        {
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            if (lifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            if (double.IsNaN(occurredAtSeconds)
                || double.IsInfinity(occurredAtSeconds)
                || occurredAtSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(occurredAtSeconds));
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            CommittedIntent = committedIntent ?? throw new ArgumentNullException(nameof(committedIntent));
            if (committedIntent.AttackerEntityId != identity.EntityInstanceId)
                throw new ArgumentException("Committed attack source must match the runtime identity.");
            if (double.IsNaN(resolvedDamage)
                || double.IsInfinity(resolvedDamage)
                || resolvedDamage <= 0d)
                throw new ArgumentOutOfRangeException(nameof(resolvedDamage));
            if (double.IsNaN(resolvedCooldownSeconds)
                || double.IsInfinity(resolvedCooldownSeconds)
                || resolvedCooldownSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(resolvedCooldownSeconds));
            ItemInstanceStableId = itemInstanceStableId;
            ExecutionKind = executionKind;
            LifecycleGeneration = lifecycleGeneration;
            OccurredAtSeconds = occurredAtSeconds;
            ResolvedDamage = resolvedDamage;
            ResolvedCooldownSeconds = resolvedCooldownSeconds;
        }

        public StableId OperationStableId { get; }
        public EnemyLiveIdentity Identity { get; }
        public long LifecycleGeneration { get; }
        public double OccurredAtSeconds { get; }
        public EnemyAttackCapabilityDescriptor Descriptor { get; }
        public EnemyAttackIntent CommittedIntent { get; }
        public StableId ItemInstanceStableId { get; }
        public EnemyAttackExecutionKind ExecutionKind { get; }
        public double ResolvedDamage { get; }
        public double ResolvedCooldownSeconds { get; }
    }

    public interface IEnemyDecisionLivePolicy
    {
        EnemyDecisionEvaluation Evaluate(
            EnemyLiveView runtime,
            EnemyDefinition definition,
            EnemyDecisionPolicyConfiguration configuration,
            EnemyPerceptionSnapshot perception);
    }

    public interface IEnemyMovementLivePolicy
    {
        EnemyMovementPolicyIntent BuildIntent(
            EnemyDecisionEvaluation evaluation,
            EnemyMovementPolicyConfiguration configuration);
    }

    public interface IEnemyMovementIntentRealizer
    {
        EnemyMovementRealization Realize(
            EnemyMovementPolicyIntent intent,
            EnemyMovementRealizationContext context,
            EnemyMovementPolicyConfiguration configuration);
    }

    public interface IEnemyTargetingAimPolicy
    {
        EnemyAttackIntent Commit(
            EnemyAttackIntent requestedIntent,
            EnemyTargetingAimContext context,
            EnemyTargetingAimPolicyConfiguration configuration);
    }

    public interface IEnemyAttackCapabilityBridge
    {
        EnemyAttackExecutionRequest BuildExecution(
            EnemyAttackCapabilityDescriptor descriptor,
            EnemyAttackIntent committedIntent,
            StableId itemInstanceStableId,
            EnemyAttackCapabilityConfiguration configuration,
            EnemyAttackExecutionContext context);
    }

    public sealed class EnemyMovementPolicyRegistration
    {
        public EnemyMovementPolicyRegistration(
            EnemyMovementPolicyConfiguration configuration,
            IEnemyMovementLivePolicy policy,
            IEnemyMovementIntentRealizer realizer)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Realizer = realizer ?? throw new ArgumentNullException(nameof(realizer));
        }

        public EnemyMovementPolicyConfiguration Configuration { get; }
        public IEnemyMovementLivePolicy Policy { get; }
        public IEnemyMovementIntentRealizer Realizer { get; }
    }

    public sealed class EnemyDecisionPolicyRegistration
    {
        public EnemyDecisionPolicyRegistration(
            EnemyDecisionPolicyConfiguration configuration,
            IEnemyDecisionLivePolicy policy)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public EnemyDecisionPolicyConfiguration Configuration { get; }
        public IEnemyDecisionLivePolicy Policy { get; }
    }

    public sealed class EnemyTargetingAimPolicyRegistration
    {
        public EnemyTargetingAimPolicyRegistration(
            EnemyTargetingAimPolicyConfiguration configuration,
            IEnemyTargetingAimPolicy policy)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public EnemyTargetingAimPolicyConfiguration Configuration { get; }
        public IEnemyTargetingAimPolicy Policy { get; }
    }

    public sealed class EnemyAttackCapabilityLiveRegistration
    {
        public EnemyAttackCapabilityLiveRegistration(
            EnemyAttackCapabilityConfiguration configuration,
            IEnemyAttackCapabilityBridge adapter)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        public EnemyAttackCapabilityConfiguration Configuration { get; }
        public IEnemyAttackCapabilityBridge Adapter { get; }
    }

    public sealed class EnemyRules
    {
        private readonly Dictionary<StableId, EnemyMovementPolicyRegistration> movementPolicies;
        private readonly Dictionary<StableId, EnemyDecisionPolicyRegistration> decisionPolicies;
        private readonly Dictionary<StableId, EnemyTargetingAimPolicyRegistration> targetingAimPolicies;
        private readonly Dictionary<StableId, EnemyAttackCapabilityLiveRegistration> attackCapabilities;

        public EnemyRules(
            IEnumerable<EnemyMovementPolicyRegistration> movementPolicies,
            IEnumerable<EnemyDecisionPolicyRegistration> decisionPolicies,
            IEnumerable<EnemyTargetingAimPolicyRegistration> targetingAimPolicies,
            IEnumerable<EnemyAttackCapabilityLiveRegistration> attackCapabilities)
        {
            this.movementPolicies = Copy(
                movementPolicies,
                item => item.Configuration.PolicyId,
                nameof(movementPolicies));
            this.decisionPolicies = Copy(
                decisionPolicies,
                item => item.Configuration.PolicyId,
                nameof(decisionPolicies));
            this.targetingAimPolicies = Copy(
                targetingAimPolicies,
                item => item.Configuration.PolicyId,
                nameof(targetingAimPolicies));
            this.attackCapabilities = Copy(
                attackCapabilities,
                item => item.Configuration.CapabilityId,
                nameof(attackCapabilities));
        }

        public bool TryResolveMovement(
            StableId policyId,
            out EnemyMovementPolicyRegistration registration)
        {
            registration = null;
            return policyId != null && movementPolicies.TryGetValue(policyId, out registration);
        }

        public bool TryResolveDecision(
            StableId policyId,
            out EnemyDecisionPolicyRegistration registration)
        {
            registration = null;
            return policyId != null && decisionPolicies.TryGetValue(policyId, out registration);
        }

        public bool TryResolveTargetingAim(
            StableId policyId,
            out EnemyTargetingAimPolicyRegistration registration)
        {
            registration = null;
            return policyId != null && targetingAimPolicies.TryGetValue(policyId, out registration);
        }

        public bool TryResolveAttackCapability(
            StableId capabilityId,
            out EnemyAttackCapabilityLiveRegistration registration)
        {
            registration = null;
            return capabilityId != null && attackCapabilities.TryGetValue(capabilityId, out registration);
        }

        private static Dictionary<StableId, T> Copy<T>(
            IEnumerable<T> source,
            Func<T, StableId> keySelector,
            string parameterName)
            where T : class
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            var result = new Dictionary<StableId, T>();
            foreach (T item in source)
            {
                if (item == null)
                {
                    throw new ArgumentException("Policy registries cannot contain null entries.", parameterName);
                }
                StableId key = keySelector(item);
                if (key == null || result.ContainsKey(key))
                {
                    throw new ArgumentException("Policy registration is missing or duplicated: " + key, parameterName);
                }
                result.Add(key, item);
            }
            return result;
        }
    }

    public sealed class FoundationEnemyDecisionLivePolicy : IEnemyDecisionLivePolicy
    {
        public EnemyDecisionEvaluation Evaluate(
            EnemyLiveView runtime,
            EnemyDefinition definition,
            EnemyDecisionPolicyConfiguration configuration,
            EnemyPerceptionSnapshot perception)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (perception == null) throw new ArgumentNullException(nameof(perception));

            EnemyAttackCapabilityDescriptor attack = SelectAttack(definition.Attacks);
            EnemyDecisionProfile profile = configuration.UsesIndependentMovementBand
                ? new EnemyDecisionProfile(
                    definition.DetectionRadius,
                    attack.MinimumAttackRange,
                    attack.PreferredAttackRange,
                    attack.MaximumAttackRange,
                    attack.AttackArcDegrees,
                    attack.AttackId,
                    configuration.ReadyPhaseId,
                    configuration.PreferredMovementDistance,
                    configuration.MovementTolerance)
                : new EnemyDecisionProfile(
                    definition.DetectionRadius,
                    attack.MinimumAttackRange,
                    attack.PreferredAttackRange,
                    attack.MaximumAttackRange,
                    attack.AttackArcDegrees,
                    attack.AttackId,
                    configuration.ReadyPhaseId);
            return EnemyDecisionPolicy.Evaluate(runtime, profile, perception);
        }

        private static EnemyAttackCapabilityDescriptor SelectAttack(
            IReadOnlyList<EnemyAttackCapabilityDescriptor> attacks)
        {
            if (attacks == null || attacks.Count == 0)
            {
                throw new InvalidOperationException("The registered decision policy requires an attack descriptor.");
            }

            EnemyAttackCapabilityDescriptor selected = attacks[0];
            for (int index = 1; index < attacks.Count; index++)
            {
                EnemyAttackCapabilityDescriptor candidate = attacks[index];
                if (candidate.SelectionPriority > selected.SelectionPriority
                    || (candidate.SelectionPriority == selected.SelectionPriority
                        && candidate.AttackId.CompareTo(selected.AttackId) < 0))
                {
                    selected = candidate;
                }
            }
            return selected;
        }
    }


    public sealed class RangeAwareEnemyDecisionLivePolicy : IEnemyDecisionLivePolicy
    {
        public EnemyDecisionEvaluation Evaluate(
            EnemyLiveView runtime,
            EnemyDefinition definition,
            EnemyDecisionPolicyConfiguration configuration,
            EnemyPerceptionSnapshot perception)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (perception == null) throw new ArgumentNullException(nameof(perception));

            EnemyPerceivedTarget target = SelectTarget(perception, definition.DetectionRadius);
            EnemyAttackCapabilityDescriptor attack = SelectAttack(definition.Attacks, target);
            EnemyDecisionProfile profile = configuration.UsesIndependentMovementBand
                ? new EnemyDecisionProfile(
                    definition.DetectionRadius,
                    attack.MinimumAttackRange,
                    attack.PreferredAttackRange,
                    attack.MaximumAttackRange,
                    attack.AttackArcDegrees,
                    attack.AttackId,
                    configuration.ReadyPhaseId,
                    configuration.PreferredMovementDistance,
                    configuration.MovementTolerance)
                : new EnemyDecisionProfile(
                    definition.DetectionRadius,
                    attack.MinimumAttackRange,
                    attack.PreferredAttackRange,
                    attack.MaximumAttackRange,
                    attack.AttackArcDegrees,
                    attack.AttackId,
                    configuration.ReadyPhaseId);
            return EnemyDecisionPolicy.Evaluate(runtime, profile, perception);
        }

        private static EnemyPerceivedTarget SelectTarget(
            EnemyPerceptionSnapshot perception,
            double detectionRadius)
        {
            EnemyPerceivedTarget selected = null;
            for (int index = 0; index < perception.Targets.Count; index++)
            {
                EnemyPerceivedTarget candidate = perception.Targets[index];
                if (candidate.Relationship != EnemyTargetRelationship.Hostile
                    || !candidate.IsWithinDetectionRange
                    || candidate.Distance > detectionRadius)
                {
                    continue;
                }
                if (selected == null
                    || candidate.Distance < selected.Distance
                    || (candidate.Distance == selected.Distance
                        && candidate.EntityId.CompareTo(selected.EntityId) < 0))
                {
                    selected = candidate;
                }
            }
            return selected;
        }

        private static EnemyAttackCapabilityDescriptor SelectAttack(
            IReadOnlyList<EnemyAttackCapabilityDescriptor> attacks,
            EnemyPerceivedTarget target)
        {
            if (attacks == null || attacks.Count == 0)
            {
                throw new InvalidOperationException("The registered decision policy requires an attack descriptor.");
            }

            double distance = target == null ? 0d : target.Distance;
            EnemyAttackCapabilityDescriptor selected = attacks[0];
            double selectedPenalty = RangePenalty(selected, distance);
            for (int index = 1; index < attacks.Count; index++)
            {
                EnemyAttackCapabilityDescriptor candidate = attacks[index];
                double candidatePenalty = RangePenalty(candidate, distance);
                if (candidatePenalty < selectedPenalty
                    || (candidatePenalty == selectedPenalty
                        && candidate.SelectionPriority > selected.SelectionPriority)
                    || (candidatePenalty == selectedPenalty
                        && candidate.SelectionPriority == selected.SelectionPriority
                        && candidate.AttackId.CompareTo(selected.AttackId) < 0))
                {
                    selected = candidate;
                    selectedPenalty = candidatePenalty;
                }
            }
            return selected;
        }

        private static double RangePenalty(
            EnemyAttackCapabilityDescriptor attack,
            double distance)
        {
            if (distance < attack.MinimumAttackRange)
            {
                return attack.MinimumAttackRange - distance;
            }
            if (distance > attack.MaximumAttackRange)
            {
                return distance - attack.MaximumAttackRange;
            }
            return 0d;
        }
    }

    public sealed class DecisionMovementLivePolicy : IEnemyMovementLivePolicy
    {
        public EnemyMovementPolicyIntent BuildIntent(
            EnemyDecisionEvaluation evaluation,
            EnemyMovementPolicyConfiguration configuration)
        {
            if (evaluation == null) throw new ArgumentNullException(nameof(evaluation));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            EnemyDecisionSnapshot snapshot = evaluation.Decision;
            return new EnemyMovementPolicyIntent(
                snapshot.DesiredMovement,
                snapshot.DesiredFacing,
                snapshot.MovementKind,
                snapshot.ReasonCode);
        }
    }

    public sealed class DirectEnemyMovementIntentRealizer : IEnemyMovementIntentRealizer
    {
        public EnemyMovementRealization Realize(
            EnemyMovementPolicyIntent intent,
            EnemyMovementRealizationContext context,
            EnemyMovementPolicyConfiguration configuration)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            EnemyVector2 direction = intent.DesiredDirection.Normalized;
            EnemyVector2 velocity = new EnemyVector2(
                direction.X * configuration.MaximumSpeed * context.SpeedScalar,
                direction.Y * configuration.MaximumSpeed * context.SpeedScalar);
            return new EnemyMovementRealization(
                velocity,
                intent.DesiredFacing.Normalized,
                intent.Kind,
                configuration.PolicyId);
        }
    }

    public sealed class LockedEnemyTargetingAimPolicy : IEnemyTargetingAimPolicy
    {
        public EnemyAttackIntent Commit(
            EnemyAttackIntent requestedIntent,
            EnemyTargetingAimContext context,
            EnemyTargetingAimPolicyConfiguration configuration)
        {
            if (requestedIntent == null) throw new ArgumentNullException(nameof(requestedIntent));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            return requestedIntent;
        }
    }

    public sealed class RequestEnemyAttackCapabilityBridge : IEnemyAttackCapabilityBridge
    {
        public EnemyAttackExecutionRequest BuildExecution(
            EnemyAttackCapabilityDescriptor descriptor,
            EnemyAttackIntent committedIntent,
            StableId itemInstanceStableId,
            EnemyAttackCapabilityConfiguration configuration,
            EnemyAttackExecutionContext context)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (context == null) throw new ArgumentNullException(nameof(context));
            return new EnemyAttackExecutionRequest(
                context.OperationStableId,
                context.Identity,
                context.LifecycleGeneration,
                context.OccurredAtSeconds,
                descriptor,
                committedIntent,
                itemInstanceStableId,
                configuration.ExecutionKind,
                descriptor.Damage * context.DifficultyScaling.DamageMultiplier,
                descriptor.CooldownSeconds * context.DifficultyScaling.CooldownMultiplier);
        }
    }
}
