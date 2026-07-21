using System;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public sealed partial class StrongboxMissionResultApplicationCoordinatorV1
    {
        private StrongboxMissionResultApplicationResultV1 ExecutePlan(
            StrongboxMissionResultApplicationCommandV1 command,
            TransferPlan plan)
        {
            bool mutated = false;
            for (int index = 0; index < plan.Transfers.Count; index++)
            {
                TransferItem item = plan.Transfers[index];
                if (item.TerminallyOpenedAlready)
                {
                    continue;
                }
                if (!item.HoldingAlreadyPresent)
                {
                    PlayerHoldingsMutationResultV1 add = plan.Holdings.Apply(
                        PlayerHoldingsCommandV1.AddStrongbox(
                            DerivedId(
                                "boxpersisttx",
                                command,
                                item.Collection,
                                index),
                            DerivedId(
                                "boxpersistop",
                                command,
                                item.Collection,
                                index),
                            plan.Holdings.AuthorityStableId,
                            item.Collection.DefinitionStableId,
                            item.Collection.InstanceStableId,
                            HoldingProvenanceV1.Create(
                                item.Collection.GrantStableId,
                                item.Collection.SourceStableId),
                            plan.Holdings.Sequence));
                    if (add == null
                        || (add.Status
                                != PlayerHoldingsMutationStatusV1.Applied
                            && add.Status
                                != PlayerHoldingsMutationStatusV1
                                    .ExactDuplicateNoChange))
                    {
                        return CompensateAndReject(
                            command,
                            plan.Graph,
                            plan.BeforeHoldings,
                            plan.BeforeStrongboxes,
                            "box-transfer-holdings-rejected:"
                                + (add == null
                                    ? "null"
                                    : add.RejectionCode));
                    }
                    mutated |= add.Status
                        == PlayerHoldingsMutationStatusV1.Applied;
                }

                if (!item.ContextAlreadyPresent)
                {
                    StrongboxRegistrationResultV1 registration =
                        plan.Strongboxes.RegisterInstance(item.Context);
                    if (registration == null
                        || (registration.Status
                                != StrongboxRegistrationStatusV1.Registered
                            && registration.Status
                                != StrongboxRegistrationStatusV1
                                    .ExactDuplicateNoChange))
                    {
                        return CompensateAndReject(
                            command,
                            plan.Graph,
                            plan.BeforeHoldings,
                            plan.BeforeStrongboxes,
                            "box-transfer-registration-rejected:"
                                + (registration == null
                                    ? "null"
                                    : registration.RejectionCode));
                    }
                    mutated |= registration.Status
                        == StrongboxRegistrationStatusV1.Registered;
                }
            }

            StableId saveOperation = StrongboxCanonicalV1.DeriveId(
                "boxpersistsave",
                command.OperationStableId.ToString(),
                command.TerminalResult.Fingerprint,
                plan.Holdings.ExportSnapshot().Fingerprint,
                plan.Strongboxes.ExportSnapshot().Fingerprint);
            CharacterCompositionResultV1 persisted =
                composition.PersistActive(saveOperation);
            if (persisted == null || !persisted.Succeeded)
            {
                return CompensateAndReject(
                    command,
                    plan.Graph,
                    plan.BeforeHoldings,
                    plan.BeforeStrongboxes,
                    "box-transfer-durable-save-rejected:"
                        + (persisted == null
                            ? "null"
                            : persisted.Diagnostic));
            }

            PlayerHoldingsSnapshotV1 afterHoldings =
                plan.Holdings.ExportSnapshot();
            StrongboxOpeningSnapshotV1 afterStrongboxes =
                plan.Strongboxes.ExportSnapshot();
            PlayerAccountSnapshotV1 afterAccount = composition.Account;
            CharacterInstanceSnapshotV1 afterCharacter = afterAccount == null
                ? null
                : afterAccount.CharacterAt(composition.ActiveSlotIndex);
            if (afterAccount == null || afterCharacter == null
                || afterCharacter.CharacterInstanceStableId
                    != command.SelectedCharacterStableId)
            {
                throw new InvalidOperationException(
                    "A successful atomic account save did not publish the selected character.");
            }

            return new StrongboxMissionResultApplicationResultV1(
                mutated
                    ? StrongboxMissionResultApplicationStatusV1.Applied
                    : StrongboxMissionResultApplicationStatusV1
                        .AcceptedNoChange,
                command.OperationStableId,
                command.Fingerprint,
                command.TerminalResult.Fingerprint,
                plan.Transfers.Count,
                afterHoldings.Fingerprint,
                afterStrongboxes.Fingerprint,
                afterAccount.Fingerprint,
                string.Empty);
        }
    }
}
