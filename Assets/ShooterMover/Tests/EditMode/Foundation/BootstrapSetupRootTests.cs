using System;
using NUnit.Framework;
using ShooterMover.Bootstrap;

namespace ShooterMover.Tests.EditMode.Foundation
{
    public sealed class BootstrapSetupRootTests
    {
        [Test]
        public void StartStopRestartDispose_TransitionsDeterministically()
        {
            var root = new BootstrapSetupRoot();

            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapSetupRoot.LifecyclePhase.Created));
            Assert.That(root.IsRunning, Is.False);
            Assert.That(root.RegisteredServiceCount, Is.Zero);

            root.Start();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapSetupRoot.LifecyclePhase.Running));
            Assert.That(root.IsRunning, Is.True);
            Assert.That(root.RegisteredServiceCount, Is.Zero);

            root.Start();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapSetupRoot.LifecyclePhase.Running),
                "Repeated Start must be idempotent while the root is running.");

            root.Stop();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapSetupRoot.LifecyclePhase.Stopped));
            Assert.That(root.IsRunning, Is.False);
            Assert.That(root.RegisteredServiceCount, Is.Zero);

            root.Stop();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapSetupRoot.LifecyclePhase.Stopped),
                "Repeated Stop must be idempotent after shutdown.");

            root.Start();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapSetupRoot.LifecyclePhase.Running));
            Assert.That(root.IsRunning, Is.True);
            Assert.That(root.RegisteredServiceCount, Is.Zero);

            root.Dispose();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapSetupRoot.LifecyclePhase.Disposed));
            Assert.That(root.IsRunning, Is.False);
            Assert.That(root.RegisteredServiceCount, Is.Zero);

            root.Dispose();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapSetupRoot.LifecyclePhase.Disposed),
                "Repeated Dispose must remain terminal and idempotent.");
        }

        [Test]
        public void DisposeBeforeStart_IsTerminalAndIdempotent()
        {
            var root = new BootstrapSetupRoot();

            root.Dispose();
            root.Dispose();

            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapSetupRoot.LifecyclePhase.Disposed));
            Assert.That(root.IsRunning, Is.False);
            Assert.That(root.RegisteredServiceCount, Is.Zero);
        }

        [Test]
        public void StartAfterDispose_ReportsDisposedRoot()
        {
            var root = new BootstrapSetupRoot();
            root.Start();
            root.Dispose();

            ObjectDisposedException exception = Assert.Throws<ObjectDisposedException>(
                () => root.Start());

            Assert.That(exception.ObjectName, Is.EqualTo(nameof(BootstrapSetupRoot)));
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapSetupRoot.LifecyclePhase.Disposed));
        }

        [Test]
        public void StopBeforeStart_ProducesReusableStoppedRoot()
        {
            var root = new BootstrapSetupRoot();

            root.Stop();
            Assert.That(
                root.Phase,
                Is.EqualTo(BootstrapSetupRoot.LifecyclePhase.Stopped));

            root.Start();
            Assert.That(root.IsRunning, Is.True);
            Assert.That(root.RegisteredServiceCount, Is.Zero);

            root.Dispose();
        }
    }
}
