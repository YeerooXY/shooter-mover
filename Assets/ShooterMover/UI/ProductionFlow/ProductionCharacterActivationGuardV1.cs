using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Composition;
using UnityEngine;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Transactional lifecycle decorator for explicit character activation. The current
    /// character is durably persisted before the wrapped lifecycle may dispose it or
    /// construct another graph. A failed save rejects the switch and leaves the current
    /// graph/profile published.
    /// </summary>
    public sealed class PersistBeforeCharacterActivationLifecycleV1 :
        IProductionCharacterProfileLifecycleV1
    {
        private readonly IProductionCharacterProfileLifecycleV1 inner;
        private readonly Func<int, bool> requiresPersistence;
        private readonly Func<int, ProductionFlowProfileRecordV1,
            CharacterCompositionResultV1> persistCurrent;

        public PersistBeforeCharacterActivationLifecycleV1(
            IProductionCharacterProfileLifecycleV1 inner,
            Func<int, bool> requiresPersistence,
            Func<int, ProductionFlowProfileRecordV1,
                CharacterCompositionResultV1> persistCurrent)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.requiresPersistence = requiresPersistence
                ?? throw new ArgumentNullException(nameof(requiresPersistence));
            this.persistCurrent = persistCurrent
                ?? throw new ArgumentNullException(nameof(persistCurrent));
        }

        public bool TryExportProfiles(
            out IReadOnlyList<ProductionFlowProfileRecordV1> profiles,
            out string rejectionCode)
        {
            return inner.TryExportProfiles(out profiles, out rejectionCode);
        }

        public bool TryActivate(
            int slotIndex,
            ProductionFlowProfileRecordV1 requestedProfile,
            out ProductionFlowProfileRecordV1 authoritativeProfile,
            out string rejectionCode)
        {
            authoritativeProfile = null;
            rejectionCode = string.Empty;
            if (requiresPersistence(slotIndex))
            {
                CharacterCompositionResultV1 persisted = persistCurrent(
                    slotIndex,
                    requestedProfile);
                if (persisted == null || !persisted.Succeeded)
                {
                    rejectionCode = persisted == null
                        ? "character-switch-save-result-null"
                        : "character-switch-save-rejected:"
                            + persisted.Diagnostic;
                    return false;
                }
            }

            return inner.TryActivate(
                slotIndex,
                requestedProfile,
                out authoritativeProfile,
                out rejectionCode);
        }

        public bool TryDelete(
            int slotIndex,
            ProductionFlowProfileRecordV1 requestedProfile,
            out string rejectionCode)
        {
            return inner.TryDelete(
                slotIndex,
                requestedProfile,
                out rejectionCode);
        }
    }

    /// <summary>
    /// Unity installation seam for the transactional decorator. It wraps the existing
    /// account lifecycle; it does not own profile, account, or subsystem state.
    /// </summary>
    [DefaultExecutionOrder(-31925)]
    [DisallowMultipleComponent]
    public sealed class ProductionCharacterActivationGuardV1 : MonoBehaviour
    {
        private static ProductionCharacterActivationGuardV1 instance;
        private ProductionFlowCoordinatorV1 flow;
        private ProductionCharacterAccountCompositionV1 accountComposition;
        private PersistBeforeCharacterActivationLifecycleV1 guardedLifecycle;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
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
            ProductionFlowCoordinatorV1 coordinator =
                UnityEngine.Object.FindFirstObjectByType<
                    ProductionFlowCoordinatorV1>(
                    FindObjectsInactive.Include);
            if (coordinator == null)
            {
                return;
            }

            ProductionCharacterAccountCompositionV1 account =
                coordinator.GetComponent<
                    ProductionCharacterAccountCompositionV1>();
            if (account == null)
            {
                account = coordinator.gameObject.AddComponent<
                    ProductionCharacterAccountCompositionV1>();
            }

            ProductionCharacterActivationGuardV1 guard =
                coordinator.GetComponent<
                    ProductionCharacterActivationGuardV1>();
            if (guard == null)
            {
                guard = coordinator.gameObject.AddComponent<
                    ProductionCharacterActivationGuardV1>();
            }
            instance = guard;
            guard.Connect();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            flow = GetComponent<ProductionFlowCoordinatorV1>();
            accountComposition = GetComponent<
                ProductionCharacterAccountCompositionV1>();
            Connect();
        }

        private void Connect()
        {
            if (flow == null)
            {
                flow = GetComponent<ProductionFlowCoordinatorV1>();
            }
            if (accountComposition == null)
            {
                accountComposition = GetComponent<
                    ProductionCharacterAccountCompositionV1>();
            }
            if (flow == null || accountComposition == null)
            {
                return;
            }

            if (guardedLifecycle == null)
            {
                guardedLifecycle =
                    new PersistBeforeCharacterActivationLifecycleV1(
                        accountComposition,
                        RequiresPersistence,
                        PersistCurrent);
            }
            flow.ConnectCharacterProfileLifecycle(guardedLifecycle);
        }

        private bool RequiresPersistence(int targetSlotIndex)
        {
            CharacterCompositionCoordinatorV1 composition =
                accountComposition == null
                    ? null
                    : accountComposition.Composition;
            return composition != null
                && composition.ActiveRuntime != null
                && !composition.ActiveRuntime.IsDisposed
                && composition.ActiveSlotIndex != targetSlotIndex;
        }

        private static CharacterCompositionResultV1 PersistCurrent(
            int targetSlotIndex,
            ProductionFlowProfileRecordV1 requestedProfile)
        {
            string requestedFingerprint = requestedProfile == null
                || requestedProfile.Payload == null
                    ? "missing-requested-profile"
                    : requestedProfile.Payload.Fingerprint;
            return ProductionCharacterAccountCompositionV1.PersistCurrent(
                "character-explicit-activation",
                "target-slot=" + targetSlotIndex
                    + "|requested=" + requestedFingerprint);
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
