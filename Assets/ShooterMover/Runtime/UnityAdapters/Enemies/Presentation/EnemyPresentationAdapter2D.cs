using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies.Presentation
{
    public enum EnemyPresentationBodyKind2D
    {
        MobileBlasterDroid = 1,
        BlasterTurret = 2,
    }

    public interface IEnemyPresentationCommandSink2D
    {
        Transform ShotOrigin { get; }

        void SetFacing(Vector2 worldDirection);

        void SetMovementIntent(Vector2 worldDirection);

        void BeginAttackWindUp(float durationSeconds);

        void SignalAttackOrigin();
    }

    public interface IEnemyRuntimeMechanicsReadiness2D
    {
        bool RuntimeMechanicsReady { get; }

        string RuntimeMechanicsReadinessReason { get; }
    }

    public interface IEnemyPlayerDamageRouteReadiness2D
    {
        bool PlayerDamageRouteReady { get; }

        string PlayerDamageRouteReadinessReason { get; }
    }

    /// <summary>
    /// Presentation-only projection over RoomEnemyActor2D. Health, decisions, attacks,
    /// rewards, room completion, and persistence remain owned by existing authorities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyPresentationAdapter2D :
        MonoBehaviour,
        IEnemyPresentationCommandSink2D
    {
        [SerializeField] private string runtimeDefinitionStableId = "enemy.unassigned";
        [SerializeField] private EnemyPresentationBodyKind2D bodyKind =
            EnemyPresentationBodyKind2D.MobileBlasterDroid;

        private static Sprite squareSprite;

        private Transform visualRoot;
        private Transform facingRoot;
        private Transform shotOrigin;
        private SpriteRenderer movementMarker;
        private SpriteRenderer telegraph;
        private SpriteRenderer muzzle;
        private SpriteRenderer[] renderers;
        private Color[] baseColors;
        private Vector3 previousPosition;
        private RoomEnemyActor2D observedActor;
        private long observedGeneration = long.MinValue;
        private double observedHealth;
        private bool observedActive;
        private bool terminal;
        private bool hasPositionSample;
        private float hitSeconds;
        private float windUpSeconds;
        private float windUpDuration;
        private float muzzleSeconds;
        private float explicitMovementSeconds;

        public Transform ShotOrigin
        {
            get
            {
                EnsureInitialized();
                return shotOrigin;
            }
        }

        public string RuntimeDefinitionStableId { get { return runtimeDefinitionStableId; } }

        public EnemyPresentationBodyKind2D BodyKind { get { return bodyKind; } }

        private void Awake()
        {
            EnsureInitialized();
            hasPositionSample = false;
        }

        private void OnDisable()
        {
            observedActor = null;
            observedGeneration = long.MinValue;
            hasPositionSample = false;
            if (visualRoot != null)
            {
                ResetPresentation();
            }
        }

        private void Update()
        {
            ObserveRuntime();
            ObserveMovement();
            Animate(Time.deltaTime);
        }

        public bool TryValidateFor(StableId expectedDefinitionId, out string reason)
        {
            if (expectedDefinitionId == null)
            {
                reason = "Expected definition identity is missing.";
                return false;
            }

            try
            {
                StableId configured = StableId.Parse(runtimeDefinitionStableId);
                if (configured != expectedDefinitionId)
                {
                    reason = "Registered for " + configured + ", not " + expectedDefinitionId + ".";
                    return false;
                }
            }
            catch (Exception exception)
            {
                reason = "Invalid presentation identity: " + exception.Message;
                return false;
            }

            if (!Enum.IsDefined(typeof(EnemyPresentationBodyKind2D), bodyKind))
            {
                reason = "Unsupported presentation body kind.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public void SetFacing(Vector2 worldDirection)
        {
            EnsureInitialized();
            if (terminal || worldDirection.sqrMagnitude <= 0.000001f) return;
            Vector3 local = transform.InverseTransformDirection(worldDirection.normalized);
            facingRoot.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg);
        }

        public void SetMovementIntent(Vector2 worldDirection)
        {
            EnsureInitialized();
            if (bodyKind != EnemyPresentationBodyKind2D.MobileBlasterDroid) return;
            explicitMovementSeconds = 0.10f;
            ApplyMovementIntent(worldDirection);
        }

        public void BeginAttackWindUp(float durationSeconds)
        {
            EnsureInitialized();
            if (terminal
                || float.IsNaN(durationSeconds)
                || float.IsInfinity(durationSeconds)
                || durationSeconds <= 0f)
            {
                return;
            }

            windUpDuration = durationSeconds;
            windUpSeconds = durationSeconds;
            telegraph.transform.localScale = Vector3.one * 1.7f;
            Color color = telegraph.color;
            color.a = 0.12f;
            telegraph.color = color;
            telegraph.enabled = true;
        }

        public void SignalAttackOrigin()
        {
            EnsureInitialized();
            if (terminal) return;
            windUpSeconds = 0f;
            telegraph.enabled = false;
            muzzleSeconds = 0.12f;
            muzzle.enabled = true;
        }

        private void ObserveRuntime()
        {
            RoomEnemyActor2D actor = GetComponentInParent<RoomEnemyActor2D>();
            if (actor == null || !actor.IsBound || actor.Runtime == null)
            {
                bool lostBinding = observedActor != null
                    || observedGeneration != long.MinValue;
                observedActor = null;
                observedGeneration = long.MinValue;
                hasPositionSample = false;
                if (lostBinding)
                {
                    ResetPresentation();
                }
                return;
            }

            EnemyActorState state = actor.Runtime.ActorState;
            if (observedActor != actor || observedGeneration != actor.LifecycleGeneration)
            {
                observedActor = actor;
                observedGeneration = actor.LifecycleGeneration;
                observedHealth = state.Health;
                observedActive = state.IsActive;
                ResetPresentation();
                previousPosition = transform.position;
                hasPositionSample = true;
                if (!state.IsActive) EnterTerminal();
                return;
            }

            if (state.Health < observedHealth) hitSeconds = 0.10f;
            if (observedActive && !state.IsActive) EnterTerminal();
            observedHealth = state.Health;
            observedActive = state.IsActive;
        }

        private void ObserveMovement()
        {
            Vector3 current = transform.position;
            if (!hasPositionSample)
            {
                previousPosition = current;
                hasPositionSample = true;
                return;
            }

            if (bodyKind == EnemyPresentationBodyKind2D.MobileBlasterDroid
                && explicitMovementSeconds <= 0f)
            {
                ApplyMovementIntent(current - previousPosition);
            }
            previousPosition = current;
        }

        private void ApplyMovementIntent(Vector2 worldDirection)
        {
            bool moving = !terminal && worldDirection.sqrMagnitude > 0.000001f;
            movementMarker.enabled = moving;
            if (moving) SetFacing(worldDirection);
        }

        private void Animate(float deltaTime)
        {
            if (terminal) return;

            explicitMovementSeconds = Mathf.Max(0f, explicitMovementSeconds - deltaTime);
            hitSeconds = Mathf.Max(0f, hitSeconds - deltaTime);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].color = hitSeconds > 0f ? Color.white : baseColors[index];
                }
            }

            if (windUpSeconds > 0f)
            {
                windUpSeconds = Mathf.Max(0f, windUpSeconds - deltaTime);
                float progress = 1f - (windUpSeconds / windUpDuration);
                telegraph.enabled = true;
                telegraph.transform.localScale = Vector3.one * Mathf.Lerp(1.7f, 0.8f, progress);
                Color color = telegraph.color;
                color.a = Mathf.Lerp(0.12f, 0.72f, progress);
                telegraph.color = color;
            }
            else
            {
                telegraph.enabled = false;
            }

            muzzleSeconds = Mathf.Max(0f, muzzleSeconds - deltaTime);
            muzzle.enabled = muzzleSeconds > 0f;
        }

        private void ResetPresentation()
        {
            terminal = false;
            hitSeconds = 0f;
            windUpSeconds = 0f;
            muzzleSeconds = 0f;
            explicitMovementSeconds = 0f;
            visualRoot.localRotation = Quaternion.identity;
            facingRoot.localRotation = Quaternion.identity;
            movementMarker.enabled = false;
            telegraph.enabled = false;
            muzzle.enabled = false;
            SetColors(1f);
        }

        private void EnterTerminal()
        {
            if (terminal) return;
            terminal = true;
            movementMarker.enabled = false;
            telegraph.enabled = false;
            muzzle.enabled = false;
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, 35f);
            SetColors(0.28f);
        }

        private void EnsureInitialized()
        {
            if (visualRoot != null) return;
            BuildVisuals();
            ConfigurePhysics();
        }

        private void BuildVisuals()
        {
            visualRoot = new GameObject("__EnemyPresentationVisualV1").transform;
            visualRoot.SetParent(transform, false);
            facingRoot = new GameObject("Facing").transform;
            facingRoot.SetParent(visualRoot, false);

            if (bodyKind == EnemyPresentationBodyKind2D.BlasterTurret)
            {
                Part("StationaryBase", visualRoot, Vector2.zero, new Vector2(1.2f, 1.2f),
                    new Color(0.22f, 0.22f, 0.27f, 1f), 0);
                Transform brace = Part("Brace", visualRoot, Vector2.zero, new Vector2(1.4f, 0.18f),
                    new Color(0.52f, 0.31f, 0.14f, 1f), 1).transform;
                brace.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Part("AimHead", facingRoot, Vector2.zero, new Vector2(0.78f, 0.78f),
                    new Color(0.58f, 0.18f, 0.13f, 1f), 2);
                Part("Barrel", facingRoot, new Vector2(0.72f, 0f), new Vector2(0.92f, 0.2f),
                    new Color(0.92f, 0.56f, 0.18f, 1f), 3);
            }
            else
            {
                Part("Chassis", facingRoot, Vector2.zero, new Vector2(1.25f, 0.72f),
                    new Color(0.13f, 0.25f, 0.39f, 1f), 0);
                Part("ForwardArmor", facingRoot, new Vector2(0.48f, 0f), new Vector2(0.55f, 0.54f),
                    new Color(0.17f, 0.64f, 0.8f, 1f), 1);
                Part("Blaster", facingRoot, new Vector2(0.84f, 0f), new Vector2(0.72f, 0.16f),
                    new Color(0.77f, 0.89f, 0.94f, 1f), 2);
            }

            movementMarker = Part("MovementIntent", facingRoot, new Vector2(-0.82f, 0f),
                new Vector2(0.42f, 0.2f), new Color(0.15f, 0.86f, 1f, 0.9f), -1);
            telegraph = Part("AttackTelegraph", visualRoot, Vector2.zero, new Vector2(1.7f, 1.7f),
                new Color(1f, 0.42f, 0.06f, 0.45f), -2);

            shotOrigin = new GameObject("ShotOrigin").transform;
            shotOrigin.SetParent(facingRoot, false);
            shotOrigin.localPosition = new Vector3(1.18f, 0f, 0f);
            muzzle = Part("OriginPulse", shotOrigin, Vector2.zero, new Vector2(0.28f, 0.28f),
                new Color(1f, 0.86f, 0.2f, 0.95f), 4);

            renderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                baseColors[index] = renderers[index].color;
            }

            movementMarker.enabled = false;
            telegraph.enabled = false;
            muzzle.enabled = false;
        }

        private void ConfigurePhysics()
        {
            BoxCollider2D collider = GetComponent<BoxCollider2D>();
            if (collider == null) collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = bodyKind == EnemyPresentationBodyKind2D.BlasterTurret
                ? new Vector2(1.2f, 1.2f)
                : new Vector2(1.25f, 0.75f);

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.bodyType = bodyKind == EnemyPresentationBodyKind2D.BlasterTurret
                ? RigidbodyType2D.Static
                : RigidbodyType2D.Kinematic;
        }

        private static SpriteRenderer Part(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 scale,
            Color color,
            int order)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = SquareSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void SetColors(float alphaMultiplier)
        {
            if (renderers == null || baseColors == null) return;
            for (int index = 0; index < renderers.Length; index++)
            {
                Color color = baseColors[index];
                color.a *= alphaMultiplier;
                renderers[index].color = color;
            }
        }

        private static Sprite SquareSprite
        {
            get
            {
                if (squareSprite == null)
                {
                    squareSprite = Sprite.Create(
                        Texture2D.whiteTexture,
                        new Rect(0f, 0f, 1f, 1f),
                        new Vector2(0.5f, 0.5f),
                        1f);
                    squareSprite.name = "EnemyPresentationSquare";
                    squareSprite.hideFlags = HideFlags.HideAndDontSave;
                }
                return squareSprite;
            }
        }
    }
}
