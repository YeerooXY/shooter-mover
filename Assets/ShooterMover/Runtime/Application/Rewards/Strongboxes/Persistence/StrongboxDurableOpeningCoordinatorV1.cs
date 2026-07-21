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
        private readonly CharacterCompositionCoordinatorV1 composition;

        public StrongboxDurableOpeningCoordinatorV1(
            CharacterCompositionCoordinatorV1 composition)
        {
            this.composition = composition
                ?? throw new ArgumentNullException(nameof(composition));
        }

        public StrongboxOpeningResultRuntimeV1 OpenAndPersist(
            MissionRunStrongboxResultV1 selectedStrongbox,
            StrongboxOpeningServiceV1 openingService,
            StrongboxOpenCommandV1 command)
        {
            var graph = composition.ActiveRuntime
                as ProductionCharacterRuntimeGraphV1;
            PlayerAccountSnapshotV1 beforeAccount = composition.Account;
            if (selectedStrongbox == null || !selectedStrongbox.IsUnopened)
            {
                return Rejected(command, "durable-opening-selection-invalid");
            }
            if (graph == null || graph.IsDisposed || beforeAccount == null)
            {
                return Rejected(command, "durable-opening-character-unavailable");
            }
            if (!ReferenceEquals(openingService, graph.StrongboxAuthority))
            {
                return Rejected(command, "durable-opening-stale-box-authority");
            }
            if (command == null
                || command.StrongboxInstanceStableId
                    != selectedStrongbox.InstanceStableId
                || command.ClaimantStableId
                    != graph.Character.CharacterInstanceStableId
                || command.HoldingsAuthorityStableId
                    != graph.LoadoutRuntime.Holdings.AuthorityStableId
                || command.ScrapAuthorityStableId
                    != graph.ScrapWallet.AuthorityStableId
                || command.MoneyAuthorityStableId
                    != MoneyWalletIdsV1.AuthorityStableId)
            {
                return Rejected(command, "durable-opening-command-context-mismatch");
            }

            string recoveryError;
            if (!TryRehydrateRewardApplication(
                openingService,
                command,
                out recoveryError))
            {
                return Rejected(command, recoveryError);
            }

            string validation = ValidateExactUnopenedState(
                graph,
                selectedStrongbox,
                command);
            if (!string.IsNullOrEmpty(validation))
            {
                return Rejected(command, validation);
            }

            CharacterInstanceSnapshotV1 beforeCharacter = graph.Character;
            string beforeComponentFingerprint = ExportComponentFingerprint(graph);
            StrongboxOpeningResultRuntimeV1 result;
            try
            {
                result = openingService.Open(command);
            }
            catch (Exception exception)
            {
                string restore = Restore(
                    beforeAccount,
                    graph,
                    beforeCharacter,
                    beforeComponentFingerprint);
                return Rejected(
                    command,
                    "durable-opening-authority-exception-"
                        + exception.GetType().Name.ToLowerInvariant()
                        + AppendRestore(restore));
            }

            bool retryablePending = result != null
                && (result.Status
                        == StrongboxOpeningRuntimeStatusV1
                            .ClaimedPendingApplication
                    || result.Status
                        == StrongboxOpeningRuntimeStatusV1.ConsumePending);
            if (result == null || (!result.Succeeded && !retryablePending))
            {
                string restore = Restore(
                    beforeAccount,
                    graph,
                    beforeCharacter,
                    beforeComponentFingerprint);
                if (!string.IsNullOrEmpty(restore))
                {
                    return SnapshotRejected(
                        command,
                        result,
                        "durable-opening-nonterminal-rollback-failed:"
                            + restore);
                }
                return result ?? Rejected(command, "durable-opening-result-null");
            }

            string currentFingerprint = ExportComponentFingerprint(graph);
            StableId saveOperation = StrongboxCanonicalV1.DeriveId(
                "boxterminalsave",
                command.OpeningStableId.ToString(),
                command.RunStableId.ToString(),
                command.StrongboxInstanceStableId.ToString(),
                result.GeneratedOutcome == null
                    ? "none"
                    : result.GeneratedOutcome.Fingerprint,
                currentFingerprint);
            CharacterCompositionResultV1 persisted;
            try
            {
                persisted = composition.PersistActive(saveOperation);
            }
            catch (Exception exception)
            {
                string restore = Restore(
                    beforeAccount,
                    graph,
                    beforeCharacter,
                    beforeComponentFingerprint);
                return SnapshotRejected(
                    command,
                    result,
                    "durable-opening-save-exception-"
                        + exception.GetType().Name.ToLowerInvariant()
                        + AppendRestore(restore));
            }

            if (persisted == null || !persisted.Succeeded)
            {
                string restore = Restore(
                    beforeAccount,
                    graph,
                    beforeCharacter,
                    beforeComponentFingerprint);
                return SnapshotRejected(
                    command,
                    result,
                    "durable-opening-save-rejected:"
                        + (persisted == null ? "null" : persisted.Diagnostic)
                        + AppendRestore(restore));
            }

            PlayerAccountSnapshotV1 durable = composition.Account;
            CharacterInstanceSnapshotV1 durableCharacter = durable == null
                ? null
                : durable.CharacterAt(composition.ActiveSlotIndex);
            if (durableCharacter == null
                || durableCharacter.CharacterInstanceStableId
                    != graph.Character.CharacterInstanceStableId
                || !ComponentsMatchGraph(durableCharacter, graph))
            {
                throw new InvalidOperationException(
                    "Atomic persistence reported success without publishing the complete selected-character component graph.");
            }
            return result;
        }
    }
}
