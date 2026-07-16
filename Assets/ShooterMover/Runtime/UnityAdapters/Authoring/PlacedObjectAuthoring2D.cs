using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring
{
    [Serializable]
    public sealed class CapabilityOverrideAuthoring
    {
        [SerializeField] private CapabilityOverrideMode mode =
            CapabilityOverrideMode.Inherit;
        [SerializeField] private string capabilityId = "capability.unassigned";
        [SerializeField] private ScriptableObject overrideDefinition;

        public CapabilityOverrideAuthoring()
        {
        }

        private CapabilityOverrideAuthoring(
            CapabilityOverrideMode mode,
            string capabilityId,
            ScriptableObject overrideDefinition)
        {
            this.mode = mode;
            this.capabilityId = capabilityId;
            this.overrideDefinition = overrideDefinition;
        }

        public CapabilityOverrideMode Mode
        {
            get { return mode; }
        }

        public string CapabilityIdText
        {
            get { return capabilityId; }
        }

        public ScriptableObject OverrideDefinition
        {
            get { return overrideDefinition; }
        }

        public CapabilityOverride BuildOverride()
        {
            if (mode == CapabilityOverrideMode.Inherit)
            {
                return CapabilityOverride.Inherit(StableId.Parse(capabilityId));
            }

            IObjectCapabilityDefinitionSource source =
                overrideDefinition as IObjectCapabilityDefinitionSource;
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"Capability '{capabilityId}' override requires an asset "
                    + "implementing IObjectCapabilityDefinitionSource.");
            }

            CapabilityDefinition definition = source.BuildDefinition();
            StableId parsedCapabilityId = StableId.Parse(capabilityId);
            if (!parsedCapabilityId.Equals(definition.CapabilityId))
            {
                throw new InvalidOperationException(
                    $"Capability override ID '{parsedCapabilityId}' does not match "
                    + $"definition ID '{definition.CapabilityId}'.");
            }

            return CapabilityOverride.Override(definition);
        }

        public static CapabilityOverrideAuthoring Inherit(string capabilityId)
        {
            return new CapabilityOverrideAuthoring(
                CapabilityOverrideMode.Inherit,
                capabilityId ?? throw new ArgumentNullException(nameof(capabilityId)),
                null);
        }

        public static CapabilityOverrideAuthoring Override(
            string capabilityId,
            ScriptableObject definition)
        {
            return new CapabilityOverrideAuthoring(
                CapabilityOverrideMode.Override,
                capabilityId ?? throw new ArgumentNullException(nameof(capabilityId)),
                definition ?? throw new ArgumentNullException(nameof(definition)));
        }
    }

    /// <summary>
    /// Generic Unity translation boundary for one persistent authored placement.
    /// It resolves data and registration only; package authorities remain separate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacedObjectAuthoring2D : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string authoredPlacedInstanceId =
            "placed.unassigned";

        [Header("Definition")]
        [SerializeField] private ScriptableObject familyDefinition;
        [SerializeField] private string selectedVariantId = "variant.unassigned";
        [SerializeField] private CapabilityOverrideAuthoring[] capabilityOverrides =
            Array.Empty<CapabilityOverrideAuthoring>();

        [Header("Scope")]
        [SerializeField] private GameplaySceneScope2D explicitScope;
        [SerializeField] private string requiredScopeCompatibilityId =
            "scope.gameplay";
        [SerializeField] private bool bindOnEnable = true;

        private RuntimeSpawnIdentityInput _runtimeSpawnIdentityInput;
        private GameplaySceneScope2D _boundScope;
        private PlacedObjectIdentity _resolvedIdentity;
        private ObjectDefinitionReference _resolvedDefinitionReference;
        private ResolvedCapabilitySet _resolvedCapabilities;
        private ObjectDefinitionResolutionResult _resolutionResult;
        private SceneScopeBindingResult _lastBindingResult;

        public GameplaySceneScope2D BoundScope
        {
            get { return _boundScope; }
        }

        public PlacedObjectIdentity ResolvedIdentity
        {
            get { return _resolvedIdentity; }
        }

        public ObjectDefinitionReference ResolvedDefinitionReference
        {
            get { return _resolvedDefinitionReference; }
        }

        public ResolvedCapabilitySet ResolvedCapabilities
        {
            get { return _resolvedCapabilities; }
        }

        public ObjectDefinitionResolutionResult ResolutionResult
        {
            get { return _resolutionResult; }
        }

        public SceneScopeBindingResult LastBindingResult
        {
            get { return _lastBindingResult; }
        }

        private void OnEnable()
        {
            if (bindOnEnable)
            {
                TryBind();
            }
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        /// <summary>
        /// Runtime-spawned identity is accepted only as explicit stable input.
        /// It must be supplied before binding.
        /// </summary>
        public void SetRuntimeSpawnIdentity(RuntimeSpawnIdentityInput input)
        {
            if (_boundScope != null)
            {
                throw new InvalidOperationException(
                    "Runtime spawn identity cannot change while the object is bound.");
            }

            _runtimeSpawnIdentityInput = input
                ?? throw new ArgumentNullException(nameof(input));
        }

        public void ClearRuntimeSpawnIdentity()
        {
            if (_boundScope != null)
            {
                throw new InvalidOperationException(
                    "Runtime spawn identity cannot change while the object is bound.");
            }

            _runtimeSpawnIdentityInput = null;
        }

        public SceneScopeBindingResult TryBind()
        {
            if (_boundScope != null && _resolvedIdentity != null)
            {
                _lastBindingResult = SceneScopeBindingResult.AlreadyBound(
                    _resolvedIdentity.Value,
                    _boundScope.ScopeId);
                return _lastBindingResult;
            }

            _resolutionResult = null;
            PlacedObjectIdentity identity;
            try
            {
                identity = ResolveIdentity();
            }
            catch (Exception exception)
            {
                ClearResolvedState();
                _lastBindingResult = SceneScopeBindingResult.Failed(
                    SceneScopeBindingStatus.InvalidIdentity,
                    SceneScopeBindingDiagnosticCode.AuthoredIdentityIsMalformed,
                    null,
                    null,
                    null,
                    exception.Message);
                return _lastBindingResult;
            }

            ObjectDefinitionResolutionResult resolution;
            try
            {
                resolution = ResolveDefinition();
            }
            catch (Exception exception)
            {
                ClearResolvedState();
                _lastBindingResult = SceneScopeBindingResult.Failed(
                    SceneScopeBindingStatus.InvalidDefinition,
                    SceneScopeBindingDiagnosticCode.FamilyDefinitionIsInvalid,
                    identity.Value,
                    null,
                    null,
                    exception.Message);
                return _lastBindingResult;
            }

            _resolutionResult = resolution;
            if (!resolution.IsResolved)
            {
                ClearBoundState();
                _lastBindingResult = SceneScopeBindingResult.Failed(
                    SceneScopeBindingStatus.InvalidDefinition,
                    SceneScopeBindingDiagnosticCode.VariantResolutionFailed,
                    identity.Value,
                    null,
                    null,
                    resolution.Message);
                return _lastBindingResult;
            }

            StableId requiredCompatibility;
            if (!StableId.TryParse(
                requiredScopeCompatibilityId,
                out requiredCompatibility))
            {
                ClearBoundState();
                _lastBindingResult = SceneScopeBindingResult.Failed(
                    SceneScopeBindingStatus.InvalidDefinition,
                    SceneScopeBindingDiagnosticCode.ScopeConfigurationIsInvalid,
                    identity.Value,
                    null,
                    null,
                    "Required scope compatibility is not a canonical StableId.");
                return _lastBindingResult;
            }

            GameplaySceneScope2D scope;
            SceneScopeBindingResult scopeFailure;
            if (!TryResolveScope(
                requiredCompatibility,
                identity.Value,
                out scope,
                out scopeFailure))
            {
                ClearBoundState();
                _lastBindingResult = scopeFailure;
                return _lastBindingResult;
            }

            List<CapabilityReference> capabilities =
                new List<CapabilityReference>(
                    resolution.ResolvedCapabilities.Capabilities.Count);
            for (int index = 0;
                index < resolution.ResolvedCapabilities.Capabilities.Count;
                index++)
            {
                capabilities.Add(
                    new CapabilityReference(
                        resolution.ResolvedCapabilities
                            .Capabilities[index]
                            .CapabilityId));
            }

            PlacedParticipantRegistration registration =
                new PlacedParticipantRegistration(
                    identity,
                    resolution.DefinitionReference,
                    scope.RuntimeProjectionId,
                    scope.RunId,
                    scope.AttemptGeneration,
                    capabilities,
                    resolution.ResolvedCapabilities.Fingerprint);
            SceneScopeRegistrationResult registrationResult =
                scope.Register(
                    new SceneScopeRegistrationRequest(
                        registration,
                        this,
                        BuildDiagnosticLocation()));

            if (!registrationResult.IsAccepted)
            {
                ClearBoundState();
                SceneScopeBindingDiagnosticCode code =
                    registrationResult.Status
                        == SceneScopeRegistrationStatus.RejectedDuplicateIdentity
                        ? SceneScopeBindingDiagnosticCode.DuplicatePlacedIdentity
                        : SceneScopeBindingDiagnosticCode.ConflictingRegistration;
                _lastBindingResult = SceneScopeBindingResult.Failed(
                    SceneScopeBindingStatus.RegistrationRejected,
                    code,
                    identity.Value,
                    scope.ScopeId,
                    registrationResult,
                    registrationResult.Diagnostic);
                return _lastBindingResult;
            }

            _boundScope = scope;
            _resolvedIdentity = identity;
            _resolvedDefinitionReference = resolution.DefinitionReference;
            _resolvedCapabilities = resolution.ResolvedCapabilities;
            _lastBindingResult = SceneScopeBindingResult.Bound(
                identity.Value,
                scope.ScopeId,
                registrationResult);
            return _lastBindingResult;
        }

        public void Unbind()
        {
            if (_boundScope != null && _resolvedIdentity != null)
            {
                _boundScope.Unregister(_resolvedIdentity.Value, this);
            }

            ClearBoundState();
        }

        public RestartParticipantRegistrationResult RegisterRestartParticipant(
            IRestartParticipant participant,
            object ownerToken,
            string diagnosticLocation)
        {
            if (_boundScope == null)
            {
                return RestartParticipantRegistrationResult.Invalid(
                    "Placed object must be bound before registering a restart participant.");
            }

            return _boundScope.RegisterRestartParticipant(
                new RestartParticipantRegistrationRequest(
                    participant,
                    ownerToken,
                    diagnosticLocation));
        }

        public bool UnregisterRestartParticipant(
            StableId participantId,
            object ownerToken)
        {
            return _boundScope != null
                && _boundScope.UnregisterRestartParticipant(
                    participantId,
                    ownerToken);
        }

        public void ConfigureForTests(
            string authoredPlacedInstanceId,
            ScriptableObject familyDefinition,
            string selectedVariantId,
            GameplaySceneScope2D explicitScope,
            string requiredScopeCompatibilityId,
            CapabilityOverrideAuthoring[] capabilityOverrides)
        {
            if (_boundScope != null)
            {
                throw new InvalidOperationException(
                    "Cannot reconfigure a bound placed object.");
            }

            ClearResolvedState();
            this.authoredPlacedInstanceId = authoredPlacedInstanceId;
            this.familyDefinition = familyDefinition;
            this.selectedVariantId = selectedVariantId;
            this.explicitScope = explicitScope;
            this.requiredScopeCompatibilityId = requiredScopeCompatibilityId;
            this.capabilityOverrides = capabilityOverrides
                ?? Array.Empty<CapabilityOverrideAuthoring>();
            bindOnEnable = false;
        }

        private PlacedObjectIdentity ResolveIdentity()
        {
            if (_runtimeSpawnIdentityInput != null)
            {
                return _runtimeSpawnIdentityInput.CreateIdentity();
            }

            return PlacedObjectIdentity.CreateAuthored(
                StableId.Parse(authoredPlacedInstanceId));
        }

        private ObjectDefinitionResolutionResult ResolveDefinition()
        {
            IObjectFamilyDefinitionSource source =
                familyDefinition as IObjectFamilyDefinitionSource;
            if (source == null)
            {
                throw new InvalidOperationException(
                    "Family definition must implement IObjectFamilyDefinitionSource.");
            }

            ObjectFamilyDefinition family = source.BuildDefinition();
            StableId variantId = string.IsNullOrEmpty(selectedVariantId)
                ? null
                : StableId.Parse(selectedVariantId);

            List<CapabilityOverride> overrides =
                new List<CapabilityOverride>(
                    capabilityOverrides == null ? 0 : capabilityOverrides.Length);
            if (capabilityOverrides != null)
            {
                for (int index = 0; index < capabilityOverrides.Length; index++)
                {
                    CapabilityOverrideAuthoring authoredOverride =
                        capabilityOverrides[index];
                    if (authoredOverride == null)
                    {
                        throw new InvalidOperationException(
                            "Capability overrides cannot contain null entries.");
                    }

                    overrides.Add(authoredOverride.BuildOverride());
                }
            }

            return ObjectDefinitionResolver.Resolve(family, variantId, overrides);
        }

        private bool TryResolveScope(
            StableId requiredCompatibility,
            StableId placedInstanceId,
            out GameplaySceneScope2D resolvedScope,
            out SceneScopeBindingResult failure)
        {
            if (explicitScope != null)
            {
                if (explicitScope.gameObject.scene != gameObject.scene)
                {
                    resolvedScope = null;
                    failure = SceneScopeBindingResult.Failed(
                        SceneScopeBindingStatus.CrossSceneExplicitScope,
                        SceneScopeBindingDiagnosticCode
                            .ExplicitScopeCrossesSceneBoundary,
                        placedInstanceId,
                        null,
                        null,
                        "Explicit scene scope belongs to a different loaded scene.");
                    return false;
                }

                if (!explicitScope.IsCompatible(requiredCompatibility))
                {
                    StableId explicitId = explicitScope.IsConfigured
                        ? explicitScope.ScopeId
                        : null;
                    resolvedScope = null;
                    failure = SceneScopeBindingResult.Failed(
                        SceneScopeBindingStatus.IncompatibleExplicitScope,
                        SceneScopeBindingDiagnosticCode.ExplicitScopeIsIncompatible,
                        placedInstanceId,
                        explicitId,
                        null,
                        "Explicit scene scope is invalid or incompatible.");
                    return false;
                }

                resolvedScope = explicitScope;
                failure = null;
                return true;
            }

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
                    failure = SceneScopeBindingResult.Failed(
                        SceneScopeBindingStatus.ConflictingParentScopes,
                        SceneScopeBindingDiagnosticCode
                            .MultipleCompatibleScopesAtNearestAncestor,
                        placedInstanceId,
                        null,
                        null,
                        "Nearest ancestor exposes more than one compatible scope.");
                    return false;
                }

                if (compatibleCount == 1)
                {
                    resolvedScope = compatible;
                    failure = null;
                    return true;
                }

                ancestor = ancestor.parent;
            }

            resolvedScope = null;
            failure = SceneScopeBindingResult.Failed(
                SceneScopeBindingStatus.MissingScope,
                SceneScopeBindingDiagnosticCode.MissingRequiredScope,
                placedInstanceId,
                null,
                null,
                "No compatible explicit or ancestor scene scope is available.");
            return false;
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

        private void ClearBoundState()
        {
            _boundScope = null;
            _resolvedIdentity = null;
            _resolvedDefinitionReference = null;
            _resolvedCapabilities = null;
        }

        private void ClearResolvedState()
        {
            ClearBoundState();
            _resolutionResult = null;
        }
    }
}
