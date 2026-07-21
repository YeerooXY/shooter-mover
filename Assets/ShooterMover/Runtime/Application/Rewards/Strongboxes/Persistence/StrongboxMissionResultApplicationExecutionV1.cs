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
    public sealed partial class StrongboxMissionResultApplicationCoordinatorV1
    {
        private StrongboxMissionResultApplicationResultV1 ExecutePlan(
            StrongboxMissionResultApplicationCommandV1 command,
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
                        PlayerHoldingsMutationResultV1 add =
                            plan.AuthorityPort.AddStrongbox(
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
                                    plan.AuthorityPort
                                        .HoldingsAuthorityStableId,
                                    item.Collection.DefinitionStableId,
                                    item.Collection.InstanceStableId,
                                    HoldingProvenanceV1.Create(
                                        item.Collection.GrantStableId,
                                        item.Collection.SourceStableId),
                                    plan.AuthorityPort.HoldingsSequence));
                        if (add == null
                            || (add.Status
                                    != PlayerHoldingsMutationStatusV1.Applied
                                && add.Status
                                    != PlayerHoldingsMutationStatusV1
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
                            == PlayerHoldingsMutationStatusV1.Applied;
                    }

                    if (!item.ContextAlreadyPresent)
                    {
                        StrongboxRegistrationResultV1 registration =
                            plan.AuthorityPort.RegisterStrongbox(item.Context);
                        if (registration == null
                            || (registration.Status
                                    != StrongboxRegistrationStatusV1.Registered
                                && registration.Status
                                    != StrongboxRegistrationStatusV1
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
                            == StrongboxRegistrationStatusV1.Registered;
                    }
                }

                PlayerHoldingsSnapshotV1 afterHoldings =
                    plan.AuthorityPort.ExportHoldings();
                StrongboxOpeningSnapshotV1 afterStrongboxes =
                    plan.AuthorityPort.ExportStrongboxes();
                if (afterHoldings == null || afterStrongboxes == null)
                {
                    return CompensateAndReject(
                        command,
                        plan,
                        "box-transfer-post-mutation-snapshot-null");
                }
                IReadOnlyList<SaveComponentSnapshotV1> expectedComponents =
                    PlayerAccountRestoreCoordinatorV1.ExportComponents(
                        plan.Graph.SaveAdapters);

                StableId saveOperation = StrongboxCanonicalV1.DeriveId(
                    "boxpersistsave",
                    command.OperationStableId.ToString(),
                    command.TerminalResult.Fingerprint,
                    afterHoldings.Fingerprint,
                    afterStrongboxes.Fingerprint);
                CharacterCompositionResultV1 persisted =
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

                PlayerAccountSnapshotV1 afterAccount = persisted.Account;
                CharacterInstanceSnapshotV1 afterCharacter =
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
