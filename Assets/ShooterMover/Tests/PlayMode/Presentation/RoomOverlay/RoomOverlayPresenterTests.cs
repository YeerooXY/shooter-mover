using System.Collections;
using NUnit.Framework;
using ShooterMover.Presentation.RoomOverlay;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Presentation.RoomOverlay
{
    public sealed class RoomOverlayPresenterTests
    {
        [UnityTest]
        public IEnumerator VisibilityAndWarningToggles_UpdateBoundViewWithoutPersistence()
        {
            GameObject root = new GameObject("VS-UI-003 Room Overlay Test");
            RoomOverlayView view = root.AddComponent<RoomOverlayView>();
            RoomOverlayPresenter presenter = root.AddComponent<RoomOverlayPresenter>();

            Assert.That(presenter.View, Is.SameAs(view));
            Assert.That(presenter.CurrentFrame, Is.Not.Null);
            Assert.That(view.CurrentFrame, Is.SameAs(presenter.CurrentFrame));
            Assert.That(view.IsVisible, Is.True);

            presenter.SetOverlayVisible(false);
            Assert.That(presenter.IsOverlayVisible, Is.False);
            Assert.That(view.IsVisible, Is.False);

            presenter.SetOverlayVisible(true);
            presenter.SetReducedEffects(true);
            presenter.SetTemporaryStateLabel("ROOM CLEAR");
            Assert.That(presenter.IsOverlayVisible, Is.True);
            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.CurrentFrame.HasReducedEffectsWarning, Is.True);
            Assert.That(view.CurrentFrame.TemporaryStateLabel, Is.EqualTo("ROOM CLEAR"));

            Object.Destroy(root);
            yield return null;
            Assert.That(root == null, Is.True);
        }

        [UnityTest]
        public IEnumerator SafeLayout_ReservesBottomHudBandAndKeepsPanelsInsideMargins()
        {
            GameObject root = new GameObject("VS-UI-003 Layout Test");
            RoomOverlayView view = root.AddComponent<RoomOverlayView>();
            RoomOverlayPresenter presenter = root.AddComponent<RoomOverlayPresenter>();
            presenter.SetReducedEffects(true);
            presenter.SetTemporaryStateLabel("PROTOTYPE");

            Rect safeArea = new Rect(0f, 0f, 1280f, 720f);
            RoomOverlayLayout layout = view.CalculateLayout(safeArea);

            Assert.That(layout.ContentArea.xMin, Is.GreaterThanOrEqualTo(view.SafeMargin));
            Assert.That(layout.ContentArea.yMin, Is.GreaterThanOrEqualTo(view.SafeMargin));
            Assert.That(layout.ContentArea.xMax, Is.LessThanOrEqualTo(safeArea.xMax - view.SafeMargin));
            Assert.That(layout.ContentArea.yMax, Is.LessThanOrEqualTo(safeArea.yMax - view.SafeMargin - view.BottomHudReserve));
            Assert.That(layout.ContentArea.Contains(layout.PrimaryPanel.min), Is.True);
            Assert.That(layout.ContentArea.Contains(layout.PrimaryPanel.max), Is.True);
            Assert.That(layout.HasStatusPanel, Is.True);
            Assert.That(layout.ContentArea.Contains(layout.StatusPanel.min), Is.True);
            Assert.That(layout.ContentArea.Contains(layout.StatusPanel.max), Is.True);

            Object.Destroy(root);
            yield return null;
        }
    }
}
