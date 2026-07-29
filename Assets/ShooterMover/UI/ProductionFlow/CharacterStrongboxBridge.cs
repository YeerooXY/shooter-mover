using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Persistence;
using UnityEngine;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Unity composition adapter for the application-level Results/BOX bridge. It resolves
    /// the selected character graph and delegates persistence to the account composition.
    /// </summary>
    [DefaultExecutionOrder(-31915)]
    [DisallowMultipleComponent]
    public sealed class CharacterStrongboxBridge :
        MonoBehaviour,
        ICharacterStrongboxBridge
    {
        private static CharacterStrongboxBridge instance;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CharacterStrongboxBridgeRegistry.Clear(instance);
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
            GameFlow flow =
                UnityEngine.Object.FindFirstObjectByType<
                    GameFlow>(
                    FindObjectsInactive.Include);
            if (flow == null)
            {
                return;
            }

            CharacterStrongboxBridge bridge =
                flow.GetComponent<CharacterStrongboxBridge>();
            if (bridge == null)
            {
                bridge = flow.gameObject.AddComponent<
                    CharacterStrongboxBridge>();
            }
            instance = bridge;
            CharacterStrongboxBridgeRegistry.Configure(bridge);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            CharacterStrongboxBridgeRegistry.Configure(this);
        }

        public bool TryResolve(
            out StrongboxOpeningActions authority,
            out string rejectionCode)
        {
            CharacterLiveGraph graph;
            FlowProfileRecord profile;
            if (!CharacterAccount.TryResolveCurrent(
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

            CharacterSetupResult result =
                CharacterAccount.PersistCurrent(
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

        public bool TryResolveDurableOpeningExecutor(
            out IStrongboxDurableOpeningExecutor executor,
            out string rejectionCode)
        {
            CharacterLiveGraph graph;
            FlowProfileRecord profile;
            CharacterSetupFlow composition;
            if (!CharacterAccount.TryResolveCurrent(
                    out graph,
                    out profile,
                    out composition)
                || graph == null
                || graph.IsDisposed
                || composition == null)
            {
                executor = null;
                rejectionCode = "selected-character-durable-opening-unavailable";
                return false;
            }

            executor = new StrongboxDurableOpeningFlow(composition);
            rejectionCode = string.Empty;
            return true;
        }

        private void OnDestroy()
        {
            CharacterStrongboxBridgeRegistry.Clear(this);
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
