using System;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Missions.Rooms
{
    /// <summary>
    /// Presentation-only adapter for an authored room door. Open/closed truth remains on
    /// RoomDoorInstance2D and its room runtime authority; this component only reflects that
    /// state through the door renderers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomDoorStatePresentation2D : MonoBehaviour
    {
        private static readonly Color ClosedColor =
            new Color(0.8f, 0.25f, 0.2f, 1f);
        private static readonly Color OpenColor =
            new Color(0.2f, 0.85f, 0.45f, 1f);

        private RoomDoorInstance2D door;
        private SpriteRenderer[] renderers = Array.Empty<SpriteRenderer>();
        private bool hasAppliedState;
        private bool appliedOpenState;
        private bool bindingFailureLogged;

        public bool IsBound
        {
            get { return door != null && door.IsConfigured; }
        }

        public bool LastKnownOpenState
        {
            get { return hasAppliedState && appliedOpenState; }
        }

        public bool RefreshNow()
        {
            if (!TryBind()) return false;

            bool open = door.IsOpen;
            if (hasAppliedState && appliedOpenState == open) return true;

            Color color = open ? OpenColor : ClosedColor;
            for (int index = 0; index < renderers.Length; index++)
            {
                SpriteRenderer renderer = renderers[index];
                if (renderer != null) renderer.color = color;
            }

            appliedOpenState = open;
            hasAppliedState = true;
            return true;
        }

        private void Start()
        {
            RefreshNow();
        }

        private void LateUpdate()
        {
            RefreshNow();
        }

        private bool TryBind()
        {
            if (door != null && door.IsConfigured) return true;

            door = GetComponent<RoomDoorInstance2D>();
            if (door == null || !door.IsConfigured)
            {
                LogBindingFailureOnce("room-door-state-presentation-door-binding-missing");
                return false;
            }

            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                renderers = Array.Empty<SpriteRenderer>();
                LogBindingFailureOnce("room-door-state-presentation-renderer-missing");
                return false;
            }

            bindingFailureLogged = false;
            return true;
        }

        private void LogBindingFailureOnce(string diagnostic)
        {
            if (bindingFailureLogged) return;
            bindingFailureLogged = true;
            Debug.LogError(diagnostic, this);
        }
    }
}
