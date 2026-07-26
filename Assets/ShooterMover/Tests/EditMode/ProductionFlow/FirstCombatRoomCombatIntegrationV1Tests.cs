using System;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UI.ProductionFlow;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.ProductionFlow
{
    public sealed class FirstCombatRoomCombatIntegrationV1Tests
    {
        [Test]
        public void KnownEnemyKineticChannelMapsExactly()
        {
            CombatChannel channel;
            bool mapped = FirstCombatRoomEnemyDamageChannelMapV1.TryMap(
                StableId.Parse("damage.kinetic"),
                out channel);

            Assert.That(mapped, Is.True);
            Assert.That(channel, Is.EqualTo(CombatChannel.Kinetic));
        }

        [Test]
        public void UnknownOrMissingEnemyChannelFailsClosed()
        {
            CombatChannel channel;
            bool unknownMapped = FirstCombatRoomEnemyDamageChannelMapV1.TryMap(
                StableId.Parse("damage.unmapped-test"),
                out channel);

            Assert.That(unknownMapped, Is.False);
            Assert.That(channel, Is.EqualTo(default(CombatChannel)));

            bool missingMapped = FirstCombatRoomEnemyDamageChannelMapV1.TryMap(
                null,
                out channel);

            Assert.That(missingMapped, Is.False);
            Assert.That(channel, Is.EqualTo(default(CombatChannel)));
        }

        [Test]
        public void DefeatTerminatesUncommittedProjectileIdempotently()
        {
            GameObject projectileObject = new GameObject(
                "Uncommitted Defeat Projectile Test Double");
            try
            {
                ProductionNormalProjectile2D projectile =
                    projectileObject.AddComponent<ProductionNormalProjectile2D>();

                ProductionNormalProjectileOwnerDefeatDispositionV1 first =
                    projectile.DisableForOwnerDefeat();
                ProductionNormalProjectileOwnerDefeatDispositionV1 replay =
                    projectile.DisableForOwnerDefeat();

                Assert.That(
                    first,
                    Is.EqualTo(
                        ProductionNormalProjectileOwnerDefeatDispositionV1
                            .UncommittedProjectileTerminated));
                Assert.That(
                    replay,
                    Is.EqualTo(
                        ProductionNormalProjectileOwnerDefeatDispositionV1
                            .AlreadyCompleted));
                Assert.That(projectile.IsOwnerDefeatShutdownRequested, Is.True);
                Assert.That(projectile.HasPendingEnemyImpactRetry, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(projectileObject);
            }
        }

        [Test]
        public void DefeatRetainsExactCommittedLethalEnemyImpactRetryState()
        {
            GameObject enemyObject = new GameObject(
                "Pending Retry Enemy Test Double");
            GameObject projectileObject = new GameObject(
                "Pending Retry Projectile Test Double");
            try
            {
                RoomEnemyActor2D enemy =
                    enemyObject.AddComponent<RoomEnemyActor2D>();
                ProductionNormalProjectile2D projectile =
                    projectileObject.AddComponent<ProductionNormalProjectile2D>();
                Rigidbody2D body = projectileObject.AddComponent<Rigidbody2D>();
                CircleCollider2D trigger =
                    projectileObject.AddComponent<CircleCollider2D>();
                body.simulated = true;
                trigger.enabled = true;

                var exactCommand = new EnemyRuntimeDamageCommandV1(
                    StableId.Parse("enemy-damage-operation.defeat-retry-test"),
                    StableId.Parse("actor.player-defeat-retry-test"),
                    StableId.Parse("participant.player-defeat-retry-test"),
                    StableId.Parse("enemy.defeat-retry-test"),
                    7L,
                    41L,
                    1,
                    500d);
#pragma warning disable SYSLIB0050
                var retainedLifecycleState =
                    (ProjectileLifecycleState)FormatterServices
                        .GetUninitializedObject(typeof(ProjectileLifecycleState));
#pragma warning restore SYSLIB0050
                const double exactOccurredAtSeconds = 47.125d;

                FieldInfo commandField = RequirePrivateField(
                    typeof(ProductionNormalProjectile2D),
                    "pendingDamageCommand");
                SetPrivateField(projectile, "configured", true);
                SetPrivateField(projectile, "launched", true);
                SetPrivateField(projectile, "state", retainedLifecycleState);
                SetPrivateField(projectile, "body", body);
                SetPrivateField(projectile, "trigger", trigger);
                SetPrivateField(projectile, "impactCommitted", true);
                SetPrivateField(projectile, "pendingImpactEnemy", enemy);
                commandField.SetValue(projectile, exactCommand);
                SetPrivateField(
                    projectile,
                    "pendingOccurredAtSeconds",
                    exactOccurredAtSeconds);

                int stopped =
                    ProductionPlayablePlayerWeaponDefeatShutdownV1
                        .DisableForDefeat(
                            null,
                            null,
                            projectileObject.scene);

                Assert.That(stopped, Is.EqualTo(1));
                Assert.That(projectile, Is.Not.Null);
                Assert.That(projectile.enabled, Is.True);
                Assert.That(projectileObject.activeSelf, Is.True);
                Assert.That(projectile.IsOwnerDefeatShutdownRequested, Is.True);
                Assert.That(projectile.HasPendingEnemyImpactRetry, Is.True);
                Assert.That(body.simulated, Is.False);
                Assert.That(trigger.enabled, Is.False);
                Assert.That(GetPrivateField<bool>(projectile, "configured"), Is.True);
                Assert.That(GetPrivateField<bool>(projectile, "launched"), Is.True);
                Assert.That(
                    GetPrivateField<ProjectileLifecycleState>(projectile, "state"),
                    Is.SameAs(retainedLifecycleState));
                Assert.That(
                    commandField.GetValue(projectile),
                    Is.SameAs(exactCommand));
                Assert.That(
                    GetPrivateField<double>(
                        projectile,
                        "pendingOccurredAtSeconds"),
                    Is.EqualTo(exactOccurredAtSeconds));
                Assert.That(
                    projectile.DisableForOwnerDefeat(),
                    Is.EqualTo(
                        ProductionNormalProjectileOwnerDefeatDispositionV1
                            .PendingEnemyImpactRetryRetained));
                Assert.That(body.simulated, Is.False);
                Assert.That(trigger.enabled, Is.False);
                Assert.That(GetPrivateField<bool>(projectile, "configured"), Is.True);
                Assert.That(GetPrivateField<bool>(projectile, "launched"), Is.True);
                Assert.That(
                    GetPrivateField<ProjectileLifecycleState>(projectile, "state"),
                    Is.SameAs(retainedLifecycleState));
                Assert.That(
                    commandField.GetValue(projectile),
                    Is.SameAs(exactCommand));
                Assert.That(
                    GetPrivateField<double>(
                        projectile,
                        "pendingOccurredAtSeconds"),
                    Is.EqualTo(exactOccurredAtSeconds));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(projectileObject);
                UnityEngine.Object.DestroyImmediate(enemyObject);
            }
        }

        private static void SetPrivateField<T>(
            object target,
            string fieldName,
            T value)
        {
            RequirePrivateField(target.GetType(), fieldName).SetValue(
                target,
                value);
        }

        private static T GetPrivateField<T>(
            object target,
            string fieldName)
        {
            return (T)RequirePrivateField(
                target.GetType(),
                fieldName).GetValue(target);
        }

        private static FieldInfo RequirePrivateField(
            Type type,
            string fieldName)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                field,
                Is.Not.Null,
                "Expected private field was not found: " + fieldName);
            return field;
        }
    }
}
