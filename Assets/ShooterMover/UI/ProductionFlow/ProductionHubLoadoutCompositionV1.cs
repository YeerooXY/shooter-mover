using System;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Weapons;
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

            InventoryEconomySafetyOverlayV1 safetyOverlay =
                controller.GetComponent<InventoryEconomySafetyOverlayV1>();
            if (safetyOverlay == null)
            {
                safetyOverlay = controller.gameObject
                    .AddComponent<InventoryEconomySafetyOverlayV1>();
            }
            safetyOverlay.Configure(controller);

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

    /// <summary>
    /// Read-only Inventory projection for unsupported canonical weapon operations. It consumes the
    /// shared structured policy and never writes wallet, receipt, holdings, assignment or mount state.
    /// </summary>
    [DefaultExecutionOrder(31900)]
    [DisallowMultipleComponent]
    public sealed class InventoryEconomySafetyOverlayV1 : MonoBehaviour
    {
        private InventoryLoadoutScreenControllerV1 controller;
        private GUIStyle warningStyle;
        private GUIStyle detailStyle;

        public void Configure(InventoryLoadoutScreenControllerV1 inventoryController)
        {
            controller = inventoryController
                ?? throw new ArgumentNullException(nameof(inventoryController));
        }

        private void OnGUI()
        {
            if (controller == null
                || controller.CanonicalSnapshot == null
                || controller.CanonicalSnapshot.SelectedWeapon == null
                || !string.Equals(
                    SceneManager.GetActiveScene().path,
                    ProductionFlowScenePathsV1.Inventory,
                    StringComparison.Ordinal))
            {
                return;
            }

            EnsureStyles();
            WeaponEquipmentInstance instance =
                controller.CanonicalSnapshot.SelectedWeapon.Instance;
            ProductionWeaponMarkV1 mark;
            bool definitionResolved = ProductionWeaponCatalogProvider.Current
                .TryGetMark(instance.WeaponDefinitionId.Value, out mark)
                && mark != null;
            CanonicalWeaponOperationAvailabilityV1 upgrade =
                CanonicalWeaponSafetyPolicyV1.EvaluateGenericUpgrade(
                    true,
                    definitionResolved);
            CanonicalWeaponOperationAvailabilityV1 live =
                CanonicalWeaponSafetyPolicyV1.EvaluateLiveExecution(
                    instance,
                    definitionResolved);
            CanonicalWeaponOperationAvailabilityV1 overclock =
                CanonicalWeaponSafetyPolicyV1.EvaluateOverclockInstallation();

            float width = Mathf.Min(470f, Mathf.Max(300f, Screen.width - 32f));
            float height = instance.OverclockAssignments.Count == 0 ? 150f : 185f;
            GUILayout.BeginArea(
                new Rect(
                    Screen.width - width - 16f,
                    Screen.height - height - 16f,
                    width,
                    height),
                GUI.skin.window);
            GUILayout.Label("WEAPON SAFETY GATE", warningStyle);
            GUI.enabled = false;
            GUILayout.Button(
                "AUGMENT UPGRADE — BLOCKED",
                GUILayout.MinHeight(30f));
            GUI.enabled = true;
            GUILayout.Label(
                upgrade.RejectionCode + " — " + upgrade.Message,
                detailStyle);

            if (instance.OverclockAssignments.Count == 0)
            {
                GUILayout.Label(
                    "OVERCLOCK INSTALLATION — NOT AVAILABLE\n"
                    + overclock.RejectionCode,
                    detailStyle);
            }
            else
            {
                GUILayout.Label(
                    "LIVE EXECUTION — BLOCKED\n"
                    + live.RejectionCode + " — " + live.Message,
                    warningStyle);
            }
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (warningStyle != null)
            {
                return;
            }
            warningStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontStyle = FontStyle.Bold,
            };
            detailStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontSize = 11,
            };
        }
    }
}
