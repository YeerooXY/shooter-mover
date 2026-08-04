using NUnit.Framework;
using ShooterMover.UI.Game;
using ShooterMover.UI.Game.Enemies;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class EnemyFloorRulesTests
    {
        private GameObject floorOwner;
        private GameObject enemy;
        private Rigidbody2D enemyBody;
        private EnemyFloorRules rules;

        [SetUp]
        public void SetUp()
        {
            floorOwner = new GameObject("Enemy Floor Test Owner");
            Rigidbody2D playerBody = floorOwner.AddComponent<Rigidbody2D>();
            playerBody.bodyType = RigidbodyType2D.Kinematic;
            CircleCollider2D playerCollider =
                floorOwner.AddComponent<CircleCollider2D>();
            playerCollider.radius = 0.4f;
            PlayerFloorGuard playerFloor =
                floorOwner.AddComponent<PlayerFloorGuard>();
            playerFloor.Bind(playerBody, playerCollider);
            playerFloor.LoadFloor(
                new[]
                {
                    new Vector2Int(-1, 0),
                    new Vector2Int(1, 0),
                },
                new Vector2(-1f, 0f));

            enemy = new GameObject("Enemy Floor Rules Test");
            enemyBody = enemy.AddComponent<Rigidbody2D>();
            enemyBody.bodyType = RigidbodyType2D.Kinematic;
            CircleCollider2D enemyCollider =
                enemy.AddComponent<CircleCollider2D>();
            enemyCollider.radius = 0.4f;
            rules = enemy.AddComponent<EnemyFloorRules>();
            rules.Bind(enemyBody, enemyCollider);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(floorOwner);
        }

        [Test]
        public void EnemyCannotCrossMissingFloorGap()
        {
            enemyBody.position = new Vector2(-1f, 0f);
            rules.ApplyMovement();

            enemyBody.position = new Vector2(1f, 0f);
            rules.ApplyMovement();

            Assert.That(enemyBody.position.x, Is.LessThan(-0.89f));
            Assert.That(enemyBody.position.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void FloorRulesDoNotAddProjectileBlockingColliders()
        {
            Assert.That(enemy.GetComponents<Collider2D>().Length, Is.EqualTo(1));
        }
    }
}
