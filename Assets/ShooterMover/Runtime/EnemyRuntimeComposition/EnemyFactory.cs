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
    public enum EnemyFactoryRejection
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

    public sealed class EnemyPlacementLiveRequest
    {
        public EnemyPlacementLiveRequest(
            RoomEnemyPlacementContent placement,
            StableId runStableId,
            StableId roomRuntimeInstanceStableId,
            StableId itemInstanceStableId,
            long roomLifecycleGeneration,
            long lifecycleGeneration,
            EnemyDifficultyContext difficulty)
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

        public RoomEnemyPlacementContent Placement { get; }
        public StableId RunStableId { get; }
        public StableId RoomRuntimeInstanceStableId { get; }
        public StableId ItemInstanceStableId { get; }
        public long RoomLifecycleGeneration { get; }
        public long LifecycleGeneration { get; }
        public EnemyDifficultyContext Difficulty { get; }
    }

    public sealed class EnemyLiveAttackBinding
    {
        public EnemyLiveAttackBinding(
            EnemyAttackCapabilityDescriptor descriptor,
            EnemyTargetingAimPolicyRegistration targetingAim,
            EnemyAttackCapabilityLiveRegistration capability)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            TargetingAim = targetingAim ?? throw new ArgumentNullException(nameof(targetingAim));
            Capability = capability ?? throw new ArgumentNullException(nameof(capability));
        }

        public EnemyAttackCapabilityDescriptor Descriptor { get; }
        public EnemyTargetingAimPolicyRegistration TargetingAim { get; }
        public EnemyAttackCapabilityLiveRegistration Capability { get; }
    }

    public sealed class EnemyFactoryResult
    {
        private EnemyFactoryResult(
            EnemyInstance runtime,
            EnemyFactoryRejection rejection,
            string diagnostic)
        {
            Runtime = runtime;
            Rejection = rejection;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public EnemyInstance Runtime { get; }
        public EnemyFactoryRejection Rejection { get; }
        public string Diagnostic { get; }
        public bool IsCreated
        {
            get
            {
                return Runtime != null
                    && Rejection == EnemyFactoryRejection.None;
            }
        }

        internal static EnemyFactoryResult Created(
            EnemyInstance runtime)
        {
            return new EnemyFactoryResult(
                runtime ?? throw new ArgumentNullException(nameof(runtime)),
                EnemyFactoryRejection.None,
                string.Empty);
        }

        internal static EnemyFactoryResult Rejected(
            EnemyFactoryRejection rejection,
            string diagnostic)
        {
            if (rejection == EnemyFactoryRejection.None)
                throw new ArgumentOutOfRangeException(nameof(rejection));
            return new EnemyFactoryResult(null, rejection, diagnostic);
        }
    }

    public sealed class EnemyRoomPlacementSetupResult
    {
        private readonly ReadOnlyCollection<EnemyInstance> runtimes;
        private readonly ReadOnlyCollection<RoomOccupantRegistration> occupants;

        private EnemyRoomPlacementSetupResult(
            StableId roomStableId,
            IEnumerable<EnemyInstance> runtimes,
            IEnumerable<RoomOccupantRegistration> occupants,
            EnemyFactoryRejection rejection,
            string diagnostic)
        {
            RoomStableId = roomStableId;
            this.runtimes = new ReadOnlyCollection<EnemyInstance>(
                new List<EnemyInstance>(
                    runtimes ?? Array.Empty<EnemyInstance>()));
            this.occupants = new ReadOnlyCollection<RoomOccupantRegistration>(
                new List<RoomOccupantRegistration>(
                    occupants ?? Array.Empty<RoomOccupantRegistration>()));
            Rejection = rejection;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public StableId RoomStableId { get; }
        public IReadOnlyList<EnemyInstance> Runtimes { get { return runtimes; } }
        public IReadOnlyList<RoomOccupantRegistration> Occupants { get { return occupants; } }
        public EnemyFactoryRejection Rejection { get; }
        public string Diagnostic { get; }
        public bool IsCreated { get { return Rejection == EnemyFactoryRejection.None; } }

        public RegisterRoomOccupantsCommand BuildRegistrationCommand(
            StableId roomRuntimeInstanceStableId,
            StableId operationStableId,
            long roomLifecycleGeneration)
        {
            if (!IsCreated)
                throw new InvalidOperationException("A rejected room composition cannot register occupants.");
            return new RegisterRoomOccupantsCommand(
                roomRuntimeInstanceStableId,
                operationStableId,
                roomLifecycleGeneration,
                RoomStableId,
                occupants);
        }

        internal static EnemyRoomPlacementSetupResult Created(
            StableId roomStableId,
            IEnumerable<EnemyInstance> runtimes,
            IEnumerable<RoomOccupantRegistration> occupants)
        {
            return new EnemyRoomPlacementSetupResult(
                roomStableId,
                runtimes,
                occupants,
                EnemyFactoryRejection.None,
                string.Empty);
        }

        internal static EnemyRoomPlacementSetupResult Rejected(
            EnemyFactoryRejection rejection,
            string diagnostic)
        {
            return new EnemyRoomPlacementSetupResult(
                null,
                Array.Empty<EnemyInstance>(),
                Array.Empty<RoomOccupantRegistration>(),
                rejection,
                diagnostic);
        }
    }

    public sealed class EnemyFactory
    {
        private static readonly Dictionary<EnemyCatalogRoomClearRole, EnemyRoomClearRole>
            RuntimeRoomRoles = new Dictionary<EnemyCatalogRoomClearRole, EnemyRoomClearRole>
            {
                { EnemyCatalogRoomClearRole.RequiredEnemy, EnemyRoomClearRole.RequiredEnemy },
                { EnemyCatalogRoomClearRole.OptionalEnemy, EnemyRoomClearRole.OptionalEnemy },
                { EnemyCatalogRoomClearRole.ObjectiveEntity, EnemyRoomClearRole.ObjectiveEntity },
                { EnemyCatalogRoomClearRole.DoesNotAffectRoomClear, EnemyRoomClearRole.DoesNotAffectRoomClear },
            };

        private static readonly Dictionary<EnemyCatalogRoomClearRole, RoomOccupantClearRole>
            OccupantRoomRoles = new Dictionary<EnemyCatalogRoomClearRole, RoomOccupantClearRole>
            {
                { EnemyCatalogRoomClearRole.RequiredEnemy, RoomOccupantClearRole.RequiredEnemy },
                { EnemyCatalogRoomClearRole.OptionalEnemy, RoomOccupantClearRole.OptionalEnemy },
                { EnemyCatalogRoomClearRole.ObjectiveEntity, RoomOccupantClearRole.ObjectiveEntity },
                { EnemyCatalogRoomClearRole.DoesNotAffectRoomClear, RoomOccupantClearRole.NonParticipant },
            };

        private readonly IRoomContentObjectCatalog roomObjects;
        private readonly EnemyCatalog enemies;
        private readonly EnemyRules policies;
        private readonly IEnemyLiveIdentityDeriver identityDeriver;
        private readonly EnemyDifficultyLiveRegistration difficulty;
        private readonly EnemyPerceptionLiveRegistration perception;
        private readonly EnemyLiveDownstreamPorts downstream;

        public EnemyFactory(
            IRoomContentObjectCatalog roomObjects,
            EnemyCatalog enemies,
            EnemyRules policies,
            IEnemyLiveIdentityDeriver identityDeriver,
            EnemyDifficultyLiveRegistration difficulty,
            EnemyPerceptionLiveRegistration perception,
            EnemyLiveDownstreamPorts downstream)
        {
            this.roomObjects = roomObjects ?? throw new ArgumentNullException(nameof(roomObjects));
            this.enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            this.policies = policies ?? throw new ArgumentNullException(nameof(policies));
            this.identityDeriver = identityDeriver ?? throw new ArgumentNullException(nameof(identityDeriver));
            this.difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
            this.perception = perception ?? throw new ArgumentNullException(nameof(perception));
            this.downstream = downstream ?? throw new ArgumentNullException(nameof(downstream));
        }

        public EnemyFactoryResult Create(EnemyPlacementLiveRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            RoomContentObjectDefinition roomObject;
            if (!roomObjects.TryResolve(
                request.Placement.ObjectStableId,
                RoomContentObjectKind.Enemy,
                out roomObject))
            {
                return Reject(
                    EnemyFactoryRejection.RoomObjectNotFound,
                    request.Placement.ObjectStableId);
            }

            EnemyDefinition definition;
            if (!enemies.TryGetDefinition(roomObject.RuntimeDefinitionStableId, out definition))
            {
                return Reject(
                    EnemyFactoryRejection.EnemyDefinitionNotFound,
                    roomObject.RuntimeDefinitionStableId);
            }
            if (roomObject.PresentationStableId != definition.PresentationId)
            {
                return Reject(
                    EnemyFactoryRejection.PresentationMismatch,
                    definition.DefinitionId);
            }
            if (definition.LevelScaling != null
                && (request.Placement.Level < definition.LevelScaling.BaseLevel
                    || request.Placement.Level > definition.LevelScaling.MaximumLevel))
            {
                return Reject(
                    EnemyFactoryRejection.LevelOutOfRange,
                    definition.DefinitionId);
            }

            EnemyMovementPolicyRegistration movement;
            if (!policies.TryResolveMovement(definition.MovementPolicyId, out movement))
            {
                return Reject(
                    EnemyFactoryRejection.MovementPolicyNotRegistered,
                    definition.MovementPolicyId);
            }
            EnemyDecisionPolicyRegistration decision;
            if (!policies.TryResolveDecision(definition.DecisionPolicyId, out decision))
            {
                return Reject(
                    EnemyFactoryRejection.DecisionPolicyNotRegistered,
                    definition.DecisionPolicyId);
            }

            var attacks = new List<EnemyLiveAttackBinding>();
            for (int index = 0; index < definition.Attacks.Count; index++)
            {
                EnemyAttackCapabilityDescriptor descriptor = definition.Attacks[index];
                EnemyAttackCapabilityLiveRegistration capability;
                if (!policies.TryResolveAttackCapability(descriptor.CapabilityId, out capability))
                {
                    return Reject(
                        EnemyFactoryRejection.AttackCapabilityNotRegistered,
                        descriptor.CapabilityId);
                }
                EnemyTargetingAimPolicyRegistration targetingAim;
                if (!policies.TryResolveTargetingAim(
                    capability.Configuration.TargetingAimPolicyId,
                    out targetingAim))
                {
                    return Reject(
                        EnemyFactoryRejection.TargetingAimPolicyNotRegistered,
                        capability.Configuration.TargetingAimPolicyId);
                }
                attacks.Add(new EnemyLiveAttackBinding(descriptor, targetingAim, capability));
            }

            EnemyLiveIdentity identity = identityDeriver.Derive(
                request.RunStableId,
                request.RoomRuntimeInstanceStableId,
                request.Placement.RoomStableId,
                request.Placement.InstanceStableId);
            EnemyDifficultyScaling scaling = difficulty.Policy.Resolve(
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
            var definitionProjection = new EnemyDefinitionView(
                definition.DefinitionId,
                definition.MovementPolicyId,
                attackIds,
                rewardIds,
                MapRuntimeRoomRole(definition.RoomClearRole));
            var occupant = new RoomOccupantRegistration(
                identity.EntityInstanceId,
                definition.DefinitionId,
                MapRoomOccupantRole(definition.RoomClearRole));

            return EnemyFactoryResult.Created(
                new EnemyInstance(
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

        public EnemyRoomPlacementSetupResult CreateRoom(
            IEnumerable<EnemyPlacementLiveRequest> requests)
        {
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            var runtimes = new List<EnemyInstance>();
            var occupants = new List<RoomOccupantRegistration>();
            var spawnIds = new HashSet<StableId>();
            StableId roomStableId = null;
            StableId runStableId = null;
            StableId roomRuntimeInstanceStableId = null;

            foreach (EnemyPlacementLiveRequest request in requests)
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
                    return EnemyRoomPlacementSetupResult.Rejected(
                        EnemyFactoryRejection.MixedRoomBatch,
                        "enemy-factory:mixed-room-batch");
                }
                else if (runStableId != request.RunStableId
                    || roomRuntimeInstanceStableId != request.RoomRuntimeInstanceStableId)
                {
                    return EnemyRoomPlacementSetupResult.Rejected(
                        EnemyFactoryRejection.MixedRuntimeBatch,
                        "enemy-factory:mixed-runtime-batch");
                }

                EnemyFactoryResult result = Create(request);
                if (!result.IsCreated)
                {
                    return EnemyRoomPlacementSetupResult.Rejected(
                        result.Rejection,
                        result.Diagnostic);
                }
                if (!spawnIds.Add(result.Runtime.SpawnStableId))
                {
                    return EnemyRoomPlacementSetupResult.Rejected(
                        EnemyFactoryRejection.DuplicateSpawnIdentity,
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
            return EnemyRoomPlacementSetupResult.Created(roomStableId, runtimes, occupants);
        }

        private static EnemyFactoryResult Reject(
            EnemyFactoryRejection rejection,
            StableId id)
        {
            return EnemyFactoryResult.Rejected(
                rejection,
                "enemy-factory:" + rejection + ":" + id);
        }

        private static EnemyRoomClearRole MapRuntimeRoomRole(EnemyCatalogRoomClearRole role)
        {
            EnemyRoomClearRole mapped;
            if (!RuntimeRoomRoles.TryGetValue(role, out mapped))
                throw new ArgumentOutOfRangeException(nameof(role));
            return mapped;
        }

        private static RoomOccupantClearRole MapRoomOccupantRole(
            EnemyCatalogRoomClearRole role)
        {
            RoomOccupantClearRole mapped;
            if (!OccupantRoomRoles.TryGetValue(role, out mapped))
                throw new ArgumentOutOfRangeException(nameof(role));
            return mapped;
        }
    }
}
