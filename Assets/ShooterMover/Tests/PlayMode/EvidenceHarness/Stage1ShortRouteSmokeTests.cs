#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using ShooterMover.Bootstrap.Unity;
using ShooterMover.Contracts.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.EvidenceHarness
{
    public sealed class Stage1ShortRouteSmokeTests
    {
        private const string BootstrapSceneName = "Bootstrap";
        private const string BootstrapScenePath =
            "Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity";
        private const string RouteSceneName = "Stage1ShortRouteShell";
        private const string RouteScenePath =
            "Assets/ShooterMover/Tests/PlayMode/EvidenceHarness/Scenes/Stage1ShortRouteShell.unity";
        private const string FixtureTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.Stage1ShortRouteFixture";
        private const string ConnectionBindingTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.Stage1ShortRouteConnectionBinding";
        private const string ProjectionTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.Stage1ShortRouteProjection";
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
            "route.arena-entry",
            "route.connector",
            "route.restart",
            "route.review-end",
            "route.start"
        };

        private static readonly string[] ExpectedTraversal =
        {
            "route.start",
            "route.arena-entry",
            "route.connector",
            "route.review-end",
            "route.restart"
        };

        private static readonly string[] ExpectedConnectionIds =
        {
            "connection.arena-connector",
            "connection.connector-review",
            "connection.restart-start",
            "connection.review-restart",
            "connection.start-arena"
        };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadBootstrapSingle("EH-005 setup");
            yield return null;
            RequireSingleRunningBootstrap();
            AssertRouteUnloaded("before EH-005 setup completed");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GetFixtureProperty<bool>("IsOperationInFlight"))
            {
                yield return WaitForFixtureOperationToFinish("EH-005 teardown");
            }

            if (GetFixtureProperty<bool>("IsLoaded"))
            {
                AsyncOperation unload = InvokeFixture("Unload") as AsyncOperation;
                yield return WaitForOperation(unload, "EH-005 teardown unload");
                yield return null;
            }

            yield return LoadBootstrapSingle("EH-005 teardown bootstrap restore");
            yield return null;
        }

        [UnityTest]
        public IEnumerator MarkersConnectionsAndProjectionSurface_AreStableUniqueAndLocal()
        {
            yield return LoadRoute();

            string[] markerIds = InvokeFixture("GetMarkerIds") as string[];
            Assert.That(markerIds, Is.EqualTo(ExpectedMarkerIds));
            Assert.That(
                markerIds.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(markerIds.Length));

            string[] traversal = InvokeFixture("GetTraversalMarkerIds") as string[];
            Assert.That(traversal, Is.EqualTo(ExpectedTraversal));

            string[] connectionIds = InvokeFixture("GetConnectionIds") as string[];
            Assert.That(connectionIds, Is.EqualTo(ExpectedConnectionIds));
            Assert.That(
                connectionIds.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(connectionIds.Length));

            string[] validation = InvokeFixture("ValidateActiveRoute") as string[];
            Assert.That(validation, Is.Empty, DescribeValidation(validation));

            Scene route = RequireRouteScene();
            GameObject[] roots = route.GetRootGameObjects();
            Assert.That(roots, Has.Length.EqualTo(1));
            Assert.That(roots[0].name, Is.EqualTo("Stage1 Short Route Shell"));

            Component[] components = roots[0].GetComponentsInChildren<Component>(true);
            Assert.That(components.Any(component => component is Camera), Is.False);
            Assert.That(components.Any(component => component is Collider), Is.False);
            Assert.That(components.Any(component => component is Rigidbody), Is.False);
            Assert.That(components.Any(component => component is Joint), Is.False);
            Assert.That(components.Any(component => component is Rigidbody2D), Is.False);

            Transform[] transforms = roots[0].GetComponentsInChildren<Transform>(true);
            Assert.That(
                transforms.All(transform => Mathf.Abs(transform.position.z) <= 0.0001f),
                Is.True,
                "At least one route-shell transform left the XY plane.");

            Type connectionBinding = ResolveType(ConnectionBindingTypeName);
            FieldInfo[] serializedConnectionFields = connectionBinding.GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(serializedConnectionFields, Has.Length.EqualTo(5));
            Assert.That(
                serializedConnectionFields.All(field => field.FieldType == typeof(string)),
                Is.True,
                "A route connection serialized a scene-object reference instead of a stable address.");

            Type projectionType = ResolveType(ProjectionTypeName);
            Assert.That(projectionType.IsSealed, Is.True);
            PropertyInfo[] projectionProperties = projectionType.GetProperties(
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(projectionProperties, Is.Not.Empty);
            Assert.That(
                projectionProperties.All(property => !property.CanWrite),
                Is.True,
                "The route projection exposed mutable public state.");
            Assert.That(
                projectionProperties.All(
                    property => !typeof(UnityEngine.Object).IsAssignableFrom(property.PropertyType)),
                Is.True,
                "The route projection exposed a Unity object reference.");

            Assert.That(
                GetFixtureProperty<string>("ProjectionReaderContractName"),
                Is.EqualTo(typeof(IRoomProjectionStateReader).FullName));
            Assert.That(GetFixtureProperty<int>("MissionCommandSubmissionCount"), Is.Zero);

            string snapshot = InvokeFixture("CaptureActiveSnapshot") as string;
            Assert.That(
                snapshot,
                Does.Contain("schema=shooter-mover.stage1-short-route-shell-snapshot"));
            Assert.That(snapshot, Does.Contain("loadOrder=" + string.Join(",", ExpectedTraversal)));
            Assert.That(snapshot, Does.Contain("marker|route.start|Start"));
            Assert.That(snapshot, Does.Contain("connection|connection.restart-start"));
            Assert.That(snapshot, Does.Contain("missionCommandSubmissions=0"));
            Debug.Log("EH-005 route marker and lifecycle snapshot\n" + snapshot);
        }

        [UnityTest]
        public IEnumerator LifecycleOrder_EnterLeaveReload_AreIdempotent()
        {
            yield return LoadRoute();

            string repeatedEnter = InvokeFixture("EnterRoute") as string;
            AssertLifecycleOrder(repeatedEnter, "enter", ExpectedTraversal);
            Assert.That(
                LifecycleLines(repeatedEnter).All(line => line.Contains("|NoChange|")),
                Is.True,
                repeatedEnter);

            string leave = InvokeFixture("LeaveRoute") as string;
            string[] reverse = ExpectedTraversal.Reverse().ToArray();
            AssertLifecycleOrder(leave, "leave-begin", reverse);
            AssertLifecycleOrder(leave, "leave-complete", reverse);
            Assert.That(
                LifecycleLines(leave).All(line => line.Contains("|Applied|")),
                Is.True,
                leave);

            string repeatedLeave = InvokeFixture("LeaveRoute") as string;
            Assert.That(
                LifecycleLines(repeatedLeave).All(line => line.Contains("|NoChange|")),
                Is.True,
                repeatedLeave);

            string reload = InvokeFixture("ReloadRoute") as string;
            AssertLifecycleOrder(reload, "reload", ExpectedTraversal);
            Assert.That(
                LifecycleLines(reload).All(line => line.Contains("|Applied|")),
                Is.True,
                reload);

            string repeatedReload = InvokeFixture("ReloadRoute") as string;
            Assert.That(
                LifecycleLines(repeatedReload).All(line => line.Contains("|NoChange|")),
                Is.True,
                repeatedReload);

            string[] validation = InvokeFixture("ValidateActiveRoute") as string[];
            Assert.That(validation, Is.Empty, DescribeValidation(validation));
            Debug.Log(
                "EH-005 lifecycle order proof\n"
                + repeatedEnter + leave + repeatedLeave + reload + repeatedReload);
        }

        [UnityTest]
        public IEnumerator UnloadAndReload_ProduceTheSameMarkerLifecycleSnapshot()
        {
            yield return LoadRoute();
            string first = InvokeFixture("CaptureActiveSnapshot") as string;

            AsyncOperation unload = InvokeFixture("Unload") as AsyncOperation;
            yield return WaitForOperation(unload, "EH-005 equality unload");
            yield return null;
            AssertRouteUnloaded("between equality loads");

            yield return LoadRoute();
            string second = InvokeFixture("CaptureActiveSnapshot") as string;

            Assert.That(second, Is.EqualTo(first));
        }

        [UnityTest]
        public IEnumerator InterruptedUnload_ResumeAndReloadRemainIdempotent()
        {
            yield return LoadRoute();

            string begin = InvokeFixture(
                "BeginInterruptedUnloadForTest",
                "route.connector") as string;
            Assert.That(begin, Does.Contain("|BeginUnload|Applied|Loaded->Unloading"));
            Assert.That(
                InvokeFixture("GetMarkerPhaseForTest", "route.connector"),
                Is.EqualTo("Unloading"));

            string repeatedBegin = InvokeFixture(
                "BeginInterruptedUnloadForTest",
                "route.connector") as string;
            Assert.That(repeatedBegin, Does.Contain("|BeginUnload|NoChange|Unloading->Unloading"));

            string resume = InvokeFixture(
                "ResumeInterruptedUnloadForTest",
                "route.connector") as string;
            Assert.That(
                resume,
                Does.Contain("|ResumeInterruptedUnload|Applied|Unloading->Loaded"));

            string repeatedResume = InvokeFixture(
                "ResumeInterruptedUnloadForTest",
                "route.connector") as string;
            Assert.That(
                repeatedResume,
                Does.Contain("|ResumeInterruptedUnload|NoChange|Loaded->Loaded"));

            InvokeFixture("BeginInterruptedUnloadForTest", "route.connector");
            string reload = InvokeFixture("ReloadRoute") as string;
            Assert.That(
                reload,
                Does.Contain("reload|route.connector|Reload|Applied|Unloading->Loaded"));
            Assert.That(
                InvokeFixture("GetMarkerPhaseForTest", "route.connector"),
                Is.EqualTo("Loaded"));

            string[] validation = InvokeFixture("ValidateActiveRoute") as string[];
            Assert.That(validation, Is.Empty, DescribeValidation(validation));
        }

        [UnityTest]
        public IEnumerator MissingConnection_FailsClosedAndCanBeRestored()
        {
            yield return LoadRoute();

            InvokeFixture(
                "SetConnectionTargetOverrideForTest",
                "connection.connector-review",
                "route.missing");

            string[] errors = InvokeFixture("ValidateActiveRoute") as string[];
            Assert.That(
                errors,
                Does.Contain(
                    "missing-connection-endpoint:connection.connector-review:route.missing"),
                DescribeValidation(errors));
            Assert.That(
                errors,
                Does.Contain("missing-traversal-link:route.connector->route.review-end"),
                DescribeValidation(errors));

            string invalidSnapshot = InvokeFixture("CaptureActiveSnapshot") as string;
            Assert.That(
                invalidSnapshot,
                Does.Contain("connection|connection.connector-review")
                    .And.Contain("|to=route.missing|missing-endpoint"));

            InvokeFixture("ClearConnectionTargetOverridesForTest");
            string[] restored = InvokeFixture("ValidateActiveRoute") as string[];
            Assert.That(restored, Is.Empty, DescribeValidation(restored));
        }

        [UnityTest]
        public IEnumerator ProjectionReads_AreExplicitUnknownAndNeverSubmitCommands()
        {
            yield return LoadRoute();

            int readsBefore = GetFixtureProperty<int>("ProjectionReadCount");
            string found = InvokeFixture(
                "ReadProjectionForTest",
                "route.start") as string;
            string unknown = InvokeFixture(
                "ReadUnknownProjectionForTest",
                "room.unknown-route") as string;

            Assert.That(found, Does.StartWith("status=Found;value=marker=route.start"));
            Assert.That(unknown, Is.EqualTo("status=UnknownKey;value=<none>"));
            Assert.That(
                GetFixtureProperty<int>("ProjectionReadCount"),
                Is.EqualTo(readsBefore + 2));
            Assert.That(GetFixtureProperty<int>("MissionCommandSubmissionCount"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator CursorTraversal_VisitsEveryMarkerAndReturnsRapidlyToStart()
        {
            yield return LoadRoute();

            Assert.That(GetFixtureProperty<string>("CursorMarkerId"), Is.EqualTo("route.start"));
            for (int index = 1; index < ExpectedTraversal.Length; index++)
            {
                string current = InvokeFixture("MoveCursorNext") as string;
                Assert.That(current, Is.EqualTo(ExpectedTraversal[index]));
            }

            Assert.That(
                InvokeFixture("MoveCursorNext"),
                Is.EqualTo("route.start"),
                "The route loop did not return from restart to start.");
            Assert.That(InvokeFixture("MoveCursorPrevious"), Is.EqualTo("route.restart"));
            Assert.That(InvokeFixture("RestartCursorToStart"), Is.EqualTo("route.start"));
        }

        [UnityTest]
        public IEnumerator DuplicateShellLoads_AreRejectedDuringAndAfterLoad()
        {
            AsyncOperation load = InvokeFixture(
                "LoadFromCanonicalConfiguration",
                CanonicalConfiguration) as AsyncOperation;
            Assert.That(load, Is.Not.Null);

            Exception inFlight = InvokeFixtureExpectingFailure(
                "LoadFromCanonicalConfiguration",
                CanonicalConfiguration);
            Assert.That(inFlight, Is.TypeOf<InvalidOperationException>());
            Assert.That(inFlight.Message, Does.Contain("already in flight"));

            yield return WaitForOperation(load, "EH-005 duplicate-request load");
            yield return null;

            Exception loaded = InvokeFixtureExpectingFailure(
                "LoadFromCanonicalConfiguration",
                CanonicalConfiguration);
            Assert.That(loaded, Is.TypeOf<InvalidOperationException>());
            Assert.That(loaded.Message, Does.Contain("already loaded"));
        }

        private static IEnumerator LoadRoute()
        {
            AsyncOperation load = InvokeFixture(
                "LoadFromCanonicalConfiguration",
                CanonicalConfiguration) as AsyncOperation;
            yield return WaitForOperation(load, "EH-005 canonical additive load");
            yield return null;

            RequireRouteScene();
            Assert.That(GetFixtureProperty<int>("ActiveInstanceCount"), Is.EqualTo(1));
            Assert.That(GetFixtureProperty<int>("ResolvedRunSeed"), Is.EqualTo(104729));
            Assert.That(
                GetFixtureProperty<string>("ResolvedConfigurationFingerprint"),
                Does.StartWith("sha256:"));
        }

        private static void AssertLifecycleOrder(
            string lifecycle,
            string label,
            IEnumerable<string> expectedMarkers)
        {
            string[] actual = LifecycleLines(lifecycle)
                .Where(line => line.StartsWith("lifecycle|" + label + "|", StringComparison.Ordinal))
                .Select(line => line.Split('|')[2])
                .ToArray();
            Assert.That(actual, Is.EqualTo(expectedMarkers.ToArray()), lifecycle);
        }

        private static string[] LifecycleLines(string lifecycle)
        {
            return (lifecycle ?? string.Empty)
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
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
            return ResolveType(FixtureTypeName);
        }

        private static Type ResolveType(string fullName)
        {
            Type found = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type candidate = assemblies[index].GetType(fullName, false);
                if (candidate == null)
                {
                    continue;
                }

                if (found != null)
                {
                    Assert.Fail(
                        "Type " + fullName + " is duplicated in assemblies '"
                        + found.Assembly.GetName().Name + "' and '"
                        + candidate.Assembly.GetName().Name + "'.");
                }

                found = candidate;
            }

            if (found == null)
            {
                Assert.Fail(
                    "Could not resolve " + fullName + " from loaded assemblies: "
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

        private static Scene RequireRouteScene()
        {
            Scene scene = SceneManager.GetSceneByPath(RouteScenePath);
            if (!scene.IsValid())
            {
                scene = SceneManager.GetSceneByName(RouteSceneName);
            }

            Assert.That(
                scene.IsValid() && scene.isLoaded,
                Is.True,
                "Stage1ShortRouteShell is not loaded from '" + RouteScenePath + "'. "
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

        private static void AssertRouteUnloaded(string context)
        {
            Scene scene = SceneManager.GetSceneByPath(RouteScenePath);
            if (!scene.IsValid())
            {
                scene = SceneManager.GetSceneByName(RouteSceneName);
            }

            Assert.That(
                scene.IsValid() && scene.isLoaded,
                Is.False,
                "Stage1ShortRouteShell remained loaded " + context + ". "
                + DescribeLoadedScenes());
            Assert.That(GetFixtureProperty<int>("ActiveInstanceCount"), Is.Zero);
            Assert.That(GetFixtureProperty<bool>("IsOperationInFlight"), Is.False);
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
