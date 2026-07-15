using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.ContentPackages.Enemies.Stage1;
using ShooterMover.ContentPackages.Weapons.Stage1Loadouts;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Encounters;
using ShooterMover.Contracts.Identity;
using ShooterMover.Contracts.Mission;
using ShooterMover.Contracts.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.TestSupport.EvidenceHarness;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.ContentPackages.Encounters.Stage1Benchmark
{
    public enum Stage1BenchmarkEnemyEncounterKind
    {
        SingleRole = 1,
        MixedPressure = 2,
        Elite = 3,
    }

    public enum Stage1BenchmarkResolutionDisposition
    {
        Applied = 1,
        NoChange = 2,
    }

    public sealed class Stage1BenchmarkEnemySpawnDefinition
    {
        public Stage1BenchmarkEnemySpawnDefinition(
            StableId entryId,
            StableId actorId,
            StableId enemyId,
            string markerId,
            int order)
        {
            EntryId = entryId ?? throw new ArgumentNullException(nameof(entryId));
            ActorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
            EnemyId = enemyId ?? throw new ArgumentNullException(nameof(enemyId));
            if (string.IsNullOrWhiteSpace(markerId))
            {
                throw new ArgumentException("A benchmark marker ID is required.", nameof(markerId));
            }

            if (order < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(order));
            }

            MarkerId = markerId;
            Order = order;
        }

        public StableId EntryId { get; }

        public StableId ActorId { get; }

        public StableId EnemyId { get; }

        public string MarkerId { get; }

        public int Order { get; }

        public EncounterParticipantEntry CreateParticipantEntry()
        {
            return new EncounterParticipantEntry(EntryId, ActorId, EnemyId, Order);
        }

        public string ToCanonicalString()
        {
            return "order=" + Order.ToString(CultureInfo.InvariantCulture)
                + "|entry_id=" + EntryId
                + "|actor_id=" + ActorId
                + "|enemy_id=" + EnemyId
                + "|marker_id=" + MarkerId;
        }
    }

    public sealed class Stage1BenchmarkEnemyEncounterDefinition
    {
        private readonly ReadOnlyCollection<Stage1BenchmarkEnemySpawnDefinition> spawns;
        private readonly string canonicalText;

        public Stage1BenchmarkEnemyEncounterDefinition(
            StableId fixtureId,
            string displayName,
            Stage1BenchmarkEnemyEncounterKind kind,
            StableId loadoutFixtureId,
            EncounterPerformanceBudget budget,
            IEnumerable<Stage1BenchmarkEnemySpawnDefinition> sourceSpawns)
        {
            FixtureId = fixtureId ?? throw new ArgumentNullException(nameof(fixtureId));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A benchmark display name is required.", nameof(displayName));
            }

            if (!Enum.IsDefined(typeof(Stage1BenchmarkEnemyEncounterKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            DisplayName = displayName;
            Kind = kind;
            LoadoutFixtureId =
                loadoutFixtureId ?? throw new ArgumentNullException(nameof(loadoutFixtureId));
            Budget = budget ?? throw new ArgumentNullException(nameof(budget));
            spawns = CopyAndValidateSpawns(sourceSpawns);
            Stage1WeaponLoadoutCatalog.Approved.GetFixedFixture(LoadoutFixtureId);
            ValidateKind();
            canonicalText = BuildCanonicalText();
        }

        public StableId FixtureId { get; }

        public string DisplayName { get; }

        public Stage1BenchmarkEnemyEncounterKind Kind { get; }

        public StableId LoadoutFixtureId { get; }

        public EncounterPerformanceBudget Budget { get; }

        public IReadOnlyList<Stage1BenchmarkEnemySpawnDefinition> Spawns
        {
            get { return spawns; }
        }

        public bool IsStandaloneElite
        {
            get
            {
                return Kind == Stage1BenchmarkEnemyEncounterKind.Elite
                    && spawns.Count == 1
                    && spawns[0].EnemyId.Equals(
                        Stage1EnemyPackageDescriptor.FourBlasterEliteId);
            }
        }

        public EncounterStartMessage CreateStartMessage(
            EncounterRuntimeIdentity runtimeIdentity)
        {
            if (runtimeIdentity == null)
            {
                throw new ArgumentNullException(nameof(runtimeIdentity));
            }

            return new EncounterStartMessage(
                runtimeIdentity,
                StableId.Create("encounter-message", "en010-start"),
                Budget,
                spawns.Select(spawn => spawn.CreateParticipantEntry()));
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public override string ToString()
        {
            return canonicalText;
        }

        private ReadOnlyCollection<Stage1BenchmarkEnemySpawnDefinition> CopyAndValidateSpawns(
            IEnumerable<Stage1BenchmarkEnemySpawnDefinition> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            List<Stage1BenchmarkEnemySpawnDefinition> ordered = source.ToList();
            if (ordered.Count == 0)
            {
                throw new ArgumentException(
                    "A benchmark encounter requires at least one enemy.",
                    nameof(source));
            }

            ordered.Sort(
                delegate(
                    Stage1BenchmarkEnemySpawnDefinition left,
                    Stage1BenchmarkEnemySpawnDefinition right)
                {
                    return left.Order.CompareTo(right.Order);
                });

            if (ordered.Count > Budget.MaximumConcurrentParticipants)
            {
                throw new ArgumentException(
                    "The initial fixture exceeds its concurrent-participant budget.",
                    nameof(source));
            }

            HashSet<StableId> entryIds = new HashSet<StableId>();
            HashSet<StableId> actorIds = new HashSet<StableId>();
            HashSet<string> markerIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < ordered.Count; index++)
            {
                Stage1BenchmarkEnemySpawnDefinition spawn = ordered[index];
                if (spawn == null)
                {
                    throw new ArgumentException(
                        "Benchmark spawns cannot contain null.",
                        nameof(source));
                }

                if (spawn.Order != index)
                {
                    throw new ArgumentException(
                        "Benchmark spawn orders must be contiguous and zero-based.",
                        nameof(source));
                }

                if (!Stage1BenchmarkEnemyEncounterCatalog.IsAcceptedEnemyId(spawn.EnemyId))
                {
                    throw new KeyNotFoundException(
                        "Missing validated Stage 1 enemy ID: " + spawn.EnemyId);
                }

                if (!Stage1BenchmarkEnemyEncounterCatalog.IsAcceptedMarkerId(spawn.MarkerId))
                {
                    throw new KeyNotFoundException(
                        "Missing benchmark arena marker ID: " + spawn.MarkerId);
                }

                if (!entryIds.Add(spawn.EntryId))
                {
                    throw new ArgumentException(
                        "Benchmark encounter entry IDs must be unique.",
                        nameof(source));
                }

                if (!actorIds.Add(spawn.ActorId))
                {
                    throw new ArgumentException(
                        "Benchmark encounter actor IDs must be unique.",
                        nameof(source));
                }

                if (!markerIds.Add(spawn.MarkerId))
                {
                    throw new ArgumentException(
                        "A controlled fixture cannot place two actors on one socket.",
                        nameof(source));
                }
            }

            return new ReadOnlyCollection<Stage1BenchmarkEnemySpawnDefinition>(ordered);
        }

        private void ValidateKind()
        {
            int eliteCount = spawns.Count(
                spawn => spawn.EnemyId.Equals(
                    Stage1EnemyPackageDescriptor.FourBlasterEliteId));

            if (Kind == Stage1BenchmarkEnemyEncounterKind.SingleRole)
            {
                if (spawns.Count != 1 || eliteCount != 0)
                {
                    throw new ArgumentException(
                        "A single-role fixture requires exactly one ordinary enemy.");
                }

                return;
            }

            if (Kind == Stage1BenchmarkEnemyEncounterKind.Elite)
            {
                if (!IsStandaloneElite)
                {
                    throw new ArgumentException(
                        "The elite benchmark must be a standalone Four-Blaster Elite.");
                }

                return;
            }

            if (spawns.Count < 2 || eliteCount != 0)
            {
                throw new ArgumentException(
                    "Mixed pressure requires at least two ordinary enemies and no elite.");
            }

            if (spawns.Select(spawn => spawn.EnemyId).Distinct().Count() < 2)
            {
                throw new ArgumentException(
                    "Mixed pressure must combine at least two ordinary roles.");
            }
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("schema=shooter-mover.stage1-benchmark-enemy-encounter")
                .Append("\nversion=1")
                .Append("\nfixture_id=").Append(FixtureId)
                .Append("\ndisplay_name=").Append(DisplayName)
                .Append("\nkind=").Append(Kind)
                .Append("\nloadout_fixture_id=").Append(LoadoutFixtureId)
                .Append("\nspawn_count=")
                .Append(spawns.Count.ToString(CultureInfo.InvariantCulture))
                .Append("\nmaximum_concurrent_participants=")
                .Append(
                    Budget.MaximumConcurrentParticipants.ToString(
                        CultureInfo.InvariantCulture))
                .Append("\nmaximum_combat_messages_per_tick=")
                .Append(
                    Budget.MaximumCombatMessagesPerTick.ToString(
                        CultureInfo.InvariantCulture))
                .Append("\nmaximum_frame_time_ms=")
                .Append(
                    Budget.MaximumFrameTimeMilliseconds.ToString(
                        "R",
                        CultureInfo.InvariantCulture));

            for (int index = 0; index < spawns.Count; index++)
            {
                builder.Append("\nspawn_")
                    .Append(index.ToString("D2", CultureInfo.InvariantCulture))
                    .Append('=')
                    .Append(spawns[index].ToCanonicalString());
            }

            return builder.ToString();
        }
    }

    public sealed class Stage1BenchmarkEnemyEncounterCatalog
    {
        public const string DefaultFixtureIdText =
            "encounter.stage1-benchmark-pursuer";
        public const string ClosePressureFixtureIdText =
            "encounter.stage1-benchmark-close-pressure";
        public const string CrossfireFixtureIdText =
            "encounter.stage1-benchmark-crossfire";
        public const string EliteFixtureIdText =
            "encounter.stage1-benchmark-four-blaster-elite";

        private static readonly string[] AcceptedMarkerIdsValue =
        {
            "socket.target.north",
            "socket.target.east",
            "socket.target.south",
            "socket.target.west",
        };

        private static readonly Stage1BenchmarkEnemyEncounterCatalog ApprovedValue =
            CreateApproved();

        private readonly ReadOnlyCollection<Stage1BenchmarkEnemyEncounterDefinition> fixtures;

        private Stage1BenchmarkEnemyEncounterCatalog(
            IEnumerable<Stage1BenchmarkEnemyEncounterDefinition> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            List<Stage1BenchmarkEnemyEncounterDefinition> ordered = source.ToList();
            ordered.Sort(
                delegate(
                    Stage1BenchmarkEnemyEncounterDefinition left,
                    Stage1BenchmarkEnemyEncounterDefinition right)
                {
                    return left.FixtureId.CompareTo(right.FixtureId);
                });

            HashSet<StableId> ids = new HashSet<StableId>();
            foreach (Stage1BenchmarkEnemyEncounterDefinition fixture in ordered)
            {
                if (fixture == null)
                {
                    throw new ArgumentException("Encounter fixtures cannot contain null.");
                }

                if (!ids.Add(fixture.FixtureId))
                {
                    throw new ArgumentException(
                        "Benchmark encounter fixture IDs must be unique.");
                }
            }

            fixtures =
                new ReadOnlyCollection<Stage1BenchmarkEnemyEncounterDefinition>(ordered);
        }

        public static Stage1BenchmarkEnemyEncounterCatalog Approved
        {
            get { return ApprovedValue; }
        }

        public IReadOnlyList<Stage1BenchmarkEnemyEncounterDefinition> FixedFixtures
        {
            get { return fixtures; }
        }

        public Stage1BenchmarkEnemyEncounterDefinition DefaultFixture
        {
            get { return GetFixture(DefaultFixtureIdText); }
        }

        public Stage1BenchmarkEnemyEncounterDefinition GetFixture(string fixtureId)
        {
            if (string.IsNullOrWhiteSpace(fixtureId))
            {
                throw new ArgumentException("A benchmark fixture ID is required.", nameof(fixtureId));
            }

            return GetFixture(StableId.Parse(fixtureId));
        }

        public Stage1BenchmarkEnemyEncounterDefinition GetFixture(StableId fixtureId)
        {
            if (fixtureId == null)
            {
                throw new ArgumentNullException(nameof(fixtureId));
            }

            for (int index = 0; index < fixtures.Count; index++)
            {
                if (fixtures[index].FixtureId.Equals(fixtureId))
                {
                    return fixtures[index];
                }
            }

            throw new KeyNotFoundException(
                "Unknown Stage 1 benchmark encounter fixture ID: " + fixtureId);
        }

        public StableId ResolveEnemyId(string enemyId)
        {
            StableId parsed = StableId.Parse(enemyId);
            if (!IsAcceptedEnemyId(parsed))
            {
                throw new KeyNotFoundException(
                    "Missing validated Stage 1 enemy ID: " + parsed);
            }

            return parsed;
        }

        public Stage1BenchmarkEnemyEncounterSession CreateSession(
            string fixtureId,
            string runId)
        {
            return new Stage1BenchmarkEnemyEncounterSession(
                GetFixture(fixtureId),
                StableId.Parse(runId));
        }

        public string CaptureFixtureMatrix()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("schema=shooter-mover.stage1-benchmark-fixture-matrix")
                .Append("\nversion=1")
                .Append("\nfixture_count=")
                .Append(fixtures.Count.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < fixtures.Count; index++)
            {
                Stage1BenchmarkEnemyEncounterDefinition fixture = fixtures[index];
                builder.Append("\nfixture_")
                    .Append(index.ToString("D2", CultureInfo.InvariantCulture))
                    .Append('=')
                    .Append(fixture.FixtureId)
                    .Append('|')
                    .Append(fixture.Kind)
                    .Append("|count=")
                    .Append(fixture.Spawns.Count.ToString(CultureInfo.InvariantCulture))
                    .Append("|roles=")
                    .Append(
                        string.Join(
                            ",",
                            fixture.Spawns.Select(spawn => spawn.EnemyId.ToString()).ToArray()))
                    .Append("|loadout=")
                    .Append(fixture.LoadoutFixtureId);
            }

            return builder.ToString();
        }

        internal static bool IsAcceptedEnemyId(StableId enemyId)
        {
            if (enemyId == null)
            {
                return false;
            }

            IReadOnlyList<StableId> accepted =
                Stage1EnemyPackageDescriptor.AcceptedEnemyIds;
            for (int index = 0; index < accepted.Count; index++)
            {
                if (accepted[index].Equals(enemyId))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsAcceptedMarkerId(string markerId)
        {
            for (int index = 0; index < AcceptedMarkerIdsValue.Length; index++)
            {
                if (string.Equals(
                        AcceptedMarkerIdsValue[index],
                        markerId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static Stage1BenchmarkEnemyEncounterCatalog CreateApproved()
        {
            StableId defaultLoadout =
                StableId.Parse(Stage1WeaponLoadoutCatalog.DefaultFixtureIdText);
            StableId ricochetLoadout =
                StableId.Parse(Stage1WeaponLoadoutCatalog.RicochetFixtureIdText);

            return new Stage1BenchmarkEnemyEncounterCatalog(
                new[]
                {
                    Fixture(
                        "encounter.stage1-benchmark-pursuer",
                        "Pursuer Drone — isolated role",
                        Stage1BenchmarkEnemyEncounterKind.SingleRole,
                        defaultLoadout,
                        Spawn(
                            "pursuer-solo",
                            "enemy.pursuer-drone",
                            "socket.target.north",
                            0)),
                    Fixture(
                        "encounter.stage1-benchmark-ram-droid",
                        "Ram Droid — isolated role",
                        Stage1BenchmarkEnemyEncounterKind.SingleRole,
                        defaultLoadout,
                        Spawn(
                            "ram-solo",
                            "enemy.ram-droid",
                            "socket.target.north",
                            0)),
                    Fixture(
                        "encounter.stage1-benchmark-mobile-blaster",
                        "Mobile Blaster Droid — isolated role",
                        Stage1BenchmarkEnemyEncounterKind.SingleRole,
                        ricochetLoadout,
                        Spawn(
                            "mobile-solo",
                            "enemy.mobile-blaster-droid",
                            "socket.target.north",
                            0)),
                    Fixture(
                        "encounter.stage1-benchmark-blaster-turret",
                        "Blaster Turret — isolated role",
                        Stage1BenchmarkEnemyEncounterKind.SingleRole,
                        defaultLoadout,
                        Spawn(
                            "turret-solo",
                            "enemy.blaster-turret",
                            "socket.target.north",
                            0)),
                    Fixture(
                        "encounter.stage1-benchmark-four-blaster-elite",
                        "Four-Blaster Elite — standalone",
                        Stage1BenchmarkEnemyEncounterKind.Elite,
                        ricochetLoadout,
                        Spawn(
                            "elite-solo",
                            "enemy.four-blaster-elite",
                            "socket.target.north",
                            0)),
                    Fixture(
                        "encounter.stage1-benchmark-close-pressure",
                        "Close pressure — Pursuer plus Ram",
                        Stage1BenchmarkEnemyEncounterKind.MixedPressure,
                        defaultLoadout,
                        Spawn(
                            "close-pursuer",
                            "enemy.pursuer-drone",
                            "socket.target.west",
                            0),
                        Spawn(
                            "close-ram",
                            "enemy.ram-droid",
                            "socket.target.east",
                            1)),
                    Fixture(
                        "encounter.stage1-benchmark-crossfire",
                        "Crossfire — Turret, Mobile, Pursuer",
                        Stage1BenchmarkEnemyEncounterKind.MixedPressure,
                        ricochetLoadout,
                        Spawn(
                            "crossfire-turret",
                            "enemy.blaster-turret",
                            "socket.target.north",
                            0),
                        Spawn(
                            "crossfire-mobile",
                            "enemy.mobile-blaster-droid",
                            "socket.target.east",
                            1),
                        Spawn(
                            "crossfire-pursuer",
                            "enemy.pursuer-drone",
                            "socket.target.west",
                            2)),
                });
        }

        private static Stage1BenchmarkEnemyEncounterDefinition Fixture(
            string fixtureId,
            string displayName,
            Stage1BenchmarkEnemyEncounterKind kind,
            StableId loadoutFixtureId,
            params Stage1BenchmarkEnemySpawnDefinition[] spawns)
        {
            return new Stage1BenchmarkEnemyEncounterDefinition(
                StableId.Parse(fixtureId),
                displayName,
                kind,
                loadoutFixtureId,
                new EncounterPerformanceBudget(
                    Math.Max(1, spawns.Length),
                    0,
                    32,
                    16.667d),
                spawns);
        }

        private static Stage1BenchmarkEnemySpawnDefinition Spawn(
            string token,
            string enemyId,
            string markerId,
            int order)
        {
            return new Stage1BenchmarkEnemySpawnDefinition(
                StableId.Create("entry", "en010-" + token),
                StableId.Create("actor", "en010-" + token),
                StableId.Parse(enemyId),
                markerId,
                order);
        }
    }

    public sealed class Stage1BenchmarkEnemyEncounterSession
    {
        private const string DefinitionFingerprint =
            "sha256:8c1e3a5f7b9d0f2a4c6e8b1d3f5a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a";

        private readonly Stage1BenchmarkEnemyEncounterDefinition fixture;
        private readonly StableId runId;
        private readonly HashSet<StableId> resolvedActorIds = new HashSet<StableId>();
        private EncounterLifecycle lifecycle;
        private int completionCount;

        internal Stage1BenchmarkEnemyEncounterSession(
            Stage1BenchmarkEnemyEncounterDefinition fixture,
            StableId runId)
        {
            this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            this.runId = runId ?? throw new ArgumentNullException(nameof(runId));
            ResetState();
        }

        public Stage1BenchmarkEnemyEncounterDefinition Fixture
        {
            get { return fixture; }
        }

        public EncounterLifecycle CurrentLifecycle
        {
            get { return lifecycle; }
        }

        public EncounterStartMessage StartMessage
        {
            get { return lifecycle.StartMessage; }
        }

        public int CompletionCount
        {
            get { return completionCount; }
        }

        public Stage1BenchmarkResolutionDisposition ResolveDestroyed(string actorId)
        {
            StableId parsedActorId = StableId.Parse(actorId);
            Stage1BenchmarkEnemySpawnDefinition spawn = FindSpawn(parsedActorId);
            if (resolvedActorIds.Contains(parsedActorId))
            {
                return Stage1BenchmarkResolutionDisposition.NoChange;
            }

            VitalMessage destroyed = new VitalMessage(
                StableId.Create(
                    "combat-event",
                    "en010-destroyed-"
                        + spawn.Order.ToString("D2", CultureInfo.InvariantCulture)),
                StableId.Parse("actor.en010-player-proxy"),
                spawn.ActorId,
                CombatChannel.Kinetic,
                VitalResult.Destroyed,
                new VitalState(0d, 100d, 0d, 0d));
            EncounterLifecycleTransition resolution =
                lifecycle.RecordCombatResolution(
                    new EncounterCombatResolutionMessage(lifecycle.Identity, destroyed));
            if (!resolution.WasApplied)
            {
                throw new InvalidOperationException(
                    "The controlled combat resolution was rejected: "
                    + resolution.Rejection);
            }

            lifecycle = resolution.Next;
            resolvedActorIds.Add(parsedActorId);
            if (lifecycle.ActiveParticipantCount == 0)
            {
                EncounterLifecycleTransition completion =
                    lifecycle.Complete(CreateCompletionMessage());
                if (completion.WasApplied)
                {
                    lifecycle = completion.Next;
                    completionCount++;
                }
                else if (completion.Kind != EncounterTransitionKind.NoChange)
                {
                    throw new InvalidOperationException(
                        "The controlled completion was rejected: "
                        + completion.Rejection);
                }
            }

            return Stage1BenchmarkResolutionDisposition.Applied;
        }

        public bool Restart()
        {
            ResetState();
            return true;
        }

        public EncounterBudgetEvaluation EvaluateBudget(
            int combatMessagesThisTick,
            double frameTimeMilliseconds)
        {
            EncounterBudgetSample sample = new EncounterBudgetSample(
                lifecycle.Identity,
                StableId.Parse("sample.en010-current"),
                lifecycle.ActiveParticipantCount,
                0,
                combatMessagesThisTick,
                frameTimeMilliseconds);
            return EncounterBudgetEvaluation.Evaluate(fixture.Budget, sample);
        }

        public string CaptureDeterministicSnapshot()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(fixture.ToCanonicalString())
                .Append("\nphase=").Append(lifecycle.Phase)
                .Append("\nactive_participants=")
                .Append(
                    lifecycle.ActiveParticipantCount.ToString(
                        CultureInfo.InvariantCulture))
                .Append("\ncompletion_count=")
                .Append(completionCount.ToString(CultureInfo.InvariantCulture))
                .Append("\nresolved_count=")
                .Append(
                    resolvedActorIds.Count.ToString(
                        CultureInfo.InvariantCulture));

            string[] resolved = resolvedActorIds
                .Select(id => id.ToString())
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < resolved.Length; index++)
            {
                builder.Append("\nresolved_")
                    .Append(index.ToString("D2", CultureInfo.InvariantCulture))
                    .Append('=')
                    .Append(resolved[index]);
            }

            return builder.ToString();
        }

        private void ResetState()
        {
            resolvedActorIds.Clear();
            completionCount = 0;
            EncounterRuntimeIdentity identity = new EncounterRuntimeIdentity(
                fixture.FixtureId,
                StableId.Parse("encounter-runtime.en010-stage1-benchmark"),
                runId,
                new RoomProjectionIdentity(
                    StableId.Parse("room.stage1-benchmark"),
                    StableId.Parse("projection.en010-stage1-benchmark")));
            EncounterLifecycleTransition started =
                EncounterLifecycle.Create(identity).Start(
                    fixture.CreateStartMessage(identity));
            if (!started.WasApplied)
            {
                throw new InvalidOperationException(
                    "The controlled encounter failed to start: " + started.Rejection);
            }

            lifecycle = started.Next;
        }

        private Stage1BenchmarkEnemySpawnDefinition FindSpawn(StableId actorId)
        {
            for (int index = 0; index < fixture.Spawns.Count; index++)
            {
                if (fixture.Spawns[index].ActorId.Equals(actorId))
                {
                    return fixture.Spawns[index];
                }
            }

            throw new KeyNotFoundException(
                "Unknown actor ID for the selected benchmark fixture: " + actorId);
        }

        private EncounterCompletionMessage CreateCompletionMessage()
        {
            MissionPayloadVersion version = new MissionPayloadVersion(
                1,
                ContentVersion.Create(1, DefinitionFingerprint));
            return new EncounterCompletionMessage(
                lifecycle.Identity,
                new MissionEventEnvelope(
                    StableId.Parse("mission-event.en010-room-cleared"),
                    StableId.Parse("command.en010-room-clear"),
                    lifecycle.Identity.RunId,
                    version,
                    new MissionSequence(1L),
                    new RoomClearedEvent(
                        lifecycle.Identity.Room.RoomId,
                        lifecycle.Identity.EncounterId)));
        }
    }

    public sealed class Stage1BenchmarkEnemyProjection : MonoBehaviour
    {
        private string fixtureId;
        private string actorId;
        private string enemyId;
        private string markerId;
        private int order;

        public string FixtureId
        {
            get { return fixtureId; }
        }

        public string ActorId
        {
            get { return actorId; }
        }

        public string EnemyId
        {
            get { return enemyId; }
        }

        public string MarkerId
        {
            get { return markerId; }
        }

        public int Order
        {
            get { return order; }
        }

        internal void Configure(
            Stage1BenchmarkEnemyEncounterDefinition fixture,
            Stage1BenchmarkEnemySpawnDefinition spawn)
        {
            fixtureId = fixture.FixtureId.ToString();
            actorId = spawn.ActorId.ToString();
            enemyId = spawn.EnemyId.ToString();
            markerId = spawn.MarkerId;
            order = spawn.Order;

            TextMesh label = gameObject.AddComponent<TextMesh>();
            label.text = TokenFor(spawn.EnemyId);
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.12f;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            transform.localScale = ScaleFor(spawn.EnemyId);
        }

        public string ToCanonicalString()
        {
            return "order=" + order.ToString(CultureInfo.InvariantCulture)
                + "|fixture_id=" + fixtureId
                + "|actor_id=" + actorId
                + "|enemy_id=" + enemyId
                + "|marker_id=" + markerId
                + "|active=" + gameObject.activeSelf.ToString().ToLowerInvariant();
        }

        private static string TokenFor(StableId enemyId)
        {
            if (enemyId.Equals(Stage1EnemyPackageDescriptor.PursuerDroneId))
            {
                return "[P]";
            }

            if (enemyId.Equals(Stage1EnemyPackageDescriptor.RamDroidId))
            {
                return "[R]";
            }

            if (enemyId.Equals(Stage1EnemyPackageDescriptor.MobileBlasterDroidId))
            {
                return "[M]";
            }

            if (enemyId.Equals(Stage1EnemyPackageDescriptor.BlasterTurretId))
            {
                return "[T]";
            }

            return "[4B]";
        }

        private static Vector3 ScaleFor(StableId enemyId)
        {
            if (enemyId.Equals(Stage1EnemyPackageDescriptor.FourBlasterEliteId))
            {
                return new Vector3(1.8f, 1.8f, 1f);
            }

            if (enemyId.Equals(Stage1EnemyPackageDescriptor.RamDroidId))
            {
                return new Vector3(1.2f, 0.9f, 1f);
            }

            return Vector3.one;
        }
    }

    [DisallowMultipleComponent]
    public sealed class Stage1BenchmarkEnemyEncounterArenaLoader : MonoBehaviour
    {
        public const string RuntimeRootName = "__EN010EncounterRuntime";
        public const string ArenaScenePath =
            "Assets/ShooterMover/Tests/PlayMode/EvidenceHarness/Scenes/"
            + "Stage1BenchmarkArena.unity";

        private static readonly Dictionary<string, Vector3> MarkerPositions =
            new Dictionary<string, Vector3>(StringComparer.Ordinal)
            {
                { "socket.target.north", new Vector3(0f, 4.5f, 0f) },
                { "socket.target.east", new Vector3(8f, 0f, 0f) },
                { "socket.target.south", new Vector3(0f, -2f, 0f) },
                { "socket.target.west", new Vector3(-8f, 0f, 0f) },
            };

        private readonly List<Stage1BenchmarkEnemyProjection> projections =
            new List<Stage1BenchmarkEnemyProjection>();
        private Stage1BenchmarkEnemyEncounterSession session;
        private GameObject runtimeRoot;
        private bool showSelector = true;

        public string CurrentFixtureId
        {
            get
            {
                return session == null
                    ? string.Empty
                    : session.Fixture.FixtureId.ToString();
            }
        }

        public int ProjectedActorCount
        {
            get { return projections.Count; }
        }

        public Stage1BenchmarkEnemyEncounterSession CurrentSession
        {
            get { return session; }
        }

        public bool ShowSelector
        {
            get { return showSelector; }
            set { showSelector = value; }
        }

        public static Stage1BenchmarkEnemyEncounterArenaLoader AttachToLoadedArena()
        {
            if (!Stage1BenchmarkArenaFixture.IsLoaded)
            {
                throw new InvalidOperationException(
                    "The Stage1BenchmarkArena must be loaded before EN-010 attaches.");
            }

            ValidateLoadedArenaMarkers();
            Scene arenaScene =
                SceneManager.GetSceneByName(Stage1BenchmarkArenaFixture.SceneName);
            if (!arenaScene.IsValid() || !arenaScene.isLoaded)
            {
                throw new InvalidOperationException(
                    "The loaded benchmark arena scene could not be resolved.");
            }

            GameObject[] roots = arenaScene.GetRootGameObjects();
            Stage1BenchmarkArenaFixture arena = null;
            for (int index = 0; index < roots.Length; index++)
            {
                arena = roots[index].GetComponent<Stage1BenchmarkArenaFixture>();
                if (arena != null)
                {
                    break;
                }
            }

            if (arena == null)
            {
                throw new InvalidOperationException(
                    "The loaded arena has no Stage1BenchmarkArenaFixture root.");
            }

            Stage1BenchmarkEnemyEncounterArenaLoader loader =
                arena.GetComponent<Stage1BenchmarkEnemyEncounterArenaLoader>();
            if (loader == null)
            {
                loader =
                    arena.gameObject.AddComponent<Stage1BenchmarkEnemyEncounterArenaLoader>();
            }

            if (loader.session == null)
            {
                loader.SelectFixture(
                    Stage1BenchmarkEnemyEncounterCatalog.DefaultFixtureIdText);
            }

            return loader;
        }

        public void SelectFixture(string fixtureId)
        {
            ClearProjectedActors();
            session = Stage1BenchmarkEnemyEncounterCatalog.Approved.CreateSession(
                fixtureId,
                "run.en010-stage1-benchmark");
            ProjectCurrentFixture();
        }

        public bool ReplayCurrentFixture()
        {
            if (session == null)
            {
                throw new InvalidOperationException(
                    "Select a benchmark fixture before replaying it.");
            }

            ClearProjectedActors();
            session.Restart();
            ProjectCurrentFixture();
            return true;
        }

        public Stage1BenchmarkResolutionDisposition ResolveProjectedActor(string actorId)
        {
            if (session == null)
            {
                throw new InvalidOperationException(
                    "Select a benchmark fixture before resolving an actor.");
            }

            Stage1BenchmarkResolutionDisposition result =
                session.ResolveDestroyed(actorId);
            if (result == Stage1BenchmarkResolutionDisposition.Applied)
            {
                for (int index = 0; index < projections.Count; index++)
                {
                    if (string.Equals(
                            projections[index].ActorId,
                            actorId,
                            StringComparison.Ordinal))
                    {
                        projections[index].gameObject.SetActive(false);
                        break;
                    }
                }
            }

            return result;
        }

        public string CaptureDeterministicSnapshot()
        {
            if (session == null)
            {
                return "schema=shooter-mover.stage1-benchmark-loader\nversion=1\nstate=empty";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("schema=shooter-mover.stage1-benchmark-loader")
                .Append("\nversion=1\n")
                .Append(session.CaptureDeterministicSnapshot())
                .Append("\nprojected_actor_count=")
                .Append(projections.Count.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < projections.Count; index++)
            {
                builder.Append("\nprojection_")
                    .Append(index.ToString("D2", CultureInfo.InvariantCulture))
                    .Append('=')
                    .Append(projections[index].ToCanonicalString());
            }

            return builder.ToString();
        }

        private static void ValidateLoadedArenaMarkers()
        {
            HashSet<string> available = new HashSet<string>(
                Stage1BenchmarkArenaFixture.GetMarkerIds(),
                StringComparer.Ordinal);
            foreach (string markerId in MarkerPositions.Keys)
            {
                if (!available.Contains(markerId))
                {
                    throw new KeyNotFoundException(
                        "The unmodified arena is missing required marker ID: " + markerId);
                }
            }
        }

        private void ProjectCurrentFixture()
        {
            runtimeRoot = new GameObject(RuntimeRootName);
            runtimeRoot.transform.SetParent(transform, false);

            IReadOnlyList<Stage1BenchmarkEnemySpawnDefinition> spawns =
                session.Fixture.Spawns;
            for (int index = 0; index < spawns.Count; index++)
            {
                Stage1BenchmarkEnemySpawnDefinition spawn = spawns[index];
                Vector3 position;
                if (!MarkerPositions.TryGetValue(spawn.MarkerId, out position))
                {
                    throw new KeyNotFoundException(
                        "Missing accepted marker position: " + spawn.MarkerId);
                }

                GameObject projected = new GameObject(
                    spawn.Order.ToString("D2", CultureInfo.InvariantCulture)
                    + " "
                    + spawn.EnemyId);
                projected.transform.SetParent(runtimeRoot.transform, false);
                projected.transform.localPosition = position;
                Stage1BenchmarkEnemyProjection projection =
                    projected.AddComponent<Stage1BenchmarkEnemyProjection>();
                projection.Configure(session.Fixture, spawn);
                projections.Add(projection);
            }
        }

        private void ClearProjectedActors()
        {
            projections.Clear();
            if (runtimeRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeRoot);
            }
            else
            {
                DestroyImmediate(runtimeRoot);
            }

            runtimeRoot = null;
        }

        private void OnEnable()
        {
            if (Application.isPlaying && session == null)
            {
                SelectFixture(
                    Stage1BenchmarkEnemyEncounterCatalog.DefaultFixtureIdText);
            }
        }

        private void OnDestroy()
        {
            ClearProjectedActors();
        }

        private void OnGUI()
        {
            if (!showSelector)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 390f, 440f), GUI.skin.box);
            GUILayout.Label("EN-010 Stage 1 benchmark encounters");
            GUILayout.Label("Controlled verification fixtures — not pacing approval");

            IReadOnlyList<Stage1BenchmarkEnemyEncounterDefinition> fixtures =
                Stage1BenchmarkEnemyEncounterCatalog.Approved.FixedFixtures;
            for (int index = 0; index < fixtures.Count; index++)
            {
                Stage1BenchmarkEnemyEncounterDefinition fixture = fixtures[index];
                if (GUILayout.Button(
                        (index + 1).ToString(CultureInfo.InvariantCulture)
                        + ". "
                        + fixture.DisplayName))
                {
                    SelectFixture(fixture.FixtureId.ToString());
                }
            }

            GUI.enabled = session != null;
            if (GUILayout.Button("Replay selected fixture"))
            {
                ReplayCurrentFixture();
            }

            GUI.enabled = true;
            if (session != null)
            {
                GUILayout.Label("Selected: " + session.Fixture.FixtureId);
                GUILayout.Label(
                    "Active: "
                    + session.CurrentLifecycle.ActiveParticipantCount.ToString(
                        CultureInfo.InvariantCulture)
                    + " / "
                    + session.Fixture.Budget.MaximumConcurrentParticipants.ToString(
                        CultureInfo.InvariantCulture));
            }

            GUILayout.EndArea();
        }
    }
}
