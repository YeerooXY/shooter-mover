using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.Breakables
{
    public enum BreakableConfigurationStatus
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
        LootSourceResolutionFailed = 11
    }

    public sealed class BreakableConfigurationResult
    {
        internal BreakableConfigurationResult(
            BreakableConfigurationStatus status,
            string diagnostic,
            BreakableResolvedPreview preview,
            Breakable runtimeProp)
        {
            Status = status;
            Diagnostic = diagnostic ?? string.Empty;
            Preview = preview;
            RuntimeProp = runtimeProp;
        }

        public BreakableConfigurationStatus Status { get; }
        public string Diagnostic { get; }
        public BreakableResolvedPreview Preview { get; }
        public Breakable RuntimeProp { get; }
        public bool IsConfigured =>
            Status == BreakableConfigurationStatus.Configured
            || Status == BreakableConfigurationStatus.AlreadyConfigured;
    }

    /// <summary>
    /// Definition-to-runtime boundary for one placed breakable. Identity comes
    /// from OBJ-001; every scene reference is explicit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BreakableSetup : MonoBehaviour, IRestartParticipant
    {
        [Header("Legacy migration values")]
        [Min(0.01f)]
        [SerializeField] private float maximumHealth = 24f;
        [SerializeField] private Vector2 colliderSize = new Vector2(2.2f, 1.35f);
        [SerializeField] private Vector2 colliderOffset = Vector2.zero;
        [SerializeField] private BreakableAnimation destructionAnimation;

        [Header("Definition and identity")]
        [SerializeField] private PlacedObject placedObject;
        [SerializeField] private BreakableFamilyDefinitionAsset familyDefinition;
        [SerializeField] private BreakableValueOverrides instanceOverrides =
            new BreakableValueOverrides();

        [Header("Explicit scene references")]
        [SerializeField] private Collider2D blockingCollider;
        [SerializeField] private SpriteRenderer intactRenderer;
        [SerializeField] private Transform destructionAnimationAnchor;

        [Header("Combat")]
        [Min(0.01f)]
        [SerializeField] private float confirmedHitDamage = 6f;

        [Header("Reward source")]
        [SerializeField] private LootSourceSetup rewardSource;
        [SerializeField] private LootSourceOverrideAuthoring rewardOverride =
            new LootSourceOverrideAuthoring();
        [SerializeField] private MonoBehaviour rewardOperationSink;

        private HitResolver hitAdapter;
        private GameplayScene registeredRestartScope;
        private Breakable runtimeProp;
        private BreakableLoot rewardBridge;
        private BreakableResolvedPreview resolvedPreview;
        private BreakableConfigurationResult lastConfiguration;
        private RestartParticipantRegistrationResult restartRegistration;
        private StableId restartParticipantId;
        private BreakableTerminalProvenance generatedTerminalProvenance;
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
        public BreakableAnimation DestructionAnimation =>
            resolvedPreview == null
                ? destructionAnimation
                : resolvedPreview.Values.DestructionAnimation;
        public Breakable RuntimeProp => runtimeProp;
        public BreakableResolvedPreview ResolvedPreview => resolvedPreview;
        public BreakableConfigurationResult LastConfiguration => lastConfiguration;
        public RestartParticipantRegistrationResult LastRestartRegistration =>
            restartRegistration;
        public BreakableLoot RewardBridge => rewardBridge;
        public BreakableTerminalProvenance GeneratedTerminalProvenance =>
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
            BreakableAnimation configuredAnimation)
        {
            ConfigureGenerated(
                configuredMaximumHealth,
                configuredColliderSize,
                configuredColliderOffset,
                configuredAnimation,
                null);
        }

        public void ConfigureGenerated(
            double configuredMaximumHealth,
            Vector2 configuredColliderSize,
            Vector2 configuredColliderOffset,
            BreakableAnimation configuredAnimation,
            BreakableTerminalProvenance configuredTerminalProvenance)
        {
            ValidatePositive(configuredMaximumHealth, nameof(configuredMaximumHealth));
            BreakableDefinitionValues.RequirePositiveVector(
                configuredColliderSize,
                nameof(configuredColliderSize));
            BreakableDefinitionValues.RequireVectorFinite(
                configuredColliderOffset,
                nameof(configuredColliderOffset));
            maximumHealth = (float)configuredMaximumHealth;
            colliderSize = configuredColliderSize;
            colliderOffset = configuredColliderOffset;
            destructionAnimation = configuredAnimation;
            generatedTerminalProvenance = configuredTerminalProvenance;
        }

        public BreakableConfigurationResult TryConfigure(
            HitResolver configuredHitAdapter)
        {
            if (runtimeProp != null && runtimeProp.IsConfigured)
            {
                return Result(
                    BreakableConfigurationStatus.AlreadyConfigured,
                    "Destructible prop is already configured.",
                    resolvedPreview,
                    runtimeProp);
            }
            if (configuredHitAdapter == null)
            {
                return Failure(
                    BreakableConfigurationStatus.TargetRegistrationFailed,
                    "An explicit HitResolver is required.");
            }
            if (!IsPositiveFinite(confirmedHitDamage))
            {
                return Failure(
                    BreakableConfigurationStatus.InvalidDefinition,
                    "Confirmed hit damage must be positive and finite.");
            }

            PlacedObject resolvedPlaced = placedObject == null
                ? GetComponent<PlacedObject>()
                : placedObject;
            if (resolvedPlaced == null)
            {
                return Failure(
                    BreakableConfigurationStatus.MissingPlacedObject,
                    "An explicit or co-located PlacedObject is required.");
            }

            SceneScopeBindingResult binding = resolvedPlaced.TryBind();
            if (!binding.IsBound || resolvedPlaced.BoundScope == null)
            {
                return Failure(
                    BreakableConfigurationStatus.PlacedObjectBindingFailed,
                    binding.Diagnostic);
            }

            GameplayScene resolvedScope = resolvedPlaced.BoundScope;
            if (familyDefinition == null)
            {
                return Failure(
                    BreakableConfigurationStatus.InvalidDefinition,
                    "Destructible prop family definition is missing.");
            }
            if (resolvedPlaced.ResolvedDefinitionReference == null
                || !resolvedPlaced.ResolvedDefinitionReference.FamilyId.Equals(
                    familyDefinition.FamilyId))
            {
                return Failure(
                    BreakableConfigurationStatus.DefinitionMismatch,
                    "Placed-object and breakable families do not match.");
            }
            if (blockingCollider == null)
            {
                return Failure(
                    BreakableConfigurationStatus.MissingBlockingCollider,
                    "An explicit blocking Collider2D is required.");
            }
            if (intactRenderer == null)
            {
                return Failure(
                    BreakableConfigurationStatus.MissingIntactRenderer,
                    "An explicit intact SpriteRenderer is required.");
            }

            BreakableResolvedPreview preview;
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
                    BreakableConfigurationStatus.InvalidDefinition,
                    exception.Message);
            }

            string colliderDiagnostic;
            if (!ApplyColliderValues(blockingCollider, preview.Values, out colliderDiagnostic))
            {
                return Failure(
                    BreakableConfigurationStatus.ColliderTypeMismatch,
                    colliderDiagnostic);
            }
            if (preview.Values.IntactSprite != null)
                intactRenderer.sprite = preview.Values.IntactSprite;

            LootSourceSetup configuredLootSource;
            string rewardDiagnostic;
            if (!TryPrepareLootSource(
                resolvedPlaced,
                preview,
                out configuredLootSource,
                out rewardDiagnostic))
            {
                return Failure(
                    BreakableConfigurationStatus.LootSourceResolutionFailed,
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
                    BreakableConfigurationStatus.RestartRegistrationFailed,
                    restart.Diagnostic);
            }

            HitTargetRegistrationStatus target =
                configuredHitAdapter.RegisterTarget(
                    blockingCollider,
                    resolvedPlaced.ResolvedIdentity.Value);
            if (target != HitTargetRegistrationStatus.Registered
                && target != HitTargetRegistrationStatus.AlreadyRegistered)
            {
                resolvedScope.UnregisterRestartParticipant(restartParticipantId, this);
                restartParticipantId = null;
                return Failure(
                    BreakableConfigurationStatus.TargetRegistrationFailed,
                    "Combat target registration failed: " + target + ".");
            }

            try
            {
                Breakable prop =
                    blockingCollider.GetComponent<Breakable>()
                    ?? blockingCollider.gameObject.AddComponent<Breakable>();
                prop.Configure(
                    resolvedPlaced.ResolvedIdentity.Value,
                    preview.Values.MaximumHealth,
                    blockingCollider,
                    new Renderer[] { intactRenderer },
                    preview.Values.DestroyedCollisionPolicy,
                    BuildDefinitionTerminalProvenance(preview));

                BreakableHitRelay relay =
                    blockingCollider.GetComponent<BreakableHitRelay>()
                    ?? blockingCollider.gameObject
                        .AddComponent<BreakableHitRelay>();
                if (!relay.IsConfigured)
                    relay.Configure(prop, confirmedHitDamage);

                BreakableEffects player =
                    blockingCollider.GetComponent<BreakableEffects>()
                    ?? blockingCollider.gameObject
                        .AddComponent<BreakableEffects>();
                player.Configure(
                    prop,
                    destructionAnimationAnchor == null
                        ? intactRenderer.transform
                        : destructionAnimationAnchor,
                    preview.Values.DestructionAnimation);

                BreakableLoot bridge = null;
                if (configuredLootSource != null)
                {
                    bridge = blockingCollider.GetComponent<BreakableLoot>()
                        ?? blockingCollider.gameObject
                            .AddComponent<BreakableLoot>();
                    bridge.Configure(prop, configuredLootSource);
                }

                placedObject = resolvedPlaced;
                hitAdapter = configuredHitAdapter;
                registeredRestartScope = resolvedScope;
                runtimeProp = prop;
                rewardSource = configuredLootSource;
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
                    BreakableConfigurationStatus.InvalidDefinition,
                    exception.Message);
            }

            return Result(
                BreakableConfigurationStatus.Configured,
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
            PlacedObject placedObject,
            BreakableFamilyDefinitionAsset familyDefinition,
            BreakableValueOverrides instanceOverrides,
            Collider2D blockingCollider,
            SpriteRenderer intactRenderer,
            Transform destructionAnimationAnchor,
            double confirmedHitDamage,
            LootSourceSetup rewardSource,
            LootSourceOverrideAuthoring rewardOverride,
            MonoBehaviour rewardOperationSink)
        {
            if (runtimeProp != null)
                throw new InvalidOperationException("Cannot reconfigure a live prop.");
            ValidatePositive(confirmedHitDamage, nameof(confirmedHitDamage));
            this.placedObject = placedObject;
            this.familyDefinition = familyDefinition;
            this.instanceOverrides = instanceOverrides ?? new BreakableValueOverrides();
            this.blockingCollider = blockingCollider;
            this.intactRenderer = intactRenderer;
            this.destructionAnimationAnchor = destructionAnimationAnchor;
            this.confirmedHitDamage = (float)confirmedHitDamage;
            this.rewardSource = rewardSource;
            this.rewardOverride = rewardOverride
                ?? LootSourceOverrideAuthoring.Inherit("reward-override.prop-default");
            this.rewardOperationSink = rewardOperationSink;
        }

        private BreakableTerminalProvenance
            BuildDefinitionTerminalProvenance(
                BreakableResolvedPreview preview)
        {
            if (generatedTerminalProvenance != null)
                return generatedTerminalProvenance;
            if (preview == null
                || preview.Values == null
                || preview.Values.InheritedRewardProfileId == null
                || string.IsNullOrWhiteSpace(preview.ResolvedFingerprint))
            {
                throw new InvalidOperationException(
                    "Definition-authored breakable terminal provenance is incomplete.");
            }
            StableId definitionStableId = StableId.Create(
                "prop-definition",
                Fingerprint64(
                    preview.FamilyId
                    + "|"
                    + preview.VariantId
                    + "|"
                    + preview.ResolvedFingerprint));
            return new BreakableTerminalProvenance(
                definitionStableId,
                preview.Values.InheritedRewardProfileId,
                preview.ResolvedFingerprint);
        }

        private bool TryPrepareLootSource(
            PlacedObject resolvedPlaced,
            BreakableResolvedPreview preview,
            out LootSourceSetup configuredSource,
            out string diagnostic)
        {
            configuredSource = null;
            diagnostic = string.Empty;
            if (preview.Values.InheritedRewardProfile == null)
                return true;

            configuredSource = rewardSource == null
                ? GetComponent<LootSourceSetup>()
                : rewardSource;
            if (configuredSource == null)
                configuredSource = gameObject.AddComponent<LootSourceSetup>();

            try
            {
                configuredSource.ConfigureForTests(
                    resolvedPlaced,
                    preview.Values.InheritedRewardProfile,
                    rewardOverride ?? LootSourceOverrideAuthoring.Inherit(
                        "reward-override.prop-default"),
                    rewardOperationSink,
                    false);
            }
            catch (Exception exception)
            {
                diagnostic = exception.Message;
                return false;
            }

            LootSourceResolutionResult resolution = configuredSource.ResolvePreview();
            diagnostic = resolution.Diagnostic;
            return resolution.IsResolved;
        }

        private static bool ApplyColliderValues(
            Collider2D collider,
            BreakableResolvedValues values,
            out string diagnostic)
        {
            BoxCollider2D box = collider as BoxCollider2D;
            CircleCollider2D circle = collider as CircleCollider2D;
            CapsuleCollider2D capsule = collider as CapsuleCollider2D;
            switch (values.ColliderShape)
            {
                case BreakableColliderShape.Box when box != null:
                    box.size = values.ColliderSize;
                    box.offset = values.ColliderOffset;
                    diagnostic = string.Empty;
                    return true;
                case BreakableColliderShape.Circle when circle != null:
                    circle.radius = Mathf.Max(
                        values.ColliderSize.x,
                        values.ColliderSize.y) * 0.5f;
                    circle.offset = values.ColliderOffset;
                    diagnostic = string.Empty;
                    return true;
                case BreakableColliderShape.Capsule when capsule != null:
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

        private BreakableConfigurationResult Failure(
            BreakableConfigurationStatus status,
            string diagnostic)
        {
            return Result(status, diagnostic, null, null);
        }

        private BreakableConfigurationResult Result(
            BreakableConfigurationStatus status,
            string diagnostic,
            BreakableResolvedPreview preview,
            Breakable prop)
        {
            lastConfiguration = new BreakableConfigurationResult(
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
            instanceOverrides = instanceOverrides ?? new BreakableValueOverrides();
            rewardOverride = rewardOverride ?? new LootSourceOverrideAuthoring();
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
