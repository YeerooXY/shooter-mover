using System;
using System.Collections;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Neutral gameplay-facing player damage boundary. The retained PlayerActorAuthority
    /// remains the sole owner of health, replay admission and the alive/dead lifecycle.
    /// </summary>
    public interface IPlayablePlayerDamageReceiverV1 : IDamageReceiver
    {
        StableId CharacterInstanceStableId { get; }
        long LifecycleGeneration { get; }
        double CurrentHealth { get; }
        double MaximumHealth { get; }
        bool IsDefeated { get; }
        event Action<PlayablePlayerDefeatedFactV1> Defeated;
        PlayerActorSnapshot ExportSnapshot();
    }

    /// <summary>
    /// Neutral integration helper for contacts that identify the selected character rather
    /// than the scene-local gameplay actor. The character identity is validated first, then
    /// the current receiver actor identity and lifecycle generation are projected into the
    /// canonical damage command.
    /// </summary>
    public static class PlayablePlayerDamageCommandFactoryV1
    {
        public static bool TryCreateForCharacterContact(
            IPlayablePlayerDamageReceiverV1 receiver,
            StableId contactedCharacterInstanceStableId,
            StableId eventStableId,
            StableId sourceActorStableId,
            StableId sourceRunParticipantStableId,
            double amount,
            CombatChannel channel,
            out DamageReceiverCommand command,
            out string rejectionCode)
        {
            command = null;
            rejectionCode = string.Empty;
            if (receiver == null)
            {
                rejectionCode = "playable-player-damage-receiver-missing";
                return false;
            }
            if (contactedCharacterInstanceStableId == null
                || receiver.CharacterInstanceStableId == null
                || contactedCharacterInstanceStableId
                    != receiver.CharacterInstanceStableId)
            {
                rejectionCode =
                    "playable-player-damage-character-target-mismatch";
                return false;
            }

            command = new DamageReceiverCommand(
                eventStableId,
                sourceActorStableId,
                sourceRunParticipantStableId,
                receiver.Identity.EntityInstanceId,
                amount,
                channel,
                receiver.LifecycleGeneration);
            return true;
        }
    }

    /// <summary>
    /// One retryable request seam for the existing production Hub transition. A false result
    /// never means that a transition was accepted.
    /// </summary>
    public interface IPlayablePlayerHubReturnRequestV1
    {
        bool TryReturnToHub(
            PlayablePlayerMarker2D player,
            out string rejectionCode);
    }

    /// <summary>
    /// Pure guard for the exact selected-character authority handoff. It observes identities
    /// and references only and cannot mutate character, holdings, or loadout state.
    /// </summary>
    public static class PlayablePlayerHubReturnAuthorityGuardV1
    {
        public static bool TryValidate(
            PlayablePlayerMarker2D player,
            StableId currentCharacterInstanceStableId,
            StableId currentClassDefinitionStableId,
            PlayerRouteProfilePayloadV1 graphRoutePayload,
            PlayerRouteProfilePayloadV1 profileRoutePayload,
            object currentHoldingsAuthority,
            object currentLoadoutAuthority,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (player == null
                || currentCharacterInstanceStableId == null
                || currentClassDefinitionStableId == null
                || graphRoutePayload == null
                || profileRoutePayload == null
                || currentHoldingsAuthority == null
                || currentLoadoutAuthority == null)
            {
                rejectionCode =
                    "playable-player-vitals-character-context-missing";
                return false;
            }

            if (player.CharacterInstanceStableId
                    != currentCharacterInstanceStableId
                || player.ClassDefinitionStableId
                    != currentClassDefinitionStableId
                || player.RoutePayload == null
                || !graphRoutePayload.Equals(player.RoutePayload)
                || !profileRoutePayload.Equals(player.RoutePayload)
                || !ReferenceEquals(
                    player.HoldingsAuthority,
                    currentHoldingsAuthority)
                || !ReferenceEquals(
                    player.LoadoutAuthority,
                    currentLoadoutAuthority))
            {
                rejectionCode =
                    "playable-player-vitals-character-authority-changed";
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Production adapter that resolves the current selected graph, validates exact authority
    /// continuity, and delegates to the retained production transition coordinator.
    /// </summary>
    public sealed class ProductionPlayablePlayerHubReturnRequestV1 :
        IPlayablePlayerHubReturnRequestV1
    {
        public bool TryReturnToHub(
            PlayablePlayerMarker2D player,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 profile;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out graph,
                    out profile)
                || graph == null
                || profile == null
                || graph.IsDisposed)
            {
                rejectionCode =
                    "playable-player-vitals-character-context-missing";
                return false;
            }

            if (!PlayablePlayerHubReturnAuthorityGuardV1.TryValidate(
                    player,
                    graph.Character.CharacterInstanceStableId,
                    graph.Character.ClassDefinitionStableId,
                    graph.RoutePayload,
                    profile.Payload,
                    graph.LoadoutRuntime.Holdings,
                    graph.LoadoutRuntime.LoadoutAuthority,
                    out rejectionCode))
            {
                return false;
            }

            ProductionFlowCoordinatorV1 flow =
                UnityEngine.Object.FindFirstObjectByType<
                    ProductionFlowCoordinatorV1>(
                    FindObjectsInactive.Include);
            if (flow == null
                || flow.Transitions == null
                || !flow.Transitions.TryReturnToHub(player.RoutePayload))
            {
                rejectionCode =
                    "playable-player-vitals-hub-return-rejected";
                return false;
            }

            return true;
        }
    }

    public sealed class PlayablePlayerDefeatedFactV1
    {
        public PlayablePlayerDefeatedFactV1(
            StableId actorInstanceStableId,
            StableId characterInstanceStableId,
            StableId lethalEventStableId,
            long lifecycleGeneration,
            long acceptedSequence)
        {
            ActorInstanceStableId = actorInstanceStableId
                ?? throw new ArgumentNullException(nameof(actorInstanceStableId));
            CharacterInstanceStableId = characterInstanceStableId
                ?? throw new ArgumentNullException(nameof(characterInstanceStableId));
            LethalEventStableId = lethalEventStableId
                ?? throw new ArgumentNullException(nameof(lethalEventStableId));
            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            if (acceptedSequence < 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(acceptedSequence));
            }

            LifecycleGeneration = lifecycleGeneration;
            AcceptedSequence = acceptedSequence;
        }

        public StableId ActorInstanceStableId { get; }
        public StableId CharacterInstanceStableId { get; }
        public StableId LethalEventStableId { get; }
        public long LifecycleGeneration { get; }
        public long AcceptedSequence { get; }
    }

    /// <summary>
    /// Adds the run-local player-vitals binding after the production level controller has
    /// spawned its exact selected-character presentation. The installer never polls globally.
    /// </summary>
    [DefaultExecutionOrder(550)]
    [DisallowMultipleComponent]
    public sealed class PlayablePlayerVitalsInstallerV1 : MonoBehaviour
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

            ProductionPlayableLevelControllerV1 controller = FindController(scene);
            if (controller == null)
            {
                Debug.LogError("playable-player-vitals-controller-missing");
                return;
            }

            if (controller.GetComponent<PlayablePlayerVitalsInstallerV1>() == null)
            {
                controller.gameObject.AddComponent<PlayablePlayerVitalsInstallerV1>();
            }
        }

        private static ProductionPlayableLevelControllerV1 FindController(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                ProductionPlayableLevelControllerV1 controller = roots[index]
                    .GetComponentInChildren<ProductionPlayableLevelControllerV1>(true);
                if (controller != null)
                {
                    return controller;
                }
            }
            return null;
        }

        private void Start()
        {
            PlayablePlayerMarker2D marker = GetComponentInChildren<
                PlayablePlayerMarker2D>(true);
            if (marker == null)
            {
                Debug.LogError("playable-player-vitals-player-missing", this);
                return;
            }

            Rigidbody2D body = marker.GetComponent<Rigidbody2D>();
            PlayableTopDownMovement2D movement = marker.GetComponent<
                PlayableTopDownMovement2D>();
            if (body == null || movement == null)
            {
                Debug.LogError("playable-player-vitals-movement-binding-missing", marker);
                return;
            }

            PlayablePlayerVitals2D vitals = marker.GetComponent<
                PlayablePlayerVitals2D>();
            if (vitals == null)
            {
                vitals = marker.gameObject.AddComponent<PlayablePlayerVitals2D>();
            }
            if (!vitals.IsBound)
            {
                vitals.Bind(marker, body, movement);
            }
        }
    }

    /// <summary>
    /// Scene-local presentation and flow adapter around PlayerActorAuthority. Persistent
    /// character holdings, loadout, money and progression are observed only for identity
    /// verification and are never mutated here.
    /// </summary>
    [DefaultExecutionOrder(600)]
    [DisallowMultipleComponent]
    public sealed class PlayablePlayerVitals2D :
        MonoBehaviour,
        IPlayablePlayerDamageReceiverV1
    {
        public const double ProvisionalMaximumHealth = 100d;
        private const float HubReturnRetrySeconds = 0.25f;

        private static readonly StableId PlayerFactionStableId =
            StableId.Parse("faction.players");

        private PlayablePlayerMarker2D marker;
        private Rigidbody2D body;
        private PlayableTopDownMovement2D movement;
        private PlayerActorAuthority authority;
        private IPlayablePlayerHubReturnRequestV1 hubReturnRequest;
        private SpriteRenderer playerRenderer;
        private Color playerBaseColor;
        private Coroutine hitFlash;
        private bool defeatedRaised;
        private bool hubReturnAccepted;
        private bool hubReturnAttemptInProgress;
        private float nextHubReturnAttemptAt;
        private int hubReturnAttemptCount;
        private string diagnostic = string.Empty;

        public event Action<PlayablePlayerDefeatedFactV1> Defeated;

        public bool IsBound { get { return authority != null; } }
        public bool UsesProvisionalMaximumHealth { get { return true; } }
        public bool IsHubReturnAccepted { get { return hubReturnAccepted; } }
        public int HubReturnAttemptCount { get { return hubReturnAttemptCount; } }
        public string Diagnostic { get { return diagnostic; } }

        public GameplayEntityIdentity Identity
        {
            get
            {
                EnsureBound();
                return authority.Identity;
            }
        }

        public StableId CharacterInstanceStableId
        {
            get { return marker == null ? null : marker.CharacterInstanceStableId; }
        }

        public long LifecycleGeneration
        {
            get { return ExportSnapshot().LifecycleGeneration; }
        }

        public double CurrentHealth
        {
            get { return ExportSnapshot().CurrentHealth; }
        }

        public double MaximumHealth
        {
            get { return ExportSnapshot().MaximumHealth; }
        }

        public bool IsDefeated
        {
            get { return IsBound && authority.ExportSnapshot().IsDead; }
        }

        public void Bind(
            PlayablePlayerMarker2D configuredMarker,
            Rigidbody2D configuredBody,
            PlayableTopDownMovement2D configuredMovement)
        {
            Bind(
                configuredMarker,
                configuredBody,
                configuredMovement,
                new ProductionPlayablePlayerHubReturnRequestV1());
        }

        public void Bind(
            PlayablePlayerMarker2D configuredMarker,
            Rigidbody2D configuredBody,
            PlayableTopDownMovement2D configuredMovement,
            IPlayablePlayerHubReturnRequestV1 configuredHubReturnRequest)
        {
            if (IsBound)
            {
                throw new InvalidOperationException(
                    "playable-player-vitals-duplicate-binding");
            }

            marker = configuredMarker
                ?? throw new ArgumentNullException(nameof(configuredMarker));
            body = configuredBody
                ?? throw new ArgumentNullException(nameof(configuredBody));
            movement = configuredMovement
                ?? throw new ArgumentNullException(nameof(configuredMovement));
            hubReturnRequest = configuredHubReturnRequest
                ?? throw new ArgumentNullException(
                    nameof(configuredHubReturnRequest));
            if (marker.CharacterInstanceStableId == null
                || marker.ClassDefinitionStableId == null
                || marker.RoutePayload == null
                || marker.HoldingsAuthority == null
                || marker.LoadoutAuthority == null)
            {
                throw new InvalidOperationException(
                    "playable-player-vitals-player-context-incomplete");
            }

            string runEntryToken = Guid.NewGuid().ToString("N");
            StableId actorStableId = StableId.Create(
                "actor",
                "playable-level-"
                + marker.CharacterInstanceStableId.Value
                + "-"
                + runEntryToken);
            StableId participantStableId = StableId.Create(
                "participant",
                "playable-level-"
                + marker.CharacterInstanceStableId.Value
                + "-"
                + runEntryToken);
            PlayerActorCreationResult creation = PlayerActorAuthority.TryCreate(
                new PlayerActorDefinition(
                    actorStableId,
                    participantStableId,
                    marker.CharacterInstanceStableId,
                    PlayerFactionStableId,
                    ProvisionalMaximumHealth,
                    0L));
            if (creation == null || !creation.IsCreated || creation.Authority == null)
            {
                throw new InvalidOperationException(
                    "playable-player-vitals-authority-rejected:"
                    + (creation == null
                        ? "result-missing"
                        : creation.RejectionCode.ToString()));
            }

            authority = creation.Authority;
            playerRenderer = marker.GetComponentInChildren<SpriteRenderer>(true);
            if (playerRenderer != null)
            {
                playerBaseColor = playerRenderer.color;
            }
            diagnostic = string.Empty;
        }

        public PlayerActorSnapshot ExportSnapshot()
        {
            EnsureBound();
            return authority.ExportSnapshot();
        }

        public DamageReceiverResult ApplyDamage(DamageReceiverCommand command)
        {
            EnsureBound();
            DamageReceiverResult result = authority.ApplyDamage(command);
            if (result != null && result.Status == DamageReceiverStatus.Applied)
            {
                ShowAcceptedHitFeedback();
                if (result.DeathFact != null)
                {
                    AcceptDefeat(result.DeathFact);
                }
            }
            return result;
        }

        /// <summary>
        /// Immediate retry seam for deterministic validation and explicit recovery callers.
        /// The accepted-transition latch is set only after the production transition accepts.
        /// </summary>
        public bool TryRetryHubReturn()
        {
            EnsureBound();
            if (!defeatedRaised)
            {
                return false;
            }
            return TryRequestHubReturn();
        }

        private void Update()
        {
            if (!defeatedRaised
                || hubReturnAccepted
                || hubReturnAttemptInProgress
                || Time.unscaledTime < nextHubReturnAttemptAt)
            {
                return;
            }

            TryRequestHubReturn();
        }

        private void AcceptDefeat(GameplayEntityDeathFact deathFact)
        {
            if (defeatedRaised || deathFact == null)
            {
                return;
            }
            defeatedRaised = true;

            movement.enabled = false;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;

            var fact = new PlayablePlayerDefeatedFactV1(
                authority.Identity.EntityInstanceId,
                marker.CharacterInstanceStableId,
                deathFact.EventId,
                deathFact.LifecycleGeneration,
                deathFact.AcceptedSequence);
            PublishDefeated(fact);
            TryRequestHubReturn();
        }

        private void PublishDefeated(PlayablePlayerDefeatedFactV1 fact)
        {
            Action<PlayablePlayerDefeatedFactV1> handlers = Defeated;
            if (handlers == null)
            {
                return;
            }

            Delegate[] observers = handlers.GetInvocationList();
            for (int index = 0; index < observers.Length; index++)
            {
                var observer =
                    (Action<PlayablePlayerDefeatedFactV1>)observers[index];
                try
                {
                    observer(fact);
                }
                catch (Exception exception)
                {
                    if (IsFatal(exception))
                    {
                        throw;
                    }
                    Debug.LogException(exception, this);
                }
            }
        }

        private bool TryRequestHubReturn()
        {
            if (hubReturnAccepted)
            {
                return true;
            }
            if (hubReturnAttemptInProgress)
            {
                return false;
            }

            hubReturnAttemptInProgress = true;
            try
            {
                hubReturnAttemptCount = checked(hubReturnAttemptCount + 1);
                string rejectionCode;
                bool accepted;
                try
                {
                    accepted = hubReturnRequest.TryReturnToHub(
                        marker,
                        out rejectionCode);
                }
                catch (Exception exception)
                {
                    if (IsFatal(exception))
                    {
                        throw;
                    }

                    Debug.LogException(exception, this);
                    RejectHubReturn(
                        "playable-player-vitals-hub-return-exception");
                    return false;
                }

                if (!accepted)
                {
                    RejectHubReturn(rejectionCode);
                    return false;
                }

                hubReturnAccepted = true;
                diagnostic = string.Empty;
                return true;
            }
            finally
            {
                hubReturnAttemptInProgress = false;
            }
        }

        private void RejectHubReturn(string code)
        {
            string normalized = string.IsNullOrWhiteSpace(code)
                ? "playable-player-vitals-hub-return-rejected"
                : code.Trim();
            if (!string.Equals(
                    diagnostic,
                    normalized,
                    StringComparison.Ordinal))
            {
                Debug.LogError(normalized, this);
            }

            diagnostic = normalized;
            nextHubReturnAttemptAt = Application.isPlaying
                ? Time.unscaledTime + HubReturnRetrySeconds
                : 0f;
        }

        private void ShowAcceptedHitFeedback()
        {
            if (playerRenderer == null)
            {
                return;
            }
            if (!Application.isPlaying)
            {
                playerRenderer.color = playerBaseColor;
                return;
            }
            if (hitFlash != null)
            {
                StopCoroutine(hitFlash);
            }
            hitFlash = StartCoroutine(FlashHit());
        }

        private IEnumerator FlashHit()
        {
            playerRenderer.color = Color.white;
            yield return new WaitForSecondsRealtime(0.12f);
            if (playerRenderer != null)
            {
                playerRenderer.color = playerBaseColor;
            }
            hitFlash = null;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !IsBound)
            {
                return;
            }

            PlayerActorSnapshot snapshot = authority.ExportSnapshot();
            float normalized = snapshot.MaximumHealth <= 0d
                ? 0f
                : Mathf.Clamp01((float)(snapshot.CurrentHealth / snapshot.MaximumHealth));
            const float x = 24f;
            const float y = 24f;
            const float width = 260f;
            const float height = 26f;

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = snapshot.IsDead
                ? new Color(0.55f, 0.08f, 0.08f, 1f)
                : new Color(0.15f, 0.75f, 0.3f, 1f);
            GUI.DrawTexture(
                new Rect(x + 3f, y + 3f, (width - 6f) * normalized, height - 6f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(x + 8f, y + 3f, width - 16f, height - 6f),
                "HEALTH  "
                + Math.Round(snapshot.CurrentHealth).ToString()
                + " / "
                + Math.Round(snapshot.MaximumHealth).ToString());
            GUI.color = previous;
        }

        private void EnsureBound()
        {
            if (!IsBound)
            {
                throw new InvalidOperationException(
                    "playable-player-vitals-not-bound");
            }
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }

        private void OnDestroy()
        {
            if (hitFlash != null)
            {
                StopCoroutine(hitFlash);
                hitFlash = null;
            }
            if (playerRenderer != null)
            {
                playerRenderer.color = playerBaseColor;
            }
            Defeated = null;
            hubReturnRequest = null;
        }
    }
}
