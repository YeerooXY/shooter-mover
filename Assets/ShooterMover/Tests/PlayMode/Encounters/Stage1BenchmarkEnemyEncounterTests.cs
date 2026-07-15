#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using ShooterMover.Contracts.Encounters;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Encounters
{
    public sealed class Stage1BenchmarkEnemyEncounterTests
    {
        private const string PackageSourcePath =
            "Assets/ShooterMover/ContentPackages/Encounters/Stage1Benchmark/"
            + "Stage1BenchmarkEnemyEncounters.cs";
        private const string PackageNotePath =
            "Assets/ShooterMover/ContentPackages/Encounters/Stage1Benchmark/"
            + "STAGE1_BENCHMARK_ENCOUNTERS_V1.md";
        private const string ArenaScenePath =
            "Assets/ShooterMover/Tests/PlayMode/EvidenceHarness/Scenes/"
            + "Stage1BenchmarkArena.unity";

        private readonly List<UnityEngine.Object> createdObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void FixtureMatrix_CoversEveryRoleMixedPressureAndStandaloneElite()
        {
            object catalog = ApprovedCatalog();
            List<object> fixtures = Objects(GetProperty<object>(catalog, "FixedFixtures"));

            Assert.That(fixtures, Has.Count.EqualTo(7));
            Assert.That(
                fixtures.Select(fixture => GetProperty<StableId>(fixture, "FixtureId"))
                    .Distinct()
                    .Count(),
                Is.EqualTo(7));

            List<object> isolated = fixtures
                .Where(fixture => Objects(GetProperty<object>(fixture, "Spawns")).Count == 1)
                .ToList();
            Assert.That(isolated, Has.Count.EqualTo(5));
            Assert.That(
                isolated.Select(SingleEnemyId).OrderBy(id => id, StringComparer.Ordinal),
                Is.EqualTo(
                    new[]
                    {
                        "enemy.blaster-turret",
                        "enemy.four-blaster-elite",
                        "enemy.mobile-blaster-droid",
                        "enemy.pursuer-drone",
                        "enemy.ram-droid",
                    }));

            List<object> mixed = fixtures
                .Where(
                    fixture => string.Equals(
                        GetProperty<object>(fixture, "Kind").ToString(),
                        "MixedPressure",
                        StringComparison.Ordinal))
                .ToList();
            Assert.That(mixed, Has.Count.EqualTo(2));
            Assert.That(
                mixed.Select(fixture => Objects(GetProperty<object>(fixture, "Spawns")).Count)
                    .OrderBy(count => count),
                Is.EqualTo(new[] { 2, 3 }));
            Assert.That(
                mixed.All(
                    fixture => Objects(GetProperty<object>(fixture, "Spawns"))
                        .Select(spawn => GetProperty<StableId>(spawn, "EnemyId").ToString())
                        .Distinct(StringComparer.Ordinal)
                        .Count() >= 2),
                Is.True);

            object elite = fixtures.Single(
                fixture => string.Equals(
                    GetProperty<object>(fixture, "Kind").ToString(),
                    "Elite",
                    StringComparison.Ordinal));
            Assert.That(GetProperty<bool>(elite, "IsStandaloneElite"), Is.True);
            Assert.That(SingleEnemyId(elite), Is.EqualTo("enemy.four-blaster-elite"));

            string[] loadouts = fixtures
                .Select(fixture => GetProperty<StableId>(fixture, "LoadoutFixtureId").ToString())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            Assert.That(
                loadouts,
                Is.EqualTo(
                    new[]
                    {
                        "loadout.stage1-default-comparison",
                        "loadout.stage1-ricochet-comparison",
                    }));

            string matrix = (string)InvokeInstance(catalog, "CaptureFixtureMatrix");
            Assert.That(matrix, Does.Contain("fixture_count=7"));
            Assert.That(matrix, Does.Contain("encounter.stage1-benchmark-close-pressure"));
            Assert.That(matrix, Does.Contain("encounter.stage1-benchmark-crossfire"));
            Assert.That(matrix, Does.Contain("encounter.stage1-benchmark-four-blaster-elite"));
            TestContext.Progress.WriteLine(matrix);
        }

        [Test]
        public void Loader_SpawnsInStableOrderAndReplayRestoresByteEqualState()
        {
            GameObject host = new GameObject("EN-010 loader test host");
            createdObjects.Add(host);
            Component loader = host.AddComponent(RuntimeTypes.Loader);
            InvokeInstance(
                loader,
                "SelectFixture",
                "encounter.stage1-benchmark-crossfire");

            Assert.That(GetProperty<int>(loader, "ProjectedActorCount"), Is.EqualTo(3));
            object session = GetProperty<object>(loader, "CurrentSession");
            EncounterStartMessage start =
                GetProperty<EncounterStartMessage>(session, "StartMessage");
            Assert.That(
                start.Entries.Select(entry => entry.Order),
                Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(
                start.Entries.Select(entry => entry.RoleId.ToString()),
                Is.EqualTo(
                    new[]
                    {
                        "enemy.blaster-turret",
                        "enemy.mobile-blaster-droid",
                        "enemy.pursuer-drone",
                    }));

            string initial = (string)InvokeInstance(loader, "CaptureDeterministicSnapshot");
            string firstActorId = start.Entries[0].ActorId.ToString();
            Assert.That(
                InvokeInstance(loader, "ResolveProjectedActor", firstActorId).ToString(),
                Is.EqualTo("Applied"));
            Assert.That(
                GetProperty<EncounterLifecycle>(session, "CurrentLifecycle")
                    .ActiveParticipantCount,
                Is.EqualTo(2));

            Assert.That((bool)InvokeInstance(loader, "ReplayCurrentFixture"), Is.True);
            string replayed = (string)InvokeInstance(loader, "CaptureDeterministicSnapshot");

            Assert.That(replayed, Is.EqualTo(initial));
            Assert.That(GetProperty<int>(loader, "ProjectedActorCount"), Is.EqualTo(3));
            Assert.That(
                GetProperty<EncounterLifecycle>(
                    GetProperty<object>(loader, "CurrentSession"),
                    "CurrentLifecycle").ActiveParticipantCount,
                Is.EqualTo(3));

            TestContext.Progress.WriteLine("spawn_order=0,1,2");
            TestContext.Progress.WriteLine("replay_snapshot_byte_equal=true");
        }

        [Test]
        public void Completion_IsEmittedOnceAndRestartClearsResolvedState()
        {
            object catalog = ApprovedCatalog();
            object session = InvokeInstance(
                catalog,
                "CreateSession",
                "encounter.stage1-benchmark-four-blaster-elite",
                "run.en010-completion-test");
            string initial = (string)InvokeInstance(session, "CaptureDeterministicSnapshot");
            object fixture = GetProperty<object>(session, "Fixture");
            object spawn = Objects(GetProperty<object>(fixture, "Spawns")).Single();
            string actorId = GetProperty<StableId>(spawn, "ActorId").ToString();

            object first = InvokeInstance(session, "ResolveDestroyed", actorId);
            object repeated = InvokeInstance(session, "ResolveDestroyed", actorId);

            Assert.That(first.ToString(), Is.EqualTo("Applied"));
            Assert.That(repeated.ToString(), Is.EqualTo("NoChange"));
            Assert.That(GetProperty<int>(session, "CompletionCount"), Is.EqualTo(1));
            Assert.That(
                GetProperty<EncounterLifecycle>(session, "CurrentLifecycle").IsCompleted,
                Is.True);
            Assert.That(
                GetProperty<EncounterLifecycle>(session, "CurrentLifecycle")
                    .ActiveParticipantCount,
                Is.Zero);

            Assert.That((bool)InvokeInstance(session, "Restart"), Is.True);
            Assert.That(
                (string)InvokeInstance(session, "CaptureDeterministicSnapshot"),
                Is.EqualTo(initial));
            Assert.That(GetProperty<int>(session, "CompletionCount"), Is.Zero);

            Assert.That(
                InvokeInstance(session, "ResolveDestroyed", actorId).ToString(),
                Is.EqualTo("Applied"));
            Assert.That(GetProperty<int>(session, "CompletionCount"), Is.EqualTo(1));

            TestContext.Progress.WriteLine("boss_completion_count=1");
            TestContext.Progress.WriteLine("boss_duplicate_resolution=no-change");
            TestContext.Progress.WriteLine("restart_clears_completion=true");
        }

        [Test]
        public void CountBudgets_AreBoundedAndMissingIdsFailVisibly()
        {
            object catalog = ApprovedCatalog();
            List<object> fixtures = Objects(GetProperty<object>(catalog, "FixedFixtures"));

            foreach (object fixture in fixtures)
            {
                string fixtureId = GetProperty<StableId>(fixture, "FixtureId").ToString();
                object session = InvokeInstance(
                    catalog,
                    "CreateSession",
                    fixtureId,
                    "run.en010-budget-test");
                EncounterBudgetEvaluation evaluation =
                    (EncounterBudgetEvaluation)InvokeInstance(
                        session,
                        "EvaluateBudget",
                        32,
                        16.667d);
                EncounterPerformanceBudget budget =
                    GetProperty<EncounterPerformanceBudget>(fixture, "Budget");
                int spawnCount = Objects(GetProperty<object>(fixture, "Spawns")).Count;

                Assert.That(evaluation.IsWithinBudget, Is.True, fixtureId);
                Assert.That(spawnCount, Is.LessThanOrEqualTo(4), fixtureId);
                Assert.That(
                    spawnCount,
                    Is.LessThanOrEqualTo(budget.MaximumConcurrentParticipants),
                    fixtureId);

                EncounterLifecycle lifecycle =
                    GetProperty<EncounterLifecycle>(session, "CurrentLifecycle");
                EncounterBudgetEvaluation overCount = EncounterBudgetEvaluation.Evaluate(
                    budget,
                    new EncounterBudgetSample(
                        lifecycle.Identity,
                        StableId.Parse("sample.en010-over-count"),
                        budget.MaximumConcurrentParticipants + 1,
                        0,
                        0,
                        0d));
                Assert.That(overCount.IsWithinBudget, Is.False, fixtureId);
                Assert.That(
                    overCount.Violations,
                    Does.Contain(
                        EncounterBudgetViolation.ConcurrentParticipantsExceeded),
                    fixtureId);
            }

            Assert.Throws<KeyNotFoundException>(
                () => InvokeInstance(
                    catalog,
                    "GetFixture",
                    "encounter.stage1-benchmark-missing"));
            Assert.Throws<KeyNotFoundException>(
                () => InvokeInstance(
                    catalog,
                    "ResolveEnemyId",
                    "enemy.missing-package"));

            GameObject host = new GameObject("EN-010 missing fixture host");
            createdObjects.Add(host);
            Component loader = host.AddComponent(RuntimeTypes.Loader);
            Assert.Throws<KeyNotFoundException>(
                () => InvokeInstance(
                    loader,
                    "SelectFixture",
                    "encounter.stage1-benchmark-missing"));

            TestContext.Progress.WriteLine("maximum_fixture_actor_count=3");
            TestContext.Progress.WriteLine("missing_fixture_id=rejected");
            TestContext.Progress.WriteLine("missing_enemy_id=rejected");
        }

        [Test]
        public void ArenaScene_RemainsReadOnlyAndLoaderUsesAcceptedMarkerContract()
        {
            string packageSource = ReadProjectFile(PackageSourcePath);
            string packageNote = ReadProjectFile(PackageNotePath);
            string arenaSource = ReadProjectFile(ArenaScenePath);

            Assert.That(arenaSource, Does.Not.Contain("__EN010EncounterRuntime"));
            Assert.That(
                arenaSource,
                Does.Not.Contain("Stage1BenchmarkEnemyEncounterArenaLoader"));
            Assert.That(packageSource, Does.Contain("Stage1BenchmarkArenaFixture.GetMarkerIds()"));
            Assert.That(packageSource, Does.Contain("SceneManager.GetSceneByName"));
            Assert.That(packageSource, Does.Not.Contain("EditorSceneManager"));
            Assert.That(packageSource, Does.Not.Contain("AssetDatabase"));
            Assert.That(packageSource, Does.Not.Contain("SaveScene"));
            Assert.That(packageSource, Does.Not.Contain("File.Write"));
            Assert.That(packageNote, Does.Contain("No pacing or quality acceptance"));
            Assert.That(packageNote, Does.Contain("read-only"));

            string arenaSha256 = ComputeSha256(ToProjectPath(ArenaScenePath));
            TestContext.Progress.WriteLine("arena_scene_sha256=" + arenaSha256);
            TestContext.Progress.WriteLine("arena_scene_edit=false");
            TestContext.Progress.WriteLine(
                "runtime_projection_only=true; scene serialization remains EH-004-owned");
        }

        private static object ApprovedCatalog()
        {
            return GetStaticProperty<object>(RuntimeTypes.Catalog, "Approved");
        }

        private static string SingleEnemyId(object fixture)
        {
            object spawn = Objects(GetProperty<object>(fixture, "Spawns")).Single();
            return GetProperty<StableId>(spawn, "EnemyId").ToString();
        }

        private static List<object> Objects(object enumerable)
        {
            var result = new List<object>();
            foreach (object item in (IEnumerable)enumerable)
            {
                result.Add(item);
            }

            return result;
        }

        private static T GetStaticProperty<T>(Type type, string propertyName)
        {
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(property, Is.Not.Null, type.FullName + "." + propertyName);
            return (T)property.GetValue(null, null);
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(
                property,
                Is.Not.Null,
                instance.GetType().FullName + "." + propertyName);
            return (T)property.GetValue(instance, null);
        }

        private static object InvokeInstance(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo[] matches = instance.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(
                    method => string.Equals(
                        method.Name,
                        methodName,
                        StringComparison.Ordinal))
                .Where(method => ParametersMatch(method.GetParameters(), arguments))
                .ToArray();
            Assert.That(
                matches,
                Has.Length.EqualTo(1),
                instance.GetType().FullName
                    + "."
                    + methodName
                    + " with "
                    + arguments.Length.ToString(CultureInfo.InvariantCulture)
                    + " arguments");

            try
            {
                return matches[0].Invoke(instance, arguments);
            }
            catch (TargetInvocationException exception)
            {
                if (exception.InnerException != null)
                {
                    throw exception.InnerException;
                }

                throw;
            }
        }


        private static bool ParametersMatch(
            ParameterInfo[] parameters,
            object[] arguments)
        {
            if (parameters.Length != arguments.Length)
            {
                return false;
            }

            for (int index = 0; index < parameters.Length; index++)
            {
                if (arguments[index] == null)
                {
                    if (parameters[index].ParameterType.IsValueType)
                    {
                        return false;
                    }

                    continue;
                }

                if (!parameters[index].ParameterType.IsInstanceOfType(arguments[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static string ReadProjectFile(string assetPath)
        {
            return File.ReadAllText(ToProjectPath(assetPath));
        }

        private static string ToProjectPath(string assetPath)
        {
            string projectRoot =
                Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static class RuntimeTypes
        {
            public static readonly Type Catalog = RequireRuntimeType(
                "ShooterMover.ContentPackages.Encounters.Stage1Benchmark."
                + "Stage1BenchmarkEnemyEncounterCatalog");
            public static readonly Type Loader = RequireRuntimeType(
                "ShooterMover.ContentPackages.Encounters.Stage1Benchmark."
                + "Stage1BenchmarkEnemyEncounterArenaLoader");

            private static Type RequireRuntimeType(string fullName)
            {
                Type[] matches = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(fullName, false))
                    .Where(type => type != null)
                    .ToArray();
                Assert.That(matches, Has.Length.EqualTo(1), fullName);
                return matches[0];
            }
        }
    }
}
#endif
