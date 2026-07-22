using System;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.UI.InventoryLoadout;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Hub adapter over the selected account-backed character graph. It does not cache or
    /// reconstruct starter authorities. Inventory confirmation explicitly asks the account
    /// composition to export the real authorities through SAVE-ADAPTERS-001 and atomically
    /// persist the selected exact character slot.
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

        /// <summary>
        /// Resolves the current selected-character graph synchronously. Scene consumers
        /// use this before their first Update; no fallback authority is constructed.
        /// </summary>
        public static bool TryResolveCurrent(
            out ProductionPlayerLoadoutRuntimeV1 currentRuntime,
            out ProductionFlowProfileRecordV1 profile)
        {
            EnsureInstalled();
            if (instance == null || !instance.SynchronizeNow())
            {
                currentRuntime = null;
                profile = null;
                return false;
            }

            return TryGetCurrent(out currentRuntime, out profile);
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
            if (instance == null)
            {
                return;
            }

            instance.CaptureConfirmedResult();
            instance.DetachBoundController();
            instance.boundPayloadFingerprint = string.Empty;
            instance.SynchronizeNow();
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
            if (SynchronizeNow())
            {
                BindInventoryScene();
            }
        }

        private bool SynchronizeNow()
        {
            if (coordinator == null)
            {
                coordinator = GetComponent<ProductionFlowCoordinatorV1>();
                if (coordinator == null)
                {
                    Clear();
                    return false;
                }
            }

            CaptureConfirmedResult();

            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 profile;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                out graph,
                out profile))
            {
                Clear();
                return false;
            }

            bool graphChanged = !ReferenceEquals(
                runtime,
                graph.LoadoutRuntime);
            runtime = graph.LoadoutRuntime;
            currentProfile = profile;
            if (graphChanged)
            {
                DetachBoundController();
                boundPayloadFingerprint = string.Empty;
            }
            return runtime != null && currentProfile != null;
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

            CharacterCompositionResultV1 saved =
                ProductionCharacterAccountCompositionV1.PersistCurrent(
                    "inventory-loadout-confirmed",
                    confirmedPayload.Fingerprint);
            if (saved == null || !saved.Succeeded)
            {
                Debug.LogError(
                    "Confirmed inventory loadout could not be persisted: "
                        + (saved == null
                            ? "character-composition-unavailable"
                            : saved.Diagnostic),
                    this);
                return;
            }

            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 profile;
            if (ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                out graph,
                out profile))
            {
                runtime = graph.LoadoutRuntime;
                currentProfile = profile;
            }
        }

        private void CaptureConfirmedResult()
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

            PlayerRouteProfilePayloadV1 payload = currentProfile.Payload;
            if (ReferenceEquals(boundController, controller)
                && string.Equals(
                    boundPayloadFingerprint,
                    payload.Fingerprint,
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
            controller.ConfigureWeaponPresentation(
                runtime.EquipmentCatalog,
                runtime.WeaponCatalog);
            controller.Present(HubRouteV1.Inventory, payload);
            controller.Confirmed -= HandleConfirmed;
            controller.Confirmed += HandleConfirmed;
            boundController = controller;
            boundPayloadFingerprint = payload.Fingerprint;
        }

        private void Clear()
        {
            runtime = null;
            currentProfile = null;
            DetachBoundController();
            boundPayloadFingerprint = string.Empty;
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
            CaptureConfirmedResult();
            DetachBoundController();
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
