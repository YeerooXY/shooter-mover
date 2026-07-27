using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UI.StrongboxOpening
{
    public sealed partial class LootPresentationShowcaseController
    {
        private void OnGUI()
        {
            if (!enabled)
            {
                return;
            }
            EnsureInitialized();
            EnsureStyles();

            GUILayout.BeginArea(new Rect(12f, 12f, 430f, Screen.height - 24f), GUI.skin.window);
            GUILayout.Label("LOOT PRESENTATION LAB", titleStyle);
            GUILayout.Label("DEVELOPMENT PREVIEW ONLY — no holdings, BOX, RAP or save mutation", warningStyle);
            DrawRunHud();
            DrawGroups();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(Screen.width - 486f, 12f, 474f, Screen.height - 24f), GUI.skin.window);
            DrawOpeningControls();
            GUILayout.Space(8f);
            DrawAuthoritativePickupFixture();
            GUILayout.EndArea();
        }

        private void DrawRunHud()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("RUN HUD", headingStyle, GUILayout.Width(90f));
            GUILayout.Label("Credits " + runTotals.Credits.ToString(CultureInfo.InvariantCulture), bodyStyle);
            GUILayout.Label("Scrap " + runTotals.Scrap.ToString(CultureInfo.InvariantCulture), bodyStyle);
            GUILayout.Label("Boxes " + runTotals.Strongboxes.ToString(CultureInfo.InvariantCulture), bodyStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawGroups()
        {
            GUILayout.Label("OWNED BOX GROUPS — exact identities remain selectable", headingStyle);
            groupScroll = GUILayout.BeginScrollView(groupScroll);
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                OwnedStrongboxGroupPresentationV1 group = groups[groupIndex];
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(
                    "T" + group.TierNumber.ToString(CultureInfo.InvariantCulture)
                    + "  " + group.TierLabel
                    + " x " + group.Quantity.ToString(CultureInfo.InvariantCulture),
                    headingStyle);
                for (int instanceIndex = 0; instanceIndex < group.Instances.Count; instanceIndex++)
                {
                    StableId instanceId = group.Instances[instanceIndex].InstanceStableId;
                    bool selected = instanceId == selection.SelectedInstanceStableId;
                    if (GUILayout.Button((selected ? "> " : "  ") + instanceId, GUILayout.Height(24f)))
                    {
                        selection.TrySelectExact(instanceId, out diagnostic);
                    }
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        private void DrawOpeningControls()
        {
            GUILayout.Label("EXACT BOX OPENING PRESENTATION", titleStyle);
            GUILayout.Label("Selected: " + selection.SelectedInstanceStableId, bodyStyle);
            GUILayout.BeginHorizontal();
            GUI.enabled = openCount != 1;
            if (GUILayout.Button("OPEN 1 LAYOUT")) openCount = 1;
            GUI.enabled = openCount != 5;
            if (GUILayout.Button("OPEN 5 LAYOUT")) openCount = 5;
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            IReadOnlyList<StableId> previewBatch = selection.ResolveBatch(openCount);
            GUILayout.Label("Frozen batch preview: " + JoinIds(previewBatch), bodyStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("PLAY", GUILayout.Height(36f))) PlayOpening();
            if (GUILayout.Button("REPLAY SAME RESULT", GUILayout.Height(36f))) ReplayPresentation();
            if (GUILayout.Button("SKIP", GUILayout.Height(36f))) SkipPresentation();
            GUILayout.EndHorizontal();
            fastForward = GUILayout.Toggle(fastForward, "FAST-FORWARD VISUAL TIME x" + fastForwardMultiplier.ToString("0.#", CultureInfo.InvariantCulture));

            GUILayout.Label("Stage: " + openingSession.Stage, headingStyle);
            GUILayout.Label(
                "Result object is frozen and reused: " + immutableFixtureResult.StatusText,
                warningStyle);
            if (lastOpeningBatch.Count > 0)
            {
                GUILayout.Label("Last exact batch: " + JoinIds(lastOpeningBatch), bodyStyle);
            }

            rewardScroll = GUILayout.BeginScrollView(rewardScroll, GUILayout.Height(210f));
            if (openingSession.Result != null)
            {
                int visible = Mathf.Min(openingSession.VisibleRewardCount, openingSession.Result.Items.Count);
                for (int index = 0; index < visible; index++)
                {
                    StrongboxRewardRevealItemV1 item = openingSession.Result.Items[index];
                    GUILayout.Label(
                        item.Kind + " — " + item.Title
                        + (item.Quantity == 1L ? string.Empty : " x" + item.Quantity.ToString(CultureInfo.InvariantCulture))
                        + "\nContent: " + item.ContentStableId
                        + (item.IsUniqueInstance ? "\nInstance: " + item.InstanceStableId : string.Empty)
                        + "\n" + item.Detail,
                        GUI.skin.box);
                }
            }
            GUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(diagnostic))
            {
                GUILayout.Label("Diagnostic: " + diagnostic, warningStyle);
            }
        }

        private void DrawAuthoritativePickupFixture()
        {
            GUILayout.Label("AUTHORITATIVE PICKUP FIXTURE", headingStyle);
            GUILayout.Label(
                "Disposable run-local truth only. Destroying the view does not collect it.",
                bodyStyle);
            GUILayout.Label(
                "Available: " + (pickupFixture.ExportAvailable() == null ? "no" : "yes")
                + " | View: " + (IsPickupFixtureViewVisible ? "visible" : "absent")
                + " | Collected: " + pickupFixture.IsCollected,
                bodyStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("REJECT NEXT")) RejectNextPickupCollection();
            if (GUILayout.Button("COLLECT")) CollectPickupFixture();
            if (GUILayout.Button("DESTROY VIEW")) DestroyPickupFixtureView();
            if (GUILayout.Button("RECONSTRUCT")) ReconstructPickupFixtureView();
            GUILayout.EndHorizontal();
            if (lastPickupResult != null)
            {
                GUILayout.Label(
                    "Last collection: " + (lastPickupResult.Accepted ? "ACCEPTED" : "REJECTED")
                    + (lastPickupResult.ExactReplay ? " (EXACT REPLAY)" : string.Empty)
                    + (lastPickupResult.Diagnostic.Length == 0 ? string.Empty : " — " + lastPickupResult.Diagnostic),
                    lastPickupResult.Accepted ? bodyStyle : warningStyle);
            }
        }

        private static string JoinIds(IReadOnlyList<StableId> ids)
        {
            if (ids == null || ids.Count == 0) return "none";
            var values = new string[ids.Count];
            for (int index = 0; index < ids.Count; index++) values[index] = ids[index].ToString();
            return string.Join(", ", values);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                wordWrap = true,
            };
            warningStyle = new GUIStyle(bodyStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
        }
    }
}
