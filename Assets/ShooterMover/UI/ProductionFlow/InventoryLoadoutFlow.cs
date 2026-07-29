using System;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.UI.InventoryLoadout;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Hub adapter over the selected account-backed character graph. Inventory binds the exact
    /// canonical weapon-holdings and physical mount authorities; opening the screen never creates
    /// or repairs weapons.
    /// </summary>
    [DefaultExecutionOrder(-31900)]
    [DisallowMultipleComponent]
    public sealed class InventoryLoadoutFlow : MonoBehaviour
    {
        private static InventoryLoadoutFlow instance;

        private GameFlow coordinator;
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
            GameFlow flow =
                UnityEngine.Object.FindFirstObjectByType<
                    GameFlow>(
                    FindObjectsInactive.Include);
            if (flow == null)
            {
                return;
            }

            InventoryLoadoutFlow existing =
                flow.GetComponent<InventoryLoadoutFlow>();
            if (existing == null)
            {
                existing = flow.gameObject
                    .AddComponent<InventoryLoadoutFlow>();
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
            coordinator = GetComponent<GameFlow>();
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
                coordinator = GetComponent<GameFlow>();
                if (coordinator == null)
                {
                    Clear();
                    return false;
                }
            }

            CaptureConfirmedResult();

            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 profile;
            if (!CharacterAccount.TryResolveCurrent(
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
                CharacterAccount.PersistCurrent(
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
            if (CharacterAccount.TryResolveCurrent(
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
                    != ShooterMover.Application.Inventory.LoadoutScreen
                        .InventoryLoadoutScreenStatusV1.Confirmed
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

            PlayerRouteProfilePayloadV1 payload = runtime.CurrentRoutePayload;
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
            ProductionWeaponMountLoadoutRegistryV2.Register(
                runtime.WeaponHoldings,
                runtime.MountLoadoutAuthority);
            controller.ConnectCanonicalAuthorities(
                runtime.Holdings,
                runtime.CatalogAdapter,
                runtime.WeaponHoldings,
                runtime.LoadoutAuthority,
                runtime.MountLayout,
                runtime.WeaponCatalog);
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
