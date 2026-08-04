using System;
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.UI.Game.Enemies
{
    /// <summary>
    /// Applies the scene-local collision policy for compact enemies.
    /// Same-altitude enemies and authored walls remain solid. Flying and ground enemies
    /// ignore only each other's body colliders so those two groups may overlap.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompactEnemyCollisionPolicy : MonoBehaviour
    {
        private static readonly HashSet<CompactEnemyCollisionPolicy> active =
            new HashSet<CompactEnemyCollisionPolicy>();

        private CompactEnemy enemy;
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private bool configured;
        private bool flying;

        public bool IsConfigured { get { return configured; } }
        public bool IsFlying { get { return flying; } }
        public Rigidbody2D Body { get { return body; } }
        public Collider2D BodyCollider { get { return bodyCollider; } }

        public void Configure(CompactEnemy configuredEnemy)
        {
            if (configuredEnemy == null)
            {
                throw new ArgumentNullException(nameof(configuredEnemy));
            }
            if (configured)
            {
                if (!ReferenceEquals(enemy, configuredEnemy))
                {
                    throw new InvalidOperationException(
                        "compact-enemy-collision-policy-reconfigured");
                }
                return;
            }

            enemy = configuredEnemy;
            body = configuredEnemy.GetComponent<Rigidbody2D>()
                ?? throw new InvalidOperationException(
                    "compact-enemy-collision-body-missing");
            bodyCollider = configuredEnemy.GetComponent<Collider2D>()
                ?? throw new InvalidOperationException(
                    "compact-enemy-collision-collider-missing");
            flying = HasTag(configuredEnemy.Definition, "flying");

            // Dynamic bodies receive actual separation from static walls and other dynamic
            // enemy bodies. CompactEnemy continues to own movement through MovePosition.
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            configured = true;
            if (isActiveAndEnabled)
            {
                RegisterActive();
            }
        }

        private void OnEnable()
        {
            if (configured)
            {
                RegisterActive();
            }
        }

        private void OnDisable()
        {
            UnregisterActive();
        }

        private void OnDestroy()
        {
            UnregisterActive();
        }

        private void RegisterActive()
        {
            if (!configured || bodyCollider == null || !active.Add(this))
            {
                return;
            }

            foreach (CompactEnemyCollisionPolicy other in active)
            {
                if (other == null || ReferenceEquals(other, this)) continue;
                ApplyPair(this, other);
            }
        }

        private void UnregisterActive()
        {
            if (!active.Remove(this) || bodyCollider == null)
            {
                return;
            }

            foreach (CompactEnemyCollisionPolicy other in active)
            {
                if (other == null || other.bodyCollider == null) continue;
                Physics2D.IgnoreCollision(bodyCollider, other.bodyCollider, false);
            }
        }

        private static void ApplyPair(
            CompactEnemyCollisionPolicy first,
            CompactEnemyCollisionPolicy second)
        {
            if (first == null
                || second == null
                || first.bodyCollider == null
                || second.bodyCollider == null)
            {
                return;
            }

            Physics2D.IgnoreCollision(
                first.bodyCollider,
                second.bodyCollider,
                first.flying != second.flying);
        }

        private static bool HasTag(
            CompactEnemyDefinition definition,
            string expected)
        {
            if (definition == null
                || definition.tags == null
                || string.IsNullOrWhiteSpace(expected))
            {
                return false;
            }

            for (int index = 0; index < definition.tags.Length; index++)
            {
                if (string.Equals(
                        definition.tags[index],
                        expected,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public static class CompactEnemyCollisionRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            CompactEnemySceneFactory.RegisterCollisionPolicy(
                gameObject =>
                {
                    CompactEnemy enemy = gameObject.GetComponent<CompactEnemy>()
                        ?? throw new InvalidOperationException(
                            "compact-enemy-collision-runtime-missing");
                    CompactEnemyCollisionPolicy policy =
                        gameObject.GetComponent<CompactEnemyCollisionPolicy>()
                        ?? gameObject.AddComponent<CompactEnemyCollisionPolicy>();
                    policy.Configure(enemy);
                });
        }
    }
}
