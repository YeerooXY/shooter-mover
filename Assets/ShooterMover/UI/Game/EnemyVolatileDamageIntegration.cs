using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Game
{
    internal static class EnemyVolatileDamageIntegration
    {
        private sealed class PlayerBinding
        {
            public PlayerBinding(
                PlayerMarker marker,
                IPlayablePlayerDamageReceiver receiver)
            {
                Marker = marker;
                Receiver = receiver;
            }

            public PlayerMarker Marker { get; }
            public IPlayablePlayerDamageReceiver Receiver { get; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Enemy.VolatileExploded -= HandleExplosion;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            Enemy.VolatileExploded -= HandleExplosion;
            Enemy.VolatileExploded += HandleExplosion;
        }

        private static void HandleExplosion(
            Enemy source,
            EnemyVolatileExplosion explosion)
        {
            try
            {
                RouteExplosion(source, explosion);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                Debug.LogException(exception, source);
            }
        }

        private static void RouteExplosion(
            Enemy source,
            EnemyVolatileExplosion explosion)
        {
            if (source == null
                || explosion == null
                || !source.IsBound
                || source.ActorStableId != explosion.SourceEntityStableId
                || source.LifecycleGeneration
                    != explosion.SourceLifecycleGeneration)
            {
                return;
            }

            PlayerBinding player;
            string diagnostic;
            if (!TryResolvePlayer(source.gameObject.scene, out player, out diagnostic))
            {
                Debug.LogError(diagnostic, source);
                return;
            }

            Vector2 delta = (Vector2)player.Marker.transform.position
                - explosion.Position;
            if (delta.sqrMagnitude > explosion.Radius * explosion.Radius)
            {
                return;
            }

            IPlayablePlayerDamageReceiver receiver = player.Receiver;
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
                    player.Marker.CharacterInstanceStableId,
                    hitId,
                    explosion.SourceEntityStableId,
                    explosion.SourceRunParticipantStableId,
                    explosion.Damage,
                    CombatChannel.Explosive,
                    out command,
                    out rejectionCode))
            {
                Debug.LogError(
                    string.IsNullOrWhiteSpace(rejectionCode)
                        ? "enemy-volatile-damage-mapping-rejected"
                        : rejectionCode,
                    source);
                return;
            }

            if (receiver.ApplyDamage(command) == null)
            {
                Debug.LogError("enemy-volatile-damage-result-missing", source);
            }
        }

        private static bool TryResolvePlayer(
            Scene scene,
            out PlayerBinding binding,
            out string diagnostic)
        {
            binding = null;
            diagnostic = string.Empty;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                diagnostic = "enemy-volatile-damage-scene-unavailable";
                return false;
            }

            PlayerMarker marker = null;
            IPlayablePlayerDamageReceiver receiver = null;
            MonoBehaviour receiverBehaviour = null;
            int markerCount = 0;
            int receiverCount = 0;
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<
                MonoBehaviour>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour.gameObject.scene != scene)
                {
                    continue;
                }

                PlayerMarker candidateMarker = behaviour as PlayerMarker;
                if (candidateMarker != null)
                {
                    marker = candidateMarker;
                    markerCount++;
                }

                IPlayablePlayerDamageReceiver candidateReceiver =
                    behaviour as IPlayablePlayerDamageReceiver;
                if (candidateReceiver != null)
                {
                    receiver = candidateReceiver;
                    receiverBehaviour = behaviour;
                    receiverCount++;
                }
            }

            if (markerCount != 1 || receiverCount != 1)
            {
                diagnostic = markerCount != 1
                    ? "enemy-volatile-damage-player-count:" + markerCount
                    : "enemy-volatile-damage-receiver-count:" + receiverCount;
                return false;
            }
            if (receiverBehaviour.gameObject != marker.gameObject
                || marker.CharacterInstanceStableId == null
                || receiver.CharacterInstanceStableId
                    != marker.CharacterInstanceStableId
                || receiver.Identity.EntityInstanceId == null)
            {
                diagnostic = "enemy-volatile-damage-player-mismatch";
                return false;
            }

            binding = new PlayerBinding(marker, receiver);
            return true;
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }
    }
}
