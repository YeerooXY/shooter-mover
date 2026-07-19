using System;
using System.Globalization;
using ShooterMover.Domain.Common;

namespace ShooterMover.GameplayEntities.Enemies
{
    public sealed class EnemyDecisionProfile
    {
        public EnemyDecisionProfile(
            double detectionRadius,
            double minimumAttackRange,
            double preferredAttackRange,
            double maximumAttackRange,
            double attackArcDegrees,
            StableId attackId,
            StableId readyPhaseId)
        {
            ValidateShared(
                detectionRadius,
                minimumAttackRange,
                preferredAttackRange,
                maximumAttackRange,
                attackArcDegrees);
            DetectionRadius = detectionRadius;
            MinimumAttackRange = minimumAttackRange;
            PreferredAttackRange = preferredAttackRange;
            MaximumAttackRange = maximumAttackRange;
            AttackArcDegrees = attackArcDegrees;
            AttackId = attackId ?? throw new ArgumentNullException(nameof(attackId));
            ReadyPhaseId = readyPhaseId ?? throw new ArgumentNullException(nameof(readyPhaseId));
            PreferredMovementDistance = preferredAttackRange;
            MovementTolerance = 0d;
            UsesIndependentMovementBand = false;
        }

        public EnemyDecisionProfile(
            double detectionRadius,
            double minimumAttackRange,
            double preferredAttackRange,
            double maximumAttackRange,
            double attackArcDegrees,
            StableId attackId,
            StableId readyPhaseId,
            double preferredMovementDistance,
            double movementTolerance)
        {
            ValidateShared(
                detectionRadius,
                minimumAttackRange,
                preferredAttackRange,
                maximumAttackRange,
                attackArcDegrees);
            RequireNonNegative(preferredMovementDistance, nameof(preferredMovementDistance));
            RequireNonNegative(movementTolerance, nameof(movementTolerance));
            if (movementTolerance > preferredMovementDistance)
                throw new ArgumentException("Movement tolerance cannot exceed preferred movement distance.");

            DetectionRadius = detectionRadius;
            MinimumAttackRange = minimumAttackRange;
            PreferredAttackRange = preferredAttackRange;
            MaximumAttackRange = maximumAttackRange;
            AttackArcDegrees = attackArcDegrees;
            AttackId = attackId ?? throw new ArgumentNullException(nameof(attackId));
            ReadyPhaseId = readyPhaseId ?? throw new ArgumentNullException(nameof(readyPhaseId));
            PreferredMovementDistance = preferredMovementDistance;
            MovementTolerance = movementTolerance;
            UsesIndependentMovementBand = true;
        }

        public double DetectionRadius { get; }
        public double MinimumAttackRange { get; }
        public double PreferredAttackRange { get; }
        public double MaximumAttackRange { get; }
        public double AttackArcDegrees { get; }
        public StableId AttackId { get; }
        public StableId ReadyPhaseId { get; }
        public double PreferredMovementDistance { get; }
        public double MovementTolerance { get; }
        public bool UsesIndependentMovementBand { get; }

        private static void ValidateShared(
            double detectionRadius,
            double minimumAttackRange,
            double preferredAttackRange,
            double maximumAttackRange,
            double attackArcDegrees)
        {
            RequireNonNegative(detectionRadius, nameof(detectionRadius));
            RequireNonNegative(minimumAttackRange, nameof(minimumAttackRange));
            RequireNonNegative(preferredAttackRange, nameof(preferredAttackRange));
            RequireNonNegative(maximumAttackRange, nameof(maximumAttackRange));
            if (minimumAttackRange > preferredAttackRange || preferredAttackRange > maximumAttackRange)
                throw new ArgumentException("Attack ranges must be ordered.");
            if (maximumAttackRange > detectionRadius)
                throw new ArgumentException("Attack range cannot exceed detection radius.");
            if (attackArcDegrees <= 0d
                || attackArcDegrees > 360d
                || double.IsNaN(attackArcDegrees)
                || double.IsInfinity(attackArcDegrees))
                throw new ArgumentOutOfRangeException(nameof(attackArcDegrees));
        }

