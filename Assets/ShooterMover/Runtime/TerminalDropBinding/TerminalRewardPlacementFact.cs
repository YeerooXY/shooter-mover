using ShooterMover.Domain.Common;

namespace ShooterMover.LootDropBinding
{
    /// <summary>
    /// Optional explicit placement surface for terminal wrappers that carry room
    /// identity not present in their underlying engine-neutral terminal fact.
    /// </summary>
    public interface ITerminalRewardPlacementFact
    {
        StableId RewardTerminalEventStableId { get; }
        StableId RewardRoomStableId { get; }
        int RewardRoomLifecycleGeneration { get; }
        StableId RewardPlacementStableId { get; }
        string RewardPlacementFingerprint { get; }
    }
}
