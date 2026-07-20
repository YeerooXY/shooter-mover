using System;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.UI.InventoryLoadout;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Persistent production composition for one active profile. The flow coordinator
    /// remains persistence owner; this component supplies profile-local holdings,
    /// class-normalized mount bindings, and the loadout authority.
    /// </summary>
    [DefaultExecutionOrder(-31900)]
    [DisallowMultipleComponent]
    public sealed class ProductionHubLoadoutCompositionV1 : MonoBehaviour
    {
        private static ProductionHubLoadoutCompositionV1 instance;

        private ProductionFlowCoordinatorV1 coordinator;
        private ProductionFlowProfileRecordV1 currentProfile;
        private ProductionPlayerLoadoutRuntimeV1 runtime;
        private ProductionPlayerLoadoutRuntimeV1 pendingConfirmedRuntime;
        private string pendingConfirmedPayloadFingerprint = string.Empty;
        private InventoryLoadoutScreenControllerV1 boundController;
        private string boundPayloadFingerprint = string.Empty;

        public ProductionPlayerLoadoutRuntimeV1 Runtime
        {
            get { return runtime; }
        }

        public ProductionFlowProfileRecordV1 CurrentProfile
        {
            get { return currentProfile; }
        }

        public static bool TryGetCurrent(
            out ProductionPlayerLoadoutRuntimeV1 currentRuntime,
            out ProductionFlowProfileRecordV1 profile)
        {
            currentRuntime = instance == null ? null : instance.runtime;
            profile = instance == null ? null : instance.currentProfile;
            return currentRuntime != null && profile != null;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureInstalled();
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            EnsureInstalled();
            if (instance != null)
            {
                instance.CapturePendingConfirmation();
                instance.DetachBoundController();
                instance.boundPayloadFingerprint = string.Empty;
            }
        }

        private static void EnsureInstalled()
        {
            ProductionFlowCoordinatorV1 flow =
                UnityEngine.Object.FindFirstObjectByType<
                    ProductionFlowCoordinatorV1>(
                    FindObjectsInactive.Include);
            if (flow == null)
            {
                return;
            }

            ProductionHubLoadoutCompositionV1 existing =
                flow.GetComponent<ProductionHubLoadoutCompositionV1>();
            if (existing == null)
            {
                existing = flow.gameObject
                    .AddComponent<ProductionHubLoadoutCompositionV1>();
            }
            instance = existing;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            coordinator = GetComponent<ProductionFlowCoordinatorV1>();
        }

        private void Update()
        {
            if (coordinator == null)
            {
                coordinator = GetComponent<ProductionFlowCoordinatorV1>();
                if (coordinator == null)
                {
                    return;
                }
            }

            CapturePendingConfirmation();
            SynchronizeProfile();
            BindInventoryScene();
        }

        private void HandleConfirmed(
            PlayerRouteProfilePayloadV1 confirmedPayload)
        {
            if (confirmedPayload == null
                || !confirmedPayload.HasValidFingerprint()
                || runtime == null)
            {
                return;
            }

            pendingConfirmedRuntime = runtime;
            pendingConfirmedPayloadFingerprint =
                confirmedPayload.Fingerprint;
        }

        private void CapturePendingConfirmation()
        {
            if (boundController == null
                || boundController.LastResult == null
                || boundController.LastResult.Status
                    != InventoryLoadoutScreenStatusV1.Confirmed
                || boundController.LastResult.RoutePayload == null)
            {
                return;
            }
            HandleConfirmed(boundController.LastResult.RoutePayload);
        }

        private void SynchronizeProfile()
        {
            ProductionFlowProfileRecordV1 coordinatorProfile =
                coordinator.Profile;
            if (coordinatorProfile == null)
            {
                currentProfile = null;
                runtime = null;
                pendingConfirmedRuntime = null;
                pendingConfirmedPayloadFingerprint = string.Empty;
                DetachBoundController();
                boundPayloadFingerprint = string.Empty;
                return;
            }

            if (runtime == null || currentProfile == null)
            {
                ComposeFreshProfile(coordinatorProfile);
                return;
            }

            if (ReferenceEquals(currentProfile, coordinatorProfile))
            {
                return;
            }

            if (pendingConfirmedRuntime != null
                && string.Equals(
                    pendingConfirmedPayloadFingerprint,
                    coordinatorProfile.Payload.Fingerprint,
                    StringComparison.Ordinal))
            {
                currentProfile = coordinatorProfile;
                runtime = pendingConfirmedRuntime;
                pendingConfirmedRuntime = null;
                pendingConfirmedPayloadFingerprint = string.Empty;
                DetachBoundController();
                boundPayloadFingerprint = string.Empty;
                return;
            }

            ComposeFreshProfile(coordinatorProfile);
        }

        private void ComposeFreshProfile(
            ProductionFlowProfileRecordV1 coordinatorProfile)
        {
            currentProfile = coordinatorProfile;
            runtime = new ProductionPlayerLoadoutRuntimeV1(
                currentProfile.Payload);
            pendingConfirmedRuntime = null;
            pendingConfirmedPayloadFingerprint = string.Empty;
            DetachBoundController();
            boundPayloadFingerprint = string.Empty;
        }

        private void BindInventoryScene()
        {
            if (runtime == null
                || currentProfile == null
                || coordinator.Transitions == null
                || coordinator.Transitions.IsTransitionPending
                || !string.Equals(
                    SceneManager.GetActiveScene().path,
                    ProductionFlowScenePathsV1.Inventory,
                    StringComparison.Ordinal))
            {
                return;
            }

            InventoryLoadoutScreenControllerV1 controller =
                UnityEngine.Object.FindFirstObjectByType<
                    InventoryLoadoutScreenControllerV1>(
                    FindObjectsInactive.Include);
            if (controller == null)
            {
                return;
            }

            PlayerRouteProfilePayloadV1 normalizedPayload =
                runtime.RoutePayload;
            if (ReferenceEquals(boundController, controller)
                && string.Equals(
                    boundPayloadFingerprint,
                    normalizedPayload.Fingerprint,
                    StringComparison.Ordinal)
                && controller.IsConfigured)
            {
                return;
            }

            DetachBoundController();
            controller.ConnectAuthorities(
                runtime.Holdings,
                runtime.CatalogAdapter,
                runtime.LoadoutAuthority);
            controller.Present(
                HubRouteV1.Inventory,
                normalizedPayload);
            controller.Confirmed -= HandleConfirmed;
            controller.Confirmed += HandleConfirmed;
            boundController = controller;
            boundPayloadFingerprint = normalizedPayload.Fingerprint;
        }

        private void DetachBoundController()
        {
            if (boundController != null)
            {
                boundController.Confirmed -= HandleConfirmed;
            }
            boundController = null;
        }

        private void OnDestroy()
        {
            DetachBoundController();
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
