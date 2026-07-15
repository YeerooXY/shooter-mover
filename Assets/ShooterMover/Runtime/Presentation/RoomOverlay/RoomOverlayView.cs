using System;
using UnityEngine;

namespace ShooterMover.Presentation.RoomOverlay
{
    public sealed class RoomOverlayLayout
    {
        internal RoomOverlayLayout(Rect contentArea, Rect primaryPanel, Rect statusPanel)
        {
            ContentArea = contentArea;
            PrimaryPanel = primaryPanel;
            StatusPanel = statusPanel;
        }

        public Rect ContentArea { get; }
        public Rect PrimaryPanel { get; }
        public Rect StatusPanel { get; }
        public bool HasStatusPanel => StatusPanel.width > 0f && StatusPanel.height > 0f;
    }

    /// <summary>
    /// Temporary immediate-mode room overlay. It intentionally owns no Canvas, scene, input,
    /// gameplay, mission, or saved state and can be removed as one presentation component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomOverlayView : MonoBehaviour
    {
        private const float PanelGap = 12f;
        private const float PrimaryPanelHeight = 112f;
        private const float StatusPanelHeight = 82f;

        [SerializeField, Min(0f)] private float safeMargin = 24f;
        [SerializeField, Min(0f)] private float bottomHudReserve = 196f;
        [SerializeField] private bool visible = true;

        private RoomOverlayFrame currentFrame;
        private GUIStyle roomTitleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle hintStyle;
        private GUIStyle warningStyle;

        public RoomOverlayFrame CurrentFrame => currentFrame;
        public bool IsVisible => visible;
        public float SafeMargin => safeMargin;
        public float BottomHudReserve => bottomHudReserve;

        public void Apply(RoomOverlayFrame frame)
        {
            currentFrame = frame ?? throw new ArgumentNullException(nameof(frame));
        }

        public void Clear()
        {
            currentFrame = null;
        }

        public void SetVisible(bool value)
        {
            visible = value;
        }

        public RoomOverlayLayout CalculateLayout(Rect safeArea)
        {
            float margin = Mathf.Max(0f, safeMargin);
            float reserve = Mathf.Max(0f, bottomHudReserve);
            float contentWidth = Mathf.Max(0f, safeArea.width - margin * 2f);
            float contentHeight = Mathf.Max(0f, safeArea.height - margin * 2f - reserve);
            Rect content = new Rect(
                safeArea.x + margin,
                safeArea.y + margin,
                contentWidth,
                contentHeight);

            if (contentWidth <= 0f || contentHeight <= 0f)
                return new RoomOverlayLayout(content, Rect.zero, Rect.zero);

            bool showStatus = currentFrame != null &&
                (currentFrame.HasReducedEffectsWarning || currentFrame.HasTemporaryStateLabel);
            bool wide = showStatus && contentWidth >= 840f;

            float primaryHeight = Mathf.Min(PrimaryPanelHeight, contentHeight);
            if (!showStatus)
            {
                return new RoomOverlayLayout(
                    content,
                    new Rect(content.x, content.y, Mathf.Min(640f, content.width), primaryHeight),
                    Rect.zero);
            }

            if (wide)
            {
                float statusWidth = Mathf.Min(360f, contentWidth * 0.34f);
                float primaryWidth = Mathf.Min(640f, contentWidth - statusWidth - PanelGap);
                return new RoomOverlayLayout(
                    content,
                    new Rect(content.x, content.y, primaryWidth, primaryHeight),
                    new Rect(content.xMax - statusWidth, content.y, statusWidth,
                        Mathf.Min(StatusPanelHeight, contentHeight)));
            }

            float stackedPrimaryHeight = Mathf.Min(PrimaryPanelHeight, Mathf.Max(0f, contentHeight - StatusPanelHeight - PanelGap));
            Rect primary = new Rect(content.x, content.y, content.width, stackedPrimaryHeight);
            float statusY = primary.yMax + PanelGap;
            float remaining = Mathf.Max(0f, content.yMax - statusY);
            Rect status = remaining > 0f
                ? new Rect(content.x, statusY, content.width, Mathf.Min(StatusPanelHeight, remaining))
                : Rect.zero;
            return new RoomOverlayLayout(content, primary, status);
        }

        private void OnGUI()
        {
            if (!visible || currentFrame == null) return;

            EnsureStyles();
            Rect safeArea = ToGuiSafeArea(Screen.safeArea);
            RoomOverlayLayout layout = CalculateLayout(safeArea);
            if (layout.PrimaryPanel.width <= 0f || layout.PrimaryPanel.height <= 0f) return;

            DrawPrimaryPanel(layout.PrimaryPanel);
            if (layout.HasStatusPanel) DrawStatusPanel(layout.StatusPanel);
        }

        private void DrawPrimaryPanel(Rect panel)
        {
            GUI.Box(panel, GUIContent.none);
            Rect line = Inset(panel, 12f, 8f, 24f);
            GUI.Label(line, currentFrame.RoomName, roomTitleStyle);
            line.y += 31f;
            GUI.Label(line, currentFrame.ObjectiveText, bodyStyle);
            line.y += 25f;
            GUI.Label(line, currentFrame.RestartHint, hintStyle);
        }

        private void DrawStatusPanel(Rect panel)
        {
            GUI.Box(panel, GUIContent.none);
            Rect line = Inset(panel, 12f, 8f, 23f);
            if (currentFrame.HasReducedEffectsWarning)
            {
                GUI.Label(line, currentFrame.ReducedEffectsWarning, warningStyle);
                line.y += 27f;
            }
            if (currentFrame.HasTemporaryStateLabel)
                GUI.Label(line, "STATE: " + currentFrame.TemporaryStateLabel, hintStyle);
        }

        private void EnsureStyles()
        {
            if (roomTitleStyle != null) return;

            roomTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
            };
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                clipping = TextClipping.Clip,
            };
            warningStyle = new GUIStyle(hintStyle)
            {
                fontStyle = FontStyle.Bold,
            };
        }

        private static Rect ToGuiSafeArea(Rect screenSafeArea)
        {
            return new Rect(
                screenSafeArea.x,
                Screen.height - screenSafeArea.yMax,
                screenSafeArea.width,
                screenSafeArea.height);
        }

        private static Rect Inset(Rect source, float horizontal, float top, float height)
        {
            return new Rect(
                source.x + horizontal,
                source.y + top,
                Mathf.Max(0f, source.width - horizontal * 2f),
                height);
        }
    }
}