        private static void RequireNonNegative(double value, string name)
        {
            if (value < 0d || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name);
        }
    }

    public sealed class EnemyDecisionSnapshot
    {
        internal EnemyDecisionSnapshot(
            StableId selectedTargetId,
            EnemyVector2 desiredMovement,
            EnemyVector2 desiredFacing,
            EnemyMovementIntentKind movementKind,
            EnemyAttackIntent requestedAttack,
            StableId behaviorPhaseId,
            StableId reasonCode)
        {
            SelectedTargetId = selectedTargetId;
            DesiredMovement = desiredMovement;
            DesiredFacing = desiredFacing;
            MovementKind = movementKind;
            RequestedAttack = requestedAttack;
            BehaviorPhaseId = behaviorPhaseId;
            ReasonCode = reasonCode;
        }

        public StableId SelectedTargetId { get; }
        public EnemyVector2 DesiredMovement { get; }
        public EnemyVector2 DesiredFacing { get; }
        public EnemyMovementIntentKind MovementKind { get; }
        public EnemyAttackIntent RequestedAttack { get; }
        public StableId BehaviorPhaseId { get; }
        public StableId ReasonCode { get; }
    }

    public sealed class EnemyDebugSnapshot
    {
        internal EnemyDebugSnapshot(
            EnemyRuntimeProjection runtime,
            EnemyDecisionProfile profile,
            EnemyDecisionSnapshot decision,
            EnemyVector2 currentFacing,
            EnemyPerceivedTarget selectedTarget,
            bool selectedTargetWithinAttackArc,
            EnemyVector2 commitmentDirection,
            EnemyVector2 commitmentPoint)
        {
            EntityId = runtime.Identity.EntityInstanceId;
            DefinitionId = runtime.Definition.DefinitionId;
            LifecyclePhase = runtime.LifecyclePhase;
            CurrentHealth = runtime.CurrentHealth;
            MaximumHealth = runtime.MaximumHealth;
            RoomClearRole = runtime.Definition.RoomClearRole;
            DetectionRadius = profile.DetectionRadius;
            MinimumAttackRange = profile.MinimumAttackRange;
            PreferredAttackRange = profile.PreferredAttackRange;
            MaximumAttackRange = profile.MaximumAttackRange;
            AttackArcDegrees = profile.AttackArcDegrees;
            CurrentFacing = currentFacing;
            SelectedTargetId = decision.SelectedTargetId;
            SelectedTargetDistance = selectedTarget == null ? 0d : selectedTarget.Distance;
            SelectedTargetHasLineOfSight = selectedTarget != null && selectedTarget.HasLineOfSight;
            SelectedTargetWithinDetectionRange =
                selectedTarget != null && selectedTarget.IsWithinDetectionRange;
            SelectedTargetWithinVisionArc =
                selectedTarget != null && selectedTarget.IsWithinVisionArc;
            SelectedTargetWithinAttackArc = selectedTargetWithinAttackArc;
            DesiredMovement = decision.DesiredMovement;
            DesiredFacing = decision.DesiredFacing;
            RequestedAttack = decision.RequestedAttack;
            BehaviorPhaseId = decision.BehaviorPhaseId;
            DecisionReasonCode = decision.ReasonCode;
            CommitmentDirection = commitmentDirection;
            CommitmentPoint = commitmentPoint;
        }

