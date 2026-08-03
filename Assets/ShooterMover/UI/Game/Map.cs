using System;
using System.Collections.Generic;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Game
{
    [DefaultExecutionOrder(700)]
    [DisallowMultipleComponent]
    public sealed class Map : MonoBehaviour
    {
        private readonly Dictionary<StableId, int> boxes =
            new Dictionary<StableId, int>();
        private readonly List<GameObject> teleporterObjects =
            new List<GameObject>();

        private LevelGame game;
        private LevelRooms rooms;
        private RoomFile roomContent;
        private MapLayout layout;
        private MapView view;
        private bool isBound;
        private bool bindingFailed;
        private bool paused;
        private float previousTimeScale;
        private long teleportSequence;
        private int screenWidth;
        private int screenHeight;

        public bool IsOpen
        {
            get { return isBound && view != null && view.IsVisible; }
        }

        public void SetBoxes(StableId roomStableId, int count)
        {
            if (roomStableId == null)
                throw new ArgumentNullException(nameof(roomStableId));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (count == 0)
                boxes.Remove(roomStableId);
            else
                boxes[roomStableId] = count;

            if (isBound && view != null)
                view.SetBoxes(roomStableId, count);
        }

        public void ClearBoxes()
        {
            boxes.Clear();
            if (isBound && view != null)
                view.ClearBoxes();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntimeHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryInstall(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryInstall(scene);
        }

        private static void TryInstall(Scene scene)
        {
            if (!scene.IsValid()
                || !string.Equals(
                    scene.path,
                    PlayableLevelCatalog.PlayableLevelScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            LevelGame levelGame = FindGame(scene);
            if (levelGame == null)
            {
                Debug.LogError("map-level-game-missing");
                return;
            }
            if (levelGame.GetComponent<Map>() == null)
            {
                levelGame.gameObject.AddComponent<Map>();
            }
        }

        private static LevelGame FindGame(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                LevelGame value = roots[index]
                    .GetComponentInChildren<LevelGame>(true);
                if (value != null)
                    return value;
            }
            return null;
        }

        private void Awake()
        {
            game = GetComponent<LevelGame>();
        }

        private void Update()
        {
            if (!isBound)
            {
                if (!bindingFailed)
                    TryBind();
                return;
            }

            if (screenWidth != Screen.width || screenHeight != Screen.height)
            {
                Rebuild();
            }

            RefreshTeleporters();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
            {
                Toggle();
            }
        }

        private void TryBind()
        {
            if (game == null || !game.IsConfigured || game.LevelStableId == null)
                return;

            PlayableLevelDefinition selectedLevel;
            if (!PlayableLevelCatalog.TryResolve(
                    game.LevelStableId,
                    out selectedLevel)
                || selectedLevel == null)
            {
                RejectBinding("map-level-definition-missing");
                return;
            }

            roomContent = Resources.Load<RoomFile>(
                selectedLevel.RoomContentResourcePath);
            if (roomContent == null)
            {
                RejectBinding("map-room-content-missing");
                return;
            }

            rooms = GetComponentInChildren<LevelRooms>(true);
            if (rooms == null)
            {
                RejectBinding("map-room-runtime-missing");
                return;
            }
            if (!rooms.IsBuilt
                || rooms.Definition == null
                || rooms.CurrentRoomStableId == null)
            {
                return;
            }

            view = GetComponent<MapView>();
            if (view == null)
                view = gameObject.AddComponent<MapView>();

            Rebuild();
            view.TeleporterClicked += HandleTeleporterClicked;
            rooms.CurrentRoomPresentationRebuilt += HandleRoomChanged;
            isBound = true;
        }

        private void Rebuild()
        {
            if (roomContent == null
                || rooms == null
                || rooms.Definition == null
                || rooms.CurrentRoomStableId == null
                || view == null)
            {
                return;
            }

            bool reopen = view.IsVisible;
            layout = MapLayout.Build(
                roomContent,
                new Vector2(
                    Mathf.Max(1, Screen.width),
                    Mathf.Max(1, Screen.height)));
            view.Build(layout);
            AddConnections(rooms.Definition);
            ApplyBoxes();
            RefreshTeleporters();
            BuildRoomTeleporters();
            if (reopen)
                view.Show(rooms.CurrentRoomStableId);
            else
                view.SetCurrentRoom(rooms.CurrentRoomStableId);

            screenWidth = Screen.width;
            screenHeight = Screen.height;
        }

        private void AddConnections(AuthorableRoomGraphDefinition definition)
        {
            for (int roomIndex = 0;
                roomIndex < definition.Rooms.Count;
                roomIndex++)
            {
                AuthorableRoomDefinition room = definition.Rooms[roomIndex];
                for (int exitIndex = 0;
                    exitIndex < room.Exits.Count;
                    exitIndex++)
                {
                    RoomExitLinkDefinition exit = room.Exits[exitIndex];
                    if (exit.LinkKind != RoomLiveLinkKind.Room)
                        continue;
                    view.AddConnection(
                        room.RoomStableId,
                        exit.TargetRoomStableId);
                }
            }
        }

        private void ApplyBoxes()
        {
            foreach (KeyValuePair<StableId, int> box in boxes)
            {
                view.SetBoxes(box.Key, box.Value);
            }
        }

        private void RefreshTeleporters()
        {
            if (layout == null || view == null || rooms == null)
                return;

            bool sourceReady = HasOpenTeleporter(
                rooms.CurrentRoomStableId);
            for (int roomIndex = 0;
                roomIndex < layout.Rooms.Count;
                roomIndex++)
            {
                MapLayout.Room room = layout.Rooms[roomIndex];
                bool roomOpen = sourceReady && IsRoomComplete(room.RoomStableId);
                for (int teleporterIndex = 0;
                    teleporterIndex < room.Teleporters.Count;
                    teleporterIndex++)
                {
                    MapLayout.Teleporter teleporter =
                        room.Teleporters[teleporterIndex];
                    view.SetTeleporterOpen(
                        teleporter.TeleporterStableId,
                        teleporter.Enabled && roomOpen);
                }
            }

            bool currentOpen = IsRoomComplete(rooms.CurrentRoomStableId);
            for (int index = 0; index < teleporterObjects.Count; index++)
            {
                if (teleporterObjects[index] == null) continue;
                Teleporter teleporter =
                    teleporterObjects[index].GetComponent<Teleporter>();
                if (teleporter != null) teleporter.SetOpen(currentOpen);
            }
        }

        private bool HasOpenTeleporter(StableId roomStableId)
        {
            if (!IsRoomComplete(roomStableId)) return false;
            MapLayout.Room room;
            if (layout == null
                || !layout.TryGetRoom(roomStableId, out room))
            {
                return false;
            }
            for (int index = 0; index < room.Teleporters.Count; index++)
            {
                if (room.Teleporters[index].Enabled) return true;
            }
            return false;
        }

        private bool IsRoomComplete(StableId roomStableId)
        {
            if (roomStableId == null
                || rooms == null
                || rooms.Query == null)
            {
                return false;
            }
            try
            {
                return rooms.Query
                    .GetRoomProjection(roomStableId)
                    .IsCompleted;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        private void BuildRoomTeleporters()
        {
            ClearRoomTeleporters();
            if (layout == null || rooms == null)
                return;

            MapLayout.Room room;
            if (!layout.TryGetRoom(rooms.CurrentRoomStableId, out room))
                return;
            bool open = IsRoomComplete(room.RoomStableId);
            for (int index = 0; index < room.Teleporters.Count; index++)
            {
                MapLayout.Teleporter source = room.Teleporters[index];
                if (!source.Enabled) continue;
                var objectInstance = new GameObject(
                    "Teleporter " + source.TeleporterStableId);
                objectInstance.transform.SetParent(transform, false);
                Teleporter teleporter = objectInstance.AddComponent<Teleporter>();
                teleporter.Bind(
                    source.LocalPosition,
                    source.LocalRotationDegrees,
                    open);
                teleporterObjects.Add(objectInstance);
            }
        }

        private void ClearRoomTeleporters()
        {
            for (int index = teleporterObjects.Count - 1; index >= 0; index--)
            {
                if (teleporterObjects[index] != null)
                    Destroy(teleporterObjects[index]);
            }
            teleporterObjects.Clear();
        }

        private void Toggle()
        {
            if (view.IsVisible)
                Close();
            else
                Open();
        }

        private void Open()
        {
            RefreshTeleporters();
            view.Show(rooms.CurrentRoomStableId);
            if (paused) return;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            paused = true;
        }

        private void Close()
        {
            if (view != null)
                view.Hide();
            if (!paused) return;
            Time.timeScale = previousTimeScale;
            paused = false;
        }

        private void HandleTeleporterClicked(MapLayout.Teleporter target)
        {
            if (target == null
                || !target.Enabled
                || !HasOpenTeleporter(rooms.CurrentRoomStableId)
                || !IsRoomComplete(target.RoomStableId))
            {
                return;
            }

            List<StableId> route;
            if (!TryBuildRoute(
                rooms.CurrentRoomStableId,
                target.RoomStableId,
                out route))
            {
                Debug.LogError("map-teleporter-route-missing", this);
                return;
            }

            for (int index = 0; index < route.Count; index++)
            {
                RoomLiveOperationResult result = rooms.Traverse(
                    NextTeleportOperation(),
                    route[index]);
                if (result == null
                    || result.Status == RoomLiveOperationStatus.Rejected)
                {
                    Debug.LogError(
                        "map-teleporter-travel-rejected:"
                        + (result == null
                            ? "result-missing"
                            : result.RejectionCode),
                        this);
                    return;
                }
            }

            if (rooms.CurrentRoomStableId != target.RoomStableId)
            {
                Debug.LogError("map-teleporter-room-mismatch", this);
                return;
            }

            PlayerMarker player = GetComponentInChildren<PlayerMarker>(true);
            Rigidbody2D body = player == null
                ? null
                : player.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                Debug.LogError("map-teleporter-player-missing", this);
                return;
            }
            body.position = target.LocalPosition;
            body.rotation = target.LocalRotationDegrees;
            body.linearVelocity = Vector2.zero;
            Close();
        }

        private bool TryBuildRoute(
            StableId startRoomStableId,
            StableId targetRoomStableId,
            out List<StableId> route)
        {
            route = new List<StableId>();
            if (startRoomStableId == targetRoomStableId)
                return true;

            var pending = new Queue<StableId>();
            var visited = new HashSet<StableId>();
            var previous = new Dictionary<StableId, TravelStep>();
            pending.Enqueue(startRoomStableId);
            visited.Add(startRoomStableId);

            while (pending.Count > 0)
            {
                StableId current = pending.Dequeue();
                AuthorableRoomDefinition room =
                    rooms.Definition.GetRoom(current);
                for (int index = 0; index < room.Exits.Count; index++)
                {
                    RoomExitLinkDefinition exit = room.Exits[index];
                    if (exit.LinkKind != RoomLiveLinkKind.Room
                        || exit.TargetRoomStableId == null
                        || visited.Contains(exit.TargetRoomStableId)
                        || !IsRoomComplete(exit.TargetRoomStableId))
                    {
                        continue;
                    }

                    visited.Add(exit.TargetRoomStableId);
                    previous.Add(
                        exit.TargetRoomStableId,
                        new TravelStep(current, exit.ExitStableId));
                    if (exit.TargetRoomStableId == targetRoomStableId)
                    {
                        BuildRoute(
                            startRoomStableId,
                            targetRoomStableId,
                            previous,
                            route);
                        return true;
                    }
                    pending.Enqueue(exit.TargetRoomStableId);
                }
            }
            return false;
        }

        private static void BuildRoute(
            StableId startRoomStableId,
            StableId targetRoomStableId,
            IReadOnlyDictionary<StableId, TravelStep> previous,
            List<StableId> route)
        {
            StableId current = targetRoomStableId;
            while (current != startRoomStableId)
            {
                TravelStep step = previous[current];
                route.Add(step.ExitStableId);
                current = step.FromRoomStableId;
            }
            route.Reverse();
        }

        private StableId NextTeleportOperation()
        {
            teleportSequence = checked(teleportSequence + 1L);
            return StableId.Create(
                "operation",
                "map-teleport-" + teleportSequence);
        }

        private void HandleRoomChanged()
        {
            if (!isBound
                || view == null
                || rooms == null
                || rooms.CurrentRoomStableId == null)
            {
                return;
            }
            view.SetCurrentRoom(rooms.CurrentRoomStableId);
            BuildRoomTeleporters();
            RefreshTeleporters();
        }

        private void RejectBinding(string code)
        {
            bindingFailed = true;
            Debug.LogError(code, this);
        }

        private void OnDestroy()
        {
            Close();
            ClearRoomTeleporters();
            if (view != null)
                view.TeleporterClicked -= HandleTeleporterClicked;
            if (rooms != null)
            {
                rooms.CurrentRoomPresentationRebuilt -= HandleRoomChanged;
            }
        }

        private sealed class TravelStep
        {
            public TravelStep(
                StableId fromRoomStableId,
                StableId exitStableId)
            {
                FromRoomStableId = fromRoomStableId;
                ExitStableId = exitStableId;
            }

            public StableId FromRoomStableId { get; }
            public StableId ExitStableId { get; }
        }
    }
}
