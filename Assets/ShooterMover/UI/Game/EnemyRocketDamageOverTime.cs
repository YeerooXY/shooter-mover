using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Content.Definitions.Enemies;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Game
{
    internal static class EnemyRocketDamageOverTimeInstaller
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

            GameObject[] roots = scene.GetRootGameObjects();
            LevelGame controller = null;
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].GetComponentInChildren<
                        EnemyRocketDamageOverTime>(true) != null)
                {
                    return;
                }

                LevelGame candidate = roots[index]
                    .GetComponentInChildren<LevelGame>(true);
                if (candidate == null) continue;
                if (controller != null && !ReferenceEquals(controller, candidate))
                {
                    Debug.LogError(
                        "enemy-rocket-dot-controller-duplicated",
                        candidate);
                    return;
                }
                controller = candidate;
            }

            if (controller != null)
            {
                controller.gameObject.AddComponent<EnemyRocketDamageOverTime>();
            }
        }
    }

    /// <summary>
    /// Applies the content-owned enemy rocket burn after the normal impact damage has already
    /// gone through EnemyPlayerDamageIntegration. Two deterministic one-damage thermal ticks
    /// are delivered over two gameplay seconds. Exact contact replay cannot schedule it twice.
    /// </summary>
    [DefaultExecutionOrder(710)]
    [DisallowMultipleComponent]
    public sealed class EnemyRocketDamageOverTime : MonoBehaviour
    {
        private const float ReconcileIntervalSeconds = 0.25f;
        private const int MaximumRememberedContacts = 2048;

        private readonly EnemyHitSubscriptionSet subscriptions =
            new EnemyHitSubscriptionSet();
        private readonly List<PendingTick> pending = new List<PendingTick>();
        private readonly HashSet<StableId> acceptedContacts =
            new HashSet<StableId>();

        private PlayerMarker playerMarker;
        private MonoBehaviour receiverBehaviour;
        private IPlayablePlayerDamageReceiver receiver;
        private float nextReconcileAt;
        private bool defeated;
        private bool destroying;
        private string lastDiagnostic = string.Empty;

        public int PendingTickCount { get { return pending.Count; } }
        public string LastDiagnostic { get { return lastDiagnostic; } }

        private void Start()
        {
            Reconcile();
        }

        private void Update()
        {
            if (destroying || defeated) return;

            if (!BindingsAreCurrent()
                || Time.unscaledTime >= nextReconcileAt)
            {
                Reconcile();
            }

            ProcessDueTicks(Time.timeAsDouble);
        }

        private void Reconcile()
        {
            nextReconcileAt = Time.unscaledTime + ReconcileIntervalSeconds;
            Scene scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            ReceiverBinding resolvedReceiver;
            PlayerMarker resolvedMarker;
            if (!TryResolvePlayer(scene, out resolvedMarker, out resolvedReceiver))
            {
                subscriptions.Clear();
                return;
            }

            if (!ReferenceEquals(receiver, resolvedReceiver.Receiver))
            {
                if (receiver != null)
                {
                    receiver.Defeated -= HandleDefeated;
                }
                playerMarker = resolvedMarker;
                receiverBehaviour = resolvedReceiver.Behaviour;
                receiver = resolvedReceiver.Receiver;
                receiver.Defeated += HandleDefeated;
                pending.Clear();
                acceptedContacts.Clear();
            }

            List<EnemyAttack> publishers = FindActiveComponents<EnemyAttack>(scene);
            var ready = new List<EnemyAttack>();
            for (int index = 0; index < publishers.Count; index++)
            {
                EnemyAttack publisher = publishers[index];
                if (publisher.IsBound && !publisher.IsTerminalStopped)
                {
                    ready.Add(publisher);
                }
            }

            try
            {
                subscriptions.Replace(ready, HandleEnemyHit);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                subscriptions.Clear();
                Report("enemy-rocket-dot-subscription-exception:"
                    + exception.GetType().Name);
                Debug.LogException(exception, this);
            }
        }

        private void HandleEnemyHit(EnemyAttack publisher, EnemyHit hit)
        {
            if (destroying
                || defeated
                || publisher == null
                || hit == null
                || !subscriptions.Contains(publisher)
                || !BuiltInEnemyProjectileProfiles.IsRocket(
                    hit.ProjectileProfileStableId))
            {
                return;
            }

            if (!BindingsAreCurrent()
                || hit.ContactStableId == null
                || hit.TargetEntityStableId == null
                || hit.SourceEntityStableId == null
                || hit.SourceRunParticipantStableId == null
                || hit.TargetEntityStableId != receiver.CharacterInstanceStableId)
            {
                Report("enemy-rocket-dot-hit-invalid");
                return;
            }

            if (!acceptedContacts.Add(hit.ContactStableId))
            {
                return;
            }
            if (acceptedContacts.Count > MaximumRememberedContacts)
            {
                acceptedContacts.Clear();
                acceptedContacts.Add(hit.ContactStableId);
            }

            int tickCount =
                BuiltInEnemyProjectileProfiles.RocketDamageOverTimeTickCount;
            double totalDamage =
                BuiltInEnemyProjectileProfiles.RocketDamageOverTimeTotalDamage;
            double duration =
                BuiltInEnemyProjectileProfiles.RocketDamageOverTimeDurationSeconds;
            double tickDamage = totalDamage / tickCount;
            double now = Time.timeAsDouble;

            for (int tick = 1; tick <= tickCount; tick++)
            {
                pending.Add(new PendingTick(
                    BuildTickEventId(hit.ContactStableId, tick),
                    hit.TargetEntityStableId,
                    hit.SourceEntityStableId,
                    hit.SourceRunParticipantStableId,
                    hit.DamageChannelStableId,
                    tickDamage,
                    now + (duration * tick / tickCount)));
            }
            pending.Sort(PendingTick.Compare);
        }

        private void ProcessDueTicks(double now)
        {
            while (pending.Count > 0 && pending[0].DueAtSeconds <= now)
            {
                PendingTick tick = pending[0];
                pending.RemoveAt(0);
                ApplyTick(tick);
                if (defeated) return;
            }
        }

        private void ApplyTick(PendingTick tick)
        {
            if (!BindingsAreCurrent())
            {
                pending.Clear();
                return;
            }

            CombatChannel channel;
            if (!EnemyPlayerDamageChannelMap.TryMap(
                    tick.DamageChannelStableId,
                    out channel))
            {
                Report("enemy-rocket-dot-channel-unknown");
                return;
            }

            DamageReceiverCommand command;
            string rejection;
            if (!PlayablePlayerDamageCommandFactory.TryCreateForCharacterContact(
                    receiver,
                    tick.TargetCharacterStableId,
                    tick.EventStableId,
                    tick.SourceEntityStableId,
                    tick.SourceRunParticipantStableId,
                    tick.Damage,
                    channel,
                    out command,
                    out rejection))
            {
                Report(string.IsNullOrWhiteSpace(rejection)
                    ? "enemy-rocket-dot-command-rejected"
                    : rejection);
                return;
            }

            DamageReceiverResult result = receiver.ApplyDamage(command);
            if (result == null)
            {
                Report("enemy-rocket-dot-result-missing");
            }
        }

        private bool BindingsAreCurrent()
        {
            return playerMarker != null
                && playerMarker.gameObject.scene == gameObject.scene
                && receiverBehaviour != null
                && receiverBehaviour.gameObject.scene == gameObject.scene
                && receiverBehaviour.isActiveAndEnabled
                && receiver != null
                && receiver.CharacterInstanceStableId
                    == playerMarker.CharacterInstanceStableId
                && subscriptions.AllCurrent(gameObject.scene);
        }

        private static bool TryResolvePlayer(
            Scene scene,
            out PlayerMarker marker,
            out ReceiverBinding receiverBinding)
        {
            marker = null;
            receiverBinding = default(ReceiverBinding);

            List<PlayerMarker> markers = FindActiveComponents<PlayerMarker>(scene);
            List<ReceiverBinding> receivers = FindActiveReceivers(scene);
            if (markers.Count != 1 || receivers.Count != 1)
            {
                return false;
            }

            if (markers[0].gameObject != receivers[0].Behaviour.gameObject
                || markers[0].CharacterInstanceStableId == null
                || receivers[0].Receiver.CharacterInstanceStableId
                    != markers[0].CharacterInstanceStableId)
            {
                return false;
            }

            marker = markers[0];
            receiverBinding = receivers[0];
            return true;
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
                    IPlayablePlayerDamageReceiver candidate =
                        value as IPlayablePlayerDamageReceiver;
                    if (value != null
                        && candidate != null
                        && value.gameObject.scene == scene
                        && value.isActiveAndEnabled)
                    {
                        result.Add(new ReceiverBinding(value, candidate));
                    }
                }
            }
            return result;
        }

        private static StableId BuildTickEventId(
            StableId contactStableId,
            int tickOrdinal)
        {
            string value = contactStableId.ToString()
                + "|rocket-dot|"
                + tickOrdinal.ToString(CultureInfo.InvariantCulture);
            ulong hash = 14695981039346656037UL;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                hash ^= (byte)(character & 0xff);
                hash *= 1099511628211UL;
                hash ^= (byte)(character >> 8);
                hash *= 1099511628211UL;
            }

            return StableId.Create(
                "event",
                "enemy-rocket-dot-"
                    + hash.ToString("x16", CultureInfo.InvariantCulture)
                    + "-"
                    + tickOrdinal.ToString(CultureInfo.InvariantCulture));
        }

        private void HandleDefeated(PlayablePlayerDefeatedFact fact)
        {
            defeated = true;
            pending.Clear();
            subscriptions.Clear();
        }

        private void Report(string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(diagnostic)
                || string.Equals(lastDiagnostic, diagnostic, StringComparison.Ordinal))
            {
                return;
            }
            lastDiagnostic = diagnostic;
            Debug.LogError(diagnostic, this);
        }

        private void OnDisable()
        {
            subscriptions.Clear();
            if (receiver != null)
            {
                receiver.Defeated -= HandleDefeated;
            }
        }

        private void OnDestroy()
        {
            destroying = true;
            subscriptions.Clear();
            if (receiver != null)
            {
                receiver.Defeated -= HandleDefeated;
            }
            pending.Clear();
            acceptedContacts.Clear();
            receiver = null;
            receiverBehaviour = null;
            playerMarker = null;
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }

        private readonly struct ReceiverBinding
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

        private sealed class PendingTick
        {
            public PendingTick(
                StableId eventStableId,
                StableId targetCharacterStableId,
                StableId sourceEntityStableId,
                StableId sourceRunParticipantStableId,
                StableId damageChannelStableId,
                double damage,
                double dueAtSeconds)
            {
                EventStableId = eventStableId;
                TargetCharacterStableId = targetCharacterStableId;
                SourceEntityStableId = sourceEntityStableId;
                SourceRunParticipantStableId = sourceRunParticipantStableId;
                DamageChannelStableId = damageChannelStableId;
                Damage = damage;
                DueAtSeconds = dueAtSeconds;
            }

            public StableId EventStableId { get; }
            public StableId TargetCharacterStableId { get; }
            public StableId SourceEntityStableId { get; }
            public StableId SourceRunParticipantStableId { get; }
            public StableId DamageChannelStableId { get; }
            public double Damage { get; }
            public double DueAtSeconds { get; }

            public static int Compare(PendingTick left, PendingTick right)
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left == null) return -1;
                if (right == null) return 1;
                int due = left.DueAtSeconds.CompareTo(right.DueAtSeconds);
                return due != 0
                    ? due
                    : string.CompareOrdinal(
                        left.EventStableId.ToString(),
                        right.EventStableId.ToString());
            }
        }
    }
}
