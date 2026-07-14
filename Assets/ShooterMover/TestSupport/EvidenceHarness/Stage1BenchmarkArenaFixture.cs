using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Bootstrap.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ShooterMover.TestSupport.EvidenceHarness
{
    public enum Stage1BenchmarkArenaMarkerKind
    {
        Shell = 0,
        PlayerSpawn = 1,
        TargetSpawn = 2,
        HazardSpawn = 3,
        CameraBounds = 4,
        CollisionEnvelope = 5,
        PerformanceProbe = 6,
        CombatHook = 7
    }

    [Serializable]
    public sealed class Stage1BenchmarkArenaMarkerBinding
    {
        [SerializeField]
        private string markerId;

        [SerializeField]
        private Stage1BenchmarkArenaMarkerKind kind;

        [SerializeField]
        private Transform socket;

        [SerializeField]
        private int sortingOrder;

        public string MarkerId
        {
            get { return markerId; }
        }

        public Stage1BenchmarkArenaMarkerKind Kind
        {
            get { return kind; }
        }

        public Transform Socket
        {
            get { return socket; }
        }

        public int SortingOrder
        {
            get { return sortingOrder; }
        }
    }

    /// <summary>
    /// Test-owned additive Stage 1 benchmark arena shell. The fixture owns only
    /// deterministic sockets, local placeholder visuals, bounds, and reset behavior.
    /// It never decides movement, combat, rewards, room truth, or progression.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Stage1BenchmarkArenaFixture : MonoBehaviour
    {
        public const string BootstrapSceneName = "Bootstrap";
        public const string BootstrapScenePath =
            "Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity";
        public const string SceneName = "Stage1BenchmarkArena";
        public const string ScenePath =
            "Assets/ShooterMover/Tests/PlayMode/EvidenceHarness/Scenes/Stage1BenchmarkArena.unity";
        public const string ConfigurationFixturePath =
            "tools/evidence/fixtures/stage1-evidence-config-v1.json";
        public const string SnapshotSchema =
            "shooter-mover.stage1-benchmark-arena-snapshot";
        public const int SnapshotVersion = 1;

        private const string VisualChildName = "__EH004Visual";
        private const string WallVisualChildName = "__EH004WallVisual";

        private static readonly string[] RequiredMarkerIds =
        {
            "arena.shell.v1",
            "socket.player.primary",
            "socket.target.north",
            "socket.target.east",
            "socket.target.south",
            "socket.target.west",
            "socket.hazard.northwest",
            "socket.hazard.southeast",
            "bounds.camera",
            "bounds.collision",
            "probe.performance.center",
            "probe.performance.north",
            "probe.performance.south",
            "hook.combat.spawn",
            "hook.combat.cleanup"
        };

        private static AsyncOperation activeOperation;
        private static Stage1BenchmarkArenaFixture activeInstance;
        private static EvidenceRunConfiguration pendingConfiguration;
        private static EvidenceRunConfiguration activeConfiguration;
        private static int activeInstanceCount;

        [SerializeField]
        private Transform arenaRoot;

        [SerializeField]
        private BoxCollider2D cameraBounds;

        [SerializeField]
        private Transform collisionEnvelope;

        [SerializeField]
        private BoxCollider2D[] collisionWalls;

        [SerializeField]
        private Stage1BenchmarkArenaMarkerBinding[] markers;

        private readonly List<AuthoredTransformState> authoredStates =
            new List<AuthoredTransformState>();

        private Texture2D visualTexture;
        private Sprite visualSprite;
        private bool authoredStateCaptured;
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
                Scene scene = GetArenaScene();
                return scene.IsValid() && scene.isLoaded;
            }
        }

        public static string ResolvedConfigurationFingerprint
        {
            get
            {
                return activeConfiguration == null
                    ? string.Empty
                    : activeConfiguration.Fingerprint;
            }
        }

        public static int ResolvedRunSeed
        {
            get { return activeConfiguration == null ? 0 : activeConfiguration.RunSeed; }
        }

        public static string CameraBoundsSummary
        {
            get
            {
                RequireActiveInstance();
                return DescribeCameraBounds(activeInstance.cameraBounds);
            }
        }

        public static string CollisionBoundsSummary
        {
            get
            {
                RequireActiveInstance();
                return activeInstance.DescribeCollisionBounds();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            activeOperation = null;
            activeInstance = null;
            pendingConfiguration = null;
            activeConfiguration = null;
            activeInstanceCount = 0;
        }

        private void OnEnable()
        {
            CaptureAuthoredState();
            EnsurePlaceholderVisuals();
            ResetArenaInternal();

            if (!Application.isPlaying)
            {
                return;
            }

            if (activeInstance != null && activeInstance != this)
            {
                enabled = false;
                throw new InvalidOperationException(
                    "Only one Stage1BenchmarkArenaFixture may be active.");
            }

            activeInstance = this;
            activeConfiguration = pendingConfiguration;
            CountRuntimeInstance();
        }

        private void Start()
        {
            CountRuntimeInstance();
        }

        private void OnDisable()
        {
            ReleaseRuntimeInstance();
            ReleaseActiveInstance();
            DestroyPlaceholderVisuals();
        }

        private void OnDestroy()
        {
            ReleaseRuntimeInstance();
            ReleaseActiveInstance();
            DestroyPlaceholderVisuals();
        }

        public static AsyncOperation LoadFromCanonicalConfiguration(string canonicalJson)
        {
            EvidenceRunConfigurationLoadResult loadResult =
                EvidenceRunConfigurationLoader.Load(canonicalJson);
            if (!loadResult.IsValid)
            {
                throw new InvalidOperationException(
                    "EH-002 evidence configuration was rejected: "
                    + loadResult.ErrorCode + ": " + loadResult.ErrorMessage);
            }

            return LoadAdditively(loadResult.Configuration);
        }

        public static AsyncOperation LoadAdditively(
            EvidenceRunConfiguration configuration)
        {
            RequirePlayMode();
            RequireNoOperation();

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (IsLoaded)
            {
                throw new InvalidOperationException(
                    "Stage1BenchmarkArena is already loaded; duplicate loads are rejected.");
            }

            Scene bootstrap = RequireBootstrapScene();
            EnsureBootstrapRunning(bootstrap);

            pendingConfiguration = configuration;
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

            Scene scene = GetArenaScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException(
                    "Stage1BenchmarkArena cannot unload because it is not loaded.");
            }

            return Track(SceneManager.UnloadSceneAsync(scene));
        }

        public static string ResetActiveArena()
        {
            RequirePlayMode();
            RequireNoOperation();
            RequireActiveInstance();

            activeInstance.ResetArenaInternal();
            string[] errors = activeInstance.ValidateArenaInternal();
            if (errors.Length != 0)
            {
                throw new InvalidOperationException(
                    "Stage1BenchmarkArena reset produced an invalid shell:\n"
                    + string.Join("\n", errors));
            }

            return activeInstance.CaptureSnapshotInternal();
        }

        public static string CaptureActiveSnapshot()
        {
            RequireActiveInstance();
            return activeInstance.CaptureSnapshotInternal();
        }

        public static string[] ValidateActiveArena()
        {
            RequireActiveInstance();
            return activeInstance.ValidateArenaInternal();
        }

        public static string[] GetMarkerIds()
        {
            RequireActiveInstance();
            return activeInstance.markers
                .Where(binding => binding != null)
                .Select(binding => binding.MarkerId)
                .OrderBy(markerId => markerId, StringComparer.Ordinal)
                .ToArray();
        }

        public static void SetMarkerActiveForTest(string markerId, bool active)
        {
            Stage1BenchmarkArenaMarkerBinding binding =
                RequireMarkerBinding(markerId);
            binding.Socket.gameObject.SetActive(active);
        }

        public static void SetMarkerLocalPositionForTest(
            string markerId,
            float x,
            float y,
            float z)
        {
            Stage1BenchmarkArenaMarkerBinding binding =
                RequireMarkerBinding(markerId);
            binding.Socket.localPosition = new Vector3(x, y, z);
        }

        private static Stage1BenchmarkArenaMarkerBinding RequireMarkerBinding(
            string markerId)
        {
            RequireActiveInstance();

            Stage1BenchmarkArenaMarkerBinding binding =
                activeInstance.markers.FirstOrDefault(
                    candidate => candidate != null
                        && string.Equals(
                            candidate.MarkerId,
                            markerId,
                            StringComparison.Ordinal));
            if (binding == null)
            {
                throw new InvalidOperationException(
                    "Unknown Stage1BenchmarkArena marker ID '" + markerId + "'.");
            }

            if (binding.Socket == null)
            {
                throw new InvalidOperationException(
                    "Stage1BenchmarkArena marker '" + markerId
                    + "' has no socket reference.");
            }

            return binding;
        }

        private void CaptureAuthoredState()
        {
            if (authoredStateCaptured)
            {
                return;
            }

            authoredStates.Clear();
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (IsOwnedVisual(candidate))
                {
                    continue;
                }

                authoredStates.Add(new AuthoredTransformState(candidate));
            }

            authoredStateCaptured = true;
        }

        private void ResetArenaInternal()
        {
            for (int index = 0; index < authoredStates.Count; index++)
            {
                authoredStates[index].Restore();
            }

            EnsurePlaceholderVisuals();
            ConfigureAllVisuals();
        }

        private string[] ValidateArenaInternal()
        {
            var errors = new List<string>();

            if (arenaRoot == null)
            {
                errors.Add("missing-arena-root");
            }
            else if (arenaRoot != transform)
            {
                errors.Add("arena-root-reference-mismatch");
            }

            ValidateCameraBounds(errors);
            ValidateCollisionEnvelope(errors);
            ValidateMarkers(errors);
            ValidateFully2D(errors);
            ValidateNoPrefabOrRemoteDependencies(errors);

            return errors
                .OrderBy(error => error, StringComparer.Ordinal)
                .ToArray();
        }

        private void ValidateCameraBounds(List<string> errors)
        {
            if (cameraBounds == null)
            {
                errors.Add("missing-camera-bounds");
                return;
            }

            if (!cameraBounds.gameObject.activeInHierarchy)
            {
                errors.Add("inactive-camera-bounds");
            }

            if (!cameraBounds.enabled || !cameraBounds.isTrigger)
            {
                errors.Add("invalid-camera-bounds-collider");
            }

            if (!Approximately(cameraBounds.size, new Vector2(24f, 14f))
                || !Approximately(cameraBounds.offset, Vector2.zero)
                || !Approximately(cameraBounds.transform.position, Vector3.zero))
            {
                errors.Add("camera-bounds-drift");
            }
        }

        private void ValidateCollisionEnvelope(List<string> errors)
        {
            if (collisionEnvelope == null)
            {
                errors.Add("missing-collision-envelope");
            }

            if (collisionWalls == null || collisionWalls.Length != 4)
            {
                errors.Add("invalid-collision-wall-count");
                return;
            }

            Bounds? combined = null;
            var wallNames = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < collisionWalls.Length; index++)
            {
                BoxCollider2D wall = collisionWalls[index];
                if (wall == null)
                {
                    errors.Add(
                        "missing-collision-wall:"
                        + index.ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                if (!wallNames.Add(wall.gameObject.name))
                {
                    errors.Add("duplicate-collision-wall:" + wall.gameObject.name);
                }

                if (!wall.enabled || wall.isTrigger || !wall.gameObject.activeInHierarchy)
                {
                    errors.Add("invalid-collision-wall:" + wall.gameObject.name);
                }

                combined = combined.HasValue
                    ? Encapsulate(combined.Value, wall.bounds)
                    : wall.bounds;
            }

            if (!combined.HasValue)
            {
                return;
            }

            Bounds bounds = combined.Value;
            Vector3 expectedCenter = Vector3.zero;
            Vector3 expectedSize = new Vector3(25f, 15f, 0f);
            if (!Approximately(bounds.center, expectedCenter)
                || !Approximately(
                    new Vector3(bounds.size.x, bounds.size.y, 0f),
                    expectedSize))
            {
                errors.Add("collision-bounds-drift");
            }

            if (cameraBounds != null)
            {
                Bounds camera = cameraBounds.bounds;
                if (camera.min.x <= bounds.min.x
                    || camera.max.x >= bounds.max.x
                    || camera.min.y <= bounds.min.y
                    || camera.max.y >= bounds.max.y)
                {
                    errors.Add("camera-bounds-not-contained");
                }
            }
        }

        private void ValidateMarkers(List<string> errors)
        {
            if (markers == null)
            {
                errors.Add("missing-marker-bindings");
                return;
            }

            var markerIds = new HashSet<string>(StringComparer.Ordinal);
            var sockets = new HashSet<Transform>();
            var counts = new Dictionary<Stage1BenchmarkArenaMarkerKind, int>();

            for (int index = 0; index < markers.Length; index++)
            {
                Stage1BenchmarkArenaMarkerBinding binding = markers[index];
                if (binding == null)
                {
                    errors.Add(
                        "missing-marker-binding:"
                        + index.ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(binding.MarkerId))
                {
                    errors.Add(
                        "empty-marker-id:"
                        + index.ToString(CultureInfo.InvariantCulture));
                }
                else if (!markerIds.Add(binding.MarkerId))
                {
                    errors.Add("duplicate-marker-id:" + binding.MarkerId);
                }

                if (binding.Socket == null)
                {
                    errors.Add("missing-socket:" + binding.MarkerId);
                    continue;
                }

                if (!sockets.Add(binding.Socket))
                {
                    errors.Add("duplicate-socket-reference:" + binding.MarkerId);
                }

                if (!binding.Socket.gameObject.activeInHierarchy)
                {
                    errors.Add("missing-socket:" + binding.MarkerId);
                }

                int count;
                counts.TryGetValue(binding.Kind, out count);
                counts[binding.Kind] = count + 1;
            }

            for (int index = 0; index < RequiredMarkerIds.Length; index++)
            {
                string required = RequiredMarkerIds[index];
                if (!markerIds.Contains(required))
                {
                    errors.Add("missing-marker-id:" + required);
                }
            }

            ValidateKindCount(counts, Stage1BenchmarkArenaMarkerKind.Shell, 1, errors);
            ValidateKindCount(counts, Stage1BenchmarkArenaMarkerKind.PlayerSpawn, 1, errors);
            ValidateKindCount(counts, Stage1BenchmarkArenaMarkerKind.TargetSpawn, 4, errors);
            ValidateKindCount(counts, Stage1BenchmarkArenaMarkerKind.HazardSpawn, 2, errors);
            ValidateKindCount(counts, Stage1BenchmarkArenaMarkerKind.CameraBounds, 1, errors);
            ValidateKindCount(counts, Stage1BenchmarkArenaMarkerKind.CollisionEnvelope, 1, errors);
            ValidateKindCount(counts, Stage1BenchmarkArenaMarkerKind.PerformanceProbe, 3, errors);
            ValidateKindCount(counts, Stage1BenchmarkArenaMarkerKind.CombatHook, 2, errors);
        }

        private static void ValidateKindCount(
            IDictionary<Stage1BenchmarkArenaMarkerKind, int> counts,
            Stage1BenchmarkArenaMarkerKind kind,
            int expected,
            ICollection<string> errors)
        {
            int actual;
            counts.TryGetValue(kind, out actual);
            if (actual != expected)
            {
                errors.Add(
                    "marker-kind-count:"
                    + kind + ":expected="
                    + expected.ToString(CultureInfo.InvariantCulture)
                    + ":actual="
                    + actual.ToString(CultureInfo.InvariantCulture));
            }
        }

        private void ValidateFully2D(List<string> errors)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (!Approximately(candidate.position.z, 0f))
                {
                    errors.Add("non-planar-transform:" + BuildPath(candidate));
                }
            }

            Component[] components = GetComponentsInChildren<Component>(true);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null)
                {
                    errors.Add("missing-script-component");
                    continue;
                }

                if (component is Collider
                    || component is Rigidbody
                    || component is Joint
                    || component is CharacterController
                    || component is Camera
                    || component is Light)
                {
                    errors.Add(
                        "forbidden-non-2d-component:"
                        + component.GetType().FullName
                        + "@"
                        + BuildPath(component.transform));
                }
            }
        }

        private void ValidateNoPrefabOrRemoteDependencies(List<string> errors)
        {
#if UNITY_EDITOR
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms =
                    roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int transformIndex = 0;
                     transformIndex < transforms.Length;
                     transformIndex++)
                {
                    if (PrefabUtility.IsPartOfAnyPrefab(
                        transforms[transformIndex].gameObject))
                    {
                        errors.Add(
                            "unowned-prefab-instance:"
                            + BuildPath(transforms[transformIndex]));
                    }
                }
            }
