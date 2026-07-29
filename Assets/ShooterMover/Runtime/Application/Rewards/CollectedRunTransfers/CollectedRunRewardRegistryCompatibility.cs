using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    /// <summary>
    /// Source-name compatibility only. Implementations of the earlier retained-payload
    /// interface automatically satisfy the V2 crash-custody preparation contract.
    /// </summary>
    public interface ICollectedRunEquipmentPayloadSource :
        ICollectedRunWeaponPayloadSource
    {
    }

    /// <summary>
    /// Reference-only compatibility façade for existing composition call sites. Remove once
    /// all callers use the V2 registry name directly. It creates and owns no authority.
    /// </summary>
    public static class CollectedRunRewardTransferLiveRegistry
    {
        public static void BindRewardApplication(
            StableId characterStableId,
            RewardApplicationActions rewardApplication)
        {
            CollectedRunRewardLiveRegistry.BindRewardApplication(
                characterStableId,
                rewardApplication);
        }

        public static bool TryResolve(
            StableId characterStableId,
            out RewardApplicationActions rewardApplication,
            out CollectedRunRewardTransferReceiptState receipts)
        {
            CollectedRunRewardPreparedTransferStore prepared;
            return CollectedRunRewardLiveRegistry.TryResolve(
                characterStableId,
                out rewardApplication,
                out prepared,
                out receipts);
        }
    }
}
