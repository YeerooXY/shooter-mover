using System;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Moves the player clear of the destination-door trigger after a room transition.
    /// The authored initial player spawn remains unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class DoorSpawnClearance : MonoBehaviour
    {
        private const string PlayableLevelScenePath =
            "Assets/ShooterMover/Scenes/Gameplay/PlayableLevel.unity";
        private const float EntryClearance = 2f;

        private LevelRooms rooms;
        private long pendingRevision;
        private long handledRevision;

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
            TryAttach(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryAttach(scene);
        }

        private static void TryAttach(Scene scene)
        {
            if (!scene.IsValid()
                || !string.Equals(
                    scene.path,
                    PlayableLevelScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            LevelRooms candidate = FindFirstObjectByType<LevelRooms>(
                FindObjectsInactive.Include);
            if (candidate == null || candidate.gameObject.scene != scene) return;

            DoorSpawnClearance clearance = candidate
                .GetComponent<DoorSpawnClearance>()
                ?? candidate.gameObject.AddComponent<DoorSpawnClearance>();
            clearance.Bind(candidate);
        }

        private void Bind(LevelRooms configuredRooms)
        {
            if (rooms != null) return;
            rooms = configuredRooms;
            handledRevision = rooms.PresentationRevision;
            pendingRevision = handledRevision;
            rooms.CurrentRoomPresentationRebuilt += HandleRoomRebuilt;
        }

        private void HandleRoomRebuilt()
        {
            long revision = rooms.PresentationRevision;
            if (FindPlayer() == null)
            {
                // The initial room is built before LevelGame creates the player.
                handledRevision = revision;
                pendingRevision = revision;
                return;
            }
            pendingRevision = revision;
        }

        private void LateUpdate()
        {
            if (rooms == null || pendingRevision <= handledRevision) return;

            PlayerMarker player = FindPlayer();
            Rigidbody2D body = player == null
                ? null
                : player.GetComponent<Rigidbody2D>();
            if (body == null) return;

            AuthorableRoomDefinition room = rooms.Definition.GetRoom(
                rooms.CurrentRoomStableId);
            RoomSpawnPointDefinition spawn;
            if (!room.TryGetSpawnPoint(
                    rooms.CurrentSpawnPointStableId,
                    out spawn)
                || spawn == null)
            {
                return;
            }

            Vector2 position = new Vector2(
                (float)spawn.LocalPosition.X,
                (float)spawn.LocalPosition.Y);
            Vector2 delta = new Vector2(
                (float)room.Bounds.Center.X - position.x,
                (float)room.Bounds.Center.Y - position.y);
            Vector2 inward = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2(Mathf.Sign(delta.x), 0f)
                : new Vector2(0f, Mathf.Sign(delta.y));

            body.position = position + inward * EntryClearance;
            body.linearVelocity = Vector2.zero;
            handledRevision = pendingRevision;
        }

        private PlayerMarker FindPlayer()
        {
            PlayerMarker player = FindFirstObjectByType<PlayerMarker>(
                FindObjectsInactive.Include);
            return player != null && player.gameObject.scene == gameObject.scene
                ? player
                : null;
        }

        private void OnDestroy()
        {
            if (rooms != null)
            {
                rooms.CurrentRoomPresentationRebuilt -= HandleRoomRebuilt;
            }
        }
    }
}
