using System;
using System.Collections.Generic;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Equipment.Upgrades;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Equipment.Upgrades
{
    public sealed partial class AugmentUpgradeActions
    {
        public AugmentUpgradeFact Confirm(
            AugmentUpgradeConfirmation confirmation)
        {
            lock (sync)
            {
                if (confirmation == null)
                {
                    return Failure(
                        AugmentUpgradeConfirmationStatus.InvalidRequest,
                        null,
                        null,
                        "upgrade-confirmation-null");
                }

                UpgradeRecord existing;
                if (records.TryGetValue(
                    confirmation.ConfirmationStableId,
                    out existing))
                {
                    if (string.Equals(
                        existing.Confirmation.Fingerprint,
                        confirmation.Fingerprint,
                        StringComparison.Ordinal))
                    {
                        return Replay(existing);
                    }

                    return Conflict(existing, confirmation);
                }

                PreparedUpgrade prepared;
                AugmentUpgradeFact validationFailure;
                if (!TryPrepare(confirmation, out prepared, out validationFailure))
                {
                    return validationFailure;
                }

                RewardApplicationResult commitResult =
                    rewardApplication.Commit(prepared.CommitCommand);
                if (commitResult.Status != RewardApplicationResultStatus.Generated
                    && commitResult.Status
                        != RewardApplicationResultStatus.ExactDuplicateNoChange)
                {
                    return prepared.CreateFact(
                        AugmentUpgradeConfirmationStatus.RewardCommitRejected,
                        AugmentUpgradeConfirmationStatus.RewardCommitRejected,
                        moneyWallet.Sequence,
                        holdings.Sequence,
                        commitResult.RejectionCode
                            ?? "upgrade-reward-commit-rejected");
                }

                var record = new UpgradeRecord(prepared);
                records.Add(confirmation.ConfirmationStableId, record);
                return Execute(record);
            }
        }

        public AugmentUpgradeFact Retry(AugmentUpgradeRetryCommand command)
        {
            lock (sync)
            {
                if (command == null || command.ConfirmationStableId == null)
                {
                    return Failure(
                        AugmentUpgradeConfirmationStatus.InvalidRequest,
                        null,
                        null,
                        "upgrade-retry-invalid");
                }

                UpgradeRecord record;
                if (!records.TryGetValue(command.ConfirmationStableId, out record))
                {
                    return Failure(
                        AugmentUpgradeConfirmationStatus.UnknownConfirmation,
                        command.ConfirmationStableId,
                        null,
                        "upgrade-confirmation-unknown");
                }

                if (record.Fact != null
                    && record.Fact.OriginalStatus
                        == AugmentUpgradeConfirmationStatus.Applied)
                {
                    return Replay(record);
                }

                return Execute(record);
            }
        }

        public bool TryGetFact(
            StableId confirmationStableId,
            out AugmentUpgradeFact fact)
        {
            lock (sync)
            {
                UpgradeRecord record;
                if (confirmationStableId != null
                    && records.TryGetValue(confirmationStableId, out record))
                {
                    fact = record.Fact;
                    return fact != null;
                }

                fact = null;
                return false;
            }
        }
    }
}