        public StableId EntityId { get; }
        public StableId DefinitionId { get; }
        public ShooterMover.Domain.Enemies.EnemyActorLifecyclePhase LifecyclePhase { get; }
        public double CurrentHealth { get; }
        public double MaximumHealth { get; }
        public EnemyRoomClearRole RoomClearRole { get; }
        public StableId SelectedTargetId { get; }
        public double DetectionRadius { get; }
        public double MinimumAttackRange { get; }
        public double PreferredAttackRange { get; }
        public double MaximumAttackRange { get; }
        public double AttackArcDegrees { get; }
        public EnemyVector2 CurrentFacing { get; }
        public double SelectedTargetDistance { get; }
        public bool SelectedTargetHasLineOfSight { get; }
        public bool SelectedTargetWithinDetectionRange { get; }
        public bool SelectedTargetWithinVisionArc { get; }
        public bool SelectedTargetWithinAttackArc { get; }
        public EnemyVector2 DesiredMovement { get; }
        public EnemyVector2 DesiredFacing { get; }
        public EnemyAttackIntent RequestedAttack { get; }
        public StableId BehaviorPhaseId { get; }
        public EnemyVector2 CommitmentDirection { get; }
        public EnemyVector2 CommitmentPoint { get; }
        public StableId DecisionReasonCode { get; }
    }

    public sealed class EnemyDecisionEvaluation
    {
        internal EnemyDecisionEvaluation(EnemyDecisionSnapshot decision, EnemyDebugSnapshot debug)
        {
            Decision = decision;
            Debug = debug;
        }

        public EnemyDecisionSnapshot Decision { get; }
        public EnemyDebugSnapshot Debug { get; }
    }

    public static class EnemyDecisionPolicy
    {
        private static readonly StableId NoTargetReason = StableId.Create("enemy-decision", "no-valid-target");
        private static readonly StableId ApproachReason = StableId.Create("enemy-decision", "approach-target");
        private static readonly StableId RetreatReason = StableId.Create("enemy-decision", "retreat-from-target");
        private static readonly StableId HoldReason = StableId.Create("enemy-decision", "hold-position");
        private static readonly StableId AttackReason = StableId.Create("enemy-decision", "request-attack");
        private static readonly StableId CadenceReason = StableId.Create("enemy-decision", "cadence-not-ready");
        private static readonly StableId AttackRejectedReason = StableId.Create("enemy-decision", "attack-conditions-rejected");

        public static EnemyDecisionEvaluation Evaluate(
            EnemyRuntimeProjection runtime,
            EnemyDecisionProfile profile,
            EnemyPerceptionSnapshot perception)
        {
            return Evaluate(
                runtime,
                profile,
                perception,
                perception == null ? new EnemyVector2() : perception.ObserverPosition);
        }

        public static EnemyDecisionEvaluation Evaluate(
            EnemyRuntimeProjection runtime,
            EnemyDecisionProfile profile,
            EnemyPerceptionSnapshot perception,
            EnemyVector2 committedAttackOrigin)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (perception == null) throw new ArgumentNullException(nameof(perception));

            EnemyPerceivedTarget selected = SelectTarget(perception, profile.DetectionRadius);
            bool selectedTargetWithinAttackArc = selected != null
                && EnemyPerceptionBuilder.IsWithinArc(
                    perception.ObserverFacing,
                    selected.Direction,
                    profile.AttackArcDegrees);

            EnemyVector2 desiredMovement = new EnemyVector2();
            EnemyVector2 desiredFacing = perception.ObserverFacing;
            EnemyMovementIntentKind movementKind = EnemyMovementIntentKind.Hold;
            StableId movementReason = HoldReason;

            if (runtime.ActorState.IsActive && selected != null)
            {
                desiredFacing = selected.Direction;
                if (profile.UsesIndependentMovementBand)
                {
                    double innerMovementDistance = Math.Max(
                        0d,
                        profile.PreferredMovementDistance - profile.MovementTolerance);
                    double outerMovementDistance =
                        profile.PreferredMovementDistance + profile.MovementTolerance;
                    if (selected.Distance < innerMovementDistance)
                    {
                        desiredMovement = new EnemyVector2(-selected.Direction.X, -selected.Direction.Y);
                        movementKind = EnemyMovementIntentKind.Retreat;
                        movementReason = RetreatReason;
                    }
                    else if (selected.Distance > outerMovementDistance)
                    {
                        desiredMovement = selected.Direction;
                        movementKind = EnemyMovementIntentKind.Approach;
                        movementReason = ApproachReason;
                    }
                }
                else if (selected.Distance < profile.MinimumAttackRange)
                {
                    desiredMovement = new EnemyVector2(-selected.Direction.X, -selected.Direction.Y);
                    movementKind = EnemyMovementIntentKind.Retreat;
                    movementReason = RetreatReason;
                }
                else if (selected.Distance > profile.MaximumAttackRange
                    || !selected.HasLineOfSight
                    || !selected.IsWithinVisionArc
                    || !selectedTargetWithinAttackArc)
                {
                    desiredMovement = selected.Direction;
                    movementKind = EnemyMovementIntentKind.Approach;
                    movementReason = ApproachReason;
                }
            }

