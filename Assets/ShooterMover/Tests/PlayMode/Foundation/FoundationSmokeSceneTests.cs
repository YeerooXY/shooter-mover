#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using ShooterMover.Bootstrap;
using ShooterMover.Bootstrap.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Foundation
{
    public sealed class FoundationSmokeSceneTests
    {
        private const string BootstrapSceneName = "Bootstrap";
        private const string BootstrapScenePath =
            "Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity";
        private const string SmokeSceneName = "FoundationSmoke";
        private const string SmokeMarkerRootName = "Foundation Smoke Visual Marker";
        private const string SmokeMarkerChildName = "Foundation Smoke Label";
        private const string FixtureTypeName =
            "ShooterMover.TestSupport.Foundation.FoundationSmokeLoaderFixture";
        private const float SceneOperationTimeoutSeconds = 30f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadBootstrapSingle("FoundationSmokeSceneTests setup");
            yield return null;
            RequireSingleRunningBootstrap();
            AssertNoRetainedSmokeObjects("before smoke test setup completed");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return LoadBootstrapSingle("FoundationSmokeSceneTests teardown");
            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadUnloadReloadFlow_ReturnsToExactBootstrapBaseline()
        {
            Scene bootstrapBefore = RequireBootstrapScene();
            BootstrapSceneAdapter adapterBefore = RequireSingleRunningBootstrap();
            int adapterInstanceId = adapterBefore.GetInstanceID();
            int baselineSceneObjectCount = CountLoadedSceneGameObjects();
            int baselineServiceCount = GetRegisteredServiceCount(adapterBefore);
            string[] baselineHierarchy = DescribeSceneHierarchy(bootstrapBefore);

            object result = InvokeFixtureMethod(
                "LoadUnloadReloadAndReturnToBootstrap");
            IEnumerator flow = result as IEnumerator;
            Assert.That(
                flow,
                Is.Not.Null,
                FixtureTypeName
                + ".LoadUnloadReloadAndReturnToBootstrap did not return IEnumerator.");

            yield return flow;
            yield return null;

            Assert.That(
                GetFixtureProperty<bool>("IsOperationInFlight"),
                Is.False,
                "Smoke fixture retained an unfinished scene operation.");
            Assert.That(
                GetFixtureProperty<bool>("IsLoaded"),
                Is.False,
                "FoundationSmoke remained loaded after the complete smoke flow.");
            Assert.That(
                GetFixtureProperty<int>("ActiveInstanceCount"),
                Is.Zero,
                "FoundationSmoke retained a runtime fixture instance after unload.");

            Scene bootstrapAfter = RequireBootstrapScene();
            BootstrapSceneAdapter adapterAfter = RequireSingleRunningBootstrap();
            Assert.That(
                adapterAfter.GetInstanceID(),
                Is.EqualTo(adapterInstanceId),
                "The smoke flow replaced the Bootstrap owner. Before: "
                + DescribeAdapter(adapterBefore) + "; after: "
                + DescribeAdapter(adapterAfter) + ".");
            Assert.That(
                SceneManager.GetActiveScene().handle,
                Is.EqualTo(bootstrapAfter.handle),
                "Bootstrap was not restored as the active scene. "
                + DescribeLoadedScenes());
            Assert.That(
                DescribeSceneHierarchy(bootstrapAfter),
                Is.EqualTo(baselineHierarchy),
                "Bootstrap hierarchy changed across smoke load/unload. Before:\n"
                + string.Join("\n", baselineHierarchy) + "\nAfter:\n"
                + string.Join("\n", DescribeSceneHierarchy(bootstrapAfter)));
            Assert.That(
                CountLoadedSceneGameObjects(),
                Is.EqualTo(baselineSceneObjectCount),
                "Loaded scene-owned GameObject count did not return to baseline. "
                + DescribeLoadedScenes());
            Assert.That(
                GetRegisteredServiceCount(adapterAfter),
                Is.EqualTo(baselineServiceCount),
                "Bootstrap registered-service count changed across the smoke flow at "
                + DescribeAdapter(adapterAfter) + ".");

            AssertNoRetainedSmokeObjects("after load/unload/reload smoke flow");
        }

        [UnityTest]
        public IEnumerator InFlightAndDuplicateSmokeLoads_AreRejectedAndCleanedUp()
        {
            int baselineSceneObjectCount = CountLoadedSceneGameObjects();
            string[] baselineHierarchy = DescribeSceneHierarchy(RequireBootstrapScene());

            AsyncOperation load = InvokeFixtureMethod("LoadAdditively") as AsyncOperation;
            Assert.That(
                load,
                Is.Not.Null,
                FixtureTypeName + ".LoadAdditively did not return AsyncOperation.");

            Exception inFlightFailure = InvokeFixtureExpectingFailure("LoadAdditively");
            Assert.That(inFlightFailure, Is.TypeOf<InvalidOperationException>());
            Assert.That(
                inFlightFailure.Message,
                Does.Contain("already in flight"),
                "In-flight duplicate rejection was not actionable: "
                + inFlightFailure);

            yield return WaitForOperation(load, "FoundationSmoke additive load");
            yield return null;

            Assert.That(GetFixtureProperty<bool>("IsLoaded"), Is.True);
            Assert.That(GetFixtureProperty<int>("ActiveInstanceCount"), Is.EqualTo(1));
            Assert.That(GetFixtureProperty<bool>("IsOperationInFlight"), Is.False);

            Exception duplicateFailure = InvokeFixtureExpectingFailure("LoadAdditively");
            Assert.That(duplicateFailure, Is.TypeOf<InvalidOperationException>());
            Assert.That(
                duplicateFailure.Message,
                Does.Contain("already loaded"),
                "Loaded-scene duplicate rejection was not actionable: "
                + duplicateFailure);

            AsyncOperation unload = InvokeFixtureMethod("Unload") as AsyncOperation;
            Assert.That(
                unload,
                Is.Not.Null,
                FixtureTypeName + ".Unload did not return AsyncOperation.");
            yield return WaitForOperation(unload, "FoundationSmoke cleanup unload");
            yield return null;

            Assert.That(GetFixtureProperty<bool>("IsLoaded"), Is.False);
            Assert.That(GetFixtureProperty<int>("ActiveInstanceCount"), Is.Zero);
            Assert.That(GetFixtureProperty<bool>("IsOperationInFlight"), Is.False);
            Assert.That(
                CountLoadedSceneGameObjects(),
                Is.EqualTo(baselineSceneObjectCount),
                "Smoke duplicate-rejection test retained scene-owned GameObjects. "
                + DescribeLoadedScenes());
            Assert.That(
                DescribeSceneHierarchy(RequireBootstrapScene()),
                Is.EqualTo(baselineHierarchy),
                "Bootstrap hierarchy changed during duplicate smoke-load rejection.");

            AssertNoRetainedSmokeObjects("after duplicate smoke-load cleanup");
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
            if (operation == null)
            {
                Assert.Fail(
                    "Unity did not create the requested scene operation for "
                    + description + ". " + DescribeLoadedScenes());
            }

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

        private static object InvokeFixtureMethod(string methodName)
        {
            MethodInfo method = RequireFixtureMethod(methodName);
            try
            {
                return method.Invoke(null, null);
            }
            catch (TargetInvocationException exception)
            {
                Exception inner = exception.InnerException ?? exception;
                ExceptionDispatchInfo.Capture(inner).Throw();
                throw;
            }
        }

        private static Exception InvokeFixtureExpectingFailure(string methodName)
        {
            MethodInfo method = RequireFixtureMethod(methodName);
            try
            {
                method.Invoke(null, null);
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

        private static MethodInfo RequireFixtureMethod(string methodName)
        {
            Type fixtureType = ResolveFixtureType();
            MethodInfo method = fixtureType.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                Assert.Fail(
                    "Missing public static fixture method " + FixtureTypeName
                    + "." + methodName + ".");
            }

            return method;
        }

        private static T GetFixtureProperty<T>(string propertyName)
        {
            Type fixtureType = ResolveFixtureType();
            PropertyInfo property = fixtureType.GetProperty(
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
                        "Fixture type " + FixtureTypeName
                        + " is duplicated in assemblies '" + found.Assembly.GetName().Name
                        + "' and '" + candidate.Assembly.GetName().Name + "'.");
                }

                found = candidate;
            }

            if (found == null)
            {
                Assert.Fail(
                    "Could not resolve " + FixtureTypeName
                    + " from loaded assemblies: "
                    + string.Join(
                        ", ",
                        assemblies
                            .Select(assembly => assembly.GetName().Name)
                            .OrderBy(name => name, StringComparer.Ordinal)
                            .ToArray())
                    + ". The UF-008 fixture must compile into the player before UF-009 runs.");
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

            if (!scene.IsValid() || !scene.isLoaded)
            {
                Assert.Fail(
                    "Bootstrap scene is not loaded from '" + BootstrapScenePath
                    + "'. " + DescribeLoadedScenes());
            }

            return scene;
        }

        private static BootstrapSceneAdapter RequireSingleRunningBootstrap()
        {
            List<BootstrapSceneAdapter> adapters = FindBootstrapAdapters();
            int runningCount = adapters.Count(adapter => adapter.IsCompositionRootRunning);
            if (adapters.Count != 1 || runningCount != 1)
            {
                Assert.Fail(
                    "Expected exactly one running BootstrapSceneAdapter; found "
                    + adapters.Count + " adapter(s), " + runningCount
                    + " running. " + DescribeAdapters(adapters));
            }

            return adapters[0];
        }

        private static List<BootstrapSceneAdapter> FindBootstrapAdapters()
        {
            var adapters = new List<BootstrapSceneAdapter>();
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
                    adapters.AddRange(
                        roots[rootIndex].GetComponentsInChildren<BootstrapSceneAdapter>(true));
                }
            }

            return adapters;
        }

        private static int GetRegisteredServiceCount(BootstrapSceneAdapter adapter)
        {
            FieldInfo field = typeof(BootstrapSceneAdapter).GetField(
                "compositionRoot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                Assert.Fail(
                    "BootstrapSceneAdapter no longer exposes the expected private "
                    + "compositionRoot ownership field at " + DescribeAdapter(adapter) + ".");
            }

            var root = field.GetValue(adapter) as BootstrapCompositionRoot;
            if (root == null)
            {
                Assert.Fail(
                    "BootstrapSceneAdapter has no live composition root at "
                    + DescribeAdapter(adapter) + ".");
            }

            return root.RegisteredServiceCount;
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
                    count += CountHierarchy(roots[rootIndex].transform);
                }
            }

            return count;
        }

        private static int CountHierarchy(Transform transform)
        {
            int count = 1;
            for (int childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                count += CountHierarchy(transform.GetChild(childIndex));
            }

            return count;
        }

        private static string[] DescribeSceneHierarchy(Scene scene)
        {
            var descriptions = new List<string>();
            GameObject[] roots = scene.GetRootGameObjects()
                .OrderBy(root => root.name, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < roots.Length; index++)
            {
                AddHierarchyDescriptions(roots[index], string.Empty, descriptions);
            }

            return descriptions.ToArray();
        }

        private static void AddHierarchyDescriptions(
            GameObject gameObject,
            string parentPath,
            ICollection<string> descriptions)
        {
            string path = string.IsNullOrEmpty(parentPath)
                ? gameObject.name
                : parentPath + "/" + gameObject.name;
            string[] componentNames = gameObject.GetComponents<Component>()
                .Select(component => component == null
                    ? "<missing component>"
                    : component.GetType().FullName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            descriptions.Add(
                path + " activeSelf=" + gameObject.activeSelf
                + " components=[" + string.Join(", ", componentNames) + "]");

            Transform[] children = new Transform[gameObject.transform.childCount];
            for (int index = 0; index < children.Length; index++)
            {
                children[index] = gameObject.transform.GetChild(index);
            }

            foreach (Transform child in children
                .OrderBy(value => value.name, StringComparer.Ordinal))
            {
                AddHierarchyDescriptions(child.gameObject, path, descriptions);
            }
        }

        private static void AssertNoRetainedSmokeObjects(string context)
        {
            Type fixtureType = ResolveFixtureType();
            var retained = new List<string>();

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
                    InspectForSmokeObjects(
                        roots[rootIndex],
                        fixtureType,
                        scene,
                        retained);
                }
            }

            Assert.That(
                retained,
                Is.Empty,
                "Retained FoundationSmoke object(s) " + context + ":\n - "
                + string.Join("\n - ", retained));
            Assert.That(
                SceneManager.GetSceneByName(SmokeSceneName).IsValid()
                    && SceneManager.GetSceneByName(SmokeSceneName).isLoaded,
                Is.False,
                "FoundationSmoke scene remained loaded " + context + ". "
                + DescribeLoadedScenes());
        }

        private static void InspectForSmokeObjects(
            GameObject gameObject,
            Type fixtureType,
            Scene scene,
            ICollection<string> retained)
        {
            bool namedSmokeObject = gameObject.name == SmokeMarkerRootName
                || gameObject.name == SmokeMarkerChildName;
            bool hasFixture = gameObject.GetComponents<Component>()
                .Any(component => component != null && component.GetType() == fixtureType);

            if (namedSmokeObject || hasFixture)
            {
                retained.Add(
                    "scene='" + scene.path + "', object='"
                    + GetHierarchyPath(gameObject.transform) + "', namedSmokeObject="
                    + namedSmokeObject + ", hasFixture=" + hasFixture);
            }

            for (int index = 0; index < gameObject.transform.childCount; index++)
            {
                InspectForSmokeObjects(
                    gameObject.transform.GetChild(index).gameObject,
                    fixtureType,
                    scene,
                    retained);
            }
        }

        private static string DescribeAdapters(IEnumerable<BootstrapSceneAdapter> adapters)
        {
            string[] descriptions = adapters.Select(DescribeAdapter).ToArray();
            return descriptions.Length == 0
                ? "Adapters: none. " + DescribeLoadedScenes()
                : "Adapters: " + string.Join(" | ", descriptions);
        }

        private static string DescribeAdapter(BootstrapSceneAdapter adapter)
        {
            if (adapter == null)
            {
                return "<destroyed adapter>";
            }

            return "scene='" + adapter.gameObject.scene.path
                + "', object='" + GetHierarchyPath(adapter.transform)
                + "', active=" + adapter.gameObject.activeInHierarchy
                + ", enabled=" + adapter.enabled
                + ", running=" + adapter.IsCompositionRootRunning;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static string DescribeLoadedScenes()
        {
            var descriptions = new List<string>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                descriptions.Add(
                    "handle=" + scene.handle + ", name='" + scene.name
                    + "', path='" + scene.path + "', loaded=" + scene.isLoaded
                    + ", roots=" + (scene.isLoaded ? scene.rootCount : 0));
            }

            return "Loaded scenes: " + string.Join(" | ", descriptions.ToArray());
        }
    }
}
#endif
