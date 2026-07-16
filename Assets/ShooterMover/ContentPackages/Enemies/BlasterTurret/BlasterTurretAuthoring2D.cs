using System;
using System.Text;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.ContentPackages.Enemies.BlasterTurret
{
    public enum BlasterTurretCardinalFacing
    {
        Right = 0,
        Up = 1,
        Left = 2,
        Down = 3,
    }

    /// <summary>
    /// Drag-and-drop scene authoring for a Blaster Turret. Placement and facing are
    /// snapped in edit mode; runtime combat dependencies are resolved from the one
    /// scene context when play starts.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BlasterTurretPackage))]
    public sealed class BlasterTurretAuthoring2D : MonoBehaviour
    {
        private const float MinimumGridSize = 0.05f;

        [Header("Placement")]
        [SerializeField] private bool snapToGrid = true;
        [SerializeField] private float gridSize = 1f;
        [SerializeField] private BlasterTurretCardinalFacing facing =
            BlasterTurretCardinalFacing.Left;

        [Header("Combat")]
        [SerializeField] private BlasterTurretDefinition definition;
        [SerializeField] private Sprite projectileSprite;
        [SerializeField] private Vector2 projectileVisualScale = new Vector2(0.07f, 0.07f);
        [SerializeField] private int projectileSortingOrder = 40;
        [SerializeField] private EnemyTarget2DAdapter targetOverride;
        [SerializeField] private BlasterTurretSceneContext2D sceneContextOverride;

        [Header("Aggro and wreck")]
        [SerializeField] private bool trackPlayer = true;
        [Min(1f)]
        [SerializeField] private float trackingDegreesPerSecond = 120f;
        [Min(1f)]
        [SerializeField] private float returnDegreesPerSecond = 90f;
        [Tooltip("Disable this to let the player move through a destroyed turret.")]
        [SerializeField] private bool keepColliderWhenDestroyed;

        private BlasterTurretPackage package;
        private BlasterTurretSceneContext2D boundContext;
        private BoundedProjectile2D projectileTemplate;
        private StableId actorId;
        private bool attemptedMissingContextWarning;

        public bool IsReady => package != null && package.IsConfigured;

        public StableId ActorId => actorId;

        public BlasterTurretPackage Package => package;

        public BlasterTurretCardinalFacing Facing => facing;

        public float GridSize => gridSize;

        public bool TrackPlayer => trackPlayer;

        public float TrackingDegreesPerSecond => trackingDegreesPerSecond;

        public float ReturnDegreesPerSecond => returnDegreesPerSecond;

        public bool KeepColliderWhenDestroyed => keepColliderWhenDestroyed;

        public void SetRuntimeOverrides(
            BlasterTurretDefinition definitionOverride,
            Sprite projectileSpriteOverride)
        {
            if (package != null && package.IsConfigured)
            {
                throw new InvalidOperationException(
                    "Runtime overrides must be assigned before turret configuration.");
            }

            if (definitionOverride != null)
            {
                definition = definitionOverride;
            }

            if (projectileSpriteOverride != null)
            {
                projectileSprite = projectileSpriteOverride;
            }
        }

        public bool TryConfigureNow()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return false;
            }

            package = package == null ? GetComponent<BlasterTurretPackage>() : package;
            if (package.IsConfigured)
            {
                return RegisterWithContext();
            }

            BlasterTurretSceneContext2D context = ResolveContext();
            EnemyTarget2DAdapter target = targetOverride != null
                ? targetOverride
                : context == null ? null : context.PlayerTarget;
            if (context == null || !context.IsConfigured || target == null || !target.IsConfigured)
            {
                return false;
            }

            BlasterTurretDefinition runtimeDefinition =
                definition != null ? definition : package.Definition;
            if (runtimeDefinition == null || target.TargetCollider == null)
            {
                return false;
            }

            ApplyPlacement();
            projectileTemplate = CreateProjectileTemplate();
            actorId = CreateActorId();
            package.ConfigureBehavior(
                trackPlayer,
                trackingDegreesPerSecond,
                returnDegreesPerSecond,
                keepColliderWhenDestroyed);
            package.Configure(
                runtimeDefinition,
                target,
                target.TargetCollider,
                projectileTemplate,
                actorId,
                target.TargetId,
                CombatWeightClass.Standard);
            boundContext = context;
            return boundContext.RegisterTurret(actorId, package);
        }

        private void Awake()
        {
            package = GetComponent<BlasterTurretPackage>();
            ApplyPlacement();
        }

        private void Start()
        {
            if (UnityEngine.Application.isPlaying)
            {
                TryConfigureNow();
            }
        }

        private void Update()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                ApplyPlacement();
                return;
            }

            if (!IsReady && !TryConfigureNow() && !attemptedMissingContextWarning)
            {
                attemptedMissingContextWarning = true;
                Debug.LogWarning(
                    "Placed Blaster Turret is waiting for a configured "
                    + nameof(BlasterTurretSceneContext2D)
                    + " in the scene.",
                    this);
            }
        }

        private void OnValidate()
        {
            gridSize = Mathf.Max(MinimumGridSize, gridSize);
            projectileVisualScale.x = Mathf.Max(0.001f, projectileVisualScale.x);
            projectileVisualScale.y = Mathf.Max(0.001f, projectileVisualScale.y);
            trackingDegreesPerSecond = Mathf.Max(1f, trackingDegreesPerSecond);
            returnDegreesPerSecond = Mathf.Max(1f, returnDegreesPerSecond);
            ApplyPlacement();
        }

        private void OnDestroy()
        {
            if (boundContext != null && actorId != null && package != null)
            {
                boundContext.UnregisterTurret(actorId, package);
            }

            if (projectileTemplate != null)
            {
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(projectileTemplate.gameObject);
                }
                else
                {
                    DestroyImmediate(projectileTemplate.gameObject);
                }
            }
        }

        private bool RegisterWithContext()
        {
            BlasterTurretSceneContext2D context = ResolveContext();
            if (context == null || !context.IsConfigured)
            {
                return false;
            }

            if (actorId == null && package.TargetAdapter != null)
            {
                actorId = package.TargetAdapter.TargetId;
            }

            boundContext = context;
            return actorId != null && boundContext.RegisterTurret(actorId, package);
        }

        private BlasterTurretSceneContext2D ResolveContext()
        {
            if (sceneContextOverride != null)
            {
                return sceneContextOverride;
            }

            return FindFirstObjectByType<BlasterTurretSceneContext2D>(
                FindObjectsInactive.Include);
        }

        private BoundedProjectile2D CreateProjectileTemplate()
        {
            GameObject root = new GameObject(name + " Projectile Template");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = new Vector3(
                projectileVisualScale.x,
                projectileVisualScale.y,
                1f);

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = projectileSprite;
            renderer.sortingOrder = projectileSortingOrder;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.simulated = false;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            BoundedProjectile2D projectile = root.AddComponent<BoundedProjectile2D>();
            root.SetActive(false);
            return projectile;
        }

        private StableId CreateActorId()
        {
            string hierarchyKey = gameObject.scene.path + "|" + BuildHierarchyPath(transform);
            ulong hash = 14695981039346656037UL;
            for (int index = 0; index < hierarchyKey.Length; index++)
            {
                hash ^= hierarchyKey[index];
                hash *= 1099511628211UL;
            }

            return StableId.Create("enemy", "blaster-turret-" + hash.ToString("x16"));
        }

        private static string BuildHierarchyPath(Transform value)
        {
            StringBuilder path = new StringBuilder();
            Transform current = value;
            while (current != null)
            {
                path.Insert(0, "/" + current.name + "[" + current.GetSiblingIndex() + "]");
                current = current.parent;
            }

            return path.ToString();
        }

        private void ApplyPlacement()
        {
            if (snapToGrid)
            {
                float safeGrid = Mathf.Max(MinimumGridSize, gridSize);
                Vector3 position = transform.position;
                position.x = Mathf.Round(position.x / safeGrid) * safeGrid;
                position.y = Mathf.Round(position.y / safeGrid) * safeGrid;
                position.z = 0f;
                transform.position = position;
            }

            transform.rotation = Quaternion.Euler(0f, 0f, FacingAngle(facing));
        }

        private static float FacingAngle(BlasterTurretCardinalFacing value)
        {
            switch (value)
            {
                case BlasterTurretCardinalFacing.Up:
                    return 90f;
                case BlasterTurretCardinalFacing.Left:
                    return 180f;
                case BlasterTurretCardinalFacing.Down:
                    return 270f;
                default:
                    return 0f;
            }
        }
    }
}
