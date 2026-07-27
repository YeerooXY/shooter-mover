using System;
using System.Globalization;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Players;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    internal sealed class BoundCanonicalWeaponBlueprintResolverV1 :
        IWeaponBlueprintMappingPolicyResolver,
        ICanonicalWeaponBlueprintResolver
    {
        private readonly WeaponDefinitionId definitionId;
        private readonly WeaponBlueprint blueprint;

        internal BoundCanonicalWeaponBlueprintResolverV1(
            WeaponDefinitionId exactDefinitionId,
            WeaponBlueprint exactBlueprint)
        {
            definitionId = exactDefinitionId
                ?? throw new ArgumentNullException(nameof(exactDefinitionId));
            blueprint = exactBlueprint
                ?? throw new ArgumentNullException(nameof(exactBlueprint));
            if (blueprint.IsTransitionalCatalogProjection
                || !blueprint.DefinitionId.Equals(definitionId))
            {
                throw new ArgumentException(
                    "The bound blueprint must be the exact canonical definition.",
                    nameof(exactBlueprint));
            }
        }

        public bool TryResolve(
            WeaponDefinitionId requested,
            out WeaponCatalogBlueprintMappingIntent mappingIntent)
        {
            mappingIntent = null;
            return false;
        }

        public bool TryResolveCanonical(
            WeaponDefinitionId requested,
            out WeaponBlueprint resolved)
        {
            bool matches = requested != null && requested.Equals(definitionId);
            resolved = matches ? blueprint : null;
            return matches;
        }
    }

    internal sealed class ProductionCanonicalWeaponActorStateV1 :
        IInventoryWeaponActorStateSource,
        IWeaponActorOwnershipResolver
    {
        private readonly WeaponActorInstanceId actorId;
        private readonly LifecycleGeneration lifecycle;
        private readonly RunParticipantId participantId;
        private bool active = true;

        internal ProductionCanonicalWeaponActorStateV1(
            StableId characterInstanceId,
            long lifecycleGeneration)
        {
            actorId = new WeaponActorInstanceId(
                characterInstanceId
                ?? throw new ArgumentNullException(nameof(characterInstanceId)));
            lifecycle = new LifecycleGeneration(lifecycleGeneration);
            participantId = new RunParticipantId(
                StableId.Create(
                    "run-participant",
                    "canonical-player-" + Hash64(actorId + "|" + lifecycle)));
        }

        internal WeaponActorInstanceId ActorId { get { return actorId; } }
        internal LifecycleGeneration Lifecycle { get { return lifecycle; } }

        public bool TryResolveActorState(
            out WeaponActorInstanceId resolvedActorId,
            out LifecycleGeneration resolvedLifecycle)
        {
            resolvedActorId = active ? actorId : null;
            resolvedLifecycle = active ? lifecycle : null;
            return active;
        }

        public bool TryResolveParticipant(
            WeaponActorInstanceId requestedActor,
            LifecycleGeneration requestedLifecycle,
            out RunParticipantId resolvedParticipant)
        {
            bool matches = active
                && requestedActor != null
                && requestedLifecycle != null
                && actorId.Equals(requestedActor)
                && lifecycle.Equals(requestedLifecycle);
            resolvedParticipant = matches ? participantId : null;
            return matches;
        }

        internal void Deactivate()
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

    [DisallowMultipleComponent]
    internal sealed class ProductionCanonicalWeaponFireInstallerV1 : MonoBehaviour
    {
        private const int MaximumStartupFrames = 600;
        private int attempts;
        private bool resolutionFailed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            ProductionCanonicalWeaponFireControllerV1.ResetLifecycleCounter();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
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
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].GetComponentInChildren<
                        ProductionCanonicalWeaponFireInstallerV1>(true) != null)
                {
                    return;
                }
            }

            GameObject installer = new GameObject(
                "Production Canonical Weapon Fire Installer");
            SceneManager.MoveGameObjectToScene(installer, scene);
            installer.AddComponent<ProductionCanonicalWeaponFireInstallerV1>();
        }

        private void Update()
        {
            if (resolutionFailed)
            {
                enabled = false;
                return;
            }

            attempts++;
            CanonicalPlayerWeaponSourceV2 source = FindExactSource();
            Camera gameplayCamera = FindExactCamera();
            if (resolutionFailed)
            {
                enabled = false;
                return;
            }
            if (source == null || !source.IsBound || gameplayCamera == null)
            {
                if (attempts >= MaximumStartupFrames)
                {
                    Debug.LogError(
                        "canonical-weapon-fire-startup-resolution-exhausted",
                        this);
                    enabled = false;
                }
                return;
            }

            ProductionCanonicalWeaponFireControllerV1 controller =
                source.GetComponent<ProductionCanonicalWeaponFireControllerV1>();
            bool controllerAdded = controller == null;
            if (controllerAdded)
            {
                controller = source.gameObject.AddComponent<
                    ProductionCanonicalWeaponFireControllerV1>();
            }
            if (!controller.TryBind(source, gameplayCamera))
            {
                Debug.LogError("canonical-weapon-fire-binding-rejected", controller);
                if (controllerAdded) Destroy(controller);
                enabled = false;
                return;
            }
            Destroy(gameObject);
        }

        private CanonicalPlayerWeaponSourceV2 FindExactSource()
        {
            CanonicalPlayerWeaponSourceV2 found = null;
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                CanonicalPlayerWeaponSourceV2[] values = roots[index]
                    .GetComponentsInChildren<CanonicalPlayerWeaponSourceV2>(true);
                for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
                {
                    CanonicalPlayerWeaponSourceV2 candidate = values[valueIndex];
                    if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    if (found != null && !ReferenceEquals(found, candidate))
                    {
                        Debug.LogError(
                            "canonical-weapon-fire-player-source-duplicated",
                            this);
                        resolutionFailed = true;
                        return null;
                    }
                    found = candidate;
                }
            }
            return found;
        }

        private Camera FindExactCamera()
        {
            Camera found = null;
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                Camera[] values = roots[index].GetComponentsInChildren<Camera>(true);
                for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
                {
                    Camera candidate = values[valueIndex];
                    if (candidate == null
                        || !candidate.enabled
                        || !candidate.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    if (found != null && !ReferenceEquals(found, candidate))
                    {
                        Debug.LogError(
                            "canonical-weapon-fire-gameplay-camera-duplicated",
                            this);
                        resolutionFailed = true;
                        return null;
                    }
                    found = candidate;
                }
            }
            return found;
        }
    }

    [DefaultExecutionOrder(700)]
    [DisallowMultipleComponent]
    public sealed class ProductionCanonicalWeaponFireControllerV1 : MonoBehaviour
    {
        private static long nextLifecycleGeneration;

        private ProductionCharacterRuntimeGraphV1 graph;
        private CanonicalPlayerWeaponSourceV2 source;
        private Camera gameplayCamera;
        private ProductionCanonicalWeaponActorStateV1 actorState;
        private InventoryWeaponRuntimeComposition runtime;
        private ProductionCanonicalProjectileEffectSink2D effectSink;
        private ProductionWeaponMountPositionV1 mountPosition;
        private Vector2 aimDirection = Vector2.right;
        private bool triggerHeld;
        private bool bound;
        private long simulationTick;
        private string lastDiagnostic = string.Empty;

        public bool IsBound { get { return bound; } }
        public StableId CharacterInstanceId
        {
            get { return source == null ? null : source.CharacterInstanceId; }
        }
        public StableId ExactWeaponInstanceId
        {
            get { return source == null ? null : source.ExactWeaponInstanceId; }
        }
        public string WeaponDefinitionId
        {
            get { return source == null ? string.Empty : source.WeaponDefinitionId; }
        }
        public StableId MountStableId
        {
            get { return mountPosition == null ? null : mountPosition.MountStableId; }
        }

        internal static void ResetLifecycleCounter()
        {
            nextLifecycleGeneration = 0L;
        }

        public bool TryBind(
            CanonicalPlayerWeaponSourceV2 configuredSource,
            Camera configuredCamera)
        {
            if (bound)
            {
                return ReferenceEquals(source, configuredSource)
                    && ReferenceEquals(gameplayCamera, configuredCamera);
            }
            if (configuredSource == null
                || !configuredSource.IsBound
                || configuredSource.ExactInstance == null
                || configuredSource.ResolvedMark == null
                || configuredSource.ResolvedMark.Blueprint == null
                || configuredCamera == null)
            {
                return false;
            }

            ProductionCharacterRuntimeGraphV1 configuredGraph;
            ProductionFlowProfileRecordV1 profile;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out configuredGraph,
                    out profile)
                || configuredGraph == null
                || profile == null
                || configuredGraph.IsDisposed
                || configuredGraph.Character.CharacterInstanceStableId
                    != configuredSource.CharacterInstanceId
                || configuredSource.ExactInstance.InstanceId
                    != configuredSource.ExactWeaponInstanceId
                || !configuredSource.ExactInstance.WeaponDefinitionId.Value.Equals(
                    configuredSource.WeaponDefinitionId,
                    StringComparison.Ordinal)
                || !configuredSource.ResolvedMark.Blueprint.DefinitionId.Equals(
                    configuredSource.ExactInstance.WeaponDefinitionId))
            {
                return false;
            }

            ProductionWeaponMountPositionV1 exactMount;
            if (!TryResolveExactFirstActiveMount(
                    configuredGraph.LoadoutRuntime,
                    configuredSource.ExactWeaponInstanceId,
                    out exactMount))
            {
                return false;
            }

            EquipmentInstance liveEquipment;
            string rejectionCode;
            if (!configuredSource.TryResolveLiveEquipment(
                    out liveEquipment,
                    out rejectionCode)
                || liveEquipment == null
                || liveEquipment.InstanceId
                    != configuredSource.ExactWeaponInstanceId)
            {
                Report("canonical-weapon-fire-live-equipment-unresolved:"
                    + rejectionCode);
                return false;
            }

            ProductionCanonicalWeaponActorStateV1 stagedActor = null;
            InventoryWeaponRuntimeComposition stagedRuntime = null;
            ProductionCanonicalProjectileEffectSink2D stagedSink = null;
            bool sinkAdded = false;
            try
            {
                var exactDefinition = new WeaponDefinitionId(
                    configuredSource.WeaponDefinitionId);
                var exactEquipmentInstanceId = new EquipmentInstanceId(
                    configuredSource.ExactWeaponInstanceId);
                var exactLookup = new CanonicalWeaponEquipmentProjectionLookupV2(
                    configuredGraph.LoadoutRuntime.WeaponHoldings,
                    configuredGraph.LoadoutRuntime.EquipmentCatalog,
                    configuredGraph.LoadoutRuntime.Holdings);
                EquipmentInstance exactProjection;
                if (!exactLookup.TryResolve(
                        exactEquipmentInstanceId,
                        out exactProjection)
                    || exactProjection == null
                    || exactProjection.InstanceId
                        != configuredSource.ExactWeaponInstanceId
                    || exactProjection.DefinitionId != liveEquipment.DefinitionId)
                {
                    throw new InvalidOperationException(
                        "canonical-weapon-fire-exact-projection-rejected");
                }

                var resolver = new BoundCanonicalWeaponBlueprintResolverV1(
                    exactDefinition,
                    configuredSource.ResolvedMark.Blueprint);
                long lifecycle = checked(++nextLifecycleGeneration);
                stagedActor = new ProductionCanonicalWeaponActorStateV1(
                    configuredSource.CharacterInstanceId,
                    lifecycle);

                stagedSink = GetComponent<
                    ProductionCanonicalProjectileEffectSink2D>();
                if (stagedSink != null)
                {
                    throw new InvalidOperationException(
                        "canonical-weapon-fire-unowned-effect-sink-present");
                }
                stagedSink = gameObject.AddComponent<
                    ProductionCanonicalProjectileEffectSink2D>();
                sinkAdded = true;
                if (!stagedSink.TryBindSource(
                        stagedActor.ActorId,
                        stagedActor.Lifecycle,
                        exactMount.MountStableId,
                        exactEquipmentInstanceId,
                        exactDefinition))
                {
                    throw new InvalidOperationException(
                        "canonical-weapon-fire-effect-sink-source-rejected");
                }

                int ticksPerSecond = Math.Max(
                    1,
                    Mathf.RoundToInt(1f / Time.fixedDeltaTime));
                var adapter = new InventoryBackedWeaponExecutionAdapter(
                    exactLookup,
                    configuredGraph.LoadoutRuntime.EquipmentCatalog,
                    configuredGraph.LoadoutRuntime.WeaponCatalog,
                    stagedActor,
                    stagedSink,
                    ticksPerSecond,
                    resolver,
                    new UnaugmentedWeaponModifierSetResolver());
                stagedRuntime = new InventoryWeaponRuntimeComposition(
                    stagedActor,
                    new[]
                    {
                        new InventoryWeaponMountedRuntimeV1(
                            exactMount.MountStableId,
                            exactEquipmentInstanceId,
                            exactMount.LateralOffset),
                    },
                    adapter);

                graph = configuredGraph;
                source = configuredSource;
                gameplayCamera = configuredCamera;
                actorState = stagedActor;
                runtime = stagedRuntime;
                effectSink = stagedSink;
                mountPosition = exactMount;
                bound = true;
                return true;
            }
            catch (Exception exception)
            {
                if (WeaponLiveExceptionPolicyV1.IsFatal(exception)) throw;
                if (stagedRuntime != null) stagedRuntime.Dispose();
                if (stagedActor != null) stagedActor.Deactivate();
                if (stagedSink != null) stagedSink.RetireOwnerPresentation();
                if (sinkAdded && stagedSink != null) Destroy(stagedSink);
                Report("canonical-weapon-fire-composition-rejected:"
                    + exception.Message);
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
            triggerHeld = mouse != null && mouse.leftButton.isPressed;
            if (mouse == null) return;
            Vector3 screen = mouse.position.ReadValue();
            screen.z = Mathf.Abs(
                gameplayCamera.transform.position.z - transform.position.z);
            Vector3 world = gameplayCamera.ScreenToWorldPoint(screen);
            Vector2 candidate = (Vector2)world - (Vector2)transform.position;
            if (candidate.sqrMagnitude > 0.000001f)
            {
                aimDirection = candidate.normalized;
            }
        }

        private void FixedUpdate()
        {
            if (!bound || runtime == null || actorState == null) return;
            string authorityRejection;
            if (!HasCurrentExactAuthority(out authorityRejection))
            {
                Report(authorityRejection);
                Shutdown();
                enabled = false;
                return;
            }

            simulationTick = checked(simulationTick + 1L);
            Vector2 origin = ResolveOrigin();
            var operationId = new FireOperationId(
                StableId.Create(
                    "fire-operation",
                    "canonical-player-input-"
                    + Hash64(
                        actorState.ActorId
                        + "|" + actorState.Lifecycle
                        + "|" + simulationTick.ToString(
                            CultureInfo.InvariantCulture))));
            var weaponOrigin = new WeaponVector2(origin.x, origin.y);
            var weaponAim = new WeaponVector2(
                aimDirection.x,
                aimDirection.y);
            InventoryWeaponExecutionResult input = runtime.UpdateTriggerInput(
                triggerHeld,
                operationId,
                simulationTick,
                SeedFor(simulationTick),
                weaponOrigin,
                weaponAim);
            ReportResult(input);
            ReportResult(runtime.Advance(simulationTick));
        }

        private bool HasCurrentExactAuthority(out string rejectionCode)
        {
            rejectionCode = string.Empty;
            ProductionCharacterRuntimeGraphV1 currentGraph;
            ProductionFlowProfileRecordV1 profile;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out currentGraph,
                    out profile)
                || currentGraph == null
                || profile == null
                || currentGraph.IsDisposed
                || !ReferenceEquals(currentGraph, graph))
            {
                rejectionCode =
                    "canonical-weapon-fire-current-character-authority-stale";
                return false;
            }
            if (source == null
                || !source.IsBound
                || source.CharacterInstanceId
                    != graph.Character.CharacterInstanceStableId)
            {
                rejectionCode = "canonical-weapon-fire-player-source-stale";
                return false;
            }

            WeaponEquipmentInstance held = graph.LoadoutRuntime.WeaponHoldings.Find(
                source.ExactWeaponInstanceId);
            if (held == null
                || held.InstanceId != source.ExactWeaponInstanceId
                || !held.WeaponDefinitionId.Value.Equals(
                    source.WeaponDefinitionId,
                    StringComparison.Ordinal))
            {
                rejectionCode = "canonical-weapon-fire-exact-ownership-stale";
                return false;
            }

            ProductionWeaponMountPositionV1 currentMount;
            if (!TryResolveExactFirstActiveMount(
                    graph.LoadoutRuntime,
                    source.ExactWeaponInstanceId,
                    out currentMount)
                || currentMount == null
                || mountPosition == null
                || currentMount.MountStableId != mountPosition.MountStableId)
            {
                rejectionCode = "canonical-weapon-fire-exact-mount-stale";
                return false;
            }

            EquipmentInstance liveEquipment;
            string projectionRejection;
            if (!source.TryResolveLiveEquipment(
                    out liveEquipment,
                    out projectionRejection)
                || liveEquipment == null
                || liveEquipment.InstanceId != source.ExactWeaponInstanceId)
            {
                rejectionCode = string.IsNullOrWhiteSpace(projectionRejection)
                    ? "canonical-weapon-fire-live-projection-stale"
                    : projectionRejection;
                return false;
            }
            return true;
        }

        private Vector2 ResolveOrigin()
        {
            Vector2 perpendicular = new Vector2(
                -aimDirection.y,
                aimDirection.x);
            return (Vector2)transform.position
                + (aimDirection * 0.55f)
                + (perpendicular * (float)mountPosition.LateralOffset);
        }

        private static bool TryResolveExactFirstActiveMount(
            ProductionPlayerLoadoutRuntimeV1 loadout,
            StableId exactInstanceId,
            out ProductionWeaponMountPositionV1 resolved)
        {
            resolved = null;
            if (loadout == null || exactInstanceId == null) return false;
            WeaponMountLoadoutSnapshotV2 snapshot =
                loadout.MountLoadoutAuthority.ExportSnapshot();
            for (int index = 0; index < loadout.MountLayout.Positions.Count; index++)
            {
                ProductionWeaponMountPositionV1 position =
                    loadout.MountLayout.Positions[index];
                if (!position.IsActive) continue;
                WeaponMountBindingV2 binding = snapshot.Find(
                    position.MountStableId);
                if (binding == null || binding.InstanceId == null) continue;
                if (binding.InstanceId != exactInstanceId) return false;
                resolved = position;
                return true;
            }
            return false;
        }

        private void ReportResult(InventoryWeaponExecutionResult result)
        {
            if (result != null && !string.IsNullOrWhiteSpace(result.RejectionCode))
            {
                Report("canonical-weapon-fire-runtime-rejected:"
                    + result.RejectionCode);
            }
        }

        private void Report(string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(diagnostic)
                || string.Equals(
                    diagnostic,
                    lastDiagnostic,
                    StringComparison.Ordinal))
            {
                return;
            }
            lastDiagnostic = diagnostic;
            Debug.LogError(diagnostic, this);
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

        private void OnDisable()
        {
            Shutdown();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void Shutdown()
        {
            triggerHeld = false;
            bound = false;
            if (actorState != null) actorState.Deactivate();
            if (runtime != null) runtime.Dispose();
            if (effectSink != null) effectSink.RetireOwnerPresentation();
            runtime = null;
            actorState = null;
            effectSink = null;
            mountPosition = null;
            gameplayCamera = null;
            source = null;
            graph = null;
        }
    }
}
