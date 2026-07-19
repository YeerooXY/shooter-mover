from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def write(path, content):
    (ROOT / path).write_text(content, encoding="utf-8", newline="\n")


def replace_once(text, old, new, label):
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{label}: expected one match, found {count}")
    return text.replace(old, new, 1)


projectile_path = "Assets/ShooterMover/ContentPackages/Weapons/Shared/Runtime/ProjectileExecutionPlanAdapter.cs"
projectile = read(projectile_path)
projectile = replace_once(
    projectile,
    '''    /// <summary>
    /// Explicit handler registered behind WeaponMount2DAdapter for one package-owned
    /// operation kind. The mount adapter is the only producer of its execution context,
    /// so projectile spawning cannot bypass a validated WeaponFireExecutionPlan.
    /// </summary>
    public sealed class ProjectileExecutionPlanAdapter :''',
    '''    /// <summary>
    /// Immutable package-neutral emission fact. The original combat event retains
    /// lifecycle/operation context while HitEventId identifies the physical collision.
    /// </summary>
    public sealed class ProjectileExecutionEmission2D
    {
        public ProjectileExecutionEmission2D(
            BoundedProjectile2D projectile,
            StableId combatEventId,
            StableId hitEventId)
        {
            Projectile = projectile
                ?? throw new ArgumentNullException(nameof(projectile));
            CombatEventId = combatEventId
                ?? throw new ArgumentNullException(nameof(combatEventId));
            HitEventId = hitEventId
                ?? throw new ArgumentNullException(nameof(hitEventId));
        }

        public BoundedProjectile2D Projectile { get; }

        public StableId CombatEventId { get; }

        public StableId HitEventId { get; }
    }

    /// <summary>
    /// Explicit handler registered behind WeaponMount2DAdapter for one package-owned
    /// operation kind. The mount adapter is the only producer of its execution context,
    /// so projectile spawning cannot bypass a validated WeaponFireExecutionPlan.
    /// </summary>
    public sealed class ProjectileExecutionPlanAdapter :''',
    "projectile emission fact")
projectile = replace_once(
    projectile,
    '''        private bool isDisposed;
        private BoundedProjectile2D lastSpawnedProjectile;
''',
    '''        private bool isDisposed;
        private BoundedProjectile2D lastSpawnedProjectile;

        public event Action<ProjectileExecutionEmission2D> ProjectileSpawned;
''',
    "projectile spawned event")
projectile = replace_once(
    projectile,
    '''                activeProjectiles.Add(instance);
                lastSpawnedProjectile = instance;
                return true;''',
    '''                activeProjectiles.Add(instance);
                lastSpawnedProjectile = instance;
                PublishProjectileSpawned(
                    new ProjectileExecutionEmission2D(
                        instance,
                        context.CombatEventId,
                        hitEventId));
                return true;''',
    "projectile publish emission")
projectile = replace_once(
    projectile,
    '''            ResetSession();
            isDisposed = true;
        }

        private bool TryValidateEnvelope(''',
    '''            ResetSession();
            ProjectileSpawned = null;
            isDisposed = true;
        }

        private void PublishProjectileSpawned(
            ProjectileExecutionEmission2D emission)
        {
            Action<ProjectileExecutionEmission2D> handler = ProjectileSpawned;
            if (handler == null)
            {
                return;
            }
            try
            {
                handler(emission);
            }
            catch (Exception)
            {
                // Emission observers are optional and cannot invalidate execution.
            }
        }

        private bool TryValidateEnvelope(''',
    "projectile publish helper")
write(projectile_path, projectile)


