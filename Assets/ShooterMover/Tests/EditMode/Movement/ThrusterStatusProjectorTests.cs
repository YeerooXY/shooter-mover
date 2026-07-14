using System;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;
using ShooterMover.UnityAdapters.Physics;

namespace ShooterMover.Tests.EditMode.Movement
{
    public sealed class ThrusterStatusProjectorTests
    {
        private static readonly StableId TuningIdentity =
            StableId.Parse("movement-tuning.mt-011-tests");

        [Test]
        public void Ready_ReportsFullBankReadinessAndTuningIdentity()
        {
            FakeThrusterStatusSource source = CreateSource(
                isActive: true,
                isDisposed: false,
                availableCharges: 2,
                maximumCharges: 2,
                burstPhase: ThrusterBurstPhase.Ready,
                chainElapsedSeconds: 0.05d);

            ThrusterStatusSnapshot snapshot = ThrusterStatusProjector.Project(source);

            Assert.That(snapshot.State, Is.EqualTo(ThrusterStatusState.Ready));
            Assert.That(snapshot.TuningIdentity, Is.EqualTo(TuningIdentity));
            Assert.That(snapshot.RuntimeGeneration, Is.EqualTo(7L));
            Assert.That(snapshot.AvailableCharges, Is.EqualTo(2));
            Assert.That(snapshot.MaximumCharges, Is.EqualTo(2));
            Assert.That(snapshot.RegeneratingCharges, Is.Zero);
            Assert.That(snapshot.RechargeSeconds, Is.EqualTo(1.75d));
            Assert.That(snapshot.IsRuntimeAvailable, Is.True);
            Assert.That(snapshot.IsReady, Is.True);
            Assert.That(snapshot.HasReadyCharge, Is.True);
            Assert.That(snapshot.IsEmpty, Is.False);
            Assert.That(snapshot.IsRegenerating, Is.False);
            Assert.That(snapshot.IsBursting, Is.False);
            Assert.That(snapshot.IsChainWindowReady, Is.True);
        }

        [Test]
        public void Empty_ReportsNoReadyChargeAndEveryChargeRegenerating()
        {
            FakeThrusterStatusSource source = CreateSource(
                isActive: true,
                isDisposed: false,
                availableCharges: 0,
                maximumCharges: 2,
                burstPhase: ThrusterBurstPhase.Ready,
                chainElapsedSeconds: 0.05d);

            ThrusterStatusSnapshot snapshot = ThrusterStatusProjector.Project(source);

            Assert.That(snapshot.State, Is.EqualTo(ThrusterStatusState.Empty));
            Assert.That(snapshot.AvailableCharges, Is.Zero);
            Assert.That(snapshot.RegeneratingCharges, Is.EqualTo(2));
            Assert.That(snapshot.HasReadyCharge, Is.False);
            Assert.That(snapshot.IsEmpty, Is.True);
            Assert.That(snapshot.IsRegenerating, Is.True);
        }

        [Test]
        public void Regenerating_ReportsAvailableAndMissingChargeCounts()
        {
            FakeThrusterStatusSource source = CreateSource(
                isActive: true,
                isDisposed: false,
                availableCharges: 1,
                maximumCharges: 3,
                burstPhase: ThrusterBurstPhase.ExitMomentum,
                velocityX: 12d,
                burstDirectionX: 1d,
                exitElapsedSeconds: 0.08d,
                chainElapsedSeconds: 0.05d);

            ThrusterStatusSnapshot snapshot = ThrusterStatusProjector.Project(source);

            Assert.That(snapshot.State, Is.EqualTo(ThrusterStatusState.Regenerating));
            Assert.That(snapshot.AvailableCharges, Is.EqualTo(1));
            Assert.That(snapshot.MaximumCharges, Is.EqualTo(3));
            Assert.That(snapshot.RegeneratingCharges, Is.EqualTo(2));
            Assert.That(snapshot.IsRegenerating, Is.True);
            Assert.That(snapshot.BurstPhase, Is.EqualTo(ThrusterBurstPhase.ExitMomentum));
            Assert.That(snapshot.ExitElapsedSeconds, Is.EqualTo(0.08d));
        }

