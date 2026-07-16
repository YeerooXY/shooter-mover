using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UnityAdapters.Authoring;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.Sources
{
    /// <summary>
    /// Thin Unity adapter for one authored reward source. It owns configuration and
    /// deterministic request projection only. It never generates rewards, mutates a
    /// wallet/holding, stores claim truth, or searches the scene globally.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardSourceAuthoring2D : MonoBehaviour, IRestartParticipant
    {
        [SerializeField] private PlacedObjectAuthoring2D placedObject;
        [SerializeField] private ScriptableObject inheritedProfileSource;
        [SerializeField] private RewardSourceOverrideAuthoring sourceOverride =
            new RewardSourceOverrideAuthoring();
        [SerializeField] private MonoBehaviour operationSink;
        [SerializeField] private bool registerForRestartOnEnable = true;

        private RewardSourceResolvedPreview _resolvedPreview;
        private RewardSourceResolutionResult _lastResolution;
        private RestartParticipantRegistrationResult _lastRestartRegistration;

        public RewardSourceResolutionResult LastResolution
        {
            get { return _lastResolution; }
        }

        public RestartParticipantRegistrationResult LastRestartRegistration
        {
            get { return _lastRestartRegistration; }
        }

        public StableId RestartParticipantId
        {
            get
            {
                if (_resolvedPreview == null)
                {
                    throw new InvalidOperationException(
                        "Reward source must resolve before its restart identity is read.");
                }

                return _resolvedPreview.RestartParticipantId;
            }
        }

        private void OnEnable()
        {
            if (registerForRestartOnEnable)
            {
                RegisterForRestart();
            }
        }

        private void OnDisable()
        {
            UnregisterFromRestart();
        }

        private void OnDestroy()
        {
            UnregisterFromRestart();
        }

        public RewardSourceResolutionResult ResolvePreview()
        {
            PlacedObjectAuthoring2D resolvedPlaced = placedObject;
            if (resolvedPlaced == null)
            {
                resolvedPlaced = GetComponent<PlacedObjectAuthoring2D>();
            }

            if (resolvedPlaced == null)
            {
                return SetFailure(
                    RewardSourceResolutionStatus.MissingPlacedObject,
                    "Reward source requires an explicitly assigned or co-located PlacedObjectAuthoring2D.");
            }

            SceneScopeBindingResult binding = resolvedPlaced.TryBind();
            if (!binding.IsBound)
            {
                return SetFailure(
                    RewardSourceResolutionStatus.PlacedObjectBindingFailed,
                    binding.Diagnostic);
            }

            if (inheritedProfileSource == null)
            {
                return SetFailure(
                    RewardSourceResolutionStatus.MissingInheritedProfile,
                    "Reward source requires an inherited reward profile definition.");
            }

            RewardProfileV1 inherited;
            try
            {
                inherited = RewardProfileCapabilityReader.BuildProfile(
                    inheritedProfileSource);
            }
            catch (Exception exception)
            {
                return SetFailure(
                    RewardSourceResolutionStatus.InvalidInheritedProfile,
                    exception.Message);
            }

            RewardProfileV1 resolved;
            RewardSourceOverrideAuthoringMode mode = sourceOverride == null
                ? RewardSourceOverrideAuthoringMode.Inherit
                : sourceOverride.Mode;
            try
            {
                resolved = (sourceOverride ?? RewardSourceOverrideAuthoring.Inherit(
                    "reward-override.default")).Resolve(
                        resolvedPlaced.ResolvedIdentity.Value,
                        inherited);
            }
            catch (Exception exception)
            {
                return SetFailure(
                    RewardSourceResolutionStatus.InvalidOverride,
                    exception.Message);
            }

            StableId runId = resolvedPlaced.BoundScope.RunId;
            StableId sourceId = resolvedPlaced.ResolvedIdentity.Value;
            StableId operationId = DeriveStableId(
                "reward-operation",
                runId + "|" + sourceId);
            StableId commitmentId = DeriveStableId(
                "reward-commitment",
                runId + "|" + sourceId);
            StableId restartId = DeriveStableId(
                "reward-restart",
                runId + "|" + sourceId);
            RewardOperationRequestV1 request = RewardOperationRequestV1.Create(
                runId,
                sourceId,
                operationId,
                commitmentId,
                resolved.ProfileStableId,
                resolved.Fingerprint);
            string previewFingerprint = Sha256(
                "mode=" + ((int)mode).ToString(CultureInfo.InvariantCulture)
                + "\ninherited=" + inherited.Fingerprint
                + "\nresolved=" + resolved.Fingerprint
                + "\nrequest=" + request.Fingerprint);
            RewardSourceResolvedPreview preview = new RewardSourceResolvedPreview(
                mode,
                inherited,
                resolved,
                request,
                restartId,
                previewFingerprint);

            if (_resolvedPreview != null)
            {
                RewardOperationIdentityComparisonV1 comparison =
                    RewardOperationIdentityV1.Classify(
                        _resolvedPreview.OperationRequest,
                        request);
                if (comparison == RewardOperationIdentityComparisonV1.ConflictingDuplicate)
                {
                    return SetFailure(
                        RewardSourceResolutionStatus.ConflictingResolvedOperation,
                        "The logical source operation was already resolved with a different immutable payload.");
                }

                if (comparison == RewardOperationIdentityComparisonV1.ExactDuplicateNoChange)
                {
                    _lastResolution = RewardSourceResolutionResult.Resolved(
                        _resolvedPreview);
                    return _lastResolution;
                }
            }

            placedObject = resolvedPlaced;
            _resolvedPreview = preview;
            _lastResolution = RewardSourceResolutionResult.Resolved(preview);
            return _lastResolution;
        }

        public RewardSourceSubmissionResult SubmitResolution()
        {
            RewardSourceResolutionResult resolution = ResolvePreview();
            if (!resolution.IsResolved)
            {
                return new RewardSourceSubmissionResult(
                    RewardSourceSubmissionStatus.Rejected,
                    resolution.Diagnostic);
            }

            IRewardSourceOperationSink sink = operationSink as IRewardSourceOperationSink;
            if (sink == null)
            {
                return new RewardSourceSubmissionResult(
                    RewardSourceSubmissionStatus.Rejected,
                    "Reward source operation sink is missing or incompatible.");
            }

            return sink.Submit(resolution.Preview)
                ?? new RewardSourceSubmissionResult(
                    RewardSourceSubmissionStatus.Rejected,
                    "Reward source operation sink returned no result.");
        }

        public RestartParticipantRegistrationResult RegisterForRestart()
        {
            RewardSourceResolutionResult resolution = ResolvePreview();
            if (!resolution.IsResolved || placedObject == null)
            {
                _lastRestartRegistration = RestartParticipantRegistrationResult.Invalid(
                    resolution.Diagnostic);
                return _lastRestartRegistration;
            }

            _lastRestartRegistration = placedObject.RegisterRestartParticipant(
                this,
                this,
                BuildDiagnosticLocation());
            return _lastRestartRegistration;
        }

        public void OnRestartPhase(
            RestartContext context,
            RestartLifecyclePhase phase)
        {
            if (_resolvedPreview == null || context == null)
            {
                return;
            }

            if (!context.RunId.Equals(
                _resolvedPreview.OperationRequest.RunStableId))
            {
                throw new InvalidOperationException(
                    "Reward source received restart context for a different run.");
            }

            // Attempt-local projection may change. The run/source operation identity,
            // resolved request, and any external claim truth intentionally remain intact.
        }

        public void ConfigureForTests(
            PlacedObjectAuthoring2D placedObject,
            ScriptableObject inheritedProfileSource,
            RewardSourceOverrideAuthoring sourceOverride,
            MonoBehaviour operationSink,
            bool registerForRestartOnEnable = false)
        {
            UnregisterFromRestart();
            this.placedObject = placedObject;
            this.inheritedProfileSource = inheritedProfileSource;
            this.sourceOverride = sourceOverride ?? RewardSourceOverrideAuthoring.Inherit(
                "reward-override.default");
            this.operationSink = operationSink;
            this.registerForRestartOnEnable = registerForRestartOnEnable;
            _resolvedPreview = null;
            _lastResolution = null;
            _lastRestartRegistration = null;
        }

        private RewardSourceResolutionResult SetFailure(
            RewardSourceResolutionStatus status,
            string diagnostic)
        {
            _lastResolution = RewardSourceResolutionResult.Failed(status, diagnostic);
            return _lastResolution;
        }

        private void UnregisterFromRestart()
        {
            if (placedObject != null && _resolvedPreview != null)
            {
                placedObject.UnregisterRestartParticipant(
                    _resolvedPreview.RestartParticipantId,
                    this);
            }
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

        private static StableId DeriveStableId(string namespaceName, string input)
        {
            return StableId.Create(namespaceName, Fnv64(input));
        }

        private static string Fnv64(string input)
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

        private static string Sha256(string input)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder("sha256:");
                for (int index = 0; index < bytes.Length; index++)
                {
                    builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }
}
