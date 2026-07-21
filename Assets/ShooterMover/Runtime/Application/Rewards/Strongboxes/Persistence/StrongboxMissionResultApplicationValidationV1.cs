using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public sealed partial class StrongboxMissionResultApplicationCoordinatorV1
    {
        private bool TryCreatePlan(
            StrongboxMissionResultApplicationCommandV1 command,
            out TransferPlan plan,
            out string rejection)
        {
            plan = null;
            rejection = string.Empty;
            PlayerAccountSnapshotV1 account = composition.Account;
            var graph = composition.ActiveRuntime
                as ProductionCharacterRuntimeGraphV1;
            if (account == null || graph == null || graph.IsDisposed)
            {
                rejection = "box-transfer-selected-character-unavailable";
                return false;
            }
            CharacterInstanceSnapshotV1 character = graph.Character;
            if (character == null
                || character.CharacterInstanceStableId
                    != command.SelectedCharacterStableId
                || command.TerminalResult.RoutePayload
                    .SelectedCharacterStableId
                    != command.SelectedCharacterStableId)
            {
                rejection = "box-transfer-selected-character-mismatch";
                return false;
            }
            if (command.TerminalResult.RunStableId != command.RunStableId)
            {
                rejection = "box-transfer-run-result-mismatch";
                return false;
            }
            if (!command.TerminalResult.RoutePayload.HasValidFingerprint())
            {
                rejection = "box-transfer-terminal-route-invalid";
                return false;
            }
            if (account.Revision != command.ExpectedAccountRevision)
            {
                rejection = "box-transfer-account-revision-stale";
                return false;
            }
            if (character.Revision != command.ExpectedCharacterRevision)
            {
                rejection = "box-transfer-character-revision-stale";
                return false;
            }
            if (!string.Equals(
                character.Fingerprint,
                command.ExpectedCharacterFingerprint,
                StringComparison.Ordinal))
            {
                rejection = "box-transfer-character-fingerprint-stale";
                return false;
            }
            if (!string.Equals(
                    command.SourceHoldings.Fingerprint,
                    command.TerminalResult.HoldingsFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    command.SourceStrongboxes.Fingerprint,
                    command.TerminalResult.StrongboxOpeningFingerprint,
                    StringComparison.Ordinal))
            {
                rejection =
                    "box-transfer-terminal-source-fingerprint-stale";
                return false;
            }

            PlayerHoldingsService targetHoldings =
                graph.LoadoutRuntime.Holdings;
            StrongboxOpeningServiceV1 targetStrongboxes =
                graph.StrongboxAuthority;
            if (targetHoldings == null || targetStrongboxes == null)
            {
                rejection = "box-transfer-target-authority-missing";
                return false;
            }

            Dictionary<StableId, UniqueHoldingSnapshotV1> sourceHeld =
                command.SourceHoldings.UniqueHoldings
                    .Where(item => item != null
                        && item.RewardKind
                            == RewardGrantKindV1.Strongbox)
                    .ToDictionary(
                        item => item.InstanceStableId,
                        item => item);
            Dictionary<StableId, StrongboxInstanceContextV1> sourceContexts =
                command.SourceStrongboxes.Contexts.ToDictionary(
                    item => item.InstanceStableId,
                    item => item);
            var sourceOpenings = command.SourceStrongboxes.Openings
                .ToDictionary(
                    item => item.Command.StrongboxInstanceStableId,
                    item => item);

            PlayerHoldingsSnapshotV1 beforeHoldings =
                targetHoldings.ExportSnapshot();
            StrongboxOpeningSnapshotV1 beforeStrongboxes =
                targetStrongboxes.ExportSnapshot();
            var targetHeld = beforeHoldings.UniqueHoldings
                .Where(item => item != null
                    && item.RewardKind == RewardGrantKindV1.Strongbox)
                .ToDictionary(
                    item => item.InstanceStableId,
                    item => item);
            var targetContexts = beforeStrongboxes.Contexts.ToDictionary(
                item => item.InstanceStableId,
                item => item);
            var targetOpenings = beforeStrongboxes.Openings.ToDictionary(
                item => item.Command.StrongboxInstanceStableId,
                item => item);

            var expectedIds = new HashSet<StableId>(
                command.TerminalResult.UnopenedStrongboxes.Select(
                    item => item.InstanceStableId));
            if (!expectedIds.SetEquals(sourceHeld.Keys))
            {
                rejection = "box-transfer-source-unopened-set-mismatch";
                return false;
            }

            var transfers = new List<TransferItem>();
            var seen = new HashSet<StableId>();
            for (int index = 0;
                index < command.TerminalResult.UnopenedStrongboxes.Count;
                index++)
            {
                MissionRunStrongboxResultV1 resultBox =
                    command.TerminalResult.UnopenedStrongboxes[index];
                if (resultBox == null || !resultBox.IsUnopened
                    || !seen.Add(resultBox.InstanceStableId))
                {
                    rejection =
                        "box-transfer-unopened-collection-invalid";
                    return false;
                }
                if (!TryValidateTransferItem(
                    resultBox.Collection,
                    sourceHeld,
                    sourceContexts,
                    sourceOpenings,
                    targetHeld,
                    targetContexts,
                    targetOpenings,
                    transfers,
                    out rejection))
                {
                    return false;
                }
            }

            plan = new TransferPlan(
                graph,
                targetHoldings,
                targetStrongboxes,
                beforeHoldings,
                beforeStrongboxes,
                transfers);
            return true;
        }

    }
}