        [Test]
        public void Burst_ReportsDirectionSteeringTimingAndChargeState()
        {
            FakeThrusterStatusSource source = CreateSource(
                isActive: true,
                isDisposed: false,
                availableCharges: 1,
                maximumCharges: 2,
                burstPhase: ThrusterBurstPhase.Burst,
                velocityX: 30d,
                velocityY: 40d,
                burstDirectionX: 0.6d,
                burstDirectionY: 0.8d,
                steeringIntentX: -1d,
                burstElapsedSeconds: 0.12d,
                chainElapsedSeconds: 0.02d);

            ThrusterStatusSnapshot snapshot = ThrusterStatusProjector.Project(source);

            Assert.That(snapshot.State, Is.EqualTo(ThrusterStatusState.Burst));
            Assert.That(snapshot.IsBursting, Is.True);
            Assert.That(snapshot.IsRegenerating, Is.True);
            Assert.That(snapshot.BurstPhase, Is.EqualTo(ThrusterBurstPhase.Burst));
            Assert.That(snapshot.VelocityX, Is.EqualTo(30d));
            Assert.That(snapshot.VelocityY, Is.EqualTo(40d));
            Assert.That(snapshot.BurstDirectionX, Is.EqualTo(0.6d));
            Assert.That(snapshot.BurstDirectionY, Is.EqualTo(0.8d));
            Assert.That(snapshot.SteeringIntentX, Is.EqualTo(-1d));
            Assert.That(snapshot.SteeringIntentY, Is.Zero);
            Assert.That(snapshot.HasSteeringIntent, Is.True);
            Assert.That(snapshot.BurstElapsedSeconds, Is.EqualTo(0.12d));
            Assert.That(snapshot.IsChainWindowReady, Is.False);
        }

        [Test]
        public void Unavailable_ReportsExplicitStateWithoutLeakingStaleRuntimeValues()
        {
            FakeThrusterStatusSource source = CreateSource(
                isActive: false,
                isDisposed: false,
                availableCharges: 2,
                maximumCharges: 2,
                burstPhase: ThrusterBurstPhase.Burst,
                velocityX: 99d,
                burstDirectionX: 1d,
                steeringIntentY: 1d,
                burstElapsedSeconds: 0.2d,
                chainElapsedSeconds: 0.04d);

            ThrusterStatusSnapshot snapshot = ThrusterStatusProjector.Project(source);

            Assert.That(snapshot.State, Is.EqualTo(ThrusterStatusState.Unavailable));
            Assert.That(snapshot.IsRuntimeAvailable, Is.False);
            Assert.That(snapshot.IsDisposed, Is.False);
            Assert.That(snapshot.AvailableCharges, Is.Zero);
            Assert.That(snapshot.MaximumCharges, Is.EqualTo(2));
            Assert.That(snapshot.RegeneratingCharges, Is.Zero);
            Assert.That(snapshot.BurstPhase, Is.EqualTo(ThrusterBurstPhase.Ready));
            Assert.That(snapshot.VelocityX, Is.Zero);
            Assert.That(snapshot.BurstDirectionX, Is.Zero);
            Assert.That(snapshot.SteeringIntentY, Is.Zero);
        }

        [Test]
        public void Disposed_ReportsTerminalStateWithoutLeakingStaleRuntimeValues()
        {
            FakeThrusterStatusSource source = CreateSource(
                isActive: false,
                isDisposed: true,
                availableCharges: 2,
                maximumCharges: 2,
                burstPhase: ThrusterBurstPhase.Burst,
                velocityY: 50d,
                burstDirectionY: 1d,
                steeringIntentX: 1d,
                burstElapsedSeconds: 0.1d);

            ThrusterStatusSnapshot snapshot = ThrusterStatusProjector.Project(source);

            Assert.That(snapshot.State, Is.EqualTo(ThrusterStatusState.Disposed));
            Assert.That(snapshot.IsDisposed, Is.True);
            Assert.That(snapshot.IsRuntimeAvailable, Is.False);
            Assert.That(snapshot.HasReadyCharge, Is.False);
            Assert.That(snapshot.IsBursting, Is.False);
            Assert.That(snapshot.AvailableCharges, Is.Zero);
            Assert.That(snapshot.VelocityY, Is.Zero);
        }

