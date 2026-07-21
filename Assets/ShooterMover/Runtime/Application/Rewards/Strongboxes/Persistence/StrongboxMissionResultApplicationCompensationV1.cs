using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Runs.Session;
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
        private StrongboxMissionResultApplicationResultV1 CompensateAndReject(
            StrongboxMissionResultApplicationCommandV1 command,
            ProductionCharacterRuntimeGraphV1 graph,
            PlayerHoldingsSnapshotV1 holdings,
            StrongboxOpeningSnapshotV1 strongboxes,
            string rejection)
        {
            string compensation = RestoreExact(graph, holdings, strongboxes);
            return Reject(
                command,
                string.IsNullOrEmpty(compensation)
                    ? rejection
                    : rejection + ";compensation=" + compensation);
        }

        private static string RestoreExact(
            ProductionCharacterRuntimeGraphV1 graph,
            PlayerHoldingsSnapshotV1 holdings,
            StrongboxOpeningSnapshotV1 strongboxes)
        {
            var errors = new List<string>();
            StrongboxOpeningImportResultV1 boxRestore =
                graph.StrongboxAuthority.ImportSnapshot(strongboxes);
            if (boxRestore == null || !boxRestore.Succeeded)
            {
                errors.Add("box:"
                    + (boxRestore == null ? "null" : boxRestore.RejectionCode));
            }
            PlayerHoldingsImportResultV1 holdingsRestore =
                graph.LoadoutRuntime.Holdings.ImportSnapshot(holdings);
            if (holdingsRestore == null || !holdingsRestore.Succeeded)
            {
                errors.Add("holdings:"
                    + (holdingsRestore == null
                        ? "null"
                        : holdingsRestore.RejectionCode));
            }
            return string.Join(",", errors);
        }

        private StrongboxMissionResultApplicationResultV1 Reject(
            StrongboxMissionResultApplicationCommandV1 command,
            string rejection)
        {
            return Reject(
                command == null ? null : command.OperationStableId,
                command == null ? string.Empty : command.Fingerprint,
                command == null ? string.Empty : command.TerminalResult.Fingerprint,
                rejection);
        }

        private StrongboxMissionResultApplicationResultV1 Reject(
            StableId operation,
            string commandFingerprint,
            string resultFingerprint,
            string rejection)
        {
            PlayerAccountSnapshotV1 account = composition.Account;
            return new StrongboxMissionResultApplicationResultV1(
                StrongboxMissionResultApplicationStatusV1.Rejected,
                operation,
                commandFingerprint,
                resultFingerprint,
                0,
                string.Empty,
                string.Empty,
                account == null ? string.Empty : account.Fingerprint,
                rejection);
        }

        private static StableId DerivedId(
            string scope,
            StrongboxMissionResultApplicationCommandV1 command,
            MissionRunStrongboxCollectionV1 collection,
            int index)
        {
            return StrongboxCanonicalV1.DeriveId(
                scope,
                command.OperationStableId.ToString(),
                command.RunStableId.ToString(),
                command.TerminalResult.Fingerprint,
                collection.Fingerprint,
                index.ToString(CultureInfo.InvariantCulture));
        }

        private sealed class TransferItem
        {
            public TransferItem(
                MissionRunStrongboxCollectionV1 collection,
                StrongboxInstanceContextV1 context,
                bool holdingAlreadyPresent,
                bool contextAlreadyPresent,
                bool terminallyOpenedAlready)
            {
                Collection = collection;
                Context = context;
                HoldingAlreadyPresent = holdingAlreadyPresent;
                ContextAlreadyPresent = contextAlreadyPresent;
                TerminallyOpenedAlready = terminallyOpenedAlready;
            }
            public MissionRunStrongboxCollectionV1 Collection { get; }
            public StrongboxInstanceContextV1 Context { get; }
            public bool HoldingAlreadyPresent { get; }
            public bool ContextAlreadyPresent { get; }
            public bool TerminallyOpenedAlready { get; }
        }
    }
}
