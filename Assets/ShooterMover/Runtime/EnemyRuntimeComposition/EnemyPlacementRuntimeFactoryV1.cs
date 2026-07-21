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
    public enum EnemyPlacementRuntimeFactoryRejectionV1
    {
        None = 0,
        RoomObjectNotFound = 1,
        EnemyDefinitionNotFound = 2,
        PresentationMismatch = 3,
        LevelOutOfRange = 4,
        MovementPolicyNotRegistered = 5,
        DecisionPolicyNotRegistered = 6,
        AttackCapabilityNotRegistered = 7,
        TargetingAimPolicyNotRegistered = 8,
        DuplicateSpawnIdentity = 9,
        MixedRoomBatch = 10,
        MixedRuntimeBatch = 11,
    }

    public sealed class EnemyPlacementRuntimeRequestV1
    {
        public EnemyPlacementRuntimeRequestV1(
            RoomEnemyPlacementContentV1 placement,
            StableId runStableId,
            StableId roomRuntimeInstanceStableId,
            StableId itemInstanceStableId,
            long roomLifecycleGeneration,
            long lifecycleGeneration,
            EnemyDifficultyContextV1 difficulty)
        {
            Placement = placement ?? throw new ArgumentNullException(nameof(placement));
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            RoomRuntimeInstanceStableId = roomRuntimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(roomRuntimeInstanceStableId));
            if (roomLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(roomLifecycleGeneration));
            if (lifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            ItemInstanceStableId = itemInstanceStableId;
            RoomLifecycleGeneration = roomLifecycleGeneration;
            LifecycleGeneration = lifecycleGeneration;
            Difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
        }

        public RoomEnemyPlacementContentV1 Placement { get; }
        public StableId RunStableId { get; }
        public StableId RoomRuntimeInstanceStableId { get; }
        public StableId ItemInstanceStableId { get; }
        public long RoomLifecycleGeneration { get; }
        public long LifecycleGeneration { get; }
        public EnemyDifficultyContextV1 Difficulty { get; }
    }

    public sealed class EnemyRuntimeAttackBindingV1
    {
        public EnemyRuntimeAttackBindingV1(
            EnemyAttackCapabilityDescriptorV1 descriptor,
            EnemyTargetingAimPolicyRegistrationV1 targetingAim,
            EnemyAttackCapabilityRuntimeRegistrationV1 capability)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            TargetingAim = targetingAim ?? throw new ArgumentNullException(nameof(targetingAim));
            Capability = capability ?? throw new ArgumentNullException(nameof(capability));
        }

        public EnemyAttackCapabilityDescriptorV1 Descriptor { get; }
        public EnemyTargetingAimPolicyRegistrationV1 TargetingAim { get; }
        public EnemyAttackCapabilityRuntimeRegistrationV1 Capability { get; }
    }

    public sealed class EnemyPlacementRuntimeFactoryResultV1
    {
        private EnemyPlacementRuntimeFactoryResultV1(
            EnemyPlacementRuntimeInstanceV1 runtime,
            EnemyPlacementRuntimeFactoryRejectionV1 rejection,
            string diagnostic)
        {
            Runtime = runtime;
            Rejection = rejection;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public EnemyPlacementRuntimeInstanceV1 Runtime { get; }
        public EnemyPlacementRuntimeFactoryRejectionV1 Rejection { get; }
        public string Diagnostic { get; }
        public bool IsCreated
        {
            get
            {
                return Runtime != null
                    && Rejection == EnemyPlacementRuntimeFactoryRejectionV1.None;
            }
        }

        internal static EnemyPlacementRuntimeFactoryResultV1 Created(
            EnemyPlacementRuntimeInstanceV1 runtime)
        {
            return new EnemyPlacementRuntimeFactoryResultV1(
                runtime ?? throw new ArgumentNullException(nameof(runtime)),
                EnemyPlacementRuntimeFactoryRejectionV1.None,
                string.Empty);
        }

        internal static EnemyPlacementRuntimeFactoryResultV1 Rejected(
            EnemyPlacementRuntimeFactoryRejectionV1 rejection,
            string diagnostic)
        {
            if (rejection == EnemyPlacementRuntimeFactoryRejectionV1.None)
                throw new ArgumentOutOfRangeException(nameof(rejection));
            return new EnemyPlacementRuntimeFactoryResultV1(null, rejection, diagnostic);
        }
    }

    public sealed class EnemyRoomPlacementCompositionResultV1
    {
        private readonly ReadOnlyCollection<EnemyPlacementRuntimeInstanceV1> runtimes;
        private readonly ReadOnlyCollection<RoomOccupantRegistrationV1> occupants;

        private EnemyRoomPlacementCompositionResultV1(
            StableId roomStableId,
            IEnumerable<EnemyPlacementRuntimeInstanceV1> runtimes,
            IEnumerable<RoomOccupantRegistrationV1> occupants,
            EnemyPlacementRuntimeFactoryRejectionV1 rejection,
            string diagnostic)
        {
            RoomStableId = roomStableId;
            this.runtimes = new ReadOnlyCollection<EnemyPlacementRuntimeInstanceV1>(
                new List<EnemyPlacementRuntimeInstanceV1>(
                    runtimes ?? Array.Empty<EnemyPlacementRuntimeInstanceV1>()));
            this.occupants = new ReadOnlyCollection<RoomOccupantRegistrationV1>(
                new List<RoomOccupantRegistrationV1>(
                    occupants ?? Array.Empty<RoomOccupantRegistrationV1>()));
            Rejection = rejection;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public StableId RoomStableId { get; }
        public IReadOnlyList<EnemyPlacementRuntimeInstanceV1> Runtimes { get { return runtimes; } }
        public IReadOnlyList<RoomOccupantRegistrationV1> Occupants { get { return occupants; } }
        public EnemyPlacementRuntimeFactoryRejectionV1 Rejection { get; }
        public string Diagnostic { get; }
        public bool IsCreated { get { return Rejection == EnemyPlacementRuntimeFactoryRejectionV1.None; } }

        public RegisterRoomOccupantsCommandV1 BuildRegistrationCommand(
            StableId roomRuntimeInstanceStableId,
            StableId operationStableId,
            long roomLifecycleGeneration)
        {
            if (!IsCreated)
                throw new InvalidOperationException("A rejected room composition cannot register occupants.");
            return new RegisterRoomOccupantsCommandV1(
                roomRuntimeInstanceStableId,
                operationStableId,
                roomLifecycleGeneration,
                RoomStableId,
                occupants);
        }

        internal static EnemyRoomPlacementCompositionResultV1 Created(
            StableId roomStableId,
            IEnumerable<EnemyPlacementRuntimeInstanceV1> runtimes,
            IEnumerable<RoomOccupantRegistrationV1> occupants)
        {
            return new EnemyRoomPlacementCompositionResultV1(
                roomStableId,
                runtimes,
                occupants,
                EnemyPlacementRuntimeFactoryRejectionV1.None,
                string.Empty);
        }

        internal static EnemyRoomPlacementCompositionResultV1 Rejected(
            EnemyPlacementRuntimeFactoryRejectionV1 rejection,
            string diagnostic)
        {
            return new EnemyRoomPlacementCompositionResultV1(
                null,
                Array.Empty<EnemyPlacementRuntimeInstanceV1>(),
                Array.Empty<RoomOccupantRegistrationV1>(),
                rejection,
                diagnostic);
        }
    }

    public sealed class EnemyPlacementRuntimeFactoryV1
    {
        private static readonly Dictionary<EnemyCatalogRoomClearRoleV1, EnemyRoomClearRole>
            RuntimeRoomRoles = new Dictionary<EnemyCatalogRoomClearRoleV1, EnemyRoomClearRole>
            {
                { EnemyCatalogRoomClearRoleV1.RequiredEnemy, EnemyRoomClearRole.RequiredEnemy },
                { EnemyCatalogRoomClearRoleV1.OptionalEnemy, EnemyRoomClearRole.OptionalEnemy },
                { EnemyCatalogRoomClearRoleV1.ObjectiveEntity, EnemyRoomClearRole.ObjectiveEntity },
                { EnemyCatalogRoomClearRoleV1.DoesNotAffectRoomClear, EnemyRoomClearRole.DoesNotAffectRoomClear },
            };

        private static readonly Dictionary<EnemyCatalogRoomClearRoleV1, RoomOccupantClearRoleV1>
            OccupantRoomRoles = new Dictionary<EnemyCatalogRoomClearRoleV1, RoomOccupantClearRoleV1>
            {
                { EnemyCatalogRoomClearRoleV1.RequiredEnemy, RoomOccupantClearRoleV1.RequiredEnemy },
                { EnemyCatalogRoomClearRoleV1.OptionalEnemy, RoomOccupantClearRoleV1.OptionalEnemy },
                { EnemyCatalogRoomClearRoleV1.ObjectiveEntity, RoomOccupantClearRoleV1.ObjectiveEntity },
                { EnemyCatalogRoomClearRoleV1.DoesNotAffectRoomClear, RoomOccupantClearRoleV1.NonParticipant },
            };

        private readonly IRoomContentObjectCatalogV1 roomObjects;
        private readonly EnemyCatalogV1 enemies;
        private readonly EnemyRuntimePolicyRegistryV1 policies;
        private readonly IEnemyRuntimeIdentityDeriverV1 identityDeriver;
        private readonly EnemyDifficultyRuntimeRegistrationV1 difficulty;
        private readonly EnemyPerceptionRuntimeRegistrationV1 perception;
        private readonly EnemyRuntimeDownstreamPortsV1 downstream;

        public EnemyPlacementRuntimeFactoryV1(
            IRoomContentObjectCatalogV1 roomObjects,
            EnemyCatalogV1 enemies,
            EnemyRuntimePolicyRegistryV1 policies,
            IEnemyRuntimeIdentityDeriverV1 identityDeriver,
            EnemyDifficultyRuntimeRegistrationV1 difficulty,
            EnemyPerceptionRuntimeRegistrationV1 perception,
            EnemyRuntimeDownstreamPortsV1 downstream)
        {
            this.roomObjects = roomObjects ?? throw new ArgumentNullException(nameof(roomObjects));
            this.enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            this.policies = policies ?? throw new ArgumentNullException(nameof(policies));
            this.identityDeriver = identityDeriver ?? throw new ArgumentNullException(nameof(identityDeriver));
            this.difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
            this.perception = perception ?? throw new ArgumentNullException(nameof(perception));
            this.downstream = downstream ?? throw new ArgumentNullException(nameof(downstream));
        }

        public EnemyPlacementRuntimeFactoryResultV1 Create(EnemyPlacementRuntimeRequestV1 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            RoomContentObjectDefinitionV1 roomObject;
            if (!roomObjects.TryResolve(
                request.Placement.ObjectStableId,
                RoomContentObjectKindV1.Enemy,
                out roomObject))
            {
                return Reject(
                    EnemyPlacementRuntimeFactoryRejectionV1.RoomObjectNotFound,
                    request.Placement.ObjectStableId);
            }

            EnemyDefinitionV1 definition;
            if (!enemies.TryGetDefinition(roomObject.RuntimeDefinitionStableId, out definition))
            {
                return Reject(
                    EnemyPlacementRuntimeFactoryRejectionV1.EnemyDefinitionNotFound,
                    roomObject.RuntimeDefinitionStableId);
            }
            if (roomObject.PresentationStableId != definition.PresentationId)
            {
                return Reject(
                    EnemyPlacementRuntimeFactoryRejectionV1.PresentationMismatch,
                    definition.DefinitionId);
            }
            if (definition.LevelScaling != null
                && (request.Placement.Level < definition.LevelScaling.BaseLevel
                    || request.Placement.Level > definition.LevelScaling.MaximumLevel))
            {
                return Reject(
                    EnemyPlacementRuntimeFactoryRejectionV1.LevelOutOfRange,
                    definition.DefinitionId);
            }

            EnemyMovementPolicyRegistrationV1 movement;
            if (!policies.TryResolveMovement(definition.MovementPolicyId, out movement))
            {
                return Reject(
                    EnemyPlacementRuntimeFactoryRejectionV1.MovementPolicyNotRegistered,
                    definition.MovementPolicyId);
            }
            EnemyDecisionPolicyRegistrationV1 decision;
            if (!policies.TryResolveDecision(definition.DecisionPolicyId, out decision))
            {
                return Reject(
                    EnemyPlacementRuntimeFactoryRejectionV1.DecisionPolicyNotRegistered,
                    definition.DecisionPolicyId);
            }

            var attacks = new List<EnemyRuntimeAttackBindingV1>();
            for (int index = 0; index < definition.Attacks.Count; index++)
            {
                EnemyAttackCapabilityDescriptorV1 descriptor = definition.Attacks[index];
                EnemyAttackCapabilityRuntimeRegistrationV1 capability;
                if (!policies.TryResolveAttackCapability(descriptor.CapabilityId, out capability))
                {
                    return Reject(
                        EnemyPlacementRuntimeFactoryRejectionV1.AttackCapabilityNotRegistered,
                        descriptor.CapabilityId);
                }
                EnemyTargetingAimPolicyRegistrationV1 targetingAim;
                if (!policies.TryResolveTargetingAim(
                    capability.Configuration.TargetingAimPolicyId,
                    out targetingAim))
                {
                    return Reject(
                        EnemyPlacementRuntimeFactoryRejectionV1.TargetingAimPolicyNotRegistered,
                        capability.Configuration.TargetingAimPolicyId);
                }
                attacks.Add(new EnemyRuntimeAttackBindingV1(descriptor, targetingAim, capability));
            }

            EnemyRuntimeIdentityV1 identity = identityDeriver.Derive(
                request.RunStableId,
                request.RoomRuntimeInstanceStableId,
                request.Placement.RoomStableId,
                request.Placement.InstanceStableId);
            EnemyDifficultyScalingV1 scaling = difficulty.Policy.Resolve(
                request.Placement.Level,
                request.Difficulty,
                difficulty.Configuration);
            double definitionHealth = definition.LevelScaling == null
                ? definition.BaseHealth
                : definition.LevelScaling.ResolveHealth(
                    definition.BaseHealth,
                    request.Placement.Level);
            double maximumHealth = definitionHealth * scaling.HealthMultiplier;
            EnemyActorState actor = EnemyActorState.Create(
                identity.EntityInstanceId,
                definition.DefinitionId,
                maximumHealth,
                2,
                EnemyContactPolicy.Create(
                    EnemyContactMode.None,
                    0d,
                    0.5d,
                    0.02d,
                    8));

            var attackIds = new List<StableId>();
            for (int index = 0; index < definition.Attacks.Count; index++)
                attackIds.Add(definition.Attacks[index].AttackId);
            var rewardIds = new[]
            {
                definition.ExperienceProfileId,
                definition.DropProfileId,
            };
            var definitionProjection = new EnemyDefinitionProjection(
                definition.DefinitionId,
                definition.MovementPolicyId,
                attackIds,
                rewardIds,
                MapRuntimeRoomRole(definition.RoomClearRole));
            var occupant = new RoomOccupantRegistrationV1(
                identity.EntityInstanceId,
                definition.DefinitionId,
                MapRoomOccupantRole(definition.RoomClearRole));

            return EnemyPlacementRuntimeFactoryResultV1.Created(
                new EnemyPlacementRuntimeInstanceV1(
                    request,
                    identity,
                    roomObject,
                    definition,
                    actor,
                    definitionProjection,
                    movement,
                    decision,
                    perception,
                    scaling,
                    attacks,
                    occupant,
                    downstream));
        }

        public EnemyRoomPlacementCompositionResultV1 CreateRoom(
            IEnumerable<EnemyPlacementRuntimeRequestV1> requests)
        {
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            var runtimes = new List<EnemyPlacementRuntimeInstanceV1>();
            var occupants = new List<RoomOccupantRegistrationV1>();
            var spawnIds = new HashSet<StableId>();
            StableId roomStableId = null;
            StableId runStableId = null;
            StableId roomRuntimeInstanceStableId = null;

            foreach (EnemyPlacementRuntimeRequestV1 request in requests)
            {
                if (request == null)
                    throw new ArgumentException("Room requests cannot contain null.", nameof(requests));
                if (roomStableId == null)
                {
                    roomStableId = request.Placement.RoomStableId;
                    runStableId = request.RunStableId;
                    roomRuntimeInstanceStableId = request.RoomRuntimeInstanceStableId;
                }
                else if (roomStableId != request.Placement.RoomStableId)
                {
                    return EnemyRoomPlacementCompositionResultV1.Rejected(
                        EnemyPlacementRuntimeFactoryRejectionV1.MixedRoomBatch,
                        "enemy-factory:mixed-room-batch");
                }
                else if (runStableId != request.RunStableId
                    || roomRuntimeInstanceStableId != request.RoomRuntimeInstanceStableId)
                {
                    return EnemyRoomPlacementCompositionResultV1.Rejected(
                        EnemyPlacementRuntimeFactoryRejectionV1.MixedRuntimeBatch,
                        "enemy-factory:mixed-runtime-batch");
                }

                EnemyPlacementRuntimeFactoryResultV1 result = Create(request);
                if (!result.IsCreated)
                {
                    return EnemyRoomPlacementCompositionResultV1.Rejected(
                        result.Rejection,
                        result.Diagnostic);
                }
                if (!spawnIds.Add(result.Runtime.SpawnStableId))
                {
                    return EnemyRoomPlacementCompositionResultV1.Rejected(
                        EnemyPlacementRuntimeFactoryRejectionV1.DuplicateSpawnIdentity,
                        "enemy-factory:duplicate-spawn:" + result.Runtime.SpawnStableId);
                }
                runtimes.Add(result.Runtime);
                occupants.Add(result.Runtime.RoomOccupant);
            }

            if (roomStableId == null)
                throw new ArgumentException(
                    "A room composition requires at least one enemy placement.",
                    nameof(requests));
            runtimes.Sort((left, right) => left.SpawnStableId.CompareTo(right.SpawnStableId));
            occupants.Sort((left, right) => left.EntityStableId.CompareTo(right.EntityStableId));
            return EnemyRoomPlacementCompositionResultV1.Created(roomStableId, runtimes, occupants);
        }

        private static EnemyPlacementRuntimeFactoryResultV1 Reject(
            EnemyPlacementRuntimeFactoryRejectionV1 rejection,
            StableId id)
        {
            return EnemyPlacementRuntimeFactoryResultV1.Rejected(
                rejection,
                "enemy-factory:" + rejection + ":" + id);
        }

        private static EnemyRoomClearRole MapRuntimeRoomRole(EnemyCatalogRoomClearRoleV1 role)
        {
            EnemyRoomClearRole mapped;
            if (!RuntimeRoomRoles.TryGetValue(role, out mapped))
                throw new ArgumentOutOfRangeException(nameof(role));
            return mapped;
        }

        private static RoomOccupantClearRoleV1 MapRoomOccupantRole(
            EnemyCatalogRoomClearRoleV1 role)
        {
            RoomOccupantClearRoleV1 mapped;
            if (!OccupantRoomRoles.TryGetValue(role, out mapped))
                throw new ArgumentOutOfRangeException(nameof(role));
            return mapped;
        }
    }
}
