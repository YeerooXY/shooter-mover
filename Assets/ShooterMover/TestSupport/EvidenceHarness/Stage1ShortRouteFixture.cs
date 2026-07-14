using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Bootstrap.Unity;
using ShooterMover.Contracts.Mission;
using ShooterMover.Contracts.Rooms;
using ShooterMover.Domain.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ShooterMover.TestSupport.EvidenceHarness
{
    public enum Stage1ShortRouteMarkerKind
    {
        Start = 0,
        ArenaEntry = 1,
        Connector = 2,
        ReviewEnd = 3,
        Restart = 4
    }

    [Serializable]
    public sealed class Stage1ShortRouteMarkerBinding
    {
        [SerializeField]
        private string markerId;

        [SerializeField]
        private Stage1ShortRouteMarkerKind kind;

        [SerializeField]
        private string roomId;

        [SerializeField]
        private string projectionId;

        [SerializeField]
        private Transform anchor;

        [SerializeField]
        private int loadOrder;

        public string MarkerId
        {
            get { return markerId; }
        }

        public Stage1ShortRouteMarkerKind Kind
        {
            get { return kind; }
        }

        public string RoomId
        {
            get { return roomId; }
        }

        public string ProjectionId
        {
            get { return projectionId; }
        }

        public Transform Anchor
        {
            get { return anchor; }
        }

        public int LoadOrder
        {
            get { return loadOrder; }
        }
    }

    /// <summary>
    /// Stable-address-only connection declaration. It deliberately contains no
    /// Unity object reference, so additive room boundaries cannot be coupled by
    /// scene object identity.
    /// </summary>
    [Serializable]
    public sealed class Stage1ShortRouteConnectionBinding
    {
        [SerializeField]
        private string connectionId;

        [SerializeField]
        private string fromMarkerId;

        [SerializeField]
        private string fromSocketId;

        [SerializeField]
        private string toMarkerId;

        [SerializeField]
        private string toSocketId;

        public string ConnectionId
        {
            get { return connectionId; }
        }

        public string FromMarkerId
        {
            get { return fromMarkerId; }
        }

        public string FromSocketId
        {
            get { return fromSocketId; }
        }

        public string ToMarkerId
        {
            get { return toMarkerId; }
        }

        public string ToSocketId
        {
            get { return toSocketId; }
        }
    }

    /// <summary>
    /// Immutable test projection returned through Room Projection v1's read-only
    /// state-reader port. It describes presentation markers only.
    /// </summary>
    public sealed class Stage1ShortRouteProjection
    {
        public Stage1ShortRouteProjection(
            string markerId,
            Stage1ShortRouteMarkerKind kind,
            int loadOrder,
            float x,
            float y)
        {
            MarkerId = markerId ?? throw new ArgumentNullException(nameof(markerId));
            Kind = kind;
            LoadOrder = loadOrder;
            X = x;
            Y = y;
        }

        public string MarkerId { get; }

        public Stage1ShortRouteMarkerKind Kind { get; }

        public int LoadOrder { get; }

        public float X { get; }

        public float Y { get; }

        public string ToCanonicalString()
        {
            return "marker=" + MarkerId
                + ";kind=" + Kind
                + ";loadOrder=" + LoadOrder.ToString(CultureInfo.InvariantCulture)
                + ";position=" + FormatFloat(X) + "," + FormatFloat(Y);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Test-owned additive Stage 1 route shell. It projects five local markers,
    /// resolves cross-space links only through stable Room Projection v1 values,
    /// and keeps all lifecycle state presentation-only and disposable.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Stage1ShortRouteFixture : MonoBehaviour
    {
        public const string BootstrapSceneName = "Bootstrap";
        public const string BootstrapScenePath =
            "Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity";
        public const string SceneName = "Stage1ShortRouteShell";
        public const string ScenePath =
            "Assets/ShooterMover/Tests/PlayMode/EvidenceHarness/Scenes/Stage1ShortRouteShell.unity";
        public const string ConfigurationFixturePath =
            "tools/evidence/fixtures/stage1-evidence-config-v1.json";
        public const string SnapshotSchema =
            "shooter-mover.stage1-short-route-shell-snapshot";
        public const int SnapshotVersion = 1;

        private const string VisualRootName = "__EH005Visuals";
        private const string RunId = "run.eh005-short-route";

        private static readonly string[] ExpectedTraversal =
        {
            "route.start",
            "route.arena-entry",
            "route.connector",
            "route.review-end",
            "route.restart"
        };

        private static readonly string[] ExpectedConnections =
        {
            "connection.start-arena",
            "connection.arena-connector",
            "connection.connector-review",
            "connection.review-restart",
            "connection.restart-start"
        };

        private static AsyncOperation activeOperation;
        private static Stage1ShortRouteFixture activeInstance;
        private static EvidenceRunConfiguration pendingConfiguration;
        private static EvidenceRunConfiguration activeConfiguration;
        private static int activeInstanceCount;

        [SerializeField]
        private Transform routeRoot;

        [SerializeField]
        private Stage1ShortRouteMarkerBinding[] markers;

        [SerializeField]
        private Stage1ShortRouteConnectionBinding[] connections;

        private readonly Dictionary<string, MarkerRuntime> runtimeByMarker =
            new Dictionary<string, MarkerRuntime>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> connectionTargetOverrides =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> lastLifecycleEvents = new List<string>();

        private ProjectionReader projectionReader;
        private GameObject visualRoot;
        private GameObject cursorVisual;
        private TextMesh statusLabel;
        private Texture2D visualTexture;
        private Sprite visualSprite;
        private int cursorIndex;
        private bool modelInitialized;
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
                Scene scene = GetRouteScene();
                return scene.IsValid() && scene.isLoaded;
            }
        }

        public static int ResolvedRunSeed
        {
            get { return activeConfiguration == null ? 0 : activeConfiguration.RunSeed; }
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

        public static string ProjectionReaderContractName
        {
            get { return typeof(IRoomProjectionStateReader).FullName; }
        }

        public static int ProjectionReadCount
        {
            get
            {
                RequireActiveInstance();
                return activeInstance.projectionReader.ReadCount;
            }
        }

        public static int MissionCommandSubmissionCount
        {
            get { return 0; }
        }

        public static string CursorMarkerId
        {
            get
            {
                RequireActiveInstance();
                return ExpectedTraversal[activeInstance.cursorIndex];
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
            if (UnityEngine.Application.isPlaying)
            {
                if (activeInstance != null && activeInstance != this)
                {
                    enabled = false;
                    throw new InvalidOperationException(
                        "Only one Stage1ShortRouteFixture may be active.");
                }

                activeInstance = this;
                activeConfiguration = pendingConfiguration;
                CountRuntimeInstance();
            }

            InitializeModel();
            EnsurePlaceholderVisuals();

            if (UnityEngine.Application.isPlaying)
            {
                EnterRouteInternal();
            }
        }

        private void Start()
        {
            CountRuntimeInstance();
        }

        private void Update()
        {
            if (!UnityEngine.Application.isPlaying || activeInstance != this)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.rightArrowKey.wasPressedThisFrame
                || keyboard.dKey.wasPressedThisFrame)
            {
                MoveCursorNextInternal();
            }
            else if (keyboard.leftArrowKey.wasPressedThisFrame
                || keyboard.aKey.wasPressedThisFrame)
            {
                MoveCursorPreviousInternal();
            }
            else if (keyboard.rKey.wasPressedThisFrame
                || keyboard.homeKey.wasPressedThisFrame)
            {
                RestartCursorInternal();
            }
        }

        private void OnDisable()
        {
            ReleaseRouteInstance();
            DestroyPlaceholderVisuals();
        }

        private void OnDestroy()
        {
            ReleaseRouteInstance();
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
                    "Stage1ShortRouteShell is already loaded; duplicate loads are rejected.");
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

            Scene scene = GetRouteScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException(
                    "Stage1ShortRouteShell cannot unload because it is not loaded.");
            }

            if (activeInstance != null)
            {
                activeInstance.LeaveRouteInternal();
            }

            return Track(SceneManager.UnloadSceneAsync(scene));
        }

        public static string EnterRoute()
        {
            RequireActiveInstance();
            return activeInstance.EnterRouteInternal();
        }

        public static string LeaveRoute()
        {
            RequireActiveInstance();
            return activeInstance.LeaveRouteInternal();
        }

        public static string ReloadRoute()
        {
            RequireActiveInstance();
            return activeInstance.ReloadRouteInternal();
        }

        public static string BeginInterruptedUnloadForTest(string markerId)
        {
            RequireActiveInstance();
            MarkerRuntime marker = activeInstance.RequireMarker(markerId);
            activeInstance.lastLifecycleEvents.Clear();
            activeInstance.ApplyTransition(
                marker,
                marker.Lifecycle.BeginUnload(),
                "interrupt-begin",
                false);
            return activeInstance.CaptureLastLifecycleBatchInternal();
        }

        public static string ResumeInterruptedUnloadForTest(string markerId)
        {
            RequireActiveInstance();
            MarkerRuntime marker = activeInstance.RequireMarker(markerId);
            activeInstance.lastLifecycleEvents.Clear();
            activeInstance.ApplyTransition(
                marker,
                marker.Lifecycle.ResumeAfterInterruptedUnload(),
                "interrupt-resume",
                true);
            return activeInstance.CaptureLastLifecycleBatchInternal();
        }

        public static string GetMarkerPhaseForTest(string markerId)
        {
            RequireActiveInstance();
            return activeInstance.RequireMarker(markerId).Lifecycle.Phase.ToString();
        }

        public static string ReadProjectionForTest(string markerId)
        {
            RequireActiveInstance();
            MarkerRuntime marker = activeInstance.RequireMarker(markerId);
            RoomProjectionReadResult<Stage1ShortRouteProjection> result =
                activeInstance.projectionReader.Read<Stage1ShortRouteProjection>(marker.Key);
            return DescribeRead(result);
        }

        public static string ReadUnknownProjectionForTest(string roomId)
        {
            RequireActiveInstance();
            var key = new RoomProjectionKey(
                StableId.Parse(RunId),
                StableId.Parse(roomId),
                MissionSequence.Initial);
            RoomProjectionReadResult<Stage1ShortRouteProjection> result =
                activeInstance.projectionReader.Read<Stage1ShortRouteProjection>(key);
            return DescribeRead(result);
        }

        public static void SetConnectionTargetOverrideForTest(
            string connectionId,
            string targetMarkerId)
        {
            RequireActiveInstance();
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                throw new ArgumentException("Connection ID is required.", nameof(connectionId));
            }

            if (string.IsNullOrWhiteSpace(targetMarkerId))
            {
                throw new ArgumentException("Target marker ID is required.", nameof(targetMarkerId));
            }

            activeInstance.connectionTargetOverrides[connectionId] = targetMarkerId;
            activeInstance.RebuildConnectionVisuals();
        }

        public static void ClearConnectionTargetOverridesForTest()
        {
            RequireActiveInstance();
            activeInstance.connectionTargetOverrides.Clear();
            activeInstance.RebuildConnectionVisuals();
        }

        public static string MoveCursorNext()
        {
            RequireActiveInstance();
            return activeInstance.MoveCursorNextInternal();
        }

        public static string MoveCursorPrevious()
        {
            RequireActiveInstance();
            return activeInstance.MoveCursorPreviousInternal();
        }

        public static string RestartCursorToStart()
        {
            RequireActiveInstance();
            return activeInstance.RestartCursorInternal();
        }

        public static string[] GetMarkerIds()
        {
            RequireActiveInstance();
            return activeInstance.markers
                .Where(marker => marker != null)
                .Select(marker => marker.MarkerId)
                .OrderBy(markerId => markerId, StringComparer.Ordinal)
                .ToArray();
        }

        public static string[] GetTraversalMarkerIds()
        {
            return (string[])ExpectedTraversal.Clone();
        }

        public static string[] GetConnectionIds()
        {
            RequireActiveInstance();
            return activeInstance.connections
                .Where(connection => connection != null)
                .Select(connection => connection.ConnectionId)
                .OrderBy(connectionId => connectionId, StringComparer.Ordinal)
                .ToArray();
        }

        public static string[] ValidateActiveRoute()
        {
            RequireActiveInstance();
            return activeInstance.ValidateRouteInternal();
        }

        public static string CaptureActiveSnapshot()
        {
            RequireActiveInstance();
            return activeInstance.CaptureSnapshotInternal();
        }

        public static string CaptureLastLifecycleBatch()
        {
            RequireActiveInstance();
            return activeInstance.CaptureLastLifecycleBatchInternal();
        }

        private static string DescribeRead(
            RoomProjectionReadResult<Stage1ShortRouteProjection> result)
        {
            return result.HasValue
                ? "status=" + result.Status + ";value=" + result.Value.ToCanonicalString()
                : "status=" + result.Status + ";value=<none>";
        }

        private void InitializeModel()
        {
            runtimeByMarker.Clear();
            connectionTargetOverrides.Clear();
            lastLifecycleEvents.Clear();

            var projections =
                new Dictionary<RoomProjectionKey, Stage1ShortRouteProjection>();
            StableId runId = StableId.Parse(RunId);

            Stage1ShortRouteMarkerBinding[] ordered = markers == null
                ? new Stage1ShortRouteMarkerBinding[0]
                : markers
                    .Where(marker => marker != null)
                    .OrderBy(marker => marker.LoadOrder)
                    .ThenBy(marker => marker.MarkerId, StringComparer.Ordinal)
                    .ToArray();

            for (int index = 0; index < ordered.Length; index++)
            {
                Stage1ShortRouteMarkerBinding binding = ordered[index];
                StableId roomId = StableId.Parse(binding.RoomId);
                var identity = new RoomProjectionIdentity(
                    roomId,
                    StableId.Parse(binding.ProjectionId));
                var key = new RoomProjectionKey(
                    runId,
                    roomId,
                    MissionSequence.Initial);
                var projection = new Stage1ShortRouteProjection(
                    binding.MarkerId,
                    binding.Kind,
                    binding.LoadOrder,
                    binding.Anchor == null ? 0f : binding.Anchor.localPosition.x,
                    binding.Anchor == null ? 0f : binding.Anchor.localPosition.y);
                var runtime = new MarkerRuntime(
                    binding,
                    identity,
                    key,
                    RoomProjectionLifecycle.Create(identity),
                    projection);

                runtimeByMarker.Add(binding.MarkerId, runtime);
                projections.Add(key, projection);
            }

            projectionReader = new ProjectionReader(projections);
            cursorIndex = 0;
            modelInitialized = true;
            UpdateCursorVisual();
        }

        private string EnterRouteInternal()
        {
            RequireModel();
            lastLifecycleEvents.Clear();

            MarkerRuntime[] ordered = runtimeByMarker.Values
                .OrderBy(marker => marker.Binding.LoadOrder)
                .ThenBy(marker => marker.Binding.MarkerId, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < ordered.Length; index++)
            {
                MarkerRuntime marker = ordered[index];
                ApplyTransition(
                    marker,
                    marker.Lifecycle.Load(marker.Key),
                    "enter",
                    true);
            }

            UpdateStatusLabel();
            return CaptureLastLifecycleBatchInternal();
        }

        private string LeaveRouteInternal()
        {
            RequireModel();
            lastLifecycleEvents.Clear();

            MarkerRuntime[] ordered = runtimeByMarker.Values
                .OrderByDescending(marker => marker.Binding.LoadOrder)
                .ThenByDescending(marker => marker.Binding.MarkerId, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < ordered.Length; index++)
            {
                MarkerRuntime marker = ordered[index];
                ApplyTransition(
                    marker,
                    marker.Lifecycle.BeginUnload(),
                    "leave-begin",
                    false);
                ApplyTransition(
                    marker,
                    marker.Lifecycle.CompleteUnload(),
                    "leave-complete",
                    false);
            }

            UpdateStatusLabel();
            return CaptureLastLifecycleBatchInternal();
        }

        private string ReloadRouteInternal()
        {
            RequireModel();
            lastLifecycleEvents.Clear();

            MarkerRuntime[] ordered = runtimeByMarker.Values
                .OrderBy(marker => marker.Binding.LoadOrder)
                .ThenBy(marker => marker.Binding.MarkerId, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < ordered.Length; index++)
            {
                MarkerRuntime marker = ordered[index];
                ApplyTransition(
                    marker,
                    marker.Lifecycle.Reload(marker.Key),
                    "reload",
                    true);
            }

            UpdateStatusLabel();
            return CaptureLastLifecycleBatchInternal();
        }

        private void ApplyTransition(
            MarkerRuntime marker,
            RoomProjectionTransition transition,
            string label,
            bool readWhenApplied)
        {
            marker.Lifecycle = transition.Next;
            lastLifecycleEvents.Add(
                label + "|" + marker.Binding.MarkerId
                + "|" + transition.Operation
                + "|" + transition.Kind
                + "|" + transition.Current.Phase
                + "->" + transition.Next.Phase);

            if (!readWhenApplied || !transition.WasApplied || !transition.Next.IsLoaded)
            {
                return;
            }

            RoomProjectionReadResult<Stage1ShortRouteProjection> read =
                projectionReader.Read<Stage1ShortRouteProjection>(marker.Key);
            if (!read.HasValue)
            {
                throw new InvalidOperationException(
                    "Known route projection failed closed for marker '"
                    + marker.Binding.MarkerId + "'.");
            }
        }

        private string[] ValidateRouteInternal()
        {
            var errors = new List<string>();
            ValidateMarkers(errors);
            ValidateConnections(errors);
            ValidateLifecycles(errors);
            ValidateSceneBoundary(errors);
            return errors.OrderBy(error => error, StringComparer.Ordinal).ToArray();
        }

        private void ValidateMarkers(ICollection<string> errors)
        {
            if (routeRoot == null)
            {
                errors.Add("missing-route-root");
            }
            else if (routeRoot != transform)
            {
                errors.Add("route-root-reference-mismatch");
            }

            if (markers == null || markers.Length != ExpectedTraversal.Length)
            {
                errors.Add("marker-count:expected=" + ExpectedTraversal.Length
                    + ":actual=" + (markers == null ? 0 : markers.Length));
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var anchors = new HashSet<Transform>();
            var orders = new HashSet<int>();
            var kinds = new HashSet<Stage1ShortRouteMarkerKind>();

            for (int index = 0; index < markers.Length; index++)
            {
                Stage1ShortRouteMarkerBinding marker = markers[index];
                if (marker == null)
                {
                    errors.Add("missing-marker-binding:" + index);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(marker.MarkerId))
                {
                    errors.Add("empty-marker-id:" + index);
                }
                else if (!ids.Add(marker.MarkerId))
                {
                    errors.Add("duplicate-marker-id:" + marker.MarkerId);
                }

                if (!orders.Add(marker.LoadOrder))
                {
                    errors.Add("duplicate-load-order:" + marker.LoadOrder);
                }

                if (!kinds.Add(marker.Kind))
                {
                    errors.Add("duplicate-marker-kind:" + marker.Kind);
                }

                if (marker.Anchor == null)
                {
                    errors.Add("missing-marker-anchor:" + marker.MarkerId);
                }
                else
                {
                    if (!anchors.Add(marker.Anchor))
                    {
                        errors.Add("duplicate-marker-anchor:" + marker.MarkerId);
                    }

                    if (!marker.Anchor.gameObject.activeInHierarchy)
                    {
                        errors.Add("inactive-marker-anchor:" + marker.MarkerId);
                    }

                    if (marker.Anchor.gameObject.scene != gameObject.scene)
                    {
                        errors.Add("cross-scene-marker-reference:" + marker.MarkerId);
                    }

                    if (!Approximately(marker.Anchor.position.z, 0f))
                    {
                        errors.Add("non-planar-marker:" + marker.MarkerId);
                    }
                }

                StableId ignored;
                if (!StableId.TryParse(marker.RoomId, out ignored))
                {
                    errors.Add("invalid-room-id:" + marker.MarkerId);
                }

                if (!StableId.TryParse(marker.ProjectionId, out ignored))
                {
                    errors.Add("invalid-projection-id:" + marker.MarkerId);
                }
            }

            for (int index = 0; index < ExpectedTraversal.Length; index++)
            {
                if (!ids.Contains(ExpectedTraversal[index]))
                {
                    errors.Add("missing-marker-id:" + ExpectedTraversal[index]);
                }

                if (!orders.Contains(index))
                {
                    errors.Add("missing-load-order:" + index);
                }
            }
        }

        private void ValidateConnections(ICollection<string> errors)
        {
            if (connections == null || connections.Length != ExpectedConnections.Length)
            {
                errors.Add("connection-count:expected=" + ExpectedConnections.Length
                    + ":actual=" + (connections == null ? 0 : connections.Length));
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < connections.Length; index++)
            {
                Stage1ShortRouteConnectionBinding binding = connections[index];
                if (binding == null)
                {
                    errors.Add("missing-connection-binding:" + index);
                    continue;
                }

                if (!ids.Add(binding.ConnectionId))
                {
                    errors.Add("duplicate-connection-id:" + binding.ConnectionId);
                }

                string target = ResolveConnectionTarget(binding);
                MarkerRuntime from;
                MarkerRuntime to;
                if (!runtimeByMarker.TryGetValue(binding.FromMarkerId, out from))
                {
                    errors.Add("missing-connection-endpoint:"
                        + binding.ConnectionId + ":" + binding.FromMarkerId);
                    continue;
                }

                if (!runtimeByMarker.TryGetValue(target, out to))
                {
                    errors.Add("missing-connection-endpoint:"
                        + binding.ConnectionId + ":" + target);
                    continue;
                }

                try
                {
                    CreateConnection(binding, from, to);
                }
                catch (Exception exception)
                {
                    errors.Add("invalid-connection:" + binding.ConnectionId
                        + ":" + exception.GetType().Name);
                }
            }

            for (int index = 0; index < ExpectedConnections.Length; index++)
            {
                if (!ids.Contains(ExpectedConnections[index]))
                {
                    errors.Add("missing-connection-id:" + ExpectedConnections[index]);
                }
            }

            for (int index = 0; index < ExpectedTraversal.Length; index++)
            {
                string expectedFrom = ExpectedTraversal[index];
                string expectedTo = ExpectedTraversal[(index + 1) % ExpectedTraversal.Length];
                bool found = connections.Any(
                    connection => connection != null
                        && string.Equals(
                            connection.FromMarkerId,
                            expectedFrom,
                            StringComparison.Ordinal)
                        && string.Equals(
                            ResolveConnectionTarget(connection),
                            expectedTo,
                            StringComparison.Ordinal));
                if (!found)
                {
                    errors.Add("missing-traversal-link:"
                        + expectedFrom + "->" + expectedTo);
                }
            }
        }

        private void ValidateLifecycles(ICollection<string> errors)
        {
            if (runtimeByMarker.Count != ExpectedTraversal.Length)
            {
                errors.Add("runtime-marker-count:" + runtimeByMarker.Count);
                return;
            }

            foreach (MarkerRuntime marker in runtimeByMarker.Values)
            {
                if (!marker.Lifecycle.Identity.Equals(marker.Identity))
                {
                    errors.Add("lifecycle-identity-mismatch:" + marker.Binding.MarkerId);
                }

                if (marker.Lifecycle.ActiveKey != null
                    && !marker.Lifecycle.ActiveKey.RoomId.Equals(marker.Identity.RoomId))
                {
                    errors.Add("lifecycle-key-room-mismatch:" + marker.Binding.MarkerId);
                }
            }
        }

        private void ValidateSceneBoundary(ICollection<string> errors)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (!IsOwnedVisual(candidate) && !Approximately(candidate.position.z, 0f))
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

                if (IsOwnedVisual(component.transform))
                {
                    continue;
                }

                if (component is Camera
                    || component is Collider
                    || component is Rigidbody
                    || component is Joint
                    || component is CharacterController
                    || component is Light)
                {
                    errors.Add("forbidden-scene-component:"
                        + component.GetType().FullName + "@" + BuildPath(component.transform));
                }
            }

