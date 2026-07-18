using NUnit.Framework;
using ShooterMover.Production.Stage1;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Missions.Run
{
    public sealed class Stage1PlayerPresentationV1Tests
    {
        [Test]
        public void Capture_RejectsMissingRetainedPlayer()
        {
            GameObject root = new GameObject("stage1-player-boundary-missing");
            try
            {
                Stage1PlayerPresentationV1 presentation =
                    root.AddComponent<Stage1PlayerPresentationV1>();

                bool captured = presentation.TryCaptureRetainedPlayer();

                Assert.That(captured, Is.False);
                Assert.That(presentation.IsCaptured, Is.False);
                Assert.That(
                    presentation.RejectionCode,
                    Is.EqualTo("stage1-player-projection-missing"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Capture_RejectsIncompletePlayerProjection()
        {
            GameObject root = new GameObject("stage1-player-boundary-incomplete");
            GameObject player = new GameObject(Stage1PlayerPresentationV1.RetainedPlayerObjectName);
            try
            {
                player.transform.SetParent(root.transform, false);
                player.AddComponent<Rigidbody2D>();
                Stage1PlayerPresentationV1 presentation =
                    root.AddComponent<Stage1PlayerPresentationV1>();

                bool captured = presentation.TryCapture(player.transform);

                Assert.That(captured, Is.False);
                Assert.That(presentation.IsCaptured, Is.False);
                Assert.That(
                    presentation.RejectionCode,
                    Is.EqualTo("stage1-player-projection-incomplete"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Capture_RejectsNullCandidate()
        {
            GameObject root = new GameObject("stage1-player-boundary-null");
            try
            {
                Stage1PlayerPresentationV1 presentation =
                    root.AddComponent<Stage1PlayerPresentationV1>();

                Assert.Throws<System.ArgumentNullException>(() =>
                    presentation.TryCapture(null));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