droid_path = "Assets/ShooterMover/ContentPackages/Enemies/MobileBlasterDroid/MobileBlasterDroidRuntime2D.cs"
droid = read(droid_path)
droid = replace_once(
    droid,
    '''        public WeaponMount2DAdapter WeaponMount { get { return weaponMount; } }
        public CombatHit2DAdapter HitAdapter { get { return hitAdapter; } }
        public Rigidbody2D EnemyBody { get { return enemyBody; } }''',
    '''        public WeaponMount2DAdapter WeaponMount { get { return weaponMount; } }
        public CombatHit2DAdapter HitAdapter { get { return hitAdapter; } }
        public ProjectileExecutionPlanAdapter ProjectileAdapter
        {
            get { return projectileExecutor; }
        }
        public Rigidbody2D EnemyBody { get { return enemyBody; } }''',
    "droid projectile emission port")
write(droid_path, droid)


adapter_path = "Assets/ShooterMover/TestSupport/VisibleSlice/Stage1PlayerLiveAuthorityAdapterV1.cs"
adapter = read(adapter_path)
adapter = replace_once(
    adapter,
    '''using ShooterMover.ContentPackages.Weapons.BlasterMachineGun;
using ShooterMover.Contracts.Combat;''',
    '''using ShooterMover.ContentPackages.Weapons.BlasterMachineGun;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Contracts.Combat;''',
    "adapter shared projectile using")
adapter = replace_once(
    adapter,
    '''        private CombatHit2DAdapter turretHitAdapter;
        private CombatHit2DAdapter droidHitAdapter;
        private VoidHazardTarget2D voidTarget;
''',
    '''        private readonly Dictionary<StableId, long> projectileGenerations =
            new Dictionary<StableId, long>();
        private readonly Dictionary<BoundedProjectile2D, StableId> trackedProjectiles =
            new Dictionary<BoundedProjectile2D, StableId>();
        private CombatHit2DAdapter turretHitAdapter;
        private CombatHit2DAdapter droidHitAdapter;
        private ProjectileExecutionPlanAdapter turretProjectileAdapter;
        private ProjectileExecutionPlanAdapter droidProjectileAdapter;
        private VoidHazardTarget2D voidTarget;
''',
    "adapter projectile ledgers")
old_restart = '''        public PlayerRuntimeRestartResult RequestRestart()
        {
            EnsureInitialized();
            PlayerRuntimeSnapshot before = runtime.ExportSnapshot();
            long retiring = before.Player.LifecycleGeneration;
            long replacement = retiring == long.MaxValue
                ? long.MaxValue
                : retiring + 1L;
            PlayerRuntimeRestartCommand command = new PlayerRuntimeRestartCommand(
                StableId.Create(
                    "operation",
                    "stage1-player-restart-g"
                        + replacement.ToString(CultureInfo.InvariantCulture)),
                before.Player.ActorInstanceId,
                retiring,
                replacement);
            PlayerRuntimeRestartResult result = runtime.Restart(command);
            if (result.Status == PlayerRuntimeRestartStatus.Applied
                && !controller.ApplyAcceptedPlayerRestart(result))
            {
                throw new InvalidOperationException(
                    "The scene rejected an accepted player-authority restart projection.");
            }
            return result;
        }
'''
new_restart = '''        public PlayerRuntimeRestartResult RequestRestart()
        {
            EnsureInitialized();
            PlayerRuntimeSnapshot before = runtime.ExportSnapshot();
            long retiring = before.Player.LifecycleGeneration;
            long replacement = retiring == long.MaxValue
                ? long.MaxValue
                : retiring + 1L;
            return ApplyRestartCommand(
                new PlayerRuntimeRestartCommand(
                    StableId.Create(
                        "operation",
                        "stage1-player-restart-g"
                            + replacement.ToString(CultureInfo.InvariantCulture)),
                    before.Player.ActorInstanceId,
                    retiring,
                    replacement));
        }

        public PlayerRuntimeRestartResult ApplyRestartCommand(
            PlayerRuntimeRestartCommand command)
        {
            EnsureInitialized();
            PlayerRuntimeRestartResult result = runtime.Restart(command);
            if (result.Status == PlayerRuntimeRestartStatus.Applied)
            {
                if (!controller.ApplyAcceptedPlayerRestart(result))
                {
                    throw new InvalidOperationException(
                        "The scene rejected an accepted player-authority restart projection.");
                }
                ClearProjectileLedger();
            }
            return result;
        }
'''
adapter = replace_once(adapter, old_restart, new_restart, "adapter restart command port")
adapter = replace_once(
    adapter,
    '''            if (controller.TurretPackage == null
                || controller.TurretPackage.HitAdapter == null
                || controller.MobileBlasterDroid == null
                || controller.MobileBlasterDroid.HitAdapter == null)
            {
                throw new InvalidOperationException(
                    "PLAYER-LIVE-001 could not bind enemy projectile facts.");
            }

            turretHitAdapter = controller.TurretPackage.HitAdapter;
            droidHitAdapter = controller.MobileBlasterDroid.HitAdapter;
            turretHitAdapter.HitTranslated += HandleTurretHit;
            droidHitAdapter.HitTranslated += HandleDroidHit;
''',
    '''            if (controller.TurretPackage == null
                || controller.TurretPackage.HitAdapter == null
                || controller.TurretPackage.ProjectileAdapter == null
                || controller.MobileBlasterDroid == null
                || controller.MobileBlasterDroid.HitAdapter == null
                || controller.MobileBlasterDroid.ProjectileAdapter == null)
            {
                throw new InvalidOperationException(
                    "PLAYER-LIVE-001 could not bind enemy projectile facts.");
            }

            turretHitAdapter = controller.TurretPackage.HitAdapter;
            droidHitAdapter = controller.MobileBlasterDroid.HitAdapter;
            turretProjectileAdapter = controller.TurretPackage.ProjectileAdapter;
            droidProjectileAdapter = controller.MobileBlasterDroid.ProjectileAdapter;
            turretHitAdapter.HitTranslated += HandleTurretHit;
            droidHitAdapter.HitTranslated += HandleDroidHit;
            turretProjectileAdapter.ProjectileSpawned += HandleProjectileSpawned;
            droidProjectileAdapter.ProjectileSpawned += HandleProjectileSpawned;
''',
    "adapter binds emission ports")
