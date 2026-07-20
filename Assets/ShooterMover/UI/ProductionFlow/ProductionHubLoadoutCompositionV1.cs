using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
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

        private readonly Dictionary<StableId, ProductionPlayerLoadoutRuntimeV1>
            runtimeByCharacter =
                new Dictionary<StableId, ProductionPlayerLoadoutRuntimeV1>();
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

        /// <summary>
        /// Resolves the current profile-local runtime synchronously. Scene consumers use
        /// this path when they must compose authority state before their first Update.
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
            if (!SynchronizeNow())
            {
                return;
            }

            BindInventoryScene();
        }

        private bool SynchronizeNow()
        {
            if (coordinator == null)
            {
                coordinator = GetComponent<ProductionFlowCoordinatorV1>();
                if (coordinator == null)
                {
                    return false;
                }
            }

            CapturePendingConfirmation();
            SynchronizeProfile();
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
                CacheCurrentRuntime();
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
                CacheCurrentRuntime();
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
            CacheCurrentRuntime();
            currentProfile = coordinatorProfile
                ?? throw new ArgumentNullException(nameof(coordinatorProfile));
            PlayerRouteProfilePayloadV1 normalized =
                ProductionWeaponMountPolicyV1.NormalizeRoutePayload(
                    currentProfile.Payload);
            StableId characterStableId =
                normalized.SelectedCharacterStableId;

            ProductionPlayerLoadoutRuntimeV1 cached;
            if (runtimeByCharacter.TryGetValue(
                    characterStableId,
                    out cached)
                && RuntimeMatchesPayload(cached, normalized))
            {
                runtime = cached;
            }
            else
            {
                ValidateStarterReconstruction(normalized);
                runtime = new ProductionPlayerLoadoutRuntimeV1(normalized);
                runtimeByCharacter[characterStableId] = runtime;
            }

            pendingConfirmedRuntime = null;
            pendingConfirmedPayloadFingerprint = string.Empty;
            DetachBoundController();
            boundPayloadFingerprint = string.Empty;
        }

        private void CacheCurrentRuntime()
        {
            if (runtime == null
                || currentProfile == null
                || currentProfile.Payload == null
                || currentProfile.Payload.SelectedCharacterStableId == null)
            {
                return;
            }

            runtimeByCharacter[
                currentProfile.Payload.SelectedCharacterStableId] = runtime;
        }

        private static bool RuntimeMatchesPayload(
            ProductionPlayerLoadoutRuntimeV1 candidate,
            PlayerRouteProfilePayloadV1 payload)
        {
            if (candidate == null
                || payload == null
                || candidate.LoadoutAuthority == null)
            {
                return false;
            }

            ProductionWeaponMountLayoutV1 expectedLayout =
                ProductionWeaponMountPolicyV1.ResolveLayout(
                    payload.LoadoutProfileStableId);
            if (candidate.MountLayout == null
                || candidate.MountLayout.LoadoutProfileStableId
                    != expectedLayout.LoadoutProfileStableId)
            {
                return false;
            }

            InventoryLoadoutAuthoritySnapshotV1 snapshot =
                candidate.LoadoutAuthority.ExportSnapshot();
            if (snapshot == null || !snapshot.HasValidFingerprint())
            {
                return false;
            }

            for (int index = 0;
                index < PlayerRouteProfilePayloadV1.WeaponSlotCount;
                index++)
            {
                PlayerRouteWeaponSlotV1 routeSlot =
                    payload.WeaponSlots[index];
                InventoryLoadoutSlotBindingV1 binding =
                    snapshot.GetBinding(routeSlot.WeaponSlotStableId);
                if (binding.EquipmentInstanceStableId
                    != routeSlot.EquipmentInstanceStableId)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ValidateStarterReconstruction(
            PlayerRouteProfilePayloadV1 payload)
        {
            for (int index = 0;
                index < payload.WeaponSlots.Count;
                index++)
            {
                StableId instanceStableId = payload.WeaponSlots[index]
                    .EquipmentInstanceStableId;
                if (instanceStableId == null)
                {
                    continue;
                }

                StableId ignoredDefinitionStableId;
                if (!ProductionStarterWeaponCatalogV1
                    .TryResolveDefinitionForInstance(
                        instanceStableId,
                        out ignoredDefinitionStableId))
                {
                    throw new InvalidOperationException(
                        "Cannot reconstruct exact equipment instance "
                        + instanceStableId
                        + " from route position "
                        + payload.WeaponSlots[index].WeaponSlotStableId
                        + ". Slot-based weapon substitution is forbidden; "
                        + "an authoritative holdings snapshot is required.");
                }
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

            PlayerRouteProfilePayloadV1 normalizedPayload =
                ProductionWeaponMountPolicyV1.NormalizeRoutePayload(
                    currentProfile.Payload);
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
            CacheCurrentRuntime();
            DetachBoundController();
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
