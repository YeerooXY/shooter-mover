using System;
using ShooterMover.Application.Enemies.Catalog;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    public sealed partial class Stage1PlayableLoopCompositionV1
    {
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
                RoomRuntimeStableId,
                Level1AuthorableRoomDefinitionV1.EntryRoomStableId,
                Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId,
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
                RoomRuntimeStableId,
                Level1AuthorableRoomDefinitionV1.TerminalRoomStableId,
                Level1AuthorableRoomDefinitionV1.TurretInstanceStableId,
                controller.TurretPackage.gameObject,
                new[] { controller.TurretPackage.EnemyCollider },
                new[] { playerTarget },
                enemyProjectilePrefabs,
                () => checked(controller.TurretPackage.Generation + 1L),
                () => controller.TurretPackage.IsActive);
        }

        private static EnemyRuntimeIdentityV1 BuildIdentity(
            RunSessionAggregateV1 run,
            EnemyAttackPatternUnitySourceBindingV1 source,
            StableId roomStableId,
            StableId placementStableId)
        {
            if (run == null)
            {
                throw new ArgumentNullException(nameof(run));
            }
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (!source.HasCanonicalPlacementIdentity
                || source.RoomStableId != roomStableId
                || source.PlacementStableId != placementStableId)
            {
                throw new InvalidOperationException(
                    "The live enemy source binding and requested placement are split.");
            }
            return new EnemyRuntimeIdentityV1(
                source.SourceEntityStableId,
                source.SourceRunParticipantStableId,
                run.RunStableId,
                source.RoomRuntimeInstanceStableId,
                source.RoomStableId,
                source.PlacementStableId);
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
    }
}
