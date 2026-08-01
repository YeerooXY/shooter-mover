using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Game
{
    internal static class VolatileDamage
    {
        private sealed class PlayerTarget
        {
            public PlayerTarget(
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
            Enemy.VolatileExploded -= OnExplosion;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            Enemy.VolatileExploded -= OnExplosion;
            Enemy.VolatileExploded += OnExplosion;
        }

        private static void OnExplosion(Enemy source, VolatileBlast blast)
        {
            try
            {
                DamagePlayer(source, blast);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                Debug.LogException(exception, source);
            }
        }

        private static void DamagePlayer(Enemy source, VolatileBlast blast)
        {
            if (source == null
                || blast == null
                || !source.IsBound
                || source.ActorStableId != blast.EnemyId
                || source.LifecycleGeneration != blast.Generation)
            {
                return;
            }

            PlayerTarget player;
            string error;
            if (!TryFindPlayer(source.gameObject.scene, out player, out error))
            {
                Debug.LogError(error, source);
                return;
            }

            Vector2 delta = (Vector2)player.Marker.transform.position
                - blast.Position;
            if (delta.sqrMagnitude > blast.Radius * blast.Radius)
            {
                return;
            }

            IPlayablePlayerDamageReceiver receiver = player.Receiver;
            StableId hitId = StableId.Create(
                "enemy-volatile-hit",
                RunFingerprint.Hash(
                    "enemy-volatile-hit-v1|"
                    + blast.EventId + "|"
                    + receiver.Identity.EntityInstanceId + "|"
                    + receiver.LifecycleGeneration));
            DamageReceiverCommand command;
            string errorCode;
            if (!PlayablePlayerDamageCommandFactory
                .TryCreateForCharacterContact(
                    receiver,
                    player.Marker.CharacterInstanceStableId,
                    hitId,
                    blast.EnemyId,
                    blast.RunParticipantId,
                    blast.Damage,
                    CombatChannel.Explosive,
                    out command,
                    out errorCode))
            {
                Debug.LogError(
                    string.IsNullOrWhiteSpace(errorCode)
                        ? "enemy-volatile-damage-mapping-rejected"
                        : errorCode,
                    source);
                return;
            }

            if (receiver.ApplyDamage(command) == null)
            {
                Debug.LogError("enemy-volatile-damage-result-missing", source);
            }
        }

        private static bool TryFindPlayer(
            Scene scene,
            out PlayerTarget player,
            out string error)
        {
            player = null;
            error = string.Empty;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = "enemy-volatile-damage-scene-unavailable";
                return false;
            }

            PlayerMarker marker = null;
            IPlayablePlayerDamageReceiver receiver = null;
            MonoBehaviour receiverView = null;
            int markerCount = 0;
            int receiverCount = 0;
            MonoBehaviour[] views = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < views.Length; index++)
            {
                MonoBehaviour view = views[index];
                if (view == null
                    || view.gameObject.scene != scene
                    || !view.isActiveAndEnabled)
                {
                    continue;
                }

                PlayerMarker foundMarker = view as PlayerMarker;
                if (foundMarker != null)
                {
                    marker = foundMarker;
                    markerCount++;
                }

                IPlayablePlayerDamageReceiver foundReceiver =
                    view as IPlayablePlayerDamageReceiver;
                if (foundReceiver != null)
                {
                    receiver = foundReceiver;
                    receiverView = view;
                    receiverCount++;
                }
            }

            if (markerCount != 1 || receiverCount != 1)
            {
                error = markerCount != 1
                    ? "enemy-volatile-damage-player-count:" + markerCount
                    : "enemy-volatile-damage-receiver-count:" + receiverCount;
                return false;
            }
            if (receiverView.gameObject != marker.gameObject
                || marker.CharacterInstanceStableId == null
                || receiver.CharacterInstanceStableId
                    != marker.CharacterInstanceStableId
                || receiver.Identity.EntityInstanceId == null)
            {
                error = "enemy-volatile-damage-player-mismatch";
                return false;
            }

            player = new PlayerTarget(marker, receiver);
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
