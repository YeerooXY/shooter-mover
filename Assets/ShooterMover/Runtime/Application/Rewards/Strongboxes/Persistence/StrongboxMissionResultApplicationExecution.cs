using System;
using System.Collections.Generic;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public sealed partial class StrongboxMissionResultApplicationFlow
    {
        private StrongboxMissionResultApplicationResult ExecutePlan(
            StrongboxMissionResultApplicationCommand command,
            TransferPlan plan)
        {
            bool mutated = false;
            try
            {
                for (int index = 0; index < plan.Transfers.Count; index++)
                {
                    TransferItem item = plan.Transfers[index];
                    if (item.TerminallyOpenedAlready)
                    {
                        continue;
                    }
                    if (!item.HoldingAlreadyPresent)
                    {
                        PlayerHoldingsMutationResult add =
                            plan.AuthorityPort.AddStrongbox(
                                PlayerHoldingsCommand.AddStrongbox(
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
                                    plan.AuthorityPort
                                        .HoldingsAuthorityStableId,
                                    item.Collection.DefinitionStableId,
                                    item.Collection.InstanceStableId,
                                    HoldingProvenance.Create(
                                        item.Collection.GrantStableId,
                                        item.Collection.SourceStableId),
                                    plan.AuthorityPort.HoldingsSequence));
                        if (add == null
                            || (add.Status
                                    != PlayerHoldingsMutationStatus.Applied
                                && add.Status
                                    != PlayerHoldingsMutationStatus
                                        .ExactDuplicateNoChange))
                        {
                            return CompensateAndReject(
                                command,
                                plan,
                                "box-transfer-holdings-rejected:"
                                    + (add == null
                                        ? "null"
                                        : add.RejectionCode));
                        }
                        mutated |= add.Status
                            == PlayerHoldingsMutationStatus.Applied;
                    }

                    if (!item.ContextAlreadyPresent)
                    {
                        StrongboxRegistrationResult registration =
                            plan.AuthorityPort.RegisterStrongbox(item.Context);
                        if (registration == null
                            || (registration.Status
                                    != StrongboxRegistrationStatus.Registered
                                && registration.Status
                                    != StrongboxRegistrationStatus
                                        .ExactDuplicateNoChange))
                        {
                            return CompensateAndReject(
                                command,
                                plan,
                                "box-transfer-registration-rejected:"
                                    + (registration == null
                                        ? "null"
                                        : registration.RejectionCode));
                        }
                        mutated |= registration.Status
                            == StrongboxRegistrationStatus.Registered;
                    }
                }

                PlayerHoldingsSnapshot afterHoldings =
                    plan.AuthorityPort.ExportHoldings();
                StrongboxOpeningSnapshot afterStrongboxes =
                    plan.AuthorityPort.ExportStrongboxes();
                if (afterHoldings == null || afterStrongboxes == null)
                {
                    return CompensateAndReject(
                        command,
                        plan,
                        "box-transfer-post-mutation-snapshot-null");
                }
                IReadOnlyList<SaveComponentSnapshot> expectedComponents =
                    PlayerAccountRestoreFlow.ExportComponents(
                        plan.Graph.SaveAdapters);

                StableId saveOperation = Strongbox.DeriveId(
                    "boxpersistsave",
                    command.OperationStableId.ToString(),
                    command.TerminalResult.Fingerprint,
                    afterHoldings.Fingerprint,
                    afterStrongboxes.Fingerprint);
                CharacterSetupResult persisted =
                    composition.PersistActive(saveOperation);
                if (persisted == null || !persisted.Succeeded)
                {
                    return CompensateAndReject(
                        command,
                        plan,
                        "box-transfer-durable-save-rejected:"
                            + (persisted == null
                                ? "null"
                                : persisted.Diagnostic));
                }

                PlayerAccountSnapshot afterAccount = persisted.Account;
                CharacterInstanceSnapshot afterCharacter =
                    persisted.Character;
                if (afterAccount == null || afterCharacter == null
                    || afterCharacter.CharacterInstanceStableId
                        != command.SelectedCharacterStableId
                    || !ComponentsMatch(
                        afterCharacter,
                        expectedComponents))
                {
                    return CompensateAndReject(
                        command,
                        plan,
                        "box-transfer-durable-verification-mismatch");
                }

                return new StrongboxMissionResultApplicationResult(
                    mutated
                        ? StrongboxMissionResultApplicationStatus.Applied
                        : StrongboxMissionResultApplicationStatus
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
            catch (Exception exception)
            {
                return CompensateAndReject(
                    command,
                    plan,
                    "box-transfer-transaction-exception-"
                        + exception.GetType().Name.ToLowerInvariant());
            }
        }
    }
}
