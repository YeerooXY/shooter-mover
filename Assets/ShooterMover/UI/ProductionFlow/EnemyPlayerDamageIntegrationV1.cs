using System;
using System.Collections.Generic;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    internal static class EnemyPlayerDamageIntegrationInstallerV1
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
                    ProductionPlayableLevelCatalogV1.PlayableLevelScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            ProductionPlayableLevelControllerV1 controller = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].GetComponentInChildren<
                        EnemyPlayerDamageIntegrationV1>(true) != null)
                {
                    return;
                }

                ProductionPlayableLevelControllerV1 candidate = roots[index]
                    .GetComponentInChildren<ProductionPlayableLevelControllerV1>(true);
                if (candidate == null) continue;
                if (controller != null && !ReferenceEquals(controller, candidate))
                {
                    Debug.LogError(
                        "enemy-player-damage-controller-duplicated",
                        candidate);
                    return;
                }
                controller = candidate;
            }

            if (controller != null)
            {
                controller.gameObject.AddComponent<EnemyPlayerDamageIntegrationV1>();
            }
        }
    }

    /// <summary>
    /// Scene-local integration boundary from neutral EnemyHitV1 facts to the exact current
    /// IPlayablePlayerDamageReceiverV1. PlayerActorAuthority remains the only health, replay,
    /// lifecycle and defeat authority.
    /// </summary>
    [DefaultExecutionOrder(700)]
    [DisallowMultipleComponent]
    public sealed class EnemyPlayerDamageIntegrationV1 : MonoBehaviour
    {
        private const int StartupResolutionAttemptLimit = 240;

        private readonly EnemyHitSubscriptionSetV1 subscriptions =
            new EnemyHitSubscriptionSetV1();

        private RoomRuntimeComposition2D roomRuntime;
        private PlayablePlayerMarker2D playerMarker;
        private MonoBehaviour receiverBehaviour;
        private IPlayablePlayerDamageReceiverV1 playerDamageReceiver;
        private DamageReceiverResult lastDamageResult;
        private int remainingResolutionAttempts;
        private bool started;
        private bool resolving;
        private bool resolutionRejected;
        private bool defeatAccepted;
        private bool destroying;
        private string lastDiagnostic = string.Empty;

        public bool IsResolved
        {
            get
            {
                return !resolving
                    && !resolutionRejected
                    && playerDamageReceiver != null
                    && roomRuntime != null;
            }
        }

        public int SubscribedEnemyPublisherCount { get { return subscriptions.Count; } }
        public bool IsDefeatAccepted { get { return defeatAccepted; } }
        public DamageReceiverResult LastDamageResult { get { return lastDamageResult; } }
        public string LastDiagnostic { get { return lastDiagnostic; } }

        private void OnEnable()
        {
            if (started && !destroying && !resolutionRejected && !defeatAccepted)
            {
                BeginResolution();
            }
        }

        private void Start()
        {
            started = true;
            BeginResolution();
        }

        private void Update()
        {
            if (destroying || resolutionRejected || defeatAccepted) return;

            if (resolving)
            {
                string diagnostic;
                EnemyPublisherResolutionStatusV1 status =
                    TryResolveAndSubscribe(out diagnostic);
                if (status == EnemyPublisherResolutionStatusV1.Ready)
                {
                    resolving = false;
                    remainingResolutionAttempts = 0;
                    lastDiagnostic = string.Empty;
                    return;
                }
                if (status == EnemyPublisherResolutionStatusV1.Rejected)
                {
                    RejectResolution(diagnostic);
                    return;
                }

                remainingResolutionAttempts--;
                if (remainingResolutionAttempts <= 0)
                {
                    RejectResolution(
                        "enemy-player-damage-resolution-timeout:"
                        + NormalizeDiagnostic(diagnostic, "dependencies-pending"));
                }
                return;
            }

            if (!KnownBindingsAreCurrent())
            {
                BeginResolution();
            }
        }

        private void BeginResolution()
        {
            if (destroying || resolutionRejected || defeatAccepted) return;
            UnsubscribeAll();
            resolving = true;
            remainingResolutionAttempts = StartupResolutionAttemptLimit;
            lastDiagnostic = string.Empty;
        }

        private EnemyPublisherResolutionStatusV1 TryResolveAndSubscribe(
            out string diagnostic)
        {
            diagnostic = string.Empty;
            Scene scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                diagnostic = "enemy-player-damage-scene-pending";
                return EnemyPublisherResolutionStatusV1.Pending;
            }

            List<PlayablePlayerMarker2D> markers =
                FindActiveComponents<PlayablePlayerMarker2D>(scene);
            if (markers.Count > 1)
            {
                diagnostic = "enemy-player-damage-player-marker-duplicated";
                return EnemyPublisherResolutionStatusV1.Rejected;
            }
            if (markers.Count == 0)
            {
                diagnostic = "enemy-player-damage-player-marker-pending";
                return EnemyPublisherResolutionStatusV1.Pending;
            }

            List<ReceiverBinding> receivers = FindActiveReceivers(scene);
            if (receivers.Count > 1)
            {
                diagnostic = "enemy-player-damage-player-receiver-duplicated";
                return EnemyPublisherResolutionStatusV1.Rejected;
            }
            if (receivers.Count == 0)
            {
                diagnostic = "enemy-player-damage-player-receiver-pending";
                return EnemyPublisherResolutionStatusV1.Pending;
            }

            List<RoomRuntimeComposition2D> rooms =
                FindActiveComponents<RoomRuntimeComposition2D>(scene);
            if (rooms.Count > 1)
            {
                diagnostic = "enemy-player-damage-room-runtime-duplicated";
                return EnemyPublisherResolutionStatusV1.Rejected;
            }
            if (rooms.Count == 0 || !rooms[0].IsBuilt)
            {
                diagnostic = "enemy-player-damage-room-runtime-pending";
                return EnemyPublisherResolutionStatusV1.Pending;
            }

            int authoritativeEnemyCount;
            try
            {
                authoritativeEnemyCount =
                    EnemyPublisherReconciliationV1.CountAuthoritativeActiveEnemies(rooms[0]);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                diagnostic = "enemy-player-damage-room-projection-pending:"
                    + exception.GetType().Name;
                return EnemyPublisherResolutionStatusV1.Pending;
            }

            List<EnemyAttack2D> discovered = FindActiveComponents<EnemyAttack2D>(scene);
            var ready = new List<EnemyAttack2D>();
            bool hasNonTerminalUnboundPublisher = false;
            for (int index = 0; index < discovered.Count; index++)
            {
                EnemyAttack2D publisher = discovered[index];
                if (publisher.IsTerminalStopped) continue;
                if (!publisher.IsBound)
                {
                    hasNonTerminalUnboundPublisher = true;
                    continue;
                }
                ready.Add(publisher);
            }

            EnemyPublisherResolutionStatusV1 publisherStatus =
                EnemyPublisherReconciliationV1.Classify(
                    authoritativeEnemyCount,
                    ready.Count,
                    hasNonTerminalUnboundPublisher,
                    out diagnostic);
            if (publisherStatus != EnemyPublisherResolutionStatusV1.Ready)
            {
                return publisherStatus;
            }

            PlayablePlayerMarker2D marker = markers[0];
            ReceiverBinding receiver = receivers[0];
            if (receiver.Behaviour.gameObject != marker.gameObject)
            {
                diagnostic = "enemy-player-damage-player-binding-object-mismatch";
                return EnemyPublisherResolutionStatusV1.Rejected;
            }
            if (marker.CharacterInstanceStableId == null
                || receiver.Receiver.CharacterInstanceStableId == null
                || marker.CharacterInstanceStableId
                    != receiver.Receiver.CharacterInstanceStableId)
            {
                diagnostic = "enemy-player-damage-player-character-mismatch";
                return EnemyPublisherResolutionStatusV1.Rejected;
            }

            try
            {
                playerMarker = marker;
                receiverBehaviour = receiver.Behaviour;
                playerDamageReceiver = receiver.Receiver;
                roomRuntime = rooms[0];
                roomRuntime.CurrentRoomPresentationRebuilt +=
                    HandleRoomPresentationRebuilt;
                playerDamageReceiver.Defeated += HandlePlayerDefeated;
                subscriptions.Replace(ready, HandleEnemyHit);
                return EnemyPublisherResolutionStatusV1.Ready;
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                UnsubscribeAll();
                diagnostic = "enemy-player-damage-subscription-exception:"
                    + exception.GetType().Name;
                Debug.LogException(exception, this);
                return EnemyPublisherResolutionStatusV1.Rejected;
            }
        }

        private void HandleEnemyHit(EnemyAttack2D publisher, EnemyHitV1 hit)
        {
            if (destroying
                || defeatAccepted
                || publisher == null
                || hit == null
                || !subscriptions.Contains(publisher))
            {
                return;
            }

            try
            {
                if (!IsReceiverCurrent())
                {
                    ReportDiagnostic("enemy-player-damage-receiver-stale");
                    return;
                }
                if (publisher.gameObject.scene != gameObject.scene || !publisher.IsBound)
                {
                    ReportDiagnostic("enemy-player-damage-publisher-stale");
                    return;
                }

                RoomEnemyActor2D enemyActor = publisher.GetComponent<RoomEnemyActor2D>();
                if (enemyActor == null
                    || !enemyActor.IsBound
                    || enemyActor.ActorStableId != hit.SourceEntityStableId
                    || enemyActor.LifecycleGeneration != hit.SourceLifecycleGeneration
                    || publisher.PresentationRevision != hit.SourceLifecycleGeneration)
                {
                    ReportDiagnostic("enemy-player-damage-hit-source-stale");
                    return;
                }
                if (hit.ContactStableId == null
                    || hit.SourceRunParticipantStableId == null
                    || hit.TargetEntityStableId == null
                    || hit.TargetCollider == null
                    || hit.TargetCollider.gameObject.scene != gameObject.scene
                    || !IsFinitePositive(hit.ResolvedDamage))
                {
                    ReportDiagnostic("enemy-player-damage-hit-invalid");
                    return;
                }

                CombatChannel channel;
                if (!EnemyPlayerDamageChannelMapV1.TryMap(
                        hit.DamageChannelStableId,
                        out channel))
                {
                    ReportDiagnostic(
                        "enemy-player-damage-channel-unknown:"
                        + (hit.DamageChannelStableId == null
                            ? "null"
                            : hit.DamageChannelStableId.ToString()));
                    return;
                }

                DamageReceiverCommand command;
                string rejectionCode;
                if (!PlayablePlayerDamageCommandFactoryV1.TryCreateForCharacterContact(
                        playerDamageReceiver,
                        hit.TargetEntityStableId,
                        hit.ContactStableId,
                        hit.SourceEntityStableId,
                        hit.SourceRunParticipantStableId,
                        hit.ResolvedDamage,
                        channel,
                        out command,
                        out rejectionCode))
                {
                    ReportDiagnostic(
                        NormalizeDiagnostic(
                            rejectionCode,
                            "enemy-player-damage-mapping-rejected"));
                    return;
                }

                lastDamageResult = playerDamageReceiver.ApplyDamage(command);
                if (lastDamageResult == null)
                {
                    ReportDiagnostic("enemy-player-damage-result-missing");
                }
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                ReportDiagnostic(
                    "enemy-player-damage-hit-exception:"
                    + exception.GetType().Name);
                Debug.LogException(exception, this);
            }
        }

        private void HandlePlayerDefeated(PlayablePlayerDefeatedFactV1 fact)
        {
            if (destroying || defeatAccepted || fact == null) return;
            if (!IsReceiverCurrent()
                || fact.ActorInstanceStableId
                    != playerDamageReceiver.Identity.EntityInstanceId
                || fact.CharacterInstanceStableId
                    != playerDamageReceiver.CharacterInstanceStableId
                || fact.LifecycleGeneration != playerDamageReceiver.LifecycleGeneration)
            {
                ReportDiagnostic("enemy-player-damage-defeat-lifecycle-mismatch");
                return;
            }

            defeatAccepted = true;
            UnsubscribeAll();
        }

        private void HandleRoomPresentationRebuilt()
        {
            if (!destroying && !defeatAccepted && !resolutionRejected)
            {
                BeginResolution();
            }
        }

        private bool KnownBindingsAreCurrent()
        {
            if (!IsReceiverCurrent()
                || roomRuntime == null
                || roomRuntime.gameObject.scene != gameObject.scene)
            {
                return false;
            }

            try
            {
                int expected =
                    EnemyPublisherReconciliationV1.CountAuthoritativeActiveEnemies(
                        roomRuntime);
                return subscriptions.Count == expected
                    && subscriptions.AllCurrent(gameObject.scene);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                return false;
            }
        }

        private bool IsReceiverCurrent()
        {
            return playerMarker != null
                && playerMarker.gameObject.scene == gameObject.scene
                && receiverBehaviour != null
                && receiverBehaviour.gameObject.scene == gameObject.scene
                && receiverBehaviour.isActiveAndEnabled
                && playerDamageReceiver != null
                && playerDamageReceiver.CharacterInstanceStableId
                    == playerMarker.CharacterInstanceStableId;
        }

        private void RejectResolution(string diagnostic)
        {
            UnsubscribeAll();
            resolving = false;
            resolutionRejected = true;
            lastDiagnostic = NormalizeDiagnostic(
                diagnostic,
                "enemy-player-damage-resolution-rejected");
            Debug.LogError(lastDiagnostic, this);
            enabled = false;
        }

        private void ReportDiagnostic(string diagnostic)
        {
            string normalized = NormalizeDiagnostic(
                diagnostic,
                "enemy-player-damage-integration-error");
            if (string.Equals(lastDiagnostic, normalized, StringComparison.Ordinal)) return;
            lastDiagnostic = normalized;
            Debug.LogError(normalized, this);
        }

        private void UnsubscribeAll()
        {
            if (roomRuntime != null)
            {
                roomRuntime.CurrentRoomPresentationRebuilt -=
                    HandleRoomPresentationRebuilt;
            }
            if (playerDamageReceiver != null)
            {
                playerDamageReceiver.Defeated -= HandlePlayerDefeated;
            }
            subscriptions.Clear();
            roomRuntime = null;
            playerMarker = null;
            receiverBehaviour = null;
            playerDamageReceiver = null;
        }

        private void OnDisable()
        {
            if (!destroying) UnsubscribeAll();
        }

        private void OnDestroy()
        {
            destroying = true;
            UnsubscribeAll();
        }

        private static List<T> FindActiveComponents<T>(Scene scene)
            where T : MonoBehaviour
        {
            var result = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                T[] values = roots[rootIndex].GetComponentsInChildren<T>(true);
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
                    IPlayablePlayerDamageReceiverV1 receiver =
                        value as IPlayablePlayerDamageReceiverV1;
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

        private static string NormalizeDiagnostic(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value > 0d;
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }

        private sealed class ReceiverBinding
        {
            public ReceiverBinding(
                MonoBehaviour behaviour,
                IPlayablePlayerDamageReceiverV1 receiver)
            {
                Behaviour = behaviour
                    ?? throw new ArgumentNullException(nameof(behaviour));
                Receiver = receiver
                    ?? throw new ArgumentNullException(nameof(receiver));
            }

            public MonoBehaviour Behaviour { get; }
            public IPlayablePlayerDamageReceiverV1 Receiver { get; }
        }
    }
}
