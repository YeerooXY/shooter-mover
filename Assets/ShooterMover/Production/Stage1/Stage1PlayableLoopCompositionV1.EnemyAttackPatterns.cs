using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Enemies.Catalog;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;
using ShooterMover.UI.ProductionFlow;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Production.Level1;
using UnityEngine;

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

        private RunSessionAuthorityV1 enemyPatternRunAuthority;
        private RunSessionAggregateV1 enemyPatternRun;
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
        private long enemyPatternObservedRestartGeneration = -1L;
        private long enemyPatternSimulationTick;
        private bool enemyPatternFailed;

        private void FixedUpdate()
        {
            if (!initialized
                || ending
                || controller == null
                || enemyPatternFailed)
            {
                return;
            }

            try
            {
                long currentRestart = controller.RestartGeneration;
                if (enemyPatternObservedRestartGeneration >= 0L
                    && enemyPatternObservedRestartGeneration != currentRestart)
                {
                    TeardownEnemyAttackPatterns();
                    enemyPatternObservedRestartGeneration = currentRestart;
                    return;
                }

                if (enemyPatternRun == null
                    || enemyPatternObservedRunStableId != runStableId)
                {
                    ComposeEnemyAttackPatterns();
                }

                if (enemyPatternSimulationTick == long.MaxValue)
                {
                    throw new InvalidOperationException(
                        "Enemy attack-pattern simulation tick overflowed.");
                }
                enemyPatternSimulationTick++;
                RunSessionTimeAdvanceResultV1 timeResult =
                    enemyPatternTime.AdvanceTo(enemyPatternSimulationTick);
                if (timeResult == null || !timeResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Run Session rejected enemy attack-pattern time: "
                        + (timeResult == null
                            ? "result-null"
                            : timeResult.RejectionCode));
                }

                mobilePatternController.Tick();
                turretPatternController.Tick();
                enemyPatternScheduler.Tick();
                enemyPatternRealizer.Tick();
            }
            catch (Exception exception)
            {
                enemyPatternFailed = true;
                diagnostic = "Enemy attack-pattern integration failed: "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message;
                Debug.LogException(exception, this);
                TeardownEnemyAttackPatterns();
            }
        }

        private void OnDisable()
        {
            TeardownEnemyAttackPatterns();
        }

        private void ComposeEnemyAttackPatterns()
        {
            TeardownEnemyAttackPatterns();
            if (runStableId == null
                || missionResults == null
                || rooms == null
                || effectEmitter == null
                || controller.PlayerLiveAuthority == null
                || !controller.PlayerLiveAuthority.IsInitialized)
            {
                throw new InvalidOperationException(
                    "Stage 1 attack-pattern prerequisites are unavailable.");
            }

            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 selectedProfile;
            ShooterMover.Application.Persistence.Composition
                .CharacterCompositionCoordinatorV1 characterComposition;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out graph,
                    out selectedProfile,
                    out characterComposition)
                || graph == null
                || selectedProfile == null
                || characterComposition == null)
            {
                throw new InvalidOperationException(
                    "The selected account-backed character graph is unavailable.");
            }

            var missionResultPort = new ExistingMissionResultRunPortV1(
                missionResults,
                graph.LoadoutRuntime.Holdings,
                graph.StrongboxAuthority.ExportSnapshot);
            var runtimePortFactory = new Stage1RunSessionRuntimePortFactoryV1(
                controller.PlayerLiveAuthority,
                rooms,
                missionResultPort,
                effectEmitter.ClearEmittedEffects);
            var startSource = new ProductionCharacterRunSessionStartSourceV1(
                characterComposition,
                new Stage1ProductionRunStatInputResolverV1(
                    controller.PlayerLiveAuthority),
                runtimePortFactory);
            enemyPatternRunAuthority = new RunSessionAuthorityV1(startSource);
            var startCommand = new StartRunSessionCommandV1(
                StableId.Create(
                    "operation",
                    "stage1-enemy-pattern-run-g"
                        + controller.RestartGeneration.ToString(
                            CultureInfo.InvariantCulture)),
                runStableId,
                string.Empty,
                graph.Character.CharacterInstanceStableId,
                graph.Character.Revision,
                graph.Character.Fingerprint,
                Level1AuthorableRoomDefinitionV1.LayoutStableId,
                StableId.Parse("difficulty.normal"),
                controller.RestartGeneration + 1L,
                0L,
                RunSessionFingerprintV1.Hash(
                    "stage1-enemy-pattern-no-active-events-v1"));
            RunSessionStartResultV1 started =
                enemyPatternRunAuthority.Start(startCommand);
            if (started == null
                || (started.Status != RunSessionStartStatusV1.Started
                    && started.Status != RunSessionStartStatusV1.ExactReplay)
                || !enemyPatternRunAuthority.TryGetRun(
                    runStableId,
                    out enemyPatternRun)
                || enemyPatternRun == null)
            {
                throw new InvalidOperationException(
                    "The authoritative Stage 1 Run Session could not start: "
                    + (started == null
                        ? "result-null"
                        : started.RejectionCode));
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
                enemyPatternRun,
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
                BuildMobileSourceBinding(
                    mobileDefinition,
                    playerTarget);
            EnemyAttackPatternUnitySourceBindingV1 turretSource =
                BuildTurretSourceBinding(
                    turretDefinition,
                    playerTarget);
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
            enemyPatternSimulationTick = 0L;
            enemyPatternObservedRunStableId = runStableId;
            enemyPatternObservedRestartGeneration =
                controller.RestartGeneration;
        }

        private EnemyAttackPatternTargetBindingV1 BuildPlayerTargetBinding()
        {
            Collider2D[] colliders = controller.PlayerTransform
                .GetComponentsInChildren<Collider2D>(true);
            return new EnemyAttackPatternTargetBindingV1(
                controller.PlayerLiveAuthority.ExportSnapshot()
                    .Player.ActorInstanceId,
                colliders,
                () => controller.PlayerLiveAuthority.ExportSnapshot()
                    .Player.LifecycleGeneration,
                () => controller.PlayerLiveAuthority.IsInitialized
                    && controller.PlayerLiveAuthority.IsPlayerGameplayActive
                    && controller.PlayerLiveAuthority.ExportSnapshot()
                        .Player.IsAlive);
        }

        private EnemyAttackPatternUnitySourceBindingV1
            BuildMobileSourceBinding(
                EnemyDefinitionV1 definition,
                EnemyAttackPatternTargetBindingV1 playerTarget)
        {
            if (controller.MobileBlasterDroid.EnemyTarget == null
                || controller.MobileBlasterDroid.EnemyBody == null
                || controller.MobileBlasterDroid.EnemyCollider == null)
            {
                throw new InvalidOperationException(
                    "The mobile droid typed attack surfaces are unavailable.");
            }
            return new EnemyAttackPatternUnitySourceBindingV1(
                controller.MobileBlasterDroid.EnemyTarget.TargetId,
                MobileParticipantStableId,
                definition.FactionId ?? EnemyFactionStableId,
                controller.MobileBlasterDroid.gameObject,
                new[] { controller.MobileBlasterDroid.EnemyCollider },
                new[] { playerTarget },
                enemyProjectilePrefabs,
                () => checked(controller.MobileBlasterDroid.Generation + 1L),
                () => controller.MobileBlasterDroid.IsActive);
        }

        private EnemyAttackPatternUnitySourceBindingV1
            BuildTurretSourceBinding(
                EnemyDefinitionV1 definition,
                EnemyAttackPatternTargetBindingV1 playerTarget)
        {
            if (controller.TurretPackage.TargetAdapter == null
                || controller.TurretPackage.EnemyBody == null
                || controller.TurretPackage.EnemyCollider == null)
            {
                throw new InvalidOperationException(
                    "The turret typed attack surfaces are unavailable.");
            }
            return new EnemyAttackPatternUnitySourceBindingV1(
                controller.TurretPackage.TargetAdapter.TargetId,
                TurretParticipantStableId,
                definition.FactionId ?? EnemyFactionStableId,
                controller.TurretPackage.gameObject,
                new[] { controller.TurretPackage.EnemyCollider },
                new[] { playerTarget },
                enemyProjectilePrefabs,
                () => checked(controller.TurretPackage.Generation + 1L),
                () => controller.TurretPackage.IsActive);
        }

        private EnemyRuntimeIdentityV1 BuildIdentity(
            EnemyAttackPatternUnitySourceBindingV1 source,
            StableId roomStableId,
            StableId placementStableId)
        {
            return new EnemyRuntimeIdentityV1(
                source.SourceEntityStableId,
                source.SourceRunParticipantStableId,
                enemyPatternRun.RunStableId,
                RoomRuntimeStableId,
                roomStableId,
                placementStableId);
        }

        private void EnsureEnemyProjectileRuntimePrefab()
        {
            if (enemyProjectileRuntimePrefab != null)
            {
                return;
            }
            var projectileObject = new GameObject(
                "SchemaV2EnemyProjectileRuntimePrefab");
            projectileObject.transform.SetParent(transform, false);
            projectileObject.transform.position =
                new Vector3(10000f, 10000f, 0f);
            Rigidbody2D body = projectileObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.simulated = false;
            CircleCollider2D collider =
                projectileObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.enabled = false;
            SpriteRenderer renderer =
                projectileObject.AddComponent<SpriteRenderer>();
            Texture2D texture = new Texture2D(
                2,
                4,
                TextureFormat.RGBA32,
                false);
            texture.name = "SchemaV2EnemyProjectileTexture";
            Color[] pixels = new Color[8];
            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = Color.white;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 4f),
                new Vector2(0.5f, 0.5f),
                32f);
            sprite.name = "SchemaV2EnemyProjectileSprite";
            renderer.sprite = sprite;
            renderer.sortingOrder = 25;
            enemyProjectileRuntimePrefab =
                projectileObject.AddComponent<BoundedProjectile2D>();
            runtimeAssets.Add(projectileObject);
            runtimeAssets.Add(sprite);
            runtimeAssets.Add(texture);
        }

        private static EnemyAttackCapabilityDescriptorV1
            RequireSingleShootingAttack(EnemyDefinitionV1 definition)
        {
            EnemyAttackCapabilityDescriptorV1 resolved = null;
            for (int index = 0; index < definition.Attacks.Count; index++)
            {
                EnemyAttackCapabilityDescriptorV1 candidate =
                    definition.Attacks[index];
                if (candidate.ShootingPattern == null
                    || candidate.ProjectilePayload == null)
                {
                    continue;
                }
                if (resolved != null)
                {
                    throw new InvalidOperationException(
                        "Production shooter definitions require one live shooting attack: "
                        + definition.DefinitionId);
                }
                resolved = candidate;
            }
            return resolved ?? throw new InvalidOperationException(
                "Production shooter definition has no schema-v2 shooting attack: "
                + definition.DefinitionId);
        }

        private static EnemyCatalogV1 LoadProductionEnemyCatalog()
        {
            TextAsset source = Resources.Load<TextAsset>(
                EnemyCatalogResourcePath);
            if (source == null)
            {
                throw new InvalidOperationException(
                    "The schema-v2 production enemy catalog resource is missing.");
            }
            EnemyCatalogImportResultV1 imported =
                EnemyCatalogJsonImporterV1.Import(
                    source.text,
                    BuildProductionEnemyCatalogRegistry());
            if (imported == null || !imported.IsValid)
            {
                string issue = imported == null
                    || imported.Issues.Count == 0
                    ? "catalog-import-result-invalid"
                    : imported.Issues[0].ToString();
                throw new InvalidOperationException(
                    "The schema-v2 production enemy catalog is invalid: "
                    + issue);
            }
            if (imported.Catalog.SchemaVersion != 2)
            {
                throw new InvalidOperationException(
                    "Production enemy attacks must use schema v2.");
            }
            return imported.Catalog;
        }

        private static EnemyCatalogRegistryV1
            BuildProductionEnemyCatalogRegistry()
        {
            return new EnemyCatalogRegistryV1(
                Ids(
                    "enemy-movement.mobile-positioning",
                    "enemy-movement.pursuit",
                    "enemy-movement.stationary"),
                Ids(
                    "enemy-decision.ranged-standard",
                    "enemy-decision.pounce-standard",
                    "enemy-decision.turret-standard",
                    "enemy-decision.contact-standard",
                    "enemy-decision.multi-attack-standard"),
                new[]
                {
                    AttackRegistration(
                        "enemy-attack.ranged-projectile",
                        EnemyAttackParameterKindsV1.Projectile),
                    AttackRegistration(
                        "enemy-attack.pounce",
                        EnemyAttackParameterKindsV1.Melee),
                    AttackRegistration(
                        "enemy-attack.projectile-area",
                        EnemyAttackParameterKindsV1.Projectile
                            | EnemyAttackParameterKindsV1.Area),
                    AttackRegistration(
                        "enemy-attack.contact",
                        EnemyAttackParameterKindsV1.Melee),
                },
                Ids(
                    "enemy-special.locked-commitment",
                    "enemy-special.rotating-aim"),
                Ids(
                    "presentation.enemy-mobile-blaster-droid",
                    "presentation.enemy-ram-pouncer",
                    "presentation.enemy-blaster-turret",
                    "presentation.enemy-pursuer-drone",
                    "presentation.enemy-hybrid-sentinel"),
                Ids(
                    "projectile.enemy-blaster",
                    "projectile.enemy-turret-shell"),
                Ids(
                    "damage.kinetic",
                    "damage.impact",
                    "damage.thermal"),
                Ids(
                    "xp.enemy-standard",
                    "xp.enemy-light",
                    "xp.enemy-turret"),
                Ids(
                    "drop.enemy-common",
                    "drop.enemy-none",
                    "drop.enemy-turret"));
        }

        private static EnemyAttackCapabilityRegistrationV1
            AttackRegistration(
                string capabilityId,
                EnemyAttackParameterKindsV1 parameters)
        {
            return new EnemyAttackCapabilityRegistrationV1(
                StableId.Parse(capabilityId),
                parameters,
                parameters);
        }

        private static StableId[] Ids(params string[] values)
        {
            var result = new StableId[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                result[index] = StableId.Parse(values[index]);
            }
            return result;
        }

        private Vector2 PlayerDirectionFrom(Vector2 origin)
        {
            Vector2 delta = (Vector2)controller.PlayerTransform.position
                - origin;
            return delta.sqrMagnitude <= 0.000001f
                ? Vector2.right
                : delta.normalized;
        }

        private void DisableHistoricalEnemyProjectileExecution()
        {
            if (controller.MobileBlasterDroid.ProjectileAdapter != null
                && !controller.MobileBlasterDroid.ProjectileAdapter.IsDisposed)
            {
                controller.MobileBlasterDroid.ProjectileAdapter.Dispose();
            }
            if (controller.TurretPackage.ProjectileAdapter != null
                && !controller.TurretPackage.ProjectileAdapter.IsDisposed)
            {
                controller.TurretPackage.ProjectileAdapter.Dispose();
            }
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
            enemyPatternRun = null;
            enemyPatternRunAuthority = null;
            enemyPatternObservedRunStableId = null;
            enemyPatternSimulationTick = 0L;
        }
    }
}
