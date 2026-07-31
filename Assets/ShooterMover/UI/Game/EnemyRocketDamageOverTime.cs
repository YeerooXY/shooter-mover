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

            LevelGame controller = null;
            GameObject[] roots = scene.GetRootGameObjects();
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
                    Debug.LogError("enemy-rocket-dot-controller-duplicated", candidate);
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
    /// Adds the built-in rocket burn after the normal enemy-hit integration applies impact
    /// damage. Each accepted rocket contact schedules two one-damage thermal ticks at one and
    /// two seconds. Distinct rocket hits stack; exact contact replay does not duplicate ticks.
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

        private PlayerHUD receiver;
        private float nextReconcileAt;
        private bool defeated;
        private string lastDiagnostic = string.Empty;

        public int PendingTickCount { get { return pending.Count; } }
        public string LastDiagnostic { get { return lastDiagnostic; } }

        private void Start()
        {
            Reconcile();
        }

        private void Update()
        {
            if (defeated) return;

            if (!ReceiverIsCurrent()
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
            List<PlayerHUD> receivers = FindActive<PlayerHUD>(scene);
            if (receivers.Count != 1)
            {
                ClearReceiverAndEffects();
                subscriptions.Clear();
                return;
            }

            PlayerHUD nextReceiver = receivers[0];
            if (!ReferenceEquals(receiver, nextReceiver))
            {
                ClearReceiverAndEffects();
                receiver = nextReceiver;
                receiver.Defeated += HandleDefeated;
            }

            List<EnemyAttack> discovered = FindActive<EnemyAttack>(scene);
            var ready = new List<EnemyAttack>();
            for (int index = 0; index < discovered.Count; index++)
            {
                EnemyAttack publisher = discovered[index];
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
                Report(
                    "enemy-rocket-dot-subscription-exception:"
                    + exception.GetType().Name);
                Debug.LogException(exception, this);
            }
        }

        private void HandleEnemyHit(EnemyAttack publisher, EnemyHit hit)
        {
            if (defeated
                || publisher == null
                || hit == null
                || !subscriptions.Contains(publisher)
                || !BuiltInEnemyProjectileProfiles.IsRocket(
                    hit.ProjectileProfileStableId))
            {
                return;
            }

            if (!ReceiverIsCurrent()
                || hit.ContactStableId == null
                || hit.TargetEntityStableId != receiver.CharacterInstanceStableId
                || hit.SourceEntityStableId == null
                || hit.SourceRunParticipantStableId == null)
            {
                Report("enemy-rocket-dot-hit-invalid");
                return;
            }

            if (!acceptedContacts.Add(hit.ContactStableId)) return;
            if (acceptedContacts.Count > MaximumRememberedContacts)
            {
                acceptedContacts.Clear();
                acceptedContacts.Add(hit.ContactStableId);
            }

            int tickCount =
                BuiltInEnemyProjectileProfiles.RocketDamageOverTimeTickCount;
            double tickDamage =
                BuiltInEnemyProjectileProfiles.RocketDamageOverTimeTotalDamage
                / tickCount;
            double duration =
                BuiltInEnemyProjectileProfiles.RocketDamageOverTimeDurationSeconds;
            double now = Time.timeAsDouble;

            for (int ordinal = 1; ordinal <= tickCount; ordinal++)
            {
                pending.Add(new PendingTick(
                    BuildTickEventId(hit.ContactStableId, ordinal),
                    hit.TargetEntityStableId,
                    hit.SourceEntityStableId,
                    hit.SourceRunParticipantStableId,
                    hit.DamageChannelStableId,
                    tickDamage,
                    now + (duration * ordinal / tickCount)));
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
            if (!ReceiverIsCurrent())
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

        private bool ReceiverIsCurrent()
        {
            return receiver != null
                && receiver.gameObject.scene == gameObject.scene
                && receiver.isActiveAndEnabled
                && receiver.IsBound
                && !receiver.IsDefeated;
        }

        private void ClearReceiverAndEffects()
        {
            if (receiver != null)
            {
                receiver.Defeated -= HandleDefeated;
            }
            receiver = null;
            pending.Clear();
            acceptedContacts.Clear();
        }

        private static List<T> FindActive<T>(Scene scene) where T : MonoBehaviour
        {
            var result = new List<T>();
            if (!scene.IsValid() || !scene.isLoaded) return result;

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
            ClearReceiverAndEffects();
        }

        private void OnDestroy()
        {
            subscriptions.Clear();
            ClearReceiverAndEffects();
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
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
