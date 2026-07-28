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
    public sealed class RoomEnemySpawner2D : MonoBehaviour
    {
        [SerializeField] private JsonRoomRuntimeBootstrap2D roomLoader;
        [SerializeField] private RoomRuntimeComposition2D room;
        [SerializeField] private EnemyCatalogAsset2D enemyCatalog;
        [SerializeField] private string runStableId;
        [SerializeField] private string difficultyStableId;
        [SerializeField] private double difficultyScalar = 1d;

        private Dictionary<StableId, EnemyBinding> enemiesByPlacement =
            new Dictionary<StableId, EnemyBinding>();
        private Dictionary<StableId, EnemyBinding> enemiesByActor =
            new Dictionary<StableId, EnemyBinding>();
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

        public bool Synchronize()
        {
            return TrySynchronizeCurrentRevision(true);
        }

        public bool TryGetEnemy(
            StableId placementStableId,
            out RoomEnemyActor2D enemy)
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
            out RoomEnemyActor2D enemy)
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
            RoomContentBundleV1 bundle;
            RoomLiveRuntimeProjectionV1 projection;
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
            var boundDuringAttempt = new List<RoomEnemyActor2D>();
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
                EnemyCatalogImportResultV1 importResult = enemyCatalog.Import();
                if (importResult == null || !importResult.IsValid)
                {
                    throw new InvalidOperationException(BuildCatalogFailure(importResult));
                }

                StableId runId = StableId.Parse(runStableId);
                StableId difficultyId = StableId.Parse(difficultyStableId);
                StableId currentRoomId = projection.CurrentRoomStableId;
                AuthorableRoomDefinitionV1 authoredRoom =
                    bundle.RuntimeDefinition.GetRoom(currentRoomId);
                RoomLiveRoomProjectionV1 currentRoomProjection =
                    room.Query.GetRoomProjection(currentRoomId);
                RoomContentObjectCatalogV1 roomObjects =
                    BuiltInRoomContentObjectCatalogV1.Create();
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
                        new RoomEnemyAttackPresentationPortV1();
                    EnemyRuntimeDownstreamPortsV1 downstream =
                        BuildDownstreamPorts(attackPresentation);
                    EnemyPlacementRuntimeFactoryV1 factory =
                        BuiltInEnemyRuntimePolicyRegistryV1.CreateFactory(
                            roomObjects,
                            importResult.Catalog,
                            downstream);
                    var requests = new List<EnemyPlacementRuntimeRequestV1>(
                        candidates.Count);
                    var difficulty = new EnemyDifficultyContextV1(
                        difficultyId,
                        difficultyScalar);
                    for (int index = 0; index < candidates.Count; index++)
                    {
                        requests.Add(new EnemyPlacementRuntimeRequestV1(
                            candidates[index].Content,
                            runId,
                            room.Query.RuntimeInstanceStableId,
                            null,
                            projection.LifecycleGeneration,
                            revision,
                            difficulty));
                    }

                    EnemyRoomPlacementCompositionResultV1 composition =
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
                        new Dictionary<StableId, EnemyPlacementRuntimeInstanceV1>();
                    for (int index = 0; index < composition.Runtimes.Count; index++)
                    {
                        EnemyPlacementRuntimeInstanceV1 runtime =
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
                        EnemyPlacementRuntimeInstanceV1 runtime;
                        if (!runtimesByPlacement.TryGetValue(
                                candidate.Content.InstanceStableId,
                                out runtime))
                        {
                            throw new InvalidOperationException(
                                "Enemy factory omitted placement "
                                + candidate.Content.InstanceStableId
                                + ".");
                        }

                        RoomEnemyActor2D actor =
                            candidate.Placed.GetComponent<RoomEnemyActor2D>()
                            ?? candidate.Placed.gameObject.AddComponent<RoomEnemyActor2D>();
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
            RoomContentBundleV1 bundle,
            AuthorableRoomDefinitionV1 authoredRoom,
            RoomLiveRoomProjectionV1 roomProjection,
            IRoomContentObjectCatalogV1 roomObjects,
            StableId currentRoomId)
        {
            var rows = new List<RoomEnemyPlacementContentV1>();
            for (int index = 0; index < bundle.Enemies.Count; index++)
            {
                RoomEnemyPlacementContentV1 row = bundle.Enemies[index];
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
                RoomEnemyPlacementContentV1 row = rows[index];
                RoomPlacedInstance2D placed;
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
                    || placed.PlacementKind != RoomLivePlacementKindV1.Enemy)
                {
                    throw new InvalidOperationException(
                        "Spawned room placement does not match enemy row "
                        + row.InstanceStableId
                        + ".");
                }

                RoomPlacedEntityDefinitionV1 placement;
                if (!authoredRoom.TryGetPlacement(row.InstanceStableId, out placement)
                    || placement == null
                    || placement.PlacementKind != RoomLivePlacementKindV1.Enemy)
                {
                    throw new InvalidOperationException(
                        "Compiled room enemy placement is missing for "
                        + row.InstanceStableId
                        + ".");
                }

                RoomContentObjectDefinitionV1 roomObject;
                if (!roomObjects.TryResolve(
                        row.ObjectStableId,
                        RoomContentObjectKindV1.Enemy,
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

                RoomEnemyActor2D existing = placed.GetComponent<RoomEnemyActor2D>();
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

        private EnemyRuntimeDownstreamPortsV1 BuildDownstreamPorts(
            RoomEnemyAttackPresentationPortV1 attackPresentation)
        {
            if (attackPresentation == null)
                throw new ArgumentNullException(nameof(attackPresentation));
            var rewards = new NoRewardPort();
            return new EnemyRuntimeDownstreamPortsV1(
                attackPresentation,
                new UnconnectedPlayerDamagePort(),
                new EnemyRoomPort(this),
                rewards,
                rewards,
                rewards,
                new EnemyCollisionPort(this));
        }

        private void ReportTerminal(
            ReportRoomOccupantTerminalCommandV1 command,
            EnemyDeathFactV1 deathFact)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (deathFact == null) throw new ArgumentNullException(nameof(deathFact));
            EnemyBinding binding = RequireBindingByActor(
                deathFact.Identity.EntityInstanceId);
            EnemyPlacementRuntimeInstanceV1 runtime = binding.Runtime;
            EnemyBinding placementBinding;
            bool placementMapped = enemiesByPlacement.TryGetValue(
                deathFact.Identity.PlacementStableId,
                out placementBinding)
                && object.ReferenceEquals(binding, placementBinding);
            RoomLiveRuntimeProjectionV1 projection = room == null
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

            RoomLiveOperationResultV1 result = room.ReportOccupantTerminal(
                command.OperationStableId,
                deathFact.Identity.RoomStableId,
                deathFact.Identity.PlacementStableId);
            if (result == null || result.Status == RoomLiveOperationStatusV1.Rejected)
            {
                string rejection = result == null
                    ? "room-terminal-result-missing"
                    : result.RejectionCode;
                throw new InvalidOperationException(
                    "Room rejected enemy terminal report: " + rejection);
            }
        }

        private void SetTerminalCollision(EnemyTerminalCollisionFactV1 fact)
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
            out RoomContentBundleV1 bundle,
            out RoomLiveRuntimeProjectionV1 projection,
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

        private void ValidateCompositionInputs(RoomContentBundleV1 bundle)
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
            if (string.IsNullOrWhiteSpace(runStableId))
            {
                throw new InvalidOperationException(
                    "A run stable ID is required to compose room enemies.");
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
            var actors = new List<RoomEnemyActor2D>(previous.Count);
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
            IReadOnlyList<RoomEnemyActor2D> actors)
        {
            Exception firstFailure = null;
            Exception firstFatalFailure = null;
            for (int index = actors.Count - 1; index >= 0; index--)
            {
                RoomEnemyActor2D actor = actors[index];
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
            EnemyCatalogImportResultV1 result)
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
            RoomLiveRoomProjectionV1 projection,
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

        private static bool SameVector(RoomVector2V1 left, RoomVector2V1 right)
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
                RoomEnemyPlacementContentV1 content,
                RoomPlacedInstance2D placed)
            {
                Content = content;
                Placed = placed;
            }

            public RoomEnemyPlacementContentV1 Content { get; }
            public RoomPlacedInstance2D Placed { get; }
        }

        private sealed class EnemyBinding
        {
            public EnemyBinding(
                RoomEnemyActor2D actor,
                EnemyPlacementRuntimeInstanceV1 runtime)
            {
                Actor = actor ?? throw new ArgumentNullException(nameof(actor));
                Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            }

            public RoomEnemyActor2D Actor { get; }
            public EnemyPlacementRuntimeInstanceV1 Runtime { get; }
        }

        private sealed class EnemyRoomPort : IEnemyRoomTerminalPortV1
        {
            private readonly RoomEnemySpawner2D owner;

            public EnemyRoomPort(RoomEnemySpawner2D owner)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public void Report(
                ReportRoomOccupantTerminalCommandV1 command,
                EnemyDeathFactV1 deathFact)
            {
                owner.ReportTerminal(command, deathFact);
            }
        }

        private sealed class EnemyCollisionPort : IEnemyTerminalCollisionAdapterV1
        {
            private readonly RoomEnemySpawner2D owner;

            public EnemyCollisionPort(RoomEnemySpawner2D owner)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public void SetTerminal(EnemyTerminalCollisionFactV1 fact)
            {
                owner.SetTerminalCollision(fact);
            }
        }

        private sealed class UnconnectedAttackPort : IEnemyAttackEffectPortV1
        {
            public void Emit(EnemyAttackExecutionRequestV1 request)
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                throw new InvalidOperationException(
                    "Room enemy attack output is not connected yet.");
            }
        }

        private sealed class UnconnectedPlayerDamagePort : IEnemyPlayerDamagePortV1
        {
            public EnemyPlayerDamagePortResultV1 Route(
                EnemyPlayerDamageRequestV1 request)
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                return new EnemyPlayerDamagePortResultV1(
                    EnemyRuntimeOperationStatusV1.Rejected,
                    EnemyRuntimeRejectionCodeV1.InvalidCommand);
            }
        }

        private sealed class NoRewardPort :
            IEnemyExperienceFactConsumerV1,
            IEnemyDropFactConsumerV1,
            IEnemyKillStatFactConsumerV1
        {
            void IEnemyExperienceFactConsumerV1.Consume(EnemyDeathFactV1 fact)
            {
                RequireFact(fact);
            }

            void IEnemyDropFactConsumerV1.Consume(EnemyDeathFactV1 fact)
            {
                RequireFact(fact);
            }

            void IEnemyKillStatFactConsumerV1.Consume(EnemyDeathFactV1 fact)
            {
                RequireFact(fact);
            }

            private static void RequireFact(EnemyDeathFactV1 fact)
            {
                if (fact == null) throw new ArgumentNullException(nameof(fact));
            }
        }
    }
}
