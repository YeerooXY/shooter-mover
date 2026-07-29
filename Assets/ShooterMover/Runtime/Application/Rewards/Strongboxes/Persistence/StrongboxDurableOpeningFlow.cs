using System;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public sealed partial class StrongboxDurableOpeningFlow :
        IStrongboxDurableOpeningExecutor
    {
        private readonly CharacterSetupFlow composition;

        public StrongboxDurableOpeningFlow(
            CharacterSetupFlow composition)
        {
            this.composition = composition
                ?? throw new ArgumentNullException(nameof(composition));
        }

        public StrongboxOpeningResultLive OpenAndPersist(
            MissionRunStrongboxResult selectedStrongbox,
            ShooterMover.Application.Rewards.Strongboxes
                .StrongboxOpeningActions openingService,
            StrongboxOpenCommand command)
        {
            CharacterLiveGraph graph = null;
            PlayerAccountSnapshot beforeAccount = null;
            CharacterInstanceSnapshot beforeCharacter = null;
            string beforeComponentFingerprint = null;
            StrongboxOpeningResultLive result = null;
            try
            {
                graph = composition.ActiveRuntime
                    as CharacterLiveGraph;
                beforeAccount = composition.Account;
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
                        != MoneyWalletIds.AuthorityStableId)
                {
                    return Rejected(command, "durable-opening-command-context-mismatch");
                }

                string validation = ValidateExactUnopenedState(
                    graph,
                    selectedStrongbox,
                    command);
                if (!string.IsNullOrEmpty(validation))
                {
                    return Rejected(command, validation);
                }

                beforeCharacter = graph.Character;
                beforeComponentFingerprint = ExportComponentFingerprint(graph);
                string recoveryError;
                if (!TryRehydrateRewardApplication(
                    graph.StrongboxRecovery,
                    command,
                    out recoveryError))
                {
                    string restore = Restore(
                        beforeAccount,
                        graph,
                        beforeCharacter,
                        beforeComponentFingerprint);
                    return SnapshotRejected(
                        command,
                        null,
                        recoveryError + AppendRestore(restore));
                }

                result = openingService.Open(command);
                bool retryablePending = result != null
                    && (result.Status
                            == StrongboxOpeningLiveStatus
                                .ClaimedPendingApplication
                        || result.Status
                            == StrongboxOpeningLiveStatus
                                .ConsumePending);
                if (result == null
                    || (!result.Succeeded && !retryablePending))
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
                    return result
                        ?? Rejected(command, "durable-opening-result-null");
                }

                string currentFingerprint =
                    ExportComponentFingerprint(graph);
                StableId saveOperation = Strongbox.DeriveId(
                    "boxterminalsave",
                    command.OpeningStableId.ToString(),
                    command.RunStableId.ToString(),
                    command.StrongboxInstanceStableId.ToString(),
                    result.GeneratedOutcome == null
                        ? "none"
                        : result.GeneratedOutcome.Fingerprint,
                    currentFingerprint);
                CharacterSetupResult persisted =
                    composition.PersistActive(saveOperation);
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
                            + (persisted == null
                                ? "null"
                                : persisted.Diagnostic)
                            + AppendRestore(restore));
                }

                PlayerAccountSnapshot durable = persisted.Account;
                CharacterInstanceSnapshot durableCharacter =
                    persisted.Character;
                if (durable == null || durableCharacter == null
                    || durableCharacter.CharacterInstanceStableId
                        != graph.Character.CharacterInstanceStableId
                    || !ComponentsMatchGraph(durableCharacter, graph))
                {
                    string restore = Restore(
                        beforeAccount,
                        graph,
                        beforeCharacter,
                        beforeComponentFingerprint);
                    return SnapshotRejected(
                        command,
                        result,
                        "durable-opening-durable-verification-mismatch"
                            + AppendRestore(restore));
                }
                return result;
            }
            catch (Exception exception)
            {
                string restore = RestoreIfCaptured(
                    beforeAccount,
                    graph,
                    beforeCharacter,
                    beforeComponentFingerprint);
                return SnapshotRejected(
                    command,
                    result,
                    "durable-opening-transaction-exception-"
                        + exception.GetType().Name.ToLowerInvariant()
                        + AppendRestore(restore));
            }
        }
    }
}