#if UNITY_EDITOR
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] sceneTransforms =
                    roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < sceneTransforms.Length; index++)
                {
                    if (PrefabUtility.IsPartOfAnyPrefab(sceneTransforms[index].gameObject))
                    {
                        errors.Add("unowned-prefab-instance:" + BuildPath(sceneTransforms[index]));
                    }
                }
            }
#endif
        }

        private string CaptureSnapshotInternal()
        {
            var builder = new StringBuilder(4096);
            builder.Append("schema=").Append(SnapshotSchema).Append('\n');
            builder.Append("version=").Append(SnapshotVersion).Append('\n');
            builder.Append("scene=").Append(SceneName).Append('\n');
            builder.Append("configurationFingerprint=")
                .Append(ResolvedConfigurationFingerprint).Append('\n');
            builder.Append("runSeed=").Append(ResolvedRunSeed).Append('\n');
            builder.Append("projectionReader=")
                .Append(ProjectionReaderContractName).Append('\n');
            builder.Append("projectionReads=").Append(projectionReader.ReadCount).Append('\n');
            builder.Append("missionCommandSubmissions=0\n");
            builder.Append("cursor=").Append(ExpectedTraversal[cursorIndex]).Append('\n');
            builder.Append("loadOrder=").Append(string.Join(",", ExpectedTraversal)).Append('\n');
            builder.Append("unloadOrder=")
                .Append(string.Join(",", ExpectedTraversal.Reverse().ToArray())).Append('\n');

            MarkerRuntime[] orderedMarkers = runtimeByMarker.Values
                .OrderBy(marker => marker.Binding.LoadOrder)
                .ThenBy(marker => marker.Binding.MarkerId, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < orderedMarkers.Length; index++)
            {
                MarkerRuntime marker = orderedMarkers[index];
                builder.Append("marker|")
                    .Append(marker.Binding.MarkerId)
                    .Append('|').Append(marker.Binding.Kind)
                    .Append("|room=").Append(marker.Identity.RoomId)
                    .Append("|projection=").Append(marker.Identity.ProjectionId)
                    .Append("|loadOrder=").Append(marker.Binding.LoadOrder)
                    .Append("|position=").Append(Format(marker.Binding.Anchor.localPosition))
                    .Append("|phase=").Append(marker.Lifecycle.Phase)
                    .Append('\n');
            }

            Stage1ShortRouteConnectionBinding[] orderedConnections = connections
                .Where(connection => connection != null)
                .OrderBy(connection => connection.ConnectionId, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < orderedConnections.Length; index++)
            {
                Stage1ShortRouteConnectionBinding binding = orderedConnections[index];
                string target = ResolveConnectionTarget(binding);
                MarkerRuntime from;
                MarkerRuntime to;
                builder.Append("connection|").Append(binding.ConnectionId)
                    .Append("|from=").Append(binding.FromMarkerId)
                    .Append("|to=").Append(target);
                if (runtimeByMarker.TryGetValue(binding.FromMarkerId, out from)
                    && runtimeByMarker.TryGetValue(target, out to))
                {
                    RoomConnection connection = CreateConnection(binding, from, to);
                    builder.Append("|hash=").Append(connection.GetHashCode());
                }
                else
                {
                    builder.Append("|missing-endpoint");
                }

                builder.Append('\n');
            }

            builder.Append(CaptureLastLifecycleBatchInternal());
            return builder.ToString();
        }

        private string CaptureLastLifecycleBatchInternal()
        {
            var builder = new StringBuilder();
            for (int index = 0; index < lastLifecycleEvents.Count; index++)
            {
                builder.Append("lifecycle|")
                    .Append(lastLifecycleEvents[index])
                    .Append('\n');
            }

            return builder.ToString();
        }

        private string ResolveConnectionTarget(
            Stage1ShortRouteConnectionBinding binding)
        {
            string target;
            return connectionTargetOverrides.TryGetValue(binding.ConnectionId, out target)
                ? target
                : binding.ToMarkerId;
        }

        private static RoomConnection CreateConnection(
            Stage1ShortRouteConnectionBinding binding,
            MarkerRuntime from,
            MarkerRuntime to)
        {
            var fromSocket = new RoomSocket(
                from.Identity,
                StableId.Parse(binding.FromSocketId),
                RoomSocketDirection.Outbound);
            var toSocket = new RoomSocket(
                to.Identity,
                StableId.Parse(binding.ToSocketId),
                RoomSocketDirection.Inbound);
            return new RoomConnection(fromSocket, toSocket);
        }

        private MarkerRuntime RequireMarker(string markerId)
        {
            MarkerRuntime marker;
            if (!runtimeByMarker.TryGetValue(markerId, out marker))
            {
                throw new InvalidOperationException(
                    "Unknown Stage1ShortRoute marker ID '" + markerId + "'.");
            }

            return marker;
        }

        private string MoveCursorNextInternal()
        {
            cursorIndex = (cursorIndex + 1) % ExpectedTraversal.Length;
            UpdateCursorVisual();
            return ExpectedTraversal[cursorIndex];
        }

        private string MoveCursorPreviousInternal()
        {
            cursorIndex = (cursorIndex + ExpectedTraversal.Length - 1)
                % ExpectedTraversal.Length;
            UpdateCursorVisual();
            return ExpectedTraversal[cursorIndex];
        }

        private string RestartCursorInternal()
        {
            cursorIndex = 0;
            UpdateCursorVisual();
            return ExpectedTraversal[cursorIndex];
        }

        private void EnsurePlaceholderVisuals()
        {
            if (visualRoot != null)
            {
                UpdateCursorVisual();
                return;
            }

            visualTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            visualTexture.name = "EH-005 Route Pixel";
            visualTexture.hideFlags = HideFlags.DontSave;
            visualTexture.SetPixel(0, 0, Color.white);
            visualTexture.Apply(false, true);
            visualSprite = Sprite.Create(
                visualTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            visualSprite.name = "EH-005 Route Sprite";
            visualSprite.hideFlags = HideFlags.DontSave;

            visualRoot = new GameObject(VisualRootName);
            visualRoot.hideFlags = HideFlags.DontSave;
            visualRoot.transform.SetParent(routeRoot == null ? transform : routeRoot, false);

            RebuildConnectionVisuals();
            CreateMarkerVisuals();
            CreateHeaderVisuals();
            CreateCursorVisual();
            UpdateCursorVisual();
        }

        private void RebuildConnectionVisuals()
        {
            if (visualRoot == null || visualSprite == null)
            {
                return;
            }

            Transform existing = visualRoot.transform.Find("Connections");
            if (existing != null)
            {
                DestroyOwned(existing.gameObject);
            }

            var connectionRoot = new GameObject("Connections");
            connectionRoot.hideFlags = HideFlags.DontSave;
            connectionRoot.transform.SetParent(visualRoot.transform, false);

            if (connections == null)
            {
                return;
            }

            for (int index = 0; index < connections.Length; index++)
            {
                Stage1ShortRouteConnectionBinding binding = connections[index];
                if (binding == null)
                {
                    continue;
                }

                MarkerRuntime from;
                MarkerRuntime to;
                if (!runtimeByMarker.TryGetValue(binding.FromMarkerId, out from)
                    || !runtimeByMarker.TryGetValue(ResolveConnectionTarget(binding), out to)
                    || from.Binding.Anchor == null
                    || to.Binding.Anchor == null)
                {
                    continue;
                }

                Vector3 start = from.Binding.Anchor.localPosition;
                Vector3 end = to.Binding.Anchor.localPosition;
                Vector3 delta = end - start;
                var line = new GameObject(binding.ConnectionId);
                line.hideFlags = HideFlags.DontSave;
                line.transform.SetParent(connectionRoot.transform, false);
                line.transform.localPosition = (start + end) * 0.5f;
                line.transform.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                line.transform.localScale = new Vector3(delta.magnitude, 0.055f, 1f);
                SpriteRenderer renderer = line.AddComponent<SpriteRenderer>();
                renderer.sprite = visualSprite;
                renderer.color = new Color(0.25f, 0.65f, 0.85f, 0.55f);
                renderer.sortingOrder = -10;
            }
        }

        private void CreateMarkerVisuals()
        {
            if (markers == null)
            {
                return;
            }

            for (int index = 0; index < markers.Length; index++)
            {
                Stage1ShortRouteMarkerBinding binding = markers[index];
                if (binding == null || binding.Anchor == null)
                {
                    continue;
                }

                var marker = new GameObject("Marker " + binding.MarkerId);
                marker.hideFlags = HideFlags.DontSave;
                marker.transform.SetParent(visualRoot.transform, false);
                marker.transform.localPosition = binding.Anchor.localPosition;
                marker.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
                SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
                renderer.sprite = visualSprite;
                renderer.color = MarkerColor(binding.Kind);
                renderer.sortingOrder = 10;

                TextMesh label = CreateText(
                    marker.transform,
                    binding.Kind + "\n" + binding.MarkerId,
                    new Vector3(0f, 0.85f, 0f),
                    0.12f,
                    20);
                label.anchor = TextAnchor.LowerCenter;
                label.alignment = TextAlignment.Center;
            }
        }

        private void CreateHeaderVisuals()
        {
            TextMesh header = CreateText(
                visualRoot.transform,
                "EH-005 SHORT ROUTE   A/D or arrows: move   R/Home: start",
                new Vector3(0f, 4.25f, 0f),
                0.12f,
                30);
            header.anchor = TextAnchor.MiddleCenter;
            header.alignment = TextAlignment.Center;

            statusLabel = CreateText(
                visualRoot.transform,
                string.Empty,
                new Vector3(0f, 3.75f, 0f),
                0.12f,
                30);
            statusLabel.anchor = TextAnchor.MiddleCenter;
            statusLabel.alignment = TextAlignment.Center;
            UpdateStatusLabel();
        }

        private void CreateCursorVisual()
        {
            cursorVisual = new GameObject("Route Test Cursor");
            cursorVisual.hideFlags = HideFlags.DontSave;
            cursorVisual.transform.SetParent(visualRoot.transform, false);
            cursorVisual.transform.localScale = new Vector3(0.78f, 0.78f, 1f);
            SpriteRenderer renderer = cursorVisual.AddComponent<SpriteRenderer>();
            renderer.sprite = visualSprite;
            renderer.color = new Color(1f, 0.85f, 0.2f, 0.55f);
            renderer.sortingOrder = 5;
        }

        private TextMesh CreateText(
            Transform parent,
            string text,
            Vector3 localPosition,
            float characterSize,
            int sortingOrder)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            if (font == null)
            {
                throw new InvalidOperationException(
                    "Unity's built-in runtime font is unavailable for EH-005.");
            }

            var labelObject = new GameObject("Label");
            labelObject.hideFlags = HideFlags.DontSave;
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.font = font;
            label.fontSize = 64;
            label.characterSize = characterSize;
            label.richText = false;
            label.color = Color.white;
            MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = sortingOrder;
            return label;
        }

        private void UpdateCursorVisual()
        {
            if (cursorVisual == null || !modelInitialized)
            {
                UpdateStatusLabel();
                return;
            }

            MarkerRuntime marker;
            if (runtimeByMarker.TryGetValue(ExpectedTraversal[cursorIndex], out marker)
                && marker.Binding.Anchor != null)
            {
                cursorVisual.transform.localPosition = marker.Binding.Anchor.localPosition;
            }

            UpdateStatusLabel();
        }

        private void UpdateStatusLabel()
        {
            if (statusLabel == null || !modelInitialized)
            {
                return;
            }

            MarkerRuntime marker;
            string markerId = ExpectedTraversal[cursorIndex];
            string phase = runtimeByMarker.TryGetValue(markerId, out marker)
                ? marker.Lifecycle.Phase.ToString()
                : "Unknown";
            statusLabel.text = "Cursor: " + markerId + "   projection phase: " + phase;
        }

        private static Color MarkerColor(Stage1ShortRouteMarkerKind kind)
        {
            switch (kind)
            {
                case Stage1ShortRouteMarkerKind.Start:
                    return new Color(0.25f, 0.9f, 0.45f, 0.9f);
                case Stage1ShortRouteMarkerKind.ArenaEntry:
                    return new Color(0.25f, 0.65f, 1f, 0.9f);
                case Stage1ShortRouteMarkerKind.Connector:
                    return new Color(0.75f, 0.45f, 1f, 0.9f);
                case Stage1ShortRouteMarkerKind.ReviewEnd:
                    return new Color(1f, 0.65f, 0.25f, 0.9f);
                case Stage1ShortRouteMarkerKind.Restart:
                    return new Color(1f, 0.35f, 0.35f, 0.9f);
                default:
                    return Color.white;
            }
        }

        private void DestroyPlaceholderVisuals()
        {
            statusLabel = null;
            cursorVisual = null;
            if (visualRoot != null)
            {
                GameObject ownedRoot = visualRoot;
                visualRoot = null;
                DestroyOwned(ownedRoot);
            }

            if (visualSprite != null)
            {
                Sprite ownedSprite = visualSprite;
                visualSprite = null;
                DestroyOwned(ownedSprite);
            }

            if (visualTexture != null)
            {
                Texture2D ownedTexture = visualTexture;
                visualTexture = null;
                DestroyOwned(ownedTexture);
            }
        }

        private static void DestroyOwned(UnityEngine.Object owned)
        {
            if (owned == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(owned);
            }
            else
            {
                DestroyImmediate(owned);
            }
        }

        private void ReleaseRouteInstance()
        {
            if (UnityEngine.Application.isPlaying && modelInitialized && activeInstance == this)
            {
                LeaveRouteInternal();
            }

            ReleaseRuntimeInstance();
            if (activeInstance == this)
            {
                activeInstance = null;
                activeConfiguration = null;
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

        private static AsyncOperation Track(AsyncOperation operation)
        {
            if (operation == null)
            {
                throw new InvalidOperationException(
                    "Unity did not create the requested Stage1ShortRouteShell operation.");
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
                    "Stage1ShortRouteShell scene operations require Play Mode.");
            }
        }

        private static void RequireNoOperation()
        {
            if (activeOperation != null)
            {
                throw new InvalidOperationException(
                    "A Stage1ShortRouteShell load or unload is already in flight.");
            }
        }

        private static void RequireActiveInstance()
        {
            if (activeInstance == null || !activeInstance.modelInitialized)
            {
                throw new InvalidOperationException(
                    "Stage1ShortRouteShell has no active initialized fixture.");
            }
        }

        private void RequireModel()
        {
            if (!modelInitialized || projectionReader == null)
            {
                throw new InvalidOperationException(
                    "Stage1ShortRouteShell projection model is not initialized.");
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
                    "Bootstrap must be loaded before Stage1ShortRouteShell is exercised.");
            }

            return scene;
        }

        private static Scene GetRouteScene()
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

        private bool IsOwnedVisual(Transform candidate)
        {
            return candidate != null
                && visualRoot != null
                && (candidate == visualRoot.transform
                    || candidate.IsChildOf(visualRoot.transform));
        }

        private static string BuildPath(Transform transform)
        {
            var names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.gameObject.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private static string Format(Vector3 value)
        {
            return FormatFloat(value.x) + "," + FormatFloat(value.y) + "," + FormatFloat(value.z);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static bool Approximately(float left, float right)
        {
            return Mathf.Abs(left - right) <= 0.0001f;
        }

        private sealed class MarkerRuntime
        {
            public MarkerRuntime(
                Stage1ShortRouteMarkerBinding binding,
                RoomProjectionIdentity identity,
                RoomProjectionKey key,
                RoomProjectionLifecycle lifecycle,
                Stage1ShortRouteProjection projection)
            {
                Binding = binding;
                Identity = identity;
                Key = key;
                Lifecycle = lifecycle;
                Projection = projection;
            }

            public Stage1ShortRouteMarkerBinding Binding { get; }

            public RoomProjectionIdentity Identity { get; }

            public RoomProjectionKey Key { get; }

            public RoomProjectionLifecycle Lifecycle { get; set; }

            public Stage1ShortRouteProjection Projection { get; }
        }

        private sealed class ProjectionReader : IRoomProjectionStateReader
        {
            private readonly IDictionary<RoomProjectionKey, Stage1ShortRouteProjection> projections;

            public ProjectionReader(
                IDictionary<RoomProjectionKey, Stage1ShortRouteProjection> projections)
            {
                this.projections = projections
                    ?? throw new ArgumentNullException(nameof(projections));
            }

            public int ReadCount { get; private set; }

            public RoomProjectionReadResult<TProjection> Read<TProjection>(
                RoomProjectionKey key)
            {
                if (key == null)
                {
                    throw new ArgumentNullException(nameof(key));
                }

                ReadCount++;
                Stage1ShortRouteProjection projection;
                if (typeof(TProjection) == typeof(Stage1ShortRouteProjection)
                    && projections.TryGetValue(key, out projection))
                {
                    return RoomProjectionReadResult<TProjection>.Found(
                        key,
                        (TProjection)(object)projection);
                }

                return RoomProjectionReadResult<TProjection>.Unknown(key);
            }
        }
    }
}
