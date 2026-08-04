using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Game.Enemies
{
    [DisallowMultipleComponent]
    public sealed class CompactEnemyShot : MonoBehaviour
    {
        private static Texture2D pixelTexture;
        private static Sprite pixelSprite;

        private Vector2 direction;
        private Transform sourceRoot;
        private StableId sourceActorStableId;
        private StableId sourceParticipantStableId;
        private StableId eventStableId;
        private string damageType;
        private double damage;
        private float speed;
        private float remainingRange;
        private Rigidbody2D body;
        private CircleCollider2D trigger;
        private bool configured;
        private bool completed;

        public static CompactEnemyShot Spawn(
            Scene scene,
            Vector2 position,
            Vector2 launchDirection,
            CompactEnemyShotDefinition definition,
            double resolvedDamage,
            string resolvedDamageType,
            StableId sourceActor,
            StableId sourceParticipant,
            StableId eventId,
            Transform source)
        {
            if (definition == null
                || definition.delivery == null
                || sourceActor == null
                || sourceParticipant == null
                || eventId == null
                || source == null)
            {
                return null;
            }

            GameObject shotObject = new GameObject(
                "EnemyShot_" + definition.id);
            SceneManager.MoveGameObjectToScene(shotObject, scene);
            shotObject.SetActive(false);
            CompactEnemyShot shot =
                shotObject.AddComponent<CompactEnemyShot>();
            if (!shot.Configure(
                    position,
                    launchDirection,
                    definition,
                    resolvedDamage,
                    resolvedDamageType,
                    sourceActor,
                    sourceParticipant,
                    eventId,
                    source))
            {
                Destroy(shotObject);
                return null;
            }

            shotObject.SetActive(true);
            shot.Launch();
            return shot;
        }

        private bool Configure(
            Vector2 position,
            Vector2 launchDirection,
            CompactEnemyShotDefinition definition,
            double resolvedDamage,
            string resolvedDamageType,
            StableId sourceActor,
            StableId sourceParticipant,
            StableId eventId,
            Transform source)
        {
            if (configured
                || definition.delivery.speed <= 0d
                || definition.delivery.radius <= 0d
                || definition.delivery.range <= 0d
                || resolvedDamage <= 0d
                || launchDirection.sqrMagnitude < 0.000001f)
            {
                return false;
            }

            direction = launchDirection.normalized;
            sourceRoot = source;
            sourceActorStableId = sourceActor;
            sourceParticipantStableId = sourceParticipant;
            eventStableId = eventId;
            damageType = resolvedDamageType;
            damage = resolvedDamage;
            speed = (float)definition.delivery.speed;
            remainingRange = (float)definition.delivery.range;

            transform.position = new Vector3(position.x, position.y, 0f);
            SetFacing();
            EnsurePixelSprite();
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = pixelSprite;
            renderer.color = new Color(1f, 0.42f, 0.1f, 1f);
            renderer.sortingOrder = 80;
            float radius = Mathf.Max(0.02f, (float)definition.delivery.radius);
            visual.transform.localScale =
                new Vector3(radius * 3.5f, radius * 1.5f, 1f);

            body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.simulated = false;

            trigger = gameObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = radius;
            configured = true;
            return true;
        }

        private void Launch()
        {
            if (!configured || completed) return;
            body.position = transform.position;
            body.simulated = true;
        }

        private void FixedUpdate()
        {
            if (!configured || completed || body == null) return;
            float distance = Mathf.Min(
                speed * Time.fixedDeltaTime,
                remainingRange);
            remainingRange -= distance;
            body.MovePosition(body.position + direction * distance);
            if (remainingRange <= 0.0001f) Complete();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (completed || other == null || IsSource(other)) return;
            if (IsEnemyBody(other)) return;

            PlayerHUD receiver = other.GetComponentInParent<PlayerHUD>();
            if (receiver != null && receiver.IsBound && !receiver.IsDefeated)
            {
                Deliver(receiver);
                Complete();
                return;
            }

            if (!other.isTrigger) Complete();
        }

        private void Deliver(PlayerHUD receiver)
        {
            CombatChannel channel;
            if (!TryParseChannel(damageType, out channel)) return;
            DamageReceiverCommand command;
            string rejection;
            if (!PlayablePlayerDamageCommandFactory.TryCreateForCharacterContact(
                    receiver,
                    receiver.CharacterInstanceStableId,
                    eventStableId,
                    sourceActorStableId,
                    sourceParticipantStableId,
                    damage,
                    channel,
                    out command,
                    out rejection))
            {
                return;
            }

            receiver.ApplyDamage(command);
        }

        private bool IsSource(Collider2D other)
        {
            if (sourceRoot == null || other == null) return false;
            Transform candidate = other.transform;
            return candidate == sourceRoot || candidate.IsChildOf(sourceRoot);
        }

        private static bool IsEnemyBody(Collider2D other)
        {
            return other != null
                && other.GetComponentInParent<CompactEnemy>() != null;
        }

        private void Complete()
        {
            if (completed) return;
            completed = true;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }
            if (trigger != null) trigger.enabled = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void SetFacing()
        {
            float angle = Mathf.Atan2(direction.y, direction.x)
                * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static bool TryParseChannel(
            string value,
            out CombatChannel channel)
        {
            channel = default(CombatChannel);
            if (string.IsNullOrWhiteSpace(value)) return false;
            string normalized = char.ToUpperInvariant(value[0])
                + value.Substring(1);
            return Enum.TryParse(normalized, true, out channel)
                && Enum.IsDefined(typeof(CombatChannel), channel);
        }

        private static void EnsurePixelSprite()
        {
            if (pixelSprite != null) return;
            pixelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixelTexture.name = "Compact Enemy Shot Pixel";
            pixelTexture.SetPixel(0, 0, Color.white);
            pixelTexture.Apply(false, true);
            pixelSprite = Sprite.Create(
                pixelTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            pixelSprite.name = "Compact Enemy Shot Sprite";
        }

        private void OnDisable()
        {
            if (!completed && configured) Complete();
        }
    }
}
