using System;
using System.Collections;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Contracts.Combat;
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
                    Content.Definitions.Levels.Selection
                        .ProductionPlayableLevelCatalogV1.PlayableLevelScenePath,
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

        private static readonly StableId PlayerFactionStableId =
            StableId.Parse("faction.players");

        private PlayablePlayerMarker2D marker;
        private Rigidbody2D body;
        private PlayableTopDownMovement2D movement;
        private PlayerActorAuthority authority;
        private SpriteRenderer playerRenderer;
        private Color playerBaseColor;
        private Coroutine hitFlash;
        private bool defeatedRaised;
        private bool hubReturnRequested;
        private string diagnostic = string.Empty;

        public event Action<PlayablePlayerDefeatedFactV1> Defeated;

        public bool IsBound { get { return authority != null; } }
        public bool UsesProvisionalMaximumHealth { get { return true; } }
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
            if (marker.CharacterInstanceStableId == null
                || marker.ClassDefinitionStableId == null
                || marker.RoutePayload == null
                || marker.HoldingsAuthority == null
                || marker.LoadoutAuthority == null)
            {
                throw new InvalidOperationException(
                    "playable-player-vitals-player-context-incomplete");
            }

            StableId actorStableId = StableId.Create(
                "actor",
                "playable-level-" + marker.CharacterInstanceStableId.Value);
            StableId participantStableId = StableId.Create(
                "participant",
                "playable-level-" + marker.CharacterInstanceStableId.Value);
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
            Action<PlayablePlayerDefeatedFactV1> handler = Defeated;
            if (handler != null)
            {
                try
                {
                    handler(fact);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            RequestHubReturnOnce();
        }

        private void RequestHubReturnOnce()
        {
            if (hubReturnRequested)
            {
                return;
            }
            hubReturnRequested = true;

            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 profile;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out graph,
                    out profile)
                || graph == null
                || profile == null
                || graph.IsDisposed)
            {
                RejectHubReturn("playable-player-vitals-character-context-missing");
                return;
            }
            if (graph.Character.CharacterInstanceStableId
                    != marker.CharacterInstanceStableId
                || graph.Character.ClassDefinitionStableId
                    != marker.ClassDefinitionStableId
                || !graph.RoutePayload.Equals(marker.RoutePayload)
                || !profile.Payload.Equals(marker.RoutePayload)
                || !ReferenceEquals(
                    marker.HoldingsAuthority,
                    graph.LoadoutRuntime.Holdings)
                || !ReferenceEquals(
                    marker.LoadoutAuthority,
                    graph.LoadoutRuntime.LoadoutAuthority))
            {
                RejectHubReturn("playable-player-vitals-character-authority-changed");
                return;
            }

            ProductionFlowCoordinatorV1 flow = FindFirstObjectByType<
                ProductionFlowCoordinatorV1>(FindObjectsInactive.Include);
            if (flow == null
                || flow.Transitions == null
                || !flow.Transitions.TryReturnToHub(marker.RoutePayload))
            {
                RejectHubReturn("playable-player-vitals-hub-return-rejected");
            }
        }

        private void RejectHubReturn(string code)
        {
            diagnostic = code;
            Debug.LogError(code, this);
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
        }
    }
}
