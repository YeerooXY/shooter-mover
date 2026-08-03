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

        private LevelGame game;
        private LevelRooms rooms;
        private RoomFile roomContent;
        private MapView view;
        private bool isBound;
        private bool bindingFailed;
        private bool paused;
        private float previousTimeScale;
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
            MapLayout layout = MapLayout.Build(
                roomContent,
                new Vector2(
                    Mathf.Max(1, Screen.width),
                    Mathf.Max(1, Screen.height)));
            view.Build(layout);
            AddConnections(rooms.Definition);
            ApplyBoxes();
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

        private void Toggle()
        {
            if (view.IsVisible)
                Close();
            else
                Open();
        }

        private void Open()
        {
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
        }

        private void RejectBinding(string code)
        {
            bindingFailed = true;
            Debug.LogError(code, this);
        }

        private void OnDestroy()
        {
            Close();
            if (rooms != null)
            {
                rooms.CurrentRoomPresentationRebuilt -= HandleRoomChanged;
            }
        }
    }
}
