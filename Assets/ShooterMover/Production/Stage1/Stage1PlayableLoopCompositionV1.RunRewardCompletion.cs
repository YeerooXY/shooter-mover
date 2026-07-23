using System;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    public sealed partial class Stage1PlayableLoopCompositionV1
    {
        private void HandleRewardMinimumFinalExitReached()
        {
            Stage1RunPickupBootstrap2D rewards =
                GetComponent<Stage1RunPickupBootstrap2D>();
            string rewardDiagnostic;
            if (rewards == null
                || !rewards.IsComposed
                || !rewards.TryGenerateRunMinimum(out rewardDiagnostic))
            {
                throw new InvalidOperationException(
                    "The run cannot complete before its guaranteed reward minimum is frozen: "
                    + (string.IsNullOrWhiteSpace(rewardDiagnostic)
                        ? "reward authority unavailable"
                        : rewardDiagnostic));
            }
        }
    }
}
