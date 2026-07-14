#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using ShooterMover.Bootstrap.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.EvidenceHarness
{
    public sealed class Stage1BenchmarkArenaSmokeTests
    {
        private const string BootstrapSceneName = "Bootstrap";
        private const string BootstrapScenePath =
            "Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity";
        private const string ArenaSceneName = "Stage1BenchmarkArena";
        private const string ArenaScenePath =
            "Assets/ShooterMover/Tests/PlayMode/EvidenceHarness/Scenes/Stage1BenchmarkArena.unity";
        private const string FixtureTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.Stage1BenchmarkArenaFixture";
        private const float SceneOperationTimeoutSeconds = 30f;

        private const string CanonicalConfiguration =
            "{\n"
            + "  \"schema\": \"shooter-mover.evidence-run-configuration\",\n"
            + "  \"version\": 1,\n"
            + "  \"runSeed\": 104729,\n"
            + "  \"identityReference\": \"sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\n"
            + "  \"intentFixtureVersion\": 1,\n"
            + "  \"qualityProfile\": \"Medium\",\n"
            + "  \"locale\": \"en-US\",\n"
            + "  \"viewport\": {\n"
            + "    \"width\": 1280,\n"
            + "    \"height\": 720,\n"
            + "    \"fullscreen\": false\n"
            + "  },\n"
            + "  \"diagnostics\": {\n"
            + "    \"maxEventCount\": 4096,\n"
            + "    \"maxEventPayloadBytes\": 4096,\n"
            + "    \"maxLogBytes\": 8388608,\n"
            + "    \"retainedLogCount\": 3\n"
            + "  },\n"
            + "  \"timeouts\": {\n"
            + "    \"setupSeconds\": 30,\n"
            + "    \"smokeRunSeconds\": 120,\n"
            + "    \"shutdownSeconds\": 15\n"
            + "  }\n"
            + "}\n";

        private static readonly string[] ExpectedMarkerIds =
        {
            "arena.shell.v1",
            "bounds.camera",
            "bounds.collision",
            "hook.combat.cleanup",
            "hook.combat.spawn",
            "probe.performance.center",
            "probe.performance.north",
            "probe.performance.south",
            "socket.hazard.northwest",
            "socket.hazard.southeast",
            "socket.player.primary",
            "socket.target.east",
            "socket.target.north",
            "socket.target.south",
            "socket.target.west"
        };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadBootstrapSingle("EH-004 setup");
            yield return null;
            RequireSingleRunningBootstrap();
            AssertArenaUnloaded("before EH-004 setup completed");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GetFixtureProperty<bool>("IsOperationInFlight"))
            {
                yield return WaitForFixtureOperationToFinish("EH-004 teardown");
            }

            if (GetFixtureProperty<bool>("IsLoaded"))
            {
                AsyncOperation unload = InvokeFixture("Unload") as AsyncOperation;
                yield return WaitForOperation(unload, "EH-004 teardown unload");
                yield return null;
            }

            yield return LoadBootstrapSingle("EH-004 teardown bootstrap restore");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CanonicalConfiguration_LoadsAdditivelyAndReturnsToBootstrapBaseline()
        {
            Scene bootstrap = RequireBootstrapScene();
            string[] baselineHierarchy = DescribeSceneHierarchy(bootstrap);
            int baselineObjectCount = CountLoadedSceneGameObjects();

            yield return LoadArena();

            Assert.That(GetFixtureProperty<bool>("IsLoaded"), Is.True);
            Assert.That(GetFixtureProperty<bool>("IsOperationInFlight"), Is.False);
            Assert.That(GetFixtureProperty<int>("ActiveInstanceCount"), Is.EqualTo(1));
            Assert.That(GetFixtureProperty<int>("ResolvedRunSeed"), Is.EqualTo(104729));
            Assert.That(
                GetFixtureProperty<string>("ResolvedConfigurationFingerprint"),
                Does.StartWith("sha256:"));

            string[] validation = InvokeFixture("ValidateActiveArena") as string[];
            Assert.That(validation, Is.Not.Null.And.Empty, DescribeValidation(validation));

            string snapshot = InvokeFixture("CaptureActiveSnapshot") as string;
            Assert.That(snapshot, Does.Contain("schema=shooter-mover.stage1-benchmark-arena-snapshot"));
            Assert.That(snapshot, Does.Contain("marker|socket.player.primary|PlayerSpawn"));
            Assert.That(snapshot, Does.Contain("marker|hook.combat.cleanup|CombatHook"));
            Assert.That(snapshot, Does.Contain("|sortingOrder=-100"));
            Assert.That(snapshot, Does.Contain("|sortingOrder=50"));

            AsyncOperation unload = InvokeFixture("Unload") as AsyncOperation;
            yield return WaitForOperation(unload, "EH-004 additive unload");
            yield return null;

            AssertArenaUnloaded("after EH-004 additive unload");
            Scene bootstrapAfter = RequireBootstrapScene();
            Assert.That(
                SceneManager.GetActiveScene().handle,
                Is.EqualTo(bootstrapAfter.handle),
                "Bootstrap was not retained as the active scene after arena unload.");
            Assert.That(
                DescribeSceneHierarchy(bootstrapAfter),
                Is.EqualTo(baselineHierarchy),
                "Bootstrap hierarchy changed while the arena was loaded additively.");
            Assert.That(
                CountLoadedSceneGameObjects(),
                Is.EqualTo(baselineObjectCount),
                "Scene-owned object count did not return to the Bootstrap baseline.");
        }

        [UnityTest]
        public IEnumerator TwoResets_AreByteEqualAndRepairTransformAndSocketDrift()
        {
            yield return LoadArena();

            string baseline = InvokeFixture("ResetActiveArena") as string;
            Assert.That(baseline, Is.Not.Null.And.Not.Empty);

            InvokeFixture(
                "SetMarkerLocalPositionForTest",
                "socket.target.east",
                99f,
                -17f,
                0f);
            InvokeFixture(
                "SetMarkerActiveForTest",
                "socket.hazard.northwest",
                false);

            string[] driftErrors = InvokeFixture("ValidateActiveArena") as string[];
            Assert.That(
                driftErrors,
                Does.Contain("missing-socket:socket.hazard.northwest"),
                DescribeValidation(driftErrors));

            string firstReset = InvokeFixture("ResetActiveArena") as string;
            string secondReset = InvokeFixture("ResetActiveArena") as string;

            Assert.That(firstReset, Is.EqualTo(baseline));
            Assert.That(secondReset, Is.EqualTo(firstReset));
            Assert.That(
                InvokeFixture("ValidateActiveArena") as string[],
                Is.Empty,
                "A reset left the arena invalid.");
        }

        [UnityTest]
        public IEnumerator UnloadAndReload_ProduceTheSameHierarchyMarkerAndTransformSnapshot()
        {
            yield return LoadArena();
            string first = InvokeFixture("ResetActiveArena") as string;

            AsyncOperation unload = InvokeFixture("Unload") as AsyncOperation;
            yield return WaitForOperation(unload, "EH-004 equality unload");
            yield return null;
            AssertArenaUnloaded("between equality loads");

            yield return LoadArena();
            string second = InvokeFixture("ResetActiveArena") as string;

            Assert.That(second, Is.EqualTo(first));
        }

        [UnityTest]
        public IEnumerator MarkerIdsBoundsAndComponents_DescribeOneFully2DShell()
        {
            yield return LoadArena();

            string[] markerIds = InvokeFixture("GetMarkerIds") as string[];
            Assert.That(markerIds, Is.EqualTo(ExpectedMarkerIds));
            Assert.That(markerIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(markerIds.Length));

            Assert.That(
                GetFixtureProperty<string>("CameraBoundsSummary"),
                Is.EqualTo("center=0,0,0;size=24,14,0"));
            Assert.That(
                GetFixtureProperty<string>("CollisionBoundsSummary"),
                Is.EqualTo("min=-12.5,-7.5,0;max=12.5,7.5,0;walls=4"));

            string[] validation = InvokeFixture("ValidateActiveArena") as string[];
            Assert.That(validation, Is.Empty, DescribeValidation(validation));

            Scene arena = RequireArenaScene();
            GameObject[] roots = arena.GetRootGameObjects();
            Assert.That(roots, Has.Length.EqualTo(1));
            Assert.That(roots[0].name, Is.EqualTo("Stage1 Benchmark Arena"));

            Component[] components = roots[0].GetComponentsInChildren<Component>(true);
            Assert.That(components.Any(component => component is Camera), Is.False);
            Assert.That(components.Any(component => component is Collider), Is.False);
            Assert.That(components.Any(component => component is Rigidbody), Is.False);
            Assert.That(components.Any(component => component is Joint), Is.False);
            Assert.That(components.OfType<BoxCollider2D>().Count(), Is.EqualTo(5));
            Assert.That(components.OfType<Rigidbody2D>(), Is.Empty);

            Transform[] transforms = roots[0].GetComponentsInChildren<Transform>(true);
            Assert.That(
                transforms.All(transform => Mathf.Abs(transform.position.z) <= 0.0001f),
                Is.True,
                "At least one authored or fixture-owned transform left the XY plane.");
        }

        [UnityTest]
        public IEnumerator InFlightAndLoadedDuplicateRequests_AreRejectedAndCleanedUp()
        {
            int baselineObjectCount = CountLoadedSceneGameObjects();

            AsyncOperation load = InvokeFixture(
                "LoadFromCanonicalConfiguration",
                CanonicalConfiguration) as AsyncOperation;
            Assert.That(load, Is.Not.Null);

            Exception inFlight = InvokeFixtureExpectingFailure(
                "LoadFromCanonicalConfiguration",
                CanonicalConfiguration);
            Assert.That(inFlight, Is.TypeOf<InvalidOperationException>());
            Assert.That(inFlight.Message, Does.Contain("already in flight"));

            yield return WaitForOperation(load, "EH-004 duplicate-request load");
            yield return null;

            Exception loaded = InvokeFixtureExpectingFailure(
                "LoadFromCanonicalConfiguration",
                CanonicalConfiguration);
            Assert.That(loaded, Is.TypeOf<InvalidOperationException>());
            Assert.That(loaded.Message, Does.Contain("already loaded"));

            AsyncOperation unload = InvokeFixture("Unload") as AsyncOperation;
            yield return WaitForOperation(unload, "EH-004 duplicate-request cleanup");
            yield return null;

            AssertArenaUnloaded("after duplicate-request cleanup");
            Assert.That(
                CountLoadedSceneGameObjects(),
                Is.EqualTo(baselineObjectCount),
                "Duplicate-request coverage retained arena objects.");
        }

        private static IEnumerator LoadArena()
        {
            AsyncOperation load = InvokeFixture(
                "LoadFromCanonicalConfiguration",
                CanonicalConfiguration) as AsyncOperation;
            yield return WaitForOperation(load, "EH-004 canonical additive load");
            yield return null;

            RequireArenaScene();
            Assert.That(GetFixtureProperty<int>("ActiveInstanceCount"), Is.EqualTo(1));
        }

        private static IEnumerator LoadBootstrapSingle(string context)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(
                BootstrapSceneName,
                LoadSceneMode.Single);
            yield return WaitForOperation(operation, context);
        }

        private static IEnumerator WaitForOperation(
            AsyncOperation operation,
            string description)
        {
            Assert.That(
                operation,
                Is.Not.Null,
                "Unity did not create the requested operation for " + description + ".");

            float startedAt = Time.realtimeSinceStartup;
            while (!operation.isDone)
            {
                if (Time.realtimeSinceStartup - startedAt > SceneOperationTimeoutSeconds)
                {
                    Assert.Fail(
                        description + " did not complete within "
                        + SceneOperationTimeoutSeconds + " seconds. "
                        + DescribeLoadedScenes());
                }

                yield return null;
            }
        }

        private static IEnumerator WaitForFixtureOperationToFinish(string description)
        {
            float startedAt = Time.realtimeSinceStartup;
            while (GetFixtureProperty<bool>("IsOperationInFlight"))
            {
                if (Time.realtimeSinceStartup - startedAt > SceneOperationTimeoutSeconds)
                {
                    Assert.Fail(
                        description + " retained an operation for more than "
                        + SceneOperationTimeoutSeconds + " seconds.");
                }

                yield return null;
            }
        }

        private static object InvokeFixture(string methodName, params object[] arguments)
        {
            MethodInfo method = RequireFixtureMethod(methodName, arguments);
            try
            {
                return method.Invoke(null, arguments);
            }
            catch (TargetInvocationException exception)
            {
                Exception inner = exception.InnerException ?? exception;
                ExceptionDispatchInfo.Capture(inner).Throw();
                throw;
            }
        }

        private static Exception InvokeFixtureExpectingFailure(
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = RequireFixtureMethod(methodName, arguments);
            try
            {
                method.Invoke(null, arguments);
            }
            catch (TargetInvocationException exception)
            {
                return exception.InnerException ?? exception;
            }

            Assert.Fail(
                FixtureTypeName + "." + methodName
                + " was expected to reject the request, but it succeeded.");
            return null;
        }

        private static MethodInfo RequireFixtureMethod(
            string methodName,
            object[] arguments)
        {
            Type fixtureType = ResolveFixtureType();
            Type[] argumentTypes = arguments
                .Select(argument => argument == null ? typeof(object) : argument.GetType())
                .ToArray();

            MethodInfo method = fixtureType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .SingleOrDefault(
                    candidate => candidate.Name == methodName
                        && ParametersMatch(candidate.GetParameters(), argumentTypes));
            if (method == null)
            {
                Assert.Fail(
                    "Missing public static fixture method " + FixtureTypeName
                    + "." + methodName + " for arguments ["
                    + string.Join(", ", argumentTypes.Select(type => type.FullName))
                    + "].");
            }

            return method;
        }

        private static bool ParametersMatch(
            ParameterInfo[] parameters,
            Type[] argumentTypes)
        {
            if (parameters.Length != argumentTypes.Length)
            {
                return false;
            }

            for (int index = 0; index < parameters.Length; index++)
            {
                if (!parameters[index].ParameterType.IsAssignableFrom(argumentTypes[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static T GetFixtureProperty<T>(string propertyName)
        {
            PropertyInfo property = ResolveFixtureType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Static);
            if (property == null)
            {
                Assert.Fail(
                    "Missing public static fixture property " + FixtureTypeName
                    + "." + propertyName + ".");
            }

            object value = property.GetValue(null, null);
            if (!(value is T))
            {
                Assert.Fail(
                    FixtureTypeName + "." + propertyName + " returned "
                    + (value == null ? "null" : value.GetType().FullName)
                    + " instead of " + typeof(T).FullName + ".");
            }

            return (T)value;
        }

        private static Type ResolveFixtureType()
        {
            Type found = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type candidate = assemblies[index].GetType(FixtureTypeName, false);
                if (candidate == null)
                {
                    continue;
                }

                if (found != null)
                {
                    Assert.Fail(
                        "Fixture type " + FixtureTypeName + " is duplicated in assemblies '"
                        + found.Assembly.GetName().Name + "' and '"
                        + candidate.Assembly.GetName().Name + "'.");
                }

                found = candidate;
            }

            if (found == null)
            {
                Assert.Fail(
                    "Could not resolve " + FixtureTypeName + " from loaded assemblies: "
                    + string.Join(
                        ", ",
                        assemblies
                            .Select(assembly => assembly.GetName().Name)
                            .OrderBy(name => name, StringComparer.Ordinal))
                    + ".");
            }

            return found;
        }

        private static Scene RequireBootstrapScene()
        {
            Scene scene = SceneManager.GetSceneByPath(BootstrapScenePath);
            if (!scene.IsValid())
            {
                scene = SceneManager.GetSceneByName(BootstrapSceneName);
            }

            Assert.That(
                scene.IsValid() && scene.isLoaded,
                Is.True,
                "Bootstrap is not loaded from '" + BootstrapScenePath + "'. "
                + DescribeLoadedScenes());
            return scene;
        }

        private static Scene RequireArenaScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ArenaScenePath);
            if (!scene.IsValid())
            {
                scene = SceneManager.GetSceneByName(ArenaSceneName);
            }

            Assert.That(
                scene.IsValid() && scene.isLoaded,
                Is.True,
                "Stage1BenchmarkArena is not loaded from '" + ArenaScenePath + "'. "
                + DescribeLoadedScenes());
            return scene;
        }

        private static void RequireSingleRunningBootstrap()
        {
            Scene bootstrap = RequireBootstrapScene();
            BootstrapSceneAdapter[] adapters = bootstrap
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BootstrapSceneAdapter>(true))
                .ToArray();

            Assert.That(adapters, Has.Length.EqualTo(1));
            Assert.That(adapters[0].IsCompositionRootRunning, Is.True);
        }

        private static void AssertArenaUnloaded(string context)
        {
            Scene scene = SceneManager.GetSceneByPath(ArenaScenePath);
            if (!scene.IsValid())
            {
                scene = SceneManager.GetSceneByName(ArenaSceneName);
            }

            Assert.That(
                scene.IsValid() && scene.isLoaded,
                Is.False,
                "Stage1BenchmarkArena remained loaded " + context + ". "
                + DescribeLoadedScenes());
            Assert.That(GetFixtureProperty<int>("ActiveInstanceCount"), Is.Zero);
            Assert.That(GetFixtureProperty<bool>("IsOperationInFlight"), Is.False);
        }

        private static int CountLoadedSceneGameObjects()
        {
            int count = 0;
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    count += roots[rootIndex].GetComponentsInChildren<Transform>(true).Length;
                }
            }

            return count;
        }

        private static string[] DescribeSceneHierarchy(Scene scene)
        {
            var entries = new List<string>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                DescribeTransform(roots[index].transform, string.Empty, entries);
            }

            return entries.OrderBy(entry => entry, StringComparer.Ordinal).ToArray();
        }

        private static void DescribeTransform(
            Transform transform,
            string parentPath,
            ICollection<string> entries)
        {
            string path = string.IsNullOrEmpty(parentPath)
                ? transform.gameObject.name
                : parentPath + "/" + transform.gameObject.name;
            entries.Add(path);

            for (int index = 0; index < transform.childCount; index++)
            {
                DescribeTransform(transform.GetChild(index), path, entries);
            }
        }

        private static string DescribeLoadedScenes()
        {
            var scenes = new List<string>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                scenes.Add(
                    scene.name + "[path=" + scene.path + ",loaded=" + scene.isLoaded
                    + ",active=" + (SceneManager.GetActiveScene() == scene) + "]");
            }

            return "Loaded scenes: " + string.Join(", ", scenes);
        }

        private static string DescribeValidation(string[] validation)
        {
            return validation == null
                ? "Validation result was null."
                : "Validation errors:\n" + string.Join("\n", validation);
        }
    }
}
#endif
