using System;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Production.Level1;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    public sealed partial class Stage1PlayableLoopCompositionV1
    {
        private const string EnemyCatalogResourcePath =
            "EnemyCatalog/enemy_catalog_v2";

        private static readonly StableId MobileDefinitionStableId =
            StableId.Parse("enemy.mobile-blaster-droid");
        private static readonly StableId TurretDefinitionStableId =
            StableId.Parse("enemy.blaster-turret");
        private static readonly StableId MobileParticipantStableId =
            StableId.Parse("participant.stage1-mobile-droid");
        private static readonly StableId TurretParticipantStableId =
            StableId.Parse("participant.stage1-blaster-turret");
        private static readonly StableId EnemyFactionStableId =
            StableId.Parse("faction.hostile-machines");
        private static readonly StableId RoomRuntimeStableId =
            StableId.Parse("room-runtime-instance.demo-cutover-level1");

        private RunSessionEnemyAttackPatternTimeV1 enemyPatternTime;
        private EnemyAttackPatternUnitySourceRegistryV1 enemyPatternSources;
        private EnemyAttackPatternHitRouterV1 enemyPatternHitRouter;
        private EnemyAttackPatternUnityEmissionRealizerV1 enemyPatternRealizer;
        private EnemyAttackPatternLiveSchedulerV1 enemyPatternScheduler;
        private EnemyAttackPatternProductionController2D mobilePatternController;
        private EnemyAttackPatternProductionController2D turretPatternController;
        private EnemyAttackPatternProjectilePrefabRegistryV1 enemyProjectilePrefabs;
        private BoundedProjectile2D enemyProjectileRuntimePrefab;
        private StableId enemyPatternObservedRunStableId;
        private long enemyPatternObservedPlayerGeneration = -1L;

        private void TickEnemyAttackPatterns()
        {
            RunSessionAggregateV1 run;
            if (!TryResolveSharedRunSession(out run))
            {
                throw new InvalidOperationException(
                    "Enemy attack patterns require the shared production Run Session.");
            }
            long playerGeneration = controller.PlayerLiveAuthority
                .ExportSnapshot()
                .Player
                .LifecycleGeneration;
            bool current = enemyPatternScheduler != null
                && enemyPatternObservedRunStableId == run.RunStableId
                && enemyPatternObservedPlayerGeneration == playerGeneration;
            if (!current)
            {
                ComposeEnemyAttackPatterns(run, playerGeneration);
            }

            mobilePatternController.Tick();
            turretPatternController.Tick();
            enemyPatternScheduler.Tick();
            enemyPatternRealizer.Tick();
        }

        private void ComposeEnemyAttackPatterns(
            RunSessionAggregateV1 run,
            long playerGeneration)
        {
            TeardownEnemyAttackPatterns();
            if (run == null
                || run.LifecycleState != RunSessionLifecycleStateV1.Active
                || run.RunStableId != runStableId
                || run.LifecycleGeneration != playerGeneration
                || controller.PlayerLiveAuthority == null
                || !controller.PlayerLiveAuthority.IsInitialized)
            {
                throw new InvalidOperationException(
                    "Enemy attack-pattern shared-run prerequisites are unavailable.");
            }

            EnemyCatalogV1 catalog = LoadProductionEnemyCatalog();
            EnemyDefinitionV1 mobileDefinition =
                catalog.GetDefinition(MobileDefinitionStableId);
            EnemyDefinitionV1 turretDefinition =
                catalog.GetDefinition(TurretDefinitionStableId);
            EnemyAttackCapabilityDescriptorV1 mobileAttack =
                RequireSingleShootingAttack(mobileDefinition);
            EnemyAttackCapabilityDescriptorV1 turretAttack =
                RequireSingleShootingAttack(turretDefinition);

            enemyPatternSources =
                new EnemyAttackPatternUnitySourceRegistryV1();
            enemyPatternTime = new RunSessionEnemyAttackPatternTimeV1(
                run,
                enemyPatternSources,
                SimulationTicksPerSecond);
            enemyProjectilePrefabs =
                new EnemyAttackPatternProjectilePrefabRegistryV1();
            EnsureEnemyProjectileRuntimePrefab();
            enemyProjectilePrefabs.Register(
                StableId.Parse("projectile.enemy-blaster"),
                enemyProjectileRuntimePrefab);
            enemyProjectilePrefabs.Register(
                StableId.Parse("projectile.enemy-turret-shell"),
                enemyProjectileRuntimePrefab);

            EnemyAttackPatternTargetBindingV1 playerTarget =
                BuildPlayerTargetBinding();
            EnemyAttackPatternUnitySourceBindingV1 mobileSource =
                BuildMobileSourceBinding(mobileDefinition, playerTarget);
            EnemyAttackPatternUnitySourceBindingV1 turretSource =
                BuildTurretSourceBinding(turretDefinition, playerTarget);
            enemyPatternSources.Register(mobileSource);
            enemyPatternSources.Register(turretSource);

            var combatContext = new Level1EnemyAttackPatternCombatContextV1(
                controller.PlayerLiveAuthority,
                enemyPatternSources);
            enemyPatternHitRouter = new EnemyAttackPatternHitRouterV1(
                combatContext);
            enemyPatternRealizer =
                new EnemyAttackPatternUnityEmissionRealizerV1(
                    enemyPatternTime,
                    enemyPatternSources,
                    enemyPatternHitRouter);
            enemyPatternRealizer.AttachSource(
                mobileSource.SourceEntityStableId);
            enemyPatternRealizer.AttachSource(
                turretSource.SourceEntityStableId);
            enemyPatternScheduler = new EnemyAttackPatternLiveSchedulerV1(
                enemyPatternTime,
                enemyPatternRealizer);

            var mobileExecutor = new EnemyCommittedAttackPatternExecutorV1(
                BuildIdentity(
                    run,
                    mobileSource,
                    Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                    Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId),
                () => mobileSource.LifecycleGeneration,
                () => mobileSource.IsActive,
                mobileAttack,
                StableId.Parse("equipment-instance.enemy-mobile-blaster"),
                enemyPatternTime,
                enemyPatternScheduler);
            var turretExecutor = new EnemyCommittedAttackPatternExecutorV1(
                BuildIdentity(
                    run,
                    turretSource,
                    Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                    Level1AuthorableRoomDefinitionV1.TurretInstanceStableId),
                () => turretSource.LifecycleGeneration,
                () => turretSource.IsActive,
                turretAttack,
                StableId.Parse("equipment-instance.enemy-turret-shell"),
                enemyPatternTime,
                enemyPatternScheduler);
            var lineOfSight =
                new PhysicsEnemyAttackPatternLineOfSightV1();
            mobilePatternController =
                new EnemyAttackPatternProductionController2D(
                    mobileDefinition,
                    mobileAttack,
                    mobileSource,
                    playerTarget,
                    () => controller.MobileBlasterDroid.EnemyBody.position,
                    () => PlayerDirectionFrom(
                        controller.MobileBlasterDroid.EnemyBody.position),
                    () => controller.PlayerTransform.position,
                    enemyPatternTime,
                    lineOfSight,
                    mobileExecutor);
            turretPatternController =
                new EnemyAttackPatternProductionController2D(
                    turretDefinition,
                    turretAttack,
                    turretSource,
                    playerTarget,
                    () => controller.TurretPackage.EnemyBody.position,
                    () => controller.TurretPackage.CurrentFacing,
                    () => controller.PlayerTransform.position,
                    enemyPatternTime,
                    lineOfSight,
                    turretExecutor);

            DisableHistoricalEnemyProjectileExecution();
            enemyPatternObservedRunStableId = run.RunStableId;
            enemyPatternObservedPlayerGeneration = playerGeneration;
        }

        private void TeardownEnemyAttackPatterns()
        {
            if (enemyPatternRealizer != null)
            {
                enemyPatternRealizer.Dispose();
            }
            if (enemyPatternHitRouter != null)
            {
                enemyPatternHitRouter.Clear();
            }
            if (enemyPatternSources != null)
            {
                enemyPatternSources.Clear();
            }
            mobilePatternController = null;
            turretPatternController = null;
            enemyPatternScheduler = null;
            enemyPatternRealizer = null;
            enemyPatternHitRouter = null;
            enemyPatternSources = null;
            enemyPatternTime = null;
            enemyPatternObservedRunStableId = null;
            enemyPatternObservedPlayerGeneration = -1L;
        }
    }
}
