using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyRuntimeComposition
{
    public enum EnemyAimCommitmentModeV1
    {
        LockedDirectionAndPoint = 1,
        LockedDirection = 2,
    }

    public enum EnemyAttackExecutionKindV1
    {
        Projectile = 1,
        Area = 2,
        Contact = 3,
        Pounce = 4,
    }

    public sealed class EnemyMovementPolicyConfigurationV1
    {
        public EnemyMovementPolicyConfigurationV1(
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

    public sealed class EnemyDecisionPolicyConfigurationV1
    {
        public EnemyDecisionPolicyConfigurationV1(
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

    public sealed class EnemyTargetingAimPolicyConfigurationV1
    {
        public EnemyTargetingAimPolicyConfigurationV1(
            StableId policyId,
            EnemyAimCommitmentModeV1 commitmentMode,
            double predictionHorizonSeconds,
            double maximumPredictionDistance)
        {
            PolicyId = policyId ?? throw new ArgumentNullException(nameof(policyId));
            if (!Enum.IsDefined(typeof(EnemyAimCommitmentModeV1), commitmentMode))
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
        public EnemyAimCommitmentModeV1 CommitmentMode { get; }
        public double PredictionHorizonSeconds { get; }
        public double MaximumPredictionDistance { get; }
    }

    public sealed class EnemyAttackCapabilityConfigurationV1
    {
        public EnemyAttackCapabilityConfigurationV1(
            StableId capabilityId,
            StableId targetingAimPolicyId,
            EnemyAttackExecutionKindV1 executionKind)
        {
            CapabilityId = capabilityId ?? throw new ArgumentNullException(nameof(capabilityId));
            TargetingAimPolicyId = targetingAimPolicyId
                ?? throw new ArgumentNullException(nameof(targetingAimPolicyId));
            if (!Enum.IsDefined(typeof(EnemyAttackExecutionKindV1), executionKind))
            {
                throw new ArgumentOutOfRangeException(nameof(executionKind));
            }
            ExecutionKind = executionKind;
        }

        public StableId CapabilityId { get; }
        public StableId TargetingAimPolicyId { get; }
        public EnemyAttackExecutionKindV1 ExecutionKind { get; }
    }

    public sealed class EnemyMovementPolicyIntentV1
    {
        public EnemyMovementPolicyIntentV1(
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

    public interface IEnemyMovementEnvironmentQueryV1
    {
        bool TryResolveDirection(
            StableId entityInstanceId,
            StableId roomStableId,
            EnemyVector2 origin,
            EnemyVector2 desiredDirection,
            double lookAheadDistance,
            out EnemyVector2 resolvedDirection);
    }

    public sealed class EnemyMovementRealizationContextV1
    {
        public EnemyMovementRealizationContextV1(
            StableId entityInstanceId,
            StableId roomStableId,
            EnemyVector2 currentPosition,
            EnemyVector2 currentFacing,
            long simulationTick,
            double speedScalar,
            IEnemyMovementEnvironmentQueryV1 environmentQuery)
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
        public IEnemyMovementEnvironmentQueryV1 EnvironmentQuery { get; }
    }

    public sealed class EnemyTargetingAimContextV1
    {
        public EnemyTargetingAimContextV1(
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

    public sealed class EnemyMovementRealizationV1
    {
        public EnemyMovementRealizationV1(
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

    public sealed class EnemyAttackExecutionRequestV1
    {
        public EnemyAttackExecutionRequestV1(
            StableId operationStableId,
            EnemyRuntimeIdentityV1 identity,
            long lifecycleGeneration,
            double occurredAtSeconds,
            EnemyAttackCapabilityDescriptorV1 descriptor,
            EnemyAttackIntent committedIntent,
            StableId itemInstanceStableId,
            EnemyAttackExecutionKindV1 executionKind,
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
        public EnemyRuntimeIdentityV1 Identity { get; }
        public long LifecycleGeneration { get; }
        public double OccurredAtSeconds { get; }
        public EnemyAttackCapabilityDescriptorV1 Descriptor { get; }
        public EnemyAttackIntent CommittedIntent { get; }
        public StableId ItemInstanceStableId { get; }
        public EnemyAttackExecutionKindV1 ExecutionKind { get; }
        public double ResolvedDamage { get; }
        public double ResolvedCooldownSeconds { get; }
    }

    public interface IEnemyDecisionRuntimePolicyV1
    {
        EnemyDecisionEvaluation Evaluate(
            EnemyRuntimeProjection runtime,
            EnemyDefinitionV1 definition,
            EnemyDecisionPolicyConfigurationV1 configuration,
            EnemyPerceptionSnapshot perception);
    }

    public interface IEnemyMovementRuntimePolicyV1
    {
        EnemyMovementPolicyIntentV1 BuildIntent(
            EnemyDecisionEvaluation evaluation,
            EnemyMovementPolicyConfigurationV1 configuration);
    }

    public interface IEnemyMovementIntentRealizerV1
    {
        EnemyMovementRealizationV1 Realize(
            EnemyMovementPolicyIntentV1 intent,
            EnemyMovementRealizationContextV1 context,
            EnemyMovementPolicyConfigurationV1 configuration);
    }

    public interface IEnemyTargetingAimPolicyV1
    {
        EnemyAttackIntent Commit(
            EnemyAttackIntent requestedIntent,
            EnemyTargetingAimContextV1 context,
            EnemyTargetingAimPolicyConfigurationV1 configuration);
    }

    public interface IEnemyAttackCapabilityAdapterV1
    {
        EnemyAttackExecutionRequestV1 BuildExecution(
            EnemyAttackCapabilityDescriptorV1 descriptor,
            EnemyAttackIntent committedIntent,
            StableId itemInstanceStableId,
            EnemyAttackCapabilityConfigurationV1 configuration,
            EnemyAttackExecutionContextV1 context);
    }

    public sealed class EnemyMovementPolicyRegistrationV1
    {
        public EnemyMovementPolicyRegistrationV1(
            EnemyMovementPolicyConfigurationV1 configuration,
            IEnemyMovementRuntimePolicyV1 policy,
            IEnemyMovementIntentRealizerV1 realizer)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Realizer = realizer ?? throw new ArgumentNullException(nameof(realizer));
        }

        public EnemyMovementPolicyConfigurationV1 Configuration { get; }
        public IEnemyMovementRuntimePolicyV1 Policy { get; }
        public IEnemyMovementIntentRealizerV1 Realizer { get; }
    }

    public sealed class EnemyDecisionPolicyRegistrationV1
    {
        public EnemyDecisionPolicyRegistrationV1(
            EnemyDecisionPolicyConfigurationV1 configuration,
            IEnemyDecisionRuntimePolicyV1 policy)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public EnemyDecisionPolicyConfigurationV1 Configuration { get; }
        public IEnemyDecisionRuntimePolicyV1 Policy { get; }
    }

    public sealed class EnemyTargetingAimPolicyRegistrationV1
    {
        public EnemyTargetingAimPolicyRegistrationV1(
            EnemyTargetingAimPolicyConfigurationV1 configuration,
            IEnemyTargetingAimPolicyV1 policy)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public EnemyTargetingAimPolicyConfigurationV1 Configuration { get; }
        public IEnemyTargetingAimPolicyV1 Policy { get; }
    }

    public sealed class EnemyAttackCapabilityRuntimeRegistrationV1
    {
        public EnemyAttackCapabilityRuntimeRegistrationV1(
            EnemyAttackCapabilityConfigurationV1 configuration,
            IEnemyAttackCapabilityAdapterV1 adapter)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        public EnemyAttackCapabilityConfigurationV1 Configuration { get; }
        public IEnemyAttackCapabilityAdapterV1 Adapter { get; }
    }

    public sealed class EnemyRuntimePolicyRegistryV1
    {
        private readonly Dictionary<StableId, EnemyMovementPolicyRegistrationV1> movementPolicies;
        private readonly Dictionary<StableId, EnemyDecisionPolicyRegistrationV1> decisionPolicies;
        private readonly Dictionary<StableId, EnemyTargetingAimPolicyRegistrationV1> targetingAimPolicies;
        private readonly Dictionary<StableId, EnemyAttackCapabilityRuntimeRegistrationV1> attackCapabilities;

        public EnemyRuntimePolicyRegistryV1(
            IEnumerable<EnemyMovementPolicyRegistrationV1> movementPolicies,
            IEnumerable<EnemyDecisionPolicyRegistrationV1> decisionPolicies,
            IEnumerable<EnemyTargetingAimPolicyRegistrationV1> targetingAimPolicies,
            IEnumerable<EnemyAttackCapabilityRuntimeRegistrationV1> attackCapabilities)
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
            out EnemyMovementPolicyRegistrationV1 registration)
        {
            registration = null;
            return policyId != null && movementPolicies.TryGetValue(policyId, out registration);
        }

        public bool TryResolveDecision(
            StableId policyId,
            out EnemyDecisionPolicyRegistrationV1 registration)
        {
            registration = null;
            return policyId != null && decisionPolicies.TryGetValue(policyId, out registration);
        }

        public bool TryResolveTargetingAim(
            StableId policyId,
            out EnemyTargetingAimPolicyRegistrationV1 registration)
        {
            registration = null;
            return policyId != null && targetingAimPolicies.TryGetValue(policyId, out registration);
        }

        public bool TryResolveAttackCapability(
            StableId capabilityId,
            out EnemyAttackCapabilityRuntimeRegistrationV1 registration)
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

    public sealed class FoundationEnemyDecisionRuntimePolicyV1 : IEnemyDecisionRuntimePolicyV1
    {
        public EnemyDecisionEvaluation Evaluate(
            EnemyRuntimeProjection runtime,
            EnemyDefinitionV1 definition,
            EnemyDecisionPolicyConfigurationV1 configuration,
            EnemyPerceptionSnapshot perception)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (perception == null) throw new ArgumentNullException(nameof(perception));

            EnemyAttackCapabilityDescriptorV1 attack = SelectAttack(definition.Attacks);
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

        private static EnemyAttackCapabilityDescriptorV1 SelectAttack(
            IReadOnlyList<EnemyAttackCapabilityDescriptorV1> attacks)
        {
            if (attacks == null || attacks.Count == 0)
            {
                throw new InvalidOperationException("The registered decision policy requires an attack descriptor.");
            }

            EnemyAttackCapabilityDescriptorV1 selected = attacks[0];
            for (int index = 1; index < attacks.Count; index++)
            {
                EnemyAttackCapabilityDescriptorV1 candidate = attacks[index];
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


    public sealed class RangeAwareEnemyDecisionRuntimePolicyV1 : IEnemyDecisionRuntimePolicyV1
    {
        public EnemyDecisionEvaluation Evaluate(
            EnemyRuntimeProjection runtime,
            EnemyDefinitionV1 definition,
            EnemyDecisionPolicyConfigurationV1 configuration,
            EnemyPerceptionSnapshot perception)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (perception == null) throw new ArgumentNullException(nameof(perception));

            EnemyPerceivedTarget target = SelectTarget(perception, definition.DetectionRadius);
            EnemyAttackCapabilityDescriptorV1 attack = SelectAttack(definition.Attacks, target);
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

        private static EnemyAttackCapabilityDescriptorV1 SelectAttack(
            IReadOnlyList<EnemyAttackCapabilityDescriptorV1> attacks,
            EnemyPerceivedTarget target)
        {
            if (attacks == null || attacks.Count == 0)
            {
                throw new InvalidOperationException("The registered decision policy requires an attack descriptor.");
            }

            double distance = target == null ? 0d : target.Distance;
            EnemyAttackCapabilityDescriptorV1 selected = attacks[0];
            double selectedPenalty = RangePenalty(selected, distance);
            for (int index = 1; index < attacks.Count; index++)
            {
                EnemyAttackCapabilityDescriptorV1 candidate = attacks[index];
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
            EnemyAttackCapabilityDescriptorV1 attack,
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

    public sealed class DecisionMovementRuntimePolicyV1 : IEnemyMovementRuntimePolicyV1
    {
        public EnemyMovementPolicyIntentV1 BuildIntent(
            EnemyDecisionEvaluation evaluation,
            EnemyMovementPolicyConfigurationV1 configuration)
        {
            if (evaluation == null) throw new ArgumentNullException(nameof(evaluation));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            EnemyDecisionSnapshot snapshot = evaluation.Decision;
            return new EnemyMovementPolicyIntentV1(
                snapshot.DesiredMovement,
                snapshot.DesiredFacing,
                snapshot.MovementKind,
                snapshot.ReasonCode);
        }
    }

    public sealed class DirectEnemyMovementIntentRealizerV1 : IEnemyMovementIntentRealizerV1
    {
        public EnemyMovementRealizationV1 Realize(
            EnemyMovementPolicyIntentV1 intent,
            EnemyMovementRealizationContextV1 context,
            EnemyMovementPolicyConfigurationV1 configuration)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            EnemyVector2 direction = intent.DesiredDirection.Normalized;
            EnemyVector2 velocity = new EnemyVector2(
                direction.X * configuration.MaximumSpeed * context.SpeedScalar,
                direction.Y * configuration.MaximumSpeed * context.SpeedScalar);
            return new EnemyMovementRealizationV1(
                velocity,
                intent.DesiredFacing.Normalized,
                intent.Kind,
                configuration.PolicyId);
        }
    }

    public sealed class LockedEnemyTargetingAimPolicyV1 : IEnemyTargetingAimPolicyV1
    {
        public EnemyAttackIntent Commit(
            EnemyAttackIntent requestedIntent,
            EnemyTargetingAimContextV1 context,
            EnemyTargetingAimPolicyConfigurationV1 configuration)
        {
            if (requestedIntent == null) throw new ArgumentNullException(nameof(requestedIntent));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            return requestedIntent;
        }
    }

    public sealed class RequestEnemyAttackCapabilityAdapterV1 : IEnemyAttackCapabilityAdapterV1
    {
        public EnemyAttackExecutionRequestV1 BuildExecution(
            EnemyAttackCapabilityDescriptorV1 descriptor,
            EnemyAttackIntent committedIntent,
            StableId itemInstanceStableId,
            EnemyAttackCapabilityConfigurationV1 configuration,
            EnemyAttackExecutionContextV1 context)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (context == null) throw new ArgumentNullException(nameof(context));
            return new EnemyAttackExecutionRequestV1(
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
