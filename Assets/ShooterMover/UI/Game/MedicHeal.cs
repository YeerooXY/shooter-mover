using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.GameplayEntities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Game
{
    [DisallowMultipleComponent]
    public sealed class MedicHeal : MonoBehaviour
    {
        public const float CooldownSeconds = 5f;
        public const float MaximumThrowRange = 2.5f;

        private PlayerHUD health;
        private Camera aimCamera;
        private float nextReadyAt;
        private int healingRank;
        private int healAmount;
        private bool bound;

        public int HealingRank { get { return healingRank; } }
        public int HealAmount { get { return healAmount; } }
        public float CooldownRemaining
        {
            get { return Mathf.Max(0f, nextReadyAt - Time.time); }
        }

        public void Bind(
            PlayerHUD configuredHealth,
            RankedSkillAllocationSnapshot allocation)
        {
            if (bound)
            {
                throw new InvalidOperationException(
                    "medic-heal-duplicate-binding");
            }
            health = configuredHealth
                ?? throw new ArgumentNullException(nameof(configuredHealth));
            if (!health.IsBound)
            {
                throw new InvalidOperationException(
                    "medic-heal-health-unbound");
            }
            if (allocation == null)
            {
                throw new ArgumentNullException(nameof(allocation));
            }
            if (!IsMedic(allocation.ClassId))
            {
                throw new InvalidOperationException(
                    "medic-heal-class-required");
            }

            healingRank = allocation.RankOf(MedicHealing.SkillId);
            healAmount = MedicHealing.HealthAtRank(healingRank);
            aimCamera = Camera.main;
            if (aimCamera == null)
            {
                aimCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            }
            bound = true;
        }

        private void Update()
        {
            if (!bound
                || health == null
                || health.IsDefeated
                || Time.time < nextReadyAt)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.qKey.wasPressedThisFrame)
            {
                return;
            }

            ThrowMedPack(ReadAimPosition());
        }

        private Vector2 ReadAimPosition()
        {
            Vector2 origin = transform.position;
            Mouse mouse = Mouse.current;
            if (mouse == null || aimCamera == null)
            {
                return origin + Vector2.up * MaximumThrowRange;
            }

            Vector2 screen = mouse.position.ReadValue();
            float depth = Mathf.Abs(
                aimCamera.transform.position.z - transform.position.z);
            Vector3 world = aimCamera.ScreenToWorldPoint(
                new Vector3(screen.x, screen.y, depth));
            return new Vector2(world.x, world.y);
        }

        private void ThrowMedPack(Vector2 targetPosition)
        {
            Vector2 start = transform.position;
            Vector2 landing = ClampLandingPoint(
                start,
                targetPosition,
                MaximumThrowRange);

            GameObject packObject = new GameObject("Medic Med Pack");
            SceneManager.MoveGameObjectToScene(packObject, gameObject.scene);
            MedPack pack = packObject.AddComponent<MedPack>();
            pack.Initialize(health, healAmount, start, landing);
            nextReadyAt = Time.time + CooldownSeconds;
        }

        private void OnGUI()
        {
            if (!bound) return;

            float remaining = CooldownRemaining;
            string text = "MED PACK +" + healAmount + " HP  ";
            text += remaining > 0f
                ? remaining.ToString("0.0") + "s"
                : "READY [Q]";
            GUI.Label(
                new Rect(16f, Screen.height - 78f, 360f, 24f),
                text);
        }

        public static bool IsMedic(string classId)
        {
            return !string.IsNullOrWhiteSpace(classId)
                && classId.IndexOf(
                    "medic",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static Vector2 ClampLandingPoint(
            Vector2 origin,
            Vector2 requested,
            float maximumRange)
        {
            if (float.IsNaN(maximumRange)
                || float.IsInfinity(maximumRange)
                || maximumRange <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRange));
            }

            Vector2 offset = requested - origin;
            float maximumSquared = maximumRange * maximumRange;
            if (offset.sqrMagnitude <= maximumSquared)
            {
                return requested;
            }

            return origin + offset.normalized * maximumRange;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MedPack : MonoBehaviour
    {
        public const float PickupDelaySeconds = 0.5f;
        public const float LifetimeSeconds = 20f;

        private const float ThrowSeconds = 0.25f;
        private const float ThrowArcHeight = 0.35f;
        private const float PickupRadius = 0.34f;

        private static Sprite pixelSprite;

        private StableId sourceActorId;
        private StableId sourceRunParticipantId;
        private Vector2 throwStart;
        private Vector2 landingPosition;
        private Transform visual;
        private CircleCollider2D pickup;
        private float throwStartedAt;
        private float pickupStartsAt;
        private float expiresAt;
        private int healAmount;
        private bool landed;
        private bool consumed;

        public void Initialize(
            PlayerHUD source,
            int configuredHealAmount,
            Vector2 start,
            Vector2 landing)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (!source.IsBound)
            {
                throw new InvalidOperationException(
                    "med-pack-source-unbound");
            }
            if (configuredHealAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredHealAmount));
            }

            GameplayEntityIdentity identity = source.Identity;
            sourceActorId = identity.EntityInstanceId;
            sourceRunParticipantId = identity.Ownership.RunParticipantId;
            healAmount = configuredHealAmount;
            throwStart = start;
            landingPosition = landing;
            throwStartedAt = Time.time;
            transform.position = start;

            BuildVisual();
            pickup = gameObject.AddComponent<CircleCollider2D>();
            pickup.isTrigger = true;
            pickup.radius = PickupRadius;
            pickup.enabled = false;
        }

        private void Update()
        {
            float now = Time.time;
            if (!landed)
            {
                float progress = Mathf.Clamp01(
                    (now - throwStartedAt) / ThrowSeconds);
                transform.position = Vector2.Lerp(
                    throwStart,
                    landingPosition,
                    progress);
                if (visual != null)
                {
                    visual.localPosition = new Vector3(
                        0f,
                        Mathf.Sin(progress * Mathf.PI) * ThrowArcHeight,
                        0f);
                }

                if (progress >= 1f)
                {
                    Land(now);
                }
                return;
            }

            if (now >= expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            if (!pickup.enabled && now >= pickupStartsAt)
            {
                pickup.enabled = true;
            }
        }

        private void Land(float now)
        {
            landed = true;
            transform.position = landingPosition;
            if (visual != null)
            {
                visual.localPosition = Vector3.zero;
            }
            pickupStartsAt = now + PickupDelaySeconds;
            expiresAt = now + LifetimeSeconds;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (consumed
                || !landed
                || pickup == null
                || !pickup.enabled
                || other == null)
            {
                return;
            }

            PlayerHUD target = other.GetComponentInParent<PlayerHUD>();
            if (target == null
                || !target.IsBound
                || target.IsDefeated
                || target.CurrentHealth >= target.MaximumHealth)
            {
                return;
            }

            PlayerActorHealingResult result = target.ApplyHealing(
                new PlayerActorHealingCommand(
                    StableId.Create(
                        "operation",
                        "med-pack-" + Guid.NewGuid().ToString("N")),
                    sourceActorId,
                    sourceRunParticipantId,
                    target.Identity.EntityInstanceId,
                    healAmount,
                    target.LifecycleGeneration));
            if (result == null || !result.StateChanged)
            {
                return;
            }

            consumed = true;
            Destroy(gameObject);
        }

        private void BuildVisual()
        {
            GameObject visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(transform, false);
            visual = visualObject.transform;

            SpriteRenderer body = visualObject.AddComponent<SpriteRenderer>();
            body.sprite = GetPixelSprite();
            body.color = new Color(0.12f, 0.8f, 0.32f, 1f);
            body.sortingOrder = 40;
            visualObject.transform.localScale = new Vector3(0.48f, 0.48f, 1f);

            AddCrossPart(
                visualObject.transform,
                "Cross Horizontal",
                new Vector3(0.68f, 0.2f, 1f));
            AddCrossPart(
                visualObject.transform,
                "Cross Vertical",
                new Vector3(0.2f, 0.68f, 1f));
        }

        private static void AddCrossPart(
            Transform parent,
            string name,
            Vector3 scale)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localScale = scale;
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPixelSprite();
            renderer.color = Color.white;
            renderer.sortingOrder = 41;
        }

        private static Sprite GetPixelSprite()
        {
            if (pixelSprite != null)
            {
                return pixelSprite;
            }

            var texture = new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false);
            texture.name = "Medic Med Pack Pixel";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;

            pixelSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            pixelSprite.name = "Medic Med Pack Pixel";
            pixelSprite.hideFlags = HideFlags.HideAndDontSave;
            return pixelSprite;
        }
    }
}
