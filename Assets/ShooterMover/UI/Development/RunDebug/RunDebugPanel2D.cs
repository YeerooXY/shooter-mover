#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using ShooterMover.Application.Development.RunDebug;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Development.RunDebug;
using UnityEngine;

namespace ShooterMover.UI.Development.RunDebug
{
    [DisallowMultipleComponent]
    public sealed class RunDebugPanel2D : MonoBehaviour
    {
        [SerializeField] private RunDebugRewardBridge2D bridge;
        [SerializeField] private Rect windowRect = new Rect(18f, 18f, 390f, 330f);
        [SerializeField] private string strongboxCount = "1";
        [SerializeField] private string strongboxTier = "strongbox.common";
        [SerializeField] private string deterministicSeed = "1";

        private RunDebugPanelSessionV1 session;
        private string diagnostic = string.Empty;

        private void Awake()
        {
            if (bridge != null)
            {
                session = new RunDebugPanelSessionV1(bridge);
            }
        }

        private void OnGUI()
        {
            windowRect = GUI.Window(
                GetInstanceID(),
                windowRect,
                DrawWindow,
                "DEV-001 Reward Run");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Physical DROP -> PICK -> RAP -> INV -> RUN");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Strongboxes", GUILayout.Width(105f));
            strongboxCount = GUILayout.TextField(strongboxCount);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Tier StableId", GUILayout.Width(105f));
            strongboxTier = GUILayout.TextField(strongboxTier);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Seed", GUILayout.Width(105f));
            deterministicSeed = GUILayout.TextField(deterministicSeed);
            GUILayout.EndHorizontal();

            GUI.enabled = session != null;
            if (GUILayout.Button("Spawn Physical Strongboxes"))
            {
                Spawn();
            }

            if (GUILayout.Button("End Run Once"))
            {
                RunDebugEndResultV1 result =
                    session.EndRun(MissionRunCompletionStateV1.Completed);
                diagnostic = result == null
                    ? "End Run returned no result."
                    : result.Diagnostic;
            }

            RunDebugSnapshotV1 snapshot = session == null
                ? null
                : session.Refresh();
            GUI.enabled = true;

            GUILayout.Space(8f);
            GUILayout.Label(
                snapshot == null
                    ? "Requested: 0   Spawned: 0   Collected: 0   Pending: 0"
                    : "Requested: " + snapshot.RequestedCount
                        + "   Spawned: " + snapshot.SpawnedCount
                        + "   Collected: " + snapshot.CollectedCount
                        + "   Pending: " + snapshot.PendingCount);
            GUILayout.Label(diagnostic);

            if (session != null
                && session.LastEndResult != null
                && session.LastEndResult.ResultsSession != null)
            {
                MissionResultPayloadV1 payload =
                    session.LastEndResult.ResultsSession.Snapshot;
                GUILayout.Label(
                    "Unopened exact instances: "
                    + payload.UnopenedStrongboxes.Count);
                for (int index = 0;
                    index < payload.UnopenedStrongboxes.Count;
                    index++)
                {
                    GUILayout.Label(
                        payload.UnopenedStrongboxes[index]
                            .InstanceStableId
                            .ToString());
                }
            }

            GUI.DragWindow();
        }

        private void Spawn()
        {
            int count;
            ulong seed;
            StableId tier;
            if (!int.TryParse(strongboxCount, out count)
                || !ulong.TryParse(deterministicSeed, out seed)
                || !StableId.TryParse(strongboxTier, out tier))
            {
                diagnostic =
                    "Count, tier StableId, or deterministic seed is invalid.";
                return;
            }

            try
            {
                RunDebugSpawnBatchResultV1 result = session.Spawn(
                    bridge.CreateRequest(count, tier, seed));
                diagnostic = result == null
                    ? "Spawn returned no result."
                    : result.Diagnostic;
            }
            catch (Exception exception)
            {
                diagnostic = exception.Message;
            }
        }
    }
}
#endif
