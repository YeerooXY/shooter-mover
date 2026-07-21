using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public sealed partial class StrongboxDurableOpeningCoordinatorV1
    {
        private static bool TryRehydrateRewardApplication(
            StrongboxOpeningServiceV1 openingService,
            StrongboxOpenCommandV1 command,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            StrongboxOpeningRecordSnapshotV1 record = openingService
                .ExportSnapshot()
                .Openings
                .FirstOrDefault(item => item.Command.OpeningStableId
                    == command.OpeningStableId);
            if (record == null
                || record.Stage == StrongboxOpeningStageV1.Prepared
                || record.Stage == StrongboxOpeningStageV1.Opened
                || record.Stage == StrongboxOpeningStageV1.GeneratorRejected
                || record.Stage == StrongboxOpeningStageV1.PayloadRejected)
            {
                return true;
            }
            if (record.CommitCommand == null)
            {
                rejectionCode = "durable-opening-recovery-commit-missing";
                return false;
            }

            try
            {
                const BindingFlags flags = BindingFlags.Instance
                    | BindingFlags.NonPublic;
                FieldInfo field = typeof(StrongboxOpeningServiceV1)
                    .GetField("rewardApplication", flags);
                var rewardApplication = field == null
                    ? null
                    : field.GetValue(openingService)
                        as RewardApplicationServiceV1;
                if (rewardApplication == null)
                {
                    rejectionCode =
                        "durable-opening-recovery-authority-unavailable";
                    return false;
                }

                RewardApplicationResultV1 committed =
                    rewardApplication.Commit(record.CommitCommand);
                if (committed == null
                    || (committed.Status
                            != RewardApplicationResultStatusV1.Generated
                        && committed.Status
                            != RewardApplicationResultStatusV1
                                .ExactDuplicateNoChange))
                {
                    rejectionCode =
                        "durable-opening-recovery-commit-rejected:"
                        + (committed == null
                            ? "null"
                            : committed.RejectionCode);
                    return false;
                }

                if (record.Stage == StrongboxOpeningStageV1.RewardCommitted)
                {
                    return true;
                }
                if (record.ClaimCommand == null)
                {
                    rejectionCode =
                        "durable-opening-recovery-claim-missing";
                    return false;
                }
                RewardApplicationResultV1 claimed =
                    rewardApplication.Claim(record.ClaimCommand);
                bool accepted = claimed != null
                    && (claimed.Status
                            == RewardApplicationResultStatusV1.Applied
                        || claimed.Status
                            == RewardApplicationResultStatusV1
                                .AlreadyAppliedNoChange
                        || claimed.Status
                            == RewardApplicationResultStatusV1
                                .ClaimedPendingApplication
                        || claimed.Status
                            == RewardApplicationResultStatusV1
                                .ExactDuplicateNoChange);
                if (!accepted)
                {
                    rejectionCode =
                        "durable-opening-recovery-claim-rejected:"
                        + (claimed == null ? "null" : claimed.RejectionCode);
                }
                return accepted;
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
