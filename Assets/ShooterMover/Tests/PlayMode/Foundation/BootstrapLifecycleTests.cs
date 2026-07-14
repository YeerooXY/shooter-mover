using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Bootstrap.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Foundation
{
    public sealed class BootstrapLifecycleTests
    {
        private const string BootstrapSceneName = "Bootstrap";
        private const float SceneOperationTimeoutSeconds = 30f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadBootstrapSingle("BootstrapLifecycleTests setup");
            yield return null;
            AssertSingleRunningBootstrap("after loading Bootstrap for test setup");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return LoadBootstrapSingle("BootstrapLifecycleTests teardown");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AdapterDisableEnableAndDestroy_PairsRootLifecycle()
        {
            BootstrapSceneAdapter adapter = RequireSingleBootstrapAdapter();
            string adapterPath = DescribeAdapter(adapter);

            Assert.That(
                adapter.IsCompositionRootRunning,
                Is.True,
                "Bootstrap composition root was not running at " + adapterPath + ".");

            adapter.enabled = false;
            yield return null;

            Assert.That(
                adapter.IsCompositionRootRunning,
                Is.False,
                "Disabling the adapter did not stop its composition root at "
                + adapterPath + ".");
            Assert.That(
                FindAllBootstrapAdapters().Count,
                Is.EqualTo(1),
                "Disabling the adapter should not create or retain another adapter. "
                + DescribeAdapters());

            adapter.enabled = true;
            yield return null;

            Assert.That(
                adapter.IsCompositionRootRunning,
                Is.True,
                "Re-enabling the adapter did not construct and start a fresh root at "
                + adapterPath + ".");
            AssertSingleRunningBootstrap("after re-enabling the Bootstrap adapter");

            GameObject adapterRoot = adapter.gameObject;
            UnityEngine.Object.Destroy(adapterRoot);
            yield return null;

            Assert.That(
                FindAllBootstrapAdapters(),
                Is.Empty,
                "Destroying Bootstrap root at " + adapterPath
                + " retained an adapter. " + DescribeAdapters());
        }

        [UnityTest]
        public IEnumerator AdditiveDuplicateBootstrap_IsRejectedWithoutReplacingOwner()
        {
            BootstrapSceneAdapter original = RequireSingleBootstrapAdapter();
            int originalInstanceId = original.GetInstanceID();
            var existingHandles = new HashSet<SceneHandle>(
                LoadedScenesNamed(BootstrapSceneName).Select(scene => scene.handle));

            AsyncOperation load = SceneManager.LoadSceneAsync(
                BootstrapSceneName,
                LoadSceneMode.Additive);
            yield return WaitForOperation(
                load,
                "additive duplicate Bootstrap load");
            yield return null;
            yield return null;

            Scene duplicateScene = RequireNewBootstrapScene(existingHandles);
            AssertSingleRunningBootstrap("after additive duplicate Bootstrap load");

            BootstrapSceneAdapter remaining = RequireSingleBootstrapAdapter();
            Assert.That(
                remaining.GetInstanceID(),
                Is.EqualTo(originalInstanceId),
                "The duplicate load replaced the original Bootstrap owner. "
                + DescribeAdapters());
            Assert.That(
                duplicateScene.GetRootGameObjects(),
                Is.Empty,
                "Duplicate Bootstrap scene handle " + duplicateScene.handle
                + " retained roots: " + DescribeSceneRoots(duplicateScene) + ".");

            AsyncOperation unload = SceneManager.UnloadSceneAsync(duplicateScene);
            yield return WaitForOperation(
                unload,
                "duplicate Bootstrap scene cleanup for handle " + duplicateScene.handle);
            yield return null;

            Assert.That(
                LoadedScenesNamed(BootstrapSceneName).Count,
                Is.EqualTo(1),
                "Duplicate Bootstrap scene remained loaded. " + DescribeLoadedScenes());
            AssertSingleRunningBootstrap("after duplicate Bootstrap scene cleanup");
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

        private static BootstrapSceneAdapter RequireSingleBootstrapAdapter()
        {
            List<BootstrapSceneAdapter> adapters = FindAllBootstrapAdapters();
            if (adapters.Count != 1)
            {
                Assert.Fail(
                    "Expected exactly one BootstrapSceneAdapter; found "
                    + adapters.Count + ". " + DescribeAdapters());
            }

            return adapters[0];
        }

        private static void AssertSingleRunningBootstrap(string context)
        {
            List<BootstrapSceneAdapter> adapters = FindAllBootstrapAdapters();
            int runningCount = adapters.Count(adapter => adapter.IsCompositionRootRunning);

            Assert.That(
                adapters.Count,
                Is.EqualTo(1),
                "Expected one BootstrapSceneAdapter " + context + "; found "
                + adapters.Count + ". " + DescribeAdapters());
            Assert.That(
                runningCount,
                Is.EqualTo(1),
                "Expected one running Bootstrap composition root " + context
                + "; found " + runningCount + ". " + DescribeAdapters());
        }

        private static List<BootstrapSceneAdapter> FindAllBootstrapAdapters()
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

        private static List<Scene> LoadedScenesNamed(string sceneName)
        {
            var scenes = new List<Scene>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded && scene.name == sceneName)
                {
                    scenes.Add(scene);
                }
            }

            return scenes;
        }

        private static Scene RequireNewBootstrapScene(ISet<SceneHandle> existingHandles)
        {
            List<Scene> candidates = LoadedScenesNamed(BootstrapSceneName)
                .Where(scene => !existingHandles.Contains(scene.handle))
                .ToList();

            if (candidates.Count != 1)
            {
                Assert.Fail(
                    "Expected one newly loaded duplicate Bootstrap scene; found "
                    + candidates.Count + ". " + DescribeLoadedScenes());
            }

            return candidates[0];
        }

        private static string DescribeAdapters()
        {
            List<BootstrapSceneAdapter> adapters = FindAllBootstrapAdapters();
            if (adapters.Count == 0)
            {
                return "Adapters: none. " + DescribeLoadedScenes();
            }

            return "Adapters: " + string.Join(
                " | ",
                adapters.Select(DescribeAdapter).ToArray());
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

        private static string DescribeSceneRoots(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return "<scene invalid or unloaded>";
            }

            return string.Join(
                ", ",
                scene.GetRootGameObjects()
                    .Select(root => GetHierarchyPath(root.transform))
                    .ToArray());
        }
    }
}
