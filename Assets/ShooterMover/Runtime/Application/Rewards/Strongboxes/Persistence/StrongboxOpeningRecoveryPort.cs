using System;
using System.Linq;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public enum StrongboxOpeningRecoveryStatus
    {
        NotRequired = 1,
        Rehydrated = 2,
        Rejected = 3,
    }

    public sealed class StrongboxOpeningRecoveryResult
    {
        public StrongboxOpeningRecoveryResult(
            StrongboxOpeningRecoveryStatus status,
            string rejectionCode)
        {
            if (!Enum.IsDefined(
                typeof(StrongboxOpeningRecoveryStatus),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public StrongboxOpeningRecoveryStatus Status { get; }
        public string RejectionCode { get; }
        public bool Succeeded
        {
            get
            {
                return Status == StrongboxOpeningRecoveryStatus.NotRequired
                    || Status == StrongboxOpeningRecoveryStatus.Rehydrated;
            }
        }
    }

    /// <summary>
    /// Typed recovery seam for the existing BOX and RAP authorities. It replays only the
    /// immutable commit/claim commands already frozen in the BOX snapshot. It performs no
    /// generation and owns no replacement reward state.
    /// </summary>
    public interface IStrongboxOpeningRecoveryPort
    {
        StrongboxOpeningRecoveryResult Recover(
            StrongboxOpenCommand command);
    }

    public sealed class ExistingStrongboxOpeningRecoveryPort :
        IStrongboxOpeningRecoveryPort
    {
        private readonly StrongboxOpeningActions openingService;
        private readonly RewardApplicationActions rewardApplication;

        public ExistingStrongboxOpeningRecoveryPort(
            StrongboxOpeningActions openingService,
            RewardApplicationActions rewardApplication)
        {
            this.openingService = openingService
                ?? throw new ArgumentNullException(nameof(openingService));
            this.rewardApplication = rewardApplication
                ?? throw new ArgumentNullException(nameof(rewardApplication));
        }

        public StrongboxOpeningRecoveryResult Recover(
            StrongboxOpenCommand command)
        {
            if (command == null)
            {
                return Rejected("opening-recovery-command-null");
            }

            StrongboxOpeningSnapshot snapshot =
                openingService.ExportSnapshot();
            StrongboxOpeningRecordSnapshot record = snapshot.Openings
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
            if (record.Stage == StrongboxOpeningStage.Prepared
                || record.Stage == StrongboxOpeningStage.Opened
                || record.Stage == StrongboxOpeningStage.GeneratorRejected
                || record.Stage == StrongboxOpeningStage.PayloadRejected)
            {
                return NotRequired();
            }
            if (record.CommitCommand == null)
            {
                return Rejected("opening-recovery-commit-missing");
            }

            RewardApplicationResult committed =
                rewardApplication.Commit(record.CommitCommand);
            if (!CommitAccepted(committed))
            {
                return Rejected(
                    "opening-recovery-commit-rejected:"
                        + ResultCode(committed));
            }
            if (record.Stage == StrongboxOpeningStage.RewardCommitted)
            {
                return Rehydrated();
            }
            if (record.ClaimCommand == null)
            {
                return Rejected("opening-recovery-claim-missing");
            }

            RewardApplicationResult claimed =
                rewardApplication.Claim(record.ClaimCommand);
            return ClaimAccepted(claimed)
                ? Rehydrated()
                : Rejected(
                    "opening-recovery-claim-rejected:"
                        + ResultCode(claimed));
        }

        private static bool CommitAccepted(
            RewardApplicationResult result)
        {
            return result != null
                && (result.Status
                        == RewardApplicationResultStatus.Generated
                    || result.Status
                        == RewardApplicationResultStatus
                            .ExactDuplicateNoChange);
        }

        private static bool ClaimAccepted(
            RewardApplicationResult result)
        {
            return result != null
                && (result.Status == RewardApplicationResultStatus.Applied
                    || result.Status
                        == RewardApplicationResultStatus
                            .AlreadyAppliedNoChange
                    || result.Status
                        == RewardApplicationResultStatus
                            .ClaimedPendingApplication
                    || result.Status
                        == RewardApplicationResultStatus
                            .ExactDuplicateNoChange);
        }

        private static string ResultCode(
            RewardApplicationResult result)
        {
            return result == null
                ? "null"
                : (string.IsNullOrEmpty(result.RejectionCode)
                    ? result.Status.ToString()
                    : result.RejectionCode);
        }

        private static StrongboxOpeningRecoveryResult NotRequired()
        {
            return new StrongboxOpeningRecoveryResult(
                StrongboxOpeningRecoveryStatus.NotRequired,
                string.Empty);
        }

        private static StrongboxOpeningRecoveryResult Rehydrated()
        {
            return new StrongboxOpeningRecoveryResult(
                StrongboxOpeningRecoveryStatus.Rehydrated,
                string.Empty);
        }

        private static StrongboxOpeningRecoveryResult Rejected(
            string rejectionCode)
        {
            return new StrongboxOpeningRecoveryResult(
                StrongboxOpeningRecoveryStatus.Rejected,
                rejectionCode);
        }
    }
}
