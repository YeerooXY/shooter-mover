using System;
using ShooterMover.Domain.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Flow-level non-interactive notice for terminal transfer failures that must be visible
    /// even when the active scene is not Results. It owns no retry command or reward payload.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionCollectedRunRewardTerminalNoticeV1 :
        MonoBehaviour
    {
        private StableId custodyStableId;
        private string heading = string.Empty;
        private string diagnostic = string.Empty;
        private string guidance = string.Empty;
        private bool hideWhenResultsIsActive;
        private bool configured;
        private Vector2 scroll;

        public void Publish(
            StableId custodyStableId,
            string heading,
            string diagnostic,
            string guidance,
            bool hideWhenResultsIsActive)
        {
            this.custodyStableId = custodyStableId
                ?? throw new ArgumentNullException(nameof(custodyStableId));
            this.heading = string.IsNullOrWhiteSpace(heading)
                ? "REWARD TRANSFER REQUIRES ATTENTION"
                : heading.Trim();
            this.diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                ? "The terminal reward transfer could not continue."
                : diagnostic.Trim();
            this.guidance = string.IsNullOrWhiteSpace(guidance)
                ? "Automatic retry is disabled."
                : guidance.Trim();
            this.hideWhenResultsIsActive = hideWhenResultsIsActive;
            configured = true;
            enabled = true;
        }

        private void Update()
        {
            if (!configured || !hideWhenResultsIsActive) return;
            if (!string.Equals(
                SceneManager.GetActiveScene().path,
                ProductionFlowScenePathsV1.Results,
                StringComparison.Ordinal))
            {
                return;
            }
            Destroy(this);
        }

        private void OnGUI()
        {
            if (!configured) return;

            float width = Mathf.Min(
                680f,
                Mathf.Max(440f, Screen.width - 48f));
            float height = 255f;
            GUILayout.BeginArea(
                new Rect(
                    (Screen.width - width) * 0.5f,
                    24f,
                    width,
                    height),
                GUI.skin.window);
            GUILayout.Label(heading);
            GUILayout.Label("Custody: " + custodyStableId);
            scroll = GUILayout.BeginScrollView(
                scroll,
                GUILayout.Height(112f));
            GUILayout.Label(diagnostic);
            GUILayout.EndScrollView();
            GUILayout.Label(guidance);
            GUILayout.Label("The retained terminal transaction remains available for diagnostics.");
            GUILayout.EndArea();
        }
    }
}