adapter = replace_once(
    adapter,
    '''            long generation;
            if (!TryResolveLifecycleGeneration(
                    translation.Message.EventId,
                    out generation))
            {
                return;
            }
            HitMessage hit = translation.Message;
            ApplyProjectileDamage(''',
    '''            long generation;
            HitMessage hit = translation.Message;
            if (!projectileGenerations.TryGetValue(hit.EventId, out generation))
            {
                return;
            }
            ApplyProjectileDamage(''',
    "adapter consumes emission ledger")
adapter = replace_once(
    adapter,
    '''        private static VoidHazardPortResult MapVoidResult(
            DamageReceiverResult result)
''',
    '''        private void HandleProjectileSpawned(
            ProjectileExecutionEmission2D emission)
        {
            if (!IsInitialized
                || emission == null
                || emission.Projectile == null
                || emission.HitEventId == null)
            {
                return;
            }

            long generation;
            if (!TryResolveLifecycleGeneration(emission.CombatEventId, out generation))
            {
                return;
            }

            long existingGeneration;
            if (projectileGenerations.TryGetValue(
                    emission.HitEventId,
                    out existingGeneration)
                && existingGeneration != generation)
            {
                throw new InvalidOperationException(
                    "A physical hit event cannot change lifecycle generation.");
            }

            projectileGenerations[emission.HitEventId] = generation;
            StableId previousHitEvent;
            if (trackedProjectiles.TryGetValue(
                    emission.Projectile,
                    out previousHitEvent)
                && previousHitEvent != emission.HitEventId)
            {
                projectileGenerations.Remove(previousHitEvent);
            }
            trackedProjectiles[emission.Projectile] = emission.HitEventId;
            emission.Projectile.Completed -= HandleTrackedProjectileCompleted;
            emission.Projectile.Completed += HandleTrackedProjectileCompleted;
        }

        private void HandleTrackedProjectileCompleted(BoundedProjectile2D projectile)
        {
            if (projectile == null)
            {
                return;
            }
            projectile.Completed -= HandleTrackedProjectileCompleted;
            StableId hitEventId;
            if (trackedProjectiles.TryGetValue(projectile, out hitEventId))
            {
                trackedProjectiles.Remove(projectile);
                projectileGenerations.Remove(hitEventId);
            }
        }

        private void ClearProjectileLedger()
        {
            foreach (KeyValuePair<BoundedProjectile2D, StableId> pair
                in trackedProjectiles)
            {
                if (pair.Key != null)
                {
                    pair.Key.Completed -= HandleTrackedProjectileCompleted;
                }
            }
            trackedProjectiles.Clear();
            projectileGenerations.Clear();
        }

        private static VoidHazardPortResult MapVoidResult(
            DamageReceiverResult result)
''',
    "adapter emission ledger handlers")
