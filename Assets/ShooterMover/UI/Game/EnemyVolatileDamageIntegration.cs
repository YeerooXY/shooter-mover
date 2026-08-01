using System;
using System.Collections.Generic;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Game
{
    internal static class EnemyVolatileDamageInstaller
    {
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

            LevelGame controller = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].GetComponentInChildren<
                        EnemyVolatileDamageIntegration>(true) != null)
                {
                    return;
                }

                LevelGame candidate = roots[index]
                    .GetComponentInChildren<LevelGame>(true);
                if (candidate == null) continue;
                if (controller != null && !ReferenceEquals(controller, candidate))
                {
                    Debug.LogError(
                        "enemy-volatile-damage-controller-duplicated",
                        candidate);
                    return;
                }
                controller = candidate;
            }

            if (controller != null)
            {
                controller.gameObject.AddComponent<
                    EnemyVolatileDamageIntegration>();
            }
        }
    }

    [DefaultExecutionOrder(705)]
    [DisallowMultipleComponent]
    public sealed class EnemyVolatileDamageIntegration : MonoBehaviour
    {
        private sealed class ReceiverBinding
        {
            public ReceiverBinding(
                MonoBehaviour behaviour,
                IPlayablePlayerDamageReceiver receiver)
            {
                Behaviour = behaviour;
                Receiver = receiver;
            }

            public MonoBehaviour Behaviour { get; }
            public IPlayablePlayerDamageReceiver Receiver { get; }
        }

        private bool destroying;
        private string lastDiagnostic = string.Empty;

        public string LastDiagnostic { get { return lastDiagnostic; } }

        private void OnEnable()
        {
            Enemy.VolatileExploded -= HandleExplosion;
            Enemy.VolatileExploded += HandleExplosion;
        }

        private void OnDisable()
        {
            Enemy.VolatileExploded -= HandleExplosion;
        }

        private void HandleExplosion(
            Enemy source,
            EnemyVolatileExplosion explosion)
        {
            if (destroying
                || source == null
                || explosion == null
                || source.gameObject.scene != gameObject.scene)
            {
                return;
            }
            if (!source.IsBound
                || source.ActorStableId != explosion.SourceEntityStableId
                || source.LifecycleGeneration
                    != explosion.SourceLifecycleGeneration)
            {
                Report("enemy-volatile-damage-source-stale");
                return;
            }

            PlayerMarker player;
            ReceiverBinding binding;
            string diagnostic;
            if (!TryResolvePlayer(out player, out binding, out diagnostic))
            {
                Report(diagnostic);
                return;
            }

            Vector2 delta = (Vector2)player.transform.position
                - explosion.Position;
            double radiusSquared = explosion.Radius * explosion.Radius;
            if (delta.sqrMagnitude > radiusSquared)
            {
                return;
            }

            IPlayablePlayerDamageReceiver receiver = binding.Receiver;
            StableId hitId = StableId.Create(
                "enemy-volatile-hit",
                RunFingerprint.Hash(
                    "enemy-volatile-hit-v1|"
                    + explosion.EventStableId + "|"
                    + receiver.Identity.EntityInstanceId + "|"
                    + receiver.LifecycleGeneration));
            DamageReceiverCommand command;
            string rejectionCode;
            if (!PlayablePlayerDamageCommandFactory
                .TryCreateForCharacterContact(
                    receiver,
                    receiver.Identity.EntityInstanceId,
                    hitId,
                    explosion.SourceEntityStableId,
                    explosion.SourceRunParticipantStableId,
                    explosion.Damage,
                    CombatChannel.Explosive,
                    out command,
                    out rejectionCode))
            {
                Report(
                    string.IsNullOrWhiteSpace(rejectionCode)
                        ? "enemy-volatile-damage-mapping-rejected"
                        : rejectionCode);
                return;
            }

            DamageReceiverResult result = receiver.ApplyDamage(command);
            if (result == null)
            {
                Report("enemy-volatile-damage-result-missing");
            }
        }

        private bool TryResolvePlayer(
            out PlayerMarker player,
            out ReceiverBinding binding,
            out string diagnostic)
        {
            player = null;
            binding = null;
            diagnostic = string.Empty;
            Scene scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                diagnostic = "enemy-volatile-damage-scene-unavailable";
                return false;
            }

            List<PlayerMarker> players =
                FindActiveComponents<PlayerMarker>(scene);
            if (players.Count != 1)
            {
                diagnostic = players.Count == 0
                    ? "enemy-volatile-damage-player-missing"
                    : "enemy-volatile-damage-player-duplicated";
                return false;
            }

            List<ReceiverBinding> receivers = FindActiveReceivers(scene);
            if (receivers.Count != 1)
            {
                diagnostic = receivers.Count == 0
                    ? "enemy-volatile-damage-receiver-missing"
                    : "enemy-volatile-damage-receiver-duplicated";
                return false;
            }

            player = players[0];
            binding = receivers[0];
            if (binding.Behaviour.gameObject != player.gameObject
                || player.CharacterInstanceStableId == null
                || binding.Receiver.CharacterInstanceStableId == null
                || player.CharacterInstanceStableId
                    != binding.Receiver.CharacterInstanceStableId
                || binding.Receiver.Identity == null
                || binding.Receiver.Identity.EntityInstanceId == null)
            {
                diagnostic = "enemy-volatile-damage-player-mismatch";
                player = null;
                binding = null;
                return false;
            }
            return true;
        }

        private static List<T> FindActiveComponents<T>(Scene scene)
            where T : MonoBehaviour
        {
            var result = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                T[] values = roots[rootIndex]
                    .GetComponentsInChildren<T>(true);
                for (int index = 0; index < values.Length; index++)
                {
                    T value = values[index];
                    if (value != null
                        && value.gameObject.scene == scene
                        && value.isActiveAndEnabled)
                    {
                        result.Add(value);
                    }
                }
            }
            return result;
        }

        private static List<ReceiverBinding> FindActiveReceivers(Scene scene)
        {
            var result = new List<ReceiverBinding>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] values = roots[rootIndex]
                    .GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < values.Length; index++)
                {
                    MonoBehaviour value = values[index];
                    IPlayablePlayerDamageReceiver receiver =
                        value as IPlayablePlayerDamageReceiver;
                    if (value != null
                        && receiver != null
                        && value.gameObject.scene == scene
                        && value.isActiveAndEnabled)
                    {
                        result.Add(new ReceiverBinding(value, receiver));
                    }
                }
            }
            return result;
        }

        private void Report(string diagnostic)
        {
            string value = string.IsNullOrWhiteSpace(diagnostic)
                ? "enemy-volatile-damage-rejected"
                : diagnostic.Trim();
            if (string.Equals(lastDiagnostic, value, StringComparison.Ordinal))
            {
                return;
            }
            lastDiagnostic = value;
            Debug.LogError(value, this);
        }

        private void OnDestroy()
        {
            destroying = true;
            Enemy.VolatileExploded -= HandleExplosion;
        }
    }
}
