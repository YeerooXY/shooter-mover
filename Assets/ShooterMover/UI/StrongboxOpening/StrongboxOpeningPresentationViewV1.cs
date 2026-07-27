using System;
using System.Globalization;
using UnityEngine;

namespace ShooterMover.UI.StrongboxOpening
{
    /// <summary>
    /// Bindable visual shell for an existing immutable opening session. It owns no opening
    /// transaction and reads stage/progress/result state only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StrongboxOpeningPresentationViewV1 : MonoBehaviour
    {
        private StrongboxRewardCardsViewV1 rewardCards;
        private GUIStyle stageLabelStyle;

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
            rewardCards.Bind(
                Session.Result,
                Session.VisibleRewardCount);
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
            float openingOffset =
                Session.Stage == StrongboxRevealStageV1.BoxClosed
                    ? 0f
                    : 28f * progress;
            if ((int)Session.Stage
                >= (int)StrongboxRevealStageV1.RewardReveal)
            {
                openingOffset = 28f;
            }

            float pulse =
                Session.Stage == StrongboxRevealStageV1.OpeningAnimation
                    ? 1f
                        + Mathf.Sin(Time.unscaledTime * 22f)
                            * 0.06f
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
            GUI.color = ResolveStageAccent(Session.Stage);
            GUI.Box(body, string.Empty);
            GUI.Box(lid, string.Empty);
            GUI.color = original;

            GUI.Label(
                new Rect(
                    area.x,
                    area.yMax - 24f,
                    area.width,
                    22f),
                ResolveStageLabel(progress),
                StageLabelStyle);
        }

        private GUIStyle StageLabelStyle
        {
            get
            {
                if (stageLabelStyle == null)
                {
                    stageLabelStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                    };
                }
                return stageLabelStyle;
            }
        }

        private string ResolveStageLabel(float progress)
        {
            switch (Session.Stage)
            {
                case StrongboxRevealStageV1.BoxClosed:
                    return "CLOSED";
                case StrongboxRevealStageV1.OpeningAnimation:
                    return "OPENING "
                        + Mathf.RoundToInt(progress * 100f)
                            .ToString(CultureInfo.InvariantCulture)
                        + "%";
                case StrongboxRevealStageV1.RewardReveal:
                    return "REVEAL";
                default:
                    return "COMPLETE";
            }
        }

        private static Color ResolveStageAccent(
            StrongboxRevealStageV1 stage)
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
