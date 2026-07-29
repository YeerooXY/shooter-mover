using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using ShooterMover.Application.Enemies.Catalog;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Missions.Rooms
{
    /// <summary>
    /// Creates pure enemy runtimes for the current authored room and binds them to the
    /// GameObjects already owned and spawned by the room presentation.
    /// </summary>
    [DefaultExecutionOrder(350)]
    [DisallowMultipleComponent]
    public sealed class RoomEnemies : MonoBehaviour
    {
        [SerializeField] private RoomLoader roomLoader;
        [SerializeField] private LevelRooms room;
        [SerializeField] private EnemyCatalogAsset enemyCatalog;
        [SerializeField] private string difficultyStableId;
        [SerializeField] private double difficultyScalar = 1d;

        private Dictionary<StableId, EnemyBinding> enemiesByPlacement =
            new Dictionary<StableId, EnemyBinding>();
        private Dictionary<StableId, EnemyBinding> enemiesByActor =
            new Dictionary<StableId, EnemyBinding>();
        private StableId configuredRunStableId;
        private IEnemyExperienceFactConsumer configuredExperienceConsumer;
        private IEnemyDropFactConsumer configuredDropConsumer;
        private IEnemyKillStatFactConsumer configuredKillStatisticsConsumer;
        private bool runDownstreamConfigured;
        private bool isSubscribed;
        private long appliedRevision;
        private long lastAttemptedRevision;
        private long lastLoggedFailureRevision = long.MinValue;
        private string lastLoggedFailureMessage;
        private string lastBuildError;

        public bool IsSynchronized
        {
            get
            {
                return roomLoader != null
                    && roomLoader.IsBuilt
                    && roomLoader.ImportedBundle != null
                    && room != null
                    && room.IsBuilt
                    && appliedRevision > 0L
                    && appliedRevision == room.PresentationRevision
                    && string.IsNullOrEmpty(lastBuildError);
            }
        }

        public long AppliedRevision
        {
            get { return appliedRevision; }
        }

        public int BoundEnemyCount
        {
            get { return enemiesByPlacement.Count; }
        }

        public string LastBuildError
        {
            get { return lastBuildError; }
        }

        /// <summary>
        /// Freezes the exact transient Run Session identity and the typed terminal-fact
        /// consumers before enemy runtimes are committed. The room terminal and collision
        /// ports remain owned by this spawner.
        /// </summary>
        public void ConfigureRunDownstream(
            StableId runStableId,
            IEnemyExperienceFactConsumer experienceConsumer,
            IEnemyDropFactConsumer dropConsumer,
            IEnemyKillStatFactConsumer killStatisticsConsumer)
        {
            if (runStableId == null) throw new ArgumentNullException(nameof(runStableId));
            if (experienceConsumer == null)
                throw new ArgumentNullException(nameof(experienceConsumer));
            if (dropConsumer == null) throw new ArgumentNullException(nameof(dropConsumer));
            if (killStatisticsConsumer == null)
                throw new ArgumentNullException(nameof(killStatisticsConsumer));
            if (runDownstreamConfigured)
            {
                throw new InvalidOperationException(
                    "Room enemy run/downstream composition is already frozen.");
            }
            if (appliedRevision > 0L || enemiesByPlacement.Count > 0
                || enemiesByActor.Count > 0)
            {
                throw new InvalidOperationException(
                    "Room enemy run/downstream composition cannot change after binding commit.");
            }

            configuredRunStableId = runStableId;
            configuredExperienceConsumer = experienceConsumer;
            configuredDropConsumer = dropConsumer;
            configuredKillStatisticsConsumer = killStatisticsConsumer;
            runDownstreamConfigured = true;

            // A room build can race ahead of the production scene composition. Missing
            // composition fails closed, then this exact configuration may retry that same
            // uncommitted presentation revision.
            lastAttemptedRevision = 0L;
            lastBuildError = null;
            lastLoggedFailureRevision = long.MinValue;
            lastLoggedFailureMessage = null;
        }

        public bool Synchronize()
        {
            return TrySynchronizeCurrentRevision(true);
        }

        public bool TryGetEnemy(
            StableId placementStableId,
            out Enemy enemy)
        {
            enemy = null;
            EnemyBinding binding;
            if (placementStableId == null
                || !enemiesByPlacement.TryGetValue(placementStableId, out binding)
                || binding == null
                || binding.Actor == null
                || !binding.Actor.IsBound)
            {
                return false;
            }

            enemy = binding.Actor;
            return true;
        }

        public bool TryGetEnemyByActor(
            StableId actorStableId,
            out Enemy enemy)
        {
            enemy = null;
            EnemyBinding binding;
            if (actorStableId == null
                || !enemiesByActor.TryGetValue(actorStableId, out binding)
                || binding == null
                || binding.Actor == null
                || !binding.Actor.IsBound)
            {
                return false;
            }

            enemy = binding.Actor;
            return true;
        }

        private void OnEnable()
        {
            Subscribe();
            TrySynchronizeCurrentRevision(true);
        }

        private void Start()
        {
            TrySynchronizeCurrentRevision(false);
        }

        private void Update()
        {
            if (room == null) return;
            long revision = room.PresentationRevision;
            if (revision <= 0L
                || revision == appliedRevision
                || revision == lastAttemptedRevision)
            {
                return;
            }

            TrySynchronizeCurrentRevision(false);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            Exception cleanupFailure = ClearCommittedBindings();
            if (cleanupFailure == null) return;
            if (IsFatalException(cleanupFailure))
            {
                ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
            }
            Debug.LogException(cleanupFailure, this);
        }

        private void Subscribe()
        {
            if (isSubscribed || room == null) return;
            room.CurrentRoomPresentationRebuilt += HandleRoomPresentationRebuilt;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed) return;
            if (room != null)
            {
                room.CurrentRoomPresentationRebuilt -= HandleRoomPresentationRebuilt;
            }
            isSubscribed = false;
        }

        private void HandleRoomPresentationRebuilt()
        {
            TrySynchronizeCurrentRevision(false);
        }

        private bool TrySynchronizeCurrentRevision(bool forceRetry)
        {
            RoomContentBundle bundle;
            RoomLiveView projection;
            long revision;
            if (!TryGetReadyState(out bundle, out projection, out revision))
            {
                return false;
            }
            if (revision == appliedRevision)
            {
                return true;
            }
            if (!forceRetry && revision == lastAttemptedRevision)
            {
                return false;
            }

            lastAttemptedRevision = revision;
            var boundDuringAttempt = new List<Enemy>();
            try
            {
                if (appliedRevision > 0L || enemiesByPlacement.Count > 0)
                {
                    Exception oldCleanup = ClearCommittedBindings();
                    if (oldCleanup != null)
                    {
                        if (IsFatalException(oldCleanup))
                        {
                            ExceptionDispatchInfo.Capture(oldCleanup).Throw();
                        }
                        throw new InvalidOperationException(
                            "Previous room enemy bindings could not be cleared.",
                            oldCleanup);
                    }
                }

                ValidateCompositionInputs(bundle);
                EnemyCatalogImportResult importResult = enemyCatalog.Import();
                if (importResult == null || !importResult.IsValid)
                {
                    throw new InvalidOperationException(BuildCatalogFailure(importResult));
                }

                StableId runId = configuredRunStableId;
                StableId difficultyId = StableId.Parse(difficultyStableId);
                StableId currentRoomId = projection.CurrentRoomStableId;
                AuthorableRoomDefinition authoredRoom =
                    bundle.RuntimeDefinition.GetRoom(currentRoomId);
                RoomLiveRoomView currentRoomProjection =
                    room.Query.GetRoomProjection(currentRoomId);
                RoomContentObjectCatalog roomObjects =
                    BuiltInRoomContentObjectCatalog.Create();
                List<EnemyCandidate> candidates = GatherCandidates(
                    bundle,
                    authoredRoom,
                    currentRoomProjection,
                    roomObjects,
                    currentRoomId);

                var temporaryByPlacement = new Dictionary<StableId, EnemyBinding>();
                var temporaryByActor = new Dictionary<StableId, EnemyBinding>();
                if (candidates.Count > 0)
                {
                    var attackPresentation =
                        new RoomEnemyAttackPresentationPort();
                    EnemyLiveDownstreamPorts downstream =
                        BuildDownstreamPorts(attackPresentation);
                    EnemyFactory factory =
                        BuiltInEnemyRules.CreateFactory(
                            roomObjects,
                            importResult.Catalog,
                            downstream);
                    var requests = new List<EnemyPlacementLiveRequest>(
                        candidates.Count);
                    var difficulty = new EnemyDifficultyContext(
                        difficultyId,
                        difficultyScalar);
                    for (int index = 0; index < candidates.Count; index++)
                    {
                        requests.Add(new EnemyPlacementLiveRequest(
                            candidates[index].Content,
                            runId,
                            room.Query.RuntimeInstanceStableId,
                            null,
                            projection.LifecycleGeneration,
                            revision,
                            difficulty));
                    }

                    EnemyRoomPlacementSetupResult composition =
                        factory.CreateRoom(requests);
                    if (composition == null || !composition.IsCreated)
                    {
                        string diagnostic = composition == null
                            ? "enemy-factory:missing-room-composition"
                            : composition.Diagnostic;
                        throw new InvalidOperationException(diagnostic);
                    }
                    if (composition.RoomStableId != currentRoomId
                        || composition.Runtimes.Count != candidates.Count)
                    {
                        throw new InvalidOperationException(
                            "Enemy factory room batch returned unexpected room facts.");
                    }

                    var runtimesByPlacement =
                        new Dictionary<StableId, EnemyInstance>();
                    for (int index = 0; index < composition.Runtimes.Count; index++)
                    {
                        EnemyInstance runtime =
                            composition.Runtimes[index];
                        if (runtime == null
                            || runtime.RoomStableId != currentRoomId
                            || runtime.Request.RoomRuntimeInstanceStableId
                                != room.Query.RuntimeInstanceStableId
                            || runtime.Request.RoomLifecycleGeneration
                                != projection.LifecycleGeneration
                            || runtime.LifecycleGeneration != revision
                            || runtimesByPlacement.ContainsKey(runtime.PlacementStableId))
                        {
                            throw new InvalidOperationException(
                                "Enemy factory room batch returned an invalid lifecycle mapping.");
                        }
                        runtimesByPlacement.Add(runtime.PlacementStableId, runtime);
                    }

                    for (int index = 0; index < candidates.Count; index++)
                    {
                        EnemyCandidate candidate = candidates[index];
                        EnemyInstance runtime;
                        if (!runtimesByPlacement.TryGetValue(
                                candidate.Content.InstanceStableId,
                                out runtime))
                        {
                            throw new InvalidOperationException(
                                "Enemy factory omitted placement "
                                + candidate.Content.InstanceStableId
                                + ".");
                        }

                        Enemy actor =
                            candidate.Placed.GetComponent<Enemy>()
                            ?? candidate.Placed.gameObject.AddComponent<Enemy>();
                        if (actor.IsBound)
                        {
                            throw new InvalidOperationException(
                                "Room enemy placement is already bound: "
                                + candidate.Content.InstanceStableId);
                        }

                        boundDuringAttempt.Add(actor);
                        actor.Bind(runtime);
                        attackPresentation.Bind(actor, revision);
                        var binding = new EnemyBinding(actor, runtime);
                        if (temporaryByPlacement.ContainsKey(runtime.PlacementStableId)
                            || temporaryByActor.ContainsKey(runtime.SpawnStableId))
                        {
                            throw new InvalidOperationException(
                                "Enemy binding identities must be unique for one room revision.");
                        }
                        temporaryByPlacement.Add(runtime.PlacementStableId, binding);
                        temporaryByActor.Add(runtime.SpawnStableId, binding);
                    }
                }

                enemiesByPlacement = temporaryByPlacement;
                enemiesByActor = temporaryByActor;
                appliedRevision = revision;
                boundDuringAttempt.Clear();
                lastBuildError = null;
                lastLoggedFailureRevision = long.MinValue;
                lastLoggedFailureMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                Exception rollbackFailure = RollbackBindings(boundDuringAttempt);
                string message = BuildFailureMessage(
                    revision,
                    exception,
                    rollbackFailure);
                LogBuildFailureOnce(revision, message);
                if (IsFatalException(exception))
                {
                    throw;
                }
                if (rollbackFailure != null && IsFatalException(rollbackFailure))
                {
                    ExceptionDispatchInfo.Capture(rollbackFailure).Throw();
                }

                lastBuildError = message;
                return false;
            }
        }

        private List<EnemyCandidate> GatherCandidates(
            RoomContentBundle bundle,
            AuthorableRoomDefinition authoredRoom,
            RoomLiveRoomView roomProjection,
            IRoomContentObjectCatalog roomObjects,
            StableId currentRoomId)
        {
            var rows = new List<RoomEnemyPlacementContent>();
            for (int index = 0; index < bundle.Enemies.Count; index++)
            {
                RoomEnemyPlacementContent row = bundle.Enemies[index];
                if (row != null
                    && row.RoomStableId == currentRoomId
                    && !IsDefeated(roomProjection, row.InstanceStableId))
                {
                    rows.Add(row);
                }
            }
            rows.Sort((left, right) => left.InstanceStableId.CompareTo(
                right.InstanceStableId));

            var candidates = new List<EnemyCandidate>(rows.Count);
            for (int index = 0; index < rows.Count; index++)
            {
                RoomEnemyPlacementContent row = rows[index];
                RoomObjectInstance placed;
                if (!room.TryGetSpawnedPlacement(row.InstanceStableId, out placed)
                    || placed == null)
                {
                    throw new InvalidOperationException(
                        "Room enemy GameObject is missing for placement "
                        + row.InstanceStableId
                        + ".");
                }
                if (!placed.IsConfigured
                    || placed.RoomStableId != currentRoomId
                    || placed.InstanceStableId != row.InstanceStableId
                    || placed.PlacementKind != RoomLivePlacementKind.Enemy)
                {
                    throw new InvalidOperationException(
                        "Spawned room placement does not match enemy row "
                        + row.InstanceStableId
                        + ".");
                }

                RoomPlacedEntityDefinition placement;
                if (!authoredRoom.TryGetPlacement(row.InstanceStableId, out placement)
                    || placement == null
                    || placement.PlacementKind != RoomLivePlacementKind.Enemy)
                {
                    throw new InvalidOperationException(
                        "Compiled room enemy placement is missing for "
                        + row.InstanceStableId
                        + ".");
                }

                RoomContentObjectDefinition roomObject;
                if (!roomObjects.TryResolve(
                        row.ObjectStableId,
                        RoomContentObjectKind.Enemy,
                        out roomObject)
                    || roomObject == null
                    || roomObject.RuntimeDefinitionStableId
                        != placement.DefinitionStableId
                    || roomObject.PresentationStableId
                        != placement.PresentationStableId
                    || placed.DefinitionStableId != placement.DefinitionStableId
                    || !SameVector(row.LocalPosition, placement.LocalPosition)
                    || row.LocalRotationDegrees != placement.LocalRotationDegrees)
                {
                    throw new InvalidOperationException(
                        "Imported and compiled enemy placement facts do not match for "
                        + row.InstanceStableId
                        + ".");
                }

                Enemy existing = placed.GetComponent<Enemy>();
                if (existing != null && existing.IsBound)
                {
                    throw new InvalidOperationException(
                        "Room enemy placement already owns a live binding: "
                        + row.InstanceStableId
                        + ".");
                }
                candidates.Add(new EnemyCandidate(row, placed));
            }

            return candidates;
        }

        private EnemyLiveDownstreamPorts BuildDownstreamPorts(
            RoomEnemyAttackPresentationPort attackPresentation)
        {
            if (attackPresentation == null)
                throw new ArgumentNullException(nameof(attackPresentation));
            return new EnemyLiveDownstreamPorts(
                attackPresentation,
                new UnconnectedPlayerDamagePort(),
                new EnemyRoomPort(this),
                configuredExperienceConsumer,
                configuredDropConsumer,
                configuredKillStatisticsConsumer,
                new EnemyCollisionPort(this));
        }

        private void ReportTerminal(
            ReportRoomOccupantTerminalCommand command,
            EnemyDeathFact deathFact)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (deathFact == null) throw new ArgumentNullException(nameof(deathFact));
            EnemyBinding binding = RequireBindingByActor(
                deathFact.Identity.EntityInstanceId);
            EnemyInstance runtime = binding.Runtime;
            EnemyBinding placementBinding;
            bool placementMapped = enemiesByPlacement.TryGetValue(
                deathFact.Identity.PlacementStableId,
                out placementBinding)
                && object.ReferenceEquals(binding, placementBinding);
            RoomLiveView projection = room == null
                ? null
                : room.CurrentProjection;
            if (projection == null
                || appliedRevision != room.PresentationRevision
                || appliedRevision != runtime.LifecycleGeneration
                || command.RuntimeInstanceStableId != room.Query.RuntimeInstanceStableId
                || command.RuntimeInstanceStableId
                    != deathFact.Identity.RoomRuntimeInstanceStableId
                || command.RuntimeInstanceStableId
                    != runtime.Identity.RoomRuntimeInstanceStableId
                || command.RoomStableId != projection.CurrentRoomStableId
                || command.RoomStableId != deathFact.Identity.RoomStableId
                || command.RoomStableId != runtime.RoomStableId
                || command.LifecycleGeneration != projection.LifecycleGeneration
                || command.LifecycleGeneration != runtime.Request.RoomLifecycleGeneration
                || command.OccupantEntityStableId != runtime.SpawnStableId
                || deathFact.Identity.EntityInstanceId != runtime.SpawnStableId
                || deathFact.Identity.RunParticipantId
                    != runtime.RunParticipantStableId
                || deathFact.Identity.RunStableId != runtime.Request.RunStableId
                || deathFact.Identity.PlacementStableId
                    != runtime.PlacementStableId
                || deathFact.LifecycleGeneration != runtime.LifecycleGeneration
                || deathFact.DefinitionStableId != runtime.Definition.DefinitionId
                || deathFact.Level != runtime.Level
                || !placementMapped)
            {
                throw new InvalidOperationException(
                    "Enemy death does not match the current room binding and lifecycle.");
            }

            RoomLiveOperationResult result = room.ReportOccupantTerminal(
                command.OperationStableId,
                deathFact.Identity.RoomStableId,
                deathFact.Identity.PlacementStableId);
            if (result == null || result.Status == RoomLiveOperationStatus.Rejected)
            {
                string rejection = result == null
                    ? "room-terminal-result-missing"
                    : result.RejectionCode;
                throw new InvalidOperationException(
                    "Room rejected enemy terminal report: " + rejection);
            }
        }

        private void SetTerminalCollision(EnemyTerminalCollisionFact fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
            EnemyBinding binding = RequireBindingByActor(fact.EntityInstanceStableId);
            if (binding.Runtime.LifecycleGeneration != fact.LifecycleGeneration
                || binding.Actor == null)
            {
                throw new InvalidOperationException(
                    "Enemy terminal collision fact is stale or unbound.");
            }

            binding.Actor.SetTerminal(fact);
        }

        private EnemyBinding RequireBindingByActor(StableId actorStableId)
        {
            EnemyBinding binding;
            if (actorStableId == null
                || !enemiesByActor.TryGetValue(actorStableId, out binding)
                || binding == null
                || binding.Runtime == null)
            {
                throw new InvalidOperationException(
                    "No live room enemy binding exists for actor "
                    + actorStableId
                    + ".");
            }

            return binding;
        }

        private bool TryGetReadyState(
            out RoomContentBundle bundle,
            out RoomLiveView projection,
            out long revision)
        {
            bundle = null;
            projection = null;
            revision = 0L;
            if (roomLoader == null || room == null || enemyCatalog == null)
            {
                return false;
            }
            if (!roomLoader.IsBuilt || !room.IsBuilt || room.Query == null)
            {
                return false;
            }

            bundle = roomLoader.ImportedBundle;
            projection = room.CurrentProjection;
            revision = room.PresentationRevision;
            return bundle != null
                && projection != null
                && projection.CurrentRoomStableId != null
                && room.Query.RuntimeInstanceStableId != null
                && revision > 0L;
        }

        private void ValidateCompositionInputs(RoomContentBundle bundle)
        {
            if (bundle == null)
            {
                throw new InvalidOperationException(
                    "An imported room bundle is required to compose room enemies.");
            }
            if (room.Definition == null
                || !string.Equals(
                    room.Definition.Fingerprint,
                    bundle.RuntimeDefinition.Fingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The imported room bundle does not match the live room definition.");
            }
            if (!runDownstreamConfigured
                || configuredRunStableId == null
                || configuredExperienceConsumer == null
                || configuredDropConsumer == null
                || configuredKillStatisticsConsumer == null)
            {
                throw new InvalidOperationException(
                    "An exact run identity and typed enemy-death downstream composition "
                    + "are required before room enemies can bind.");
            }
            if (string.IsNullOrWhiteSpace(difficultyStableId))
            {
                throw new InvalidOperationException(
                    "A difficulty stable ID is required to compose room enemies.");
            }
            if (double.IsNaN(difficultyScalar)
                || double.IsInfinity(difficultyScalar)
                || difficultyScalar <= 0d)
            {
                throw new InvalidOperationException(
                    "The room enemy difficulty scalar must be finite and positive.");
            }
        }

        private Exception ClearCommittedBindings()
        {
            Dictionary<StableId, EnemyBinding> previous = enemiesByPlacement;
            enemiesByPlacement = new Dictionary<StableId, EnemyBinding>();
            enemiesByActor = new Dictionary<StableId, EnemyBinding>();
            appliedRevision = 0L;
            var actors = new List<Enemy>(previous.Count);
            foreach (KeyValuePair<StableId, EnemyBinding> pair in previous)
            {
                if (pair.Value != null && pair.Value.Actor != null)
                {
                    actors.Add(pair.Value.Actor);
                }
            }
            return RollbackBindings(actors);
        }

        private static Exception RollbackBindings(
            IReadOnlyList<Enemy> actors)
        {
            Exception firstFailure = null;
            Exception firstFatalFailure = null;
            for (int index = actors.Count - 1; index >= 0; index--)
            {
                Enemy actor = actors[index];
                if (actor == null) continue;
                try
                {
                    actor.Unbind();
                }
                catch (Exception exception)
                {
                    if (IsFatalException(exception))
                    {
                        if (firstFatalFailure == null)
                        {
                            firstFatalFailure = exception;
                        }
                    }
                    else if (firstFailure == null)
                    {
                        firstFailure = exception;
                    }
                }
            }
            return firstFatalFailure ?? firstFailure;
        }

        private string BuildFailureMessage(
            long revision,
            Exception exception,
            Exception rollbackFailure)
        {
            string roomId = room == null || room.CurrentRoomStableId == null
                ? "<unknown>"
                : room.CurrentRoomStableId.ToString();
            string message = "Room enemy binding failed for room "
                + roomId
                + " at presentation revision "
                + revision
                + ": "
                + exception.GetType().Name
                + ": "
                + exception.Message;
            if (rollbackFailure != null)
            {
                message += " Rollback also failed: "
                    + rollbackFailure.GetType().Name
                    + ": "
                    + rollbackFailure.Message;
            }
            return message;
        }

        private void LogBuildFailureOnce(long revision, string message)
        {
            if (lastLoggedFailureRevision == revision
                && string.Equals(
                    lastLoggedFailureMessage,
                    message,
                    StringComparison.Ordinal))
            {
                return;
            }

            lastLoggedFailureRevision = revision;
            lastLoggedFailureMessage = message;
            Debug.LogError(message, this);
        }

        private static string BuildCatalogFailure(
            EnemyCatalogImportResult result)
        {
            if (result == null)
            {
                return "The enemy catalogue importer returned no result.";
            }
            if (result.Issues != null
                && result.Issues.Count > 0
                && result.Issues[0] != null)
            {
                return "Enemy catalogue import failed ["
                    + result.Issues[0].Code
                    + "] at "
                    + result.Issues[0].Path
                    + ": "
                    + result.Issues[0].Message;
            }
            return "The enemy catalogue import failed without a structured issue.";
        }

        private static bool IsDefeated(
            RoomLiveRoomView projection,
            StableId placementStableId)
        {
            for (int index = 0; index < projection.DefeatedOccupants.Count; index++)
            {
                if (projection.DefeatedOccupants[index].EntityStableId
                    == placementStableId)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool SameVector(RoomVector2 left, RoomVector2 right)
        {
            return left != null
                && right != null
                && left.X == right.X
                && left.Y == right.Y;
        }

        private static bool IsFatalException(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }

        private sealed class EnemyCandidate
        {
            public EnemyCandidate(
                RoomEnemyPlacementContent content,
                RoomObjectInstance placed)
            {
                Content = content;
                Placed = placed;
            }

            public RoomEnemyPlacementContent Content { get; }
            public RoomObjectInstance Placed { get; }
        }

        private sealed class EnemyBinding
        {
            public EnemyBinding(
                Enemy actor,
                EnemyInstance runtime)
            {
                Actor = actor ?? throw new ArgumentNullException(nameof(actor));
                Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            }

            public Enemy Actor { get; }
            public EnemyInstance Runtime { get; }
        }

        private sealed class EnemyRoomPort : IEnemyRoomTerminalPort
        {
            private readonly RoomEnemies owner;

            public EnemyRoomPort(RoomEnemies owner)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public void Report(
                ReportRoomOccupantTerminalCommand command,
                EnemyDeathFact deathFact)
            {
                owner.ReportTerminal(command, deathFact);
            }
        }

        private sealed class EnemyCollisionPort : IEnemyTerminalCollisionBridge
        {
            private readonly RoomEnemies owner;

            public EnemyCollisionPort(RoomEnemies owner)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public void SetTerminal(EnemyTerminalCollisionFact fact)
            {
                owner.SetTerminalCollision(fact);
            }
        }

        private sealed class UnconnectedPlayerDamagePort : IEnemyPlayerDamagePort
        {
            public EnemyPlayerDamagePortResult Route(
                EnemyPlayerDamageRequest request)
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                return new EnemyPlayerDamagePortResult(
                    EnemyLiveOperationStatus.Rejected,
                    EnemyLiveRejectionCode.InvalidCommand);
            }
        }
    }
}
