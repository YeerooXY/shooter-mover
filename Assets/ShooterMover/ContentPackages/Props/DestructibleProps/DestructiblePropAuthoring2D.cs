using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    public enum DestructiblePropConfigurationStatus
    {
        Configured = 0,
        AlreadyConfigured = 1,
        MissingPlacedObject = 2,
        PlacedObjectBindingFailed = 3,
        DefinitionMismatch = 4,
        InvalidDefinition = 5,
        MissingBlockingCollider = 6,
        MissingIntactRenderer = 7,
        ColliderTypeMismatch = 8,
        TargetRegistrationFailed = 9,
        RestartRegistrationFailed = 10,
        RewardSourceResolutionFailed = 11
    }

    public sealed class DestructiblePropConfigurationResult
    {
        internal DestructiblePropConfigurationResult(
            DestructiblePropConfigurationStatus status,
            string diagnostic,
            DestructiblePropResolvedPreview preview,
            DestructibleProp2D runtimeProp)
        {
            Status = status;
            Diagnostic = diagnostic ?? string.Empty;
            Preview = preview;
            RuntimeProp = runtimeProp;
        }

        public DestructiblePropConfigurationStatus Status { get; }
        public string Diagnostic { get; }
        public DestructiblePropResolvedPreview Preview { get; }
        public DestructibleProp2D RuntimeProp { get; }
        public bool IsConfigured =>
            Status == DestructiblePropConfigurationStatus.Configured
            || Status == DestructiblePropConfigurationStatus.AlreadyConfigured;
    }

    /// <summary>
    /// Definition-to-runtime boundary for one placed destructible prop. Identity comes
    /// from OBJ-001; every scene reference is explicit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DestructiblePropAuthoring2D : MonoBehaviour, IRestartParticipant
    {
        [Header("Legacy migration values")]
        [Min(0.01f)]
        [SerializeField] private float maximumHealth = 24f;
        [SerializeField] private Vector2 colliderSize = new Vector2(2.2f, 1.35f);
        [SerializeField] private Vector2 colliderOffset = Vector2.zero;
        [SerializeField] private DestructiblePropDestructionAnimation destructionAnimation;

        [Header("Definition and identity")]
        [SerializeField] private PlacedObjectAuthoring2D placedObject;
        [SerializeField] private DestructiblePropFamilyDefinitionAsset familyDefinition;
        [SerializeField] private DestructiblePropValueOverrides instanceOverrides =
            new DestructiblePropValueOverrides();

        [Header("Explicit scene references")]
        [SerializeField] private Collider2D blockingCollider;
        [SerializeField] private SpriteRenderer intactRenderer;
        [SerializeField] private Transform destructionAnimationAnchor;

        [Header("Combat")]
        [Min(0.01f)]
        [SerializeField] private float confirmedHitDamage = 6f;

        [Header("Reward source")]
        [SerializeField] private RewardSourceAuthoring2D rewardSource;
        [SerializeField] private RewardSourceOverrideAuthoring rewardOverride =
            new RewardSourceOverrideAuthoring();
        [SerializeField] private MonoBehaviour rewardOperationSink;

        private CombatHit2DAdapter hitAdapter;
        private GameplaySceneScope2D registeredRestartScope;
        private DestructibleProp2D runtimeProp;
        private DestructiblePropRewardBridge2D rewardBridge;
        private DestructiblePropResolvedPreview resolvedPreview;
        private DestructiblePropConfigurationResult lastConfiguration;
        private RestartParticipantRegistrationResult restartRegistration;
        private StableId restartParticipantId;
        private DestructiblePropTerminalProvenanceV1 generatedTerminalProvenance;
        private bool targetRegistered;

        public double MaximumHealth => resolvedPreview == null
            ? maximumHealth
            : resolvedPreview.Values.MaximumHealth;
        public Vector2 ColliderSize => resolvedPreview == null
            ? colliderSize
            : resolvedPreview.Values.ColliderSize;
        public Vector2 ColliderOffset => resolvedPreview == null
            ? colliderOffset
            : resolvedPreview.Values.ColliderOffset;
        public DestructiblePropDestructionAnimation DestructionAnimation =>
            resolvedPreview == null
                ? destructionAnimation
                : resolvedPreview.Values.DestructionAnimation;
        public DestructibleProp2D RuntimeProp => runtimeProp;
        public DestructiblePropResolvedPreview ResolvedPreview => resolvedPreview;
        public DestructiblePropConfigurationResult LastConfiguration => lastConfiguration;
        public RestartParticipantRegistrationResult LastRestartRegistration =>
            restartRegistration;
        public DestructiblePropRewardBridge2D RewardBridge => rewardBridge;
        public DestructiblePropTerminalProvenanceV1 GeneratedTerminalProvenance =>
            generatedTerminalProvenance;

        public StableId RestartParticipantId
        {
            get
            {
                if (restartParticipantId == null)
                {
                    throw new InvalidOperationException(
                        "Destructible prop must configure before its restart ID is read.");
                }
                return restartParticipantId;
            }
        }

        public void ConfigureGenerated(
            double configuredMaximumHealth,
            Vector2 configuredColliderSize,
            Vector2 configuredColliderOffset,
            DestructiblePropDestructionAnimation configuredAnimation)
        {
            ConfigureGenerated(
                configuredMaximumHealth,
                configuredColliderSize,
                configuredColliderOffset,
                configuredAnimation,
                Stage1TerminalDropContentV1.ResolveLegacyAuthoringKey(
                    gameObject.name));
        }

        public void ConfigureGenerated(
            double configuredMaximumHealth,
            Vector2 configuredColliderSize,
            Vector2 configuredColliderOffset,
            DestructiblePropDestructionAnimation configuredAnimation,
            DestructiblePropTerminalProvenanceV1 configuredTerminalProvenance)
        {
            ValidatePositive(configuredMaximumHealth, nameof(configuredMaximumHealth));
            DestructiblePropDefinitionValues.RequirePositiveVector(
                configuredColliderSize,
                nameof(configuredColliderSize));
            DestructiblePropDefinitionValues.RequireVectorFinite(
                configuredColliderOffset,
                nameof(configuredColliderOffset));
            maximumHealth = (float)configuredMaximumHealth;
            colliderSize = configuredColliderSize;
            colliderOffset = configuredColliderOffset;
            destructionAnimation = configuredAnimation;
            generatedTerminalProvenance = configuredTerminalProvenance;
        }

        public DestructiblePropConfigurationResult TryConfigure(
            CombatHit2DAdapter configuredHitAdapter)
        {
            if (runtimeProp != null && runtimeProp.IsConfigured)
            {
                return Result(
                    DestructiblePropConfigurationStatus.AlreadyConfigured,
                    "Destructible prop is already configured.",
                    resolvedPreview,
                    runtimeProp);
            }
            if (configuredHitAdapter == null)
            {
                return Failure(
                    DestructiblePropConfigurationStatus.TargetRegistrationFailed,
                    "An explicit CombatHit2DAdapter is required.");
            }
            if (!IsPositiveFinite(confirmedHitDamage))
            {
                return Failure(
                    DestructiblePropConfigurationStatus.InvalidDefinition,
                    "Confirmed hit damage must be positive and finite.");
            }

            PlacedObjectAuthoring2D resolvedPlaced = placedObject == null
                ? GetComponent<PlacedObjectAuthoring2D>()
                : placedObject;
            if (resolvedPlaced == null)
            {
                return Failure(
                    DestructiblePropConfigurationStatus.MissingPlacedObject,
                    "An explicit or co-located PlacedObjectAuthoring2D is required.");
            }

            SceneScopeBindingResult binding = resolvedPlaced.TryBind();
            if (!binding.IsBound || resolvedPlaced.BoundScope == null)
            {
                return Failure(
                    DestructiblePropConfigurationStatus.PlacedObjectBindingFailed,
                    binding.Diagnostic);
            }

            GameplaySceneScope2D resolvedScope = resolvedPlaced.BoundScope;
            if (familyDefinition == null)
            {
                return Failure(
                    DestructiblePropConfigurationStatus.InvalidDefinition,
                    "Destructible prop family definition is missing.");
            }
            if (resolvedPlaced.ResolvedDefinitionReference == null
                || !resolvedPlaced.ResolvedDefinitionReference.FamilyId.Equals(
                    familyDefinition.FamilyId))
            {
                return Failure(
                    DestructiblePropConfigurationStatus.DefinitionMismatch,
                    "Placed-object and destructible-prop families do not match.");
            }
            if (blockingCollider == null)
            {
                return Failure(
                    DestructiblePropConfigurationStatus.MissingBlockingCollider,
                    "An explicit blocking Collider2D is required.");
            }
            if (intactRenderer == null)
            {
                return Failure(
                    DestructiblePropConfigurationStatus.MissingIntactRenderer,
                    "An explicit intact SpriteRenderer is required.");
            }

            DestructiblePropResolvedPreview preview;
            try
            {
                preview = familyDefinition.Resolve(
                    resolvedPlaced.ResolvedDefinitionReference.VariantId,
                    instanceOverrides,
                    resolvedPlaced.ResolvedIdentity.Value);
            }
            catch (Exception exception)
            {
                return Failure(
                    DestructiblePropConfigurationStatus.InvalidDefinition,
                    exception.Message);
            }

            string colliderDiagnostic;
            if (!ApplyColliderValues(blockingCollider, preview.Values, out colliderDiagnostic))
            {
                return Failure(
                    DestructiblePropConfigurationStatus.ColliderTypeMismatch,
                    colliderDiagnostic);
            }
            if (preview.Values.IntactSprite != null)
                intactRenderer.sprite = preview.Values.IntactSprite;

            RewardSourceAuthoring2D configuredRewardSource;
            string rewardDiagnostic;
            if (!TryPrepareRewardSource(
                resolvedPlaced,
                preview,
                out configuredRewardSource,
                out rewardDiagnostic))
            {
                return Failure(
                    DestructiblePropConfigurationStatus.RewardSourceResolutionFailed,
                    rewardDiagnostic);
            }

            restartParticipantId = StableId.Create(
                "prop-restart",
                Fingerprint64(resolvedPlaced.ResolvedIdentity.Value.ToString()));
            RestartParticipantRegistrationResult restart =
                resolvedPlaced.RegisterRestartParticipant(
                    this,
                    this,
                    BuildDiagnosticLocation());
            if (!restart.IsAccepted)
            {
                restartParticipantId = null;
                return Failure(
                    DestructiblePropConfigurationStatus.RestartRegistrationFailed,
                    restart.Diagnostic);
            }

            CombatHit2DTargetRegistrationStatus target =
                configuredHitAdapter.RegisterTarget(
                    blockingCollider,
                    resolvedPlaced.ResolvedIdentity.Value);
            if (target != CombatHit2DTargetRegistrationStatus.Registered
                && target != CombatHit2DTargetRegistrationStatus.AlreadyRegistered)
            {
                resolvedScope.UnregisterRestartParticipant(restartParticipantId, this);
                restartParticipantId = null;
                return Failure(
                    DestructiblePropConfigurationStatus.TargetRegistrationFailed,
                    "Combat target registration failed: " + target + ".");
            }

            try
            {
                DestructibleProp2D prop =
                    blockingCollider.GetComponent<DestructibleProp2D>()
                    ?? blockingCollider.gameObject.AddComponent<DestructibleProp2D>();
                prop.Configure(
                    resolvedPlaced.ResolvedIdentity.Value,
                    preview.Values.MaximumHealth,
                    blockingCollider,
                    new Renderer[] { intactRenderer },
                    preview.Values.DestroyedCollisionPolicy,
                    BuildDefinitionTerminalProvenance(preview));

                DestructiblePropProjectileRelay2D relay =
                    blockingCollider.GetComponent<DestructiblePropProjectileRelay2D>()
                    ?? blockingCollider.gameObject
                        .AddComponent<DestructiblePropProjectileRelay2D>();
                if (!relay.IsConfigured)
                    relay.Configure(prop, confirmedHitDamage);

                DestructiblePropDestructionPlayer2D player =
                    blockingCollider.GetComponent<DestructiblePropDestructionPlayer2D>()
                    ?? blockingCollider.gameObject
                        .AddComponent<DestructiblePropDestructionPlayer2D>();
                player.Configure(
                    prop,
                    destructionAnimationAnchor == null
                        ? intactRenderer.transform
                        : destructionAnimationAnchor,
                    preview.Values.DestructionAnimation);

                DestructiblePropRewardBridge2D bridge = null;
                if (configuredRewardSource != null)
                {
                    bridge = blockingCollider.GetComponent<DestructiblePropRewardBridge2D>()
                        ?? blockingCollider.gameObject
                            .AddComponent<DestructiblePropRewardBridge2D>();
                    bridge.Configure(prop, configuredRewardSource);
                }

                placedObject = resolvedPlaced;
                hitAdapter = configuredHitAdapter;
                registeredRestartScope = resolvedScope;
                runtimeProp = prop;
                rewardSource = configuredRewardSource;
                rewardBridge = bridge;
                resolvedPreview = preview;
                restartRegistration = restart;
                targetRegistered = true;
            }
            catch (Exception exception)
            {
                configuredHitAdapter.UnregisterTarget(
                    blockingCollider,
                    resolvedPlaced.ResolvedIdentity.Value);
                resolvedScope.UnregisterRestartParticipant(restartParticipantId, this);
                restartParticipantId = null;
                return Failure(
                    DestructiblePropConfigurationStatus.InvalidDefinition,
                    exception.Message);
            }

            return Result(
                DestructiblePropConfigurationStatus.Configured,
                "Destructible prop configured.",
                preview,
                runtimeProp);
        }

        public void OnRestartPhase(RestartContext context, RestartLifecyclePhase phase)
        {
            if (runtimeProp == null || context == null) return;
            if (registeredRestartScope == null
                || !context.RunId.Equals(registeredRestartScope.RunId))
            {
                throw new InvalidOperationException(
                    "Destructible prop received restart context for a different run.");
            }
            if (phase == RestartLifecyclePhase.ApplyResetProjection)
                runtimeProp.Restart();
        }

        internal void ApplyLegacyConfirmedHitDamage(double value)
        {
            if (runtimeProp != null)
                throw new InvalidOperationException(
                    "Cannot change hit damage after configuration.");
            ValidatePositive(value, nameof(value));
            confirmedHitDamage = (float)value;
        }

        public void ConfigureForTests(
            PlacedObjectAuthoring2D placedObject,
            DestructiblePropFamilyDefinitionAsset familyDefinition,
            DestructiblePropValueOverrides instanceOverrides,
            Collider2D blockingCollider,
            SpriteRenderer intactRenderer,
            Transform destructionAnimationAnchor,
            double confirmedHitDamage,
            RewardSourceAuthoring2D rewardSource,
            RewardSourceOverrideAuthoring rewardOverride,
            MonoBehaviour rewardOperationSink)
        {
            if (runtimeProp != null)
                throw new InvalidOperationException("Cannot reconfigure a live prop.");
            ValidatePositive(confirmedHitDamage, nameof(confirmedHitDamage));
            this.placedObject = placedObject;
            this.familyDefinition = familyDefinition;
            this.instanceOverrides = instanceOverrides ?? new DestructiblePropValueOverrides();
            this.blockingCollider = blockingCollider;
            this.intactRenderer = intactRenderer;
            this.destructionAnimationAnchor = destructionAnimationAnchor;
            this.confirmedHitDamage = (float)confirmedHitDamage;
            this.rewardSource = rewardSource;
            this.rewardOverride = rewardOverride
                ?? RewardSourceOverrideAuthoring.Inherit("reward-override.prop-default");
            this.rewardOperationSink = rewardOperationSink;
        }

        private DestructiblePropTerminalProvenanceV1
            BuildDefinitionTerminalProvenance(
                DestructiblePropResolvedPreview preview)
        {
            if (generatedTerminalProvenance != null)
                return generatedTerminalProvenance;
            if (preview == null
                || preview.Values == null
                || preview.Values.InheritedRewardProfileId == null
                || string.IsNullOrWhiteSpace(preview.ResolvedFingerprint))
            {
                throw new InvalidOperationException(
                    "Definition-authored destructible prop terminal provenance is incomplete.");
            }
            StableId definitionStableId = StableId.Create(
                "prop-definition",
                Fingerprint64(
                    preview.FamilyId
                    + "|"
                    + preview.VariantId
                    + "|"
                    + preview.ResolvedFingerprint));
            return new DestructiblePropTerminalProvenanceV1(
                definitionStableId,
                preview.Values.InheritedRewardProfileId,
                preview.ResolvedFingerprint);
        }

        private bool TryPrepareRewardSource(
            PlacedObjectAuthoring2D resolvedPlaced,
            DestructiblePropResolvedPreview preview,
            out RewardSourceAuthoring2D configuredSource,
            out string diagnostic)
        {
            configuredSource = null;
            diagnostic = string.Empty;
            if (preview.Values.InheritedRewardProfile == null)
                return true;

            configuredSource = rewardSource == null
                ? GetComponent<RewardSourceAuthoring2D>()
                : rewardSource;
            if (configuredSource == null)
                configuredSource = gameObject.AddComponent<RewardSourceAuthoring2D>();

            try
            {
                configuredSource.ConfigureForTests(
                    resolvedPlaced,
                    preview.Values.InheritedRewardProfile,
                    rewardOverride ?? RewardSourceOverrideAuthoring.Inherit(
                        "reward-override.prop-default"),
                    rewardOperationSink,
                    false);
            }
            catch (Exception exception)
            {
                diagnostic = exception.Message;
                return false;
            }

            RewardSourceResolutionResult resolution = configuredSource.ResolvePreview();
            diagnostic = resolution.Diagnostic;
            return resolution.IsResolved;
        }

        private static bool ApplyColliderValues(
            Collider2D collider,
            DestructiblePropResolvedValues values,
            out string diagnostic)
        {
            BoxCollider2D box = collider as BoxCollider2D;
            CircleCollider2D circle = collider as CircleCollider2D;
            CapsuleCollider2D capsule = collider as CapsuleCollider2D;
            switch (values.ColliderShape)
            {
                case DestructiblePropColliderShape2D.Box when box != null:
                    box.size = values.ColliderSize;
                    box.offset = values.ColliderOffset;
                    diagnostic = string.Empty;
                    return true;
                case DestructiblePropColliderShape2D.Circle when circle != null:
                    circle.radius = Mathf.Max(
                        values.ColliderSize.x,
                        values.ColliderSize.y) * 0.5f;
                    circle.offset = values.ColliderOffset;
                    diagnostic = string.Empty;
                    return true;
                case DestructiblePropColliderShape2D.Capsule when capsule != null:
                    capsule.size = values.ColliderSize;
                    capsule.offset = values.ColliderOffset;
                    diagnostic = string.Empty;
                    return true;
                default:
                    diagnostic = "Resolved collider shape '" + values.ColliderShape
                        + "' does not match explicit collider type '"
                        + collider.GetType().Name + "'.";
                    return false;
            }
        }

        private DestructiblePropConfigurationResult Failure(
            DestructiblePropConfigurationStatus status,
            string diagnostic)
        {
            return Result(status, diagnostic, null, null);
        }

        private DestructiblePropConfigurationResult Result(
            DestructiblePropConfigurationStatus status,
            string diagnostic,
            DestructiblePropResolvedPreview preview,
            DestructibleProp2D prop)
        {
            lastConfiguration = new DestructiblePropConfigurationResult(
                status,
                diagnostic,
                preview,
                prop);
            return lastConfiguration;
        }

        private string BuildDiagnosticLocation()
        {
            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            return gameObject.scene.name + ":" + string.Join("/", names.ToArray());
        }

        private void OnDestroy()
        {
            if (targetRegistered
                && hitAdapter != null
                && blockingCollider != null
                && runtimeProp != null
                && runtimeProp.PropId != null)
            {
                hitAdapter.UnregisterTarget(blockingCollider, runtimeProp.PropId);
            }
            if (registeredRestartScope != null && restartParticipantId != null)
            {
                registeredRestartScope.UnregisterRestartParticipant(
                    restartParticipantId,
                    this);
            }
            registeredRestartScope = null;
        }

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(0.01f, maximumHealth);
            colliderSize.x = Mathf.Max(0.01f, colliderSize.x);
            colliderSize.y = Mathf.Max(0.01f, colliderSize.y);
            confirmedHitDamage = Mathf.Max(0.01f, confirmedHitDamage);
            instanceOverrides = instanceOverrides ?? new DestructiblePropValueOverrides();
            rewardOverride = rewardOverride ?? new RewardSourceOverrideAuthoring();
        }

        private static bool IsPositiveFinite(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value > 0d
                && value <= float.MaxValue;
        }

        private static void ValidatePositive(double value, string parameterName)
        {
            if (!IsPositiveFinite(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }

        private static string Fingerprint64(string input)
        {
            unchecked
            {
                const ulong offset = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offset;
                for (int index = 0; index < input.Length; index++)
                {
                    char value = input[index];
                    hash ^= (byte)(value & 0xff);
                    hash *= prime;
                    hash ^= (byte)(value >> 8);
                    hash *= prime;
                }
                return hash.ToString("x16", CultureInfo.InvariantCulture);
            }
        }
    }
}
