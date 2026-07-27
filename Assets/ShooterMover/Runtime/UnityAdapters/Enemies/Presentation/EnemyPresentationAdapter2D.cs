using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies.Presentation
{
    public enum EnemyPresentationPartParent2D
    {
        VisualRoot = 0,
        FacingRoot = 1,
        ShotOrigin = 2,
    }

    [Serializable]
    public sealed class EnemyPresentationPartDefinition2D
    {
        [SerializeField] private string partName = "Part";
        [SerializeField] private EnemyPresentationPartParent2D parent =
            EnemyPresentationPartParent2D.FacingRoot;
        [SerializeField] private Vector2 localPosition = Vector2.zero;
        [SerializeField] private Vector2 localScale = Vector2.one;
        [SerializeField] private float localRotationDegrees;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private int sortingOrder;

        public string PartName { get { return partName; } }

        public EnemyPresentationPartParent2D Parent { get { return parent; } }

        public Vector2 LocalPosition { get { return localPosition; } }

        public Vector2 LocalScale { get { return localScale; } }

        public float LocalRotationDegrees { get { return localRotationDegrees; } }

        public Color Color { get { return color; } }

        public int SortingOrder { get { return sortingOrder; } }

        public bool TryValidate(string path, out string reason)
        {
            if (string.IsNullOrWhiteSpace(partName))
            {
                reason = path + " requires a part name.";
                return false;
            }
            if (!Enum.IsDefined(typeof(EnemyPresentationPartParent2D), parent))
            {
                reason = path + " has an unsupported parent.";
                return false;
            }
            if (!IsFinite(localPosition.x)
                || !IsFinite(localPosition.y)
                || !IsFinite(localScale.x)
                || !IsFinite(localScale.y)
                || !IsFinite(localRotationDegrees)
                || !IsFinite(color.r)
                || !IsFinite(color.g)
                || !IsFinite(color.b)
                || !IsFinite(color.a))
            {
                reason = path + " contains a non-finite visual value.";
                return false;
            }
            if (Mathf.Abs(localScale.x) <= 0.000001f
                || Mathf.Abs(localScale.y) <= 0.000001f)
            {
                reason = path + " requires non-zero scale.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
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
    /// Presentation-only projection over RoomEnemyActor2D. Enemy-specific appearance and
    /// physics are authored in prefab data. Health, decisions, attacks, rewards, room
    /// completion, and persistence remain owned by existing authorities.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyPresentationAdapter2D :
        MonoBehaviour,
        IEnemyPresentationCommandSink2D
    {
        [SerializeField] private string runtimeDefinitionStableId = "enemy.unassigned";
        [SerializeField] private bool supportsMovementIntent;
        [SerializeField] private bool inferMovementFromTransform;
        [SerializeField] private RigidbodyType2D physicsBodyType = RigidbodyType2D.Kinematic;
        [SerializeField] private bool freezeBodyRotation = true;
        [SerializeField] private Vector2 colliderSize = Vector2.one;
        [SerializeField] private Vector2 shotOriginLocalPosition = Vector2.right;
        [SerializeField] private EnemyPresentationPartDefinition2D[] visualParts =
            Array.Empty<EnemyPresentationPartDefinition2D>();
        [SerializeField] private EnemyPresentationPartDefinition2D movementMarkerPart;
        [SerializeField] private EnemyPresentationPartDefinition2D attackTelegraphPart;
        [SerializeField] private EnemyPresentationPartDefinition2D originPulsePart;
        [SerializeField] private float telegraphEndScaleMultiplier = 0.47f;
        [SerializeField] private float telegraphStartAlpha = 0.12f;
        [SerializeField] private float telegraphEndAlpha = 0.72f;

        private static Sprite squareSprite;

        private Transform visualRoot;
        private Transform facingRoot;
        private Transform shotOrigin;
        private SpriteRenderer movementMarker;
        private SpriteRenderer telegraph;
        private SpriteRenderer muzzle;
        private SpriteRenderer[] renderers;
        private Color[] baseColors;
        private Vector3 telegraphBaseScale;
        private Color telegraphBaseColor;
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

            StableId configured;
            try
            {
                configured = StableId.Parse(runtimeDefinitionStableId);
            }
            catch (Exception exception)
            {
                reason = "Invalid presentation identity: " + exception.Message;
                return false;
            }

            if (configured != expectedDefinitionId)
            {
                reason = "Registered for " + configured + ", not " + expectedDefinitionId + ".";
                return false;
            }

            return TryValidateConfiguration(out reason);
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
            if (!supportsMovementIntent) return;
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
            telegraph.transform.localScale = telegraphBaseScale;
            Color color = telegraphBaseColor;
            color.a = telegraphStartAlpha;
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

            if (supportsMovementIntent
                && inferMovementFromTransform
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
                telegraph.transform.localScale = telegraphBaseScale
                    * Mathf.Lerp(1f, telegraphEndScaleMultiplier, progress);
                Color color = telegraphBaseColor;
                color.a = Mathf.Lerp(telegraphStartAlpha, telegraphEndAlpha, progress);
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
            telegraph.transform.localScale = telegraphBaseScale;
            telegraph.color = telegraphBaseColor;
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
            string reason;
            if (!TryValidateConfiguration(out reason))
            {
                throw new InvalidOperationException(
                    "Enemy presentation configuration is invalid: " + reason);
            }
            BuildVisuals();
            ConfigurePhysics();
        }

        private bool TryValidateConfiguration(out string reason)
        {
            try
            {
                StableId.Parse(runtimeDefinitionStableId);
            }
            catch (Exception exception)
            {
                reason = "Invalid presentation identity: " + exception.Message;
                return false;
            }

            if (!Enum.IsDefined(typeof(RigidbodyType2D), physicsBodyType))
            {
                reason = "Unsupported Rigidbody body type.";
                return false;
            }
            if (!IsFinite(colliderSize.x)
                || !IsFinite(colliderSize.y)
                || colliderSize.x <= 0f
                || colliderSize.y <= 0f)
            {
                reason = "Collider size must be finite and positive.";
                return false;
            }
            if (!IsFinite(shotOriginLocalPosition.x)
                || !IsFinite(shotOriginLocalPosition.y))
            {
                reason = "Shot origin must be finite.";
                return false;
            }
            if (inferMovementFromTransform && !supportsMovementIntent)
            {
                reason = "Transform movement inference requires movement-intent support.";
                return false;
            }
            if (visualParts == null || visualParts.Length == 0)
            {
                reason = "At least one authored visual part is required.";
                return false;
            }
            for (int index = 0; index < visualParts.Length; index++)
            {
                if (visualParts[index] == null)
                {
                    reason = "Visual part " + index + " is missing.";
                    return false;
                }
                if (!visualParts[index].TryValidate(
                    "visualParts[" + index + "]",
                    out reason))
                {
                    return false;
                }
            }
            if (!ValidateSpecialPart(
                movementMarkerPart,
                EnemyPresentationPartParent2D.FacingRoot,
                "movement marker",
                out reason)
                || !ValidateSpecialPart(
                    attackTelegraphPart,
                    EnemyPresentationPartParent2D.VisualRoot,
                    "attack telegraph",
                    out reason)
                || !ValidateSpecialPart(
                    originPulsePart,
                    EnemyPresentationPartParent2D.ShotOrigin,
                    "origin pulse",
                    out reason))
            {
                return false;
            }
            if (!IsFinite(telegraphEndScaleMultiplier)
                || telegraphEndScaleMultiplier <= 0f
                || !IsFinite(telegraphStartAlpha)
                || !IsFinite(telegraphEndAlpha)
                || telegraphStartAlpha < 0f
                || telegraphStartAlpha > 1f
                || telegraphEndAlpha < 0f
                || telegraphEndAlpha > 1f)
            {
                reason = "Attack telegraph animation values are invalid.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool ValidateSpecialPart(
            EnemyPresentationPartDefinition2D definition,
            EnemyPresentationPartParent2D requiredParent,
            string label,
            out string reason)
        {
            if (definition == null)
            {
                reason = "The " + label + " definition is missing.";
                return false;
            }
            if (!definition.TryValidate(label, out reason))
            {
                return false;
            }
            if (definition.Parent != requiredParent)
            {
                reason = "The " + label + " must use parent " + requiredParent + ".";
                return false;
            }
            return true;
        }

        private void BuildVisuals()
        {
            visualRoot = new GameObject("__EnemyPresentationVisualV1").transform;
            visualRoot.SetParent(transform, false);
            facingRoot = new GameObject("Facing").transform;
            facingRoot.SetParent(visualRoot, false);
            shotOrigin = new GameObject("ShotOrigin").transform;
            shotOrigin.SetParent(facingRoot, false);
            shotOrigin.localPosition = new Vector3(
                shotOriginLocalPosition.x,
                shotOriginLocalPosition.y,
                0f);

            for (int index = 0; index < visualParts.Length; index++)
            {
                Part(visualParts[index]);
            }
            movementMarker = Part(movementMarkerPart);
            telegraph = Part(attackTelegraphPart);
            muzzle = Part(originPulsePart);

            renderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                baseColors[index] = renderers[index].color;
            }

            telegraphBaseScale = telegraph.transform.localScale;
            telegraphBaseColor = telegraph.color;
            movementMarker.enabled = false;
            telegraph.enabled = false;
            muzzle.enabled = false;
        }

        private void ConfigurePhysics()
        {
            BoxCollider2D collider = GetComponent<BoxCollider2D>();
            if (collider == null) collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = colliderSize;

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.bodyType = physicsBodyType;
            body.constraints = freezeBodyRotation
                ? RigidbodyConstraints2D.FreezeRotation
                : RigidbodyConstraints2D.None;
        }

        private SpriteRenderer Part(EnemyPresentationPartDefinition2D definition)
        {
            GameObject part = new GameObject(definition.PartName);
            part.transform.SetParent(ResolveParent(definition.Parent), false);
            part.transform.localPosition = new Vector3(
                definition.LocalPosition.x,
                definition.LocalPosition.y,
                0f);
            part.transform.localScale = new Vector3(
                definition.LocalScale.x,
                definition.LocalScale.y,
                1f);
            part.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                definition.LocalRotationDegrees);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = SquareSprite;
            renderer.color = definition.Color;
            renderer.sortingOrder = definition.SortingOrder;
            return renderer;
        }

        private Transform ResolveParent(EnemyPresentationPartParent2D parent)
        {
            switch (parent)
            {
                case EnemyPresentationPartParent2D.VisualRoot:
                    return visualRoot;
                case EnemyPresentationPartParent2D.FacingRoot:
                    return facingRoot;
                case EnemyPresentationPartParent2D.ShotOrigin:
                    return shotOrigin;
                default:
                    throw new InvalidOperationException(
                        "Unsupported presentation part parent: " + parent + ".");
            }
        }

        private void SetColors(float alphaMultiplier)
        {
            if (renderers == null || baseColors == null) return;
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] == null) continue;
                Color color = baseColors[index];
                color.a *= alphaMultiplier;
                renderers[index].color = color;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
