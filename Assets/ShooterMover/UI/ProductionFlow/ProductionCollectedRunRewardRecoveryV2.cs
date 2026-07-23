using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Restore-time recovery for durable Prepared custody. AwaitingAcceptedEnd records are
    /// never applied because they do not prove an accepted completed run. Recoverable failures
    /// use bounded exponential backoff and then remain visible through a persistent flow-level
    /// notice with an exact custody retry action. Fatal or durability-uncertain outcomes are
    /// permanently non-retryable in this process.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionCollectedRunRewardRecoveryV2 : MonoBehaviour
    {
        private const int AutomaticRetryLimit = 5;
        private const float ProbeIntervalSeconds = 1f;
        private const float InitialRetryDelaySeconds = 1f;
        private const float MaximumRetryDelaySeconds = 30f;

        private sealed class RecoveryAttemptState
        {
            public int AttemptCount;
            public float NextAttemptAt;
            public bool Exhausted;
            public bool Fatal;
            public string Diagnostic = string.Empty;
        }

        private sealed class RecoveryNotice
        {
            public StableId CustodyStableId;
            public StableId SelectedCharacterStableId;
            public string Diagnostic = string.Empty;
            public bool Fatal;
            public int AttemptCount;
            public float NextAttemptAt;
            public CollectedRunRewardTransferResultsProjectionV1 Projection;
        }

        private readonly Dictionary<StableId, RecoveryAttemptState> attempts =
            new Dictionary<StableId, RecoveryAttemptState>();
        private float nextProbeAt;
        private RecoveryNotice notice;
        private Vector2 noticeScroll;

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
            nextProbeAt = Time.unscaledTime + ProbeIntervalSeconds;

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
                        != CollectedRunRewardPreparedTransferStateV1.Prepared)
                {
                    continue;
                }

                RecoveryAttemptState state;
                if (!attempts.TryGetValue(
                    prepared.CustodyStableId,
                    out state))
                {
                    state = new RecoveryAttemptState();
                    attempts.Add(prepared.CustodyStableId, state);
                }
                if (state.Fatal
                    || state.Exhausted
                    || Time.unscaledTime < state.NextAttemptAt)
                {
                    continue;
                }

                AttemptRecovery(
                    prepared,
                    graph,
                    composition,
                    rewardApplication,
                    preparedAuthority,
                    receipts,
                    state);

                // One permanent-state transaction per probe keeps retries serialized.
                break;
            }
        }

        private void AttemptRecovery(
            CollectedRunRewardPreparedTransferV1 prepared,
            ProductionCharacterRuntimeGraphV1 graph,
            CharacterCompositionCoordinatorV1 composition,
            RewardApplicationServiceV1 rewardApplication,
            CollectedRunRewardPreparedTransferAuthorityV1 preparedAuthority,
            CollectedRunRewardTransferReceiptAuthorityV1 receipts,
            RecoveryAttemptState state)
        {
            state.AttemptCount++;

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
                RegisterRecoverableFailure(
                    prepared,
                    state,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "The durable transfer plan could not be rebuilt."
                        : diagnostic,
                    null);
                return;
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
                RegisterRecoverableFailure(
                    prepared,
                    state,
                    "The durable transfer recovery returned no result.",
                    null);
                return;
            }

            ProductionCollectedRunRewardResultsBridge.Publish(
                prepared,
                result);
            ConfigureResultsOverlay();
            CollectedRunRewardTransferResultsProjectionV1 projection =
                ProductionCollectedRunRewardResultsBridge.Current;

            if (result.Succeeded)
            {
                attempts.Remove(prepared.CustodyStableId);
                if (notice != null
                    && notice.CustodyStableId
                        == prepared.CustodyStableId)
                {
                    notice = null;
                }
                Debug.Log(
                    "Durable collected-run transfer recovery completed for "
                    + prepared.CustodyStableId,
                    this);
                return;
            }

            bool fatal = result.Status
                    == CollectedRunRewardTransferStatusV1
                        .FatalCompensationFailure
                || (result.Persistence != null
                    && result.Persistence.DurableStateUncertain);
            if (fatal)
            {
                state.Fatal = true;
                state.Diagnostic = result.Diagnostic;
                notice = new RecoveryNotice
                {
                    CustodyStableId = prepared.CustodyStableId,
                    SelectedCharacterStableId =
                        prepared.SelectedCharacterStableId,
                    Diagnostic = BuildResultDiagnostic(result),
                    Fatal = true,
                    AttemptCount = state.AttemptCount,
                    NextAttemptAt = float.PositiveInfinity,
                    Projection = projection,
                };
                Debug.LogError(
                    "Durable collected-run transfer recovery is fatal for "
                    + prepared.CustodyStableId
                    + ": "
                    + notice.Diagnostic,
                    this);
                return;
            }

            RegisterRecoverableFailure(
                prepared,
                state,
                BuildResultDiagnostic(result),
                projection);
        }

        private void RegisterRecoverableFailure(
            CollectedRunRewardPreparedTransferV1 prepared,
            RecoveryAttemptState state,
            string diagnostic,
            CollectedRunRewardTransferResultsProjectionV1 projection)
        {
            state.Diagnostic = diagnostic ?? string.Empty;
            if (state.AttemptCount >= AutomaticRetryLimit)
            {
                state.Exhausted = true;
                state.NextAttemptAt = float.PositiveInfinity;
            }
            else
            {
                int exponent = Math.Min(state.AttemptCount - 1, 5);
                float delay = Mathf.Min(
                    MaximumRetryDelaySeconds,
                    InitialRetryDelaySeconds * (1 << exponent));
                state.NextAttemptAt = Time.unscaledTime + delay;
            }

            notice = new RecoveryNotice
            {
                CustodyStableId = prepared.CustodyStableId,
                SelectedCharacterStableId = prepared.SelectedCharacterStableId,
                Diagnostic = state.Diagnostic,
                Fatal = false,
                AttemptCount = state.AttemptCount,
                NextAttemptAt = state.NextAttemptAt,
                Projection = projection,
            };

            Debug.LogWarning(
                "Durable collected-run transfer remains prepared for recovery: "
                + prepared.CustodyStableId
                + ": "
                + state.Diagnostic,
                this);
        }

        private void OnGUI()
        {
            if (notice == null) return;

            float width = Mathf.Min(
                620f,
                Mathf.Max(420f, Screen.width - 48f));
            float height = 245f;
            GUILayout.BeginArea(
                new Rect(
                    Screen.width - width - 24f,
                    24f,
                    width,
                    height),
                GUI.skin.window);
            GUILayout.Label(
                notice.Fatal
                    ? "REWARD RECOVERY REQUIRES ATTENTION"
                    : "REWARD RECOVERY PENDING");
            GUILayout.Label(
                "Custody: "
                + notice.CustodyStableId
                + "\nAttempts: "
                + notice.AttemptCount.ToString(
                    CultureInfo.InvariantCulture));

            noticeScroll = GUILayout.BeginScrollView(
                noticeScroll,
                GUILayout.Height(90f));
            GUILayout.Label(notice.Diagnostic);
            GUILayout.EndScrollView();

            if (notice.Fatal)
            {
                GUILayout.Label(
                    "Durable state is uncertain. Automatic and exact retry are disabled.");
            }
            else
            {
                RecoveryAttemptState state;
                bool exhausted = attempts.TryGetValue(
                        notice.CustodyStableId,
                        out state)
                    && state.Exhausted;
                if (!exhausted)
                {
                    float remaining = Mathf.Max(
                        0f,
                        notice.NextAttemptAt - Time.unscaledTime);
                    GUILayout.Label(
                        "Automatic retry in "
                        + remaining.ToString("0", CultureInfo.InvariantCulture)
                        + "s.");
                }

                if (GUILayout.Button(
                    "RETRY EXACT RECOVERY NOW",
                    GUILayout.Height(36f)))
                {
                    RetryNoticeNow();
                }
            }
            GUILayout.EndArea();
        }

        private void RetryNoticeNow()
        {
            if (notice == null || notice.Fatal) return;

            CollectedRunRewardTransferResultsProjectionV1 projection =
                notice.Projection;
            if (projection != null && projection.ExactRetryAllowed)
            {
                CollectedRunRewardTransferResultsProjectionV1 retried;
                ProductionCollectedRunRewardResultsBridge.TryRetry(
                    new RetryCollectedRunRewardTransferCommandV1(
                        projection.CustodyStableId,
                        projection.TransferOperationStableId,
                        projection.BatchFingerprint,
                        projection.ApplicationPlanFingerprint),
                    out retried);
                if (retried != null)
                {
                    notice.Projection = retried;
                    notice.Diagnostic = retried.Diagnostic;
                    if (retried.IsComplete)
                    {
                        attempts.Remove(notice.CustodyStableId);
                        notice = null;
                        return;
                    }
                    if (retried.PersistenceStatus
                        == CollectedRunRewardTransferPersistenceStatusV1
                            .DurableStateUncertain)
                    {
                        RecoveryAttemptState fatalState;
                        if (attempts.TryGetValue(
                            retried.CustodyStableId,
                            out fatalState))
                        {
                            fatalState.Fatal = true;
                            fatalState.Exhausted = false;
                        }
                        notice.Fatal = true;
                        return;
                    }
                }
            }

            RecoveryAttemptState state;
            if (attempts.TryGetValue(notice.CustodyStableId, out state))
            {
                state.Exhausted = false;
                state.NextAttemptAt = Time.unscaledTime;
                notice.NextAttemptAt = state.NextAttemptAt;
                notice.Diagnostic =
                    "Exact durable custody retry was requested.";
            }
        }

        private void ConfigureResultsOverlay()
        {
            ProductionCollectedRunRewardResultsOverlay overlay =
                GetComponent<ProductionCollectedRunRewardResultsOverlay>();
            if (overlay == null)
            {
                overlay = gameObject.AddComponent<
                    ProductionCollectedRunRewardResultsOverlay>();
            }
            overlay.Configure();
        }

        private static string BuildResultDiagnostic(
            CollectedRunRewardTransferResultV1 result)
        {
            if (result == null) return "recovery-result-null";
            if (string.IsNullOrWhiteSpace(result.CompensationDiagnostic))
                return result.Diagnostic;
            return result.Diagnostic
                + " | compensation: "
                + result.CompensationDiagnostic;
        }
    }
}
