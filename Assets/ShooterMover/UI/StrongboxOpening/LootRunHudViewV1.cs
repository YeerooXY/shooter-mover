using System;
using System.Globalization;
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
            GUILayout.Label(
                "RUN HUD",
                headingStyle ?? GUI.skin.label,
                GUILayout.Width(90f));
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
}