adapter = replace_once(
    adapter,
    '''            if (droidHitAdapter != null)
            {
                droidHitAdapter.HitTranslated -= HandleDroidHit;
            }
            if (voidTarget != null && controller != null)
''',
    '''            if (droidHitAdapter != null)
            {
                droidHitAdapter.HitTranslated -= HandleDroidHit;
            }
            if (turretProjectileAdapter != null)
            {
                turretProjectileAdapter.ProjectileSpawned -= HandleProjectileSpawned;
            }
            if (droidProjectileAdapter != null)
            {
                droidProjectileAdapter.ProjectileSpawned -= HandleProjectileSpawned;
            }
            ClearProjectileLedger();
            if (voidTarget != null && controller != null)
''',
    "adapter unbinds emission ports")
write(adapter_path, adapter)


controller_path = "Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs"
controller = read(controller_path)
controller = replace_once(
    controller,
    '''        public bool IsPlayerGameplayActive => sessionActive
            && movementLifecycle != null
            && movementLifecycle.IsRunning
            && playerCollider != null
            && playerCollider.enabled;
        public int PlayerHealth
''',
    '''        public bool IsPlayerGameplayActive => sessionActive
            && movementLifecycle != null
            && movementLifecycle.IsRunning
            && playerCollider != null
            && playerCollider.enabled;
        public bool IsPlayerDead => playerLiveAuthority != null
            && playerLiveAuthority.IsInitialized
            && playerLiveAuthority.ExportHudHealth().IsDead;
        public int PlayerHealth
''',
    "controller dead projection property")
controller = replace_once(
    controller,
    '''            if (Keyboard.current != null
                && Keyboard.current.lKey.wasPressedThisFrame)
            {
                OpenLoadoutSelection();
                return;
            }
''',
    '''            if (Keyboard.current != null
                && Keyboard.current.lKey.wasPressedThisFrame
                && !IsPlayerDead)
            {
                OpenLoadoutSelection();
                return;
            }
''',
    "controller blocks dead loadout input")
controller = replace_once(
    controller,
    '''        private void RefreshArenaFlow()
        {
            if (roomMissionLayout == null || playerTransform == null)
''',
    '''        private void RefreshArenaFlow()
        {
            if (IsPlayerDead || roomMissionLayout == null || playerTransform == null)
''',
    "controller blocks dead room interaction")
write(controller_path, controller)


play_path = "Assets/ShooterMover/Tests/PlayMode/VisibleSliceIntegration/Stage1PlayerLiveAuthorityPlayModeTests.cs"
play = read(play_path)
play = replace_once(
    play,
    '''            PlayerRuntimeRestartResult first =
                Invoke<PlayerRuntimeRestartResult>(controller, "QuickRestart");
            PlayerRuntimeRestartResult second =
                Invoke<PlayerRuntimeRestartResult>(controller, "QuickRestart");

            Assert.That(first.Status, Is.EqualTo(PlayerRuntimeRestartStatus.Applied));
            Assert.That(second.Status, Is.EqualTo(PlayerRuntimeRestartStatus.Applied));
''',
    '''            PlayerRuntimeRestartResult first =
                Invoke<PlayerRuntimeRestartResult>(controller, "QuickRestart");
            PlayerRuntimeRestartResult replay = Invoke<PlayerRuntimeRestartResult>(
                adapter,
                "ApplyRestartCommand",
                first.Command);
            Assert.That(replay.Status, Is.EqualTo(PlayerRuntimeRestartStatus.Duplicate));
            Assert.That(Read<long>(controller, "RestartGeneration"),
                Is.EqualTo(before.Player.LifecycleGeneration + 1L),
                "An exact restart replay must not project a second scene reset.");
            PlayerRuntimeRestartResult second =
                Invoke<PlayerRuntimeRestartResult>(controller, "QuickRestart");

            Assert.That(first.Status, Is.EqualTo(PlayerRuntimeRestartStatus.Applied));
            Assert.That(second.Status, Is.EqualTo(PlayerRuntimeRestartStatus.Applied));
''',
    "playmode restart idempotence")
