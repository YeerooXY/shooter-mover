#if UNITY_EDITOR
using System;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Enemies
{
    public sealed class EnemyTerminalCollider2DAdapterTests
    {
        private GameObject enemy;

        [TearDown]
        public void TearDown()
        {
            if (enemy != null)
            {
                UnityEngine.Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void DestroyedEnemy_DisablesHitboxAndRestartRestoresIt()
        {
            enemy = new GameObject("Generic Terminal Enemy Collider Test");
            CircleCollider2D hitbox = enemy.AddComponent<CircleCollider2D>();
            TerminalColliderTestAuthority authority =
                enemy.AddComponent<TerminalColliderTestAuthority>();
            authority.Initialize();
            EnemyTerminalCollider2DAdapter adapter =
                enemy.AddComponent<EnemyTerminalCollider2DAdapter>();
            adapter.Configure(authority, new Collider2D[] { hitbox });

            Assert.That(hitbox.enabled, Is.True);
            EnemyActorStepResult lethal = authority.Apply(
                EnemyActorCommand.Damage(
                    0L,
                    StableId.Parse("event.terminal-collider-lethal"),
                    StableId.Parse("actor.player-terminal-collider-test"),
                    1,
                    100d));
            Assert.That(lethal.State.IsDestroyed, Is.True);

            Assert.That(adapter.SyncNow(), Is.True);
            Assert.That(hitbox.enabled, Is.False);

            Assert.That(authority.Reset(), Is.True);
            Assert.That(adapter.SyncNow(), Is.True);
            Assert.That(hitbox.enabled, Is.True);
        }
    }

    public sealed class TerminalColliderTestAuthority : MonoBehaviour, IEnemyActor2DAuthority
    {
        private static readonly StableId ActorId =
            StableId.Parse("actor.terminal-collider-enemy");
        private static readonly StableId RoleId =
            StableId.Parse("enemy.terminal-collider-test");
        private EnemyActorState state;

        public void Initialize()
        {
            state = CreateState();
        }

        public bool TryReadState(out EnemyActorState current)
        {
            current = state;
            return current != null;
        }

        public EnemyActorStepResult Apply(EnemyActorCommand command)
        {
            if (state == null)
            {
                throw new InvalidOperationException("Test authority is not initialized.");
            }

            EnemyActorStepResult result = EnemyActorStepper.Step(state, new[] { command });
            state = result.State;
            return result;
        }

        public bool Reset()
        {
            state = CreateState();
            return true;
        }

        private static EnemyActorState CreateState()
        {
            return EnemyActorState.Create(
                ActorId,
                RoleId,
                10d,
                2,
                EnemyContactPolicy.Create(
                    EnemyContactMode.None,
                    0d,
                    0.5d,
                    0.02d,
                    4));
        }
    }
}
#endif
