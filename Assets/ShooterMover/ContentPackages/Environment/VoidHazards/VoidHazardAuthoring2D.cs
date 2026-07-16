using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring;
using UnityEngine;

namespace ShooterMover.ContentPackages.Environment.VoidHazards
{
    /// <summary>
    /// Reusable scene-independent void region. It owns typed routing and transient
    /// contact bookkeeping only; all gameplay outcomes remain authority-owned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class VoidHazardAuthoring2D : MonoBehaviour, IRestartParticipant
    {
        [Header("Placed Object")]
        [SerializeField] private PlacedObjectAuthoring2D placedObject;
        [SerializeField] private Collider2D hazardCollider;
        [SerializeField] private bool bindOnEnable = true;
        [SerializeField] private bool activeAtAttemptStart = true;

        [Header("Player Policy")]
        [SerializeField] private VoidPlayerResponseKind playerResponse =
            VoidPlayerResponseKind.Ignore;
        [SerializeField] private double playerDamageAmount = 25d;
        [SerializeField] private string playerCheckpointId = "checkpoint.unassigned";

        [Header("Enemy Policy")]
        [SerializeField] private VoidEnemyResponseKind enemyResponse =
            VoidEnemyResponseKind.Ignore;

        [Header("Projectile Policy")]
        [SerializeField] private VoidProjectileResponseKind projectileResponse =
            VoidProjectileResponseKind.RemoveProjectile;

        [Header("Prop Policy")]
        [SerializeField] private VoidPropResponseKind propResponse =
            VoidPropResponseKind.KeepSupported;

        [Header("Typed Scene Ports")]
        [SerializeField] private MonoBehaviour checkpointPort;
        [SerializeField] private MonoBehaviour presentationPort;

        [Header("Editor Visibility")]
        [SerializeField] private Color editorRegionColor =
            new Color(0.75f, 0.12f, 0.12f, 0.75f);

        private readonly Dictionary<StableId, int> _activeContacts =
            new Dictionary<StableId, int>();

        private VoidHazardPolicy _resolvedPolicy;
        private StableId _restartParticipantId;
        private long _contactOrdinal;
        private bool _isReady;
        private bool _acceptContacts;

        public event Action<VoidHazardPresentationEvent> PresentationRequested;

        public StableId RestartParticipantId
        {
            get { return _restartParticipantId; }
        }

        public bool IsReady
        {
            get { return _isReady; }
        }

        public bool AcceptsContacts
        {
            get { return _isReady && _acceptContacts; }
        }

        public int ActiveTargetCount
        {
            get { return _activeContacts.Count; }
        }

        public VoidHazardValidationResult LastValidationResult { get; private set; }

        public SceneScopeBindingResult LastBindingResult { get; private set; }

        public RestartParticipantRegistrationResult LastRestartRegistrationResult
        {
            get;
            private set;
        }

        private void OnEnable()
        {
            CacheLocalReferences();
            if (bindOnEnable)
            {
                TryActivate();
            }
        }

        private void OnDisable()
        {
            Deactivate();
        }

        private void OnDestroy()
        {
            Deactivate();
        }

        public bool TryActivate()
        {
            if (_isReady)
            {
                return true;
            }

            CacheLocalReferences();
            LastValidationResult = ValidateConfiguration();
            if (!LastValidationResult.IsValid)
            {
                FailClosed();
                return false;
            }

            LastBindingResult = placedObject.TryBind();
            if (LastBindingResult == null || !LastBindingResult.IsBound)
            {
                FailClosed();
                return false;
            }

            _restartParticipantId = placedObject.ResolvedIdentity.Value;
            LastRestartRegistrationResult = placedObject.RegisterRestartParticipant(
                this,
                this,
                BuildDiagnosticLocation());
            if (LastRestartRegistrationResult == null
                || !LastRestartRegistrationResult.IsAccepted)
            {
                FailClosed();
                return false;
            }

            _isReady = true;
            _activeContacts.Clear();
            _contactOrdinal = 0L;
            hazardCollider.enabled = activeAtAttemptStart;
            _acceptContacts = activeAtAttemptStart;
            return true;
        }

        public void Deactivate()
        {
            if (placedObject != null && _restartParticipantId != null)
            {
                placedObject.UnregisterRestartParticipant(_restartParticipantId, this);
            }

            _activeContacts.Clear();
            _resolvedPolicy = null;
            _restartParticipantId = null;
            _contactOrdinal = 0L;
            _acceptContacts = false;
            _isReady = false;
        }

        public VoidHazardValidationResult ValidateConfiguration()
        {
            if (placedObject == null)
            {
                return VoidHazardValidationResult.Failed(
                    VoidHazardValidationStatus.MissingPlacedObject,
                    "A PlacedObjectAuthoring2D reference is required.");
            }

            if (!ReferenceEquals(placedObject.gameObject, gameObject))
            {
                return VoidHazardValidationResult.Failed(
                    VoidHazardValidationStatus.PlacedObjectMustShareGameObject,
                    "The placed-object and hazard authoring components must share one GameObject.");
            }

            if (hazardCollider == null)
            {
                return VoidHazardValidationResult.Failed(
                    VoidHazardValidationStatus.MissingHazardCollider,
                    "A Collider2D reference is required.");
            }

            if (!ReferenceEquals(hazardCollider.gameObject, gameObject))
            {
                return VoidHazardValidationResult.Failed(
                    VoidHazardValidationStatus.ColliderMustShareGameObject,
                    "The hazard Collider2D and authoring component must share one GameObject.");
            }

            if (!hazardCollider.isTrigger)
            {
                return VoidHazardValidationResult.Failed(
                    VoidHazardValidationStatus.ColliderMustBeTrigger,
                    "The hazard Collider2D must be configured as a trigger.");
            }

            if (!Enum.IsDefined(typeof(VoidPlayerResponseKind), playerResponse)
                || !Enum.IsDefined(typeof(VoidEnemyResponseKind), enemyResponse)
                || !Enum.IsDefined(typeof(VoidProjectileResponseKind), projectileResponse)
                || !Enum.IsDefined(typeof(VoidPropResponseKind), propResponse))
            {
                return VoidHazardValidationResult.Failed(
                    VoidHazardValidationStatus.InvalidPolicy,
                    "Every category requires one declared response.");
            }

            if (playerResponse == VoidPlayerResponseKind.Damage
                && (double.IsNaN(playerDamageAmount)
                    || double.IsInfinity(playerDamageAmount)
                    || playerDamageAmount <= 0d))
            {
                return VoidHazardValidationResult.Failed(
                    VoidHazardValidationStatus.InvalidDamageAmount,
                    "Player damage response requires a finite positive amount.");
            }

            StableId checkpointId = null;
            if (playerResponse == VoidPlayerResponseKind.Respawn)
            {
                if (!(checkpointPort is IVoidHazardCheckpointPort))
                {
                    return VoidHazardValidationResult.Failed(
                        VoidHazardValidationStatus.MissingCheckpointPort,
                        "Respawn response requires an explicit checkpoint port.");
                }

                if (!StableId.TryParse(playerCheckpointId, out checkpointId))
                {
                    return VoidHazardValidationResult.Failed(
                        VoidHazardValidationStatus.InvalidCheckpointId,
                        "Respawn response requires a canonical checkpoint StableId.");
                }
            }

            if (presentationPort != null
                && !(presentationPort is IVoidHazardPresentationPort))
            {
                return VoidHazardValidationResult.Failed(
                    VoidHazardValidationStatus.InvalidPresentationPort,
                    "Presentation reference must implement IVoidHazardPresentationPort.");
            }

            try
            {
                _resolvedPolicy = new VoidHazardPolicy(
                    playerResponse,
                    playerDamageAmount,
                    checkpointId,
                    enemyResponse,
                    projectileResponse,
                    propResponse);
            }
            catch (Exception exception)
            {
                return VoidHazardValidationResult.Failed(
                    VoidHazardValidationStatus.InvalidPolicy,
                    exception.Message);
            }

            return VoidHazardValidationResult.Valid();
        }

        public void OnRestartPhase(
            RestartContext context,
            RestartLifecyclePhase phase)
        {
            if (!_isReady || context == null)
            {
                return;
            }

            switch (phase)
            {
                case RestartLifecyclePhase.RetireAttempt:
                    _acceptContacts = false;
                    if (hazardCollider != null)
                    {
                        hazardCollider.enabled = false;
                    }
                    break;
                case RestartLifecyclePhase.ReleaseTransientResources:
                    _activeContacts.Clear();
                    _contactOrdinal = 0L;
                    break;
                case RestartLifecyclePhase.ApplyResetProjection:
                    if (hazardCollider != null)
                    {
                        hazardCollider.enabled = activeAtAttemptStart;
                    }
                    break;
                case RestartLifecyclePhase.CompleteRebind:
                    _acceptContacts = activeAtAttemptStart;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase));
            }
        }

        public void ConfigureForTests(
            PlacedObjectAuthoring2D placedObject,
            Collider2D hazardCollider,
            bool activeAtAttemptStart,
            VoidPlayerResponseKind playerResponse,
            double playerDamageAmount,
            string playerCheckpointId,
            VoidEnemyResponseKind enemyResponse,
            VoidProjectileResponseKind projectileResponse,
            VoidPropResponseKind propResponse,
            MonoBehaviour checkpointPort,
            MonoBehaviour presentationPort)
        {
            Deactivate();
            this.placedObject = placedObject;
            this.hazardCollider = hazardCollider;
            this.activeAtAttemptStart = activeAtAttemptStart;
            this.playerResponse = playerResponse;
            this.playerDamageAmount = playerDamageAmount;
            this.playerCheckpointId = playerCheckpointId;
            this.enemyResponse = enemyResponse;
            this.projectileResponse = projectileResponse;
            this.propResponse = propResponse;
            this.checkpointPort = checkpointPort;
            this.presentationPort = presentationPort;
            bindOnEnable = false;
        }

        private void CacheLocalReferences()
        {
            if (placedObject == null)
            {
                placedObject = GetComponent<PlacedObjectAuthoring2D>();
            }

            if (hazardCollider == null)
            {
                hazardCollider = GetComponent<Collider2D>();
            }
        }

        private void FailClosed()
        {
            _activeContacts.Clear();
            _resolvedPolicy = null;
            _restartParticipantId = null;
            _contactOrdinal = 0L;
            _acceptContacts = false;
            _isReady = false;
            if (hazardCollider != null)
            {
                hazardCollider.enabled = false;
            }
        }
    }
}
