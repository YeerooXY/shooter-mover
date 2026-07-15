using System;
using UnityEngine;

namespace ShooterMover.Presentation.RoomOverlay
{
    /// <summary>
    /// Temporary session presenter for room framing. Callers may replace labels at runtime,
    /// but this component never stores them beyond the current object lifetime.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RoomOverlayView))]
    public sealed class RoomOverlayPresenter : MonoBehaviour
    {
        [SerializeField] private RoomOverlayView view;
        [SerializeField] private bool startVisible = true;
        [SerializeField] private bool reducedEffects;
        [SerializeField] private string roomName = RoomOverlayProjector.DefaultRoomName;
        [SerializeField] private string objectiveText = RoomOverlayProjector.DefaultObjective;
        [SerializeField] private string restartKeyboardHint = RoomOverlayProjector.DefaultKeyboardRestartHint;
        [SerializeField] private string restartControllerHint = RoomOverlayProjector.DefaultControllerRestartHint;
        [SerializeField] private string temporaryStateLabel = "PROTOTYPE";

        private RoomOverlayProjector projector;
        private RoomOverlayFrame currentFrame;

        public RoomOverlayFrame CurrentFrame => currentFrame;
        public RoomOverlayView View => view;
        public bool IsOverlayVisible => view != null && view.IsVisible;

        private void Awake()
        {
            EnsureDependencies();
            view.SetVisible(startVisible);
            Refresh();
        }

        public void BindView(RoomOverlayView target)
        {
            view = target ?? throw new ArgumentNullException(nameof(target));
            EnsureProjector();
            view.SetVisible(startVisible);
            Refresh();
        }

        public void Present(RoomOverlayInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            EnsureDependencies();
            currentFrame = projector.Project(input);
            view.Apply(currentFrame);
        }

        public void Refresh()
        {
            EnsureDependencies();
            Present(new RoomOverlayInput(
                roomName,
                objectiveText,
                restartKeyboardHint,
                restartControllerHint,
                temporaryStateLabel,
                reducedEffects));
        }

        public void SetOverlayVisible(bool value)
        {
            startVisible = value;
            EnsureDependencies();
            view.SetVisible(value);
        }

        public void SetRoomName(string value)
        {
            roomName = value;
            Refresh();
        }

        public void SetObjectiveText(string value)
        {
            objectiveText = value;
            Refresh();
        }

        public void SetRestartHints(string keyboardHint, string controllerHint)
        {
            restartKeyboardHint = keyboardHint;
            restartControllerHint = controllerHint;
            Refresh();
        }

        public void SetTemporaryStateLabel(string value)
        {
            temporaryStateLabel = value;
            Refresh();
        }

        public void SetReducedEffects(bool value)
        {
            reducedEffects = value;
            Refresh();
        }

        [ContextMenu("VS-UI-003/Load Prototype Overlay")]
        private void LoadPrototypeOverlay()
        {
            roomName = RoomOverlayProjector.DefaultRoomName;
            objectiveText = RoomOverlayProjector.DefaultObjective;
            restartKeyboardHint = RoomOverlayProjector.DefaultKeyboardRestartHint;
            restartControllerHint = RoomOverlayProjector.DefaultControllerRestartHint;
            temporaryStateLabel = "PROTOTYPE";
            Refresh();
        }

        private void EnsureDependencies()
        {
            EnsureProjector();
            if (view == null && !TryGetComponent(out view))
                throw new InvalidOperationException("RoomOverlayPresenter requires a RoomOverlayView on the same object or an explicit bound view.");
        }

        private void EnsureProjector()
        {
            if (projector == null) projector = new RoomOverlayProjector();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            Refresh();
        }
#endif
    }
}
