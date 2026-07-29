using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Application.Weapons.Execution;
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
        private readonly Dictionary<string, WeaponBlueprint> blueprints =
            new Dictionary<string, WeaponBlueprint>(StringComparer.Ordinal);

        internal BoundCanonicalWeaponBlueprintResolverV1(
            IEnumerable<ProductionWeaponMarkV1> equippedMarks)
        {
            if (equippedMarks == null)
            {
                throw new ArgumentNullException(nameof(equippedMarks));
            }

            foreach (ProductionWeaponMarkV1 mark in equippedMarks)
            {
                if (mark == null
                    || mark.Blueprint == null
                    || mark.Blueprint.IsTransitionalCatalogProjection
                    || mark.Blueprint.DefinitionId == null)
                {
                    throw new ArgumentException(
                        "Every equipped gun requires an exact canonical blueprint.",
                        nameof(equippedMarks));
                }

                string definitionId = mark.Blueprint.DefinitionId.Value;
                WeaponBlueprint existing;
                if (blueprints.TryGetValue(definitionId, out existing))
                {
                    if (!ReferenceEquals(existing, mark.Blueprint))
                    {
                        throw new ArgumentException(
                            "One gun definition cannot resolve to conflicting blueprints.",
                            nameof(equippedMarks));
                    }
                    continue;
                }
                blueprints.Add(definitionId, mark.Blueprint);
            }

            if (blueprints.Count == 0)
            {
                throw new ArgumentException(
                    "At least one equipped gun blueprint is required.",
                    nameof(equippedMarks));
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
            resolved = null;
            return requested != null
                && blueprints.TryGetValue(requested.Value, out resolved);
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

    internal sealed class ProductionEquippedGunV1
    {
        internal ProductionEquippedGunV1(
            ProductionWeaponMountPositionV1 mount,
            WeaponEquipmentInstance exactInstance,
            ProductionWeaponMarkV1 mark)
        {
            Mount = mount ?? throw new ArgumentNullException(nameof(mount));
            ExactInstance = exactInstance
                ?? throw new ArgumentNullException(nameof(exactInstance));
            Mark = mark ?? throw new ArgumentNullException(nameof(mark));
        }

        internal ProductionWeaponMountPositionV1 Mount { get; }
        internal WeaponEquipmentInstance ExactInstance { get; }
        internal ProductionWeaponMarkV1 Mark { get; }
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
        private IReadOnlyList<ProductionEquippedGunV1> equippedGuns =
            Array.Empty<ProductionEquippedGunV1>();
        private Vector2 aimDirection = Vector2.right;
        private bool triggerHeld;
        private bool bound;
        private long simulationTick;
        private string lastDiagnostic = string.Empty;

        public bool IsBound { get { return bound; } }
        public int EquippedGunCount { get { return equippedGuns.Count; } }
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
            get
            {
                return equippedGuns.Count == 0
                    ? null
                    : equippedGuns[0].Mount.MountStableId;
            }
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
            if (!CharacterAccount.TryResolveCurrent(
                    out configuredGraph,
                    out profile)
                || configuredGraph == null
                || profile == null
                || configuredGraph.IsDisposed
                || configuredGraph.Character.CharacterInstanceStableId
                    != configuredSource.CharacterInstanceId)
            {
                return false;
            }

            List<ProductionEquippedGunV1> resolvedGuns;
            string rejectionCode;
            if (!TryResolveEquippedGuns(
                    configuredGraph.LoadoutRuntime,
                    out resolvedGuns,
                    out rejectionCode)
                || resolvedGuns.Count == 0
                || resolvedGuns[0].ExactInstance.InstanceId
                    != configuredSource.ExactWeaponInstanceId)
            {
                Report(string.IsNullOrWhiteSpace(rejectionCode)
                    ? "canonical-weapon-fire-equipped-guns-unresolved"
                    : rejectionCode);
                return false;
            }

            EquipmentInstance firstLiveEquipment;
            if (!configuredSource.TryResolveLiveEquipment(
                    out firstLiveEquipment,
                    out rejectionCode)
                || firstLiveEquipment == null
                || firstLiveEquipment.InstanceId
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
                var exactLookup = new CanonicalWeaponEquipmentProjectionLookupV2(
                    configuredGraph.LoadoutRuntime.WeaponHoldings,
                    configuredGraph.LoadoutRuntime.EquipmentCatalog,
                    configuredGraph.LoadoutRuntime.Holdings);
                var marks = new List<ProductionWeaponMarkV1>(resolvedGuns.Count);
                var mounts = new List<InventoryWeaponMountedRuntimeV1>(
                    resolvedGuns.Count);

                for (int index = 0; index < resolvedGuns.Count; index++)
                {
                    ProductionEquippedGunV1 gun = resolvedGuns[index];
                    var equipmentId = new EquipmentInstanceId(
                        gun.ExactInstance.InstanceId);
                    EquipmentInstance exactProjection;
                    if (!exactLookup.TryResolve(equipmentId, out exactProjection)
                        || exactProjection == null
                        || exactProjection.InstanceId
                            != gun.ExactInstance.InstanceId)
                    {
                        throw new InvalidOperationException(
                            "canonical-weapon-fire-exact-projection-rejected:"
                            + gun.ExactInstance.InstanceId);
                    }
                    marks.Add(gun.Mark);
                    mounts.Add(new InventoryWeaponMountedRuntimeV1(
                        gun.Mount.MountStableId,
                        equipmentId,
                        gun.Mount.LateralOffset));
                }

                var resolver = new BoundCanonicalWeaponBlueprintResolverV1(marks);
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

                for (int index = 0; index < resolvedGuns.Count; index++)
                {
                    ProductionEquippedGunV1 gun = resolvedGuns[index];
                    if (!stagedSink.TryBindSource(
                            stagedActor.ActorId,
                            stagedActor.Lifecycle,
                            gun.Mount.MountStableId,
                            new EquipmentInstanceId(gun.ExactInstance.InstanceId),
                            gun.ExactInstance.WeaponDefinitionId))
                    {
                        throw new InvalidOperationException(
                            "canonical-weapon-fire-effect-sink-source-rejected:"
                            + gun.ExactInstance.InstanceId);
                    }
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
                    mounts,
                    adapter);

                graph = configuredGraph;
                source = configuredSource;
                gameplayCamera = configuredCamera;
                actorState = stagedActor;
                runtime = stagedRuntime;
                effectSink = stagedSink;
                equippedGuns = resolvedGuns.AsReadOnly();
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
            Vector2 origin = ResolveBaseOrigin();
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
        }

        private bool HasCurrentExactAuthority(out string rejectionCode)
        {
            rejectionCode = string.Empty;
            ProductionCharacterRuntimeGraphV1 currentGraph;
            ProductionFlowProfileRecordV1 profile;
            if (!CharacterAccount.TryResolveCurrent(
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

            List<ProductionEquippedGunV1> currentGuns;
            if (!TryResolveEquippedGuns(
                    currentGraph.LoadoutRuntime,
                    out currentGuns,
                    out rejectionCode))
            {
                return false;
            }
            if (currentGuns.Count != equippedGuns.Count)
            {
                rejectionCode = "canonical-weapon-fire-equipped-gun-count-stale";
                return false;
            }
            for (int index = 0; index < equippedGuns.Count; index++)
            {
                ProductionEquippedGunV1 expected = equippedGuns[index];
                ProductionEquippedGunV1 current = currentGuns[index];
                if (expected.Mount.MountStableId != current.Mount.MountStableId
                    || expected.ExactInstance.InstanceId
                        != current.ExactInstance.InstanceId
                    || !expected.ExactInstance.WeaponDefinitionId.Equals(
                        current.ExactInstance.WeaponDefinitionId))
                {
                    rejectionCode =
                        "canonical-weapon-fire-equipped-gun-binding-stale";
                    return false;
                }
            }
            if (currentGuns.Count == 0
                || currentGuns[0].ExactInstance.InstanceId
                    != source.ExactWeaponInstanceId)
            {
                rejectionCode = "canonical-weapon-fire-player-source-gun-stale";
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

        private static bool TryResolveEquippedGuns(
            ProductionPlayerLoadoutRuntimeV1 loadout,
            out List<ProductionEquippedGunV1> resolved,
            out string rejectionCode)
        {
            resolved = new List<ProductionEquippedGunV1>();
            rejectionCode = string.Empty;
            if (loadout == null
                || loadout.MountLayout == null
                || loadout.MountLoadoutAuthority == null
                || loadout.WeaponHoldings == null)
            {
                rejectionCode = "canonical-weapon-fire-loadout-unresolved";
                return false;
            }

            WeaponMountLoadoutSnapshotV2 snapshot =
                loadout.MountLoadoutAuthority.ExportSnapshot();
            if (snapshot == null)
            {
                rejectionCode = "canonical-weapon-fire-mount-snapshot-unresolved";
                return false;
            }

            var equipmentIds = new HashSet<StableId>();
            for (int index = 0; index < loadout.MountLayout.Positions.Count; index++)
            {
                ProductionWeaponMountPositionV1 position =
                    loadout.MountLayout.Positions[index];
                if (position == null || !position.IsActive)
                {
                    continue;
                }

                WeaponMountBindingV2 binding = snapshot.Find(
                    position.MountStableId);
                if (binding == null || binding.InstanceId == null)
                {
                    continue;
                }
                if (!equipmentIds.Add(binding.InstanceId))
                {
                    rejectionCode =
                        "canonical-weapon-fire-equipped-gun-duplicated";
                    return false;
                }

                WeaponEquipmentInstance exact = loadout.WeaponHoldings.Find(
                    binding.InstanceId);
                if (exact == null
                    || exact.InstanceId != binding.InstanceId
                    || exact.WeaponDefinitionId == null)
                {
                    rejectionCode =
                        "canonical-weapon-fire-equipped-gun-unowned:"
                        + binding.InstanceId;
                    return false;
                }

                ProductionWeaponMarkV1 mark;
                if (!ProductionWeaponCatalogProvider.Current.TryGetMark(
                        exact.WeaponDefinitionId.Value,
                        out mark)
                    || mark == null
                    || mark.Blueprint == null
                    || !mark.Blueprint.DefinitionId.Equals(
                        exact.WeaponDefinitionId))
                {
                    rejectionCode =
                        "canonical-weapon-fire-equipped-definition-unresolved:"
                        + exact.WeaponDefinitionId.Value;
                    return false;
                }

                resolved.Add(new ProductionEquippedGunV1(
                    position,
                    exact,
                    mark));
            }

            if (resolved.Count == 0)
            {
                rejectionCode = "canonical-weapon-fire-no-active-equipped-guns";
                return false;
            }
            return true;
        }

        private Vector2 ResolveBaseOrigin()
        {
            return (Vector2)transform.position + (aimDirection * 0.55f);
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
            equippedGuns = Array.Empty<ProductionEquippedGunV1>();
            gameplayCamera = null;
            source = null;
            graph = null;
        }
    }
}
