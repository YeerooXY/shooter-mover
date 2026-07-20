using System;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Flow.Production;
using ShooterMover.UI.InventoryLoadout;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Persistent production composition for the Hub inventory/loadout screen. The
    /// production-flow coordinator remains the route/profile persistence owner; this
    /// component supplies the shared holdings and loadout authorities only.
    /// </summary>
    [DefaultExecutionOrder(-31900)]
    [DisallowMultipleComponent]
    public sealed class ProductionHubLoadoutCompositionV1 : MonoBehaviour
    {
        private static ProductionHubLoadoutCompositionV1 instance;

        private ProductionFlowCoordinatorV1 coordinator;
        private ProductionFlowProfileRecordV1 currentProfile;
        private ProductionPlayerLoadoutRuntimeV1 runtime;
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
                instance.boundController = null;
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

            SynchronizeProfile();
            BindInventoryScene();
        }

        private void SynchronizeProfile()
        {
            ProductionFlowProfileRecordV1 coordinatorProfile =
                coordinator.Profile;
            if (coordinatorProfile == null)
            {
                currentProfile = null;
                runtime = null;
                boundController = null;
                boundPayloadFingerprint = string.Empty;
                return;
            }

            if (runtime == null
                || currentProfile == null
                || !ReferenceEquals(currentProfile, coordinatorProfile))
            {
                currentProfile = coordinatorProfile;
                runtime = new ProductionPlayerLoadoutRuntimeV1(
                    currentProfile.Payload);
                boundController = null;
                boundPayloadFingerprint = string.Empty;
            }
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

            if (ReferenceEquals(boundController, controller)
                && string.Equals(
                    boundPayloadFingerprint,
                    currentProfile.Payload.Fingerprint,
                    StringComparison.Ordinal)
                && controller.IsConfigured)
            {
                return;
            }

            controller.ConnectAuthorities(
                runtime.Holdings,
                runtime.CatalogAdapter,
                runtime.LoadoutAuthority);
            controller.Present(
                HubRouteV1.Inventory,
                currentProfile.Payload);
            boundController = controller;
            boundPayloadFingerprint =
                currentProfile.Payload.Fingerprint;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