        [Test]
        public void Projection_IsRepeatableReadOnlyAndCannotMutateSourceState()
        {
            FakeThrusterStatusSource source = CreateSource(
                isActive: true,
                isDisposed: false,
                availableCharges: 1,
                maximumCharges: 2,
                burstPhase: ThrusterBurstPhase.Burst,
                velocityX: 30d,
                velocityY: 40d,
                burstDirectionX: 0.6d,
                burstDirectionY: 0.8d,
                steeringIntentX: 1d,
                burstElapsedSeconds: 0.1d,
                chainElapsedSeconds: 0.05d);

            SourceValues before = SourceValues.Capture(source);
            ThrusterStatusSnapshot first = ThrusterStatusProjector.Project(source);
            ThrusterStatusSnapshot second = ThrusterStatusProjector.Project(source);
            SourceValues after = SourceValues.Capture(source);

            Assert.That(after, Is.EqualTo(before));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(second, Is.Not.SameAs(first));

            foreach (System.Reflection.PropertyInfo property
                in typeof(ThrusterStatusSnapshot).GetProperties())
            {
                Assert.That(
                    property.CanWrite,
                    Is.False,
                    "Snapshot property exposes a setter: " + property.Name);
            }

            foreach (System.Reflection.MethodInfo method
                in typeof(IThrusterStatusSource).GetMethods())
            {
                Assert.That(
                    method.IsSpecialName
                    && method.Name.StartsWith("get_", StringComparison.Ordinal),
                    Is.True,
                    "Read source exposes a non-getter member: " + method.Name);
            }
        }

        private static FakeThrusterStatusSource CreateSource(
            bool isActive,
            bool isDisposed,
            int availableCharges,
            int maximumCharges,
            ThrusterBurstPhase burstPhase,
            double velocityX = 0d,
            double velocityY = 0d,
            double burstDirectionX = 0d,
            double burstDirectionY = 0d,
            double steeringIntentX = 0d,
            double steeringIntentY = 0d,
            double burstElapsedSeconds = 0d,
            double exitElapsedSeconds = 0d,
            double chainElapsedSeconds = 0d)
        {
            return new FakeThrusterStatusSource(
                isActive,
                isDisposed,
                7L,
                TuningIdentity,
                availableCharges,
                maximumCharges,
                1.75d,
                burstPhase,
                velocityX,
                velocityY,
                burstDirectionX,
                burstDirectionY,
                steeringIntentX,
                steeringIntentY,
                burstElapsedSeconds,
                exitElapsedSeconds,
                chainElapsedSeconds,
                0.05d);
        }

        private sealed class FakeThrusterStatusSource : IThrusterStatusSource
        {
            public FakeThrusterStatusSource(
                bool isActive,
                bool isDisposed,
                long generation,
                StableId tuningIdentity,
                int availableCharges,
                int maximumCharges,
                double rechargeSeconds,
                ThrusterBurstPhase burstPhase,
                double velocityX,
                double velocityY,
                double burstDirectionX,
                double burstDirectionY,
                double steeringIntentX,
                double steeringIntentY,
                double burstElapsedSeconds,
                double exitElapsedSeconds,
                double chainElapsedSeconds,
                double minimumChainIntervalSeconds)
            {
                IsActive = isActive;
                IsDisposed = isDisposed;
                Generation = generation;
                TuningIdentity = tuningIdentity;
                AvailableCharges = availableCharges;
                MaximumCharges = maximumCharges;
                RechargeSeconds = rechargeSeconds;
                BurstPhase = burstPhase;
                VelocityX = velocityX;
                VelocityY = velocityY;
                BurstDirectionX = burstDirectionX;
                BurstDirectionY = burstDirectionY;
                SteeringIntentX = steeringIntentX;
                SteeringIntentY = steeringIntentY;
                BurstElapsedSeconds = burstElapsedSeconds;
                ExitElapsedSeconds = exitElapsedSeconds;
                ChainElapsedSeconds = chainElapsedSeconds;
                MinimumChainIntervalSeconds = minimumChainIntervalSeconds;
            }

            public bool IsActive { get; }

            public bool IsDisposed { get; }

            public long Generation { get; }

            public StableId TuningIdentity { get; }

            public int AvailableCharges { get; }

            public int MaximumCharges { get; }

            public double RechargeSeconds { get; }

            public ThrusterBurstPhase BurstPhase { get; }

            public double VelocityX { get; }

            public double VelocityY { get; }

            public double BurstDirectionX { get; }

            public double BurstDirectionY { get; }

            public double SteeringIntentX { get; }

            public double SteeringIntentY { get; }

            public double BurstElapsedSeconds { get; }

            public double ExitElapsedSeconds { get; }

            public double ChainElapsedSeconds { get; }

