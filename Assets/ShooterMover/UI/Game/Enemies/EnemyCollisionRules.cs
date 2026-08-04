using System;
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.UI.Game.Enemies
{
    /// <summary>
    /// Keeps enemies solid against authored walls and enemies at the same altitude.
    /// Ground and flying enemies ignore only each other's body colliders.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyCollisionRules : MonoBehaviour
    {
        private static readonly HashSet<EnemyCollisionRules> active =
            new HashSet<EnemyCollisionRules>();

        private CompactEnemy enemy;
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private bool ready;
        private bool flying;

        public bool IsReady { get { return ready; } }
        public bool IsFlying { get { return flying; } }
        public Rigidbody2D Body { get { return body; } }
        public Collider2D BodyCollider { get { return bodyCollider; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveRules()
        {
            active.Clear();
        }

        public void Bind(CompactEnemy target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (ready)
            {
                if (!ReferenceEquals(enemy, target))
                {
                    throw new InvalidOperationException(
                        "enemy-collision-rules-rebound");
                }
                return;
            }

            enemy = target;
            body = target.GetComponent<Rigidbody2D>()
                ?? throw new InvalidOperationException(
                    "enemy-collision-body-missing");
            bodyCollider = target.GetComponent<Collider2D>()
                ?? throw new InvalidOperationException(
                    "enemy-collision-collider-missing");
            CircleCollider2D floorCollider =
                target.GetComponent<CircleCollider2D>()
                ?? throw new InvalidOperationException(
                    "enemy-floor-circle-collider-missing");
            flying = ReadFlying(target.Definition);

            // Dynamic bodies receive separation from static walls and other dynamic enemy
            // bodies. The enemy component continues to own movement through MovePosition.
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            EnemyFloorRules floorRules =
                target.GetComponent<EnemyFloorRules>()
                ?? target.gameObject.AddComponent<EnemyFloorRules>();
            floorRules.Bind(body, floorCollider);

            ready = true;
            if (isActiveAndEnabled)
            {
                AddActive();
            }
        }

        private void OnEnable()
        {
            if (ready)
            {
                AddActive();
            }
        }

        private void OnDisable()
        {
            RemoveActive();
        }

        private void OnDestroy()
        {
            RemoveActive();
        }

        private void AddActive()
        {
            if (!ready || bodyCollider == null || !active.Add(this))
            {
                return;
            }

            foreach (EnemyCollisionRules other in active)
            {
                if (other == null || ReferenceEquals(other, this)) continue;
                ApplyPair(this, other);
            }
        }

        private void RemoveActive()
        {
            if (!active.Remove(this) || bodyCollider == null)
            {
                return;
            }

            foreach (EnemyCollisionRules other in active)
            {
                if (other == null || other.bodyCollider == null) continue;
                Physics2D.IgnoreCollision(bodyCollider, other.bodyCollider, false);
            }
        }

        private static void ApplyPair(
            EnemyCollisionRules first,
            EnemyCollisionRules second)
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

        private static bool ReadFlying(CompactEnemyDefinition definition)
        {
            if (definition == null)
            {
                throw new InvalidOperationException(
                    "enemy-collision-definition-missing");
            }

            bool ground = HasTag(definition, "ground");
            bool flyingTag = HasTag(definition, "flying");
            if (ground == flyingTag)
            {
                throw new InvalidOperationException(
                    ground
                        ? "enemy-altitude-tags-conflict"
                        : "enemy-altitude-tag-missing");
            }
            return flyingTag;
        }

        private static bool HasTag(
            CompactEnemyDefinition definition,
            string expected)
        {
            if (definition.tags == null)
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

    public static class EnemyCollisionSetup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            EnemySpawner.SetCollisionRules(
                gameObject =>
                {
                    CompactEnemy enemy = gameObject.GetComponent<CompactEnemy>()
                        ?? throw new InvalidOperationException(
                            "enemy-collision-runtime-missing");
                    EnemyCollisionRules rules =
                        gameObject.GetComponent<EnemyCollisionRules>()
                        ?? gameObject.AddComponent<EnemyCollisionRules>();
                    rules.Bind(enemy);
                });
        }
    }
}
