using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.UnityAdapters.Guns.Live;
using ShooterMover.UnityAdapters.Players;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Game
{
    [DefaultExecutionOrder(-2000)]
    [DisallowMultipleComponent]
    internal sealed class PlayerFireInstaller : MonoBehaviour
    {
        private const int MaxStartFrames = 600;
        private int frames;
        private bool failed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            PlayerFire.ResetRuns();
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
                        PlayerFireInstaller>(true) != null)
                {
                    return;
                }
            }

            GameObject installer = new GameObject("Player Fire Installer");
            SceneManager.MoveGameObjectToScene(installer, scene);
            installer.AddComponent<PlayerFireInstaller>();
        }

        private void Update()
        {
            StopOldInstaller();
            if (failed)
            {
                enabled = false;
                return;
            }

            frames++;
            PlayerGunSource source = FindSource();
            Camera camera = FindCamera();
            if (failed)
            {
                enabled = false;
                return;
            }
            if (source == null || !source.IsBound || camera == null)
            {
                if (frames >= MaxStartFrames)
                {
                    Debug.LogError("player-fire-start-failed", this);
                    enabled = false;
                }
                return;
            }

            PlayerGuns oldFire = source.GetComponent<PlayerGuns>();
            if (oldFire != null)
            {
                oldFire.enabled = false;
                Destroy(oldFire);
            }
            BulletSpawner oldSpawn = source.GetComponent<BulletSpawner>();
            if (oldSpawn != null)
            {
                oldSpawn.ClearOwnerBullets();
                Destroy(oldSpawn);
            }

            PlayerFire fire = source.GetComponent<PlayerFire>();
            bool added = fire == null;
            if (added) fire = source.gameObject.AddComponent<PlayerFire>();
            if (!fire.TryBind(source, camera))
            {
                Debug.LogError("player-fire-bind-failed", fire);
                if (added) Destroy(fire);
                enabled = false;
                return;
            }
            Destroy(gameObject);
        }

        private void StopOldInstaller()
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] scripts = roots[rootIndex]
                    .GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < scripts.Length; index++)
                {
                    MonoBehaviour script = scripts[index];
                    if (script != null
                        && string.Equals(
                            script.GetType().FullName,
                            "ShooterMover.UI.Game.GunFireInstaller",
                            StringComparison.Ordinal))
                    {
                        script.enabled = false;
                    }
                }
            }
        }

        private PlayerGunSource FindSource()
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
                        Debug.LogError("player-fire-source-duplicated", this);
                        failed = true;
                        return null;
                    }
                    found = candidate;
                }
            }
            return found;
        }

        private Camera FindCamera()
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
                        Debug.LogError("player-fire-camera-duplicated", this);
                        failed = true;
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
    public sealed class PlayerFire : MonoBehaviour
    {
        private const string DamageSkillId = "striker.damage_bonus";
        private sealed class GunPlay
        {
            internal GunPlay(
                int index,
                EquippedGun source,
                EffectiveGun gun,
                ProjectileExecutionProfile bullet,
                double now)
            {
                Index = index;
                Source = source;
                Gun = gun;
                Bullet = bullet;
                Timer = new GunTimer();
                Timer.Reset(now);
            }

            internal int Index { get; }
            internal EquippedGun Source { get; }
            internal EffectiveGun Gun { get; }
            internal ProjectileExecutionProfile Bullet { get; }
            internal GunTimer Timer { get; }
            internal long ShotNo { get; set; }
        }

        private static readonly StableId BulletRandomPurpose =
            StableId.Parse("gun.projectile-execution");
        private static long nextRun;

        private readonly List<GunPlay> guns = new List<GunPlay>();
        private CharacterLiveGraph graph;
        private PlayerGunSource source;
        private Camera camera;
        private BulletSpawn bullets;
        private GunActorInstanceId actorId;
        private RunParticipantId playerId;
        private LifecycleGeneration runId;
        private Vector2 aim = Vector2.right;
        private bool held;
        private bool pressed;
        private bool bound;
        private long tick;
        private long runNo;
        private string lastError = string.Empty;

        public bool IsBound { get { return bound; } }
        public int GunCount { get { return guns.Count; } }
        public int BulletCount
        {
            get { return bullets == null ? 0 : bullets.ActiveCount; }
        }

        internal static void ResetRuns()
        {
            nextRun = 0L;
        }

        public bool TryBind(
            PlayerGunSource playerSource,
            Camera gameCamera)
        {
            if (bound)
            {
                return ReferenceEquals(source, playerSource)
                    && ReferenceEquals(camera, gameCamera);
            }
            if (playerSource == null
                || !playerSource.IsBound
                || gameCamera == null)
            {
                return false;
            }

            CharacterLiveGraph currentGraph;
            FlowProfileRecord profile;
            if (!CharacterSave.TryResolveCurrent(
                    out currentGraph,
                    out profile)
                || currentGraph == null
                || profile == null
                || currentGraph.IsDisposed
                || currentGraph.Character.CharacterInstanceStableId
                    != playerSource.CharacterInstanceId)
            {
                return false;
            }

            RankedSkillAllocationSnapshot allocation;
            if (!currentGraph.SkillAuthority.TryGet(
                    currentGraph.SkillProfileId,
                    out allocation)
                || allocation == null)
            {
                return false;
            }
            double damageMultiplier = 1d
                + allocation.RankOf(DamageSkillId) * 1d;

            List<GunPlay> resolved;
            string error;
            if (!TryBuildGuns(
                    currentGraph.LoadoutRuntime,
                    damageMultiplier,
                    Time.fixedTimeAsDouble,
                    out resolved,
                    out error))
            {
                Report(error);
                return false;
            }

            long newRun = checked(++nextRun);
            BulletSpawn spawn = GetComponent<BulletSpawn>();
            if (spawn == null)
            {
                spawn = gameObject.AddComponent<BulletSpawn>();
            }

            graph = currentGraph;
            source = playerSource;
            camera = gameCamera;
            bullets = spawn;
            runNo = newRun;
            actorId = new GunActorInstanceId(
                playerSource.CharacterInstanceId);
            runId = new LifecycleGeneration(newRun);
            playerId = new RunParticipantId(
                StableId.Create(
                    "run-participant",
                    "player-fire-" + newRun.ToString(
                        CultureInfo.InvariantCulture)));
            guns.Clear();
            guns.AddRange(resolved);
            bound = true;
            return true;
        }

        private void Update()
        {
            if (!bound || camera == null)
            {
                held = false;
                return;
            }

            Mouse mouse = Mouse.current;
            bool nowHeld = mouse != null && mouse.leftButton.isPressed;
            if (nowHeld && !held) pressed = true;
            held = nowHeld;

            if (mouse == null) return;
            Vector3 screen = mouse.position.ReadValue();
            screen.z = Mathf.Abs(
                camera.transform.position.z - transform.position.z);
            Vector3 world = camera.ScreenToWorldPoint(screen);
            Vector2 candidate = (Vector2)world
                - (Vector2)transform.position;
            if (candidate.sqrMagnitude > 0.000001f)
            {
                aim = candidate.normalized;
            }
        }

        private void FixedUpdate()
        {
            if (!bound || bullets == null) return;
            tick = checked(tick + 1L);
            bool shotPressed = pressed;
            pressed = false;
            double now = Time.fixedTimeAsDouble;

            for (int index = 0; index < guns.Count; index++)
            {
                GunPlay gun = guns[index];
                int shots = gun.Timer.Step(
                    gun.Gun.FireSettings,
                    held,
                    shotPressed,
                    now);
                if (shots > 0 && !TryShoot(gun))
                {
                    Report(
                        "player-fire-shot-failed:"
                        + gun.Gun.DefinitionId);
                }
            }
        }

        private bool TryShoot(GunPlay gun)
        {
            if (gun == null
                || gun.Gun == null
                || gun.Bullet == null
                || actorId == null
                || playerId == null
                || runId == null)
            {
                return false;
            }

            long shotNo = gun.ShotNo;
            gun.ShotNo = checked(shotNo + 1L);
            var shotId = new FireOperationId(
                StableId.Create(
                    "shot",
                    "r" + runNo.ToString(
                        CultureInfo.InvariantCulture)
                    + "-g" + gun.Index.ToString(
                        CultureInfo.InvariantCulture)
                    + "-s" + shotNo.ToString(
                        CultureInfo.InvariantCulture)));
            ulong seed = ShotSeed(runNo, gun.Index, shotNo);
            GunVector2 direction = new GunVector2(aim.x, aim.y);
            GunVector2 origin =
                GunOrigin(gun.Source.Mount.LateralOffset);
            int count = gun.Gun.ShotPattern.ProjectilesPerShot;
            var effects = new List<ProjectileLaunchEffect>(count);

            for (int index = 0; index < count; index++)
            {
                ProjectileOrdinal ordinal =
                    new ProjectileOrdinal(index);
                var identity = new GunEffectIdentity(
                    actorId,
                    playerId,
                    gun.Gun.EquipmentInstanceId,
                    gun.Gun.DefinitionId,
                    shotId,
                    runId,
                    shotNo,
                    ordinal);
                var bulletId =
                    new ProjectileExecutionIdentity(identity);
                DeterministicRandom random =
                    DeterministicRandom.CreateSubstream(
                        seed,
                        DeterministicRandom.CurrentAlgorithmVersion,
                        BulletRandomPurpose,
                        checked((ulong)index));
                var life = new ProjectileLifecycleContext(
                    bulletId,
                    tick,
                    random);
                GunVector2 shotDirection =
                    GunDeterministicSpread.DirectionFor(
                        direction,
                        gun.Gun.ShotPattern.SpreadDegrees,
                        seed,
                        shotId,
                        gun.Gun.EquipmentInstanceId,
                        shotNo,
                        ordinal);
                var request = new ProjectileLaunchRequest(
                    life,
                    gun.Bullet,
                    origin,
                    shotDirection,
                    null);
                effects.Add(new ProjectileLaunchEffect(
                    request,
                    ProjectileLifecycleState.Launch(request)));
            }

            return bullets.TrySpawn(effects, transform);
        }

        private GunVector2 GunOrigin(double side)
        {
            Vector2 baseOrigin = (Vector2)transform.position
                + (aim * 0.55f);
            Vector2 right = new Vector2(-aim.y, aim.x);
            Vector2 value = baseOrigin + (right * (float)side);
            return new GunVector2(value.x, value.y);
        }

        private bool TryBuildGuns(
            PlayerLoadoutLive loadout,
            double damageMultiplier,
            double now,
            out List<GunPlay> result,
            out string error)
        {
            result = new List<GunPlay>();
            List<EquippedGun> equipped;
            if (!TryResolveEquippedGuns(
                    loadout,
                    out equipped,
                    out error))
            {
                return false;
            }

            var marks = new List<GunMark>(equipped.Count);
            for (int index = 0; index < equipped.Count; index++)
            {
                marks.Add(equipped[index].Mark);
            }
            var blueprint = new BoundGunResolver(marks);
            var lookup = new GunEquipmentViewLookup(
                loadout.GunInventory,
                loadout.EquipmentCatalog,
                loadout.Holdings);
            var resolver = new InventoryGunEffectiveResolver(
                loadout.EquipmentCatalog,
                loadout.GunCatalog,
                blueprint,
                new GunAugmentResolver());

            for (int index = 0; index < equipped.Count; index++)
            {
                EquippedGun selected = equipped[index];
                EquipmentInstance item;
                if (!lookup.TryResolve(
                        new EquipmentInstanceId(
                            selected.ExactInstance.InstanceId),
                        out item)
                    || item == null)
                {
                    error = "player-fire-item-missing";
                    return false;
                }

                EffectiveGun gun;
                string gunError;
                if (!resolver.TryResolve(
                        item,
                        out gun,
                        out gunError)
                    || gun == null)
                {
                    error = string.IsNullOrWhiteSpace(gunError)
                        ? "player-fire-gun-missing"
                        : gunError;
                    return false;
                }
                if (!GunPlayRules.Supports(gun))
                {
                    error = "player-fire-gun-not-supported:"
                        + gun.DefinitionId;
                    return false;
                }

                ProjectileExecutionProfile bullet;
                try
                {
                    bullet = ProjectileExecutionProfile.From(gun)
                        .WithDamageMultiplier(damageMultiplier);
                }
                catch (Exception exception)
                {
                    if (GunLiveExceptionPolicy.IsFatal(exception)) throw;
                    error = "player-fire-bullet-invalid:"
                        + exception.Message;
                    return false;
                }

                result.Add(new GunPlay(
                    index,
                    selected,
                    gun,
                    bullet,
                    now));
            }

            error = string.Empty;
            return result.Count > 0;
        }

        private static bool TryResolveEquippedGuns(
            PlayerLoadoutLive loadout,
            out List<EquippedGun> resolved,
            out string error)
        {
            resolved = new List<EquippedGun>();
            error = string.Empty;
            if (loadout == null
                || loadout.MountLayout == null
                || loadout.MountLoadoutAuthority == null
                || loadout.GunInventory == null)
            {
                error = "player-fire-loadout-missing";
                return false;
            }

            LoadoutSnapshot snapshot =
                loadout.MountLoadoutAuthority.ExportSnapshot();
            if (snapshot == null)
            {
                error = "player-fire-loadout-snapshot-missing";
                return false;
            }

            var itemIds = new HashSet<StableId>();
            for (int index = 0;
                 index < loadout.MountLayout.Positions.Count;
                 index++)
            {
                GunSlot slot =
                    loadout.MountLayout.Positions[index];
                if (slot == null || !slot.IsActive) continue;

                ShooterMover.Application.Flow.Game.EquippedGun binding =
                    snapshot.Find(slot.MountStableId);
                if (binding == null || binding.InstanceId == null)
                {
                    continue;
                }
                if (!itemIds.Add(binding.InstanceId))
                {
                    error = "player-fire-gun-duplicated";
                    return false;
                }

                GunItem item =
                    loadout.GunInventory.Find(binding.InstanceId);
                if (item == null
                    || item.InstanceId != binding.InstanceId
                    || item.GunDefinitionId == null)
                {
                    error = "player-fire-gun-not-owned";
                    return false;
                }

                GunMark mark;
                if (!GunCatalogProvider.Current.TryGetMark(
                        item.GunDefinitionId.Value,
                        out mark)
                    || mark == null
                    || mark.Blueprint == null
                    || !mark.Blueprint.DefinitionId.Equals(
                        item.GunDefinitionId))
                {
                    error = "player-fire-gun-definition-missing";
                    return false;
                }

                resolved.Add(new EquippedGun(slot, item, mark));
            }

            if (resolved.Count == 0)
            {
                error = "player-fire-no-guns";
                return false;
            }
            return true;
        }

        private void Report(string error)
        {
            if (string.IsNullOrWhiteSpace(error)
                || string.Equals(
                    error,
                    lastError,
                    StringComparison.Ordinal))
            {
                return;
            }
            lastError = error;
            Debug.LogError(error, this);
        }

        private static ulong ShotSeed(long run, int gun, long shot)
        {
            unchecked
            {
                return ((ulong)(run + 1L)
                        * 11400714819323198485UL)
                    ^ ((ulong)(gun + 1)
                        * 14029467366897019727UL)
                    ^ ((ulong)(shot + 1L)
                        * 1609587929392839161UL);
            }
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
            held = false;
            pressed = false;
            bound = false;
            if (bullets != null) bullets.Clear();
            guns.Clear();
            bullets = null;
            actorId = null;
            playerId = null;
            runId = null;
            camera = null;
            source = null;
            graph = null;
        }
    }
}
