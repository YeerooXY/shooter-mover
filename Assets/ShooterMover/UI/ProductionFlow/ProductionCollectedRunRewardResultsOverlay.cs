using System;
using System.Globalization;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Additive Results projection for durable collected-run transfer proof. It owns no
    /// reward state and can only issue the exact typed retry held by the Results bridge.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionCollectedRunRewardResultsOverlay :
        MonoBehaviour
    {
        private bool configured;
        private bool hasSeenResults;
        private Vector2 scroll;

        public void Configure()
        {
            configured = true;
            hasSeenResults = false;
            enabled = true;
        }

        private void Update()
        {
            if (!configured) return;
            bool isResults = string.Equals(
                SceneManager.GetActiveScene().path,
                ProductionFlowScenePathsV1.Results,
                StringComparison.Ordinal);
            if (isResults)
            {
                hasSeenResults = true;
                return;
            }
            if (!hasSeenResults) return;

            ProductionCollectedRunRewardResultsBridge.Clear();
            Destroy(this);
        }

        private void OnGUI()
        {
            if (!configured
                || !string.Equals(
                    SceneManager.GetActiveScene().path,
                    ProductionFlowScenePathsV1.Results,
                    StringComparison.Ordinal))
            {
                return;
            }

            CollectedRunRewardTransferResultsProjectionV1 projection =
                ProductionCollectedRunRewardResultsBridge.Current;
            if (projection == null) return;

            float width = Mathf.Min(1040f, Mathf.Max(480f, Screen.width - 64f));
            float height = Mathf.Min(250f, Mathf.Max(170f, Screen.height * 0.30f));
            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    Screen.height - height - 24f,
                    width,
                    height),
                GUI.skin.window);
            GUILayout.Label("PERMANENT REWARD TRANSFER");
            GUILayout.Label(
                projection.Status
                + "  •  persistence "
                + projection.PersistenceStatus
                + "  •  rewards "
                + projection.AppliedRewardStableIds.Count.ToString(
                    CultureInfo.InvariantCulture));
            GUILayout.Label(
                "Operation: " + projection.TransferOperationStableId
                + "\nBatch: " + projection.BatchFingerprint
                + "\nReceipt: "
                + (string.IsNullOrWhiteSpace(projection.ReceiptFingerprint)
                    ? "not recorded"
                    : projection.ReceiptFingerprint)
                + "\nCharacter revision: "
                + projection.CharacterRevision.ToString(
                    CultureInfo.InvariantCulture)
                + "  Account revision: "
                + projection.AccountRevision.ToString(
                    CultureInfo.InvariantCulture));

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(65f));
            for (int index = 0;
                index < projection.AppliedRewardStableIds.Count;
                index++)
            {
                GUILayout.Label(
                    projection.AppliedRewardStableIds[index].ToString());
            }
            if (!string.IsNullOrWhiteSpace(projection.Diagnostic))
                GUILayout.Label("Diagnostic: " + projection.Diagnostic);
            if (!string.IsNullOrWhiteSpace(
                projection.CompensationDiagnostic))
            {
                GUILayout.Label(
                    "Compensation: "
                    + projection.CompensationDiagnostic);
            }
            GUILayout.EndScrollView();

            GUI.enabled = projection.ExactRetryAllowed;
            if (GUILayout.Button(
                "RETRY EXACT TRANSFER",
                GUILayout.Height(36f)))
            {
                CollectedRunRewardTransferResultsProjectionV1 retried;
                ProductionCollectedRunRewardResultsBridge.TryRetry(
                    new RetryCollectedRunRewardTransferCommandV1(
                        projection.TransferOperationStableId,
                        projection.BatchFingerprint),
                    out retried);
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }
    }
}
