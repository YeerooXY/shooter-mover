using NUnit.Framework;
using ShooterMover.UI.LevelSelection;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Missions.Run
{
    public sealed class Stage1ProductionPresentationHostV1Tests
    {
        [Test]
        public void Host_ControlsExactRetainedPresentation()
        {
            GameObject root = new GameObject("stage1-presentation-host-test");
            try
            {
                Stage1ProductionPresentationHostV1 host =
                    root.AddComponent<Stage1ProductionPresentationHostV1>();
                Stage1ProductionPresentationHostV1 retained =
                    root.AddComponent<Stage1ProductionPresentationHostV1>();

                host.ConfigureForTests(retained);
                host.SetPresentationEnabled(false);

                Assert.That(host.RetainedPresentation, Is.SameAs(retained));
                Assert.That(host.HasRetainedPresentation, Is.True);
                Assert.That(host.IsPresentationEnabled, Is.False);
                Assert.That(retained.enabled, Is.False);

                host.SetPresentationEnabled(true);

                Assert.That(host.IsPresentationEnabled, Is.True);
                Assert.That(retained.enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Host_DisableFailsClosed()
        {
            GameObject root = new GameObject("stage1-presentation-disable-test");
            try
            {
                Stage1ProductionPresentationHostV1 host =
                    root.AddComponent<Stage1ProductionPresentationHostV1>();
                Stage1ProductionPresentationHostV1 retained =
                    root.AddComponent<Stage1ProductionPresentationHostV1>();
                host.ConfigureForTests(retained);
                retained.enabled = true;

                host.enabled = false;

                Assert.That(retained.enabled, Is.False);
                Assert.That(host.IsPresentationEnabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Host_RejectsConflictingRetainedPresentation()
        {
            GameObject root = new GameObject("stage1-presentation-conflict-test");
            try
            {
                Stage1ProductionPresentationHostV1 host =
                    root.AddComponent<Stage1ProductionPresentationHostV1>();
                Stage1ProductionPresentationHostV1 first =
                    root.AddComponent<Stage1ProductionPresentationHostV1>();
                Stage1ProductionPresentationHostV1 second =
                    root.AddComponent<Stage1ProductionPresentationHostV1>();
                host.ConfigureForTests(first);

                Assert.Throws<System.InvalidOperationException>(() =>
                    host.ConfigureForTests(second));
                Assert.That(host.RetainedPresentation, Is.SameAs(first));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
