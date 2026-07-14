using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using ShooterMover.Bootstrap.Unity;

namespace ShooterMover.TestSupport.Foundation
{
    /// <summary>
    /// Scene-owned visual hook and deterministic additive-load driver for
    /// FoundationSmoke. It creates no services and retains no objects after unload.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FoundationSmokeLoaderFixture : MonoBehaviour
    {
        public const string BootstrapSceneName = "Bootstrap";
        public const string BootstrapScenePath =
            "Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity";
        public const string SceneName = "FoundationSmoke";
        public const string ScenePath =
            "Assets/ShooterMover/Scenes/Tests/FoundationSmoke.unity";
        public const string MarkerRootName = "Foundation Smoke Visual Marker";
        public const string MarkerLabel = "FOUNDATION SMOKE\nADDITIVE SCENE ACTIVE";

        private static AsyncOperation activeOperation;
        private static int activeInstanceCount;

        private GameObject markerObject;
        private bool countedRuntimeInstance;

        public static int ActiveInstanceCount
        {
            get { return activeInstanceCount; }
        }

        public static bool IsOperationInFlight
        {
            get { return activeOperation != null; }
        }

        public static bool IsLoaded
        {
            get
            {
                Scene scene = GetSmokeScene();
                return scene.IsValid() && scene.isLoaded;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            activeOperation = null;
            activeInstanceCount = 0;
        }

        private void OnEnable()
        {
            CreateMarker();
            CountRuntimeInstance();
        }

        private void Start()
        {
            // Handles domain-reload-disabled Play Mode entry after editor-time OnEnable.
            CountRuntimeInstance();
        }

        private void OnDisable()
        {
            ReleaseRuntimeInstance();
            DestroyMarker();
        }

        private void OnDestroy()
        {
            ReleaseRuntimeInstance();
            DestroyMarker();
        }

        public static AsyncOperation LoadAdditively()
        {
            RequirePlayMode();
            RequireNoOperation();

            if (IsLoaded)
            {
                throw new InvalidOperationException(
                    "FoundationSmoke is already loaded; duplicate loads are rejected.");
            }

            EnsureBootstrapRunning(RequireBootstrapScene());

            AsyncOperation operation;
#if UNITY_EDITOR
            operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            operation = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
#endif
            return Track(operation);
        }

        public static AsyncOperation Unload()
        {
            RequirePlayMode();
            RequireNoOperation();

            Scene scene = GetSmokeScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException(
                    "FoundationSmoke cannot unload because it is not loaded.");
            }

            return Track(SceneManager.UnloadSceneAsync(scene));
        }

        /// <summary>
        /// Loads, unloads, reloads, unloads again, and restores Bootstrap as the
        /// active scene. Callers should yield the returned enumerator.
        /// </summary>
        public static IEnumerator LoadUnloadReloadAndReturnToBootstrap()
        {
            RequirePlayMode();
            RequireNoOperation();

            Scene bootstrap = RequireBootstrapScene();
            EnsureBootstrapRunning(bootstrap);
            SetBootstrapActive(bootstrap);

            yield return LoadAdditively();
            yield return null;
            EnsureSmokeLoaded();

            yield return Unload();
            yield return null;
            EnsureSmokeUnloaded();

            yield return LoadAdditively();
            yield return null;
            EnsureSmokeLoaded();

            yield return Unload();
            yield return null;
            EnsureSmokeUnloaded();

            bootstrap = RequireBootstrapScene();
            EnsureBootstrapRunning(bootstrap);
            SetBootstrapActive(bootstrap);
        }

        private static AsyncOperation Track(AsyncOperation operation)
        {
            if (operation == null)
            {
                throw new InvalidOperationException(
                    "Unity did not create the requested FoundationSmoke operation.");
            }

            activeOperation = operation;
            operation.completed += CompleteOperation;
            return operation;
        }

        private static void CompleteOperation(AsyncOperation operation)
        {
            if (ReferenceEquals(activeOperation, operation))
            {
                activeOperation = null;
            }
        }

        private static void RequirePlayMode()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                throw new InvalidOperationException(
                    "FoundationSmoke scene operations require Play Mode.");
            }
        }

        private static void RequireNoOperation()
        {
            if (activeOperation != null)
            {
                throw new InvalidOperationException(
                    "A FoundationSmoke load or unload is already in flight.");
            }
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
                throw new InvalidOperationException(
                    "Bootstrap must be loaded before FoundationSmoke is exercised.");
            }

            return scene;
        }

        private static Scene GetSmokeScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            return scene.IsValid() ? scene : SceneManager.GetSceneByName(SceneName);
        }

        private static void EnsureBootstrapRunning(Scene bootstrap)
        {
            int adapterCount = 0;
            bool isRunning = false;
            GameObject[] roots = bootstrap.GetRootGameObjects();

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                BootstrapSceneAdapter[] adapters =
                    roots[rootIndex].GetComponentsInChildren<BootstrapSceneAdapter>(true);

                for (int adapterIndex = 0; adapterIndex < adapters.Length; adapterIndex++)
                {
                    adapterCount++;
                    isRunning |= adapters[adapterIndex].IsCompositionRootRunning;
                }
            }

            if (adapterCount != 1 || !isRunning)
            {
                throw new InvalidOperationException(
                    "Expected exactly one running BootstrapSceneAdapter; found "
                    + adapterCount + ".");
            }
        }

        private static void SetBootstrapActive(Scene bootstrap)
        {
            if (SceneManager.GetActiveScene() != bootstrap
                && !SceneManager.SetActiveScene(bootstrap))
            {
                throw new InvalidOperationException(
                    "Unity could not restore Bootstrap as the active scene.");
            }
        }

        private static void EnsureSmokeLoaded()
        {
            Scene scene = GetSmokeScene();
            GameObject[] roots = scene.IsValid() && scene.isLoaded
                ? scene.GetRootGameObjects()
                : new GameObject[0];

            if (activeOperation != null
                || roots.Length != 1
                || roots[0].name != MarkerRootName
                || activeInstanceCount != 1)
            {
                throw new InvalidOperationException(
                    "FoundationSmoke did not load as one active labeled marker scene.");
            }
        }

        private static void EnsureSmokeUnloaded()
        {
            Scene scene = GetSmokeScene();
            if (activeOperation != null
                || (scene.IsValid() && scene.isLoaded)
                || activeInstanceCount != 0)
            {
                throw new InvalidOperationException(
                    "FoundationSmoke retained a scene object or unfinished operation after unload.");
            }
        }

        private void CountRuntimeInstance()
        {
            if (UnityEngine.Application.isPlaying && !countedRuntimeInstance)
            {
                countedRuntimeInstance = true;
                activeInstanceCount++;
            }
        }

        private void ReleaseRuntimeInstance()
        {
            if (!countedRuntimeInstance)
            {
                return;
            }

            countedRuntimeInstance = false;
            activeInstanceCount = Math.Max(0, activeInstanceCount - 1);
        }

        private void CreateMarker()
        {
            if (markerObject != null)
            {
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            if (font == null)
            {
                throw new InvalidOperationException(
                    "Unity's built-in runtime font is unavailable for FoundationSmoke.");
            }

            markerObject = new GameObject("Foundation Smoke Label", typeof(TextMesh));
            markerObject.hideFlags = HideFlags.DontSave;
            markerObject.transform.SetParent(transform, false);

            TextMesh text = markerObject.GetComponent<TextMesh>();
            text.text = MarkerLabel;
            text.font = font;
            text.fontSize = 64;
            text.characterSize = 0.18f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.lineSpacing = 1.1f;
            text.richText = false;
            text.color = new Color32(74, 230, 255, 255);

            MeshRenderer renderer = markerObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 0;
        }

        private void DestroyMarker()
        {
            if (markerObject == null)
            {
                return;
            }

            GameObject ownedMarker = markerObject;
            markerObject = null;

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(ownedMarker);
            }
            else
            {
                DestroyImmediate(ownedMarker);
            }
        }
    }
}
