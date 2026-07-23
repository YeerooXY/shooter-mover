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
        ICollectedRunEquipmentPayloadSourceV2
    {
    }

    /// <summary>
    /// Reference-only compatibility façade for existing composition call sites. Remove once
    /// all callers use the V2 registry name directly. It creates and owns no authority.
    /// </summary>
    public static class ProductionCollectedRunRewardTransferRuntimeRegistry
    {
        public static void BindRewardApplication(
            StableId characterStableId,
            RewardApplicationServiceV1 rewardApplication)
        {
            ProductionCollectedRunRewardRuntimeRegistryV2.BindRewardApplication(
                characterStableId,
                rewardApplication);
        }

        public static bool TryResolve(
            StableId characterStableId,
            out RewardApplicationServiceV1 rewardApplication,
            out CollectedRunRewardTransferReceiptAuthorityV1 receipts)
        {
            CollectedRunRewardPreparedTransferAuthorityV1 prepared;
            return ProductionCollectedRunRewardRuntimeRegistryV2.TryResolve(
                characterStableId,
                out rewardApplication,
                out prepared,
                out receipts);
        }
    }
}
