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
    public sealed partial class AugmentUpgradeServiceV1
    {
        public AugmentUpgradeFactV1 Confirm(
            AugmentUpgradeConfirmationV1 confirmation)
        {
            lock (sync)
            {
                if (confirmation == null)
                {
                    return Failure(
                        AugmentUpgradeConfirmationStatusV1.InvalidRequest,
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
                AugmentUpgradeFactV1 validationFailure;
                if (!TryPrepare(confirmation, out prepared, out validationFailure))
                {
                    return validationFailure;
                }

                RewardApplicationResultV1 commitResult =
                    rewardApplication.Commit(prepared.CommitCommand);
                if (commitResult.Status != RewardApplicationResultStatusV1.Generated
                    && commitResult.Status
                        != RewardApplicationResultStatusV1.ExactDuplicateNoChange)
                {
                    return prepared.CreateFact(
                        AugmentUpgradeConfirmationStatusV1.RewardCommitRejected,
                        AugmentUpgradeConfirmationStatusV1.RewardCommitRejected,
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

        public AugmentUpgradeFactV1 Retry(AugmentUpgradeRetryCommandV1 command)
        {
            lock (sync)
            {
                if (command == null || command.ConfirmationStableId == null)
                {
                    return Failure(
                        AugmentUpgradeConfirmationStatusV1.InvalidRequest,
                        null,
                        null,
                        "upgrade-retry-invalid");
                }

                UpgradeRecord record;
                if (!records.TryGetValue(command.ConfirmationStableId, out record))
                {
                    return Failure(
                        AugmentUpgradeConfirmationStatusV1.UnknownConfirmation,
                        command.ConfirmationStableId,
                        null,
                        "upgrade-confirmation-unknown");
                }

                if (record.Fact != null
                    && record.Fact.OriginalStatus
                        == AugmentUpgradeConfirmationStatusV1.Applied)
                {
                    return Replay(record);
                }

                return Execute(record);
            }
        }

        public bool TryGetFact(
            StableId confirmationStableId,
            out AugmentUpgradeFactV1 fact)
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
