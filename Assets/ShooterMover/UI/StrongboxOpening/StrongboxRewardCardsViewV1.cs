using System.Globalization;
using UnityEngine;

namespace ShooterMover.UI.StrongboxOpening
{
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
                : Mathf.Clamp(
                    visibleRewardCount,
                    0,
                    immutableResult.Items.Count);
        }

        public void DrawImGui(GUIStyle bodyStyle, float height)
        {
            scroll =
                GUILayout.BeginScrollView(scroll, GUILayout.Height(height));
            if (Result == null)
            {
                GUILayout.Label(
                    "No committed result.",
                    bodyStyle ?? GUI.skin.label);
                GUILayout.EndScrollView();
                return;
            }

            int visible =
                Mathf.Min(VisibleRewardCount, Result.Items.Count);
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
                        : " x"
                            + item.Quantity.ToString(
                                CultureInfo.InvariantCulture)),
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
                    GUILayout.Label(
                        item.Detail,
                        bodyStyle ?? GUI.skin.label);
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        private static Color ResolveCardTint(
            StrongboxRewardPresentationKindV1 kind)
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
}
