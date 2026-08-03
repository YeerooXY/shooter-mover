using System;
using System.Collections;
using System.Collections.Generic;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UI.Game;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    /// <summary>
    /// One scene-local enemy life backed directly by a schema-1 compact definition.
    /// Room completion remains owned by LevelRooms and player health remains owned by PlayerHUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompactEnemy : Damageable
    {
        private static Texture2D pixelTexture;
        private static Sprite pixelSprite;

        private readonly Dictionary<string, Transform> mounts =
            new Dictionary<string, Transform>(StringComparer.Ordinal);

        private LevelRooms roomOwner;
        private StableId roomStableId;
        private StableId placementStableId;
        private StableId participantStableId;
        private CompactEnemyDefinition definition;
        private CompactEnemyResolvedStats stats;
        private Rigidbody2D body;
        private CircleCollider2D bodyCollider;
        private SpriteRenderer bodyRenderer;
        private PlayerHUD player;
        private Coroutine attackRoutine;
        private Coroutine hitFlashRoutine;
        private double currentHealth;
        private float nextTargetSearchAt;
        private float nextAttackAt;
        private float nextContactAt;
        private float nextWanderAt;
        private Vector2 wanderDirection = Vector2.right;
        private long lifecycleGeneration;
        private int eventSequence;
        private bool configured;
        private bool defeated;

        public override StableId DamageableStableId
        {
            get { return placementStableId; }
        }

        public override long DamageableLifecycleGeneration
        {
            get { return lifecycleGeneration; }
        }

        public override bool CanTakeDamage
        {
            get { return configured && !defeated && currentHealth > 0d; }
        }

        public double CurrentHealth { get { return currentHealth; } }
        public double MaximumHealth { get { return stats.MaximumHealth; } }
        public int EnemyLevel { get { return stats.Level; } }
        public CompactEnemyDefinition Definition { get { return definition; } }

        public void Configure(
            LevelRooms configuredRoomOwner,
            StableId configuredRoomStableId,
            RoomPlacedEntityDefinition placement,
            int enemyLevel = 1)
        {
            if (configured) throw new InvalidOperationException(
                "compact-enemy-duplicate-configuration");
            roomOwner = configuredRoomOwner
                ?? throw new ArgumentNullException(nameof(configuredRoomOwner));
            roomStableId = configuredRoomStableId
                ?? throw new ArgumentNullException(nameof(configuredRoomStableId));
            if (placement == null) throw new ArgumentNullException(nameof(placement));
            if (placement.PlacementKind != RoomLivePlacementKind.Enemy)
            {
                throw new InvalidOperationException(
                    "compact-enemy-placement-kind-invalid");
            }

            CompactEnemyDefinition resolved;
            if (!CompactEnemyCatalog.TryResolve(
                    placement.DefinitionStableId,
                    out resolved))
            {
                throw new InvalidOperationException(
                    "compact-enemy-definition-missing:"
                    + placement.DefinitionStableId);
            }

            placementStableId = placement.InstanceStableId;
            definition = resolved;
            stats = CompactEnemyCatalog.ResolveStats(definition, enemyLevel);
            currentHealth = stats.MaximumHealth;
            lifecycleGeneration = Math.Max(1L, roomOwner.PresentationRevision);
            participantStableId = StableId.Create(
                "participant",
                "enemy-" + HashToken(
                    placementStableId + "|" + lifecycleGeneration));

            ConfigureBody();
            ConfigureMounts();
            configured = true;
        }

        public override void TakeHit(Hit hit)
        {
            if (!CanTakeDamage || hit == null) return;
            if (hit.TargetEntityStableId != placementStableId
                || hit.TargetLifecycleGeneration != lifecycleGeneration)
            {
                return;
            }

            currentHealth = Math.Max(0d, currentHealth - hit.Amount);
            FlashHit();
            if (currentHealth <= 0d) Defeat();
        }

        private void Update()
        {
            if (!configured || defeated) return;
            ResolvePlayer();
            if (player == null || !player.IsBound || player.IsDefeated) return;
            if (attackRoutine != null || Time.time < nextAttackAt) return;

            CompactEnemyAttack attack = SelectShotAttack();
            if (attack == null) return;
            attackRoutine = StartCoroutine(RunShotAttack(attack));
        }

        private void FixedUpdate()
        {
            if (!configured || defeated || body == null) return;
            ResolvePlayer();
            Vector2 movement = ResolveMovement();
            if (movement.sqrMagnitude < 0.000001f)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            movement.Normalize();
            body.MovePosition(
                body.position
                + movement * (float)definition.movement.speed
                * Time.fixedDeltaTime);
            SetFacing(movement);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!configured || defeated || collision == null
                || Time.time < nextContactAt)
            {
                return;
            }

            PlayerHUD receiver = collision.collider == null
                ? null
                : collision.collider.GetComponentInParent<PlayerHUD>();
            if (receiver == null || !receiver.IsBound || receiver.IsDefeated) return;

            CompactEnemyAttack attack = SelectContactAttack();
            if (attack == null) return;
            double amount = DirectDamage(attack) * stats.DamageMultiplier;
            if (amount <= 0d) return;

            nextContactAt = Time.time + Mathf.Max(0.05f, (float)attack.cooldown);
            DeliverPlayerDamage(
                receiver,
                attack,
                amount,
                attack.damage == null || attack.damage.Length == 0
                    ? "impact"
                    : attack.damage[0].type);
            if (string.Equals(attack.kind, "suicide", StringComparison.Ordinal))
            {
                Defeat();
            }
        }

        private CompactEnemyAttack SelectShotAttack()
        {
            if (definition.attacks == null || player == null) return null;
            float distance = Vector2.Distance(
                transform.position,
                player.transform.position);
            for (int index = 0; index < definition.attacks.Length; index++)
            {
                CompactEnemyAttack attack = definition.attacks[index];
                if (attack == null
                    || !string.Equals(attack.kind, "shot", StringComparison.Ordinal)
                    || attack.range == null
                    || distance < attack.range.min
                    || distance > attack.range.max
                    || string.IsNullOrWhiteSpace(attack.shot))
                {
                    continue;
                }

                CompactEnemyShotDefinition ignored;
                if (CompactEnemyCatalog.TryResolveShot(attack.shot, out ignored))
                {
                    return attack;
                }
            }

            return null;
        }

        private CompactEnemyAttack SelectContactAttack()
        {
            if (definition.attacks == null) return null;
            for (int index = 0; index < definition.attacks.Length; index++)
            {
                CompactEnemyAttack attack = definition.attacks[index];
                if (attack == null) continue;
                if (string.Equals(attack.kind, "contact", StringComparison.Ordinal)
                    || string.Equals(attack.kind, "suicide", StringComparison.Ordinal))
                {
                    return attack;
                }
            }

            return null;
        }

        private IEnumerator RunShotAttack(CompactEnemyAttack attack)
        {
            int triggers = attack.sequence == null
                ? 1
                : Math.Max(1, attack.sequence.triggers);
            float interval = attack.sequence == null
                ? 0f
                : Mathf.Max(0f, (float)attack.sequence.interval);
            for (int triggerIndex = 0;
                triggerIndex < triggers && !defeated;
                triggerIndex++)
            {
                FireTrigger(attack, triggerIndex);
                if (triggerIndex + 1 < triggers && interval > 0f)
                {
                    yield return new WaitForSeconds(interval);
                }
            }

            nextAttackAt = Time.time + Mathf.Max(0.01f, (float)attack.cooldown);
            attackRoutine = null;
        }

        private void FireTrigger(CompactEnemyAttack attack, int triggerIndex)
        {
            CompactEnemyShotDefinition shot;
            if (!CompactEnemyCatalog.TryResolveShot(attack.shot, out shot)) return;
            List<Transform> emitters = ResolveEmitters(attack, triggerIndex);
            if (emitters.Count == 0) return;

            int shotsPerTrigger = attack.volley == null
                ? 1
                : Math.Max(1, attack.volley.shotsPerTrigger);
            float spread = attack.volley == null
                ? 0f
                : Mathf.Max(0f, (float)attack.volley.spread);
            bool random = attack.volley != null
                && string.Equals(
                    attack.volley.distribution,
                    "random",
                    StringComparison.Ordinal);
            double damage = DirectDamage(attack) * stats.DamageMultiplier;
            if (damage <= 0d) return;
            string damageType = attack.damage == null || attack.damage.Length == 0
                ? "kinetic"
                : attack.damage[0].type;

            for (int emitterIndex = 0; emitterIndex < emitters.Count; emitterIndex++)
            {
                Transform emitter = emitters[emitterIndex];
                for (int shotIndex = 0;
                    shotIndex < shotsPerTrigger;
                    shotIndex++)
                {
                    float offset = SpreadOffset(
                        shotIndex,
                        shotsPerTrigger,
                        spread,
                        random);
                    Vector2 direction = Quaternion.Euler(0f, 0f, -offset)
                        * emitter.up;
                    CompactEnemyShot.Spawn(
                        gameObject.scene,
                        emitter.position,
                        direction,
                        shot,
                        damage,
                        damageType,
                        placementStableId,
                        participantStableId,
                        NextEventStableId("shot"),
                        transform);
                }
            }
        }

        private List<Transform> ResolveEmitters(
            CompactEnemyAttack attack,
            int triggerIndex)
        {
            var result = new List<Transform>();
            string[] ids = attack.emitters ?? Array.Empty<string>();
            if (ids.Length == 0) return result;

            if (string.Equals(
                    attack.firePattern,
                    "simultaneous",
                    StringComparison.Ordinal))
            {
                for (int index = 0; index < ids.Length; index++)
                {
                    Transform emitter;
                    if (mounts.TryGetValue(ids[index], out emitter))
                    {
                        result.Add(emitter);
                    }
                }

                return result;
            }

            int chosen = string.Equals(
                    attack.firePattern,
                    "single",
                    StringComparison.Ordinal)
                ? 0
                : Math.Abs(triggerIndex) % ids.Length;
            Transform selected;
            if (mounts.TryGetValue(ids[chosen], out selected))
            {
                result.Add(selected);
            }
            return result;
        }

        private Vector2 ResolveMovement()
        {
            if (definition.movement == null
                || definition.movement.speed <= 0d
                || string.Equals(
                    definition.movement.kind,
                    "stationary",
                    StringComparison.Ordinal))
            {
                return Vector2.zero;
            }

            if (string.Equals(
                    definition.movement.kind,
                    "wander",
                    StringComparison.Ordinal))
            {
                if (Time.time >= nextWanderAt)
                {
                    wanderDirection = UnityEngine.Random.insideUnitCircle.normalized;
                    if (wanderDirection.sqrMagnitude < 0.000001f)
                    {
                        wanderDirection = Vector2.right;
                    }
                    nextWanderAt = Time.time + UnityEngine.Random.Range(1.2f, 2.8f);
                }
                return wanderDirection;
            }

            if (player == null || !player.IsBound || player.IsDefeated)
            {
                return Vector2.zero;
            }

            Vector2 delta = player.transform.position - transform.position;
            float distance = delta.magnitude;
            if (distance > definition.detectionRange || distance < 0.0001f)
            {
                return Vector2.zero;
            }

            Vector2 direct = delta / distance;
            if (!string.Equals(
                    definition.movement.kind,
                    "strafe",
                    StringComparison.Ordinal))
            {
                return direct;
            }

            float desiredRange = PreferredRange();
            float radial = Mathf.Clamp((distance - desiredRange) * 0.45f, -1f, 1f);
            Vector2 tangent = new Vector2(-direct.y, direct.x);
            return tangent + direct * radial;
        }

        private float PreferredRange()
        {
            if (definition.attacks == null) return 4f;
            for (int index = 0; index < definition.attacks.Length; index++)
            {
                CompactEnemyAttack attack = definition.attacks[index];
                if (attack != null
                    && string.Equals(attack.kind, "shot", StringComparison.Ordinal)
                    && attack.range != null)
                {
                    return Mathf.Max(
                        0.5f,
                        (float)((attack.range.min + attack.range.max) * 0.5d));
                }
            }
            return 4f;
        }

        private void ResolvePlayer()
        {
            if (player != null
                && player.gameObject.activeInHierarchy
                && player.IsBound)
            {
                return;
            }
            if (Time.time < nextTargetSearchAt) return;
            nextTargetSearchAt = Time.time + 0.35f;
            player = FindFirstObjectByType<PlayerHUD>(
                FindObjectsInactive.Exclude);
        }

        private void ConfigureBody()
        {
            body = GetComponent<Rigidbody2D>()
                ?? gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.freezeRotation = true;

            bodyCollider = GetComponent<CircleCollider2D>()
                ?? gameObject.AddComponent<CircleCollider2D>();
            bodyCollider.isTrigger = false;
            bodyCollider.radius = Mathf.Max(0.05f, (float)definition.body.radius);
            bodyCollider.offset = definition.body.offset == null
                ? Vector2.zero
                : new Vector2(
                    (float)definition.body.offset.x,
                    (float)definition.body.offset.y);

            EnsurePixelSprite();
            GameObject visual = new GameObject("BodyVisual");
            visual.transform.SetParent(transform, false);
            bodyRenderer = visual.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = pixelSprite;
            bodyRenderer.color = stats.Color;
            bodyRenderer.sortingOrder = 40;
            float diameter = bodyCollider.radius * 2f;
            visual.transform.localScale =
                new Vector3(diameter, diameter, 1f);
        }

        private void ConfigureMounts()
        {
            mounts.Clear();
            CompactEnemyMount[] values = definition.mounts
                ?? Array.Empty<CompactEnemyMount>();
            for (int index = 0; index < values.Length; index++)
            {
                CompactEnemyMount mount = values[index];
                if (mount == null
                    || string.IsNullOrWhiteSpace(mount.id)
                    || mount.position == null)
                {
                    continue;
                }

                GameObject mountObject = new GameObject(mount.id);
                mountObject.transform.SetParent(transform, false);
                mountObject.transform.localPosition = new Vector3(
                    (float)mount.position.x,
                    (float)mount.position.y,
                    0f);
                mountObject.transform.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    -(float)mount.rotation);
                SpriteRenderer renderer =
                    mountObject.AddComponent<SpriteRenderer>();
                renderer.sprite = pixelSprite;
                renderer.color = new Color(0.12f, 0.14f, 0.18f, 1f);
                renderer.sortingOrder = 41;
                mountObject.transform.localScale =
                    new Vector3(0.14f, 0.34f, 1f);
                mounts.Add(mount.id, mountObject.transform);
            }
        }

        private void DeliverPlayerDamage(
            PlayerHUD receiver,
            CompactEnemyAttack attack,
            double amount,
            string damageType)
        {
            CombatChannel channel;
            if (!TryParseChannel(damageType, out channel)) return;
            DamageReceiverCommand command;
            string rejection;
            if (!PlayablePlayerDamageCommandFactory.TryCreateForCharacterContact(
                    receiver,
                    receiver.CharacterInstanceStableId,
                    NextEventStableId(attack.id ?? "contact"),
                    placementStableId,
                    participantStableId,
                    amount,
                    channel,
                    out command,
                    out rejection))
            {
                return;
            }

            receiver.ApplyDamage(command);
        }

        private void Defeat()
        {
            if (defeated) return;
            defeated = true;
            currentHealth = 0d;
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }
            if (bodyCollider != null) bodyCollider.enabled = false;
            if (bodyRenderer != null)
            {
                bodyRenderer.color = new Color(
                    bodyRenderer.color.r * 0.35f,
                    bodyRenderer.color.g * 0.35f,
                    bodyRenderer.color.b * 0.35f,
                    0.7f);
            }

            try
            {
                roomOwner.ReportOccupantTerminal(
                    StableId.Create(
                        "operation",
                        "compact-enemy-terminal-" + HashToken(
                            roomStableId
                            + "|" + placementStableId
                            + "|" + lifecycleGeneration)),
                    roomStableId,
                    placementStableId);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                Debug.LogError(
                    "compact-enemy-terminal-report-failed:"
                    + exception.Message,
                    this);
            }
        }

        private void FlashHit()
        {
            if (bodyRenderer == null || defeated) return;
            if (hitFlashRoutine != null) StopCoroutine(hitFlashRoutine);
            hitFlashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            Color original = bodyRenderer.color;
            bodyRenderer.color = Color.white;
            yield return new WaitForSeconds(0.06f);
            if (bodyRenderer != null && !defeated)
            {
                bodyRenderer.color = original;
            }
            hitFlashRoutine = null;
        }

        private void SetFacing(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.000001f) return;
            float angle = Mathf.Atan2(direction.y, direction.x)
                * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private StableId NextEventStableId(string kind)
        {
            eventSequence++;
            return StableId.Create(
                "event",
                "compact-enemy-"
                + Sanitize(kind)
                + "-"
                + HashToken(
                    placementStableId
                    + "|" + lifecycleGeneration
                    + "|" + eventSequence));
        }

        private static double DirectDamage(CompactEnemyAttack attack)
        {
            if (attack == null || attack.damage == null) return 0d;
            double total = 0d;
            for (int index = 0; index < attack.damage.Length; index++)
            {
                CompactEnemyDamage value = attack.damage[index];
                if (value != null && value.amount > 0d) total += value.amount;
            }
            return total;
        }

        private static float SpreadOffset(
            int index,
            int count,
            float spread,
            bool random)
        {
            if (spread <= 0f || count <= 1)
            {
                return random && spread > 0f
                    ? UnityEngine.Random.Range(-spread * 0.5f, spread * 0.5f)
                    : 0f;
            }
            if (random)
            {
                return UnityEngine.Random.Range(-spread * 0.5f, spread * 0.5f);
            }

            return Mathf.Lerp(-spread * 0.5f, spread * 0.5f, index / (float)(count - 1));
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

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "attack";
            string source = value.ToLowerInvariant();
            var result = new System.Text.StringBuilder();
            bool hyphen = false;
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
                if ((character >= 'a' && character <= 'z')
                    || (character >= '0' && character <= '9'))
                {
                    result.Append(character);
                    hyphen = false;
                }
                else if (!hyphen && result.Length > 0)
                {
                    result.Append('-');
                    hyphen = true;
                }
            }
            while (result.Length > 0 && result[result.Length - 1] == '-')
            {
                result.Length--;
            }
            return result.Length == 0 ? "attack" : result.ToString();
        }

        private static string HashToken(object value)
        {
            string source = value == null ? string.Empty : value.ToString();
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < source.Length; index++)
                {
                    hash ^= source[index];
                    hash *= 16777619u;
                }
                return hash.ToString("x8");
            }
        }

        private static void EnsurePixelSprite()
        {
            if (pixelSprite != null) return;
            pixelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixelTexture.name = "Compact Enemy Pixel";
            pixelTexture.SetPixel(0, 0, Color.white);
            pixelTexture.Apply(false, true);
            pixelSprite = Sprite.Create(
                pixelTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            pixelSprite.name = "Compact Enemy Sprite";
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }
    }
}
