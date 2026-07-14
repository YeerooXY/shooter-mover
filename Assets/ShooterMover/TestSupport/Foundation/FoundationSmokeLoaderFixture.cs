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
    /// Scene-owned hook and deterministic additive-load driver for FoundationSmoke.
    /// It creates only a transient 2D marker and never registers runtime services.
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
        public const string MarkerObjectName = "Foundation Smoke Visual Marker";
        public const string MarkerLabel = "FOUNDATION SMOKE";

        private const int TextureWidth = 256;
        private const int TextureHeight = 96;
        private const int PixelsPerUnit = 32;
        private const int GlyphWidth = 5;
        private const int GlyphHeight = 7;
        private const int GlyphSpacing = 1;

        private static AsyncOperation activeOperation;
        private static OperationKind activeOperationKind;
        private static int activeInstanceCount;

        private GameObject markerObject;
        private SpriteRenderer markerRenderer;
        private Texture2D markerTexture;
        private Sprite markerSprite;
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
                Scene scene = GetLoadedSmokeScene();
                return scene.IsValid() && scene.isLoaded;
            }
        }

        public static string ActiveOperationName
        {
            get { return activeOperationKind.ToString(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            activeOperation = null;
            activeOperationKind = OperationKind.None;
            activeInstanceCount = 0;
        }

        private void OnEnable()
        {
            EnsureMarker();
            RegisterRuntimeInstance();
        }

        private void Start()
        {
            // Covers domain-reload-disabled Play Mode transitions where an editor-time
            // OnEnable may have occurred before Application.isPlaying became true.
            RegisterRuntimeInstance();
        }

        private void OnDisable()
        {
            UnregisterRuntimeInstance();
            ReleaseMarker();
        }

        private void OnDestroy()
        {
            UnregisterRuntimeInstance();
            ReleaseMarker();
        }

        /// <summary>
        /// Starts one additive FoundationSmoke load. A second request is rejected
        /// until the current load or unload operation has completed.
        /// </summary>
        public static AsyncOperation LoadAdditively()
        {
            RequirePlayMode();
            RequireNoOperation();

            if (IsLoaded)
            {
                throw new InvalidOperationException(
                    "FoundationSmoke is already loaded; duplicate smoke loads are not allowed.");
            }

            Scene bootstrapScene = RequireBootstrapScene();
            EnsureBootstrapRunning(bootstrapScene);

            AsyncOperation operation;
#if UNITY_EDITOR
            operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            operation = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
#endif
            return TrackOperation(operation, OperationKind.Loading);
        }

        /// <summary>
        /// Starts one unload of the currently loaded FoundationSmoke scene.
        /// </summary>
        public static AsyncOperation Unload()
        {
            RequirePlayMode();
            RequireNoOperation();

            Scene smokeScene = GetLoadedSmokeScene();
            if (!smokeScene.IsValid() || !smokeScene.isLoaded)
            {
                throw new InvalidOperationException(
                    "FoundationSmoke cannot unload because it is not loaded.");
            }

            return TrackOperation(
                SceneManager.UnloadSceneAsync(smokeScene),
                OperationKind.Unloading);
        }

        /// <summary>
        /// Exercises load, unload, reload, final unload, and explicit return to the
        /// still-running Bootstrap scene. Callers should yield this enumerator.
        /// </summary>
        public static IEnumerator LoadUnloadReloadAndReturnToBootstrap()
        {
            RequirePlayMode();
            RequireNoOperation();

            Scene bootstrapScene = RequireBootstrapScene();
            EnsureBootstrapRunning(bootstrapScene);
            SetBootstrapActive(bootstrapScene);

            AsyncOperation load = LoadAdditively();
            yield return load;
            yield return null;
            EnsureSmokeLoadedExactlyOnce();

            AsyncOperation unload = Unload();
            yield return unload;
            yield return null;
            EnsureSmokeUnloaded();

            AsyncOperation reload = LoadAdditively();
            yield return reload;
            yield return null;
            EnsureSmokeLoadedExactlyOnce();

            AsyncOperation finalUnload = Unload();
            yield return finalUnload;
            yield return null;
            EnsureSmokeUnloaded();

            bootstrapScene = RequireBootstrapScene();
            EnsureBootstrapRunning(bootstrapScene);
            SetBootstrapActive(bootstrapScene);
        }

        private static AsyncOperation TrackOperation(
            AsyncOperation operation,
            OperationKind operationKind)
        {
            if (operation == null)
            {
                throw new InvalidOperationException(
                    "Unity did not create the requested FoundationSmoke scene operation.");
            }

            activeOperation = operation;
            activeOperationKind = operationKind;
            operation.completed += OnOperationCompleted;
            return operation;
        }

        private static void OnOperationCompleted(AsyncOperation operation)
        {
            if (!ReferenceEquals(activeOperation, operation))
            {
                return;
            }

            activeOperation = null;
            activeOperationKind = OperationKind.None;
        }

        private static void RequirePlayMode()
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException(
                    "The FoundationSmoke load sequence may only run in Play Mode.");
            }
        }

        private static void RequireNoOperation()
        {
            if (activeOperation != null)
            {
                throw new InvalidOperationException(
                    "A FoundationSmoke " + activeOperationKind
                    + " operation is already in flight. Wait for it to complete before retrying.");
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
                    "Bootstrap must be loaded before FoundationSmoke can be exercised.");
            }

            return scene;
        }

        private static Scene GetLoadedSmokeScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid())
            {
                scene = SceneManager.GetSceneByName(SceneName);
            }

            return scene;
        }

        private static void EnsureBootstrapRunning(Scene bootstrapScene)
        {
            BootstrapSceneAdapter runningAdapter = null;
            int adapterCount = 0;
            GameObject[] roots = bootstrapScene.GetRootGameObjects();

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                BootstrapSceneAdapter[] adapters =
                    roots[rootIndex].GetComponentsInChildren<BootstrapSceneAdapter>(true);

                for (int adapterIndex = 0; adapterIndex < adapters.Length; adapterIndex++)
                {
                    adapterCount++;
                    if (adapters[adapterIndex].IsCompositionRootRunning)
                    {
                        runningAdapter = adapters[adapterIndex];
                    }
                }
            }

            if (adapterCount != 1 || runningAdapter == null)
            {
                throw new InvalidOperationException(
                    "Bootstrap must contain exactly one running BootstrapSceneAdapter; found "
                    + adapterCount + ".");
            }
        }

        private static void SetBootstrapActive(Scene bootstrapScene)
        {
            if (SceneManager.GetActiveScene() == bootstrapScene)
            {
                return;
            }

            if (!SceneManager.SetActiveScene(bootstrapScene))
            {
                throw new InvalidOperationException(
                    "Unity could not restore Bootstrap as the active scene.");
            }
        }

        private static void EnsureSmokeLoadedExactlyOnce()
        {
            if (activeOperation != null)
            {
                throw new InvalidOperationException(
                    "The FoundationSmoke operation reported completion but is still tracked as active.");
            }

            Scene smokeScene = GetLoadedSmokeScene();
            if (!smokeScene.IsValid() || !smokeScene.isLoaded)
            {
                throw new InvalidOperationException(
                    "FoundationSmoke did not become a loaded additive scene.");
            }

            GameObject[] roots = smokeScene.GetRootGameObjects();
            if (roots.Length != 1 || roots[0].name != MarkerObjectName)
            {
                throw new InvalidOperationException(
                    "FoundationSmoke must contain exactly one root named '"
                    + MarkerObjectName + "'; found " + roots.Length + " root object(s).");
            }

            FoundationSmokeLoaderFixture[] fixtures =
                roots[0].GetComponentsInChildren<FoundationSmokeLoaderFixture>(true);
            if (fixtures.Length != 1 || activeInstanceCount != 1)
            {
                throw new InvalidOperationException(
                    "FoundationSmoke must have exactly one active loader fixture; scene count="
                    + fixtures.Length + ", active count=" + activeInstanceCount + ".");
            }
        }

        private static void EnsureSmokeUnloaded()
        {
            if (activeOperation != null)
            {
                throw new InvalidOperationException(
                    "The FoundationSmoke operation reported completion but is still tracked as active.");
            }

            Scene smokeScene = GetLoadedSmokeScene();
            if (smokeScene.IsValid() && smokeScene.isLoaded)
            {
                throw new InvalidOperationException(
                    "FoundationSmoke remained loaded after its unload operation completed.");
            }

            if (activeInstanceCount != 0)
            {
                throw new InvalidOperationException(
                    "FoundationSmoke retained " + activeInstanceCount
                    + " active fixture instance(s) after unload.");
            }
        }

        private void RegisterRuntimeInstance()
        {
            if (!Application.isPlaying || countedRuntimeInstance)
            {
                return;
            }

            countedRuntimeInstance = true;
            activeInstanceCount++;
        }

        private void UnregisterRuntimeInstance()
        {
            if (!countedRuntimeInstance)
            {
                return;
            }

            countedRuntimeInstance = false;
            activeInstanceCount = Math.Max(0, activeInstanceCount - 1);
        }

        private void EnsureMarker()
        {
            if (markerObject != null && markerRenderer != null && markerSprite != null)
            {
                return;
            }

            ReleaseMarker();

            markerObject = new GameObject(MarkerObjectName, typeof(SpriteRenderer));
            markerObject.hideFlags = HideFlags.DontSave;
            markerObject.transform.SetParent(transform, false);
            markerObject.transform.localPosition = Vector3.zero;
            markerObject.transform.localRotation = Quaternion.identity;
            markerObject.transform.localScale = Vector3.one;

            markerRenderer = markerObject.GetComponent<SpriteRenderer>();
            markerRenderer.sortingOrder = 0;
            markerRenderer.color = Color.white;

            markerTexture = BuildMarkerTexture();
            markerSprite = Sprite.Create(
                markerTexture,
                new Rect(0f, 0f, TextureWidth, TextureHeight),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            markerSprite.name = MarkerLabel;
            markerSprite.hideFlags = HideFlags.DontSave;
            markerRenderer.sprite = markerSprite;
        }

        private static Texture2D BuildMarkerTexture()
        {
            Color32 background = new Color32(10, 18, 28, 255);
            Color32 panel = new Color32(18, 36, 52, 255);
            Color32 accent = new Color32(74, 230, 255, 255);
            Color32 text = new Color32(240, 250, 255, 255);
            Color32[] pixels = new Color32[TextureWidth * TextureHeight];

            Fill(pixels, background);
            FillRectangle(pixels, 4, 4, TextureWidth - 8, TextureHeight - 8, accent);
            FillRectangle(pixels, 8, 8, TextureWidth - 16, TextureHeight - 16, panel);
            FillRectangle(pixels, 18, 47, TextureWidth - 36, 2, accent);

            DrawCenteredText(pixels, "FOUNDATION", 55, 3, text);
            DrawCenteredText(pixels, "SMOKE", 20, 3, text);

            Texture2D texture = new Texture2D(
                TextureWidth,
                TextureHeight,
                TextureFormat.RGBA32,
                false,
                false);
            texture.name = MarkerLabel + " Texture";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.DontSave;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void Fill(Color32[] pixels, Color32 color)
        {
            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = color;
            }
        }

        private static void FillRectangle(
            Color32[] pixels,
            int x,
            int y,
            int width,
            int height,
            Color32 color)
        {
            for (int pixelY = y; pixelY < y + height; pixelY++)
            {
                for (int pixelX = x; pixelX < x + width; pixelX++)
                {
                    pixels[(pixelY * TextureWidth) + pixelX] = color;
                }
            }
        }

        private static void DrawCenteredText(
            Color32[] pixels,
            string value,
            int originY,
            int scale,
            Color32 color)
        {
            int textWidth =
                (value.Length * (GlyphWidth + GlyphSpacing) * scale)
                - (GlyphSpacing * scale);
            int originX = (TextureWidth - textWidth) / 2;

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (character != ' ')
                {
                    DrawGlyph(
                        pixels,
                        character,
                        originX,
                        originY,
                        scale,
                        color);
                }

                originX += (GlyphWidth + GlyphSpacing) * scale;
            }
        }

        private static void DrawGlyph(
            Color32[] pixels,
            char character,
            int originX,
            int originY,
            int scale,
            Color32 color)
        {
            string glyph = GetGlyph(character);

            for (int row = 0; row < GlyphHeight; row++)
            {
                for (int column = 0; column < GlyphWidth; column++)
                {
                    if (glyph[(row * 6) + column] != '1')
                    {
                        continue;
                    }

                    int pixelX = originX + (column * scale);
                    int pixelY = originY + ((GlyphHeight - 1 - row) * scale);
                    FillRectangle(pixels, pixelX, pixelY, scale, scale, color);
                }
            }
        }

        private static string GetGlyph(char character)
        {
            switch (character)
            {
                case 'A':
                    return "01110|10001|10001|11111|10001|10001|10001";
                case 'D':
                    return "11110|10001|10001|10001|10001|10001|11110";
                case 'E':
                    return "11111|10000|10000|11110|10000|10000|11111";
                case 'F':
                    return "11111|10000|10000|11110|10000|10000|10000";
                case 'I':
                    return "11111|00100|00100|00100|00100|00100|11111";
                case 'K':
                    return "10001|10010|10100|11000|10100|10010|10001";
                case 'M':
                    return "10001|11011|10101|10101|10001|10001|10001";
                case 'N':
                    return "10001|11001|11001|10101|10011|10011|10001";
                case 'O':
                    return "01110|10001|10001|10001|10001|10001|01110";
                case 'S':
                    return "01111|10000|10000|01110|00001|00001|11110";
                case 'T':
                    return "11111|00100|00100|00100|00100|00100|00100";
                case 'U':
                    return "10001|10001|10001|10001|10001|10001|01110";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(character),
                        character,
                        "No marker glyph is defined for this character.");
            }
        }

        private void ReleaseMarker()
        {
            if (markerRenderer != null)
            {
                markerRenderer.sprite = null;
            }

            DestroyOwned(markerSprite);
            DestroyOwned(markerTexture);
            DestroyOwned(markerObject);

            markerSprite = null;
            markerTexture = null;
            markerRenderer = null;
            markerObject = null;
        }

        private static void DestroyOwned(UnityEngine.Object ownedObject)
        {
            if (ownedObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(ownedObject);
            }
            else
            {
                DestroyImmediate(ownedObject);
            }
        }

        private enum OperationKind
        {
            None,
            Loading,
            Unloading
        }
    }
}
