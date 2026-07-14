using System;
using NUnit.Framework;
using ShooterMover.Bootstrap;

namespace ShooterMover.Tests.EditMode.Foundation
{
    public sealed class BootstrapCompositionRootTests
    {
        [Test]
        public void StartStopRestartDispose_TransitionsDeterministically()
        {
            var root = new BootstrapCompositionRoot();

            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapCompositionRoot.LifecyclePhase.Created));
            Assert.That(root.IsRunning, Is.False);
            Assert.That(root.RegisteredServiceCount, Is.Zero);

            root.Start();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapCompositionRoot.LifecyclePhase.Running));
            Assert.That(root.IsRunning, Is.True);
            Assert.That(root.RegisteredServiceCount, Is.Zero);

            root.Start();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapCompositionRoot.LifecyclePhase.Running),
                "Repeated Start must be idempotent while the root is running.");

            root.Stop();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapCompositionRoot.LifecyclePhase.Stopped));
            Assert.That(root.IsRunning, Is.False);
            Assert.That(root.RegisteredServiceCount, Is.Zero);

            root.Stop();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapCompositionRoot.LifecyclePhase.Stopped),
                "Repeated Stop must be idempotent after shutdown.");

            root.Start();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapCompositionRoot.LifecyclePhase.Running));
            Assert.That(root.IsRunning, Is.True);
            Assert.That(root.RegisteredServiceCount, Is.Zero);

            root.Dispose();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapCompositionRoot.LifecyclePhase.Disposed));
            Assert.That(root.IsRunning, Is.False);
            Assert.That(root.RegisteredServiceCount, Is.Zero);

            root.Dispose();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapCompositionRoot.LifecyclePhase.Disposed),
                "Repeated Dispose must remain terminal and idempotent.");
        }

        [Test]
        public void DisposeBeforeStart_IsTerminalAndIdempotent()
        {
            var root = new BootstrapCompositionRoot();

            root.Dispose();
            root.Dispose();

            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapCompositionRoot.LifecyclePhase.Disposed));
            Assert.That(root.IsRunning, Is.False);
            Assert.That(root.RegisteredServiceCount, Is.Zero);
        }

        [Test]
        public void StartAfterDispose_ReportsDisposedRoot()
        {
            var root = new BootstrapCompositionRoot();
            root.Start();
            root.Dispose();

            ObjectDisposedException exception = Assert.Throws<ObjectDisposedException>(
                () => root.Start());

            Assert.That(exception.ObjectName, Is.EqualTo(nameof(BootstrapCompositionRoot)));
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapCompositionRoot.LifecyclePhase.Disposed));
        }

        [Test]
        public void StopBeforeStart_ProducesReusableStoppedRoot()
        {
            var root = new BootstrapCompositionRoot();

            root.Stop();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapCompositionRoot.LifecyclePhase.Stopped));

            root.Start();
            Assert.That(root.IsRunning, Is.True);
            Assert.That(root.RegisteredServiceCount, Is.Zero);

            root.Dispose();
        }
    }
}
