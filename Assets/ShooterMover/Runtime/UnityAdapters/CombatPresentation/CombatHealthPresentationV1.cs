using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.GameplayEntities.Enemies;
using ShooterMover.UnityAdapters.Players;

namespace ShooterMover.UnityAdapters.CombatPresentation
{
    public enum CombatHealthPresentationStateV1
    {
        Alive = 1,
        Terminal = 2,
    }

    public sealed class CombatPresentationAnchorFactsV1 : IEquatable<CombatPresentationAnchorFactsV1>
    {
        public CombatPresentationAnchorFactsV1(
            StableId anchorStableId,
            double localOffsetX,
            double localOffsetY,
            double localOffsetZ)
        {
            AnchorStableId = anchorStableId;
            RequireFinite(localOffsetX, nameof(localOffsetX));
            RequireFinite(localOffsetY, nameof(localOffsetY));
            RequireFinite(localOffsetZ, nameof(localOffsetZ));
            LocalOffsetX = localOffsetX;
            LocalOffsetY = localOffsetY;
            LocalOffsetZ = localOffsetZ;
        }

        public StableId AnchorStableId { get; }
        public double LocalOffsetX { get; }
        public double LocalOffsetY { get; }
        public double LocalOffsetZ { get; }

        public bool Equals(CombatPresentationAnchorFactsV1 other)
        {
            return !ReferenceEquals(other, null)
                && AnchorStableId == other.AnchorStableId
                && LocalOffsetX == other.LocalOffsetX
                && LocalOffsetY == other.LocalOffsetY
                && LocalOffsetZ == other.LocalOffsetZ;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CombatPresentationAnchorFactsV1);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (AnchorStableId == null ? 0 : AnchorStableId.GetHashCode());
                hash = (hash * 31) + LocalOffsetX.GetHashCode();
                hash = (hash * 31) + LocalOffsetY.GetHashCode();
                return (hash * 31) + LocalOffsetZ.GetHashCode();
            }
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }

    /// <summary>
    /// Immutable presentation-only projection of authoritative health. It contains no
    /// mutation reference and derives its normalized fill exactly once at construction.
    /// </summary>
    public sealed class CombatHealthBarSnapshotV1 : IEquatable<CombatHealthBarSnapshotV1>
    {
        public CombatHealthBarSnapshotV1(
            StableId entityInstanceStableId,
            long lifecycleGeneration,
            double currentHealth,
            double maximumHealth,
            CombatHealthPresentationStateV1 state,
            CombatPresentationAnchorFactsV1 anchorFacts = null)
        {
            EntityInstanceStableId = entityInstanceStableId
                ?? throw new ArgumentNullException(nameof(entityInstanceStableId));
            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            RequireFinite(currentHealth, nameof(currentHealth));
            RequireFinitePositive(maximumHealth, nameof(maximumHealth));
            if (!Enum.IsDefined(typeof(CombatHealthPresentationStateV1), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            LifecycleGeneration = lifecycleGeneration;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            NormalizedFill = Clamp01(currentHealth / maximumHealth);
            State = state;
            AnchorFacts = anchorFacts;
        }

        public StableId EntityInstanceStableId { get; }
        public long LifecycleGeneration { get; }
        public double CurrentHealth { get; }
        public double MaximumHealth { get; }
        public double NormalizedFill { get; }
        public CombatHealthPresentationStateV1 State { get; }
        public CombatPresentationAnchorFactsV1 AnchorFacts { get; }
        public bool IsAlive { get { return State == CombatHealthPresentationStateV1.Alive; } }
        public bool IsTerminal { get { return State == CombatHealthPresentationStateV1.Terminal; } }

        public bool Equals(CombatHealthBarSnapshotV1 other)
        {
            return !ReferenceEquals(other, null)
                && EntityInstanceStableId == other.EntityInstanceStableId
                && LifecycleGeneration == other.LifecycleGeneration
                && CurrentHealth == other.CurrentHealth
                && MaximumHealth == other.MaximumHealth
                && NormalizedFill == other.NormalizedFill
                && State == other.State
                && object.Equals(AnchorFacts, other.AnchorFacts);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CombatHealthBarSnapshotV1);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + EntityInstanceStableId.GetHashCode();
                hash = (hash * 31) + LifecycleGeneration.GetHashCode();
                hash = (hash * 31) + CurrentHealth.GetHashCode();
                hash = (hash * 31) + MaximumHealth.GetHashCode();
                hash = (hash * 31) + State.GetHashCode();
                return (hash * 31) + (AnchorFacts == null ? 0 : AnchorFacts.GetHashCode());
            }
        }

        private static double Clamp01(double value)
        {
            if (value < 0d) return 0d;
            if (value > 1d) return 1d;
            return value;
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private static void RequireFinitePositive(double value, string name)
        {
            RequireFinite(value, name);
            if (value <= 0d)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }

    public interface ICombatHealthBarSnapshotSourceV1
    {
        bool TryRead(out CombatHealthBarSnapshotV1 snapshot);
    }

    public delegate bool TryReadEnemyActorStateV1(out EnemyActorState state);

    public sealed class PlayerHudCombatHealthSnapshotSourceV1 : ICombatHealthBarSnapshotSourceV1
    {
        private readonly Func<PlayerHudHealthSnapshot> read;
        private readonly CombatPresentationAnchorFactsV1 anchorFacts;

        public PlayerHudCombatHealthSnapshotSourceV1(
            Func<PlayerHudHealthSnapshot> read,
            CombatPresentationAnchorFactsV1 anchorFacts = null)
        {
            this.read = read ?? throw new ArgumentNullException(nameof(read));
            this.anchorFacts = anchorFacts;
        }

        public bool TryRead(out CombatHealthBarSnapshotV1 snapshot)
        {
            PlayerHudHealthSnapshot source = read();
            if (source == null)
            {
                snapshot = null;
                return false;
            }

            snapshot = new CombatHealthBarSnapshotV1(
                source.ActorInstanceId,
                source.LifecycleGeneration,
                source.CurrentHealth,
                source.MaximumHealth,
                source.IsDead
                    ? CombatHealthPresentationStateV1.Terminal
                    : CombatHealthPresentationStateV1.Alive,
                anchorFacts);
            return true;
        }
    }

    public sealed class EnemyActorCombatHealthSnapshotSourceV1 : ICombatHealthBarSnapshotSourceV1
    {
        private readonly StableId boundEntityStableId;
        private readonly Func<long> readLifecycleGeneration;
        private readonly TryReadEnemyActorStateV1 readState;
        private readonly CombatPresentationAnchorFactsV1 anchorFacts;

        public EnemyActorCombatHealthSnapshotSourceV1(
            StableId boundEntityStableId,
            Func<long> readLifecycleGeneration,
            TryReadEnemyActorStateV1 readState,
            CombatPresentationAnchorFactsV1 anchorFacts = null)
        {
            this.boundEntityStableId = boundEntityStableId
                ?? throw new ArgumentNullException(nameof(boundEntityStableId));
            this.readLifecycleGeneration = readLifecycleGeneration
                ?? throw new ArgumentNullException(nameof(readLifecycleGeneration));
            this.readState = readState ?? throw new ArgumentNullException(nameof(readState));
            this.anchorFacts = anchorFacts;
        }

        public bool TryRead(out CombatHealthBarSnapshotV1 snapshot)
        {
            EnemyActorState state;
            long lifecycleGeneration = readLifecycleGeneration();
            if (lifecycleGeneration < 0L
                || !readState(out state)
                || state == null
                || state.ActorId != boundEntityStableId)
            {
                snapshot = null;
                return false;
            }

            snapshot = new CombatHealthBarSnapshotV1(
                state.ActorId,
                lifecycleGeneration,
                state.Health,
                state.MaximumHealth,
                state.IsActive
                    ? CombatHealthPresentationStateV1.Alive
                    : CombatHealthPresentationStateV1.Terminal,
                anchorFacts);
            return true;
        }
    }

    public sealed class EnemyRuntimeCombatHealthSnapshotSourceV1 : ICombatHealthBarSnapshotSourceV1
    {
        private readonly Func<EnemyRuntimeProjection> read;
        private readonly CombatPresentationAnchorFactsV1 anchorFacts;

        public EnemyRuntimeCombatHealthSnapshotSourceV1(
            Func<EnemyRuntimeProjection> read,
            CombatPresentationAnchorFactsV1 anchorFacts = null)
        {
            this.read = read ?? throw new ArgumentNullException(nameof(read));
            this.anchorFacts = anchorFacts;
        }

        public bool TryRead(out CombatHealthBarSnapshotV1 snapshot)
        {
            EnemyRuntimeProjection source = read();
            if (source == null)
            {
                snapshot = null;
                return false;
            }

            snapshot = new CombatHealthBarSnapshotV1(
                source.Identity.EntityInstanceId,
                source.LifecycleGeneration,
                source.CurrentHealth,
                source.MaximumHealth,
                source.ActorState.IsActive
                    ? CombatHealthPresentationStateV1.Alive
                    : CombatHealthPresentationStateV1.Terminal,
                anchorFacts);
            return true;
        }
    }
}
