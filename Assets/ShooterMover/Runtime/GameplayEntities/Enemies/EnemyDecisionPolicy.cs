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
            RequireNonNegative(detectionRadius, nameof(detectionRadius));
            RequireNonNegative(minimumAttackRange, nameof(minimumAttackRange));
            RequireNonNegative(preferredAttackRange, nameof(preferredAttackRange));
            RequireNonNegative(maximumAttackRange, nameof(maximumAttackRange));
            if (minimumAttackRange > preferredAttackRange || preferredAttackRange > maximumAttackRange)
                throw new ArgumentException("Attack ranges must be ordered.");
            if (maximumAttackRange > detectionRadius)
                throw new ArgumentException("Attack range cannot exceed detection radius.");
            if (attackArcDegrees <= 0d || attackArcDegrees > 360d || double.IsNaN(attackArcDegrees))
                throw new ArgumentOutOfRangeException(nameof(attackArcDegrees));

            DetectionRadius = detectionRadius;
            MinimumAttackRange = minimumAttackRange;
            PreferredAttackRange = preferredAttackRange;
            MaximumAttackRange = maximumAttackRange;
            AttackArcDegrees = attackArcDegrees;
            AttackId = attackId ?? throw new ArgumentNullException(nameof(attackId));
            ReadyPhaseId = readyPhaseId ?? throw new ArgumentNullException(nameof(readyPhaseId));
        }

        public double DetectionRadius { get; }
        public double MinimumAttackRange { get; }
        public double PreferredAttackRange { get; }
        public double MaximumAttackRange { get; }
        public double AttackArcDegrees { get; }
        public StableId AttackId { get; }
        public StableId ReadyPhaseId { get; }
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
        private static readonly StableId AttackReason = StableId.Create("enemy-decision", "request-attack");

        public static EnemyDecisionEvaluation Evaluate(
            EnemyRuntimeProjection runtime,
            EnemyDecisionProfile profile,
            EnemyPerceptionSnapshot perception)
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
            EnemyDecisionSnapshot decision;
            if (!runtime.ActorState.IsActive || selected == null)
            {
                decision = new EnemyDecisionSnapshot(null, new EnemyVector2(), perception.ObserverFacing,
                    EnemyMovementIntentKind.Hold, null, profile.ReadyPhaseId, NoTargetReason);
            }
            else if (selected.Distance < profile.MinimumAttackRange)
            {
                decision = new EnemyDecisionSnapshot(selected.EntityId,
                    new EnemyVector2(-selected.Direction.X, -selected.Direction.Y), selected.Direction,
                    EnemyMovementIntentKind.Retreat, null, profile.ReadyPhaseId, RetreatReason);
            }
            else if (selected.Distance > profile.MaximumAttackRange
                || !selected.HasLineOfSight
                || !selected.IsWithinVisionArc
                || !selectedTargetWithinAttackArc)
            {
                decision = new EnemyDecisionSnapshot(selected.EntityId, selected.Direction, selected.Direction,
                    EnemyMovementIntentKind.Approach, null, profile.ReadyPhaseId, ApproachReason);
            }
            else
            {
                StableId decisionId = StableId.Create(
                    "enemy-decision",
                    "attack-"
                    + unchecked((uint)runtime.Identity.EntityInstanceId.GetHashCode())
                        .ToString("x8", CultureInfo.InvariantCulture)
                    + "-tick-"
                    + perception.SimulationTick.ToString(CultureInfo.InvariantCulture));
                EnemyAttackIntent attack = new EnemyAttackIntent(runtime.Identity.EntityInstanceId,
                    runtime.Identity.Ownership.RunParticipantId, selected.EntityId, profile.AttackId,
                    perception.ObserverPosition, selected.Direction, selected.Position, decisionId,
                    profile.ReadyPhaseId, AttackReason);
                decision = new EnemyDecisionSnapshot(selected.EntityId, new EnemyVector2(), selected.Direction,
                    EnemyMovementIntentKind.Hold, attack, profile.ReadyPhaseId, AttackReason);
            }

            EnemyVector2 commitDirection = decision.RequestedAttack == null ? new EnemyVector2() : decision.RequestedAttack.CommittedDirection;
            EnemyVector2 commitPoint = decision.RequestedAttack == null ? new EnemyVector2() : decision.RequestedAttack.CommittedTargetPoint;
            EnemyDebugSnapshot debug = new EnemyDebugSnapshot(runtime, profile, decision,
                perception.ObserverFacing,
                selected,
                selectedTargetWithinAttackArc,
                commitDirection,
                commitPoint);
            return new EnemyDecisionEvaluation(decision, debug);
        }

        private static EnemyPerceivedTarget SelectTarget(EnemyPerceptionSnapshot perception, double detectionRadius)
        {
            EnemyPerceivedTarget selected = null;
            foreach (EnemyPerceivedTarget candidate in perception.Targets)
            {
                if (candidate.Relationship != EnemyTargetRelationship.Hostile
                    || !candidate.IsWithinDetectionRange || candidate.Distance > detectionRadius)
                    continue;
                if (selected == null || candidate.Distance < selected.Distance
                    || (candidate.Distance == selected.Distance && candidate.EntityId.CompareTo(selected.EntityId) < 0))
                    selected = candidate;
            }
            return selected;
        }
    }
}
