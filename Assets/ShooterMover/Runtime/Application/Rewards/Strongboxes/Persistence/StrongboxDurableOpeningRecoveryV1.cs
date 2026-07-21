using System;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public sealed partial class StrongboxDurableOpeningCoordinatorV1
    {
        private static bool TryRehydrateRewardApplication(
            IStrongboxOpeningRecoveryPortV1 recoveryPort,
            StrongboxOpenCommandV1 command,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (recoveryPort == null)
            {
                rejectionCode = "durable-opening-recovery-port-unavailable";
                return false;
            }
            try
            {
                StrongboxOpeningRecoveryResultV1 recovered =
                    recoveryPort.Recover(command);
                if (recovered == null || !recovered.Succeeded)
                {
                    rejectionCode = recovered == null
                        ? "durable-opening-recovery-result-null"
                        : "durable-opening-recovery-rejected:"
                            + recovered.RejectionCode;
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                rejectionCode =
                    "durable-opening-recovery-exception-"
                    + exception.GetType().Name.ToLowerInvariant();
                return false;
            }
        }
    }
}