#endif

            Component[] components = GetComponentsInChildren<Component>(true);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null)
                {
                    continue;
                }

                string namespaceName = component.GetType().Namespace ?? string.Empty;
                if (namespaceName.StartsWith(
                    "UnityEngine.Networking",
                    StringComparison.Ordinal))
                {
                    errors.Add(
                        "remote-dependency:"
                        + component.GetType().FullName);
                }
            }
        }

        private string CaptureSnapshotInternal()
        {
            var builder = new StringBuilder(8192);
            builder.Append("schema=").Append(SnapshotSchema).Append('\n');
            builder.Append("version=")
                .Append(SnapshotVersion.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            builder.Append("scene=").Append(SceneName).Append('\n');
            builder.Append("configurationFingerprint=")
                .Append(ResolvedConfigurationFingerprint)
                .Append('\n');
            builder.Append("runSeed=")
                .Append(ResolvedRunSeed.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            builder.Append("cameraBounds=")
                .Append(DescribeCameraBounds(cameraBounds))
                .Append('\n');
            builder.Append("collisionBounds=")
                .Append(DescribeCollisionBounds())
                .Append('\n');

            Stage1BenchmarkArenaMarkerBinding[] orderedMarkers = markers
                .Where(binding => binding != null)
                .OrderBy(binding => binding.MarkerId, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < orderedMarkers.Length; index++)
            {
                Stage1BenchmarkArenaMarkerBinding binding = orderedMarkers[index];
                builder.Append("marker|")
                    .Append(binding.MarkerId)
                    .Append('|')
                    .Append(binding.Kind)
                    .Append('|')
                    .Append(BuildPath(binding.Socket))
                    .Append("|sortingOrder=")
                    .Append(binding.SortingOrder.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
            }

            Transform[] transforms = GetComponentsInChildren<Transform>(true)
                .OrderBy(BuildPath, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                builder.Append("object|")
                    .Append(BuildPath(candidate))
                    .Append("|active=")
                    .Append(candidate.gameObject.activeSelf ? "1" : "0")
                    .Append("|localPosition=")
                    .Append(Format(candidate.localPosition))
                    .Append("|localRotation=")
                    .Append(Format(candidate.localRotation))
                    .Append("|localScale=")
                    .Append(Format(candidate.localScale))
                    .Append('\n');

                Component[] components = candidate.GetComponents<Component>()
                    .Where(component => component != null)
                    .OrderBy(
                        component => component.GetType().FullName,
                        StringComparer.Ordinal)
                    .ToArray();
                for (int componentIndex = 0;
                     componentIndex < components.Length;
                     componentIndex++)
                {
                    Component component = components[componentIndex];
                    builder.Append("component|")
                        .Append(BuildPath(candidate))
                        .Append('|')
                        .Append(component.GetType().FullName);

                    Renderer renderer = component as Renderer;
                    if (renderer != null)
                    {
                        builder.Append("|enabled=")
                            .Append(renderer.enabled ? "1" : "0")
                            .Append("|sortingLayer=")
                            .Append(renderer.sortingLayerName)
                            .Append("|sortingOrder=")
                            .Append(
                                renderer.sortingOrder.ToString(
                                    CultureInfo.InvariantCulture));
                    }

                    BoxCollider2D box = component as BoxCollider2D;
                    if (box != null)
                    {
                        builder.Append("|enabled=")
                            .Append(box.enabled ? "1" : "0")
                            .Append("|trigger=")
                            .Append(box.isTrigger ? "1" : "0")
                            .Append("|offset=")
                            .Append(Format(box.offset))
                            .Append("|size=")
                            .Append(Format(box.size));
                    }

                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }

        private void EnsurePlaceholderVisuals()
        {
            EnsureVisualSprite();

            if (markers != null)
            {
                for (int index = 0; index < markers.Length; index++)
                {
                    Stage1BenchmarkArenaMarkerBinding binding = markers[index];
                    if (binding != null && binding.Socket != null)
                    {
                        EnsureVisualChild(binding.Socket, VisualChildName);
                    }
                }
            }

            if (collisionWalls != null)
            {
                for (int index = 0; index < collisionWalls.Length; index++)
                {
                    BoxCollider2D wall = collisionWalls[index];
                    if (wall != null)
                    {
                        EnsureVisualChild(wall.transform, WallVisualChildName);
                    }
                }
            }
        }

        private void ConfigureAllVisuals()
        {
            if (markers != null)
            {
                for (int index = 0; index < markers.Length; index++)
                {
                    Stage1BenchmarkArenaMarkerBinding binding = markers[index];
                    if (binding == null || binding.Socket == null)
                    {
                        continue;
                    }

                    Transform visual =
                        RequireOwnedChild(binding.Socket, VisualChildName);
                    ConfigureMarkerVisual(binding, visual);
                }
            }

            if (collisionWalls != null)
            {
                for (int index = 0; index < collisionWalls.Length; index++)
                {
                    BoxCollider2D wall = collisionWalls[index];
                    if (wall == null)
                    {
                        continue;
                    }

                    Transform visual =
                        RequireOwnedChild(wall.transform, WallVisualChildName);
                    visual.localPosition = Vector3.zero;
                    visual.localRotation = Quaternion.identity;
                    visual.localScale =
                        new Vector3(wall.size.x, wall.size.y, 1f);
                    visual.gameObject.SetActive(true);

                    SpriteRenderer renderer =
                        visual.GetComponent<SpriteRenderer>();
                    renderer.sprite = visualSprite;
                    renderer.color = new Color32(70, 220, 255, 210);
                    renderer.sortingLayerName = "Default";
                    renderer.sortingOrder = -80;
                }
            }
        }

        private void ConfigureMarkerVisual(
            Stage1BenchmarkArenaMarkerBinding binding,
            Transform visual)
        {
            Vector2 size;
            Color32 color;
            switch (binding.Kind)
            {
                case Stage1BenchmarkArenaMarkerKind.Shell:
                    size = new Vector2(24f, 14f);
                    color = new Color32(22, 28, 36, 255);
                    break;
                case Stage1BenchmarkArenaMarkerKind.PlayerSpawn:
                    size = new Vector2(0.9f, 0.9f);
                    color = new Color32(80, 255, 155, 255);
                    break;
                case Stage1BenchmarkArenaMarkerKind.TargetSpawn:
                    size = new Vector2(0.75f, 0.75f);
                    color = new Color32(255, 210, 70, 255);
                    break;
                case Stage1BenchmarkArenaMarkerKind.HazardSpawn:
                    size = new Vector2(0.85f, 0.85f);
                    color = new Color32(255, 90, 90, 255);
                    break;
                case Stage1BenchmarkArenaMarkerKind.CameraBounds:
                    size = new Vector2(24f, 14f);
                    color = new Color32(80, 150, 255, 28);
                    break;
                case Stage1BenchmarkArenaMarkerKind.CollisionEnvelope:
                    size = new Vector2(25f, 15f);
                    color = new Color32(70, 220, 255, 18);
                    break;
                case Stage1BenchmarkArenaMarkerKind.PerformanceProbe:
                    size = new Vector2(0.35f, 0.35f);
                    color = new Color32(210, 110, 255, 255);
                    break;
                case Stage1BenchmarkArenaMarkerKind.CombatHook:
                    size = new Vector2(0.5f, 0.5f);
                    color = new Color32(245, 245, 245, 255);
                    break;
                default:
                    size = Vector2.one;
                    color = new Color32(255, 0, 255, 255);
                    break;
            }

            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(size.x, size.y, 1f);
            visual.gameObject.SetActive(true);

            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            renderer.sprite = visualSprite;
            renderer.color = color;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = binding.SortingOrder;
        }

        private void EnsureVisualSprite()
        {
            if (visualSprite != null)
            {
                return;
            }

            visualTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            visualTexture.name = "EH004 Placeholder Pixel";
            visualTexture.hideFlags = HideFlags.HideAndDontSave;
            visualTexture.SetPixel(0, 0, Color.white);
            visualTexture.Apply(false, true);

            visualSprite = Sprite.Create(
                visualTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            visualSprite.name = "EH004 Placeholder Sprite";
            visualSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        private static Transform EnsureVisualChild(
            Transform parent,
            string childName)
        {
            Transform existing = FindOwnedChild(parent, childName);
            if (existing != null)
            {
                if (existing.GetComponent<SpriteRenderer>() == null)
                {
                    existing.gameObject.AddComponent<SpriteRenderer>();
                }

                return existing;
            }

            var visualObject = new GameObject(childName, typeof(SpriteRenderer));
            visualObject.hideFlags = HideFlags.DontSave;
            visualObject.transform.SetParent(parent, false);
            return visualObject.transform;
        }

        private static Transform RequireOwnedChild(
            Transform parent,
            string childName)
        {
            Transform child = FindOwnedChild(parent, childName);
            if (child == null)
            {
                throw new InvalidOperationException(
                    "Missing owned EH-004 visual child '" + childName
                    + "' under " + BuildPath(parent) + ".");
            }

            return child;
        }

        private static Transform FindOwnedChild(
            Transform parent,
            string childName)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (string.Equals(
                    child.gameObject.name,
                    childName,
                    StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private void DestroyPlaceholderVisuals()
        {
            var ownedVisuals = new List<GameObject>();
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (IsOwnedVisual(transforms[index]))
                {
                    ownedVisuals.Add(transforms[index].gameObject);
                }
            }

            for (int index = 0; index < ownedVisuals.Count; index++)
            {
                DestroyOwnedObject(ownedVisuals[index]);
            }

            if (visualSprite != null)
            {
                DestroyOwnedObject(visualSprite);
                visualSprite = null;
            }

            if (visualTexture != null)
            {
                DestroyOwnedObject(visualTexture);
                visualTexture = null;
            }
        }

        private static bool IsOwnedVisual(Transform transform)
        {
            return transform != null
                && (string.Equals(
                        transform.gameObject.name,
                        VisualChildName,
                        StringComparison.Ordinal)
                    || string.Equals(
                        transform.gameObject.name,
                        WallVisualChildName,
                        StringComparison.Ordinal));
        }

        private static void DestroyOwnedObject(UnityEngine.Object ownedObject)
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

        private void CountRuntimeInstance()
        {
            if (Application.isPlaying && !countedRuntimeInstance)
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

        private void ReleaseActiveInstance()
        {
            if (activeInstance == this)
            {
                activeInstance = null;
                activeConfiguration = null;
            }
        }

        private static AsyncOperation Track(AsyncOperation operation)
        {
            if (operation == null)
            {
                pendingConfiguration = null;
                throw new InvalidOperationException(
                    "Unity did not create the requested Stage1BenchmarkArena operation.");
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

            if (!IsLoaded)
            {
                pendingConfiguration = null;
                activeConfiguration = null;
            }
        }

        private static void RequirePlayMode()
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException(
                    "Stage1BenchmarkArena scene operations require Play Mode.");
            }
        }

        private static void RequireNoOperation()
        {
            if (activeOperation != null)
            {
                throw new InvalidOperationException(
                    "A Stage1BenchmarkArena load or unload is already in flight.");
            }
        }

        private static void RequireActiveInstance()
        {
            if (activeInstance == null
                || !activeInstance.isActiveAndEnabled
                || !activeInstance.gameObject.scene.isLoaded)
            {
                throw new InvalidOperationException(
                    "Stage1BenchmarkArena has no active fixture instance.");
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
                    "Bootstrap must be loaded before Stage1BenchmarkArena is exercised.");
            }

            return scene;
        }

        private static Scene GetArenaScene()
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
                    + adapterCount.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }
        }

        private static string DescribeCameraBounds(BoxCollider2D bounds)
        {
            if (bounds == null)
            {
                return "missing";
            }

            return "center=" + Format(bounds.bounds.center)
                + ";size=" + Format(
                    new Vector3(
                        bounds.bounds.size.x,
                        bounds.bounds.size.y,
                        0f));
        }

        private string DescribeCollisionBounds()
        {
            if (collisionWalls == null
                || collisionWalls.Length == 0
                || collisionWalls.Any(wall => wall == null))
            {
                return "missing";
            }

            Bounds combined = collisionWalls[0].bounds;
            for (int index = 1; index < collisionWalls.Length; index++)
            {
                combined = Encapsulate(combined, collisionWalls[index].bounds);
            }

            return "min=" + Format(
                    new Vector3(combined.min.x, combined.min.y, 0f))
                + ";max=" + Format(
                    new Vector3(combined.max.x, combined.max.y, 0f))
                + ";walls="
                + collisionWalls.Length.ToString(CultureInfo.InvariantCulture);
        }

        private static Bounds Encapsulate(Bounds first, Bounds second)
        {
            first.Encapsulate(second);
            return first;
        }

        private static string BuildPath(Transform transform)
        {
            if (transform == null)
            {
                return "<missing>";
            }

            var segments = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                segments.Push(current.gameObject.name);
                current = current.parent;
            }

            return string.Join("/", segments.ToArray());
        }

        private static string Format(Vector2 value)
        {
            return Format(value.x) + "," + Format(value.y);
        }

        private static string Format(Vector3 value)
        {
            return Format(value.x) + "," + Format(value.y) + "," + Format(value.z);
        }

        private static string Format(Quaternion value)
        {
            return Format(value.x) + "," + Format(value.y)
                + "," + Format(value.z) + "," + Format(value.w);
        }

        private static string Format(float value)
        {
            if (Mathf.Abs(value) < 0.0000005f)
            {
                value = 0f;
            }

            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool Approximately(Vector2 actual, Vector2 expected)
        {
            return Approximately(actual.x, expected.x)
                && Approximately(actual.y, expected.y);
        }

        private static bool Approximately(Vector3 actual, Vector3 expected)
        {
            return Approximately(actual.x, expected.x)
                && Approximately(actual.y, expected.y)
                && Approximately(actual.z, expected.z);
        }

        private static bool Approximately(float actual, float expected)
        {
            return Mathf.Abs(actual - expected) <= 0.0001f;
        }

        private sealed class AuthoredTransformState
        {
            private readonly Transform transform;
            private readonly Vector3 localPosition;
            private readonly Quaternion localRotation;
            private readonly Vector3 localScale;
            private readonly bool activeSelf;

            public AuthoredTransformState(Transform transform)
            {
                this.transform = transform;
                localPosition = transform.localPosition;
                localRotation = transform.localRotation;
                localScale = transform.localScale;
                activeSelf = transform.gameObject.activeSelf;
            }

            public void Restore()
            {
                if (transform == null)
                {
                    throw new InvalidOperationException(
                        "An authored Stage1BenchmarkArena transform was destroyed.");
                }

                transform.localPosition = localPosition;
                transform.localRotation = localRotation;
                transform.localScale = localScale;
                transform.gameObject.SetActive(activeSelf);
            }
        }
    }
}
