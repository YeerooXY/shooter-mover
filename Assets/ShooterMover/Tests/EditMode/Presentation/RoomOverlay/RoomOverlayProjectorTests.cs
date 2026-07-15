using System;
using NUnit.Framework;
using ShooterMover.Presentation.RoomOverlay;

namespace ShooterMover.Tests.EditMode.Presentation.RoomOverlay
{
    public sealed class RoomOverlayProjectorTests
    {
        [Test]
        public void EquivalentInput_ProjectsDeterministicTrace()
        {
            RoomOverlayProjector projector = new RoomOverlayProjector();
            RoomOverlayFrame first = projector.Project(new RoomOverlayInput(
                "  GENERATOR\nHALL  ",
                " Reach   the exit ",
                " R ",
                " MENU ",
                " WAVE\t1 ",
                true));
            RoomOverlayFrame second = projector.Project(new RoomOverlayInput(
                "GENERATOR HALL",
                "Reach the exit",
                "R",
                "MENU",
                "WAVE 1",
                true));

            Assert.That(first.ToTraceString(), Is.EqualTo(second.ToTraceString()));
            Assert.That(first.RoomName, Is.EqualTo("GENERATOR HALL"));
            Assert.That(first.ObjectiveText, Is.EqualTo("OBJECTIVE: Reach the exit"));
            Assert.That(first.RestartHint, Is.EqualTo("RESTART: R / MENU"));
            Assert.That(first.TemporaryStateLabel, Is.EqualTo("WAVE 1"));
            Assert.That(first.ReducedEffectsWarning, Is.EqualTo(RoomOverlayProjector.ReducedEffectsWarning));
        }

        [Test]
        public void MissingOptionalLabels_UseFallbackHintsWithoutInventingState()
        {
            RoomOverlayFrame frame = new RoomOverlayProjector().Project(new RoomOverlayInput(
                null,
                " ",
                null,
                null,
                "\r\n",
                false));

            Assert.That(frame.RoomName, Is.EqualTo(RoomOverlayProjector.DefaultRoomName));
            Assert.That(frame.ObjectiveText, Is.EqualTo("OBJECTIVE: " + RoomOverlayProjector.DefaultObjective));
            Assert.That(frame.RestartHint, Is.EqualTo("RESTART: R / MENU"));
            Assert.That(frame.HasTemporaryStateLabel, Is.False);
            Assert.That(frame.HasReducedEffectsWarning, Is.False);
            Assert.That(frame.ToTraceString(), Does.Contain("temporary_state=none"));
            Assert.That(frame.ToTraceString(), Does.Contain("warning=none"));
        }

        [Test]
        public void NullInput_IsRejectedVisibly()
        {
            Assert.Throws<ArgumentNullException>(() => new RoomOverlayProjector().Project(null));
        }
    }
}
