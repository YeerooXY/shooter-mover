using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UI.StrongboxOpening
{
    /// <summary>
    /// Bindable projection-only HUD. It renders immutable totals and exposes no mutation API.
    /// The same component can be composed by development fixtures or production callers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootRunHudViewV1 : MonoBehaviour
    {
        public RunLootTotalsPresentationV1 Projection { get; private set; }

        public void Bind(RunLootTotalsPresentationV1 immutableProjection)
        {
            Projection = immutableProjection
                ?? throw new ArgumentNullException(nameof(immutableProjection));
        }

        public void DrawImGui(GUIStyle headingStyle, GUIStyle bodyStyle)
        {
            if (Projection == null)
            {
                GUILayout.Label("RUN HUD UNBOUND", headingStyle ?? GUI.skin.label);
                return;
            }

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("RUN HUD", headingStyle ?? GUI.skin.label, GUILayout.Width(90f));
            GUILayout.Label(
                "Credits " + Projection.Credits.ToString(CultureInfo.InvariantCulture),
                bodyStyle ?? GUI.skin.label);
            GUILayout.Label(
                "Scrap " + Projection.Scrap.ToString(CultureInfo.InvariantCulture),
                bodyStyle ?? GUI.skin.label);
            GUILayout.Label(
                "Boxes " + Projection.Strongboxes.ToString(CultureInfo.InvariantCulture),
                bodyStyle ?? GUI.skin.label);
            GUILayout.EndHorizontal();
        }
    }

    /// <summary>
    /// Bindable grouped-owned-box view. It owns only local exact selection state.
    /// Batch resolution fails closed when the selected group cannot satisfy the requested count.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OwnedStrongboxGroupsViewV1 : MonoBehaviour
    {
        private IReadOnlyList<OwnedStrongboxGroupPresentationV1> groups =
            Array.Empty<OwnedStrongboxGroupPresentationV1>();
        private ExactStrongboxSelectionV1 selection;
        private Vector2 scroll;

        public IReadOnlyList<OwnedStrongboxGroupPresentationV1> Groups { get { return groups; } }
        public ExactStrongboxSelectionV1 Selection { get { return selection; } }
        public StableId SelectedInstanceStableId
        {
            get { return selection == null ? null : selection.SelectedInstanceStableId; }
        }

        public OwnedStrongboxGroupPresentationV1 SelectedGroup
        {
            get
            {
                if (selection == null || selection.SelectedInstanceStableId == null)
                {
                    return null;
                }
                for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    OwnedStrongboxGroupPresentationV1 group = groups[groupIndex];
                    for (int instanceIndex = 0;
                         instanceIndex < group.Instances.Count;
                         instanceIndex++)
                    {
                        if (group.Instances[instanceIndex].InstanceStableId
                            == selection.SelectedInstanceStableId)
                        {
                            return group;
                        }
                    }
                }
                return null;
            }
        }

        public void Bind(IEnumerable<OwnedStrongboxGroupPresentationV1> immutableGroups)
        {
            if (immutableGroups == null)
            {
                throw new ArgumentNullException(nameof(immutableGroups));
            }

            var copy = new List<OwnedStrongboxGroupPresentationV1>();
            foreach (OwnedStrongboxGroupPresentationV1 group in immutableGroups)
            {
                if (group == null)
                {
                    throw new ArgumentException(
                        "Owned strongbox groups cannot contain null.",
                        nameof(immutableGroups));
                }
                copy.Add(group);
            }

            groups = new ReadOnlyCollection<OwnedStrongboxGroupPresentationV1>(copy);
            selection = new ExactStrongboxSelectionV1(groups);
            scroll = Vector2.zero;
        }

        public bool TrySelectExact(StableId instanceStableId, out string diagnostic)
        {
            if (selection == null)
            {
                diagnostic = "loot-presentation-groups-view-unbound";
                return false;
            }
            return selection.TrySelectExact(instanceStableId, out diagnostic);
        }

        public bool TryResolveBatchExact(
            int requestedCount,
            out IReadOnlyList<StableId> batch,
            out string diagnostic)
        {
            batch = Array.Empty<StableId>();
            diagnostic = string.Empty;
            if (requestedCount < 1)
            {
                diagnostic = "loot-presentation-opening-count-invalid";
                return false;
            }
            if (selection == null)
            {
                diagnostic = "loot-presentation-groups-view-unbound";
                return false;
            }

            OwnedStrongboxGroupPresentationV1 selected = SelectedGroup;
            if (selected == null)
            {
                diagnostic = "loot-presentation-opening-selection-missing";
                return false;
            }
            if (selected.Quantity < requestedCount)
            {
                diagnostic = "loot-presentation-opening-insufficient-exact-instances:"
                    + selected.Quantity.ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + requestedCount.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            IReadOnlyList<StableId> resolved = selection.ResolveBatch(requestedCount);
            if (resolved.Count != requestedCount)
            {
                diagnostic = "loot-presentation-opening-exact-batch-incomplete";
                return false;
            }
            batch = resolved;
            return true;
        }

        public void DrawImGui(GUIStyle headingStyle)
        {
            GUILayout.Label(
                "OWNED BOX GROUPS — exact identities remain selectable",
                headingStyle ?? GUI.skin.label);
            scroll = GUILayout.BeginScrollView(scroll);
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                OwnedStrongboxGroupPresentationV1 group = groups[groupIndex];
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(
                    "T" + group.TierNumber.ToString(CultureInfo.InvariantCulture)
                    + "  " + group.TierLabel
                    + " x " + group.Quantity.ToString(CultureInfo.InvariantCulture),
                    headingStyle ?? GUI.skin.label);
                for (int instanceIndex = 0;
                     instanceIndex < group.Instances.Count;
                     instanceIndex++)
                {
                    StableId instanceId = group.Instances[instanceIndex].InstanceStableId;
                    bool selected = instanceId == SelectedInstanceStableId;
                    if (GUILayout.Button(
                        (selected ? "> " : "  ") + instanceId,
                        GUILayout.Height(24f)))
                    {
                        string ignored;
                        TrySelectExact(instanceId, out ignored);
                    }
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }
    }

    /// <summary>
    /// Bindable immutable reward-card view. It never applies or transforms rewards.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StrongboxRewardCardsViewV1 : MonoBehaviour
    {
        private Vector2 scroll;

        public StrongboxOpeningPresentationResultV1 Result { get; private set; }
        public int VisibleRewardCount { get; private set; }

        public void Bind(
            StrongboxOpeningPresentationResultV1 immutableResult,
            int visibleRewardCount)
        {
            Result = immutableResult;
            VisibleRewardCount = immutableResult == null
                ? 0
                : Mathf.Clamp(visibleRewardCount, 0, immutableResult.Items.Count);
        }

        public void DrawImGui(GUIStyle bodyStyle, float height)
        {
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(height));
            if (Result == null)
            {
                GUILayout.Label("No committed result.", bodyStyle ?? GUI.skin.label);
                GUILayout.EndScrollView();
                return;
            }

            int visible = Mathf.Min(VisibleRewardCount, Result.Items.Count);
            for (int index = 0; index < visible; index++)
            {
                StrongboxRewardRevealItemV1 item = Result.Items[index];
                Color original = GUI.color;
                GUI.color = ResolveCardTint(item.Kind);
                GUILayout.BeginVertical(GUI.skin.box);
                GUI.color = original;
                GUILayout.Label(
                    item.Kind + " — " + item.Title
                    + (item.Quantity == 1L
                        ? string.Empty
                        : " x" + item.Quantity.ToString(CultureInfo.InvariantCulture)),
                    bodyStyle ?? GUI.skin.label);
                GUILayout.Label(
                    "Content: " + item.ContentStableId,
                    bodyStyle ?? GUI.skin.label);
                if (item.IsUniqueInstance)
                {
                    GUILayout.Label(
                        "Instance: " + item.InstanceStableId,
                        bodyStyle ?? GUI.skin.label);
                }
                if (!string.IsNullOrEmpty(item.Detail))
                {
                    GUILayout.Label(item.Detail, bodyStyle ?? GUI.skin.label);
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        private static Color ResolveCardTint(StrongboxRewardPresentationKindV1 kind)
        {
            switch (kind)
            {
                case StrongboxRewardPresentationKindV1.Money:
                    return new Color(1f, 0.86f, 0.34f, 1f);
                case StrongboxRewardPresentationKindV1.Scrap:
                    return new Color(0.72f, 0.78f, 0.84f, 1f);
                case StrongboxRewardPresentationKindV1.Equipment:
                case StrongboxRewardPresentationKindV1.Armor:
                    return new Color(0.52f, 0.72f, 1f, 1f);
                default:
                    return Color.white;
            }
        }
    }

    /// <summary>
    /// Bindable visual shell for an existing immutable opening session. It owns no opening
    /// transaction and reads stage/progress/result state only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StrongboxOpeningPresentationViewV1 : MonoBehaviour
    {
        private StrongboxRewardCardsViewV1 rewardCards;

        public StrongboxOpeningSceneSessionV1 Session { get; private set; }

        public void Bind(
            StrongboxOpeningSceneSessionV1 immutableSession,
            StrongboxRewardCardsViewV1 rewardCardsView)
        {
            Session = immutableSession
                ?? throw new ArgumentNullException(nameof(immutableSession));
            rewardCards = rewardCardsView
                ?? throw new ArgumentNullException(nameof(rewardCardsView));
            Synchronize();
        }

        public void Synchronize()
        {
            if (Session == null || rewardCards == null)
            {
                return;
            }
            rewardCards.Bind(Session.Result, Session.VisibleRewardCount);
        }

        public void DrawImGui(
            GUIStyle headingStyle,
            GUIStyle bodyStyle,
            GUIStyle warningStyle)
        {
            if (Session == null)
            {
                GUILayout.Label(
                    "OPENING PRESENTATION UNBOUND",
                    warningStyle ?? GUI.skin.label);
                return;
            }

            Synchronize();
            GUILayout.Label(
                Session.Configuration.TierLabel + " STRONGBOX",
                headingStyle ?? GUI.skin.label);
            GUILayout.Label(
                "Stage: " + Session.Stage,
                bodyStyle ?? GUI.skin.label);
            Rect stageRect = GUILayoutUtility.GetRect(
                250f,
                150f,
                GUILayout.ExpandWidth(true));
            DrawAnimatedBox(stageRect);

            if (Session.Result != null)
            {
                GUILayout.Label(
                    "Committed result: " + Session.Result.StatusText,
                    warningStyle ?? GUI.skin.label);
            }
            rewardCards.DrawImGui(bodyStyle, 210f);
        }

        private void DrawAnimatedBox(Rect area)
        {
            float progress = Session.OpeningProgress;
            float openingOffset = Session.Stage == StrongboxRevealStageV1.BoxClosed
                ? 0f
                : 28f * progress;
            if (Session.Stage >= StrongboxRevealStageV1.RewardReveal)
            {
                openingOffset = 28f;
            }

            float pulse = Session.Stage == StrongboxRevealStageV1.OpeningAnimation
                ? 1f + Mathf.Sin(Time.unscaledTime * 22f) * 0.06f
                : 1f;
            float width = 170f * pulse;
            float height = 86f * pulse;
            float centerX = area.x + area.width * 0.5f;
            float centerY = area.y + area.height * 0.58f;
            var body = new Rect(
                centerX - width * 0.5f,
                centerY - height * 0.25f,
                width,
                height * 0.72f);
            var lid = new Rect(
                centerX - width * 0.52f,
                body.y - 20f - openingOffset,
                width * 1.04f,
                24f);

            Color original = GUI.color;
            Color accent = ResolveStageAccent(Session.Stage);
            GUI.color = accent;
            GUI.Box(body, string.Empty);
            GUI.Box(lid, string.Empty);
            GUI.color = original;

            string stageLabel = Session.Stage == StrongboxRevealStageV1.BoxClosed
                ? "CLOSED"
                : Session.Stage == StrongboxRevealStageV1.OpeningAnimation
                    ? "OPENING " + Mathf.RoundToInt(progress * 100f)
                        .ToString(CultureInfo.InvariantCulture) + "%"
                    : Session.Stage == StrongboxRevealStageV1.RewardReveal
                        ? "REVEAL"
                        : "COMPLETE";
            GUI.Label(
                new Rect(area.x, area.yMax - 24f, area.width, 22f),
                stageLabel,
                new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                });
        }

        private static Color ResolveStageAccent(StrongboxRevealStageV1 stage)
        {
            switch (stage)
            {
                case StrongboxRevealStageV1.BoxClosed:
                    return new Color(0.44f, 0.5f, 0.58f, 1f);
                case StrongboxRevealStageV1.OpeningAnimation:
                    return new Color(0.82f, 0.58f, 0.18f, 1f);
                case StrongboxRevealStageV1.RewardReveal:
                    return new Color(0.38f, 0.72f, 1f, 1f);
                default:
                    return new Color(0.44f, 0.9f, 0.62f, 1f);
            }
        }
    }
}
