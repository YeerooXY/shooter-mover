using System;
using System.Collections.Generic;

namespace ShooterMover.Bootstrap
{
    /// <summary>
    /// Explicit process-local composition root for Shooter Mover runtime services.
    /// Unity scene integration is supplied by the UF-007 adapter; this type does
    /// not discover scene objects or expose a global service locator.
    /// </summary>
    public sealed class BootstrapCompositionRoot : IDisposable
    {
        public enum LifecyclePhase
        {
            Created,
            Registering,
            Starting,
            Running,
            Stopping,
            Disposing,
            Stopped,
            Disposed
        }

        private readonly List<LifecycleEntry> entries = new List<LifecycleEntry>();
        private int startedEntryCount;

        public LifecyclePhase Phase { get; private set; } = LifecyclePhase.Created;

        public bool IsRunning
        {
            get { return Phase == LifecyclePhase.Running; }
        }

        public int RegisteredServiceCount
        {
            get { return entries.Count; }
        }

        /// <summary>
        /// Registers and starts services in a fixed source-defined order.
        /// Repeated calls while already running are intentionally idempotent.
        /// </summary>
        public void Start()
        {
            ThrowIfDisposed();

            if (Phase == LifecyclePhase.Running)
            {
                return;
            }

            if (Phase != LifecyclePhase.Created && Phase != LifecyclePhase.Stopped)
            {
                throw new InvalidOperationException(
                    "Bootstrap cannot start while lifecycle phase is " + Phase + ".");
            }

            entries.Clear();
            startedEntryCount = 0;
            Phase = LifecyclePhase.Registering;

            try
            {
                RegisterServices();

                Phase = LifecyclePhase.Starting;
                for (int index = 0; index < entries.Count; index++)
                {
                    entries[index].Start();
                    startedEntryCount++;
                }

                Phase = LifecyclePhase.Running;
            }
            catch (Exception startupException)
            {
                Exception cleanupException = TryStopAndDisposeRegisteredServices();
                if (cleanupException != null)
                {
                    throw new AggregateException(
                        "Bootstrap startup and rollback both failed.",
                        startupException,
                        cleanupException);
                }

                throw;
            }
        }

        /// <summary>
        /// Stops started services and disposes registered services in reverse order.
        /// Repeated calls before startup or after shutdown are intentionally idempotent.
        /// </summary>
        public void Stop()
        {
            if (Phase == LifecyclePhase.Disposed)
            {
                return;
            }

            if (Phase == LifecyclePhase.Created || Phase == LifecyclePhase.Stopped)
            {
                Phase = LifecyclePhase.Stopped;
                return;
            }

            if (Phase != LifecyclePhase.Running)
            {
                throw new InvalidOperationException(
                    "Bootstrap cannot stop while lifecycle phase is " + Phase + ".");
            }

            Exception cleanupException = TryStopAndDisposeRegisteredServices();
            if (cleanupException != null)
            {
                throw cleanupException;
            }
        }

        public void Dispose()
        {
            if (Phase == LifecyclePhase.Disposed)
            {
                return;
            }

            if (Phase == LifecyclePhase.Registering
                || Phase == LifecyclePhase.Starting
                || Phase == LifecyclePhase.Stopping
                || Phase == LifecyclePhase.Disposing)
            {
                throw new InvalidOperationException(
                    "Bootstrap cannot dispose while lifecycle phase is " + Phase + ".");
            }

            try
            {
                Stop();
            }
            finally
            {
                Phase = LifecyclePhase.Disposed;
                GC.SuppressFinalize(this);
            }
        }

        private void RegisterServices()
        {
            var productionSession = new ProductionSessionAuthorityOwnerV1();
            Register(
                "Production session authorities",
                productionSession.Start,
                productionSession.Stop,
                productionSession.Dispose);
        }

        private void Register(
            string serviceName,
            Action start,
            Action stop,
            Action dispose)
        {
            if (Phase != LifecyclePhase.Registering)
            {
                throw new InvalidOperationException(
                    "Services may only be registered during the registration phase.");
            }

            entries.Add(new LifecycleEntry(serviceName, start, stop, dispose));
        }

        private Exception TryStopAndDisposeRegisteredServices()
        {
            List<Exception> failures = null;

            Phase = LifecyclePhase.Stopping;
            for (int index = startedEntryCount - 1; index >= 0; index--)
            {
                TryInvoke(entries[index].Stop, ref failures);
            }

            startedEntryCount = 0;
            Phase = LifecyclePhase.Disposing;

            for (int index = entries.Count - 1; index >= 0; index--)
            {
                TryInvoke(entries[index].Dispose, ref failures);
            }

            entries.Clear();
            Phase = LifecyclePhase.Stopped;

            if (failures == null)
            {
                return null;
            }

            return new AggregateException("One or more bootstrap shutdown actions failed.", failures);
        }

        private static void TryInvoke(Action action, ref List<Exception> failures)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                if (failures == null)
                {
                    failures = new List<Exception>();
                }

                failures.Add(exception);
            }
        }

        private void ThrowIfDisposed()
        {
            if (Phase == LifecyclePhase.Disposed)
            {
                throw new ObjectDisposedException(nameof(BootstrapCompositionRoot));
            }
        }

        private sealed class LifecycleEntry
        {
            private static readonly Action NoOp = delegate { };

            public LifecycleEntry(
                string serviceName,
                Action start,
                Action stop,
                Action dispose)
            {
                if (string.IsNullOrWhiteSpace(serviceName))
                {
                    throw new ArgumentException("A service name is required.", nameof(serviceName));
                }

                ServiceName = serviceName;
                Start = start ?? NoOp;
                Stop = stop ?? NoOp;
                Dispose = dispose ?? NoOp;
            }

            public string ServiceName { get; private set; }

            public Action Start { get; private set; }

            public Action Stop { get; private set; }

            public Action Dispose { get; private set; }
        }
    }
}
