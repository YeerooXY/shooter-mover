using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.UnityAdapters.Players;
using ShooterMover.UnityAdapters.Guns.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Game
{
    internal sealed class BoundGunResolver :
        IGunMappingPolicyResolver,
        IGunResolver
    {
        private readonly Dictionary<string, Gun> blueprints =
            new Dictionary<string, Gun>(StringComparer.Ordinal);

        internal BoundGunResolver(
            IEnumerable<GunMark> equippedMarks)
        {
            if (equippedMarks == null)
            {
                throw new ArgumentNullException(nameof(equippedMarks));
            }

            foreach (GunMark mark in equippedMarks)
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
                Gun existing;
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
            GunDefinitionId requested,
            out GunCatalogBlueprintMappingIntent mappingIntent)
        {
            mappingIntent = null;
            return false;
        }

        public bool TryResolveCanonical(
            GunDefinitionId requested,
            out Gun resolved)
        {
            resolved = null;
            return requested != null
                && blueprints.TryGetValue(requested.Value, out resolved);
        }
    }

    internal sealed class GunActorState :
        IInventoryGunActorStateSource,
        IGunActorOwnershipResolver
    {
        private readonly GunActorInstanceId actorId;
        private readonly LifecycleGeneration lifecycle;
        private readonly RunParticipantId participantId;
        private bool active = true;

        internal GunActorState(
            StableId characterInstanceId,
            long lifecycleGeneration)
        {
            actorId = new GunActorInstanceId(
                characterInstanceId
                ?? throw new ArgumentNullException(nameof(characterInstanceId)));
            lifecycle = new LifecycleGeneration(lifecycleGeneration);
            participantId = new RunParticipantId(
                StableId.Create(
                    "run-participant",
                    "canonical-player-" + Hash64(actorId + "|" + lifecycle)));
        }

        internal GunActorInstanceId ActorId { get { return actorId; } }
        internal LifecycleGeneration Lifecycle { get { return lifecycle; } }

        public bool TryResolveActorState(
            out GunActorInstanceId resolvedActorId,
            out LifecycleGeneration resolvedLifecycle)
        {
            resolvedActorId = active ? actorId : null;
            resolvedLifecycle = active ? lifecycle : null;
            return active;
        }

        public bool TryResolveParticipant(
            GunActorInstanceId requestedActor,
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

    internal sealed class EquippedGun
    {
        internal EquippedGun(
            GunSlot mount,
            GunItem exactInstance,
            GunMark mark)
        {
            Mount = mount ?? throw new ArgumentNullException(nameof(mount));
            ExactInstance = exactInstance
                ?? throw new ArgumentNullException(nameof(exactInstance));
            Mark = mark ?? throw new ArgumentNullException(nameof(mark));
        }

        internal GunSlot Mount { get; }
        internal GunItem ExactInstance { get; }
        internal GunMark Mark { get; }
    }

    [DisallowMultipleComponent]
    internal sealed class GunFireInstaller : MonoBehaviour
    {
        private const int MaximumStartupFrames = 600;
        private int attempts;
        private bool resolutionFailed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            PlayerGuns.ResetLifecycleCounter();
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
                    PlayableLevelCatalog.PlayableLevelScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].GetComponentInChildren<
                        GunFireInstaller>(true) != null)
                {
                    return;
                }
            }

            GameObject installer = new GameObject(
                "Production Canonical Gun Fire Installer");
            SceneManager.MoveGameObjectToScene(installer, scene);
            installer.AddComponent<GunFireInstaller>();
        }

        private void Update()
        {
            if (resolutionFailed)
            {
                enabled = false;
                return;
            }

            attempts++;
            PlayerGunSource source = FindExactSource();
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
                        "canonical-gun-fire-startup-resolution-exhausted",
                        this);
                    enabled = false;
                }
                return;
            }

            PlayerGuns controller =
                source.GetComponent<PlayerGuns>();
            bool controllerAdded = controller == null;
            if (controllerAdded)
            {
                controller = source.gameObject.AddComponent<
                    PlayerGuns>();
            }
            if (!controller.TryBind(source, gameplayCamera))
            {
                Debug.LogError("canonical-gun-fire-binding-rejected", controller);
                if (controllerAdded) Destroy(controller);
                enabled = false;
                return;
            }
            Destroy(gameObject);
        }

        private PlayerGunSource FindExactSource()
        {
            PlayerGunSource found = null;
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                PlayerGunSource[] values = roots[index]
                    .GetComponentsInChildren<PlayerGunSource>(true);
                for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
                {
                    PlayerGunSource candidate = values[valueIndex];
                    if (candidate == null || !candidate.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    if (found != null && !ReferenceEquals(found, candidate))
                    {
                        Debug.LogError(
                            "canonical-gun-fire-player-source-duplicated",
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
                            "canonical-gun-fire-gameplay-camera-duplicated",
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
    public sealed class PlayerGuns : MonoBehaviour
    {
        private static long nextLifecycleGeneration;

        private CharacterLiveGraph graph;
        private PlayerGunSource source;
        private Camera gameplayCamera;
        private GunActorState actorState;
        private InventoryGunLiveSetup runtime;
        private BulletSpawner bulletSpawner;
        private IReadOnlyList<EquippedGun> equippedGuns =
            Array.Empty<EquippedGun>();
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
        public StableId ExactGunInstanceId
        {
            get { return source == null ? null : source.ExactGunInstanceId; }
        }
        public string GunDefinitionId
        {
            get { return source == null ? string.Empty : source.GunDefinitionId; }
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
            PlayerGunSource configuredSource,
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

            CharacterLiveGraph configuredGraph;
            FlowProfileRecord profile;
            if (!CharacterSave.TryResolveCurrent(
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

            List<EquippedGun> resolvedGuns;
            string rejectionCode;
            if (!TryResolveEquippedGuns(
                    configuredGraph.LoadoutRuntime,
                    out resolvedGuns,
                    out rejectionCode)
                || resolvedGuns.Count == 0
                || resolvedGuns[0].ExactInstance.InstanceId
                    != configuredSource.ExactGunInstanceId)
            {
                Report(string.IsNullOrWhiteSpace(rejectionCode)
                    ? "canonical-gun-fire-equipped-guns-unresolved"
                    : rejectionCode);
                return false;
            }

            EquipmentInstance firstLiveEquipment;
            if (!configuredSource.TryResolveLiveEquipment(
                    out firstLiveEquipment,
                    out rejectionCode)
                || firstLiveEquipment == null
                || firstLiveEquipment.InstanceId
                    != configuredSource.ExactGunInstanceId)
            {
                Report("canonical-gun-fire-live-equipment-unresolved:"
                    + rejectionCode);
                return false;
            }

            GunActorState stagedActor = null;
            InventoryGunLiveSetup stagedRuntime = null;
            BulletSpawner stagedSpawner = null;
            bool spawnerAdded = false;
            try
            {
                var exactLookup = new GunEquipmentViewLookup(
                    configuredGraph.LoadoutRuntime.GunInventory,
                    configuredGraph.LoadoutRuntime.EquipmentCatalog,
                    configuredGraph.LoadoutRuntime.Holdings);
                var marks = new List<GunMark>(resolvedGuns.Count);
                var mounts = new List<InventoryGunMountedLive>(
                    resolvedGuns.Count);

                for (int index = 0; index < resolvedGuns.Count; index++)
                {
                    EquippedGun gun = resolvedGuns[index];
                    var equipmentId = new EquipmentInstanceId(
                        gun.ExactInstance.InstanceId);
                    EquipmentInstance exactProjection;
                    if (!exactLookup.TryResolve(equipmentId, out exactProjection)
                        || exactProjection == null
                        || exactProjection.InstanceId
                            != gun.ExactInstance.InstanceId)
                    {
                        throw new InvalidOperationException(
                            "canonical-gun-fire-exact-projection-rejected:"
                            + gun.ExactInstance.InstanceId);
                    }
                    marks.Add(gun.Mark);
                    mounts.Add(new InventoryGunMountedLive(
                        gun.Mount.MountStableId,
                        equipmentId,
                        gun.Mount.LateralOffset));
                }

                var resolver = new BoundGunResolver(marks);
                long lifecycle = checked(++nextLifecycleGeneration);
                stagedActor = new GunActorState(
                    configuredSource.CharacterInstanceId,
                    lifecycle);

                stagedSpawner = GetComponent<
                    BulletSpawner>();
                if (stagedSpawner != null)
                {
                    throw new InvalidOperationException(
                        "canonical-gun-fire-unowned-effect-sink-present");
                }
                stagedSpawner = gameObject.AddComponent<
                    BulletSpawner>();
                spawnerAdded = true;

                for (int index = 0; index < resolvedGuns.Count; index++)
                {
                    EquippedGun gun = resolvedGuns[index];
                    if (!stagedSpawner.TryBindSource(
                            stagedActor.ActorId,
                            stagedActor.Lifecycle,
                            gun.Mount.MountStableId,
                            new EquipmentInstanceId(gun.ExactInstance.InstanceId),
                            gun.ExactInstance.GunDefinitionId))
                    {
                        throw new InvalidOperationException(
                            "canonical-gun-fire-effect-sink-source-rejected:"
                            + gun.ExactInstance.InstanceId);
                    }
                }

                int ticksPerSecond = Math.Max(
                    1,
                    Mathf.RoundToInt(1f / Time.fixedDeltaTime));
                var adapter = new InventoryBackedGunExecutionBridge(
                    exactLookup,
                    configuredGraph.LoadoutRuntime.EquipmentCatalog,
                    configuredGraph.LoadoutRuntime.GunCatalog,
                    stagedActor,
                    stagedSpawner,
                    ticksPerSecond,
                    resolver,
                    new GunAugmentResolver());
                stagedRuntime = new InventoryGunLiveSetup(
                    stagedActor,
                    mounts,
                    adapter);

                graph = configuredGraph;
                source = configuredSource;
                gameplayCamera = configuredCamera;
                actorState = stagedActor;
                runtime = stagedRuntime;
                bulletSpawner = stagedSpawner;
                equippedGuns = resolvedGuns.AsReadOnly();
                bound = true;
                return true;
            }
            catch (Exception exception)
            {
                if (GunLiveExceptionPolicy.IsFatal(exception)) throw;
                if (stagedRuntime != null) stagedRuntime.Dispose();
                if (stagedActor != null) stagedActor.Deactivate();
                if (stagedSpawner != null) stagedSpawner.ClearOwnerBullets();
                if (spawnerAdded && stagedSpawner != null) Destroy(stagedSpawner);
                Report("canonical-gun-fire-composition-rejected:"
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
            var gunOrigin = new GunVector2(origin.x, origin.y);
            var gunAim = new GunVector2(
                aimDirection.x,
                aimDirection.y);
            InventoryGunExecutionResult input = runtime.UpdateTriggerInput(
                triggerHeld,
                operationId,
                simulationTick,
                SeedFor(simulationTick),
                gunOrigin,
                gunAim);
            ReportResult(input);
        }

        private bool HasCurrentExactAuthority(out string rejectionCode)
        {
            rejectionCode = string.Empty;
            CharacterLiveGraph currentGraph;
            FlowProfileRecord profile;
            if (!CharacterSave.TryResolveCurrent(
                    out currentGraph,
                    out profile)
                || currentGraph == null
                || profile == null
                || currentGraph.IsDisposed
                || !ReferenceEquals(currentGraph, graph))
            {
                rejectionCode =
                    "canonical-gun-fire-current-character-authority-stale";
                return false;
            }
            if (source == null
                || !source.IsBound
                || source.CharacterInstanceId
                    != graph.Character.CharacterInstanceStableId)
            {
                rejectionCode = "canonical-gun-fire-player-source-stale";
                return false;
            }

            List<EquippedGun> currentGuns;
            if (!TryResolveEquippedGuns(
                    currentGraph.LoadoutRuntime,
                    out currentGuns,
                    out rejectionCode))
            {
                return false;
            }
            if (currentGuns.Count != equippedGuns.Count)
            {
                rejectionCode = "canonical-gun-fire-equipped-gun-count-stale";
                return false;
            }
            for (int index = 0; index < equippedGuns.Count; index++)
            {
                EquippedGun expected = equippedGuns[index];
                EquippedGun current = currentGuns[index];
                if (expected.Mount.MountStableId != current.Mount.MountStableId
                    || expected.ExactInstance.InstanceId
                        != current.ExactInstance.InstanceId
                    || !expected.ExactInstance.GunDefinitionId.Equals(
                        current.ExactInstance.GunDefinitionId))
                {
                    rejectionCode =
                        "canonical-gun-fire-equipped-gun-binding-stale";
                    return false;
                }
            }
            if (currentGuns.Count == 0
                || currentGuns[0].ExactInstance.InstanceId
                    != source.ExactGunInstanceId)
            {
                rejectionCode = "canonical-gun-fire-player-source-gun-stale";
                return false;
            }

            EquipmentInstance liveEquipment;
            string projectionRejection;
            if (!source.TryResolveLiveEquipment(
                    out liveEquipment,
                    out projectionRejection)
                || liveEquipment == null
                || liveEquipment.InstanceId != source.ExactGunInstanceId)
            {
                rejectionCode = string.IsNullOrWhiteSpace(projectionRejection)
                    ? "canonical-gun-fire-live-projection-stale"
                    : projectionRejection;
                return false;
            }
            return true;
        }

        private static bool TryResolveEquippedGuns(
            PlayerLoadoutLive loadout,
            out List<EquippedGun> resolved,
            out string rejectionCode)
        {
            resolved = new List<EquippedGun>();
            rejectionCode = string.Empty;
            if (loadout == null
                || loadout.MountLayout == null
                || loadout.MountLoadoutAuthority == null
                || loadout.GunInventory == null)
            {
                rejectionCode = "canonical-gun-fire-loadout-unresolved";
                return false;
            }

            LoadoutSnapshot snapshot =
                loadout.MountLoadoutAuthority.ExportSnapshot();
            if (snapshot == null)
            {
                rejectionCode = "canonical-gun-fire-mount-snapshot-unresolved";
                return false;
            }

            var equipmentIds = new HashSet<StableId>();
            for (int index = 0; index < loadout.MountLayout.Positions.Count; index++)
            {
                GunSlot position =
                    loadout.MountLayout.Positions[index];
                if (position == null || !position.IsActive)
                {
                    continue;
                }

                ShooterMover.Application.Flow.Game.EquippedGun binding = snapshot.Find(
                    position.MountStableId);
                if (binding == null || binding.InstanceId == null)
                {
                    continue;
                }
                if (!equipmentIds.Add(binding.InstanceId))
                {
                    rejectionCode =
                        "canonical-gun-fire-equipped-gun-duplicated";
                    return false;
                }

                GunItem exact = loadout.GunInventory.Find(
                    binding.InstanceId);
                if (exact == null
                    || exact.InstanceId != binding.InstanceId
                    || exact.GunDefinitionId == null)
                {
                    rejectionCode =
                        "canonical-gun-fire-equipped-gun-unowned:"
                        + binding.InstanceId;
                    return false;
                }

                GunMark mark;
                if (!GunCatalogProvider.Current.TryGetMark(
                        exact.GunDefinitionId.Value,
                        out mark)
                    || mark == null
                    || mark.Blueprint == null
                    || !mark.Blueprint.DefinitionId.Equals(
                        exact.GunDefinitionId))
                {
                    rejectionCode =
                        "canonical-gun-fire-equipped-definition-unresolved:"
                        + exact.GunDefinitionId.Value;
                    return false;
                }

                resolved.Add(new EquippedGun(
                    position,
                    exact,
                    mark));
            }

            if (resolved.Count == 0)
            {
                rejectionCode = "canonical-gun-fire-no-active-equipped-guns";
                return false;
            }
            return true;
        }

        private Vector2 ResolveBaseOrigin()
        {
            return (Vector2)transform.position + (aimDirection * 0.55f);
        }

        private void ReportResult(InventoryGunExecutionResult result)
        {
            if (result != null && !string.IsNullOrWhiteSpace(result.RejectionCode))
            {
                Report("canonical-gun-fire-runtime-rejected:"
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
            if (bulletSpawner != null) bulletSpawner.ClearOwnerBullets();
            runtime = null;
            actorState = null;
            bulletSpawner = null;
            equippedGuns = Array.Empty<EquippedGun>();
            gameplayCamera = null;
            source = null;
            graph = null;
        }
    }
}
