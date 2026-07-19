using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.GameplayEntities.Enemies
{
    public struct EnemyVector2 : IEquatable<EnemyVector2>
    {
        public EnemyVector2(double x, double y)
        {
            if (!IsFinite(x) || !IsFinite(y))
            {
                throw new ArgumentOutOfRangeException(nameof(x), "Coordinates must be finite.");
            }

            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
        public double Length { get { return Math.Sqrt((X * X) + (Y * Y)); } }
        public EnemyVector2 Normalized
        {
            get
            {
                double length = Length;
                return length <= 0d ? new EnemyVector2(0d, 0d) : new EnemyVector2(X / length, Y / length);
            }
        }

        public bool Equals(EnemyVector2 other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is EnemyVector2 && Equals((EnemyVector2)obj); }
        public override int GetHashCode() { unchecked { return (X.GetHashCode() * 397) ^ Y.GetHashCode(); } }
        private static bool IsFinite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }

    public enum EnemyTargetRelationship { Friendly = 1, Neutral = 2, Hostile = 3 }
    public enum EnemyMovementIntentKind { Hold = 1, Approach = 2, Retreat = 3, Strafe = 4, Committed = 5 }

    public sealed class EnemyPerceptionCandidate
    {
        public EnemyPerceptionCandidate(
            StableId entityId,
            StableId factionId,
            EnemyTargetRelationship relationship,
            EnemyVector2 position,
            EnemyVector2 velocity,
            bool hasLineOfSight)
        {
            if (!Enum.IsDefined(typeof(EnemyTargetRelationship), relationship))
            {
                throw new ArgumentOutOfRangeException(nameof(relationship));
            }

            EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
            FactionId = factionId ?? throw new ArgumentNullException(nameof(factionId));
            Relationship = relationship;
            Position = position;
            Velocity = velocity;
            HasLineOfSight = hasLineOfSight;
        }

        public StableId EntityId { get; }
        public StableId FactionId { get; }
        public EnemyTargetRelationship Relationship { get; }
        public EnemyVector2 Position { get; }
        public EnemyVector2 Velocity { get; }
        public bool HasLineOfSight { get; }
    }

    public sealed class EnemyPerceivedTarget
    {
        public EnemyPerceivedTarget(
            StableId entityId,
            StableId factionId,
            EnemyTargetRelationship relationship,
            EnemyVector2 position,
            EnemyVector2 velocity,
            double distance,
            EnemyVector2 direction,
            bool hasLineOfSight,
            bool isWithinDetectionRange,
            bool isWithinVisionArc)
        {
            EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
            FactionId = factionId ?? throw new ArgumentNullException(nameof(factionId));
            Relationship = relationship;
            Position = position;
            Velocity = velocity;
            if (double.IsNaN(distance) || double.IsInfinity(distance) || distance < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(distance));
            }

            Distance = distance;
            Direction = direction.Normalized;
            HasLineOfSight = hasLineOfSight;
            IsWithinDetectionRange = isWithinDetectionRange;
            IsWithinVisionArc = isWithinVisionArc;
        }

        public StableId EntityId { get; }
        public StableId FactionId { get; }
        public EnemyTargetRelationship Relationship { get; }
        public EnemyVector2 Position { get; }
        public EnemyVector2 Velocity { get; }
        public double Distance { get; }
        public EnemyVector2 Direction { get; }
        public bool HasLineOfSight { get; }
        public bool IsWithinDetectionRange { get; }
        public bool IsWithinVisionArc { get; }
    }

    public sealed class EnemyPerceptionSnapshot
    {
        private readonly ReadOnlyCollection<EnemyPerceivedTarget> targets;

        public EnemyPerceptionSnapshot(
            EnemyVector2 observerPosition,
            EnemyVector2 observerFacing,
            IEnumerable<EnemyPerceivedTarget> targets,
            long simulationTick)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            if (simulationTick < 0L) throw new ArgumentOutOfRangeException(nameof(simulationTick));
            List<EnemyPerceivedTarget> copy = new List<EnemyPerceivedTarget>();
            foreach (EnemyPerceivedTarget target in targets)
            {
                if (target == null) throw new ArgumentException("Targets cannot contain null.", nameof(targets));
                copy.Add(target);
            }

            ObserverPosition = observerPosition;
            ObserverFacing = observerFacing.Normalized;
            this.targets = new ReadOnlyCollection<EnemyPerceivedTarget>(copy);
            SimulationTick = simulationTick;
        }

        public EnemyVector2 ObserverPosition { get; }
        public EnemyVector2 ObserverFacing { get; }
        public IReadOnlyList<EnemyPerceivedTarget> Targets { get { return targets; } }
        public long SimulationTick { get; }
    }

    public sealed class EnemyAttackIntent
    {
        public EnemyAttackIntent(
            StableId attackerEntityId,
            StableId sourceRunParticipantId,
            StableId targetEntityId,
            StableId attackId,
            EnemyVector2 committedOrigin,
            EnemyVector2 committedDirection,
            EnemyVector2 committedTargetPoint,
            StableId decisionId,
            StableId behaviorPhaseId,
            StableId reasonCode)
        {
            AttackerEntityId = attackerEntityId ?? throw new ArgumentNullException(nameof(attackerEntityId));
            SourceRunParticipantId = sourceRunParticipantId;
            TargetEntityId = targetEntityId;
            AttackId = attackId ?? throw new ArgumentNullException(nameof(attackId));
            CommittedOrigin = committedOrigin;
            CommittedDirection = committedDirection.Normalized;
            CommittedTargetPoint = committedTargetPoint;
            DecisionId = decisionId ?? throw new ArgumentNullException(nameof(decisionId));
            BehaviorPhaseId = behaviorPhaseId ?? throw new ArgumentNullException(nameof(behaviorPhaseId));
            ReasonCode = reasonCode;
        }

        public StableId AttackerEntityId { get; }
        public StableId SourceRunParticipantId { get; }
        public StableId TargetEntityId { get; }
        public StableId AttackId { get; }
        public EnemyVector2 CommittedOrigin { get; }
        public EnemyVector2 CommittedDirection { get; }
        public EnemyVector2 CommittedTargetPoint { get; }
        public StableId DecisionId { get; }
        public StableId BehaviorPhaseId { get; }
        public StableId ReasonCode { get; }
    }

    /// <summary>
    /// Captures the non-steering portion of a charge. Later perception may inform impact
    /// opportunity, but cannot replace the direction or point chosen at commitment.
    /// </summary>
    public sealed class EnemyPounceCommitment
    {
        public EnemyPounceCommitment(EnemyAttackIntent attackIntent)
        {
            AttackIntent = attackIntent ?? throw new ArgumentNullException(nameof(attackIntent));
            CommittedTargetId = attackIntent.TargetEntityId;
            Direction = attackIntent.CommittedDirection;
            TargetPoint = attackIntent.CommittedTargetPoint;
        }

        public EnemyAttackIntent AttackIntent { get; }
        public StableId CommittedTargetId { get; }
        public EnemyVector2 Direction { get; }
        public EnemyVector2 TargetPoint { get; }

        public EnemyPounceImpactOpportunity ObserveImpact(
            StableId contactedEntityId,
            bool contactOccurred)
        {
            return new EnemyPounceImpactOpportunity(
                this,
                contactedEntityId,
                contactOccurred && contactedEntityId != null);
        }
    }

    public sealed class EnemyPounceImpactOpportunity
    {
        internal EnemyPounceImpactOpportunity(
            EnemyPounceCommitment commitment,
            StableId contactedEntityId,
            bool canImpact)
        {
            Commitment = commitment;
            ContactedEntityId = contactedEntityId;
            CanImpact = canImpact;
        }

        public EnemyPounceCommitment Commitment { get; }
        public StableId ContactedEntityId { get; }
        public bool CanImpact { get; }
    }
}
