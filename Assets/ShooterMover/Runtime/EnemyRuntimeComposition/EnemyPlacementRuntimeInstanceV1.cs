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
    public sealed partial class EnemyPlacementRuntimeInstanceV1
    {
        private sealed class IssuedDecisionRecord
        {
            public IssuedDecisionRecord(string fingerprint, EnemyPlacementDecisionV1 decision)
            {
                Fingerprint = fingerprint;
                Decision = decision;
            }

            public string Fingerprint { get; }
            public EnemyPlacementDecisionV1 Decision { get; }
        }

        private sealed class AcceptedExecutionRecord
        {
            public AcceptedExecutionRecord(
                string fingerprint,
                string decisionFingerprint,
                EnemyAttackExecutionRequestV1 execution)
            {
                Fingerprint = fingerprint;
                DecisionFingerprint = decisionFingerprint;
                Execution = execution;
            }

            public string Fingerprint { get; }
            public string DecisionFingerprint { get; }
            public EnemyAttackExecutionRequestV1 Execution { get; }
        }

        private sealed class AttackReplayRecord
        {
            public AttackReplayRecord(string signature, EnemyAttackExecutionResultV1 result)
            {
                Signature = signature;
                Result = result;
            }

            public string Signature { get; }
            public EnemyAttackExecutionResultV1 Result { get; }
        }

        private sealed class DamageReplayRecord
        {
            public DamageReplayRecord(string signature, EnemyRuntimeDamageResultV1 result)
            {
                Signature = signature;
                Result = result;
            }

            public string Signature { get; }
            public EnemyRuntimeDamageResultV1 Result { get; }
        }

        private sealed class ImpactReplayRecord
        {
            public ImpactReplayRecord(string signature, EnemyPlayerDamagePortResultV1 result)
            {
                Signature = signature;
                Result = result;
            }

            public string Signature { get; }
            public EnemyPlayerDamagePortResultV1 Result { get; }
        }

        private readonly ReadOnlyCollection<EnemyRuntimeAttackBindingV1> attacks;
        private readonly Dictionary<StableId, EnemyRuntimeAttackBindingV1> attacksById;
        private readonly Dictionary<StableId, double> nextReadyAtByAttack;
        private readonly Dictionary<string, IssuedDecisionRecord> issuedDecisions;
        private readonly Dictionary<StableId, AcceptedExecutionRecord> acceptedExecutions;
        private readonly Dictionary<StableId, AttackReplayRecord> attackReplay;
        private readonly Dictionary<StableId, DamageReplayRecord> damageReplay;
        private readonly Dictionary<StableId, ImpactReplayRecord> impactReplay;
        private readonly EnemyPerceptionRuntimeRegistrationV1 perception;
        private readonly EnemyRuntimeDownstreamPortsV1 downstream;
        private readonly EnemyDefinitionProjection definitionProjection;
        private EnemyActorState actorState;
        private StableId currentTargetId;
        private EnemyDeathFactV1 publishedDeath;

        internal EnemyPlacementRuntimeInstanceV1(
            EnemyPlacementRuntimeRequestV1 request,
            EnemyRuntimeIdentityV1 identity,
            RoomContentObjectDefinitionV1 roomObject,
            EnemyDefinitionV1 definition,
            EnemyActorState actorState,
            EnemyDefinitionProjection definitionProjection,
            EnemyMovementPolicyRegistrationV1 movement,
            EnemyDecisionPolicyRegistrationV1 decision,
            EnemyPerceptionRuntimeRegistrationV1 perception,
            EnemyDifficultyScalingV1 difficultyScaling,
            IEnumerable<EnemyRuntimeAttackBindingV1> attacks,
            RoomOccupantRegistrationV1 roomOccupant,
            EnemyRuntimeDownstreamPortsV1 downstream)
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

            var copy = new List<EnemyRuntimeAttackBindingV1>(
                attacks ?? throw new ArgumentNullException(nameof(attacks)));
            copy.Sort((left, right) => left.Descriptor.AttackId.CompareTo(right.Descriptor.AttackId));
            this.attacks = new ReadOnlyCollection<EnemyRuntimeAttackBindingV1>(copy);
            attacksById = new Dictionary<StableId, EnemyRuntimeAttackBindingV1>();
            for (int index = 0; index < copy.Count; index++)
            {
                EnemyRuntimeAttackBindingV1 binding = copy[index];
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

        public EnemyPlacementRuntimeRequestV1 Request { get; }
        public EnemyRuntimeIdentityV1 Identity { get; }
        public RoomContentObjectDefinitionV1 RoomObject { get; }
        public EnemyDefinitionV1 Definition { get; }
        public EnemyMovementPolicyRegistrationV1 Movement { get; }
        public EnemyDecisionPolicyRegistrationV1 Decision { get; }
        public EnemyDifficultyScalingV1 DifficultyScaling { get; }
        public RoomOccupantRegistrationV1 RoomOccupant { get; }
        public IReadOnlyList<EnemyRuntimeAttackBindingV1> Attacks { get { return attacks; } }
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
                    "runtime-" + DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                        Identity.EntityInstanceId
                        + "|generation|"
                        + LifecycleGeneration.ToString(CultureInfo.InvariantCulture)));
            }
        }
        public EnemyActorState ActorState { get { return actorState; } }
        public EnemyDeathFactV1 PublishedDeath { get { return publishedDeath; } }

        public EnemyRuntimeProjection Runtime
        {
            get
            {
                return new EnemyRuntimeProjection(
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

        public EnemyPlacementDecisionV1 Evaluate(EnemyPerceptionSnapshot sourcePerception)
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
            var projection = new EnemyPlacementDecisionV1(
                Identity.EntityInstanceId,
                LifecycleGeneration,
                adapted,
                evaluation);
            string fingerprint = EnemyRuntimeAuthorityFingerprintV1.Decision(projection);
            issuedDecisions[fingerprint] = new IssuedDecisionRecord(fingerprint, projection);
            return projection;
        }
    }
}
