using System;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Moves the player a short distance into a newly entered room after the normal room
    /// synchronization has placed them at the authored door spawn. The initial level spawn
    /// is intentionally left unchanged.
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

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                LevelRooms candidate = roots[index]
                    .GetComponentInChildren<LevelRooms>(true);
                if (candidate == null) continue;

                DoorSpawnClearance clearance = candidate
                    .GetComponent<DoorSpawnClearance>();
                if (clearance == null)
                {
                    clearance = candidate.gameObject
                        .AddComponent<DoorSpawnClearance>();
                }
                clearance.Bind(candidate);
                return;
            }
        }

        private void Bind(LevelRooms configuredRooms)
        {
            if (rooms != null) return;
            rooms = configuredRooms
                ?? throw new ArgumentNullException(nameof(configuredRooms));
            handledRevision = rooms.PresentationRevision;
            pendingRevision = handledRevision;
            rooms.CurrentRoomPresentationRebuilt += HandleRoomRebuilt;
        }

        private void HandleRoomRebuilt()
        {
            if (rooms == null) return;
            long revision = rooms.PresentationRevision;
            if (FindPlayer() == null)
            {
                // The first room is built before LevelGame creates the player.
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
            if (player == null) return;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
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

            Vector2 spawnPosition = new Vector2(
                (float)spawn.LocalPosition.X,
                (float)spawn.LocalPosition.Y);
            Vector2 roomCenter = new Vector2(
                (float)room.Bounds.Center.X,
                (float)room.Bounds.Center.Y);
            Vector2 inward = roomCenter - spawnPosition;
            if (inward.sqrMagnitude <= Mathf.Epsilon)
            {
                float radians = (float)spawn.LocalRotationDegrees
                    * Mathf.Deg2Rad;
                inward = new Vector2(
                    Mathf.Cos(radians),
                    Mathf.Sin(radians));
            }

            body.position = spawnPosition + inward.normalized * EntryClearance;
            body.linearVelocity = Vector2.zero;
            handledRevision = pendingRevision;
        }

        private PlayerMarker FindPlayer()
        {
            PlayerMarker[] players = FindObjectsByType<PlayerMarker>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < players.Length; index++)
            {
                PlayerMarker candidate = players[index];
                if (candidate != null
                    && candidate.gameObject.scene == gameObject.scene)
                {
                    return candidate;
                }
            }
            return null;
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
