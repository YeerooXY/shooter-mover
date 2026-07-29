using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.GameplayEntities;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyRuntimeComposition
{
    public sealed partial class EnemyPlacementLiveInstance
    {
        private sealed class IssuedDecisionRecord
        {
            public IssuedDecisionRecord(string fingerprint, EnemyPlacementDecision decision)
            {
                Fingerprint = fingerprint;
                Decision = decision;
            }

            public string Fingerprint { get; }
            public EnemyPlacementDecision Decision { get; }
        }

        private sealed class AcceptedExecutionRecord
        {
            public AcceptedExecutionRecord(
                string fingerprint,
                string decisionFingerprint,
                EnemyAttackExecutionRequest execution)
            {
                Fingerprint = fingerprint;
                DecisionFingerprint = decisionFingerprint;
                Execution = execution;
            }

            public string Fingerprint { get; }
            public string DecisionFingerprint { get; }
            public EnemyAttackExecutionRequest Execution { get; }
        }

        private sealed class AttackReplayRecord
        {
            public AttackReplayRecord(string signature, EnemyAttackExecutionResult result)
            {
                Signature = signature;
                Result = result;
            }

            public string Signature { get; }
            public EnemyAttackExecutionResult Result { get; }
        }

        private sealed class DamageReplayRecord
        {
            public DamageReplayRecord(string signature, EnemyLiveDamageResult result)
            {
                Signature = signature;
                Result = result;
            }

            public string Signature { get; }
            public EnemyLiveDamageResult Result { get; }
        }

        private sealed class ImpactReplayRecord
        {
            public ImpactReplayRecord(string signature, EnemyPlayerDamagePortResult result)
            {
                Signature = signature;
                Result = result;
            }

            public string Signature { get; }
            public EnemyPlayerDamagePortResult Result { get; }
        }

        private readonly ReadOnlyCollection<EnemyLiveAttackBinding> attacks;
        private readonly Dictionary<StableId, EnemyLiveAttackBinding> attacksById;
        private readonly Dictionary<StableId, double> nextReadyAtByAttack;
        private readonly Dictionary<string, IssuedDecisionRecord> issuedDecisions;
        private readonly Dictionary<StableId, AcceptedExecutionRecord> acceptedExecutions;
        private readonly Dictionary<StableId, AttackReplayRecord> attackReplay;
        private readonly Dictionary<StableId, DamageReplayRecord> damageReplay;
        private readonly Dictionary<StableId, ImpactReplayRecord> impactReplay;
        private readonly EnemyPerceptionLiveRegistration perception;
        private readonly EnemyLiveDownstreamPorts downstream;
        private readonly EnemyDefinitionView definitionProjection;
        private EnemyActorState actorState;
        private StableId currentTargetId;
        private EnemyDeathFact publishedDeath;

        internal EnemyPlacementLiveInstance(
            EnemyPlacementLiveRequest request,
            EnemyLiveIdentity identity,
            RoomContentObjectDefinition roomObject,
            EnemyDefinition definition,
            EnemyActorState actorState,
            EnemyDefinitionView definitionProjection,
            EnemyMovementPolicyRegistration movement,
            EnemyDecisionPolicyRegistration decision,
            EnemyPerceptionLiveRegistration perception,
            EnemyDifficultyScaling difficultyScaling,
            IEnumerable<EnemyLiveAttackBinding> attacks,
            RoomOccupantRegistration roomOccupant,
            EnemyLiveDownstreamPorts downstream)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            RoomObject = roomObject ?? throw new ArgumentNullException(nameof(roomObject));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.actorState = actorState ?? throw new ArgumentNullException(nameof(actorState));
            this.definitionProjection = definitionProjection
                ?? throw new ArgumentNullException(nameof(definitionProjection));
            Movement = movement ?? throw new ArgumentNullException(nameof(movement));
            Decision = decision ?? throw new ArgumentNullException(nameof(decision));
            this.perception = perception ?? throw new ArgumentNullException(nameof(perception));
            DifficultyScaling = difficultyScaling
                ?? throw new ArgumentNullException(nameof(difficultyScaling));
            RoomOccupant = roomOccupant ?? throw new ArgumentNullException(nameof(roomOccupant));
            this.downstream = downstream ?? throw new ArgumentNullException(nameof(downstream));

            var copy = new List<EnemyLiveAttackBinding>(
                attacks ?? throw new ArgumentNullException(nameof(attacks)));
            copy.Sort((left, right) => left.Descriptor.AttackId.CompareTo(right.Descriptor.AttackId));
            this.attacks = new ReadOnlyCollection<EnemyLiveAttackBinding>(copy);
            attacksById = new Dictionary<StableId, EnemyLiveAttackBinding>();
            for (int index = 0; index < copy.Count; index++)
            {
                EnemyLiveAttackBinding binding = copy[index];
                if (attacksById.ContainsKey(binding.Descriptor.AttackId))
                {
                    throw new ArgumentException(
                        "Enemy runtime attack IDs must be unique: " + binding.Descriptor.AttackId,
                        nameof(attacks));
                }
                attacksById.Add(binding.Descriptor.AttackId, binding);
            }

            nextReadyAtByAttack = new Dictionary<StableId, double>();
            issuedDecisions = new Dictionary<string, IssuedDecisionRecord>(StringComparer.Ordinal);
            acceptedExecutions = new Dictionary<StableId, AcceptedExecutionRecord>();
            attackReplay = new Dictionary<StableId, AttackReplayRecord>();
            damageReplay = new Dictionary<StableId, DamageReplayRecord>();
            impactReplay = new Dictionary<StableId, ImpactReplayRecord>();
        }

        public EnemyPlacementLiveRequest Request { get; }
        public EnemyLiveIdentity Identity { get; }
        public RoomContentObjectDefinition RoomObject { get; }
        public EnemyDefinition Definition { get; }
        public EnemyMovementPolicyRegistration Movement { get; }
        public EnemyDecisionPolicyRegistration Decision { get; }
        public EnemyDifficultyScaling DifficultyScaling { get; }
        public RoomOccupantRegistration RoomOccupant { get; }
        public IReadOnlyList<EnemyLiveAttackBinding> Attacks { get { return attacks; } }
        public StableId RoomStableId { get { return Request.Placement.RoomStableId; } }
        public StableId PlacementStableId { get { return Request.Placement.InstanceStableId; } }
        public StableId SpawnStableId { get { return Identity.EntityInstanceId; } }
        public StableId RunParticipantStableId { get { return Identity.RunParticipantId; } }
        public StableId ItemInstanceStableId { get { return Request.ItemInstanceStableId; } }
        public StableId PresentationStableId { get { return Definition.PresentationId; } }
        public int Level { get { return Request.Placement.Level; } }
        public long LifecycleGeneration { get { return Request.LifecycleGeneration; } }
        public StableId LifecycleStableId
        {
            get
            {
                return StableId.Create(
                    "enemy-lifecycle",
                    "runtime-" + DeterministicEnemyLiveIdentityDeriver.Hash64(
                        Identity.EntityInstanceId
                        + "|generation|"
                        + LifecycleGeneration.ToString(CultureInfo.InvariantCulture)));
            }
        }
        public EnemyActorState ActorState { get { return actorState; } }
        public EnemyDeathFact PublishedDeath { get { return publishedDeath; } }

        public EnemyLiveView Runtime
        {
            get
            {
                return new EnemyLiveView(
                    new GameplayEntityIdentity(
                        Identity.EntityInstanceId,
                        GameplayEntityOwnership.Create(Identity.RunParticipantId, null),
                        Definition.FactionId),
                    definitionProjection,
                    actorState,
                    LifecycleGeneration,
                    currentTargetId,
                    Decision.Configuration.ReadyPhaseId);
            }
        }

        public EnemyPlacementDecision Evaluate(EnemyPerceptionSnapshot sourcePerception)
        {
            EnemyPerceptionSnapshot adapted = perception.Adapter.Adapt(
                Runtime,
                Definition,
                sourcePerception,
                perception.Configuration);
            EnemyDecisionEvaluation evaluation = Decision.Policy.Evaluate(
                Runtime,
                Definition,
                Decision.Configuration,
                adapted);
            currentTargetId = evaluation.Decision.SelectedTargetId;
            var projection = new EnemyPlacementDecision(
                Identity.EntityInstanceId,
                LifecycleGeneration,
                adapted,
                evaluation);
            string fingerprint = EnemyLiveStateFingerprint.Decision(projection);
            issuedDecisions[fingerprint] = new IssuedDecisionRecord(fingerprint, projection);
            return projection;
        }
    }
}
