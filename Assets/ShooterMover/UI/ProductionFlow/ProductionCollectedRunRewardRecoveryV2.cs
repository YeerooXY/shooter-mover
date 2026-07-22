using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Domain.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Restore-time recovery for durable Prepared custody. It never applies
    /// AwaitingAcceptedEnd records, because those do not prove an accepted completed run.
    /// Each exact custody record is attempted at most once per process; durable receipts
    /// remain the source of truth across further restarts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionCollectedRunRewardRecoveryV2 : MonoBehaviour
    {
        private readonly HashSet<StableId> attempted =
            new HashSet<StableId>();
        private float nextProbeAt;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Install();
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            Install();
        }

        private static void Install()
        {
            ProductionFlowCoordinatorV1 flow =
                FindFirstObjectByType<ProductionFlowCoordinatorV1>(
                    FindObjectsInactive.Include);
            if (flow != null
                && flow.GetComponent<
                    ProductionCollectedRunRewardRecoveryV2>() == null)
            {
                flow.gameObject.AddComponent<
                    ProductionCollectedRunRewardRecoveryV2>();
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextProbeAt) return;
            nextProbeAt = Time.unscaledTime + 1f;

            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 ignoredProfile;
            CharacterCompositionCoordinatorV1 composition;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out graph,
                    out ignoredProfile,
                    out composition)
                || graph == null
                || graph.IsDisposed
                || composition == null)
            {
                return;
            }
            ProductionCollectedRunRewardRuntimeRegistryV2.BindRuntime(
                graph,
                composition);

            RewardApplicationServiceV1 rewardApplication;
            CollectedRunRewardPreparedTransferAuthorityV1 preparedAuthority;
            CollectedRunRewardTransferReceiptAuthorityV1 receipts;
            if (!ProductionCollectedRunRewardRuntimeRegistryV2.TryResolve(
                    graph.Character.CharacterInstanceStableId,
                    out rewardApplication,
                    out preparedAuthority,
                    out receipts))
            {
                return;
            }

            IReadOnlyList<CollectedRunRewardPreparedTransferV1> recoverable =
                preparedAuthority.ExportRecoverable(
                    graph.Character.CharacterInstanceStableId);
            for (int index = 0; index < recoverable.Count; index++)
            {
                CollectedRunRewardPreparedTransferV1 prepared =
                    recoverable[index];
                if (prepared == null
                    || prepared.State
                        != CollectedRunRewardPreparedTransferStateV1.Prepared
                    || !attempted.Add(prepared.CustodyStableId))
                {
                    continue;
                }

                CollectedRunRewardAtomicPlanV2 plan;
                string diagnostic;
                if (!CollectedRunRewardTransferPreparationFactoryV2
                    .TryBuildPlanFromPrepared(
                        prepared,
                        graph,
                        rewardApplication,
                        out plan,
                        out diagnostic))
                {
                    Debug.LogError(
                        "Durable collected-run transfer recovery could not rebuild "
                        + prepared.CustodyStableId
                        + ": "
                        + diagnostic,
                        this);
                    continue;
                }

                var authority =
                    new ProductionCollectedRunRewardAtomicAuthorityV2(
                        graph,
                        rewardApplication,
                        preparedAuthority,
                        receipts);
                var persistence =
                    new ProductionCollectedRunRewardPersistenceV2(
                        composition,
                        preparedAuthority,
                        receipts,
                        graph.Character.CharacterInstanceStableId);
                var service =
                    new ProductionCollectedRunRewardTransferServiceV2(
                        plan,
                        authority,
                        persistence);
                CollectedRunRewardTransferResultV1 result = service.Apply();
                if (result == null)
                {
                    Debug.LogError(
                        "Durable collected-run transfer recovery returned no result for "
                        + prepared.CustodyStableId,
                        this);
                    continue;
                }
                ProductionCollectedRunRewardResultsBridge.Publish(
                    prepared,
                    result);
                if (result.Status
                    == CollectedRunRewardTransferStatusV1
                        .FatalCompensationFailure)
                {
                    Debug.LogError(
                        "Durable collected-run transfer recovery is fatal for "
                        + prepared.CustodyStableId
                        + ": "
                        + result.Diagnostic
                        + " | "
                        + result.CompensationDiagnostic,
                        this);
                }
                else if (!result.Succeeded)
                {
                    Debug.LogWarning(
                        "Durable collected-run transfer remains prepared for explicit retry: "
                        + prepared.CustodyStableId
                        + ": "
                        + result.Diagnostic,
                        this);
                }
            }
        }
    }
}
