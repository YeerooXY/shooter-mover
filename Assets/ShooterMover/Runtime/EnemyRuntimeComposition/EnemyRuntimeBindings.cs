using System;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;

namespace ShooterMover.EnemyRuntimeComposition
{
    /// <summary>
    /// Immutable placement and lifecycle input retained by the live enemy state while the
    /// compact enemy-definition replacement is introduced. This is data, not a factory.
    /// </summary>
    public sealed class EnemyPlacementLiveRequest
    {
        public EnemyPlacementLiveRequest(
            RoomEnemyPlacementContent placement,
            StableId runStableId,
            StableId roomRuntimeInstanceStableId,
            StableId itemInstanceStableId,
            long roomLifecycleGeneration,
            long lifecycleGeneration,
            EnemyDifficultyContext difficulty)
        {
            Placement = placement ?? throw new ArgumentNullException(nameof(placement));
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            RoomRuntimeInstanceStableId = roomRuntimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(roomRuntimeInstanceStableId));
            if (roomLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(roomLifecycleGeneration));
            if (lifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));

            ItemInstanceStableId = itemInstanceStableId;
            RoomLifecycleGeneration = roomLifecycleGeneration;
            LifecycleGeneration = lifecycleGeneration;
            Difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
        }

        public RoomEnemyPlacementContent Placement { get; }
        public StableId RunStableId { get; }
        public StableId RoomRuntimeInstanceStableId { get; }
        public StableId ItemInstanceStableId { get; }
        public long RoomLifecycleGeneration { get; }
        public long LifecycleGeneration { get; }
        public EnemyDifficultyContext Difficulty { get; }
    }

    /// <summary>
    /// Compatibility binding consumed by the retained live state. New compact enemies must bind
    /// canonical shared guns instead of constructing this legacy attack descriptor shape.
    /// </summary>
    public sealed class EnemyLiveAttackBinding
    {
        public EnemyLiveAttackBinding(
            EnemyAttackCapabilityDescriptor descriptor,
            EnemyTargetingAimPolicyRegistration targetingAim,
            EnemyAttackCapabilityLiveRegistration capability)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            TargetingAim = targetingAim ?? throw new ArgumentNullException(nameof(targetingAim));
            Capability = capability ?? throw new ArgumentNullException(nameof(capability));
        }

        public EnemyAttackCapabilityDescriptor Descriptor { get; }
        public EnemyTargetingAimPolicyRegistration TargetingAim { get; }
        public EnemyAttackCapabilityLiveRegistration Capability { get; }
    }
}
