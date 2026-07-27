using System;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Binds the playable character presentation to the exact canonical instance selected in the
    /// first active equipped class mount. The binding resolves definition and assignments from the
    /// character graph and never creates a scene-local fallback weapon.
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

            CharacterInstanceId = marker.CharacterInstanceStableId;
            ExactWeaponInstanceId = exact.InstanceId;
            WeaponDefinitionId = exact.WeaponDefinitionId.Value;
            ExactInstance = exact;
            ResolvedMark = mark;
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