old_damage = '''            PlayerDamageRequest damage = new PlayerDamageRequest(
                eventId,
                source,
                StableId.Parse("participant.untrusted"),
                initial.Player.ActorInstanceId,
                12.5d,
                CombatChannel.Kinetic,
                initial.Player.LifecycleGeneration);
            DamageReceiverResult applied =
                Invoke<DamageReceiverResult>(adapter, "ApplyDamage", damage);
            DamageReceiverResult duplicate =
                Invoke<DamageReceiverResult>(adapter, "ApplyDamage", damage);
            DamageReceiverResult conflict = Invoke<DamageReceiverResult>(
                adapter,
                "ApplyDamage",
                new PlayerDamageRequest(
                    eventId,
                    source,
                    null,
                    initial.Player.ActorInstanceId,
                    8d,
                    CombatChannel.Kinetic,
                    initial.Player.LifecycleGeneration));
'''
new_damage = '''            DamageReceiverResult applied = Invoke<DamageReceiverResult>(
                adapter,
                "ApplyProjectileDamage",
                eventId,
                source,
                initial.Player.ActorInstanceId,
                12.5d,
                CombatChannel.Kinetic,
                initial.Player.LifecycleGeneration);
            DamageReceiverResult duplicate = Invoke<DamageReceiverResult>(
                adapter,
                "ApplyProjectileDamage",
                eventId,
                source,
                initial.Player.ActorInstanceId,
                12.5d,
                CombatChannel.Kinetic,
                initial.Player.LifecycleGeneration);
            DamageReceiverResult conflict = Invoke<DamageReceiverResult>(
                adapter,
                "ApplyProjectileDamage",
                eventId,
                source,
                initial.Player.ActorInstanceId,
                8d,
                CombatChannel.Kinetic,
                initial.Player.LifecycleGeneration);
'''
play = replace_once(play, old_damage, new_damage, "playmode projectile duplicate port")
play = replace_once(
    play,
    '''            Assert.That(Read<bool>(controller, "IsSessionActive"), Is.False);
            Assert.That(Read<bool>(controller, "IsPlayerGameplayActive"), Is.False);
''',
    '''            Assert.That(Read<bool>(controller, "IsSessionActive"), Is.False);
            Assert.That(Read<bool>(controller, "IsPlayerDead"), Is.True);
            Assert.That(Read<bool>(controller, "IsPlayerGameplayActive"), Is.False);
''',
    "playmode dead projection")
write(play_path, play)


doc_path = "docs/architecture/gameplay/PLAYER_LIVE_AUTHORITY_V1.md"
doc = read(doc_path)
doc += '''

Projectile execution now publishes a package-neutral immutable emission fact that
pairs the original combat event with the physical hit event and projectile instance.
The Stage 1 bridge records the source lifecycle generation at emission, consumes it
on collision, and removes the entry when the projectile completes or the accepted
player restart clears the scene. Weapon-plan and hit-event hashes are never decoded.
Exact live restart command replay returns `Duplicate` and does not repeat scene reset.
Dead players are also excluded from loadout and room-transition interaction.
'''
write(doc_path, doc)

print("projectile emission ledger repaired")