            EnemyAttackIntent attack = null;
            StableId reasonCode = movementReason;
            if (!runtime.ActorState.IsActive || selected == null)
            {
                reasonCode = NoTargetReason;
            }
            else
            {
                bool withinAttackRange = selected.Distance >= profile.MinimumAttackRange
                    && selected.Distance <= profile.MaximumAttackRange;
                bool attackGeometryAccepted = withinAttackRange
                    && selected.HasLineOfSight
                    && selected.IsWithinVisionArc
                    && selectedTargetWithinAttackArc;
                bool cadenceAccepted = runtime.BehaviorPhaseId == profile.ReadyPhaseId;
                if (attackGeometryAccepted && cadenceAccepted)
                {
                    StableId decisionId = StableId.Create(
                        "enemy-decision",
                        "attack-"
                        + unchecked((uint)runtime.Identity.EntityInstanceId.GetHashCode())
                            .ToString("x8", CultureInfo.InvariantCulture)
                        + "-generation-"
                        + runtime.LifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                        + "-tick-"
                        + perception.SimulationTick.ToString(CultureInfo.InvariantCulture));
                    EnemyVector2 committedOffset = new EnemyVector2(
                        selected.Position.X - committedAttackOrigin.X,
                        selected.Position.Y - committedAttackOrigin.Y);
                    attack = new EnemyAttackIntent(
                        runtime.Identity.EntityInstanceId,
                        runtime.Identity.Ownership.RunParticipantId,
                        selected.EntityId,
                        profile.AttackId,
                        committedAttackOrigin,
                        committedOffset.Normalized,
                        selected.Position,
                        decisionId,
                        profile.ReadyPhaseId,
                        AttackReason);
                    reasonCode = AttackReason;
                }
                else if (attackGeometryAccepted)
                {
                    reasonCode = CadenceReason;
                }
                else if (movementKind == EnemyMovementIntentKind.Hold)
                {
                    reasonCode = AttackRejectedReason;
                }
            }

            EnemyDecisionSnapshot decision = new EnemyDecisionSnapshot(
                selected == null ? null : selected.EntityId,
                desiredMovement,
                desiredFacing,
                movementKind,
                attack,
                runtime.BehaviorPhaseId,
                reasonCode);
            EnemyVector2 commitDirection = attack == null
                ? new EnemyVector2()
                : attack.CommittedDirection;
            EnemyVector2 commitPoint = attack == null
                ? new EnemyVector2()
                : attack.CommittedTargetPoint;
            EnemyDebugSnapshot debug = new EnemyDebugSnapshot(
                runtime,
                profile,
                decision,
                perception.ObserverFacing,
                selected,
                selectedTargetWithinAttackArc,
                commitDirection,
                commitPoint);
            return new EnemyDecisionEvaluation(decision, debug);
        }

        private static EnemyPerceivedTarget SelectTarget(
            EnemyPerceptionSnapshot perception,
            double detectionRadius)
        {
            EnemyPerceivedTarget selected = null;
            foreach (EnemyPerceivedTarget candidate in perception.Targets)
            {
                if (candidate.Relationship != EnemyTargetRelationship.Hostile
                    || !candidate.IsWithinDetectionRange
                    || candidate.Distance > detectionRadius)
                    continue;
                if (selected == null
                    || candidate.Distance < selected.Distance
                    || (candidate.Distance == selected.Distance
                        && candidate.EntityId.CompareTo(selected.EntityId) < 0))
                    selected = candidate;
            }

            return selected;
        }
    }
}