            public double MinimumChainIntervalSeconds { get; }
        }

        private struct SourceValues : IEquatable<SourceValues>
        {
            private readonly bool isActive;
            private readonly bool isDisposed;
            private readonly long generation;
            private readonly StableId tuningIdentity;
            private readonly int availableCharges;
            private readonly int maximumCharges;
            private readonly double rechargeSeconds;
            private readonly ThrusterBurstPhase burstPhase;
            private readonly double velocityX;
            private readonly double velocityY;
            private readonly double burstDirectionX;
            private readonly double burstDirectionY;
            private readonly double steeringIntentX;
            private readonly double steeringIntentY;
            private readonly double burstElapsedSeconds;
            private readonly double exitElapsedSeconds;
            private readonly double chainElapsedSeconds;
            private readonly double minimumChainIntervalSeconds;

            private SourceValues(FakeThrusterStatusSource source)
            {
                isActive = source.IsActive;
                isDisposed = source.IsDisposed;
                generation = source.Generation;
                tuningIdentity = source.TuningIdentity;
                availableCharges = source.AvailableCharges;
                maximumCharges = source.MaximumCharges;
                rechargeSeconds = source.RechargeSeconds;
                burstPhase = source.BurstPhase;
                velocityX = source.VelocityX;
                velocityY = source.VelocityY;
                burstDirectionX = source.BurstDirectionX;
                burstDirectionY = source.BurstDirectionY;
                steeringIntentX = source.SteeringIntentX;
                steeringIntentY = source.SteeringIntentY;
                burstElapsedSeconds = source.BurstElapsedSeconds;
                exitElapsedSeconds = source.ExitElapsedSeconds;
                chainElapsedSeconds = source.ChainElapsedSeconds;
                minimumChainIntervalSeconds = source.MinimumChainIntervalSeconds;
            }

            public static SourceValues Capture(FakeThrusterStatusSource source)
            {
                return new SourceValues(source);
            }

            public bool Equals(SourceValues other)
            {
                return isActive == other.isActive
                    && isDisposed == other.isDisposed
                    && generation == other.generation
                    && object.Equals(tuningIdentity, other.tuningIdentity)
                    && availableCharges == other.availableCharges
                    && maximumCharges == other.maximumCharges
                    && rechargeSeconds.Equals(other.rechargeSeconds)
                    && burstPhase == other.burstPhase
                    && velocityX.Equals(other.velocityX)
                    && velocityY.Equals(other.velocityY)
                    && burstDirectionX.Equals(other.burstDirectionX)
                    && burstDirectionY.Equals(other.burstDirectionY)
                    && steeringIntentX.Equals(other.steeringIntentX)
                    && steeringIntentY.Equals(other.steeringIntentY)
                    && burstElapsedSeconds.Equals(other.burstElapsedSeconds)
                    && exitElapsedSeconds.Equals(other.exitElapsedSeconds)
                    && chainElapsedSeconds.Equals(other.chainElapsedSeconds)
                    && minimumChainIntervalSeconds.Equals(other.minimumChainIntervalSeconds);
            }

            public override bool Equals(object obj)
            {
                return obj is SourceValues && Equals((SourceValues)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 31) + isActive.GetHashCode();
                    hash = (hash * 31) + isDisposed.GetHashCode();
                    hash = (hash * 31) + generation.GetHashCode();
                    hash = (hash * 31) + tuningIdentity.GetHashCode();
                    hash = (hash * 31) + availableCharges;
                    hash = (hash * 31) + maximumCharges;
                    hash = (hash * 31) + rechargeSeconds.GetHashCode();
                    hash = (hash * 31) + burstPhase.GetHashCode();
                    hash = (hash * 31) + velocityX.GetHashCode();
                    hash = (hash * 31) + velocityY.GetHashCode();
                    hash = (hash * 31) + burstDirectionX.GetHashCode();
                    hash = (hash * 31) + burstDirectionY.GetHashCode();
                    hash = (hash * 31) + steeringIntentX.GetHashCode();
                    hash = (hash * 31) + steeringIntentY.GetHashCode();
                    hash = (hash * 31) + burstElapsedSeconds.GetHashCode();
                    hash = (hash * 31) + exitElapsedSeconds.GetHashCode();
                    hash = (hash * 31) + chainElapsedSeconds.GetHashCode();
                    hash = (hash * 31) + minimumChainIntervalSeconds.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
