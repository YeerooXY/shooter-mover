using System;
using System.Globalization;
using System.Text;
using ShooterMover.ContentPackages.Weapons.Shared.Runtime;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring;
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
    /// Drag-and-drop scene authoring for one Blaster Turret placement. The authored
    /// identity is stable data, while runtime combat dependencies come from an
    /// explicit or nearest-parent OBJ-001 gameplay scope.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BlasterTurretPackage))]
    public sealed class BlasterTurretAuthoring2D : MonoBehaviour
    {
        private const float MinimumGridSize = 0.05f;
        private const string DefaultScopeCompatibilityId = "scope.gameplay";

        private static readonly StableId TurretFamilyId =
            StableId.Parse("enemy.blaster-turret");
        private static readonly StableId TurretVariantId =
            StableId.Parse("variant.blaster-turret-standard");

        [Header("Identity and scope")]
        [SerializeField] private string authoredPlacedInstanceId =
            "placed.blaster-turret-template";
        [SerializeField] private GameplaySceneScope2D sceneScopeOverride;
        [SerializeField] private string requiredScopeCompatibilityId =
            DefaultScopeCompatibilityId;

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

        [Header("Aggro and wreck")]
        [SerializeField] private bool trackPlayer = true;
        [Min(1f)]
        [SerializeField] private float trackingDegreesPerSecond = 120f;
        [Min(1f)]
        [SerializeField] private float returnDegreesPerSecond = 90f;
        [Tooltip("Disable this to let the player move through a destroyed turret.")]
        [SerializeField] private bool keepColliderWhenDestroyed;

        private BlasterTurretPackage package;
        private GameplaySceneScope2D boundSceneScope;
        private BlasterTurretSceneContext2D boundContext;
        private BoundedProjectile2D projectileTemplate;
        private StableId actorId;
        private SceneScopeRegistrationResult lastSceneScopeRegistrationResult;
        private string lastBindingDiagnostic = string.Empty;
        private bool attemptedBindingWarning;

        public bool IsReady => package != null
            && package.IsConfigured
            && boundSceneScope != null
            && boundContext != null;

        public StableId ActorId => actorId;

        public GameplaySceneScope2D BoundSceneScope => boundSceneScope;

        public SceneScopeRegistrationResult LastSceneScopeRegistrationResult =>
            lastSceneScopeRegistrationResult;

        public string LastBindingDiagnostic => lastBindingDiagnostic ?? string.Empty;

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

        /// <summary>
        /// Test-only authoring seam. Production placements use the serialized fields.
        /// </summary>
        public void ConfigurePlacementForTests(
            string placedInstanceId,
            GameplaySceneScope2D explicitScope,
            string scopeCompatibilityId)
        {
            package = package == null ? GetComponent<BlasterTurretPackage>() : package;
            if (boundSceneScope != null || (package != null && package.IsConfigured))
            {
                throw new InvalidOperationException(
                    "A bound or configured turret cannot change authored identity or scope.");
            }

            authoredPlacedInstanceId = placedInstanceId;
            sceneScopeOverride = explicitScope;
            requiredScopeCompatibilityId = scopeCompatibilityId;
            actorId = null;
            lastSceneScopeRegistrationResult = null;
            lastBindingDiagnostic = string.Empty;
            attemptedBindingWarning = false;
        }

        public bool TryConfigureNow()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return false;
            }

            package = package == null ? GetComponent<BlasterTurretPackage>() : package;
            if (package == null)
            {
                return Fail("The placed Blaster Turret has no package component.");
            }

            if (package.IsConfigured)
            {
                return RegisterConfiguredPackage();
            }

            StableId resolvedActorId;
            GameplaySceneScope2D sceneScope;
            BlasterTurretSceneContext2D context;
            if (!TryResolveBinding(
                    out resolvedActorId,
                    out sceneScope,
                    out context))
            {
                return false;
            }

            EnemyTarget2DAdapter target = targetOverride != null
                ? targetOverride
                : context.PlayerTarget;
            if (target == null || !target.IsConfigured || target.TargetCollider == null)
            {
                return Fail(
                    "The selected Blaster Turret scope has no configured player target.");
            }

            BlasterTurretDefinition runtimeDefinition =
                definition != null ? definition : package.Definition;
            if (runtimeDefinition == null)
            {
                return Fail("The placed Blaster Turret has no definition.");
            }

            SceneScopeRegistrationResult registration = RegisterWithSceneScope(
                sceneScope,
                resolvedActorId,
                runtimeDefinition);
            lastSceneScopeRegistrationResult = registration;
            if (registration == null || !registration.IsAccepted)
            {
                return Fail(
                    registration == null
                        ? "The gameplay scope returned no turret registration result."
                        : registration.Diagnostic);
            }

            actorId = resolvedActorId;
            ApplyPlacement();
            try
            {
                projectileTemplate = CreateProjectileTemplate();
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
            }
            catch (Exception exception)
            {
                sceneScope.Unregister(actorId, this);
                DestroyProjectileTemplate();
                return Fail(
                    "Blaster Turret package configuration failed closed: "
                    + exception.Message);
            }

            if (!context.RegisterTurret(actorId, package))
            {
                sceneScope.Unregister(actorId, this);
                package.enabled = false;
                return Fail(
                    string.IsNullOrEmpty(context.LastRegistrationDiagnostic)
                        ? "Blaster Turret combat-context registration failed."
                        : context.LastRegistrationDiagnostic);
            }

            boundSceneScope = sceneScope;
            boundContext = context;
            package.enabled = true;
            lastBindingDiagnostic = string.Empty;
            attemptedBindingWarning = false;
            return true;
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

            if (!IsReady && !TryConfigureNow() && !attemptedBindingWarning)
            {
                attemptedBindingWarning = true;
                Debug.LogWarning(
                    "Placed Blaster Turret is inactive because binding failed: "
                    + LastBindingDiagnostic,
                    this);
            }
        }

        private void OnDisable()
        {
            if (UnityEngine.Application.isPlaying)
            {
                UnregisterRuntimeBindings();
            }
        }

        private void OnValidate()
        {
            gridSize = Mathf.Max(MinimumGridSize, gridSize);
            projectileVisualScale.x = Mathf.Max(0.001f, projectileVisualScale.x);
            projectileVisualScale.y = Mathf.Max(0.001f, projectileVisualScale.y);
            trackingDegreesPerSecond = Mathf.Max(1f, trackingDegreesPerSecond);
            returnDegreesPerSecond = Mathf.Max(1f, returnDegreesPerSecond);
            if (requiredScopeCompatibilityId == null)
            {
                requiredScopeCompatibilityId = DefaultScopeCompatibilityId;
            }

            ApplyPlacement();
        }

        private void OnDestroy()
        {
            UnregisterRuntimeBindings();
            DestroyProjectileTemplate();
        }

        private bool RegisterConfiguredPackage()
        {
            if (package.TargetAdapter == null || package.TargetAdapter.TargetId == null)
            {
                return Fail(
                    "The configured Blaster Turret has no stable target-adapter identity.");
            }

            StableId resolvedActorId;
            GameplaySceneScope2D sceneScope;
            BlasterTurretSceneContext2D context;
            if (!TryResolveBinding(
                    out resolvedActorId,
                    out sceneScope,
                    out context))
            {
                return false;
            }

            StableId packageActorId = package.TargetAdapter.TargetId;
            if (!resolvedActorId.Equals(packageActorId))
            {
                return Fail(
                    "The configured package identity does not match the authored placed ID.");
            }

            SceneScopeRegistrationResult registration = RegisterWithSceneScope(
                sceneScope,
                resolvedActorId,
                package.Definition);
            lastSceneScopeRegistrationResult = registration;
            if (registration == null || !registration.IsAccepted)
            {
                package.enabled = false;
                return Fail(
                    registration == null
                        ? "The gameplay scope returned no turret registration result."
                        : registration.Diagnostic);
            }

            if (!context.RegisterTurret(resolvedActorId, package))
            {
                sceneScope.Unregister(resolvedActorId, this);
                package.enabled = false;
                return Fail(
                    string.IsNullOrEmpty(context.LastRegistrationDiagnostic)
                        ? "Blaster Turret combat-context registration failed."
                        : context.LastRegistrationDiagnostic);
            }

            actorId = resolvedActorId;
            boundSceneScope = sceneScope;
            boundContext = context;
            package.enabled = true;
            lastBindingDiagnostic = string.Empty;
            attemptedBindingWarning = false;
            return true;
        }

        private bool TryResolveBinding(
            out StableId resolvedActorId,
            out GameplaySceneScope2D resolvedScope,
            out BlasterTurretSceneContext2D resolvedContext)
        {
            resolvedActorId = null;
            resolvedScope = null;
            resolvedContext = null;

            if (!StableId.TryParse(authoredPlacedInstanceId, out resolvedActorId))
            {
                return Fail(
                    "Authored Blaster Turret placed ID is missing or malformed: '"
                    + (authoredPlacedInstanceId ?? "<null>")
                    + "'.");
            }

            actorId = resolvedActorId;

            StableId requiredCompatibility;
            if (!StableId.TryParse(
                    requiredScopeCompatibilityId,
                    out requiredCompatibility))
            {
                return Fail(
                    "Required Blaster Turret scope compatibility ID is malformed: '"
                    + (requiredScopeCompatibilityId ?? "<null>")
                    + "'.");
            }

            if (sceneScopeOverride != null)
            {
                if (sceneScopeOverride.gameObject.scene != gameObject.scene)
                {
                    return Fail(
                        "Explicit Blaster Turret gameplay scope belongs to another scene.");
                }

                if (!sceneScopeOverride.IsCompatible(requiredCompatibility))
                {
                    return Fail(
                        "Explicit Blaster Turret gameplay scope is invalid or incompatible: "
                        + sceneScopeOverride.ConfigurationError);
                }

                resolvedScope = sceneScopeOverride;
            }
            else if (!TryResolveNearestParentScope(
                         requiredCompatibility,
                         out resolvedScope))
            {
                return false;
            }

            resolvedContext = resolvedScope.GetComponent<BlasterTurretSceneContext2D>();
            if (resolvedContext == null)
            {
                return Fail(
                    "The selected gameplay scope does not expose a "
                    + nameof(BlasterTurretSceneContext2D)
                    + " combat port.");
            }

            if (!resolvedContext.IsConfigured)
            {
                return Fail(
                    "The selected Blaster Turret scene context is not configured.");
            }

            return true;
        }

        private bool TryResolveNearestParentScope(
            StableId requiredCompatibility,
            out GameplaySceneScope2D resolvedScope)
        {
            Transform ancestor = transform.parent;
            while (ancestor != null)
            {
                GameplaySceneScope2D[] candidates =
                    ancestor.GetComponents<GameplaySceneScope2D>();
                GameplaySceneScope2D compatible = null;
                int compatibleCount = 0;

                for (int index = 0; index < candidates.Length; index++)
                {
                    GameplaySceneScope2D candidate = candidates[index];
                    if (candidate != null
                        && candidate.IsCompatible(requiredCompatibility))
                    {
                        compatible = candidate;
                        compatibleCount++;
                    }
                }

                if (compatibleCount > 1)
                {
                    resolvedScope = null;
                    return Fail(
                        "The nearest Blaster Turret ancestor exposes multiple compatible "
                        + "gameplay scopes.");
                }

                if (compatibleCount == 1)
                {
                    resolvedScope = compatible;
                    return true;
                }

                ancestor = ancestor.parent;
            }

            resolvedScope = null;
            return Fail(
                "No compatible explicit or nearest-parent gameplay scope is available "
                + "for the placed Blaster Turret.");
        }

        private SceneScopeRegistrationResult RegisterWithSceneScope(
            GameplaySceneScope2D sceneScope,
            StableId placedId,
            BlasterTurretDefinition runtimeDefinition)
        {
            if (runtimeDefinition == null)
            {
                return SceneScopeRegistrationResult.Invalid(
                    "A Blaster Turret definition is required for scope registration.");
            }

            PlacedParticipantRegistration registration =
                new PlacedParticipantRegistration(
                    PlacedObjectIdentity.CreateAuthored(placedId),
                    new ObjectDefinitionReference(TurretFamilyId, TurretVariantId),
                    sceneScope.RuntimeProjectionId,
                    sceneScope.RunId,
                    sceneScope.AttemptGeneration,
                    Array.Empty<CapabilityReference>(),
                    BuildResolvedFingerprint(runtimeDefinition));
            return sceneScope.Register(
                new SceneScopeRegistrationRequest(
                    registration,
                    this,
                    BuildDiagnosticLocation()));
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

        private string BuildResolvedFingerprint(BlasterTurretDefinition runtimeDefinition)
        {
            StringBuilder builder = new StringBuilder();
            Append(builder, "maximumHealth", runtimeDefinition.MaximumHealth);
            Append(builder, "warningSeconds", runtimeDefinition.WarningSeconds);
            Append(builder, "recoverySeconds", runtimeDefinition.RecoverySeconds);
            Append(builder, "maximumRange", runtimeDefinition.MaximumRange);
            Append(builder, "muzzleOffset", runtimeDefinition.MuzzleOffset);
            Append(builder, "warningLineWidth", runtimeDefinition.WarningLineWidth);
            Append(builder, "facingConeDegrees", runtimeDefinition.FacingConeDegrees);
            Append(builder, "projectileSpeed", runtimeDefinition.ProjectileSpeed);
            Append(builder, "contactGraceSeconds", runtimeDefinition.ContactGraceSeconds);
            Append(
                builder,
                "simultaneousContactWindowSeconds",
                runtimeDefinition.SimultaneousContactWindowSeconds);
            builder.Append("moverColliderCapacity=")
                .Append(runtimeDefinition.MoverColliderCapacity)
                .Append('|');
            builder.Append("facing=").Append((int)facing).Append('|');
            builder.Append("trackPlayer=").Append(trackPlayer ? '1' : '0').Append('|');
            Append(builder, "trackingDegreesPerSecond", trackingDegreesPerSecond);
            Append(builder, "returnDegreesPerSecond", returnDegreesPerSecond);
            builder.Append("keepColliderWhenDestroyed=")
                .Append(keepColliderWhenDestroyed ? '1' : '0')
                .Append('|');
            return DeterministicFingerprint64(builder.ToString());
        }

        private static void Append(
            StringBuilder builder,
            string key,
            double value)
        {
            builder.Append(key)
                .Append('=')
                .Append(value.ToString("R", CultureInfo.InvariantCulture))
                .Append('|');
        }

        private static string DeterministicFingerprint64(string text)
        {
            unchecked
            {
                const ulong offsetBasis = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offsetBasis;
                for (int index = 0; index < text.Length; index++)
                {
                    char value = text[index];
                    hash ^= (byte)(value & 0xff);
                    hash *= prime;
                    hash ^= (byte)(value >> 8);
                    hash *= prime;
                }

                return hash.ToString("x16", CultureInfo.InvariantCulture);
            }
        }

        private string BuildDiagnosticLocation()
        {
            StringBuilder path = new StringBuilder();
            Transform current = transform;
            while (current != null)
            {
                path.Insert(0, "/" + current.name);
                current = current.parent;
            }

            return gameObject.scene.name + ":" + path;
        }

        private void UnregisterRuntimeBindings()
        {
            if (boundContext != null && actorId != null && package != null)
            {
                boundContext.UnregisterTurret(actorId, package);
            }

            if (boundSceneScope != null && actorId != null)
            {
                boundSceneScope.Unregister(actorId, this);
            }

            boundContext = null;
            boundSceneScope = null;
        }

        private void DestroyProjectileTemplate()
        {
            if (projectileTemplate == null)
            {
                return;
            }

            GameObject templateObject = projectileTemplate.gameObject;
            projectileTemplate = null;
            if (UnityEngine.Application.isPlaying)
            {
                Destroy(templateObject);
            }
            else
            {
                DestroyImmediate(templateObject);
            }
        }

        private bool Fail(string diagnostic)
        {
            lastBindingDiagnostic = string.IsNullOrEmpty(diagnostic)
                ? "Blaster Turret binding failed without a diagnostic."
                : diagnostic;
            return false;
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
