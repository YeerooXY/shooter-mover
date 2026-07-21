using System;
using System.Linq;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public enum StrongboxOpeningRecoveryStatusV1
    {
        NotRequired = 1,
        Rehydrated = 2,
        Rejected = 3,
    }

    public sealed class StrongboxOpeningRecoveryResultV1
    {
        public StrongboxOpeningRecoveryResultV1(
            StrongboxOpeningRecoveryStatusV1 status,
            string rejectionCode)
        {
            if (!Enum.IsDefined(
                typeof(StrongboxOpeningRecoveryStatusV1),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public StrongboxOpeningRecoveryStatusV1 Status { get; }
        public string RejectionCode { get; }
        public bool Succeeded
        {
            get
            {
                return Status == StrongboxOpeningRecoveryStatusV1.NotRequired
                    || Status == StrongboxOpeningRecoveryStatusV1.Rehydrated;
            }
        }
    }

    /// <summary>
    /// Typed recovery seam for the existing BOX and RAP authorities. It replays only the
    /// immutable commit/claim commands already frozen in the BOX snapshot. It performs no
    /// generation and owns no replacement reward state.
    /// </summary>
    public interface IStrongboxOpeningRecoveryPortV1
    {
        StrongboxOpeningRecoveryResultV1 Recover(
            StrongboxOpenCommandV1 command);
    }

    public sealed class ExistingStrongboxOpeningRecoveryPortV1 :
        IStrongboxOpeningRecoveryPortV1
    {
        private readonly StrongboxOpeningServiceV1 openingService;
        private readonly RewardApplicationServiceV1 rewardApplication;

        public ExistingStrongboxOpeningRecoveryPortV1(
            StrongboxOpeningServiceV1 openingService,
            RewardApplicationServiceV1 rewardApplication)
        {
            this.openingService = openingService
                ?? throw new ArgumentNullException(nameof(openingService));
            this.rewardApplication = rewardApplication
                ?? throw new ArgumentNullException(nameof(rewardApplication));
        }

        public StrongboxOpeningRecoveryResultV1 Recover(
            StrongboxOpenCommandV1 command)
        {
            if (command == null)
            {
                return Rejected("opening-recovery-command-null");
            }

            StrongboxOpeningSnapshotV1 snapshot =
                openingService.ExportSnapshot();
            StrongboxOpeningRecordSnapshotV1 record = snapshot.Openings
                .FirstOrDefault(item => item.Command.OpeningStableId
                    == command.OpeningStableId);
            if (record == null)
            {
                return NotRequired();
            }
            if (!record.Command.Equals(command))
            {
                return Rejected("opening-recovery-command-conflict");
            }
            if (record.Stage == StrongboxOpeningStageV1.Prepared
                || record.Stage == StrongboxOpeningStageV1.Opened
                || record.Stage == StrongboxOpeningStageV1.GeneratorRejected
                || record.Stage == StrongboxOpeningStageV1.PayloadRejected)
            {
                return NotRequired();
            }
            if (record.CommitCommand == null)
            {
                return Rejected("opening-recovery-commit-missing");
            }

            RewardApplicationResultV1 committed =
                rewardApplication.Commit(record.CommitCommand);
            if (!CommitAccepted(committed))
            {
                return Rejected(
                    "opening-recovery-commit-rejected:"
                        + ResultCode(committed));
            }
            if (record.Stage == StrongboxOpeningStageV1.RewardCommitted)
            {
                return Rehydrated();
            }
            if (record.ClaimCommand == null)
            {
                return Rejected("opening-recovery-claim-missing");
            }

            RewardApplicationResultV1 claimed =
                rewardApplication.Claim(record.ClaimCommand);
            return ClaimAccepted(claimed)
                ? Rehydrated()
                : Rejected(
                    "opening-recovery-claim-rejected:"
                        + ResultCode(claimed));
        }

        private static bool CommitAccepted(
            RewardApplicationResultV1 result)
        {
            return result != null
                && (result.Status
                        == RewardApplicationResultStatusV1.Generated
                    || result.Status
                        == RewardApplicationResultStatusV1
                            .ExactDuplicateNoChange);
        }

        private static bool ClaimAccepted(
            RewardApplicationResultV1 result)
        {
            return result != null
                && (result.Status == RewardApplicationResultStatusV1.Applied
                    || result.Status
                        == RewardApplicationResultStatusV1
                            .AlreadyAppliedNoChange
                    || result.Status
                        == RewardApplicationResultStatusV1
                            .ClaimedPendingApplication
                    || result.Status
                        == RewardApplicationResultStatusV1
                            .ExactDuplicateNoChange);
        }

        private static string ResultCode(
            RewardApplicationResultV1 result)
        {
            return result == null
                ? "null"
                : (string.IsNullOrEmpty(result.RejectionCode)
                    ? result.Status.ToString()
                    : result.RejectionCode);
        }

        private static StrongboxOpeningRecoveryResultV1 NotRequired()
        {
            return new StrongboxOpeningRecoveryResultV1(
                StrongboxOpeningRecoveryStatusV1.NotRequired,
                string.Empty);
        }

        private static StrongboxOpeningRecoveryResultV1 Rehydrated()
        {
            return new StrongboxOpeningRecoveryResultV1(
                StrongboxOpeningRecoveryStatusV1.Rehydrated,
                string.Empty);
        }

        private static StrongboxOpeningRecoveryResultV1 Rejected(
            string rejectionCode)
        {
            return new StrongboxOpeningRecoveryResultV1(
                StrongboxOpeningRecoveryStatusV1.Rejected,
                rejectionCode);
        }
    }
}
