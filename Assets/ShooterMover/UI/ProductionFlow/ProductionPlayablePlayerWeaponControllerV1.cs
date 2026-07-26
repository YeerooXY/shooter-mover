using System;
using System.Globalization;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    internal sealed class ProductionWeaponCanonicalBlueprintResolverV1 :
        IWeaponBlueprintMappingPolicyResolver,
        ICanonicalWeaponBlueprintResolver
    {
        public bool TryResolve(
            WeaponDefinitionId definitionId,
            out WeaponCatalogBlueprintMappingIntent mappingIntent)
        {
            mappingIntent = null;
            return false;
        }

        public bool TryResolveCanonical(
            WeaponDefinitionId definitionId,
            out WeaponBlueprint blueprint)
        {
            blueprint = null;
            return definitionId != null
                && ProductionWeaponCatalogueV1.Current.TryGetBlueprint(
                    definitionId.Value,
                    out blueprint)
                && blueprint != null
                && !blueprint.IsTransitionalCatalogProjection
                && blueprint.DefinitionId.Equals(definitionId);
        }
    }

    internal sealed class ProductionPlayableWeaponActorStateV1 :
        IInventoryWeaponActorStateSource,
        IWeaponActorOwnershipResolver
    {
        private readonly WeaponActorInstanceId actorId;
        private readonly LifecycleGeneration lifecycleGeneration;
        private readonly RunParticipantId participantId;
        private bool active = true;

        public ProductionPlayableWeaponActorStateV1(
            StableId characterInstanceStableId,
            string routeFingerprint)
        {
            actorId = new WeaponActorInstanceId(
                characterInstanceStableId
                ?? throw new ArgumentNullException(
                    nameof(characterInstanceStableId)));
            lifecycleGeneration = new LifecycleGeneration(1L);
            participantId = new RunParticipantId(
                StableId.Create(
                    "run-participant",
                    "playable-player-"
                    + Hash64(
                        characterInstanceStableId
                        + "|" + (routeFingerprint ?? string.Empty))));
        }

        public WeaponActorInstanceId ActorId { get { return actorId; } }
        public LifecycleGeneration LifecycleGeneration
        {
            get { return lifecycleGeneration; }
        }

        public bool TryResolveActorState(
            out WeaponActorInstanceId resolvedActorId,
            out LifecycleGeneration resolvedLifecycleGeneration)
        {
            resolvedActorId = active ? actorId : null;
            resolvedLifecycleGeneration =
                active ? lifecycleGeneration : null;
            return active;
        }

        public bool TryResolveParticipant(
            WeaponActorInstanceId requestedActorId,
            LifecycleGeneration requestedLifecycleGeneration,
            out RunParticipantId resolvedParticipantId)
        {
            bool matches = active
                && requestedActorId != null
                && requestedLifecycleGeneration != null
                && actorId.Equals(requestedActorId)
                && lifecycleGeneration.Equals(
                    requestedLifecycleGeneration);
            resolvedParticipantId = matches ? participantId : null;
            return matches;
        }

        public void Deactivate()
        {
            active = false;
        }

        private static string Hash64(string value)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            string text = value ?? string.Empty;
            for (int index = 0; index < text.Length; index++)
            {
                hash ^= text[index];
                hash *= prime;
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Additive scene hook. The production level controller remains the player/scene authority;
    /// this hook waits for its exact player marker, binds once, then destroys itself.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class ProductionPlayablePlayerWeaponBootstrapV1 :
        MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryInstall(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
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
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].GetComponentInChildren<
                        ProductionPlayablePlayerWeaponBootstrapV1>(true)
                    != null)
                {
                    return;
                }
            }

            GameObject root = new GameObject(
                "Production Playable Player Weapon Bootstrap");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<
                ProductionPlayablePlayerWeaponBootstrapV1>();
        }

        private void Update()
        {
            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 profile;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out graph,
                    out profile)
                || graph == null
                || profile == null
                || graph.IsDisposed)
            {
                return;
            }

            PlayablePlayerMarker2D marker = FindPlayerMarker();
            Camera gameplayCamera = FindGameplayCamera();
            if (marker == null || gameplayCamera == null) return;
            if (marker.CharacterInstanceStableId
                    != graph.Character.CharacterInstanceStableId
                || marker.ClassDefinitionStableId
                    != graph.Character.ClassDefinitionStableId
                || marker.RoutePayload == null
                || !marker.RoutePayload.Equals(graph.RoutePayload)
                || !ReferenceEquals(
                    marker.HoldingsAuthority,
                    graph.LoadoutRuntime.Holdings)
                || !ReferenceEquals(
                    marker.LoadoutAuthority,
                    graph.LoadoutRuntime.LoadoutAuthority))
            {
                Debug.LogError(
                    "player-weapon-live-character-authority-mismatch",
                    this);
                enabled = false;
                return;
            }

            ProductionPlayablePlayerWeaponControllerV1 controller =
                marker.GetComponent<
                    ProductionPlayablePlayerWeaponControllerV1>()
                ?? marker.gameObject.AddComponent<
                    ProductionPlayablePlayerWeaponControllerV1>();
            if (!controller.TryBind(graph, marker, gameplayCamera))
            {
                Debug.LogError(
                    "player-weapon-live-binding-rejected",
                    controller);
                enabled = false;
                return;
            }

            Destroy(gameObject);
        }

        private PlayablePlayerMarker2D FindPlayerMarker()
        {
            PlayablePlayerMarker2D found = null;
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                PlayablePlayerMarker2D[] markers = roots[index]
                    .GetComponentsInChildren<PlayablePlayerMarker2D>(
                        true);
                for (int markerIndex = 0;
                    markerIndex < markers.Length;
                    markerIndex++)
                {
                    if (!markers[markerIndex].gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    if (found != null && !ReferenceEquals(
                            found,
                            markers[markerIndex]))
                    {
                        Debug.LogError(
                            "player-weapon-live-player-marker-duplicated",
                            this);
                        enabled = false;
                        return null;
                    }
                    found = markers[markerIndex];
                }
            }
            return found;
        }

        private Camera FindGameplayCamera()
        {
            Camera found = null;
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                Camera[] cameras = roots[index]
                    .GetComponentsInChildren<Camera>(true);
                for (int cameraIndex = 0;
                    cameraIndex < cameras.Length;
                    cameraIndex++)
                {
                    Camera candidate = cameras[cameraIndex];
                    if (candidate == null
                        || !candidate.enabled
                        || !candidate.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    if (found != null
                        && !ReferenceEquals(found, candidate))
                    {
                        Debug.LogError(
                            "player-weapon-live-gameplay-camera-duplicated",
                            this);
                        enabled = false;
                        return null;
                    }
                    found = candidate;
                }
            }
            return found;
        }
    }

    [DefaultExecutionOrder(650)]
    [DisallowMultipleComponent]
    public sealed class ProductionPlayablePlayerWeaponControllerV1 :
        MonoBehaviour
    {
        private ProductionCharacterRuntimeGraphV1 graph;
        private PlayablePlayerMarker2D marker;
        private Camera gameplayCamera;
        private ProductionPlayableWeaponActorStateV1 actorState;
        private InventoryWeaponRuntimeComposition runtime;
        private ProductionNormalProjectileEffectSink2D effectSink;
        private ProductionWeaponMountPositionV1 mountPosition;
        private Sprite muzzleSprite;
        private Texture2D muzzleTexture;
        private SpriteRenderer muzzleRenderer;
        private Vector2 aimDirection = Vector2.right;
        private bool triggerHeld;
        private bool bound;
        private long simulationTick;
        private string lastDiagnostic = string.Empty;

        public bool IsBound { get { return bound; } }
        public StableId EquipmentInstanceStableId { get; private set; }
        public StableId MountStableId
        {
            get { return mountPosition == null ? null : mountPosition.MountStableId; }
        }

        public bool TryBind(
            ProductionCharacterRuntimeGraphV1 configuredGraph,
            PlayablePlayerMarker2D configuredMarker,
            Camera configuredCamera)
        {
            if (bound)
            {
                return ReferenceEquals(graph, configuredGraph)
                    && ReferenceEquals(marker, configuredMarker)
                    && ReferenceEquals(gameplayCamera, configuredCamera);
            }
            if (configuredGraph == null
                || configuredGraph.IsDisposed
                || configuredMarker == null
                || configuredCamera == null
                || configuredMarker.CharacterInstanceStableId
                    != configuredGraph.Character.CharacterInstanceStableId
                || !ReferenceEquals(
                    configuredMarker.HoldingsAuthority,
                    configuredGraph.LoadoutRuntime.Holdings)
                || !ReferenceEquals(
                    configuredMarker.LoadoutAuthority,
                    configuredGraph.LoadoutRuntime.LoadoutAuthority))
            {
                return false;
            }

            try
            {
                ProductionWeaponMountSetV1 mountSet =
                    ProductionWeaponMountPolicyV1.BuildMountSet(
                        configuredGraph.RoutePayload);
                if (mountSet == null
                    || mountSet.EnabledBindings.Count < 1)
                {
                    throw new InvalidOperationException(
                        "player-weapon-live-enabled-mount-missing");
                }

                ProductionWeaponMountBindingV1 binding =
                    mountSet.EnabledBindings[0];
                mountPosition = ProductionWeaponMountPolicyV1.FindPosition(
                    mountSet.Layout,
                    binding.MountStableId);
                if (mountPosition == null)
                {
                    throw new InvalidOperationException(
                        "player-weapon-live-mount-position-missing");
                }

                graph = configuredGraph;
                marker = configuredMarker;
                gameplayCamera = configuredCamera;
                EquipmentInstanceStableId =
                    binding.EquipmentInstanceStableId;
                actorState = new ProductionPlayableWeaponActorStateV1(
                    marker.CharacterInstanceStableId,
                    graph.RoutePayload.Fingerprint);
                effectSink = GetComponent<
                    ProductionNormalProjectileEffectSink2D>()
                    ?? gameObject.AddComponent<
                        ProductionNormalProjectileEffectSink2D>();

                int ticksPerSecond = Math.Max(
                    1,
                    Mathf.RoundToInt(1f / Time.fixedDeltaTime));
                var adapter = new InventoryBackedWeaponExecutionAdapter(
                    graph.LoadoutRuntime.Holdings,
                    graph.LoadoutRuntime.EquipmentCatalog,
                    graph.LoadoutRuntime.WeaponCatalog,
                    actorState,
                    effectSink,
                    ticksPerSecond,
                    new ProductionWeaponCanonicalBlueprintResolverV1(),
                    new UnaugmentedWeaponModifierSetResolver());
                runtime = new InventoryWeaponRuntimeComposition(
                    actorState,
                    new[]
                    {
                        new InventoryWeaponMountedRuntimeV1(
                            binding.MountStableId,
                            new EquipmentInstanceId(
                                binding.EquipmentInstanceStableId),
                            mountPosition.LateralOffset),
                    },
                    adapter);
                CreateMuzzle();
                bound = true;
                return true;
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                Debug.LogError(
                    "player-weapon-live-composition-rejected:"
                    + exception.Message,
                    this);
                DisposeRuntime();
                return false;
            }
        }

        private void Update()
        {
            if (!bound || gameplayCamera == null)
            {
                triggerHeld = false;
                return;
            }

            Mouse mouse = Mouse.current;
            triggerHeld = mouse != null
                && mouse.leftButton.isPressed;
            if (mouse != null)
            {
                Vector3 screen = mouse.position.ReadValue();
                screen.z = Mathf.Abs(
                    gameplayCamera.transform.position.z
                    - transform.position.z);
                Vector3 world = gameplayCamera.ScreenToWorldPoint(screen);
                Vector2 candidate = new Vector2(
                    world.x - transform.position.x,
                    world.y - transform.position.y);
                if (candidate.sqrMagnitude > 0.000001f)
                {
                    aimDirection = candidate.normalized;
                }
            }

            UpdateMuzzle();
        }

        private void FixedUpdate()
        {
            if (!bound || runtime == null) return;
            simulationTick = checked(simulationTick + 1L);
            Vector2 origin = ResolveMuzzleOrigin();
            var operationId = new FireOperationId(
                StableId.Create(
                    "fire-operation",
                    "player-input-"
                    + Hash64(
                        actorState.ActorId
                        + "|" + actorState.LifecycleGeneration
                        + "|" + simulationTick.ToString(
                            CultureInfo.InvariantCulture))));
            var weaponOrigin = new WeaponVector2(origin.x, origin.y);
            var weaponAim = new WeaponVector2(
                aimDirection.x,
                aimDirection.y);
            ulong seed = SeedFor(simulationTick);

            InventoryWeaponExecutionResult inputResult =
                runtime.UpdateTriggerInput(
                    triggerHeld,
                    operationId,
                    simulationTick,
                    seed,
                    weaponOrigin,
                    weaponAim);
            ReportFailure(inputResult);
            InventoryWeaponExecutionResult advanceResult =
                runtime.Advance(simulationTick);
            ReportFailure(advanceResult);
        }

        private void ReportFailure(
            InventoryWeaponExecutionResult result)
        {
            if (result == null
                || string.IsNullOrWhiteSpace(result.RejectionCode)
                || string.Equals(
                    result.RejectionCode,
                    lastDiagnostic,
                    StringComparison.Ordinal))
            {
                return;
            }

            lastDiagnostic = result.RejectionCode;
            Debug.LogError(
                "player-weapon-live-runtime-rejected:"
                + result.RejectionCode,
                this);
        }

        private void CreateMuzzle()
        {
            muzzleTexture = new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false);
            muzzleTexture.name = "Player Weapon Muzzle Pixel";
            muzzleTexture.SetPixel(0, 0, Color.white);
            muzzleTexture.Apply(false, true);
            muzzleSprite = Sprite.Create(
                muzzleTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            muzzleSprite.name = "Player Weapon Muzzle Sprite";

            GameObject muzzle = new GameObject(
                "Player Weapon Muzzle "
                + mountPosition.MountStableId);
            muzzle.transform.SetParent(transform, false);
            muzzleRenderer = muzzle.AddComponent<SpriteRenderer>();
            muzzleRenderer.sprite = muzzleSprite;
            muzzleRenderer.color = new Color(1f, 0.75f, 0.15f, 1f);
            muzzleRenderer.sortingOrder = 99;
            muzzleRenderer.transform.localScale =
                new Vector3(0.22f, 0.12f, 1f);
            UpdateMuzzle();
        }

        private Vector2 ResolveMuzzleOrigin()
        {
            Vector2 perpendicular = new Vector2(
                -aimDirection.y,
                aimDirection.x);
            return (Vector2)transform.position
                + (aimDirection * 0.55f)
                + (perpendicular
                    * (float)mountPosition.LateralOffset);
        }

        private void UpdateMuzzle()
        {
            if (muzzleRenderer == null || mountPosition == null) return;
            Vector2 position = ResolveMuzzleOrigin();
            muzzleRenderer.transform.position =
                new Vector3(position.x, position.y, transform.position.z);
            muzzleRenderer.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(aimDirection.y, aimDirection.x)
                    * Mathf.Rad2Deg);
        }

        private static ulong SeedFor(long tick)
        {
            unchecked
            {
                ulong value = (ulong)tick;
                value ^= 0x9e3779b97f4a7c15UL;
                value *= 1099511628211UL;
                return value;
            }
        }

        private static string Hash64(string value)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            string text = value ?? string.Empty;
            for (int index = 0; index < text.Length; index++)
            {
                hash ^= text[index];
                hash *= prime;
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }

        private void OnDisable()
        {
            triggerHeld = false;
        }

        private void OnDestroy()
        {
            DisposeRuntime();
            if (muzzleSprite != null) Destroy(muzzleSprite);
            if (muzzleTexture != null) Destroy(muzzleTexture);
        }

        private void DisposeRuntime()
        {
            bound = false;
            triggerHeld = false;
            if (actorState != null) actorState.Deactivate();
            if (runtime != null) runtime.Dispose();
            runtime = null;
            actorState = null;
        }
    }
}
