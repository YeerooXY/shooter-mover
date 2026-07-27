using System;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Authoritative exact-weapon source attached to the spawned player object. Gameplay consumers
    /// resolve through this source; it owns no fallback and cannot be rebound to a different exact
    /// instance during the player lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CanonicalPlayerWeaponSourceV2 : MonoBehaviour
    {
        private CanonicalWeaponEquipmentProjectionLookupV2 projectionLookup;

        public StableId CharacterInstanceId { get; private set; }
        public StableId ExactWeaponInstanceId { get; private set; }
        public string WeaponDefinitionId { get; private set; }
        public WeaponEquipmentInstance ExactInstance { get; private set; }
        public ProductionWeaponMarkV1 ResolvedMark { get; private set; }
        public string Diagnostic { get; private set; }
        public bool IsBound { get { return ExactInstance != null; } }

        public void Bind(
            PlayablePlayerMarker2D marker,
            ProductionPlayerLoadoutRuntimeV1 runtime,
            WeaponEquipmentInstance exact,
            ProductionWeaponMarkV1 mark)
        {
            if (IsBound)
            {
                if (marker != null
                    && CharacterInstanceId == marker.CharacterInstanceStableId
                    && exact != null
                    && ExactWeaponInstanceId == exact.InstanceId)
                {
                    return;
                }
                throw new InvalidOperationException(
                    "gameplay-canonical-player-source-duplicate-binding");
            }
            if (marker == null)
            {
                throw new ArgumentNullException(nameof(marker));
            }
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }
            if (exact == null)
            {
                throw new ArgumentNullException(nameof(exact));
            }
            if (mark == null)
            {
                throw new ArgumentNullException(nameof(mark));
            }
            if (marker.CharacterInstanceStableId == null
                || marker.CharacterInstanceStableId
                    != runtime.RoutePayload.SelectedCharacterStableId
                || runtime.WeaponHoldings.Find(exact.InstanceId) != exact
                || !string.Equals(
                    mark.Blueprint.DefinitionId.Value,
                    exact.WeaponDefinitionId.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "gameplay-canonical-player-source-context-mismatch");
            }

            CharacterInstanceId = marker.CharacterInstanceStableId;
            ExactWeaponInstanceId = exact.InstanceId;
            WeaponDefinitionId = exact.WeaponDefinitionId.Value;
            ExactInstance = exact;
            ResolvedMark = mark;
            projectionLookup = new CanonicalWeaponEquipmentProjectionLookupV2(
                runtime.WeaponHoldings,
                runtime.EquipmentCatalog,
                runtime.Holdings);
            Diagnostic = string.Empty;
        }

        public bool TryResolveLiveEquipment(
            out EquipmentInstance equipmentInstance,
            out string rejectionCode)
        {
            equipmentInstance = null;
            rejectionCode = string.Empty;
            if (!IsBound || projectionLookup == null)
            {
                rejectionCode = "gameplay-canonical-player-source-unbound";
                return false;
            }
            if (!projectionLookup.TryResolve(
                    new EquipmentInstanceId(ExactWeaponInstanceId),
                    out equipmentInstance)
                || equipmentInstance == null
                || equipmentInstance.InstanceId != ExactWeaponInstanceId)
            {
                equipmentInstance = null;
                rejectionCode =
                    "gameplay-canonical-live-equipment-unresolved:"
                    + ExactWeaponInstanceId;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Installs the player-local authoritative source from the first active canonical mount. The
    /// installer verifies route, character, definition and live compatibility projection before
    /// declaring the gameplay handoff complete.
    /// </summary>
    [DefaultExecutionOrder(650)]
    [DisallowMultipleComponent]
    public sealed class ProductionCanonicalWeaponGameplayBindingV2 : MonoBehaviour
    {
        private static ProductionCanonicalWeaponGameplayBindingV2 instance;
        private bool bound;

        public StableId CharacterInstanceId { get; private set; }
        public StableId ExactWeaponInstanceId { get; private set; }
        public string WeaponDefinitionId { get; private set; }
        public WeaponEquipmentInstance ExactInstance { get; private set; }
        public ProductionWeaponMarkV1 ResolvedMark { get; private set; }
        public CanonicalPlayerWeaponSourceV2 PlayerSource { get; private set; }
        public string Diagnostic { get; private set; }

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
            TryInstall(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryInstall(scene);
        }

        private static void TryInstall(Scene scene)
        {
            if (!scene.IsValid()
                || !string.Equals(
                    scene.path,
                    ProductionPlayableLevelCatalogV1.PlayableLevelScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            ProductionPlayableLevelControllerV1 controller =
                UnityEngine.Object.FindFirstObjectByType<
                    ProductionPlayableLevelControllerV1>(
                    FindObjectsInactive.Include);
            if (controller == null)
            {
                return;
            }
            ProductionCanonicalWeaponGameplayBindingV2 existing =
                controller.GetComponent<
                    ProductionCanonicalWeaponGameplayBindingV2>();
            if (existing == null)
            {
                existing = controller.gameObject.AddComponent<
                    ProductionCanonicalWeaponGameplayBindingV2>();
            }
            instance = existing;
        }

        private void Update()
        {
            if (bound)
            {
                return;
            }

            PlayablePlayerMarker2D marker =
                UnityEngine.Object.FindFirstObjectByType<
                    PlayablePlayerMarker2D>(
                    FindObjectsInactive.Include);
            if (marker == null || marker.CharacterInstanceStableId == null)
            {
                return;
            }

            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 profile;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out graph,
                    out profile)
                || graph == null
                || graph.IsDisposed
                || graph.Character.CharacterInstanceStableId
                    != marker.CharacterInstanceStableId)
            {
                Diagnostic = "gameplay-canonical-character-context-missing";
                return;
            }

            WeaponEquipmentInstance exact;
            string rejectionCode;
            if (!graph.LoadoutRuntime.TryResolveFirstActiveEquippedWeapon(
                    out exact,
                    out rejectionCode)
                || exact == null)
            {
                Diagnostic = string.IsNullOrWhiteSpace(rejectionCode)
                    ? "gameplay-canonical-first-active-weapon-unresolved"
                    : rejectionCode;
                return;
            }

            ProductionWeaponMarkV1 mark;
            if (!ProductionWeaponCatalogProvider.Current.TryGetMark(
                    exact.WeaponDefinitionId.Value,
                    out mark)
                || mark == null)
            {
                Diagnostic =
                    "gameplay-canonical-definition-unresolved:"
                    + exact.WeaponDefinitionId.Value;
                return;
            }

            if (marker.RoutePayload == null
                || !marker.RoutePayload.HasValidFingerprint()
                || !RouteContainsExact(
                    marker.RoutePayload,
                    exact.InstanceId))
            {
                Diagnostic =
                    "gameplay-canonical-route-instance-mismatch:"
                    + exact.InstanceId;
                return;
            }

            CanonicalPlayerWeaponSourceV2 source =
                marker.GetComponent<CanonicalPlayerWeaponSourceV2>()
                ?? marker.gameObject.AddComponent<
                    CanonicalPlayerWeaponSourceV2>();
            try
            {
                source.Bind(marker, graph.LoadoutRuntime, exact, mark);
                EquipmentInstance liveEquipment;
                if (!source.TryResolveLiveEquipment(
                        out liveEquipment,
                        out rejectionCode))
                {
                    Diagnostic = rejectionCode;
                    return;
                }
            }
            catch (Exception exception)
            {
                Diagnostic = string.IsNullOrWhiteSpace(exception.Message)
                    ? "gameplay-canonical-player-source-binding-failed"
                    : exception.Message;
                return;
            }

            CharacterInstanceId = marker.CharacterInstanceStableId;
            ExactWeaponInstanceId = exact.InstanceId;
            WeaponDefinitionId = exact.WeaponDefinitionId.Value;
            ExactInstance = exact;
            ResolvedMark = mark;
            PlayerSource = source;
            Diagnostic = string.Empty;
            bound = true;
        }

        private static bool RouteContainsExact(
            ShooterMover.Contracts.Flow.Session.PlayerRouteProfilePayloadV1 route,
            StableId instanceId)
        {
            for (int index = 0; index < route.WeaponSlots.Count; index++)
            {
                if (route.WeaponSlots[index].EquipmentInstanceStableId
                    == instanceId)
                {
                    return true;
                }
            }
            return false;
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
