using System;
using System.Collections.Generic;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Explicit integration-owned mapping from neutral enemy attack damage channels to the
    /// canonical player combat channel. Unknown channels fail closed.
    /// </summary>
    public static class FirstCombatRoomEnemyDamageChannelMapV1
    {
        private static readonly StableId KineticDamageChannelStableId =
            StableId.Parse("damage.kinetic");

        public static bool TryMap(
            StableId damageChannelStableId,
            out CombatChannel channel)
        {
            channel = default(CombatChannel);
            if (damageChannelStableId == null
                || damageChannelStableId != KineticDamageChannelStableId)
            {
                return false;
            }

            channel = CombatChannel.Kinetic;
            return true;
        }
    }

    /// <summary>
    /// Lifecycle-local shutdown seam for the production player weapon. Disabling the controller
    /// clears held input synchronously through OnDisable; destroying it invokes its existing
    /// runtime disposal path. Existing player-owned projectiles are deactivated before their
    /// deferred destruction so defeat cannot produce another collision or emission.
    /// </summary>
    public static class ProductionPlayablePlayerWeaponDefeatShutdownV1
    {
        public static int DisableForDefeat(
            ProductionPlayablePlayerWeaponControllerV1 controller,
            ProductionNormalProjectileEffectSink2D effectSink,
            Scene scene)
        {
            if (controller != null)
            {
                controller.enabled = false;
            }
            if (effectSink != null)
            {
                effectSink.enabled = false;
            }

            int stoppedProjectiles = StopPlayerProjectiles(scene);
            DestroyLifecycleLocal(controller);
            DestroyLifecycleLocal(effectSink);
            return stoppedProjectiles;
        }

        private static int StopPlayerProjectiles(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return 0;
            }

            int stopped = 0;
            var visited = new HashSet<GameObject>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                ProductionNormalProjectile2D[] projectiles = roots[rootIndex]
                    .GetComponentsInChildren<ProductionNormalProjectile2D>(true);
                for (int index = 0; index < projectiles.Length; index++)
                {
                    ProductionNormalProjectile2D projectile = projectiles[index];
                    if (projectile == null
                        || projectile.gameObject.scene != scene
                        || !visited.Add(projectile.gameObject))
                    {
                        continue;
                    }

                    stopped++;
                    projectile.gameObject.SetActive(false);
                    DestroyLifecycleLocal(projectile.gameObject);
                }
            }

            return stopped;
        }

        private static void DestroyLifecycleLocal(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(value);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }

    internal static class FirstCombatRoomCombatIntegrationInstallerV1
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

            GameObject[] roots = scene.GetRootGameObjects();
            ProductionPlayableLevelControllerV1 controller = null;
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].GetComponentInChildren<
                        FirstCombatRoomCombatIntegrationV1>(true) != null)
                {
                    return;
                }

                ProductionPlayableLevelControllerV1 candidate = roots[index]
                    .GetComponentInChildren<ProductionPlayableLevelControllerV1>(true);
                if (candidate != null && controller == null)
                {
                    controller = candidate;
                }
            }

            // The production level controller has its own fatal composition diagnostic. Its
            // temporary absence during runtime-hook ordering is not an integration error.
            if (controller != null)
            {
                controller.gameObject.AddComponent<
                    FirstCombatRoomCombatIntegrationV1>();
            }
        }
    }

    /// <summary>
    /// Final first-combat-room integration boundary. It owns neutral enemy-hit subscriptions,
    /// maps accepted contacts into the current run-local player authority, and shuts down the
    /// exact player weapon immediately after the first canonical defeat fact.
    /// </summary>
    [DefaultExecutionOrder(700)]
    [DisallowMultipleComponent]
    public sealed class FirstCombatRoomCombatIntegrationV1 : MonoBehaviour
    {
        private const int StartupResolutionAttemptLimit = 240;

        private readonly Dictionary<EnemyAttack2D, Action<EnemyHitV1>>
            enemyHitSubscriptions =
                new Dictionary<EnemyAttack2D, Action<EnemyHitV1>>();

        private RoomRuntimeComposition2D roomRuntime;
        private PlayablePlayerMarker2D playerMarker;
        private MonoBehaviour receiverBehaviour;
        private IPlayablePlayerDamageReceiverV1 playerDamageReceiver;
        private ProductionPlayablePlayerWeaponControllerV1 playerWeaponController;
        private ProductionNormalProjectileEffectSink2D playerProjectileSink;
        private DamageReceiverResult lastDamageResult;
        private int remainingResolutionAttempts;
        private int acceptedDefeatCount;
        private int stoppedProjectileCount;
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
                    && roomRuntime != null
                    && enemyHitSubscriptions.Count > 0;
            }
        }

        public int SubscribedEnemyPublisherCount
        {
            get { return enemyHitSubscriptions.Count; }
        }

        public int AcceptedDefeatCount
        {
            get { return acceptedDefeatCount; }
        }

        public int StoppedPlayerProjectileCount
        {
            get { return stoppedProjectileCount; }
        }

        public bool IsDefeatAccepted
        {
            get { return defeatAccepted; }
        }

        public DamageReceiverResult LastDamageResult
        {
            get { return lastDamageResult; }
        }

        public string LastDiagnostic
        {
            get { return lastDiagnostic; }
        }

        private void OnEnable()
        {
            if (started && !destroying && !resolutionRejected)
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
            if (destroying || resolutionRejected)
            {
                return;
            }

            if (resolving)
            {
                string diagnostic;
                ResolutionStatus status = TryResolveAndSubscribe(out diagnostic);
                if (status == ResolutionStatus.Ready)
                {
                    resolving = false;
                    remainingResolutionAttempts = 0;
                    lastDiagnostic = string.Empty;
                    return;
                }
                if (status == ResolutionStatus.Rejected)
                {
                    RejectResolution(diagnostic);
                    return;
                }

                remainingResolutionAttempts--;
                if (remainingResolutionAttempts <= 0)
                {
                    RejectResolution(
                        "first-combat-room-integration-resolution-timeout:"
                        + NormalizeDiagnostic(
                            diagnostic,
                            "combat-dependencies-pending"));
                }
                return;
            }

            // After defeat the controller and sink are intentionally destroyed. The player
            // receiver and enemy subscriptions remain alive until scene cleanup so late contacts
            // still flow to the canonical dead-target rejection path.
            if (!defeatAccepted && !KnownPlayerBindingsAreCurrent())
            {
                BeginResolution();
            }
        }

        private void BeginResolution()
        {
            if (destroying || resolutionRejected)
            {
                return;
            }

            UnsubscribeAll();
            resolving = true;
            remainingResolutionAttempts = StartupResolutionAttemptLimit;
            lastDiagnostic = string.Empty;
        }

        private ResolutionStatus TryResolveAndSubscribe(out string diagnostic)
        {
            diagnostic = string.Empty;
            Scene scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                diagnostic = "first-combat-room-integration-scene-pending";
                return ResolutionStatus.Pending;
            }

            List<PlayablePlayerMarker2D> markers =
                FindActiveComponents<PlayablePlayerMarker2D>(scene);
            if (markers.Count > 1)
            {
                diagnostic =
                    "first-combat-room-integration-player-marker-duplicated";
                return ResolutionStatus.Rejected;
            }
            if (markers.Count == 0)
            {
                diagnostic = "first-combat-room-integration-player-marker-pending";
                return ResolutionStatus.Pending;
            }

            List<ReceiverBinding> receivers = FindActiveReceivers(scene);
            if (receivers.Count > 1)
            {
                diagnostic =
                    "first-combat-room-integration-player-receiver-duplicated";
                return ResolutionStatus.Rejected;
            }
            if (receivers.Count == 0)
            {
                diagnostic = "first-combat-room-integration-player-receiver-pending";
                return ResolutionStatus.Pending;
            }

            List<ProductionPlayablePlayerWeaponControllerV1> controllers =
                FindActiveComponents<ProductionPlayablePlayerWeaponControllerV1>(scene);
            if (controllers.Count > 1)
            {
                diagnostic =
                    "first-combat-room-integration-weapon-controller-duplicated";
                return ResolutionStatus.Rejected;
            }
            if (controllers.Count == 0 || !controllers[0].IsBound)
            {
                diagnostic =
                    "first-combat-room-integration-weapon-controller-pending";
                return ResolutionStatus.Pending;
            }

            List<ProductionNormalProjectileEffectSink2D> sinks =
                FindActiveComponents<ProductionNormalProjectileEffectSink2D>(scene);
            if (sinks.Count > 1)
            {
                diagnostic =
                    "first-combat-room-integration-projectile-sink-duplicated";
                return ResolutionStatus.Rejected;
            }
            if (sinks.Count == 0)
            {
                diagnostic =
                    "first-combat-room-integration-projectile-sink-pending";
                return ResolutionStatus.Pending;
            }

            List<RoomRuntimeComposition2D> rooms =
                FindActiveComponents<RoomRuntimeComposition2D>(scene);
            if (rooms.Count > 1)
            {
                diagnostic =
                    "first-combat-room-integration-room-runtime-duplicated";
                return ResolutionStatus.Rejected;
            }
            if (rooms.Count == 0 || !rooms[0].IsBuilt)
            {
                diagnostic = "first-combat-room-integration-room-runtime-pending";
                return ResolutionStatus.Pending;
            }

            List<EnemyAttack2D> publishers =
                FindActiveComponents<EnemyAttack2D>(scene);
            if (publishers.Count == 0)
            {
                diagnostic =
                    "first-combat-room-integration-enemy-publisher-pending";
                return ResolutionStatus.Pending;
            }
            for (int index = 0; index < publishers.Count; index++)
            {
                if (!publishers[index].IsBound
                    || publishers[index].IsTerminalStopped)
                {
                    diagnostic =
                        "first-combat-room-integration-enemy-publisher-binding-pending";
                    return ResolutionStatus.Pending;
                }
            }

            PlayablePlayerMarker2D marker = markers[0];
            ReceiverBinding receiver = receivers[0];
            ProductionPlayablePlayerWeaponControllerV1 controller = controllers[0];
            ProductionNormalProjectileEffectSink2D sink = sinks[0];
            if (receiver.Behaviour.gameObject != marker.gameObject
                || controller.gameObject != marker.gameObject
                || sink.gameObject != marker.gameObject)
            {
                diagnostic =
                    "first-combat-room-integration-player-binding-object-mismatch";
                return ResolutionStatus.Rejected;
            }
            if (marker.CharacterInstanceStableId == null
                || receiver.Receiver.CharacterInstanceStableId == null
                || marker.CharacterInstanceStableId
                    != receiver.Receiver.CharacterInstanceStableId)
            {
                diagnostic =
                    "first-combat-room-integration-player-character-mismatch";
                return ResolutionStatus.Rejected;
            }

            try
            {
                playerMarker = marker;
                receiverBehaviour = receiver.Behaviour;
                playerDamageReceiver = receiver.Receiver;
                playerWeaponController = controller;
                playerProjectileSink = sink;
                roomRuntime = rooms[0];

                roomRuntime.CurrentRoomPresentationRebuilt +=
                    HandleRoomPresentationRebuilt;
                playerDamageReceiver.Defeated += HandlePlayerDefeated;
                for (int index = 0; index < publishers.Count; index++)
                {
                    EnemyAttack2D publisher = publishers[index];
                    if (enemyHitSubscriptions.ContainsKey(publisher))
                    {
                        continue;
                    }

                    EnemyAttack2D capturedPublisher = publisher;
                    Action<EnemyHitV1> handler = hit =>
                        HandleEnemyHit(capturedPublisher, hit);
                    publisher.Hit += handler;
                    enemyHitSubscriptions.Add(publisher, handler);
                }

                return ResolutionStatus.Ready;
            }
            catch (Exception exception)
            {
                if (IsFatal(exception))
                {
                    throw;
                }

                UnsubscribeAll();
                diagnostic =
                    "first-combat-room-integration-subscription-exception:"
                    + exception.GetType().Name;
                Debug.LogException(exception, this);
                return ResolutionStatus.Rejected;
            }
        }

        private void HandleEnemyHit(
            EnemyAttack2D publisher,
            EnemyHitV1 hit)
        {
            if (destroying
                || publisher == null
                || hit == null
                || !enemyHitSubscriptions.ContainsKey(publisher))
            {
                return;
            }

            try
            {
                if (!IsReceiverCurrent())
                {
                    ReportDiagnostic(
                        "first-combat-room-integration-player-receiver-stale");
                    return;
                }
                if (publisher.gameObject.scene != gameObject.scene
                    || !publisher.IsBound)
                {
                    ReportDiagnostic(
                        "first-combat-room-integration-enemy-publisher-stale");
                    return;
                }

                RoomEnemyActor2D enemyActor =
                    publisher.GetComponent<RoomEnemyActor2D>();
                if (enemyActor == null
                    || !enemyActor.IsBound
                    || enemyActor.ActorStableId != hit.SourceEntityStableId
                    || enemyActor.LifecycleGeneration
                        != hit.SourceLifecycleGeneration
                    || publisher.PresentationRevision
                        != hit.SourceLifecycleGeneration)
                {
                    ReportDiagnostic(
                        "first-combat-room-integration-enemy-hit-source-stale");
                    return;
                }
                if (hit.ContactStableId == null
                    || hit.SourceRunParticipantStableId == null
                    || hit.TargetEntityStableId == null
                    || hit.TargetCollider == null
                    || hit.TargetCollider.gameObject.scene != gameObject.scene
                    || !IsFinitePositive(hit.ResolvedDamage))
                {
                    ReportDiagnostic(
                        "first-combat-room-integration-enemy-hit-invalid");
                    return;
                }

                CombatChannel channel;
                if (!FirstCombatRoomEnemyDamageChannelMapV1.TryMap(
                        hit.DamageChannelStableId,
                        out channel))
                {
                    ReportDiagnostic(
                        "first-combat-room-integration-enemy-damage-channel-unknown:"
                        + (hit.DamageChannelStableId == null
                            ? "null"
                            : hit.DamageChannelStableId.ToString()));
                    return;
                }

                DamageReceiverCommand command;
                string rejectionCode;
                if (!PlayablePlayerDamageCommandFactoryV1
                        .TryCreateForCharacterContact(
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
                            "first-combat-room-integration-enemy-hit-mapping-rejected"));
                    return;
                }

                // The PlayerActorAuthority result remains the only replay, conflict, target,
                // lifecycle, alive/dead, health, feedback and defeat source of truth.
                lastDamageResult = playerDamageReceiver.ApplyDamage(command);
                if (lastDamageResult == null)
                {
                    ReportDiagnostic(
                        "first-combat-room-integration-player-damage-result-missing");
                }
            }
            catch (Exception exception)
            {
                if (IsFatal(exception))
                {
                    throw;
                }

                ReportDiagnostic(
                    "first-combat-room-integration-enemy-hit-exception:"
                    + exception.GetType().Name);
                Debug.LogException(exception, this);
            }
        }

        private void HandlePlayerDefeated(PlayablePlayerDefeatedFactV1 fact)
        {
            if (destroying || fact == null || defeatAccepted)
            {
                return;
            }

            try
            {
                if (!IsReceiverCurrent()
                    || fact.ActorInstanceStableId
                        != playerDamageReceiver.Identity.EntityInstanceId
                    || fact.CharacterInstanceStableId
                        != playerDamageReceiver.CharacterInstanceStableId
                    || fact.LifecycleGeneration
                        != playerDamageReceiver.LifecycleGeneration)
                {
                    ReportDiagnostic(
                        "first-combat-room-integration-defeat-lifecycle-mismatch");
                    return;
                }

                // Latch before shutdown so an ordinary cleanup failure cannot admit a second
                // defeat-side effect. The canonical vitals adapter continues its Hub request
                // after this synchronous observer returns.
                defeatAccepted = true;
                acceptedDefeatCount = checked(acceptedDefeatCount + 1);
                stoppedProjectileCount =
                    ProductionPlayablePlayerWeaponDefeatShutdownV1
                        .DisableForDefeat(
                            playerWeaponController,
                            playerProjectileSink,
                            gameObject.scene);
                playerWeaponController = null;
                playerProjectileSink = null;
            }
            catch (Exception exception)
            {
                if (IsFatal(exception))
                {
                    throw;
                }

                ReportDiagnostic(
                    "first-combat-room-integration-defeat-shutdown-exception:"
                    + exception.GetType().Name);
                Debug.LogException(exception, this);
            }
        }

        private void HandleRoomPresentationRebuilt()
        {
            if (!destroying && !defeatAccepted && !resolutionRejected)
            {
                BeginResolution();
            }
        }

        private bool KnownPlayerBindingsAreCurrent()
        {
            return roomRuntime != null
                && roomRuntime.gameObject.scene == gameObject.scene
                && playerMarker != null
                && playerMarker.gameObject.scene == gameObject.scene
                && receiverBehaviour != null
                && receiverBehaviour.gameObject.scene == gameObject.scene
                && playerDamageReceiver != null
                && playerWeaponController != null
                && playerWeaponController.gameObject.scene == gameObject.scene
                && playerProjectileSink != null
                && playerProjectileSink.gameObject.scene == gameObject.scene;
        }

        private bool IsReceiverCurrent()
        {
            if (playerDamageReceiver == null
                || receiverBehaviour == null
                || playerMarker == null
                || receiverBehaviour.gameObject.scene != gameObject.scene
                || playerMarker.gameObject.scene != gameObject.scene
                || receiverBehaviour.gameObject != playerMarker.gameObject
                || playerDamageReceiver.CharacterInstanceStableId == null
                || playerDamageReceiver.CharacterInstanceStableId
                    != playerMarker.CharacterInstanceStableId)
            {
                return false;
            }

            return true;
        }

        private void RejectResolution(string diagnostic)
        {
            resolving = false;
            resolutionRejected = true;
            remainingResolutionAttempts = 0;
            UnsubscribeAll();
            ReportDiagnostic(
                NormalizeDiagnostic(
                    diagnostic,
                    "first-combat-room-integration-resolution-rejected"));
        }

        private void ReportDiagnostic(string diagnostic)
        {
            string normalized = NormalizeDiagnostic(
                diagnostic,
                "first-combat-room-integration-rejected");
            if (string.Equals(
                normalized,
                lastDiagnostic,
                StringComparison.Ordinal))
            {
                return;
            }

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

            foreach (KeyValuePair<EnemyAttack2D, Action<EnemyHitV1>> pair
                in enemyHitSubscriptions)
            {
                if (pair.Key != null)
                {
                    pair.Key.Hit -= pair.Value;
                }
            }
            enemyHitSubscriptions.Clear();

            roomRuntime = null;
            playerMarker = null;
            receiverBehaviour = null;
            playerDamageReceiver = null;
            playerWeaponController = null;
            playerProjectileSink = null;
        }

        private static List<T> FindActiveComponents<T>(Scene scene)
            where T : Component
        {
            var result = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                T[] candidates = roots[rootIndex]
                    .GetComponentsInChildren<T>(true);
                for (int index = 0; index < candidates.Length; index++)
                {
                    T candidate = candidates[index];
                    if (candidate == null
                        || candidate.gameObject.scene != scene
                        || !candidate.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    result.Add(candidate);
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
                MonoBehaviour[] behaviours = roots[rootIndex]
                    .GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < behaviours.Length; index++)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null
                        || behaviour.gameObject.scene != scene
                        || !behaviour.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    IPlayablePlayerDamageReceiverV1 receiver =
                        behaviour as IPlayablePlayerDamageReceiverV1;
                    if (receiver != null)
                    {
                        result.Add(new ReceiverBinding(behaviour, receiver));
                    }
                }
            }
            return result;
        }

        private static bool IsFinitePositive(double value)
        {
            return value > 0d
                && !double.IsNaN(value)
                && !double.IsInfinity(value);
        }

        private static string NormalizeDiagnostic(
            string diagnostic,
            string fallback)
        {
            return string.IsNullOrWhiteSpace(diagnostic)
                ? fallback
                : diagnostic.Trim();
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }

        private void OnDisable()
        {
            if (!destroying)
            {
                resolving = false;
                UnsubscribeAll();
            }
        }

        private void OnDestroy()
        {
            destroying = true;
            resolving = false;
            UnsubscribeAll();
        }

        private enum ResolutionStatus
        {
            Pending = 1,
            Ready = 2,
            Rejected = 3,
        }

        private sealed class ReceiverBinding
        {
            public ReceiverBinding(
                MonoBehaviour behaviour,
                IPlayablePlayerDamageReceiverV1 receiver)
            {
                Behaviour = behaviour;
                Receiver = receiver;
            }

            public MonoBehaviour Behaviour { get; }
            public IPlayablePlayerDamageReceiverV1 Receiver { get; }
        }
    }
}
