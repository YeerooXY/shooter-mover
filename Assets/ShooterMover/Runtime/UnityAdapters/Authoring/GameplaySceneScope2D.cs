using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring
{
    /// <summary>
    /// Generic, explicitly configured registration boundary for one gameplay
    /// projection. It owns no combat, reward, mission, wallet, or persistence truth.
    /// </summary>
    public sealed class GameplaySceneScope2D :
        MonoBehaviour,
        IPlacedObjectSceneScope,
        IRestartParticipantRegistrar
    {
        [SerializeField] private string scopeId = "scope.gameplay";
        [SerializeField] private string compatibilityId = "scope.gameplay";
        [SerializeField] private string runtimeProjectionId = "projection.gameplay";
        [SerializeField] private string runId = "run.current";
        [SerializeField] private long attemptGeneration;

        private readonly SceneScopeRegistrationRegistry _participantRegistry =
            new SceneScopeRegistrationRegistry();
        private readonly RestartParticipantRegistry _restartRegistry =
            new RestartParticipantRegistry();

        private bool _configurationAttempted;
        private bool _isConfigured;
        private string _configurationError;
        private StableId _parsedScopeId;
        private StableId _parsedCompatibilityId;
        private StableId _parsedRuntimeProjectionId;
        private StableId _parsedRunId;

        public StableId ScopeId
        {
            get
            {
                RequireConfigured();
                return _parsedScopeId;
            }
        }

        public StableId CompatibilityId
        {
            get
            {
                RequireConfigured();
                return _parsedCompatibilityId;
            }
        }

        public StableId RuntimeProjectionId
        {
            get
            {
                RequireConfigured();
                return _parsedRuntimeProjectionId;
            }
        }

        public StableId RunId
        {
            get
            {
                RequireConfigured();
                return _parsedRunId;
            }
        }

        public long AttemptGeneration
        {
            get { return attemptGeneration; }
        }

        public bool IsConfigured
        {
            get
            {
                EnsureConfigured();
                return _isConfigured;
            }
        }

        public string ConfigurationError
        {
            get
            {
                EnsureConfigured();
                return _configurationError ?? string.Empty;
            }
        }

        public int RegisteredParticipantCount
        {
            get { return _participantRegistry.Count; }
        }

        public int RegisteredRestartParticipantCount
        {
            get { return _restartRegistry.Count; }
        }

        private void Awake()
        {
            EnsureConfigured();
        }

        public bool IsCompatible(StableId requiredCompatibilityId)
        {
            if (requiredCompatibilityId == null || !EnsureConfigured())
            {
                return false;
            }

            return _parsedCompatibilityId.Equals(requiredCompatibilityId);
        }

        public SceneScopeRegistrationResult Register(
            SceneScopeRegistrationRequest request)
        {
            if (!EnsureConfigured())
            {
                return SceneScopeRegistrationResult.Invalid(
                    "Scene scope configuration is invalid: " + ConfigurationError);
            }

            return _participantRegistry.Register(request);
        }

        public bool Unregister(StableId placedInstanceId, object ownerToken)
        {
            return _participantRegistry.Unregister(placedInstanceId, ownerToken);
        }

        public RestartParticipantRegistrationResult RegisterRestartParticipant(
            RestartParticipantRegistrationRequest request)
        {
            if (!EnsureConfigured())
            {
                return RestartParticipantRegistrationResult.Invalid(
                    "Scene scope configuration is invalid: " + ConfigurationError);
            }

            return _restartRegistry.Register(request);
        }

        public bool UnregisterRestartParticipant(
            StableId participantId,
            object ownerToken)
        {
            return _restartRegistry.Unregister(participantId, ownerToken);
        }

        public IReadOnlyList<PlacedParticipantRegistration>
            ReadOrderedParticipantSnapshot()
        {
            return _participantRegistry.ReadOrderedSnapshot();
        }

        public IReadOnlyList<IRestartParticipant>
            ReadOrderedRestartParticipantSnapshot()
        {
            return _restartRegistry.ReadOrderedSnapshot();
        }

        /// <summary>
        /// Runs only the generic typed lifecycle sequence. Each participant owns
        /// its package-specific reset behavior and external authorities remain untouched.
        /// </summary>
        public void RunRestart(long replacementAttemptGeneration)
        {
            RequireConfigured();
            RestartContext context = new RestartContext(
                RunId,
                RuntimeProjectionId,
                attemptGeneration,
                replacementAttemptGeneration);
            IReadOnlyList<IRestartParticipant> participants =
                _restartRegistry.ReadOrderedSnapshot();

            InvokePhase(participants, context, RestartLifecyclePhase.RetireAttempt);
            InvokePhase(
                participants,
                context,
                RestartLifecyclePhase.ReleaseTransientResources);
            InvokePhase(
                participants,
                context,
                RestartLifecyclePhase.ApplyResetProjection);

            attemptGeneration = replacementAttemptGeneration;

            InvokePhase(
                participants,
                context,
                RestartLifecyclePhase.CompleteRebind);
        }

        public void ConfigureForTests(
            string scopeId,
            string compatibilityId,
            string runtimeProjectionId,
            string runId,
            long attemptGeneration)
        {
            this.scopeId = scopeId;
            this.compatibilityId = compatibilityId;
            this.runtimeProjectionId = runtimeProjectionId;
            this.runId = runId;
            this.attemptGeneration = attemptGeneration;
            ResetConfigurationCache();
            RequireConfigured();
        }

        private static void InvokePhase(
            IReadOnlyList<IRestartParticipant> participants,
            RestartContext context,
            RestartLifecyclePhase phase)
        {
            for (int index = 0; index < participants.Count; index++)
            {
                participants[index].OnRestartPhase(context, phase);
            }
        }

        private bool EnsureConfigured()
        {
            if (_configurationAttempted)
            {
                return _isConfigured;
            }

            _configurationAttempted = true;
            _isConfigured = false;
            _configurationError = null;

            if (attemptGeneration < 0)
            {
                _configurationError = "Attempt generation cannot be negative.";
                return false;
            }

            if (!StableId.TryParse(scopeId, out _parsedScopeId))
            {
                _configurationError = "Scope ID is not a canonical StableId.";
                return false;
            }

            if (!StableId.TryParse(compatibilityId, out _parsedCompatibilityId))
            {
                _configurationError =
                    "Scope compatibility ID is not a canonical StableId.";
                return false;
            }

            if (!StableId.TryParse(
                runtimeProjectionId,
                out _parsedRuntimeProjectionId))
            {
                _configurationError =
                    "Runtime projection ID is not a canonical StableId.";
                return false;
            }

            if (!StableId.TryParse(runId, out _parsedRunId))
            {
                _configurationError = "Run ID is not a canonical StableId.";
                return false;
            }

            _isConfigured = true;
            return true;
        }

        private void RequireConfigured()
        {
            if (!EnsureConfigured())
            {
                throw new InvalidOperationException(
                    "Gameplay scene scope configuration is invalid: "
                    + ConfigurationError);
            }
        }

        private void ResetConfigurationCache()
        {
            _configurationAttempted = false;
            _isConfigured = false;
            _configurationError = null;
            _parsedScopeId = null;
            _parsedCompatibilityId = null;
            _parsedRuntimeProjectionId = null;
            _parsedRunId = null;
        }

        private void OnValidate()
        {
            ResetConfigurationCache();
        }
    }
}
