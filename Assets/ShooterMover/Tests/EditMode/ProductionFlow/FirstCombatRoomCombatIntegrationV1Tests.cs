using System;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
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
        public void DefeatRetainsExactCommittedEnemyImpactRetryState()
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

                FieldInfo commandField = RequirePrivateField(
                    typeof(ProductionNormalProjectile2D),
                    "pendingDamageCommand");
#pragma warning disable SYSLIB0050
                object exactCommand = FormatterServices.GetUninitializedObject(
                    commandField.FieldType);
#pragma warning restore SYSLIB0050
                const double exactOccurredAtSeconds = 47.125d;

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
                Assert.That(projectileObject.activeSelf, Is.True);
                Assert.That(projectile.IsOwnerDefeatShutdownRequested, Is.True);
                Assert.That(projectile.HasPendingEnemyImpactRetry, Is.True);
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
