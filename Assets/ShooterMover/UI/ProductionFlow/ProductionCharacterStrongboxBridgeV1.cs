using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Strongboxes;
using UnityEngine;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Unity composition adapter for the application-level Results/BOX bridge. It resolves
    /// the selected character graph and delegates persistence to the account composition.
    /// </summary>
    [DefaultExecutionOrder(-31915)]
    [DisallowMultipleComponent]
    public sealed class ProductionCharacterStrongboxBridgeV1 :
        MonoBehaviour,
        IProductionCharacterStrongboxBridgeV1
    {
        private static ProductionCharacterStrongboxBridgeV1 instance;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ProductionCharacterStrongboxBridgeRegistryV1.Clear(instance);
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            EnsureInstalled();
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

            ProductionCharacterStrongboxBridgeV1 bridge =
                flow.GetComponent<ProductionCharacterStrongboxBridgeV1>();
            if (bridge == null)
            {
                bridge = flow.gameObject.AddComponent<
                    ProductionCharacterStrongboxBridgeV1>();
            }
            instance = bridge;
            ProductionCharacterStrongboxBridgeRegistryV1.Configure(bridge);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            ProductionCharacterStrongboxBridgeRegistryV1.Configure(this);
        }

        public bool TryResolve(
            out StrongboxOpeningServiceV1 authority,
            out string rejectionCode)
        {
            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 profile;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                out graph,
                out profile)
                || graph == null
                || graph.IsDisposed
                || graph.StrongboxAuthority == null)
            {
                authority = null;
                rejectionCode = "selected-character-strongbox-unavailable";
                return false;
            }

            authority = graph.StrongboxAuthority;
            rejectionCode = string.Empty;
            return true;
        }

        public bool TryPersist(
            string strongboxSnapshotFingerprint,
            out string rejectionCode)
        {
            if (string.IsNullOrWhiteSpace(strongboxSnapshotFingerprint))
            {
                rejectionCode = "strongbox-snapshot-fingerprint-missing";
                return false;
            }

            CharacterCompositionResultV1 result =
                ProductionCharacterAccountCompositionV1.PersistCurrent(
                    "strongbox-opening-confirmed",
                    strongboxSnapshotFingerprint);
            if (result == null || !result.Succeeded)
            {
                rejectionCode = result == null
                    ? "strongbox-character-save-result-null"
                    : "strongbox-character-save-rejected:"
                        + result.Diagnostic;
                return false;
            }

            rejectionCode = string.Empty;
            return true;
        }

        private void OnDestroy()
        {
            ProductionCharacterStrongboxBridgeRegistryV1.Clear(this);
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
