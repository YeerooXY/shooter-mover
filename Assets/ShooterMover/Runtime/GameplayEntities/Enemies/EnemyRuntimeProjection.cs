using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;

namespace ShooterMover.GameplayEntities.Enemies
{
    public enum EnemyRoomClearRole
    {
        RequiredEnemy = 1,
        OptionalEnemy = 2,
        ObjectiveEntity = 3,
        DoesNotAffectRoomClear = 4,
    }

    public sealed class EnemyDefinitionProjection
    {
        private readonly ReadOnlyCollection<StableId> attackIds;
        private readonly ReadOnlyCollection<StableId> rewardProfileIds;

        public EnemyDefinitionProjection(
            StableId definitionId,
            StableId movementProfileId,
            IEnumerable<StableId> attackIds,
            IEnumerable<StableId> rewardProfileIds,
            EnemyRoomClearRole roomClearRole)
        {
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            MovementProfileId = movementProfileId;
            this.attackIds = CopyIds(attackIds, nameof(attackIds));
            this.rewardProfileIds = CopyIds(rewardProfileIds, nameof(rewardProfileIds));
            if (!Enum.IsDefined(typeof(EnemyRoomClearRole), roomClearRole))
            {
                throw new ArgumentOutOfRangeException(nameof(roomClearRole));
            }

            RoomClearRole = roomClearRole;
        }

        public StableId DefinitionId { get; }
        public StableId MovementProfileId { get; }
        public IReadOnlyList<StableId> AttackIds { get { return attackIds; } }
        public IReadOnlyList<StableId> RewardProfileIds { get { return rewardProfileIds; } }
        public EnemyRoomClearRole RoomClearRole { get; }

        private static ReadOnlyCollection<StableId> CopyIds(
            IEnumerable<StableId> values,
            string parameterName)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            List<StableId> copy = new List<StableId>();
            foreach (StableId value in values)
            {
                if (value == null)
                {
                    throw new ArgumentException("IDs cannot contain null.", parameterName);
                }

                copy.Add(value);
            }

            return new ReadOnlyCollection<StableId>(copy);
        }
    }

    /// <summary>
    /// Read-only shared-contract view over the canonical EnemyActorState authority.
    /// Lifecycle generation is session metadata and never changes Identity equality.
    /// </summary>
    public sealed class EnemyRuntimeProjection
    {
        public EnemyRuntimeProjection(
            GameplayEntityIdentity identity,
            EnemyDefinitionProjection definition,
            EnemyActorState actorState,
            long lifecycleGeneration,
            StableId currentTargetId,
            StableId behaviorPhaseId)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            ActorState = actorState ?? throw new ArgumentNullException(nameof(actorState));
            if (identity.EntityInstanceId != actorState.ActorId)
            {
                throw new ArgumentException("Entity identity must project the canonical enemy actor ID.");
            }

            if (definition.DefinitionId != actorState.RoleId)
            {
                throw new ArgumentException("Definition identity must project the canonical enemy role ID.");
            }

            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }

            LifecycleGeneration = lifecycleGeneration;
            CurrentTargetId = currentTargetId;
            BehaviorPhaseId = behaviorPhaseId;
        }

        public GameplayEntityIdentity Identity { get; }
        public EnemyDefinitionProjection Definition { get; }
        public EnemyActorState ActorState { get; }
        public long LifecycleGeneration { get; }
        public StableId CurrentTargetId { get; }
        public StableId BehaviorPhaseId { get; }
        public double CurrentHealth { get { return ActorState.Health; } }
        public double MaximumHealth { get { return ActorState.MaximumHealth; } }
        public EnemyActorLifecyclePhase LifecyclePhase { get { return ActorState.LifecyclePhase; } }
        public bool BlocksRoomClear
        {
            get
            {
                return ActorState.IsActive
                    && (Definition.RoomClearRole == EnemyRoomClearRole.RequiredEnemy
                        || Definition.RoomClearRole == EnemyRoomClearRole.ObjectiveEntity);
            }
        }
    }

    /// <summary>
    /// Multiplayer-ready projection of the canonical destroyed notification. Reward and
    /// statistics systems consume this fact; the enemy actor does not apply either outcome.
    /// </summary>
    public sealed class EnemyAttributedDeathFact
    {
        public EnemyAttributedDeathFact(
            EnemyDestroyedNotification destroyed,
            StableId sourceRunParticipantId,
            long lifecycleGeneration)
        {
            if (destroyed == null) throw new ArgumentNullException(nameof(destroyed));
            if (lifecycleGeneration < 0L) throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            EventId = destroyed.EventId;
            SourceEntityId = destroyed.SourceId;
            SourceRunParticipantId = sourceRunParticipantId;
            TargetEntityId = destroyed.TargetId;
            LifecycleGeneration = lifecycleGeneration;
            DeathCause = destroyed.DeathCause;
        }

        public StableId EventId { get; }
        public StableId SourceEntityId { get; }
        public StableId SourceRunParticipantId { get; }
        public StableId TargetEntityId { get; }
        public long LifecycleGeneration { get; }
        public EnemyActorDeathCause DeathCause { get; }
    }
}
